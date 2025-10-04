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
        protected static double? CurrentTime {get; set;} 
        protected static double? NextTime {get; set;} 
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
                //Scheduler.SchedulerStep += 1;
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
        # endregion



    }
}
