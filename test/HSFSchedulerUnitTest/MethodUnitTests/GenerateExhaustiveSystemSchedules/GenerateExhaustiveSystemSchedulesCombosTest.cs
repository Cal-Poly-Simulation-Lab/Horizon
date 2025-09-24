using Microsoft.VisualStudio.TestPlatform.TestHost;
using Horizon;
using Utilities;
using NUnit.Framework.Internal;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using System.Runtime.InteropServices.Marshalling;
using log4net;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Transactions;
using UserModel;

namespace HSFSchedulerUnitTest
{
    [TestFixture]
    public class GenerateExhaustiveSystemSchedulesCombosTest : SchedulerUnitTest
    {
        // private SystemClass? _testSimSystem;
        // private Stack<MissionElements.Task>? _testSystemTasks;

        [SetUp]
        public void SetupDefaultExhaustiveTest()
        {
            // // Use the existing test files for the 1 asset, 3 tasks scenario
            SimInputFile = Path.Combine(Utilities.DevEnvironment.GetTestDirectory(), "HSFSchedulerUnitTest", "SchedulerTestSimulationInput.json"); // Bulletproof path to default simulation input
            TaskInputFile = Path.Combine(CurrentTestDir, "ThreeTaskTestInput.json");
            ModelInputFile = Path.Combine(CurrentTestDir, "OneAssetTestModel.json");

            //Console.WriteLine($"  Setting up GenerateExhaustiveSystemSchedules test with {SimInputFile}");
            TestContext.WriteLine($"\n{CurrentTestName}--->Setting up test with -s {SimInputFile}, -t {TaskInputFile}, -m  {ModelInputFile}\n");
        }

        [TearDown]
        public void GenerateExhaustiveCombosTearDown()
        {
            // Reset the main program instance - this is the most important one
            program = new Horizon.Program();
            
            // Reset program-related attributes
            _testSimSystem = null;
            _testSystemTasks = null;
            
            // Clear all collection attributes (they're initialized as new instances)
            _systemSchedules.Clear();
            _scheduleCombos.Clear();
            _potentialSystemSchedules.Clear();
            _systemCanPerformList.Clear();
            
            // Reset nullable attributes
            _canPregenAccess = null;
            _preGeneratedAccesses = null;
            _schedEvaluator = null;
            _emptySchedIdx = null;
            
            // File paths can stay - they get set by [SetUp] anyway
            // SimInputFile, TaskInputFile, ModelInputFile don't need resetting
            
            TestContext.WriteLine($"=~==~==~= Test {CurrentClassName}.{CurrentTestName} TeraDown Completed =~==~==~=\n");
        }

        private void BuildProgram()
        {
            // Load the program to get the system and tasks
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            // Create (a copy of) the system and tasks for testing -- These are created by the program, under program.SimSystem and program.SystemTasks
            _testSimSystem = new SystemClass(program.AssetList, program.SubList, program.ConstraintsList, program.SystemUniverse);
            _testSystemTasks = new Stack<MissionElements.Task>(program.SystemTasks);

            // Initialize the (test-internal) _scheduleCombos stack, in preparation for the method call
            _scheduleCombos = new Stack<Stack<Access>>();
        }

        [TestCase("OneAssetTestModel.json", "ThreeTaskTestInput.json", 1, 3, 3)]
        [TestCase("TwoAssetTestModel.json", "ThreeTaskTestInput.json", 2, 3, 9)]
        [TestCase("TwoAssetTestModel.json", "SixteenTaskTestInput.json", 2, 16, 16*16 )]
        public void TestNumberTotalCombosGenerated(string _modelInputFile, string _taskInputFile, int _numAssets, int _numTasks, int _expectedResult)
        {

            // Reset the program to not add assets and tasks in on accident. This is also taken care of in TearDown(), which fixed this
            // bug, but leaving it here too to be obivous! 
            this.program = new Horizon.Program();

            // Set the proper (variable) test case input files
            TaskInputFile = Path.Combine(CurrentTestDir, _taskInputFile);
            ModelInputFile = Path.Combine(CurrentTestDir, _modelInputFile);

            //Manually call the setup:
            BuildProgram();

            double currentTime = 0.0;
            double endTime = 60.0;

            // Call the Method
            var result = Scheduler.GenerateExhaustiveSystemSchedules(_testSimSystem, _testSystemTasks, _scheduleCombos, currentTime, endTime);

            // Assert
            Assert.Multiple(() =>
            {
                // Ensure criteria is correct before testing 
                Assert.That(program.SystemTasks.Count, Is.EqualTo(_numTasks), "Should have 3 tasks in this test context."); //Three Tasks in both cases
                Assert.That(program.AssetList.Count, Is.EqualTo(_numAssets), "Should have  asset in this test context.");

                // Main Assertion: Schedule combos; should be Tasks^assets
                Assert.That(result.Count, Is.EqualTo(_expectedResult), "Should return exactly 3 combinations for 1 asset and 3 tasks");

                // Assert.Pass Message out to Console:
                // Assert.Pass($"Successfully generated {result.Count} schedule combos for {_numAssets} assets and {_numTasks} tasks");
            });

            // Write to TextContext Console:
            TestContext.WriteLine($"\n--->{CurrentClassName}--->{CurrentTestName}---> passed!\n");
        }

        [Test]
        public void TestAccessStartTime()
        {
            // Build the program (no longer can do in setup)
            BuildProgram();

            //This input file (used in SetUp()) should start at 0.0 seconds. 
            double _simStart_TestInput = 0.0;

            double startTime = SimParameters.SimStartSeconds;
            double endTime = SimParameters.SimEndSeconds;


            // Call the Method
            var result = Scheduler.GenerateExhaustiveSystemSchedules(_testSimSystem, _testSystemTasks, _scheduleCombos, startTime, endTime);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(SimParameters.SimStartSeconds, Is.EqualTo(_simStart_TestInput), "Esnure the SimStart time is equal to the test case expected Result. Otherwise, may be a bug in the Simluation file and/or the unit test.");
                foreach (var accessStack in _scheduleCombos)
                {
                    foreach (var access in accessStack)
                    {
                        Assert.That(access.AccessStart, Is.EqualTo(_simStart_TestInput), "Default behavior is to have Access open the entire simulation, from start to end.");
                    }
                }
            });

            // Write to TextContext Console:
            TestContext.WriteLine($"\n--->{CurrentClassName}--->{CurrentTestName}---> passed!\n");
        }

        [Test]
        public void TestAccessEndTime()
        {
            // Build the program (no longer can do in setup)
            BuildProgram();

            //This input file (used in SetUp()) should start at 0.0 seconds. 
            double _simEnd_TestInput = 60.0;

            double startTime = SimParameters.SimStartSeconds;
            double endTime = SimParameters.SimEndSeconds;


            // Call the Method
            var result = Scheduler.GenerateExhaustiveSystemSchedules(_testSimSystem, _testSystemTasks, _scheduleCombos, startTime, endTime);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(SimParameters.SimEndSeconds, Is.EqualTo(_simEnd_TestInput), "Esnure the SimEnd time is equal to the test case expected Result. Otherwise, may be a bug in the Simluation file and/or the unit test.");
                foreach (var accessStack in _scheduleCombos)
                {
                    foreach (var access in accessStack)
                    {
                        Assert.That(access.AccessEnd, Is.EqualTo(_simEnd_TestInput), "Default behavior is to have Access open the entire simulation, from start to end.");
                    }
                }
            });

            // Write to TextContext Console:
            TestContext.WriteLine($"\n--->{CurrentClassName}--->{CurrentTestName}---> passed!\n");    
        }

        
    }
}