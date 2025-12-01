using Microsoft.VisualStudio.TestPlatform.TestHost;
using Horizon;
using Utilities;
using NUnit.Framework;
using NUnit.Framework.Internal;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using System.Runtime.InteropServices.Marshalling;
using log4net;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using log4net.Appender;
using System.Runtime.CompilerServices;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using UserModel;

namespace HSFSchedulerUnitTest
{
    public abstract class SchedulerUnitTest // This is the base class that all other SchedulerUnitTests will derive from. 
    // Place common "HSFSchedulerUnitTest" functionality here to be used in other classes and/or overriden. 
    {
        # region Scheduler Unit Test Base Attributes 
        protected virtual string? SimInputFile { get; set; }
        protected virtual string? TaskInputFile { get; set; }
        protected virtual string? ModelInputFile { get; set; }
        protected int? _emptySchedIdx { get; set; }
        # endregion


        # region Private/Internal Program Attributes
        protected Horizon.Program program { get; set; } = new Horizon.Program();
        protected SystemClass? _testSimSystem { get; set; }
        protected Stack<MissionElements.Task> _testSystemTasks { get; set; } = new Stack<MissionElements.Task>();
        protected SystemState _testInitialSysState { get; set; } = new SystemState();
        #endregion

        #region Private/Intneral Scheduler Attributes
        // Scheduler static class attributes neded to be mirrored for testing:
        protected static int SchedulerStep {get; set;} = -1;
        protected static double CurrentTime { get; set; } = SimParameters.SimStartSeconds;
        protected static double NextTime { get; set; } = SimParameters.SimStepSeconds;
        protected static int _schedID { get; set; } = 0;
        protected static int? _SchedulesGenerated { get; set; } = 0;
        protected static int? _SchedulesCarriedOver { get; set; } = 0;
        protected static int? _SchedulesCropped { get; set; } = 0;

        // Attributes that are private in the Scheduler class that are needed for testing:
        // Needed for schedule evaluation and computation:
        protected static SystemSchedule? _emptySchedule {get; set;}
        protected List<SystemSchedule> _systemSchedules = new List<SystemSchedule>();
        protected bool _canPregenAccess { get; set; } = false;
        protected Stack<Stack<Access>> _scheduleCombos = new Stack<Stack<Access>>();
        protected Stack<Access>? _preGeneratedAccesses { get; set; }
        protected List<SystemSchedule> _potentialSystemSchedules = new List<SystemSchedule>();
        protected List<SystemSchedule> _systemCanPerformList = new List<SystemSchedule>();
        protected Evaluator? _ScheduleEvaluator { get; set; }
        # endregion

        # region Test Class Attributes
        // Test method name and class name for logging
        protected string? CurrentTestName { get; private set; }
        protected string? CurrentClassName { get; private set; }

        // Class file tracking variables
        private string? _classSourceFilePath;
        private string? _className;
        /// Property that automatically resolves the test project root directory
        protected static string ProjectTestDir => GetTestProjectRootDirectory();
        /// Property that automatically resolves the test directory for the current test class
        protected string CurrentTestDir => GetClassSourceDirectory();
        # endregion

        # region Setup and TearDown
        [SetUp]
        public virtual void Setup()
        {
            // A small bit of functionality for readability and logging during testing and test development
            CurrentTestName = TestContext.CurrentContext.Test.Name;
            CurrentClassName = TestContext.CurrentContext.Test.ClassName;
            //Console.WriteLine($"=~==~= Starting Test: {CurrentTestName} =~==~=\n");
            
            TestContext.WriteLine($"=~==~==~= Starting Test: {CurrentClassName}.{CurrentTestName} =~==~==~=\n");
        }

        [TearDown]
        public virtual void TearDown()
        {
            // Simple approach - just log completion
            //Console.WriteLine($"=~===~= Test {CurrentTestName} Completed =~==~=\n");
            TestContext.WriteLine($"=~==~==~= Test {CurrentClassName}.{CurrentTestName} Completed =~==~==~=\n");
        }
        # endregion
        public static List<SystemSchedule> MainSchedulingLoopHelper(
            List<SystemSchedule> systemSchedules,
            Stack<Stack<Access>> scheduleCombos,
            SystemClass system,
            Evaluator evaluator,
            SystemSchedule emptySchedule,
            double startTime, 
            double timeStep, 
            int iterations)
        {
            for (double currentTime = startTime; currentTime < startTime + iterations * timeStep; currentTime += timeStep)
            {
                Scheduler.SchedulerStep += 1; // Im pretty sure its static and called in the ScheduleInfo class to make it easy; so screw it-- its public set now to make it smooth for visualization sake. Doesnt impact the algorithm main logic. 
                SchedulerUnitTest.CurrentTime = currentTime;
                SchedulerUnitTest.NextTime = currentTime + timeStep;
                systemSchedules = Scheduler.CropToMaxSchedules(systemSchedules, emptySchedule, evaluator);
                var potential = Scheduler.TimeDeconfliction(systemSchedules, scheduleCombos, currentTime);
                var canPerform = Scheduler.CheckAllPotentialSchedules(system, potential);
                var sorted = Scheduler.EvaluateAndSortCanPerformSchedules(evaluator, canPerform);
                //SchedulerUnitTest._SchedulesGenerated = canPerform.Count();
                systemSchedules = Scheduler.MergeAndClearSystemSchedules(systemSchedules, sorted);
                //SchedulerUnitTest._SchedulesCarriedOver = systemSchedules.Count() - SchedulerUnitTest._SchedulesGenerated;
                Scheduler.UpdateScheduleIDs(systemSchedules);
                //SystemScheduleInfo.PrintAllSchedulesSummary(systemSchedules, showAssetTaskDetails: false);
            }
            return systemSchedules;
        }


        #region Horizon Load Helper
        public virtual Horizon.Program HorizonLoadHelper(string SimInputFile, string TaskInputFile, string ModelInputFile)
        {
            #region Input File (argsList) Pathing Setup & Validation

            // Use the bulletproof project test directory property
            string SchedulerTestDirectory = ProjectTestDir;

            // Check if the input files exist (full path was passed) if not, assume relative path from SchedulerTestDirectory
            if (!File.Exists(SimInputFile)) { SimInputFile = Path.Combine(SchedulerTestDirectory, SimInputFile); }
            if (!File.Exists(TaskInputFile)) { TaskInputFile = Path.Combine(SchedulerTestDirectory, TaskInputFile); }
            if (!File.Exists(ModelInputFile)) { ModelInputFile = Path.Combine(SchedulerTestDirectory, ModelInputFile); }


            // Initiate a (spoofed) argsList as if input from the CLI to the console application:
            List<string> argsList = new List<String>();

            // Check if the input files above exist before adding them to the argsList: 
            if (File.Exists(SimInputFile)) { argsList.Add("-s"); argsList.Add(SimInputFile); }
            else { Console.WriteLine("HSFSchedulerUnitTest: No valid Test Simulation Input file was found. Using default."); }
            if (File.Exists(TaskInputFile)) { argsList.Add("-t"); argsList.Add(TaskInputFile); }
            else { Console.WriteLine("HSFSchedulerUnitTest: No valid Test Task Input file was found. Using default."); }
            if (File.Exists(ModelInputFile)) { argsList.Add("-m"); argsList.Add(ModelInputFile); }
            else { Console.WriteLine("HSFSchedulerUnitTest: No valid Test Model Input file was found. Using default."); }

            // Check and create the test output directory. 
            string outputDir = Path.Combine(SchedulerTestDirectory, @"output/");
            if (!Directory.Exists(outputDir)) { Directory.CreateDirectory(outputDir); }
            // Add the output directory to the argsList
            argsList.Add("-o"); argsList.Add(outputDir);

            #endregion

            // Create a new Horizon program
            // Horizon.Program program = new Horizon.Program(); // Now created in the program attribute

            // Run Horizon like normal to load all necessary elements: 
            program.InitInput(argsList);
            program.InitOutput(argsList);
            program.LoadScenario();
            program.LoadTasks();
            program.LoadSubsystems();
            program.LoadEvaluator();

            // Call the rest of the Program.CreateSchedules() method manually to complete setup: 
            // Program.CreateSchedules(){
            program.SimSystem = new SystemClass(program.AssetList, program.SubList, program.ConstraintsList, program.SystemUniverse);
            if (program.SimSystem.CheckForCircularDependencies())
                throw new NotFiniteNumberException("System has circular dependencies! Please correct then try again.");
            program.scheduler = new Scheduler(program.SchedEvaluator);

            _testSimSystem = program.SimSystem;
            _testSystemTasks = new Stack<MissionElements.Task>(program.SystemTasks);
            _testInitialSysState = program.InitialSysState;
            _ScheduleEvaluator = program.SchedEvaluator;
            
            // And this is where we pause the setup because we are testing this method:
            //program.Schedules = program.scheduler.GenerateSchedules(program.SimSystem, program.SystemTasks, program.InitialSysState);
            // }

            // Now everything is loaded in like the normal sart to the program... 
            // Return to Test for further Scheduler method entrance / testing ...
            return program;

        }
        # endregion

        # region Test Directory Helpers
        protected string GetClassSourceFilePath()
        {
            if (!string.IsNullOrEmpty(_classSourceFilePath))
                return _classSourceFilePath;

            var stackTrace = new StackTrace(true);

            // Walk up the stack to find the first frame that's not in the base class
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                string fileName = frame?.GetFileName();

                if (!string.IsNullOrEmpty(fileName) &&
                    !fileName.EndsWith("SchedulerUnitTest.cs") &&
                    fileName.EndsWith(".cs"))
                {
                    _classSourceFilePath = fileName;
                    return fileName;
                }
            }

            // Fallback: try to construct expected path
            string expectedFileName = $"{this.GetType().Name}.cs";
            string fallbackPath = Path.Combine(ProjectTestDir, "MethodUnitTests", this.GetType().Name.Replace("Test", ""), expectedFileName);
            _classSourceFilePath = fallbackPath;
            return fallbackPath;
        }

        /// Gets the source directory of the derived class using StackFrame analysis
        protected string GetClassSourceDirectory()
        {
            if (!string.IsNullOrEmpty(_classSourceFilePath))
            {
                _className = this.GetType().Name;
                return Path.GetDirectoryName(_classSourceFilePath) ?? ProjectTestDir;
            }

            var stackTrace = new StackTrace(true);

            // Walk up the stack to find the first frame that's not in the base class
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                string fileName = frame?.GetFileName();

                if (!string.IsNullOrEmpty(fileName) &&
                    !fileName.EndsWith("SchedulerUnitTest.cs") &&
                    fileName.EndsWith(".cs"))
                {
                    _classSourceFilePath = fileName;
                    _className = this.GetType().Name;
                    return Path.GetDirectoryName(fileName) ?? ProjectTestDir;
                }
            }

            // Fallback: construct expected directory
            string fallbackDir = this.GetType().Name.Replace("Test", "");
            string fallbackPath = Path.Combine(ProjectTestDir, "MethodUnitTests", fallbackDir);
            _className = this.GetType().Name;
            return fallbackPath;
        }

        protected static string GetTestProjectRootDirectory()
        {
            string baseTestDir = Utilities.DevEnvironment.GetTestDirectory();
            return Path.Combine(baseTestDir, "HSFSchedulerUnitTest");
        }

        /// <summary>
        /// Get the next run version by scanning existing Run_* directories
        /// </summary>
        private static string GetNextRunVersion(string outputDir)
        {
            if (!Directory.Exists(outputDir))
                return "00A";
            
            var runDirs = Directory.GetDirectories(outputDir, "Run_*");
            if (runDirs.Length == 0)
                return "00A";
            
            string maxVersion = "00A";
            foreach (var dir in runDirs)
            {
                string dirName = Path.GetFileName(dir);
                // Extract version: "Run_00A_..." → "00A"
                if (dirName.StartsWith("Run_") && dirName.Length > 8)
                {
                    string version = dirName.Substring(4, 3);
                    if (string.Compare(version, maxVersion) > 0)
                        maxVersion = version;
                }
            }
            
            return IncrementRunVersion(maxVersion);
        }

        /// <summary>
        /// Increment run version (00A → 00B → ... → 00Z → 01A → ...)
        /// </summary>
        private static string IncrementRunVersion(string currentVersion)
        {
            if (string.IsNullOrEmpty(currentVersion) || currentVersion.Length != 3)
                return "00A";
            
            int number = int.Parse(currentVersion.Substring(0, 2));
            char letter = currentVersion[2];
            
            number++;
            if (number > 99)
            {
                number = 0;
                letter++;
                if (letter > 'Z')
                    throw new InvalidOperationException("Run version overflow! Exceeded 99Z.");
            }
            
            return $"{number:D2}{letter}";
        }

        /// <summary>
        /// Runs a scenario two ways and generates hash summaries for comparison:
        /// 1. Direct GenerateSchedules call - runs full scheduler and saves hash summary
        /// 2. MainSchedulingLoopHelper - runs iteration-by-iteration and saves hash summary after each iteration
        /// All outputs go to test/output/ with subdirectories for each run type.
        /// </summary>
        /// <param name="simInputFile">Path to simulation input file</param>
        /// <param name="taskInputFile">Path to task input file</param>
        /// <param name="modelInputFile">Path to model input file</param>
        public void RunScenarioWithHashSummaries(string simInputFile, string taskInputFile, string modelInputFile, string? outputDirectory = null, bool muteSubsystemTracking = true)
        {
            // Determine base output directory: use provided one, or default to test/HSFSchedulerUnitTest/output/
            string baseOutputDir = outputDirectory ?? Path.Combine(ProjectTestDir, "output");
            
            // Create run directory with timestamp and scenario name (similar to main program)
            // We'll get the scenario name after loading, but create the directory structure first
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string runDir = Path.Combine(baseOutputDir, $"run_{timestamp}_Loading");
            Directory.CreateDirectory(runDir);
            
            // Set up run log capture BEFORE loading to capture all output
            string directOutputDir = Path.Combine(runDir, "direct_generateschedules");
            Directory.CreateDirectory(directOutputDir);
            
            // Mute SubsystemCallTracker console output by default (to reduce log verbosity)
            // Can be enabled by setting muteSubsystemTracking = false
            if (muteSubsystemTracking)
            {
                HSFSystem.SubsystemCallTracker.SetConsoleOutput(false);
            }
            
            // Create and start console logger to capture output during loading AND GenerateSchedules
            DateTime runDateTime = DateTime.Now;
            Horizon.ConsoleLogger directLogger = new Horizon.ConsoleLogger(
                directOutputDir, 
                "Loading", // Will be updated after loading
                simInputFile, 
                modelInputFile, 
                taskInputFile, 
                runDateTime
            );
            directLogger.StartLogging();
            
            Console.WriteLine($"\n=== Output Directory: {runDir} ===");
            
            // Load scenario using HorizonLoadHelper (output will be captured to run_log.txt)
            // Note: EnableHashTracking defaults to true and can be set in the simulation JSON file
            HorizonLoadHelper(simInputFile, taskInputFile, modelInputFile);
            
            // Get scheduler and evaluator from loaded program
            if (program.scheduler == null || program.SchedEvaluator == null || program.SimSystem == null || program.SystemTasks == null || program.InitialSysState == null)
            {
                directLogger.StopLogging();
                throw new InvalidOperationException("Failed to load scenario - required program components are null");
            }
            
            // Update run directory name with actual scenario name now that it's loaded
            string scenarioName = UserModel.SimParameters.ScenarioName;
            
            // Handle last_run → Run_XXY versioning (similar to main program)
            var lastRunDirs = Directory.GetDirectories(baseOutputDir, "last_run_*");
            
            if (lastRunDirs.Length > 0)
            {
                // Rename existing last_run to Run_XXY
                string lastRunDir = lastRunDirs[0];  // Should only be one
                string nextVersion = GetNextRunVersion(baseOutputDir);
                string versionedName = Path.GetFileName(lastRunDir).Replace("last_run_", $"Run_{nextVersion}_");
                string versionedPath = Path.Combine(baseOutputDir, versionedName);
                
                try
                {
                    Directory.Move(lastRunDir, versionedPath);
                    Console.WriteLine($"Archived previous run: {Path.GetFileName(versionedPath)}");
                }
                catch
                {
                    // If rename fails, continue anyway
                    Console.WriteLine($"Warning: Could not archive previous run directory");
                }
            }
            
            // Create new last_run directory with actual scenario name
            string finalRunDir = Path.Combine(baseOutputDir, $"last_run_{timestamp}_{scenarioName}");
            
            // Rename directory if it's different from the temporary "Loading" name
            if (runDir != finalRunDir)
            {
                try
                {
                    Directory.Move(runDir, finalRunDir);
                    runDir = finalRunDir;
                    // Update logger output path (need to stop, update, restart)
                    directLogger.StopLogging();
                    directOutputDir = Path.Combine(runDir, "direct_generateschedules");
                    Directory.CreateDirectory(directOutputDir);
                    directLogger = new Horizon.ConsoleLogger(
                        directOutputDir, 
                        scenarioName, 
                        simInputFile, 
                        modelInputFile, 
                        taskInputFile, 
                        runDateTime
                    );
                    directLogger.StartLogging();
                }
                catch
                {
                    // If rename fails, just continue with original directory name
                    Console.WriteLine($"Warning: Could not rename directory to include scenario name. Using: {runDir}");
                }
            }
            
            var scheduler = program.scheduler;
            var evaluator = program.SchedEvaluator;
            var system = program.SimSystem;
            var tasks = program.SystemTasks;
            var initialState = program.InitialSysState;

            // Set SimParameters.OutputDirectory to our test output directory
            // This ensures all hash history files (including combined hash history) write to the correct location
            // Safe in test context - we control the entire execution
            UserModel.SimParameters.OutputDirectory = directOutputDir;
            
            // Initialize hash history files BEFORE GenerateSchedules (so they can be written to during execution)
            // This ensures the files exist and are ready to receive data during GenerateSchedules
            if (UserModel.SimParameters.EnableHashTracking)
            {
                HSFScheduler.SystemScheduleInfo.InitializeHashHistoryFile(directOutputDir);
                HSFScheduler.StateHistory.InitializeStateHashHistoryFile(directOutputDir);
                HSFScheduler.SystemScheduleInfo.InitializeCombinedHashHistoryFile(directOutputDir);
            }
            
            Console.WriteLine("\n=== Method 1: Direct GenerateSchedules Call ===");
            
            // Direct GenerateSchedules call (output will be captured to run_log.txt)
            // Hash history files will be written to during this execution
            List<SystemSchedule> directSchedules = scheduler.GenerateSchedules(system, tasks, initialState);
            
            // Match Program.cs flow: EvaluateSchedules() is called after GenerateSchedules()
            // This re-evaluates schedules and sorts them, which writes an additional hash history line
            program.Schedules = directSchedules;
            program.EvaluateSchedules();
            directSchedules = program.Schedules; // Update reference in case it was modified
            
            // Stop logging after EvaluateSchedules completes
            directLogger.StopLogging();
            
            // Use Program.cs static method to write hash summary (same as main program does)
            // Hash history files were already written during GenerateSchedules execution
            if (UserModel.SimParameters.EnableHashTracking)
            {
                Horizon.Program.SaveScheduleHashBlockchainSummary(directSchedules, directOutputDir);
                Console.WriteLine($"Saved hash summary: {Path.Combine(directOutputDir, "HashData", "scheduleHashBlockchainSummary.txt")}");
            }
            
            Console.WriteLine($"\n=== Complete: Hash summaries saved to {runDir} ===");
        }

        /// <summary>
        /// Helper method to save schedule hash blockchain summary to test output directory.
        /// Creates the same hash summary files as the main program, but outputs to test/output/HashData/.
        /// Optionally initializes hash history files (FullScheduleHashHistory.txt, FullStateHistoryHash.txt, FullScheduleStateHashHistory.txt)
        /// so they can be written to during scheduler execution.
        /// Can be called between iterations or after GenerateSchedules runs.
        /// </summary>
        /// <param name="schedules">List of schedules to generate summary for</param>
        /// <param name="subdirectory">Optional subdirectory name (e.g., "iteration_0", "after_generate"). 
        /// If empty, outputs directly to baseOutputDir/HashData/. Defaults to empty string.</param>
        /// <param name="baseOutputDir">Base output directory. If null, defaults to test/HSFSchedulerUnitTest/output/</param>
        /// <param name="skipHashHistoryInit">If true, skips initializing hash history files (use when they were already initialized before execution)</param>
        /// <returns>Path to the created summary file</returns>
        protected string SaveTestHashSummary(List<SystemSchedule> schedules, string subdirectory = "", string? baseOutputDir = null, bool skipHashHistoryInit = false)
        {
            // Get test output directory (default: test/HSFSchedulerUnitTest/output/)
            // Or use provided baseOutputDir
            string testOutputDir = baseOutputDir ?? Path.Combine(ProjectTestDir, "output");
            if (!Directory.Exists(testOutputDir))
            {
                Directory.CreateDirectory(testOutputDir);
            }

            // If subdirectory specified, create it (e.g., "iteration_0", "after_generate")
            // If not specified, output directly to baseOutputDir/HashData/
            string outputPath = testOutputDir;
            if (!string.IsNullOrEmpty(subdirectory))
            {
                outputPath = Path.Combine(testOutputDir, subdirectory);
                Directory.CreateDirectory(outputPath);
            }

            // Initialize hash history files (same as main program does)
            // This creates the files and sets up tracking so they can be written to during execution
            // Skip if they were already initialized before GenerateSchedules (to avoid clearing existing data)
            if (UserModel.SimParameters.EnableHashTracking && !skipHashHistoryInit)
            {
                HSFScheduler.SystemScheduleInfo.InitializeHashHistoryFile(outputPath);
                HSFScheduler.StateHistory.InitializeStateHashHistoryFile(outputPath);
                // Combined hash history file is initialized automatically when first used
            }

            // Call the main program's method to create the summary
            Horizon.Program.SaveScheduleHashBlockchainSummary(schedules, outputPath);

            // Return path to the created summary file
            return Path.Combine(outputPath, "HashData", "scheduleHashBlockchainSummary.txt");
        }
        # endregion



    }
}
