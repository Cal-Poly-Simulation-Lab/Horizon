# CheckConstraintsUnitTest

## Overview

Barebones simple unit tests for `Constraint.Accepts()` method. Tests validate that constraints correctly pass when they should and fail when they should.

## Test Approach

The test suite uses the **TwoAsset_Imaging** scenario which contains a `FAIL_IF_LOWER` constraint on power (value >= 10). Tests create simple `SystemState` objects with specific power values and verify that `Constraint.Accepts()` returns the correct boolean result.

## Input Files

**Note:** This test suite uses **shared input files** from the `CheckSchedule/Inputs/` directory:

- `../Inputs/SimInput_CanPerform.json` - Simulation parameters
- `../Inputs/TwoAsset_Imaging_Tasks.json` - Task definitions
- `../Inputs/TwoAsset_Imaging_Model.json` - System model with constraint

The model file defines a constraint on `asset1.Power`:
- **Type:** `FAIL_IF_LOWER`
- **Value:** `10`
- **State Variable:** `checker_power`
- **Name:** `asset1_10W_power_constraint`

This constraint requires that `checker_power >= 10` for the constraint to pass.

## Test Cases

### FAIL_IF_LOWER Constraint Tests

1. **ConstraintAccepts_PowerAboveLimit_ReturnsTrue** - Power = 15, should pass (15 >= 10)
2. **ConstraintAccepts_PowerAtLimit_ReturnsTrue** - Power = 10, should pass (10 >= 10, at limit)
3. **ConstraintAccepts_PowerBelowLimit_ReturnsFalse** - Power = 5, should fail (5 < 10)
4. **ConstraintAccepts_PowerAtZero_ReturnsFalse** - Power = 0, should fail (0 < 10)

### Edge Cases

5. **ConstraintAccepts_PowerJustAboveLimit_ReturnsTrue** - Power = 10.001, should pass
6. **ConstraintAccepts_PowerJustBelowLimit_ReturnsFalse** - Power = 9.999, should fail

## Implementation Details

The test creates fresh `SystemState` objects (not copying from initial state) and adds power values at time 1.0 to avoid conflicts with initial state values at time 0.0. The constraint checks the maximum value in the state profile using `prof.Max()`, so a single value at time 1.0 is sufficient for testing.

## Constraint Logic

For `FAIL_IF_LOWER` constraint type:
- Returns `true` if `prof.Max() >= constraintValue` (value is at or above limit)
- Returns `false` if `prof.Max() < constraintValue` (value is below limit)

