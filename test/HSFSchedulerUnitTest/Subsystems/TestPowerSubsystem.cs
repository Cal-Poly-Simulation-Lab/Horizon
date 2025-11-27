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
        protected double requiredPowerImage;
        // State key (reference to where state lives in SystemState)
        protected StateVariableKey<double> CHECKER_POWER_KEY;
        
        public TestPowerSubsystem(JObject subJson, Asset asset) : base(subJson, asset)
        {
            this.GetParameterByName<double>(subJson, nameof(rechargeValue), out rechargeValue);
            this.GetParameterByName<double>(subJson, nameof(maxPower), out maxPower);
            this.GetParameterByName<double>(subJson, nameof(minPower), out minPower);
            this.GetParameterByName<double>(subJson, nameof(transmitPowerRequired), out transmitPowerRequired);
            this.GetParameterByName<double>(subJson, nameof(requiredPowerImage), out requiredPowerImage);
            Console.WriteLine($"[TestPowerSubsystem] {Asset.Name}: recharge={rechargeValue}, max={maxPower}, min={minPower}, transmit={transmitPowerRequired}, image={requiredPowerImage}");
            // Load rechargeValue Parameter
            // if (JsonLoader<double>.TryGetValue("rechargeValue", subJson, out double rechargeValue))
            // {
            //     this.rechargeValue = rechargeValue;
            // }
            // // Load maxPower Paramter
            // if (JsonLoader<double>.TryGetValue("MaxPower", subJson, out double maxPower))
            // {
            //     this.maxPower = maxPower;
            // }
            // // Load minPower Parameter
            // if (JsonLoader<double>.TryGetValue("MinPower", subJson, out double minPower))
            // {
            //     this.minPower = minPower;
            // }
            // // Load maxPower Paramter
            // if (JsonLoader<double>.TryGetValue("requiredPowerTrasmit", subJson, out double requiredPowerTrasmit))
            // {
            //     this.transmitPowerRequired = requiredPowerTrasmit;
            // }  
            // // Load maxPower Paramter
            // if (JsonLoader<double>.TryGetValue("requiredPowerImage", subJson, out double requiredPowerImage))
            // {
            //     this.requiredPowerImage = requiredPowerImage;
            // } 

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
            double lastPower = state.GetLastValue(CHECKER_POWER_KEY).Item2; // last power value
            //Console.WriteLine($"[Power] {Asset.Name} task={task?.Name} type={taskType} last={lastPower}");

            if (taskType == "RECHARGE")
            {
                if (lastPower + rechargeValue > maxPower) { return false; } // Fail if recharge would exceed max charge. 
                state.AddValue(CHECKER_POWER_KEY, proposedEvent.GetTaskStart(Asset) + 0.1, lastPower + rechargeValue);
                return true;
            }
            else if (taskType == "TRANSMIT")
            {
                if (lastPower < transmitPowerRequired) { return false; } // Fail if not enough power for transmission. 
                state.AddValue(CHECKER_POWER_KEY, proposedEvent.GetTaskStart(Asset) + 0.1, lastPower - transmitPowerRequired);
                return true;
            }
            else if (taskType == "IMAGING")
            {
                if (lastPower < requiredPowerImage) { return false; } // Fail if not enough power for imaging. 
                state.AddValue(CHECKER_POWER_KEY, proposedEvent.GetTaskStart(Asset) + 0.1, lastPower - requiredPowerImage);
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
