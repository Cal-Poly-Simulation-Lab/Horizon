// Copyright (c) 2025 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using NUnit.Framework;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using UserModel;
using HSFUniverse;
using Utilities;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Unit tests for Subsystem.CanPerform() across all subsystem types
    /// 
    /// TEST STRATEGY:
    /// - Use TestCanPerformSubsystem (simple, deterministic)
    /// - Test all 3 subsystem types: hardcoded C#, ScriptedCS, Scripted (Python)
    /// - Verify: correct true/false returns, state key loading, state updates
    /// 
    /// ARCHITECTURAL VALIDATION:
    /// - Event start/stop times NOT modified by subsystems (design decision)
    /// - Times set in SystemSchedule constructor only
    /// - May change in future - documented for thesis
    /// </summary>
    [TestFixture]
    public class CanPerformUnitTest : SchedulerUnitTest
    {
        [TearDown]
        public override void TearDown()
        {
            // Reset scheduler static/shared state between tests
            SchedulerStep = -1;
            _schedID = 0;
            _emptySchedule = null;
            
            // Clear collections
            _systemSchedules.Clear();
            _scheduleCombos.Clear();
            _potentialSystemSchedules.Clear();
            _systemCanPerformList.Clear();
            
            // CRITICAL: Create new program to reset InitialSysState
            program = new Horizon.Program();
            _testInitialSysState = new SystemState();
            
            base.TearDown();
        }
        
        #region Helper Methods
        
        /// <summary>
        /// Load scenario and generate potential schedules up to (but before) Checker
        /// Uses proper GenerateSchedules flow: EmptySchedule → TimeDeconfliction → Crop
        /// </summary>
        private (Asset asset, Subsystem subsystem, List<SystemSchedule> potentialSchedules, Domain universe) 
            LoadAndGeneratePotentialSchedules(string modelFile)
        {
            // Load using base class helper
            program = HorizonLoadHelper(
                Path.Combine(CurrentTestDir, "Inputs/SimInput_CanPerform.json"),
                Path.Combine(CurrentTestDir, "Inputs/OneTaskInput.json"), 
                Path.Combine(CurrentTestDir, modelFile)
            );
            
            // Initialize empty schedule (Flow Step 1)
            Scheduler.InitializeEmptySchedule(_systemSchedules, _testInitialSysState);
            _emptySchedule = Scheduler.emptySchedule;
            
            // Generate schedule combos (Flow Step 2)
            _scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(
                _testSimSystem, 
                _testSystemTasks, 
                _scheduleCombos, 
                SimParameters.SimStartSeconds, 
                SimParameters.SimEndSeconds
            );
            
            // TimeDeconfliction (Flow Step 3)
            _potentialSystemSchedules = Scheduler.TimeDeconfliction(
                _systemSchedules, 
                _scheduleCombos, 
                SimParameters.SimStartSeconds
            );
            
            // Crop to max (Flow Step 4)
            _systemSchedules = Scheduler.CropToMaxSchedules(_potentialSystemSchedules, _emptySchedule, _ScheduleEvaluator, false);
            
            return (program.AssetList[0], program.SubList[0], _potentialSystemSchedules, program.SystemUniverse);
        }
        
        #endregion
        
        #region Test: Basic CanPerform Behavior
        
        [TestCase("Inputs/TestCanPerformModel_ScriptedCS.json")]
        [TestCase("Inputs/TestCanPerformModel_Scripted.json", TestName = "Scripted")] // TODO: Debug Python
        public void CanPerform_IterationLoop_ReturnsCorrectly(string modelFile)
        {
            // SETUP: maxIterations=5
            var (asset, subsystem, potentialSchedules, universe) = LoadAndGeneratePotentialSchedules(modelFile);
            var evt = potentialSchedules[0].AllStates.Events.ToList()[0];
            var iterKey = new StateVariableKey<int>(asset.Name + ".iteration");
            
            // Initial: iteration=0
            Assert.That(evt.State.GetLastValue(iterKey).Item2, Is.EqualTo(0));
            
            // Loop: Call CanPerform 5 times, manually advance task start time each call
            for (int i = 0; i < 5; i++)
            {
                // Set task start to new time so subsystem adds at unique time
                double time = i * 12.0;
                evt.SetTaskStart(new Dictionary<Asset, double> { { asset, time } });
                
                // Set NewState (normally done by CheckDependentSubsystems before calling CanPerform)
                subsystem.NewState = evt.State;
                
                bool result = subsystem.CanPerform(evt, universe);
                int iterValue = evt.State.GetLastValue(iterKey).Value;
                
                Assert.Multiple(() =>
                {
                    if (i < 4)
                    {
                        // Iterations 1-4: should pass
                        Assert.That(result, Is.True, $"Iteration {i+1} should return true");
                        Assert.That(iterValue, Is.EqualTo(i + 1), $"Iteration value = {i+1}");
                    }
                    else
                    {
                        // Iteration 5: should fail (at max)
                        Assert.That(result, Is.False, "Iteration 5 should return false (at max)");
                        Assert.That(iterValue, Is.EqualTo(5));
                    }
                });
            }
        }
        
        #endregion
        
        #region Test: Task Timing Manipulation
        
        [TestCase("Inputs/TaskTimeManipulator_ScriptedCS.json")]
        [TestCase("Inputs/TaskTimeManipulator_Scripted.json")]
        public void CanPerform_CanModifyTaskTimes(string modelFile)
        {
            // SETUP: Load TaskTimeManipulator (taskStart +5.0, taskEnd -3.0)
            var prog = HorizonLoadHelper(
                Path.Combine(CurrentTestDir, "Inputs/SimInput_CanPerform.json"),
                Path.Combine(CurrentTestDir, "Inputs/OneTaskInput.json"),
                Path.Combine(CurrentTestDir, modelFile)
            );
            
            var asset = prog.AssetList[0];
            var subsystem = prog.SubList[0];
            var evt = new Event(new System.Collections.Generic.Dictionary<Asset, MissionElements.Task>(), prog.InitialSysState);
            evt.SetTaskStart(new System.Collections.Generic.Dictionary<Asset, double> { { asset, 10.0 } });
            evt.SetTaskEnd(new System.Collections.Generic.Dictionary<Asset, double> { { asset, 20.0 } });
            
            subsystem.NewState = evt.State;
            
            // CALL: CanPerform (should shift times)
            subsystem.CanPerform(evt, prog.SystemUniverse);
            
            // VERIFY: Task times modified
            Assert.Multiple(() =>
            {
                Assert.That(evt.GetTaskStart(asset), Is.EqualTo(15.0), "TaskStart: 10 + 5 = 15");
                Assert.That(evt.GetTaskEnd(asset), Is.EqualTo(17.0), "TaskEnd: 20 - 3 = 17");
            });
        }
        
        [TestCase("Inputs/EventTimeManipulator_ScriptedCS.json", TestName = "ScriptedCS")]
        [TestCase("Inputs/EventTimeManipulator_Scripted.json", TestName = "Scripted")]
        public void CanPerform_CanModifyEventTimes(string modelFile)
        {
            // SETUP: Load TaskTimeManipulator (eventStart +7.0, eventEnd -2.5, task shifts=0)
            var prog = HorizonLoadHelper(
                Path.Combine(CurrentTestDir, "Inputs/SimInput_CanPerform.json"),
                Path.Combine(CurrentTestDir, "Inputs/OneTaskInput.json"),
                Path.Combine(CurrentTestDir, modelFile)
            );
            
            var asset = prog.AssetList[0];
            var subsystem = prog.SubList[0];
            var evt = new Event(new System.Collections.Generic.Dictionary<Asset, MissionElements.Task>(), prog.InitialSysState);
            evt.SetEventStart(new System.Collections.Generic.Dictionary<Asset, double> { { asset, 5.0 } });
            evt.SetEventEnd(new System.Collections.Generic.Dictionary<Asset, double> { { asset, 25.0 } });
            
            subsystem.NewState = evt.State;
            
            // CALL: CanPerform (should shift event times)
            subsystem.CanPerform(evt, prog.SystemUniverse);
            
            // VERIFY: Event times modified
            Assert.Multiple(() =>
            {
                Assert.That(evt.GetEventStart(asset), Is.EqualTo(12.0), "EventStart: 5 + 7 = 12");
                Assert.That(evt.GetEventEnd(asset), Is.EqualTo(22.5), "EventEnd: 25 - 2.5 = 22.5");
            });
        }
        
        #endregion
        
        #region Test: Toy Example Subsystems (TwoAsset_Imaging scenario)
        
        /// <summary>
        /// Helper to load toy example scenario and get subsystems
        /// </summary>
        private (Asset asset1, Subsystem powerSub, Subsystem cameraSub, Subsystem antennaSub, Domain universe) 
            LoadToyExampleScenario()
        {
            // Load toy example from shared inputs
            var inputsDir = Path.Combine(CurrentTestDir, "../../../Inputs");
            program = HorizonLoadHelper(
                Path.Combine(inputsDir, "SimInput_CanPerform.json"),
                Path.Combine(inputsDir, "TwoAsset_Imaging_Tasks.json"),
                Path.Combine(inputsDir, "TwoAsset_Imaging_Model.json")
            );
            
            var asset1 = program.AssetList.Single(a => a.Name == "asset1");
            // Debug: Check what subsystems are actually loaded
            var asset1Subs = program.SubList.Where(s => s.Asset == asset1).ToList();
            var powerSub = asset1Subs.Single(s => s.Name.Contains("power", StringComparison.OrdinalIgnoreCase));
            var cameraSub = asset1Subs.Single(s => s.Name.Contains("camera", StringComparison.OrdinalIgnoreCase));
            var antennaSub = asset1Subs.Single(s => s.Name.Contains("antenna", StringComparison.OrdinalIgnoreCase));
            
            return (asset1, powerSub, cameraSub, antennaSub, program.SystemUniverse);
        }
        
        [Test]
        public void CanPerform_ToyExample_Power_FirstIteration_Passes()
        {
            var (asset1, powerSub, _, _, universe) = LoadToyExampleScenario();
            
            // Create event with IMAGING task (requires 10.0 power, initial is 75.0)
            var task = program.SystemTasks.ToList().First(t => t.Type.ToUpper() == "IMAGING");
            var evt = new Event(new Dictionary<Asset, MissionElements.Task> { { asset1, task } }, program.InitialSysState);
            evt.SetTaskStart(new Dictionary<Asset, double> { { asset1, 0.0 } });
            
            powerSub.NewState = evt.State;
            
            // First iteration should pass: 75.0 >= 10.0
            bool result = powerSub.CanPerform(evt, universe);
            
            // Verify result and state update
            var powerKey = new StateVariableKey<double>("asset1.checker_power");
            double newPower = evt.State.GetLastValue(powerKey).Item2;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Power subsystem should pass first IMAGING (75.0 >= 10.0)");
                Assert.That(newPower, Is.EqualTo(65.0), "Power should be 75.0 - 10.0 = 65.0");
            });
        }
        
        [Test]
        public void CanPerform_ToyExample_Power_FailsWhenInsufficient()
        {
            var (asset1, powerSub, _, _, universe) = LoadToyExampleScenario();
            
            // Create event with TRANSMIT task (requires 20.0 power)
            var task = program.SystemTasks.ToList().First(t => t.Type.ToUpper() == "TRANSMIT");
            var evt = new Event(new Dictionary<Asset, MissionElements.Task> { { asset1, task } }, program.InitialSysState);
            
            // Manually set power to 15.0 (below 20.0 required) at time 1.0 to avoid conflict with initial state
            var powerKey = new StateVariableKey<double>("asset1.checker_power");
            evt.State.AddValue(powerKey, 1.0, 15.0);
            evt.SetTaskStart(new Dictionary<Asset, double> { { asset1, 0.0 } });
            
            powerSub.NewState = evt.State;
            
            // Should fail: 15.0 < 20.0
            bool result = powerSub.CanPerform(evt, universe);
            Assert.That(result, Is.False, "Power subsystem should fail TRANSMIT when power (15.0) < required (20.0)");
        }
        
        [Test]
        public void CanPerform_ToyExample_Camera_FirstIteration_Passes()
        {
            var (asset1, _, cameraSub, _, universe) = LoadToyExampleScenario();
            
            // Create event with IMAGING task (initial images = 0, max = 10)
            var task = program.SystemTasks.ToList().First(t => t.Type.ToUpper() == "IMAGING");
            var evt = new Event(new Dictionary<Asset, MissionElements.Task> { { asset1, task } }, program.InitialSysState);
            evt.SetTaskStart(new Dictionary<Asset, double> { { asset1, 0.0 } });
            
            cameraSub.NewState = evt.State;
            
            // First iteration should pass: 0 < 10
            bool result = cameraSub.CanPerform(evt, universe);
            
            // Verify result and state update
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            double numImages = evt.State.GetLastValue(imageKey).Item2;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Camera subsystem should pass first IMAGING (0 < 10)");
                Assert.That(numImages, Is.EqualTo(1.0), "Images should be 0 + 1 = 1");
            });
        }
        
        [Test]
        public void CanPerform_ToyExample_Camera_FailsAtMaxImages()
        {
            var (asset1, _, cameraSub, _, universe) = LoadToyExampleScenario();
            
            // Create event with IMAGING task
            var task = program.SystemTasks.ToList().First(t => t.Type.ToUpper() == "IMAGING");
            var evt = new Event(new Dictionary<Asset, MissionElements.Task> { { asset1, task } }, program.InitialSysState);
            
            // Manually set images to 10.0 (at max) at time 1.0 to avoid conflict with initial state
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            evt.State.AddValue(imageKey, 1.0, 10.0);
            evt.SetTaskStart(new Dictionary<Asset, double> { { asset1, 0.0 } });
            
            cameraSub.NewState = evt.State;
            
            // Should fail: 10 >= 10
            bool result = cameraSub.CanPerform(evt, universe);
            Assert.That(result, Is.False, "Camera subsystem should fail when numImages (10) >= maxImages (10)");
        }
        
        [Test]
        public void CanPerform_ToyExample_Antenna_FailsWithNoImages()
        {
            var (asset1, _, _, antennaSub, universe) = LoadToyExampleScenario();
            
            // Create event with TRANSMIT task (initial images = 0)
            var task = program.SystemTasks.ToList().First(t => t.Type.ToUpper() == "TRANSMIT");
            var evt = new Event(new Dictionary<Asset, MissionElements.Task> { { asset1, task } }, program.InitialSysState);
            evt.SetTaskStart(new Dictionary<Asset, double> { { asset1, 0.0 } });
            
            antennaSub.NewState = evt.State;
            
            // First iteration should fail: 0 <= 0 (no images to transmit)
            bool result = antennaSub.CanPerform(evt, universe);
            Assert.That(result, Is.False, "Antenna subsystem should fail TRANSMIT when numImages (0) <= 0");
        }
        
        [Test]
        public void CanPerform_ToyExample_Antenna_PassesWithImages()
        {
            var (asset1, _, _, antennaSub, universe) = LoadToyExampleScenario();
            
            // Create event with TRANSMIT task
            var task = program.SystemTasks.ToList().First(t => t.Type.ToUpper() == "TRANSMIT");
            var evt = new Event(new Dictionary<Asset, MissionElements.Task> { { asset1, task } }, program.InitialSysState);
            
            // Manually set images to 5.0 (above 0) at time 1.0 to avoid conflict with initial state
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            evt.State.AddValue(imageKey, 1.0, 5.0);
            // Set task start to 2.0 so subsystem adds at 2.1 (after our 1.0 value, avoiding causality violation)
            evt.SetTaskStart(new Dictionary<Asset, double> { { asset1, 2.0 } });
            
            antennaSub.NewState = evt.State;
            
            // Should pass: 5.0 > 0
            bool result = antennaSub.CanPerform(evt, universe);
            
            // Verify result and state updates
            double numImages = evt.State.GetLastValue(imageKey).Item2;
            var transKey = new StateVariableKey<double>("asset1.num_transmissions");
            double transmissions = evt.State.GetLastValue(transKey).Item2;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Antenna subsystem should pass TRANSMIT when numImages (5.0) > 0");
                Assert.That(numImages, Is.EqualTo(4.0), "Images should be 5.0 - 1 = 4.0");
                Assert.That(transmissions, Is.EqualTo(1.0), "Transmissions should be 0 + 1 = 1.0");
            });
        }
        
        #endregion

    }
}

