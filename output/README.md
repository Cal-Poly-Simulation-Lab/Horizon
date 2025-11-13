# Horizon Output Directory

All simulation run outputs are automatically organized into versioned directories.

## Directory Structure

```
output/
├── README.md
├── last_run_{timestamp}_{scenario}/   # Most recent run
│   ├── run_log.txt                     # Console output + git metadata
│   ├── schedules_summary.txt           # Schedule details
│   ├── AccessReport.csv                # Access windows
│   ├── .repo_state/                    # Git state for reproducibility
│   │   ├── README.md
│   │   ├── git_diff.txt
│   │   └── git_status.txt
│   └── data/
│       ├── TopSchedule_value{N}_{asset}_Data.csv
│       ├── additional_schedule_data/
│       │   └── {rank}_Schedule_value{N}_{id}.csv
│       └── heritage/                   # Legacy format
│           └── asset*_*.csv
├── Run_00A_{timestamp}_{scenario}/     # Archived runs
├── Run_00B_{timestamp}_{scenario}/
└── ...                                 # Up to Run_99Z
```

## Configuration

### Custom Output Location
```bash
dotnet run -- -s <sim> -m <model> -t <tasks> -o /custom/path
```

### Number of Schedules to Output
Controls how many top schedules get full state data written to `data/additional_schedule_data/`.

Add to simulation JSON at root level:
```json
{
  "name": "MyScenario",
  "numSchedulesForStateOutput": 5,     // Default: 5 top schedules, or "all"
  "startJD": 2454680,
  "startSec": 0,
  ...
}
```

**Output behavior:**
- Top schedule: Always written to `TopSchedule_value{N}_{asset}_Data.csv` (one per asset)
- Top N schedules: Written to `additional_schedule_data/{rank}_Schedule_value{N}_{id}.csv`

## Defaults
- **Location**: `<repo>/output/`
- **Directory**: `last_run_{YYYY-MM-DD_HH-mm-ss}_{ScenarioName}/`
- **State Output**: Top 5 schedules
- **Versioning**: Previous runs archived automatically

## Notes
- Implemented: Nov 11-12, 2025 (commits `80a2606`, `ad48eb3`)
- Versioning prevents overwriting previous results
- Git state captured in `.repo_state/` for full reproducibility
- See `last_run` for example output structure

