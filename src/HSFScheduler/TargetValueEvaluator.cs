// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using HSFSystem;
using MissionElements;
using Utilities;
using Newtonsoft.Json.Linq;
using UserModel;
using Task = MissionElements.Task; // error CS0104: 'Task' is an ambiguous reference between 'MissionElements.Task' and 'System.Threading.Tasks.Task'


namespace HSFScheduler
{
    public class TargetValueEvaluator : Evaluator
    {
        #region Attributes
        public List<dynamic> _keychain;
        private StateVariableKey<double> A1_DataBufferFillRatio;
        private StateVariableKey<double> A2_DataBufferFillRatio;
        #endregion

        #region Constructors
        public TargetValueEvaluator()
        {
            
        }
        public TargetValueEvaluator(JToken statesJson, SystemState initialSysState)
        {
            foreach (JObject stateJson in statesJson)
            {
                if (JsonLoader<string>.TryGetValue("type", stateJson, out string stateType))
                {
                    if (stateType.Equals("double"))
                    {
                        JsonLoader<string>.TryGetValue("asset", stateJson, out string asset);
                        asset = asset.ToLower();
                        JsonLoader<string>.TryGetValue("key", stateJson, out string key);
                        key = key.ToLower();
                        string keyName = asset + "." + key;
                        if (asset.Equals("asset1"))
                            A1_DataBufferFillRatio = initialSysState.Ddata.Keys.First(s => s.VariableName == keyName);
                        else if (asset.Equals("asset2"))
                            A2_DataBufferFillRatio = initialSysState.Ddata.Keys.First(s => s.VariableName == keyName);
                        else
                            throw new Exception();
                    }
                }
            }
        }
        #endregion

            #region Methods
            /// <summary>
            /// Override of the Evaluate method
            /// </summary>
            /// <param name="schedule"></param>
            /// <returns></returns>
        public override double Evaluate(SystemSchedule schedule)
        {
            double sum = 0;
            foreach(Event eit in schedule.AllStates.Events)
            {
                foreach (KeyValuePair<Asset, Task> assetTask in eit.Tasks)
                {
                    Task task = assetTask.Value;
                    Asset asset = assetTask.Key;
                    sum += task.Target.Value;
                    if (task.Type == "comm")
                    {
                        double StartTime = eit.GetTaskStart(asset);
                        double EndTime = eit.GetTaskEnd(asset);
                        double dataBufferRatioStart = 0;
                        double dataBufferRatioEnd = 0;

                        if (asset.Name == "asset1")
                        {
                            dataBufferRatioStart = eit.State.GetValueAtTime(A1_DataBufferFillRatio, StartTime).Value;
                            dataBufferRatioEnd = eit.State.GetValueAtTime(A1_DataBufferFillRatio, EndTime).Value;
                        }
                        else if (asset.Name == "asset2")
                        {
                            dataBufferRatioStart = eit.State.GetValueAtTime(A2_DataBufferFillRatio, StartTime).Value;
                            dataBufferRatioEnd = eit.State.GetValueAtTime(A2_DataBufferFillRatio, EndTime).Value;
                        }
                        
                        sum += (dataBufferRatioStart - dataBufferRatioEnd) * 50;
                    }
                }
            }
            return sum;
        }
        #endregion
    }
}
