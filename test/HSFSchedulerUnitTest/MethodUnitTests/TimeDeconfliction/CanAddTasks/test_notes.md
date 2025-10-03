# CanAddTasks Unit Tests

## Test Objective

**Primary Goal:** Validate the **SystemSchedule.CanAddTasks** method functionality for proper task addition validation based on event timing constraints and task count limits.

**Methodology:**
- Verify event timing constraint validation (asset's last event must have ended)
- Ensure task count limit enforcement (MaxTimesToPerform)
- Validate asset availability checks (previous events in schedule)
- Test edge cases and error conditions for task addition scenarios

## Test Start Point

**Program Location:** `Scheduler.TimeDeconfliction()` method - **Task Validation Phase**

**Program Flow Context (what comes before):**
1. **InitializeEmptySchedule** - Creates empty baseline schedule
2. **GenerateExhaustiveSystemSchedules** - Generates all possible access combinations
3. **Main Scheduling Loop** - Iterates through simulation timesteps
4. **CropToMaxSchedules** - Maintains schedule count limits
5. **TimeDeconfliction** - For each existing schedule, tries each access combination
6. **CanAddTasks** - Validates if tasks can be added before SystemSchedule construction

**Required Program Setup (BEFORE tested method):**
- `oldSystemSchedule` has valid StateHistory from previous timesteps - *Set via existing schedule from systemSchedules*
- `newAccessList` contains Access objects with Asset, Task, AccessStart, AccessEnd - *Set via scheduleCombos iteration*
- `currentTime` represents current simulation timestep - *Set via main scheduling loop iteration*
- `AllStates.isEmpty(access.Asset)` method available for asset event checking - *Set via StateHistory class*
- `AllStates.GetLastEvent().GetEventEnd(access.Asset)` method available for event timing - *Set via Event class*
- `AllStates.timesCompletedTask(access.Task)` method available for task counting - *Set via StateHistory class*
- `access.Task.MaxTimesToPerform` property available for task limits - *Set via Task class*

**What the Tested Algorithm Does:**
- **Method:** `CanAddTasks(Stack<Access> newAccessList, double currentTime)`
- **Event Timing Validation:** Checks if asset's last event has ended before currentTime using AllStates.GetLastEvent().GetEventEnd(access.Asset) > currentTime
- **Task Count Enforcement:** Counts completed tasks using AllStates.timesCompletedTask(access.Task) and validates against access.Task.MaxTimesToPerform
- **Asset Availability Check:** Ensures asset has previous events using AllStates.isEmpty(access.Asset) before timing validation
- **Return Logic:** Returns false if any constraint fails, true if all tasks can be added
- **Constraint Philosophy:** Prevents task addition if timing conflicts or count limits exceeded

**Program Context After Tested Method:**
- If true: SystemSchedule constructor can proceed with new access combination
- If false: Access combination is skipped, no new schedule created
- Enables TimeDeconfliction to filter valid schedule combinations before construction

## Test Coverage

All tests use the standard test input files and verify the CanAddTasks method behavior in different constraint scenarios.

### 1. Basic Functionality Tests

- **Test:** `CanAddTasks_EmptySchedule_ReturnsTrue`
- **Scenario:** New schedule with no previous events
- **Setup:** Empty StateHistory, valid access list
- **Expected Results:**
  - All assets pass isEmpty check (no previous events)
  - All tasks pass count validation (0 completed)
  - Method returns true

- **Test:** `CanAddTasks_ValidAccess_ReturnsTrue`
- **Scenario:** Normal case with valid timing and counts
- **Setup:** Schedule with ended events, tasks under limit
- **Expected Results:**
  - Event timing validation passes
  - Task count validation passes
  - Method returns true

### 2. Event Timing Constraint Tests

- **Test:** `CanAddTasks_EventNotEnded_ReturnsFalse`
- **Scenario:** Asset's last event hasn't ended
- **Setup:** GetLastEvent().GetEventEnd(asset) > currentTime
- **Expected Results:**
  - Event timing validation fails
  - Method returns false

- **Test:** `CanAddTasks_EventEnded_ReturnsTrue`
- **Scenario:** Asset's last event has ended
- **Setup:** GetLastEvent().GetEventEnd(asset) <= currentTime
- **Expected Results:**
  - Event timing validation passes
  - Method returns true

### 3. Task Count Limit Tests

- **Test:** `CanAddTasks_WithinMaxTimes_ReturnsTrue`
- **Scenario:** Task count under limit
- **Setup:** timesCompletedTask(task) < task.MaxTimesToPerform
- **Expected Results:**
  - Task count validation passes
  - Method returns true

- **Test:** `CanAddTasks_AtMaxTimes_ReturnsFalse`
- **Scenario:** Task count at limit
- **Setup:** timesCompletedTask(task) >= task.MaxTimesToPerform
- **Expected Results:**
  - Task count validation fails
  - Method returns false

### 4. Edge Case Tests

- **Test:** `CanAddTasks_NullTask_ReturnsTrue`
- **Scenario:** Access with null task
- **Setup:** access.Task == null
- **Expected Results:**
  - Task count validation skipped
  - Method returns true

- **Test:** `CanAddTasks_EmptyAccessList_ReturnsTrue`
- **Scenario:** Empty access list
- **Setup:** newAccessList.Count == 0
- **Expected Results:**
  - No constraints to validate
  - Method returns true

### 5. Complex Scenario Tests

- **Test:** `CanAddTasks_MixedConstraints_ReturnsFalse`
- **Scenario:** Some timing issues, some count issues
- **Setup:** Multiple assets with different constraint violations
- **Expected Results:**
  - Any constraint failure causes false return
  - Method returns false

## Subsystem Configuration

**Subsystems Used:**
- `SchedulerSubTest` - Basic test subsystem for system initialization
- **Configuration:** Standard scripted C# subsystem with minimal functionality
- **Purpose:** Provides necessary subsystem structure for SystemClass creation

## Evaluator Configuration

**Evaluator Used:**
- `SchedulerTestEval.py` - Python-based test evaluator
- **Configuration:** Loaded but not actively used in these tests
- **Purpose:** Maintains consistency with other test suites

## Required Files

1. `SchedulerTestSimulationInput.json` - Base simulation configuration
2. `OneAssetTestModel.json` - Single asset model
3. `TwoAssetTestModel.json` - Two asset model
4. `ThreeTaskTestInput.json` - Three task definitions
5. `SixTaskTestInput.json` - Six task definitions (for count testing)
6. `SchedulerSubTest.cs` - Test subsystem implementation
7. `SchedulerTestEval.py` - Test evaluator (loaded but not used)

   **Required Files Filepaths (Repository-root-relative):**

     1. `test/HSFSchedulerUnitTest/InputFiles/SchedulerTestSimulationInput.json`
 	2. `test/HSFSchedulerUnitTest/MethodUnitTests/TimeDeconfliction/CanAddTasks/OneAssetTestModel.json`
 	3. `test/HSFSchedulerUnitTest/MethodUnitTests/TimeDeconfliction/CanAddTasks/TwoAssetTestModel.json`
 	4. `test/HSFSchedulerUnitTest/MethodUnitTests/TimeDeconfliction/CanAddTasks/ThreeTaskTestInput.json`
 	5. `test/HSFSchedulerUnitTest/MethodUnitTests/TimeDeconfliction/CanAddTasks/SixTaskTestInput.json`
 	6. `test/HSFSchedulerUnitTest/Subsystems/SchedulerSubTest.cs`
 	7. `test/HSFSchedulerUnitTest/SchedulerTestEval.py`

## Maintenance Notes

### Last Updated
- **Date:** 2025-01-09
- **Changes:** Initial comprehensive test suite documentation
- **Status:** Complete and functional

### Test Dependencies
- Requires proper initialization of Horizon Program
- Depends on SystemClass and SystemState creation
- Uses standard test input files from InputFiles/ directory

### Future Considerations
- May need additional tests for edge cases (null parameters, etc.)
- Could add tests for static emptySchedule property validation
- Consider testing with different initial system states

## Algorithm Validation

The tests validate the core CanAddTasks algorithm:

1. **Event Timing Validation** - Verifies asset event timing constraints
2. **Task Count Enforcement** - Confirms task completion limit checking
3. **Asset Availability** - Ensures proper asset event existence checking
4. **Return Logic** - Validates correct true/false return behavior
5. **Constraint Philosophy** - Tests the overall constraint validation approach

This test suite ensures the CanAddTasks method provides reliable validation for the scheduling system by properly enforcing timing and count constraints before schedule construction.


