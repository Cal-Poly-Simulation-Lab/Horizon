# CanPerform Unit Tests

## Overview

Unit tests for `Subsystem.CanPerform()` method across C# (`ScriptedCS`) and Python (`Scripted`) subsystem types. These tests verify that subsystems correctly evaluate events, maintain stateless design, and properly manipulate state data and event timing.

## Design Philosophy

### Stateless Subsystem Architecture
- **Subsystems hold PARAMETERS, not STATE**
  - Configuration (e.g., `maxIterations`, `shiftAmount`) stored as private fields
  - State data (e.g., iteration count) lives in `SystemState`, not in subsystem instance
  - Ensures thread-safety for future parallelization

### State Management
- **State keys are REFERENCES to SystemState data**
  - `SetStateVariableKey()` stores `StateVariableKey<T>` references
  - `CanPerform()` reads/writes via `event.State.GetLastValue()` / `AddValues()`
  - State is time-series (`HSFProfile<T>`), indexed by task/event times

### Time Manipulation
- **Subsystems CAN modify event and task times**
  - `SetTaskStart()`, `SetTaskEnd()`, `SetEventStart()`, `SetEventEnd()` are accessible
  - Tests verify this capability (design decision for thesis discussion)
  - Future constraint: task times must stay within event boundaries (TODO)

## Test Coverage

### 1. Stateless Iteration Tracking
**Tests:** `CanPerform_IterationLoop_ReturnsCorrectly` (ScriptedCS, Scripted)

- ✅ Reads current iteration from `SystemState` (not internal field)
- ✅ Increments and writes new iteration at task start time
- ✅ Returns `true` while under `maxIterations` threshold
- ✅ Returns `false` when at max (subsystem fails correctly)
- ✅ Verifies iteration state persists in `event.State` across calls

**Key Verification:** Subsystem is stateless - calling `CanPerform()` 5 times on same subsystem instance correctly increments shared state.

### 2. Task Time Manipulation
**Tests:** `CanPerform_CanModifyTaskTimes` (ScriptedCS, Scripted)

- ✅ `GetTaskStart()` / `GetTaskEnd()` retrieve current times
- ✅ `SetTaskStart()` / `SetTaskEnd()` successfully modify times
- ✅ Verification: `taskStart + 5.0`, `taskEnd - 3.0` applied correctly
- ✅ Both C# and Python implementations behave identically

**Design Note:** Task time manipulation is currently allowed. Future tests will enforce task times stay within event boundaries.

### 3. Event Time Manipulation
**Tests:** `CanPerform_CanModifyEventTimes` (ScriptedCS, Scripted)

- ✅ `GetEventStart()` / `GetEventEnd()` retrieve current times
- ✅ `SetEventStart()` / `SetEventEnd()` successfully modify times
- ✅ Verification: `eventStart + 7.0`, `eventEnd - 2.5` applied correctly
- ✅ Both C# and Python implementations behave identically

**Design Note:** Event time manipulation inside `CanPerform()` is currently allowed. Future constraint tests will verify event times are NOT manipulated outside `CanPerform()` (e.g., in `CheckDependentSubsystems`).

### 4. Toy Example Subsystems (TwoAsset_Imaging Scenario)
**Tests:** `CanPerform_ToyExample_*` (6 tests)

Uses the shared **TwoAsset_Imaging** scenario with `TestPowerSubsystem`, `TestCameraSubsystem`, and `TestAntennaSubsystem` to validate real-world subsystem behavior.

#### Power Subsystem Tests
- **`CanPerform_ToyExample_Power_FirstIteration_Passes`**
  - ✅ Verifies Power subsystem passes first IMAGING task (75.0 >= 10.0 required)
  - ✅ Confirms power is correctly decremented (75.0 → 65.0)
  - ✅ Validates state update occurs at correct time

- **`CanPerform_ToyExample_Power_FailsWhenInsufficient`**
  - ✅ Verifies Power subsystem fails TRANSMIT when power (15.0) < required (20.0)
  - ✅ Confirms failure condition is correctly detected
  - ✅ Tests power constraint enforcement

#### Camera Subsystem Tests
- **`CanPerform_ToyExample_Camera_FirstIteration_Passes`**
  - ✅ Verifies Camera subsystem passes first IMAGING task (0 < 10 max)
  - ✅ Confirms images are correctly incremented (0 → 1)
  - ✅ Validates state update occurs at correct time

- **`CanPerform_ToyExample_Camera_FailsAtMaxImages`**
  - ✅ Verifies Camera subsystem fails when numImages (10) >= maxImages (10)
  - ✅ Confirms capacity limit is correctly enforced
  - ✅ Tests buffer overflow prevention

#### Antenna Subsystem Tests
- **`CanPerform_ToyExample_Antenna_FailsWithNoImages`**
  - ✅ Verifies Antenna subsystem fails TRANSMIT when numImages (0) <= 0
  - ✅ Confirms dependency on camera buffer is enforced
  - ✅ Tests prerequisite validation

- **`CanPerform_ToyExample_Antenna_PassesWithImages`**
  - ✅ Verifies Antenna subsystem passes TRANSMIT when numImages (5.0) > 0
  - ✅ Confirms images are correctly decremented (5.0 → 4.0)
  - ✅ Confirms transmissions are correctly incremented (0 → 1.0)
  - ✅ Validates dual state variable updates

**Key Verification:** Each subsystem correctly:
- Passes on first iteration when conditions are met
- Fails at the right time/iteration when constraints are violated
- Updates state values correctly and deterministically

## Test Infrastructure

### Helper Methods
- **`HorizonLoadHelper(simJson, taskJson, modelJson)`**
  - Loads full scenario using production infrastructure
  - Returns `Horizon.Program` with assets, subsystems, tasks, universe
  - Ensures tests use real loading logic (no mocking)

- **`LoadAndGeneratePotentialSchedules(modelFile)`**
  - Loads scenario and generates initial potential schedules
  - Returns `(asset, subsystem, potentialSchedules, universe)` tuple
  - Used for tests requiring pre-existing schedules with events

- **`LoadToyExampleScenario()`**
  - Loads the TwoAsset_Imaging scenario from shared inputs
  - Returns `(asset1, powerSub, cameraSub, antennaSub, universe)` tuple
  - Used for toy example subsystem tests
  - Accesses subsystems via `program.SubList` filtered by asset

- **`VerifyTimeMutationParametersAreZero(powerSub, cameraSub, antennaSub)`**
  - Verifies that test subsystems have time mutation parameters (`_taskStartTimeMutation`, `_taskEndTimeMutation`) set to 0
  - Uses reflection to access methods on dynamically compiled subsystems
  - Ensures existing tests are not affected by time mutations (subsystems won't change task times)

### Test Subsystems

#### `TestCanPerformSubsystem` (C# & Python)
**Purpose:** Simple stateless subsystem for iteration tracking

**Parameters:**
- `maxIterations` (int, default=5): Max allowed iterations before failure
- `test_parameter` (double, default=0.0): Unused, for parameter loading verification

**State:**
- `iteration` (int, initial=0): Counter incremented each `CanPerform()` call

**Behavior:**
- Reads current iteration from `SystemState`
- Increments and writes new value at task start time
- Returns `false` when `newIteration >= maxIterations`

#### `TaskTimeManipulatorSubsystem` (C# & Python)
**Purpose:** Time manipulation verification

**Parameters:**
- `taskStartShift` (double): Amount to shift task start time
- `taskEndShift` (double): Amount to shift task end time
- `eventStartShift` (double): Amount to shift event start time
- `eventEndShift` (double): Amount to shift event end time

**Behavior:**
- Reads current task/event times
- Applies shifts via `Set[Task|Event][Start|End]()`
- Always returns `true`

**Test Configurations:**
- **Task tests:** `taskStartShift=5.0, taskEndShift=-3.0, eventShifts=0.0`
- **Event tests:** `taskShifts=0.0, eventStartShift=7.0, eventEndShift=-2.5`

#### Toy Example Subsystems (TestPowerSubsystem, TestCameraSubsystem, TestAntennaSubsystem)
**Purpose:** Real-world subsystem behavior validation using the TwoAsset_Imaging scenario

**TestPowerSubsystem:**
- **Parameters:**
  - `imagePowerRequired` (double, default=10.0): Power required for IMAGING task
  - `transmitPowerRequired` (double, default=20.0): Power required for TRANSMIT task
  - `rechargeValue` (double, default=25.0): Power added during RECHARGE task
  - `maxPower` (double, default=100.0): Maximum power capacity
  - `minPower` (double, default=0.0): Minimum power capacity
- **State:** `checker_power` (double, initial=75.0)
- **Behavior:**
  - IMAGING: Consumes `imagePowerRequired`, fails if insufficient power
  - TRANSMIT: Consumes `transmitPowerRequired`, fails if insufficient power
  - RECHARGE: Adds `rechargeValue`, fails if would exceed `maxPower`

**TestCameraSubsystem:**
- **Parameters:**
  - `maxImages` (double, default=10.0): Maximum number of images that can be stored
- **State:** `num_images_stored` (double, initial=0.0)
- **Behavior:**
  - IMAGING: Increments image count, fails if `numImages >= maxImages`
  - Other tasks: Returns `true` (no-op)

**TestAntennaSubsystem:**
- **Parameters:** None
- **State:** 
  - `num_images_stored` (double, shared with Camera, initial=0.0)
  - `num_transmissions` (double, initial=0.0)
- **Behavior:**
  - TRANSMIT: Decrements image count, increments transmission count, fails if `numImages <= 0`
  - Other tasks: Returns `true` (no-op)

## Input Files

### Scenario Files (Shared)
- `SimInput_TwoAssetImaging_ToyExample.json`: Minimal simulation config (1 asset, 1 task, 5 timesteps)
- `OneTaskInput.json`: Single task definition

### Model Files (Subsystem-Specific)
- `TestCanPerformModel_ScriptedCS.json`: C# iteration test subsystem
- `TestCanPerformModel_Scripted.json`: Python iteration test subsystem
- `TaskTimeManipulator_ScriptedCS.json`: C# task time manipulation
- `TaskTimeManipulator_Scripted.json`: Python task time manipulation
- `EventTimeManipulator_ScriptedCS.json`: C# event time manipulation
- `EventTimeManipulator_Scripted.json`: Python event time manipulation

### Toy Example Scenario Files (Shared from `CheckSchedule/Inputs/`)
- `SimInput_TwoAssetImaging_ToyExample.json`: Simulation parameters (shared)
- `TwoAsset_Imaging_Tasks.json`: Task definitions (RECHARGE, IMAGING, TRANSMIT)
- `TwoAsset_Imaging_Model.json`: System model with Power, Camera, and Antenna subsystems

## Future Work (TODO)

### Dependent Subsystems
- Test subsystem dependency resolution
- Verify `CheckDependentSubsystems()` correctly calls dependencies before `CanPerform()`
- Ensure dependency functions return correct profiles

### Time Boundary Constraints
- **Task times must stay within event boundaries**
  - Assert `taskStart >= eventStart && taskEnd <= eventEnd`
  - Test that violating this constraint fails appropriately
- **Event times should NOT be manipulated outside `CanPerform()`**
  - Verify `CheckDependentSubsystems()` does not modify event times
  - Only `CanPerform()` should have time manipulation authority

### Parameter Loading Verification
- Test that subsystem parameters load correctly from JSON
- Verify type conversions (int, double, string, bool)
- Test default parameter values when not specified

## Running Tests

```bash
# All CanPerform tests
dotnet test --filter "FullyQualifiedName~CanPerformUnitTest"

# Specific test group
dotnet test --filter "FullyQualifiedName~CanPerform_IterationLoop"
dotnet test --filter "FullyQualifiedName~CanModifyTaskTimes"
dotnet test --filter "FullyQualifiedName~CanModifyEventTimes"
dotnet test --filter "FullyQualifiedName~CanPerform_ToyExample"

# Specific subsystem type
dotnet test --filter "FullyQualifiedName~CanPerformUnitTest.ScriptedCS"
dotnet test --filter "FullyQualifiedName~CanPerformUnitTest.Scripted"

# Toy example tests only
dotnet test --filter "FullyQualifiedName~CanPerform_ToyExample"
```

## Notes

- **Test execution time:** ~10s for full suite (12 tests)
- **No external dependencies:** All tests use minimal input files
- **Deterministic:** Tests use fixed parameters and expected values
- **Isolated:** Each test uses `SetUp`/`TearDown` for clean state
- **Toy example tests:** Use shared input files from `CheckSchedule/Inputs/` to validate real subsystem behavior
- **Time mutation verification:** Toy example tests verify time mutation parameters are 0 to ensure subsystems don't modify task times (preserves test logic)

## Thesis Relevance

These tests establish baseline correctness before parallelization:
1. **Stateless verification** ensures no shared mutable state
2. **Iteration tracking** validates state updates are deterministic
3. **Time manipulation** documents current design decisions for discussion
4. **C# vs Python parity** confirms both subsystem types behave identically

All tests passing = ready for parallelization work.
