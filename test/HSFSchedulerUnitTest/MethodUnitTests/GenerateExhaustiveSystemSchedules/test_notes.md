# GenerateExhaustiveSystemSchedules Unit Tests

## Test Objective

**Primary Goal:** Validate the **DEFAULT** schedule combination generation algorithm that runs **once** outside the main timestep loop in `GenerateSchedules()`.

**Methodology:**

- Verify mathematical correctness of combinatorial generation (Tasks^Assets)
- Ensure access windows are automatically set to full simulation duration (Default generation setting)
- Validate input data integrity and loading

## Test Start Point

**Program Location:** `Scheduler.GenerateSchedules()` method - **Access Generation Phase**

**Program Flow Context (what comes before):**
1. **Scheduler Constructor** - Initializes scheduler with parameters
2. **InitializeEmptySchedule** - Creates empty baseline schedule
3. **Access Generation Decision** - Checks `canPregenAccess` flag (false = exhaustive mode)
4. **GenerateExhaustiveSystemSchedules** - Generates all possible combinations

**Required Program Setup (BEFORE tested method):**
- `canPregenAccess` is false (default exhaustive generation mode) - *Set via canPregenAccessLogic() method*
- `system` contains loaded assets and subsystems - *Set via Horizon Program loading*
- `tasks` stack contains all mission tasks - *Set via Horizon Program loading*
- `scheduleCombos` is empty stack ready for population - *Set via Scheduler constructor*
- `_startTime` and `_endTime` set from simulation parameters - *Set via Scheduler constructor from SimParameters*
- `system.Assets` contains all available assets for access generation - *Set via SystemClass construction*
- `tasks` contains all mission tasks with proper Task definitions - *Set via program.SystemTasks loading*
- `Access.getCurrentAccessesForAsset()` method available for asset-specific access filtering - *Set via Access class*
- `CartesianProduct()` extension method available for combinatorial generation - *Set via Utilities extension*

**What the Tested Algorithm Does:**
- **Method:** `GenerateExhaustiveSystemSchedules(SystemClass system, Stack<Task> tasks, Stack<Stack<Access>> scheduleCombos, double startTime, double endTime)`
- **Access Object Creation:** Creates Access objects for each Asset-Task combination with full simulation duration (AccessStart = startTime, AccessEnd = endTime)
- **Asset Grouping:** Groups accesses by asset into separate stacks using `Access.getCurrentAccessesForAsset(asset, currentTime)`
- **Combinatorial Generation:** Generates Cartesian product of all asset access stacks using `CartesianProduct()` extension method
- **Return Structure:** Returns complete `Stack<Stack<Access>>` containing all possible schedule combinations (Total = Tasks^Assets)
- **Default Generation Philosophy:** Assumes full access availability for all Asset-Task pairs, no orbital mechanics constraints
- **Access Stack Management:** Each inner Stack<Access> represents one possible schedule combination

**Program Context After Tested Method:**
- `scheduleCombos` populated with all possible access combinations
- Main scheduling loop can begin using these combinations
- Each combination represents one possible schedule for TimeDeconfliction phase

## Test Coverage

- **Access windows** automatically span entire simulation (0.0 to 60.0 seconds here, given input file)
- **No orbital mechanics** - uses NULL_STATE for pure combinatorial testing
- **All possible combinations** generated regardless of feasibility
- **No time-based filtering** - exhaustive generation

## Test Coverage

### 1. Combinatorial Mathematics Validation

- **Test:** `TestNumberTotalCombosGenerated` (Parameterized)
- **Validates:** Schedule combinations follow the formula **Tasks^Assets**
- **Test Cases:**
  - 1 Asset × 3 Tasks = 3 combinations (3^1 = 3)
  - 2 Assets × 3 Tasks = 9 combinations (3^2 = 9)
  - 2 Assets × 16 Tasks = 256 combinations (16^2 = 256)
- **Key Assertions:**
  - `result.Count == _expectedResult`
  - `program.AssetList.Count == _numAssets`
  - `program.SystemTasks.Count == _numTasks`

### 2. Access Time Boundary Validation

- **Test:** `TestAccessStartTime` & `TestAccessSEndTime`
- **Validates:** Access windows are automatically set to full simulation duration
- **Key Assertions:**
  - `access.AccessStart == SimParameters.SimStartSeconds` (0.0)
  - `access.AccessEnd == SimParameters.SimEndSeconds` (60.0)
  - All accesses span entire simulation by default

## Test Input & Required Files

**Simulation File:**

- **`SchedulerTestSimulationInput.json`**: Base simulation configuration (moved to InputFiles/)
- **Simulation Configuration:**
  - `simulationParameters.simStartSeconds: 0.0` - Simulation start time
  - `simulationParameters.simEndSeconds: 60.0` - Simulation end time
  - `simulationParameters.simStepSeconds: 12.0` - Time step size
  - `schedulerParameters.maxNumScheds: 100` - Maximum schedules to maintain
  - `schedulerParameters.numSchedCropTo: 10` - Schedule cropping threshold
- **Purpose:** Provides time boundaries and scheduling parameters for DEFAULT access generation

**Model Files:**

- **`OneAssetTestModel.json`**: Single asset with NULL_STATE dynamics
- **`TwoAssetTestModel.json`**: Two assets with different positions
- **Asset Configuration:**

  - `dynamicState.type: "NULL_STATE"` - No orbital mechanics (static positioning)
  - `Eoms.type: "None"` - No equations of motion
  - `stateData: [position values]` - Simple coordinate data
  - `subsystems: [SchedulerSubTest]` - Barebones test subsystem
  - `constraints: []` - No operational constraints

**Task Files:**

- **`ThreeTaskTestInput.json`**: Three identical tasks

  - `type: "None"` - Generic task type
  - `maxTimes: 100` - High execution limit
  - `target.dynamicState.type: "NULL_STATE"` - Static targets
  - `value: [10.0, 20.0, 30.0]` - Different target values
- **`SixteenTaskTestInput.json`**: Sixteen identical tasks (stress test)

  - `type: "None"` - Generic task type
  - `maxTimes: 100` - High execution limit
  - `target.dynamicState.type: "NULL_STATE"` - Static targets
  - `value: [10.0, 20.0, ..., 160.0]` - Incremental target values
  - **Purpose:** Tests large-scale combinatorial generation (2^16 = 65,536 potential combinations)

### Subsystem Configuration

- **`SchedulerSubTest.cs`**: Test subsystem implementation
- **Subsystem Behavior:**
  - `CanPerform()` always returns `true` for testing purposes
  - `type: "scriptedcs"` - C# scripted subsystem
  - `className: "SchedulerSubTest"` - Class name for dynamic loading
  - **Purpose:** Provides minimal subsystem functionality for combinatorial testing

### Evaluator Configuration

- **`DefaultEvaluator`**: C# default evaluator
- **Note:** Evaluator is loaded but **Checker is never called** in these tests
- **Purpose:** Complete system setup without evaluation logic

### Future Considerations

- **Scripted access generation** may be added in future versions
- **Custom access windows** based on orbital mechanics
- **Time-dependent access patterns** (jebeals - 2024)

## Test Results & Notes

### Pass/Fail Status

- [X] All combinatorial tests passing
- [X] Access time validation passing
- [X] Input data integrity verified

### Key Validations

- **Mathematical correctness** of combination generation
- **Default access duration** spans full simulation
- **Input file loading** produces expected asset/task counts
- **Cartesian product logic** generates all possible combinations

### -----APPENDIX------

### Required Files

1. `SchedulerTestSimulationInput.json` - Base simulation configuration
2. `OneAssetTestModel.json` - Single asset model
3. `TwoAssetTestModel.json` - Two asset model
4. `ThreeTaskTestInput.json` - Three task definitions
5. `SixteenTaskTestInput.json` - Sixteen task definitions (stress test)
6. `SchedulerSubTest.cs` - Test subsystem implementation

   **Required Files Filepaths (Reposity-root-relative):**

     1. `test/HSFSchedulerUnitTest/InputFiles/SchedulerTestSimulationInput.json`
 	2. `test/HSFSchedulerUnitTest/MethodUnitTests/GenerateExhaustiveSystemSchedules/OneAssetTestModel.json`
 	3. `test/HSFSchedulerUnitTest/MethodUnitTests/GenerateExhaustiveSystemSchedules/TwoAssetTestModel.json`
 	4. `test/HSFSchedulerUnitTest/MethodUnitTests/GenerateExhaustiveSystemSchedules/ThreeTaskTestInput.json`
 	5. `test/HSFSchedulerUnitTest/MethodUnitTests/GenerateExhaustiveSystemSchedules/SixteenTaskTestInput.json`
 	6. `test/HSFSchedulerUnitTest/Subsystems/SchedulerSubTest.cs`

## Maintenance Notes

### Last Updated

- **Date:** Sep 8, 2025
- **Author:** jebeals
- **Focus:** DEFAULT schedule combination generation validation

**Build Requirements:**

- .NET 8.0
- NUnit framework
- Horizon project dependencies

### Test Philosophy

This test suite validates the **foundational combinatorial algorithm** that generates all possible asset-task combinations. It uses simplified dummy data to isolate the mathematical logic from complex orbital mechanics and subsystem behaviors.

---

## Quick Reference

### Running Tests

```bash
dotnet test --filter "GenerateExhaustiveSchedulesCombosTest"
```

### Key Assertions

- Combination count = Tasks^Assets
- Access times = Full simulation duration
- Input data loads correctly
