using Microsoft.VisualStudio.TestPlatform.TestHost;
using Horizon;
using Utilities;
using NUnit.Framework.Internal;
using NUnit.Framework.Interfaces;
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
using System.Diagnostics;

namespace HSFSchedulerUnitTest
{
    [TestFixture]
    public class SystemSchedulerConstructorUnitTest : SchedulerUnitTest
    {
        # region Derived-Class Attributes
        private Stack<Access> accessStack = new Stack<Access>();
        private Asset? asset1 { get; set; }
        private MissionElements.Task? task1 { get; set; }
        #endregion

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Validate that GenerateExhaustiveSystemSchedules is working correctly:
            // Need to implement. Past implementation with NUnit.Engine (aimed to internally run other tests) broke the Assembly Class calls
            // used in the ScriptedSubsystemCS.cs somehow with .NET dependency madness and like... we just dont fuck with that. No need to fully understand.
            // I underestand perfectly sir, we can add that feature later in another way, perhaps using reflection. 
        }


        [SetUp]
        public void SetupDefaultExhaustiveTest()
        {
            // Optional: Log the resolved path for debugging
            TestContext.WriteLine($"Test directory resolved to: {CurrentTestDir}");

            // // Use the existing test files for the 1 asset, 3 tasks scenario
            SimInputFile = Path.Combine(ProjectTestDir, "InputFiles", "SchedulerTestSimulationInput.json"); // Bulletproof path to default simulation input
            TaskInputFile = Path.Combine(CurrentTestDir, "DefaultThreeTaskInput.json");
            ModelInputFile = Path.Combine(CurrentTestDir, "DefaultOneAssetModelInput.json");

            // Load the program to get the system and tasks & Create the system and tasks for testing      
            BuildProgram();

            /* Foreword:
                Let us remind ourselves that this method is tested using a stack because it will loop through all assets in the asset list.
                AKA: The schedule combos is generates is a Stac<Stack<Access>> Because that lower stack is one acces for each asset. 
            */

        }

        private void BuildProgram()
        {
            // Load the program to get the system and tasks
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            //Schedules = this.scheduler.GenerateSchedules(SimSystem, SystemTasks, InitialSysState);

            // SimParameters are read-only, use the values from the loaded program
            double simEnd = SimParameters.SimEndSeconds;
            double simStep = SimParameters.SimStepSeconds;
            double simStart = SimParameters.SimStartSeconds;

            Scheduler.InitializeEmptySchedule(_systemSchedules, program.InitialSysState); // Create the empty schedule and add it to the systemSchedules list
            _scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(program.SimSystem, program.SystemTasks, _scheduleCombos, simStart, simEnd);
            _systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, Scheduler.emptySchedule, program.SchedEvaluator); // bump


        }

        [TearDown]
        public void TearDownCommonVariables()
        {
            // Clear the access stack to remove any Access objects from previous tests
            accessStack.Clear();

            // Reset the asset and task properties to null
            asset1 = null;
            task1 = null;
        }

        # region Tests
        [Test, Order(1)]
        public void FullAccessTest_0_60s()
        {
            // Get some time properties:
            var currentTime = SimParameters.SimStartSeconds;
            var endTime = SimParameters.SimEndSeconds;
            var stepTime = SimParameters.SimStepSeconds;

            // Lets create the next step on the fundamnetal timestep
            var nextTime = currentTime + stepTime;

            // Draw an arbitrary Asset and Task to start creating Accesses with the values we want. 
            asset1 = program.AssetList[0];
            task1 = program.SystemTasks.Peek();
            var stateHistory = new StateHistory(_systemSchedules[0].AllStates); // Draw the StateHistory fromt he program-instantiated _systemSchedules 
                                                                                // Here, it should be the empty schedule, so a list of 0 Events will be copied over.

            // We Use a Stack of Access (length one) because on the internal it will go through the stack, in the case that there are multiple assets.
            // This constructor still only creates one schedule per asset/system (per access). 
            accessStack.Push(new Access(asset1, task1, currentTime, endTime));

            // Create the new schedule
            var newSysSchedule = new SystemSchedule(stateHistory, accessStack, 0.0);
            var result = newSysSchedule.AllStates.Events.Peek();
            //Assert the result
            Assert.Multiple(() =>
            {
                #region Assert 0:
                //Access must start at 0/currentTime here
                Assert.That(accessStack.Peek().AccessStart, Is.EqualTo(currentTime), $"Asset 0a. AccesStart within the stack must be the SimStartTime, {SimParameters.SimEndSeconds}, and currentTime, {currentTime}s");
                Assert.That(accessStack.Peek().AccessEnd, Is.EqualTo(endTime), $"Asset 0b. AccessEnd within the stack must be the SimEndTime, {endTime}s");
                #endregion

                #region Assert 1:
                // Assert 1a: that Event Start will always be the start of the fundamental timestep.
                Assert.That(result.EventStarts[asset1], Is.EqualTo(currentTime), $"Asset 1a: Event Start should always be on the fundamental timestep " +
                    $"(for Default functionality). The current time, {currentTime}, should be the same as Event Start.");
                // Assert 1b: that Event End will always be end of the fundamental timestep. 
                Assert.That(result.EventEnds[asset1], Is.EqualTo(nextTime), $"Assert 1b: Event Ends should always be on the fundamental timestep " +
                    $"(for Default functionality). The current time, {nextTime}, should be the same as Event Start.");
                #endregion

                # region Assert 2:
                // Assert 2a: that Task Start will always be end of the fundamental timestep. 
                Assert.That(result.TaskStarts[asset1], Is.EqualTo(currentTime), $"Assert 2a: Task Start should be at the earliest access within the Event at hand. " +
                    $"Because the Access spans the entire simulation time, this should be equivalent to the current time, {currentTime}, which reflects the Event Start time (as well).");

                // Assert 2b: that Task End will always be end of the fundamental timestep. 
                Assert.That(result.TaskEnds[asset1], Is.EqualTo(stepTime), $"Assert 2b: Task End should be at the latest accessible time within the Event at hand. " +
                    $"Because the Access spans the entire simulation time, this should be equivalent to the event end time, {nextTime}, which reflects the " +
                    "full step length/start of the next fundamental timestep.");
                #endregion
            });
        }

        [Test, Order(2)]
        public void LateAccessTest_6_60s()
        {
            // Get some time properties:
            var currentTime = SimParameters.SimStartSeconds;
            var endTime = SimParameters.SimEndSeconds;
            var stepTime = SimParameters.SimStepSeconds;

            // Lets create the next step on the fundamnetal timestep
            var nextTime = currentTime + stepTime;

            // Draw an arbitrary Asset and Task to start creating Accesses with the values we want. 
            asset1 = program.AssetList[0];
            task1 = program.SystemTasks.Peek();
            var stateHistory = new StateHistory(_systemSchedules[0].AllStates); // Draw the StateHistory fromt he program-instantiated _systemSchedules 
                                                                                // Here, it should be the empty schedule, so a list of 0 Events will be copied over.

            // We Use a Stack of Access (length one) because on the internal it will go through the stack, in the case that there are multiple assets.
            // This constructor still only creates one schedule per asset/system (per access).
            double accessStart = 6.0;
            accessStack.Push(new Access(asset1, task1, accessStart, endTime));

            // Create the new schedule
            var newSysSchedule = new SystemSchedule(stateHistory, accessStack, 0.0);
            var result = newSysSchedule.AllStates.Events.Peek();
            //Assert the result
            Assert.Multiple(() =>
            {
                #region Assert 0:
                //Access must start at 6.0/currentTime here
                Assert.That(accessStack.Peek().AccessStart, Is.EqualTo(accessStart), $"Asset 0a. AccesStart within the stack must be set to {accessStart}s");
                Assert.That(accessStack.Peek().AccessEnd, Is.EqualTo(endTime), $"Asset 0b. AccessEnd within the stack must be the SimEndTime, {endTime}s");
                #endregion

                #region Assert 1:
                // Assert 1a: that Event Start will always be the start of the fundamental timestep.
                Assert.That(result.EventStarts[asset1], Is.EqualTo(currentTime), $"Asset 1a: Event Start should always be on the fundamental timestep " +
                    $"(for Default functionality). The current time, {currentTime}, should be the same as Event Start.");
                // Assert 1b: that Event End will always be end of the fundamental timestep. 
                Assert.That(result.EventEnds[asset1], Is.EqualTo(nextTime), $"Assert 1b: Event Ends should always be on the fundamental timestep " +
                    $"(for Default functionality). The current time, {nextTime}, should be the same as Event Start.");
                #endregion

                # region Assert 2:
                // Assert 2a: that Task Start will always be end of the fundamental timestep. 
                Assert.That(result.TaskStarts[asset1], Is.EqualTo(accessStart), $"Assert 2a: Task Start should be at the earliest access within the Event at hand. " +
                    $"Because the Access starts a bit late, this should be equivalent to the access start time, {accessStart}.");

                // Assert 2b: that Task End will always be end of the fundamental timestep. 
                Assert.That(result.TaskEnds[asset1], Is.EqualTo(stepTime), $"Assert 2b: Task End should be at the latest accessible time within the Event at hand. " +
                    $"Because the Access spans the rest of thesimulation time, this should be equivalent to the event end time, {nextTime}, which reflects the " +
                    "full start of the next event (fundamental timestep).");
                #endregion
            });
        }

        [Test, Order(3)]
        public void LateAccessTest_6_14s()
        {
            // Get some time properties:
            var currentTime = SimParameters.SimStartSeconds;
            var endTime = SimParameters.SimEndSeconds;
            var stepTime = SimParameters.SimStepSeconds;

            // Lets create the next step on the fundamnetal timestep
            var nextTime = currentTime + stepTime;

            // Draw an arbitrary Asset and Task to start creating Accesses with the values we want. 
            asset1 = program.AssetList[0];
            task1 = program.SystemTasks.Peek();
            var stateHistory = new StateHistory(_systemSchedules[0].AllStates); // Draw the StateHistory fromt he program-instantiated _systemSchedules 
                                                                                // Here, it should be the empty schedule, so a list of 0 Events will be copied over.

            // We Use a Stack of Access (length one) because on the internal it will go through the stack, in the case that there are multiple assets.
            // This constructor still only creates one schedule per asset/system (per access).
            double accessStart = 6.0;
            double accessEnd = 14.0;
            accessStack.Push(new Access(asset1, task1, accessStart, accessEnd));

            // Create the new schedule
            var newSysSchedule = new SystemSchedule(stateHistory, accessStack, 0.0);
            var result = newSysSchedule.AllStates.Events.Peek();
            //Assert the result
            Assert.Multiple(() =>
            {
                #region Assert 0:
                //Access must start at 6.0/currentTime here
                Assert.That(accessStack.Peek().AccessStart, Is.EqualTo(accessStart), $"Asset 0a. AccesStart within the stack must be set to {accessStart}s");
                Assert.That(accessStack.Peek().AccessEnd, Is.EqualTo(accessEnd), $"Asset 0b. AccessEnd within the stack must be {accessEnd}s");
                #endregion

                #region Assert 1:
                // Assert 1a: that Event Start will always be the start of the fundamental timestep.
                Assert.That(result.EventStarts[asset1], Is.EqualTo(currentTime), $"Asset 1a: Event Start should always be on the fundamental timestep " +
                    $"(for Default functionality). The current time, {currentTime}, should be the same as Event Start.");
                // Assert 1b: that Event End will always be end of the fundamental timestep. 
                Assert.That(result.EventEnds[asset1], Is.EqualTo(nextTime), $"Assert 1b: Event Ends should always be on the fundamental timestep " +
                    $"(for Default functionality). The current time, {nextTime}, should be the same as Event Start.");
                #endregion

                # region Assert 2:
                // Assert 2a: that Task Start will always be end of the fundamental timestep. 
                Assert.That(result.TaskStarts[asset1], Is.EqualTo(accessStart), $"Assert 2a: Task Start should be at the earliest access within the Event at hand. " +
                    $"Because the Access starts a bit late, this should be equivalent to the access start time, {accessStart}.");

                // Assert 2b: that Task End will always be end of the fundamental timestep. 
                Assert.That(result.TaskEnds[asset1], Is.EqualTo(stepTime), $"Assert 2b: Task End should be at the latest accessible time within the Event at hand. " +
                    $"Because the Access spans past the end of the event/fundamental timestep, this should be equivalent to the event end time, {nextTime}, which reflects the " +
                    "full start of the next event (fundamental timestep).");
                #endregion
            });
        }

        [Test, Order(4)]
        public void WithinEventAccessTest_6_11s()
        {
            // Get some time properties:
            var currentTime = SimParameters.SimStartSeconds;
            var endTime = SimParameters.SimEndSeconds;
            var stepTime = SimParameters.SimStepSeconds;

            // Lets create the next step on the fundamnetal timestep
            var nextTime = currentTime + stepTime;

            // Draw an arbitrary Asset and Task to start creating Accesses with the values we want. 
            asset1 = program.AssetList[0];
            task1 = program.SystemTasks.Peek();
            var stateHistory = new StateHistory(_systemSchedules[0].AllStates); // Draw the StateHistory fromt he program-instantiated _systemSchedules 
                                                                                // Here, it should be the empty schedule, so a list of 0 Events will be copied over.

            // We Use a Stack of Access (length one) because on the internal it will go through the stack, in the case that there are multiple assets.
            // This constructor still only creates one schedule per asset/system (per access).
            double accessStart = 6.0;
            double accessEnd = 11.0;
            accessStack.Push(new Access(asset1, task1, accessStart, accessEnd));

            // Create the new schedule
            var newSysSchedule = new SystemSchedule(stateHistory, accessStack, 0.0);
            var result = newSysSchedule.AllStates.Events.Peek();
            //Assert the result
            Assert.Multiple(() =>
            {
                #region Assert 0:
                //Access must start at 6.0/currentTime here
                Assert.That(accessStack.Peek().AccessStart, Is.EqualTo(accessStart), $"Asset 0a. AccesStart within the stack must be set to {accessStart}s");
                Assert.That(accessStack.Peek().AccessEnd, Is.EqualTo(accessEnd), $"Asset 0b. AccessEnd within the stack must be {accessEnd}s");
                #endregion

                #region Assert 1:
                // Assert 1a: that Event Start will always be the start of the fundamental timestep.
                Assert.That(result.EventStarts[asset1], Is.EqualTo(currentTime), $"Asset 1a: Event Start should always be on the fundamental timestep " +
                    $"(for Default functionality). The current time, {currentTime}, should be the same as Event Start.");
                // Assert 1b: that Event End will always be end of the fundamental timestep. 
                Assert.That(result.EventEnds[asset1], Is.EqualTo(nextTime), $"Assert 1b: Event Ends should always be on the fundamental timestep " +
                    $"(for Default functionality). The current time, {nextTime}, should be the same as Event Start.");
                #endregion

                # region Assert 2:
                // Assert 2a: that Task Start will always be end of the fundamental timestep. 
                Assert.That(result.TaskStarts[asset1], Is.EqualTo(accessStart), $"Assert 2a: Task Start should be at the earliest access within the Event at hand. " +
                    $"Because the Access starts a bit late, this should be equivalent to the access start time, {accessStart}.");

                // Assert 2b: that Task End will always be end of the fundamental timestep. 
                Assert.That(result.TaskEnds[asset1], Is.EqualTo(accessEnd), $"Assert 2b: Task End should be at the latest accessible time within the Event at hand. " +
                    $"Because the Access ends within the event, this should be equivalent to the access end time, {accessEnd}s. ");
                #endregion
            });
        }

        // This is actually going to be taken care of by testing the Pre-generation or Scripted-generation of Accesses.
        // In short, there should not be accesses here that dont start within the event time. End should not count either. 
        // [Test, Order(4)]
        // public void AccessPastEventTest_13_20s()
        // {
        //     // Not sure if this will ever be able to happen, but we can test for the case where Access is 0-0s. 

        //     // Get some time properties:
        //     var currentTime = SimParameters.SimStartSeconds;
        //     var endTime = SimParameters.SimEndSeconds;
        //     var stepTime = SimParameters.SimStepSeconds;

        //     // Lets create the next step on the fundamnetal timestep
        //     var nextTime = currentTime + stepTime;

        //     // Draw an arbitrary Asset and Task to start creating Accesses with the values we want. 
        //     asset1 = program.AssetList[0];
        //     task1 = program.SystemTasks.Peek();
        //     var stateHistory = new StateHistory(_systemSchedules[0].AllStates); // Draw the StateHistory fromt he program-instantiated _systemSchedules 
        //                                                                         // Here, it should be the empty schedule, so a list of 0 Events will be copied over.

        //     // We Use a Stack of Access (length one) because on the internal it will go through the stack, in the case that there are multiple assets.
        //     // This constructor still only creates one schedule per asset/system (per access).
        //     double accessStart = 6.0;
        //     double accessEnd = 11.0;
        //     accessStack.Push(new Access(asset1, task1, accessStart, accessEnd));

        //     // Create the new schedule
        //     var newSysSchedule = new SystemSchedule(stateHistory, accessStack, 0.0);
        //     var result = newSysSchedule.AllStates.Events.Peek();
        //     //Assert the result
        //     Assert.Multiple(() =>
        //     {
        //         #region Assert 0:
        //         //Access must start at 6.0/currentTime here
        //         Assert.That(accessStack.Peek().AccessStart, Is.EqualTo(accessStart), $"Asset 0a. AccesStart within the stack must be set to {accessStart}s");
        //         Assert.That(accessStack.Peek().AccessEnd, Is.EqualTo(accessEnd), $"Asset 0b. AccessEnd within the stack must be {accessEnd}s");
        //         #endregion

        //         #region Assert 1:
        //         // Assert 1a: that Event Start will always be the start of the fundamental timestep.
        //         Assert.That(result.EventStarts[asset1], Is.EqualTo(currentTime), $"Asset 1a: Event Start should always be on the fundamental timestep " +
        //             $"(for Default functionality). The current time, {currentTime}, should be the same as Event Start.");
        //         // Assert 1b: that Event End will always be end of the fundamental timestep. 
        //         Assert.That(result.EventEnds[asset1], Is.EqualTo(nextTime), $"Assert 1b: Event Ends should always be on the fundamental timestep " +
        //             $"(for Default functionality). The current time, {nextTime}, should be the same as Event Start.");
        //         #endregion

        //         # region Assert 2:
        //         // Assert 2a: that Task Start will always be end of the fundamental timestep. 
        //         Assert.That(result.TaskStarts[asset1], Is.EqualTo(accessStart), $"Assert 2a: Task Start should be at the earliest access within the Event at hand. " +
        //             $"Because the Access starts a bit late, this should be equivalent to the access start time, {accessStart}.");

        //         // Assert 2b: that Task End will always be end of the fundamental timestep. 
        //         Assert.That(result.TaskEnds[asset1], Is.EqualTo(accessEnd), $"Assert 2b: Task End should be at the latest accessible time within the Event at hand. " +
        //             $"Because the Access ends within the event, this should be equivalent to the access end time, {accessEnd}s. ");
        //         #endregion
        //     });
        // }

        #endregion

    }
}