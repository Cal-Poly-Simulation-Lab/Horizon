// Copyright (c) 2025 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using NUnit.Framework;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using UserModel;
using Utilities;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HSFUniverse;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Simple tests for Subsystem.CheckDependentSubsystems() using the toy example (TwoAsset_Imaging scenario).
    /// Validates that CheckDependentSubsystems returns true when all CanPerform checks should pass,
    /// and returns false when any CanPerform check should fail.
    /// </summary>
    [TestFixture]
    public class CheckDependentSubsystemsUnitTest : SchedulerUnitTest
    {
        private Asset _asset1 = null!;
        private Subsystem _powerSub = null!;
        private Subsystem _cameraSub = null!;
        private Subsystem _antennaSub = null!;
        private Domain _universe = null!;

        public override void Setup()
        {
            base.Setup();

            program = new Horizon.Program();
            // Uses shared input files from CheckScheudle/Inputs (see README.md)
            var inputsDir = Path.Combine(CurrentTestDir, "../../Inputs");
            var simPath = Path.Combine(inputsDir, "SimInput_CanPerform.json");
            var taskPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Tasks.json");
            var modelPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Model.json");

            HorizonLoadHelper(simPath, taskPath, modelPath);

            _asset1 = program.AssetList.Single(a => a.Name == "asset1");
            _universe = program.SystemUniverse;
            
            // Get subsystems for asset1
            var asset1Subs = program.SubList.Where(s => s.Asset == _asset1).ToList();
            _powerSub = asset1Subs.Single(s => s.Name.Contains("power", System.StringComparison.OrdinalIgnoreCase));
            _cameraSub = asset1Subs.Single(s => s.Name.Contains("camera", System.StringComparison.OrdinalIgnoreCase));
            _antennaSub = asset1Subs.Single(s => s.Name.Contains("antenna", System.StringComparison.OrdinalIgnoreCase));
        }

        #region Helper Methods

        // Note: ResetSubsystems removed - tests now verify order via state mutations, not IsEvaluated flag
        // When IsEvaluated is removed from Subsystem, this approach will continue to work

        private Event CreateEvent(MissionElements.Task task, SystemState state, double eventStart = 0.0, double eventEnd = 10.0, double taskStart = 0.0, double taskEnd = 10.0)
        {
            var evt = new Event(new Dictionary<Asset, MissionElements.Task> { { _asset1, task } }, state);
            evt.SetEventStart(new Dictionary<Asset, double> { { _asset1, eventStart } });
            evt.SetEventEnd(new Dictionary<Asset, double> { { _asset1, eventEnd } });
            evt.SetTaskStart(new Dictionary<Asset, double> { { _asset1, taskStart } });
            evt.SetTaskEnd(new Dictionary<Asset, double> { { _asset1, taskEnd } });
            return evt;
        }

        private MissionElements.Task GetTask(string type)
        {
            return program.SystemTasks.ToList().First(t => t.Type.ToUpper() == type.ToUpper());
        }

        #endregion

        #region Test: Dependency Evaluation Order (via State Mutations)

        [Test]
        public void CheckDependentSubsystems_CameraRunsBeforePower_VerifiedByStateMutation()
        {
            // Power depends on Camera, so Camera must run first
            // IMAGING task: Camera increments images (0 → 1), Power consumes power (75 → 65)
            // If Camera runs before Power, we'll see Camera's state update
            var task = GetTask("IMAGING");
            var state = new SystemState(program.InitialSysState, true);
            var evt = CreateEvent(task, state);
            
            // Initial state: 0 images
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            double initialImages = state.GetLastValue(imageKey).Item2;
            Assert.That(initialImages, Is.EqualTo(0.0), "Initial images should be 0");
            
            // Call CheckDependentSubsystems on Power (depends on Camera)
            bool result = _powerSub.CheckDependentSubsystems(evt, _universe);
            
            // Verify Camera ran before Power by checking Camera's state mutation occurred
            double finalImages = state.GetLastValue(imageKey).Item2;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Power should pass when Camera passes");
                Assert.That(finalImages, Is.EqualTo(1.0), "Camera should have incremented images (0 → 1) before Power ran");
            });
        }

        [Test]
        public void CheckDependentSubsystems_CameraRunsBeforeAntenna_VerifiedByStateMutation()
        {
            // Antenna depends on Camera, so Camera must run first
            // IMAGING task: Camera increments images (0 → 1), Antenna is no-op for IMAGING
            // If Camera runs before Antenna, we'll see Camera's state update
            var task = GetTask("IMAGING");
            var state = new SystemState(program.InitialSysState, true);
            var evt = CreateEvent(task, state);
            
            // Initial state: 0 images
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            double initialImages = state.GetLastValue(imageKey).Item2;
            Assert.That(initialImages, Is.EqualTo(0.0), "Initial images should be 0");
            
            // Call CheckDependentSubsystems on Antenna (depends on Camera)
            bool result = _antennaSub.CheckDependentSubsystems(evt, _universe);
            
            // Verify Camera ran before Antenna by checking Camera's state mutation occurred
            double finalImages = state.GetLastValue(imageKey).Item2;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Antenna should pass when Camera passes");
                Assert.That(finalImages, Is.EqualTo(1.0), "Camera should have incremented images (0 → 1) before Antenna ran");
            });
        }

        [Test]
        public void CheckDependentSubsystems_FullDependencyChain_VerifiedByStateMutations()
        {
            // Power depends on Camera and Antenna, Antenna depends on Camera
            // Expected order: Camera → Antenna → Power
            // IMAGING task: Camera increments images, Antenna no-op, Power consumes power
            var task = GetTask("IMAGING");
            var state = new SystemState(program.InitialSysState, true);
            var evt = CreateEvent(task, state);
            
            // Track initial state
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            var powerKey = new StateVariableKey<double>("asset1.checker_power");
            double initialImages = state.GetLastValue(imageKey).Item2;
            double initialPower = state.GetLastValue(powerKey).Item2;
            
            // Call CheckDependentSubsystems on Power (top of dependency chain)
            bool result = _powerSub.CheckDependentSubsystems(evt, _universe);
            
            // Verify all subsystems ran in correct order by checking their state mutations
            double finalImages = state.GetLastValue(imageKey).Item2;
            double finalPower = state.GetLastValue(powerKey).Item2;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Power should pass when all dependencies pass");
                Assert.That(finalImages, Is.EqualTo(1.0), "Camera should have incremented images (0 → 1)");
                Assert.That(finalPower, Is.EqualTo(65.0), "Power should have consumed power (75 → 65) after Camera and Antenna ran");
            });
        }

        #endregion

        #region Test: CheckDependentSubsystems Returns True When All Should Pass

        [Test]
        public void CheckDependentSubsystems_IMAGING_AllPass_ReturnsTrue()
        {
            // IMAGING task: Camera should pass (0 < 10), Antenna should pass (no-op), Power should pass (75 >= 10)
            var task = GetTask("IMAGING");
            var state = new SystemState(program.InitialSysState, true);
            var evt = CreateEvent(task, state);
            
            bool result = _powerSub.CheckDependentSubsystems(evt, _universe);
            
            Assert.That(result, Is.True, "Power subsystem should pass when Camera and Antenna pass for IMAGING task");
        }

        [Test]
        public void CheckDependentSubsystems_TRANSMIT_AllPass_ReturnsTrue()
        {
            // TRANSMIT task: Camera should pass (no-op), Antenna should pass (images > 0), Power should pass (75 >= 20)
            var task = GetTask("TRANSMIT");
            var state = new SystemState(program.InitialSysState, true);
            // Set images to 5 so Antenna can transmit
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            state.AddValue(imageKey, 1.0, 5.0);
            var evt = CreateEvent(task, state, taskStart: 2.0);
            
            bool result = _powerSub.CheckDependentSubsystems(evt, _universe);
            
            Assert.That(result, Is.True, "Power subsystem should pass when Camera and Antenna pass for TRANSMIT task");
        }

        #endregion

        #region Test: CheckDependentSubsystems Returns False When Any Should Fail

        [Test]
        public void CheckDependentSubsystems_CameraFails_ReturnsFalse()
        {
            // IMAGING task with camera buffer full (10 >= 10) - Camera should fail
            var task = GetTask("IMAGING");
            var state = new SystemState(program.InitialSysState, true);
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            state.AddValue(imageKey, 1.0, 10.0); // At max
            var evt = CreateEvent(task, state);
            
            bool result = _powerSub.CheckDependentSubsystems(evt, _universe);
            
            Assert.That(result, Is.False, "Power subsystem should fail when Camera fails (buffer full)");
        }

        [Test]
        public void CheckDependentSubsystems_AntennaFails_ReturnsFalse()
        {
            // TRANSMIT task with no images (0 <= 0) - Antenna should fail
            var task = GetTask("TRANSMIT");
            var state = new SystemState(program.InitialSysState, true);
            // Images remain at 0 (initial state)
            var evt = CreateEvent(task, state);
            
            bool result = _powerSub.CheckDependentSubsystems(evt, _universe);
            
            Assert.That(result, Is.False, "Power subsystem should fail when Antenna fails (no images to transmit)");
        }

        [Test]
        public void CheckDependentSubsystems_PowerFails_ReturnsFalse()
        {
            // TRANSMIT task with insufficient power (15 < 20) - Power should fail
            var task = GetTask("TRANSMIT");
            var state = new SystemState(program.InitialSysState, true);
            var powerKey = new StateVariableKey<double>("asset1.checker_power");
            state.AddValue(powerKey, 1.0, 15.0); // Below required 20.0
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            state.AddValue(imageKey, 1.0, 5.0); // Ensure Antenna passes
            var evt = CreateEvent(task, state, taskStart: 2.0);
            
            bool result = _powerSub.CheckDependentSubsystems(evt, _universe);
            
            Assert.That(result, Is.False, "Power subsystem should fail when it has insufficient power");
        }

        #endregion
    }
}
