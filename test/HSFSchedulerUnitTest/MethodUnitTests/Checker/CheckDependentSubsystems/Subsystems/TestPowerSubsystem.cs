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
    public class TestPowerSubsystem : Subsystem
    {
        // Parameters (configuration, not state!)
        public double? _maxPower {get; private set;}
        public double? _minPower {get; private set;}
        
        // State key (reference to where state lives in SystemState)
        protected StateVariableKey<double> POWER_KEY;
        
        public TestPowerSubsystem(JObject subJson, Asset asset) : base(subJson, asset)
        {
            // Load maxPower Paramter
            if (JsonLoader<int>.TryGetValue("MaxPower", subJson, out double maxPower))
            {
                _maxPower = maxPower;
            }
            // Load minPower Parameter
            if (JsonLoader<int>.TryGetValue("MinPower", subJson, out double minPower))
            {
                _minPower = minPower;
            }
        }
        
        // Public getter for testing
        // public int GetMaxIterations() => _maxIterations;
        // public double GetTestParameter() => _testParameter;
        
        public override bool CanPerform(Event proposedEvent, Domain environment)
        {

            return true;
 
        }
        
        public override void SetStateVariableKey(dynamic stateKey)
        {
            // Store the KEY reference (not the value!)
            if (stateKey.VariableName.Equals(Asset.Name + ".power"))
                this.POWER_KEY = stateKey;
            else{
                throw new ArgumentException("Attempting to set unknown TestPower state variable key.", stateKey, stateKey.VariableName);
                }
        }
    }
}
