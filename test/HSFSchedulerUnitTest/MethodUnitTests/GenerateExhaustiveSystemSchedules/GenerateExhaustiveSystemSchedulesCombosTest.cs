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
        private string? DefaultSimInputFile;
        private string? DefaultModelInputFile;
        private string? DefaultTaskInputFile;
        private SystemClass? testSystem;
        private Stack<MissionElements.Task>? testTasks;

        [SetUp]
        public void SetupDefaultExhaustiveTest()
        {
            // // Use the existing test files for the 1 asset, 3 tasks scenario
            SimInputFile = Path.Combine(Utilities.DevEnvironment.GetTestDirectory(), "HSFSchedulerUnitTest", "SchedulerTestSimulationInput.json"); // Bulletproof path to default simulation input
            TaskInputFile = Path.Combine(CurrentTestDir, "ThreeTaskTestInput.json");
            ModelInputFile = Path.Combine(CurrentTestDir, "OneAssetTestModel.json");

            //Console.WriteLine($"  Setting up GenerateExhaustiveSystemSchedules test with {SimInputFile}");
            TestContext.WriteLine($"\n{CurrentTestName}--->Setting up test with -s {SimInputFile}, -t {TaskInputFile}, -m  {ModelInputFile}\n");

            // Load the program to get the system and tasks & Create the system and tasks for testing      
            BuildProgram();
        }

        private void BuildProgram()
        {
            // Load the program to get the system and tasks
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);


            // Create (a copy of) the system and tasks for testing -- These are created by the program, under program.SimSystem and program.SystemTasks
            testSystem = new SystemClass(program.AssetList, program.SubList, program.ConstraintsList, program.SystemUniverse);
            testTasks = new Stack<MissionElements.Task>(program.SystemTasks);

            // Initialize the scheduleCombos stack, in preparation for the method call
            scheduleCombos = new Stack<Stack<Access>>();
        }

        [TestCase("OneAssetTestModel.json", "ThreeTaskTestInput.json", 1, 3, 3)]
        [TestCase("TwoAssetTestModel.json", "ThreeTaskTestInput.json", 2, 3, 9)]
        public void TestNumberTotalCombosGenerated(string _modelInputFile, string _taskInputFile, int _numAssets, int _numTasks, int _expectedResult)
        {
            // Set the proper (variable) test case input files
            TaskInputFile = Path.Combine(CurrentTestDir, _taskInputFile);
            ModelInputFile = Path.Combine(CurrentTestDir, _modelInputFile);

            //Manually call the setup:
            BuildProgram();

            double currentTime = 0.0;
            double endTime = 60.0;

            // Call the Method
            var result = Scheduler.GenerateExhaustiveSystemSchedules(testSystem, testTasks, scheduleCombos, currentTime, endTime);

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
            //This input file (used in SetUp()) should start at 0.0 seconds. 
            double _simStart_TestInput = 0.0;

            double startTime = SimParameters.SimStartSeconds;
            double endTime = SimParameters.SimEndSeconds;


            // Call the Method
            var result = Scheduler.GenerateExhaustiveSystemSchedules(testSystem, testTasks, scheduleCombos, startTime, endTime);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(SimParameters.SimStartSeconds, Is.EqualTo(_simStart_TestInput), "Esnure the SimStart time is equal to the test case expected Result. Otherwise, may be a bug in the Simluation file and/or the unit test.");
                foreach (var accessStack in scheduleCombos)
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
        public void TestAccessSEndTime()
        {
            //This input file (used in SetUp()) should start at 0.0 seconds. 
            double _simEnd_TestInput = 60.0;

            double startTime = SimParameters.SimStartSeconds;
            double endTime = SimParameters.SimEndSeconds;


            // Call the Method
            var result = Scheduler.GenerateExhaustiveSystemSchedules(testSystem, testTasks, scheduleCombos, startTime, endTime);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(SimParameters.SimEndSeconds, Is.EqualTo(_simEnd_TestInput), "Esnure the SimEnd time is equal to the test case expected Result. Otherwise, may be a bug in the Simluation file and/or the unit test.");
                foreach (var accessStack in scheduleCombos)
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