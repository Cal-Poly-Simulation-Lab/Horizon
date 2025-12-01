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
    /// Tests for Checker.checkSub() method using the toy example (TwoAsset_Imaging scenario).
    /// Validates that checkSub correctly evaluates subsystems, maintains schedule hash consistency,
    /// and updates state hashes appropriately. Tests focus on the first scheduler iteration.
    /// </summary>
    [TestFixture]
    public class CheckSubUnitTest : SchedulerUnitTest
    {
        private SystemClass _system = null!;
        private Asset _asset1 = null!;
        private Domain _universe = null!;
        private List<SystemSchedule> _potentialSchedules = null!;
        private MethodInfo _checkSubMethod = null!;

        public override void Setup()
        {
            base.Setup();

            // Clear static tracking state before each test
            SubsystemCallTracker.Clear();

            program = new Horizon.Program();
            // Uses shared input files from CheckScheudle/Inputs (see README.md)
            var inputsDir = Path.Combine(CurrentTestDir, "../Inputs");
            var simPath = Path.Combine(inputsDir, "SimInput_CanPerform.json");
            var taskPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Tasks.json");
            var modelPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Model.json");

            HorizonLoadHelper(simPath, taskPath, modelPath);

            _system = program.SimSystem;
            _asset1 = program.AssetList.Single(a => a.Name == "asset1");
            _universe = program.SystemUniverse;

            // Get checkSub method via reflection (it's private static)
            _checkSubMethod = typeof(Checker).GetMethod("checkSub", 
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(_checkSubMethod, Is.Not.Null, "checkSub method should exist");
            if (_checkSubMethod == null)
                throw new InvalidOperationException("checkSub method not found via reflection");

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
        /// Calls checkSub via reflection
        /// </summary>
        private bool CallCheckSub(Subsystem subsystem, SystemSchedule schedule, Domain environment)
        {
            return (bool)_checkSubMethod!.Invoke(null, new object[] { subsystem, schedule, environment })!;
        }

        /// <summary>
        /// Resets IsEvaluated flag for ALL subsystems before testing a schedule branch.
        /// This ensures each schedule branch starts with a clean evaluation state.
        /// TODO: Remove this entire method when subsystems become stateless (IsEvaluated flag will be removed).
        /// This is a temporary workaround to ensure clean state between schedule branch tests.
        /// </summary>
        private void ResetSubsystemEvaluationState()
        {
            // Reset IsEvaluated for ALL subsystems in the system
            // This ensures each schedule branch test starts with no subsystems evaluated
            // Isolate this logic for easy removal when subsystems become stateless
            foreach (var sub in _system.Subsystems)
            {
                sub.IsEvaluated = false;
            }
        }

        /// <summary>
        /// Gets the schedule hash for a schedule
        /// </summary>
        private string GetScheduleHash(SystemSchedule schedule)
        {
            return schedule.ScheduleInfo.ScheduleHash;
        }


        #endregion

        #region Unit Tests


        /// <summary>
        /// Helper method to get unique schedule branches grouped by hash.
        /// This setup is reusable for CheckAll unit tests.
        /// Returns: Dictionary mapping schedule hash to list of schedules with that hash.
        /// </summary>
        private Dictionary<string, List<SystemSchedule>> GetUniqueScheduleBranches()
        {
            return _potentialSchedules
                .GroupBy(s => GetScheduleHash(s))
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Tests checkSub for each unique schedule branch (identified by hash).
        /// Simple test: calls checkSub for ALL subsystems on each asset, verifies boolean results only.
        /// Expected results:
        /// - IMAGING: all subsystems return true
        /// - RECHARGE: all subsystems return true
        /// - TRANSMIT: Camera and Power return true, Antenna returns false (no images initially)
        /// </summary>
        [Test]
        public void CheckSub_FirstIteration_UniqueScheduleBranches()
        {
            // 1. Use helper method to obtain all 9 unique schedule branches
            var schedulesByHash = GetUniqueScheduleBranches();
            
            // Verify there are 9 unique ones
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
                
                // Identify task assignments for each asset
                var asset2 = program.AssetList.Single(a => a.Name == "asset2");
                var asset1Task = lastEvent.GetAssetTask(_asset1);
                var asset2Task = lastEvent.GetAssetTask(asset2);
                
                string asset1TaskType = asset1Task?.Type ?? "NULL";
                string asset2TaskType = asset2Task?.Type ?? "NULL";
                
                TestContext.WriteLine($"\nSchedule Hash: {scheduleHash.Substring(0, Math.Min(16, scheduleHash.Length))}...");
                TestContext.WriteLine($"  asset1 -> {asset1TaskType}, asset2 -> {asset2TaskType}");
                
                int scheduleIndex = 0;
                
                // For each asset
                foreach (var asset in new[] { _asset1, asset2 })
                {
                    var task = asset == _asset1 ? asset1Task : asset2Task;
                    string taskType = task?.Type ?? "NULL";
                    
                    if (task != null)
                    {
                        // Get all subsystems for this asset and order them: relevant subsystem first, then respect dependencies
                        var subsystemsForAsset = _system.Subsystems.Where(s => s.Asset.Name == asset.Name).ToList();
                        var orderedSubsystems = GetOrderedSubsystems(subsystemsForAsset, task);
                        
                        // Call checkSub on each subsystem in the correct order
                        Assert.Multiple(() =>
                        {
                            foreach (var subsystem in orderedSubsystems)
                            {
                                // Use a different schedule object from the hash group for each subsystem call
                                var testSchedule = schedulesInGroup[scheduleIndex % schedulesInGroup.Count];
                                scheduleIndex++;
                                
                                bool expected = GetExpectedBooleanResult(subsystem, task);
                                bool actual = CallCheckSub(subsystem, testSchedule, _universe);
                                
                                Assert.That(actual, Is.EqualTo(expected),
                                    $"checkSub should return {expected} for {subsystem.Name} on {asset.Name} " +
                                    $"with task {taskType} in schedule {scheduleHash.Substring(0, Math.Min(8, scheduleHash.Length))}...");
                            }
                        });
                    }
                    
                    // Reset IsEvaluated states in separate code block before next asset call
                    ResetSubsystemEvaluationState();
                }
                
                // Reset IsEvaluated before next unique schedule call
                ResetSubsystemEvaluationState();
            }
        }

        /// <summary>
        /// Orders subsystems so the relevant subsystem (matching task type) is called first,
        /// then respects dependency order (dependencies before dependents).
        /// - IMAGING: Camera first (relevant), then Power (depends on Camera)
        /// - RECHARGE: Power first (relevant), then Camera and Antenna
        /// - TRANSMIT: Antenna first (relevant), but Camera must come before Antenna (dependency), then Power
        /// </summary>
        private List<Subsystem> GetOrderedSubsystems(List<Subsystem> subsystems, MissionElements.Task task)
        {
            if (task == null) return subsystems;
            
            string taskType = task.Type.ToUpper();
            var ordered = new List<Subsystem>();
            var remaining = new List<Subsystem>(subsystems);
            
            // Find the relevant subsystem for this task type
            Subsystem? relevantSub = null;
            if (taskType == "IMAGING")
            {
                relevantSub = remaining.FirstOrDefault(s => s.Name.Contains("camera", StringComparison.OrdinalIgnoreCase));
            }
            else if (taskType == "RECHARGE")
            {
                relevantSub = remaining.FirstOrDefault(s => s.Name.Contains("power", StringComparison.OrdinalIgnoreCase));
            }
            else if (taskType == "TRANSMIT")
            {
                relevantSub = remaining.FirstOrDefault(s => s.Name.Contains("antenna", StringComparison.OrdinalIgnoreCase));
            }
            
            // For TRANSMIT: Camera must come before Antenna (Antenna depends on Camera)
            if (taskType == "TRANSMIT" && relevantSub != null)
            {
                var cameraSub = remaining.FirstOrDefault(s => s.Name.Contains("camera", StringComparison.OrdinalIgnoreCase));
                if (cameraSub != null && cameraSub != relevantSub)
                {
                    ordered.Add(cameraSub);
                    remaining.Remove(cameraSub);
                }
            }
            
            // Add the relevant subsystem first
            if (relevantSub != null)
            {
                ordered.Add(relevantSub);
                remaining.Remove(relevantSub);
            }
            
            // Add remaining subsystems
            ordered.AddRange(remaining);
            
            return ordered;
        }

        /// <summary>
        /// Gets expected boolean result for a subsystem given a task.
        /// - IMAGING: all subsystems return true
        /// - RECHARGE: all subsystems return true
        /// - TRANSMIT: Camera and Power return true, Antenna returns false (no images initially)
        /// </summary>
        private bool GetExpectedBooleanResult(Subsystem subsystem, MissionElements.Task task)
        {
            if (task == null) return true;
            
            string subName = subsystem.Name.ToLower();
            string taskType = task.Type.ToUpper();
            
            // IMAGING: all return true
            if (taskType == "IMAGING")
                return true;
            
            // RECHARGE: all return true
            if (taskType == "RECHARGE")
                return true;
            
            // TRANSMIT: Camera and Power return true, Antenna returns false
            if (taskType == "TRANSMIT")
            {
                if (subName.Contains("antenna"))
                    return false; // Antenna fails (no images initially)
                return true; // Camera and Power pass
            }
            
            return true; // Default
        }


        #endregion
    }
}

