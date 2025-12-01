// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MissionElements;
using UserModel;

namespace HSFScheduler
{
    /// <summary>
    /// Debug and visualization information for SystemSchedule
    /// Tracks schedule growth patterns, task execution timing, and algorithm behavior
    /// </summary>
    public class SystemScheduleInfo
    {
        #region Static Hash History Tracking
        
        private static readonly object _hashHistoryLock = new object();
        private static int _sortIteration = 0;
        private static string? _hashHistoryFilePath = null;
        private static string? _lastContext = null;
        
        // Static fields for combined schedule-state hash history file tracking
        private static readonly object _combinedHashHistoryLock = new object();
        private static string? _combinedHashHistoryFilePath = null;
        
        /// <summary>
        /// Initializes the hash history file path (called once at program start)
        /// Sets the file path to FullScheduleHashHistory.txt in HashData/ subdirectory
        /// Can be called multiple times to update the path (useful for test runs)
        /// </summary>
        public static void InitializeHashHistoryFile(string outputDirectory)
        {
            lock (_hashHistoryLock)
            {
                // Create HashData subdirectory if it doesn't exist
                string hashDataDir = Path.Combine(outputDirectory, "HashData");
                Directory.CreateDirectory(hashDataDir);
                
                // Always update path (allows re-initialization for test runs with different directories)
                _hashHistoryFilePath = Path.Combine(hashDataDir, "FullScheduleHashHistory.txt");
                // Clear existing file if it exists (start fresh each run)
                if (File.Exists(_hashHistoryFilePath))
                {
                    File.Delete(_hashHistoryFilePath);
                }
                // Reset iteration counter and context when initializing (fresh run)
                _sortIteration = 0;
                _lastContext = null;
            }
        }
        
        /// <summary>
        /// Initializes the combined hash history file path (called once at program start)
        /// Sets the file path to FullScheduleStateHashHistory.txt in HashData/ subdirectory
        /// Can be called multiple times to update the path (useful for test runs)
        /// </summary>
        public static void InitializeCombinedHashHistoryFile(string outputDirectory)
        {
            lock (_combinedHashHistoryLock)
            {
                // Create HashData subdirectory if it doesn't exist
                string hashDataDir = Path.Combine(outputDirectory, "HashData");
                Directory.CreateDirectory(hashDataDir);
                
                // Always update path (allows re-initialization for test runs with different directories)
                _combinedHashHistoryFilePath = Path.Combine(hashDataDir, "FullScheduleStateHashHistory.txt");
                // Clear existing file if it exists (start fresh each run)
                if (File.Exists(_combinedHashHistoryFilePath))
                {
                    File.Delete(_combinedHashHistoryFilePath);
                }
            }
        }
        
        /// <summary>
        /// Records schedule hash history after sorting
        /// Writes a line with format: [<iteration>: <context>] <all hashes space delimited>
        /// Context is either "CropToMax" or "EvalSort" (detected from order or previous call)
        /// Thread-safe and maintains iteration counter
        /// Only records if SimParameters.EnableHashTracking is true
        /// </summary>
        public static void RecordSortHashHistory(List<SystemSchedule> schedules, string context = "")
        {
            // Early return if hash tracking is disabled
            if (!SimParameters.EnableHashTracking)
                return;
                
            lock (_hashHistoryLock)
            {
                // Initialize file path if not set (use SimParameters.OutputDirectory)
                if (string.IsNullOrEmpty(_hashHistoryFilePath))
                {
                    string outputDir = SimParameters.OutputDirectory ?? Path.Combine(Utilities.DevEnvironment.RepoDirectory, "output");
                    InitializeHashHistoryFile(outputDir);
                }
                
                // Auto-detect context if not provided
                if (string.IsNullOrEmpty(context))
                {
                    // EvalSort always follows CropToMax in the same iteration, or starts a new iteration
                    // If last context was CropToMax, this must be EvalSort
                    if (_lastContext == "CropToMax")
                    {
                        context = "EvalSort";
                    }
                    else
                    {
                        // CropToMax is always called first (at start of iteration)
                        context = "CropToMax";
                        _sortIteration++;
                    }
                }
                else if (context == "CropToMax")
                {
                    _sortIteration++;
                }
                
                _lastContext = context;
                
                // Collect all schedule hashes (final blockchain hash for each schedule)
                var hashList = new List<string>();
                foreach (var schedule in schedules)
                {
                    string scheduleHash = schedule.ScheduleInfo.ScheduleHash;
                    if (!string.IsNullOrEmpty(scheduleHash))
                    {
                        hashList.Add(scheduleHash);
                    }
                }
                
                // Format: [<iteration>: <context>] <hashes space delimited>
                // Ensure consistent header width: [<4 chars>: <9 chars>] = 15 chars total
                string contextPadded = context.Length <= 9 ? context.PadRight(9) : context.Substring(0, 9);
                string iterationStr = _sortIteration.ToString().PadLeft(4);
                string hashesStr = string.Join(" ", hashList);
                string line = $"[{iterationStr}: {contextPadded}] {hashesStr}";
                
                // Append to file (thread-safe via lock)
                if (!string.IsNullOrEmpty(_hashHistoryFilePath))
                {
                    File.AppendAllText(_hashHistoryFilePath, line + Environment.NewLine);
                }
            }
        }
        
        /// <summary>
        /// Computes a combined hash from schedule hash and state hash
        /// Format: hash(scheduleHash + stateHash)
        /// </summary>
        private static string ComputeCombinedHash(string scheduleHash, string stateHash)
        {
            if (string.IsNullOrEmpty(scheduleHash) || string.IsNullOrEmpty(stateHash))
                return "";
            
            string combined = $"{scheduleHash}||{stateHash}";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
                return System.BitConverter.ToString(hashBytes).Replace("-", "").ToLower().Substring(0, 16);  // 16 char hash
            }
        }
        
        /// <summary>
        /// Records combined schedule-state hash history after CheckSchedule or evaluation
        /// Matches schedule hashes with their corresponding state hashes using (Step, ScheduleHash) key
        /// Writes a line with format: [<iteration>A/B] <combined hashes space delimited>
        /// A = Check context, B = EvalAll context (matching the step from StateHashHistory)
        /// Only records if SimParameters.EnableHashTracking is true
        /// </summary>
        public static void RecordCombinedHashHistory(List<SystemSchedule> schedules, string context, int step)
        {
            // Early return if hash tracking is disabled
            if (!SimParameters.EnableHashTracking)
                return;
                
            lock (_combinedHashHistoryLock)
            {
                // Initialize file path if not set
                if (string.IsNullOrEmpty(_combinedHashHistoryFilePath))
                {
                    string outputDir = SimParameters.OutputDirectory ?? Path.Combine(Utilities.DevEnvironment.RepoDirectory, "output");
                    string hashDataDir = Path.Combine(outputDir, "HashData");
                    Directory.CreateDirectory(hashDataDir);
                    _combinedHashHistoryFilePath = Path.Combine(hashDataDir, "FullScheduleStateHashHistory.txt");
                    // Clear existing file if it exists (start fresh each run)
                    if (File.Exists(_combinedHashHistoryFilePath))
                    {
                        File.Delete(_combinedHashHistoryFilePath);
                    }
                }
                
                // Build dictionary of schedule hash -> combined hash (sorted by schedule hash for determinism)
                var combinedHashDict = new Dictionary<string, string>();
                var verificationErrors = new List<string>();
                
                foreach (var schedule in schedules)
                {
                    string scheduleHash = schedule.ScheduleInfo.ScheduleHash;
                    
                    // Fallback: if blockchain hash not initialized, compute full hash (same as CheckAllPotentialSchedules)
                    if (string.IsNullOrEmpty(scheduleHash))
                    {
                        scheduleHash = SystemSchedule.ComputeScheduleHash(schedule);
                    }
                    
                    if (!string.IsNullOrEmpty(scheduleHash))
                    {
                        // Find corresponding state hash using (Step, ScheduleHash) key
                        string stateHash = "";
                        var stateHistory = schedule.AllStates;
                        
                        // Check if schedule hash is a key in StateHashHistory
                        bool found = stateHistory.StateHashHistory.TryGetValue((step, scheduleHash), out stateHash!);
                        
                        if (!found)
                        {
                            // Try to find any entry with matching schedule hash (might be from different step)
                            var matchingEntry = stateHistory.StateHashHistory
                                .Where(kvp => kvp.Key.ScheduleHash == scheduleHash)
                                .OrderByDescending(kvp => kvp.Key.Step)
                                .FirstOrDefault();
                            
                            if (matchingEntry.Key != default)
                            {
                                stateHash = matchingEntry.Value;
                                verificationErrors.Add($"WARNING: ScheduleHash {scheduleHash} found at step {matchingEntry.Key.Step} but expected step {step}");
                            }
                            else
                            {
                                verificationErrors.Add($"ERROR: ScheduleHash {scheduleHash} not found in StateHashHistory for schedule {schedule._scheduleID}");
                                continue;  // Skip this schedule
                            }
                        }
                        
                        // Compute combined hash if state hash found
                        if (!string.IsNullOrEmpty(stateHash))
                        {
                            // Compute combined hash
                            string combinedHash = ComputeCombinedHash(scheduleHash, stateHash);
                            combinedHashDict[scheduleHash] = combinedHash;
                        }
                    }
                }
                
                // Print verification errors only (not successful lookups)
                if (verificationErrors.Count > 0)
                {
                    Console.WriteLine($"  Combined Hash Verification Issues ({verificationErrors.Count}):");
                    foreach (var error in verificationErrors)
                    {
                        Console.WriteLine($"    {error}");
                    }
                }
                
                // Sort by schedule hash (deterministic ordering)
                var sortedEntries = combinedHashDict.OrderBy(kvp => kvp.Key).ToList();
                var combinedHashList = sortedEntries.Select(kvp => kvp.Value).ToList();
                
                // Format: [<iteration>A/B] <combined hashes space delimited>
                // A = Check (step iteration), B = EvalAll (step iteration)
                string contextSuffix = context == "Check" ? "A" : "B";
                string iterationStr = step.ToString().PadLeft(4);
                string hashesStr = string.Join(" ", combinedHashList);
                string line = $"[{iterationStr}{contextSuffix}] {hashesStr}";
                
                // Append to file (thread-safe via lock)
                if (!string.IsNullOrEmpty(_combinedHashHistoryFilePath))
                {
                    File.AppendAllText(_combinedHashHistoryFilePath, line + Environment.NewLine);
                }
            }
        }
        
        #endregion
        
        #region Visualization Attributes
        
        /// <summary>
        /// Number of total time steps in the simulation
        /// </summary>
        public int TotalTimeSteps { get; private set; } = (int)Math.Floor((SimParameters.SimEndSeconds - SimParameters.SimStartSeconds) / SimParameters.SimStepSeconds);
        
        /// <summary>
        /// Tracks which time steps have events (1) vs holes (0)
        /// Index = time step, Value = 1 if event exists, 0 if hole
        /// </summary>
        public int[]? EventExistence_TimeSteps { get; private set; }
        
        /// <summary>
        /// Detailed event content - when events exist, what tasks were scheduled
        /// Key = time step, Value = dictionary of asset->task mappings for that event
        /// </summary>
        public Dictionary<int, Dictionary<Asset, MissionElements.Task>>? EventDetails { get; private set; }
        
        /// <summary>
        /// Tracks the growth pattern of this schedule (how many schedules existed at each time step)
        /// </summary>
        public List<int>?ScheduleGrowthPattern { get; private set; }
        
        /// <summary>
        /// Tracks when this schedule was created (which time step)
        /// </summary>
        public int CreationTimeStep { get; private set; }
        
        /// <summary>
        /// Tracks the "depth" of this schedule (how many events it contains)
        /// </summary>
        public int? ScheduleDepth { get; private set; }
        public string? _printInfoString { get; private set; }
        
        /// <summary>
        /// String representation of time step headers (e.g., "[0][1][2][3]...")
        /// </summary>
        public string? TimeStepString { get; private set; }
        
        /// <summary>
        /// String representation of event existence pattern (e.g., "1010..." where 1=event, 0=hole)
        /// </summary>
        public string? EventString { get; private set; }
        
        /// <summary>
        /// Schedule hash history - tracks schedule hash evolution per scheduler step (blockchain-style)
        /// Key: Scheduler step (iteration number)
        /// Value: Stack of hashes for that step (bottom = after event added, top = after value evaluated)
        /// This structure ensures repeatability regardless of execution order
        /// </summary>
        public Dictionary<int, Stack<string>> ScheduleHashHistory { get; set; } = new Dictionary<int, Stack<string>>();
        
        /// <summary>
        /// Returns the final schedule hash (top of the last step's stack)
        /// This is the hash after all iterations have completed
        /// </summary>
        public string ScheduleHash
        {
            get
            {
                if (ScheduleHashHistory.Count == 0)
                    return "";
                
                // Get the last step's stack (highest key)
                int lastStep = ScheduleHashHistory.Keys.Max();
                var lastStack = ScheduleHashHistory[lastStep];
                
                // Return top of stack (most recent hash for that step)
                return lastStack.Count > 0 ? lastStack.Peek() : "";
            }
        }
        
        #endregion

        #region Constructor
        public SystemScheduleInfo()
        {
            CreationTimeStep = Scheduler.SchedulerStep;
            
            // Initialize arrays for empty schedule
            EventExistence_TimeSteps = new int[TotalTimeSteps];
            EventDetails = new Dictionary<int, Dictionary<Asset, MissionElements.Task>>();
            ScheduleGrowthPattern = new List<int>();
            ScheduleDepth = 0; // Empty schedule has no events
            
            // Mark all time steps as holes (no events)
            for (int i = 0; i < TotalTimeSteps; i++)
            {
                EventExistence_TimeSteps[i] = 0;
            }
            
            // Generate the time step and event strings
            TimeStepString = GenerateTimeStepString();
            EventString = GenerateEventString();
            
            // Initialize hash history for empty schedule (no scheduler step yet, will be set when first event added)
        }
        public SystemScheduleInfo(StateHistory allStates, int creationTimeStep)
        {
            CreationTimeStep = creationTimeStep;

            EventExistence_TimeSteps = new int[TotalTimeSteps];
            EventDetails = new Dictionary<int, Dictionary<Asset, MissionElements.Task>>();
            ScheduleGrowthPattern = new List<int>();
            ScheduleDepth = allStates.Events.Count;

            // Populate visualization based on current schedule state
            PopulateVisualizationFromSchedule(allStates);
            _printInfoString = GetScheduleVisualizationPrintString();
            
            // Generate the time step and event strings
            TimeStepString = GenerateTimeStepString();
            EventString = GenerateEventString();
            
            // Hash history will be initialized when first event is added (in SystemSchedule constructor)
        }
        
        #endregion
        
        #region Visualization Methods
        
        /// <summary>
        /// Populate the visualization arrays based on the current schedule state
        /// </summary>
        private void PopulateVisualizationFromSchedule(StateHistory allStates)
        {
            // First, mark all time steps as holes
            for (int i = 0; i < TotalTimeSteps; i++)
            {
                EventExistence_TimeSteps[i] = 0;
            }
            
            // Then, mark time steps that have events based on their actual start times
            foreach (var evt in allStates.Events)
            {
                // Calculate which time step this event belongs to based on its actual start time
                // Time step = (event_start_time - sim_start_time) / sim_step_size
                int timeStepIndex = (int)Math.Floor((evt.GetEventStart(evt.Tasks.Keys.First()) - SimParameters.SimStartSeconds) / SimParameters.SimStepSeconds);
                
                if (timeStepIndex >= 0 && timeStepIndex < TotalTimeSteps)
                {
                    EventExistence_TimeSteps[timeStepIndex] = 1;
                    EventDetails[timeStepIndex] = new Dictionary<Asset, MissionElements.Task>(evt.Tasks);
                }
            }
        }
        
        /// <summary>
        /// Record that an event was created at a specific time step with given asset-task mappings
        /// </summary>
        public void RecordEvent(int timeStep, Dictionary<Asset, MissionElements.Task> eventTasks)
        {
            if (timeStep >= 0 && timeStep < TotalTimeSteps)
            {
                EventExistence_TimeSteps[timeStep] = 1; // Mark as having an event
                EventDetails[timeStep] = new Dictionary<Asset, MissionElements.Task>(eventTasks);
            }
        }
        
        /// <summary>
        /// Record that no event occurred at a specific time step (hole)
        /// </summary>
        public void RecordHole(int timeStep)
        {
            if (timeStep >= 0 && timeStep < TotalTimeSteps)
            {
                EventExistence_TimeSteps[timeStep] = 0; // Mark as hole
                // Don't add to EventDetails - holes have no details
            }
        }
        
        /// <summary>
        /// Record the current schedule depth (number of events)
        /// </summary>
        public void UpdateScheduleDepth(int depth)
        {
            ScheduleDepth = depth;
        }

        /// <summary>
        /// Gets the next iteration number for this schedule (starts at 0, increments upward)
        /// Uses the count of existing hash history entries to ensure deterministic iteration numbering
        /// </summary>
        private int GetNextIterationNumber()
        {
            return ScheduleHashHistory.Count;
        }

        /// <summary>
        /// Gets the final hash from the previous iteration (top of last iteration's stack)
        /// Returns empty string if no previous iterations exist
        /// </summary>
        private string GetPreviousIterationHash()
        {
            if (ScheduleHashHistory.Count == 0)
                return "";
            
            int lastIteration = ScheduleHashHistory.Keys.Max();
            if (ScheduleHashHistory[lastIteration].Count > 0)
            {
                return ScheduleHashHistory[lastIteration].Peek();
            }
            return "";
        }

        /// <summary>
        /// Update schedule hash when a new event is added (blockchain-style)
        /// Uses the top of the previous iteration's stack as previous hash, creates new hash with new event
        /// Pushes hash to current iteration's stack (first entry in stack for this iteration)
        /// Iteration numbers start at 0 and increment upward deterministically
        /// </summary>
        /// <param name="newEvent">The new event being added</param>
        /// <param name="scheduleValue">Current schedule value (typically 0 when event first added)</param>
        /// <returns>The new hash after adding the event</returns>
        private string UpdateScheduleHashAfterEvent(Event newEvent, double scheduleValue)
        {
            // Get iteration number (deterministic: 0, 1, 2, ... based on history count)
            int iteration = GetNextIterationNumber();
            
            // Get previous hash from last iteration's stack top
            string previousHash = GetPreviousIterationHash();
            
            // Ensure stack exists for current iteration
            if (!ScheduleHashHistory.ContainsKey(iteration))
            {
                ScheduleHashHistory[iteration] = new Stack<string>();
            }
            
            // Compute incremental hash: previous hash + new event + value
            string newHash = SystemSchedule.ComputeIncrementalHash(previousHash, newEvent, scheduleValue);
            
            // Push to current iteration's stack (first entry = after event added)
            ScheduleHashHistory[iteration].Push(newHash);
            
            return newHash;
        }

        /// <summary>
        /// Update schedule hash when schedule value is evaluated (blockchain-style)
        /// Uses the top of current iteration's stack as previous hash, creates new hash with updated value
        /// Pushes hash to current iteration's stack (second entry in stack for this iteration)
        /// </summary>
        /// <param name="scheduleValue">New schedule value after evaluation</param>
        /// <returns>The new hash after value evaluation</returns>
        private string UpdateScheduleHashAfterValueEvaluation(double scheduleValue)
        {
            // Get current iteration (should already exist from event addition)
            if (ScheduleHashHistory.Count == 0)
            {
                // No previous iteration - create one
                int iteration = GetNextIterationNumber();
                ScheduleHashHistory[iteration] = new Stack<string>();
                // Use empty string as previous hash
                string initialHash = SystemSchedule.ComputeIncrementalHash("", null, scheduleValue);
                ScheduleHashHistory[iteration].Push(initialHash);
                return initialHash;
            }
            
            // Get current iteration (last one in dictionary)
            int currentIteration = ScheduleHashHistory.Keys.Max();
            
            // Get previous hash (top of current iteration's stack - the hash after event was added)
            string previousHash = "";
            if (ScheduleHashHistory[currentIteration].Count > 0)
            {
                previousHash = ScheduleHashHistory[currentIteration].Peek();
            }
            else
            {
                // If no hash for this iteration yet, get from previous iteration
                previousHash = GetPreviousIterationHash();
            }
            
            // Compute incremental hash: previous hash + no new event + new value
            string hashAfterValueEval = SystemSchedule.ComputeIncrementalHash(previousHash, null, scheduleValue);
            
            // Push to current iteration's stack (second entry = after value evaluated)
            ScheduleHashHistory[currentIteration].Push(hashAfterValueEval);
            
            return hashAfterValueEval;
        }

        /// <summary>
        /// Static helper: Updates schedule hash after a new event is added
        /// Wraps the instance method for clean integration into existing code
        /// </summary>
        public static void UpdateHashAfterEvent(SystemSchedule schedule, Event newEvent, double scheduleValue = 0.0)
        {
            schedule.ScheduleInfo.UpdateScheduleHashAfterEvent(newEvent, scheduleValue);
        }

        /// <summary>
        /// Static helper: Updates schedule hash after value evaluation
        /// Wraps the instance method for clean integration into existing code
        /// </summary>
        public static void UpdateHashAfterValueEvaluation(SystemSchedule schedule, double scheduleValue)
        {
            schedule.ScheduleInfo.UpdateScheduleHashAfterValueEvaluation(scheduleValue);
        }

        /// <summary>
        /// Static helper: Copies schedule hash history from old schedule to new schedule
        /// Used when creating a new schedule from an existing one (preserves traceability)
        /// </summary>
        public static void CopyHashHistoryFromOldSchedule(SystemSchedule newSchedule, SystemSchedule oldSchedule)
        {
            if (oldSchedule.ScheduleInfo.ScheduleHashHistory.Count == 0)
                return;
            
            // Copy hash history from old schedule (preserve traceability)
            newSchedule.ScheduleInfo.ScheduleHashHistory = new Dictionary<int, Stack<string>>();
            foreach (var kvp in oldSchedule.ScheduleInfo.ScheduleHashHistory)
            {
                // Deep copy stacks (reverse to preserve order)
                var stackCopy = new Stack<string>();
                var reversed = new List<string>(kvp.Value);
                reversed.Reverse();
                foreach (var hash in reversed)
                {
                    stackCopy.Push(hash);
                }
                newSchedule.ScheduleInfo.ScheduleHashHistory[kvp.Key] = stackCopy;
            }
        }
        
        /// <summary>
        /// Record the schedule growth pattern at a specific time step
        /// </summary>
        public void RecordScheduleCount(int timeStep, int scheduleCount)
        {
            // Ensure we have enough entries
            while (ScheduleGrowthPattern.Count <= timeStep)
            {
                ScheduleGrowthPattern.Add(0);
            }
            ScheduleGrowthPattern[timeStep] = scheduleCount;
        }
        
        /// <summary>
        /// Generate the time step string (e.g., "[0][1][2][-]...")
        /// Shows actual step numbers for completed steps, '-' for future steps
        /// </summary>
        private string GenerateTimeStepString()
        {
            var output = new System.Text.StringBuilder();
            int currentStep = Scheduler.SchedulerStep;
            
            for (int i = 0; i < TotalTimeSteps; i++)
            {
                if (i <= currentStep)
                    output.Append($"[{i}]");
                else
                    output.Append("[-]");
            }
            return output.ToString();
        }
        
        /// <summary>
        /// Generate the event string (e.g., "[1][0][1][-]..." where 1=event, 0=hole, -=not reached)
        /// Shows event status for completed steps, '-' for future steps
        /// </summary>
        private string GenerateEventString()
        {
            var output = new System.Text.StringBuilder();
            int currentStep = Scheduler.SchedulerStep;
            
            for (int i = 0; i < TotalTimeSteps; i++)
            {
                if (i > currentStep)
                {
                    output.Append("[-]"); // Future step, not reached yet
                }
                else if (EventExistence_TimeSteps != null && i < EventExistence_TimeSteps.Length && EventExistence_TimeSteps[i] == 1)
                {
                    output.Append("[1]"); // Event exists at this step
                }
                else
                {
                    output.Append("[0]"); // Hole at this step
                }
            }
            return output.ToString();
        }
        
        /// <summary>
        /// Update visualization strings when scheduler advances to a new time step
        /// Converts any remaining [-] entries up to current step to [0] (holes)
        /// </summary>
        public void UpdateForCurrentTimeStep()
        {
            // Regenerate the strings with current scheduler step
            TimeStepString = GenerateTimeStepString();
            EventString = GenerateEventString();
        }

        #endregion

        #region Debug Output Methods

        /// <summary>
        /// Generate a comprehensive schedule summary for all schedules
        /// </summary>
        public static void PrintAllSchedulesSummary(List<SystemSchedule> schedules, bool showAssetTaskDetails = false, bool overRideConsoleLogging = false, TimeSpan? iterationTime = null, bool finalScheduleSummary = false)
        {
            string it = Scheduler.SchedulerStep.ToString(); 
            if (finalScheduleSummary) { it = "FINAL"; }
            string timeMessage = iterationTime.HasValue ? $"Step {it} in {iterationTime.Value.TotalSeconds,8:F4}s | Total Schedules: {schedules.Count,7} " : $"Step {it} | Total: {schedules.Count,7} schedules";
            
            string statusMessage = $" Status: {100 * Scheduler.CurrentTime / SimParameters.SimEndSeconds,4:F1}% ... Generated: {Scheduler._SchedulesGenerated,5} " +
                                $"| Carried Over: {Scheduler._SchedulesCarriedOver,5} | Cropped: {Scheduler._SchedulesCropped,5}";
            
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine($"SCHEDULE SUMMARY - {timeMessage}");
            Console.WriteLine($"{statusMessage}");
            Console.WriteLine(new string('=', 80));
            
            if (!SchedParameters.ConsoleLogging && !overRideConsoleLogging)
            { return; } // return eraly if "all" is not set. }
            
            // Otherwise continue printing:
            if (schedules.Count == 0)
            {
                Console.WriteLine("No schedules to display.");
                return;
            }
            
            // Print time step headers
            Console.WriteLine("Time:  " + GenerateTimeStepHeader());
            // Console.WriteLine("Events:" + GenerateEventHeader(schedules)); // Commented out - shows erroneous large counts
            Console.WriteLine(new string('-', 80));

            // Print each schedule's details
            for (int i = 0; i < schedules.Count; i++)
            {
                var schedule = schedules[i];
                if (SchedParameters.ConsoleLogMode == "truncate" && i > SchedParameters.NumSchedCropTo || i > 20)
                {
                    Console.WriteLine("                               .");
                    Console.WriteLine("                               .");
                    Console.WriteLine("                               .");
                    Console.WriteLine($" [SytemScheduleInfo]: {schedules.Count - (i-1)} evalutated schedules not printed... \n" +
                                      $" Schedule printing truncated at {i-1} given ConsoleLogMode = '{SchedParameters.ConsoleLogMode}'. Top {i-1} shown above. \n" +
                                      $" (Note: these {schedules.Count - (i-1)} generated and evlauted this scheduler timestep (from either/both carried over and new schedules).");
                    break;
                }
                // Otherwise print the line! Top sched down! 
                string scheduleHashDisplay = finalScheduleSummary ? $" | Hash:{schedule.ScheduleInfo.ScheduleHash,16}" : "";
                Console.WriteLine($" {schedule._scheduleID,12} | Val:{schedule.ScheduleValue,10:F2} | Ev:{schedule.AllStates.Events.Count,2}{scheduleHashDisplay} | {schedule.ScheduleInfo.EventString}");
                if (showAssetTaskDetails)
                {
                    PrintAssetTaskDetails(schedule);
                }
            }
            
            // Print out the time on the final schedule summary print:
            if (finalScheduleSummary)
            {
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"SCHEDULER TOTAL TIME: {iterationTime.Value.TotalSeconds:F3} seconds");
            }
            Console.WriteLine(new string('-', 80) + "\n");
        }
        
        /// <summary>
        /// Generate time step header string
        /// </summary>
        private static string GenerateTimeStepHeader()
        {
            var output = new System.Text.StringBuilder();
            int currentStep = Scheduler.SchedulerStep;
            
            for (int i = 0; i < Math.Min(20, (int)Math.Floor((SimParameters.SimEndSeconds - SimParameters.SimStartSeconds) / SimParameters.SimStepSeconds)); i++)
            {
                if (i <= currentStep)
                    output.Append($"[{i,2}]");
                else
                    output.Append("[-]");
            }
            return output.ToString();
        }
        
        /// <summary>
        /// Generate event header string (shows which schedules have events at each step)
        /// </summary>
        private static string GenerateEventHeader(List<SystemSchedule> schedules)
        {
            var output = new System.Text.StringBuilder();
            int currentStep = Scheduler.SchedulerStep;
            int totalSteps = Math.Min(20, (int)Math.Floor((SimParameters.SimEndSeconds - SimParameters.SimStartSeconds) / SimParameters.SimStepSeconds));
            
            for (int i = 0; i < totalSteps; i++)
            {
                if (i > currentStep)
                {
                    output.Append("[-]");
                }
                else
                {
                    // Count how many schedules have events at this step
                    int eventCount = 0;
                    foreach (var schedule in schedules)
                    {
                        if (schedule.ScheduleInfo.EventExistence_TimeSteps != null && 
                            i < schedule.ScheduleInfo.EventExistence_TimeSteps.Length && 
                            schedule.ScheduleInfo.EventExistence_TimeSteps[i] == 1)
                        {
                            eventCount++;
                        }
                    }
                    output.Append($"[{eventCount,2}]");
                }
            }
            return output.ToString();
        }
        
        /// <summary>
        /// Print detailed asset-task information for a schedule
        /// </summary>
        private static void PrintAssetTaskDetails(SystemSchedule schedule)
        {
            if (schedule.ScheduleInfo.EventDetails == null) return;
            
            foreach (var kvp in schedule.ScheduleInfo.EventDetails.OrderBy(x => x.Key))
            {
                int timeStep = kvp.Key;
                var tasks = kvp.Value;
                
                Console.WriteLine($"  Step {timeStep}: {string.Join(", ", tasks.Select(t => $"{t.Key.Name}→{t.Value.Name}"))}");
            }
        }

        /// <summary>
        /// Generate a string visualization of the schedule pattern
        /// </summary>
        public void PrintInfoString()
        {
            Console.WriteLine(_printInfoString);
        }
        public string GetScheduleVisualizationPrintString()
        {
            var output = new System.Text.StringBuilder();
            output.AppendLine("=== SCHEDULE VISUALIZATION ===");
            output.AppendLine($"Creation Time Step: {CreationTimeStep}");
            output.AppendLine($"Schedule Depth: {ScheduleDepth}");
            output.AppendLine();

            // Time step header
            output.Append("Time:  ");
            for (int i = 0; i < Math.Min(TotalTimeSteps, 20); i++) // Limit to first 20 steps for readability
            {
                output.Append($"[{i,2}]");
            }
            output.AppendLine();

            // Event existence visualization
            output.Append("Events:");
            for (int i = 0; i < Math.Min(TotalTimeSteps, 20); i++)
            {
                if (EventExistence_TimeSteps[i] == 1)
                    output.Append("[1]");
                else
                    output.Append("[0]");
            }
            output.AppendLine();

            return output.ToString();

        }
        
        /// <summary>
        /// Get summary statistics about the schedule
        /// </summary>
        public string GetScheduleSummary()
        {
            int totalEvents = 0;
            int totalHoles = 0;
            
            foreach (int value in EventExistence_TimeSteps)
            {
                if (value == 0)
                    totalHoles++;
                else
                    totalEvents++;
            }
            
            return $"Schedule Summary: {totalEvents} events, {totalHoles} holes, {ScheduleDepth} events deep";
        }
        
        #endregion
    }
}
