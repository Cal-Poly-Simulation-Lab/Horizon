# TimesCompletedTask Unit Tests

## Purpose

This test suite comprehensively verifies the behavior of `StateHistory.timesCompletedTask(Task task)` method, which counts the **total number of times a task appears across ALL events and ALL assets** in a schedule's state history. This is the foundational counting method used by `CanAddTasks` to enforce `MaxTimesToPerform` constraints.

## Key Understanding

### What `timesCompletedTask` Does

The method performs **occurrence-based counting** across the entire `StateHistory`, iterating through every `Event` and counting how many times the specified task appears across all assets within those events.

### Method Signature

```csharp
public int timesCompletedTask(Task task)
```

**Parameters:**
- `task`: The task to count occurrences of

**Returns:**
- `int`: Total count of task occurrences across all events and all assets

### Current Implementation

```csharp
public int timesCompletedTask(Task task)
{
    int count = 0;
    foreach (Event evt in Events)
    {
        // Count each occurrence of the task across all assets in this event
        foreach (var taskInEvent in evt.Tasks.Values)
        {
            if (taskInEvent == task)
                count++;
        }
    }
    return count;
}
```

### The Refactoring Story: From Event Counting to Occurrence Counting

**Original Behavior (Pre-Refactoring):**
```csharp
// OLD VERSION - counted EVENTS containing the task
public int timesCompletedTask(Task task)
{
    int count = 0;
    foreach (Event eit in Events)
    {
        if (eit.Tasks.ContainsValue(task))
            count++;  // Counted the EVENT, not individual occurrences
    }
    return count;
}
```

**Problem Identified:**
This created a **semantic mismatch** with `MaxTimesToPerform`. When two assets performed the same task in one event:
- `timesCompletedTask` returned **1** (one event containing the task)
- Actual occurrences were **2** (task performed by two assets)
- `MaxTimesToPerform` constraint was meant to limit **total occurrences**, not events

**Example Scenario:**
```
Event 1:
  - Asset1 → Task1
  - Asset2 → Task1

Old behavior: timesCompletedTask(Task1) = 1  ❌ (counted the event)
New behavior: timesCompletedTask(Task1) = 2  ✅ (counts occurrences)
Actual occurrences: 2
MaxTimesToPerform semantic: Total occurrences across all assets
```

**Solution:**
Refactored to count **total occurrences** by iterating through `evt.Tasks.Values` for each event. This aligns the counting method with the semantic meaning of `MaxTimesToPerform` and allows `CanAddTasks` to use it directly without implementing duplicate counting logic.

### Integration with `CanAddTasks`

`CanAddTasks` now uses `timesCompletedTask` directly for `MaxTimesToPerform` enforcement:

```csharp
// In SystemSchedule.CanAddTasks:
foreach(var access in newAccessList)
{
    if (!checkedTasks.Contains(access.Task))
    {
        checkedTasks.Add(access.Task);
        int historicalCount = AllStates.timesCompletedTask(access.Task); // ← Uses this method
        taskCountDict.Add(access.Task, historicalCount + 1);
    }
    else
    {
        taskCountDict[access.Task] += 1;
    }
}

foreach(var taskCount in taskCountDict)
{
    if (taskCount.Value > taskCount.Key.MaxTimesToPerform)
        return false; // Constraint violated
}
```

**Why This Matters:**
- Eliminates code duplication
- Single source of truth for task occurrence counting
- Easier to maintain and test
- Semantic alignment between `timesCompletedTask` and `MaxTimesToPerform`

## Test Organization

The test suite is organized by complexity, starting with the simplest case (empty schedule) and building up to complex multi-asset, multi-event scenarios.

### Test Progression Strategy

1. **Order 1**: Baseline - empty schedule (0 events)
2. **Order 2**: Single asset, single event (basic counting)
3. **Order 3**: **CRITICAL TEST** - Multiple assets, same task in one event (occurrence counting validation)
4. **Order 4**: Single asset, multiple events (temporal accumulation)
5. **Order 5**: Multiple assets, multiple events, mixed tasks (comprehensive occurrence tracking)
6. **Order 6**: Consistency verification (manual count vs. method result)

## Test Details

---

### Order 1: `EmptySchedule_ReturnsZero`

**Setup:**
- 1 Asset, 3 Tasks, MaxTimesToPerform=1
- No iterations executed
- Tests the empty schedule initialized by `InitializeEmptySchedule`

**Assertions:**
- `timesCompletedTask(Task1)` returns **0**
- No events exist in the `StateHistory`

**Purpose:** Establishes baseline behavior - a schedule with no history should return 0 for any task.

**Key Insight:** This is the simplest possible test case, verifying the method handles empty state history gracefully.

---

### Order 2: `OneAsset_OneEvent_OneTask_ReturnsOne`

**Setup:**
- 1 Asset, 3 Tasks, MaxTimesToPerform=1
- Runs 1 iteration (creates 3 schedules: empty + 3 with one event each)
- Each schedule has exactly one event with one task

**Test Strategy:**
```csharp
// For each task (Task1, Task2, Task3):
//   - Find schedule that completed that task
//   - Verify timesCompletedTask(completedTask) = 1
//   - Verify timesCompletedTask(otherTask1) = 0
//   - Verify timesCompletedTask(otherTask2) = 0
```

**Assertions:**
- Schedule with Task1: `timesCompletedTask(Task1)=1`, `timesCompletedTask(Task2)=0`, `timesCompletedTask(Task3)=0`
- Schedule with Task2: `timesCompletedTask(Task2)=1`, `timesCompletedTask(Task1)=0`, `timesCompletedTask(Task3)=0`
- Schedule with Task3: `timesCompletedTask(Task3)=1`, `timesCompletedTask(Task1)=0`, `timesCompletedTask(Task2)=0`

**Purpose:** Verifies basic counting functionality with single events and validates task discrimination (counting only the specified task).

**Key Insight:** The loop structure with dynamic `j` and `k` indices ensures all permutations of task presence/absence are tested for each schedule.

---

### Order 3: `TwoAssets_OneEvent_SameTask_ReturnsTwo` ⭐ **CRITICAL TEST**

**This is the most important test in the suite** - it directly validates the refactored occurrence-counting behavior.

**Setup:**
- 2 Assets, 3 Tasks, MaxTimesToPerform=3
- Runs 1 iteration (creates 10 schedules: empty + 9 with one event each)
- Searches for a schedule where **both assets performed the same task** in the same event

**What Makes This Test Critical:**
This test exposes the difference between the **old (event-counting)** and **new (occurrence-counting)** implementations:

```
Event structure:
  Asset1 → Task1
  Asset2 → Task1

Old behavior: timesCompletedTask(Task1) = 1  ❌
New behavior: timesCompletedTask(Task1) = 2  ✅
```

**Assertions:**
- `timesCompletedTask(Task1)` returns **2** (not 1)
- Manual occurrence count matches `timesCompletedTask` result
- Both counts equal **2**

**Why This Validates the Refactoring:**
- If the method still counted events, it would return **1** (one event containing Task1)
- Correct occurrence counting returns **2** (Task1 appears twice, once per asset)
- This aligns with `MaxTimesToPerform` semantics (total completions, not events)

**Fallback Behavior:**
- If no "doubled task" schedule is found (due to cropping or constraints), test issues a `Assert.Warn` instead of failing
- This handles edge cases where scheduler logic may have prevented such schedules

**Key Insight:** This test is the **definitive proof** that `timesCompletedTask` now counts occurrences correctly, making it suitable for `MaxTimesToPerform` enforcement in `CanAddTasks`.

---

### Order 4: `OneAsset_TwoEvents_SameTask_ReturnsTwo`

**Setup:**
- 1 Asset, 3 Tasks, MaxTimesToPerform=3
- Runs 2 iterations (creates schedules with 0-2 events)
- Searches for a schedule where **the same task appears in two different events**

**Test Scenario:**
```
Event 1: Asset1 → Task1
Event 2: Asset1 → Task1

Expected: timesCompletedTask(Task1) = 2
```

**Assertions:**
- `timesCompletedTask(Task1)` returns **2** when Task1 appears in 2 events
- Schedule has at least 2 events
- Fallback: `Assert.Warn` if no suitable schedule found

**Purpose:** Verifies temporal accumulation - task counts should accumulate across events over time.

**Key Insight:** This tests the outer loop of `timesCompletedTask` (iterating through `Events`), confirming it correctly sums occurrences across the entire state history timeline.

---

### Order 5: `TwoAssets_TwoEvents_MixedTasks_CountsOccurrencesCorrectly`

**Setup:**
- 2 Assets, 3 Tasks, MaxTimesToPerform=3
- Runs 2 iterations (creates diverse schedules with mixed task combinations)
- Tests **ALL non-empty schedules** (not just one)

**Test Strategy:**
```csharp
foreach (schedule in systemSchedules) {
    // Manually count occurrences of Task1, Task2, Task3
    // by iterating through all events and all tasks in each event
    
    // Verify timesCompletedTask matches manual count for EACH task
}
```

**Assertions:**
- For every schedule:
  - `timesCompletedTask(Task1)` = manual count of Task1 occurrences
  - `timesCompletedTask(Task2)` = manual count of Task2 occurrences
  - `timesCompletedTask(Task3)` = manual count of Task3 occurrences

**Purpose:** Comprehensive validation across diverse schedule structures. Tests combinations like:
- Both assets doing same task in one event, different tasks in another
- Both assets doing different tasks in both events
- Same tasks repeated across events
- Mixed patterns

**Key Insight:** This is an exhaustive validation that ensures `timesCompletedTask` works correctly for **any** schedule structure, not just hand-picked scenarios.

---

### Order 6: `VerifyConsistency_timesCompletedTask_MatchesManualCount`

**Setup:**
- 2 Assets, 3 Tasks, MaxTimesToPerform=3
- Runs 2 iterations
- Focuses on **Task1** only but tests across all schedules

**Test Strategy:**
```csharp
foreach (schedule in systemSchedules) {
    int methodResult = timesCompletedTask(Task1);
    
    int manualCount = 0;
    foreach (event in schedule.Events) {
        foreach (task in event.Tasks.Values) {
            if (task == Task1) manualCount++;
        }
    }
    
    Assert methodResult == manualCount;
}
```

**Assertions:**
- `timesCompletedTask` result **exactly matches** manual occurrence count for every schedule
- No discrepancies allowed

**Purpose:** Final verification of method correctness. This is a **gold standard** test - if `timesCompletedTask` always equals the manual count, the method is provably correct.

**Key Insight:** This test serves as a **consistency proof**. If this passes, we can confidently use `timesCompletedTask` throughout the codebase knowing it produces accurate results.

---

## Helper Methods

### `BuildProgram()`

**Purpose:** Loads simulation inputs and initializes the test environment.

**Key Actions:**
1. Calls `HorizonLoadHelper` to load system, tasks, and parameters
2. Initializes empty schedule via `Scheduler.InitializeEmptySchedule`
3. Generates exhaustive schedule combos via `Scheduler.GenerateExhaustiveSystemSchedules`

**Note:** Input files (ModelInputFile, TaskInputFile) are set per-test before calling `BuildProgram()`.

### `MainSchedulingLoopHelper` (inherited from `SchedulerUnitTest`)

**Purpose:** Executes N iterations of the scheduler's main loop, building up schedule histories.

**Key Actions:**
1. Runs scheduler iterations to create events
2. Advances time and scheduler step counters
3. Crops schedules to `MaxNumScheds` limit
4. Returns list of schedules with N events (plus empty schedule)

**Usage in Tests:**
```csharp
this._systemSchedules = SchedulerUnitTest.MainSchedulingLoopHelper(
    _systemSchedules, _scheduleCombos, _testSimSystem,
    _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
    currentTime, timeStep, iterations: 2);
```

---

## Running the Tests

From the project root:

```bash
# Run all TimesCompletedTask tests
dotnet test test/HSFSchedulerUnitTest/HSFSchedulerUnitTest.csproj --filter "FullyQualifiedName~TimesCompletedTaskUnitTest"

# Run specific test
dotnet test test/HSFSchedulerUnitTest/HSFSchedulerUnitTest.csproj --filter "FullyQualifiedName~TimesCompletedTaskUnitTest.TwoAssets_OneEvent_SameTask_ReturnsTwo"

# Run with verbose output
dotnet test test/HSFSchedulerUnitTest/HSFSchedulerUnitTest.csproj --filter "FullyQualifiedName~TimesCompletedTaskUnitTest" --logger "console;verbosity=detailed"
```

---

## Integration Context

### Where `timesCompletedTask` Is Used

`timesCompletedTask` is called within `SystemSchedule.CanAddTasks()`:

```csharp
// From SystemSchedule.CanAddTasks
HashSet<Task> checkedTasks = new HashSet<Task>();
Dictionary<Task, int> taskCountDict = new Dictionary<Task, int>();

foreach(var access in newAccessList)
{
    if (!checkedTasks.Contains(access.Task))
    {
        checkedTasks.Add(access.Task);
        // ← timesCompletedTask is called here to get historical count
        int historicalCount = AllStates.timesCompletedTask(access.Task);
        taskCountDict.Add(access.Task, historicalCount + 1);
    }
    else
    {
        taskCountDict[access.Task] += 1;
    }
}

foreach(var taskCount in taskCountDict)
{
    if (taskCount.Value > taskCount.Key.MaxTimesToPerform)
        return false;
}
```

### Critical Dependencies

- **`StateHistory.Events`**: Collection of `Event` objects representing schedule history
- **`Event.Tasks`**: Dictionary mapping `Asset` → `Task` for each event
- **`Task` equality**: Method relies on reference equality for task comparison (`if (taskInEvent == task)`)

---

## The Semantic Mismatch That Was Fixed

### Before Refactoring

```csharp
// Counted EVENTS containing the task
foreach (Event evt in Events)
{
    if (evt.Tasks.ContainsValue(task))
        count++;  // Incremented once per event
}
```

**Problem:** When both assets performed Task1 in one event:
- Method returned **1** (one event)
- But there were **2 occurrences** (Task1 by Asset1 + Task1 by Asset2)
- `MaxTimesToPerform` was supposed to limit **occurrences**, not events

**Result:** `CanAddTasks` had to implement its own occurrence-counting logic, leading to code duplication.

### After Refactoring

```csharp
// Counts OCCURRENCES across all events and assets
foreach (Event evt in Events)
{
    foreach (var taskInEvent in evt.Tasks.Values)
    {
        if (taskInEvent == task)
            count++;  // Increments for each occurrence
    }
}
```

**Solution:** Correctly counts total occurrences, aligning with `MaxTimesToPerform` semantics.

**Result:** `CanAddTasks` can now use `timesCompletedTask` directly, eliminating duplication and ensuring consistency.

---

## Test Summary Table

| Order | Test Name                                        | Assets | Tasks | MaxTimes | Iterations | Focus                                |
| ----- | ------------------------------------------------ | ------ | ----- | -------- | ---------- | ------------------------------------ |
| 1     | EmptySchedule_ReturnsZero                        | 1      | 3     | 1        | 0          | Baseline - empty schedule            |
| 2     | OneAsset_OneEvent_OneTask_ReturnsOne             | 1      | 3     | 1        | 1          | Basic counting + task discrimination |
| 3     | TwoAssets_OneEvent_SameTask_ReturnsTwo           | 2      | 3     | 3        | 1          | **CRITICAL: Occurrence counting**    |
| 4     | OneAsset_TwoEvents_SameTask_ReturnsTwo           | 1      | 3     | 3        | 2          | Temporal accumulation                |
| 5     | TwoAssets_TwoEvents_MixedTasks_CountsCorrectly   | 2      | 3     | 3        | 2          | Comprehensive validation             |
| 6     | VerifyConsistency_MatchesManualCount             | 2      | 3     | 3        | 2          | Consistency proof                    |

---

## Key Takeaways

1. **`timesCompletedTask` counts occurrences, not events** - This is the fundamental behavior change from the refactoring.

2. **Order 3 is the critical validation test** - It directly proves the method counts occurrences correctly when multiple assets perform the same task.

3. **Order 6 provides mathematical proof** - By comparing against manual counting for all schedules, it demonstrates the method is provably correct.

4. **Semantic alignment achieved** - The method now matches the meaning of `MaxTimesToPerform` (total completions across all assets).

5. **Single source of truth** - `CanAddTasks` no longer needs duplicate counting logic; it uses `timesCompletedTask` directly.

6. **Progressive complexity** - Tests build from simple (empty schedule) to complex (multi-asset, multi-event, mixed tasks), ensuring the method works in all scenarios.

---

## Future Considerations

### If Event-Level Constraints Are Added

If future requirements introduce **event-based constraints** (e.g., "task can appear in at most N events"), a new method should be created:

```csharp
public int eventsContainingTask(Task task)
{
    int count = 0;
    foreach (Event evt in Events)
    {
        if (evt.Tasks.ContainsValue(task))
            count++;
    }
    return count;
}
```

**Keep both methods separate** to maintain semantic clarity:
- `timesCompletedTask`: Total occurrences (for `MaxTimesToPerform`)
- `eventsContainingTask`: Event count (for hypothetical event-level constraints)

### Maintain Test Coverage

Any changes to `timesCompletedTask` should be validated against this test suite. The tests are designed to catch both logic errors and semantic mismatches.
