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
        protected string? SimInputFile { get; set; }
        protected string? TaskInputFile { get; set; }
        protected string? ModelInputFile { get; set; }
        protected Horizon.Program? program { get; set; }
        protected int? _emptySchedIdx { get; set; }
        # endregion

        #region Private/Intneral Scheduler Attributes
        // Attributes that are private in the Scheduler class that are needed for testing:
        // Needed for schedule evaluation and computation:
        protected List<SystemSchedule> systemSchedules = new List<SystemSchedule>();
        protected Stack<Stack<Access>> scheduleCombos = new Stack<Stack<Access>>(); 
        protected List<SystemSchedule> potentialSystemSchedules = new List<SystemSchedule>();
        protected List<SystemSchedule> systemCanPerformList = new List<SystemSchedule>();
        protected bool? canPregenAccess {get; set; }
        protected Stack<Access>? preGeneratedAccesses {get; set;}
        # endregion

        # region Test Class Attributes
        // Test method name and class name for logging
        protected string? CurrentTestName { get; private set; }
        protected string? CurrentClassName { get; private set; }
        
        // Class file tracking variables
        private string? _classSourceFilePath;
        private string? _className;
        
        /// <summary>
        /// Property that automatically resolves the test project root directory
        /// Derived classes can use this directly without needing their own property
        /// </summary>
        protected string ProjectTestDir => GetTestProjectRootDirectory();
        
        /// <summary>
        /// Property that automatically resolves the test directory for the current test class
        /// Derived classes can use this directly without needing their own property
        /// </summary>
        protected string CurrentTestDir => GetClassSourceDirectory();

        /// <summary>
        /// Gets the source file path of the derived class using StackFrame analysis
        /// </summary>
        /// <returns>Full path to the derived class source file</returns>
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

        /// <summary>
        /// Gets the source directory of the derived class using StackFrame analysis
        /// </summary>
        /// <returns>Full path to the derived class source directory</returns>
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

        protected string GetTestProjectRootDirectory()
        {
            string baseTestDir = Utilities.DevEnvironment.GetTestDirectory();
            return Path.Combine(baseTestDir, "HSFSchedulerUnitTest");
        }
        
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
            Horizon.Program program = new Horizon.Program();

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

            // And this is where we pause the setup because we are testing this method:
            //program.Schedules = program.scheduler.GenerateSchedules(program.SimSystem, program.SystemTasks, program.InitialSysState);
            // }

            // Now everything is loaded in like the normal sart to the program... 
            // Return to Test for further Scheduler method entrance / testing ...
            return program;

        }
        # endregion

        #region Test Directory Helpers (LEGACY - COMMENTED OUT)
        /*
        /// <summary>
        /// Gets the test project root directory (where HSFSchedulerUnitTest.csproj and SchedulerUnitTest.cs are located)
        /// </summary>
        /// <returns>Full path to the test project root directory</returns>
        protected string GetTestProjectRootDirectory()
        {
            string baseTestDir = Utilities.DevEnvironment.GetTestDirectory();
            return Path.Combine(baseTestDir, "HSFSchedulerUnitTest");
        }

        /// <summary>
        /// Gets the test directory path for the current test class by searching for the .cs file
        /// </summary>
        /// <returns>Full path to the current test class directory</returns>
        protected string GetCurrentTestClassDirectory()
        {
            string expectedFileName = $"{this.GetType().Name}.cs";
            
            // Directories to exclude from search
            string[] excludeDirs = { "Subsystems", "bin", "obj", "output", "InputFiles" };
            
            // First, check if the file is directly in the project root
            string directPath = Path.Combine(ProjectTestDir, expectedFileName);
            if (File.Exists(directPath))
            {
                return ProjectTestDir;
            }
            
            // Search in MethodUnitTests and all its subdirectories first (priority search)
            string methodUnitTestsDir = Path.Combine(ProjectTestDir, "MethodUnitTests");
            if (Directory.Exists(methodUnitTestsDir))
            {
                string foundPath = SearchForClassFile(methodUnitTestsDir, expectedFileName, excludeDirs);
                if (!string.IsNullOrEmpty(foundPath))
                {
                    return foundPath;
                }
            }
            
            // If not found in MethodUnitTests, search all other subdirectories
            string foundPathInOther = SearchForClassFile(ProjectTestDir, expectedFileName, excludeDirs, skipMethodUnitTests: true);
            if (!string.IsNullOrEmpty(foundPathInOther))
            {
                return foundPathInOther;
            }
            
            // Fallback: if file not found, return MethodUnitTests directory with class name
            string fallbackDir = this.GetType().Name.Replace("Test", "");
            return Path.Combine(methodUnitTestsDir, fallbackDir);
        }

        /// <summary>
        /// Recursively searches for a class file in a directory and its subdirectories
        /// </summary>
        /// <param name="searchDir">Directory to search in</param>
        /// <param name="fileName">File name to search for</param>
        /// <param name="excludeDirs">Directories to exclude from search</param>
        /// <param name="skipMethodUnitTests">If true, skip searching in MethodUnitTests directory</param>
        /// <returns>Directory path where the file was found, or empty string if not found</returns>
        private string SearchForClassFile(string searchDir, string fileName, string[] excludeDirs, bool skipMethodUnitTests = false)
        {
            if (!Directory.Exists(searchDir))
                return string.Empty;
            
            try
            {
                // Check if the file exists in the current directory
                string filePath = Path.Combine(searchDir, fileName);
                if (File.Exists(filePath))
                {
                    return searchDir;
                }
                
                // Search subdirectories
                var subDirs = Directory.GetDirectories(searchDir);
                foreach (string subDir in subDirs)
                {
                    string dirName = Path.GetFileName(subDir);
                    
                    // Skip excluded directories
                    if (excludeDirs.Contains(dirName))
                        continue;
                    
                    // Skip MethodUnitTests if requested
                    if (skipMethodUnitTests && dirName == "MethodUnitTests")
                        continue;
                    
                    // Recursively search the subdirectory
                    string foundPath = SearchForClassFile(subDir, fileName, excludeDirs, skipMethodUnitTests);
                    if (!string.IsNullOrEmpty(foundPath))
                    {
                        return foundPath;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception but continue searching
                TestContext.WriteLine($"Warning: Error searching directory {searchDir}: {ex.Message}");
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Test method to verify directory resolution works correctly (for debugging)
        /// </summary>
        [Test]
        public void TestDirectoryResolution()
        {
            TestContext.WriteLine($"Project Root: {ProjectTestDir}");
            TestContext.WriteLine($"Current Test Dir: {CurrentTestDir}");
            TestContext.WriteLine($"Current Class: {CurrentClassName}");
            TestContext.WriteLine($"Class Source File Path: {_classSourceFilePath ?? "Not Found"}");
            TestContext.WriteLine($"Class Name: {_className ?? "Not Set"}");
            
            // Verify project root contains the expected files
            Assert.That(File.Exists(Path.Combine(ProjectTestDir, "HSFSchedulerUnitTest.csproj")), Is.True, "Project file should exist in project root");
            Assert.That(File.Exists(Path.Combine(ProjectTestDir, "SchedulerUnitTest.cs")), Is.True, "Base class file should exist in project root");
            
            // Verify current directory exists
            Assert.That(Directory.Exists(CurrentTestDir), Is.True, "Current test directory should exist");
            
            // Verify class source file was found and exists
            if (!string.IsNullOrEmpty(_classSourceFilePath))
            {
                Assert.That(File.Exists(_classSourceFilePath), Is.True, "Class source file should exist at the found path");
            }
        }
        */
        #endregion


    }
}
