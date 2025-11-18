// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System.Collections.Generic;
using System;
using Utilities;
using MissionElements;
using HSFSystem;
using Task = MissionElements.Task;
using System.Data; // error CS0104: 'Task' is an ambiguous reference between 'MissionElements.Task' and 'System.Threading.Tasks.Task'
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UserModel;

namespace HSFScheduler
{
    [Serializable]
    public class StateHistory
    {
        #region Attributes
        public SystemState InitialState { get; private set; }
        public Stack<Event> Events { get; private set; }
        public string _stateHistoryID{ get; private set; } = "";
        
        /// <summary>
        /// State hash history - tracks state hash evolution per scheduler iteration (blockchain-style)
        /// Key: Tuple of (scheduler iteration step, schedule hash at that step)
        /// Value: State history hash for that schedule at that iteration
        /// This structure ensures repeatability regardless of execution order
        /// </summary>
        public Dictionary<(int Step, string ScheduleHash), string> StateHashHistory { get; set; } = new Dictionary<(int Step, string ScheduleHash), string>();
        
        /// <summary>
        /// Returns the final state hash (most recent entry in StateHashHistory)
        /// This is the hash after all iterations have completed for this state history
        /// </summary>
        public string StateHash
        {
            get
            {
                if (StateHashHistory.Count == 0)
                    return "";
                
                // Get the last entry (most recent based on step, then schedule hash)
                var lastEntry = StateHashHistory.OrderByDescending(kvp => kvp.Key.Step)
                    .ThenByDescending(kvp => kvp.Key.ScheduleHash)
                    .First();
                return lastEntry.Value;
            }
        }
        #endregion
        
        #region Static Hash Tracking
        
        // Static fields for state hash history file tracking
        private static readonly object _stateHashHistoryLock = new object();
        private static string? _stateHashHistoryFilePath = null;
        
        /// <summary>
        /// Initializes the state hash history file path (called once at program start)
        /// Sets the file path to FullStateHistoryHash.txt in HashData/ subdirectory
        /// Can be called multiple times to update the path (useful for test runs)
        /// </summary>
        public static void InitializeStateHashHistoryFile(string outputDirectory)
        {
            lock (_stateHashHistoryLock)
            {
                // Create HashData subdirectory if it doesn't exist
                string hashDataDir = Path.Combine(outputDirectory, "HashData");
                Directory.CreateDirectory(hashDataDir);
                
                // Always update path (allows re-initialization for test runs with different directories)
                _stateHashHistoryFilePath = Path.Combine(hashDataDir, "FullStateHistoryHash.txt");
                // Clear existing file if it exists (start fresh each run)
                if (File.Exists(_stateHashHistoryFilePath))
                {
                    File.Delete(_stateHashHistoryFilePath);
                }
            }
        }
        
        /// <summary>
        /// Computes a hash for a StateHistory based on current state data at the current time
        /// Includes: all state variables at their current time (GetLastValue for each), CheckSchedule result
        /// All double times and values truncated to 2 decimals to avoid precision errors
        /// Uses blockchain-style incremental hashing if previous hash provided
        /// </summary>
        public static string ComputeStateHistoryHash(StateHistory stateHistory, double currentTime, bool checkScheduleResult, string previousHash = "")
        {
            var stateDataParts = new List<string>();
            
            // Get the current SystemState (last event's state, or initial state if no events)
            SystemState currentState = stateHistory.GetLastState();
            
            // Extract all state variables from all type dictionaries, sorted by key name for determinism
            // Idata (int)
            foreach (var kvp in currentState.Idata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString();
                    stateDataParts.Add($"I:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch
                {
                    // If no value, skip (state variable not initialized yet)
                }
            }
            
            // Ddata (double)
            foreach (var kvp in currentState.Ddata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString("F2");
                    stateDataParts.Add($"D:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch
                {
                    // If no value, skip
                }
            }
            
            // Bdata (bool)
            foreach (var kvp in currentState.Bdata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString();
                    stateDataParts.Add($"B:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch
                {
                    // If no value, skip
                }
            }
            
            // Mdata (Matrix<double>)
            foreach (var kvp in currentState.Mdata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString();
                    stateDataParts.Add($"M:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch
                {
                    // If no value, skip
                }
            }
            
            // Qdata (Quaternion)
            foreach (var kvp in currentState.Qdata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString();
                    stateDataParts.Add($"Q:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch
                {
                    // If no value, skip
                }
            }
            
            // Vdata (Vector)
            foreach (var kvp in currentState.Vdata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString();
                    stateDataParts.Add($"V:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch
                {
                    // If no value, skip
                }
            }
            
            // Combine: time, checkSchedule result, all state data
            string stateDataStr = string.Join("||", stateDataParts);
            string combined = $"time{currentTime:F2}||checkResult{checkScheduleResult}||{stateDataStr}";
            
            // If previous hash provided, use blockchain-style incremental hashing
            if (!string.IsNullOrEmpty(previousHash))
            {
                combined = $"{previousHash}||{combined}";
            }
            
            // Compute SHA256 hash
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower().Substring(0, 16);  // 16 char hash
            }
        }
        
        /// <summary>
        /// Updates state hash after CheckSchedule completes
        /// Uses blockchain-style incremental hashing (builds on previous hash if exists)
        /// </summary>
        public static string UpdateStateHashAfterCheck(StateHistory stateHistory, double currentTime, bool checkScheduleResult, string scheduleHash)
        {
            // Find previous hash for this schedule (if any)
            string previousHash = "";
            var matchingEntries = stateHistory.StateHashHistory
                .Where(kvp => kvp.Key.ScheduleHash == scheduleHash)
                .OrderByDescending(kvp => kvp.Key.Step)
                .ToList();
            
            if (matchingEntries.Count > 0)
            {
                previousHash = matchingEntries[0].Value;
            }
            
            // Compute new hash
            string newHash = ComputeStateHistoryHash(stateHistory, currentTime, checkScheduleResult, previousHash);
            
            // Store in history
            int step = Scheduler.SchedulerStep;
            stateHistory.StateHashHistory[(step, scheduleHash)] = newHash;
            
            return newHash;
        }
        
        /// <summary>
        /// Updates state hash after evaluation (adds "NEXT" spoof to ensure different hash even if no change)
        /// Uses blockchain-style incremental hashing
        /// </summary>
        public static string UpdateStateHashAfterEval(StateHistory stateHistory, double currentTime, bool checkScheduleResult, string scheduleHash)
        {
            // Find previous hash (should exist from Check)
            string previousHash = stateHistory.StateHash;
            
            // Compute hash with "NEXT" spoof added to ensure different hash after evaluation
            var stateDataParts = new List<string>();
            SystemState currentState = stateHistory.GetLastState();
            
            // Extract state data (same logic as ComputeStateHistoryHash but we'll add "NEXT" at end)
            // Idata
            foreach (var kvp in currentState.Idata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString();
                    stateDataParts.Add($"I:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch { }
            }
            
            // Ddata
            foreach (var kvp in currentState.Ddata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString("F2");
                    stateDataParts.Add($"D:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch { }
            }
            
            // Bdata
            foreach (var kvp in currentState.Bdata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString();
                    stateDataParts.Add($"B:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch { }
            }
            
            // Mdata
            foreach (var kvp in currentState.Mdata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString();
                    stateDataParts.Add($"M:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch { }
            }
            
            // Qdata
            foreach (var kvp in currentState.Qdata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString();
                    stateDataParts.Add($"Q:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch { }
            }
            
            // Vdata
            foreach (var kvp in currentState.Vdata.OrderBy(k => k.Key.VariableName))
            {
                try
                {
                    var (time, value) = currentState.GetValueAtTime(kvp.Key, currentTime);
                    string valueStr = value.ToString();
                    stateDataParts.Add($"V:{kvp.Key.VariableName}:{time:F2}:{valueStr}");
                }
                catch { }
            }
            
            // Combine with "EALSPOOF" spoof to ensure unqiueness in the face of identical state data
            string stateDataStr = string.Join("||", stateDataParts);
            string combined = $"time{currentTime:F2}||checkResult{checkScheduleResult}||{stateDataStr}||EVALSPOOF";
            
            // Use blockchain-style incremental hashing
            combined = $"{previousHash}||{combined}";
            
            // Compute SHA256 hash
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                string newHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower().Substring(0, 16);
                
                // Store in history
                int step = Scheduler.SchedulerStep;
                stateHistory.StateHashHistory[(step, scheduleHash)] = newHash;
                
                return newHash;
            }
        }
        
        /// <summary>
        /// Records state hash history after CheckSchedule or evaluation
        /// Writes a line with format: [<iteration>: <context>] <hashes sorted by schedule hash key>
        /// Context is either "Check" or "EvalAll"
        /// Only records if SimParameters.EnableHashTracking is true
        /// </summary>
        public static void RecordStateHashHistory(List<SystemSchedule> schedules, string context, double currentTime)
        {
            // Early return if hash tracking is disabled
            if (!SimParameters.EnableHashTracking)
                return;
                
            lock (_stateHashHistoryLock)
            {
                // Initialize file path if not set
                if (string.IsNullOrEmpty(_stateHashHistoryFilePath))
                {
                    string outputDir = SimParameters.OutputDirectory ?? Path.Combine(Utilities.DevEnvironment.RepoDirectory, "output");
                    InitializeStateHashHistoryFile(outputDir);
                }
                
                // Get iteration step
                int step = Scheduler.SchedulerStep;
                
                // Build dictionary of schedule hash -> state hash (sorted by schedule hash for determinism)
                var stateHashDict = new Dictionary<string, string>();
                foreach (var schedule in schedules)
                {
                    string scheduleHash = schedule.ScheduleInfo.ScheduleHash;
                    if (!string.IsNullOrEmpty(scheduleHash))
                    {
                        string stateHash = schedule.AllStates.StateHash;
                        if (!string.IsNullOrEmpty(stateHash))
                        {
                            stateHashDict[scheduleHash] = stateHash;
                        }
                    }
                }
                
                // Sort by schedule hash (deterministic ordering)
                var sortedEntries = stateHashDict.OrderBy(kvp => kvp.Key).ToList();
                var stateHashList = sortedEntries.Select(kvp => kvp.Value).ToList();
                
                // Format: [<iteration>: <context>] <state hashes space delimited>
                // Ensure consistent header width: [<4 chars>: <9 chars>] = 15 chars total
                string contextPadded = context.Length <= 9 ? context.PadRight(9) : context.Substring(0, 9);
                string iterationStr = step.ToString().PadLeft(4);
                string hashesStr = string.Join(" ", stateHashList);
                string line = $"[{iterationStr}: {contextPadded}] {hashesStr}";
                
                // Append to file (thread-safe via lock)
                if (!string.IsNullOrEmpty(_stateHashHistoryFilePath))
                {
                    File.AppendAllText(_stateHashHistoryFilePath, line + Environment.NewLine);
                }
            }
        }
        
        #endregion

        /// <summary>
        ///  Creates a new empty schedule with the given initial state.
        /// </summary>
        /// <param name="initialState"></param>
        public StateHistory(SystemState initialState)
        {
            InitialState = initialState;
            Events = new Stack<Event>();
        }
        /// <summary>
        /// Creates a new copy of a state history so that events can be added to it
        /// </summary>
        /// <param name="oldSchedule"></param>
        public StateHistory(StateHistory oldSchedule)
        {
            InitialState = oldSchedule.InitialState;
            Events = new Stack<Event>();
            int i;
            Event eit;
            Event[] copy = new Event[oldSchedule.Events.Count] ;
            oldSchedule.Events.CopyTo(copy, 0);
            for (i = oldSchedule.Events.Count - 1; i >= 0; i--)
            {
                eit = copy[i];
                Events.Push(eit);
            }
            // Copy state hash history from old schedule (preserve traceability)
            StateHashHistory = new Dictionary<(int Step, string ScheduleHash), string>(oldSchedule.StateHashHistory);
            //UpdateStateHistoryID(this, oldSchedule);
        }

        /// <summary>
        /// Creates a new assetSchedule from and old assetSchedule and a new Event
        /// </summary>
        /// <param name="oldSchedule"></param>
        /// <param name="newEvent"></param>
        public StateHistory(StateHistory oldSchedule, Event newEvent)
        {
            Stack<Event> temp = new Stack<Event>(oldSchedule.Events);
            Events = new Stack<Event>(temp);
            //Events = new Stack<Event>(oldSchedule.Events);
            InitialState = oldSchedule.InitialState;  //Should maybe be a deep copy -->not for this one
            Events.Push(newEvent);
            // Copy state hash history from old schedule (preserve traceability)
            StateHashHistory = new Dictionary<(int Step, string ScheduleHash), string>(oldSchedule.StateHashHistory);
        //    Asset = newAssetSched.Asset;
            //UpdateStateHistoryID(this, oldSchedule);
        }
        // public static void UpdateStateHistoryID(StateHistory newStartStateHistory, StateHistory oldStateHistory)
        // {
        //     if (oldStateHistory._stateHistoryID == "")
        //     {
        //         var prefix = ""; 
        //         for (int i = 0; i < Scheduler.SchedulerStep; i++)
        //         {
        //             if (i==0) { prefix += "0";  }
        //             else if (i>0) { prefix += ".0";  } 
        //         }
        //         newStartStateHistory._stateHistoryID = prefix + Scheduler._schedID.ToString();
        //         Scheduler._schedID++;
        //     }
        //     else
        //     {
        //         newStartStateHistory._stateHistoryID = $"{oldStateHistory._stateHistoryID}.{Scheduler._schedID.ToString()}";
        //         Scheduler._schedID++;
        //     }
        //}


        /// <summary>
        ///  Returns the last State in the schedule
        /// </summary>
        /// <returns></returns>
        public SystemState GetLastState()
        {
            if (!isEmpty()) 
            {
                return Events.Peek().State;
            }
            else
                return InitialState;
        }

        /// <summary>
        /// returns the last task in the schedule for a specific asset
        /// </summary>
        /// <returns></returns>
        public Task GetLastTask(Asset asset)
        {
            if (isEmpty() == false) //TODO: check that this is actually what we want to do.
            {
                return Events.Peek().GetAssetTask(asset);
            }
            else return null;
        }

        /// <summary>
        /// Return all the last tasks of all the assets in a dictionary of Asset, Task
        /// </summary>
        /// <returns></returns>
        public Dictionary<Asset, Task> GetLastTasks()
        {
            return GetLastEvent().Tasks;
        }

        /// <summary>
        /// Return the last event (last task of each asset in the system and the system state)
        /// </summary>
        /// <returns></returns>
        public Event GetLastEvent()
        {
            return Events.Peek();
        }

        /// <summary>
        /// Returns the number of times the specified task has been completed in this schedule for a specific asset
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public int timesCompletedTask(Asset asset, Task task)
        {
            int count = 0;
            KeyValuePair<Asset, Task> search = new KeyValuePair<Asset, Task>(asset, task);
            foreach(Event eit in Events)
            {
                foreach(KeyValuePair<Asset, Task> pair in eit.Tasks)
                {
                    if (pair.Equals(search))
                        count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Returns the total number of times a task has been completed in the schedule
        /// across ALL events and ALL assets. This counts the actual occurrences of the task,
        /// not just the number of events containing the task.
        /// </summary>
        /// <param name="task"></param>
        /// <returns>Total count of task occurrences across all assets and events</returns>
        public int timesCompletedTask(Task task)
        {
            int count = 0;
            foreach (Event evt in Events)
            {
                // Count each occurrence of the task across all assets in this event
                foreach (var taskInEvent in evt.Tasks.Values)
                {
                    if (taskInEvent == task)
                        count++;
                }
            }
            return count;
        }

        /// <summary>
        ///  Returns the number of events in the schedule for ALL assets
        /// </summary>
        /// <returns></returns>
        public int size()
        {
            return Events.Count;
        }

        /// <summary>
        /// Returns the number of events in the schedule for a specific asset
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        public int size(Asset asset)
        {
            int count = 0;
            foreach (Event eit in Events)
            {
                if (eit.Tasks.ContainsKey(asset))
                    count++;
            }
            return count;
        }
        /// <summary>
        /// Returns true if the specified asset doesn't have a task (the asset isn't scheduled)
        /// </summary>
        /// <returns></returns>
        public bool isEmpty(Asset asset)
        {
            foreach(Event eit in Events)
            {
                if (eit.Tasks.ContainsKey(asset)) //something has been scheduled
                    return false;}
            return true;
        }

        /// <summary>
        /// returns true is no assets have any events
        /// </summary>
        /// <returns></returns>
        public bool isEmpty()
        {
            if (Events.Count == 0)
                return true;
            return false;
        }
    }
}