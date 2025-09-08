# GenerateExhaustiveSystemSchedules Unit Tests

## Test Objective

**Primary Goal:** Validate the **DEFAULT** schedule combination generation algorithm that runs **once** outside the main timestep loop in `GenerateSchedules()`.

**Methodology:**

- Verify mathematical correctness of combinatorial generation (Tasks^Assets)
- Ensure access windows are automatically set to full simulation duration (Default generation setting)
- Validate input data integrity and loading

## Tested Algorithm Details

The `GenerateExhaustiveSystemSchedules()` method:

1. **Creates Access objects** for each Asset-Task pair with full simulation duration
2. **Groups accesses by asset** into separate stacks
3. **Generates Cartesian product** of all asset access stacks
4. **Returns all possible combinations** as `Stack<Stack<Access>>`

**Expected Algorithm Behavior** --> Default Functionality:

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

## Test Input Files

**Simulation File:**

- **`SchedulerTestSimulationInput.json`**: Base simulation configuration
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

**Task File:**

- **`ThreeTaskTestInput.json`**: Three identical tasks
- **Task Configuration:**
  - `type: "None"` - Generic task type
  - `maxTimes: 100` - High execution limit
  - `target.dynamicState.type: "NULL_STATE"` - Static targets
  - `value: [10.0, 20.0, 30.0]` - Different target values

### Evaluator Configuration

- **`SchedulerTestEval.py`**: Python scripted evaluator
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

- `SchedulerTestSimulationInput.json` - Base simulation configuration
- Model files: `OneAssetTestModel.json`, `TwoAssetTestModel.json`
- Task file: `ThreeTaskTestInput.json`
- `SchedulerSubTest.cs` - Test subsystem implementation
- `SchedulerTestEval.py` - Test evaluator (loaded but not used)

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
