# TimesCompletedTask Unit Tests

## Purpose

This test suite verifies the behavior of `StateHistory.timesCompletedTask(Task task)` method.

## Key Understanding

### What `timesCompletedTask` Actually Does

The method counts **how many events contain the specified task**, NOT the total number of times the task appears across all assets.

```csharp

publicinttimesCompletedTask(Tasktask)

{

intcount = 0;

foreach (EventeitinEvents)

    {

if (eit.Tasks.ContainsValue(task))

count++;  // Increments once per event, regardless of how many assets did the task

    }

returncount;

}

```

### The Semantic Mismatch

There's an important mismatch between:

1.**What `timesCompletedTask` counts**: Events containing the task (1 per event)

2.**What `MaxTimesToPerform` actually means**: Total task occurrences across all assets

### Example Scenario

If an event has:

- Asset1 → Task1
- Asset2 → Task1

Then:

-`timesCompletedTask(Task1)` returns **1** (one event containing Task1)

- But the **actual occurrence count** is **2** (Task1 performed by 2 assets)

This is why `CanAddTasks` had to implement its own counting logic instead of using `timesCompletedTask`.

## Test Coverage

### Test Order 1: EmptySchedule_ReturnsZero

Verifies that an empty schedule returns 0 for any task.

### Test Order 2: OneAsset_OneEvent_OneTask_ReturnsOne

Tests basic counting with a single asset and single event.

### Test Order 3: TwoAssets_OneEvent_SameTask_ReturnsOne_NotTwo

**CRITICAL TEST**: Demonstrates that when both assets do the same task in one event, `timesCompletedTask` returns 1 (not 2). This is correct behavior for the method, but highlights why it can't be used for `MaxTimesToPerform` enforcement.

### Test Order 4: OneAsset_TwoEvents_SameTask_ReturnsTwo

Verifies counting across multiple events with the same task.

### Test Order 5: TwoAssets_TwoEvents_MixedTasks_CountsCorrectly

Validates counting across multiple events with various task combinations.

### Test Order 6: CompareEventCountVsOccurrenceCount

Demonstrates the difference between event counting (what `timesCompletedTask` does) and occurrence counting (what `MaxTimesToPerform` needs). Identifies "doubled up" tasks where the counts diverge.

## Running the Tests

From the project root:

```bash

dotnettesttest/HSFSchedulerUnitTest/HSFSchedulerUnitTest.csproj--filter"FullyQualifiedName~TimesCompletedTaskUnitTest"

```

Run a specific test:

```bash

dotnettesttest/HSFSchedulerUnitTest/HSFSchedulerUnitTest.csproj--filter"FullyQualifiedName~TimesCompletedTaskUnitTest.TwoAssets_OneEvent_SameTask_ReturnsOne_NotTwo"

```

## Relation to CanAddTasks

These tests validate the behavior of `timesCompletedTask`, which helps explain why `CanAddTasks` needed its own custom counting logic:

```csharp

// CanAddTasks uses this logic instead of timesCompletedTask:

inthistoricalCount = 0;

foreach (EventevtinAllStates.Events)

{

foreach (vartaskinevt.Tasks.Values)

    {

if (task == access.Task)

historicalCount++;  // Counts total occurrences, not events

    }

}

```

This ensures `MaxTimesToPerform` is enforced based on total task occurrences across all assets, not just the number of events containing the task.
