// Copyright (c) 2025 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using NUnit.Framework;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using UserModel;
using HSFUniverse;
using Utilities;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Unit tests for Subsystem.CanPerform() across all subsystem types
    /// 
    /// TEST STRATEGY:
    /// - Use TestCanPerformSubsystem (simple, deterministic)
    /// - Test all 3 subsystem types: hardcoded C#, ScriptedCS, Scripted (Python)
    /// - Verify: correct true/false returns, state key loading, state updates
    /// 
    /// ARCHITECTURAL VALIDATION:
    /// - Event start/stop times NOT modified by subsystems (design decision)
    /// - Times set in SystemSchedule constructor only
    /// - May change in future - documented for thesis
    /// </summary>
    [TestFixture]
    public class CanPerformUnitTest : SchedulerUnitTest
    {
        [TearDown]
        public override void TearDown()
        {
            // Reset scheduler static/shared state between tests
            SchedulerStep = -1;
            _schedID = 0;
            _emptySchedule = null;
            
            // Clear collections
            _systemSchedules.Clear();
            _scheduleCombos.Clear();
            _potentialSystemSchedules.Clear();
            _systemCanPerformList.Clear();
            
            // CRITICAL: Create new program to reset InitialSysState
            program = new Horizon.Program();
            _testInitialSysState = new SystemState();
            
            base.TearDown();
        }
        
        #region Helper Methods
        
        /// <summary>
        /// Load scenario and generate potential schedules up to (but before) Checker
        /// Uses proper GenerateSchedules flow: EmptySchedule → TimeDeconfliction → Crop
        /// </summary>
        private (Asset asset, Subsystem subsystem, List<SystemSchedule> potentialSchedules, Domain universe) 
            LoadAndGeneratePotentialSchedules(string modelFile)
        {
            // Load using base class helper
            program = HorizonLoadHelper(
                Path.Combine(CurrentTestDir, "Inputs/SimInput_CanPerform.json"),
                Path.Combine(CurrentTestDir, "Inputs/OneTaskInput.json"), 
                Path.Combine(CurrentTestDir, modelFile)
            );
            
            // Initialize empty schedule (Flow Step 1)
            Scheduler.InitializeEmptySchedule(_systemSchedules, _testInitialSysState);
            _emptySchedule = Scheduler.emptySchedule;
            
            // Generate schedule combos (Flow Step 2)
            _scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(
                _testSimSystem, 
                _testSystemTasks, 
                _scheduleCombos, 
                SimParameters.SimStartSeconds, 
                SimParameters.SimEndSeconds
            );
            
            // TimeDeconfliction (Flow Step 3)
            _potentialSystemSchedules = Scheduler.TimeDeconfliction(
                _systemSchedules, 
                _scheduleCombos, 
                SimParameters.SimStartSeconds
            );
            
            // Crop to max (Flow Step 4)
            _systemSchedules = Scheduler.CropToMaxSchedules(_potentialSystemSchedules, _emptySchedule, _ScheduleEvaluator, false);
            
            return (program.AssetList[0], program.SubList[0], _potentialSystemSchedules, program.SystemUniverse);
        }
        
        #endregion
        
        #region Test: Basic CanPerform Behavior
        
        [TestCase("Inputs/TestCanPerformModel_ScriptedCS.json")]
        [TestCase("Inputs/TestCanPerformModel_Scripted.json", TestName = "Scripted")] // TODO: Debug Python
        public void CanPerform_IterationLoop_ReturnsCorrectly(string modelFile)
        {
            // SETUP: maxIterations=5
            var (asset, subsystem, potentialSchedules, universe) = LoadAndGeneratePotentialSchedules(modelFile);
            var evt = potentialSchedules[0].AllStates.Events.ToList()[0];
            var iterKey = new StateVariableKey<int>(asset.Name + ".iteration");
            
            // Initial: iteration=0
            Assert.That(evt.State.GetLastValue(iterKey).Item2, Is.EqualTo(0));
            
            // Loop: Call CanPerform 5 times, manually advance task start time each call
            for (int i = 0; i < 5; i++)
            {
                // Set task start to new time so subsystem adds at unique time
                double time = i * 12.0;
                evt.SetTaskStart(new Dictionary<Asset, double> { { asset, time } });
                
                // Set NewState (normally done by CheckDependentSubsystems before calling CanPerform)
                subsystem.NewState = evt.State;
                
                bool result = subsystem.CanPerform(evt, universe);
                int iterValue = evt.State.GetLastValue(iterKey).Value;
                
                if (i < 4)
                {
                    // Iterations 1-4: should pass
                    Assert.That(result, Is.True, $"Iteration {i+1} should return true");
                    Assert.That(iterValue, Is.EqualTo(i + 1), $"Iteration value = {i+1}");
                }
                else
                {
                    // Iteration 5: should fail (at max)
                    Assert.That(result, Is.False, "Iteration 5 should return false (at max)");
                    Assert.That(iterValue, Is.EqualTo(5));
                }
            }
        }
        
        // [TestCase("Inputs/TestCanPerformModel_ScriptedCS_Max10.json", 10, 42.0, TestName = "ScriptedCS_Max10")]
        // [TestCase("Inputs/TestCanPerformModel_ScriptedCS.json", 5, 99.9, TestName = "ScriptedCS_Max5")]
        // [TestCase("Inputs/TestCanPerformModel_Scripted.json", 5, 99.9, TestName = "Scripted")]
        // public void CanPerform_ParametersLoadCorrectly(string modelFile, int expectedMax, double expectedTestParam)
        // {
        //     // SETUP
        //     var (asset, subsystem, potentialSchedules, universe) = LoadAndGeneratePotentialSchedules(modelFile);
        //     var evt = potentialSchedules[0].AllStates.Events.Peek();
        //     var iterKey = new StateVariableKey<int>(asset.Name + ".iteration");

        //     Assert.That(subsystem.Get
        //     GetTestParameter() => _testParameter;
            
        //     // Force iteration to (max-1)
        //     evt.State.Idata[iterKey] = new HSFProfile<int>(12.0, expectedMax - 1);
        //     evt.SetTaskStart(new Dictionary<Asset, double> { { asset, 24.0 } });
            
        //     // CALL: Should return false (hits max)
        //     bool result = subsystem.CanPerform(evt, universe);
        //     Assert.That(result, Is.False, $"Should fail at max={expectedMax}");
        // }
        
        #endregion
        
        #region Test: Task Timing Manipulation
        
        [TestCase("Inputs/TaskTimeManipulator_ScriptedCS.json")]
        [TestCase("Inputs/TaskTimeManipulator_Scripted.json")]
        public void CanPerform_CanModifyTaskTimes(string modelFile)
        {
            // SETUP: Load TaskTimeManipulator (taskStart +5.0, taskEnd -3.0)
            var prog = HorizonLoadHelper(
                Path.Combine(CurrentTestDir, "Inputs/SimInput_CanPerform.json"),
                Path.Combine(CurrentTestDir, "Inputs/OneTaskInput.json"),
                Path.Combine(CurrentTestDir, modelFile)
            );
            
            var asset = prog.AssetList[0];
            var subsystem = prog.SubList[0];
            var evt = new Event(new System.Collections.Generic.Dictionary<Asset, MissionElements.Task>(), prog.InitialSysState);
            evt.SetTaskStart(new System.Collections.Generic.Dictionary<Asset, double> { { asset, 10.0 } });
            evt.SetTaskEnd(new System.Collections.Generic.Dictionary<Asset, double> { { asset, 20.0 } });
            
            subsystem.NewState = evt.State;
            
            // CALL: CanPerform (should shift times)
            subsystem.CanPerform(evt, prog.SystemUniverse);
            
            // VERIFY: Task times modified
            Assert.That(evt.GetTaskStart(asset), Is.EqualTo(15.0), "TaskStart: 10 + 5 = 15");
            Assert.That(evt.GetTaskEnd(asset), Is.EqualTo(17.0), "TaskEnd: 20 - 3 = 17");
        }
        
        [TestCase("Inputs/EventTimeManipulator_ScriptedCS.json", TestName = "ScriptedCS")]
        [TestCase("Inputs/EventTimeManipulator_Scripted.json", TestName = "Scripted")]
        public void CanPerform_CanModifyEventTimes(string modelFile)
        {
            // SETUP: Load TaskTimeManipulator (eventStart +7.0, eventEnd -2.5, task shifts=0)
            var prog = HorizonLoadHelper(
                Path.Combine(CurrentTestDir, "Inputs/SimInput_CanPerform.json"),
                Path.Combine(CurrentTestDir, "Inputs/OneTaskInput.json"),
                Path.Combine(CurrentTestDir, modelFile)
            );
            
            var asset = prog.AssetList[0];
            var subsystem = prog.SubList[0];
            var evt = new Event(new System.Collections.Generic.Dictionary<Asset, MissionElements.Task>(), prog.InitialSysState);
            evt.SetEventStart(new System.Collections.Generic.Dictionary<Asset, double> { { asset, 5.0 } });
            evt.SetEventEnd(new System.Collections.Generic.Dictionary<Asset, double> { { asset, 25.0 } });
            
            subsystem.NewState = evt.State;
            
            // CALL: CanPerform (should shift event times)
            subsystem.CanPerform(evt, prog.SystemUniverse);
            
            // VERIFY: Event times modified
            Assert.That(evt.GetEventStart(asset), Is.EqualTo(12.0), "EventStart: 5 + 7 = 12");
            Assert.That(evt.GetEventEnd(asset), Is.EqualTo(22.5), "EventEnd: 25 - 2.5 = 22.5");
        }
        
        #endregion
        
        #region Aeolus CanPerform (Legacy - may remove after TestCanPerformSubsystem validated)
        
        // // These tests use full Aeolus subsystems (Power, EOSensor, SSDR)
        // // More complex, less isolated
        // // Will likely be moved to integration tests or removed
        
        // [Test]
        // public void CanPerform_Aeolus_Power_UpdatesDOD()
        // {
        //     // Legacy test with real Power subsystem
        //     Assert.Fail("TODO: Migrate or remove");
        // }
        
        // [Test]
        // public void CanPerform_Aeolus_EOSensor_UpdatesPixels()
        // {
        //     // Legacy test with real EOSensor subsystem
        //     Assert.Fail("TODO: Migrate or remove");
        // }
        
        // [Test]
        // public void CanPerform_Aeolus_SSDR_UpdatesDataBuffer()
        // {
        //     // Legacy test with real SSDR subsystem
        //     Assert.Fail("TODO: Migrate or remove");
        // }
        
        #endregion
    }
}

