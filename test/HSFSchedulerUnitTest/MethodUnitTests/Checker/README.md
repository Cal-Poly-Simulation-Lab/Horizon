# CheckAllPotentialSchedulesUnitTest

## Checker Unit Tests Overview

The Checker unit tests validate the schedule validation pipeline from bottom-up, testing each level of the scheduling system's constraint checking and feasibility evaluation. The test hierarchy builds from simple subsystem-level checks to full schedule filtering:

### CheckConstraintsUnitTest
Tests `Constraint.Accepts()` validation logic. Validates that constraints correctly pass or fail based on state variable values, including boundary conditions and multiple constraint types (FAIL_IF_HIGHER, FAIL_IF_LOWER, FAIL_IF_EQUAL). See `CheckSchedule/CheckConstraints/README.md` for details.

### CanPerformUnitTest
Tests `Subsystem.CanPerform()` for all subsystem types (hardcoded, ScriptedCS, Scripted Python). Validates state updates, event timing immutability, and subsystem-specific logic. See `CheckSchedule/CheckSubUnitTest/CheckDependentSubsystems/CanPerformUnitTest/README.md` for details.

### CheckDependentSubsystemsUnitTest
Tests `Subsystem.CheckDependentSubsystems()` method, which ensures dependent subsystems are evaluated before parent subsystems. Validates dependency evaluation order, state mutations, task time boundary validation, and recursive dependency resolution. See `CheckSchedule/CheckSubUnitTest/CheckDependentSubsystems/README.md` for details.

### CheckSubUnitTest
Tests `Checker.checkSub()` method, which evaluates a single subsystem's feasibility for its most recent task. Validates boolean pass/fail results for each unique schedule branch, ensuring correct subsystem routing and evaluation order. See `CheckSchedule/CheckSubUnitTest/README.md` for details.

### CheckScheduleUnitTest
Tests `Checker.CheckSchedule()` method, which evaluates whether an entire proposed schedule can be executed. Validates boolean results for all unique schedule branches, state updates for passing schedules, constraint-driven evaluation, and multi-asset scenarios. See `CheckSchedule/CheckScheduleUnitTest/README.md` for details.

### CheckAllPotentialSchedulesUnitTest
Tests `Scheduler.CheckAllPotentialSchedules()` method, which filters a list of potential schedules by calling `CheckSchedule` on each and returning only those that pass. This is the highest-level test in the Checker hierarchy, validating full schedule filtering, state hash updates, and schedule hash preservation. See detailed section below.

---

## CheckAllPotentialSchedulesUnitTest Overview

Tests the `Scheduler.CheckAllPotentialSchedules()` method, which filters a list of potential schedules by calling `CheckSchedule` on each and returning only those that pass. This test validates that `CheckAllPotentialSchedules` correctly filters schedules, returns only passing schedules, updates state correctly, and updates StateHashHistory while preserving schedule hashes.

## Test Objective

**Primary Goal:** Verify that `CheckAllPotentialSchedules` correctly filters schedules, returns only expected passing schedules, updates state correctly for each asset, and properly updates StateHashHistory while keeping schedule hashes unchanged.

**Key Validations:**
- Only expected passing schedules are returned
- State is correctly updated for each asset in passing schedules
- Schedule hash remains unchanged (not updated by `CheckAllPotentialSchedules`)
- StateHashHistory is updated for all passing schedules
- Constraints correctly cause schedule failures even when tasks pass

## Test Methods

### CheckAllPotentialSchedules_FirstIteration_UniqueScheduleBranches

Tests `CheckAllPotentialSchedules` with the basic TwoAsset_Imaging scenario. Validates that:
- All 9 unique schedule branches are processed
- Only schedules without TRANSMIT are returned (4 passing schedules)
- State is correctly updated for each asset in passing schedules
- Schedule hash remains unchanged
- StateHashHistory is updated after `CheckAllPotentialSchedules`

**Input Files:** Uses `TwoAsset_Imaging_Model.json` (basic scenario with FAIL_IF_LOWER constraint on asset1 Power).

### CheckAllPotentialSchedules_WithConstraintPowerMax75_RechargeFailsConstraint

Tests `CheckAllPotentialSchedules` with a `FAIL_IF_HIGHER` constraint (power > 75 fails). Validates that:
- Schedules with RECHARGE on asset1 fail (power 75→100, constraint violation)
- Schedules with IMAGING on asset1 pass (power 75→65, constraint satisfied)
- Schedules with TRANSMIT fail (Antenna fails, not constraint)
- Only 2 passing schedules are returned
- State and hash verifications match the first test

**Input Files:** Uses `TwoAsset_Imaging_Model_ConstraintPowerMax75.json` (FAIL_IF_HIGHER constraint on asset1 Power, value: 75).

## Detailed Test Flow: CheckAllPotentialSchedules_FirstIteration_UniqueScheduleBranches

### Step 1: Obtain Unique Schedule Branches

Uses helper functions to simulate the first scheduler iteration:
1. **Initialize Empty Schedule:** Creates the baseline empty schedule
2. **Generate Schedule Combos:** Creates exhaustive access combinations (all possible task assignments)
3. **TimeDeconfliction:** Creates potential schedules by combining the empty schedule with each access combination

This generates `_potentialSchedules`, which contains all possible schedule branches from the first iteration. The test then groups these by schedule hash to identify unique branches:
- **Expected:** 9 unique schedule branches (3 tasks × 2 assets = 9 combinations)

### Step 2: Store Initial Hash Values

Before calling `CheckAllPotentialSchedules`, the test stores:
- **Initial Schedule Hashes:** The schedule hash for each unique branch (should remain unchanged)
- **Initial State Hashes:** The state hash for each unique branch (should be updated)

These are used later to verify that schedule hashes remain unchanged and state hashes are updated.

### Step 3: Call CheckAllPotentialSchedules

Calls `Scheduler.CheckAllPotentialSchedules(_system, _potentialSchedules)`, which:
1. Iterates through each potential schedule
2. Calls `Checker.CheckSchedule()` on each
3. Updates StateHashHistory after each check (if hash tracking is enabled)
4. Returns only schedules that passed `CheckSchedule`

### Step 4: Verify Output Contains Only Expected Passing Schedules

For each of the 9 unique schedule branches:
- Determines if the schedule should pass using `ShouldSchedulePass()`:
  - **TRANSMIT in EITHER asset:** Schedule should fail (Antenna fails because no images are stored initially)
  - **All other combinations:** Schedule should pass
- Verifies that the schedule is present in the output if it should pass, and absent if it should fail

**Expected Results:**
- 4 passing schedules (no TRANSMIT)
- 5 failing schedules (with TRANSMIT)

### Step 5: Verify State for Each Asset in Passing Schedules

For each passing schedule, verifies state changes for each asset based on task type:

**IMAGING Task:**
- **Power:** Decreases by 10 (from 75 to 65)
- **Images:** Increases by 1 (from 0 to 1)

**RECHARGE Task:**
- **Power:** Increases by 25 (from 75 to 100)

**Initial State Values:**
- `checker_power`: 75.0 (both assets)
- `num_images_stored`: 0.0 (both assets)

### Step 6: Verify Hash Updates

For each passing schedule, verifies:

**Schedule Hash Unchanged:**
- The schedule hash before `CheckAllPotentialSchedules` matches the schedule hash after
- Schedule hashes are not updated by `CheckAllPotentialSchedules` (they are set during TimeDeconfliction and updated later during evaluation)

**StateHashHistory Updated:**
- `StateHashHistory` contains an entry for the current scheduler step and schedule hash
- The state hash value has changed from the initial value (if it existed)
- The state hash is stored in `StateHashHistory[(SchedulerStep, ScheduleHash)]`

## CheckAllPotentialSchedules Behavior

### Schedule Filtering

`CheckAllPotentialSchedules` iterates through all potential schedules and:
1. Gets the schedule hash (set during TimeDeconfliction)
2. Calls `Checker.CheckSchedule()` on each schedule
3. Updates StateHashHistory after each check (blockchain-style incremental hashing)
4. Adds passing schedules to the return list

### State Hash Updates

After `CheckSchedule` completes for each schedule:
- `StateHistory.UpdateStateHashAfterCheck()` is called
- Computes a new state hash based on:
  - Previous state hash (if exists)
  - Current time
  - CheckSchedule result (true/false)
  - All state variable values at current time
- Stores the hash in `StateHashHistory[(SchedulerStep, ScheduleHash)]`

### Schedule Hash Preservation

Schedule hashes are **not** updated by `CheckAllPotentialSchedules`. They are:
- Set during `TimeDeconfliction` (when events are added)
- Updated later during `EvaluateAndSortCanPerformSchedules` (when schedule values are computed)

This test verifies that schedule hashes remain unchanged through the `CheckAllPotentialSchedules` step.

## Test Scenarios

### Test 1: Basic Validation

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

**Expected Passing Schedules (4):**
- asset1 → IMAGING, asset2 → IMAGING
- asset1 → IMAGING, asset2 → RECHARGE
- asset1 → RECHARGE, asset2 → IMAGING
- asset1 → RECHARGE, asset2 → RECHARGE

**Expected Failing Schedules (5):**
- Any schedule with TRANSMIT in either asset

### Test 2: Constraint Failure Validation

Uses a modified model with a `FAIL_IF_HIGHER` constraint:
- **Constraint:** Power must be <= 75 (fails if power > 75)
- **Input File:** `TwoAsset_Imaging_Model_ConstraintPowerMax75.json`

**Expected Passing Schedules (2):**
- asset1 → IMAGING, asset2 → IMAGING (power 75→65, constraint passes)
- asset1 → IMAGING, asset2 → RECHARGE (power 75→65, constraint passes)

**Expected Failing Schedules (7):**
- Any schedule with RECHARGE on asset1 (power 75→100, constraint fails: 100 > 75)
- Any schedule with TRANSMIT (Antenna fails, not constraint)

## Input Files

Located in `CheckSchedule/Inputs/`:
- `SimInput_TwoAssetImaging_ToyExample.json` - Simulation parameters
- `TwoAsset_Imaging_Tasks.json` - Task definitions (RECHARGE, IMAGING, TRANSMIT)
- `TwoAsset_Imaging_Model.json` - System model for basic validation test
- `TwoAsset_Imaging_Model_ConstraintPowerMax75.json` - System model with FAIL_IF_HIGHER constraint

## Helper Methods

### SetupFirstIteration()

Simulates the first scheduler iteration to generate potential schedules:
1. **Initialize Empty Schedule:** Creates the baseline empty schedule
2. **Generate Schedule Combos:** Creates exhaustive access combinations (all possible task assignments)
3. **TimeDeconfliction:** Creates potential schedules by combining the empty schedule with each access combination

### GetUniqueScheduleBranches()

Groups `_potentialSchedules` by schedule hash to identify unique schedule branches. Each unique hash represents a distinct task assignment combination.

### ShouldSchedulePass()

Determines if a schedule should pass based on task assignments:
- **TRANSMIT in EITHER asset:** Schedule should fail (Antenna fails because no images are stored initially)
- **All other combinations:** Schedule should pass

### VerifyStateForAsset()

Verifies state changes for each asset in passing schedules:
- **IMAGING:** power -10, images +1
- **RECHARGE:** power +25

### VerifyStateHashUpdated()

Verifies that `StateHashHistory` contains an entry for the current scheduler step and schedule hash, and that the state hash value is not null or empty.

## Test Coverage

### CheckAllPotentialSchedules_FirstIteration_UniqueScheduleBranches

This test validates:
- ✅ All 9 unique schedule branches are processed
- ✅ Only expected passing schedules are returned (4 passing, 5 failing)
- ✅ State is correctly updated for asset1 in passing schedules
- ✅ State is correctly updated for asset2 in passing schedules
- ✅ Schedule hash remains unchanged after `CheckAllPotentialSchedules`
- ✅ StateHashHistory is updated for all passing schedules
- ✅ State hash value changes from initial to final

### CheckAllPotentialSchedules_WithConstraintPowerMax75_RechargeFailsConstraint

This test validates:
- ✅ Constraint correctly causes schedule failures (RECHARGE on asset1 fails)
- ✅ Only expected passing schedules are returned (2 passing, 7 failing)
- ✅ State is correctly updated for each asset in passing schedules
- ✅ Schedule hash remains unchanged
- ✅ StateHashHistory is updated correctly

## Limitations

- **First Iteration Only:** Tests only the first scheduler iteration's potential schedules
- **State Verification:** Only verifies state for schedules that pass `CheckAllPotentialSchedules`
- **Single Event:** Tests only schedules with a single event (first iteration)

## Integration with Other Tests

This test builds on the lower-level unit tests:
- **CheckDependentSubsystemsUnitTest:** Validates dependency evaluation and `CanPerform` logic
- **CheckSubUnitTest:** Validates `checkSub` boolean results for individual subsystems
- **CheckScheduleUnitTest:** Validates `CheckSchedule` boolean results and state updates
- **CheckAllPotentialSchedulesUnitTest:** Validates the full filtering flow, including state hash updates

The test hierarchy ensures that each level of the scheduling system is properly validated, from individual subsystem evaluation to full schedule filtering and hash tracking.

