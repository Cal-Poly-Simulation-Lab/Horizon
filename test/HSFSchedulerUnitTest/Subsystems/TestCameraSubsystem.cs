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
    /// Simple STATELESS test subsystem - tracks iteration count in SystemState
    /// Demonstrates proper stateless subsystem design for parallel execution
    /// </summary>
    public class TestCameraSubsystem : Subsystem
    {
        // Parameters (configuration, not state!)
        protected double maxImages;
        
        // State key (reference to where state lives in SystemState)
        protected StateVariableKey<double> NUM_IMAGE_KEY;
        
        public TestCameraSubsystem(JObject subJson, Asset asset) : base(subJson, asset)
        {
            this.GetParameterByName<double>(subJson, nameof(maxImages), out maxImages);
            
        }
        
        // Public getter for testing
        // public int GetMaxIterations() => _maxIterations;
        // public double GetTestParameter() => _testParameter;
        
        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            // So the whoole idea of a subsytem having a state is not going to wor. Call what you need here:
            var state = proposedEvent.State; // current system state
            var task = proposedEvent.GetAssetTask(Asset);
            var taskType = task.Type.ToUpper();

            // Get the last power value from the state
            double numImages = state.GetLastValue(NUM_IMAGE_KEY).Item2; // last power value
            double updateTime = proposedEvent.GetTaskStart(Asset) + 0.1;
            if (taskType == "IMAGING")
            {
                if (numImages >= maxImages) { return false; } // Fail if max images reached. 
                state.AddValue(NUM_IMAGE_KEY, updateTime, numImages + 1);
                return true;
            }
            return true; // Return true and do nothing if its not an imaging task. 
 
        }
        
        public override void SetStateVariableKey(dynamic stateKey)
        {
            // Store the KEY reference (not the value!)
            if (stateKey.VariableName.Equals(Asset.Name.ToLower() + ".num_images_stored"))
            {
                this.NUM_IMAGE_KEY = stateKey;
            }
            else
            {
                throw new ArgumentException($"Attempting to set unknown TestCamera state variable key '{stateKey.VariableName}'.", nameof(stateKey));
            }
        }

    }
}
