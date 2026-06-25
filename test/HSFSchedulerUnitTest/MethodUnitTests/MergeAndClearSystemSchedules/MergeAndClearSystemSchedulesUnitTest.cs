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
    /// Unit tests for Scheduler.MergeAndClearSystemSchedules().
    /// Validates that new schedules are merged at the front of existing schedules,
    /// and that the input list is cleared.
    /// </summary>
    [TestFixture]
    public class MergeAndClearSystemSchedulesUnitTest : SchedulerUnitTest
    {
        private SystemClass _system = null!;
        private Asset _asset1 = null!;
        private Asset _asset2 = null!;
        

        public override void Setup()
        {
            base.Setup();

            program = new Horizon.Program();
            // Uses shared input files from CheckSchedule/Inputs
            var inputsDir = Path.Combine(CurrentTestDir, "../Checker/CheckSchedule/Inputs");
            var simPath = Path.Combine(inputsDir, "SimInput_TwoAssetImaging_ToyExample.json");
            var taskPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Tasks.json");
            var modelPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Model.json");

            HorizonLoadHelper(simPath, taskPath, modelPath);

            _system = program.SimSystem;
            _asset1 = program.AssetList.Single(a => a.Name == "asset1");
            _asset2 = program.AssetList.Single(a => a.Name == "asset2");
        }

        #region Tests

        /// <summary>
        /// Tests that MergeAndClearSystemSchedules correctly merges new schedules at the front
        /// and clears the input list.
        /// </summary>
        [Test]
        public void MergeAndClearSystemSchedules_MergesNewSchedulesAtFront_ClearsInputList()
        {
            // Setup: Create some existing schedules (simulating schedules from previous iterations)
            var existingSchedules = new List<SystemSchedule>();
            Scheduler.InitializeEmptySchedule(existingSchedules, _testInitialSysState);
            var emptySchedule = Scheduler.emptySchedule;
            if (emptySchedule == null)
                throw new InvalidOperationException("Empty schedule should not be null");

            // Add a few existing schedules (simulate from previous iteration)
            existingSchedules.Add(emptySchedule);
            var existingSchedule1 = CreateTestSchedule("existing1");
            var existingSchedule2 = CreateTestSchedule("existing2");
            existingSchedules.Add(existingSchedule1);
            existingSchedules.Add(existingSchedule2);

            int existingCount = existingSchedules.Count;
            // Store references to existing schedules for comparison
            var existingScheduleRefs = existingSchedules.ToList();

            // Create new schedules (simulating schedules from current iteration)
            var newSchedules = new List<SystemSchedule>();
            var newSchedule1 = CreateTestSchedule("new1");
            var newSchedule2 = CreateTestSchedule("new2");
            var newSchedule3 = CreateTestSchedule("new3");
            newSchedules.Add(newSchedule1);
            newSchedules.Add(newSchedule2);
            newSchedules.Add(newSchedule3);

            int newCount = newSchedules.Count;
            // Store references to new schedules for comparison
            var newScheduleRefs = newSchedules.ToList();

            // Call MergeAndClearSystemSchedules
            var mergedSchedules = Scheduler.MergeAndClearSystemSchedules(existingSchedules, newSchedules);

            // Verify: Total count should be existing + new
            Assert.Multiple(() =>
            {
                Assert.That(mergedSchedules.Count, Is.EqualTo(existingCount + newCount),
                    "Merged schedules should contain all existing and new schedules");

                // Verify: New schedules are at the front (indices 0, 1, 2)
                Assert.That(mergedSchedules[0], Is.SameAs(newScheduleRefs[0]),
                    "First schedule should be from new schedules");
                Assert.That(mergedSchedules[1], Is.SameAs(newScheduleRefs[1]),
                    "Second schedule should be from new schedules");
                Assert.That(mergedSchedules[2], Is.SameAs(newScheduleRefs[2]),
                    "Third schedule should be from new schedules");

                // Verify: Existing schedules are after new schedules (indices 3, 4, 5)
                Assert.That(mergedSchedules[3], Is.SameAs(existingScheduleRefs[0]),
                    "Fourth schedule should be from existing schedules");
                Assert.That(mergedSchedules[4], Is.SameAs(existingScheduleRefs[1]),
                    "Fifth schedule should be from existing schedules");
                Assert.That(mergedSchedules[5], Is.SameAs(existingScheduleRefs[2]),
                    "Sixth schedule should be from existing schedules");

                // Verify: Input list (newSchedules) was cleared
                Assert.That(newSchedules.Count, Is.EqualTo(0),
                    "Input list should be cleared after merge");

                // Verify: Returned list is the same reference as existingSchedules
                Assert.That(mergedSchedules, Is.SameAs(existingSchedules),
                    "Returned list should be the same reference as the first input parameter");
            });
        }

        /// <summary>
        /// Tests that MergeAndClearSystemSchedules handles empty new schedules list.
        /// </summary>
        [Test]
        public void MergeAndClearSystemSchedules_EmptyNewSchedules_KeepsExistingSchedules()
        {
            // Setup: Create existing schedules
            var existingSchedules = new List<SystemSchedule>();
            Scheduler.InitializeEmptySchedule(existingSchedules, _testInitialSysState);
            var emptySchedule = Scheduler.emptySchedule;
            if (emptySchedule == null)
                throw new InvalidOperationException("Empty schedule should not be null");

            existingSchedules.Add(emptySchedule);
            var existingSchedule1 = CreateTestSchedule("existing1");
            existingSchedules.Add(existingSchedule1);

            int existingCount = existingSchedules.Count;
            var existingScheduleRefs = existingSchedules.ToList();

            // Create empty new schedules list
            var newSchedules = new List<SystemSchedule>();

            // Call MergeAndClearSystemSchedules
            var mergedSchedules = Scheduler.MergeAndClearSystemSchedules(existingSchedules, newSchedules);

            // Verify: Count should be unchanged, order should be unchanged
            Assert.Multiple(() =>
            {
                Assert.That(mergedSchedules.Count, Is.EqualTo(existingCount),
                    "Merged schedules should have same count as existing when new list is empty");

                for (int i = 0; i < existingCount; i++)
                {
                    Assert.That(mergedSchedules[i], Is.SameAs(existingScheduleRefs[i]),
                        $"Schedule at index {i} should remain unchanged");
                }

                // Verify: Input list was cleared (even though it was empty)
                Assert.That(newSchedules.Count, Is.EqualTo(0),
                    "Input list should be cleared even if it was empty");
            });
        }

        /// <summary>
        /// Tests that MergeAndClearSystemSchedules handles empty existing schedules list.
        /// </summary>
        [Test]
        public void MergeAndClearSystemSchedules_EmptyExistingSchedules_AddsNewSchedules()
        {
            // Setup: Create empty existing schedules
            var existingSchedules = new List<SystemSchedule>();

            // Create new schedules
            var newSchedules = new List<SystemSchedule>();
            var newSchedule1 = CreateTestSchedule("new1");
            var newSchedule2 = CreateTestSchedule("new2");
            newSchedules.Add(newSchedule1);
            newSchedules.Add(newSchedule2);

            int newCount = newSchedules.Count;
            var newScheduleRefs = newSchedules.ToList();

            // Call MergeAndClearSystemSchedules
            var mergedSchedules = Scheduler.MergeAndClearSystemSchedules(existingSchedules, newSchedules);

            // Verify: All new schedules should be in merged list
            Assert.Multiple(() =>
            {
                Assert.That(mergedSchedules.Count, Is.EqualTo(newCount),
                    "Merged schedules should contain all new schedules when existing is empty");

                for (int i = 0; i < newCount; i++)
                {
                    Assert.That(mergedSchedules[i], Is.SameAs(newScheduleRefs[i]),
                        $"Schedule at index {i} should match new schedule {i}");
                }

                // Verify: Input list was cleared
                Assert.That(newSchedules.Count, Is.EqualTo(0),
                    "Input list should be cleared after merge");
            });
        }

        /// <summary>
        /// Tests MergeAndClearSystemSchedules across all scheduler iterations of the toy example.
        /// Verifies that merging works correctly at each iteration step.
        /// </summary>
        [Test]
        public void MergeAndClearSystemSchedules_AllIterations_MergesCorrectly()
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
            var iterationInfo = new List<(int step, int existingCount, int newCount, int mergedCount)>();

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

                int existingCountBefore = systemSchedules.Count;

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
                    TestContext.WriteLine($"  No passing schedules at iteration {Scheduler.SchedulerStep}, skipping merge");
                    continue;
                }

                // 4. EvaluateAndSortCanPerformSchedules
                var evaluatedAndSorted = Scheduler.EvaluateAndSortCanPerformSchedules(
                    _ScheduleEvaluator!, passingSchedules);

                int newCount = evaluatedAndSorted.Count;
                int existingCountAfterCrop = systemSchedules.Count;

                // Store references to new schedules BEFORE merge (since merge clears the list)
                var newScheduleRefs = evaluatedAndSorted.ToList();

                // 5. MergeAndClearSystemSchedules ← VERIFY THIS
                var mergedSchedules = Scheduler.MergeAndClearSystemSchedules(
                    systemSchedules, evaluatedAndSorted);

                int mergedCount = mergedSchedules.Count;

                // Verify merge operation
                Assert.Multiple(() =>
                {
                    // Total count should be existing + new
                    Assert.That(mergedCount, Is.EqualTo(existingCountAfterCrop + newCount),
                        $"Iteration {Scheduler.SchedulerStep}: Merged count should be existing ({existingCountAfterCrop}) + new ({newCount})");

                    // New schedules should be at the front
                    if (newCount > 0)
                    {
                        for (int i = 0; i < newCount; i++)
                        {
                            Assert.That(mergedSchedules[i], Is.SameAs(newScheduleRefs[i]),
                                $"Iteration {Scheduler.SchedulerStep}: Schedule at index {i} should be from new schedules");
                        }
                    }

                    // Input list should be cleared
                    Assert.That(evaluatedAndSorted.Count, Is.EqualTo(0),
                        $"Iteration {Scheduler.SchedulerStep}: Input list should be cleared after merge");

                    // Returned list should be same reference
                    Assert.That(mergedSchedules, Is.SameAs(systemSchedules),
                        $"Iteration {Scheduler.SchedulerStep}: Returned list should be same reference as input");
                });

                // Track iteration info
                iterationInfo.Add((Scheduler.SchedulerStep, existingCountAfterCrop, newCount, mergedCount));
                
                TestContext.WriteLine($"  Existing before: {existingCountBefore}, After crop: {existingCountAfterCrop}, " +
                    $"New: {newCount}, Merged: {mergedCount}");

                // Update systemSchedules for next iteration
                systemSchedules = mergedSchedules;
            }

            // Final verification: ensure we processed at least some iterations
            Assert.That(iterationInfo.Count, Is.GreaterThan(0),
                "Should have processed at least one iteration with passing schedules");

            // Summary
            TestContext.WriteLine($"\n=== Summary: {iterationInfo.Count} iterations processed ===");
            foreach (var info in iterationInfo)
            {
                TestContext.WriteLine($"  Iteration {info.step}: Existing={info.existingCount}, " +
                    $"New={info.newCount}, Merged={info.mergedCount}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a simple test schedule with a unique identifier.
        /// </summary>
        private SystemSchedule CreateTestSchedule(string identifier)
        {
            // Use the same constructor pattern as other tests
            var schedule = new SystemSchedule(_testInitialSysState, $"TestSchedule_{identifier}");
            return schedule;
        }

        #endregion
    }
}

