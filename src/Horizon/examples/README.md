# Horizon Space Flight Scheduler (HSF)

**Horizon** is an autonomous scheduling framework for spacecraft mission planning. It generates task schedules hueristics for multi-asset systems under complex subsystem constraints, maximizing mission value while respecting power, data storage, sensor, and communication limitations.

## What It Does

Given a constellation of spacecraft (assets), a list of science tasks (observations, data collection), and subsystem models (power, sensors, data recorders), Horizon:

1. **Generates all possible schedules** - Combinatorial expansion of asset-task-time assignments
2. **Filters by feasibility** - Checks subsystem constraints (power budget, data storage, slew rates)
3. **Evaluates and ranks** - Scores schedules by mission value (science return, data collection)
4. **Optimizes** - Crops to top N schedules, iterates through time steps
5. **Outputs** - Final optimal schedules with state profiles (power, data, pointing, etc.)

**Key Features:**

- Multi-asset scheduling (satellite constellations)
- Subsystem constraint checking (power, ADCS, sensors, comm, data storage)
- Dynamic subsystems (Python scripted or C# compiled at runtime)
- Configurable evaluation (target value, coverage, custom metrics)
- Parallelization-ready architecture (thesis work in progress)

---

## Example Scenarios

### Aeolus - Realistic Earth Observation Mission

**Scenario:** 2 spacecraft, 300 imaging tasks, 90-second mission window
**Subsystems:** ADCS (pointing), EOSensor (camera), SSDR (data storage), Comm (downlink), Power (battery)
**Complexity:** ~1,254 potential schedules per iteration, crops to top 6

**What it demonstrates:** Real-world scheduling with interdependent subsystems, resource constraints, and multi-asset coordination.

### 2Asset3Task - Simple Tutorial Scenario

**Scenario:** 2 test assets, 3 tasks, 60-second mission
**Subsystems:** AlwaysTrueSubsystem (no constraints, for testing)
**Complexity:** Varies by `maxTimesToPerform` (2, 6, 10) - demonstrates combinatorial growth

**What it demonstrates:** Scheduler fundamentals, schedule growth patterns, cropping behavior.

---

## Quick Start

### Prerequisites

**.NET 8.0 SDK** - Download and install:

**macOS:**

```bash
brew install dotnet-sdk
```

Or download from: https://dotnet.microsoft.com/download/dotnet/8.0

**Linux (Ubuntu/Debian):**

```bash
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
```

**Windows:**
Download installer from: https://dotnet.microsoft.com/download/dotnet/8.0

**Verify installation:**

```bash
dotnet --version  # Should show 8.0.x
```

### Build & Run

**Option 1: From Horizon directory (simplest)**

```bash
cd src/Horizon
dotnet clean
dotnet build
dotnet run
```

These above commands run the default 90-sec Aeolus mission using the built-in C# subsystems. This is the same as running the command:

`dotnet run -- -scen Aeolus_CS`

**Option 2: From repo root (with explicit paths)**

```bash
dotnet clean src/Horizon/Horizon.csproj
dotnet build src/Horizon/Horizon.csproj
./src/Horizon/bin/Debug/net8.0/Horizon -scen Aeolus_CS
```

---

## Example Scenarios

**First, navigate to Horizon directory and build** (do this once):

```bash
cd src/Horizon
dotnet clean && dotnet build
```

**All commands below are copy-pasteable and assume you're in `Horizon/src/Horizon`.**

---

### 1. Aeolus - Console Logging OFF (Clean Output)

**300-second mission, 10 iterations, realistic subsystems**

```bash
dotnet run -- \
  -s examples/Aeolus_300sec/AeolusSimulationInput.json \
  -m ../../samples/Aeolus/DSAC_Static_Mod.json \
  -t ../../samples/Aeolus/AeolusTasks.json
```

**Output:** Summary statistics only (Generated, Carried Over, Cropped counts per iteration)
**Runtime:** ~5-10 seconds

---

### 2. Aeolus - Console Logging KEPT (See Surviving Schedules)

**Same mission, but shows which schedules survived cropping**

```bash
dotnet run -- \
  -s examples/Aeolus_300sec/AeolusSimulationInput_LogKept.json \
  -m ../../samples/Aeolus/DSAC_Static_Mod.json \
  -t ../../samples/Aeolus/AeolusTasks.json
```

**Output:** Prints top 5 schedules after each cropping step + final schedules with asset→task breakdowns
**Runtime:** ~5-10 seconds

---

### 3. Simple 2Asset3Task - All Logging, MaxTimes = [10, 10, 10]

**Watch exponential schedule growth**

```bash
dotnet run -- \
  -s examples/2Asset3Task/SimInput_LogAll.json \
  -m examples/2Asset3Task/TwoAssetModel.json \
  -t examples/2Asset3Task/ThreeTaskInput_10_10_10.json
```

**Output:** ALL schedules printed every iteration (verbose)
**Pattern:** Exponential growth: Iteration 0→3 scheds, I1→9, I2→27, I3→81, I4→243 (cropped to 10)
**Runtime:** ~2 seconds
**Good for:** Understanding how combinatorics explode

---

### 4. Simple 2Asset3Task - All Logging, MaxTimes = [2, 6, 10] (The Multiverse)

**Watch how mixed constraints create branching schedule paths**

```bash
dotnet run -- \
  -s examples/2Asset3Task/SimInput_LogAll.json \
  -m examples/2Asset3Task/TwoAssetModel.json \
  -t examples/2Asset3Task/ThreeTaskInput_2_6_10.json
```

**Output:** ALL schedules with IDs showing which tasks are included
**Pattern:** Task1 (value=1000, maxTimes=2) dominates early, saturates quickly
Task2 (value=10, maxTimes=6) fills mid-range
Task3 (value=1, maxTimes=10) enables long-tail exploration
**Runtime:** ~2 seconds
**Good for:** Understanding MaxTimesToPerform constraints and value-based cropping

---

### 5. Simple 1Asset1Task - Visualize Linear Growth (MaxTimes=10)

**Simplest case: one asset, one task, watch the multiverse unfold**

```bash
dotnet run -- \
  -s examples/1Asset1Task/SimInput_LogAll.json \
  -m examples/1Asset1Task/OneAssetModel.json \
  -t examples/1Asset1Task/OneTaskInput_10.json
```

**Output:** ALL schedules printed every iteration  
**Pattern:** Exponential: 1 → 2 → 4 → 8 → 16 → 32 (schedules with 0, 1, 2, 3... tasks)  
**Runtime:** <1 second  
**Good for:** Understanding basic schedule building, combinatorial growth, schedule IDs

---

### 6. Simple 1Asset3Task - Exponential Multiverse (MaxTimes=10 each)

**Watch pure exponential growth with one asset, three tasks**

```bash
dotnet run -- \
  -s examples/1Asset3Task/SimInput_LogAll.json \
  -m examples/1Asset3Task/OneAssetModel.json \
  -t examples/1Asset3Task/ThreeTaskInput_10_10_10.json
```

**Output:** ALL schedules with detailed breakdowns  
**Pattern:** Massive exponential: 3 → 12 → 48 → 192 → 768 → 1024 (cropped to 11 at iteration 4)  
**Runtime:** ~2 seconds  
**Good for:** Visualizing why cropping is necessary, seeing value-based selection in action

**The Multiverse:** Each schedule ID (e.g., `0.1.2.1`) encodes the path through decision space - which tasks were chosen at each iteration. With all logging, you can trace the branching tree of possibilities.

---

## Understanding the Output

**Scheduler Status Line:**

```
Scheduler Status: 33.333% done; Generated: 1188 | Carried Over: 6 | Cropped: 199 | Total: 1194
```

- **Generated:** New schedules created this iteration
- **Carried Over:** Schedules from previous iteration that survived cropping
- **Cropped:** Schedules removed (kept top N by value)
- **Total:** Generated + Carried Over

**Schedule Details (when logging enabled):**

```
Schedule: 0.0.1.1.2  Value: 20010.0000  Events: 3  (A1→Task2 x1, A2→Task3 x2)
```

- **ID:** Unique schedule identifier (hierarchical)
- **Value:** Score from evaluator (higher = better)
- **Events:** Number of task executions
- **Asset→Task pairs:** What each asset is doing

---

## Subsystem Types

**Hardcoded C# Subsystems:**

- ADCS, EOSensor, SSDR, Comm, Power (in `samples/Aeolus/DSAC_Static_Mod.json`)

**Dynamically Compiled C# Subsystems (ScriptedCS):**

- Same subsystems but compiled from source at runtime (in `samples/Aeolus/DSAC_Static_ScriptedCS.json`)

**Python Scripted Subsystems:**

- IronPython-based (in `samples/Aeolus/DSAC_Static_Scripted.json`)

**Test Subsystems:**

- AlwaysTrueSubsystem - always returns true (for unit testing)

---

## Configuration Files

**Simulation Input:** Defines time bounds, step size, scenario name, max schedules, crop count, logging mode
**Model Input:** Defines assets, subsystems (with parameters/states), constraints, dependencies, evaluator
**Task Input:** Defines tasks (name, value, maxTimesToPerform, type, target)

**Logging Modes (set in Simulation Input):**

- `"off"` - Summary stats only
- `"kept"` - Print schedules that survive cropping
- `"all"` - Print ALL schedules every iteration (verbose)

---

## For More Information

- **Unit Tests:** `test/HSFSchedulerUnitTest/` - Comprehensive test suite for scheduler methods
- **Test Documentation:** See `test/HSFSchedulerUnitTest/MethodUnitTests/*/README.md` for detailed test explanations
- **Thesis Notes:** `test/HSFSchedulerUnitTest/MethodUnitTests/GenerateSchedules/notes.md` - Parallelization strategy and progress

---

**Built by:** Jason Ebeals (Master's Thesis, Cal Poly)
**Original HSF Framework:** Dr. Eric Mehiel, Morgan Yost
**Purpose:** Autonomous spacecraft scheduling with parallelization for real-time mission planning
