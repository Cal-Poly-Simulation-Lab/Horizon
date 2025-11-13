# Checker Unit Tests

Unit-level tests building from simple to complex.

## Test Files (Bottom-Up)

### 1. CanPerformUnitTest.cs (Lowest Level)
**Focus:** Subsystem.CanPerform() for all subsystem types
- Hardcoded, ScriptedCS, Scripted (Python)
- State updates (DOD, pixels, buffer)
- Event timing immutability

### 2. CheckSubUnitTest.cs (Mid Level - combines checkSub + checkSubs)
**Focus:** IsEvaluated logic, dependency triggering
- checkSub: single subsystem evaluation
- checkSubs: batch processing
- Dependency chain triggering

### 3. CheckConstraintsUnitTest.cs (Constraint Logic)
**Focus:** Constraint.Accepts() validation
- FAIL_IF_HIGHER/LOWER/EQUAL types
- Boundary conditions
- Multiple constraints

### 4. CheckScheduleUnitTest.cs (Top Level Integration)
**Focus:** Full orchestration
- IsEvaluated flag management across all subsystems
- Constraint-driven evaluation order
- Multi-asset scenarios
- Overall pass/fail logic

## Parallelization Concerns

- `IsEvaluated`: shared mutable state (race risk)
- Constraint evaluation order
- Thread-safety required before parallelizing CheckAllPotentialSchedules()

