using HSFUniverse;
using MissionElements;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Utilities;

namespace HSFSystem
{
    public class AlwaysTrueSubsystem : Subsystem
    {
        #region Attributes

        // Dictionary<string, double> lookup;
        // protected StateVariableKey<double> maj_Key;
        string Description = "This is the always true subsystem, used for testing purposes.";

        #endregion

        #region Constructors
        public AlwaysTrueSubsystem(JObject subtestJson, Asset asset) : base(subtestJson, asset)
        {
            // No additional initialization needed - base constructor handles Name, Asset, Type, etc.
        }

        #endregion Constructors

        #region Methods
        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            return true;
        }
        public override void SetStateVariableKey(dynamic stateKey)
        {
            // Do Not need anything here fr the time being. 
        }
        #endregion
    }
}
