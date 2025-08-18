// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using Utilities;
using HSFUniverse;
using MissionElements;

using System.Xml;
using System.Runtime.CompilerServices;
using log4net;
using Newtonsoft.Json.Linq;
using UserModel;
using System.Reflection.PortableExecutable;


namespace HSFSystem
{
    public abstract class Subsystem
    {
        #region Attributes

        protected static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public virtual String Type { get; set; }
        public bool IsEvaluated { get; set; }
        public virtual Asset Asset { get; set; }
        public virtual List<Subsystem> DependentSubsystems { get; set; } = new List<Subsystem>();
        public virtual string Name { get; set; }
        public virtual Dictionary<string, Delegate> SubsystemDependencyFunctions { get; set; }
        public virtual SystemState NewState { get; set; }
        public virtual MissionElements.Task Task { get; set; }
        public ScriptedSubsystemCS? Loader {get; set; }

        #endregion Attributes

        #region Constructors
        public Subsystem()
        {

        }

        public Subsystem(JObject subsystemJson, Asset asset)
        {
            string msg;
            if (JsonLoader<string>.TryGetValue("name", subsystemJson, out string name))
                this.Name = asset.Name + "." + name.ToLower();
            else
            {
                msg = $"Missing a subsystem 'name' attribute for subsystem in {asset.Name}, {this.Name}";
                Console.WriteLine(msg);
                log.Error(msg);
                throw new ArgumentOutOfRangeException(msg);
            }

            if (JsonLoader<string>.TryGetValue("type", subsystemJson, out string type))
                this.Type = type.ToLower();
            else
            {
                msg = $"Missing a subsystem 'type' attribute for subsystem in {asset.Name}, {this.Name}";
                Console.WriteLine(msg);
                log.Error(msg);
                throw new ArgumentOutOfRangeException(msg);
            }

            this.Asset = asset;
            this.DependentSubsystems = new List<Subsystem>();
            this.SubsystemDependencyFunctions = new Dictionary<string, Delegate>();
            this.AddDependencyCollector();
        }

        #endregion

        #region Methods
        public virtual Subsystem clone()
        {
            return DeepCopy.Copy<Subsystem>(this);
        }

        public abstract void SetStateVariableKey(dynamic stateKey);

        /// <summary>
        /// The default canPerform method. 
        /// Should be used to check if all dependent subsystems can perform and extended by subsystem implementations.
        /// </summary>
        /// <param name="proposedEvent"></param>
        /// <param name="environment"></param>
        /// <returns></returns>
        public abstract bool CanPerform(Event proposedEvent, Domain environment);
        //{
            //foreach (var sub in DependentSubsystems)
            //{
            //    if (!sub.IsEvaluated)// && !sub.GetType().Equals(typeof(ScriptedSubsystem)))
            //        if (sub.CanPerform(proposedEvent, environment) == false)
            //            return false;
            //}
            //_task = proposedEvent.GetAssetTask(Asset); //Find the correct task for the subsystem
            //_newState = proposedEvent.State;
            //IsEvaluated = true;
            //return true;
        //}
        /// <summary>
        /// This method tracks four things:
        /// 1.  Ensure all dependents Subsystems are evaluated before the current Subsystem is evaluates and set the
        ///     IsEvaluated status.
        /// 2.  Calls the CanPerform() method for the subsystem when all dependent subsystems have been evlauted
        /// 3.  Calls the CanPerform() method when a subsystem has no dependent subsystems
        /// 4.  If a Subsystem CanPerform() method returns false, colapse the nested call to false
        /// </summary>
        /// <param name="proposedEvent"></param>
        /// <param name="environment"></param>
        /// <returns></returns>
        public bool CheckDependentSubsystems(Event proposedEvent, Domain environment)
        {
            if (DependentSubsystems.Count == 0)
            {
                IsEvaluated = true;
                Task = proposedEvent.GetAssetTask(Asset); //Find the correct task for the subsystem
                NewState = proposedEvent.State;
                bool result = false;
                try
                {
                    result = this.CanPerform(proposedEvent, environment);
                    // Enforce that Task Start and End are within EVENT task start and end
                    // Also report this out. Where to report? --> 
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                //  Need to deal with this issue in next update
                //double te = proposedEvent.GetTaskEnd(Asset);
                //double ee = proposedEvent.GetEventEnd(Asset);
                //proposedEvent.SetEventEnd(Asset, Math.Max(te, ee));
                return result;
            }
            else
            {
                foreach (var sub in DependentSubsystems)
                {
                    if (!sub.IsEvaluated)// && !sub.GetType().Equals(typeof(ScriptedSubsystem)))
                    {
                        if (sub.CheckDependentSubsystems(proposedEvent, environment))
                        {
                            IsEvaluated = true;
                            Task = proposedEvent.GetAssetTask(Asset); //Find the correct task for the subsystem
                            NewState = proposedEvent.State;
                            if (!CanPerform(proposedEvent, environment))
                                return false;
                            //  Need to deal with this issue in next update
                            //double te = proposedEvent.GetTaskEnd(Asset);
                            //double ee = proposedEvent.GetEventEnd(Asset);
                            //proposedEvent.SetEventEnd(Asset, Math.Max(te, ee));
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

        }
        /// <summary>
        /// The default canExtend function. May be over written for additional functionality.
        /// </summary>
        /// <param name="newState"></param>
        /// <param name="position"></param>
        /// <param name="environment"></param>
        /// <param name="evalToTime"></param>
        /// <returns></returns>
        public virtual bool CanExtend(Event proposedEvent, Domain environment, double evalToTime)
        {
            if (proposedEvent.GetEventEnd(Asset) < evalToTime)
                proposedEvent.SetEventEnd(Asset, evalToTime);
            return true;
        }

        /// <summary>
        /// Add the dependency collector to the dependency dictionary
        /// </summary>
        /// <param name="Deps"></param>
        /// <param name="FuncNames"></param>
        public void AddDependencyCollector()
        {
            SubsystemDependencyFunctions.Add("DepCollector", new Func<Event, HSFProfile<double>>(DependencyCollector));
        }

        /// <summary>
        /// Default Dependency Collector simply sums the results of the dependecy functions
        /// </summary>
        /// <param name="currentState"></param>
        /// <returns></returns>
        public virtual HSFProfile<double> DependencyCollector(Event currentEvent)
        {
            if (SubsystemDependencyFunctions.Count == 0)
                throw new MissingMemberException("You may not call the dependency collector in your can perform because you have not specified any dependency functions for " + Name);
            HSFProfile<double> outProf = new HSFProfile<double>();
            foreach (var dep in SubsystemDependencyFunctions)
            {
                if (!dep.Key.Equals("DepCollector"))
                {
                    HSFProfile<double> temp = (HSFProfile<double>)dep.Value.DynamicInvoke(currentEvent);
                    outProf += temp;
                }
            }

            return outProf;
        }

        public void GetParameterByName<T>(JObject subsysJson, string name, out T variable)
        {
            variable = default;

            if (JsonLoader<JArray>.TryGetValue("parameters", subsysJson, out JArray parameters))

            {
                foreach (JObject parameter in parameters)
                {
                    if (JsonLoader<string>.TryGetValue("name", parameter, out string varName))
                    {
                        if (varName == name)
                        {
                            JsonLoader<double>.TryGetValue("value", parameter, out  variable);;
                        }
                    }
                    else
                    {
                        string msg = $"Missing the subsystem 'parameter' attribute, '{name}' for subsystem {this.Name}";
                        Console.WriteLine(msg);
                        log.Error(msg);
                        throw new ArgumentOutOfRangeException(msg);
                    }
                }
            }
        }
        #endregion
    }
}//HSFSubsystem