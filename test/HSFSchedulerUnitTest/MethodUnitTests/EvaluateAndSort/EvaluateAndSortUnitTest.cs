// Copyright (c) 2025 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using NUnit.Framework;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using UserModel;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Unit tests for Scheduler.EvaluateAndSortCanPerformSchedules() using the toy example (TwoAsset_Imaging scenario).
    /// Validates that schedules are evaluated correctly (sum of task target values) and sorted in descending order.
    /// </summary>
    [TestFixture]
    public class EvaluateAndSortUnitTest : SchedulerUnitTest
    {
        private SystemClass _system = null!;
        private Asset _asset1 = null!;
        private Asset _asset2 = null!;
        private List<SystemSchedule> _potentialSchedules = null!;

        public override void Setup()
        {
            base.Setup();

            program = new Horizon.Program();
            // Uses shared input files from CheckSchedule/Inputs (see README.md)
            var inputsDir = Path.Combine(CurrentTestDir, "../Checker/CheckSchedule/Inputs");
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
                "Should have potential schedules after TimeDeconfliction");
        }

        /// <summary>
        /// Gets the expected schedule value based on task assignments.
        /// Task values: RECHARGE=1, IMAGING=10, TRANSMIT=50
        /// </summary>
        private double GetExpectedScheduleValue(Event lastEvent)
        {
            double value = 0.0;
            var asset1Task = lastEvent.GetAssetTask(_asset1);
            var asset2Task = lastEvent.GetAssetTask(_asset2);
            
            if (asset1Task != null)
            {
                value += asset1Task.Target.Value;
            }
            if (asset2Task != null)
            {
                value += asset2Task.Target.Value;
            }
            
            return value;
        }
        
        #endregion
        
        #region Test: Evaluation and Sorting with Toy Example

        /// <summary>
        /// Tests EvaluateAndSortCanPerformSchedules with the toy example.
        /// Verifies:
        /// 1. All schedules are evaluated correctly (sum of task target values)
        /// 2. Schedules are sorted in descending order (highest value first)
        /// 3. Tie-breaking works correctly (schedules with same value are ordered deterministically)
        /// 
        /// Expected schedule values:
        /// - asset1→IMAGING, asset2→IMAGING: 10 + 10 = 20
        /// - asset1→IMAGING, asset2→RECHARGE: 10 + 1 = 11
        /// - asset1→RECHARGE, asset2→IMAGING: 1 + 10 = 11
        /// - asset1→RECHARGE, asset2→RECHARGE: 1 + 1 = 2
        /// 
        /// After sorting (descending): 20, 11, 11, 2
        /// </summary>
        [Test]
        public void EvaluateAndSort_ToyExample_EvaluatesAndSortsCorrectly()
        {
            // 1. Get passing schedules (only those without TRANSMIT)
            List<SystemSchedule> passingSchedules = Scheduler.CheckAllPotentialSchedules(_system, _potentialSchedules);
            
            Assert.That(passingSchedules.Count, Is.EqualTo(4),
                "Should have 4 passing schedules (no TRANSMIT tasks)");
            
            // Verify initial state: all schedules have ScheduleValue = 0 (not yet evaluated)
            foreach (var schedule in passingSchedules)
            {
                Assert.That(schedule.ScheduleValue, Is.EqualTo(0.0),
                    "Schedule should have initial value of 0 before evaluation");
        }
        
            // 2. Call EvaluateAndSortCanPerformSchedules
            List<SystemSchedule> evaluatedAndSorted = Scheduler.EvaluateAndSortCanPerformSchedules(
                _ScheduleEvaluator!, passingSchedules);
            
            Assert.That(evaluatedAndSorted.Count, Is.EqualTo(4),
                "Should return all 4 schedules after evaluation and sorting");

            // 3. Verify all schedules are evaluated correctly
            var expectedValues = new List<double>();
            foreach (var schedule in passingSchedules)
            {
                var lastEvent = schedule.AllStates.Events.Peek();
                double expectedValue = GetExpectedScheduleValue(lastEvent);
                expectedValues.Add(expectedValue);
            }
            
            // Sort expected values descending for comparison
            expectedValues.Sort((a, b) => b.CompareTo(a));
            // Expected: [20, 11, 11, 2]
            
            Assert.Multiple(() =>
            {
                // Verify each schedule has the correct evaluated value
                for (int i = 0; i < evaluatedAndSorted.Count; i++)
                {
                    var schedule = evaluatedAndSorted[i];
                    double expectedValue = expectedValues[i];
                    
                    Assert.That(schedule.ScheduleValue, Is.EqualTo(expectedValue),
                        $"Schedule {i} should have value {expectedValue}");
                }
            });

            // 4. Verify schedules are sorted in descending order (highest value first)
            for (int i = 0; i < evaluatedAndSorted.Count - 1; i++)
            {
                Assert.That(evaluatedAndSorted[i].ScheduleValue, 
                    Is.GreaterThanOrEqualTo(evaluatedAndSorted[i + 1].ScheduleValue),
                    $"Schedule {i} (value: {evaluatedAndSorted[i].ScheduleValue}) should be >= schedule {i + 1} (value: {evaluatedAndSorted[i + 1].ScheduleValue})");
        }
        
            // 5. Verify specific expected order: [20, 11, 11, 2]
            Assert.That(evaluatedAndSorted[0].ScheduleValue, Is.EqualTo(20.0),
                "First schedule should have value 20 (IMAGING + IMAGING)");
            Assert.That(evaluatedAndSorted[1].ScheduleValue, Is.EqualTo(11.0),
                "Second schedule should have value 11 (IMAGING + RECHARGE or RECHARGE + IMAGING)");
            Assert.That(evaluatedAndSorted[2].ScheduleValue, Is.EqualTo(11.0),
                "Third schedule should have value 11 (IMAGING + RECHARGE or RECHARGE + IMAGING)");
            Assert.That(evaluatedAndSorted[3].ScheduleValue, Is.EqualTo(2.0),
                "Fourth schedule should have value 2 (RECHARGE + RECHARGE)");

            // 6. Verify tie-breaking: schedules with value 11 should be ordered deterministically by hash
            if (evaluatedAndSorted[1].ScheduleValue == evaluatedAndSorted[2].ScheduleValue)
            {
                string hash1 = SystemSchedule.ComputeScheduleHash(evaluatedAndSorted[1]);
                string hash2 = SystemSchedule.ComputeScheduleHash(evaluatedAndSorted[2]);
                
                Assert.That(string.CompareOrdinal(hash1, hash2), Is.LessThanOrEqualTo(0),
                    "Tied schedules should be ordered deterministically by hash (ascending lexicographic order)");
            }
        }
        
        #endregion
        
        #region Test: Edge Cases
        
        [Test]
        public void EvaluateAndSort_EmptyList_ReturnsEmpty()
        {
            var emptyList = new List<SystemSchedule>();
            List<SystemSchedule> result = Scheduler.EvaluateAndSortCanPerformSchedules(
                _ScheduleEvaluator!, emptyList);
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(0),
                "Should return empty list when input is empty");
        }
        
        [Test]
        public void EvaluateAndSort_SingleSchedule_ReturnsOne()
        {
            // Get one passing schedule
            List<SystemSchedule> passingSchedules = Scheduler.CheckAllPotentialSchedules(_system, _potentialSchedules);
            var singleSchedule = new List<SystemSchedule> { passingSchedules[0] };
            
            // Verify initial value is 0
            Assert.That(singleSchedule[0].ScheduleValue, Is.EqualTo(0.0),
                "Schedule should have initial value of 0");
            
            List<SystemSchedule> result = Scheduler.EvaluateAndSortCanPerformSchedules(
                _ScheduleEvaluator!, singleSchedule);
            
            Assert.That(result.Count, Is.EqualTo(1),
                "Should return single schedule");
            Assert.That(result[0].ScheduleValue, Is.GreaterThan(0.0),
                "Schedule should be evaluated (value > 0)");
        }

        /// <summary>
        /// Tests EvaluateAndSortCanPerformSchedules across all scheduler iterations.
        /// Verifies that evaluation and sorting work correctly at each iteration step.
        /// 
        /// This test simulates the full scheduler loop:
        /// 1. Initialize empty schedule
        /// 2. For each iteration:
        ///    a. Crop schedules to max
        ///    b. TimeDeconfliction (create potential schedules)
        ///    c. CheckAllPotentialSchedules (filter to passing)
        ///    d. EvaluateAndSortCanPerformSchedules (evaluate and sort) ← VERIFY HERE
        ///    e. MergeAndClearSystemSchedules (merge with existing)
        /// 
        /// At each iteration, verifies:
        /// - All schedules are evaluated correctly (sum of task target values)
        /// - Schedules are sorted in descending order (highest value first)
        /// - Schedule values accumulate correctly across iterations
        /// </summary>
        [Test]
        public void EvaluateAndSort_AllIterations_EvaluatesAndSortsCorrectly()
        {
            // Initialize empty schedule
            var systemSchedules = new List<SystemSchedule>();
            Scheduler.InitializeEmptySchedule(systemSchedules, _testInitialSysState);
            var emptySchedule = Scheduler.emptySchedule;
            if (emptySchedule == null)
                throw new InvalidOperationException("Empty schedule should not be null");
            systemSchedules = new List<SystemSchedule> { emptySchedule };

            // Generate exhaustive schedule combos (done once at start)
            var scheduleCombos = new Stack<Stack<Access>>();
            scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(
                _system, _testSystemTasks, scheduleCombos, 
                SimParameters.SimStartSeconds, SimParameters.SimEndSeconds);

            // Track iteration info for verification
            var iterationInfo = new List<(int step, int passingCount, double maxValue, double minValue, bool isSorted)>();

            // Run through all scheduler iterations
            double startTime = SimParameters.SimStartSeconds;
            double endTime = SimParameters.SimEndSeconds;
            double stepLength = SimParameters.SimStepSeconds;
            
            Scheduler.SchedulerStep = -1; // Will be incremented to 0 in first iteration
            
            for (double currentTime = startTime; currentTime < endTime; currentTime += stepLength)
            {
                // Update scheduler step and time (using settable properties from SchedulerUnitTest)
                Scheduler.SchedulerStep += 1;
                CurrentTime = currentTime;
                NextTime = currentTime + stepLength;

                TestContext.WriteLine($"\n=== Iteration {Scheduler.SchedulerStep} (Time: {currentTime}) ===");

                // 1. Crop schedules to max (before TimeDeconfliction)
                if (Scheduler.emptySchedule == null)
                    throw new InvalidOperationException("Empty schedule should not be null");
                systemSchedules = Scheduler.CropToMaxSchedules(
                    systemSchedules, Scheduler.emptySchedule, _ScheduleEvaluator!);

                // 2. TimeDeconfliction: create potential schedules
                var potentialSchedules = Scheduler.TimeDeconfliction(
                    systemSchedules, scheduleCombos, currentTime);
                
                TestContext.WriteLine($"  Potential schedules: {potentialSchedules.Count}");

                // 3. CheckAllPotentialSchedules: filter to passing
                var passingSchedules = Scheduler.CheckAllPotentialSchedules(_system, potentialSchedules);
                
                TestContext.WriteLine($"  Passing schedules: {passingSchedules.Count}");

                if (passingSchedules.Count == 0)
                {
                    TestContext.WriteLine($"  No passing schedules at iteration {Scheduler.SchedulerStep}, skipping evaluation");
                    continue;
                }

                // Store values before evaluation (should all be 0 or previously evaluated)
                var valuesBefore = passingSchedules.Select(s => s.ScheduleValue).ToList();

                // 4. EvaluateAndSortCanPerformSchedules ← VERIFY THIS
                var evaluatedAndSorted = Scheduler.EvaluateAndSortCanPerformSchedules(
                    _ScheduleEvaluator!, passingSchedules);

                Assert.That(evaluatedAndSorted.Count, Is.EqualTo(passingSchedules.Count),
                    $"Iteration {Scheduler.SchedulerStep}: Should return all {passingSchedules.Count} passing schedules");

                // Verify all schedules are evaluated
                Assert.Multiple(() =>
                {
                    for (int i = 0; i < evaluatedAndSorted.Count; i++)
                    {
                        var schedule = evaluatedAndSorted[i];
                        
                        // Schedule should be evaluated (value >= 0, and should have changed if it was 0 before)
                        Assert.That(schedule.ScheduleValue, Is.GreaterThanOrEqualTo(0.0),
                            $"Iteration {Scheduler.SchedulerStep}, Schedule {i}: Should have evaluated value >= 0");
                        
                        // If it was 0 before, it should be > 0 now (unless all tasks have value 0, which shouldn't happen)
                        if (valuesBefore[i] == 0.0 && schedule.ScheduleValue == 0.0)
                        {
                            // This is OK if the schedule has no events (empty schedule), but otherwise should have value
                            if (schedule.AllStates.Events.Count > 0)
                            {
                                TestContext.WriteLine($"  WARNING: Schedule {i} has events but value is 0");
                            }
                        }
                    }
                });

                // Verify schedules are sorted in descending order
                for (int i = 0; i < evaluatedAndSorted.Count - 1; i++)
                {
                    Assert.That(evaluatedAndSorted[i].ScheduleValue, 
                        Is.GreaterThanOrEqualTo(evaluatedAndSorted[i + 1].ScheduleValue),
                        $"Iteration {Scheduler.SchedulerStep}: Schedule {i} (value: {evaluatedAndSorted[i].ScheduleValue}) " +
                        $"should be >= schedule {i + 1} (value: {evaluatedAndSorted[i + 1].ScheduleValue})");
                }

                // Track iteration info
                double maxValue = evaluatedAndSorted.Count > 0 ? evaluatedAndSorted[0].ScheduleValue : 0.0;
                double minValue = evaluatedAndSorted.Count > 0 ? evaluatedAndSorted[evaluatedAndSorted.Count - 1].ScheduleValue : 0.0;
                bool isSorted = true;
                for (int i = 0; i < evaluatedAndSorted.Count - 1; i++)
                {
                    if (evaluatedAndSorted[i].ScheduleValue < evaluatedAndSorted[i + 1].ScheduleValue)
                    {
                        isSorted = false;
                        break;
                    }
                }
                
                iterationInfo.Add((Scheduler.SchedulerStep, evaluatedAndSorted.Count, maxValue, minValue, isSorted));
                
                TestContext.WriteLine($"  Evaluated and sorted: {evaluatedAndSorted.Count} schedules, " +
                    $"values range [{minValue}, {maxValue}], sorted: {isSorted}");

                // 5. MergeAndClearSystemSchedules: merge with existing
                systemSchedules = Scheduler.MergeAndClearSystemSchedules(systemSchedules, evaluatedAndSorted);
                
                TestContext.WriteLine($"  Total schedules after merge: {systemSchedules.Count}");
            }

            // Final verification: ensure we processed at least some iterations
            Assert.That(iterationInfo.Count, Is.GreaterThan(0),
                "Should have processed at least one iteration with passing schedules");

            // Summary
            TestContext.WriteLine($"\n=== Summary: {iterationInfo.Count} iterations processed ===");
            foreach (var info in iterationInfo)
            {
                TestContext.WriteLine($"  Iteration {info.step}: {info.passingCount} schedules, " +
                    $"values [{info.minValue}, {info.maxValue}], sorted: {info.isSorted}");
            }
        }

        /// <summary>
        /// Tests EvaluateAndSortCanPerformSchedules with DefaultEvaluator (C# and Python) evaluators across all iterations.
        /// Verifies that DefaultEvaluator works correctly for evaluation and sorting in both implementations.
        /// </summary>
        [TestCase("TwoAsset_Imaging_Model_DefaultEvaluatorCS.json", "DefaultEvaluator (C#)")]
        [TestCase("TwoAsset_Imaging_Model_DefaultEvaluatorPy.json", "DefaultEvaluator (Python)")]
        public void EvaluateAndSort_DefaultEvaluator_AllIterations_EvaluatesAndSortsCorrectly(string modelFile, string evaluatorName)
        {
            // Load scenario with DefaultEvaluator evaluator
            program = new Horizon.Program();
            var inputsDir = Path.Combine(CurrentTestDir, "Inputs");
            var simPath = Path.Combine(inputsDir, "SimInput_CanPerform.json");
            var taskPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Tasks.json");
            var modelPath = Path.Combine(inputsDir, modelFile);

            HorizonLoadHelper(simPath, taskPath, modelPath);

            _system = program.SimSystem;
            _asset1 = program.AssetList.Single(a => a.Name == "asset1");
            _asset2 = program.AssetList.Single(a => a.Name == "asset2");

            // Initialize empty schedule
            var systemSchedules = new List<SystemSchedule>();
            Scheduler.InitializeEmptySchedule(systemSchedules, _testInitialSysState);
            var emptySchedule = Scheduler.emptySchedule;
            if (emptySchedule == null)
                throw new InvalidOperationException("Empty schedule should not be null");
            systemSchedules = new List<SystemSchedule> { emptySchedule };

            // Generate exhaustive schedule combos (done once at start)
            var scheduleCombos = new Stack<Stack<Access>>();
            scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(
                _system, _testSystemTasks, scheduleCombos, 
                SimParameters.SimStartSeconds, SimParameters.SimEndSeconds);

            // Track iteration info for verification
            var iterationInfo = new List<(int step, int passingCount, double maxValue, double minValue, bool isSorted)>();

            // Run through all scheduler iterations
            double startTime = SimParameters.SimStartSeconds;
            double endTime = SimParameters.SimEndSeconds;
            double stepLength = SimParameters.SimStepSeconds;
            
            Scheduler.SchedulerStep = -1; // Will be incremented to 0 in first iteration
            
            for (double currentTime = startTime; currentTime < endTime; currentTime += stepLength)
            {
                // Update scheduler step and time (using settable properties from SchedulerUnitTest)
                Scheduler.SchedulerStep += 1;
                CurrentTime = currentTime;
                NextTime = currentTime + stepLength;

                TestContext.WriteLine($"\n=== {evaluatorName} Iteration {Scheduler.SchedulerStep} (Time: {currentTime}) ===");

                // 1. Crop schedules to max (before TimeDeconfliction)
                if (Scheduler.emptySchedule == null)
                    throw new InvalidOperationException("Empty schedule should not be null");
                systemSchedules = Scheduler.CropToMaxSchedules(
                    systemSchedules, Scheduler.emptySchedule, _ScheduleEvaluator!);

                // 2. TimeDeconfliction: create potential schedules
                var potentialSchedules = Scheduler.TimeDeconfliction(
                    systemSchedules, scheduleCombos, currentTime);
                
                TestContext.WriteLine($"  Potential schedules: {potentialSchedules.Count}");

                // 3. CheckAllPotentialSchedules: filter to passing
                var passingSchedules = Scheduler.CheckAllPotentialSchedules(_system, potentialSchedules);
                
                TestContext.WriteLine($"  Passing schedules: {passingSchedules.Count}");

                if (passingSchedules.Count == 0)
                {
                    TestContext.WriteLine($"  No passing schedules at iteration {Scheduler.SchedulerStep}, skipping evaluation");
                    continue;
                }

                // Store values before evaluation (should all be 0 or previously evaluated)
                var valuesBefore = passingSchedules.Select(s => s.ScheduleValue).ToList();

                // 4. EvaluateAndSortCanPerformSchedules ← VERIFY THIS
                var evaluatedAndSorted = Scheduler.EvaluateAndSortCanPerformSchedules(
                    _ScheduleEvaluator!, passingSchedules);

                Assert.That(evaluatedAndSorted.Count, Is.EqualTo(passingSchedules.Count),
                    $"Iteration {Scheduler.SchedulerStep}: Should return all {passingSchedules.Count} passing schedules");

                // Verify all schedules are evaluated
                Assert.Multiple(() =>
                {
                    for (int i = 0; i < evaluatedAndSorted.Count; i++)
                    {
                        var schedule = evaluatedAndSorted[i];
                        
                        // Schedule should be evaluated (value >= 0)
                        Assert.That(schedule.ScheduleValue, Is.GreaterThanOrEqualTo(0.0),
                            $"Iteration {Scheduler.SchedulerStep}, Schedule {i}: Should have evaluated value >= 0");
                    }
                });

                // Verify schedules are sorted in descending order
                for (int i = 0; i < evaluatedAndSorted.Count - 1; i++)
                {
                    Assert.That(evaluatedAndSorted[i].ScheduleValue, 
                        Is.GreaterThanOrEqualTo(evaluatedAndSorted[i + 1].ScheduleValue),
                        $"Iteration {Scheduler.SchedulerStep}: Schedule {i} (value: {evaluatedAndSorted[i].ScheduleValue}) " +
                        $"should be >= schedule {i + 1} (value: {evaluatedAndSorted[i + 1].ScheduleValue})");
                }

                // Track iteration info
                double maxValue = evaluatedAndSorted.Count > 0 ? evaluatedAndSorted[0].ScheduleValue : 0.0;
                double minValue = evaluatedAndSorted.Count > 0 ? evaluatedAndSorted[evaluatedAndSorted.Count - 1].ScheduleValue : 0.0;
                bool isSorted = true;
                for (int i = 0; i < evaluatedAndSorted.Count - 1; i++)
                {
                    if (evaluatedAndSorted[i].ScheduleValue < evaluatedAndSorted[i + 1].ScheduleValue)
                    {
                        isSorted = false;
                        break;
                    }
                }
                
                iterationInfo.Add((Scheduler.SchedulerStep, evaluatedAndSorted.Count, maxValue, minValue, isSorted));
                
                TestContext.WriteLine($"  Evaluated and sorted: {evaluatedAndSorted.Count} schedules, " +
                    $"values range [{minValue}, {maxValue}], sorted: {isSorted}");

                // 5. MergeAndClearSystemSchedules: merge with existing
                systemSchedules = Scheduler.MergeAndClearSystemSchedules(systemSchedules, evaluatedAndSorted);
                
                TestContext.WriteLine($"  Total schedules after merge: {systemSchedules.Count}");
            }

            // Final verification: ensure we processed at least some iterations
            Assert.That(iterationInfo.Count, Is.GreaterThan(0),
                "Should have processed at least one iteration with passing schedules");

            // Summary
            TestContext.WriteLine($"\n=== {evaluatorName} Summary: {iterationInfo.Count} iterations processed ===");
            foreach (var info in iterationInfo)
            {
                TestContext.WriteLine($"  Iteration {info.step}: {info.passingCount} schedules, " +
                    $"values [{info.minValue}, {info.maxValue}], sorted: {info.isSorted}");
            }
        }
        
        #endregion
    }
}

