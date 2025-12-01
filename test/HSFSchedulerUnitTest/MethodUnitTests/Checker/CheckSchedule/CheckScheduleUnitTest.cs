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
    /// Tests for Checker.CheckSchedule() method using the toy example (TwoAsset_Imaging scenario).
    /// Validates that CheckSchedule correctly evaluates entire schedules, returns correct boolean results,
    /// and updates state correctly for passing schedules.
    /// </summary>
    [TestFixture]
    public class CheckScheduleUnitTest : SchedulerUnitTest
    {
        private SystemClass _system = null!;
        private Asset _asset1 = null!;
        private Asset _asset2 = null!;
        private List<SystemSchedule> _potentialSchedules = null!;

        public override void Setup()
        {
            base.Setup();

            // Clear static tracking state before each test
            SubsystemCallTracker.Clear();

            program = new Horizon.Program();
            // Uses shared input files from CheckSchedule/Inputs (see README.md)
            var inputsDir = Path.Combine(CurrentTestDir, "Inputs");
            var simPath = Path.Combine(inputsDir, "SimInput_TwoAssetImaging_ToyExample.json");
            var taskPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Tasks.json");
            var modelPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Model.json");

            HorizonLoadHelper(simPath, taskPath, modelPath);

            _system = program.SimSystem;
            _asset1 = program.AssetList.Single(a => a.Name == "asset1");
            _asset2 = program.AssetList.Single(a => a.Name == "asset2");

            // Simulate first scheduler iteration
            SetupFirstIteration();
        }

        [TearDown]
        public override void TearDown()
        {
            // Clear static tracking state after each test
            SubsystemCallTracker.Clear();
            
            base.TearDown();
        }

        #region Setup Helper Methods

        /// <summary>
        /// Simulates the first scheduler iteration:
        /// 1. Initialize empty schedule
        /// 2. Generate schedule combos (exhaustive access combinations)
        /// 3. TimeDeconfliction to create potential schedules
        /// </summary>
        private void SetupFirstIteration()
        {
            // Reset static Scheduler fields for first iteration
            Scheduler.SchedulerStep = 0;
            CurrentTime = SimParameters.SimStartSeconds;
            NextTime = SimParameters.SimStartSeconds + SimParameters.SimStepSeconds;
            _schedID = 0;

            // Initialize empty schedule
            var emptySchedules = new List<SystemSchedule>();
            Scheduler.InitializeEmptySchedule(emptySchedules, _testInitialSysState);
            var emptySchedule = Scheduler.emptySchedule;

            // Generate schedule combos (exhaustive access combinations)
            var scheduleCombos = new Stack<Stack<Access>>();
            scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(
                _system, _testSystemTasks, scheduleCombos, 
                CurrentTime, SimParameters.SimEndSeconds);

            // TimeDeconfliction: create potential schedules from empty schedule + access combos
            if (emptySchedule == null)
                throw new InvalidOperationException("Empty schedule should not be null");
            _potentialSchedules = Scheduler.TimeDeconfliction(
                new List<SystemSchedule> { emptySchedule }, 
                scheduleCombos, 
                CurrentTime);

            // Verify we have potential schedules
            Assert.That(_potentialSchedules, Is.Not.Null);
            Assert.That(_potentialSchedules.Count, Is.GreaterThan(0), 
                "Should have at least one potential schedule from first iteration");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets unique schedule branches grouped by hash.
        /// Reuses the same setup from CheckSubUnitTest.
        /// </summary>
        private Dictionary<string, List<SystemSchedule>> GetUniqueScheduleBranches()
        {
            return _potentialSchedules
                .GroupBy(s => s.ScheduleInfo.ScheduleHash)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Determines if a schedule should pass CheckSchedule.
        /// A schedule fails if TRANSMIT is assigned to EITHER asset (Antenna will fail).
        /// </summary>
        private bool ShouldSchedulePass(SystemSchedule schedule)
        {
            var lastEvent = schedule.AllStates.Events.Peek();
            var asset1Task = lastEvent.GetAssetTask(_asset1);
            var asset2Task = lastEvent.GetAssetTask(_asset2);
            
            // If TRANSMIT is in EITHER asset-task combo, schedule should fail
            if (asset1Task != null && asset1Task.Type.ToUpper() == "TRANSMIT")
                return false;
            if (asset2Task != null && asset2Task.Type.ToUpper() == "TRANSMIT")
                return false;
            
            return true; // All other combinations should pass
        }

        /// <summary>
        /// Gets the initial state value for a given asset and state variable name.
        /// </summary>
        private double GetInitialStateValue(Asset asset, string stateVarName)
        {
            string fullKey = $"{asset.Name.ToLower()}.{stateVarName}";
            var stateKey = program.InitialSysState.Ddata.Keys
                .FirstOrDefault(k => k.VariableName == fullKey);
            
            if (stateKey == null)
            {
                throw new InvalidOperationException($"Initial state value not found for {fullKey}");
            }
            
            return program.InitialSysState.GetLastValue(stateKey).Value;
        }

        /// <summary>
        /// Gets the final state value for a given asset and state variable name from the schedule's last state.
        /// </summary>
        private double GetFinalStateValue(SystemSchedule schedule, Asset asset, string stateVarName)
        {
            string fullKey = $"{asset.Name.ToLower()}.{stateVarName}";
            var lastState = schedule.AllStates.GetLastState();
            
            // Try to find the state key in the last state
            var stateKey = lastState.Ddata.Keys
                .FirstOrDefault(k => k.VariableName == fullKey);
            
            if (stateKey != null)
            {
                return lastState.GetLastValue(stateKey).Value;
            }
            
            // If not found in last state, check initial state
            return GetInitialStateValue(asset, stateVarName);
        }

        /// <summary>
        /// Verifies state changes for a passing schedule.
        /// For IMAGING: power -10, images +1
        /// For RECHARGE: power +25
        /// NOTE: Only call this for schedules that passed CheckSchedule (fail-fast means asset2 won't be evaluated if asset1 fails)
        /// </summary>
        private void VerifyStateForAsset(SystemSchedule schedule, Asset asset, MissionElements.Task task)
        {
            if (task == null) return;
            
            string taskType = task.Type.ToUpper();
            double initialPower = GetInitialStateValue(asset, "checker_power");
            double finalPower = GetFinalStateValue(schedule, asset, "checker_power");
            
            if (taskType == "IMAGING")
            {
                // IMAGING: power should decrease by 10, images should increase by 1
                double initialImages = GetInitialStateValue(asset, "num_images_stored");
                double finalImages = GetFinalStateValue(schedule, asset, "num_images_stored");
                
                Assert.Multiple(() =>
                {
                    Assert.That(finalPower, Is.EqualTo(initialPower - 10.0),
                        $"Power should decrease by 10 for IMAGING on {asset.Name} (initial: {initialPower}, final: {finalPower})");
                    Assert.That(finalImages, Is.EqualTo(initialImages + 1.0),
                        $"Images should increase by 1 for IMAGING on {asset.Name} (initial: {initialImages}, final: {finalImages})");
                });
            }
            else if (taskType == "RECHARGE")
            {
                // RECHARGE: power should increase by 25
                Assert.That(finalPower, Is.EqualTo(initialPower + 25.0),
                    $"Power should increase by 25 for RECHARGE on {asset.Name} (initial: {initialPower}, final: {finalPower})");
            }
            // TRANSMIT schedules should fail, so we don't verify state for them
        }

        #endregion

        #region Unit Tests

        /// <summary>
        /// Tests CheckSchedule for each unique schedule branch.
        /// 1. Verifies there are 9 unique schedule branches
        /// 2. Calls CheckSchedule on each
        /// 3. Verifies schedules that should pass (return true)
        /// 4. Verifies schedules that should fail (return false) - TRANSMIT in EITHER asset should fail
        /// 5. For passing schedules, verifies state for EACH asset
        /// </summary>
        [Test]
        public void CheckSchedule_FirstIteration_UniqueScheduleBranches()
        {
            // 1. Obtain the 9 unique schedule branches and verify
            var schedulesByHash = GetUniqueScheduleBranches();
            
            Assert.That(schedulesByHash.Count, Is.EqualTo(9),
                "Should have exactly 9 unique schedule branches (3 tasks × 2 assets)");
            
            TestContext.WriteLine($"Found {schedulesByHash.Count} unique schedule hashes from TimeDeconfliction");

            // 2. For each unique schedule branch
            foreach (var hashGroup in schedulesByHash)
            {
                string scheduleHash = hashGroup.Key;
                var schedulesInGroup = hashGroup.Value;
                
                // Use first schedule to identify tasks
                var representativeSchedule = schedulesInGroup[0];
                var lastEvent = representativeSchedule.AllStates.Events.Peek();
                
                var asset1Task = lastEvent.GetAssetTask(_asset1);
                var asset2Task = lastEvent.GetAssetTask(_asset2);
                
                string asset1TaskType = asset1Task?.Type ?? "NULL";
                string asset2TaskType = asset2Task?.Type ?? "NULL";
                
                TestContext.WriteLine($"\nSchedule Hash: {scheduleHash.Substring(0, Math.Min(16, scheduleHash.Length))}...");
                TestContext.WriteLine($"  asset1 -> {asset1TaskType}, asset2 -> {asset2TaskType}");
                
                // Determine if this schedule should pass
                bool expectedToPass = ShouldSchedulePass(representativeSchedule);
                
                // Call CheckSchedule on the first schedule in the group
                bool actualResult = Checker.CheckSchedule(_system, representativeSchedule);
                
                // 3 & 4. Verify boolean result FIRST
                Assert.That(actualResult, Is.EqualTo(expectedToPass),
                    $"CheckSchedule should return {expectedToPass} for schedule with " +
                    $"asset1->{asset1TaskType}, asset2->{asset2TaskType}");
                
                // 5. For passing schedules ONLY, verify state for EACH asset
                // IMPORTANT: Only verify state if the schedule actually passed (fail-fast means asset2 won't be evaluated if asset1 fails)
                if (actualResult && expectedToPass)
                {
                    Assert.Multiple(() =>
                    {
                        // Verify state for asset1 if it has a task
                        if (asset1Task != null)
                        {
                            VerifyStateForAsset(representativeSchedule, _asset1, asset1Task);
                        }
                        
                        // Verify state for asset2 if it has a task
                        if (asset2Task != null)
                        {
                            VerifyStateForAsset(representativeSchedule, _asset2, asset2Task);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Tests CheckSchedule with a constraint that fails if power > 75.
        /// This verifies that constraints correctly cause schedule failures even when tasks pass.
        /// - RECHARGE on asset1: power goes from 75 to 100, constraint fails (100 > 75), schedule fails
        /// - IMAGING on asset1: power goes from 75 to 65, constraint passes (65 <= 75), schedule passes
        /// - TRANSMIT on asset1: power goes from 75 to 55, constraint passes (55 <= 75), but Antenna fails (no images), schedule fails
        /// </summary>
        [Test]
        public void CheckSchedule_WithConstraintPowerMax75_RechargeFailsConstraint()
        {
            // Clear static tracking state before test
            SubsystemCallTracker.Clear();

            // Load the constraint model (power > 75 fails)
            program = new Horizon.Program();
            var inputsDir = Path.Combine(CurrentTestDir, "Inputs");
            var simPath = Path.Combine(inputsDir, "SimInput_TwoAssetImaging_ToyExample.json");
            var taskPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Tasks.json");
            var modelPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Model_ConstraintPowerMax75.json");

            HorizonLoadHelper(simPath, taskPath, modelPath);

            _system = program.SimSystem;
            _asset1 = program.AssetList.Single(a => a.Name == "asset1");
            _asset2 = program.AssetList.Single(a => a.Name == "asset2");

            // Simulate first scheduler iteration
            SetupFirstIteration();

            // Get unique schedule branches
            var schedulesByHash = GetUniqueScheduleBranches();
            
            Assert.That(schedulesByHash.Count, Is.EqualTo(9),
                "Should have exactly 9 unique schedule branches");

            // For each unique schedule branch
            foreach (var hashGroup in schedulesByHash)
            {
                string scheduleHash = hashGroup.Key;
                var schedulesInGroup = hashGroup.Value;
                
                var representativeSchedule = schedulesInGroup[0];
                var lastEvent = representativeSchedule.AllStates.Events.Peek();
                
                var asset1Task = lastEvent.GetAssetTask(_asset1);
                var asset2Task = lastEvent.GetAssetTask(_asset2);
                
                string asset1TaskType = asset1Task?.Type ?? "NULL";
                string asset2TaskType = asset2Task?.Type ?? "NULL";
                
                // Determine expected result based on constraint and task types
                bool expectedToPass = true;
                
                // If asset1 has RECHARGE, constraint will fail (power goes from 75 to 100, which is > 75)
                if (asset1Task != null && asset1Task.Type.ToUpper() == "RECHARGE")
                {
                    expectedToPass = false;
                }
                // If asset1 has TRANSMIT, Antenna will fail (no images initially), so schedule fails
                else if (asset1Task != null && asset1Task.Type.ToUpper() == "TRANSMIT")
                {
                    expectedToPass = false;
                }
                // If asset2 has TRANSMIT, Antenna will fail (no images initially), so schedule fails
                else if (asset2Task != null && asset2Task.Type.ToUpper() == "TRANSMIT")
                {
                    expectedToPass = false;
                }
                // IMAGING on asset1: power goes from 75 to 65, constraint passes (65 <= 75)
                // All other combinations without RECHARGE on asset1 or TRANSMIT should pass
                
                // Call CheckSchedule
                bool actualResult = Checker.CheckSchedule(_system, representativeSchedule);
                
                // Verify boolean result
                Assert.That(actualResult, Is.EqualTo(expectedToPass),
                    $"CheckSchedule should return {expectedToPass} for schedule with " +
                    $"asset1->{asset1TaskType}, asset2->{asset2TaskType} " +
                    $"(constraint: power > 75 fails, RECHARGE on asset1 causes power 75->100)");
            }
        }

        #endregion
    }
}

