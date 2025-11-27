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
        protected double rechargeValue;
        protected double maxPower;
        protected double minPower;
        protected double transmitPowerRequired;
        protected double imagePowerRequired;
        // State key (reference to where state lives in SystemState)
        protected StateVariableKey<double> CHECKER_POWER_KEY = null!;
        
        public TestPowerSubsystem(JObject subJson, Asset asset) : base(subJson, asset)
        {
            GetParameterByName<double>(subJson, nameof(rechargeValue), out rechargeValue);
            GetParameterByName<double>(subJson, nameof(maxPower), out maxPower);
            GetParameterByName<double>(subJson, nameof(minPower), out minPower);
            GetParameterByName<double>(subJson, nameof(transmitPowerRequired), out transmitPowerRequired);
            GetParameterByName<double>(subJson, nameof(imagePowerRequired), out imagePowerRequired);

        }
        
        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            // So the whoole idea of a subsytem having a state is not going to wor. Call what you need here:
            var state = proposedEvent.State; // current system state
            var task = proposedEvent.GetAssetTask(Asset);
            var taskType = task.Type.ToUpper();

            // Get the last power value from the state
            double lastPower = state.GetLastValue(CHECKER_POWER_KEY).Item2; // last power value
            double updateTime = proposedEvent.GetTaskStart(Asset) + 0.1;

            if (taskType == "RECHARGE")
            {
                if (lastPower + rechargeValue > maxPower) { return false; } // Fail if recharge would exceed max charge. 
                state.AddValue(CHECKER_POWER_KEY, updateTime, lastPower + rechargeValue);
                return true;
            }
            else if (taskType == "TRANSMIT")
            {
                if (lastPower < transmitPowerRequired) { return false; } // Fail if not enough power for transmission. 
                state.AddValue(CHECKER_POWER_KEY, updateTime, lastPower - transmitPowerRequired);
                return true;
            }
            else if (taskType == "IMAGING")
            {
                if (lastPower < imagePowerRequired) { return false; } // Fail if not enough power for imaging. 
                state.AddValue(CHECKER_POWER_KEY, updateTime, lastPower - imagePowerRequired);
                return true;
            }
            return false; // Fail if not a valid task type. 
 
        }
        
        public override void SetStateVariableKey(dynamic stateKey)
        {
            // Store the KEY reference (not the value!)
            if (stateKey.VariableName.Equals(Asset.Name.ToLower() + ".checker_power"))
            {
                this.CHECKER_POWER_KEY = stateKey;
            }
            else
            {
                throw new ArgumentException($"Attempting to set unknown TestPower state variable key '{stateKey.VariableName}'.", nameof(stateKey));
            }
        }

    }
}
