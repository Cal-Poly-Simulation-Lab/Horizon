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

## Test Coverage

### 1. Dependency Evaluation Order (via State Mutations)
**Tests:** `CheckDependentSubsystems_CameraRunsBeforePower_VerifiedByStateMutation`, `CheckDependentSubsystems_CameraRunsBeforeAntenna_VerifiedByStateMutation`, `CheckDependentSubsystems_FullDependencyChain_VerifiedByStateMutations`

- ✅ Verifies Camera runs before Power by checking Camera's state mutation (images 0 → 1) occurs before Power consumes power
- ✅ Verifies Camera runs before Antenna by checking Camera's state mutation occurs before Antenna reads the state
- ✅ Verifies full dependency chain (Camera → Antenna → Power) by checking all state mutations occur in correct order
- ✅ Uses state mutations to verify order (future-proof, doesn't rely on `IsEvaluated` flag)

**Key Verification:** Dependency evaluation order is correct - subsystems with no dependencies run first, then their dependents, ensuring state is properly updated before dependent subsystems read it.

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

## Test Infrastructure

### Helper Methods
- **`CreateEvent(task, state, eventStart, eventEnd, taskStart, taskEnd)`**
  - Creates an `Event` with specified task, state, and timing parameters
  - Sets event and task start/end times for the asset
  - Used to create test events with controlled timing

- **`GetTask(type)`**
  - Retrieves a task from `program.SystemTasks` by type (case-insensitive)
  - Returns the first matching task

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
- `../../Inputs/TwoAsset_Imaging_Model.json` - System model with Power, Camera, and Antenna subsystems

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

## Running Tests

```bash
# All CheckDependentSubsystems tests
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystemsUnitTest"

# Specific test group
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystems_CameraRunsBefore"
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystems_IMAGING"
dotnet test --filter "FullyQualifiedName~CheckDependentSubsystems_TRANSMIT"
```

## Notes

- **Test execution time:** ~10s for full suite (8 tests)
- **No external dependencies:** All tests use shared input files from `CheckScheudle/Inputs/`
- **Deterministic:** Tests use fixed parameters and expected values
- **Isolated:** Each test creates fresh state and events
- **Future-proof:** Tests verify order via state mutations, not `IsEvaluated` flag (will work when flag is removed)

## Future Work (TODO)

### Task Time Boundary Validation
- Test that `CheckDependentSubsystems` returns `false` when `CanPerform` passes but task times are out of bounds
- Verify `CheckTaskStartAndEnd` correctly enforces task times within event boundaries
- Test various boundary conditions (task start before event start, task end after event end, etc.)

## Thesis Relevance

These tests establish correctness of the dependency evaluation mechanism:
1. **Order verification** ensures dependencies are evaluated before parents (critical for state consistency)
2. **State mutation verification** validates that state updates occur in the correct order
3. **Failure propagation** confirms that dependency failures correctly cause parent failures
4. **Future-proof design** uses state mutations instead of internal flags, ensuring tests remain valid when `IsEvaluated` is removed

All tests passing = dependency evaluation mechanism is working correctly.

