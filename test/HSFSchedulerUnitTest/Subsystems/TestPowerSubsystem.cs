// Copyright (c) 2025 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using System;
using System.Collections.Generic;
using System.Reflection;
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
        protected double _taskStartTimeMutation;
        protected double _taskEndTimeMutation;
        // State key (reference to where state lives in SystemState)
        protected StateVariableKey<double> CHECKER_POWER_KEY = null!;
        
        public TestPowerSubsystem(JObject subJson, Asset asset) : base(subJson, asset)
        {
            GetParameterByName<double>(subJson, nameof(rechargeValue), out rechargeValue);
            GetParameterByName<double>(subJson, nameof(maxPower), out maxPower);
            GetParameterByName<double>(subJson, nameof(minPower), out minPower);
            GetParameterByName<double>(subJson, nameof(transmitPowerRequired), out transmitPowerRequired);
            GetParameterByName<double>(subJson, nameof(imagePowerRequired), out imagePowerRequired);
            
            // Time mutation parameters (default to 0 if not provided)
            _taskStartTimeMutation = TryGetParameterByName<double>(subJson, "_taskStartTimeMutation", 0.0);
            _taskEndTimeMutation = TryGetParameterByName<double>(subJson, "_taskEndTimeMutation", 0.0);
        }
        
        // Helper method to safely call SubsystemCallTracker if available (only in test context)
        private static void SafeTrackCall(string assetName, string subsystemName, string taskType, bool mutated)
        {
            try
            {
                // Use reflection to check if SubsystemCallTracker exists and call it
                System.Type? trackerType = System.Type.GetType("HSFSystem.SubsystemCallTracker, HSFSchedulerUnitTest");
                if (trackerType != null)
                {
                    MethodInfo? trackMethod = trackerType.GetMethod("Track", BindingFlags.Public | BindingFlags.Static);
                    trackMethod?.Invoke(null, new object[] { assetName, subsystemName, taskType, mutated });
                }
            }
            catch
            {
                // Silently ignore if SubsystemCallTracker is not available (normal execution context)
            }
        }
        
        // Helper method for optional parameters
        private T TryGetParameterByName<T>(JObject subsysJson, string name, T defaultValue)
        {
            if (JsonLoader<JArray>.TryGetValue("parameters", subsysJson, out JArray parameters))
            {
                foreach (JObject parameter in parameters)
                {
                    if (JsonLoader<string>.TryGetValue("name", parameter, out string varName))
                    {
                        if (varName == name)
                        {
                            JsonLoader<double>.TryGetValue("value", parameter, out double value);
                            return (T)(object)value;
                        }
                    }
                }
            }
            return defaultValue;
        }
        
        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            // So the whoole idea of a subsytem having a state is not going to wor. Call what you need here:
            var state = proposedEvent.State; // current system state
            var task = proposedEvent.GetAssetTask(Asset);
            var taskType = task.Type.ToUpper();

            // Apply time mutations if configured
            double currentTaskStart = proposedEvent.GetTaskStart(Asset);
            double currentTaskEnd = proposedEvent.GetTaskEnd(Asset);
            double mutatedTaskStart = currentTaskStart + _taskStartTimeMutation;
            double mutatedTaskEnd = currentTaskEnd + _taskEndTimeMutation;
            
            // Update task times if mutations are non-zero
            if (_taskStartTimeMutation != 0.0 || _taskEndTimeMutation != 0.0)
            {
                proposedEvent.SetTaskStart(new Dictionary<Asset, double> { { Asset, mutatedTaskStart } });
                proposedEvent.SetTaskEnd(new Dictionary<Asset, double> { { Asset, mutatedTaskEnd } });
            }

            // Get the last power value from the state
            double lastPower = state.GetLastValue(CHECKER_POWER_KEY).Item2; // last power value
            double updateTime = proposedEvent.GetTaskStart(Asset) + 0.1;

            if (taskType == "RECHARGE")
            {
                if (lastPower + rechargeValue > maxPower) { return false; } // Fail if recharge would exceed max charge. 
                state.AddValue(CHECKER_POWER_KEY, updateTime, lastPower + rechargeValue);
                SafeTrackCall(Asset.Name, "Power", taskType, mutated: true);
                return true;
            }
            else if (taskType == "TRANSMIT")
            {
                if (lastPower < transmitPowerRequired) { return false; } // Fail if not enough power for transmission. 
                state.AddValue(CHECKER_POWER_KEY, updateTime, lastPower - transmitPowerRequired);
                SafeTrackCall(Asset.Name, "Power", taskType, mutated: true);
                return true;
            }
            else if (taskType == "IMAGING")
            {
                if (lastPower < imagePowerRequired) { return false; } // Fail if not enough power for imaging. 
                state.AddValue(CHECKER_POWER_KEY, updateTime, lastPower - imagePowerRequired);
                SafeTrackCall(Asset.Name, "Power", taskType, mutated: true);
                return true;
            }
            SafeTrackCall(Asset.Name, "Power", taskType ?? "NULL", mutated: false);
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
        
        // Public getters for testing
        public double GetTaskStartTimeMutation() => _taskStartTimeMutation;
        public double GetTaskEndTimeMutation() => _taskEndTimeMutation;

    }
}
