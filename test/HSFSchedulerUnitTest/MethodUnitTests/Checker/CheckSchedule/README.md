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

## Test Scenario

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

## Input Files

Located in `../Inputs/`:
- `SimInput_TwoAssetImaging_ToyExample.json` - Simulation parameters
- `TwoAsset_Imaging_Tasks.json` - Task definitions (RECHARGE, IMAGING, TRANSMIT)
- `TwoAsset_Imaging_Model.json` - System model with Power, Camera, and Antenna subsystems for both assets

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
  - asset1 → IMAGING, asset2 → IMAGING
  - asset1 → IMAGING, asset2 → TRANSMIT
  - asset1 → IMAGING, asset2 → RECHARGE
  - asset1 → TRANSMIT, asset2 → IMAGING
  - asset1 → TRANSMIT, asset2 → TRANSMIT
  - asset1 → TRANSMIT, asset2 → RECHARGE
  - asset1 → RECHARGE, asset2 → IMAGING
  - asset1 → RECHARGE, asset2 → TRANSMIT
  - asset1 → RECHARGE, asset2 → RECHARGE

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

## Expected Boolean Results

### ShouldSchedulePass()

Determines if a schedule should pass based on task assignments:
- **TRANSMIT in EITHER asset:** Schedule should fail (Antenna fails because no images are stored initially)
- **All other combinations:** Schedule should pass

**Examples:**
- asset1 → IMAGING, asset2 → IMAGING: **PASS** (no TRANSMIT)
- asset1 → IMAGING, asset2 → TRANSMIT: **FAIL** (TRANSMIT in asset2)
- asset1 → TRANSMIT, asset2 → RECHARGE: **FAIL** (TRANSMIT in asset1)
- asset1 → RECHARGE, asset2 → RECHARGE: **PASS** (no TRANSMIT)

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
   - Constraint is checked (Power must be >= 10)

### Remaining Subsystems

After constraint evaluation, `checkSubs` is called for all remaining subsystems:
- For `asset2`: Camera, Antenna, and Power are evaluated (no constraints)
- Each subsystem's `CheckDependentSubsystems` is called, which:
  - Evaluates dependencies first (if not already evaluated)
  - Then calls the subsystem's `CanPerform`

### Fail-Fast Behavior

`CheckSchedule` uses fail-fast behavior:
- If any subsystem fails during constraint evaluation, `CheckSchedule` returns `false` immediately
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

## Limitations

- **First Iteration Only:** Tests only the first scheduler iteration's potential schedules
- **State Verification:** Only verifies state for schedules that pass `CheckSchedule` (respects fail-fast behavior)
- **Single Event:** Tests only schedules with a single event (first iteration)

## Integration with Other Tests

This test builds on the lower-level unit tests:
- **CheckDependentSubsystemsUnitTest:** Validates dependency evaluation and `CanPerform` logic
- **CheckSubUnitTest:** Validates `checkSub` boolean results for individual subsystems
- **CheckScheduleUnitTest:** Validates the full `CheckSchedule` flow, including constraints, dependencies, and state updates

The test hierarchy ensures that each level of the scheduling system is properly validated, from individual subsystem evaluation to full schedule validation.

