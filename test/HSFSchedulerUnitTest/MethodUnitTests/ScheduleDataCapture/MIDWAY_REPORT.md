# Midway Report: Deterministic Sorting and Hash Tracking
**Commit:** `1685d98`  
**Date:** November 18, 2025

## Summary
Implemented deterministic schedule sorting with blockchain-style hash tracking for repeatable schedule ordering across program and test runs. Added combined schedule-state hash tracking system.

## File Changes

### Core Sorting & Hashing
- **`src/HSFScheduler/Scheduler.cs`**
  - Added `SortSchedulesDeterministic()` static method replacing unstable `List<T>.Sort()`
  - Sorts by `ScheduleValue` (descending), then `ComputeScheduleHash()` (ascending) for tie-breaking
  - Updated `CropSchedules()`, `EvaluateAndSortCanPerformSchedules()`, and `Program.EvaluateSchedules()` to use deterministic sort
  - Integrated hash history recording via `SystemScheduleInfo.RecordSortHashHistory()`
  - Modified `CheckAllPotentialSchedules()` to use blockchain hash (`ScheduleHash` property) instead of full hash for state hash key matching
  - Added fallback to `ComputeScheduleHash()` if blockchain hash not initialized

- **`src/HSFScheduler/SystemSchedule.cs`**
  - Moved `ComputeScheduleHash()` from `Program.cs` to `SystemSchedule.cs` as static method
  - Hash includes: `ScheduleValue:F2` + all events (chronological) + event times + asset→task pairs (preserving dictionary order)
  - All double times truncated to `:F2` to avoid precision errors
  - Added `ComputeIncrementalHash()` for blockchain-style incremental hashing
  - Preserves object iteration order (no sorting) to allow different hashes for reflectively symmetric schedules
  - Constructor: Copies hash history from old schedule (if exists), then calls `UpdateHashAfterEvent()` with new event

- **`src/HSFScheduler/SystemScheduleInfo.cs`**
  - Added `ScheduleHashHistory: Dictionary<int, Stack<string>>` tracking per-iteration hash evolution
  - `ScheduleHash` property returns top of last iteration's stack (final blockchain hash)
  - Two hash points per iteration: after event added (bottom), after value evaluated (top)
  - Static methods: `InitializeHashHistoryFile()`, `RecordSortHashHistory()` for file tracking
  - Added `RecordCombinedHashHistory()` for combined schedule-state hash tracking
  - Added `ComputeCombinedHash()` for schedule+state hash combination
  - Iteration numbering: deterministic 0,1,2... based on history count (not `Scheduler.SchedulerStep`)
  - Removed verbose console output for successful hash lookups; only prints errors

- **`src/HSFScheduler/StateHistory.cs`**
  - Added `StateHashHistory: Dictionary<(int Step, string ScheduleHash), string>` tracking state hashes by step and schedule hash key
  - `StateHash` property returns most recent entry (max step, then max schedule hash)
  - Added `ComputeStateHistoryHash()` for blockchain-style state hashing
  - Added `UpdateStateHashAfterCheck()` stores state hash with key `(step, scheduleHash)` after CheckSchedule
  - Added `UpdateStateHashAfterEval()` stores state hash with key `(step, scheduleHash)` after evaluation (adds "EVALSPOOF" to ensure different hash)
  - Added `RecordStateHashHistory()` writes state hashes to file sorted by schedule hash
  - Added `InitializeStateHashHistoryFile()` for file tracking

- **`src/Horizon/Program.cs`**
  - Updated `EvaluateSchedules()` to use `SortSchedulesDeterministic()`
  - Hash history file initialization in `InitOutput()` and `InitTestOutput()`
  - Hash set generation moved to after `GenerateSchedules()` but before `EvaluateSchedules()`
  - Added `SaveScheduleHashBlockchainSummary()` to include `StateHash` and `CombinedHash` columns
  - Added `ComputeCombinedHash()` helper method

### Test Infrastructure
- **`test/.../ScheduleDataCapture/ScheduleDataCapture.cs`**
  - Added hash history file initialization in `SetOutputDirectory()`
  - Output directory structure: `Run_<scenario>_<tasks>/ProgramOutput/`

## Three Hash Systems: Schedule Hash, State Hash, Combined Hash

### 1. Schedule Hash (Blockchain-Style)
**Structure:** `ScheduleHashHistory[iteration] = Stack<string>` where iteration = 0,1,2,... (deterministic)
- **First entry (bottom):** Hash after event added to schedule
  - Formula: `SHA256(previousHash || eventHash || value{value:F2})[:16]`
  - Previous hash = top of previous iteration's stack (or "" if first)
  - Event hash = all asset-task pairs + event/task times (preserving order)
- **Second entry (top):** Hash after value evaluated
  - Formula: `SHA256(previousHash || null || value{value:F2})[:16]`
  - Previous hash = first entry in current iteration (hash-after-event)
- **`ScheduleHash` property:** Returns top of last iteration's stack (hash-after-value)

**Blockchain Proof:** Each hash incorporates previous hash:
- Iteration 0: `H0 = SHA256("" || event0 || value0)[:16]`
- Iteration 0 (value): `H0v = SHA256(H0 || null || value0)[:16]`
- Iteration 1: `H1 = SHA256(H0v || event1 || value1)[:16]` ← uses previous iteration's hash
- Iteration 1 (value): `H1v = SHA256(H1 || null || value1)[:16]` ← uses same iteration's first hash

### 2. State Hash (Blockchain-Style)
**Structure:** `StateHashHistory[(step, scheduleHash)] = string`
- **After Check:** Hash stored with key `(SchedulerStep, scheduleHash)` where `scheduleHash` = hash-after-event
  - Formula: `SHA256(previousStateHash || time{time:F2} || checkResult{result} || stateData)[:16]`
  - Previous hash = most recent entry for this scheduleHash (if exists), else ""
  - State data = all Idata, Ddata, Bdata, Mdata, Qdata, Vdata at currentTime (sorted by key, :F2 truncated)
- **After Eval:** Hash stored with key `(SchedulerStep, scheduleHash)` where `scheduleHash` = hash-after-value
  - Formula: `SHA256(previousStateHash || time{time:F2} || checkResult{true} || stateData || EVALSPOOF)[:16]`
  - Previous hash = `StateHash` property (most recent state hash for this schedule)
  - "EVALSPOOF" ensures different hash even if state data identical

**Blockchain Proof:** Each state hash incorporates previous state hash:
- Check (step 0): `SH0 = SHA256("" || time || result || state0)[:16]`
- Eval (step 0): `SH0e = SHA256(SH0 || time || true || state0 || EVALSPOOF)[:16]` ← uses Check hash
- Check (step 1): `SH1 = SHA256(previousSH1 || time || result || state1)[:16]` ← uses previous step's hash for same scheduleHash

**Key Dependencies:** State hash key `(step, scheduleHash)` requires schedule hash to exist:
- At Check: Uses `ScheduleHash` property which = hash-after-event (first in stack)
- At Eval: Uses `ScheduleHash` property which = hash-after-value (second in stack, after UpdateHashAfterValueEvaluation called)

### 3. Combined Hash (Schedule + State)
**Structure:** `SHA256(scheduleHash || stateHash)[:16]`
- Combines schedule blockchain hash with corresponding state hash
- Lookup: Uses `(step, scheduleHash)` key from `StateHashHistory` to find matching state hash
- Stored in `FullScheduleStateHashHistory.txt` with format `[<iteration>A/B] <hashes>`
  - A = Check context (hash-after-event + state-after-check)
  - B = EvalAll context (hash-after-value + state-after-eval)

## Exact Temporal Flow in Main Loop

### Per-Iteration Flow (`GenerateSchedules()` main loop)

**T0: Start of Iteration**
- `SchedulerStep += 1`
- `CurrentTime = currentTime`

**T1: CropToMaxSchedules**
- Sorts schedules deterministically (by value, then content hash)
- Records sort hash history (context: "CropToMax")
- No hash updates (values already set from previous iteration)

**T2: TimeDeconfliction**
- Creates new `SystemSchedule` objects from old schedules + new accesses
- **NEW SCHEDULE CREATION:** `SystemSchedule(oldStates, newAccessStack, currentTime, oldSchedule)`
  - Copies hash history from old schedule (if exists)
  - Calls `UpdateHashAfterEvent(newEvent, ScheduleValue=0)`
  - Creates new iteration in `ScheduleHashHistory` (0,1,2,... based on count)
  - Computes: `newHash = SHA256(previousIterationHash || eventHash || value0)[:16]`
  - Pushes to stack: `ScheduleHashHistory[iteration].Push(newHash)`
  - At this point: `ScheduleHash` property = `newHash` (hash-after-event, first in stack)

**T3: CheckAllPotentialSchedules**
- For each `potentialSchedule`:
  - Gets `scheduleHash = potentialSchedule.ScheduleInfo.ScheduleHash` (hash-after-event)
  - Fallback: If empty, uses `ComputeScheduleHash(potentialSchedule)` (full hash)
  - Calls `Checker.CheckSchedule()` → updates `SystemState` inside schedule
  - Calls `StateHistory.UpdateStateHashAfterCheck(stateHistory, currentTime, checkResult, scheduleHash)`
    - Looks up previous state hash for this `scheduleHash` key (if exists)
    - Computes: `newStateHash = SHA256(previousStateHash || time || result || stateData)[:16]`
    - Stores: `StateHashHistory[(SchedulerStep, scheduleHash)] = newStateHash`
  - If check passes, adds to `_canPerformList`
- After all checks:
  - Calls `StateHistory.RecordStateHashHistory(potentialSystemSchedules, "Check", currentTime)` → writes to file
  - Calls `SystemScheduleInfo.RecordCombinedHashHistory(potentialSystemSchedules, "Check", SchedulerStep)`
    - For each schedule: Gets `scheduleHash` (hash-after-event), looks up state hash at `(step, scheduleHash)`
    - Computes combined hash, writes to `FullScheduleStateHashHistory.txt` as `[<step>A]`

**T4: EvaluateAndSortCanPerformSchedules**
- For each schedule in `_canPerformList`:
  - Evaluates: `systemSchedule.ScheduleValue = scheduleEvaluator.Evaluate(schedule)`
  - Calls `SystemScheduleInfo.UpdateHashAfterValueEvaluation(schedule, ScheduleValue)`
    - Gets current iteration (last in `ScheduleHashHistory`)
    - Gets previous hash = top of current iteration's stack (hash-after-event)
    - Computes: `newHash = SHA256(previousHash || null || value{value:F2})[:16]`
    - Pushes to stack: `ScheduleHashHistory[currentIteration].Push(newHash)`
    - At this point: `ScheduleHash` property = `newHash` (hash-after-value, second in stack)
- Sorts schedules deterministically (context: "EvalSort")
- For each schedule:
  - Gets `scheduleHash = systemSchedule.ScheduleInfo.ScheduleHash` (hash-after-value)
  - Calls `StateHistory.UpdateStateHashAfterEval(stateHistory, currentTime, true, scheduleHash)`
    - Gets previous hash = `StateHash` property (most recent state hash for this schedule)
    - Computes: `newStateHash = SHA256(previousStateHash || time || true || stateData || EVALSPOOF)[:16]`
    - Stores: `StateHashHistory[(SchedulerStep, scheduleHash)] = newStateHash`
- After all evals:
  - Calls `StateHistory.RecordStateHashHistory(_canPerformList, "EvalAll", currentTime)`
  - Calls `SystemScheduleInfo.RecordCombinedHashHistory(_canPerformList, "EvalAll", SchedulerStep)`
    - For each schedule: Gets `scheduleHash` (hash-after-value), looks up state hash at `(step, scheduleHash)`
    - Computes combined hash, writes to `FullScheduleStateHashHistory.txt` as `[<step>B]`

**T5: MergeAndClearSystemSchedules**
- Merges surviving schedules into main list
- Schedules carry forward their `ScheduleHashHistory` and `StateHashHistory`

**T6: Final Crop (after loop)**
- Same as T1, records sort hash history (context: "CropToMax")

## Hash Interplay & Dependencies

### Schedule Hash → State Hash Dependency
**At Check (T3):**
- State hash key = `(SchedulerStep, scheduleHash)` where `scheduleHash` = hash-after-event
- `scheduleHash` computed at T2 when event added to schedule
- State hash lookup uses this `scheduleHash` to find previous state hash for blockchain chaining

**At Eval (T4):**
- State hash key = `(SchedulerStep, scheduleHash)` where `scheduleHash` = hash-after-value
- `scheduleHash` computed at T4 after value evaluation (pushes second hash to stack)
- State hash lookup uses this new `scheduleHash` (different from Check key)
- Creates separate state hash entry for post-evaluation state

### Combined Hash Dependencies
**At Check:**
- Schedule hash = hash-after-event (first in current iteration's stack)
- State hash = looked up using `(step, hash-after-event)` key
- Combined = `SHA256(hash-after-event || state-after-check)[:16]`

**At EvalAll:**
- Schedule hash = hash-after-value (second in current iteration's stack, after value eval)
- State hash = looked up using `(step, hash-after-value)` key
- Combined = `SHA256(hash-after-value || state-after-eval)[:16]`

### Critical Timing Requirements
1. **Schedule hash must exist before state hash:** State hash key requires `scheduleHash`, so schedule hash must be computed first (at T2)
2. **Hash updates must be sequential:** 
   - T2: Event added → schedule hash updated (hash-after-event)
   - T3: CheckSchedule → state hash stored with hash-after-event key
   - T4: Value evaluated → schedule hash updated (hash-after-value) 
   - T4: After eval → state hash stored with hash-after-value key
3. **Lookup matching:** `RecordCombinedHashHistory` must use same hash source as what was used for state hash key storage
   - Check: Uses `ScheduleHash` property (hash-after-event)
   - EvalAll: Uses `ScheduleHash` property (hash-after-value, after UpdateHashAfterValueEvaluation called)

## Blockchain Verification

### Schedule Hash Blockchain Chain
```
Iteration 0:
  Stack[0] = [H0v, H0]  (top to bottom)
  H0 = SHA256("" || event0 || value0)
  H0v = SHA256(H0 || null || value0)  ← chains from H0

Iteration 1:
  Stack[1] = [H1v, H1]
  H1 = SHA256(H0v || event1 || value1)  ← chains from previous iteration
  H1v = SHA256(H1 || null || value1)  ← chains from same iteration's first hash

ScheduleHash property = H1v (top of last stack)
```

### State Hash Blockchain Chain
```
Step 0, ScheduleHash=H0 (hash-after-event):
  StateHashHistory[(0, H0)] = SH0 = SHA256("" || time || result || state0)

Step 0, ScheduleHash=H0v (hash-after-value):
  StateHashHistory[(0, H0v)] = SH0e = SHA256(SH0 || time || true || state0 || EVALSPOOF)  ← chains from SH0

Step 1, ScheduleHash=H1 (hash-after-event):
  Previous = most recent for H1 (if exists) or ""
  StateHashHistory[(1, H1)] = SH1 = SHA256(previous || time || result || state1)  ← chains from previous

Step 1, ScheduleHash=H1v (hash-after-value):
  Previous = StateHash property (most recent for this schedule) = SH1
  StateHashHistory[(1, H1v)] = SH1e = SHA256(SH1 || time || true || state1 || EVALSPOOF)  ← chains from SH1
```

### Combined Hash Generation
```
Check context (step 0):
  ScheduleHash = H0 (hash-after-event)
  StateHash = StateHashHistory[(0, H0)] = SH0
  Combined = SHA256(H0 || SH0)[:16]

EvalAll context (step 0):
  ScheduleHash = H0v (hash-after-value, after eval update)
  StateHash = StateHashHistory[(0, H0v)] = SH0e
  Combined = SHA256(H0v || SH0e)[:16]
```

## Output Files

### `FullScheduleHashHistory.txt` (HashData/)
- Format: `[<iteration>: <context>] <hashes space delimited>`
- Context: `CropToMax` (iterations 1-5), `EvalSort` (iterations 0-5)
- Written after each sort operation
- Contains schedule hashes (top of stack for each schedule)

### `FullStateHistoryHash.txt` (HashData/)
- Format: `[<iteration>: <context>] <hashes space delimited>`
- Context: `Check` (step 0-4), `EvalAll` (step 0-4)
- Written after CheckSchedule and after evaluation
- Contains state hashes sorted by corresponding schedule hash

### `FullScheduleStateHashHistory.txt` (HashData/)
- Format: `[<iteration>A/B] <hashes space delimited>`
- A = Check context (hash-after-event + state-after-check)
- B = EvalAll context (hash-after-value + state-after-eval)
- Written after CheckSchedule and after evaluation
- Contains combined hashes sorted by schedule hash

### `scheduleHashBlockchainSummary.txt` (HashData/)
- Final summary with columns: ScheduleID, Value, Events, ScheduleHash, StateHash, CombinedHash
- StateHash and CombinedHash looked up from `StateHashHistory` using final `ScheduleHash` and last step

## Current Status & Flags

**✅ COMPLETED:**
- Blockchain-style schedule hash tracking (per-iteration stacks)
- Blockchain-style state hash tracking (by step + schedule hash key)
- Combined hash tracking (schedule + state)
- Deterministic hash matching between Check and EvalAll contexts
- File output for all three hash types
- Error-only console output (no spam)
- Fallback for empty blockchain hashes

**🔧 FIXED:**
- Hash key mismatch: `CheckAllPotentialSchedules` now uses `ScheduleHash` property (blockchain hash) instead of `ComputeScheduleHash()` (full hash) for state hash key
- Console spam: Removed verbose "Combined Hash Check" messages for successful lookups
- Empty hash handling: Added fallback in `RecordCombinedHashHistory` to match `CheckAllPotentialSchedules`

**🎯 VERIFIED:**
- Schedule hash updates correctly: hash-after-event at T2, hash-after-value at T4
- State hash stores correctly: Check hash at T3 with hash-after-event key, Eval hash at T4 with hash-after-value key
- Combined hash lookup works: finds correct state hash for each schedule hash at correct step
- All three hash systems chain correctly (blockchain-style with previous hash incorporation)
- Output files generated correctly for all contexts (Check and EvalAll)

**📝 NOTES:**
- Iteration numbering (0,1,2,...) is deterministic based on `ScheduleHashHistory.Count`, not `Scheduler.SchedulerStep`
- State hash uses same `Scheduler.SchedulerStep` for key (0,1,2,3,4 for 5-step simulation)
- Schedule hash iteration can differ from scheduler step (e.g., iteration 0 might be at step 1 if schedule created mid-simulation)
- Combined hash context "A" = Check, "B" = EvalAll (matches state hash context names)

## Determinism Verification & Parallel Testing

### Current Determinism Verification (Aeolus/Program Runs)

**Problem Solved:** ScheduleIDs are non-deterministic (assignment order dependent), making it impossible to verify that program runs and test runs produce identical schedule sets.

**Solution:** Blockchain-style hashing provides content-based deterministic identity:
- **Schedule Hash:** Same schedule content → same hash, regardless of execution order
- **State Hash:** Same state at same step for same schedule → same hash
- **Combined Hash:** Verifies that both schedule structure AND system state evolved identically

**Verification Process:**
1. Run program: Save final `scheduleHashBlockchainSummary.txt` with all hashes
2. Run test: Capture same output with identical hashes
3. Compare: Hash sets should be identical (same final hashes = same schedules)
4. Verify: If hashes match, schedules are functionally identical even if IDs differ

**Current Status:**
- ✅ Verified: Test runs (300 tasks) produce identical hashes as program runs
- ✅ Verified: `FullScheduleHashHistory.txt` matches between runs
- ✅ Verified: `FullStateHistoryHash.txt` matches between runs
- ✅ Verified: `FullScheduleStateHashHistory.txt` matches between runs

### Code Refactoring Verification

**Use Case:** When refactoring scheduler logic (e.g., changing data structures, optimizing algorithms), verify behavior unchanged.

**Verification Strategy:**
1. **Baseline:** Run refactor-before code, save all hash files
2. **Refactor:** Implement changes (e.g., parallelization, optimization)
3. **Compare:** Run refactor-after code, compare hash files
4. **Validate:** 
   - Identical final hashes → behavior unchanged
   - Different hashes → investigate: bug or intentional change?
   - Partial match → identify which schedules changed and why

**Blockchain Advantage:**
- Incremental hashes verify stage-by-stage evolution (Check vs EvalAll)
- State hashes verify system state evolution matches
- Combined hashes verify schedule-state synchronization
- If refactor breaks determinism, hashes fail immediately (no silent bugs)

**Example Refactoring Scenarios:**
- **Data structure change:** If hash computation uses same data (event times, tasks), hashes remain identical
- **Algorithm optimization:** If result identical, hashes identical (proves correctness)
- **Parallel implementation:** Schedule processing order changes, but content hashes remain deterministic

### Parallel Verification

**Challenge:** Parallel runs process schedules in different orders → different ScheduleIDs, but same content.

**Solution:** Content-based hashing provides order-independent identity:
- **Schedule Hash:** Computed from schedule content (events, times, tasks), not processing order
- **Hash Set Comparison:** Collect all final hashes into a set → compare sets (order-independent)
- **Deterministic Matching:** Same content → same hash → can match across parallel runs

**Parallel Implementation Strategy:**
1. **Hash Computation:** Must be deterministic (no thread-local state in hash computation)
2. **Hash Collection:** Collect all hashes into set (order-independent) after parallel processing
3. **Verification:** Compare hash sets (not ordered lists) to verify identical results

**Current Code Status:**
- ✅ Hash computation is deterministic (no race conditions in `ComputeScheduleHash`)
- ✅ Hash history tracking is thread-safe (locks in `RecordSortHashHistory`, `RecordCombinedHashHistory`)
- ⚠️ **Potential Issue:** Schedule hash initialization timing in parallel context needs verification

**Parallel Execution Flow:**
```
Thread 1: Process schedule A → hash_A
Thread 2: Process schedule B → hash_B
Thread 3: Process schedule C → hash_C

Final hash set: {hash_A, hash_B, hash_C}  (order-independent)
Compare with baseline: {hash_A, hash_B, hash_C}  ← should match
```

**State Hash in Parallel Context:**
- **Critical:** State hash key `(step, scheduleHash)` requires exact hash match
- **Solution:** Schedule hash must be computed BEFORE state hash lookup
- **Verification:** Same schedule → same schedule hash → same state hash key → same state hash
- **Parallel Safe:** As long as hash computation deterministic, parallel processing order irrelevant

**Combined Hash in Parallel Context:**
- Schedule hash and state hash both deterministic → combined hash deterministic
- Lookup using `(step, scheduleHash)` key works identically in parallel
- Final combined hash set comparison verifies parallel run correctness

### Known Issues & Limitations

**⚠️ Issue 1: Hash Initialization Timing**
- **Problem:** If `ScheduleHash` property returns empty (hash not initialized), fallback to `ComputeScheduleHash()` used
- **Impact:** Could cause hash mismatch between runs if one run uses blockchain hash and other uses fallback
- **Mitigation:** Ensure all schedules have hash initialized before CheckSchedule (currently done in constructor)
- **Status:** Fallback added for safety, but should not trigger in normal operation

**⚠️ Issue 2: ScheduleID Non-Determinism**
- **Problem:** ScheduleIDs still non-deterministic (assignment order dependent)
- **Impact:** Cannot use ScheduleIDs for verification or matching
- **Solution:** Use content hashes for all verification (current approach)
- **Status:** Acceptable - hashes provide deterministic identity

**⚠️ Issue 3: Hash Collision Risk**
- **Problem:** 16-character hex hashes have 2^64 possible values, but many schedules could exist
- **Impact:** Low probability but theoretically possible hash collision
- **Mitigation:** 
  - Full hash collision would require identical schedule content (acceptable)
  - Partial collision would require investigation (unlikely at 16 chars)
- **Future:** Could extend to 32 chars if collision concerns arise

**⚠️ Issue 4: State Hash Key Dependency**
- **Problem:** State hash lookup depends on exact `(step, scheduleHash)` key match
- **Impact:** If schedule hash differs between runs, state hash lookup fails
- **Mitigation:** 
  - Same schedule → same schedule hash (blockchain deterministic)
  - Same step → same scheduler step (deterministic)
  - Combined hashes verify correct matching
- **Status:** Verified working in current tests

**⚠️ Issue 5: Performance Overhead**
- **Problem:** Hash computation adds overhead to each schedule processing
- **Impact:** Slows down scheduler execution
- **Mitigation:** 
  - Hash computation only when `EnableHashTracking = true`
  - Can disable for production runs if performance critical
- **Status:** Acceptable overhead for verification/testing purposes

### Future Work

**🔧 Planned:**
- Verify hash initialization in all schedule creation paths (no fallback needed)
- Add hash collision detection/warning if duplicates found
- Performance profiling to quantify hash computation overhead
- Parallel execution testing with hash verification

**🧪 Testing:**
- Compare program vs test runs for all task decks (3, 30, 300 tasks) ✅
- Verify hash matching across multiple runs of same input
- Test parallel scheduler implementation with hash verification
- Validate refactored code produces identical hashes

**📊 Analysis:**
- Analyze hash distribution (ensure good spread, no clustering)
- Profile hash computation time (identify bottlenecks)
- Measure file I/O overhead for hash history tracking

## Thesis Justification Tips

### Key Points for Academic Justification

**1. Reproducibility & Scientific Rigor**
- **Claim:** Simulation systems must produce reproducible results for scientific validity
- **Evidence:** Blockchain hashing provides content-based verification independent of execution order
- **Justification:** Deterministic hashing enables verification that algorithm changes preserve behavior
- **Academic Angle:** Reproducibility is fundamental to computational science; this provides quantitative verification

**2. Verification of Correctness**
- **Claim:** Content-based hashing enables automatic verification of parallel implementation correctness
- **Evidence:** Same schedule content produces same hash regardless of processing order
- **Justification:** Parallel implementations can be verified correct by comparing hash sets, not just final outputs
- **Academic Angle:** Formal verification methods for parallel systems; hash comparison provides practical verification

**3. Incremental Verification (Blockchain Approach)**
- **Claim:** Incremental hashing enables stage-by-stage verification of system evolution
- **Evidence:** Separate hashes for Check vs EvalAll contexts show state evolution at each stage
- **Justification:** Identifies exactly where behavior diverges if refactoring introduces bugs
- **Academic Angle:** System state tracking and verification; blockchain provides audit trail

**4. State-Schedule Synchronization Verification**
- **Claim:** Combined hashes verify that schedule structure and system state evolve in sync
- **Evidence:** Combined hash requires matching schedule hash and state hash at same step
- **Justification:** Catches bugs where schedule evolution and state evolution become desynchronized
- **Academic Angle:** Consistency verification in stateful systems; combined hashes prove synchronization

**5. Parallel Testing & Scalability**
- **Claim:** Deterministic hashing enables testing parallel implementations against sequential baseline
- **Evidence:** Hash set comparison verifies parallel runs produce identical results despite different processing order
- **Justification:** Enables confidence in parallel correctness without manual result inspection
- **Academic Angle:** Parallel algorithm verification; hash comparison provides scalable verification method

### Academic Paper Structure Suggestions

**Abstract/Introduction:**
- Emphasize reproducibility challenge in simulation systems
- Highlight non-deterministic execution (parallelization, order-dependent operations)
- Introduce content-based hashing as solution

**Related Work:**
- Cite work on deterministic testing, hash-based verification
- Reference blockchain-style verification in distributed systems
- Discuss reproducibility in scientific computing

**Methodology:**
- Explain three-layer hashing (schedule, state, combined)
- Describe blockchain-style incremental hashing
- Detail temporal flow and hash dependencies

**Results:**
- Present hash matching verification between runs
- Show hash distribution analysis
- Demonstrate parallel verification feasibility

**Discussion:**
- Address limitations (hash collisions, performance)
- Discuss scalability to larger simulations
- Propose future work (collision detection, optimization)

**Conclusion:**
- Reiterate reproducibility achievement
- Emphasize practical verification capability
- Highlight contribution to simulation system reliability

### Key Metrics to Emphasize

1. **Determinism:** Same input → same hash set (proven empirically)
2. **Completeness:** All schedules tracked (no missing hashes)
3. **Accuracy:** Hash collision probability (2^64 space, low risk)
4. **Performance:** Overhead measurement (if acceptable, emphasize minimal impact)
5. **Scalability:** Works for small (3) to large (300) task sets

### Potential Thesis Sections

**"Deterministic Schedule Verification in Mission Planning Systems"**
- Problem: Non-deterministic schedule IDs prevent verification
- Solution: Content-based blockchain-style hashing
- Results: Verified identical behavior across runs
- Impact: Enables confident refactoring and parallelization

**"Reproducibility Verification for Parallel Scheduling Algorithms"**
- Problem: Parallel runs cannot be directly compared due to order differences
- Solution: Order-independent hash set comparison
- Results: Parallel correctness verified against sequential baseline
- Impact: Enables parallel implementation with confidence

**"Incremental State Verification in Time-Series Schedulers"**
- Problem: Need to verify system state evolution matches schedule evolution
- Solution: Combined schedule-state hashing with blockchain chaining
- Results: Stage-by-stage verification (Check vs EvalAll)
- Impact: Catches synchronization bugs early

## Visualization Prompt for AI Tools

### Prompt for Visualization-Specialized AI

**Target Tools:** GPT-4 with DALL-E/Code Interpreter, Claude with diagram generation, specialized diagram AI (Mermaid AI, DiagramGPT, or similar)

**Prompt:**

```
Create a comprehensive visualization explaining a blockchain-style hashing verification system for a scheduler simulation. The system has THREE interconnected hash types:

1. **Schedule Hash** (Blockchain-style, per-iteration stacks)
   - Each iteration has a Stack with 2 entries: [hash-after-value, hash-after-event] (top to bottom)
   - Iteration 0: H0 = SHA256("" || event0 || value0), H0v = SHA256(H0 || null || value0)
   - Iteration 1: H1 = SHA256(H0v || event1 || value1), H1v = SHA256(H1 || null || value1)
   - Each hash chains from previous (blockchain pattern)

2. **State Hash** (Blockchain-style, keyed by step + schedule hash)
   - Stored in Dictionary: StateHashHistory[(step, scheduleHash)] = stateHash
   - After Check: SH0 = SHA256("" || time || result || state0) with key (0, hash-after-event)
   - After Eval: SH0e = SHA256(SH0 || time || true || state0 || EVALSPOOF) with key (0, hash-after-value)
   - Each state hash chains from previous state hash for same schedule

3. **Combined Hash** (Schedule + State)
   - Combined = SHA256(scheduleHash || stateHash)
   - Lookup: Find stateHash using (step, scheduleHash) key
   - Check context: Uses hash-after-event + state-after-check
   - EvalAll context: Uses hash-after-value + state-after-eval

**Temporal Flow (one scheduler iteration):**
T0: Start iteration, SchedulerStep++
T1: CropToMaxSchedules (sort, record hashes)
T2: TimeDeconfliction (create new schedules, UpdateHashAfterEvent → hash-after-event)
T3: CheckAllPotentialSchedules (CheckSchedule, UpdateStateHashAfterCheck with hash-after-event key)
T4: EvaluateAndSortCanPerformSchedules (Evaluate, UpdateHashAfterValueEvaluation → hash-after-value, UpdateStateHashAfterEval with hash-after-value key)
T5: MergeAndClearSystemSchedules
T6: Final crop

**Requirements:**
- Show blockchain chaining clearly (previous hash → new hash arrows)
- Show THREE hash systems side-by-side with their relationships
- Show temporal flow T0-T6 with hash updates at each stage
- Show how schedule hash key links to state hash (dotted line/dependency)
- Use distinct colors: Schedule hash (blue), State hash (green), Combined hash (purple)
- Include example hash values (16-char hex) to show chain progression
- Show Check vs EvalAll contexts with different schedule hash values (hash-after-event vs hash-after-value)
- Emphasize verification value: Same content → same hash, regardless of execution order

**Format:** Generate as Mermaid flowchart, D3.js interactive diagram, or high-quality diagram export (PNG/SVG) suitable for academic paper.
```

### Visualization Tool Suggestions

**1. Mermaid AI / DiagramGPT**
- **Best for:** Flowcharts, sequence diagrams, flowchart-style blockchain visualization
- **Prompt style:** "Create a Mermaid flowchart showing..."
- **Output:** Direct Mermaid code or rendered diagram
- **Advantage:** Markdown-compatible, easy to embed in documents

**2. GPT-4 with DALL-E / Code Interpreter**
- **Best for:** Custom diagram generation, creative visualization
- **Prompt style:** "Create a detailed technical diagram showing..."
- **Output:** PNG image or code-generated SVG
- **Advantage:** Can handle complex multi-layer visualizations

**3. Claude with Diagram Generation**
- **Best for:** Structured diagrams, clear relationships
- **Prompt style:** "Generate a diagram showing..."
- **Output:** Mermaid, PlantUML, or SVG
- **Advantage:** Good at technical documentation visualization

**4. D3.js / Observable**
- **Best for:** Interactive visualizations, animated blockchain chains
- **Prompt style:** "Create an interactive D3.js visualization..."
- **Output:** Interactive web page
- **Advantage:** Can show hash propagation dynamically

**5. Graphviz (via AI code generation)**
- **Best for:** Directed graphs, dependency diagrams
- **Prompt style:** "Generate Graphviz DOT code for..."
- **Output:** SVG/PNG via Graphviz rendering
- **Advantage:** Excellent for showing key dependencies

### Visualization Attempt (Mermaid)

Below is a Mermaid flowchart attempt showing the hash flow:

```mermaid
graph TD
    subgraph "Scheduler Main Loop - Iteration N"
        T0[Start: SchedulerStep++] --> T1[CropToMaxSchedules]
        T1 --> T2[TimeDeconfliction:<br/>Create New Schedules]
        T2 --> T3[CheckAllPotentialSchedules]
        T3 --> T4[EvaluateAndSortCanPerformSchedules]
        T4 --> T5[MergeAndClearSystemSchedules]
        T5 --> T6[Final Crop]
    end

    subgraph "Schedule Hash Blockchain (Stack-based)"
        direction TB
        SCH0[Iteration 0 Stack<br/>H0v=hash-after-value TOP<br/>H0=hash-after-event BOTTOM]
        SCH1[Iteration 1 Stack<br/>H1v=hash-after-value TOP<br/>H1=hash-after-event BOTTOM]
        
        SCH0 -->|Previous Hash| SCH1
        H0[Hash H0:<br/>SHA256''\|event0\|value0] -->|Chain| H0v[Hash H0v:<br/>SHA256H0\|null\|value0]
        H0v -->|Previous Hash| H1[Hash H1:<br/>SHA256H0v\|event1\|value1]
        H1 -->|Chain| H1v[Hash H1v:<br/>SHA256H1\|null\|value1]
    end

    subgraph "State Hash Blockchain (Key-value)"
        direction TB
        SH0[StateHash Check:<br/>key: step0, H0<br/>SHA256''\|time\|result\|state]
        SH0e[StateHash Eval:<br/>key: step0, H0v<br/>SHA256SH0\|time\|true\|state\|SPOOF]
        SH1[StateHash Check:<br/>key: step1, H1<br/>SHA256previous\|time\|result\|state]
        SH1e[StateHash Eval:<br/>key: step1, H1v<br/>SHA256SH1\|time\|true\|state\|SPOOF]
        
        SH0 -->|Previous Hash| SH0e
        SH0e -->|Previous Hash| SH1
        SH1 -->|Previous Hash| SH1e
    end

    subgraph "Combined Hash"
        CH0[Check Combined:<br/>SHA256H0\|SH0]
        CH0e[Eval Combined:<br/>SHA256H0v\|SH0e]
        CH1[Check Combined:<br/>SHA256H1\|SH1]
        CH1e[Eval Combined:<br/>SHA256H1v\|SH1e]
    end

    T2 -->|UpdateHashAfterEvent| H0
    T4 -->|UpdateHashAfterValueEvaluation| H0v
    T3 -->|UpdateStateHashAfterCheck<br/>key: step0, H0| SH0
    T4 -->|UpdateStateHashAfterEval<br/>key: step0, H0v| SH0e
    
    H0 -.->|Key Dependency| SH0
    H0v -.->|Key Dependency| SH0e
    
    SH0 -->|Combined| CH0
    SH0e -->|Combined| CH0e
    
    style SCH0 fill:#e1f5ff
    style SCH1 fill:#e1f5ff
    style H0 fill:#b3e5fc
    style H0v fill:#b3e5fc
    style H1 fill:#b3e5fc
    style H1v fill:#b3e5fc
    
    style SH0 fill:#c8e6c9
    style SH0e fill:#c8e6c9
    style SH1 fill:#c8e6c9
    style SH1e fill:#c8e6c9
    
    style CH0 fill:#e1bee7
    style CH0e fill:#e1bee7
    style CH1 fill:#e1bee7
    style CH1e fill:#e1bee7
```

**Key Features Shown:**
- ✅ Temporal flow T0-T6 with hash update points
- ✅ Schedule hash blockchain (stack-based, per iteration)
- ✅ State hash blockchain (key-value, chained by previous)
- ✅ Combined hash generation
- ✅ Key dependency links (dotted lines)
- ✅ Color coding (blue=schedule, green=state, purple=combined)
- ✅ Blockchain chaining arrows

**Limitations of Mermaid:**
- Cannot show exact hash values in detail
- Limited styling for complex relationships
- Stack visualization is simplified

### Enhanced Visualization Ideas

**1. Interactive Timeline Visualization**
- Horizontal timeline showing T0-T6
- Hash blocks appearing at each stage
- Click to expand hash details (value, formula, dependencies)
- Shows how same schedule hash used at T3 (Check) and T4 (Eval) but with different state

**2. Blockchain Chain Visualization**
- Vertical chains for each hash type
- Connected blocks showing previous→current hash arrows
- Color-coded by type (schedule/state/combined)
- Hover to see hash formulas

**3. Dependency Graph**
- Nodes: schedule hashes, state hashes, combined hashes
- Edges: blockchain chaining, key lookups, combined generation
- Layout: Schedule hashes top, state hashes middle, combined bottom
- Shows exact (step, scheduleHash) key matching

**4. Side-by-Side Comparison**
- Left: Sequential execution (program run)
- Right: Parallel execution (test run)
- Both show same final hash sets
- Emphasizes: Order different, content same, hashes match

**5. Stage-by-Stage Animation**
- Step through T0-T6 showing hash updates
- Highlight which hash is active at each stage
- Show how Check uses hash-after-event, Eval uses hash-after-value
- Animate hash chaining (previous → current)

### Academic Paper Visualization Recommendations

**For Publication:**
1. **Figure 1:** High-level overview (three hash systems + temporal flow)
2. **Figure 2:** Blockchain chaining detail (show formula and previous hash dependency)
3. **Figure 3:** Verification comparison (sequential vs parallel, same hash sets)
4. **Figure 4:** Stage-by-stage hash updates (Check vs EvalAll contexts)

**Visual Style:**
- Clean, professional (black/white or minimal color)
- Clear labels and legends
- Formula annotations for hash computation
- Arrows showing data flow and dependencies
- Avoid clutter, emphasize relationships
