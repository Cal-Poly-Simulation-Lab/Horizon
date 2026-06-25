# SortSchedulesDeterministic Unit Tests

## Overview
Unit tests for `Scheduler.SortSchedulesDeterministic()` - the deterministic sorting algorithm that ensures schedules are ordered consistently across runs.

## Method Under Test
`Scheduler.SortSchedulesDeterministic(List<SystemSchedule> schedules, bool descending = true, string context = "")`

## Test Coverage

### 1. Descending Sort (Default)
- ✅ **SortSchedulesDeterministic_Descending_HighestFirst**: Verifies schedules sorted by value descending (50, 40, 30, 20, 10)
- ✅ **SortSchedulesDeterministic_Descending_AllSameValue_PreservesCount**: Verifies tied values don't lose schedules

### 2. Ascending Sort
- ✅ **SortSchedulesDeterministic_Ascending_LowestFirst**: Verifies schedules sorted by value ascending (10, 20, 30, 40, 50)

### 3. Tie-Breaking with Content Hash
- ✅ **SortSchedulesDeterministic_TiedValues_UsesContentHashForDeterminism**: Verifies tied values use content hash for deterministic ordering

### 4. Determinism
- ✅ **SortSchedulesDeterministic_MultipleRuns_SameOrder**: Verifies same input produces same output across multiple runs

### 5. Edge Cases
- ✅ **SortSchedulesDeterministic_EmptyList_NoCrash**: Verifies empty list handled gracefully
- ✅ **SortSchedulesDeterministic_SingleSchedule_Unchanged**: Verifies single schedule unchanged

## Key Features Tested

1. **Deterministic Ordering**: Same input → same output (critical for verification)
2. **Value-Based Sorting**: Primary sort by `ScheduleValue` (descending or ascending)
3. **Content Hash Tie-Breaking**: Secondary sort by `ComputeScheduleHash()` for equal values
4. **Stability**: Multiple runs with same input produce identical ordering

### 6. Real-World Scenario (300 Tasks)
- ✅ **SortSchedulesDeterministic_Aeolus300Tasks_DeterministicSorting**: Verifies sorting works on real 300-task Aeolus scenario
  - Loads full scenario, generates schedules, sorts them
  - Verifies descending order (highest value first)
  - Verifies tie-breaking with content hash for equal values
  - Verifies deterministic ordering (same input → same output)
- ✅ **SortSchedulesDeterministic_Aeolus300Tasks_MultipleRuns_IdenticalOrder**: Verifies multiple runs produce identical order
  - Runs scenario twice, compares sorted results
  - Verifies same number of schedules
  - Verifies same order (by value, then by hash)

## Running Tests

```bash
# Run all tests
dotnet test --filter "FullyQualifiedName~SortSchedulesDeterministic"

# Run only 300-task scenario tests
dotnet test --filter "FullyQualifiedName~Aeolus300Tasks"
```

## Test Input Files

Located in `Inputs/` directory:
- `AeolusSim_150sec_max10_cropTo5.json` - Simulation parameters (150 sec, max 10 schedules, crop to 5)
- `AeolusTasks_300.json` - 300 task deck for Aeolus scenario
- `DSAC_Static_ScriptedCS.json` - Model file with assets and subsystems

## Design Philosophy

The sorting algorithm ensures deterministic ordering by:
1. Sorting primarily by `ScheduleValue`
2. Using content-based hash (`ComputeScheduleHash`) as tie-breaker for equal values
3. Hash is computed from schedule content (events, times, tasks, value), ensuring same content → same hash

This enables:
- Verification that program runs and test runs produce identical schedules
- Confidence in refactoring (same input → same output)
- Parallel testing (order-independent hash set comparison)

