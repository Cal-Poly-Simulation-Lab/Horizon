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
using System.Reflection;
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
        
        /// <summary>
        /// Verifies that all test subsystems have time mutation parameters set to 0.
        /// This ensures existing tests are not affected by time mutations.
        /// Uses reflection to access methods on dynamically compiled subsystems.
        /// </summary>
        private void VerifyTimeMutationParametersAreZero()
        {
            Assert.Multiple(() =>
            {
                // Use reflection to call GetTaskStartTimeMutation and GetTaskEndTimeMutation
                // (subsystems are dynamically compiled, so direct casting won't work)
                var cameraType = _cameraSub.GetType();
                var antennaType = _antennaSub.GetType();
                var powerType = _powerSub.GetType();
                
                var getStartMethod = cameraType.GetMethod("GetTaskStartTimeMutation");
                var getEndMethod = cameraType.GetMethod("GetTaskEndTimeMutation");
                
                if (getStartMethod != null && getEndMethod != null)
                {
                    double cameraStart = (double)getStartMethod.Invoke(_cameraSub, null)!;
                    double cameraEnd = (double)getEndMethod.Invoke(_cameraSub, null)!;
                    Assert.That(cameraStart, Is.EqualTo(0.0), "Camera task start time mutation should be 0");
                    Assert.That(cameraEnd, Is.EqualTo(0.0), "Camera task end time mutation should be 0");
                }
                
                getStartMethod = antennaType.GetMethod("GetTaskStartTimeMutation");
                getEndMethod = antennaType.GetMethod("GetTaskEndTimeMutation");
                
                if (getStartMethod != null && getEndMethod != null)
                {
                    double antennaStart = (double)getStartMethod.Invoke(_antennaSub, null)!;
                    double antennaEnd = (double)getEndMethod.Invoke(_antennaSub, null)!;
                    Assert.That(antennaStart, Is.EqualTo(0.0), "Antenna task start time mutation should be 0");
                    Assert.That(antennaEnd, Is.EqualTo(0.0), "Antenna task end time mutation should be 0");
                }
                
                getStartMethod = powerType.GetMethod("GetTaskStartTimeMutation");
                getEndMethod = powerType.GetMethod("GetTaskEndTimeMutation");
                
                if (getStartMethod != null && getEndMethod != null)
                {
                    double powerStart = (double)getStartMethod.Invoke(_powerSub, null)!;
                    double powerEnd = (double)getEndMethod.Invoke(_powerSub, null)!;
                    Assert.That(powerStart, Is.EqualTo(0.0), "Power task start time mutation should be 0");
                    Assert.That(powerEnd, Is.EqualTo(0.0), "Power task end time mutation should be 0");
                }
            });
        }

        #endregion

        #region Test: Dependency Evaluation Order (via State Mutations)

        [Test]
        public void CheckDependentSubsystems_CameraRunsBeforePower_VerifiedByStateMutation()
        {
            // Power depends on Camera, so Camera must run first
            // IMAGING task: Camera increments images (0 → 1), Power consumes power (75 → 65)
            // If Camera runs before Power, we'll see Camera's state update
            
            SubsystemCallTracker.Clear();
            
            var task = GetTask("IMAGING");
            var state = new SystemState(program.InitialSysState, true);
            
            // Track initial state
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            double initialImages = state.GetLastValue(imageKey).Item2;
            Assert.That(initialImages, Is.EqualTo(0.0), "Initial images should be 0");
            
            var evt = CreateEvent(task, state);
            
            // Call CheckDependentSubsystems on Power (depends on Camera)
            bool result = _powerSub.CheckDependentSubsystems(evt, _universe);
            
            // Get tracking data
            var allCalls = SubsystemCallTracker.GetTracking();
            var cameraCalls = allCalls.Where(c => c.SubsystemName.Equals("Camera", System.StringComparison.OrdinalIgnoreCase) && c.TaskType == "IMAGING").ToList();
            
            // Verify Camera ran before Power by checking Camera's state mutation occurred and tracking
            double finalImages = state.GetLastValue(imageKey).Item2;
            
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Power should pass when Camera passes");
                
                // Verify Camera was called and reported mutation
                Assert.That(cameraCalls.Count, Is.EqualTo(1), "Camera should have been called exactly once");
                var cameraCall = cameraCalls[0];
                Assert.That(cameraCall.AssetName, Is.EqualTo("asset1"), "Camera call should be for asset1");
                Assert.That(cameraCall.Mutated, Is.True, "Camera should report YES mutation for IMAGING task");
                
                // Verify state actually mutated (matches reported YES)
                Assert.That(finalImages, Is.EqualTo(1.0), 
                    $"Camera should have incremented images (reported YES mutation: {initialImages} → {finalImages})");
                
                // Verify time mutation parameters are 0 (no time mutations in this test)
                VerifyTimeMutationParametersAreZero();
            });
        }

        [Test]
        public void CheckDependentSubsystems_CameraRunsBeforeAntenna_VerifiedByStateMutation()
        {
            // Antenna depends on Camera, so Camera must run first
            // IMAGING task: Camera increments images (0 → 1), Antenna is no-op for IMAGING
            // If Camera runs before Antenna, we'll see Camera's state update
            
            SubsystemCallTracker.Clear();
            
            var task = GetTask("IMAGING");
            var state = new SystemState(program.InitialSysState, true);
            
            // Track initial state
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            double initialImages = state.GetLastValue(imageKey).Item2;
            Assert.That(initialImages, Is.EqualTo(0.0), "Initial images should be 0");
            
            var evt = CreateEvent(task, state);
            
            // Call CheckDependentSubsystems on Antenna (depends on Camera)
            bool result = _antennaSub.CheckDependentSubsystems(evt, _universe);
            
            // Get tracking data
            var allCalls = SubsystemCallTracker.GetTracking();
            var cameraCalls = allCalls.Where(c => c.SubsystemName.Equals("Camera", System.StringComparison.OrdinalIgnoreCase) && c.TaskType == "IMAGING").ToList();
            var antennaCalls = allCalls.Where(c => c.SubsystemName.Equals("Antenna", System.StringComparison.OrdinalIgnoreCase) && c.TaskType == "IMAGING").ToList();
            
            // Verify Camera ran before Antenna by checking Camera's state mutation occurred and tracking
            double finalImages = state.GetLastValue(imageKey).Item2;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Antenna should pass when Camera passes");
                
                // Verify Camera was called and reported mutation
                Assert.That(cameraCalls.Count, Is.EqualTo(1), "Camera should have been called exactly once");
                var cameraCall = cameraCalls[0];
                Assert.That(cameraCall.AssetName, Is.EqualTo("asset1"), "Camera call should be for asset1");
                Assert.That(cameraCall.Mutated, Is.True, "Camera should report YES mutation for IMAGING task");
                
                // Verify Antenna was called and reported NO mutation
                Assert.That(antennaCalls.Count, Is.EqualTo(1), "Antenna should have been called exactly once");
                var antennaCall = antennaCalls[0];
                Assert.That(antennaCall.AssetName, Is.EqualTo("asset1"), "Antenna call should be for asset1");
                Assert.That(antennaCall.Mutated, Is.False, "Antenna should report NO mutation for IMAGING task");
                
                // Verify call order: Camera before Antenna
                Assert.That(cameraCall.CallOrder, Is.LessThan(antennaCall.CallOrder), 
                    "Camera should be called before Antenna (dependency order)");
                
                // Verify state actually mutated (matches reported YES from Camera)
                Assert.That(finalImages, Is.EqualTo(1.0), 
                    $"Camera should have incremented images (reported YES mutation: {initialImages} → {finalImages})");
                
                // Verify time mutation parameters are 0 (no time mutations in this test)
                VerifyTimeMutationParametersAreZero();
            });
        }

        [Test]
        public void CheckDependentSubsystems_FullDependencyChain_VerifiedByStateMutations()
        {
            // Power depends on Camera and Antenna, Antenna depends on Camera
            // Expected order: Camera → Antenna → Power
            // IMAGING task: Camera increments images, Antenna no-op, Power consumes power
            
            SubsystemCallTracker.Clear();
            
            var task = GetTask("IMAGING");
            var state = new SystemState(program.InitialSysState, true);
            
            // Track initial state
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            var powerKey = new StateVariableKey<double>("asset1.checker_power");
            double initialImages = state.GetLastValue(imageKey).Item2;
            double initialPower = state.GetLastValue(powerKey).Item2;
            
            var evt = CreateEvent(task, state);
            
            // Call CheckDependentSubsystems on Power (top of dependency chain)
            bool result = _powerSub.CheckDependentSubsystems(evt, _universe);
            
            // Get tracking data
            var allCalls = SubsystemCallTracker.GetTracking();
            var cameraCalls = allCalls.Where(c => c.SubsystemName.Equals("Camera", System.StringComparison.OrdinalIgnoreCase) && c.TaskType == "IMAGING").ToList();
            var antennaCalls = allCalls.Where(c => c.SubsystemName.Equals("Antenna", System.StringComparison.OrdinalIgnoreCase) && c.TaskType == "IMAGING").ToList();
            
            // Verify all subsystems ran in correct order by checking their state mutations and tracking
            double finalImages = state.GetLastValue(imageKey).Item2;
            double finalPower = state.GetLastValue(powerKey).Item2;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Power should pass when all dependencies pass");
                
                // Verify Camera was called and reported mutation
                Assert.That(cameraCalls.Count, Is.EqualTo(1), "Camera should have been called exactly once");
                var cameraCall = cameraCalls[0];
                Assert.That(cameraCall.AssetName, Is.EqualTo("asset1"), "Camera call should be for asset1");
                Assert.That(cameraCall.Mutated, Is.True, "Camera should report YES mutation for IMAGING task");
                
                // Verify Antenna was called and reported NO mutation
                Assert.That(antennaCalls.Count, Is.EqualTo(1), "Antenna should have been called exactly once");
                var antennaCall = antennaCalls[0];
                Assert.That(antennaCall.AssetName, Is.EqualTo("asset1"), "Antenna call should be for asset1");
                Assert.That(antennaCall.Mutated, Is.False, "Antenna should report NO mutation for IMAGING task");
                
                // Verify call order: Camera before Antenna
                Assert.That(cameraCall.CallOrder, Is.LessThan(antennaCall.CallOrder), 
                    "Camera should be called before Antenna (dependency order)");
                
                // Verify state mutations match reported status
                Assert.That(finalImages, Is.EqualTo(1.0), 
                    $"Camera should have incremented images (reported YES mutation: {initialImages} → {finalImages})");
                Assert.That(finalPower, Is.EqualTo(65.0), 
                    $"Power should have consumed power (reported YES mutation: {initialPower} → {finalPower}) after Camera and Antenna ran");
                
                // Verify time mutation parameters are 0 (no time mutations in this test)
                VerifyTimeMutationParametersAreZero();
            });
        }

        #endregion

        #region Test: Both Dependents Evaluated Even When Neither Mutates

        [Test]
        public void CheckDependentSubsystems_RECHARGE_BothDependentsEvaluated_NeitherMutates()
        {
            // RECHARGE task: Camera and Antenna are both evaluated but neither mutates state
            // Power depends on Camera and Antenna, Antenna depends on Camera
            // Expected order: Camera → Antenna → Power
            // This verifies that both dependents are called even when they don't mutate state
            
            // Clear tracking from previous tests
            SubsystemCallTracker.Clear();
            
            var task = GetTask("RECHARGE");
            var state = new SystemState(program.InitialSysState, true);
            
            // Track initial state values that Camera and Antenna would mutate
            var imageKey = new StateVariableKey<double>("asset1.num_images_stored");
            var transmissionKey = new StateVariableKey<double>("asset1.num_transmissions");
            double initialImages = state.GetLastValue(imageKey).Item2;
            double initialTransmissions = state.GetLastValue(transmissionKey).Item2;
            
            var evt = CreateEvent(task, state);
            
            // Call CheckDependentSubsystems on Power (depends on Camera and Antenna)
            bool result = _powerSub.CheckDependentSubsystems(evt, _universe);
            
            // Get tracking data
            var allCalls = SubsystemCallTracker.GetTracking();
            var cameraCalls = allCalls.Where(c => c.SubsystemName.Equals("Camera", System.StringComparison.OrdinalIgnoreCase) && c.TaskType == "RECHARGE").ToList();
            var antennaCalls = allCalls.Where(c => c.SubsystemName.Equals("Antenna", System.StringComparison.OrdinalIgnoreCase) && c.TaskType == "RECHARGE").ToList();
            
            // Verify both were called, order is correct, and state mutations match reported status
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Power should pass for RECHARGE task");
                
                // Verify Camera was called
                Assert.That(cameraCalls.Count, Is.EqualTo(1), "Camera should have been called exactly once for RECHARGE task");
                var cameraCall = cameraCalls[0];
                Assert.That(cameraCall.AssetName, Is.EqualTo("asset1"), "Camera call should be for asset1");
                Assert.That(cameraCall.Mutated, Is.False, "Camera should report NO mutation for RECHARGE task");
                
                // Verify Antenna was called
                Assert.That(antennaCalls.Count, Is.EqualTo(1), "Antenna should have been called exactly once for RECHARGE task");
                var antennaCall = antennaCalls[0];
                Assert.That(antennaCall.AssetName, Is.EqualTo("asset1"), "Antenna call should be for asset1");
                Assert.That(antennaCall.Mutated, Is.False, "Antenna should report NO mutation for RECHARGE task");
                
                // Verify call order: Camera before Antenna (both before Power, but Power doesn't track)
                Assert.That(cameraCall.CallOrder, Is.LessThan(antennaCall.CallOrder), 
                    "Camera should be called before Antenna (dependency order)");
                
                // Verify state was NOT mutated by Camera or Antenna (matches reported NO)
                double finalImages = state.GetLastValue(imageKey).Item2;
                double finalTransmissions = state.GetLastValue(transmissionKey).Item2;
                Assert.That(finalImages, Is.EqualTo(initialImages), 
                    $"Images should remain unchanged (Camera reported NO mutation: {initialImages} → {finalImages})");
                Assert.That(finalTransmissions, Is.EqualTo(initialTransmissions), 
                    $"Transmissions should remain unchanged (Antenna reported NO mutation: {initialTransmissions} → {finalTransmissions})");
                
                // Verify time mutation parameters are 0 (no time mutations in this test)
                VerifyTimeMutationParametersAreZero();
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
            
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Power subsystem should pass when Camera and Antenna pass for IMAGING task");
                VerifyTimeMutationParametersAreZero();
            });
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
            
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "Power subsystem should pass when Camera and Antenna pass for TRANSMIT task");
                VerifyTimeMutationParametersAreZero();
            });
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
            
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False, "Power subsystem should fail when Camera fails (buffer full)");
                VerifyTimeMutationParametersAreZero();
            });
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
            
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False, "Power subsystem should fail when Antenna fails (no images to transmit)");
                VerifyTimeMutationParametersAreZero();
            });
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
            
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False, "Power subsystem should fail when it has insufficient power");
                VerifyTimeMutationParametersAreZero();
            });
        }

        #endregion
    }
}
