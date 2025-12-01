# CanPerform Unit Tests - TODO

## Next Priority: Dependent Subsystems Testing

### Test: CheckDependentSubsystems Flow
**Goal:** Verify dependency resolution and execution order

- [ ] Create 3 test subsystems with dependency chain:
  - `BaseSubsystem` (no dependencies)
  - `DependentSubsystem1` (depends on `BaseSubsystem`)
  - `DependentSubsystem2` (depends on `DependentSubsystem1`)

- [ ] Test `CheckDependentSubsystems()` calls dependencies in correct order
  - Verify `BaseSubsystem.CanPerform()` called before `DependentSubsystem1.CanPerform()`
  - Verify dependency functions (`GetDependencyCollector()`) return correct profiles

- [ ] Verify state updates propagate through dependency chain
  - `BaseSubsystem` writes state → `DependentSubsystem1` reads it
  - `DependentSubsystem1` writes state → `DependentSubsystem2` reads it

## Time Boundary Constraint Tests

### Test: Task Times Must Stay Within Event Boundaries
**Goal:** Enforce architectural constraint for future parallelization

- [ ] Test valid task time manipulation (within event bounds)
  - `eventStart=0, eventEnd=100`
  - `taskStart=10, taskEnd=90` → PASS

- [ ] Test invalid task time manipulation (outside event bounds)
  - `eventStart=0, eventEnd=100`
  - `taskStart=-5, taskEnd=90` → FAIL (start before event)
  - `taskStart=10, taskEnd=105` → FAIL (end after event)

- [ ] Determine where constraint is enforced:
  - Inside `CanPerform()`? (subsystem responsibility)
  - Inside `Checker.checkSub()`? (scheduler responsibility)
  - Document design decision

### Test: Event Times NOT Manipulated Outside CanPerform
**Goal:** Ensure only `CanPerform()` has authority to modify event times

- [ ] Verify `CheckDependentSubsystems()` does NOT modify event times
  - Capture event times before `CheckDependentSubsystems()` call
  - Assert event times unchanged after call
  - Only task times and state should be modified

- [ ] Verify `Checker.checkSub()` does NOT modify event times
  - Similar test as above but at `checkSub()` level

- [ ] Test that event time manipulation inside `CanPerform()` is allowed
  - Already covered by `CanPerform_CanModifyEventTimes` ✅

## Parameter Loading Verification

### Test: JSON Parameter Parsing
**Goal:** Verify subsystem parameters load correctly from JSON

- [ ] Test integer parameters (`maxIterations`)
  - Verify correct value loaded
  - Test default value when not specified

- [ ] Test double parameters (`taskStartShift`, `eventEndShift`)
  - Verify correct value loaded
  - Test default value when not specified

- [ ] Test string parameters (if needed)
  - Verify correct value loaded

- [ ] Test boolean parameters (if needed)
  - Verify correct value loaded

- [ ] Test parameter type mismatches
  - What happens if JSON has `"value": "string"` but subsystem expects `int`?
  - Should fail gracefully or throw clear error

## State Variable Key Loading

### Test: SetStateVariableKey Verification
**Goal:** Ensure state keys are correctly set and accessible

- [ ] Test that `SetStateVariableKey()` is called during subsystem initialization
  - Verify `StateVariableKey<T>` reference is stored correctly

- [ ] Test that state keys match expected names
  - `asset1.iteration` for `TestCanPerformSubsystem`
  - Verify `stateKey.VariableName` matches expected pattern

- [ ] Test state key type matching
  - `StateVariableKey<int>` for integer state
  - `StateVariableKey<double>` for double state

## Integration with CheckSub

### Test: CheckSub Calls CanPerform Correctly
**Goal:** Verify `Checker.checkSub()` correctly invokes `CanPerform()`

- [ ] Test that `checkSub()` calls `CheckDependentSubsystems()` first
- [ ] Test that `checkSub()` then calls `CanPerform()`
- [ ] Test that `checkSub()` respects `CanPerform()` return value
  - `true` → subsystem passes, schedule continues
  - `false` → subsystem fails, schedule rejected

## Performance / Parallelization Readiness

### Test: Thread Safety Indicators
**Goal:** Identify any remaining shared mutable state

- [ ] Test that multiple `CanPerform()` calls on same subsystem instance are safe
  - Already partially covered by iteration test ✅
  - Expand to test concurrent calls (future work)

- [ ] Test that subsystems do not hold internal state between calls
  - Verify state is always read from `SystemState`, never cached

- [ ] Document any thread-unsafe patterns for refactoring

## Notes

- **Priority:** Dependent subsystems test is most critical for `CheckSub` unit tests
- **Time boundary constraints:** Important for correctness, may require architecture discussion
- **Parameter loading:** Nice-to-have, can be deferred if working correctly
- **Integration tests:** Some of these may belong in `CheckSub` unit tests, not here

## Related Files

- `../CheckSubUnitTest.cs` - Will test `Checker.checkSub()` integration
- `../CheckSubsUnitTest.cs` - Will test batch subsystem checking
- `../CheckScheduleUnitTest.cs` - Top-level schedule checking

