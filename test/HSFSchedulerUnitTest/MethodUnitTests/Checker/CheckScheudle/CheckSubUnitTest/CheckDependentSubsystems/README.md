# CheckDependentSubsystems Unit Tests

## Overview

Simple unit tests for `Subsystem.CheckDependentSubsystems()` method using the toy example (TwoAsset_Imaging scenario). These tests validate that dependency evaluation order is correct, that `CheckDependentSubsystems` returns `true` when all `CanPerform` checks should pass, and returns `false` when any `CanPerform` check should fail.

## Design Philosophy

### Dependency Evaluation Order
- **Dependencies are evaluated recursively before the parent subsystem**
- Subsystems with no dependencies are evaluated first
- Parent subsystems are only evaluated after all their dependencies pass
- Failures in dependencies propagate up the chain, causing the parent to fail

### State Mutation Verification
- **Tests verify dependency order via state mutations, not internal flags**
- Camera writes to `num_images_stored` before Antenna reads it
- Power reads final state after both Camera and Antenna have updated state
- This approach is future-proof and will work when `IsEvaluated` flag is removed

### Call Tracking and Verification
- **Thread-safe call tracking** via `SubsystemCallTracker` (shared static class)
- Tracks: call order, asset name, subsystem name, task type, and mutation status (YES/NO)
- **Call order verification:** Verifies dependencies are called before parents (e.g., Camera before Antenna before Power)
- **Mutation status verification:** Verifies reported mutation status matches actual state changes:
  - If tracker reports "YES" → verifies state actually changed
  - If tracker reports "NO" → verifies state did not change
- **Asset name verification:** Ensures correct asset is being evaluated
- All tracking is thread-safe using `ConcurrentBag` and `Interlocked` operations for parallel execution support

## Test Coverage

### 1. Dependency Evaluation Order (via State Mutations)
**Tests:** `CheckDependentSubsystems_CameraRunsBeforePower_VerifiedByStateMutation`, `CheckDependentSubsystems_CameraRunsBeforeAntenna_VerifiedByStateMutation`, `CheckDependentSubsystems_FullDependencyChain_VerifiedByStateMutations`

- ✅ Verifies Camera runs before Power by checking Camera's state mutation (images 0 → 1) occurs before Power consumes power
- ✅ Verifies Camera runs before Antenna by checking Camera's state mutation occurs before Antenna reads the state
- ✅ Verifies full dependency chain (Camera → Antenna → Power) by checking all state mutations occur in correct order
- ✅ Uses state mutations to verify order (future-proof, doesn't rely on `IsEvaluated` flag)
- ✅ **Call order verification:** Verifies call order via `SubsystemCallTracker` (Camera call order < Antenna call order)
- ✅ **Mutation status verification:** Verifies reported mutation status (YES/NO) matches actual state changes

**Key Verification:** Dependency evaluation order is correct - subsystems with no dependencies run first, then their dependents, ensuring state is properly updated before dependent subsystems read it. Call tracking provides additional verification of execution order.

### 2. CheckDependentSubsystems Returns True When All Should Pass
**Tests:** `CheckDependentSubsystems_IMAGING_AllPass_ReturnsTrue`, `CheckDependentSubsystems_TRANSMIT_AllPass_ReturnsTrue`

- ✅ IMAGING task: Camera passes (0 < 10), Antenna passes (no-op), Power passes (75 >= 10)
- ✅ TRANSMIT task: Camera passes (no-op), Antenna passes (images > 0), Power passes (75 >= 20)
- ✅ Verifies that when all subsystems in the dependency chain should pass, `CheckDependentSubsystems` returns `true`

**Key Verification:** The method correctly aggregates results from the dependency chain and returns `true` when all subsystems pass.

### 3. CheckDependentSubsystems Returns False When Any Should Fail
**Tests:** `CheckDependentSubsystems_CameraFails_ReturnsFalse`, `CheckDependentSubsystems_AntennaFails_ReturnsFalse`, `CheckDependentSubsystems_PowerFails_ReturnsFalse`

- ✅ Camera fails: Buffer full (10 >= 10) causes Power to return `false`
- ✅ Antenna fails: No images (0 <= 0) causes Power to return `false`
- ✅ Power fails: Insufficient power (15 < 20) causes Power to return `false`
- ✅ Verifies that failures in any subsystem in the dependency chain propagate up and cause the parent to return `false`

**Key Verification:** The method correctly propagates failures from dependencies up the chain, returning `false` when any subsystem fails.

### 4. Both Dependents Evaluated Even When Neither Mutates
**Test:** `CheckDependentSubsystems_RECHARGE_BothDependentsEvaluated_NeitherMutates`

- ✅ RECHARGE task: Both Camera and Antenna are evaluated but neither mutates state
- ✅ Verifies that both dependents are called even when they don't mutate state (no-op behavior)
- ✅ **Call order verification:** Verifies Camera is called before Antenna (dependency order)
- ✅ **Mutation status verification:** Verifies both report "NO" mutation and state remains unchanged
- ✅ Verifies Power still passes (it mutates via recharge)

**Key Verification:** Dependencies are always evaluated, even when they don't mutate state. This ensures consistent evaluation order regardless of mutation behavior.

### 5. Task Time Mutation Validation
**Tests:** 24 tests covering task time mutations (8 scenarios × 3 subsystems)

#### How Task Time Mutations Work

Subsystems can mutate task start and end times during `CanPerform()` via the `_taskStartTimeMutation` and `_taskEndTimeMutation` parameters. These mutations are applied as offsets to the current task times:

```csharp
double mutatedTaskStart = currentTaskStart + _taskStartTimeMutation;
double mutatedTaskEnd = currentTaskEnd + _taskEndTimeMutation;
proposedEvent.SetTaskStart(new Dictionary<Asset, double> { { Asset, mutatedTaskStart } });
proposedEvent.SetTaskEnd(new Dictionary<Asset, double> { { Asset, mutatedTaskEnd } });
```

After `CanPerform()` mutates task times, `CheckTaskStartAndEnd()` validates that the mutated times remain within event boundaries:
- `taskStart >= eventStart` and `taskStart <= eventEnd`
- `taskEnd >= eventStart` and `taskEnd <= eventEnd`
- `taskStart <= taskEnd` (task start must not be after task end)

If any of these conditions are violated, `CheckDependentSubsystems` returns `false`, even if `CanPerform()` would have returned `true`.

#### Test Coverage

**Out of Bounds Mutations (Should Fail):**
- **Start Out of Bounds:**
  - `*_StartOutOfBounds_BeforeEventStart`: Task start < event start (e.g., mutation = -1.0, taskStart = 0.0 → -1.0 < 0.0)
  - `*_StartOutOfBounds_AfterEventEnd`: Task start > event end (e.g., mutation = 11.0, taskStart = 0.0 → 11.0 > 10.0)
- **End Out of Bounds:**
  - `*_EndOutOfBounds_BeforeEventStart`: Task end < event start (e.g., mutation = -11.0, taskEnd = 10.0 → -1.0 < 0.0)
  - `*_EndOutOfBounds_AfterEventEnd`: Task end > event end (e.g., mutation = 1.0, taskEnd = 10.0 → 11.0 > 10.0)
- **Start After End:**
  - `*_StartAfterEnd`: Task start > task end (e.g., start mutation = 6.0, end mutation = -5.0, taskStart = 0.0 → 6.0, taskEnd = 10.0 → 5.0, 6.0 > 5.0)

**In Bounds Mutations (Should Pass):**
- **Start In Bounds:**
  - `*_StartInBounds_Changed`: Task start changed but within [eventStart, eventEnd] (e.g., mutation = 2.0, taskStart = 0.0 → 2.0, within [0.0, 10.0])
- **End In Bounds:**
  - `*_EndInBounds_Changed`: Task end changed but within [eventStart, eventEnd] (e.g., mutation = -2.0, taskEnd = 10.0 → 8.0, within [0.0, 10.0])
- **Both In Bounds:**
  - `*_BothInBounds`: Both start and end changed but within bounds and start <= end (e.g., start mutation = 1.0, end mutation = -1.0, taskStart = 0.0 → 1.0, taskEnd = 10.0 → 9.0, both within [0.0, 10.0] and 1.0 <= 9.0)

**Test Organization:**
- Tests are grouped by subsystem (Camera, Antenna, Power) and mutation type (OutOfBounds, InBounds)
- Each test uses `[TestCase]` attributes to parameterize mutation scenarios
- Tests use dedicated input files in `TaskMutationInput/` directory with specific mutation parameters
- Default event/task times: eventStart = 0.0, eventEnd = 10.0, taskStart = 0.0 (or 2.0 for Antenna), taskEnd = 10.0

**Key Verification:** `CheckDependentSubsystems` correctly fails when task time mutations cause out-of-bounds conditions, even if `CanPerform()` would pass. This ensures subsystems cannot violate event time boundaries through time mutations.

#### Bug Fix: Exception Handling in CheckDependentSubsystems

**Bug Discovered:** When a subsystem mutated task times to out-of-bounds values (e.g., taskStart = -1.0), `CanPerform()` would attempt to add a state value at `updateTime = taskStart + 0.1 = -0.9`, which is before the initial state time (0.0). This caused `SystemState.AddValue()` to throw an `ArgumentOutOfRangeException`, preventing `CheckTaskStartAndEnd()` from being called. As a result, out-of-bounds time mutations were not caught.

**Fix Applied:** Wrapped `CanPerform()` calls in try-catch blocks for both code paths (subsystems with and without dependents). This ensures `CheckTaskStartAndEnd()` is always called, even if `CanPerform()` throws an exception. The fix ensures that:
1. Time mutations are applied (they occur early in `CanPerform()`)
2. `CheckTaskStartAndEnd()` is called regardless of `CanPerform()` exceptions
3. Out-of-bounds time mutations are correctly caught and cause `CheckDependentSubsystems` to return `false`

**Impact:** This bug fix ensures that time boundary validation works correctly even when `CanPerform()` throws exceptions due to invalid state update times. Without this fix, subsystems could mutate task times to out-of-bounds values without being caught, potentially causing scheduling errors.

#### Code Refactoring: Eliminating Duplication in CheckDependentSubsystems

**Refactoring Applied:** The `CheckDependentSubsystems` method was refactored to eliminate code duplication while maintaining the exact same execution logic.

**Before (Old Code):**
```csharp
public bool CheckDependentSubsystems(Event proposedEvent, Domain environment)
{
    if (DependentSubsystems.Count == 0)
    {
        IsEvaluated = true;
        Task = proposedEvent.GetAssetTask(Asset);
        NewState = proposedEvent.State;
        bool result = false;
        try
        {   
            result = this.CanPerform(proposedEvent, environment);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CanPerform threw exception for {Name}: {ex.Message}");
        }
        if (CheckTaskStartAndEnd(proposedEvent, Asset)){
            return result;
        }
        return false;
    }
    else
    {
        foreach (var sub in DependentSubsystems)
        {
            if (!sub.IsEvaluated)
            {
                if (!sub.CheckDependentSubsystems(proposedEvent, environment))
                {
                    return false;
                }
            }
        }

        IsEvaluated = true;
        Task = proposedEvent.GetAssetTask(Asset);
        NewState = proposedEvent.State;
        bool resultParent = false;
        try
        {
            resultParent = CanPerform(proposedEvent, environment);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CanPerform threw exception for {Name}: {ex.Message}");
        }
        if (CheckTaskStartAndEnd(proposedEvent, Asset)){
            return resultParent;
        }
        return false;
    }
}
```

**After (Refactored Code):**
```csharp
public bool CheckDependentSubsystems(Event proposedEvent, Domain environment)
{
    // First, recursively evaluate all dependent subsystems (depth-first)
    // If any dependency fails, propagate failure up immediately
    foreach (var sub in DependentSubsystems)
    {
        if (!sub.IsEvaluated)
        {
            if (!sub.CheckDependentSubsystems(proposedEvent, environment))
            {
                return false;
            }
        }
    }

    // Now evaluate this subsystem (common logic for both leaf and parent nodes)
    IsEvaluated = true;
    Task = proposedEvent.GetAssetTask(Asset);
    NewState = proposedEvent.State;
    
    bool result = false;
    try
    {   
        result = this.CanPerform(proposedEvent, environment);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"CanPerform threw exception for {Name}: {ex.Message}");
    }
    
    if (CheckTaskStartAndEnd(proposedEvent, Asset))
    {
        return result;
    }
    return false;
}
```

**Key Changes:**
- Removed the `if (DependentSubsystems.Count == 0)` / `else` branching structure
- The `foreach` loop for dependent subsystems now runs for all cases (empty list = no-op)
- Common logic (IsEvaluated, Task, NewState, CanPerform, task time check) appears only once

**Why It's Better:**
1. **Eliminates Duplication:** Common logic is written once instead of twice, reducing maintenance burden
2. **Same Execution Logic:** The refactored code maintains identical execution order and behavior:
   - Dependencies are still evaluated first (depth-first recursion)
   - Parent subsystem evaluation follows dependency evaluation
   - All error handling and validation logic remains unchanged
3. **Improved Readability:** The code flow is clearer - dependencies first, then this subsystem
4. **Easier Maintenance:** Future changes to the common logic only need to be made in one place

**Test Verification:** All 33 tests passed both before and after the refactoring, confirming that the execution logic remains identical. The refactoring was purely structural - no functional changes were made.

## Test Infrastructure

### SubsystemCallTracker
**Location:** `test/HSFSchedulerUnitTest/Subsystems/SubsystemCallTracker.cs`

Thread-safe static tracking class used by test subsystems to record `CanPerform` calls:
- **Tracks:** Call order (thread-safe counter), asset name, subsystem name, task type, mutation status (YES/NO)
- **Thread-safe:** Uses `ConcurrentBag` and `Interlocked.Increment` for parallel execution support
- **Access:** `SubsystemCallTracker.Clear()`, `SubsystemCallTracker.GetTracking()`, `SubsystemCallTracker.GetTrackingForSubsystem()`, `SubsystemCallTracker.GetTrackingForAsset()`
- **CallRecord structure:** Contains all tracking information for a single `CanPerform` call

**Usage in tests:**
- Tests clear tracking before execution: `SubsystemCallTracker.Clear()`
- Tests retrieve tracking data after execution: `SubsystemCallTracker.GetTracking()`
- Tests verify call order: `cameraCall.CallOrder < antennaCall.CallOrder`
- Tests verify mutation status matches actual state changes

### Helper Methods
- **`CreateEvent(task, state, eventStart, eventEnd, taskStart, taskEnd)`**
  - Creates an `Event` with specified task, state, and timing parameters
  - Sets event and task start/end times for the asset
  - Used to create test events with controlled timing

- **`CreateEventWithAsset(task, state, asset, eventStart, eventEnd, taskStart, taskEnd)`**
  - Creates an `Event` with a specific asset (for mutation tests)
  - Similar to `CreateEvent` but uses the provided asset instead of `_asset1` from setup
  - Used in mutation tests to ensure correct asset is used with mutation-loaded subsystems

- **`GetTask(type)`**
  - Retrieves a task from `program.SystemTasks` by type (case-insensitive)
  - Returns the first matching task

- **`LoadMutationInput(mutationFileName)`**
  - Loads a mutation input file from `TaskMutationInput/` directory
  - Creates a new `Horizon.Program` instance and loads the mutation-specific model file
  - Returns `(powerSub, cameraSub, antennaSub, universe, asset1)` tuple
  - Used by mutation tests to load subsystems with specific time mutation parameters
  - Ensures each mutation test uses the correct subsystem configuration

- **`VerifyTimeMutationParametersAreZero()`**
  - Verifies that all test subsystems have time mutation parameters (`_taskStartTimeMutation`, `_taskEndTimeMutation`) set to 0
  - Uses reflection to access methods on dynamically compiled subsystems
  - Ensures existing tests are not affected by time mutations (subsystems won't change task times)

### Test Scenario
Uses the **TwoAsset_Imaging** scenario with the following dependency structure:
- **Power** depends on **Camera** and **Antenna**
- **Antenna** depends on **Camera**
- **Camera** has no dependencies

**Dependency Chain:** Camera → Antenna → Power

## Input Files

**Note:** This test suite uses **shared input files** from the `CheckScheudle/Inputs/` directory:

- `../../Inputs/SimInput_CanPerform.json` - Simulation parameters
- `../../Inputs/TwoAsset_Imaging_Tasks.json` - Task definitions (RECHARGE, IMAGING, TRANSMIT)
- `../../Inputs/TwoAsset_Imaging_Model.json` - System model with Power, Camera, and Antenna subsystems (time mutations = 0.0)

**Task Mutation Input Files:** Located in `../../Inputs/TaskMutationInput/` directory:
- 24 mutation input files (8 scenarios × 3 subsystems)
- Each file is identical to `TwoAsset_Imaging_Model.json` except for mutation parameters
- File naming: `{Subsystem}_{MutationType}_{Description}.json`
- Examples:
  - `Camera_StartOutOfBounds_BeforeEventStart.json` - Camera mutates task start to -1.0
  - `Antenna_BothInBounds.json` - Antenna mutates both start and end within bounds
  - `Power_StartAfterEnd.json` - Power mutates task start after task end

The model file defines dependencies:
- `asset1.Power` depends on `asset1.Camera` and `asset1.Antenna`
- `asset1.Antenna` depends on `asset1.Camera`
- `asset1.Camera` has no dependencies

## Test Cases

### Dependency Evaluation Order Tests

1. **CheckDependentSubsystems_CameraRunsBeforePower_VerifiedByStateMutation**
   - **Input:** IMAGING task, initial state (0 images, 75 power)
   - **Validates:** Camera increments images (0 → 1) before Power consumes power (75 → 65)
   - **Verifies:** Camera runs before Power in dependency chain

2. **CheckDependentSubsystems_CameraRunsBeforeAntenna_VerifiedByStateMutation**
   - **Input:** IMAGING task, initial state (0 images)
   - **Validates:** Camera increments images (0 → 1) before Antenna reads state
   - **Verifies:** Camera runs before Antenna in dependency chain

3. **CheckDependentSubsystems_FullDependencyChain_VerifiedByStateMutations**
   - **Input:** IMAGING task, initial state (0 images, 75 power)
   - **Validates:** Camera increments images, Power consumes power after both dependencies run
   - **Verifies:** Full chain (Camera → Antenna → Power) executes in correct order
   - **Tracking:** Verifies call order (Camera before Antenna), mutation status (Camera=YES, Antenna=NO), and state changes match reported status

### Returns True When All Should Pass

4. **CheckDependentSubsystems_IMAGING_AllPass_ReturnsTrue**
   - **Input:** IMAGING task, initial state (0 images, 75 power)
   - **Validates:** Returns `true` when Camera, Antenna, and Power all pass

5. **CheckDependentSubsystems_TRANSMIT_AllPass_ReturnsTrue**
   - **Input:** TRANSMIT task, state with 5 images, 75 power
   - **Validates:** Returns `true` when Camera, Antenna, and Power all pass

### Returns False When Any Should Fail

6. **CheckDependentSubsystems_CameraFails_ReturnsFalse**
   - **Input:** IMAGING task, state with 10 images (buffer full)
   - **Validates:** Returns `false` when Camera fails (buffer full)

7. **CheckDependentSubsystems_AntennaFails_ReturnsFalse**
   - **Input:** TRANSMIT task, state with 0 images
   - **Validates:** Returns `false` when Antenna fails (no images to transmit)

8. **CheckDependentSubsystems_PowerFails_ReturnsFalse**
   - **Input:** TRANSMIT task, state with 5 images, 15 power (insufficient)
   - **Validates:** Returns `false` when Power fails (insufficient power)

9. **CheckDependentSubsystems_RECHARGE_BothDependentsEvaluated_NeitherMutates**
   - **Input:** RECHARGE task, initial state (0 images, 0 transmissions, 75 power)
   - **Validates:** Both Camera and Antenna are called but neither mutates state
   - **Verifies:** Call order (Camera before Antenna), mutation status (both NO), state remains unchanged

### Task Time Mutation Tests

**Test Methods:** `CheckDependentSubsystems_{Subsystem}_TimeMutation_{Type}_Returns{Result}`

**Subsystems:** Camera, Antenna, Power  
**Types:** OutOfBounds (5 scenarios), InBounds (3 scenarios)  
**Total:** 24 tests (8 scenarios × 3 subsystems)

#### Out of Bounds Tests (Should Return False)

10-14. **Camera Time Mutation Out of Bounds** (5 tests)
   - `Camera_StartOutOfBounds_BeforeEventStart`: Mutation = -1.0, taskStart = 0.0 → -1.0 < 0.0 (eventStart)
   - `Camera_StartOutOfBounds_AfterEventEnd`: Mutation = 11.0, taskStart = 0.0 → 11.0 > 10.0 (eventEnd)
   - `Camera_EndOutOfBounds_BeforeEventStart`: Mutation = -11.0, taskEnd = 10.0 → -1.0 < 0.0 (eventStart)
   - `Camera_EndOutOfBounds_AfterEventEnd`: Mutation = 1.0, taskEnd = 10.0 → 11.0 > 10.0 (eventEnd)
   - `Camera_StartAfterEnd`: Start mutation = 6.0, end mutation = -5.0, taskStart = 6.0 > taskEnd = 5.0

15-19. **Antenna Time Mutation Out of Bounds** (5 tests)
   - Similar scenarios to Camera, but with Antenna mutations
   - Note: Antenna tests use `taskStart: 2.0` (not 0.0), so mutations are adjusted accordingly (e.g., -3.0 to get -1.0)

20-24. **Power Time Mutation Out of Bounds** (5 tests)
   - Similar scenarios to Camera, but with Power mutations

#### In Bounds Tests (Should Return True)

25-27. **Camera Time Mutation In Bounds** (3 tests)
   - `Camera_StartInBounds_Changed`: Mutation = 2.0, taskStart = 0.0 → 2.0 (within [0.0, 10.0])
   - `Camera_EndInBounds_Changed`: Mutation = -2.0, taskEnd = 10.0 → 8.0 (within [0.0, 10.0])
   - `Camera_BothInBounds`: Start mutation = 1.0, end mutation = -1.0, both within bounds and start <= end

28-30. **Antenna Time Mutation In Bounds** (3 tests)
   - Similar scenarios to Camera, but with Antenna mutations

31-33. **Power Time Mutation In Bounds** (3 tests)
   - Similar scenarios to Camera, but with Power mutations

## Running Tests

```bash
# All CheckDependentSubsystems tests
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystemsUnitTest"

# Specific test group
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystems_CameraRunsBefore"
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystems_IMAGING"
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystems_TRANSMIT"

# Task mutation tests
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystems_Camera_TimeMutation"
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystems_Antenna_TimeMutation"
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystems_Power_TimeMutation"

# Out of bounds only
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystems.*TimeMutation_OutOfBounds"

# In bounds only
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystems.*TimeMutation_InBounds"
```

## Notes

- **Test execution time:** ~45s for full suite (33 tests: 9 original + 24 mutation tests)
- **No external dependencies:** All tests use shared input files from `CheckScheudle/Inputs/` or `TaskMutationInput/`
- **Deterministic:** Tests use fixed parameters and expected values
- **Isolated:** Each test creates fresh state and events, clears call tracking
- **Future-proof:** Tests verify order via state mutations, not `IsEvaluated` flag (will work when flag is removed)
- **Thread-safe tracking:** `SubsystemCallTracker` uses thread-safe collections for parallel execution support
- **Dual verification:** Tests verify both call order (via tracking) and state mutations (via state values)
- **Time mutation verification:** Original tests verify time mutation parameters are 0 to ensure subsystems don't modify task times (preserves test logic)
- **Comprehensive mutation coverage:** 24 mutation tests cover all combinations of out-of-bounds and in-bounds scenarios for all three subsystems

## Thesis Relevance

These tests establish correctness of the dependency evaluation mechanism and time boundary validation:
1. **Order verification** ensures dependencies are evaluated before parents (critical for state consistency)
   - Verified via call order tracking (`SubsystemCallTracker`) and state mutation sequencing
2. **State mutation verification** validates that state updates occur in the correct order
   - Verified by comparing reported mutation status (YES/NO) with actual state changes
3. **Failure propagation** confirms that dependency failures correctly cause parent failures
4. **Time boundary validation** ensures subsystems cannot violate event time constraints through mutations
   - Comprehensive coverage of all out-of-bounds and in-bounds mutation scenarios
   - Validates that `CheckTaskStartAndEnd` correctly catches boundary violations
5. **Exception handling robustness** ensures time boundary checks occur even when `CanPerform()` throws exceptions
   - Critical bug fix: Prevents out-of-bounds mutations from being missed due to `CanPerform()` exceptions
6. **Future-proof design** uses state mutations instead of internal flags, ensuring tests remain valid when `IsEvaluated` is removed
7. **Thread-safe tracking** enables parallel regression testing at higher levels (hash verification can be added for parallel execution validation)

All tests passing = dependency evaluation mechanism is working correctly, with verified call order, mutation status matching actual behavior, and comprehensive time boundary validation that catches violations even when `CanPerform()` throws exceptions.

