// Copyright (c) 2025 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using System;
using HSFSystem;
using MissionElements;
using UserModel;
using Utilities;
using HSFUniverse;
using Newtonsoft.Json.Linq;

namespace HSFSystem
{
    /// <summary>
    /// Test subsystem that attempts to manipulate task start/end times
    /// Used to verify architectural constraints on time mutability
    /// </summary>
    public class TaskTimeManipulatorSubsystem : Subsystem
    {
        // Parameters: how much to shift times
        private double _taskStartShift;
        private double _taskEndShift;
        private double _eventStartShift;
        private double _eventEndShift;
        
        public TaskTimeManipulatorSubsystem(JObject subJson, Asset asset) : base(subJson, asset)
        {
            this.GetParameterByName<double>(subJson, "taskStartShift", out _taskStartShift);
            this.GetParameterByName<double>(subJson, "taskEndShift", out _taskEndShift);
            this.GetParameterByName<double>(subJson, "eventStartShift", out _eventStartShift);
            this.GetParameterByName<double>(subJson, "eventEndShift", out _eventEndShift);
        }
        
        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            // Get current times
            double currentTaskStart = proposedEvent.GetTaskStart(Asset);
            double currentTaskEnd = proposedEvent.GetTaskEnd(Asset);
            double currentEventStart = proposedEvent.GetEventStart(Asset);
            double currentEventEnd = proposedEvent.GetEventEnd(Asset);
            
            // Modify task times
            proposedEvent.SetTaskStart(new System.Collections.Generic.Dictionary<Asset, double> 
            { 
                { Asset, currentTaskStart + _taskStartShift } 
            });
            
            proposedEvent.SetTaskEnd(new System.Collections.Generic.Dictionary<Asset, double> 
            { 
                { Asset, currentTaskEnd + _taskEndShift } 
            });
            
            // Modify event times
            proposedEvent.SetEventStart(new System.Collections.Generic.Dictionary<Asset, double> 
            { 
                { Asset, currentEventStart + _eventStartShift } 
            });
            
            proposedEvent.SetEventEnd(new System.Collections.Generic.Dictionary<Asset, double> 
            { 
                { Asset, currentEventEnd + _eventEndShift } 
            });
            
            return true; // Always succeeds
        }
        
        public override void SetStateVariableKey(dynamic stateKey)
        {
            // No states for this test subsystem
        }
    }
}

