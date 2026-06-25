# Task Mutation Input Files

This directory contains input files for testing task time mutations in `CheckDependentSubsystems`.

## File Naming Convention

Files are named: `{Subsystem}_{MutationType}_{Description}.json`

- **Subsystem**: `Camera`, `Antenna`, or `Power`
- **MutationType**: `StartOutOfBounds`, `EndOutOfBounds`, `StartInBounds`, `EndInBounds`, `BothInBounds`, `StartAfterEnd`
- **Description**: Additional detail (e.g., `BeforeEventStart`, `AfterEventEnd`, `Changed`, `Unchanged`)

## Mutation Scenarios

### Out of Bounds (Should Fail)
- **StartOutOfBounds_BeforeEventStart**: Task start < event start (0.0)
- **StartOutOfBounds_AfterEventEnd**: Task start > event end (10.0)
- **EndOutOfBounds_BeforeEventStart**: Task end < event start (0.0)
- **EndOutOfBounds_AfterEventEnd**: Task end > event end (10.0)
- **StartAfterEnd**: Task start > task end

### In Bounds (Should Pass)
- **StartInBounds_Changed**: Task start changed but within [0.0, 10.0]
- **EndInBounds_Changed**: Task end changed but within [0.0, 10.0]
- **BothInBounds**: Both start and end changed but within bounds and start <= end

## Default Event/Task Times

All tests use:
- Event Start: 0.0
- Event End: 10.0
- Task Start: 0.0 (before mutation)
- Task End: 10.0 (before mutation)

## Usage

These files are used by `CheckDependentSubsystemsUnitTest` to verify that `CheckTaskStartAndEnd` correctly enforces task time boundaries, even when `CanPerform` would pass.

