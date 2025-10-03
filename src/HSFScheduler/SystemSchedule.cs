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
using Microsoft.CodeAnalysis.CSharp.Syntax; // error CS0104: 'Task' is an ambiguous reference between 'MissionElements.Task' and 'System.Threading.Tasks.Task'

namespace HSFScheduler
{
    public class SystemSchedule
    {
        #region Attributes
        public string Name = ""; 
        public StateHistory AllStates; //pop never gets used so just use list
        public double ScheduleValue;
        #endregion

        #region Constructors
        public SystemSchedule(SystemState initialstates) 
        {
            ScheduleValue = 0;
            AllStates = new StateHistory(initialstates);
        }
        public SystemSchedule(SystemState initialstates, string name) 
        {
            ScheduleValue = 0;
            Name = name;
            AllStates = new StateHistory(initialstates);
        }
        public SystemSchedule(StateHistory allStates)
        {
            AllStates = new StateHistory(allStates);
        }
        public SystemSchedule(StateHistory allStates, string name)
        {
            AllStates = new StateHistory(allStates);
            Name = name; 
        }
        public SystemSchedule(SystemSchedule oldSchedule, Event emptyEvent)
        {
            AllStates = new StateHistory(oldSchedule.AllStates);
            AllStates.Events.Push(emptyEvent);
        }
        public SystemSchedule(SystemSchedule oldSchedule, Event emptyEvent,string name)
        {
            AllStates = new StateHistory(oldSchedule.AllStates);
            AllStates.Events.Push(emptyEvent);
            Name = name;
        }

        public SystemSchedule(StateHistory oldStates, Stack<Access> newAccessStack, double currentTime)
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
            // Track which tasks we've already checked to avoid double-counting
            HashSet<Task> checkedTasks = new HashSet<Task>();

	        foreach(var access in newAccessList)
            {
                // This is where event timing gets enforced. 
                if (!AllStates.isEmpty(access.Asset)) // Ensure there is an event with the accessible asset. Otherwise skip
                {
                    if (AllStates.GetLastEvent().GetEventEnd(access.Asset) > currentTime)
                        return false;
                }
                
                // Check Access times here?  

                // This is where the task count gets enforced. 
                if (access.Task != null && !checkedTasks.Contains(access.Task))
                {
                    checkedTasks.Add(access.Task);
                    
                    // Count how many times this task has been completed historically (across all assets -- All Events)
                    int historicalCount = AllStates.timesCompletedTask(access.Task);
                    
                    // Count how many times we're adding it in this newAccessList (across all assets -- newAccessList)
                    int newCount = 0;
                    foreach(var a in newAccessList)
                    {
                        if (a.Task == access.Task)
                            newCount++;
                    }
                    
                    // Reject if adding these new instances would exceed the limit
                    if (historicalCount + newCount > access.Task.MaxTimesToPerform)
                        return false; // Return false (cant add tasks) if the task has been performed too many times. 
                }
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
                            Console.WriteLine("idk"); //TERRIBLE!

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

            System.IO.File.WriteAllText(scheduleWritePath + "\\" + fileName + ".csv", csv.ToString());
            csv.Clear();
        }
    }
}