# CanAddTasks Unit Tests

## Purpose

This test suite comprehensively verifies the behavior of `SystemSchedule.CanAddTasks(Stack<Access> newAccessList, double currentTime)` method, which determines whether a set of tasks can be added to an existing schedule without violating timing or `MaxTimesToPerform` constraints.

## Key Understanding

### What `CanAddTasks` Does

The method enforces two critical constraints:

1. **Temporal Constraint**: Ensures all assets have completed their previous events before starting new tasks
2. **Task Count Constraint**: Ensures no task exceeds its `MaxTimesToPerform` limit across all events and assets

### Method Signature

```csharp
public bool CanAddTasks(Stack<Access> newAccessList, double currentTime)
```

### Core Logic

```csharp
// 1. Check timing: previous events must be finished
foreach(var access in newAccessList)
{
    if (!AllStates.isEmpty(access.Asset))
    {
        if (AllStates.GetLastEvent().GetEventEnd(access.Asset) > currentTime)
            return false;  // Asset still busy
    }
}

// 2. Check task counts using Dictionary approach
Dictionary<Task, int> taskCountDict = new Dictionary<Task, int>();
foreach(var access in newAccessList)
{
    if (!checkedTasks.Contains(access.Task))
    {
        int historicalCount = AllStates.timesCompletedTask(access.Task);
        taskCountDict.Add(access.Task, historicalCount + 1);
        checkedTasks.Add(access.Task);
    }
    else
    {
        taskCountDict[access.Task] += 1;  // Another asset doing same task
    }
}

// 3. Validate against MaxTimesToPerform
foreach(var taskCount in taskCountDict)
{
    if (taskCount.Value > taskCount.Key.MaxTimesToPerform)
        return false;
}

return true;
```

### Important Constraint Assumption

**All tests assume that if ANY asset in the `newAccessList` cannot add its task, the ENTIRE schedule addition fails.** There is no partial success where some assets add tasks while others do nothing. This design decision simplifies scheduling logic but may be revisited in future versions.

---

## Test Organization

Tests are organized into three regions based on complexity:

### Region 1: Time Tests + Combinatorics

- **Focus**: Temporal constraint enforcement and schedule permutation generation
- **Tests**: Event timing validation, combinatorics helper

### Region 2: Simple Tests (One Asset)

- **Focus**: Basic `CanAddTasks` behavior with single asset
- **Tests**: Orders 1-3, progressively increasing complexity

### Region 3: Advanced Tests (Two Assets, Multiple Tasks)

- **Focus**: Multi-asset scenarios with task doubling and complex constraints
- **Tests**: Orders 4-7, including the comprehensive third-iteration test

---

## Detailed Test Descriptions

### Region 1: Time Tests + Combinatorics

#### `TestCanAddTasks_EventTime_OneAsset_ThreeTask_100TimesMax` (Unordered)

**Setup:**

- 1 Asset, 3 Tasks, MaxTimesToPerform=100 (effectively unlimited)
- Runs 1 iteration, then advances to second iteration checkpoint

**Focus:** Temporal constraint validation

**Test Strategy:**

1. Verify all schedules (empty + 3 with history) pass `CanAddTasks` at correct time
2. **Manipulate event end times** to be AFTER `currentTime`
3. Verify `CanAddTasks` correctly **rejects** when timing violated
4. Restore original times and verify it passes again

**Key Insight:** Isolates temporal checking from task counting by using a very high  `MaxTimesToPerform`  = 100.

---

#### `Create_Combinatorics_TwoAssetThreeTask` (Order 0)

**Setup:**

- 2 Assets, 3 Tasks, 5 time steps
- No scheduler execution - pure combinatorics

**Focus:** Schedule permutation generation

**Algorithm:**

```csharp
// Generates all possible schedule paths across iterations
// Format: "11-22-33" means (A1→T1, A2→T1) then (A1→T2, A2→T2) then (A1→T3, A2→T3)
scheduleCombos = ["0", "11", "12", "13", "21", "22", "23", "31", "32", "33"]
// Iteration 0: 1 schedule (empty)
// Iteration 1: 10 schedules (1 * 10)
// Iteration 2: 100 schedules (10 * 10)
// Iteration 3: 1000 schedules (100 * 10)
```

**Key Insight:** The "0" represents the empty schedule persisting across iterations. Without cropping or `MaxTimesToPerform` constraints, the schedule space grows exponentially: 10^n.

---

### Region 2: Simple Tests (One Asset)

These tests use ONE asset to eliminate multi-asset complexity ("doubled up" tasks), making them deterministic and easy to reason about.

#### Order 1: `OneAssetOneTask_OneTimeMax_FirstIterationReturnsTrue`

**Setup:**

- 1 Asset, 1 Task, MaxTimesToPerform=1
- Tests empty schedule at iteration 0

**Assertions:**

- Empty schedule has 0 events
- `CanAddTasks` returns `true` (task not yet completed)
- `timesCompletedTask` returns 0

**Why It Passes:** MaxTimesToPerform=1, adding it once: 0+1=1 ≤ 1 ✅

---

#### Order 2: `OneAssetOneTask_OneTimeMax_SecondIterationReturnsFalse`

**Setup:**

- 1 Asset, 1 Task, MaxTimesToPerform=1
- Runs 1 iteration (creates 2 schedules: empty + 1 with history)
- Tests at iteration 1 checkpoint

**Assertions:**

- Empty schedule: `CanAddTasks` returns `true` (still 0+1=1 ≤ 1)
- Schedule with history:
  - Has 1 event
  - `timesCompletedTask` returns 1
  - `CanAddTasks` returns `false` (would be 1+1=2 > 1) ❌

**Key Insight:** With one asset, MaxTimesToPerform=1 means the task can only be done once across the entire schedule history.

---

#### Order 3: `OneAssetThreeTask_ThreeMaxTimes_ThirdIterationAlwaysTrue`

**Setup:**

- 1 Asset, 3 Tasks, MaxTimesToPerform=3 (for all tasks)
- Runs 2 iterations, tests at iteration 2 checkpoint

**Assertions:**

- ALL schedules (empty + those with 1-2 events) can add ANY task
- `CanAddTasks` always returns `true`

**Why It Always Passes:** With one asset, after 2 iterations, any given task has been completed AT MOST twice. Adding it again: 2+1=3 ≤ 3 ✅. Since there's only one asset, tasks cannot "double up" in a single event, so the maximum historical count for any task is bounded by the number of iterations.

---

### Region 3: Advanced Tests (Two Assets)

These tests introduce **multi-asset complexity**, where both assets can perform the same task in a single event ("doubling up"), causing that task's count to increment by 2 instead of 1.

#### Order 4: `EmptySchedule_CanAddTasks_ReturnsTrue_TwoAssetThreeTask_2TimesMax`

**Setup:**

- 2 Assets, 3 Tasks, MaxTimesToPerform=2 (for all tasks)
- Tests empty schedule only (no iterations)

**Assertions:**

- All 9 schedule combos (3^2) should pass on empty schedule
- Both assets doing same task: 0+2=2 ≤ 2 ✅
- Different tasks: 0+1+1=2 ≤ 2 ✅

**Key Insight:** MaxTimesToPerform=2 is the minimum value that allows all combos to pass on empty schedule with 2 assets. If MaxTimesToPerform=1, combos where both assets do the same task would fail.

---

#### Order 5: `TwoAssetThreeTask_OneTimeMax_FirstIterationReturnsCorrectCombinations`

**Setup:**

- 2 Assets, 3 Tasks, MaxTimesToPerform=1 (for all tasks)
- Tests empty schedule only (iteration 0 checkpoint, no history yet)

**Critical Behavior:**

- **Same task** (both assets): `CanAddTasks` returns `false`
  - Reason: 0+2=2 > 1 ❌
- **Different tasks**: `CanAddTasks` returns `true`
  - Reason: Each task 0+1=1 ≤ 1 ✅

**Expected Combos That Pass:** 6 out of 9

- ❌ 11, 22, 33 (same task doubled)
- ✅ 12, 13, 21, 23, 31, 32 (different tasks)

---

#### Order 6: `TwoAssetThreeTask_OneTimeMax_SecondIterationReturnsFalse_ExceptForSelectEmptyCombos`

**Setup:**

- 2 Assets, 3 Tasks, MaxTimesToPerform=1
- Runs 1 iteration (creates 6 schedules with history + empty)
- Tests at iteration 1 checkpoint

**Assertions:**

- **Empty schedule:** Same behavior as Order 5

  - Same task: `false` (0+2 > 1)
  - Different tasks: `true` (0+1, 0+1 ≤ 1)
- **All non-empty schedules:** `CanAddTasks` ALWAYS returns `false`

  - Reason: Each schedule already has 2 tasks completed (one per asset)
  - ANY new combo would add at least 2 more tasks
  - With MaxTimesToPerform=1, ALL tasks are already "used up"

**Key Insight:** After one iteration with 2 assets and MaxTimesToPerform=1, only the empty schedule can branch further (and only with different tasks).

---

#### Order 7: `TwoAssetThreeTask_ThreeMaxTimes_ThirdIterationAlwaysTrue`

**The Most Complex Test** - Tests the complete interplay of task counting across multiple iterations.

**Setup:**

- 2 Assets, 3 Tasks, MaxTimesToPerform=3
- Runs 2 iterations, tests at iteration 2 checkpoint
- Generates schedules with 0, 1, or 2 events (depending on cropping)

**Test Strategy:**
The test **mirrors the exact logic of `CanAddTasks`** to predict expected outcomes:

```csharp
// For each schedule, for each new combo:
1. Count historical occurrences of each task (manually)
2. Count how many times each task appears in newAccessStack
3. Check if historical + new > MaxTimesToPerform
4. Assert CanAddTasks matches this prediction
```

**Edge Cases Tested:**

1. **Empty schedule** (0 events):

   - Same task (0+2=2 ≤ 3): `true` ✅
   - Different tasks (0+1, 0+1 ≤ 3): `true` ✅
2. **Schedules with 1 event** (various task combinations):

   - Depends on which tasks were completed
   - Example: If schedule has Task1 done twice (both assets), trying to add Task1 again:
     - Same task: 2+2=4 > 3: `false` ❌
     - Including Task1 with others: depends on how many times Task1 appears in new combo
3. **Schedules with 2 events** (maximum history):

   - Most restrictive case
   - Many combos will fail because tasks approach their limits
   - Example: Task1 done 3 times historically + trying to add it once: 3+1=4 > 3: `false` ❌

**Why This Test Is Critical:**

- Tests real scheduling scenarios with partial schedule histories
- Verifies task counting works correctly when tasks are "doubled up" in events
- Ensures `CanAddTasks` correctly enforces `MaxTimesToPerform` across complex multi-asset, multi-iteration scenarios
- Validates the dictionary-based counting approach in `CanAddTasks`

**Complexity:** This test has ~100+ individual `CanAddTasks` calls (schedules × combos after 2 iterations), each with unique historical context.

---

## Helper Methods

### `CanAddTasks_MainSchedulingLoop`

**Purpose:** Runs N iterations of the scheduler and positions system state at the checkpoint BEFORE `TimeDeconfliction` is called in iteration N+1.

**Key Actions:**

1. Calls `MainSchedulingLoopHelper` to run iterations
2. Advances `SchedulerStep`, `CurrentTime`, `NextTime`
3. Calls `CropToMaxSchedules` to mirror real scheduler flow

**Why It's Needed:** Tests need to examine schedules at the exact moment `CanAddTasks` would be called in the real scheduler.

---

### `PrintAttemptedTaskAdditionInfo`

**Purpose:** Generates detailed debug output for assertion failures.

**Output Format:**

```
SchedID: 5
Event [0]: (testasset1->Task1,testasset2->Task3)
Tried to add:
(testasset1->Task2,testasset2->Task1)
```

**Usage:** Included in assertion messages to provide context when tests fail.

---

## Running the Tests

From the project root:

```bash
# Run all CanAddTasks tests
dotnet test test/HSFSchedulerUnitTest/HSFSchedulerUnitTest.csproj --filter "FullyQualifiedName~CanAddTasksUnitTest"

# Run a specific test
dotnet test test/HSFSchedulerUnitTest/HSFSchedulerUnitTest.csproj --filter "FullyQualifiedName~CanAddTasksUnitTest.TwoAssetThreeTask_ThreeMaxTimes_ThirdIterationAlwaysTrue"

# Run tests in order
dotnet test test/HSFSchedulerUnitTest/HSFSchedulerUnitTest.csproj --filter "FullyQualifiedName~CanAddTasksUnitTest" -- NUnit.DefaultTestNamePattern="*"
```

---

## Relation to Core Scheduler Logic

### Where `CanAddTasks` Is Called

`CanAddTasks` is invoked in `Scheduler.TimeDeconfliction()`:

```csharp
foreach (var oldSystemSchedule in systemSchedules)
{
    foreach (var newAccessStack in scheduleCombos)
    {
        if (oldSystemSchedule.CanAddTasks(newAccessStack, currentTime))
        {
            var newSchedule = new SystemSchedule(oldSystemSchedule.AllStates, 
                                                  newAccessStack, currentTime, oldSystemSchedule);
            potentialSystemSchedules.Add(newSchedule);
        }
    }
}
```

Only schedules that pass `CanAddTasks` proceed to `SystemSchedule` construction and subsystem validation.

### Critical Dependencies

- **`StateHistory.timesCompletedTask(Task task)`**: Counts total occurrences of a task across all events and assets
- **`StateHistory.GetLastEvent()`**: Retrieves the most recent event for timing checks
- **`Event.GetEventEnd(Asset asset)`**: Gets when an asset's event ends
- **`Task.MaxTimesToPerform`**: The constraint being enforced

---

## Known Limitations & Future Considerations

### Current Design Assumption

Tests assume **all-or-nothing task addition**: if any asset in `newAccessList` violates constraints, the entire combo is rejected. No partial additions are allowed (e.g., one asset adds a task while another does nothing).

### If Partial Additions Are Implemented

Many of these tests would need refactoring:

- Orders 5-7 would have significantly different combinatorics
- The "doubled up" task rejection logic would change
- More schedule combinations would become valid

These tests serve as a baseline for the current design and would guide refactoring if the constraint model changes.

---

## Test Summary Table

| Order | Test Name                                 | Assets | Tasks | MaxTimes | Iterations | Focus                     |
| ----- | ----------------------------------------- | ------ | ----- | -------- | ---------- | ------------------------- |
| -     | TestCanAddTasks_EventTime                 | 1      | 3     | 100      | 1          | Temporal constraints      |
| 0     | Create_Combinatorics                      | 2      | 3     | N/A      | 0          | Permutation generation    |
| 1     | OneAssetOneTask_OneTimeMax_First          | 1      | 1     | 1        | 0          | Basic empty schedule      |
| 2     | OneAssetOneTask_OneTimeMax_Second         | 1      | 1     | 1        | 1          | Basic with history        |
| 3     | OneAssetThreeTask_ThreeMaxTimes_Third     | 1      | 3     | 3        | 2          | Always pass scenario      |
| 4     | EmptySchedule_TwoAssetThreeTask_2TimesMax | 2      | 3     | 2        | 0          | Multi-asset empty         |
| 5     | TwoAssetThreeTask_OneTimeMax_First        | 2      | 3     | 1        | 0          | Task doubling rejection   |
| 6     | TwoAssetThreeTask_OneTimeMax_Second       | 2      | 3     | 1        | 1          | Historical rejection      |
| 7     | TwoAssetThreeTask_ThreeMaxTimes_Third     | 2      | 3     | 3        | 2          | Comprehensive integration |
