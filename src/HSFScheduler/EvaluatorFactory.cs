// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using HSFSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using MissionElements;
using Utilities;
using Newtonsoft.Json.Linq;
using UserModel;
using System.CodeDom;
using log4net;

namespace HSFScheduler
{
    public class EvaluatorFactory
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static Evaluator GetEvaluator(JObject evaluatorJson, SystemState initialSysState)
        {
            Evaluator schedEvaluator = null;
            List<StateVariableKey<dynamic>> evaluatorStates = new List<StateVariableKey<dynamic>>();


            string msg = "";

            if (JsonLoader<string>.TryGetValue("type", evaluatorJson, out string evaluatorType))
            {
                if (JsonLoader<JToken>.TryGetValue("states", evaluatorJson, out JToken statesJson))
                {
                }
                else
                {
                    msg = $"The Evaluator {evaluatorType} does not contain any stateKey requests.";
                    Console.WriteLine(msg);
                    log.Error(msg);
                    throw new ArgumentOutOfRangeException(msg);
                }

                if (evaluatorType.Equals("scripted", StringComparison.InvariantCultureIgnoreCase))
                {
                    //List<dynamic> keychain = BuildKeyChain(keyRequests, subsystemList);
                    //schedEvaluator = new ScriptedEvaluator(keyRequests, initialSysState);
                    Console.WriteLine("Scripted Evaluator Loaded");
                }
                else if (evaluatorType.ToLower().Equals("TargetValueEvaluator", StringComparison.InvariantCultureIgnoreCase))
                {
                    //List<dynamic> keychain = BuildKeyChain(keyRequests, subsystemList);
                    schedEvaluator = new TargetValueEvaluator(statesJson, initialSysState);
                    Console.WriteLine("Target Value Evaluator Loaded");
                }
                else
                {
                    msg = $"Could not load the Evaluator of type {evaluatorType}.  Default Evaluator Loaded...  This may cause problems...";
                    Console.WriteLine(msg);
                    log.Error(msg);
                    schedEvaluator = new DefaultEvaluator(); // ensures at least default is used
                }
            }
            else
            {
                    msg = $"Could not load the Evaluator of type {evaluatorType}.  Default Evaluator Loaded...  This may cause problems...";
                    Console.WriteLine(msg);
                    log.Error(msg);
                    schedEvaluator = new DefaultEvaluator(); // ensures at least default is used
                }

            return schedEvaluator;
        }

        private static List<dynamic> BuildKeyChain(JToken keyRequests, List<Subsystem> subsystems)
        {
            List<dynamic> keychain = new List<dynamic>();

            foreach (JObject key in keyRequests)
            {
                JsonLoader<string>.TryGetValue("key", key, out string InputKey);
                InputKey = InputKey.ToLower();
                JsonLoader<string>.TryGetValue("asset", key, out string InputAsset);
                InputAsset = InputAsset.ToLower();
                JsonLoader<string>.TryGetValue("type", key, out string InputType);
                InputType = InputType.ToLower();
                //string InputSub = keySourceNode.Attributes["keySub"].Value.ToString().ToLower();
                //string InputAsset = keySourceNode.Attributes["keyAsset"].Value.ToString().ToLower();
                //string InputType = keySourceNode.Attributes["keyType"].Value.ToString().ToLower();

                Subsystem subRequested = subsystems.Find(s => s.Name == InputAsset + "." + InputKey);

                StateVariableKey<dynamic> newKey = new StateVariableKey<dynamic>(InputAsset + "." + InputKey);
                keychain.Add(newKey);
                //if (subRequested == null)
                //{
                //    Console.WriteLine("Asset/Subsystem pair requested was not found!");
                //    throw new ArgumentException("Asset/Subsystem pair requested was not found!");
                //}
                //if (InputType.Equals("int"))
                //{
                //    foreach (StateVariableKey<Int32> keyOfTypeRequested in subRequested.Ikeys)
                //    {
                //        keychain.Add(keyOfTypeRequested);
                //    }
                //}
                //else if (InputType.Equals("double"))
                //{
                //    foreach (StateVariableKey<double> keyOfTypeRequested in subRequested.Dkeys)
                //    {
                //        keychain.Add(keyOfTypeRequested);
                //    }
                //}
                //else if (InputType.Equals("bool"))
                //{
                //    foreach (StateVariableKey<bool> keyOfTypeRequested in subRequested.Bkeys)
                //    {
                //        keychain.Add(keyOfTypeRequested);
                //    }
                //}
                //else if (InputType.Equals("matrix"))
                //{
                //    foreach (StateVariableKey<Matrix<double>> keyOfTypeRequested in subRequested.Mkeys)
                //    {
                //        keychain.Add(keyOfTypeRequested);
                //    }
                //}
                //else if (InputType.Equals("quat"))
                //{
                //    foreach (StateVariableKey<Quaternion> keyOfTypeRequested in subRequested.Qkeys)
                //    {
                //        keychain.Add(keyOfTypeRequested);
                //    }
                //}
                //else if (InputType.Equals("vector"))
                //{
                //    foreach (StateVariableKey<Vector> keyOfTypeRequested in subRequested.Vkeys)
                //    {
                //        keychain.Add(keyOfTypeRequested);
                //    }
                //}
                //else
                //{
                //    Console.WriteLine("Key type requested is not supported!");
                //    throw new ArgumentException("Key type requested is not supported!");
                //}
            }
            return keychain;
        }
    }
}
