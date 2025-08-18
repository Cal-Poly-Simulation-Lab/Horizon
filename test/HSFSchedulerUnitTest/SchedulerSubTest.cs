using HSFUniverse;
using MissionElements;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using Utilities;

namespace HSFSystem
{
    public class SubTest : Subsystem
    {
        #region Attributes

        Dictionary<string, double> lookup;
        protected StateVariableKey<double> maj_Key;
        string temporary = "This is the scheduler unit test subsystem"; 

        #endregion

        #region Constructors
        public SubTest(JObject subtestJson)
        {
            // Initialize lookup to prevent null reference exceptions
            lookup = new Dictionary<string, double>();
            lookup.Add("Task1", 0.0);  // Default mapping for the test task
            lookup.Add("Target1", 0.0); // Also add target name mapping
        }

        #endregion Constructors

        #region Methods
        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            double es = proposedEvent.GetEventStart(Asset);
            double ee = proposedEvent.GetEventEnd(Asset);
            double ts = proposedEvent.GetTaskStart(Asset);
            double te = proposedEvent.GetTaskEnd(Asset);

            string taskathand = proposedEvent.GetAssetTask(Asset).ToString();

            double tasknum = 0;
            lookup.TryGetValue(taskathand, out tasknum);
            if (tasknum == es)
            {
                //if (taskathand == "target1")
                //    proposedEvent.SetTaskEnd(Asset, ee + 0.25);
                //else
                //    proposedEvent.SetTaskEnd(Asset, ee - 0.25);
                return true;
            }
            else
            {
                return false;
            }
        }



    }
    #endregion
}
        // public double depFunc(Event currentEvent)
        // {
        //     return currentEvent.EventEnds[Asset]; //no reason for this, just need to return something
        // }

        // #endregion Methods
        // static Dictionary<string, double> getList()
        // {
        //     Dictionary<string, double> lookup = new Dictionary<string, double>();
        //     lookup.Add("target0", 0);
        //     lookup.Add("target1", 1);
        //     lookup.Add("target1.1", 1);
        //     lookup.Add("target2", 2);
        //     lookup.Add("target3", 3);
        //     return lookup;
        // }
        // static Dictionary<string, double> getList(double time)
        // {
        //     Dictionary<string, double> lookup = new Dictionary<string, double>();
        //     lookup.Add("target0", 0);
        //     lookup.Add("target1", 0);
        //     lookup.Add("target1.1", time);
        //     lookup.Add("target2", time);
        //     lookup.Add("target3", time);
        //     return lookup;
        // }