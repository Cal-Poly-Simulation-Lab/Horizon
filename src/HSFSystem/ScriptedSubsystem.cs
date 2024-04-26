﻿// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using IronPython.Hosting;
using MissionElements;
using UserModel;
using HSFUniverse;
using System.Xml;
using System.IO;
using System.Reflection;
using Utilities;
using System.Security.Cryptography.X509Certificates;

namespace HSFSystem
{
    public class ScriptedSubsystem : Subsystem
    {
        #region Attributes
        // A reference to the python scripted class
        protected dynamic _pythonInstance;

        // Overide the accessors in order to modify the python instance
        public override List<Subsystem> DependentSubsystems
        {
            get { return ( List<Subsystem>)_pythonInstance.DependentSubsystems; }
            set { _pythonInstance.DependentSubsystems = (List<Subsystem>)value; }
        }

        public override Dictionary<string, Delegate> SubsystemDependencyFunctions
        {
            get { return (Dictionary<string, Delegate>)_pythonInstance.SubsystemDependencyFunctions; }
            set { _pythonInstance.SubsystemDependencyFunctions = (Dictionary<string, Delegate>)value; }
        }

        //public override bool IsEvaluated
        //{
        //    get { return (bool)_pythonInstance.IsEvaluated; }
        //    set { _pythonInstance.IsEvaluated = (bool)value; }
        //}

        public override SystemState _newState {
            get { return (SystemState)_pythonInstance._newState; }
            set { _pythonInstance._newState = value; }
        }

        public override MissionElements.Task _task {
            get { return (MissionElements.Task)_pythonInstance._task; }  // error CS0104: 'Task' is an ambiguous reference between 'MissionElements.Task' and 'System.Threading.Tasks.Task'
            set { _pythonInstance._task = value; }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor to initialize the python subsystem
        /// </summary>
        /// <param name="scriptedSubXmlNode"></param>
        /// <param name="asset"></param>
        public ScriptedSubsystem(XmlNode scriptedSubXmlNode, Asset asset)
        {
            // TO make sure, the asset, name, keys, and other properties are set for the C# instance and the python instance
            // I'm not convinced about this.  I think either the ScriptedSubsystem needs to have the Keys and Data, or the
            // python instance needs to have the Keys and Data, but not both.
            Asset = asset;
            GetSubNameFromXmlNode(scriptedSubXmlNode);

            string pythonFilePath ="", className = "";
            XmlParser.ParseScriptedSrc(scriptedSubXmlNode, ref pythonFilePath, ref className);
            pythonFilePath = Path.Combine(Utilities.DevEnvironment.RepoDirectory,pythonFilePath.Replace('\\','/')); //Replace backslashes with forward slashes, if applicable

            //  I believe this was added by Jack B. for unit testing.  Still need to sort out IO issues, but with this commented out
            //  the execuitable will look for python files in the same directory as the .exe file is located.
            //  Need to do better specifying the input and output paths.
            //if (!pythonFilePath.StartsWith("..\\")) //patch work for nunit testing which struggles with relative paths
            //{
            //    string baselocation = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\"));
            //    pythonFilePath = Path.Combine(baselocation, @pythonFilePath);
            //}
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            var ops = engine.Operations;
            // Search paths are for importing modules from python scripts, not for executing python subsystem files
            var p = engine.GetSearchPaths();
            p.Add(DevEnvironment.RepoDirectory);
            p.Add(Path.Combine(DevEnvironment.RepoDirectory,"samples/PythonSubs"));
            p.Add(Path.Combine(DevEnvironment.RepoDirectory,"tools"));            
            
            //p.Add(AppDomain.CurrentDomain.BaseDirectory + @"..\..\..\samples\Aeolus\pythonScripts"); // Need to do something about this line
            p.Add(Path.Combine(DevEnvironment.RepoDirectory,"samples/Aeolus/pythonScripts"));
            // Trying to use these so we can call numpy, etc...  Does not seem to work 8/31/23
            //p.Add(@"C:\Python310\Lib\site-packages\");
            //p.Add(@"C:\Python310\Lib");
            // Jason Beals: Add code/functionality to set and find Python environment used for HSF. Can add user input package requirements too
            // 04/24/24

            engine.SetSearchPaths(p);
            engine.ExecuteFile(pythonFilePath, scope);
            var pythonType = scope.GetVariable(className);
            _pythonInstance = ops.CreateInstance(pythonType);//, scriptedSubXmlNode, asset);
            Delegate depCollector = _pythonInstance.GetDependencyCollector();
            SubsystemDependencyFunctions = new Dictionary<string, Delegate>
            {
                { "DepCollector", depCollector }
            };

            _pythonInstance.Asset = asset;
            _pythonInstance.Name = this.Name;
            DependentSubsystems = new List<Subsystem>();

        }
        #endregion

        #region Methods

        public void SetStateVariable<T>(ScriptedSubsystemHelper HSFHelper, string StateName, StateVariableKey<T> key)
        {
            /*
            An argument error was thrown here... Is this possibly because of StateVariableKey<T> SetStateVariable constructor overload?
            As far as I can tell, the _pythonInstance seems to be the python file every time... HSFHelper has been replaced to set the
            attribute within itself "self" and it works this way but this might not be the proper fix. -JB 4/25/24
             */
            HSFHelper.PythonInstance.SetStateVariable(_pythonInstance, StateName, key);
            //HSFHelper.PythonInstance.SetStateVariable(StateName,key);

        }

        public void SetSubsystemParameter(ScriptedSubsystemHelper HSFHelper, string paramenterName, dynamic parameterValue)
        {
            HSFHelper.PythonInstance.SetStateVariable(_pythonInstance, paramenterName, parameterValue);
        }

        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            //if (IsEvaluated)
            //    return true;

            //// Check all dependent subsystems
            //foreach (var sub in DependentSubsystems)
            //{
            //    if (!sub.IsEvaluated)
            //        if (sub.CanPerform(proposedEvent, environment) == false)
            //            return false;
            //}

            //_task = proposedEvent.GetAssetTask(Asset); //Find the correct task for the subsystem
            //_newState = proposedEvent.State;
            //IsEvaluated = true;

            // Call the can perform method that is in the python class
            dynamic perform = _pythonInstance.CanPerform(proposedEvent, environment);
            return (bool)perform;
        }

        public override bool CanExtend(Event proposedEvent, Domain environment, double evalToTime)
        {
            dynamic extend = _pythonInstance.CanExtend(proposedEvent, environment, evalToTime);
            return (bool)extend;
        }

        public Delegate GetDepFn(string depFnName, ScriptedSubsystem depSub)
        {
            // Access the python instance, call DepFinder from python model, return the Delegate fn requested
            var pythonInstance = depSub._pythonInstance;
            Dictionary<String, Delegate> theBook = pythonInstance.DepFinder(depFnName);
            Delegate page = theBook[depFnName];
            return page;
        }
        #endregion
    }
}