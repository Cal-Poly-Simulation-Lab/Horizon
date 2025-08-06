// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.Xml;
using HSFUniverse;
using MissionElements;
using Newtonsoft.Json.Linq;
using Utilities;
using static IronPython.Modules._ast;

namespace HSFSystem
{
    public class AccessSub : Subsystem
    {
        public AccessSub(JObject accessJson, Asset asset):base(accessJson, asset) { }

        public override void SetStateVariableKey(dynamic stateKey)
        {
            string msg = $"Warning: No StateVariableKeys set for subsystem {this.Name}";
            Console.WriteLine(msg);
            log.Warn(msg);
        }

        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            DynamicState position = Asset.AssetDynamicState;
            Vector assetPosECI = position.PositionECI(proposedEvent.GetTaskStart(Asset));
            Vector targetPosECI = Task.Target.DynamicState.PositionECI(proposedEvent.GetTaskStart(Asset));
            return GeometryUtilities.hasLOS(assetPosECI, targetPosECI);
        }

        public override bool CanExtend(Event proposedEvent, Domain environment, double evalToTime)
        {
            if (proposedEvent.GetEventEnd(Asset) < evalToTime)
                proposedEvent.SetEventEnd(Asset, evalToTime);
            return true;
        }
    }
}
