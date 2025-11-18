// Copyright (c) 2025 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using Horizon;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using UserModel;
using Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Comprehensive schedule data capture for observation and analysis
    /// Builds off ObservationGenerator pattern, adds detailed tracking:
    /// - Potential schedules created (from CanAddTasks)
    /// - Subsystem evaluation order and failure tracking
    /// - State data for all passing schedules
    /// - Crop tracking (which schedules survive)
    /// - Content-based hashing for parallelization traceability
    /// 
    /// NOT A TEST - No [Test] attributes
    /// Run manually to generate comprehensive observation data
    /// </summary>
    public class ScheduleDataCapture : SchedulerUnitTest
    {
        #region Properties & Fields
        
        protected override string SimInputFile { get; set; }
        protected override string TaskInputFile { get; set; }
        protected override string ModelInputFile { get; set; }
        
        // Use inherited properties from SchedulerUnitTest:
        // - CurrentTestDir: Auto-detects class source directory (test/HSFSchedulerUnitTest/MethodUnitTests/ScheduleDataCapture/)
        // - ProjectTestDir: Uses Utilities.DevEnvironment.GetTestDirectory()
        
        private string OutputDir => Path.Combine(CurrentTestDir, "Output");
        private string CurrentRunDir = "";
        public string CurrentRunDirPublic => CurrentRunDir;  // Public accessor for runner
        
        // Public accessors for runner
        public Horizon.Program Program => program;
        public SystemClass TestSimSystem => _testSimSystem;
        public Stack<MissionElements.Task> TestSystemTasks => _testSystemTasks;
        public SystemState TestInitialSysState => _testInitialSysState;
        
        // Output path: test/HSFSchedulerUnitTest/MethodUnitTests/ScheduleDataCapture/Output/Run_N/scenarioName_full_trace.json
        
        #endregion
        
        #region Data Structures
        
        /// <summary>
        /// Evaluation context tracking subsystem order and failures
        /// </summary>
        public class EvaluationContext
        {
            public List<string> EvaluationOrder { get; set; } = new();
            public string? FirstFailingSubsystem { get; set; } = null;
            public string? FailureReason { get; set; } = null; // "CanPerform", "Constraint"
        }
        
        /// <summary>
        /// Potential schedule created from CanAddTasks/TimeDeconfliction
        /// </summary>
        public class PotentialScheduleInfo
        {
            public string ScheduleID { get; set; } = "";
            public string ContentHash { get; set; } = "";  // Deterministic hash for parallel traceability
            public string ParentScheduleID { get; set; } = "";
            public double CreationTime { get; set; }
            public List<TaskAccessInfo> TasksAdded { get; set; } = new();
        }
        
        public class TaskAccessInfo
        {
            public string AssetName { get; set; } = "";
            public string TaskName { get; set; } = "";
            public double TaskStart { get; set; }
            public double TaskEnd { get; set; }
        }
        
        /// <summary>
        /// Schedule that passed Checker
        /// </summary>
        public class PassingScheduleInfo
        {
            public string ScheduleID { get; set; } = "";
            public string ContentHash { get; set; } = "";
            public EvaluationContext EvaluationContext { get; set; } = new();
            public Dictionary<string, object> AllStateData { get; set; } = new();  // Serialized AllStates.Events
            public List<EventSnapshot> Events { get; set; } = new();
            public double ScheduleValue { get; set; }
        }
        
        /// <summary>
        /// Schedule that failed Checker
        /// </summary>
        public class FailingScheduleInfo
        {
            public string ScheduleID { get; set; } = "";
            public string ContentHash { get; set; } = "";
            public EvaluationContext EvaluationContext { get; set; } = new();
            public Dictionary<string, object> StateDataUpToFailure { get; set; } = new();
        }
        
        /// <summary>
        /// Event snapshot with all state data
        /// </summary>
        public class EventSnapshot
        {
            public double EventStart { get; set; }
            public double EventEnd { get; set; }
            public Dictionary<string, Dictionary<string, TaskAccessInfo>> AssetTasks { get; set; } = new();
            public Dictionary<string, Dictionary<string, List<TimeValuePair>>> StateVariables { get; set; } = new();
        }
        
        public class TimeValuePair
        {
            public double Time { get; set; }
            public object? Value { get; set; }
        }
        
        /// <summary>
        /// Iteration data capture
        /// </summary>
        public class IterationCapture
        {
            public int Iteration { get; set; }
            public double CurrentTime { get; set; }
            
            // Step a: Potential schedules created
            public List<PotentialScheduleInfo> PotentialSchedulesCreated { get; set; } = new();
            
            // Step b: Schedules passing Checker
            public List<PassingScheduleInfo> SchedulesPassingChecker { get; set; } = new();
            
            // Step c: Schedules failing Checker
            public List<FailingScheduleInfo> SchedulesFailingChecker { get; set; } = new();
            
            // Step d: Schedules surviving crop
            public List<string> ScheduleIDsSurvivingCrop { get; set; } = new();  // ContentHash list
            public int CountBeforeCrop { get; set; }
            public int CountAfterCrop { get; set; }
            public bool CropOccurred { get; set; }
        }
        
        /// <summary>
        /// Complete scenario capture (iteration-by-iteration)
        /// </summary>
        public class ScenarioCapture
        {
            public string ScenarioName { get; set; } = "";
            public string SimInputFile { get; set; } = "";
            public string TaskInputFile { get; set; } = "";
            public string ModelInputFile { get; set; } = "";
            public int MaxNumScheds { get; set; }
            public int NumSchedCropTo { get; set; }
            public List<IterationCapture> Iterations { get; set; } = new();
        }
        
        /// <summary>
        /// Simple I/O capture - final output only (baseline)
        /// </summary>
        public class FinalOutputCapture
        {
            public string ScenarioName { get; set; } = "";
            public string SimInputFile { get; set; } = "";
            public string TaskInputFile { get; set; } = "";
            public string ModelInputFile { get; set; } = "";
            public int MaxNumScheds { get; set; }
            public int NumSchedCropTo { get; set; }
            public List<PassingScheduleInfo> FinalSchedules { get; set; } = new();  // All final schedules with hashes and state

        }
        
        #endregion
        
        #region Public Entry Points
        
        /// <summary>
        /// Load scenario up to main scheduling loop (using HorizonLoadHelper)
        /// </summary>
        public Horizon.Program LoadScenarioToMainLoop(string simInputFile, string taskInputFile, string modelInputFile)
        {
            Console.WriteLine("Loading scenario...");
            
            // Use HorizonLoadHelper (inherited from SchedulerUnitTest)
            SimInputFile = simInputFile;
            TaskInputFile = taskInputFile;
            ModelInputFile = modelInputFile;
            
            program = HorizonLoadHelper(simInputFile, taskInputFile, modelInputFile);
            
            // Note: Static Scheduler fields (SchedulerStep, _schedID) are NOT reset here
            // They must be reset in the test runner right before GenerateSchedules() is called
            // This is because Program.Main() runs in a fresh process (static fields at defaults),
            // but NUnit tests run in the same process (static fields persist from previous tests)
            // The runner explicitly resets them to match fresh process state
            
            Console.WriteLine($"✅ Scenario loaded: {_testSimSystem.Assets.Count} assets, {_testSystemTasks.Count} tasks");
            
            return program;
        }

        public void SetOutputDirectory(){
            if (program == null || _testSimSystem == null)
                throw new InvalidOperationException("Must call LoadScenarioToMainLoop() first");
            
            // Setup output directory with scenario name and task count for clarity
            if (string.IsNullOrEmpty(CurrentRunDir))
            {
                // Get scenario name (prefer SimParameters.ScenarioName, fallback to extracting from SimInputFile)
                string scenarioName = "";
                if (!string.IsNullOrEmpty(SimParameters.ScenarioName) && SimParameters.ScenarioName != "Default Scenario")
                {
                    scenarioName = SimParameters.ScenarioName;
                }
                else if (!string.IsNullOrEmpty(SimInputFile))
                {
                    // Extract scenario name from SimInputFile (e.g., "AeolusSim_150sec_max10_cropTo5.json" -> "Aeolus")
                    string simFileName = Path.GetFileNameWithoutExtension(SimInputFile);
                    var simMatch = Regex.Match(simFileName, @"^(\w+?)Sim");
                    if (simMatch.Success)
                    {
                        scenarioName = simMatch.Groups[1].Value;
                    }
                }
                
                // Extract task count from TaskInputFile name (e.g., "AeolusTasks_3.json" -> "3Tasks")
                string taskCountSuffix = "";
                if (!string.IsNullOrEmpty(TaskInputFile))
                {
                    string taskFileName = Path.GetFileNameWithoutExtension(TaskInputFile);
                    // Try to extract number from filename (e.g., "AeolusTasks_3" -> "3")
                    var taskMatch = Regex.Match(taskFileName, @"_(\d+)$");
                    if (taskMatch.Success)
                    {
                        taskCountSuffix = $"{taskMatch.Groups[1].Value}Tasks";
                    }
                }
                
                // Combine scenario name and task count: "Run_ScenarioName_TaskCount" or "Run_TaskCount" if no scenario name
                string dirPrefix = "Run";
                if (!string.IsNullOrEmpty(scenarioName) && !string.IsNullOrEmpty(taskCountSuffix))
                {
                    dirPrefix = $"Run_{scenarioName}_{taskCountSuffix}";
                }
                else if (!string.IsNullOrEmpty(scenarioName))
                {
                    dirPrefix = $"Run_{scenarioName}";
                }
                else if (!string.IsNullOrEmpty(taskCountSuffix))
                {
                    dirPrefix = $"Run_{taskCountSuffix}";
                }
                
                int runNum = 1;
                while (Directory.Exists(Path.Combine(OutputDir, $"{dirPrefix}_Run_{runNum}"))) runNum++;
                CurrentRunDir = Path.Combine(OutputDir, $"{dirPrefix}_Run_{runNum}");
                Directory.CreateDirectory(CurrentRunDir);
                
                // Create ProgramOutput subdirectory for normal program output
                string programOutputDir = Path.Combine(CurrentRunDir, "ProgramOutput");
                Directory.CreateDirectory(programOutputDir);
                SimParameters.OutputDirectory = programOutputDir;
                
                // Initialize hash history file tracking if enabled (uses ProgramOutput as base for HashData/ subdir)
                if (SimParameters.EnableHashTracking)
                {
                    HSFScheduler.SystemScheduleInfo.InitializeHashHistoryFile(programOutputDir);
                    HSFScheduler.StateHistory.InitializeStateHashHistoryFile(programOutputDir);
                }
            }
            else
            {
                // Ensure ProgramOutput directory exists and is set
                string programOutputDir = Path.Combine(CurrentRunDir, "ProgramOutput");
                if (!Directory.Exists(programOutputDir))
                {
                    Directory.CreateDirectory(programOutputDir);
                }
                SimParameters.OutputDirectory = programOutputDir;
                
                // Ensure hash history file is initialized if enabled (uses ProgramOutput as base for HashData/ subdir)
                if (SimParameters.EnableHashTracking)
                {
                    HSFScheduler.SystemScheduleInfo.InitializeHashHistoryFile(programOutputDir);
                    HSFScheduler.StateHistory.InitializeStateHashHistoryFile(programOutputDir);
                }
            }
        }
        
        /// <summary>
        /// Simple I/O capture: Run program → capture final output
        /// Flow: GenerateSchedules() → ProcessAndCaptureSchedules()
        /// </summary>
        // public FinalOutputCapture CaptureFinalOutput(string scenarioName)
        // {
        //     // Generate schedules (main scheduling loop)
        //     List<SystemSchedule> finalSchedules = program.scheduler.GenerateSchedules(
        //         _testSimSystem, _testSystemTasks, _testInitialSysState);
            
        //     program.Schedules = finalSchedules;
        //     program.EvaluateSchedules();
            
        //     // Process and capture schedules (generic method - saves normal output + hashes)
        //     List<PassingScheduleInfo> capturedSchedules = ProcessAndCaptureSchedules(
        //         finalSchedules, SimParameters.SimEndSeconds, CurrentRunDir, saveNormalProgramOutput: true);
            
        //     return new FinalOutputCapture
        //     {
        //         ScenarioName = scenarioName,
        //         SimInputFile = SimInputFile,
        //         TaskInputFile = TaskInputFile,
        //         ModelInputFile = ModelInputFile,
        //         MaxNumScheds = SchedParameters.MaxNumScheds,
        //         NumSchedCropTo = SchedParameters.NumSchedCropTo,
        //         FinalSchedules = capturedSchedules
        //     };
        // }
        
        /// <summary>
        /// Generic: Process List<SystemSchedule> → hash + capture + optionally save normal output
        /// Called after GenerateSchedules() and after CropSchedules() steps
        /// </summary>
        public List<PassingScheduleInfo> ProcessAndCaptureSchedules(
            List<SystemSchedule> schedules, 
            double currentTime, 
            string outputPath, 
            bool saveNormalProgramOutput = false)
        {
            if (saveNormalProgramOutput)
                SaveNormalProgramOutput(schedules, outputPath);
            
            return CaptureSchedulesWithHashes(schedules, currentTime);
        }
        
        /// <summary>
        /// Save normal program output (schedules_summary.txt, state CSV files, heritage format)
        /// Replicates Program.Main() output saving logic
        /// </summary>
        private void SaveNormalProgramOutput(List<SystemSchedule> schedules, string outputPath)
        {
            // schedules_summary.txt
            string summaryPath = Path.Combine(outputPath, "schedules_summary.txt");
            using (StreamWriter sw = File.CreateText(summaryPath))
            {
                int i = 0;
                foreach (SystemSchedule sched in schedules)
                {
                    sw.WriteLine("Schedule Number: " + i + "Schedule Value: " + schedules[i].ScheduleValue);
                    if (i < 5)
                    {
                        foreach (var eit in sched.AllStates.Events)
                            sw.WriteLine(eit.ToString());
                    }
                    i++;
                }
            }
            
            // State CSV files (new format)
            SystemSchedule.WriteScheduleData(schedules, outputPath, SimParameters.NumSchedulesForStateOutput);
            
            // Heritage format (old format, best schedule only)
            SystemSchedule.WriteSchedule(schedules[0], Path.Combine(outputPath, "data", "heritage"));
        }
        
        /// <summary>
        /// Save final output capture to file (additional JSON)
        /// </summary>
        public void SaveFinalOutputToFile(FinalOutputCapture capture)
        {
            // Output directory already created in CaptureFinalOutput
            string jsonPath = Path.Combine(CurrentRunDir, $"{capture.ScenarioName}_final_output.json");
            
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(capture, options));
            
            Console.WriteLine($"  ✅ Saved capture JSON: {Path.GetFileName(jsonPath)}");
        }
        
        /// <summary>
        /// Main method: Step through each schedule iteration and capture all data
        /// </summary>
        public ScenarioCapture CaptureScheduleIterations(string scenarioName, int maxIterations = -1)
        {
            if (program == null || _testSimSystem == null)
            {
                throw new InvalidOperationException("Must call LoadScenarioToMainLoop() first");
            }
            
            Console.WriteLine($"\n{new string('=', 80)}");
            Console.WriteLine($"  Schedule Data Capture: {scenarioName}");
            Console.WriteLine($"{new string('=', 80)}\n");
            
            double simStart = SimParameters.SimStartSeconds;
            double simStep = SimParameters.SimStepSeconds;
            double simEnd = SimParameters.SimEndSeconds;
            int totalIterations = maxIterations > 0 ? maxIterations : (int)((simEnd - simStart) / simStep);
            
            var scenarioCapture = new ScenarioCapture
            {
                ScenarioName = scenarioName,
                SimInputFile = SimInputFile,
                TaskInputFile = TaskInputFile,
                ModelInputFile = ModelInputFile,
                MaxNumScheds = SchedParameters.MaxNumScheds,
                NumSchedCropTo = SchedParameters.NumSchedCropTo
            };
            
            // Step through each iteration
            for (int i = 0; i < totalIterations; i++)
            {
                double currentTime = simStart + (i * simStep);
                Scheduler.SchedulerStep = i;
                SchedulerUnitTest.CurrentTime = currentTime;
                SchedulerUnitTest.NextTime = currentTime + simStep;
                
                Console.WriteLine($"\n[Iteration {i}] Time: {currentTime:F3}");
                
                var iterationCapture = CaptureIteration(currentTime, i);
                scenarioCapture.Iterations.Add(iterationCapture);
                
                // Basic output - granular counts commented out
                // Console.WriteLine($"  Potential: {iterationCapture.PotentialSchedulesCreated.Count}");
                // Console.WriteLine($"  Passing:   {iterationCapture.SchedulesPassingChecker.Count}");
                // Console.WriteLine($"  Failing:   {iterationCapture.SchedulesFailingChecker.Count}");
                Console.WriteLine($"  Schedules after merge: {iterationCapture.ScheduleIDsSurvivingCrop.Count}");
                Console.WriteLine($"  Crop:      {iterationCapture.CountBeforeCrop} → {iterationCapture.CountAfterCrop}");
            }
            
            Console.WriteLine($"\n✅ Capture complete: {scenarioCapture.Iterations.Count} iterations");
            
            return scenarioCapture;
        }
        
        /// <summary>
        /// Save capture to JSON file
        /// </summary>
        public void SaveCaptureToFile(ScenarioCapture capture)
        {
            // Find/create output directory (with scenario name and task count for clarity)
            if (string.IsNullOrEmpty(CurrentRunDir))
            {
                // Get scenario name (prefer from ScenarioName, fallback to extracting from SimInputFile)
                string scenarioName = "";
                if (!string.IsNullOrEmpty(capture.ScenarioName))
                {
                    scenarioName = capture.ScenarioName;
                }
                else if (!string.IsNullOrEmpty(capture.SimInputFile))
                {
                    // Extract scenario name from SimInputFile (e.g., "AeolusSim_150sec_max10_cropTo5.json" -> "Aeolus")
                    string simFileName = Path.GetFileNameWithoutExtension(capture.SimInputFile);
                    var simMatch = Regex.Match(simFileName, @"^(\w+?)Sim");
                    if (simMatch.Success)
                    {
                        scenarioName = simMatch.Groups[1].Value;
                    }
                }
                
                // Extract task count from TaskInputFile name (e.g., "AeolusTasks_3.json" -> "3Tasks")
                string taskCountSuffix = "";
                if (!string.IsNullOrEmpty(capture.TaskInputFile))
                {
                    string taskFileName = Path.GetFileNameWithoutExtension(capture.TaskInputFile);
                    // Try to extract number from filename (e.g., "AeolusTasks_3" -> "3")
                    var taskMatch = Regex.Match(taskFileName, @"_(\d+)$");
                    if (taskMatch.Success)
                    {
                        taskCountSuffix = $"{taskMatch.Groups[1].Value}Tasks";
                    }
                }
                
                // Combine scenario name and task count: "Run_ScenarioName_TaskCount" or "Run_TaskCount" if no scenario name
                string dirPrefix = "Run";
                if (!string.IsNullOrEmpty(scenarioName) && !string.IsNullOrEmpty(taskCountSuffix))
                {
                    dirPrefix = $"Run_{scenarioName}_{taskCountSuffix}";
                }
                else if (!string.IsNullOrEmpty(scenarioName))
                {
                    dirPrefix = $"Run_{scenarioName}";
                }
                else if (!string.IsNullOrEmpty(taskCountSuffix))
                {
                    dirPrefix = $"Run_{taskCountSuffix}";
                }
                
                int runNum = 1;
                while (Directory.Exists(Path.Combine(OutputDir, $"{dirPrefix}_Run_{runNum}")))
                {
                    runNum++;
                }
                CurrentRunDir = Path.Combine(OutputDir, $"{dirPrefix}_Run_{runNum}");
                Directory.CreateDirectory(CurrentRunDir);
                
                // Create ProgramOutput subdirectory for normal program output
                string programOutputDir = Path.Combine(CurrentRunDir, "ProgramOutput");
                Directory.CreateDirectory(programOutputDir);
                
                Console.WriteLine($"\n  Output directory: {CurrentRunDir}");
            }
            
            string jsonPath = Path.Combine(CurrentRunDir, $"{capture.ScenarioName}_full_trace.json");
            
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(capture, options));
            
            Console.WriteLine($"  ✅ Saved: {Path.GetFileName(jsonPath)}");
        }
        
        #endregion
        
        #region Private Capture Methods
        
        /// <summary>
        /// Capture single iteration: potential schedules, check results, crop
        /// </summary>
        private IterationCapture CaptureIteration(double currentTime, int iteration)
        {
            var iterationCapture = new IterationCapture
            {
                Iteration = iteration,
                CurrentTime = currentTime
            };
            
            // Step 1: Crop existing schedules (before generating new ones)
            int countBeforeCrop = _systemSchedules.Count;
            _systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, _emptySchedule, _ScheduleEvaluator);
            int countAfterCrop = _systemSchedules.Count;
            
            iterationCapture.CountBeforeCrop = countBeforeCrop;
            iterationCapture.CountAfterCrop = countAfterCrop;
            iterationCapture.CropOccurred = (countBeforeCrop > SchedParameters.MaxNumScheds);
            
            // Capture schedules after crop (reusable method - same as final output)
            // This ensures we can verify the final iteration matches the I/O capture
            // Note: First iteration doesn't matter, but last iteration's post-crop should match I/O
            // For now, we'll capture this at the end of the iteration after merge
            
            // Step 2: Generate potential schedules (TimeDeconfliction)
            List<SystemSchedule> potentialSchedules = Scheduler.TimeDeconfliction(
                _systemSchedules, _scheduleCombos, currentTime);
            
            // ============================================================================
            // GRANULAR CAPTURE - COMMENTED OUT (to be reintroduced later)
            // ============================================================================
            // Step a: Capture all potential schedules created (with hashing)
            // TODO: Reintroduce when basic capture is verified
            // foreach (var potentialSchedule in potentialSchedules)
            // {
            //     var potentialInfo = new PotentialScheduleInfo
            //     {
            //         ScheduleID = potentialSchedule._scheduleID,
            //         ContentHash = ComputeContentHash(potentialSchedule, currentTime),
            //         ParentScheduleID = potentialSchedule.Name.Contains("empty") ? "root" : GetParentScheduleID(potentialSchedule),
            //         CreationTime = currentTime,
            //         TasksAdded = ExtractTasksAdded(potentialSchedule, currentTime)
            //     };
            //     iterationCapture.PotentialSchedulesCreated.Add(potentialInfo);
            // }
            
            // Step 3: Check all potential schedules (with evaluation tracking)
            // Simplified for basic capture - just check without granular tracking
            var passingSchedules = new List<SystemSchedule>();
            // var checkResults = new Dictionary<string, (bool passed, EvaluationContext ctx)>();
            // var failingSchedules = new List<SystemSchedule>();
            
            foreach (var potentialSchedule in potentialSchedules)
            {
                // Simple check without evaluation tracking for now
                bool passed = Checker.CheckSchedule(_testSimSystem, potentialSchedule);
                
                // TODO: Reintroduce evaluation tracking when Checker callbacks are wired
                // var evalCtx = new EvaluationContext();
                // bool passed = CheckScheduleWithTracking(potentialSchedule, evalCtx);
                // checkResults[potentialSchedule._scheduleID] = (passed, evalCtx);
                
                if (passed)
                {
                    passingSchedules.Add(potentialSchedule);
                }
                // else
                // {
                //     failingSchedules.Add(potentialSchedule);
                // }
            }
            
            // Step b: Capture passing schedules (full state data)
            // TODO: Reintroduce when basic capture is verified
            // foreach (var passingSchedule in passingSchedules)
            // {
            //     var passingInfo = new PassingScheduleInfo
            //     {
            //         ScheduleID = passingSchedule._scheduleID,
            //         ContentHash = ComputeContentHash(passingSchedule, currentTime),
            //         EvaluationContext = checkResults[passingSchedule._scheduleID].ctx,
            //         AllStateData = SerializeStateData(passingSchedule),
            //         Events = ExtractEventSnapshots(passingSchedule),
            //         ScheduleValue = passingSchedule.ScheduleValue
            //     };
            //     iterationCapture.SchedulesPassingChecker.Add(passingInfo);
            // }
            
            // Step c: Capture failing schedules (failure point + state up to failure)
            // TODO: Reintroduce when basic capture is verified
            // foreach (var failingSchedule in failingSchedules)
            // {
            //     var failingInfo = new FailingScheduleInfo
            //     {
            //         ScheduleID = failingSchedule._scheduleID,
            //         ContentHash = ComputeContentHash(failingSchedule, currentTime),
            //         EvaluationContext = checkResults[failingSchedule._scheduleID].ctx,
            //         StateDataUpToFailure = SerializeStateData(failingSchedule)  // Partial state
            //     };
            //     iterationCapture.SchedulesFailingChecker.Add(failingInfo);
            // }
            // ============================================================================
            
            // Step 4: Evaluate, sort, and merge (core scheduler logic - keep active)
            var evaluatedSchedules = Scheduler.EvaluateAndSortCanPerformSchedules(_ScheduleEvaluator, passingSchedules);
            _systemSchedules = Scheduler.MergeAndClearSystemSchedules(_systemSchedules, evaluatedSchedules);
            
            Scheduler.UpdateScheduleIDs(_systemSchedules);
            
            // Step d: Process and capture schedules after merge (generic method)
            // Same method as final output capture - ensures equivalence
            // saveNormalProgramOutput=false (only save normal output for final, not each iteration)
            // Use CurrentRunDir if available, otherwise empty string (won't save normal output anyway)
            string outputPathForIteration = string.IsNullOrEmpty(CurrentRunDir) ? "" : CurrentRunDir;
            var schedulesAfterMerge = ProcessAndCaptureSchedules(
                _systemSchedules, currentTime, outputPathForIteration, saveNormalProgramOutput: false);
            
            iterationCapture.ScheduleIDsSurvivingCrop = schedulesAfterMerge.Select(s => s.ContentHash).ToList();
            
            // Note: Full schedule info stored in schedulesAfterMerge (PassingScheduleInfo objects)
            // Can be used for verification against I/O capture to ensure equivalence
            
            return iterationCapture;
        }
        
        /// <summary>
        /// Check schedule with evaluation tracking (placeholder - will wire to Checker callbacks)
        /// </summary>
        private bool CheckScheduleWithTracking(SystemSchedule schedule, EvaluationContext evalCtx)
        {
            // TODO: Wire up to Checker.CheckSchedule() with callbacks when Checker modified
            // For now, just call normally and populate context manually (limited)
            bool passed = Checker.CheckSchedule(_testSimSystem, schedule);
            
            // Manual tracking (limited) - will be replaced with callback hooks
            // This is a placeholder that shows where hooks will go
            
            return passed;
        }
        
        #endregion
        
        #region Reusable Capture Methods
        
        /// <summary>
        /// Capture all schedules with their hashes and state data
        /// Reusable method called after cropping steps - ensures consistency between
        /// iteration capture and I/O capture. Final iteration's post-merge should match I/O.
        /// </summary>
        private List<PassingScheduleInfo> CaptureSchedulesWithHashes(List<SystemSchedule> schedules, double currentTime)
        {
            var capturedSchedules = new List<PassingScheduleInfo>();
            
            // First pass: Build a mapping of ScheduleID -> ContentHash for parent lookup
            var scheduleIdToHash = new Dictionary<string, string>();
            
            // Sort schedules by depth (number of events) to ensure parents are hashed before children
            var sortedSchedules = schedules.OrderBy(s => s.AllStates.Events.Count).ToList();
            
            foreach (var schedule in sortedSchedules)
            {
                string contentHash = ComputeContentHash(schedule, currentTime, scheduleIdToHash);
                scheduleIdToHash[schedule._scheduleID] = contentHash;
                
                var scheduleInfo = new PassingScheduleInfo
                {
                    ScheduleID = schedule._scheduleID,
                    ContentHash = contentHash,
                    EvaluationContext = new EvaluationContext(),  // Empty unless tracking enabled
                    AllStateData = SerializeStateData(schedule),
                    Events = ExtractEventSnapshots(schedule),
                    ScheduleValue = schedule.ScheduleValue
                };
                capturedSchedules.Add(scheduleInfo);
            }
            
            return capturedSchedules;
        }
        
        #endregion
        
        #region Hash & Serialization Helpers
        
        /// <summary>
        /// Compute content-based deterministic hash for schedule
        /// Components: parent hash, ALL events (sorted), guarantees unique hash for unique schedule content
        /// </summary>
        private string ComputeContentHash(SystemSchedule schedule, double currentTime, Dictionary<string, string>? scheduleIdToHash = null)
        {
            var components = new List<string>();
            
            // Parent hash (or "root" for empty schedule)
            string parentHash = "root";
            if (!schedule.Name.Contains("empty"))
            {
                string parentScheduleID = GetParentScheduleID(schedule);
                if (!string.IsNullOrEmpty(parentScheduleID) && parentScheduleID != "root")
                {
                    // Try to get parent hash from dictionary (for repeatability)
                    if (scheduleIdToHash != null && scheduleIdToHash.ContainsKey(parentScheduleID))
                    {
                        parentHash = scheduleIdToHash[parentScheduleID];
                    }
                    else
                    {
                        // Fallback: use parent ScheduleID if hash not available yet
                        parentHash = parentScheduleID;
                    }
                }
            }
            components.Add(parentHash);
            
            // Extract ALL events in the schedule (not just the most recent)
            // Convert Events stack to list and reverse for chronological order
            var eventsList = schedule.AllStates.Events.ToList();
            eventsList.Reverse();  // Now in chronological order (oldest first)
            
            // Build hash for all events: each event's tasks sorted deterministically
            var allEventHashes = new List<string>();
            foreach (var evt in eventsList)
            {
                var eventTasks = new List<string>();
                foreach (var assetTaskPair in evt.Tasks)
                {
                    double taskStart = evt.GetTaskStart(assetTaskPair.Key);
                    double taskEnd = evt.GetTaskEnd(assetTaskPair.Key);
                    eventTasks.Add($"{assetTaskPair.Key.Name}:{assetTaskPair.Value.Name}:{taskStart:F6}:{taskEnd:F6}");
                }
                
                // Sort tasks within each event for determinism
                eventTasks.Sort();
                
                // Event representation: tasks separated by '|', events separated by '||'
                // If event has no tasks, use default times
                double eventStart = 0;
                double eventEnd = 0;
                if (evt.Tasks.Count > 0)
                {
                    var firstAsset = evt.Tasks.Keys.First();
                    eventStart = evt.GetEventStart(firstAsset);
                    eventEnd = evt.GetEventEnd(firstAsset);
                }
                string eventHash = $"e{eventStart:F6}:{eventEnd:F6}|{string.Join("|", eventTasks)}";
                allEventHashes.Add(eventHash);
            }
            
            components.Add(string.Join("||", allEventHashes));
            
            // Hash combined string (parent hash + all events)
            string combined = string.Join("::", components);
            return ComputeSHA256(combined);
        }
        
        private string ComputeSHA256(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower().Substring(0, 16);  // 16 char hash
            }
        }
        
        private string GetParentScheduleID(SystemSchedule schedule)
        {
            // Extract parent from schedule ID (parent is everything before last '.')
            string scheduleID = schedule._scheduleID;
            if (string.IsNullOrEmpty(scheduleID))
                return "root";
            
            int lastDot = scheduleID.LastIndexOf('.');
            if (lastDot < 0)
                return "root";
            
            return scheduleID.Substring(0, lastDot);
        }
        
        private List<TaskAccessInfo> ExtractTasksAdded(SystemSchedule schedule, double currentTime)
        {
            var tasks = new List<TaskAccessInfo>();
            
            // Get most recent event (newly added tasks)
            if (schedule.AllStates.Events.Count > 0)
            {
                var latestEvent = schedule.AllStates.Events.Peek();
                foreach (var assetTaskPair in latestEvent.Tasks)
                {
                    tasks.Add(new TaskAccessInfo
                    {
                        AssetName = assetTaskPair.Key.Name,
                        TaskName = assetTaskPair.Value.Name,
                        TaskStart = latestEvent.GetTaskStart(assetTaskPair.Key),
                        TaskEnd = latestEvent.GetTaskEnd(assetTaskPair.Key)
                    });
                }
            }
            
            return tasks;
        }
        
        private Dictionary<string, object> SerializeStateData(SystemSchedule schedule)
        {
            // Serialize AllStates.Events to dictionary
            // This is a placeholder - will need full implementation
            return new Dictionary<string, object>
            {
                ["eventCount"] = schedule.AllStates.Events.Count,
                ["events"] = ExtractEventSnapshots(schedule)
            };
        }
        
        private List<EventSnapshot> ExtractEventSnapshots(SystemSchedule schedule)
        {
            var snapshots = new List<EventSnapshot>();
            
            // Convert Events stack to list (reverse order to get chronological)
            var eventsList = schedule.AllStates.Events.ToList();
            eventsList.Reverse();  // Now in chronological order
            
            foreach (var evt in eventsList)
            {
                var snapshot = new EventSnapshot
                {
                    AssetTasks = new Dictionary<string, Dictionary<string, TaskAccessInfo>>(),
                    StateVariables = new Dictionary<string, Dictionary<string, List<TimeValuePair>>>()
                };
                
                // Extract asset tasks
                foreach (var asset in _testSimSystem.Assets)
                {
                    try
                    {
                        double eventStart = evt.GetEventStart(asset);
                        double eventEnd = evt.GetEventEnd(asset);
                        
                        snapshot.EventStart = Math.Min(snapshot.EventStart == 0 ? eventStart : snapshot.EventStart, eventStart);
                        snapshot.EventEnd = Math.Max(snapshot.EventEnd, eventEnd);
                        
                        if (evt.Tasks.ContainsKey(asset))
                        {
                            var task = evt.Tasks[asset];
                            snapshot.AssetTasks[asset.Name] = new Dictionary<string, TaskAccessInfo>
                            {
                                [task.Name] = new TaskAccessInfo
                                {
                                    AssetName = asset.Name,
                                    TaskName = task.Name,
                                    TaskStart = evt.GetTaskStart(asset),
                                    TaskEnd = evt.GetTaskEnd(asset)
                                }
                            };
                        }
                    }
                    catch
                    {
                        // Asset not in event
                    }
                }
                
                // Extract state variables (placeholder - needs full implementation)
                // This would iterate through evt.State.Ddata, Idata, Mdata, Qdata
                // and serialize all HSFProfile<T> data
                
                snapshots.Add(snapshot);
            }
            
            return snapshots;
        }
        
        #endregion
    }
}




