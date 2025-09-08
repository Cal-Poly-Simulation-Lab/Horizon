using System;
using System.Collections.Generic;
using HSFSystem;
using HSFUniverse;
using MissionElements;
using Utilities;
using Newtonsoft.Json.Linq;

namespace TestSubsystem
{
    public class TestSubsystem : Subsystem
    {
        public TestSubsystem(JObject subsystemJson, Asset asset) : base(subsystemJson, asset)
        {
            // Simple test subsystem implementation
        }

        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            // Always return true for testing purposes
            return true;
        }


        public override void SetStateVariableKey(dynamic stateKey)
        {
            return; 
            // throw new NotImplementedException();
        }

        // public override bool CanExtend(Event proposedEvent, Domain environment, double evalToTime)
        // {
        //     // Always return true for testing purposes
        //     return true;
        // }

    }
}
