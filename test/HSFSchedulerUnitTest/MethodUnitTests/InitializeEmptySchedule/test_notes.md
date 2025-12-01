# InitializeEmptySchedule Unit Tests

## Test Objective

**Primary Goal:** Validate the **Scheduler.InitializeEmptySchedule** static method functionality for proper initialization of the empty schedule baseline.

**Methodology:**

- Verify correct creation and naming of the empty schedule
- Ensure proper addition to the system schedules list
- Validate initial state setup with no events
- Test both local and program-level schedule list initialization

## Test Start Point

**Program Location:** `Scheduler.GenerateSchedules()` method - **Initialization Phase**

**Required Program Setup (BEFORE tested method):**

- Scheduler constructor completes initialization - *Set via Scheduler(Evaluator) constructor*
- `systemSchedules` list is empty - *Set via Scheduler constructor initialization*
- `initialStateList` (SystemState) provided from program initialization - *Set via Horizon Program loading*
- Horizon Program loaded with assets, subsystems, and tasks - *Set via HorizonLoadHelper()*
- `SystemSchedule(StateHistory, string)` constructor available for empty schedule creation - *Set via SystemSchedule class*
- `Scheduler.emptySchedule` static property ready for assignment - *Set via Scheduler class definition*

**What the Tested Algorithm Does:**

- **Method:** `InitializeEmptySchedule(List<SystemSchedule> systemSchedules, SystemState initialStateList)`
- **Empty Schedule Creation:** Creates baseline empty schedule with name "Empty Schedule" using SystemSchedule(initialStateList, Name)
- **List Management:** Adds empty schedule to `systemSchedules` list via systemSchedules.Add(emptySchedule)
- **Static Reference:** Sets static `Scheduler.emptySchedule` reference for global access throughout scheduling process
- **Baseline Establishment:** Provides foundation schedule for all future schedule additions and cropping operations
- **Schedule Continuity:** Ensures at least one schedule always exists for TimeDeconfliction phase

**Program Context After Tested Method:**

- `systemSchedules` now contains the empty baseline schedule
- Subsequent GenerateSchedules steps can begin (access generation, time deconfliction, etc.)
- Empty schedule serves as foundation for all future schedule additions

## Test Coverage

All tests use the standard test input files and verify the InitializeEmptySchedule method behavior in different contexts.

### 1. Local System Schedules Test

- **Test:** `EmptyScheduleInitialized`
- **Scenario:** Initialize empty schedule in local test variable
- **Setup:** Empty `_systemSchedules` list before initialization
- **Expected Results:**
  - List count = 0 before initialization
  - List count = 1 after initialization
  - Schedule name = "Empty Schedule"
  - Schedule has 0 events (empty state)

### 2. Program-Level System Schedules Test

- **Test:** `EmptyScheduleExistsInProgram`
- **Scenario:** Initialize empty schedule in program's scheduler
- **Setup:** Empty `program.scheduler.systemSchedules` list before initialization
- **Expected Results:**
  - Program list count = 0 before initialization
  - Program list count = 1 after initialization
  - Schedule name = "Empty Schedule"
  - Schedule has 0 events (empty state)

## Test Execution Order

Tests are ordered to ensure proper execution sequence:

1. **`EmptyScheduleInitialized`** - Tests local initialization first
2. **`EmptyScheduleExistsInProgram`** - Tests program-level initialization second

## Assertion Strategy

Each test uses `Assert.Multiple()` to verify multiple conditions:

- **Count validation** - Ensures list size changes correctly
- **Name validation** - Verifies correct schedule naming
- **State validation** - Confirms empty event state
- **Pre-condition checks** - Validates starting state before initialization

## Error Handling

Tests include explicit failure conditions for:

- Non-empty list before initialization (should be empty)
- Empty list after initialization (should contain one schedule)

## Subsystem Configuration

**Subsystems Used:**

- `SchedulerSubTest` - Basic test subsystem for system initialization
- **Configuration:** Standard scripted C# subsystem with minimal functionality
- **Purpose:** Provides necessary subsystem structure for SystemClass creation

## Evaluator Configuration

**Evaluator Used:**

- `DefaultEvaluator` - C# default evaluator
- **Configuration:** Loaded but not actively used in these tests
- **Purpose:** Maintains consistency with other test suites

## Required Files

1. `SchedulerTestSimulationInput.json` - Base simulation configuration
2. `SchedulerTestTasks.json` - Task definitions
3. `SchedulerTestModel.json` - System model with assets and subsystems
4. `SchedulerSubTest.cs` - Test subsystem implementation

   **Required Files Filepaths (Repository-root-relative):**

   1. `test/HSFSchedulerUnitTest/InputFiles/SchedulerTestSimulationInput.json`
   2. `test/HSFSchedulerUnitTest/InputFiles/SchedulerTestTasks.json`
   3. `test/HSFSchedulerUnitTest/InputFiles/SchedulerTestModel.json`
   4. `test/HSFSchedulerUnitTest/Subsystems/SchedulerSubTest.cs`

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

- N/A

## Algorithm Validation

The tests validate the core InitializeEmptySchedule algorithm:

1. **Schedule Creation** - Verifies new SystemSchedule instance creation
2. **Naming Convention** - Ensures consistent "Empty Schedule" naming
3. **List Management** - Confirms proper addition to system schedules list
4. **State Initialization** - Validates empty event state setup
5. **Static Reference** - Tests both local and program-level list handling

This test suite ensures the InitializeEmptySchedule method provides a reliable foundation for the scheduling system by creating a proper empty baseline schedule.
