# Observation Data Generator

**Purpose:** Generate empirically observed scheduler behavior for use in test assertions.

⚠️ **IMPORTANT:** This is NOT part of the normal test suite. It generates baseline data that tests will verify against.

## How to Generate Observation Data

**Run the script:**
   ```bash
cd /Users/jason/source/Horizon/test/HSFSchedulerUnitTest/MethodUnitTests/GenerateSchedules/ObservationGenerator
./run_observation_capture.sh
```

**Data will be saved to:**
   ```
Output/Run_1/OneAsset_OneTask.json
Output/Run_1/OneAsset_OneTask.txt
Output/Run_1/OneAsset_ThreeTasks.json
Output/Run_1/OneAsset_ThreeTasks.txt
```

Each run creates a new `Run_N` directory, so files never overwrite.
Use `Run_1` for your baseline test assertions.

## Available Scenarios

| Scenario | Assets | Tasks | MaxTimes | Values | File Prefix |
|----------|--------|-------|----------|--------|-------------|
| 1a1t | 1 | 1 | [2] | [1000] | `OneAsset_OneTask` |
| 1a3t | 1 | 3 | [2,6,10] | [1000,1,0.01] | `OneAsset_ThreeTasks` |
| 2a1t | 2 | 1 | [2] | [1000] | `TwoAssets_OneTask` |
| 2a3t | 2 | 3 | [2,6,10] | [1000,1,0.01] | `TwoAssets_ThreeTasks` |

## Data Structure

```json
{
  "scenarioName": "OneAsset_OneTask",
  "description": "...",
  "maxNumScheds": 10,
  "numSchedCropTo": 5,
  "numAssets": 1,
  "numTasks": 1,
  "taskMaxTimes": {
    "Task1": 2
  },
  "taskValues": {
    "Task1": 1000.0
  },
  "iterations": [
    {
      "iteration": 0,
      "time": 0.0,
      "countBeforeIteration": 1,
      "countAfterIteration": 2,
      "cropOccurred": false,
      "schedulesCropped": 0,
      "schedulesAfterIteration": [
        {
          "scheduleID": "0",
          "value": 0.0,
          "eventCount": 0,
          "name": "Empty Schedule"
        },
        {
          "scheduleID": "1",
          "value": 1000.0,
          "eventCount": 1,
          "name": ""
        }
      ]
    },
    // ... more iterations
  ],
  "finalCrop": {
    // Same structure as iteration
  },
  "timestamp": "2025-11-09T..."
}
```

## Usage in Tests

```csharp
// Load observation data
var observedData = LoadObservationData("OneAsset_OneTask");

// Use in assertions
for (int i = 0; i < observedData.Iterations.Count; i++)
{
    var expected = observedData.Iterations[i];
    
    // Assert count matches
    Assert.That(_systemSchedules.Count, Is.EqualTo(expected.CountAfterIteration));
    
    // Assert exact schedules exist
    var expectedIDs = expected.SchedulesAfterIteration.Select(s => s.ScheduleID).ToHashSet();
    var actualIDs = _systemSchedules.Select(s => s._scheduleID).ToHashSet();
    
    Assert.That(actualIDs, Is.EquivalentTo(expectedIDs), 
        $"i={i}: Exact schedule IDs should match");
    
    // Assert exact values
    foreach (var expectedSched in expected.SchedulesAfterIteration)
    {
        var actualSched = _systemSchedules.FirstOrDefault(s => s._scheduleID == expectedSched.ScheduleID);
        Assert.That(actualSched, Is.Not.Null, $"Schedule {expectedSched.ScheduleID} should exist");
        Assert.That(actualSched.ScheduleValue, Is.EqualTo(expectedSched.Value).Within(0.001));
    }
}
```

## Benefits of This Approach

1. **Exact Verification:** Assert every single schedule ID and value, not just counts
2. **Crop Verification:** Know exactly which schedules survived cropping by value
3. **Reproducibility:** Same input → same output (determinism)
4. **Documentation:** Observation files document expected behavior
5. **Debugging:** If test fails, compare actual vs. observed data
6. **Version Control:** Commit observation files as "expected behavior" snapshots

## Directory Structure

```
ObservationGenerator/
├── ObservationDataCapture.cs   - Main capture logic (inherits from SchedulerUnitTest)
├── ObservationRunner.cs         - Wrapper to run capture ([Explicit] test)
├── run_observation_capture.sh   - Shell script to execute
├── README.md                    - This file
└── Output/                      - Generated observation data
    ├── Run_1/                   - First run (baseline for tests)
    │   ├── OneAsset_OneTask.json
    │   ├── OneAsset_OneTask.txt
    │   ├── OneAsset_ThreeTasks.json
    │   └── OneAsset_ThreeTasks.txt
    └── Run_2/                   - Second run (if needed for comparison)
        ├── OneAsset_OneTask.json
        ├── OneAsset_OneTask.txt
        ├── OneAsset_ThreeTasks.json
        └── OneAsset_ThreeTasks.txt
```

**Note:** Keep the most recent/correct observation file and delete old ones, or use version numbers to track changes.


