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
    /// Tests for Scheduler.CheckAllPotentialSchedules() method using the toy example (TwoAsset_Imaging scenario).
    /// Validates that CheckAllPotentialSchedules correctly filters schedules, returns only passing schedules,
    /// updates state correctly, and updates StateHashHistory.
    /// </summary>
    [TestFixture]
    public class CheckAllPotentialSchedulesUnitTest : SchedulerUnitTest
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
            var inputsDir = Path.Combine(CurrentTestDir, "CheckSchedule/Inputs");
            var simPath = Path.Combine(inputsDir, "SimInput_CanPerform.json");
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

        /// <summary>
        /// Verifies that StateHashHistory is updated for a schedule after CheckAllPotentialSchedules.
        /// Checks that the state hash exists in StateHashHistory for the current scheduler step and schedule hash.
        /// </summary>
        private void VerifyStateHashUpdated(SystemSchedule schedule)
        {
            string scheduleHash = schedule.ScheduleInfo.ScheduleHash;
            int currentStep = Scheduler.SchedulerStep;
            var stateHistory = schedule.AllStates;
            
            // Check if state hash exists in StateHashHistory for this step and schedule hash
            bool found = stateHistory.StateHashHistory.TryGetValue((currentStep, scheduleHash), out string? stateHash);
            
            Assert.That(found, Is.True,
                $"StateHashHistory should contain entry for step {currentStep} and schedule hash {scheduleHash.Substring(0, Math.Min(8, scheduleHash.Length))}...");
            
            Assert.That(stateHash, Is.Not.Null.And.Not.Empty,
                $"State hash should not be null or empty for schedule {scheduleHash.Substring(0, Math.Min(8, scheduleHash.Length))}...");
        }

        #endregion

        #region Unit Tests

        /// <summary>
        /// Tests CheckAllPotentialSchedules for all unique schedule branches.
        /// 1. Obtains all 9 unique schedule branches using helper functions
        /// 2. Calls CheckAllPotentialSchedules
        /// 3. Confirms output includes only expected passing schedules
        /// 4. For passing schedules, verifies state for EACH asset
        /// 5. Ensures StateHashHistory is updated from the beginning schedule branch to its output
        /// </summary>
        [Test]
        public void CheckAllPotentialSchedules_FirstIteration_UniqueScheduleBranches()
        {
            // 1. Obtain all 9 unique schedule branches and verify
            var schedulesByHash = GetUniqueScheduleBranches();
            
            Assert.That(schedulesByHash.Count, Is.EqualTo(9),
                "Should have exactly 9 unique schedule branches (3 tasks × 2 assets)");
            
            TestContext.WriteLine($"Found {schedulesByHash.Count} unique schedule hashes from TimeDeconfliction");

            // Store initial schedule hashes and state hashes for comparison (before CheckAllPotentialSchedules)
            var initialScheduleHashes = new Dictionary<string, string>(); // schedule hash -> schedule hash (should stay same)
            var initialStateHashes = new Dictionary<string, string>(); // schedule hash -> state hash (should change)
            foreach (var hashGroup in schedulesByHash)
            {
                var representativeSchedule = hashGroup.Value[0];
                string scheduleHash = hashGroup.Key;
                initialScheduleHashes[scheduleHash] = representativeSchedule.ScheduleInfo.ScheduleHash;
                initialStateHashes[scheduleHash] = representativeSchedule.AllStates.StateHash ?? "";
            }

            // 2. Call CheckAllPotentialSchedules
            List<SystemSchedule> passingSchedules = Scheduler.CheckAllPotentialSchedules(_system, _potentialSchedules);
            
            TestContext.WriteLine($"CheckAllPotentialSchedules returned {passingSchedules.Count} passing schedules");

            // 3. Confirm output includes only expected passing schedules
            // Group passing schedules by hash to identify unique ones
            var passingSchedulesByHash = passingSchedules
                .GroupBy(s => s.ScheduleInfo.ScheduleHash)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            TestContext.WriteLine($"Found {passingSchedulesByHash.Count} unique passing schedule hashes");

            // Verify each unique schedule branch
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
                
                bool expectedToPass = ShouldSchedulePass(representativeSchedule);
                bool actuallyPassed = passingSchedulesByHash.ContainsKey(scheduleHash);
                
                Assert.That(actuallyPassed, Is.EqualTo(expectedToPass),
                    $"Schedule with asset1->{asset1TaskType}, asset2->{asset2TaskType} " +
                    $"should {(expectedToPass ? "pass" : "fail")} (hash: {scheduleHash.Substring(0, Math.Min(8, scheduleHash.Length))}...)");
            }

            // 4. For passing schedules, verify state for EACH asset
            foreach (var passingSchedule in passingSchedules)
            {
                string scheduleHash = passingSchedule.ScheduleInfo.ScheduleHash;
                var lastEvent = passingSchedule.AllStates.Events.Peek();
                
                var asset1Task = lastEvent.GetAssetTask(_asset1);
                var asset2Task = lastEvent.GetAssetTask(_asset2);
                
                Assert.Multiple(() =>
                {
                    // Verify state for asset1 if it has a task
                    if (asset1Task != null)
                    {
                        VerifyStateForAsset(passingSchedule, _asset1, asset1Task);
                    }
                    
                    // Verify state for asset2 if it has a task
                    if (asset2Task != null)
                    {
                        VerifyStateForAsset(passingSchedule, _asset2, asset2Task);
                    }
                });
            }

            // 5. Ensure StateHashHistory is updated and schedule hash remains unchanged
            foreach (var passingSchedule in passingSchedules)
            {
                string scheduleHash = passingSchedule.ScheduleInfo.ScheduleHash;
                
                Assert.Multiple(() =>
                {
                    // Verify schedule hash remains unchanged (not updated by CheckAllPotentialSchedules)
                    if (initialScheduleHashes.TryGetValue(scheduleHash, out string? initialScheduleHash))
                    {
                        Assert.That(passingSchedule.ScheduleInfo.ScheduleHash, Is.EqualTo(initialScheduleHash),
                            $"Schedule hash should remain unchanged after CheckAllPotentialSchedules " +
                            $"(hash: {scheduleHash.Substring(0, Math.Min(8, scheduleHash.Length))}...)");
                    }
                    
                    // Verify StateHashHistory is updated
                    VerifyStateHashUpdated(passingSchedule);
                    
                    // Verify that state hash changed from initial (if it existed)
                    if (initialStateHashes.TryGetValue(scheduleHash, out string? initialStateHash) && !string.IsNullOrEmpty(initialStateHash))
                    {
                        string finalStateHash = passingSchedule.AllStates.StateHash ?? "";
                        Assert.That(finalStateHash, Is.Not.EqualTo(initialStateHash),
                            $"State hash should be updated after CheckAllPotentialSchedules for schedule {scheduleHash.Substring(0, Math.Min(8, scheduleHash.Length))}...");
                    }
                });
            }
        }

        /// <summary>
        /// Tests CheckAllPotentialSchedules with a constraint that fails if power > 75.
        /// This verifies that constraints correctly cause schedule failures even when tasks pass.
        /// - RECHARGE on asset1: power goes from 75 to 100, constraint fails (100 > 75), schedule fails
        /// - IMAGING on asset1: power goes from 75 to 65, constraint passes (65 <= 75), schedule passes
        /// - TRANSMIT on asset1: power goes from 75 to 55, constraint passes (55 <= 75), but Antenna fails (no images), schedule fails
        /// </summary>
        [Test]
        public void CheckAllPotentialSchedules_WithConstraintPowerMax75_RechargeFailsConstraint()
        {
            // Clear static tracking state before test
            SubsystemCallTracker.Clear();

            // Load the constraint model (power > 75 fails)
            program = new Horizon.Program();
            var inputsDir = Path.Combine(CurrentTestDir, "CheckSchedule/Inputs");
            var simPath = Path.Combine(inputsDir, "SimInput_CanPerform.json");
            var taskPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Tasks.json");
            var modelPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Model_ConstraintPowerMax75.json");

            HorizonLoadHelper(simPath, taskPath, modelPath);

            _system = program.SimSystem;
            _asset1 = program.AssetList.Single(a => a.Name == "asset1");
            _asset2 = program.AssetList.Single(a => a.Name == "asset2");

            // Simulate first scheduler iteration
            SetupFirstIteration();

            // 1. Obtain all 9 unique schedule branches and verify
            var schedulesByHash = GetUniqueScheduleBranches();
            
            Assert.That(schedulesByHash.Count, Is.EqualTo(9),
                "Should have exactly 9 unique schedule branches (3 tasks × 2 assets)");
            
            TestContext.WriteLine($"Found {schedulesByHash.Count} unique schedule hashes from TimeDeconfliction");

            // Store initial schedule hashes and state hashes for comparison (before CheckAllPotentialSchedules)
            var initialScheduleHashes = new Dictionary<string, string>();
            var initialStateHashes = new Dictionary<string, string>();
            foreach (var hashGroup in schedulesByHash)
            {
                var representativeSchedule = hashGroup.Value[0];
                string scheduleHash = hashGroup.Key;
                initialScheduleHashes[scheduleHash] = representativeSchedule.ScheduleInfo.ScheduleHash;
                initialStateHashes[scheduleHash] = representativeSchedule.AllStates.StateHash ?? "";
            }

            // 2. Call CheckAllPotentialSchedules
            List<SystemSchedule> passingSchedules = Scheduler.CheckAllPotentialSchedules(_system, _potentialSchedules);
            
            TestContext.WriteLine($"CheckAllPotentialSchedules returned {passingSchedules.Count} passing schedules");

            // 3. Confirm output includes only expected passing schedules
            // Group passing schedules by hash to identify unique ones
            var passingSchedulesByHash = passingSchedules
                .GroupBy(s => s.ScheduleInfo.ScheduleHash)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            TestContext.WriteLine($"Found {passingSchedulesByHash.Count} unique passing schedule hashes");

            // Verify each unique schedule branch with adjusted expected passing logic
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
                
                bool actuallyPassed = passingSchedulesByHash.ContainsKey(scheduleHash);
                
                Assert.That(actuallyPassed, Is.EqualTo(expectedToPass),
                    $"Schedule with asset1->{asset1TaskType}, asset2->{asset2TaskType} " +
                    $"should {(expectedToPass ? "pass" : "fail")} " +
                    $"(constraint: power > 75 fails, RECHARGE on asset1 causes power 75->100) " +
                    $"(hash: {scheduleHash.Substring(0, Math.Min(8, scheduleHash.Length))}...)");
            }

            // 4. For passing schedules, verify state for EACH asset
            foreach (var passingSchedule in passingSchedules)
            {
                string scheduleHash = passingSchedule.ScheduleInfo.ScheduleHash;
                var lastEvent = passingSchedule.AllStates.Events.Peek();
                
                var asset1Task = lastEvent.GetAssetTask(_asset1);
                var asset2Task = lastEvent.GetAssetTask(_asset2);
                
                Assert.Multiple(() =>
                {
                    // Verify state for asset1 if it has a task
                    if (asset1Task != null)
                    {
                        VerifyStateForAsset(passingSchedule, _asset1, asset1Task);
                    }
                    
                    // Verify state for asset2 if it has a task
                    if (asset2Task != null)
                    {
                        VerifyStateForAsset(passingSchedule, _asset2, asset2Task);
                    }
                });
            }

            // 5. Ensure StateHashHistory is updated and schedule hash remains unchanged
            foreach (var passingSchedule in passingSchedules)
            {
                string scheduleHash = passingSchedule.ScheduleInfo.ScheduleHash;
                
                Assert.Multiple(() =>
                {
                    // Verify schedule hash remains unchanged (not updated by CheckAllPotentialSchedules)
                    if (initialScheduleHashes.TryGetValue(scheduleHash, out string? initialScheduleHash))
                    {
                        Assert.That(passingSchedule.ScheduleInfo.ScheduleHash, Is.EqualTo(initialScheduleHash),
                            $"Schedule hash should remain unchanged after CheckAllPotentialSchedules " +
                            $"(hash: {scheduleHash.Substring(0, Math.Min(8, scheduleHash.Length))}...)");
                    }
                    
                    // Verify StateHashHistory is updated
                    VerifyStateHashUpdated(passingSchedule);
                    
                    // Verify that state hash changed from initial (if it existed)
                    if (initialStateHashes.TryGetValue(scheduleHash, out string? initialStateHash) && !string.IsNullOrEmpty(initialStateHash))
                    {
                        string finalStateHash = passingSchedule.AllStates.StateHash ?? "";
                        Assert.That(finalStateHash, Is.Not.EqualTo(initialStateHash),
                            $"State hash should be updated after CheckAllPotentialSchedules for schedule {scheduleHash.Substring(0, Math.Min(8, scheduleHash.Length))}...");
                    }
                });
            }
        }

        #endregion
    }
}

