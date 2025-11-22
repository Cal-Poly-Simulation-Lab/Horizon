// Copyright (c) 2025 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using NUnit.Framework;
using HSFScheduler;
using MissionElements;
using System.Collections.Generic;
using System.Linq;
using UserModel;
using System.IO;
using Horizon;

namespace HSFSchedulerUnitTest.MethodUnitTests.SortSchedulesDeterministic
{
    /// <summary>
    /// Unit tests for Scheduler.SortSchedulesDeterministic()
    /// 
    /// TEST APPROACH:
    /// - Create schedules with known values
    /// - Verify sorting is deterministic (same input → same output)
    /// - Test descending order (high to low)
    /// - Test ascending order (low to high)
    /// - Test tie-breaking with content hash when values are equal
    /// - Verify multiple runs produce identical order
    /// </summary>
    [TestFixture]
    public class SortSchedulesDeterministicUnitTest : SchedulerUnitTest
    {
        [TearDown]
        public override void TearDown()
        {
            // Reset scheduler static state
            SchedulerStep = -1;
            _schedID = 0;
            _emptySchedule = null;
            
            // Clear collections
            _systemSchedules.Clear();
            _potentialSystemSchedules.Clear();
            _systemCanPerformList.Clear();
            
            // Reset program
            program = new Horizon.Program();
            _testInitialSysState = new SystemState();
            
            base.TearDown();
        }
        
        #region Helper Methods
        
        /// <summary>
        /// Create a schedule with a specific value for testing
        /// </summary>
        private SystemSchedule CreateScheduleWithValue(double value)
        {
            var schedule = new SystemSchedule(_testInitialSysState, $"TestSchedule_{value}");
            schedule.ScheduleValue = value;
            return schedule;
        }
        
        /// <summary>
        /// Create multiple schedules with specified values
        /// </summary>
        private List<SystemSchedule> CreateSchedulesWithValues(params double[] values)
        {
            var schedules = new List<SystemSchedule>();
            foreach (var value in values)
            {
                schedules.Add(CreateScheduleWithValue(value));
            }
            return schedules;
        }
        
        #endregion
        
        #region Test: Descending Sort (Default)
        
        [Test]
        public void SortSchedulesDeterministic_Descending_HighestFirst()
        {
            // Arrange: Create schedules with values [10, 50, 30, 20, 40]
            var schedules = CreateSchedulesWithValues(10, 50, 30, 20, 40);
            
            // Act: Sort descending (default)
            Scheduler.SortSchedulesDeterministic(schedules, descending: true);
            
            // Assert: Result is [50, 40, 30, 20, 10]
            var expectedValues = new[] { 50.0, 40.0, 30.0, 20.0, 10.0 };
            var actualValues = schedules.Select(s => s.ScheduleValue).ToList();
            
            Assert.That(actualValues, Is.EqualTo(expectedValues));
        }
        
        [Test]
        public void SortSchedulesDeterministic_Descending_AllSameValue_PreservesCount()
        {
            // Arrange: Create 5 schedules, all value = 100
            var schedules = CreateSchedulesWithValues(100, 100, 100, 100, 100);
            int originalCount = schedules.Count;
            
            // Act: Sort descending
            Scheduler.SortSchedulesDeterministic(schedules, descending: true);
            
            // Assert: Count preserved, all values still 100
            Assert.That(schedules.Count, Is.EqualTo(originalCount));
            Assert.That(schedules.All(s => s.ScheduleValue == 100), Is.True);
        }
        
        #endregion
        
        #region Test: Ascending Sort
        
        [Test]
        public void SortSchedulesDeterministic_Ascending_LowestFirst()
        {
            // Arrange: Create schedules with values [50, 10, 30, 40, 20]
            var schedules = CreateSchedulesWithValues(50, 10, 30, 40, 20);
            
            // Act: Sort ascending
            Scheduler.SortSchedulesDeterministic(schedules, descending: false);
            
            // Assert: Result is [10, 20, 30, 40, 50]
            var expectedValues = new[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
            var actualValues = schedules.Select(s => s.ScheduleValue).ToList();
            
            Assert.That(actualValues, Is.EqualTo(expectedValues));
        }
        
        #endregion
        
        #region Test: Tie-Breaking with Content Hash
        
        [Test]
        public void SortSchedulesDeterministic_TiedValues_UsesContentHashForDeterminism()
        {
            // Arrange: Create 3 schedules with same value but different content (different names = different hashes)
            var schedule1 = CreateScheduleWithValue(20.0);
            schedule1.Name = "ScheduleA";
            
            var schedule2 = CreateScheduleWithValue(20.0);
            schedule2.Name = "ScheduleB";
            
            var schedule3 = CreateScheduleWithValue(20.0);
            schedule3.Name = "ScheduleC";
            
            var schedules = new List<SystemSchedule> { schedule1, schedule2, schedule3 };
            
            // Act: Sort descending (tied values should use content hash for tie-breaking)
            Scheduler.SortSchedulesDeterministic(schedules, descending: true);
            
            // Assert: All have same value
            Assert.That(schedules.All(s => s.ScheduleValue == 20.0), Is.True);
            
            // Assert: Order is deterministic (same input produces same output)
            var firstRunOrder = schedules.Select(s => s.Name).ToList();
            
            // Reset and sort again
            schedules = new List<SystemSchedule> { schedule1, schedule2, schedule3 };
            Scheduler.SortSchedulesDeterministic(schedules, descending: true);
            var secondRunOrder = schedules.Select(s => s.Name).ToList();
            
            // Same order both times (deterministic)
            Assert.That(secondRunOrder, Is.EqualTo(firstRunOrder));
        }
        
        #endregion
        
        #region Test: Determinism
        
        [Test]
        public void SortSchedulesDeterministic_MultipleRuns_SameOrder()
        {
            // Arrange: Create schedules with mixed values [10, 20, 20, 30, 40]
            var schedules1 = CreateSchedulesWithValues(10, 20, 20, 30, 40);
            var schedules2 = CreateSchedulesWithValues(10, 20, 20, 30, 40);
            
            // Act: Sort both lists
            Scheduler.SortSchedulesDeterministic(schedules1, descending: true);
            Scheduler.SortSchedulesDeterministic(schedules2, descending: true);
            
            // Assert: Same order (by value, then by hash for ties)
            var values1 = schedules1.Select(s => s.ScheduleValue).ToList();
            var values2 = schedules2.Select(s => s.ScheduleValue).ToList();
            
            Assert.That(values2, Is.EqualTo(values1));
            
            // For tied values (20, 20), verify same hash order
            var tiedSchedules1 = schedules1.Where(s => s.ScheduleValue == 20).ToList();
            var tiedSchedules2 = schedules2.Where(s => s.ScheduleValue == 20).ToList();
            
            if (tiedSchedules1.Count > 1 && tiedSchedules2.Count > 1)
            {
                var hashes1 = tiedSchedules1.Select(s => SystemSchedule.ComputeScheduleHash(s)).ToList();
                var hashes2 = tiedSchedules2.Select(s => SystemSchedule.ComputeScheduleHash(s)).ToList();
                Assert.That(hashes2, Is.EqualTo(hashes1), "Tied schedules should have deterministic hash-based ordering");
            }
        }
        
        #endregion
        
        #region Test: Edge Cases
        
        [Test]
        public void SortSchedulesDeterministic_EmptyList_NoCrash()
        {
            // Arrange: Empty list
            var schedules = new List<SystemSchedule>();
            
            // Act & Assert: Should not crash
            Assert.DoesNotThrow(() => Scheduler.SortSchedulesDeterministic(schedules, descending: true));
            Assert.That(schedules.Count, Is.EqualTo(0));
        }
        
        [Test]
        public void SortSchedulesDeterministic_SingleSchedule_Unchanged()
        {
            // Arrange: Single schedule
            var schedules = CreateSchedulesWithValues(50.0);
            double originalValue = schedules[0].ScheduleValue;
            
            // Act: Sort
            Scheduler.SortSchedulesDeterministic(schedules, descending: true);
            
            // Assert: Unchanged
            Assert.That(schedules.Count, Is.EqualTo(1));
            Assert.That(schedules[0].ScheduleValue, Is.EqualTo(originalValue));
        }
        
        #endregion
        
        #region Test: Real-World Scenario (300 Tasks)
        
        [Test]
        public void SortSchedulesDeterministic_Aeolus300Tasks_DeterministicSorting()
        {
            // Arrange: Load 300-task Aeolus scenario
            string inputsDir = Path.Combine(CurrentTestDir, "Inputs");
            SimInputFile = Path.Combine(inputsDir, "AeolusSim_150sec_max10_cropTo5.json");
            TaskInputFile = Path.Combine(inputsDir, "AeolusTasks_300.json");
            ModelInputFile = Path.Combine(inputsDir, "DSAC_Static_ScriptedCS.json");
            
            // Load program
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);
            
            // CRITICAL: Reset static Scheduler fields to match fresh process state
            Scheduler.SchedulerStep = -1;
            Scheduler._schedID = 0;
            
            // Generate schedules
            var scheduler = new Scheduler(program.SchedEvaluator);
            var schedules = scheduler.GenerateSchedules(_testSimSystem!, _testSystemTasks, _testInitialSysState);
            
            // Verify we got schedules
            Assert.That(schedules.Count, Is.GreaterThan(0), "Should have generated schedules");
            
            // Act: Sort schedules deterministically (descending)
            var schedulesCopy1 = new List<SystemSchedule>(schedules);
            var schedulesCopy2 = new List<SystemSchedule>(schedules);
            
            Scheduler.SortSchedulesDeterministic(schedulesCopy1, descending: true);
            Scheduler.SortSchedulesDeterministic(schedulesCopy2, descending: true);
            
            // Assert: Sorting is deterministic (same input → same output)
            var values1 = schedulesCopy1.Select(s => s.ScheduleValue).ToList();
            var values2 = schedulesCopy2.Select(s => s.ScheduleValue).ToList();
            Assert.That(values2, Is.EqualTo(values1), "Multiple sorts should produce identical order");
            
            // Assert: Sorted in descending order (highest value first)
            for (int i = 0; i < schedulesCopy1.Count - 1; i++)
            {
                Assert.That(schedulesCopy1[i].ScheduleValue, Is.GreaterThanOrEqualTo(schedulesCopy1[i + 1].ScheduleValue),
                    $"Schedule at index {i} should have value >= schedule at index {i+1}");
            }
            
            // Assert: For tied values, verify deterministic hash-based ordering
            var groupedByValue = schedulesCopy1.GroupBy(s => s.ScheduleValue).Where(g => g.Count() > 1).ToList();
            foreach (var group in groupedByValue)
            {
                var tiedSchedules = group.ToList();
                var hashes = tiedSchedules.Select(s => SystemSchedule.ComputeScheduleHash(s)).ToList();
                
                // Verify hashes are in ascending order (tie-breaker)
                for (int i = 0; i < hashes.Count - 1; i++)
                {
                    Assert.That(string.CompareOrdinal(hashes[i], hashes[i + 1]), Is.LessThanOrEqualTo(0),
                        $"Tied schedules with value {group.Key} should be ordered by hash (ascending)");
                }
            }
            
            Console.WriteLine($"✅ Sorted {schedulesCopy1.Count} schedules deterministically");
            Console.WriteLine($"   Top 5 values: {string.Join(", ", values1.Take(5))}");
        }
        
        [Test]
        public void SortSchedulesDeterministic_Aeolus300Tasks_MultipleRuns_IdenticalOrder()
        {
            // Arrange: Load 300-task Aeolus scenario
            string inputsDir = Path.Combine(CurrentTestDir, "Inputs");
            SimInputFile = Path.Combine(inputsDir, "AeolusSim_150sec_max10_cropTo5.json");
            TaskInputFile = Path.Combine(inputsDir, "AeolusTasks_300.json");
            ModelInputFile = Path.Combine(inputsDir, "DSAC_Static_ScriptedCS.json");
            
            // Generate schedules (run 1)
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);
            Scheduler.SchedulerStep = -1;
            Scheduler._schedID = 0;
            var scheduler1 = new Scheduler(program.SchedEvaluator);
            var schedules1 = scheduler1.GenerateSchedules(_testSimSystem!, _testSystemTasks, _testInitialSysState);
            Scheduler.SortSchedulesDeterministic(schedules1, descending: true);
            var hashes1 = schedules1.Select(s => SystemSchedule.ComputeScheduleHash(s)).ToList();
            var values1 = schedules1.Select(s => s.ScheduleValue).ToList();
            
            // Reset and generate again (run 2)
            program = new Horizon.Program();
            _testInitialSysState = new SystemState();
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);
            Scheduler.SchedulerStep = -1;
            Scheduler._schedID = 0;
            var scheduler2 = new Scheduler(program.SchedEvaluator);
            var schedules2 = scheduler2.GenerateSchedules(_testSimSystem!, _testSystemTasks, _testInitialSysState);
            Scheduler.SortSchedulesDeterministic(schedules2, descending: true);
            var hashes2 = schedules2.Select(s => SystemSchedule.ComputeScheduleHash(s)).ToList();
            var values2 = schedules2.Select(s => s.ScheduleValue).ToList();
            
            // Assert: Same number of schedules
            Assert.That(schedules2.Count, Is.EqualTo(schedules1.Count), "Should generate same number of schedules");
            
            // Assert: Same order (by value, then by hash)
            Assert.That(values2, Is.EqualTo(values1), "Values should be in same order");
            Assert.That(hashes2, Is.EqualTo(hashes1), "Hashes should be in same order (deterministic)");
            
            Console.WriteLine($"✅ Verified deterministic sorting across multiple runs: {schedules1.Count} schedules");
        }
        
        #endregion
    }
}

