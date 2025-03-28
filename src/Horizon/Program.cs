﻿// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using System.Linq;
using HSFScheduler;
using MissionElements;
using UserModel;
using HSFUniverse;
//using HSFSubsystem;
using HSFSystem;
using log4net;
using Utilities;
using Microsoft.Scripting.Actions.Calls;
using System.Net.Http.Headers;
using Task = MissionElements.Task; // error CS0104: 'Task' is an ambiguous reference between 'MissionElements.Task' and 'System.Threading.Tasks.Task'
using System.Diagnostics;
using System.CodeDom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
//using System.Web.Configuration;
using IronPython.Compiler.Ast;
using System.Diagnostics.Eventing.Reader;
//using System.Net.Configuration;

namespace Horizon
{
    public class Program
    {
        public ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public string SimulationFilePath { get; set; }
        public string TaskDeckFilePath { get; set; }
        public string ModelFilePath { get; set; }
        public string OutputPath { get; set; }

        // Load the environment. First check if there is an ENVIRONMENT XMLNode in the input file
        public Domain SystemUniverse { get; set; }

        //Create singleton dependency dictionary
        public Dependency Dependencies { get; } = Dependency.Instance;

        // Initialize Lists to hold assets, subsystems and evaluators
        public List<Asset> AssetList { get; set; } = new List<Asset>();
        public List<Subsystem> SubList { get; set; } = new List<Subsystem>();

        // Maps used to set up preceeding nodes
        //public Dictionary<ISubsystem, XmlNode> SubsystemXMLNodeMap { get; set; } = new Dictionary<ISubsystem, XmlNode>(); //Depreciated (?)

        public List<KeyValuePair<string, string>> DependencyList { get; set; } = new List<KeyValuePair<string, string>>();
        public List<KeyValuePair<string, string>> DependencyFcnList { get; set; } = new List<KeyValuePair<string, string>>();
        
        // Create Constraint list 
        public List<Constraint> ConstraintsList { get; set; } = new List<Constraint>();

        //Create Lists to hold all the dependency nodes to be parsed later
        //List<XmlNode> _depNodes = new List<XmlNode>();
        public SystemState InitialSysState { get; set; } = new SystemState();

        //XmlNode _evaluatorNode; //Depreciated (?)
        public Evaluator SchedEvaluator;
        public List<SystemSchedule> Schedules { get; set; }
        public SystemClass SimSystem { get; set; }
        public Stack<Task> SystemTasks { get; set; } = new Stack<Task>();

        public static int Main(string[] args) //
        {
            Program program = new Program();

            // Begin the Logger
            program.log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            program.log.Info("STARTING HSF RUN"); //Do not delete
            
            List<string> argsList = args.ToList();
            program.InitInput(argsList);
            program.InitOutput(argsList);
            program.LoadScenario();
            program.LoadTasks();
            program.LoadSubsystems();
            program.LoadEvaluator();
            program.CreateSchedules();
            double maxSched = program.EvaluateSchedules();

            int i = 0;
            //Morgan's Way
            Console.WriteLine($"Publishing simulation results to {program.OutputPath}");
            StreamWriter sw = File.CreateText(program.OutputPath);
            foreach (SystemSchedule sched in program.Schedules)
            {
                sw.WriteLine("Schedule Number: " + i + "Schedule Value: " + program.Schedules[i].ScheduleValue);
                foreach (var eit in sched.AllStates.Events)
                {
                    if (i < 5)//just compare the first 5 schedules for now
                    {
                        sw.WriteLine(eit.ToString());
                    }
                }
                i++;
            }
            program.log.Info("Max Schedule Value: " + maxSched);

            // Mehiel's way
            string stateDataFilePath = Path.Combine(DevEnvironment.RepoDirectory, "output/HorizonLog/Scratch");// + string.Format("output-{0:yyyy-MM-dd-hh-mm-ss}", DateTime.Now);
            SystemSchedule.WriteSchedule(program.Schedules[0], stateDataFilePath);

            //  Move this to a method that always writes out data about the dynamic state of assets, the target dynamic state data, other data?
            //var csv = new StringBuilder();
            //csv.Clear();
            //foreach (var asset in program.simSystem.Assets)
            //{
            //    File.WriteAllText(@"..\..\..\" + asset.Name + "_dynamicStateData.csv", asset.AssetDynamicState.ToString());
            //}

            //Console.ReadKey();
            return 0;
        }
        public void InitInput(List<string> argsList)
        {
            // This would be in a config file - not used right now (4/26/24)
            string basePath = @"C:\Users\emehiel\Source\Repos\Horizon8\";
            string subPath = "";

            if (argsList.Contains("-scen"))
            {
                List<string> tags = new List<string>() { "-subpath", "-s", "-t", "-m", "-o" };
                foreach (var tag in tags)
                {
                    if (argsList.Contains(tag))
                    {
                        Console.WriteLine("The input argument -scen cannot be used with other arguments.");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                }
            }

            if (argsList.Contains("-subpath"))
            {
                int indx = argsList.IndexOf("-subpath");
                subPath = Path.Combine(basePath, argsList[indx + 1]);
            }

            if (argsList.Count == 0)
            {
                argsList.Add("-scen");
                // Set this to the default scenario you would like to run
                string scenarioName = "Aeolus_CS";
                argsList.Add(scenarioName);
                // This is the path or "subpath" to the Horizon/samples/ directory where the simulation input files are stored.
                subPath = Path.Combine(DevEnvironment.RepoDirectory, "samples");
            }

            bool simulationSet = false, targetSet = false, modelSet = false; bool outputSet = false;

            // Get the input filenames
            int i = 0;
            foreach (var input in argsList)
            {
                i++;
                switch (input)
                {
                    case "-scen":
                        switch (argsList[i])
                        {
                            case "Aeolus_scripted":
                                // Set Defaults
                                //subpath = @"..\..\..\..\samples\Aeolus\";
                                subPath = Path.Combine(DevEnvironment.RepoDirectory, "samples");
                                subPath = Path.Combine(subPath, "Aeolus");
                                SimulationFilePath = Path.Combine(subPath, "AeolusSimulationInput.json");
                                TaskDeckFilePath = Path.Combine(subPath, "AeolusTasks.json");
                                // Asset 1 Scripted, Asset 2 Scripted
                                ModelFilePath = Path.Combine(subPath, "DSAC_Static_Scripted.json");
                                simulationSet = true;
                                targetSet = true;
                                modelSet = true;
                                break;
                            case "Aeolus_CS":
                                // Set Defaults
                                //subpath = @"..\..\..\..\samples\Aeolus\";
                                subPath = Path.Combine(DevEnvironment.RepoDirectory, "samples");
                                subPath = Path.Combine(subPath, "Aeolus");
                                SimulationFilePath = Path.Combine(subPath, "AeolusSimulationInput.json");
                                TaskDeckFilePath = Path.Combine(subPath, "AeolusTasks.json");
                                // Asset 1 C#, Asset 2 C#
                                ModelFilePath = Path.Combine(subPath, "DSAC_Static_Mod.json");
                                // Asset 1 mix Scripted/C#, Asset 2 C#
                                //ModelInputFilePath = subpath + @"DSAC_Static_Mod_PartialScripted.xml"; 
                                // Asset 1 C#, Asset 2 C#
                                //ModelInputFilePath = subpath + @"DSAC_Static_Mod.xml";
                                simulationSet = true;
                                targetSet = true;
                                modelSet = true;
                                break;
                            case "myFirstHSFProject":
                                // Set myFirstHSFProject file paths
                                //subpath = @"..\..\..\..\samples\myFirstHSFProject\";
                                subPath = Path.Combine(DevEnvironment.RepoDirectory, "samples");
                                subPath = Path.Combine(subPath, "myFirstHSFProject");
                                SimulationFilePath = Path.Combine(subPath, "myFirstHSFScenario.json");
                                TaskDeckFilePath = Path.Combine(subPath, "myFirstHSFTaskList.json");
                                ModelFilePath = Path.Combine(subPath, "myFirstHSFSystem.json");
                                simulationSet = true;
                                targetSet = true;
                                modelSet = true;
                                break;
                            case "myFirstHSFProjectConstraint":
                                // Set myFirstHSFProjectConstraint file paths
                                //subpath = @"..\..\..\..\samples\myFirstHSFProjectConstraint\";
                                subPath = Path.Combine(DevEnvironment.RepoDirectory, "samples");
                                subPath = Path.Combine(subPath, "myFirstHSFProjectConstraint");
                                SimulationFilePath = Path.Combine(subPath, "myFirstHSFScenario.json");
                                TaskDeckFilePath = Path.Combine(subPath, "myFirstHSFTaskList.json");
                                ModelFilePath = Path.Combine(subPath, "myFirstHSFSystemLook.json");
                                simulationSet = true;
                                targetSet = true;
                                modelSet = true;
                                break;
                            case "myFirstHSFProjectDependency":
                                // Set myFirstHSFProjectDependency file paths
                                //subpath = @"..\..\..\..\samples\myFirstHSFProjectDependency\";
                                subPath = Path.Combine(DevEnvironment.RepoDirectory, "samples");
                                subPath = Path.Combine(subPath, "myFirstHSFProjectDependency");
                                SimulationFilePath = Path.Combine(subPath, "myFirstHSFScenario.json");
                                TaskDeckFilePath = Path.Combine(subPath, "myFirstHSFTargetDeck.json");
                                ModelFilePath = Path.Combine(subPath, "myFirstHSFSystemDependency.json");
                                simulationSet = true;
                                targetSet = true;
                                modelSet = true;
                                break;
                        }
                        break;
                    case "-s":
                        SimulationFilePath = Path.Combine(subPath, argsList[i]);
                        simulationSet = true;
                        break;
                    case "-t":
                        TaskDeckFilePath = Path.Combine(subPath, argsList[i]);
                        targetSet = true;
                        break;
                    case "-m":
                        ModelFilePath = Path.Combine(subPath, argsList[i]);
                        modelSet = true;
                        break;
                    case "-o": // In the CLI args-in, this would be set as a directory path. 
                        OutputPath = Path.Combine(subPath, argsList[i]);
                        outputSet = true;
                        break;
                }
            }
            ///add usage statement

            if (simulationSet)
            {
                Console.WriteLine("Using simulation file: " + SimulationFilePath);
                log.Info("Using simulation file: " + SimulationFilePath);
            }

            if (targetSet)
            {
                Console.WriteLine("Using target deck file: " + TaskDeckFilePath);
                log.Info("Using simulation file: " + TaskDeckFilePath);
            }

            if (modelSet)
            {
                Console.WriteLine("Using model file: " + ModelFilePath);
                log.Info("Using model file: " + ModelFilePath);
            }
            if (outputSet)
            {
                Console.WriteLine("Using output path: " + OutputPath);
                log.Info("Using output path: " + OutputPath);
            }

        }
        public void InitOutput(List<string> argsList)
        {
            // Initialize Output File
            var outputFileName = string.Format("output-{0:yyyy-MM-dd}-*", DateTime.Now);
            string outputPath = "";

            // This is the way that works with initInput args in only. Using other way for now
            // if (this.OutputPath != null) {outputPath = this.OutputPath; } // Use user-specified if applicable // Update the outputPath to the user specified input, if applicable
            // else {outputPath = Path.Combine(DevEnvironment.RepoDirectory, "output/HorizonLog");} // Otherwise use default
            // Directory.CreateDirectory(outputPath); // Create the output directory if it doesn't already exist. 

            if (argsList.Contains("-o"))
            {
                int indx = argsList.IndexOf("-o");
                outputPath = argsList[indx + 1];
            }
            else
            {
                outputPath = Path.Combine(DevEnvironment.RepoDirectory, "output/HorizonLog");
            }
            // Create the output directory if it doesn't already exist.
            Directory.CreateDirectory(outputPath); 

            // Filter out other output files for naming ocnvention
            var txt = ".txt";
            string[] fileNames = System.IO.Directory.GetFiles(outputPath, outputFileName, System.IO.SearchOption.TopDirectoryOnly);
            double number = 0;
            foreach (var fileName in fileNames)
            {
                char version = fileName[fileName.Length - txt.Length - 10];
                if (number < Char.GetNumericValue(version))
                    number = Char.GetNumericValue(version);
            }
            number++;
            outputFileName = outputFileName.Remove(outputFileName.Length - 1) + number + string.Format("_{0:HH:mm:ss}",DateTime.Now);
            outputFileName = outputFileName.Replace(':', '_');
            outputPath = Path.Combine(outputPath,outputFileName + txt); 
            this.OutputPath = outputPath;
        }

        public void LoadScenario()
        {
            StreamReader jsonStream = new StreamReader(SimulationFilePath);

            JObject scenarioJson = JObject.Parse(jsonStream.ReadToEnd());

            // Load Scenario Name
            if (JsonLoader<string>.TryGetValue("name", scenarioJson, out string name))
            {
                Console.WriteLine($"Loading scenario {name}");
                log.Info($"Loading scenario {name}");
            }
            else
            {
                string msg = $"Scenario {name} must contain a name.";
                log.Fatal(msg);
                Console.WriteLine(msg);
                throw new ArgumentException(msg);
            }
            // Load Base Dependencies
            if(JsonLoader<JObject>.TryGetValue("dependencies", scenarioJson, out JObject dependenciesJson))
            {
                Console.WriteLine($"Base Dependecies Loaded for {name} at {SimulationFilePath}");
                log.Info($"Base Dependecies Loaded for {name} at {SimulationFilePath}");
            }
            else
            {
                string msg = $"Base Dependecies not found for {name} at {SimulationFilePath}";
                log.Fatal(msg);
                Console.WriteLine(msg);
                throw new ArgumentException(msg);
            }

            // Load Simulation Parameters
            if (JsonLoader<JObject>.TryGetValue("simulationParameters", scenarioJson, out JObject simulationJson))
            {
                SimParameters.LoadSimulationJson(simulationJson, name);
            }
            else
            {
                string msg = $"Simulation Parameters are not found in input files for scenario {name}.";
                log.Fatal(msg);
                Console.WriteLine(msg);
                throw new ArgumentException(msg);
            }

            // Load Scheduler Parameters
            if (JsonLoader<JObject>.TryGetValue("schedulerParameters", scenarioJson, out JObject schedulerJson))
            {
                SchedParameters.LoadScheduleJson(schedulerJson);
            }
            else
            {
                string msg = $"Scheduler Parameters are not found in input files for scenario {name}.";
                log.Fatal(msg);
                Console.WriteLine(msg);
                throw new ArgumentException(msg);
            }

        }
        public void LoadTasks()
        {
            StreamReader jsonStream = new StreamReader(TaskDeckFilePath);
            JObject taskListJson = JObject.Parse(jsonStream.ReadToEnd());

            if (!Task.LoadTasks(taskListJson, SystemTasks))
            {
                log.Fatal("Error loading Tasks at LoadTasks()");
                throw new Exception("Error loading Tasks at LoadTasks()");
            }

        }
        public void LoadSubsystems()
        {
            StreamReader jsonStream = new StreamReader(ModelFilePath);
            JObject scenarioJson = JObject.Parse(jsonStream.ReadToEnd());
            string msg;

            if (scenarioJson != null)
            {
                if (JsonLoader<JObject>.TryGetValue("model", scenarioJson, out JObject modelJson))
                {
                    // Load Environment
                    if (JsonLoader<JObject>.TryGetValue("environment", modelJson, out JObject environmentJson))
                    {
                        SystemUniverse = EnvironmentFactory.GetUniverseClass(environmentJson);
                    }
                    else
                    {
                        SystemUniverse = new SpaceEnvironment();
                        Console.WriteLine("Default Space Environment Loaded");
                        log.Info("Default Space Environment Loaded");
                    }

                    // Load Assets
                    if (JsonLoader<JToken>.TryGetValue("assets", modelJson, out JToken assetsListJson))
                    {
                        foreach (JObject assetJson in assetsListJson)
                        {
                            Asset asset = new Asset(assetJson);
                            asset.AssetDynamicState.Eoms.Environment = SystemUniverse;
                            AssetList.Add(asset);

                            // Load Subsystems
                            if (JsonLoader<JToken>.TryGetValue("subsystems", assetJson, out JToken subsystemListJson))
                            {
                                foreach (JObject subsystemJson in subsystemListJson)
                                {
                                    Subsystem subsys = SubsystemFactory.GetSubsystem(subsystemJson, asset);
                                    SubList.Add(subsys);

                                    // Load Subsystem States (Formerly ICs)
                                    if (JsonLoader<JToken>.TryGetValue("states", subsystemJson, out JToken stateListJson))
                                    {
                                        foreach (JObject stateJson in stateListJson)
                                        {
                                            // Parse state node for key name and state type, add the key to the subsys's list of keys, return the key name
                                            string keyName = SubsystemFactory.SetStateKeys(stateJson, subsys);
                                            // Use key name and state type to set initial conditions 
                                            InitialSysState.SetInitialSystemState(stateJson, keyName);
                                        }
                                    }
                                    else
                                    {
                                        msg = $"Warning: Subsystem {subsys.Name} loaded with no states";
                                        Console.WriteLine(msg);
                                        log.Warn(msg);
                                    }

                                    // Load Subsystem Parameters
                                    if (subsys.Type == "scripted")
                                    {
                                        // Load Subsystem Parameters                        
                                        if (JsonLoader<JToken>.TryGetValue("parameters", subsystemJson, out JToken parameterListJson))
                                            foreach (JObject parameterJson in parameterListJson)
                                                SubsystemFactory.SetParameters(parameterJson, subsys);
                                        else
                                        {
                                            msg = $"Warning: Subsystem {subsys.Name} loaded with no parameters";
                                            Console.WriteLine(msg);
                                            log.Warn(msg);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                msg = $"Error loading model for {SimParameters.ScenarioName}.  Error loading subsystems for asset, {asset.Name}";
                                Console.WriteLine(msg);
                                log.Fatal(msg);
                                throw new ArgumentException(msg);
                            }
                            // Load Constraints
                            if (JsonLoader<JToken>.TryGetValue("constraints", assetJson, out JToken constraintListJson))
                                foreach (JObject constraintJson in constraintListJson)
                                    ConstraintsList.Add(ConstraintFactory.GetConstraint(constraintJson, SubList, asset.Name));
                            else
                            {
                                msg = $"Warning: Asset {asset.Name} loaded with no constraints";
                                Console.WriteLine(msg);
                                log.Warn(msg);
                            }
                        }

                        // give some numbers here
                        msg = $"Environment, {AssetList.Count} Assets, Subsystems, and Constraints Loaded";
                        Console.WriteLine(msg);
                        log.Info(msg);
                    }
                    else
                    {
                        msg = $"Error loading assets for {SimParameters.ScenarioName}.";
                        Console.WriteLine(msg);
                        log.Fatal(msg);
                        throw new ArgumentException(msg);
                    }

                    // Load Dependencies
                    if (JsonLoader<JToken>.TryGetValue("dependencies", modelJson, out JToken dependencyListJson))
                    {
                        foreach (JObject dependencyJson in dependencyListJson)
                            SubsystemFactory.SetDependencies(dependencyJson, SubList);

                        Console.WriteLine("Dependencies Loaded");
                        log.Info("Dependencies Loaded");
                    }
                    else
                    {
                        msg = $"Warning: {SimParameters.ScenarioName} loaded with no dependencies.";
                        Console.WriteLine(msg);
                        log.Warn(msg);
                    }
                }
                else
                {
                    msg = $"Error loading model for {SimParameters.ScenarioName}.  No model element found in Model File.";
                    Console.WriteLine(msg);
                    log.Fatal(msg);
                    throw new ArgumentException(msg);
                }
            }
            else
            {
                msg = $"Error loading model for {SimParameters.ScenarioName}.  No model file found or loaded.";
                Console.WriteLine(msg);
                log.Fatal(msg);
                throw new ArgumentException(msg);
            }
        }

        public void LoadEvaluator()
        {
            StreamReader jsonStream = new StreamReader(ModelFilePath);
            JObject scenarioJson = JObject.Parse(jsonStream.ReadToEnd());

            if (scenarioJson != null)
            {
                if (JsonLoader<JObject>.TryGetValue("Model", scenarioJson, out JObject modelJson))
                {
                    // Load Environment
                    if(JsonLoader<JObject>.TryGetValue("Evaluator", modelJson, out JObject evaluatorJson))
                    {
                        SchedEvaluator = EvaluatorFactory.GetEvaluator(evaluatorJson, SubList);
                        Console.WriteLine("Evaluator Loaded");
                        log.Info("Evaluator Loaded");
                    }
                    else
                    {
                        SchedEvaluator = new DefaultEvaluator(); // ensures at least default is used
                        Console.WriteLine("Default Evaluator Loaded");
                    }
                }
            }
            
            //var modelInputXMLNode = XmlParser.GetModelNode(ModelFilePath);
            //var evalNodes = modelInputXMLNode.SelectNodes("EVALUATOR");
            //if (evalNodes.Count > 1)
            //{
            //    throw new NotImplementedException("Too many evaluators in input!");
            //    Console.WriteLine("Too many evaluators in input");
            //    log.Info("Too many evaluators in input");
            //}
            //else
            //{
            //    SchedEvaluator = EvaluatorFactory.GetEvaluator(evalNodes[0],SubList);
            //    Console.WriteLine("Evaluator Loaded");
            //    log.Info("Evaluator Loaded");
            //}
        }
        public void CreateSchedules()
        {
            SimSystem = new SystemClass(AssetList, SubList, ConstraintsList, SystemUniverse);

            if (SimSystem.CheckForCircularDependencies())
                throw new NotFiniteNumberException("System has circular dependencies! Please correct then try again.");

            Scheduler _scheduler = new Scheduler(SchedEvaluator);
            Schedules = _scheduler.GenerateSchedules(SimSystem, SystemTasks, InitialSysState);
        }
        public double EvaluateSchedules()
        {
            // Evaluate the schedules and set their values
            foreach (SystemSchedule systemSchedule in Schedules)
            {
                systemSchedule.ScheduleValue = SchedEvaluator.Evaluate(systemSchedule);
                bool canExtendUntilEnd = true;
                // Extend the subsystem states to the end of the simulation 
                foreach (var subsystem in SimSystem.Subsystems)
                {
                    if (systemSchedule.AllStates.Events.Count > 0)
                            if (!subsystem.CanExtend(systemSchedule.AllStates.Events.Peek(), (Domain)SimSystem.Environment, SimParameters.SimEndSeconds))
                            log.Error("Cannot Extend " + subsystem.Name + " to end of simulation");
                }
            }

            // Sort the sysScheds by their values
            Schedules.Sort((x, y) => x.ScheduleValue.CompareTo(y.ScheduleValue));
            Schedules.Reverse();
            double maxSched = Schedules[0].ScheduleValue;
            return maxSched;
        }
    }
}






// // Copyright (c) 2016 California Polytechnic State University
// // Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Xml;
// using System.Text;
// using HSFScheduler;
// using MissionElements;
// using UserModel;
// using HSFUniverse;
// using HSFSystem;
// using log4net;
// using Utilities;
// using Microsoft.Scripting.Actions.Calls;
// using System.Net.Http.Headers;
// using Task = MissionElements.Task; // error CS0104: 'Task' is an ambiguous reference between 'MissionElements.Task' and 'System.Threading.Tasks.Task'
// using System.Diagnostics;
// using System.CodeDom;

// namespace Horizon
// {
//     public class Program
//     {
//         public ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

//         public string SimulationInputFilePath { get; set; }
//         public string TargetDeckFilePath { get; set; }
//         public string ModelInputFilePath { get; set; }
//         public string OutputPath { get; set; }

//         // Load the environment. First check if there is an ENVIRONMENT XMLNode in the input file
//         public Domain SystemUniverse { get; set; }

//         //Create singleton dependency dictionary
//         public Dependency Dependencies { get; } = Dependency.Instance;

//         // Initialize Lists to hold assets, subsystems and evaluators
//         public List<Asset> AssetList { get; set; } = new List<Asset>();
//         public List<Subsystem> SubList { get; set; } = new List<Subsystem>();

//         // Maps used to set up preceeding nodes
//         //public Dictionary<ISubsystem, XmlNode> SubsystemXMLNodeMap { get; set; } = new Dictionary<ISubsystem, XmlNode>(); //Depreciated (?)

//         public List<KeyValuePair<string, string>> DependencyList { get; set; } = new List<KeyValuePair<string, string>>();
//         public List<KeyValuePair<string, string>> DependencyFcnList { get; set; } = new List<KeyValuePair<string, string>>();
        
//         // Create Constraint list 
//         public List<Constraint> ConstraintsList { get; set; } = new List<Constraint>();

//         //Create Lists to hold all the dependency nodes to be parsed later
//         //List<XmlNode> _depNodes = new List<XmlNode>();
//         public SystemState InitialSysState { get; set; } = new SystemState();

//         //XmlNode _evaluatorNode; //Depreciated (?)
//         public Evaluator SchedEvaluator;
//         public List<SystemSchedule> Schedules { get; set; }
//         public SystemClass SimSystem { get; set; }

//         public Stack<Task> SystemTasks { get; set; } = new Stack<Task>();

//         // Main Program
//         public static int Main(string[] args) //
//         {
//             Program program = new Program();

//             // Begin the Logger
//             program.log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
//             program.log.Info("STARTING HSF RUN"); //Do not delete

//             List<string> argsList = args.ToList();
//             program.InitInput(argsList);
//             program.InitOutput();
//             program.LoadScenario();
//             program.LoadTargets();
//             program.LoadSubsystems();
//             program.LoadEvaluator();
//             program.CreateSchedules();
//             double maxSched = program.EvaluateSchedules();

//             int i = 0;
//             //Morgan's Way
//             StreamWriter sw = File.CreateText(program.OutputPath);
//             foreach (SystemSchedule sched in program.Schedules)
//             {
//                 sw.WriteLine("Schedule Number: " + i + "Schedule Value: " + program.Schedules[i].ScheduleValue);
//                 foreach (var eit in sched.AllStates.Events)
//                 {
//                     if (i < 5)//just compare the first 5 schedules for now
//                     {
//                         sw.WriteLine(eit.ToString());
//                     }
//                 }
//                 i++;
//             }
//             program.log.Info("Max Schedule Value: " + maxSched);

//             // Mehiel's way
//             string stateDataFilePath = Path.Combine(DevEnvironment.RepoDirectory, "output/HorizonLog/Scratch");// + string.Format("output-{0:yyyy-MM-dd-hh-mm-ss}", DateTime.Now);
//             SystemSchedule.WriteSchedule(program.Schedules[0], stateDataFilePath);

//             //  Move this to a method that always writes out data about the dynamic state of assets, the target dynamic state data, other data?
//             //var csv = new StringBuilder();
//             //csv.Clear();
//             //foreach (var asset in program.simSystem.Assets)
//             //{
//             //    File.WriteAllText(@"..\..\..\" + asset.Name + "_dynamicStateData.csv", asset.AssetDynamicState.ToString());
//             //}

//             //Console.ReadKey();
//             return 0;
//         }

//         public void InitInput(List<string> argsList)
//         {
//             // This would be in a config file - not used right now (4/26/24)
//             string basePath = @"C:\Users\emehiel\Source\Repos\Horizon8\";
//             string subPath = "";

//             if (argsList.Contains("-scen"))
//             {
//                 List<string> tags = new List<string>() { "-subpath", "-s", "-t", "-m", "-o" };
//                 foreach (var tag in tags)
//                 {
//                     if (argsList.Contains(tag))
//                     {
//                         Console.WriteLine("The input argument -scen cannot be used with other arguments.");
//                         Console.ReadKey();
//                         Environment.Exit(0);
//                     }
//                 }
//             }

//             if (argsList.Contains("-subpath"))
//             {
//                 int indx = argsList.IndexOf("-subpath");
//                 subPath = Path.Combine(basePath, argsList[indx + 1]);
//             }

//             if (argsList.Count == 0)
//             {
//                 argsList.Add("-scen");
//                 // Set this to the default scenario you would like to run
//                 string scenarioName = "myFirstHSFProject";
//                 argsList.Add(scenarioName);
//                 // This is the path or "subpath" to the Horizon/samples/ directory where the simulation input files are stored.
//                 subPath = Path.Combine(DevEnvironment.RepoDirectory, "samples");
//             }

//             bool simulationSet = false, targetSet = false, modelSet = false; bool outputSet = false;

//             // Get the input filenames
//             int i = 0;
//             foreach (var input in argsList)
//             {
//                 i++;
//                 switch (input)
//                 {
//                     case "-scen":
//                         switch(argsList[i])
//                         { 
//                             case "Aeolus":
//                                 // Set Defaults
//                                 //subpath = @"..\..\..\..\samples\Aeolus\";
//                                 subPath = Path.Combine(subPath, "Aeolus");
//                                 SimulationInputFilePath = Path.Combine(subPath, "AeolusSimulationInput.xml");
//                                 TargetDeckFilePath = Path.Combine(subPath, "v2.2-300targets.xml");
//                                 // Asset 1 Scripted, Asset 2 C#
//                                 ModelInputFilePath = Path.Combine(subPath, "DSAC_Static_Mod_Scripted.xml");
//                                 // Asset 1 mix Scripted/C#, Asset 2 C#
//                                 //ModelInputFilePath = subpath + @"DSAC_Static_Mod_PartialScripted.xml"; 
//                                 // Asset 1 C#, Asset 2 C#
//                                 //ModelInputFilePath = subpath + @"DSAC_Static_Mod.xml";
//                                 simulationSet = true;
//                                 targetSet = true;
//                                 modelSet = true;
//                                 break;
//                             case "myFirstHSFProject":
//                                 // Set myFirstHSFProject file paths
//                                 //subpath = @"..\..\..\..\samples\myFirstHSFProject\";
//                                 subPath = Path.Combine(subPath, "myFirstHSFProject");
//                                 SimulationInputFilePath = Path.Combine(subPath, "myFirstHSFScenario.xml");
//                                 TargetDeckFilePath = Path.Combine(subPath, "myFirstHSFTargetDeck.xml");
//                                 ModelInputFilePath = Path.Combine(subPath, "myFirstHSFSystem.xml");
//                                 simulationSet = true;
//                                 targetSet = true;
//                                 modelSet = true;
//                                 break;
//                             case "myFirstHSFProjectConstraint":
//                                 // Set myFirstHSFProjectConstraint file paths
//                                 //subpath = @"..\..\..\..\samples\myFirstHSFProjectConstraint\";
//                                 subPath = Path.Combine(subPath, "myFirstHSFProjectConstraint");
//                                 SimulationInputFilePath = Path.Combine(subPath, "myFirstHSFScenario.xml");
//                                 TargetDeckFilePath = Path.Combine(subPath, "myFirstHSFTargetDeck.xml");
//                                 ModelInputFilePath = Path.Combine(subPath, "myFirstHSFSystemLook.xml");
//                                 simulationSet = true;
//                                 targetSet = true;
//                                 modelSet = true;
//                                 break;
//                             case "myFirstHSFProjectDependency":
//                                 // Set myFirstHSFProjectDependency file paths
//                                 //subpath = @"..\..\..\..\samples\myFirstHSFProjectDependency\";
//                                 subPath = Path.Combine(subPath, "myFirstHSFProjectDependency");
//                                 SimulationInputFilePath = Path.Combine(subPath, "myFirstHSFScenario.xml");
//                                 TargetDeckFilePath = Path.Combine(subPath, "myFirstHSFTargetDeck.xml");
//                                 ModelInputFilePath = Path.Combine(subPath, "myFirstHSFSystemDependency.xml");
//                                 simulationSet = true;
//                                 targetSet = true;
//                                 modelSet = true;
//                                 break;
//                         }
//                         break;
//                     case "-s":
//                         SimulationInputFilePath = Path.Combine(subPath, argsList[i]);
//                         simulationSet = true;
//                         break;
//                     case "-t":
//                         TargetDeckFilePath = Path.Combine(subPath, argsList[i]);
//                         targetSet = true;
//                         break;
//                     case "-m":
//                         ModelInputFilePath = Path.Combine(subPath, argsList[i]);
//                         modelSet = true;
//                         break;
//                     case "-o": // In the CLI args-in, this would be set as a directory path. 
//                         OutputPath = Path.Combine(subPath, argsList[i]);
//                         outputSet = true;
//                         break;
//                 }
//             }
//             ///add usage statement

//             if (simulationSet)
//             {
//                 Console.WriteLine("Using simulation file: " + SimulationInputFilePath);
//                 log.Info("Using simulation file: " + SimulationInputFilePath);
//             }

//             if (targetSet)
//             {
//                 Console.WriteLine("Using target deck file: " + TargetDeckFilePath);
//                 log.Info("Using simulation file: " + TargetDeckFilePath);
//             }

//             if (modelSet)
//             {
//                 Console.WriteLine("Using model file: " + ModelInputFilePath);
//                 log.Info("Using model file: " + ModelInputFilePath);
//             }
//             if (outputSet)
//             {
//                 Console.WriteLine("Using output path: " + OutputPath);
//                 log.Info("Using output path: " + OutputPath);
//             }

//         }
//         public void InitOutput()
//         {
//             // Initialize Output File
//             var outputFileName = string.Format("output-{0:yyyy-MM-dd}-*", DateTime.Now);
//             string outputPath = Path.Combine(DevEnvironment.RepoDirectory, "output/HorizonLog");
//             if (this.OutputPath != null) {outputPath = this.OutputPath; } // Update the outputPath to the user specified input, if applicable
//             Directory.CreateDirectory(outputPath); // Create the output directory if it doesn't already exist. 
//             var txt = ".txt";
//             string[] fileNames = System.IO.Directory.GetFiles(outputPath, outputFileName, System.IO.SearchOption.TopDirectoryOnly);
//             double number = 0;
//             foreach (var fileName in fileNames)
//             {
//                 char version = fileName[fileName.Length - txt.Length - 1];
//                 if (number < Char.GetNumericValue(version))
//                     number = Char.GetNumericValue(version);
//             }
//             number++;
//             outputFileName = outputFileName.Remove(outputFileName.Length - 1) + number;
//             outputPath += outputFileName + txt;
//             this.OutputPath = outputPath;
//         }

//         public void LoadScenario()
//         {
//             // Find the main input node from the XML input files
//             XmlParser.ParseSimulationInput(SimulationInputFilePath);
//         }
//         public void LoadTargets()
//         {
//             // Load the target deck into the targets list from the XML target deck input file
//             bool targetsLoaded = Task.loadTargetsIntoTaskList(XmlParser.GetTargetNode(TargetDeckFilePath), SystemTasks);
//             if (!targetsLoaded)
//             {
//                 throw new Exception("Targets were not loaded.");
//             }

//         }
//         public void LoadSubsystems()
//         {

//             // Find the main model node from the XML model input file
//             var modelInputXMLNode = XmlParser.GetModelNode(ModelInputFilePath);

//             var environments = modelInputXMLNode.SelectNodes("ENVIRONMENT");

//             // Check if environment count is empty, default is space
//             if (environments.Count == 0)
//             {
//                 SystemUniverse = new SpaceEnvironment();
//                 Console.WriteLine("Default Space Environment Loaded");
//                 log.Info("Default Space Environment Loaded");
//             }
            
//             // Load Environments
//             foreach (XmlNode environmentNode in environments)
//             {
//                 SystemUniverse = UniverseFactory.GetUniverseClass(environmentNode);
//             }

//             var snakes = modelInputXMLNode.SelectNodes("PYTHON");
//             foreach (XmlNode pythonNode in snakes)
//             {
//                 throw new NotImplementedException();
//             }

//             // Load Assets
//             var assets = modelInputXMLNode.SelectNodes("ASSET");
//             foreach(XmlNode assetNode in assets)
//             {
//                 Asset asset = new Asset(assetNode);
//                 asset.AssetDynamicState.Eoms.SetEnvironment(SystemUniverse);
//                 AssetList.Add(asset);

//                 // Load Subsystems
//                 var subsystems = assetNode.SelectNodes("SUBSYSTEM");

//                 foreach (XmlNode subsystemNode in subsystems)
//                 {
//                     Subsystem subsys = SubsystemFactory.GetSubsystem(subsystemNode, asset);
//                     SubList.Add(subsys);

//                     // Load States (Formerly ICs)
//                     var States = subsystemNode.SelectNodes("STATE");

//                     foreach (XmlNode StateNode in States)
//                     {
//                         // Parse state node for key name and state type, add the key to the subsys's list of keys, return the key name
//                         string keyName = SubsystemFactory.SetStateKeys(StateNode, subsys);
//                         // Use key name and state type to set initial conditions 
//                         InitialSysState.SetInitialSystemState(StateNode, keyName);
//                     }

//                     if (subsys.Type == "scripted")
//                     {
//                         // Load Subsystem Parameters
//                         var parameters = subsystemNode.SelectNodes("PARAMETER");

//                         foreach (XmlNode parameterNode in parameters)
//                         {
//                             SubsystemFactory.SetParamenters(parameterNode, subsys);
//                         }
//                     }
//                 }

//                 // Load Constraints
//                 var constraints = assetNode.SelectNodes("CONSTRAINT");

//                 foreach (XmlNode constraintNode in constraints)
//                 {
//                     ConstraintsList.Add(ConstraintFactory.GetConstraint(constraintNode, SubList, asset));
//                 }
//             }
//             Console.WriteLine("Environment, Assets, and Constraints Loaded");
//             log.Info("Environment, Assets, and Constraints Loaded");

//             // Load Dependencies
//             var dependencies = modelInputXMLNode.SelectNodes("DEPENDENCY");

//             foreach (XmlNode dependencyNode in dependencies)
//             {
//                 //var SubFact = new SubsystemFactory();
//                 SubsystemFactory.SetDependencies(dependencyNode, SubList);
//             }
//             Console.WriteLine("Dependencies Loaded");
//             log.Info("Dependencies Loaded");
//         }

//         public void LoadEvaluator()
//         {
//             var modelInputXMLNode = XmlParser.GetModelNode(ModelInputFilePath);
//             var evalNodes = modelInputXMLNode.SelectNodes("EVALUATOR");
//             if (evalNodes.Count > 1)
//             {
//                 throw new NotImplementedException("Too many evaluators in input!");
//                 Console.WriteLine("Too many evaluators in input");
//                 log.Info("Too many evaluators in input");
//             }
//             else
//             {
//                 SchedEvaluator = EvaluatorFactory.GetEvaluator(evalNodes[0],SubList);
//                 Console.WriteLine("Evaluator Loaded");
//                 log.Info("Evaluator Loaded");
//             }
//         }
//         public void CreateSchedules()
//         {
//             SimSystem = new SystemClass(AssetList, SubList, ConstraintsList, SystemUniverse);

//             if (SimSystem.CheckForCircularDependencies())
//                 throw new NotFiniteNumberException("System has circular dependencies! Please correct then try again.");

//             Scheduler _scheduler = new Scheduler(SchedEvaluator);
//             Schedules = _scheduler.GenerateSchedules(SimSystem, SystemTasks, InitialSysState);
//         }
//         public double EvaluateSchedules()
//         {
//             // Evaluate the schedules and set their values
//             foreach (SystemSchedule systemSchedule in Schedules)
//             {
//                 systemSchedule.ScheduleValue = SchedEvaluator.Evaluate(systemSchedule);
//                 bool canExtendUntilEnd = true;
//                 // Extend the subsystem states to the end of the simulation 
//                 foreach (var subsystem in SimSystem.Subsystems)
//                 {
//                     if (systemSchedule.AllStates.Events.Count > 0)
//                             if (!subsystem.CanExtend(systemSchedule.AllStates.Events.Peek(), (Domain)SimSystem.Environment, SimParameters.SimEndSeconds))
//                             log.Error("Cannot Extend " + subsystem.Name + " to end of simulation");
//                 }
//             }

//             // Sort the sysScheds by their values
//             Schedules.Sort((x, y) => x.ScheduleValue.CompareTo(y.ScheduleValue));
//             Schedules.Reverse();
//             double maxSched = Schedules[0].ScheduleValue;
//             return maxSched;
//         }
//     }
// }

