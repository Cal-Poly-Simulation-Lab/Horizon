// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.Xml;
using MissionElements;
using log4net;
using System.Reflection;
using Utilities;
using IronPython.Hosting;
using Newtonsoft.Json.Linq;
using Microsoft.Scripting.Utils;
using System.Runtime.InteropServices;
using System.Linq;
using UserModel;
using static IronPython.Modules._ast;
using System.Runtime.CompilerServices;

namespace HSFSystem
{
    public class SubsystemFactory
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        SubsystemFactory()
        {

        }
        /// <summary>
        /// A method to interpret the JSON and create subsystems
        /// </summary>
        /// <param name="SubsystemJson"></param>
        /// <param name="asset"></param>
        /// <returns></returns>
        public static Subsystem GetSubsystem(JObject SubsystemJson, Asset asset)
        {
            string msg;

            if (JsonLoader<string>.TryGetValue("type", SubsystemJson, out string type))
                type = type.ToLower();
            else
            {
                msg = $"Missing a subsystem 'type' attribute for subsystem in {asset.Name}";
                Console.WriteLine(msg);
                log.Error(msg);
                throw new ArgumentOutOfRangeException(msg);
            }

            Subsystem subsystem;

            if (type.Equals("scripted"))
            {
                subsystem = new ScriptedSubsystem(SubsystemJson, asset);
            }
            else // not scripted subsystem
            {
                if (type.Equals("access"))
                {
                    subsystem = new AccessSub(SubsystemJson, asset);
                }
                else if (type.Equals("adcs"))
                {
                    subsystem = new ADCS(SubsystemJson, asset);
                }
                else if (type.Equals("power"))
                {
                    subsystem = new Power(SubsystemJson, asset);
                }
                else if (type.Equals("eosensor"))
                {
                    subsystem = new EOSensor(SubsystemJson, asset);
                }
                else if (type.Equals("ssdr"))
                {
                    subsystem = new SSDR(SubsystemJson, asset);
                }
                else if (type.Equals("comm"))
                {
                    subsystem = new Comm(SubsystemJson, asset);
                }
                else if (type.Equals("imu"))
                {
                    //sub = new IMU(SubsystemXmlNode, asset);
                    throw new NotImplementedException("Removed after the great SubsystemFactory update.");
                }
                else if (type.Equals("subtest"))
                {
                    subsystem = new SubTest(SubsystemJson, asset);
                    //sub = new SubTest(SubsystemXmlNode, asset);
                    //throw new NotImplementedException("Removed after the great SubsystemFactory update.");
                }
                else if (type.Equals("networked"))
                {
                    throw new NotImplementedException("Networked Subsystem is a depreciated feature!");
                }
                else
                {
                    msg = $"Horizon does not recognize the subsystem: {type}";
                    Console.WriteLine(msg);
                    log.Fatal(msg);
                    throw new ArgumentOutOfRangeException(msg);
                }
            }
            return subsystem;
        }

        public static void SetDependencies(JObject dependencyJson, List<Subsystem> SubsystemList)
        {
            StringComparison stringCompare = StringComparison.CurrentCultureIgnoreCase;

            string assetName = dependencyJson.GetValue("assetName", stringCompare).ToString().ToLower();
            string subName = dependencyJson.GetValue("subsystemName", stringCompare).ToString().ToLower();
            string depSubName = dependencyJson.GetValue("depSubsystemName", stringCompare).ToString(); // NOT lowercase
            string depAssetName = dependencyJson.GetValue("depAssetName", stringCompare).ToString().ToLower();

            // Add dependent subsystem to subsystem's list of dep subs
            var sub = SubsystemList.Find(s => s.Name == assetName + "." + subName);
            var depSub = SubsystemList.Find(s => s.Name == depAssetName + "." + depSubName.ToLower());
            sub.DependentSubsystems.Add(depSub);

            if (dependencyJson.TryGetValue("fcnName", stringCompare, out JToken depFncJToken))
            {
                // Get dep fn name
                string depFncName = dependencyJson["fcnName"].ToString();

                // Determine in what type of sub the depFn lives
                Type depSubType = depSub.GetType();

                if (depSubType.Name == "ScriptedSubsystem") // If depFn lives in Python subsystem
                {
                    // Cast depSub to Scripted so compiler does not get mad (it should be scripted to reach here?)
                    ScriptedSubsystem depSubCasted = (ScriptedSubsystem)depSub;
                    // Get method from python script & add to sub's dep fns
                    Delegate fnc = depSubCasted.GetDepFn(depFncName, depSubCasted);
                    sub.SubsystemDependencyFunctions.Add(depFncName, fnc);
                }
                else // If depFn lives in C# subsystem
                {
                    // Find method that matches name via reflection & add to sub's dep fns
                    var TypeIn = Type.GetType("HSFSystem." + depSubName).GetMethod(depFncName);
                    Delegate fnc = Delegate.CreateDelegate(typeof(Func<Event, HSFProfile<double>>), depSub, TypeIn);
                    sub.SubsystemDependencyFunctions.Add(depFncName, fnc);
                }
            }
        }
        public static void SetInitialState(JObject stateJson, Subsystem subsys, SystemState InitialState)
        {

            if(!JsonLoader<string>.TryGetValue("Type", stateJson, out string type))
            {
                string msg = $"Missing the subsystem State Type for subsystem {subsys.Name}";
                Console.WriteLine(msg);
                log.Error(msg);
                throw new ArgumentOutOfRangeException(msg);
            }
            type = type.ToLower();

            if (!JsonLoader<string>.TryGetValue("Key", stateJson, out string keyName))
            {
                string msg = $"Missing the subsystem State Key for subsystem {subsys.Name}";
                Console.WriteLine(msg);
                log.Error(msg);
                throw new ArgumentOutOfRangeException(msg);
            }
            keyName = keyName.ToLower();

            string assetName = subsys.Asset.Name.ToLower();
            string key = assetName + "." + keyName;
            dynamic stateKey = null;
            if (type.Equals("int"))
            {
                stateKey = new StateVariableKey<int>(key);
            }
            else if (type.Equals("double"))
            {
                stateKey = new StateVariableKey<double>(key);
            }
            else if (type.Equals("bool"))
            {
                stateKey = new StateVariableKey<bool>(key);
            }
            else if (type.Equals("matrix"))
            {
                stateKey = new StateVariableKey<Matrix<double>>(key);
            }
            else if (type.Equals("quaternion"))
            {
                stateKey = new StateVariableKey<Quaternion>(key);
            }
            else if (type.Equals("vector"))
            {
                stateKey = new StateVariableKey<Vector>(key);
            }

            var HSFHelper = new ScriptedSubsystemHelper();

            if (subsys.Type == "scripted")
            {
                if (!JsonLoader<string>.TryGetValue("Name", stateJson, out string stateName))
                {
                    string msg = $"Missing the subsystem State Name for key {key} for subsystem {subsys.Name}";
                    Console.WriteLine(msg);
                    log.Error(msg);
                    throw new ArgumentOutOfRangeException(msg);
                }
                
                ((ScriptedSubsystem)subsys).SetStateVariable(HSFHelper, stateName.ToLower(), stateKey);
            }
            else
                subsys.SetStateVariableKey(stateKey);

            if (!JsonLoader<JToken>.TryGetValue("Value", stateJson, out JToken intValueJson))
            {
                string msg = $"Missing the subsystem State Value for '{key}' for subsystem {subsys.Name}";
                Console.WriteLine(msg);
                log.Error(msg);
                throw new ArgumentOutOfRangeException(msg);
            }

            InitialState.SetInitialSystemState(intValueJson, stateKey);

        }

        public static void SetParameters(JObject parameterJson, Subsystem subsystem)
        {
            StringComparison stringCompare = StringComparison.CurrentCultureIgnoreCase;
            string name = parameterJson.GetValue("name", stringCompare).ToString();
            // TODO:  Check to make sure name is a valid python variable name
            string value = parameterJson.GetValue("value", stringCompare).ToString();
            string type = parameterJson.GetValue("type", stringCompare).ToString().ToLower();

            SubsystemFactory.InitParameter(name, value, type, subsystem);

        }

        private static void InitParameter(string name, string value, string type, Subsystem subsystem)
        {
            dynamic paramValue = null;

            switch (type)
            {
                case ("double"):
                    paramValue = Convert.ToDouble(value);
                    break;
                case ("int"):
                    paramValue = Convert.ToInt32(value);
                    break;
                case ("string"):
                    paramValue = Convert.ToString(value);
                    break;
                case ("bool"):
                    paramValue = Convert.ToBoolean(value);
                    break;
                case ("matrix"):
                    paramValue = new Matrix<double>(value);
                    break;
                case ("quaterion"):
                    paramValue = new Quaternion(value);
                    break;
                case ("vector"):
                    paramValue = new Vector(value);
                    break;
            }
            var HSFHelper = new ScriptedSubsystemHelper();
            ((ScriptedSubsystem)subsystem).SetSubsystemParameter(HSFHelper, name, paramValue);

        }
    }
}
