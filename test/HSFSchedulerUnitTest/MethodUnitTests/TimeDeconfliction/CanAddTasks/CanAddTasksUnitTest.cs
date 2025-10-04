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
    public class CanAddTasksUnitTest : SchedulerUnitTest
    {
        protected override string SimInputFile { get; set; } = "InputFiles/SchedulerTestSimulationInput.json";
        protected override string TaskInputFile { get; set; } = Path.Combine(ProjectTestDir, "InputFiles", "ThreeTaskTestInput.json");
        protected override string ModelInputFile { get; set; } = Path.Combine(ProjectTestDir, "InputFiles", "TwoAssetTestModel.json");

        // private SystemClass? testSystem;
        // private Stack<MissionElements.Task>? testTasks;
        // private SystemSchedule? testSchedule;
        // private Asset? testAsset;
        // private MissionElements.Task? testTask;
        private double currentTime = SimParameters.SimStartSeconds;
        private double endTime = SimParameters.SimEndSeconds;
        private double nextTime = SimParameters.SimStartSeconds + SimParameters.SimStepSeconds;

        [SetUp]
        public void SetupDefaults()
        {
            // Use the existing test files for the 1 asset, 3 tasks scenario
            // SimInputFile = "InputFiles/SchedulerTestSimulationInput.json";
            //TaskInputFile = Path.Combine(ProjectTestDir, "InputFiles", "ThreeTaskTestInput.json");
            // ModelInputFile = Path.Combine(ProjectTestDir, "InputFiles", "TwoAssetTestModel.json");

            // Load the program to get the system and tasks
            // BuildProgram();
        }

        [TearDown]
        public void ResetSchedulerAttributes()
        {
            // Reset static Scheduler attributes that mirror the Scheduler class
            SchedulerStep = -1;
            CurrentTime = SimParameters.SimStartSeconds;
            NextTime = SimParameters.SimStartSeconds + SimParameters.SimStepSeconds;
            _schedID = 0;
            _SchedulesGenerated = 0;
            _SchedulesCarriedOver = 0;
            _SchedulesCropped = 0;
            _emptySchedule = null;

            // Reset instance attributes
            _systemSchedules.Clear();
            _canPregenAccess = false;
            _scheduleCombos.Clear();
            _preGeneratedAccesses = null;
            _potentialSystemSchedules.Clear();
            _systemCanPerformList.Clear();
            _ScheduleEvaluator = null;

            // Reset program attributes
            program = new Horizon.Program();
            _testSimSystem = null;
            _testSystemTasks.Clear();
            _testInitialSysState = new SystemState();

            // Reset local test attributes
            currentTime = SimParameters.SimStartSeconds;
            endTime = SimParameters.SimEndSeconds;
            nextTime = SimParameters.SimStartSeconds + SimParameters.SimStepSeconds;
        }

        private void BuildProgram()
        {
            // Load the program to get the system and tasks
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            // SimParameters are read-only, use the values from the loaded program
            double simEnd = SimParameters.SimEndSeconds;
            double simStep = SimParameters.SimStepSeconds;
            double simStart = SimParameters.SimStartSeconds;

            // GenerateSchedules() Method Flow Stop #1: Initialize Empty Shchedule
            Scheduler.InitializeEmptySchedule(_systemSchedules, _testInitialSysState); // Create the empty schedule and add it to the systemSchedules list
            SchedulerUnitTest._emptySchedule = Scheduler.emptySchedule;
            //Sccheduler.InitializeEmptySchedule(_systemSchedules, program.InitialSysState); // Create the empty schedule and add it to the systemSchedules list

            // Make sure the Test Attributes and Program Attributes are loaded together
            // GenerateSchedules() Method Flow Stop #2: Generate all default schedule combos
            //program.scheduler.scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(program.SimSystem, program.SystemTasks, program.scheduler.scheduleCombos, simStart, simEnd);
            _scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(_testSimSystem, _testSystemTasks, _scheduleCombos, simStart, simEnd);

        }

        [Test, Order(1)]
        public void EmptySchedule_CanAddTasks_ReturnsTrue_TwoAssetThreeTask()
        {
            // Have to call the build manually
            BuildProgram();
            //Now that we have generated the exhaustive system schedules, we can be at the CURRENT TIME: SIM START, and execute the first CropToMaxSchedules call.:
            _systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, Scheduler.emptySchedule, program.SchedEvaluator); //bump
            var _emptySchedule = _systemSchedules[0]; // Define the empty Schedule. It is the first one in Scheduler.systemSchedules after InitializeEmptyShecule() has been called. 

            Assert.Multiple(() =>
            {
                // Just a copy of the empty schedule test... But Oh well, we can test it here too.
                Assert.IsTrue(_systemSchedules.Count() == 1, "Assert 0a: The system schedules list should have one schedule after the empty schedule is initialized.");
                Assert.IsTrue(_systemSchedules[0].Name == "Empty Schedule", "Assert 0b: The empty schedule should be named 'Empty Schedule'.");
                Assert.IsTrue(_systemSchedules[0].AllStates.Events.Count() == 0, "Assert 0c: The empty schedule should have no events.");

                //
                // CurrentTime here is the Start Time of the Simulation, 0.0, as set in the initialziation of the attributes of this class. 
                int k = 0;
                foreach (var _newAccessStack in _scheduleCombos)
                {
                    // Ensure that EVERY Task has MaxTimesToPerform > 0. 
                    int a = 0; // Iterator to track asset
                    foreach (var access in _newAccessStack)
                    {
                        Assert.IsTrue(access.Task.MaxTimesToPerform > 0,
                            $"AccessStack {k}, Access {a}: Task {access.Task.Name}: MaxTimesToPerform, {access.Task.MaxTimesToPerform} must be greater than 0 .... " +
                            $"INFO: {access.Asset.Name}_to_{access.Task.Target.Name}. ");
                        a++;
                    }
                    // Call CanAddTasks() forn the empty schedule across all schedule combos. 
                    Assert.IsTrue(_emptySchedule.CanAddTasks(_newAccessStack, currentTime), $"The empty schedule should always allow task addition, given the MaxTimesToPerform > 0 .... INFO: AccessStack {k},");
                    k++;
                }
            });

        }
        [Test, Order(2)]
        public void OneAssetOneTask_FirstIterationReturnsTrue()
        {
            // Set Inputs and call the build program
            ModelInputFile = Path.Combine(CurrentTestDir, "OneAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "OneTaskTestFile_CanAddTasks.json");
            BuildProgram();
            //Now that we have generated the exhaustive system schedules, we can be at the CURRENT TIME: SIM START, and execute the first CropToMaxSchedules call.:
            _systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, Scheduler.emptySchedule, program.SchedEvaluator); //bump


            var _sched = _systemSchedules[0]; // This is the empty schedule here
            var _newAccessStack = _scheduleCombos.First(); // This is the one and only 


            Assert.Multiple(() =>
            {
                //First Ensure that there is only one task and one asset and that they have been loaded properly.
                Assert.IsTrue(_newAccessStack.Count() == 1, "The access stack should have one access"); //falining
                Assert.IsTrue(_newAccessStack.First().Asset.Name.ToLower() == "testasset1", "The asset should be TestAsset1 (case in-sensitive).");
                Assert.IsTrue(_newAccessStack.First().Task.Name.ToLower() == "task1", "The task should be Task1 (case in-sensitive).");
                Assert.IsTrue(_newAccessStack.First().Task.MaxTimesToPerform == 1, "The task should have a MaxTimesToPerform of 1");

                // The first call should return true
                Assert.IsTrue(_sched.CanAddTasks(_newAccessStack, currentTime), "The empty schedule should always allow task addition; given the MaxTimesToPerform == 1 .... INFO: AccessStack {k},");
                Assert.That(_sched.AllStates.timesCompletedTask(_newAccessStack.First().Task), Is.EqualTo(0), "The timesCompletedTask should return 0 since it has not been added to an Event yet, and would not yet exist in this potential schedule's StateHistory."); // failing
            });
        }

        [Test, Order(3)]
        public void OneAssetOneTask_SecondIterationReturnsFalse()
        {
            // Set Inputs and call the build program
            ModelInputFile = Path.Combine(CurrentTestDir, "OneAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "OneTaskTestFile_CanAddTasks.json");
            BuildProgram();

            double currentTime = 0.0;
            double timeStep = 12.0;
            int iterations = 1;
            // Main Scheduling Loop Helper: Make sure to use all the mirrored attributes for the SchedulerUnitTest class. 
            this._systemSchedules = SchedulerUnitTest.MainSchedulingLoopHelper(_systemSchedules, _scheduleCombos, _testSimSystem,
                                                        _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                                                        currentTime, timeStep, iterations);

            // Start the second iteration before CanAddTasks: 
            _systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, SchedulerUnitTest._emptySchedule, _ScheduleEvaluator);

            // Now we would enter Time Deconfliction Step:
            Assert.Multiple(() =>
            {
                // Ensure that the schedule Parameters are correct here:
                Assert.That(SchedParameters.MaxNumScheds, Is.EqualTo(100), "The max number of schedules should be 100 per the input file.");
                Assert.That(_scheduleCombos.Count(), Is.EqualTo(1), "The schedule combos should have only one access stack given it is only one asset and one task.");
                Assert.That(_systemSchedules.Count(), Is.EqualTo(2), $"The total system schedules after {iterations} should be {2 ^ iterations}.");

                // int i = 0; 
                foreach (var _oldSystemSchedule in _systemSchedules)
                {
                    foreach (var _newAccessStack in _scheduleCombos)
                    {
                        Assert.IsTrue(_newAccessStack.Count() == 1, "The access stack should have one access given it is only one asset."); //falining
                        Assert.IsTrue(_newAccessStack.First().Asset.Name.ToLower() == "testasset1", "The asset should be TestAsset1 (case in-sensitive).");
                        Assert.IsTrue(_newAccessStack.First().Task.Name.ToLower() == "task1", "The task should be Task1 (case in-sensitive).");
                        Assert.IsTrue(_newAccessStack.First().Task.MaxTimesToPerform == 1, "The task should have a MaxTimesToPerform of 1");

                        if (_oldSystemSchedule.Name.ToLower().Contains("empty"))
                        {
                            // This is the empty schedule:
                            Assert.That(_oldSystemSchedule.AllStates.Events.Count(), Is.EqualTo(0), "The empty schedule should have no events.");
                            Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack, currentTime), "The empty schedule should always allow task addition; given the MaxTimesToPerform > 1. (This is because there are no matching Tasks in the StateHistory as there is no StateHistory for the EmptySchedule).,");
                        }
                        else
                        {
                            // This is all other schedules (with StateHistory):
                            Assert.That(_oldSystemSchedule.AllStates.Events.Count(), Is.EqualTo(1), "The schedule should have one event (asset1-->target1).");
                            Assert.That(_oldSystemSchedule.AllStates.timesCompletedTask(_newAccessStack.First().Task), Is.EqualTo(1), "The task should have been completed once.");
                            Assert.IsFalse(_oldSystemSchedule.CanAddTasks(_newAccessStack, currentTime), "The schedule should not allow task addition; given the MaxTimesToPerform = 1. (This is because there is a matching Task in the StateHistory as there is a StateHistory for the Non-EmptySchedule).,");
                        }
                    }
                }
            });


        } // End Test

        [Test, Order(4)]
        public void OneAssetThreeTask_ThreeMaxTimes_ThirdIterationAlwaysTrue()
        {
            // Set Inputs and call the build program
            ModelInputFile = Path.Combine(CurrentTestDir, "OneAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "ThreeTaskTestInput_ThreeTimesMax.json");
            BuildProgram();     

            double currentTime = SimParameters.SimStartSeconds; // 0.0s
            double timeStep = SimParameters.SimStepSeconds; // 12.0s
            int iterations = 2;
            // Main Scheduling Loop Helper: Make sure to use all the mirrored attributes for the SchedulerUnitTest class. 
            this._systemSchedules = SchedulerUnitTest.MainSchedulingLoopHelper(_systemSchedules, _scheduleCombos, _testSimSystem,
                                                        _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                                                        currentTime, timeStep, iterations);
      
            // Start the second iteration before CanAddTasks: 
            _systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, SchedulerUnitTest._emptySchedule, _ScheduleEvaluator);
            double thirdStepTime = currentTime + (timeStep*iterations+1); // This is the current Time
            
            // Now Time Deconfliction is Stepped into... 
            Assert.Multiple(()=>
            {
                // Ensure things are loaded in correctly, and sched parametr and schedule combos are as expected:
                Assert.That(SchedParameters.MaxNumScheds, Is.EqualTo(100), "The max number of schedules should be 100 per the input file.");
                Assert.That(_testSimSystem.Assets.Count(), Is.EqualTo(1), "There should be one (1) asset loaded in this test simulation.");
                Assert.That(_testSystemTasks.Count(), Is.EqualTo(3), "There should be three (3) tasks loaded in this test simulation.");
                Assert.That(_scheduleCombos.Count(), Is.EqualTo(_testSystemTasks.Count()^_testSimSystem.Assets.Count()), "The schedule combo is three given 1 task and 3 asset");
                foreach (var task in _testSystemTasks)
                { Assert.That(task.MaxTimesToPerform, Is.EqualTo(3), "It should be three (3) times max to perform for each Task."); }
                
                foreach (var _oldSystemSchedule in _systemSchedules)
                {
                    foreach (var _newAccessStack in _scheduleCombos)
                    {
                        Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack,thirdStepTime));
                    }
                }
            });
        }
    } // End Class

}    // End Namespace