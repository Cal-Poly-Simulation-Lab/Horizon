# CheckScheduleUnitTest

## Overview

Tests the `Checker.CheckSchedule()` method, which is responsible for evaluating whether an entire proposed schedule can be executed by the system. This test validates that `CheckSchedule` correctly returns boolean results (true/false) for all unique schedule branches and that state is correctly updated for passing schedules.

## Test Objective

**Primary Goal:** Verify that `CheckSchedule` returns the correct boolean result for each unique schedule branch and that state is correctly updated for each asset in passing schedules.

**Key Validations:**
- `CheckSchedule` returns `true` when schedules should pass
- `CheckSchedule` returns `false` when schedules should fail
- State is correctly updated for each asset in passing schedules
- Fail-fast behavior: asset2 is not evaluated if asset1 fails
- Constraints correctly cause schedule failures even when tasks pass

## Test Scenarios

### Test 1: Basic CheckSchedule Validation

Uses the **TwoAsset_Imaging** toy example scenario:
- **Assets:** `asset1` and `asset2` (both have Power, Camera, and Antenna subsystems)
- **Tasks:** IMAGING, TRANSMIT, RECHARGE
- **Dependencies:** 
  - Power depends on Camera and Antenna
  - Antenna depends on Camera
  - Camera has no dependencies
- **Constraints:** 
  - `asset1` has a Power constraint (FAIL_IF_LOWER, value: 10)
  - `asset2` has no constraints

### Test 2: Constraint Failure Validation

Uses a modified model with a `FAIL_IF_HIGHER` constraint:
- **Constraint:** Power must be <= 75 (fails if power > 75)
- **Purpose:** Verifies that constraints correctly cause schedule failures even when tasks themselves pass
- **Input File:** `TwoAsset_Imaging_Model_ConstraintPowerMax75.json`

## Input Files

Located in `../Inputs/`:
- `SimInput_CanPerform.json` - Simulation parameters
- `TwoAsset_Imaging_Tasks.json` - Task definitions (RECHARGE, IMAGING, TRANSMIT)
- `TwoAsset_Imaging_Model.json` - System model for basic validation test
- `TwoAsset_Imaging_Model_ConstraintPowerMax75.json` - System model with FAIL_IF_HIGHER constraint

## Test Setup

### SetupFirstIteration()

Simulates the first scheduler iteration to generate potential schedules:
1. **Initialize Empty Schedule:** Creates the baseline empty schedule
2. **Generate Schedule Combos:** Creates exhaustive access combinations (all possible task assignments)
3. **TimeDeconfliction:** Creates potential schedules by combining the empty schedule with each access combination

This generates `_potentialSchedules`, which contains all possible schedule branches from the first iteration.

### GetUniqueScheduleBranches()

Groups `_potentialSchedules` by schedule hash to identify unique schedule branches. Each unique hash represents a distinct task assignment combination:
- **Expected:** 9 unique schedule branches (3 tasks × 2 assets = 9 combinations)

## Test Flow

### CheckSchedule_FirstIteration_UniqueScheduleBranches()

**Step 1: Obtain and Verify Unique Schedule Branches**
- Calls `GetUniqueScheduleBranches()` to get all unique schedule branches grouped by hash
- Verifies there are exactly 9 unique schedule branches

**Step 2: For Each Unique Schedule Branch**
- Identifies task assignments for each asset (asset1 and asset2)
- Determines if the schedule should pass using `ShouldSchedulePass()`
- Calls `CheckSchedule` on the representative schedule
- Verifies the boolean result matches the expected result

**Step 3: State Verification (Passing Schedules Only)**
- **IMPORTANT:** Only verifies state for schedules that actually passed `CheckSchedule`
- This respects fail-fast behavior: if asset1 fails, asset2 is not evaluated, so its state should not be checked
- For each asset with a task in a passing schedule:
  - Verifies state changes match expected values based on task type

### CheckSchedule_WithConstraintPowerMax75_RechargeFailsConstraint()

**Step 1: Load Constraint Model**
- Loads the model with `FAIL_IF_HIGHER` constraint (power > 75 fails)
- Sets up first scheduler iteration

**Step 2: For Each Unique Schedule Branch**
- Determines expected result based on constraint and task types:
  - **RECHARGE on asset1:** Schedule fails (power 75 → 100, which is > 75)
  - **IMAGING on asset1:** Schedule passes (power 75 → 65, which is <= 75)
  - **TRANSMIT on asset1:** Schedule fails (Antenna fails due to no images, not constraint)
  - **TRANSMIT on asset2:** Schedule fails (Antenna fails due to no images)
- Calls `CheckSchedule` and verifies boolean result

## Expected Boolean Results

### ShouldSchedulePass() (Test 1)

Determines if a schedule should pass based on task assignments:
- **TRANSMIT in EITHER asset:** Schedule should fail (Antenna fails because no images are stored initially)
- **All other combinations:** Schedule should pass

### Constraint-Based Results (Test 2)

- **RECHARGE on asset1:** Schedule fails (constraint: power 75 → 100, which is > 75)
- **IMAGING on asset1:** Schedule passes (constraint: power 75 → 65, which is <= 75)
- **TRANSMIT on asset1:** Schedule fails (Antenna fails, not constraint)
- **TRANSMIT on asset2:** Schedule fails (Antenna fails, not constraint)

## Expected State Changes

### VerifyStateForAsset()

Verifies state changes for each asset in passing schedules:

**IMAGING Task:**
- **Power:** Decreases by 10 (from 75 to 65)
- **Images:** Increases by 1 (from 0 to 1)

**RECHARGE Task:**
- **Power:** Increases by 25 (from 75 to 100)

**TRANSMIT Task:**
- State verification is not performed (schedules with TRANSMIT fail)

**Initial State Values:**
- `checker_power`: 75.0 (both assets)
- `num_images_stored`: 0.0 (both assets)

## CheckSchedule Behavior

### Constraint Evaluation

`CheckSchedule` first evaluates subsystems that are part of constraints:
1. For `asset1`: Power is in a constraint, so it's evaluated first
   - Power's `CheckDependentSubsystems` evaluates Camera and Antenna first (dependencies)
   - Then Power's `CanPerform` is called
   - Constraint is checked (Power must meet constraint criteria)

### Remaining Subsystems

After constraint evaluation, `checkSubs` is called for all remaining subsystems:
- For `asset2`: Camera, Antenna, and Power are evaluated (no constraints)
- Each subsystem's `CheckDependentSubsystems` is called, which:
  - Evaluates dependencies first (if not already evaluated)
  - Then calls the subsystem's `CanPerform`

### Fail-Fast Behavior

`CheckSchedule` uses fail-fast behavior:
- If any subsystem fails during constraint evaluation, `CheckSchedule` returns `false` immediately
- If any constraint fails, `CheckSchedule` returns `false` immediately
- If any subsystem fails during `checkSubs`, `CheckSchedule` returns `false` immediately
- **Important:** If asset1 fails, asset2 is not evaluated, so its state should not be checked in the test

## State Access

### GetInitialStateValue()

Retrieves the initial state value for a given asset and state variable name from `program.InitialSysState`.

### GetFinalStateValue()

Retrieves the final state value for a given asset and state variable name from the schedule's last state:
1. Attempts to find the state key in the last event's state
2. If not found, falls back to the initial state value

This ensures that state values are correctly retrieved even if they haven't been updated in the current event.

## Test Coverage

### CheckSchedule_FirstIteration_UniqueScheduleBranches

This test validates:
- ✅ `CheckSchedule` returns correct boolean results for all 9 unique schedule branches
- ✅ Schedules with TRANSMIT in either asset fail (as expected)
- ✅ All other schedule combinations pass (as expected)
- ✅ State is correctly updated for asset1 in passing schedules
- ✅ State is correctly updated for asset2 in passing schedules
- ✅ Fail-fast behavior: state verification only occurs for passing schedules
- ✅ State changes match expected values:
  - IMAGING: power -10, images +1
  - RECHARGE: power +25

### CheckSchedule_WithConstraintPowerMax75_RechargeFailsConstraint

This test validates constraint evaluation in `CheckSchedule`:
- ✅ Uses a `FAIL_IF_HIGHER` constraint on asset1 Power (value: 75)
- ✅ Schedules with RECHARGE on asset1 fail (power goes from 75 to 100, which is > 75)
- ✅ Schedules with IMAGING on asset1 pass (power goes from 75 to 65, which is <= 75)
- ✅ Schedules with TRANSMIT on asset1 fail (Antenna fails due to no images, not constraint)
- ✅ Verifies that constraints correctly cause schedule failures even when tasks themselves pass

**Input File:** `TwoAsset_Imaging_Model_ConstraintPowerMax75.json`
- **Constraint:** `FAIL_IF_HIGHER` on `asset1.Power` with value `75`
- **Behavior:** Constraint fails if power > 75 (power must be <= 75 to pass)

## Limitations

- **First Iteration Only:** Tests only the first scheduler iteration's potential schedules
- **State Verification:** Only verifies state for schedules that pass `CheckSchedule` (respects fail-fast behavior)
- **Single Event:** Tests only schedules with a single event (first iteration)

## Integration with Other Tests

This test builds on the lower-level unit tests:
- **CheckDependentSubsystemsUnitTest:** Validates dependency evaluation and `CanPerform` logic
- **CheckSubUnitTest:** Validates `checkSub` boolean results for individual subsystems
- **CheckConstraintsUnitTest:** Validates `Constraint.Accepts()` behavior
- **CheckScheduleUnitTest:** Validates the full `CheckSchedule` flow, including constraints, dependencies, and state updates

The test hierarchy ensures that each level of the scheduling system is properly validated, from individual subsystem evaluation to full schedule validation.

