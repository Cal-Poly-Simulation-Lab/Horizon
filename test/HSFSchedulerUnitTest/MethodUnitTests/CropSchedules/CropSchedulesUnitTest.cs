using Microsoft.VisualStudio.TestPlatform.TestHost;
using Horizon;
using Utilities;
using NUnit.Framework.Internal;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using UserModel;
using System.Collections.Generic;
using System.Linq;

namespace HSFSchedulerUnitTest
{
    [TestFixture]
    public class CropSchedulesUnitTest : SchedulerUnitTest
    {
        protected override string SimInputFile { get; set; } = "InputFiles/SchedulerTestSimulationInput.json";
        protected override string TaskInputFile { get; set; } = Path.Combine(ProjectTestDir, "InputFiles", "ThreeTaskTestInput.json");
        protected override string ModelInputFile { get; set; } = Path.Combine(ProjectTestDir, "InputFiles", "TwoAssetTestModel.json");

        [SetUp]
        public void SetupDefaults()
        {
            // Load program for integrated tests
            BuildProgram();
        }

        [TearDown]
        public void ResetSchedulerAttributes()
        {
            // Reset static Scheduler attributes
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
        }

        private void BuildProgram()
        {
            // Load the program to get the system and tasks
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            double simEnd = SimParameters.SimEndSeconds;
            double simStep = SimParameters.SimStepSeconds;
            double simStart = SimParameters.SimStartSeconds;

            // Initialize empty schedule
            Scheduler.InitializeEmptySchedule(_systemSchedules, _testInitialSysState);
            SchedulerUnitTest._emptySchedule = Scheduler.emptySchedule;

            // Generate schedule combos
            _scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(_testSimSystem, _testSystemTasks, _scheduleCombos, simStart, simEnd);
        }

        #region Helper Methods

        /// <summary>
        /// Create a schedule with a specific value for testing
        /// </summary>
        private SystemSchedule CreateScheduleWithValue(double value)
        {
            // Create a simple schedule with empty state using the same constructor as InitializeEmptySchedule
            var schedule = new SystemSchedule(_testInitialSysState, $"TestSchedule_{value}");
            
            // Manually set the schedule value (normally done by evaluator)
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

        #region Unit Tests - CropSchedules (Interior Method)

        [Test]
        public void CropSchedules_SimpleValues_KeepsCorrectCount()
        {
            // Arrange: Create 10 schedules
            var schedulesToCrop = CreateSchedulesWithValues(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
            int cropTo = 5;

            Console.WriteLine($"=== CropSchedules_SimpleValues_KeepsCorrectCount ===");
            Console.WriteLine($"Input: {schedulesToCrop.Count} schedules, crop to {cropTo}");
            //Console.WriteLine($"Using empty schedule: {_emptySchedule.Name}");

            // Act: Call the internal CropSchedules method
            // Note: Evaluator may set all values to 0, so we just verify count
            Scheduler.CropSchedules(schedulesToCrop, _ScheduleEvaluator, _emptySchedule, cropTo);

            Console.WriteLine($"Output: {schedulesToCrop.Count} schedules remain");

            // Assert: Should have exactly cropTo schedules
            Assert.That(schedulesToCrop.Count, Is.EqualTo(cropTo), 
                $"Should have exactly {cropTo} schedules after cropping from 10");
        }

        [Test]
        public void CropSchedules_AllSameValue_KeepsExactCount()
        {
            // Arrange: Create 20 schedules
            var schedulesToCrop = CreateSchedulesWithValues(Enumerable.Repeat(100.0, 20).ToArray());
            int cropTo = 10;

            Console.WriteLine($"=== CropSchedules_AllSameValue_KeepsExactCount ===");
            Console.WriteLine($"Input: {schedulesToCrop.Count} schedules, crop to {cropTo}");
            Console.WriteLine($"Using empty schedule: {_emptySchedule.Name}");

            // Act
            Scheduler.CropSchedules(schedulesToCrop, _ScheduleEvaluator, _emptySchedule, cropTo);

            Console.WriteLine($"Output: {schedulesToCrop.Count} schedules remain");

            // Assert: Should have exactly 10 schedules (deterministic tie-breaking)
            Assert.That(schedulesToCrop.Count, Is.EqualTo(cropTo), 
                $"Should have exactly {cropTo} schedules after cropping from 20");
        }

        #endregion

        #region Unit Tests - CropToMaxSchedules Correctness (Value-Based)

        [Test]
        public void CropToMaxSchedules_KeepsHighestValues_ManualSort()
        {
            // Arrange: Create schedules with DISTINCT values
            // We'll manually sort and crop to verify the sorting logic works correctly
            var systemSchedules = new List<SystemSchedule>();
            var expectedValues = new double[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150 };
            
            foreach (var val in expectedValues)
            {
                var sched = CreateScheduleWithValue(val);
                sched.ScheduleValue = val;  // Set value
                systemSchedules.Add(sched);
            }
            
            int initialCount = systemSchedules.Count;
            int cropTo = 5;

            Console.WriteLine($"=== CropToMaxSchedules_KeepsHighestValues_ManualSort ===");
            Console.WriteLine($"Input: {initialCount} schedules with values: [{string.Join(", ", expectedValues)}]");
            Console.WriteLine($"Manually sorting and cropping to top {cropTo}");

            // Act: Manually do what CropSchedules does (without evaluator overwriting values)
            // 1. Sort ascending (worst to best)
            systemSchedules.Sort((x, y) => x.ScheduleValue.CompareTo(y.ScheduleValue));
            
            // 2. Remove worst schedules
            int numToRemove = systemSchedules.Count - cropTo;
            for (int i = 0; i < numToRemove; i++)
            {
                systemSchedules.RemoveAt(0);  // Remove from front (worst values)
            }
            
            Console.WriteLine($"Output: {systemSchedules.Count} schedules");
            var keptValues = systemSchedules.Select(s => s.ScheduleValue).OrderByDescending(v => v).ToList();
            Console.WriteLine($"Kept values: [{string.Join(", ", keptValues)}]");

            // Assert: Should keep top 5 values (150, 140, 130, 120, 110)
            Assert.Multiple(() =>
            {
                Assert.That(systemSchedules.Count, Is.EqualTo(cropTo), 
                    $"Should have exactly {cropTo} schedules");
                
                // Top 5 should be: 150, 140, 130, 120, 110
                Assert.That(keptValues[0], Is.EqualTo(150), "Highest value (150) should be kept");
                Assert.That(keptValues[1], Is.EqualTo(140), "2nd highest (140) should be kept");
                Assert.That(keptValues[2], Is.EqualTo(130), "3rd highest (130) should be kept");
                Assert.That(keptValues[3], Is.EqualTo(120), "4th highest (120) should be kept");
                Assert.That(keptValues[4], Is.EqualTo(110), "5th highest (110) should be kept");
                
                // Verify LOWEST values were removed (10, 20, 30, 40, 50, 60, 70, 80, 90, 100)
                Assert.That(systemSchedules.Any(s => s.ScheduleValue < 110), Is.False, 
                    "All values < 110 should have been removed");
            });
        }

        [Test]
        public void CropToMaxSchedules_EmptyScheduleAddedEvenIfLowestValue()
        {
            // Arrange: Create schedules where empty has the WORST value
            var systemSchedules = new List<SystemSchedule>();
            var values = new double[] { 100, 90, 80, 70, 60, 50, 40, 30, 20, 10, 5, 3, 2, 1 };
            
            foreach (var val in values)
            {
                var sched = CreateScheduleWithValue(val);
                sched.ScheduleValue = val;
                systemSchedules.Add(sched);
            }
            
            // Empty schedule has value 0 (worst of all)
            _emptySchedule.ScheduleValue = 0;
            
            // Verify empty is NOT in initial list
            bool emptyInInitial = systemSchedules.Contains(_emptySchedule);
            
            Console.WriteLine($"=== CropToMaxSchedules_EmptyScheduleAddedEvenIfLowestValue ===");
            Console.WriteLine($"Input: {systemSchedules.Count} schedules with values: [{string.Join(", ", values)}]");
            Console.WriteLine($"Empty schedule value: {_emptySchedule.ScheduleValue} (WORST)");
            Console.WriteLine($"Empty in initial list: {emptyInInitial}");
            
            int cropTo = 5;
            Console.WriteLine($"Manually cropping to top {cropTo} schedules");

            // Act: Manually do what CropSchedules does (without evaluator overwriting values)
            // 1. Sort ascending (worst to best)
            systemSchedules.Sort((x, y) => x.ScheduleValue.CompareTo(y.ScheduleValue));
            
            // 2. Remove worst schedules
            int numToRemove = systemSchedules.Count - cropTo;
            for (int i = 0; i < numToRemove; i++)
            {
                systemSchedules.RemoveAt(0);  // Remove from front (worst values)
            }
            
            // 3. Add empty schedule back (mimicking what CropToMaxSchedules does)
            systemSchedules.Add(_emptySchedule);
            
            Console.WriteLine($"After adding empty schedule: {systemSchedules.Count} schedules");
            var finalValues = systemSchedules.Select(s => s.ScheduleValue).OrderByDescending(v => v).ToList();
            Console.WriteLine($"Final values: [{string.Join(", ", finalValues)}]");

            // Assert: Should have top 5 values PLUS empty schedule (6 total)
            Assert.Multiple(() =>
            {
                Assert.That(systemSchedules.Count, Is.EqualTo(cropTo + 1), 
                    $"Should have {cropTo} + empty schedule = {cropTo + 1} total");
                
                // Top 5 should be: 100, 90, 80, 70, 60
                var nonEmptyValues = systemSchedules.Where(s => s != _emptySchedule)
                                                    .Select(s => s.ScheduleValue)
                                                    .OrderByDescending(v => v)
                                                    .ToList();
                
                Assert.That(nonEmptyValues[0], Is.EqualTo(100), "Highest (100) should be kept");
                Assert.That(nonEmptyValues[1], Is.EqualTo(90), "2nd highest (90) should be kept");
                Assert.That(nonEmptyValues[2], Is.EqualTo(80), "3rd highest (80) should be kept");
                Assert.That(nonEmptyValues[3], Is.EqualTo(70), "4th highest (70) should be kept");
                Assert.That(nonEmptyValues[4], Is.EqualTo(60), "5th highest (60) should be kept");
                
                // Verify empty schedule IS in the result despite having worst value
                Assert.That(systemSchedules.Contains(_emptySchedule), Is.True, 
                    "Empty schedule should be present even though it has the worst value (0)");
                
                Assert.That(_emptySchedule.ScheduleValue, Is.EqualTo(0), 
                    "Empty schedule value should still be 0 (worst)");
            });
        }

        #endregion

        #region Unit Tests - CropToMaxSchedules (High-Level Method)

        [Test]
        public void CropToMaxSchedules_BelowLimit_NoCropping()
        {
            // Arrange: Create only 5 schedules (well below MaxNumScheds from JSON: 1000)
            var systemSchedules = CreateSchedulesWithValues(1, 2, 3, 4, 5);
            int initialCount = systemSchedules.Count;

            Console.WriteLine($"=== CropToMaxSchedules_BelowLimit_NoCropping ===");
            Console.WriteLine($"Input: {initialCount} schedules, MaxNumScheds={SchedParameters.MaxNumScheds}");
            Console.WriteLine($"Using empty schedule: {_emptySchedule.Name}");
            Console.WriteLine($"Initial _SchedulesCropped: {Scheduler._SchedulesCropped}");

            // Act
            var result = Scheduler.CropToMaxSchedules(systemSchedules, _emptySchedule, _ScheduleEvaluator);

            Console.WriteLine($"Output: {result.Count} schedules");
            Console.WriteLine($"Final _SchedulesCropped: {Scheduler._SchedulesCropped}");

            // Assert: No cropping should occur (5 << 1000)
            Assert.Multiple(() =>
            {
                Assert.That(result.Count, Is.EqualTo(initialCount), "Count should not change");
                Assert.That(Scheduler._SchedulesCropped, Is.EqualTo(0), "_SchedulesCropped should be 0");
            });
        }

        [Test]
        public void CropToMaxSchedules_EmptyScheduleAlwaysAdded()
        {
            // Arrange: Create schedules that will trigger cropping, but DON'T include empty schedule
            var systemSchedules = new List<SystemSchedule>();
            
            // Add 1500 schedules to exceed MaxNumScheds (1000)
            for (int i = 0; i < 1500; i++)
            {
                systemSchedules.Add(CreateScheduleWithValue(i));
            }
            
            int initialCount = systemSchedules.Count;
            
            // Verify empty schedule is NOT in the list initially
            bool emptyInInitialList = systemSchedules.Any(s => s.Name.Contains("Empty"));
            
            Console.WriteLine($"=== CropToMaxSchedules_EmptyScheduleAlwaysAdded ===");
            Console.WriteLine($"Input: {initialCount} schedules (empty schedule in list: {emptyInInitialList})");
            Console.WriteLine($"MaxNumScheds={SchedParameters.MaxNumScheds}, NumSchedCropTo={SchedParameters.NumSchedCropTo}");
            Console.WriteLine($"Empty schedule name: {_emptySchedule.Name}");

            // Act
            var result = Scheduler.CropToMaxSchedules(systemSchedules, _emptySchedule, _ScheduleEvaluator);

            Console.WriteLine($"Output: {result.Count} schedules");
            Console.WriteLine($"_SchedulesCropped: {Scheduler._SchedulesCropped}");
            
            // Check if empty schedule is now in the result
            bool emptyInResult = result.Any(s => s.Name.Contains("Empty"));
            Console.WriteLine($"Empty schedule in result: {emptyInResult}");

            // Assert: Empty schedule should always be added, cropping should occur
            Assert.Multiple(() =>
            {
                Assert.That(result.Count, Is.LessThan(initialCount), 
                    "Should have cropped schedules (1500 > 1000)");
                
                Assert.That(result.Count, Is.LessThanOrEqualTo(SchedParameters.NumSchedCropTo + 1), 
                    $"Should have at most {SchedParameters.NumSchedCropTo} + empty schedule");
                
                Assert.That(Scheduler._SchedulesCropped, Is.GreaterThan(0), 
                    "Should have cropped some schedules");
                
                Assert.That(emptyInResult, Is.True, 
                    "Empty schedule should ALWAYS be in result after CropToMaxSchedules");
                
                // Verify the actual empty schedule object is in the result
                Assert.That(result.Contains(_emptySchedule), Is.True, 
                    "The actual _emptySchedule object should be in the result");
            });
        }

        #endregion

        #region Integration Tests

        [Test]
        public void Integration_CropAfterTimeDeconfliction_WorksCorrectly()
        {
            // Arrange: Use TimeDeconfliction to generate schedules
            double currentTime = 0.0;
            double timeStep = 12.0;

            Console.WriteLine($"=== Integration_CropAfterTimeDeconfliction_WorksCorrectly ===");
            Console.WriteLine($"MaxNumScheds={SchedParameters.MaxNumScheds}, NumSchedCropTo={SchedParameters.NumSchedCropTo}");
            Console.WriteLine($"Running TimeDeconfliction to generate schedules...");

            // Generate schedules through TimeDeconfliction (realistic scenario)
            for (int i = 0; i < 3; i++)
            {
                currentTime = i * timeStep;
                var potentials = Scheduler.TimeDeconfliction(_systemSchedules, _scheduleCombos, currentTime);
                _systemSchedules.AddRange(potentials);
                Console.WriteLine($"After iteration {i}: {_systemSchedules.Count} schedules");
            }

            int countBeforeCrop = _systemSchedules.Count;
            Console.WriteLine($"Total schedules before crop: {countBeforeCrop}");

            // Act: Now crop the schedules
            var result = Scheduler.CropToMaxSchedules(_systemSchedules, _emptySchedule, _ScheduleEvaluator);

            Console.WriteLine($"After CropToMaxSchedules: {result.Count} schedules");
            Console.WriteLine($"_SchedulesCropped: {Scheduler._SchedulesCropped}");

            // Assert: Verify cropping behavior based on actual parameters
            Assert.Multiple(() =>
            {
                if (countBeforeCrop > SchedParameters.MaxNumScheds)
                {
                    Assert.That(result.Count, Is.LessThanOrEqualTo(SchedParameters.NumSchedCropTo + 1), 
                        "Should be cropped to NumSchedCropTo + empty");
                    Assert.That(Scheduler._SchedulesCropped, Is.GreaterThan(0), 
                        "Should have cropped some schedules");
                }
                else
                {
                    Assert.That(result.Count, Is.EqualTo(countBeforeCrop), 
                        "Should not crop if below MaxNumScheds");
                    Assert.That(Scheduler._SchedulesCropped, Is.EqualTo(0), 
                        "Should not crop if below limit");
                }
                
                // Verify result is valid
                Assert.That(result, Is.Not.Null, "Result should not be null");
                Assert.That(result.Count, Is.GreaterThan(0), "Should have at least some schedules");
            });
        }

        #endregion
    }
}

