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
        public double? _rechargeValue {get; private set;}
        public double? _maxPower {get; private set;}
        public double? _minPower {get; private set;}
        public double? _transmitPowerRequired {get; private set;}
        public double? _requiredPowerImage {get; private set;}
        // State key (reference to where state lives in SystemState)
        protected StateVariableKey<double> TEST_POWER_KEY;
        
        public TestPowerSubsystem(JObject subJson, Asset asset) : base(subJson, asset)
        {
            // Load rechargeValue Parameter
            if (JsonLoader<double>.TryGetValue("rechargeValue", subJson, out double rechargeValue))
            {
                this._rechargeValue = rechargeValue;
            }
            // Load maxPower Paramter
            if (JsonLoader<double>.TryGetValue("MaxPower", subJson, out double maxPower))
            {
                this._maxPower = maxPower;
            }
            // Load minPower Parameter
            if (JsonLoader<double>.TryGetValue("MinPower", subJson, out double minPower))
            {
                _minPower = minPower;
            }
            // Load maxPower Paramter
            if (JsonLoader<double>.TryGetValue("requiredPowerTrasmit", subJson, out double requiredPowerTrasmit))
            {
                this._transmitPowerRequired = requiredPowerTrasmit;
            }  
            // Load maxPower Paramter
            if (JsonLoader<double>.TryGetValue("requiredPowerImage", subJson, out double requiredPowerImage))
            {
                this._requiredPowerImage = requiredPowerImage;
            } 

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
            double lastPower = state.GetLastValue(TEST_POWER_KEY).Item2; // last power value

            if (taskType == "RECHARGE")
            {
                if (lastPower + _rechargeValue > _maxPower) { return false; } // Fail if recharge would exceed max charge. 
                state.AddValue(TEST_POWER_KEY, proposedEvent.GetTaskEnd(Asset), lastPower + _rechargeValue);
                return true;
            }
            else if (taskType == "TRASMIT")
            {
                if (lastPower < _transmitPowerRequired) { return false; } // Fail if not enough power for transmission. 
                state.AddValue(TEST_POWER_KEY, proposedEvent.GetTaskEnd(Asset), lastPower - _transmitPowerRequired);
                return true;
            }
            else if (taskType == "IMAGING")
            {
                if (lastPower < _requiredPowerImage) { return false; } // Fail if not enough power for imaging. 
                state.AddValue(TEST_POWER_KEY, proposedEvent.GetTaskEnd(Asset), lastPower - _requiredPowerImage);
                return true;
            }
            return false; // Fail if not a valid task type. 
 
        }
        
        public override void SetStateVariableKey(dynamic stateKey)
        {
            // Store the KEY reference (not the value!)
            if (stateKey.VariableName.Equals(Asset.Name + ".test_power"))
            {
                this.TEST_POWER_KEY = stateKey;
            }
            else
            {
                throw new ArgumentException($"Attempting to set unknown TestPower state variable key '{stateKey.VariableName}'.", nameof(stateKey));
            }
        }
    }
}
