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
    public class TestAntennaSubsystem: Subsystem
    {
        // Parameters (configuration, not state!)
        public double? _transmissionPowerRequired {get; private set;}
        
        // State keys (references to where state lives in SystemState)
        protected StateVariableKey<double> NUM_IMAGE_KEY;
        protected StateVariableKey<int> TRANSMISSION_KEY;
        
        public TestAntennaSubsystem(JObject subJson, Asset asset) : base(subJson, asset)
        {

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

            double numImages = state.GetLastValue(NUM_IMAGE_KEY).Item2;
            if (taskType == "TRANSMIT")
            {
                if (numImages <= 0)
                {
                    return false; // cannot transmit if nothing stored
                }

                state.AddValue(NUM_IMAGE_KEY, proposedEvent.GetTaskEnd(Asset), numImages - 1);

                int transmissions = state.GetLastValue(TRANSMISSION_KEY).Item2;
                state.AddValue(TRANSMISSION_KEY, proposedEvent.GetTaskEnd(Asset), transmissions + 1);
                return true;
            }
            return true; // Return true and do nothing if its not an imaging task. 
        }
        
        public override void SetStateVariableKey(dynamic stateKey)
        {
            // Store the KEY reference (not the value!)
            if (stateKey.VariableName.Equals(Asset.Name + ".num_images_stored"))
            {
                this.NUM_IMAGE_KEY = stateKey;
            }
            else if (stateKey.VariableName.Equals(Asset.Name + ".num_transmissions"))
                this.TRANSMISSION_KEY = stateKey;
            else{
                throw new ArgumentException("Attempting to set unknown TestAntenna state variable key.", stateKey, stateKey.VariableName);
                }
        }
    }
}
