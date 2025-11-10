using HSFUniverse;
using MissionElements;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Utilities;

namespace HSFSystem
{
    public class SchedulerSubTest : Subsystem
    {
        #region Attributes

        // Dictionary<string, double> lookup;
        protected StateVariableKey<double> maj_Key;
        string temporary = "This is the scheduler unit test subsystem";

        #endregion

        #region Constructors
        public SchedulerSubTest(JObject subtestJson, Asset asset) : base(subtestJson, asset)
        {
            // No additional initialization needed - base constructor handles Name, Asset, Type, etc.
        }

        #endregion Constructors

        #region Methods
        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            int swtch = 1; 
            if (swtch == 1) { return true; }
            return false; 
            // double es = proposedEvent.GetEventStart(Asset);
            // double ee = proposedEvent.GetEventEnd(Asset);
            // double ts = proposedEvent.GetTaskStart(Asset);
            // double te = proposedEvent.GetTaskEnd(Asset);

            // string taskathand = proposedEvent.GetAssetTask(Asset).ToString();

            // double tasknum = 0;
            // //lookup.TryGetValue(taskathand, out tasknum);
            // if (tasknum == es)
            // {
            //     //if (taskathand == "target1")
            //     //    proposedEvent.SetTaskEnd(Asset, ee + 0.25);
            //     //else
            //     //    proposedEvent.SetTaskEnd(Asset, ee - 0.25);
            //     return true;
            // }
            // else
            // {
            //     return false;
            // }
        }
        public override void SetStateVariableKey(dynamic stateKey)
        {
            // if (stateKey.VariableName.Equals(Asset.Name + ".datarate(mb/s)"))
            //     this.DATARATE_KEY = stateKey;
            // else
            //     throw new ArgumentException("Attempting to set unknown Comm state variable key.", stateKey);
        }



    }
    #endregion
}
