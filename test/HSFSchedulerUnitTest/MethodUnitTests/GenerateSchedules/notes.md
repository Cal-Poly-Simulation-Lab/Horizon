# GenerateSchedules Test Suite - Progress Notes

## Completed Today (Nov 9, 2025)

### ScriptedCS Dynamic Compilation - WORKING
**Major Achievement:** Fixed ScriptedCS to dynamically compile C# subsystems at runtime
- Aeolus ScriptedCS now produces identical results to Aeolus_CS ✅
- All 72 unit tests passing ✅

**Key Fixes:**
1. Added `Basic.Reference.Assemblies.Net80` + upgraded CodeAnalysis to 4.9.2
2. Implemented dynamic `using` directive parsing → auto-adds required assemblies
3. Enabled implicit usings in compilation (matches .csproj behavior)
4. Fixed subsystem Type to use class name ("adcs", "power") not "scriptedcs"
5. Fixed `SubsystemFactory.SetDependencies` to use `depSub.GetType()` for dynamic subsystems
6. Added `using System;` to Power.cs (required for dynamic compilation)
7. Updated test subsystems (`AlwaysTrueSubsystem`, `SchedulerSubTest`) to modern constructor pattern `(JObject, Asset)` with `base()` call (aligns with Dr. Mehiel's e6c2742 refactor from Aug 2025)
8. Fixed DSAC_Static_ScriptedCS.json parameter names to match C# code

**Git Reference:** Constructor pattern change in commit `e6c2742` (Aug 5, 2025) by Dr. Mehiel

---

## TODO - Tomorrow

### 1. **Aeolus Comprehensive State Validation Suite** (CRITICAL FOR THESIS)
**Goal:** Use Aeolus as the benchmark for validating refactoring → parallelization doesn't introduce bugs

**⚠️ DECISION NEEDED: Full Aeolus (300 tasks) vs Scaled-Down Aeolus (10-30 tasks)?**
- **Trade-off:** Logging ALL states for ALL schedules across ALL iterations with 300 tasks = MASSIVE data volume
- **Question:** Does reduced task count (10-30 tasks) still expose parallelization edge cases?
- **Considerations:**
  - **Data race conditions:** May only manifest with high schedule counts (1000s of schedules)
  - **Memory contention:** Large-scale scenarios stress thread-safe collections differently
  - **Combinatorial edge cases:** 300 tasks → 10^N+ schedules may hit race conditions that 10 tasks → 100 schedules won't
  - **Thesis credibility:** "Validated with 10 tasks" vs "Validated with 300 tasks" - which is more convincing?
- **Hybrid Approach?**
  - Full state validation on scaled-down Aeolus (10-30 tasks, manageable logging)
  - Schedule-count-only validation on full Aeolus (300 tasks, minimal logging)
  - Gives both depth (state validation) AND scale (realistic load)
- [ ] **DISCUSS:** Is full state logging worth it for 300 tasks, or scale down for tractability?

**Phase A: Observation Mode - Capture Ground Truth**
- [ ] Add `CaptureScenario_Aeolus()` to ObservationDataCapture.cs
  - Input: DSAC_Static_Mod.json (or ScriptedCS), AeolusTasks.json, AeolusSimulationInput.json
  - 2 assets, **[DECIDE: 10-30 tasks OR full 300 tasks]**, real subsystems (ADCS, EOSensor, SSDR, Comm, Power)
  - 3 iterations (0s, 30s, 60s)
- [ ] **Expand data capture to include ALL subsystem states** (not just schedule counts/values/IDs)
  - After each iteration: capture `SystemState` for each schedule
  - Log all state variables: DOD, DataBufferFillRatio, numPixels, etc.
  - Log subsystem evaluations: which subsystems passed/failed CanPerform
  - **Stretch Goal:** Log intermediate states DURING iteration (after TimeDeconfliction, after CheckAll, after Evaluate, after Merge)
- [ ] Output format: JSON with full state snapshots + TXT with human-readable summary
- [ ] Run observation, save to ExpectedResults/Aeolus_FullState.json

**Phase B: Assertion Mode - Validate Against Ground Truth**
- [ ] Create `GenerateSchedules_Aeolus_FullStateValidation` test
  - Load expected state data from observation run
  - For each iteration:
    - Assert schedule count/values/IDs (existing pattern)
    - **NEW:** Assert state variables match for each schedule
    - **NEW:** Assert subsystem evaluation results match
  - Final iteration: verify all states match expected
- [ ] **This becomes the refactoring validation test:**
  - Run with current code → passes (by definition, matches observation)
  - Refactor for thread safety → run test → must still pass (proves no bugs introduced)
  - Add parallelization → run test → must still pass (proves parallel == sequential)

**Why This Matters for Thesis:**
- Aeolus is realistic scenario (not toy test data)
- Validates ENTIRE scheduler pipeline, not just schedule counts
- Proves refactoring/parallelization preserves correctness at STATE level
- Can cite: "Validated 300-task, 2-asset scenario with full state verification across all iterations"

### 2. Add Simple Aeolus Test (Schedule-Level Only)
- [ ] Add test case using Aeolus inputs (schedule counts/values/IDs only, not full states)
- [ ] Verify against observed data
- [ ] Confirm ScriptedCS Aeolus works in test framework

### 3. Add Arbitrary/Stress Integration Test
- [ ] Design scenario: multiple assets, many tasks, mixed MaxTimes
- [ ] Test edge cases (cropping, large schedule counts)
- [ ] Verify correctness via observation→assertion pattern

### 4. Unit Test Checker Methods (Foundation for Parallelization)
- [ ] `Checker.CheckSchedule()` - basic I/O tests
- [ ] `checkSub()` - subsystem evaluation
- [ ] `checkSubs()` - batch subsystem checks
- [ ] `CheckConstraints()` - constraint validation
- [ ] Thread safety prep (identify shared state issues for parallelization)

### 5. Unit Test Sorter/Evaluator Methods
- [ ] `EvaluateAndSortCanPerformSchedules()` 
- [ ] Verify correct sorting by value
- [ ] Test with mixed values, ties, empty schedules

---

**Current Status:** Phase 1 baseline testing foundation complete. ScriptedCS working. Ready to expand integration test coverage.

