# Master's Thesis: Safe Parallelization of the Horizon Simulation Framework (HSF) Scheduling Algorithm

**Author:** [Your Name]  
**Date Started:** November 1, 2025  
**Advisor:** [Advisor Name]

---

## Thesis Outline

### Chapter 1: Introduction & Motivation

#### 1.1 Horizon Simulation Framework Overview
- **What is HSF?**
  - Discrete-event simulation framework for space mission scheduling
  - Models satellite systems, subsystems, constraints, and tasks
  - Generates feasible schedules for satellite operations
  
- **HSF's Place in the Simulation Landscape**
  - Comparison to other simulation frameworks (STK, GMAT, etc.)
  - Unique features: subsystem-level modeling, scripted subsystems, constraint hierarchy
  - Use cases: mission planning, constellation scheduling, resource allocation

#### 1.2 Current Limitations
- Sequential scheduling algorithm
- Performance bottleneck for large mission scenarios
  - Example: 100+ targets, multiple satellites, 48-hour scheduling horizon
  - Current performance: [TBD: baseline measurements from Aeolus]
- Need for faster scheduling to enable:
  - Real-time mission replanning
  - Larger search spaces (more targets, longer horizons)
  - Monte Carlo analysis for uncertainty quantification

---

### Chapter 2: The Parallelization Problem

#### 2.1 The Multiverse Branch-Searching Algorithm
- **Core Concept:**
  - At each time step, scheduler explores multiple "universes" (schedule branches)
  - Each schedule represents a different sequence of task completions
  - Branches grow exponentially: 1 → N → N×M → N×M×K schedules
  
- **Algorithm Flow:**
  ```
  Time Step 0: [Empty Schedule]
  Time Step 1: Generate 9 schedules (3 tasks × 3 assets combinations)
  Time Step 2: Each of 9 schedules tries to add new tasks → potentially 81 schedules
  Time Step 3: Cropping limits growth, but still evaluating 100+ schedule branches
  ```

- **Why It's "Laughably Parallelizable":**
  - Each schedule evaluation is **independent** at a given time step
  - No dependencies between schedule branches during `TimeDeconfliction`
  - Perfect candidate for embarrassingly parallel computation
  - **BUT:** Must ensure data safety when parallelizing

#### 2.2 Thread Safety & Data Races
- **What is Thread Safety?**
  - Multiple threads accessing shared data concurrently
  - Risks: race conditions, data corruption, non-deterministic results
  
- **Why It Matters for HSF:**
  - Schedule objects share references to tasks, assets, subsystems
  - Static variables used for tracking (e.g., `Scheduler._schedID`)
  - Constraint evaluation may access shared system state
  
- **Key Challenges:**
  1. **Schedule ID generation** - static counter incremented in loop
  2. **StateHistory deep copying** - must ensure true independence
  3. **CanPerform subsystem evaluation** - potential shared state in subsystems
  4. **IronPython scripted subsystems** - Global Interpreter Lock (GIL) implications

#### 2.3 Schedule Timing & Task Acceptance Logic
- **High-Level Design Decisions:**
  - `CanAddTasks` enforces temporal constraints (event end times)
  - `MaxTimesToPerform` limits task repetitions across schedule history
  - Constraint hierarchy: asset-level → subsystem-level → system-level
  
- **Why This Matters for Parallelization:**
  - These checks are **read-only** on existing schedules (good for parallelization)
  - New schedule creation is **independent** (each thread creates its own)
  - But must verify: no hidden shared state in constraint evaluation

---

### Chapter 3: Methodology

#### 3.1 Unit Testing Framework Approach
- **Philosophy: Test-Driven Parallelization**
  - "If we implement thorough unit tests, then we can parallelize, test, and ensure functionality"
  - Establish baseline sequential correctness first
  - Parallelize incrementally, validating at each step
  - Feature flag enables A/B testing (sequential vs. parallel)

- **Test Categories:**
  1. **Method-Level Unit Tests**
     - `CanAddTasks`: Validates task acceptance logic
     - `timesCompletedTask`: Validates task counting across schedules
     - `TimeDeconfliction`: Validates schedule generation
     - `CropToMaxSchedules`: Validates schedule pruning
  
  2. **Integration Tests**
     - `MainSchedulingLoop`: End-to-end scheduling with simple inputs
     - Aeolus benchmark: Real-world mission scenario
  
  3. **Parallelization-Specific Tests**
     - Determinism tests (parallel == sequential results)
     - Thread safety tests (no data races)
     - Race condition detection (unique schedule IDs)
     - Performance benchmarking (speedup measurements)
     - Stress tests (large inputs, memory usage)

#### 3.2 Test Development Process
- **Phase 1: Baseline Sequential Testing** ✅ (In Progress)
  - Developed comprehensive unit tests for:
    - `CanAddTasks` (7 tests covering 1-2 assets, 1-3 tasks, various `MaxTimesToPerform`)
    - `timesCompletedTask` (6 tests validating occurrence counting)
    - `TimeDeconfliction` (3 tests for simple 1-asset-1-task scenario)
  - Each test suite includes detailed README.md documentation
  - Tests verify input assumptions and output correctness

- **Phase 2: Parallelization Implementation** (Next)
  - Add feature flag: `SchedParameters.EnableParallelScheduling` (default: false)
  - Parallelize `TimeDeconfliction` using `Parallel.ForEach`
  - Parallelize `CheckAllPotentialSchedules`
  - Fix identified issues:
    - Schedule ID race condition → `Interlocked.Increment`
    - Thread-safe collections → `ConcurrentBag`
    - StateHistory deep copy verification

- **Phase 3: Validation & Benchmarking** (Next)
  - Run all existing tests with parallel mode enabled
  - Add determinism tests to compare sequential vs. parallel
  - Benchmark performance on Aeolus scenario
  - Create simple deterministic test program for controlled testing

#### 3.3 Implementation Details

##### 3.3.1 TimeDeconfliction Parallelization
**Before (Sequential):**
```csharp
public static List<SystemSchedule> TimeDeconfliction(
    List<SystemSchedule> systemSchedules, 
    Stack<Stack<Access>> scheduleCombos, 
    double currentTime)
{
    var _potentialSystemSchedules = new List<SystemSchedule>();
    foreach(var oldSystemSchedule in systemSchedules) {
        Scheduler._schedID = 1; // ⚠️ DATA RACE
        foreach (var newAccessTaskStack in scheduleCombos) {
            if (oldSystemSchedule.CanAddTasks(newAccessTaskStack, currentTime)) {
                var CopySchedule = new StateHistory(oldSystemSchedule.AllStates);
                _potentialSystemSchedules.Add(new SystemSchedule(...));
            }
        } 
    }
    return _potentialSystemSchedules;
}
```

**After (Parallel):**
```csharp
public static List<SystemSchedule> TimeDeconfliction(
    List<SystemSchedule> systemSchedules, 
    Stack<Stack<Access>> scheduleCombos, 
    double currentTime)
{
    if (SchedParameters.EnableParallelScheduling) {
        // Parallel path
        var potentialSchedules = new ConcurrentBag<SystemSchedule>();
        Parallel.ForEach(systemSchedules, oldSchedule => {
            foreach (var newAccessStack in scheduleCombos) {
                if (oldSchedule.CanAddTasks(newAccessStack, currentTime)) {
                    var copy = new StateHistory(oldSchedule.AllStates);
                    var newSchedule = new SystemSchedule(copy, newAccessStack, currentTime, oldSchedule);
                    potentialSchedules.Add(newSchedule); // Thread-safe add
                }
            }
        });
        return potentialSchedules.ToList();
    } else {
        // Original sequential path (unchanged)
        var _potentialSystemSchedules = new List<SystemSchedule>();
        foreach(var oldSystemSchedule in systemSchedules) {
            foreach (var newAccessTaskStack in scheduleCombos) {
                if (oldSystemSchedule.CanAddTasks(newAccessTaskStack, currentTime)) {
                    var CopySchedule = new StateHistory(oldSystemSchedule.AllStates);
                    _potentialSystemSchedules.Add(new SystemSchedule(...));
                }
            } 
        }
        return _potentialSystemSchedules;
    }
}
```

**Key Changes:**
- `ConcurrentBag<SystemSchedule>` for thread-safe collection
- `Parallel.ForEach` on outer loop (systemSchedules)
- Schedule ID generation moved to constructor with `Interlocked.Increment`
- Feature flag allows A/B testing

##### 3.3.2 CheckAllPotentialSchedules Parallelization
**Before (Sequential):**
```csharp
public static List<SystemSchedule> CheckAllPotentialSchedules(
    SystemClass system, 
    List<SystemSchedule> potentialSystemSchedules)
{
    int numSched = 0;
    List<SystemSchedule> validSchedules = new List<SystemSchedule>();
    foreach (SystemSchedule schedule in potentialSystemSchedules) {
        if (system.CanPerform(schedule, schedule.AllStates.GetLastEvent())) {
            validSchedules.Add(schedule);
            numSched++;
        }
    }
    return validSchedules;
}
```

**After (Parallel):**
```csharp
public static List<SystemSchedule> CheckAllPotentialSchedules(
    SystemClass system, 
    List<SystemSchedule> potentialSystemSchedules)
{
    if (SchedParameters.EnableParallelScheduling) {
        var validSchedules = new ConcurrentBag<SystemSchedule>();
        Parallel.ForEach(potentialSystemSchedules, schedule => {
            if (system.CanPerform(schedule, schedule.AllStates.GetLastEvent())) {
                validSchedules.Add(schedule);
            }
        });
        return validSchedules.ToList();
    } else {
        // Original sequential path
        List<SystemSchedule> validSchedules = new List<SystemSchedule>();
        foreach (SystemSchedule schedule in potentialSystemSchedules) {
            if (system.CanPerform(schedule, schedule.AllStates.GetLastEvent())) {
                validSchedules.Add(schedule);
            }
        }
        return validSchedules;
    }
}
```

**Challenges:**
- `CanPerform` calls into subsystems - must verify no shared state mutations
- IronPython scripted subsystems - GIL may limit parallelism
- Constraint hierarchy evaluation - potential hidden dependencies

#### 3.4 Bugs Found & Fixes

##### Bug 1: Schedule ID Race Condition
**Problem:** `Scheduler._schedID` was a static variable incremented in the `TimeDeconfliction` loop.
```csharp
Scheduler._schedID = 1; // ⚠️ Multiple threads would overwrite this
```
**Solution:** Moved ID generation to `SystemSchedule` constructor with thread-safe increment:
```csharp
public SystemSchedule(...) {
    _scheduleID = Interlocked.Increment(ref Scheduler._schedID).ToString();
}
```

##### Bug 2: StateHistory Deep Copy (Potential Issue)
**Problem:** StateHistory copy constructor must create truly independent copies.
**Investigation:** [TBD - verify no shared references to Events/Tasks]
**Solution:** [TBD - if needed, implement proper deep copy]

##### Bug 3: timesCompletedTask Counting Logic
**Problem:** Original implementation counted **events** containing a task, not **occurrences** of the task.
- When both assets performed Task1 in one event:
  - Old: returned 1 (one event)
  - Correct: should return 2 (two occurrences)
- This caused `CanAddTasks` to incorrectly allow tasks exceeding `MaxTimesToPerform`

**Solution:** Refactored to count total occurrences:
```csharp
public int timesCompletedTask(Task task) {
    int count = 0;
    foreach (Event evt in Events) {
        foreach (var taskInEvent in evt.Tasks.Values) {
            if (taskInEvent == task)
                count++; // Count each occurrence
        }
    }
    return count;
}
```
**Impact:** This bug was unrelated to parallelization but discovered during unit test development. Highlights value of comprehensive testing before parallelization.

##### Bug 4: [TBD - Additional bugs found during parallel implementation]

---

### Chapter 4: Results

#### 4.1 Test Coverage & Validation
- **Unit Test Summary:**
  - Total tests written: [TBD]
  - CanAddTasks: 7 tests
  - timesCompletedTask: 6 tests
  - TimeDeconfliction: 3 tests (more to add)
  - CropToMaxSchedules: [TBD]
  - Determinism tests: [TBD]
  - Performance benchmarks: [TBD]

- **Test Results:**
  - All sequential baseline tests: ✅ Passing
  - All parallel determinism tests: [TBD]
  - All thread safety tests: [TBD]

#### 4.2 Performance Benchmarking

##### 4.2.1 Test Scenarios
1. **Simple Deterministic Scenario**
   - 1 Asset, 3 Tasks, 24-hour horizon
   - Purpose: Controlled testing, verify correctness
   - Expected speedup: Minimal (small problem size)

2. **Aeolus Benchmark Scenario**
   - [TBD: Describe Aeolus mission parameters]
   - X assets, Y tasks, Z-hour horizon
   - [TBD: Number of schedules evaluated per time step]
   - Purpose: Real-world mission complexity

##### 4.2.2 Hardware Configuration
- **Test System:**
  - Processor: [TBD - e.g., Intel Core i7-12700K, 12 cores (8P+4E), 20 threads]
  - RAM: [TBD - e.g., 32 GB DDR4]
  - OS: [TBD - e.g., Windows 11 / macOS 14]
  - .NET Runtime: [TBD - e.g., .NET 8.0]

##### 4.2.3 Performance Results

**Table 1: Sequential vs. Parallel Performance (Aeolus Scenario)**

| Metric | Sequential | Parallel (2 threads) | Parallel (4 threads) | Parallel (8 threads) |
|--------|------------|----------------------|----------------------|----------------------|
| Total Runtime | [TBD]s | [TBD]s | [TBD]s | [TBD]s |
| TimeDeconfliction | [TBD]s | [TBD]s | [TBD]s | [TBD]s |
| CheckAllPotentialSchedules | [TBD]s | [TBD]s | [TBD]s | [TBD]s |
| Speedup (Overall) | 1.0x | [TBD]x | [TBD]x | [TBD]x |
| Speedup (TimeDeconfliction) | 1.0x | [TBD]x | [TBD]x | [TBD]x |
| Efficiency | 100% | [TBD]% | [TBD]% | [TBD]% |

**Figure 1: Speedup vs. Number of Threads**
[TBD - Graph showing speedup curve]

**Figure 2: Scaling Efficiency**
[TBD - Graph showing efficiency (speedup/threads) vs. threads]

##### 4.2.4 Analysis
- **Where did we get speedup?**
  - TimeDeconfliction: [TBD - expected to see significant gains]
  - CheckAllPotentialSchedules: [TBD - may be limited by GIL if scripted subsystems]
  
- **Why not linear speedup?**
  - Overhead: Thread creation, synchronization
  - Amdahl's Law: Sequential portions (CropToMaxSchedules, sorting, etc.)
  - Memory bandwidth: Multiple threads copying StateHistory objects
  - IronPython GIL: Serializes scripted subsystem evaluation
  
- **Sweet spot:**
  - [TBD - e.g., "4-8 threads provides best speedup with minimal overhead"]

#### 4.3 Unit Testing Framework Value
- **Bugs caught before parallelization:**
  - `timesCompletedTask` counting logic error
  - [TBD - other bugs found]
  
- **Confidence in parallelization:**
  - Determinism tests prove parallel == sequential
  - Thread safety tests prevent data races
  - Regression tests ensure no functionality loss
  
- **Future maintainability:**
  - Comprehensive test suite enables safe future changes
  - README.md documentation explains design decisions
  - Easy to verify correctness after code modifications

---

### Chapter 5: Conclusion

#### 5.1 Summary of Contributions
1. **Parallelized HSF scheduling algorithm**
   - Identified key parallelization opportunities (TimeDeconfliction, CheckAllPotentialSchedules)
   - Implemented thread-safe parallel versions with feature flag
   - Achieved [TBD]x speedup on real-world scenarios

2. **Comprehensive unit testing framework**
   - Established baseline correctness tests
   - Created parallelization-specific validation tests
   - Documented test rationale and design decisions

3. **Bug fixes and improvements**
   - Fixed `timesCompletedTask` counting logic
   - Fixed schedule ID race condition
   - [TBD - other improvements]

#### 5.2 Lessons Learned
- **Test-driven parallelization works:**
  - Unit tests caught subtle bugs before parallelization
  - Determinism tests proved correctness of parallel implementation
  - Feature flag enabled easy A/B testing

- **Embarrassingly parallel != trivially parallel:**
  - Even "independent" operations can have hidden shared state
  - Static variables, global state are parallelization killers
  - Proper isolation (deep copies, thread-local data) is critical

- **Amdahl's Law is real:**
  - Sequential portions limit overall speedup
  - Overhead matters (thread creation, synchronization)
  - Parallelizing hotspots first yields best ROI

#### 5.3 Future Work
1. **Further parallelization:**
   - Parallelize `EvaluateAndSortSchedules`
   - Parallelize across time steps (speculative execution)
   - GPU acceleration for constraint evaluation?

2. **IronPython GIL mitigation:**
   - Replace IronPython with Python.NET (no GIL)?
   - Pre-evaluate scripted subsystems in parallel?
   - Cache scripted subsystem results?

3. **Advanced scheduling algorithms:**
   - Beam search pruning for better scalability
   - Heuristic-guided schedule generation
   - Machine learning for constraint prediction

4. **Distributed scheduling:**
   - Parallelize across machines (not just cores)
   - Cloud-based scheduling for massive scenarios
   - Real-time replanning with distributed HSF

#### 5.4 Final Thoughts
[TBD - Reflective conclusion tying together problem → solution → validation → results]

---

## Development Log

### 2025-11-01
- ✅ Completed `CanAddTasksUnitTest` with 7 comprehensive tests
- ✅ Completed `TimesCompletedTaskUnitTest` with 6 tests
- ✅ Created detailed README.md for both test suites
- ✅ Fixed `timesCompletedTask` counting bug (events → occurrences)
- ✅ Refactored `CanAddTasks` to use `timesCompletedTask` directly
- ✅ Started `TimeDeconflictionUnitTest` with 3 simple tests
- ✅ Created `thesis_notes.md` with comprehensive outline

### [Future entries]
- [ ] Complete `TimeDeconflictionUnitTest` (add multi-asset, multi-task tests)
- [ ] Create `CropToMaxSchedulesUnitTest`
- [ ] Add `SchedParameters.EnableParallelScheduling` feature flag
- [ ] Implement parallel `TimeDeconfliction`
- [ ] Create determinism tests
- [ ] Benchmark Aeolus scenario (sequential baseline)
- [ ] Implement parallel `CheckAllPotentialSchedules`
- [ ] Full benchmark suite (2, 4, 8, 16 threads)
- [ ] Analyze results and write thesis chapters

---

## Key References & Resources

### Codebase Structure
- **Main Scheduling Loop:** `src/HSFScheduler/Scheduler.cs::GenerateSchedules()`
- **TimeDeconfliction:** `src/HSFScheduler/Scheduler.cs::TimeDeconfliction()`
- **CanAddTasks:** `src/HSFScheduler/SystemSchedule.cs::CanAddTasks()`
- **timesCompletedTask:** `src/HSFScheduler/StateHistory.cs::timesCompletedTask()`

### Test Suites
- **CanAddTasks Tests:** `test/HSFSchedulerUnitTest/MethodUnitTests/TimeDeconfliction/CanAddTasks/`
- **TimesCompletedTask Tests:** `test/HSFSchedulerUnitTest/MethodUnitTests/TimeDeconfliction/CanAddTasks/TimesCompletedTask/`
- **TimeDeconfliction Tests:** `test/HSFSchedulerUnitTest/MethodUnitTests/TimeDeconfliction/`

### Benchmark Scenarios
- **Aeolus:** `samples/Aeolus/`

### External References
- [TBD - Papers on parallel scheduling algorithms]
- [TBD - Thread safety best practices]
- [TBD - Amdahl's Law, parallel computing theory]

---

## Notes & Ideas

### Open Questions
1. **IronPython GIL:** How much does it limit `CheckAllPotentialSchedules` parallelism?
   - Hypothesis: Scripted subsystems will serialize, limiting speedup
   - Test: Compare non-scripted vs. scripted benchmark scenarios

2. **Memory overhead:** How much extra memory does parallel version use?
   - Each thread creates schedule copies
   - ConcurrentBag vs. List memory characteristics

3. **Optimal thread count:** What's the sweet spot for Aeolus scenario?
   - Too few threads: underutilize CPU
   - Too many threads: overhead dominates

### Thesis Writing Tips
- Use "we" (inclusive) rather than "I" in methodology/results
- Define all acronyms on first use
- Include code snippets for key algorithms (before/after)
- Use figures/graphs for performance results
- Explain *why* decisions were made, not just *what* was done

---

## Acknowledgments
[TBD - Advisor, lab members, HSF community, etc.]

