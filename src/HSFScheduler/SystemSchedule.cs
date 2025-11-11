// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Utilities;
using MissionElements;
using UserModel;
using Task = MissionElements.Task;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Scripting.Interpreter;
using Microsoft.CodeAnalysis; // error CS0104: 'Task' is an ambiguous reference between 'MissionElements.Task' and 'System.Threading.Tasks.Task'

namespace HSFScheduler
{
    public class SystemSchedule
    {
        #region Attributes
        public string Name = "";
        public string _scheduleID{ get; set; } = "";
        public StateHistory AllStates; //pop never gets used so just use list
        public double ScheduleValue;
        #endregion
        
        # region Debug / Private Attributes & Methods
        private int numEvents { get; set; }
        private string EventExistString { get; set; }
        
        /// <summary>
        /// Debug and visualization information for this schedule
        /// </summary>
        public SystemScheduleInfo ScheduleInfo { get; private set; }

        public void UpdateInfoStrings()
        {
            numEvents = this.AllStates.Events.Count();
            EventExistString = this.ScheduleInfo.EventString; 
        }

        public string UpdateScheduleID(SystemSchedule oldSystemSchedule)
        {
            string prefix = "";
            if (oldSystemSchedule.Name.ToLower().Contains("empty")) // This is the empty schedule
            {
                for (int i = 0; i < Scheduler.SchedulerStep; i++)
                {
                    prefix += "0.";
                }
                _scheduleID = prefix + Scheduler._schedID.ToString();
            }
            else
            {
                _scheduleID = oldSystemSchedule._scheduleID + "." + Scheduler._schedID.ToString(); 
            }
            // else
            // {
            //     int numPeriods = oldSystemScheduleID.Count(c => c == '.');
            //     for (int i = 0; i < Scheduler.SchedulerStep - 1 - numPeriods; i++)
            //     {
            //         prefix += ".0";
            //     }
            //     _scheduleID = oldSystemScheduleID + prefix + "." + Scheduler._schedID.ToString();
            // }
            Scheduler._schedID++;
            return _scheduleID; 
        }
        #endregion

        #region Constructors
        public SystemSchedule(SystemState initialstates, string name)
        {
            ScheduleValue = 0;
            Name = name;
            AllStates = new StateHistory(initialstates);
            ScheduleInfo = new SystemScheduleInfo();
            UpdateInfoStrings();
        }


        public SystemSchedule(StateHistory oldStates, Stack<Access> newAccessStack, double currentTime, SystemSchedule oldSystemSchedule)
        {

            Dictionary<Asset, Task> tasks = new Dictionary<Asset, Task>();
            Dictionary<Asset, double> taskStarts = new Dictionary<Asset, double>();
            Dictionary<Asset, double> taskEnds = new Dictionary<Asset, double>();
            Dictionary<Asset, double> eventStarts = new Dictionary<Asset, double>();
            Dictionary<Asset, double> eventEnds = new Dictionary<Asset, double>();

            // Calculate nextStep (event end time)
            double nextStep = currentTime + SimParameters.SimStepSeconds;

            foreach (var access in newAccessStack)
            {
                // Exception handling for invalid access times
                // These Accesses should not come in this form, unless the future-implemenation of scripted-access-generation is 
                // implemented incorrectly. A test confirming Aeolus pregenAccess validation will be implemented as part of this 
                // Scheduling NUnit test suite. --jebeals 09/23/25.
                if (access.AccessStart >= access.AccessEnd)
                {
                    throw new InvalidOperationException(
                        $"Invalid access time range for asset {access.Asset}. " +
                        $"AccessStart ({access.AccessStart}) must be less than AccessEnd ({access.AccessEnd})"
                    );
                }

                if (access.AccessStart < currentTime && access.AccessEnd < currentTime)
                {
                    throw new InvalidOperationException(
                        $"Access times are both before current time for asset {access.Asset}. " +
                        $"AccessStart ({access.AccessStart}) and AccessEnd ({access.AccessEnd}) are both before currentTime ({currentTime})"
                    );
                }

                if (access.AccessStart > nextStep && access.AccessEnd > nextStep)
                {
                    throw new InvalidOperationException(
                        $"Access times are both after event end time for asset {access.Asset}. " +
                        $"AccessStart ({access.AccessStart}) and AccessEnd ({access.AccessEnd}) are both after eventEnd ({nextStep})"
                    );
                }
                // End Exception Handling

                // Start Event/Task Start/End Time assignment logic:
                // Note: For this iteration of HSF, we hve decided that "events" are the occurences of the fundamental timestep,
                //        and tasks embedded within start at the earliest access time within the event, and end at the latest
                //        available access time within the event. This may be changed in the future to allow tasks to span multiple
                //        timesteps, but they are retricted to being within one event window for now. --jebeals 09/23/25
                if (access.Task != null)
                {
                    // EventStart should always be set to currentTime
                    eventStarts.Add(access.Asset, currentTime);

                    // EventEnd should always be set to nextStep (currentTime + SimStepSeconds)
                    eventEnds.Add(access.Asset, nextStep);

                    // TaskStart should be set to earliest time based on available access
                    // If access is before eventStart, it should be set to event start
                    // If access is after, it should be set to the access start
                    double taskStart;
                    if (access.AccessStart <= currentTime)
                    {
                        // Access starts before or at event start - use event start
                        taskStart = currentTime;
                    }
                    else
                    {
                        // Access starts after event start - use access start
                        taskStart = access.AccessStart;
                    }
                    taskStarts.Add(access.Asset, taskStart);

                    // TaskEnd should be set to the latest possible access time within the event
                    // If the access time extends past the eventEnd, then taskEnd should be set to EventEnd
                    // If access ends before then it should be set to accessEnd time
                    double taskEnd;
                    if (access.AccessEnd >= nextStep)
                    {
                        // Access extends past event end - use event end
                        taskEnd = nextStep;
                    }
                    else
                    {
                        // Access ends before event end - use access end
                        taskEnd = access.AccessEnd;
                    }
                    taskEnds.Add(access.Asset, taskEnd);

                    tasks.Add(access.Asset, access.Task);
                }
                else
                {
                    // For null tasks, set everything to current time
                    taskStarts.Add(access.Asset, currentTime);
                    taskEnds.Add(access.Asset, currentTime);
                    tasks.Add(access.Asset, null);
                    eventStarts.Add(access.Asset, currentTime);
                    eventEnds.Add(access.Asset, nextStep);
                }
            }

            // Add all of the new objects to the Scheduler and program at large. 
            Event eventToAdd = new Event(tasks, new SystemState(oldStates.GetLastState())); //all references
            eventToAdd.SetEventEnd(eventEnds);
            eventToAdd.SetTaskEnd(taskEnds);
            eventToAdd.SetEventStart(eventStarts);
            eventToAdd.SetTaskStart(taskStarts);
            AllStates = new StateHistory(oldStates, eventToAdd);

            // Informational Use Only:
            ScheduleInfo = new SystemScheduleInfo(AllStates, Scheduler.SchedulerStep);
            _scheduleID = UpdateScheduleID(oldSystemSchedule);
            UpdateInfoStrings();

        }
                
        #endregion
        
        /// <summary>
        /// Determine if a task can be added to a schedule at the new start time
        /// </summary>
        /// <param name="newAccessList"></param>
        /// <param name="newTaskStartTime"></param>
        /// <returns></returns>
        public bool CanAddTasks(Stack<Access> newAccessList, double currentTime)
        {


            foreach (var access in newAccessList)
            {
                // This is where event timing gets enforced. 
                if (!AllStates.isEmpty(access.Asset)) // Ensure there is an event with the accessible asset. Otherwise skip
                {
                    if (AllStates.GetLastEvent().GetEventEnd(access.Asset) > currentTime)
                        return false;
                }
            } // Otherwise continue on to check if any tasks have been performed too many times...

            if (Scheduler.SchedulerStep >= 1) {
                int a =4; // breakpoint for Debugging
            }
            // Task Completion Counting Logic:
            HashSet<Task> checkedTasks = new HashSet<Task>(); //  Used to track which tasks we've already checked to avoid double-counting
            Dictionary<Task, int> taskCountDict = new Dictionary<Task, int>(); // Used to track the total number of times each task has been performed (across all assets and events).
            foreach(var access in newAccessList)
            {
                if (!checkedTasks.Contains(access.Task)) // This prevents double-counting the same task (as we count across all events and assets therewithin.)
                {
                    // This is the first time we've seen this task, so add it to the checked tasks set.
                    checkedTasks.Add(access.Task);
                    int historicalCount = AllStates.timesCompletedTask(access.Task); // Count the number of times this task has been performed historically (across all assets and events)
                    taskCountDict.Add(access.Task, historicalCount + 1); // Add the task to the dictionary with the total number of times it has been performed (including this event's asset).
                }
                else
                {
                    // Another asset is performing the same task in this event, so increment the count one further.
                    taskCountDict[access.Task] += 1;
                }
            }

            // Task Completion Counting Logic Enforcement: Check if any task has been performed too many times.
            foreach(var taskCount in taskCountDict)
            {
                // Here taskCount.Value is the total number of times the task has been performed; taskCount.Key is the task itself.
                if (taskCount.Value > taskCount.Key.MaxTimesToPerform) // This task has been performed more times than the maximum allowed.
                    return false; // If the task has been performed more times than the maximum allowed, return false.
            }

	        return true; // otherwise return true! 

        }

        #region Accessors
        public int GetTotalNumEvents()
        {
            return AllStates.size();
        }

        public SystemState GetSubsystemNewState()
        {
            return AllStates.GetLastState();
        }

        public Task GetSubsytemNewTask(Asset asset)
        {
            return AllStates.GetLastTask(asset);
        }

        //public StateHistory GetStateHistory(Asset asset)
        //{
        //    return AllStates.Find(item => item.Asset == asset);
        //}

        public double GetLastTaskStart()
        {
            double lasttime = 0;
            foreach (KeyValuePair<Asset, double> assetTaskStarts in AllStates.GetLastEvent().TaskStarts)
            {
                lasttime = lasttime > assetTaskStarts.Value ? lasttime : assetTaskStarts.Value;
            }
            return lasttime;
        }

        public SystemState GetEndState()
        {
            return AllStates.GetLastState();
        }
        #endregion 

        /// <summary>
        /// Determine if the first schedule value is greater than the second
        /// </summary>
        /// <param name="elem1"></param>
        /// <param name="elem2"></param>
        /// <returns></returns>
        bool SchedGreater(SystemSchedule elem1, SystemSchedule elem2)
        {
            return elem1.ScheduleValue > elem2.ScheduleValue;
        }

        /// <summary>
        /// Utilitiy method to write the schedule to csv file
        /// </summary>
        /// <param name="schedule"></param>
        /// <param name="scheduleWritePath"></param>
        /// <summary>
        /// Write state data for multiple schedules in clean CSV format
        /// Creates: 1) TopSchedule_{value}_{asset}_Data.csv (best schedule, one per asset)
        ///          2) additional_schedule_data/{rank}_Schedule_{value}_{schedID}.csv (top X schedules, all assets)
        /// </summary>
        public static void WriteScheduleData(List<SystemSchedule> schedules, string outputDir, int maxSchedules = 5)
        {
            if (schedules == null || schedules.Count == 0) return;
            
            // Sort by value descending (best first)
            var sortedSchedules = schedules.OrderByDescending(s => s.ScheduleValue).ToList();
            
            int numToWrite = Math.Min(sortedSchedules.Count, maxSchedules == int.MaxValue ? sortedSchedules.Count : maxSchedules);
            string dataDir = Path.Combine(outputDir, "data");
            Directory.CreateDirectory(dataDir);
            
            // Get all unique assets from all schedules
            var allAssets = new HashSet<string>();
            foreach (var schedule in sortedSchedules.Take(numToWrite))
            {
                foreach (var evt in schedule.AllStates.Events)
                {
                    foreach (var assetTaskPair in evt.Tasks)
                    {
                        allAssets.Add(assetTaskPair.Key.Name);
                    }
                }
            }
            
            // 1. ALWAYS write top schedule data, one CSV per asset
            if (sortedSchedules.Count > 0)
            {
                var topSchedule = sortedSchedules[0];
                int topValue = (int)topSchedule.ScheduleValue;  // Truncate to integer
                
                foreach (var assetName in allAssets.OrderBy(a => a))
                {
                    WriteTopScheduleAssetCSV(topSchedule, assetName, dataDir, topValue);
                }
            }
            
            // 2. Write additional schedule data (rank 0 to X) in subdirectory
            if (numToWrite > 0)
            {
                string additionalDir = Path.Combine(dataDir, "additional_schedule_data");
                Directory.CreateDirectory(additionalDir);
                
                for (int rank = 0; rank < numToWrite; rank++)
                {
                    var schedule = sortedSchedules[rank];
                    WriteRankedScheduleCSV(schedule, rank, additionalDir, allAssets.ToList());
                }
            }
        }
        
        /// <summary>
        /// Write top schedule data for a single asset: TopSchedule_{value}_{asset}_Data.csv
        /// Format: Time, state1, state2, ... (asset data only, headers are state names)
        /// </summary>
        private static void WriteTopScheduleAssetCSV(SystemSchedule schedule, string assetName, string dataDir, int scheduleValue)
        {
            var csv = new StringBuilder();
            var stateData = ExtractAllStatesWithTime(schedule);
            
            if (!stateData.ContainsKey(assetName)) return;
            
            var assetData = stateData[assetName];
            if (assetData.Count == 0) return;
            
            // Build header: Time, then all state variables for this asset
            csv.Append("Time");
            foreach (var stateVar in assetData.Keys.OrderBy(s => s))
            {
                csv.Append($",{stateVar}");
            }
            csv.AppendLine();
            
            // Collect all unique time steps for this asset
            var allTimes = new SortedSet<double>();
            foreach (var stateVarData in assetData.Values)
            {
                foreach (var time in stateVarData.Keys)
                {
                    allTimes.Add(time);
                }
            }
            
            // Write data rows (one row per time step)
            foreach (var time in allTimes)
            {
                csv.Append($"{time}");
                
                foreach (var stateVar in assetData.Keys.OrderBy(s => s))
                {
                    var timeValuePairs = assetData[stateVar];
                    // Find value at or before this time
                    double value = 0;
                    foreach (var kvp in timeValuePairs.Where(t => t.Key <= time).OrderByDescending(t => t.Key))
                    {
                        value = kvp.Value;
                        break;
                    }
                    csv.Append($",{value}");
                }
                csv.AppendLine();
            }
            
            string fileName = $"TopSchedule_value{scheduleValue}_{assetName}_Data.csv";
            File.WriteAllText(Path.Combine(dataDir, fileName), csv.ToString());
        }
        
        /// <summary>
        /// Write ranked schedule data for all assets: {rank}_Schedule_{value}_{schedID}.csv
        /// Format: Asset, Time, state1, state2, ... (all assets, asset as column)
        /// </summary>
        private static void WriteRankedScheduleCSV(SystemSchedule schedule, int rank, string additionalDir, List<string> allAssets)
        {
            var csv = new StringBuilder();
            var stateData = ExtractAllStatesWithTime(schedule);
            
            if (stateData.Count == 0) return;
            
            // Build header: Asset, Time, then all state variables across all assets
            var allStateVars = new HashSet<string>();
            foreach (var assetData in stateData.Values)
            {
                foreach (var stateVar in assetData.Keys)
                {
                    allStateVars.Add(stateVar);
                }
            }
            
            csv.Append("Asset,Time");
            foreach (var stateVar in allStateVars.OrderBy(s => s))
            {
                csv.Append($",{stateVar}");
            }
            csv.AppendLine();
            
            // Collect all unique time steps across all assets
            var allTimes = new SortedSet<double>();
            foreach (var assetData in stateData.Values)
            {
                foreach (var stateVarData in assetData.Values)
                {
                    foreach (var time in stateVarData.Keys)
                    {
                        allTimes.Add(time);
                    }
                }
            }
            
            // Write data rows (one row per asset per time step)
            foreach (var asset in allAssets.OrderBy(a => a))
            {
                if (!stateData.ContainsKey(asset)) continue;
                
                foreach (var time in allTimes)
                {
                    csv.Append($"{asset},{time}");
                    
                    foreach (var stateVar in allStateVars.OrderBy(s => s))
                    {
                        // Get value for this state at this time (or repeat last value)
                        if (stateData[asset].ContainsKey(stateVar))
                        {
                            var timeValuePairs = stateData[asset][stateVar];
                            // Find value at or before this time
                            double value = 0;
                            foreach (var kvp in timeValuePairs.Where(t => t.Key <= time).OrderByDescending(t => t.Key))
                            {
                                value = kvp.Value;
                                break;
                            }
                            csv.Append($",{value}");
                        }
                        else
                        {
                            csv.Append(",");  // No data for this state/asset combo
                        }
                    }
                    csv.AppendLine();
                }
            }
            
            int scheduleValue = (int)schedule.ScheduleValue;
            string fileName = $"{rank}_Schedule_value{scheduleValue}_{schedule._scheduleID.Replace('.', '-')}.csv";
            File.WriteAllText(Path.Combine(additionalDir, fileName), csv.ToString());
        }
        
        private static void WriteScheduleFinalCSV(SystemSchedule schedule, string dataDir, List<string> allAssets)
        {
            var csv = new StringBuilder();
            var stateData = ExtractAllStatesWithTime(schedule);
            
            if (stateData.Count == 0) return;
            
            // Build header: Asset, Time, then all state variables
            var allStateVars = new HashSet<string>();
            foreach (var asset in allAssets)
            {
                if (stateData.ContainsKey(asset))
                {
                    foreach (var stateVar in stateData[asset].Keys)
                    {
                        allStateVars.Add(stateVar);
                    }
                }
            }
            
            csv.Append("Asset,Time");
            foreach (var stateVar in allStateVars.OrderBy(s => s))
            {
                csv.Append($",{stateVar}");
            }
            csv.AppendLine();
            
            // Collect all unique time steps across all assets
            var allTimes = new SortedSet<double>();
            foreach (var assetData in stateData.Values)
            {
                foreach (var stateVarData in assetData.Values)
                {
                    foreach (var time in stateVarData.Keys)
                    {
                        allTimes.Add(time);
                    }
                }
            }
            
            // Write data rows (one row per asset per time step)
            foreach (var asset in allAssets.OrderBy(a => a))
            {
                if (!stateData.ContainsKey(asset)) continue;
                
                foreach (var time in allTimes)
                {
                    csv.Append($"{asset},{time}");
                    
                    foreach (var stateVar in allStateVars.OrderBy(s => s))
                    {
                        // Get value for this state at this time (or repeat last value)
                        if (stateData[asset].ContainsKey(stateVar))
                        {
                            var timeValuePairs = stateData[asset][stateVar];
                            // Find value at or before this time
                            double value = 0;
                            foreach (var kvp in timeValuePairs.Where(t => t.Key <= time).OrderByDescending(t => t.Key))
                            {
                                value = kvp.Value;
                                break;
                            }
                            csv.Append($",{value}");
                        }
                        else
                        {
                            csv.Append(",");  // No data for this state/asset combo
                        }
                    }
                    csv.AppendLine();
                }
            }
            
            string fileName = $"ScheduleFinal_{schedule._scheduleID.Replace('.', '-')}.csv";
            File.WriteAllText(Path.Combine(dataDir, fileName), csv.ToString());
        }
        
        private static void WriteAssetSchedulesCSV(List<SystemSchedule> schedules, string assetName, string dataDir)
        {
            var csv = new StringBuilder();
            
            // Build header: ScheduleID, Value, Time, then all state variables for this asset
            var allStateVars = new HashSet<string>();
            foreach (var schedule in schedules)
            {
                var stateData = ExtractAllStatesWithTime(schedule);
                if (stateData.ContainsKey(assetName))
                {
                    foreach (var stateVar in stateData[assetName].Keys)
                    {
                        allStateVars.Add(stateVar);
                    }
                }
            }
            
            csv.Append("ScheduleID,Value,Time");
            foreach (var stateVar in allStateVars.OrderBy(s => s))
            {
                csv.Append($",{stateVar}");
            }
            csv.AppendLine();
            
            // Write data (one row per schedule per time step)
            foreach (var schedule in schedules.OrderByDescending(s => s.ScheduleValue))
            {
                var stateData = ExtractAllStatesWithTime(schedule);
                if (!stateData.ContainsKey(assetName)) continue;
                
                // Collect all times for this schedule/asset
                var allTimes = new SortedSet<double>();
                foreach (var stateVarData in stateData[assetName].Values)
                {
                    foreach (var time in stateVarData.Keys)
                    {
                        allTimes.Add(time);
                    }
                }
                
                foreach (var time in allTimes)
                {
                    csv.Append($"{schedule._scheduleID},{schedule.ScheduleValue},{time}");
                    
                    foreach (var stateVar in allStateVars.OrderBy(s => s))
                    {
                        if (stateData[assetName].ContainsKey(stateVar))
                        {
                            var timeValuePairs = stateData[assetName][stateVar];
                            // Find value at or before this time
                            double value = 0;
                            foreach (var kvp in timeValuePairs.Where(t => t.Key <= time).OrderByDescending(t => t.Key))
                            {
                                value = kvp.Value;
                                break;
                            }
                            csv.Append($",{value}");
                        }
                        else
                        {
                            csv.Append(",");
                        }
                    }
                    csv.AppendLine();
                }
            }
            
            string fileName = $"{assetName}_{schedules.Count}scheds_data.csv";
            File.WriteAllText(Path.Combine(dataDir, fileName), csv.ToString());
        }
        
        /// <summary>
        /// Extract all state data with time from a schedule
        /// Returns: Dictionary[AssetName][StateVariableName][Time] = Value
        /// </summary>
        private static Dictionary<string, Dictionary<string, SortedList<double, double>>> ExtractAllStatesWithTime(SystemSchedule schedule)
        {
            var result = new Dictionary<string, Dictionary<string, SortedList<double, double>>>();
            
            SystemState sysState = null;
            if (schedule.AllStates.Events.Count != 0)
            {
                sysState = schedule.AllStates.Events.Peek().State;
            }
            
            while (sysState != null)
            {
                // Extract all double states (most common: DOD, databuffer, pixels, etc.)
                foreach (var kvpDoubleProfile in sysState.Ddata)
                {
                    // Parse state variable name: "asset1.depthofdischarge" → asset="asset1", var="depthofdischarge"
                    string fullName = kvpDoubleProfile.Key.VariableName;
                    string[] parts = fullName.Split('.');
                    if (parts.Length < 2) continue;
                    
                    string assetName = parts[0];
                    string stateName = string.Join(".", parts.Skip(1));  // Rejoin if there are more dots
                    
                    if (!result.ContainsKey(assetName))
                        result[assetName] = new Dictionary<string, SortedList<double, double>>();
                    
                    if (!result[assetName].ContainsKey(stateName))
                        result[assetName][stateName] = new SortedList<double, double>();
                    
                    foreach (var data in kvpDoubleProfile.Value.Data)
                    {
                        if (!result[assetName][stateName].ContainsKey(data.Key))
                            result[assetName][stateName].Add(data.Key, data.Value);
                    }
                }
                
                // TODO: Add Idata, Bdata, Mdata, Qdata extraction if needed
                // For now, focusing on Ddata (covers Power DOD, SSDR buffer, EOSensor pixels, etc.)
                
                sysState = sysState.PreviousState;
            }
            
            return result;
        }

        public static void WriteSchedule(SystemSchedule schedule, String scheduleWritePath) //TODO: Unit Test.
        {
            var csv = new StringBuilder();
            Dictionary<StateVariableKey<double>, SortedList<double, double>> stateTimeDData = new Dictionary<StateVariableKey<double>, SortedList<double, double>>();
            Dictionary<StateVariableKey<int>, SortedList<double, int>> stateTimeIData = new Dictionary<StateVariableKey<int>, SortedList<double, int>>();
            Dictionary<StateVariableKey<bool>, SortedList<double, bool>> stateTimeBData = new Dictionary<StateVariableKey<bool>, SortedList<double, bool>>(); // need 0s and 1 for matlab to read in csv
            Dictionary<StateVariableKey<Matrix<double>>, SortedList<double, Matrix<double>>> stateTimeMData = new Dictionary<StateVariableKey<Matrix<double>>, SortedList<double, Matrix<double>>>();
            Dictionary<StateVariableKey<Quaternion>, SortedList<double, Quaternion>> stateTimeQData = new Dictionary<StateVariableKey<Quaternion>, SortedList<double, Quaternion>>();
            string stateTimeData = "Time,";
            string stateData = "";
            csv.Clear();

            SystemState sysState=null;
            if (schedule.AllStates.Events.Count!= 0)
            {
                sysState = schedule.AllStates.Events.Peek().State;
            }
            

            while(sysState != null) { 
                foreach (var kvpDoubleProfile in sysState.Ddata)
                    foreach (var data in kvpDoubleProfile.Value.Data)
                        if (!stateTimeDData.ContainsKey(kvpDoubleProfile.Key))
                        {
                            var lt = new SortedList<double, double>();
                            lt.Add(data.Key, data.Value);
                            stateTimeDData.Add(kvpDoubleProfile.Key, lt);
                        }
                        else if (!stateTimeDData[kvpDoubleProfile.Key].ContainsKey(data.Key))
                            stateTimeDData[kvpDoubleProfile.Key].Add(data.Key, data.Value);
                        else
                            //Console.WriteLine("idk"); //TERRIBLE!
                            Console.Write(""); // pass this up for now

                foreach (var kvpIntProfile in sysState.Idata)
                    foreach (var data in kvpIntProfile.Value.Data)
                        if (!stateTimeIData.ContainsKey(kvpIntProfile.Key))
                        {
                            var lt = new SortedList<double, int>();
                            lt.Add(data.Key, data.Value);
                            stateTimeIData.Add(kvpIntProfile.Key, lt);
                        }
                        else if (!stateTimeIData[kvpIntProfile.Key].ContainsKey(data.Key))
                            stateTimeIData[kvpIntProfile.Key].Add(data.Key, data.Value);

                foreach (var kvpBoolProfile in sysState.Bdata)
                    foreach (var data in kvpBoolProfile.Value.Data)
                        if (!stateTimeBData.ContainsKey(kvpBoolProfile.Key))
                        {
                            var lt = new SortedList<double, bool>();
                            lt.Add(data.Key, (data.Value)); //convert to int for matlab to read in for csv
                            stateTimeBData.Add(kvpBoolProfile.Key, lt);
                        }
                        else if (!stateTimeBData[kvpBoolProfile.Key].ContainsKey(data.Key))
                            stateTimeBData[kvpBoolProfile.Key].Add(data.Key, data.Value);

                foreach (var kvpMatrixProfile in sysState.Mdata)
                    foreach (var data in kvpMatrixProfile.Value.Data)
                        if (!stateTimeMData.ContainsKey(kvpMatrixProfile.Key))
                        {
                            var lt = new SortedList<double, Matrix<double>>();
                            lt.Add(data.Key, data.Value);
                            stateTimeMData.Add(kvpMatrixProfile.Key, lt);
                        }
                        else if (!stateTimeMData[kvpMatrixProfile.Key].ContainsKey(data.Key))
                            stateTimeMData[kvpMatrixProfile.Key].Add(data.Key, data.Value);
                foreach (var kvpQuatProfile in sysState.Qdata)
                    foreach (var data in kvpQuatProfile.Value.Data)
                        if (!stateTimeQData.ContainsKey(kvpQuatProfile.Key))
                        {
                            var lt = new SortedList<double, Quaternion>();
                            lt.Add(data.Key, data.Value);
                            stateTimeQData.Add(kvpQuatProfile.Key, lt);
                        }
                        else if (!stateTimeQData[kvpQuatProfile.Key].ContainsKey(data.Key))
                            stateTimeQData[kvpQuatProfile.Key].Add(data.Key, data.Value);
                sysState = sysState.PreviousState;
            }

            System.IO.Directory.CreateDirectory(scheduleWritePath);

            foreach(var list in stateTimeDData)
                writeStateVariable(list, scheduleWritePath);

            foreach(var list in stateTimeIData)
                writeStateVariable(list, scheduleWritePath);

            foreach (var list in stateTimeBData)
                writeStateVariable(list, scheduleWritePath);

            foreach (var list in stateTimeMData)
                writeStateVariable(list, scheduleWritePath);
            foreach (var list in stateTimeQData)
                writeStateVariable(list, scheduleWritePath);
        }
        
        /// <summary>
        /// Write out all the state variables in the schedule to file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="scheduleWritePath"></param>
        static void writeStateVariable<T>(KeyValuePair<StateVariableKey<T>, SortedList<double, T>> list, string scheduleWritePath) //TODO: Unit Test.
        {
            var csv = new StringBuilder();
            string fileName = list.Key.VariableName;

            string invalidChars = "";

            foreach (char c in System.IO.Path.GetInvalidPathChars())
                invalidChars += c;

            invalidChars += "(" + ")" + "/" + ".";

            foreach (char c in invalidChars)
                fileName = fileName.Replace(c, '_');

            csv.AppendLine("time" + "," + fileName);
            foreach (var k in list.Value)
                csv.AppendLine(k.Key + "," + k.Value);

            System.IO.File.WriteAllText(Path.Combine(scheduleWritePath, fileName + ".csv"), csv.ToString());
            csv.Clear();
        }
    }
}