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
using log4net.Appender;
using IronPython.Runtime.Operations;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Unit tests for StateHistory.timesCompletedTask() method.
    /// This method should count how many EVENTS contain the specified task,
    /// NOT the total number of times the task appears across all assets.
    /// 
    /// NOTE: There's a semantic mismatch between what timesCompletedTask counts
    /// (events containing the task) and what MaxTimesToPerform actually means
    /// (total occurrences across all assets). This is why CanAddTasks had to
    /// implement its own counting logic instead of using timesCompletedTask.
    /// </summary>
    [TestFixture]
    public class TimesCompletedTaskUnitTest : SchedulerUnitTest
    {
        protected override string SimInputFile { get; set; } = "InputFiles/SchedulerTestSimulationInput.json";
        protected override string TaskInputFile { get; set; } = Path.Combine(ProjectTestDir, "InputFiles", "ThreeTaskTestInput.json");
        protected override string ModelInputFile { get; set; } = Path.Combine(ProjectTestDir, "InputFiles", "TwoAssetTestModel.json");

        private double currentTime = SimParameters.SimStartSeconds;
        private double endTime = SimParameters.SimEndSeconds;
        private double nextTime = SimParameters.SimStartSeconds + SimParameters.SimStepSeconds;

        [SetUp]
        public void SetupDefaults()
        {
            // Default setup - can be overridden in individual tests
        }

        [TearDown]
        public void ResetSchedulerAttributes()
        {
            // Reset static Scheduler attributes that mirror the Scheduler class
            SchedulerStep = -1;
            CurrentTime = SimParameters.SimStartSeconds;
            NextTime = SimParameters.SimStartSeconds + SimParameters.SimStepSeconds;
            _schedID = 0;
            _SchedulesGenerated = 0;
            _SchedulesCarriedOver = 0;
            _SchedulesCropped = 0;
            _emptySchedule = null;

            // Reset instance attributes
            _systemSchedules.Clear();
            _canPregenAccess = false;
            _scheduleCombos.Clear();
            _preGeneratedAccesses = null;
            _potentialSystemSchedules.Clear();
            _systemCanPerformList.Clear();
            _ScheduleEvaluator = null;

            // Reset program attributes
            program = new Horizon.Program();
            _testSimSystem = null;
            _testSystemTasks.Clear();
            _testInitialSysState = new SystemState();

            // Reset local test attributes
            currentTime = SimParameters.SimStartSeconds;
            endTime = SimParameters.SimEndSeconds;
            nextTime = SimParameters.SimStartSeconds + SimParameters.SimStepSeconds;
        }

        private void BuildProgram()
        {
            // Load the program to get the system and tasks
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            // SimParameters are read-only, use the values from the loaded program
            double simEnd = SimParameters.SimEndSeconds;
            double simStep = SimParameters.SimStepSeconds;
            double simStart = SimParameters.SimStartSeconds;

            // Initialize Empty Schedule
            Scheduler.InitializeEmptySchedule(_systemSchedules, _testInitialSysState);
            SchedulerUnitTest._emptySchedule = Scheduler.emptySchedule;

            // Generate all default schedule combos
            _scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(_testSimSystem, _testSystemTasks, _scheduleCombos, simStart, simEnd);
        }

        [Test, Order(1)]
        public void EmptySchedule_ReturnsZero()
        {
            // Arrange
            ModelInputFile = Path.Combine(CurrentTestDir, "../Inputs", "OneAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "../Inputs", "ThreeTaskTestInput_OneTimeMax.json");
            BuildProgram();

            var emptySchedule = _systemSchedules[0];
            var task1 = _testSystemTasks.First(t => t.Name == "Task1");

            // Act
            int count = emptySchedule.AllStates.timesCompletedTask(task1);

            // Assert
            Assert.That(count, Is.EqualTo(0), "Empty schedule should return 0 for any task.");
        }

        [Test, Order(2)]
        public void OneAsset_OneEvent_OneTask_ReturnsOne()
        {
            // Arrange
            ModelInputFile = Path.Combine(CurrentTestDir, "../Inputs", "OneAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "../Inputs", "ThreeTaskTestInput_OneTimeMax.json");
            BuildProgram();

            double currentTime = 0.0;
            double timeStep = 12.0;
            
            // Run one iteration to create schedules with one event
            this._systemSchedules = SchedulerUnitTest.MainSchedulingLoopHelper(
                _systemSchedules, _scheduleCombos, _testSimSystem,
                _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                currentTime, timeStep, iterations: 1);

            // Act & Assert
            Assert.Multiple(() =>
            {
                var task1 = _testSystemTasks.First(t => t.Name == "Task1");
                var task2 = _testSystemTasks.First(t => t.Name == "Task2");
                var task3 = _testSystemTasks.First(t => t.Name == "Task3");

                // Find the schedule that did Task1
                var schedWithTask1 = _systemSchedules.FirstOrDefault(s => 
                    !s.Name.ToLower().Contains("empty") && 
                    s.AllStates.Events.Any(e => e.Tasks.ContainsValue(task1)));

                Assert.IsNotNull(schedWithTask1, "Should find a schedule that completed Task1");
                Assert.That(schedWithTask1.AllStates.timesCompletedTask(task1), Is.EqualTo(1), 
                    "Schedule with Task1 should return 1 for Task1");
                Assert.That(schedWithTask1.AllStates.timesCompletedTask(task2), Is.EqualTo(0), 
                    "Schedule with Task1 should return 0 for Task2 (not completed)");
                Assert.That(schedWithTask1.AllStates.timesCompletedTask(task3), Is.EqualTo(0), 
                    "Schedule with Task1 should return 0 for Task3 (not completed)");
            });
        }

        [Test, Order(3)]
        public void TwoAssets_OneEvent_SameTask_ReturnsOne_NotTwo()
        {
            // This is THE CRITICAL TEST that shows the semantic issue with timesCompletedTask.
            // When both assets do the same task in one event, timesCompletedTask returns 1
            // (one event containing the task), but the ACTUAL count of task occurrences is 2.
            
            // Arrange
            ModelInputFile = Path.Combine(CurrentTestDir, "../Inputs", "TwoAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "../Inputs", "ThreeTaskTestInput_ThreeTimesMax.json");
            BuildProgram();

            double currentTime = 0.0;
            double timeStep = 12.0;
            
            // Run one iteration
            this._systemSchedules = SchedulerUnitTest.MainSchedulingLoopHelper(
                _systemSchedules, _scheduleCombos, _testSimSystem,
                _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                currentTime, timeStep, iterations: 1);

            // Act & Assert
            Assert.Multiple(() =>
            {
                var task1 = _testSystemTasks.First(t => t.Name == "Task1");

                // Find a schedule where both assets did Task1 in the same event
                var schedWithDoubledTask = _systemSchedules.FirstOrDefault(s => 
                {
                    if (s.Name.ToLower().Contains("empty") || s.AllStates.Events.Count == 0)
                        return false;
                    
                    var firstEvent = s.AllStates.Events.First();
                    int task1Count = firstEvent.Tasks.Values.Count(t => t == task1);
                    return task1Count == 2; // Both assets did Task1
                });

                if (schedWithDoubledTask != null)
                {
                    // The key assertion: timesCompletedTask returns 1 (one event)
                    // even though the task was actually performed twice (by both assets)
                    Assert.That(schedWithDoubledTask.AllStates.timesCompletedTask(task1), Is.EqualTo(1), 
                        "timesCompletedTask returns 1 for an event where both assets did Task1. " +
                        "This is CORRECT behavior for this method (counts events, not occurrences), " +
                        "but it's why CanAddTasks can't use this method for MaxTimesToPerform enforcement.");

                    // Manual count to verify the actual occurrences
                    int actualOccurrences = 0;
                    foreach (var evt in schedWithDoubledTask.AllStates.Events)
                    {
                        foreach (var task in evt.Tasks.Values)
                        {
                            if (task == task1)
                                actualOccurrences++;
                        }
                    }
                    Assert.That(actualOccurrences, Is.EqualTo(2), 
                        "The ACTUAL number of Task1 occurrences across all assets should be 2.");
                }
                else
                {
                    Assert.Warn("Could not find a schedule where both assets performed the same task. " +
                        "This might be due to MaxTimesToPerform constraints or cropping.");
                }
            });
        }

        [Test, Order(4)]
        public void OneAsset_TwoEvents_SameTask_ReturnsTwo()
        {
            // Arrange
            ModelInputFile = Path.Combine(CurrentTestDir, "../Inputs", "OneAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "../Inputs", "ThreeTaskTestInput_ThreeTimesMax.json");
            BuildProgram();

            double currentTime = 0.0;
            double timeStep = 12.0;
            
            // Run two iterations to create schedules with potentially two events
            this._systemSchedules = SchedulerUnitTest.MainSchedulingLoopHelper(
                _systemSchedules, _scheduleCombos, _testSimSystem,
                _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                currentTime, timeStep, iterations: 2);

            // Act & Assert
            Assert.Multiple(() =>
            {
                var task1 = _testSystemTasks.First(t => t.Name == "Task1");

                // Find a schedule that did Task1 twice across two events
                var schedWithTask1Twice = _systemSchedules.FirstOrDefault(s => 
                {
                    if (s.Name.ToLower().Contains("empty") || s.AllStates.Events.Count < 2)
                        return false;
                    
                    int task1EventCount = s.AllStates.Events.Count(e => e.Tasks.ContainsValue(task1));
                    return task1EventCount == 2;
                });

                if (schedWithTask1Twice != null)
                {
                    Assert.That(schedWithTask1Twice.AllStates.timesCompletedTask(task1), Is.EqualTo(2), 
                        "timesCompletedTask should return 2 for a task that appears in 2 events.");
                    Assert.That(schedWithTask1Twice.AllStates.Events.Count, Is.GreaterThanOrEqualTo(2), 
                        "Schedule should have at least 2 events.");
                }
                else
                {
                    Assert.Warn("Could not find a schedule with Task1 in two events. " +
                        "This might be expected if MaxTimesToPerform or cropping prevented it.");
                }
            });
        }

        [Test, Order(5)]
        public void TwoAssets_TwoEvents_MixedTasks_CountsCorrectly()
        {
            // Test that timesCompletedTask correctly counts events across multiple events
            // with different task combinations.
            
            // Arrange
            ModelInputFile = Path.Combine(CurrentTestDir, "../Inputs", "TwoAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "../Inputs", "ThreeTaskTestInput_ThreeTimesMax.json");
            BuildProgram();

            double currentTime = 0.0;
            double timeStep = 12.0;
            
            // Run two iterations
            this._systemSchedules = SchedulerUnitTest.MainSchedulingLoopHelper(
                _systemSchedules, _scheduleCombos, _testSimSystem,
                _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                currentTime, timeStep, iterations: 2);

            // Act & Assert
            Assert.Multiple(() =>
            {
                var task1 = _testSystemTasks.First(t => t.Name == "Task1");
                var task2 = _testSystemTasks.First(t => t.Name == "Task2");
                var task3 = _testSystemTasks.First(t => t.Name == "Task3");

                // Check several schedules to verify counting logic
                foreach (var schedule in _systemSchedules)
                {
                    if (schedule.Name.ToLower().Contains("empty"))
                        continue;

                    // Count events containing each task
                    int task1EventCount = schedule.AllStates.Events.Count(e => e.Tasks.ContainsValue(task1));
                    int task2EventCount = schedule.AllStates.Events.Count(e => e.Tasks.ContainsValue(task2));
                    int task3EventCount = schedule.AllStates.Events.Count(e => e.Tasks.ContainsValue(task3));

                    // Verify timesCompletedTask matches our manual count
                    Assert.That(schedule.AllStates.timesCompletedTask(task1), Is.EqualTo(task1EventCount), 
                        $"Schedule {schedule._scheduleID}: Task1 event count mismatch");
                    Assert.That(schedule.AllStates.timesCompletedTask(task2), Is.EqualTo(task2EventCount), 
                        $"Schedule {schedule._scheduleID}: Task2 event count mismatch");
                    Assert.That(schedule.AllStates.timesCompletedTask(task3), Is.EqualTo(task3EventCount), 
                        $"Schedule {schedule._scheduleID}: Task3 event count mismatch");
                }
            });
        }

        [Test, Order(6)]
        public void CompareEventCountVsOccurrenceCount()
        {
            // This test demonstrates the difference between:
            // 1. timesCompletedTask (counts events containing the task)
            // 2. Actual occurrence count (counts total times task appears across all assets)
            
            // Arrange
            ModelInputFile = Path.Combine(CurrentTestDir, "../Inputs", "TwoAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "../Inputs", "ThreeTaskTestInput_ThreeTimesMax.json");
            BuildProgram();

            double currentTime = 0.0;
            double timeStep = 12.0;
            
            // Run two iterations
            this._systemSchedules = SchedulerUnitTest.MainSchedulingLoopHelper(
                _systemSchedules, _scheduleCombos, _testSimSystem,
                _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                currentTime, timeStep, iterations: 2);

            // Act & Assert
            Assert.Multiple(() =>
            {
                var task1 = _testSystemTasks.First(t => t.Name == "Task1");

                foreach (var schedule in _systemSchedules)
                {
                    if (schedule.Name.ToLower().Contains("empty"))
                        continue;

                    // Method 1: timesCompletedTask (counts events)
                    int eventCount = schedule.AllStates.timesCompletedTask(task1);

                    // Method 2: Manual count of actual occurrences
                    int occurrenceCount = 0;
                    foreach (var evt in schedule.AllStates.Events)
                    {
                        foreach (var task in evt.Tasks.Values)
                        {
                            if (task == task1)
                                occurrenceCount++;
                        }
                    }

                    // The occurrence count should always be >= event count
                    Assert.That(occurrenceCount, Is.GreaterThanOrEqualTo(eventCount), 
                        $"Schedule {schedule._scheduleID}: Occurrence count should be >= event count. " +
                        $"EventCount={eventCount}, OccurrenceCount={occurrenceCount}");

                    // If they're different, it means at least one event had Task1 performed by multiple assets
                    if (occurrenceCount > eventCount)
                    {
                        Console.WriteLine($"Schedule {schedule._scheduleID}: Task1 was 'doubled up' " +
                            $"(EventCount={eventCount}, OccurrenceCount={occurrenceCount})");
                    }
                }
            });
        }

    } // End Class

} // End Namespace

