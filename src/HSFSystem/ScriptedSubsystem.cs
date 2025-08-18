// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using IronPython.Hosting;
using MissionElements;
using HSFUniverse;
using Utilities;
using Newtonsoft.Json.Linq;
using UserModel;
using System.Security.Permissions;

namespace HSFSystem
{
    public class ScriptedSubsystem : Subsystem
    {
        #region Attributes
        // A reference to the python scripted class
        private dynamic _pythonInstance;

        // Overide the accessors in order to modify the python instance

        public override String Type
        {
            get { return _pythonInstance.Type; }
            set { _pythonInstance.Type = value; }
        }

        public override Asset Asset
        {
            get { return _pythonInstance.Asset; }
            set { _pythonInstance.Asset = value; }
        }
        public override List<Subsystem> DependentSubsystems
        {
            get { return (List<Subsystem>)_pythonInstance.DependentSubsystems; }
            set { _pythonInstance.DependentSubsystems = (List<Subsystem>)value; }
        }

        public override Dictionary<string, Delegate> SubsystemDependencyFunctions
        {
            get { return (Dictionary<string, Delegate>)_pythonInstance.SubsystemDependencyFunctions; }
            set { _pythonInstance.SubsystemDependencyFunctions = (Dictionary<string, Delegate>)value; }
        }

        public override string Name
        {
            get { return (string)_pythonInstance.Name; }
            set { _pythonInstance.Name = (string)value; }
        }

        public override SystemState NewState
        {
            get { return (SystemState)_pythonInstance.NewState; }
            set { _pythonInstance.NewState = value; }
        }

        public override MissionElements.Task Task
        {
            get { return (MissionElements.Task)_pythonInstance.Task; }
            set { _pythonInstance.Task = value; }
        }

        private readonly string src = "";
        private readonly string className = "";
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor to build ScriptedSubsystem from JSON input
        /// </summary>
        /// <param name="scriptedSubsystemJson"></param>
        /// <param name="asset"></param>
        /// <exception cref="ArgumentException"></exception>
        public ScriptedSubsystem(JObject scriptedSubsystemJson, Asset asset)
        {
            StringComparison stringCompare = StringComparison.CurrentCultureIgnoreCase;

            if (scriptedSubsystemJson.TryGetValue("src", stringCompare, out JToken srcJason))
            {
                this.src = srcJason.ToString().Replace('\\','/');
                if (!File.Exists(src))
                {
                    this.src = Path.Combine(Utilities.DevEnvironment.RepoDirectory, src); //Replace backslashes with forward slashes, if applicable
                }
            }

            // else if (scriptedSubsystemJson.TryGetValue("fullpath", stringCompare, out JToken fullpathJason))
            // {
            // }
            else
            {
                Console.WriteLine($"Error loading subsytem of type {this.Type}, missing Src attribute");
                throw new ArgumentException($"Error loading subsytem of type {this.Type}, missing Src attribute");
            }

            if (scriptedSubsystemJson.TryGetValue("className", stringCompare, out JToken classNameJason))
                this.className = classNameJason.ToString();
            else
            {
                Console.WriteLine($"Error loading subsytem of type {this.Type}, missing ClassName attribute");
                throw new ArgumentException($"Error loading subsytem of type {this.Type}, missing ClassName attribute");
            }

            // What needs to be part of the python instance and what is part of the C# get/set?
            InitSubsystem(scriptedSubsystemJson);

            Delegate depCollector = _pythonInstance.GetDependencyCollector();
            this.SubsystemDependencyFunctions = new Dictionary<string, Delegate>
            {
                { "DepCollector", depCollector }
            };

            this.Asset = asset;
            string msg;
            if (JsonLoader<string>.TryGetValue("name", scriptedSubsystemJson, out string name))
                this.Name = asset.Name + "." + name.ToLower();
            else
            {
                msg = $"Missing a subsystem 'name' attribute for subsystem in {asset.Name}, {this.Name}";
                Console.WriteLine(msg);
                log.Error(msg);
                throw new ArgumentOutOfRangeException(msg);
            }

            if (JsonLoader<string>.TryGetValue("type", scriptedSubsystemJson, out string type))
                this.Type = type.ToLower();
            else
            {
                msg = $"Missing a subsystem 'type' attribute for subsystem in {asset.Name}, {this.Name}";
                Console.WriteLine(msg);
                log.Error(msg);
                throw new ArgumentOutOfRangeException(msg);
            }

            this.DependentSubsystems = new List<Subsystem>();
        }

        private void InitSubsystem(params object[] parameters)
        {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            var ops = engine.Operations;
            // Search paths are for importing modules from python scripts, not for executing python subsystem files
            var p = engine.GetSearchPaths();
            p.Add(AppDomain.CurrentDomain.BaseDirectory + @"..\..\..\PythonSubs");
            p.Add(AppDomain.CurrentDomain.BaseDirectory + @"..\..\..\");
            p.Add(AppDomain.CurrentDomain.BaseDirectory + @"..\..\..\samples\Aeolus\pythonScripts");

            // Trying to use these so we can call numpy, etc...  Does not seem to work 8/31/23
            p.Add(@"C:\Python310\Lib\site-packages\");
            p.Add(@"C:\Python310\Lib");

            engine.SetSearchPaths(p);
            engine.ExecuteFile(this.src, scope);
            var pythonType = scope.GetVariable(className);
            // Look into this, string matters - related to file name, I think
            _pythonInstance = ops.CreateInstance(pythonType);//, parameters);

        }
        #endregion

        #region Methods
        //NEED TO FIX THIS...
        public override void SetStateVariableKey( dynamic stateKey)
        {
            throw new NotImplementedException();
        }
        public void SetStateVariable<T>(ScriptedSubsystemHelper HSFHelper, string StateName, StateVariableKey<T> key)
        {
            HSFHelper.PythonInstance.SetStateVariable(_pythonInstance, StateName, key);

        }

        public void SetSubsystemParameter(ScriptedSubsystemHelper HSFHelper, string paramenterName, dynamic parameterValue)
        {
            HSFHelper.PythonInstance.SetStateVariable(_pythonInstance, paramenterName, parameterValue);
        }

        public override bool CanPerform(Event proposedEvent, Domain environment)
        {

            // Call the can perform method that is in the python class
            bool perform = false;
            try
            {
                perform = _pythonInstance.CanPerform(proposedEvent, environment);
            } catch (Exception ex)
            {
                Console.WriteLine($"Error in subsystem CanPerform call {this.Name} for task type {Task.Type}. With exception {ex}");
            }
            return perform;
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
