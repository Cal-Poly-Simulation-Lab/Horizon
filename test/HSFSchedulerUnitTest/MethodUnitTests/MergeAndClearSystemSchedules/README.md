# MergeAndClearSystemSchedules Unit Tests

## Overview

Unit tests for `Scheduler.MergeAndClearSystemSchedules()`, which merges new schedules from the current iteration into existing schedules from previous iterations, and clears the input list.

## Method Under Test

**`Scheduler.MergeAndClearSystemSchedules(List<SystemSchedule> systemSchedules, List<SystemSchedule> systemCanPerformList)`**

### Behavior
- Inserts all schedules from `systemCanPerformList` at the **front** of `systemSchedules` (index 0)
- Clears `systemCanPerformList` (sets count to 0)
- Returns the merged `systemSchedules` list (same reference as input)

### Purpose
This method is called after `EvaluateAndSortCanPerformSchedules()` to merge newly evaluated schedules with existing schedules from previous iterations. New schedules are prepended so they appear first (since they're already sorted best-to-worst).

## Test Cases

### 1. `MergeAndClearSystemSchedules_MergesNewSchedulesAtFront_ClearsInputList`
**Objective:** Verify normal merge operation with both existing and new schedules.

**Setup:**
- Creates 3 existing schedules (simulating from previous iterations)
- Creates 3 new schedules (simulating from current iteration)

**Verifies:**
- Total count = existing + new (6 schedules)
- New schedules are at indices 0, 1, 2 (front)
- Existing schedules are at indices 3, 4, 5 (after new)
- Input list (`systemCanPerformList`) is cleared (count = 0)
- Returned list is same reference as `systemSchedules`

### 2. `MergeAndClearSystemSchedules_EmptyNewSchedules_KeepsExistingSchedules`
**Objective:** Verify behavior when no new schedules are provided.

**Setup:**
- Creates 2 existing schedules
- Creates empty new schedules list

**Verifies:**
- Count remains unchanged (2 schedules)
- Order of existing schedules is preserved
- Input list is cleared (even though it was empty)

### 3. `MergeAndClearSystemSchedules_EmptyExistingSchedules_AddsNewSchedules`
**Objective:** Verify behavior when no existing schedules are present.

**Setup:**
- Creates empty existing schedules list
- Creates 2 new schedules

**Verifies:**
- All new schedules are added (count = 2)
- New schedules are in correct order
- Input list is cleared

### 4. `MergeAndClearSystemSchedules_AllIterations_MergesCorrectly`
**Objective:** Verify merge operation across all scheduler iterations of the toy example.

**Setup:**
- Loads toy example (TwoAsset_Imaging scenario)
- Simulates full scheduler loop across all iterations:
  1. Initialize empty schedule
  2. Generate exhaustive schedule combos
  3. For each iteration:
     - Crop schedules to max
     - TimeDeconfliction (create potential schedules)
     - CheckAllPotentialSchedules (filter to passing)
     - EvaluateAndSortCanPerformSchedules (evaluate and sort)
     - **MergeAndClearSystemSchedules** ← VERIFY HERE

**Verifies at each iteration:**
- Merged count = existing count + new count
- New schedules are at the front of merged list
- Input list (`systemCanPerformList`) is cleared after merge
- Returned list is same reference as input `systemSchedules`
- Merge operation works correctly across all iterations

## Test Input Files

Uses shared input files from `Checker/CheckSchedule/Inputs`:
- `SimInput_TwoAssetImaging_ToyExample.json` - Simulation parameters
- `TwoAsset_Imaging_Tasks.json` - Task definitions
- `TwoAsset_Imaging_Model.json` - Model with assets, subsystems, constraints

## Implementation Notes

- Uses `SystemSchedule(SystemState initialState, string name)` constructor for test schedule creation
- Compares schedules by reference (`Is.SameAs`) rather than by hash, since we're testing list manipulation
- Tests verify both the merge operation and the side effect (clearing input list)
- The all-iterations test stores references to new schedules **before** calling `MergeAndClearSystemSchedules` since the method clears the input list
- All tests use `Assert.Multiple()` to verify multiple aspects in a single test execution

