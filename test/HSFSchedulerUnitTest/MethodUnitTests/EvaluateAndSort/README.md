# EvaluateAndSort Unit Tests

Tests for `Scheduler.EvaluateAndSortCanPerformSchedules()` method.

## Method Under Test

```csharp
public static List<SystemSchedule> EvaluateAndSortCanPerformSchedules(
    Evaluator scheduleEvaluator, 
    List<SystemSchedule> _canPerformList)
```

**Function:**
1. Evaluates each schedule using provided evaluator
2. Sorts schedules by value (descending - highest first)
3. Returns sorted list

## Test Coverage Goals

1. **Evaluation**: Verify all schedules get evaluated
2. **Sorting**: Descending order, ties, edge cases
3. **Evaluator Integration**: Uses provided evaluator correctly
4. **Edge Cases**: Empty list, single item, all same value

## Parallelization Concerns

- `List.Sort()` is NOT thread-safe
- Evaluator may have internal state
- Sorting with ties may be non-deterministic

