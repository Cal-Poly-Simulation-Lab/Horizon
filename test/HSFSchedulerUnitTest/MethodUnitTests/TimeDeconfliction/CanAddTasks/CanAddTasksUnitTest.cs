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
            SimInputFile = "InputFiles/SchedulerTestSimulationInput.json";
            TaskInputFile = Path.Combine(ProjectTestDir, "InputFiles", "ThreeTaskTestInput.json");
            ModelInputFile = Path.Combine(ProjectTestDir, "InputFiles", "TwoAssetTestModel.json");

            // Load the program to get the system and tasks
            BuildProgram();
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

            // Genera
            //Now that we have generated the exhaustive system schedules, we can be at the CURRENT TIME: SIM START, and execute the first CropToMaxSchedules call.:
            _systemSchedules = program.scheduler.CropToMaxSchedules(_systemSchedules, Scheduler.emptySchedule);
            // Later, we will have to manually propagate the currentTime to the nextTime for the next call to CropToMaxSchedules.

        }

        [Test, Order(1)]
        public void EmptySchedule_CanAddTasks_ReturnsTrue()
        {
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

        

    }    
}