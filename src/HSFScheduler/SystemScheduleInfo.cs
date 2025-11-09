// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
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
        public static void PrintAllSchedulesSummary(List<SystemSchedule> schedules, bool showAssetTaskDetails = false, bool overRideConsoleLogging = false)
        {
            string statusMessage = $"Scheduler Status: {100 * Scheduler.CurrentTime / SimParameters.SimEndSeconds:F}% done; Generated: {Scheduler._SchedulesGenerated} " +
                                $"| Carried Over: {Scheduler._SchedulesCarriedOver} | Cropped: {Scheduler._SchedulesCropped} | Total: {schedules.Count}";
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine($"SCHEDULE SUMMARY - Current Scheduler Step: {Scheduler.SchedulerStep} | Total Schedules: {schedules.Count}");
            Console.WriteLine($"{statusMessage}");
            Console.WriteLine(new string('=', 80));
            
            if (!SchedParameters.ConsoleLogging && !overRideConsoleLogging)
            { return; } // return eraly if verbose is not set. }
            
            // Otherwise continue printing:
            if (schedules.Count == 0)
            {
                Console.WriteLine("No schedules to display.");
                return;
            }
            
            // Print time step headers
            Console.WriteLine("Time:  " + GenerateTimeStepHeader());
            Console.WriteLine("Events:" + GenerateEventHeader(schedules));
            Console.WriteLine(new string('-', 80));
            
            // Print each schedule's details
            for (int i = 0; i < schedules.Count; i++)
            {
                var schedule = schedules[i];
                // Console.WriteLine($"Schedule #{i + 1,2}: Events={schedule.AllStates.Events.Count,2} | Value={schedule.ScheduleValue,8:F2} | Pattern: {schedule.ScheduleInfo.EventString}");
                Console.WriteLine($"Schedule {schedule._scheduleID}: Events={schedule.AllStates.Events.Count,2} | Value={schedule.ScheduleValue,8:F2} | Pattern: {schedule.ScheduleInfo.EventString}");
                if (showAssetTaskDetails)
                {
                    PrintAssetTaskDetails(schedule);
                }
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
