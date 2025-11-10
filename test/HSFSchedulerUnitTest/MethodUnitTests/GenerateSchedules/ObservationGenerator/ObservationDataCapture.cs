using Horizon;
using HSFScheduler;
using MissionElements;
using UserModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Utility class to capture observation data for GenerateSchedules tests
    /// NOT A TEST - No [Test] attributes, so it won't run during test sweeps
    /// Run manually via script to generate baseline observation data
    /// 
    /// To run: Call public methods directly (CaptureAll(), or individual scenarios)
    /// </summary>
    public class ObservationDataCapture : SchedulerUnitTest
    {
        // Entry point for standalone execution
        public static void Main_ObservationCapture()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("  Observation Data Capture");
            Console.WriteLine("═══════════════════════════════════════════════════════\n");
            
            var capture = new ObservationDataCapture();
            capture.CaptureAll();
            
            Console.WriteLine("\n✅ All observation data captured!");
        }
        

        // Get to source directory from bin/Debug/net8.0
        private string CurrentTestDir = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "MethodUnitTests", "GenerateSchedules"));
        
        private string OutputDir => Path.Combine(CurrentTestDir, "ObservationGenerator", "Output");
        private string CurrentRunDir = "";

        protected override string SimInputFile { get; set; }
        protected override string TaskInputFile { get; set; }
        protected override string ModelInputFile { get; set; }

        #region Data Structures
        
        public class ScheduleInfo
        {
            public string ID { get; set; } = "";
            public double Value { get; set; }
            public int Events { get; set; }
            public List<string> AssetTaskPairs { get; set; } = new();
        }
        
        public class IterationInfo
        {
            public int Iteration { get; set; }
            public int CountBefore { get; set; }
            public int CountAfter { get; set; }
            public bool CropOccurred { get; set; }
            public List<ScheduleInfo> Schedules { get; set; } = new();
        }
        
        public class ScenarioData
        {
            public string Name { get; set; } = "";
            public string SimInputFile { get; set; } = "";
            public string TaskInputFile { get; set; } = "";
            public string ModelInputFile { get; set; } = "";
            public int MaxNumScheds { get; set; }
            public int NumSchedCropTo { get; set; }
            public List<IterationInfo> Iterations { get; set; } = new();
            public IterationInfo FinalCrop { get; set; } = new();
        }
        
        #endregion

        /// <summary>
        /// Generate observation data for: 1 Asset, 1 Task
        /// </summary>
        public void CaptureScenario_1A1T()
        {
            Console.WriteLine("\n[1/4] Capturing: One Asset, One Task");
            
            // Use full paths
            string inputsDir = Path.GetFullPath(Path.Combine(CurrentTestDir, "Inputs"));
            SimInputFile = Path.Combine(inputsDir, "SimInput_MaxSched100_CropTo50.json");
            TaskInputFile = Path.Combine(inputsDir, "OneTaskTestFile.json");
            ModelInputFile = Path.Combine(inputsDir, "OneAssetTestModel_AlwaysTrue.json");
            
            Console.WriteLine($"  SimInput: {SimInputFile}");
            Console.WriteLine($"  Exists: {File.Exists(SimInputFile)}");
            
            var data = RunAndCapture("OneAsset_OneTask");
            SaveToFiles(data);
        }

        /// <summary>
        /// Generate observation data for: 1 Asset, 3 Tasks
        /// </summary>
        public void CaptureScenario_1A3T()
        {
            Console.WriteLine("\n[2/4] Capturing: One Asset, Three Tasks");
            
            // Use full paths
            string inputsDir = Path.GetFullPath(Path.Combine(CurrentTestDir, "Inputs"));
            SimInputFile = Path.Combine(inputsDir, "SimInput_MaxSched100_CropTo50.json");
            TaskInputFile = Path.Combine(inputsDir, "ThreeTaskTestFile.json");
            ModelInputFile = Path.Combine(inputsDir, "OneAssetTestModel_AlwaysTrue.json");
            
            var data = RunAndCapture("OneAsset_ThreeTasks");
            SaveToFiles(data);
        }

        /// <summary>
        /// Generate observation data for: 2 Assets, 1 Task
        /// </summary>
        public void CaptureScenario_2A1T()
        {
            Console.WriteLine("\n[3/4] Capturing: Two Assets, One Task");
            
            // Use full paths
            string inputsDir = Path.GetFullPath(Path.Combine(CurrentTestDir, "Inputs"));
            SimInputFile = Path.Combine(inputsDir, "SimInput_MaxSched100_CropTo50.json");
            TaskInputFile = Path.Combine(inputsDir, "OneTaskTestFile.json");
            ModelInputFile = Path.Combine(inputsDir, "TwoAssetTestModel_AlwaysTrue.json");
            
            var data = RunAndCapture("TwoAssets_OneTask");
            SaveToFiles(data);
        }

        /// <summary>
        /// Generate observation data for: 2 Assets, 3 Tasks
        /// </summary>
        public void CaptureScenario_2A3T()
        {
            Console.WriteLine("\n[4/4] Capturing: Two Assets, Three Tasks");
            
            // Use full paths
            string inputsDir = Path.GetFullPath(Path.Combine(CurrentTestDir, "Inputs"));
            SimInputFile = Path.Combine(inputsDir, "SimInput_MaxSched100_CropTo50.json");
            TaskInputFile = Path.Combine(inputsDir, "ThreeTaskTestFile.json");
            ModelInputFile = Path.Combine(inputsDir, "TwoAssetTestModel_AlwaysTrue.json");
            
            var data = RunAndCapture("TwoAssets_ThreeTasks");
            SaveToFiles(data);
        }

        /// <summary>
        /// Run all 4 scenarios
        /// </summary>
        public void CaptureAll()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("  Observation Data Capture - All Scenarios");
            Console.WriteLine("═══════════════════════════════════════════════════════");
            
            CaptureScenario_1A1T();
            ResetScheduler();
            
            CaptureScenario_1A3T();
            ResetScheduler();
            
            CaptureScenario_2A1T();
            ResetScheduler();
            
            CaptureScenario_2A3T();
            ResetScheduler();
            
            Console.WriteLine("\n✅ All observation data captured!");
        }

        private ScenarioData RunAndCapture(string scenarioName)
        {
            // CRITICAL: Reset Scheduler static state BEFORE building program
            Scheduler.SchedulerStep = -1;
            Scheduler._schedID = 0;
            
            // Build program using inherited method
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);
            
            double simStart = SimParameters.SimStartSeconds;
            double simStep = SimParameters.SimStepSeconds;
            double simEnd = SimParameters.SimEndSeconds;
            int totalIterations = (int)((simEnd - simStart) / simStep);
            
            Console.WriteLine($"  MaxNumScheds={SchedParameters.MaxNumScheds}, CropTo={SchedParameters.NumSchedCropTo}");
            
            var scenarioData = new ScenarioData
            {
                Name = scenarioName,
                SimInputFile = SimInputFile,
                TaskInputFile = TaskInputFile,
                ModelInputFile = ModelInputFile,
                MaxNumScheds = SchedParameters.MaxNumScheds,
                NumSchedCropTo = SchedParameters.NumSchedCropTo
            };
            
            // Initialize using inherited fields
            Scheduler.InitializeEmptySchedule(_systemSchedules, _testInitialSysState);
            SchedulerUnitTest._emptySchedule = Scheduler.emptySchedule;
            _scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(_testSimSystem, _testSystemTasks, _scheduleCombos, simStart, simEnd);
            
            // Run each iteration
            for (int i = 0; i < totalIterations; i++)
            {
                double currentTime = i * simStep;
                int countBefore = _systemSchedules.Count;
                
                // Use MainSchedulingLoopHelper (static method from SchedulerUnitTest)
                _systemSchedules = MainSchedulingLoopHelper(_systemSchedules, _scheduleCombos, _testSimSystem, 
                                                             _ScheduleEvaluator, _emptySchedule, currentTime, simStep, 1);
                
                int countAfter = _systemSchedules.Count;
                bool cropOccurred = (countBefore > SchedParameters.MaxNumScheds);
                
                Console.WriteLine($"  i={i}: {countBefore,3} → {countAfter,3} | Crop: {(cropOccurred ? "YES" : "NO ")}");
                
                var iterInfo = new IterationInfo
                {
                    Iteration = i,
                    CountBefore = countBefore,
                    CountAfter = countAfter,
                    CropOccurred = cropOccurred
                };
                
                foreach (var sched in _systemSchedules)
                {
                    var schedInfo = new ScheduleInfo
                    {
                        ID = sched._scheduleID,
                        Value = sched.ScheduleValue,
                        Events = sched.AllStates.Events.Count
                    };
                    
                    // Capture Asset→Task pairs
                    if (sched.ScheduleInfo.EventDetails != null)
                    {
                        foreach (var eventDetail in sched.ScheduleInfo.EventDetails.OrderBy(x => x.Key))
                        {
                            foreach (var assetTask in eventDetail.Value)
                            {
                                schedInfo.AssetTaskPairs.Add($"{assetTask.Key.Name}→{assetTask.Value.Name}");
                            }
                        }
                    }
                    
                    iterInfo.Schedules.Add(schedInfo);
                }
                
                scenarioData.Iterations.Add(iterInfo);
            }
            
            // Final crop
            int countBeforeFinal = _systemSchedules.Count;
            _systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, _emptySchedule, _ScheduleEvaluator);
            int countAfterFinal = _systemSchedules.Count;
            
            Console.WriteLine($"  Final: {countBeforeFinal,3} → {countAfterFinal,3}");
            
            var finalInfo = new IterationInfo
            {
                Iteration = -1,
                CountBefore = countBeforeFinal,
                CountAfter = countAfterFinal,
                CropOccurred = (countBeforeFinal > SchedParameters.MaxNumScheds)
            };
            
            foreach (var sched in _systemSchedules)
            {
                var schedInfo = new ScheduleInfo
                {
                    ID = sched._scheduleID,
                    Value = sched.ScheduleValue,
                    Events = sched.AllStates.Events.Count
                };
                
                // Capture Asset→Task pairs
                if (sched.ScheduleInfo.EventDetails != null)
                {
                    foreach (var eventDetail in sched.ScheduleInfo.EventDetails.OrderBy(x => x.Key))
                    {
                        foreach (var assetTask in eventDetail.Value)
                        {
                            schedInfo.AssetTaskPairs.Add($"{assetTask.Key.Name}→{assetTask.Value.Name}");
                        }
                    }
                }
                
                finalInfo.Schedules.Add(schedInfo);
            }
            
            scenarioData.FinalCrop = finalInfo;
            
            return scenarioData;
        }

        private void SaveToFiles(ScenarioData data)
        {
            // Find next run number (only on first save)
            // Max 3 dirs: Run_1, Run_2, Run_Last (Run_Last gets overwritten with archive)
            if (string.IsNullOrEmpty(CurrentRunDir))
            {
                int runNum = 1;
                while (runNum <= 2 && Directory.Exists(Path.Combine(OutputDir, $"Run_{runNum}")))
                {
                    runNum++;
                }
                
                if (runNum <= 2)
                {
                    CurrentRunDir = Path.Combine(OutputDir, $"Run_{runNum}");
                    Console.WriteLine($"\n  Creating output directory: Run_{runNum}");
                }
                else
                {
                    // Archive mechanism: keeps one backup level
                    string archiveDir = Path.Combine(OutputDir, "_Archive");
                    string archivedRunLast = Path.Combine(archiveDir, "Run_Last");
                    CurrentRunDir = Path.Combine(OutputDir, "Run_Last");
                    
                    // If archived Run_Last exists, delete it
                    if (Directory.Exists(archivedRunLast))
                    {
                        Directory.Delete(archivedRunLast, true);
                        Console.WriteLine($"\n  Deleted old archived Run_Last");
                    }
                    
                    // If current Run_Last exists, archive it
                    if (Directory.Exists(CurrentRunDir))
                    {
                        Directory.CreateDirectory(archiveDir);
                        Directory.Move(CurrentRunDir, archivedRunLast);
                        Console.WriteLine($"\n  Archived current Run_Last → _Archive/Run_Last");
                    }
                    
                    Console.WriteLine($"  Creating new Run_Last");
                }
                
                Directory.CreateDirectory(CurrentRunDir);
            }
            
            string runDir = CurrentRunDir;
            
            // Use consistent file names (no numbers)
            string jsonPath = Path.Combine(runDir, $"{data.Name}.json");
            string txtPath = Path.Combine(runDir, $"{data.Name}.txt");
            
            // Save JSON
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, options));
            
            // Save TXT (human-readable)
            using (var writer = new StreamWriter(txtPath))
            {
                writer.WriteLine($"{data.Name} - Observation Data");
                writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                // Add git info if available
                try
                {
                    var gitBranch = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "rev-parse --abbrev-ref HEAD",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.Combine(CurrentTestDir, "..", "..", "..", "..")
                    });
                    gitBranch?.WaitForExit();
                    string branch = gitBranch?.StandardOutput.ReadToEnd().Trim() ?? "unknown";
                    
                    var gitCommit = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "rev-parse --short HEAD",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.Combine(CurrentTestDir, "..", "..", "..", "..")
                    });
                    gitCommit?.WaitForExit();
                    string commit = gitCommit?.StandardOutput.ReadToEnd().Trim() ?? "unknown";
                    
                    writer.WriteLine($"Branch: {branch}");
                    writer.WriteLine($"Commit: {commit}");
                }
                catch
                {
                    writer.WriteLine($"Branch: (git not available)");
                    writer.WriteLine($"Commit: (git not available)");
                }
                
                writer.WriteLine();
                writer.WriteLine("Input Files:");
                writer.WriteLine($"  Sim:   {SimInputFile}");
                writer.WriteLine($"  Task:  {TaskInputFile}");
                writer.WriteLine($"  Model: {ModelInputFile}");
                
                writer.WriteLine();
                writer.WriteLine("═════════════════════════════════════════════");
                writer.WriteLine($"SUMMARY: MaxScheds={data.MaxNumScheds} | CropTo={data.NumSchedCropTo}");
                writer.WriteLine("═════════════════════════════════════════════");
                writer.WriteLine();
                
                writer.WriteLine("Assets:");
                // Group subsystems by asset
                var assetSubsystems = new Dictionary<string, List<string>>();
                for (int i = 0; i < _testSimSystem.Assets.Count; i++)
                {
                    var asset = _testSimSystem.Assets[i];
                    var subsystem = _testSimSystem.Subsystems[i];
                    if (!assetSubsystems.ContainsKey(asset.Name))
                        assetSubsystems[asset.Name] = new List<string>();
                    assetSubsystems[asset.Name].Add(subsystem.Name);
                }
                
                foreach (var kvp in assetSubsystems)
                {
                    writer.WriteLine($"  {kvp.Key}: [{string.Join(", ", kvp.Value)}]");
                }
                
                writer.WriteLine();
                writer.WriteLine("Tasks:");
                foreach (var task in _testSystemTasks)
                {
                    writer.WriteLine($"  {task.Name}: Value = {task.Target.Value,-12} MaxTimes = {task.MaxTimesToPerform}");
                }
                
                writer.WriteLine("═════════════════════════════════════════════");
                writer.WriteLine();
                
                foreach (var iter in data.Iterations)
                {
                    writer.WriteLine($"─────────────────────────────────────────────");
                    writer.WriteLine($"Iteration {iter.Iteration}: {iter.CountBefore} → {iter.CountAfter} | Crop: {(iter.CropOccurred ? "YES" : "NO")}");
                    writer.WriteLine($"─────────────────────────────────────────────");
                    
                    foreach (var sched in iter.Schedules.OrderByDescending(s => s.Value).ThenBy(s => s.ID))
                    {
                        string pairsText = "";
                        if (sched.AssetTaskPairs.Count > 0)
                        {
                            // Group by unique Asset→Task pairs and show each with count
                            var grouped = sched.AssetTaskPairs
                                .GroupBy(x => x)
                                .OrderByDescending(g => g.Count())
                                .Select(g => {
                                    // Abbreviate testassetN to AN
                                    string pair = g.Key;
                                    if (pair.StartsWith("testasset", StringComparison.OrdinalIgnoreCase))
                                    {
                                        pair = System.Text.RegularExpressions.Regex.Replace(
                                            pair, 
                                            @"testasset(\d+)", 
                                            "A$1", 
                                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                    }
                                    return $"{pair} x{g.Count()}";
                                });
                            pairsText = $" ({string.Join(", ", grouped)})";
                        }
                        writer.WriteLine($"  {sched.ID,-30} | {sched.Value,10:F4} | Events: {sched.Events} ..... {pairsText}");
                    }
                    writer.WriteLine();
                }
                
                writer.WriteLine($"═════════════════════════════════════════════");
                writer.WriteLine($"FINAL: {data.FinalCrop.CountBefore} → {data.FinalCrop.CountAfter}");
                writer.WriteLine($"═════════════════════════════════════════════");
                foreach (var sched in data.FinalCrop.Schedules.OrderByDescending(s => s.Value).ThenBy(s => s.ID))
                {
                    string pairsText = "";
                    if (sched.AssetTaskPairs.Count > 0)
                    {
                        // Group by unique Asset→Task pairs and show each with count
                        var grouped = sched.AssetTaskPairs
                            .GroupBy(x => x)
                            .OrderByDescending(g => g.Count())
                            .Select(g => {
                                // Abbreviate testassetN to AN
                                string pair = g.Key;
                                if (pair.StartsWith("testasset", StringComparison.OrdinalIgnoreCase))
                                {
                                    pair = System.Text.RegularExpressions.Regex.Replace(
                                        pair, 
                                        @"testasset(\d+)", 
                                        "A$1", 
                                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                }
                                return $"{pair} x{g.Count()}";
                            });
                        pairsText = $" ({string.Join(", ", grouped)})";
                    }
                    writer.WriteLine($"  {sched.ID,-30} | {sched.Value,10:F4} ..... {pairsText}");
                }
            }
            
            Console.WriteLine($"     ✅ Saved: {Path.GetFileName(jsonPath)}");
            Console.WriteLine($"     ✅ Saved: {Path.GetFileName(txtPath)}");
        }

        private void ResetScheduler()
        {
            // Reset instance fields (static Scheduler fields reset in RunAndCapture)
            _systemSchedules.Clear();
            _scheduleCombos.Clear();
            _potentialSystemSchedules.Clear();
            _systemCanPerformList.Clear();
            _emptySchedule = null;
            program = new Horizon.Program();
            _testSimSystem = null;
            _testSystemTasks.Clear();
        }
    }
}

