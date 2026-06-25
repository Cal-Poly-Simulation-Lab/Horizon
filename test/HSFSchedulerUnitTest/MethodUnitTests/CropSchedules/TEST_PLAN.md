# CropToMaxSchedules Unit Test Plan

**Date Created:** 2025-11-05  
**Purpose:** Establish baseline behavior of `CropToMaxSchedules` and `CropSchedules` methods before parallelization  
**Test File:** `CropSchedulesUnitTest.cs`

---

## Method Under Test

### `CropToMaxSchedules(systemSchedules, emptySchedule, scheduleEvaluator)`

**Location:** `src/HSFScheduler/Scheduler.cs` (Lines 229-244)

**Purpose:** 
- Limit the number of schedules to `MaxNumScheds` by keeping only the highest-valued schedules
- Always preserve the empty schedule
- Track how many schedules were cropped

**Current Implementation Logic:**
```csharp
1. Count current schedules (_oldScheduleCount)
2. If count > MaxNumScheds:
   a. Call CropSchedules(schedules, evaluator, emptySchedule, NumSchedCropTo)
      - Evaluate all schedules (assign ScheduleValue)
      - Sort schedules by value (ascending: worst to best)
      - Remove worst schedules until only NumSchedCropTo remain
   b. Add emptySchedule back to the list
3. Calculate _SchedulesCropped = _oldScheduleCount - systemSchedules.Count()
4. Update all schedule visualizations
5. Return cropped list
```

**Key Parameters:**
- `MaxNumScheds`: Maximum allowed schedules before cropping (e.g., 1000)
- `NumSchedCropTo`: Target count after cropping (e.g., 10)
- `emptySchedule`: The schedule with no tasks (always preserved)
- `scheduleEvaluator`: Function to score schedules (higher = better)

**Parallelization Relevance:**
- **NOT a parallelization target** (already sequential operation at single time step)
- However, evaluating schedules (`scheduleEvaluator.Evaluate()`) *could* be parallelized
- Sorting is inherently sequential
- **Phase 0 Test** (foundation, not part of parallel effort)

---

## Test Strategy

### Black-Box I/O Approach
We test **outputs** given **inputs**, focusing on:
1. **Cropping logic**: Correct number of schedules removed
2. **Value sorting**: Highest-valued schedules retained
3. **Empty schedule preservation**: Always present after cropping
4. **Counter tracking**: `_SchedulesCropped` is accurate

### Test Progression
Start simple → increase complexity:
1. **Edge cases**: No cropping needed, exact limit
2. **Core functionality**: Cropping with clear value differences
3. **Tie-breaking**: Schedules with identical values
4. **Integration**: Cropping after TimeDeconfliction (realistic scenario)

---

## Test Cases

### Category 1: Edge Cases (No Cropping)

#### Test 1: `NoCropping_CountBelowMax_AllSchedulesRetained`
**Setup:**
- MaxNumScheds = 1000
- NumSchedCropTo = 10
- systemSchedules.Count = 5 (below threshold)

**Expected Output:**
- Output count = 5 (no change)
- `_SchedulesCropped = 0`
- All input schedules present in output

**Rationale:** Verify method doesn't crop when unnecessary.

---

#### Test 2: `NoCropping_CountEqualsMax_AllSchedulesRetained`
**Setup:**
- MaxNumScheds = 1000
- NumSchedCropTo = 10
- systemSchedules.Count = 1000 (exactly at threshold)

**Expected Output:**
- Output count = 1000 (no change)
- `_SchedulesCropped = 0`
- All input schedules present in output

**Rationale:** Verify `>` comparison (not `>=`) in cropping condition.

---

### Category 2: Core Functionality (Cropping Required)

#### Test 3: `Cropping_CountAboveMax_LowestValuesRemoved`
**Setup:**
- MaxNumScheds = 100
- NumSchedCropTo = 10
- systemSchedules.Count = 150
- Create 150 schedules with distinct values (0.0, 1.0, 2.0, ..., 149.0)
- emptySchedule value = 50.0

**Expected Output:**
- Output count = 11 (10 cropped + 1 empty)
- `_SchedulesCropped = 139` (150 - 11)
- Output contains schedules with values: [140.0, 141.0, ..., 149.0] (top 10) + emptySchedule
- emptySchedule is present (even though it's not in top 10)

**Rationale:** Verify highest-valued schedules are kept and empty schedule is always preserved.

---

#### Test 4: `Cropping_EmptyScheduleAddedAfterCrop`
**Setup:**
- MaxNumScheds = 100
- NumSchedCropTo = 10
- systemSchedules.Count = 150
- emptySchedule value = 0.0 (worst possible)
- Other schedules have values 1.0 to 150.0

**Expected Output:**
- Output count = 11
- Output contains top 10 valued schedules (141.0-150.0)
- emptySchedule (value 0.0) is present despite having worst value

**Rationale:** Verify empty schedule is *added back* after cropping, not just protected during cropping.

---

#### Test 5: `Cropping_SchedulesCroppedTracking_Accurate`
**Setup:**
- MaxNumScheds = 50
- NumSchedCropTo = 5
- systemSchedules.Count = 100

**Expected Output:**
- Output count = 6 (5 cropped + 1 empty)
- `_SchedulesCropped = 94` (100 - 6)

**Rationale:** Verify counter tracks actual number removed.

---

### Category 3: Value Sorting

#### Test 6: `Sorting_HighestValuesKept_LowestRemoved`
**Setup:**
- MaxNumScheds = 20
- NumSchedCropTo = 5
- systemSchedules.Count = 30
- Schedule values: [100, 90, 80, ..., 10] (descending order)
- emptySchedule value = 50

**Expected Output:**
- Output contains schedules with values: [100, 90, 80, 70, 60] + emptySchedule
- Schedules with values [40, 30, 20, 10] are removed

**Rationale:** Verify sorting is correct (descending by value).

---

#### Test 7: `Sorting_WithTiedValues_DeterministicResults`
**Setup:**
- MaxNumScheds = 20
- NumSchedCropTo = 5
- systemSchedules.Count = 30
- 10 schedules with value = 100.0
- 10 schedules with value = 50.0
- 10 schedules with value = 10.0

**Expected Output:**
- Output count = 6
- Output contains 5 schedules with value 100.0 + emptySchedule
- All schedules with value 10.0 are removed
- Run test multiple times, verify same schedules kept each time (deterministic)

**Rationale:** Verify tie-breaking is deterministic (important for parallel comparison later).

---

### Category 4: Integration with Scheduler

#### Test 8: `Integration_AfterTimeDeconfliction_CropsCorrectly`
**Setup:**
- Run TimeDeconfliction to generate ~100 schedules
- MaxNumScheds = 50
- NumSchedCropTo = 10
- Use real evaluator (DefaultEvaluator)

**Expected Output:**
- Output count ≤ 11
- All output schedules have evaluation values assigned
- Highest-valued schedules from TimeDeconfliction output are retained

**Rationale:** Test realistic scenario where CropToMaxSchedules is called in scheduler loop.

---

### Category 5: Empty Schedule Preservation

#### Test 9: `EmptySchedule_AlwaysPresent_AfterCropping`
**Setup:**
- MaxNumScheds = 10
- NumSchedCropTo = 5
- systemSchedules.Count = 20
- emptySchedule is NOT in initial list (will be added)

**Expected Output:**
- Output count = 6
- emptySchedule is in output list
- `emptySchedule.AllStates.Events.Count == 0`

**Rationale:** Verify empty schedule is added even if it wasn't in the original list.

---

#### Test 10: `EmptySchedule_NotDuplicated_IfAlreadyPresent`
**Setup:**
- MaxNumScheds = 10
- NumSchedCropTo = 5
- systemSchedules.Count = 20 (including emptySchedule)
- emptySchedule is already in the list (value = 0.0)

**Expected Output:**
- Output count = 6 (not 7)
- Only ONE empty schedule in output
- No duplicate empty schedules

**Rationale:** Verify empty schedule isn't duplicated if already present.

---

## Test Implementation Strategy

### Phase 1: Setup Infrastructure
1. Create `CropSchedulesUnitTest.cs` (already exists as skeleton)
2. Add helper methods:
   - `CreateScheduleWithValue(double value)`: Generate a schedule with specific value
   - `CreateMultipleSchedules(int count, Func<int, double> valueGenerator)`: Batch creation
   - `VerifyScheduleValues(List<SystemSchedule> schedules, List<double> expectedValues)`: Assertion helper

### Phase 2: Implement Tests (Order)
1. Start with Test 1 (simplest: no cropping)
2. Test 2 (edge case: exactly at limit)
3. Test 3 (core: cropping with distinct values)
4. Test 4 (empty schedule added)
5. Test 5 (counter tracking)
6. Test 6 (sorting verification)
7. Test 7 (tie-breaking)
8. Test 8 (integration)
9. Test 9-10 (empty schedule edge cases)

### Phase 3: Parameterization
Use `[TestCase]` for variations:
```csharp
[TestCase(50, 10, 20, 0)]  // MaxNumScheds, NumSchedCropTo, InputCount, ExpectedCropped
[TestCase(100, 10, 50, 0)]
[TestCase(100, 10, 150, 139)]
public void Cropping_VariousInputs_CorrectOutput(int maxScheds, int cropTo, int inputCount, int expectedCropped)
{
    // ...
}
```

---

## Helper Methods Needed

### 1. Schedule Creation with Specific Value
```csharp
private SystemSchedule CreateScheduleWithValue(double value)
{
    // Create a schedule
    var schedule = new SystemSchedule(new StateHistory(), currentTime);
    
    // Manually set its value (normally done by evaluator)
    schedule.ScheduleValue = value;
    
    return schedule;
}
```

### 2. Batch Schedule Creation
```csharp
private List<SystemSchedule> CreateMultipleSchedules(int count, Func<int, double> valueGenerator)
{
    var schedules = new List<SystemSchedule>();
    for (int i = 0; i < count; i++)
    {
        schedules.Add(CreateScheduleWithValue(valueGenerator(i)));
    }
    return schedules;
}
```

### 3. Value Verification
```csharp
private void VerifyScheduleValues(List<SystemSchedule> schedules, List<double> expectedValues)
{
    Assert.That(schedules.Count, Is.EqualTo(expectedValues.Count));
    
    var actualValues = schedules.Select(s => s.ScheduleValue).OrderByDescending(v => v).ToList();
    var sortedExpected = expectedValues.OrderByDescending(v => v).ToList();
    
    Assert.That(actualValues, Is.EqualTo(sortedExpected));
}
```

---

## Known Challenges & Solutions

### Challenge 1: Creating Schedules with Specific Values
**Problem:** `ScheduleValue` is normally set by evaluator, which requires complex setup.

**Solution:** Directly assign `ScheduleValue` property in test setup. This is acceptable because we're testing cropping logic, not evaluation logic.

### Challenge 2: Empty Schedule Creation
**Problem:** Need to ensure empty schedule has `Events.Count == 0`.

**Solution:** Use `InitializeEmptySchedule()` logic from existing tests, or create with empty `StateHistory`.

### Challenge 3: Realistic Integration Test
**Problem:** Test 8 requires full scheduler setup with real evaluator.

**Solution:** Reuse setup from `TimeDeconflictionUnitTest` (BuildProgram, GenerateScheduleCombos, etc.).

---

## Success Criteria

All tests pass, demonstrating:
1. ✅ No cropping when count ≤ MaxNumScheds
2. ✅ Correct cropping when count > MaxNumScheds
3. ✅ Highest-valued schedules retained
4. ✅ Empty schedule always present after cropping
5. ✅ `_SchedulesCropped` tracking is accurate
6. ✅ Sorting is deterministic (important for parallel comparison)
7. ✅ Integration with real evaluator works

---

## Parallelization Notes

**Why CropToMaxSchedules is Phase 0 (Not Parallelized):**
- Cropping happens at end of each time step (single point)
- Sorting is inherently sequential
- `MaxNumScheds` is typically small (10-1000), so parallelization overhead > benefit

**However:** Schedule *evaluation* (inside `CropSchedules`) could be parallelized:
```csharp
// Potential future optimization (not in scope for this test plan):
Parallel.ForEach(schedulesToCrop, schedule =>
{
    schedule.ScheduleValue = scheduleEvaluator.Evaluate(schedule);
});
```

**These tests will NOT change** even if evaluation is parallelized (black-box approach).

---

## Commit Strategy

1. **Commit 1:** Add helper methods to `CropSchedulesUnitTest.cs`
2. **Commit 2:** Implement Tests 1-2 (edge cases)
3. **Commit 3:** Implement Tests 3-5 (core functionality)
4. **Commit 4:** Implement Tests 6-7 (sorting)
5. **Commit 5:** Implement Tests 8-10 (integration & empty schedule)
6. **Commit 6:** Add README.md documenting all tests
7. **Tag:** `phase0-crop-tests-complete`

---

## Test File Structure

```
test/HSFSchedulerUnitTest/MethodUnitTests/CropSchedules/
├── CropSchedulesUnitTest.cs       (main test file)
├── TEST_PLAN.md                   (this file)
├── README.md                      (comprehensive documentation, created after tests pass)
└── Inputs/                        (if needed for integration test)
    └── [simulation files]
```

---

**End of Test Plan**

**Next Steps:**
1. Implement helper methods
2. Start with Test 1 (simplest case)
3. Progressively add tests in order
4. Run tests frequently, fix bugs as found
5. Document any unexpected behaviors discovered


