// Copyright (c) 2025 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using HSFUniverse;
using Utilities;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Focused tests for Subsystem.CheckDependentSubsystems() using the two-asset imaging scenario.
    /// Validates dependency ordering, true/false propagation, constraint enforcement,
    /// and state updates for the scripted power/camera/antenna subsystems.
    /// </summary>
    [TestFixture]
    public class CheckDependentSubsystemsUnitTest : SchedulerUnitTest
    {
        private Asset _asset1 = null!;
        private Asset _asset2 = null!;
        private TestPowerSubsystem _asset1Power = null!;
        private TestPowerSubsystem _asset2Power = null!;
        private TestCameraSubsystem _asset1Camera = null!;
        private TestCameraSubsystem _asset2Camera = null!;
        private TestAntennaSubsystem _asset1Antenna = null!;
        private TestAntennaSubsystem _asset2Antenna = null!;
        private Domain _environment = null!;
        private SystemClass _system = null!;
        private Dictionary<string, Task> _tasksByType = null!;

        private static readonly MethodInfo CheckSubsMethod =
            typeof(Checker).GetMethod("checkSubs", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Unable to locate Checker.checkSubs via reflection.");

        public override void Setup()
        {
            base.Setup();

            program = new Horizon.Program();
            var simPath = Path.Combine(CurrentTestDir, "Inputs/SimInput_CanPerform.json");
            var taskPath = Path.Combine(CurrentTestDir, "Inputs/TwoAsset_Imaging_Tasks.json");
            var modelPath = Path.Combine(CurrentTestDir, "Inputs/TwoAsset_Imaging_Model.json");

            HorizonLoadHelper(simPath, taskPath, modelPath);

            _system = program.SimSystem;
            _environment = program.SystemUniverse;

            _asset1 = program.AssetList.Single(a => a.Name == "asset1");
            _asset2 = program.AssetList.Single(a => a.Name == "asset2");

            _asset1Power = program.SubList.OfType<TestPowerSubsystem>().Single(s => s.Asset == _asset1);
            _asset2Power = program.SubList.OfType<TestPowerSubsystem>().Single(s => s.Asset == _asset2);
            _asset1Camera = program.SubList.OfType<TestCameraSubsystem>().Single(s => s.Asset == _asset1);
            _asset2Camera = program.SubList.OfType<TestCameraSubsystem>().Single(s => s.Asset == _asset2);
            _asset1Antenna = program.SubList.OfType<TestAntennaSubsystem>().Single(s => s.Asset == _asset1);
            _asset2Antenna = program.SubList.OfType<TestAntennaSubsystem>().Single(s => s.Asset == _asset2);

            _tasksByType = program.SystemTasks
                .ToArray()
                .GroupBy(t => t.Type.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First());
        }

        #region Helpers

        private static string KeyFor(Asset asset, string suffix) =>
            $"{asset.Name.ToLowerInvariant()}.{suffix}";

        private static void SetStateValue(SystemState state, Asset asset, string suffix, double value)
        {
            var key = new StateVariableKey<double>(KeyFor(asset, suffix));
            state.AddValue(key, 0.0, value);
        }

        private (Event evt, SystemSchedule schedule, SystemState state) BuildSchedule(
            Task asset1Task,
            Task asset2Task,
            Action<SystemState>? stateSetup = null)
        {
            var stateCopy = new SystemState(program.InitialSysState, true);
            stateSetup?.Invoke(stateCopy);

            var tasks = new Dictionary<Asset, Task>
            {
                { _asset1, asset1Task },
                { _asset2, asset2Task }
            };

            var evt = new Event(tasks, stateCopy);
            var eventTimes = new Dictionary<Asset, double>
            {
                { _asset1, 0.0 },
                { _asset2, 0.0 }
            };
            var endTimes = new Dictionary<Asset, double>
            {
                { _asset1, 5.0 },
                { _asset2, 5.0 }
            };

            evt.SetEventStart(eventTimes);
            evt.SetEventEnd(endTimes);
            evt.SetTaskStart(eventTimes);
            evt.SetTaskEnd(endTimes);

            var schedule = new SystemSchedule(program.InitialSysState, "unit-test");
            schedule.AllStates = new StateHistory(schedule.AllStates, evt);

            return (evt, schedule, stateCopy);
        }

        private Task TaskOf(string type) => _tasksByType[type.ToUpperInvariant()];

        private void ResetAllSubsystems()
        {
            foreach (var subsystem in program.SubList)
            {
                subsystem.IsEvaluated = false;
                subsystem.Task = null;
                subsystem.NewState = null;
            }
        }

        private bool InvokeCheckSubs(List<Subsystem> subsystems, SystemSchedule schedule) =>
            (bool)CheckSubsMethod.Invoke(null, new object[] { subsystems, schedule, _environment });

        private List<Subsystem> AssetSubsystems(Asset asset) =>
            program.SubList.Where(s => s.Asset == asset).ToList();

        #endregion

        #region Dependency Ordering & Propagation

        [Test]
        public void PowerEvaluation_EvaluatesAllDependenciesFirst()
        {
            ResetAllSubsystems();
            var imagingTask = TaskOf("IMAGING");
            var rechargeTask = TaskOf("RECHARGE");
            var (evt, _, _) = BuildSchedule(imagingTask, rechargeTask);

            _asset1Power.CheckDependentSubsystems(evt, _environment);

            var evaluationFlags = new[]
            {
                _asset1Camera.IsEvaluated,
                _asset1Antenna.IsEvaluated,
                _asset1Power.IsEvaluated
            };

            Assert.That(evaluationFlags, Is.EqualTo(new[] { true, true, true }));
        }

        [Test]
        public void PowerEvaluation_ReturnsTrue_WhenDependentsPass()
        {
            ResetAllSubsystems();
            var imagingTask = TaskOf("IMAGING");
            var (evt, _, _) = BuildSchedule(imagingTask, TaskOf("RECHARGE"));

            var result = _asset1Power.CheckDependentSubsystems(evt, _environment);

            Assert.That(result, Is.True);
        }

        [Test]
        public void PowerEvaluation_ReturnsFalse_WhenDependentFails()
        {
            ResetAllSubsystems();
            var transmitTask = TaskOf("TRANSMIT");
            var (evt, _, state) = BuildSchedule(transmitTask, TaskOf("RECHARGE"), s =>
            {
                SetStateValue(s, _asset1, "num_images_stored", 0);
            });

            var result = _asset1Power.CheckDependentSubsystems(evt, _environment);

            Assert.That(result, Is.False);
        }

        #endregion

        #region Camera Subsystem

        [Test]
        public void CameraEvaluation_FailsWhenBufferIsFull()
        {
            ResetAllSubsystems();
            var imagingTask = TaskOf("IMAGING");
            var (evt, _, state) = BuildSchedule(imagingTask, TaskOf("RECHARGE"), s =>
            {
                SetStateValue(s, _asset1, "num_images_stored", 10);
            });

            var result = _asset1Camera.CheckDependentSubsystems(evt, _environment);
            var tuple = (result, state.GetLastValue(new StateVariableKey<double>(KeyFor(_asset1, "num_images_stored"))).Item2);

            Assert.That(tuple, Is.EqualTo((false, 10d)));
        }

        [Test]
        public void CameraEvaluation_IncrementsImageCountOnSuccess()
        {
            ResetAllSubsystems();
            var imagingTask = TaskOf("IMAGING");
            var (evt, _, state) = BuildSchedule(imagingTask, TaskOf("RECHARGE"), s =>
            {
                SetStateValue(s, _asset1, "num_images_stored", 0);
            });

            var result = _asset1Camera.CheckDependentSubsystems(evt, _environment);
            var tuple = (result, state.GetLastValue(new StateVariableKey<double>(KeyFor(_asset1, "num_images_stored"))).Item2);

            Assert.That(tuple, Is.EqualTo((true, 1d)));
        }

        #endregion

        #region Antenna Subsystem

        [Test]
        public void AntennaEvaluation_TransmitsWhenImagesAvailable()
        {
            ResetAllSubsystems();
            var transmitTask = TaskOf("TRANSMIT");
            var (evt, _, state) = BuildSchedule(transmitTask, TaskOf("RECHARGE"), s =>
            {
                SetStateValue(s, _asset1, "num_images_stored", 2);
                SetStateValue(s, _asset1, "num_transmissions", 0);
            });

            var result = _asset1Antenna.CheckDependentSubsystems(evt, _environment);
            var stored = state.GetLastValue(new StateVariableKey<double>(KeyFor(_asset1, "num_images_stored"))).Item2;
            var transmissions = state.GetLastValue(new StateVariableKey<double>(KeyFor(_asset1, "num_transmissions"))).Item2;

            Assert.That((result, stored, transmissions), Is.EqualTo((true, 1d, 1d)));
        }

        [Test]
        public void AntennaEvaluation_FailsWithoutImages()
        {
            ResetAllSubsystems();
            var transmitTask = TaskOf("TRANSMIT");
            var (evt, _, state) = BuildSchedule(transmitTask, TaskOf("RECHARGE"), s =>
            {
                SetStateValue(s, _asset1, "num_images_stored", 0);
                SetStateValue(s, _asset1, "num_transmissions", 0);
            });

            var result = _asset1Antenna.CheckDependentSubsystems(evt, _environment);
            var stored = state.GetLastValue(new StateVariableKey<double>(KeyFor(_asset1, "num_images_stored"))).Item2;
            var transmissions = state.GetLastValue(new StateVariableKey<double>(KeyFor(_asset1, "num_transmissions"))).Item2;

            Assert.That((result, stored, transmissions), Is.EqualTo((false, 0d, 0d)));
        }

        #endregion

        #region Power Subsystem State Updates

        [Test]
        public void PowerEvaluation_RechargeRaisesEnergy()
        {
            ResetAllSubsystems();
            var rechargeTask = TaskOf("RECHARGE");
            var (evt, _, state) = BuildSchedule(rechargeTask, rechargeTask, s =>
            {
                SetStateValue(s, _asset1, "checker_power", 50);
            });

            var result = _asset1Power.CheckDependentSubsystems(evt, _environment);
            var newValue = state.GetLastValue(new StateVariableKey<double>(KeyFor(_asset1, "checker_power"))).Item2;

            Assert.That((result, newValue), Is.EqualTo((true, 75d)));
        }

        [Test]
        public void PowerEvaluation_RechargeFailsWhenOverMax()
        {
            ResetAllSubsystems();
            var rechargeTask = TaskOf("RECHARGE");
            var (evt, _, state) = BuildSchedule(rechargeTask, rechargeTask, s =>
            {
                SetStateValue(s, _asset1, "checker_power", 90);
            });

            var result = _asset1Power.CheckDependentSubsystems(evt, _environment);
            var newValue = state.GetLastValue(new StateVariableKey<double>(KeyFor(_asset1, "checker_power"))).Item2;

            Assert.That((result, newValue), Is.EqualTo((false, 90d)));
        }

        #endregion

        #region checkSubs Behavior

        [Test]
        public void CheckSubs_ReturnsTrue_WhenAllSubsystemsPass()
        {
            ResetAllSubsystems();
            var (_, schedule, _) = BuildSchedule(TaskOf("RECHARGE"), TaskOf("RECHARGE"));

            var result = InvokeCheckSubs(AssetSubsystems(_asset1), schedule);

            Assert.That(result, Is.True);
        }

        [Test]
        public void CheckSubs_ReturnsFalse_WhenAnySubsystemFails()
        {
            ResetAllSubsystems();
            var (_, schedule, _) = BuildSchedule(TaskOf("TRANSMIT"), TaskOf("RECHARGE"), s =>
            {
                SetStateValue(s, _asset1, "num_images_stored", 0);
            });

            var result = InvokeCheckSubs(AssetSubsystems(_asset1), schedule);

            Assert.That(result, Is.False);
        }

        #endregion

        #region Constraint Enforcement

        [Test]
        public void CheckSchedule_FailsConstraint_WhenPowerDropsBelowThreshold()
        {
            ResetAllSubsystems();
            var (_, schedule, _) = BuildSchedule(TaskOf("TRANSMIT"), TaskOf("RECHARGE"), s =>
            {
                SetStateValue(s, _asset1, "checker_power", 25);
                SetStateValue(s, _asset1, "num_images_stored", 1);
            });

            var result = Checker.CheckSchedule(_system, schedule);

            Assert.That(result, Is.False);
        }

        #endregion
    }
}

