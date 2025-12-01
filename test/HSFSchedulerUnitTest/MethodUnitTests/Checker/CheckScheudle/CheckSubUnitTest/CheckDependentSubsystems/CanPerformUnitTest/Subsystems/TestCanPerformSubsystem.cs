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
    public class TestCanPerformSubsystem : Subsystem
    {
        // Parameters (configuration, not state!)
        private int _maxIterations;
        private double _testParameter;
        
        // State key (reference to where state lives in SystemState)
        protected StateVariableKey<int> ITERATION_KEY;
        
        public TestCanPerformSubsystem(JObject subJson, Asset asset) : base(subJson, asset)
        {
            // Load maxIterations parameter (default 5)
            if (JsonLoader<int>.TryGetValue("maxIterations", subJson, out int maxIter))
            {
                _maxIterations = maxIter;
            }
            else
            {
                _maxIterations = 5;
            }
            
            // Load test_parameter (default 0.0)
            if (JsonLoader<double>.TryGetValue("test_parameter", subJson, out double testParam))
            {
                _testParameter = testParam;
            }
            else
            {
                _testParameter = 0.0;
            }
        }
        
        // Public getter for testing
        // public int GetMaxIterations() => _maxIterations;
        // public double GetTestParameter() => _testParameter;
        
        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            // Get the state from the event (NOT from this object - STATELESS!)
            SystemState state = proposedEvent.State;
            
            // Read current iteration from STATE (must exist - set during loading)
            int currentIteration = state.GetLastValue(ITERATION_KEY).Item2;
            
            // Calculate new iteration
            int newIteration = currentIteration + 1;
            
            // Write new value to STATE at the task start time
            double taskStart = proposedEvent.GetTaskStart(Asset);
            var prof = new HSFProfile<int>(taskStart, newIteration);
            state.AddValues(ITERATION_KEY, prof);
            
            // Return false if max reached (using PARAMETER, not state!)
            return (newIteration < _maxIterations);
        }
        
        public override void SetStateVariableKey(dynamic stateKey)
        {
            // Store the KEY reference (not the value!)
            if (stateKey.VariableName.Equals(Asset.Name + ".iteration"))
                this.ITERATION_KEY = stateKey;
        }
    }
}
