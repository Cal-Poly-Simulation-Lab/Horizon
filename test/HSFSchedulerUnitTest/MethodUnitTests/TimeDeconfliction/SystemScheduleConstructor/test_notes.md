# SystemScheduleConstructor Unit Tests

## Test Objective

**Primary Goal:** Validate the **SystemSchedule constructor** logic for proper EventStart/End and TaskStart/End time assignment based on access windows.

**Methodology:**

- Verify correct time boundary calculations for events and tasks
- Ensure proper handling of access windows within event timeframes
- Validate exception handling for invalid access time scenarios
- Test edge cases for access windows that span, start before, or end after event boundaries

## Test Start Point

**Program Location:** `Scheduler.TimeDeconfliction()` method - **Schedule Creation Phase**

**Program Flow Context (what comes before):**
1. **InitializeEmptySchedule** - Creates empty baseline schedule
2. **GenerateExhaustiveSystemSchedules** - Generates all possible access combinations
3. **Main Scheduling Loop** - Iterates through simulation timesteps
4. **CropToMaxSchedules** - Maintains schedule count limits
5. **TimeDeconfliction** - For each existing schedule, tries each access combination

**Required Program Setup (BEFORE tested method):**
- `systemSchedules` contains existing schedules (including empty schedule) - *Set via InitializeEmptySchedule*
- `scheduleCombos` generated with access/task combinations for current time - *Set via GenerateExhaustiveSystemSchedules*
- `currentTime` represents current simulation timestep - *Set via main scheduling loop iteration*
- `oldSystemSchedule` has valid StateHistory from previous timesteps - *Set via existing schedule from systemSchedules*
- `CanAddTasks()` validation has already passed for this access combination - *Set via TimeDeconfliction loop validation*
- `SimParameters.SimStepSeconds` available for event boundary calculations - *Set via simulation parameter loading*
- `newAccessStack` contains valid Access objects with Asset, Task, AccessStart, AccessEnd - *Set via scheduleCombos iteration*
- `AllStates.GetLastState()` provides initial system state for new schedule - *Set via StateHistory management*
- `Event` constructor ready to create new event with tasks and SystemState - *Set via Event class availability*

**What the Tested Algorithm Does:**
- **Constructor:** `SystemSchedule(StateHistory oldStates, Stack<Access> newAccessStack, double currentTime)`
- Creates new SystemSchedule from existing StateHistory and new Access stack
- **Event Boundary Calculation:** EventStart = currentTime, EventEnd = currentTime + SimStepSeconds
- **Task Timing Logic:** 
  - TaskStart = max(currentTime, access.AccessStart) if access.AccessStart > currentTime, else currentTime
  - TaskEnd = min(currentTime + SimStepSeconds, access.AccessEnd) if access.AccessEnd < EventEnd, else EventEnd
- **Access Validation:** Throws InvalidOperationException for AccessStart >= AccessEnd, times before currentTime, times after EventEnd
- **Event Creation:** Creates new Event with tasks dictionary, SystemState from AllStates.GetLastState()
- **StateHistory Management:** Constructs new StateHistory(oldStates, eventToAdd) for schedule continuity
- **Event/Task Philosophy:** Events are fundamental timestep windows, tasks restricted to single event window

**Program Context After Tested Method:**
- New SystemSchedule added to `potentialSystemSchedules` list
- Schedule contains properly timed events and tasks for current timestep
- Enables subsequent schedule evaluation and cropping operations

## Test Coverage

All tests were initiated with Test Input Files, and then Access times were manually modifed before `SystemSchedule(StateHistory oldStates, Stack<Access> newAccessStack, double currentTime)` was called, in order to observe the output of the Constructor. The different test cases are as follows:

### 1. Full Access Window Test

- **Test:** `FullAccessTest_0_60s`
- **Scenario:** Access window spans entire simulation (0.0 to 60.0 seconds)
- **Current Time:** 0.0 seconds
- **Event Duration:** 0.0 to 12.0 seconds (one timestep)
- **Expected Results:**
  - `EventStart` = 0.0 (currentTime)
  - `EventEnd` = 12.0 (currentTime + SimStepSeconds)
  - `TaskStart` = 0.0 (access starts at/before event start)
  - `TaskEnd` = 12.0 (access extends past event end, capped at event end)

### 2. Late Access Start Test

- **Test:** `LateAccessTest_6_60s`
- **Scenario:** Access starts at 6.0 seconds, ends at 60.0 seconds
- **Current Time:** 0.0 seconds
- **Event Duration:** 0.0 to 12.0 seconds (one timestep)
- **Expected Results:**
  - `EventStart` = 0.0 (currentTime)
  - `EventEnd` = 12.0 (currentTime + SimStepSeconds)
  - `TaskStart` = 6.0 (access starts after event start, use access start)
  - `TaskEnd` = 12.0 (access extends past event end, capped at event end)

### 3. Short Access Window Test

- **Test:** `LateAccessTest_6_14s`
- **Scenario:** Access starts at 6.0 seconds, ends at 14.0 seconds
- **Current Time:** 0.0 seconds
- **Event Duration:** 0.0 to 12.0 seconds (one timestep)
- **Expected Results:**
  - `EventStart` = 0.0 (currentTime)
  - `EventEnd` = 12.0 (currentTime + SimStepSeconds)
  - `TaskStart` = 6.0 (access starts after event start, use access start)
  - `TaskEnd` = 12.0 (access extends past event end, capped at event end)

### 4. Within Event Access Test

- **Test:** `WithinEventAccessTest_6_11s`
- **Scenario:** Access starts at 6.0 seconds, ends at 11.0 seconds (fully within event)
- **Current Time:** 0.0 seconds
- **Event Duration:** 0.0 to 12.0 seconds (one timestep)
- **Expected Results:**
  - `EventStart` = 0.0 (currentTime)
  - `EventEnd` = 12.0 (currentTime + SimStepSeconds)
  - `TaskStart` = 6.0 (access starts after event start, use access start)
  - `TaskEnd` = 11.0 (access ends before event end, use access end)

## Exception Handling

### Invalid Access Time Scenarios

The constructor validates access times and throws `InvalidOperationException` for:

1. **AccessStart >= AccessEnd**

   - Error: "AccessStart must be less than AccessEnd"
   - **Note:** These accesses should not occur unless scripted access generation is implemented incorrectly
2. **Both access times before currentTime**

   - Error: "Access times are both before current time"
   - **Scenario:** Access window completely in the past
3. **Both access times after eventEnd**

   - Error: "Access times are both after event end time"
   - **Scenario:** Access window completely in the future

## Test Input Files

Input files are rather

**Simulation File:**

- **`SchedulerTestSimulationInput.json`**: Base simulation configuration (moved to InputFiles/)
- **Simulation Configuration:**
  - `simulationParameters.simStartSeconds: 0.0` - Simulation start time
  - `simulationParameters.simEndSeconds: 60.0` - Simulation end time
  - `simulationParameters.simStepSeconds: 12.0` - Time step size (fundamental timestep)

**Model Files:**

- **`DefaultOneAssetModelInput.json`**: Single asset configuration
- **`TwoAssetModelInput.json`**: Two asset configuration
- **Asset Configuration:**
  - `dynamicState.type: "NULL_STATE"` - No orbital mechanics
  - `subsystems: [AlwaysTrueSubsystem]` - Test subsystem
  - `constraints: []` - No operational constraints

**Task Files:**

- **`DefaultThreeTaskInput.json`**: Three test tasks
- **`TwoTaskInput.json`**: Two test tasks
- **Task Configuration:**
  - `type: "None"` - Generic task type
  - `maxTimes: 100` - High execution limit
  - `target.dynamicState.type: "NULL_STATE"` - Static targets

### Subsystem Configuration

- **`AlwaysTrueSubsystem.cs`**: Test subsystem implementation
- **Subsystem Behavior:**
  - `CanPerform()` always returns `true` for testing purposes
  - `type: "scriptedcs"` - C# scripted subsystem
  - `className: "AlwaysTrueSubsystem"` - Class name for dynamic loading
  - **Purpose:** Provides minimal subsystem functionality for constructor testing

### Evaluator Configuration

- **`SchedulerTestEval.py`**: Python scripted evaluator
- **Evaluator Behavior:**
  - `type: "scripted"` - Python scripted evaluator
  - `className: "eval"` - Python class name
  - **Note:** Evaluator is loaded but **Checker is never called** in these tests
  - **Purpose:** Complete system setup without evaluation logic

## Test Results & Notes

### Pass/Fail Status

- [X] FullAccessTest_0_60s - PASSED
- [X] LateAccessTest_6_60s - PASSED
- [X] LateAccessTest_6_14s - PASSED
- [X] WithinEventAccessTest_6_11s - PASSED

### Key Validations

- **Event timing** correctly set to fundamental timestep boundaries
- **Task timing** properly calculated based on access window availability
- **Edge case handling** for access windows that span, start before, or end after events
- **Exception handling** for invalid access time scenarios
- **Time boundary logic** follows specified requirements

### Implementation Notes

**Event/Task Time Assignment Logic:**

- Events represent fundamental timestep occurrences
- Tasks are restricted to single event windows (current HSF iteration)
- Future versions may allow tasks to span multiple timesteps
- Access windows determine task availability within events

**Exception Handling Philosophy:**

- Invalid access times indicate upstream generation errors
- Future Aeolus pregenAccess validation tests will be implemented
- Current tests focus on constructor logic, not access generation

### -----APPENDIX------

### Required Files

1. `SchedulerTestSimulationInput.json` - Base simulation configuration
2. `DefaultOneAssetModelInput.json` - Single asset model
3. `TwoAssetModelInput.json` - Two asset model
4. `DefaultThreeTaskInput.json` - Three task definitions
5. `TwoTaskInput.json` - Two task definitions
6. `AlwaysTrueSubsystem.cs` - Test subsystem implementation
7. `SchedulerTestEval.py` - Test evaluator (loaded but not used)

   **Required Files Filepaths (Reposity-root-relative):**

    1.`test/HSFSchedulerUnitTest/InputFiles/SchedulerTestSimulationInput.json`
	2. `test/HSFSchedulerUnitTest/MethodUnitTests/TimeDeconfliction/SystemScheduleConstructor/DefaultOneAssetModelInput.json`
	3. `test/HSFSchedulerUnitTest/MethodUnitTests/TimeDeconfliction/SystemScheduleConstructor/TwoAssetModelInput.json`
	4. `test/HSFSchedulerUnitTest/MethodUnitTests/TimeDeconfliction/SystemScheduleConstructor/DefaultThreeTaskInput.json`
	5. `test/HSFSchedulerUnitTest/MethodUnitTests/TimeDeconfliction/SystemScheduleConstructor/TwoTaskInput.json`
	6. `test/HSFSchedulerUnitTest/Subsystems/AlwaysTrueSubsystem.cs`
	7. `test/HSFSchedulerUnitTest/SchedulerTestEval.py`

## Maintenance Notes

### Last Updated

- **Date:** Sep 23, 2025
- **Author:** jebeals
- **Focus:** SystemSchedule constructor time assignment logic validation

**Build Requirements:**

- .NET 8.0
- NUnit framework
- Horizon project dependencies

### Test Philosophy

This test suite validates the **core time assignment logic** in the SystemSchedule constructor. It ensures that events and tasks are properly timed based on access window availability within fundamental timestep boundaries, providing the foundation for accurate schedule construction.

---

## Quick Reference

### Running Tests

```bash
dotnet test --filter "SystemScheduleConstructorUnitTest"
```

### Key Assertions

- EventStart = currentTime
- EventEnd = currentTime + SimStepSeconds
- TaskStart = earliest access time within event
- TaskEnd = latest access time within event
- Exception handling for invalid access times
