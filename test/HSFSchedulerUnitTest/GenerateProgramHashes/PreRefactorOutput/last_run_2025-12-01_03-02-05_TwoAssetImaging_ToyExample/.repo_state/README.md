# Repository State Snapshot

This directory contains the exact git repository state at the time of the run.

## Quick Reference
- **Branch**: jebeals-scheduler
- **Commit**: 88c368f00766504f75ca2001fe7ea4edf5d631f6
- **Origin**: https://github.com/Cal-Poly-Simulation-Lab/Horizon.git
- **Captured**: 2025-12-01 03:02:05
- **Modified Files**: 0
- **Untracked Files**: 1

## Files
- **`git_status.txt`**: Machine-readable list of modified/untracked files
- **`git_diff.txt`**: Complete diff of all uncommitted changes to tracked files

## Untracked Files
The following files were untracked (not in git diff):
- `test/HSFSchedulerUnitTest/GenerateProgramHashes/PreRefactorOutput/`

## How to Reproduce Exact State
1. Clone the repository:
   ```bash
   git clone https://github.com/Cal-Poly-Simulation-Lab/Horizon.git
   ```

2. Checkout the exact commit:
   ```bash
   git checkout 88c368f00766504f75ca2001fe7ea4edf5d631f6
   ```

3. Apply uncommitted changes to tracked files:
   ```bash
   git apply .repo_state/git_diff.txt
   ```

4. **IMPORTANT**: Manually recreate untracked files listed above
   (Untracked files cannot be included in git diff)

## Notes
- If `git_status.txt` is empty, the working tree was clean (no uncommitted changes)
- If `git_diff.txt` has no content after the header, there were no modifications to tracked files
- Untracked files (marked with `??` in status) cannot be captured in a diff
- For complete reproducibility with untracked files, manually copy them from the original workspace
