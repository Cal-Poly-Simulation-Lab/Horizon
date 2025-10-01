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

        private void BuildProgram()
        {
            // Load the program to get the system and tasks
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            // SimParameters are read-only, use the values from the loaded program
            double simEnd = SimParameters.SimEndSeconds;
            double simStep = SimParameters.SimStepSeconds;
            double simStart = SimParameters.SimStartSeconds;

            // GenerateSchedules() Method Flow Stop #1: Initialize Empty Shchedule
            Scheduler.InitializeEmptySchedule(_systemSchedules, program.InitialSysState); // Create the empty schedule and add it to the systemSchedules list

            // GenerateSchedules() Method Flow Stop #2: Generate all default schedule combos
            _scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(program.SimSystem, program.SystemTasks, _scheduleCombos, simStart, simEnd);

        }

        [Test, Order(1)]
        public void EmptySchedule_CanAddTasks_ReturnsTrue_TwoAssetThreeTask()
        {
            // Have to call the build manually
            BuildProgram();
            //Now that we have generated the exhaustive system schedules, we can be at the CURRENT TIME: SIM START, and execute the first CropToMaxSchedules call.:
            _systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, Scheduler.emptySchedule, program.SchedEvaluator); //bump


            // Define the empty Schedule. It is the first one in Scheduler.systemSchedules after InitializeEmptyShecule() has been called. 
            var _emptySchedule = _systemSchedules[0];

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
                Assert.IsTrue(_newAccessStack.Count() == 1, "The access stack should have one access");
                Assert.IsTrue(_newAccessStack.First().Asset.Name.ToLower() == "testasset1", "The asset should be TestAsset1 (case in-sensitive).");
                Assert.IsTrue(_newAccessStack.First().Task.Name.ToLower() == "task1", "The task should be Task1 (case in-sensitive).");
                Assert.IsTrue(_newAccessStack.First().Task.MaxTimesToPerform == 1, "The task should have a MaxTimesToPerform of 1");

                // The first call should return true
                Assert.IsTrue(_sched.CanAddTasks(_newAccessStack, currentTime), "The empty schedule should always allow task addition; given the MaxTimesToPerform == 1 .... INFO: AccessStack {k},");
                Assert.That(_sched.AllStates.timesCompletedTask(_newAccessStack.First().Task), Is.EqualTo(1), "The task should have been completed once given the first call to CanAddTasks.");
                // The second call should return false
                Assert.IsFalse(_sched.CanAddTasks(_newAccessStack, currentTime), "The empty schedule should not allow task addition, given the MaxTimesToPerform > 0 .... INFO: AccessStack {k},");
                Assert.That(_sched.AllStates.timesCompletedTask(_newAccessStack.First().Task), Is.EqualTo(2), "The task should have been completed twice given the second call to CanAddTasks.");
            });
            }


    

        // [TestCase("OneAssetTestModel.json", "ThreeTaskTestInput_ThreeTimesMax.json")]
        // public void MaxTimesToPerform_TestFalse(string _modelFile, string _taskFile){

        // }

    }    
}