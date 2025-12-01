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

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Barebones simple tests for Constraint.Accepts()
    /// Tests that constraints pass when they should and fail when they should.
    /// Uses the TwoAsset_Imaging scenario which has a FAIL_IF_LOWER constraint on power (>= 10).
    /// </summary>
    [TestFixture]
    public class CheckConstraintsUnitTest : SchedulerUnitTest
    {
        private SystemClass _system = null!;
        private Constraint _powerConstraint = null!;
        private Asset _asset1 = null!;

        public override void Setup()
        {
            base.Setup();

            program = new Horizon.Program();
            // Uses shared input files from CheckSchedule/Inputs (see README.md)
            var inputsDir = Path.Combine(CurrentTestDir, "../Inputs");
            var simPath = Path.Combine(inputsDir, "SimInput_TwoAssetImaging_ToyExample.json");
            var taskPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Tasks.json");
            var modelPath = Path.Combine(inputsDir, "TwoAsset_Imaging_Model.json");

            HorizonLoadHelper(simPath, taskPath, modelPath);

            _system = program.SimSystem;
            _asset1 = program.AssetList.Single(a => a.Name == "asset1");
            
            // Get the constraint from asset1 (FAIL_IF_LOWER, value=10, on checker_power)
            _powerConstraint = _system.Constraints.First(c => c.Name == "asset1_10W_power_constraint");
        }

        #region Helper Methods

        private SystemState CreateStateWithPower(double powerValue)
        {
            // Create a fresh state (don't copy initial, which has values at time 0.0)
            // We'll add our test value at time 1.0 to avoid conflicts
            var state = new SystemState();
            var key = new StateVariableKey<double>("asset1.checker_power");
            state.AddValue(key, 1.0, powerValue);
            return state;
        }

        #endregion

        #region FAIL_IF_LOWER Constraint Tests (from TwoAsset_Imaging scenario)

        [Test]
        public void ConstraintAccepts_PowerAboveLimit_ReturnsTrue()
        {
            // Power = 15, constraint = FAIL_IF_LOWER (>= 10)
            // Should PASS: 15 >= 10
            var state = CreateStateWithPower(15.0);
            var result = _powerConstraint.Accepts(state);
            Assert.That(result, Is.True, "Power 15 should pass constraint (>= 10)");
        }

        [Test]
        public void ConstraintAccepts_PowerAtLimit_ReturnsTrue()
        {
            // Power = 10, constraint = FAIL_IF_LOWER (>= 10)
            // Should PASS: 10 >= 10 (at limit is acceptable)
            var state = CreateStateWithPower(10.0);
            var result = _powerConstraint.Accepts(state);
            Assert.That(result, Is.True, "Power 10 should pass constraint (>= 10, at limit)");
        }

        [Test]
        public void ConstraintAccepts_PowerBelowLimit_ReturnsFalse()
        {
            // Power = 5, constraint = FAIL_IF_LOWER (>= 10)
            // Should FAIL: 5 < 10
            var state = CreateStateWithPower(5.0);
            var result = _powerConstraint.Accepts(state);
            Assert.That(result, Is.False, "Power 5 should fail constraint (must be >= 10)");
        }

        [Test]
        public void ConstraintAccepts_PowerAtZero_ReturnsFalse()
        {
            // Power = 0, constraint = FAIL_IF_LOWER (>= 10)
            // Should FAIL: 0 < 10
            var state = CreateStateWithPower(0.0);
            var result = _powerConstraint.Accepts(state);
            Assert.That(result, Is.False, "Power 0 should fail constraint (must be >= 10)");
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ConstraintAccepts_PowerJustAboveLimit_ReturnsTrue()
        {
            // Power = 10.001, constraint = FAIL_IF_LOWER (>= 10)
            // Should PASS: 10.001 >= 10
            var state = CreateStateWithPower(10.001);
            var result = _powerConstraint.Accepts(state);
            Assert.That(result, Is.True, "Power 10.001 should pass constraint (just above limit)");
        }

        [Test]
        public void ConstraintAccepts_PowerJustBelowLimit_ReturnsFalse()
        {
            // Power = 9.999, constraint = FAIL_IF_LOWER (>= 10)
            // Should FAIL: 9.999 < 10
            var state = CreateStateWithPower(9.999);
            var result = _powerConstraint.Accepts(state);
            Assert.That(result, Is.False, "Power 9.999 should fail constraint (just below limit)");
        }

        #endregion
    }
}
