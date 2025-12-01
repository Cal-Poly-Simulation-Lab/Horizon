# CheckSubUnitTest

## Overview

Tests the `Checker.checkSub()` method, which is responsible for evaluating whether a single subsystem can perform its most recent task in a proposed schedule. This test validates that `checkSub` correctly returns boolean results (true/false) for all subsystems across all unique schedule branches generated in the first scheduler iteration.

## Test Objective

**Primary Goal:** Verify that `checkSub` returns the correct boolean result for each subsystem when called on each unique schedule branch, ensuring that subsystem evaluation logic and dependency handling work correctly.

**Key Validations:**
- `checkSub` returns `true` when subsystems should pass
- `checkSub` returns `false` when subsystems should fail
- Subsystem evaluation order respects dependencies (relevant subsystem called first, dependencies evaluated before dependents)
- Each schedule branch is tested in isolation (using separate schedule objects)

## Test Scenario

Uses the **TwoAsset_Imaging** toy example scenario:
- **Assets:** `asset1` and `asset2` (both have Power, Camera, and Antenna subsystems)
- **Tasks:** IMAGING, TRANSMIT, RECHARGE
- **Dependencies:** 
  - Power depends on Camera and Antenna
  - Antenna depends on Camera
  - Camera has no dependencies

## Input Files

Located in `../Inputs/`:
- `SimInput_TwoAssetImaging_ToyExample.json` - Simulation parameters
- `TwoAsset_Imaging_Tasks.json` - Task definitions (RECHARGE, IMAGING, TRANSMIT)
- `TwoAsset_Imaging_Model.json` - System model with Power, Camera, and Antenna subsystems

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

**Note:** This helper method is reusable for CheckAll unit tests.

## Test Flow

### CheckSub_FirstIteration_UniqueScheduleBranches()

**Step 1: Obtain and Verify Unique Schedule Branches**
- Calls `GetUniqueScheduleBranches()` to get all unique schedule branches grouped by hash
- Verifies there are exactly 9 unique schedule branches

**Step 2: For Each Unique Schedule Branch**
- Identifies task assignments for each asset (asset1 and asset2)
- For each asset:
  - **Orders Subsystems:** Calls `GetOrderedSubsystems()` to ensure correct evaluation order
    - Relevant subsystem (matching task type) is called first
    - Dependencies are respected (dependencies before dependents)
  - **Calls checkSub:** For each subsystem in order:
    - Uses a different schedule object from the hash group for each subsystem call (ensures state isolation)
    - Calls `checkSub` via reflection (it's a private static method)
    - Verifies the boolean result matches expected
  - **Resets IsEvaluated:** Resets `IsEvaluated` flag for all subsystems before next asset call
- **Resets IsEvaluated:** Resets `IsEvaluated` flag for all subsystems before next unique schedule branch

## Subsystem Ordering Logic

### GetOrderedSubsystems()

Ensures subsystems are called in the correct order, with the relevant subsystem first and dependencies respected:

**IMAGING Task:**
- Order: Camera → Power → Antenna
- Camera is relevant (no dependencies)
- Power depends on Camera, so Camera must be called first

**RECHARGE Task:**
- Order: Power → Camera → Antenna
- Power is relevant (no dependencies for RECHARGE)

**TRANSMIT Task:**
- Order: Camera → Antenna → Power
- Antenna is relevant, but Antenna depends on Camera, so Camera must come first
- Power depends on both Camera and Antenna, so it comes last

## Expected Results

### GetExpectedBooleanResult()

Determines the expected boolean result for each subsystem based on task type:

**IMAGING Task:**
- **Camera:** `true` (can perform imaging if buffer not full)
- **Antenna:** `true` (no-op for IMAGING)
- **Power:** `true` (can perform if power >= 10)

**RECHARGE Task:**
- **Camera:** `true` (no-op for RECHARGE)
- **Antenna:** `true` (no-op for RECHARGE)
- **Power:** `true` (can recharge if power < max)

**TRANSMIT Task:**
- **Camera:** `true` (no-op for TRANSMIT)
- **Antenna:** `false` (fails if no images stored initially)
- **Power:** `true` (can perform if power >= 20, but note: Power depends on Antenna, so if Antenna fails, Power should also fail - this is handled by dependency evaluation)

**Note:** The actual result for Power on TRANSMIT may be `false` if Antenna fails, because Power depends on Antenna. The test verifies the actual behavior matches expectations based on dependency evaluation.

## State Isolation

Each subsystem call uses a **different schedule object** from the hash group to ensure state isolation:
- Prevents state mutations from one subsystem call affecting another
- Each schedule object in the hash group has the same structure (same hash) but is a separate instance
- Uses modulo indexing: `schedulesInGroup[scheduleIndex % schedulesInGroup.Count]`

## IsEvaluated Reset Strategy

The `IsEvaluated` flag is reset at two critical points:

1. **Between Asset Calls:** Before testing the next asset within the same schedule branch
   - Ensures each asset's subsystem evaluation starts with a clean state
   - Prevents `IsEvaluated` from one asset affecting the next

2. **Between Schedule Branches:** Before testing the next unique schedule branch
   - Ensures each schedule branch test starts with no subsystems evaluated
   - Prevents state from one branch affecting another

**Note:** This reset logic is temporary and will be removed when subsystems become stateless (the `IsEvaluated` flag will be removed).

## Helper Methods

### CallCheckSub()

Uses reflection to invoke the private static `Checker.checkSub()` method:
```csharp
private bool CallCheckSub(Subsystem subsystem, SystemSchedule schedule, Domain environment)
```

### ResetSubsystemEvaluationState()

Resets the `IsEvaluated` flag for all subsystems in the system. This is called:
- Before each asset call within a schedule branch
- Before each unique schedule branch test

### GetScheduleHash()

Retrieves the schedule hash from a `SystemSchedule` object for grouping and identification.

## Test Coverage

This test validates:
- ✅ `checkSub` returns correct boolean results for all subsystems
- ✅ Subsystem evaluation order (relevant subsystem first, dependencies respected)
- ✅ State isolation (each subsystem call uses a separate schedule object)
- ✅ All 9 unique schedule branches are tested
- ✅ Both assets are tested for each schedule branch

## Limitations

- **No State Verification:** This test only verifies boolean results, not state mutations. State verification is done at the CheckAll level.
- **First Iteration Only:** Tests only the first scheduler iteration's potential schedules.
- **IsEvaluated Flag:** Uses temporary `IsEvaluated` reset logic that will be removed when subsystems become stateless.

## Reusability

The `GetUniqueScheduleBranches()` helper method is designed to be reusable for CheckAll unit tests, providing a consistent way to obtain and group unique schedule branches for testing.

