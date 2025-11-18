// Copyright (c) 2016 California Polytechnic State University
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
        public string OutputPath { get; private set; }
        public static string StaticOutputPath { get; private set; }  // For static methods (AccessReport, etc.)
        public bool outputSet { get; set; } = false;
        public string basePath { get; set; } = Utilities.DevEnvironment.RepoDirectory;
        
        private ConsoleLogger _consoleLogger;
        private DateTime _runDateTime; 

        // Load the environment. First check if there is an ENVIRONMENT XMLNode in the input file
        public Domain SystemUniverse { get; set; }
        public Scheduler? scheduler { get; set; }

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

        /// <summary>
        /// Parse run version string (e.g., "00A", "99Z") and increment to next version
        /// Format: XXY where XX = 00-99, Y = A-Z
        /// </summary>
        private static string IncrementRunVersion(string currentVersion)
        {
            if (string.IsNullOrEmpty(currentVersion) || currentVersion.Length != 3)
                return "00A";
            
            int number = int.Parse(currentVersion.Substring(0, 2));
            char letter = currentVersion[2];
            
            number++;
            if (number > 99)
            {
                number = 0;
                letter++;
                if (letter > 'Z')
                    throw new InvalidOperationException("Run version overflow! Exceeded 99Z.");
            }
            
            return $"{number:D2}{letter}";
        }
        
        /// <summary>
        /// Get the next run version by scanning existing Run_* directories
        /// </summary>
        private static string GetNextRunVersion(string outputDir)
        {
            if (!Directory.Exists(outputDir))
                return "00A";
            
            var runDirs = Directory.GetDirectories(outputDir, "Run_*");
            if (runDirs.Length == 0)
                return "00A";
            
            string maxVersion = "00A";
            foreach (var dir in runDirs)
            {
                string dirName = Path.GetFileName(dir);
                // Extract version: "Run_00A_..." → "00A"
                if (dirName.StartsWith("Run_") && dirName.Length > 8)
                {
                    string version = dirName.Substring(4, 3);
                    if (string.Compare(version, maxVersion) > 0)
                        maxVersion = version;
                }
            }
            
            return IncrementRunVersion(maxVersion);
        }

        public static int Main(string[] args) //
        {
            var programStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            Program program = new Program();
            program._runDateTime = DateTime.Now;

            // Begin the Logger
            program.log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            program.log.Info("STARTING HSF RUN"); //Do not delete
            
            List<string> argsList = args.ToList();
            program.InitInput(argsList);
            program.LoadScenario();  // Load scenario FIRST to get the name for output directory
            program.InitOutput(argsList);  // Creates output dir AND starts console logging
            program.LoadTasks();
            program.LoadSubsystems();
            program.LoadEvaluator();
            program.CreateSchedules();
            
            // Generate hash set right after GenerateSchedules() returns (before EvaluateSchedules() sorts them)
            // Set to true to enable hash generation
            bool generateHashSet = true;
            if (generateHashSet)
            {
                GenerateAndSaveScheduleHashSet(program.Schedules, program.OutputPath);
                SaveScheduleHashBlockchainSummary(program.Schedules, program.OutputPath);
            }
            
            double maxSched = program.EvaluateSchedules();

            int i = 0;
            
            // Write schedule summary text file
            string summaryPath = Path.Combine(program.OutputPath, "schedules_summary.txt");
            Console.WriteLine($"Publishing simulation results to {program.OutputPath}");
            
            // MERGE RESOLUTION: Kept enhanced version (jebeals)
            // - Uses Path.Combine for cross-platform compatibility (vs Eric's hardcoded "\\" backslash)
            // - Uses "using" statement for proper StreamWriter disposal
            // - Writes to "schedules_summary.txt" in versioned run directory
            // Eric's version: OutputPath + "\\ScheduleResults.txt" (simpler, but Windows-only path)
            using (StreamWriter sw = File.CreateText(summaryPath))
            {
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
            } // StreamWriter auto-closes here
            
            program.log.Info("Max Schedule Value: " + maxSched);

            // MERGE RESOLUTION: Kept enhanced version (jebeals)
            // New approach:
            //   - WriteScheduleData(): Outputs top N schedules in clean CSV format
            //     * TopSchedule_valueXXX_{asset}_Data.csv (one per asset)
            //     * additional_schedule_data/{rank}_Schedule_valueXXX_{schedID}.csv
            //   - Heritage format kept in data/heritage/ for backward compatibility
            // Eric's version: Single WriteSchedule() call to OutputPath (simpler, less organized)
            // Write detailed state data using new clean CSV format
            SystemSchedule.WriteScheduleData(program.Schedules, program.OutputPath, SimParameters.NumSchedulesForStateOutput);
            
            // Also keep old format for backward compatibility (best schedule only, in data/heritage/)
            string heritageDir = Path.Combine(program.OutputPath, "data", "heritage");
            SystemSchedule.WriteSchedule(program.Schedules[0], heritageDir);

            //  Move this to a method that always writes out data about the dynamic state of assets, the target dynamic state data, other data?
            //var csv = new StringBuilder();
            //csv.Clear();
            //foreach (var asset in program.simSystem.Assets)
            //{
            //    File.WriteAllText(@"..\..\..\" + asset.Name + "_dynamicStateData.csv", asset.AssetDynamicState.ToString());
            //}

            // MERGE RESOLUTION: Kept enhanced version (jebeals)
            // New features:
            //   - Program timing with Stopwatch
            //   - Console output capture to run_log.txt (via StopConsoleLogging)
            //   - Formatted output confirmation message
            // Eric's version: Just "return 0;" (simpler, no timing or logging)
            //Console.ReadKey()
            programStopwatch.Stop();
            Console.WriteLine($"Simulation results published to {program.OutputPath}"); // Not an actual verification? 
            Console.WriteLine($"TOTAL PROGRAM TIME: {programStopwatch.Elapsed.TotalSeconds:F3} seconds\n");
            
            // Stop console logging before exiting
            program.StopConsoleLogging();
            
            return 0;
        }
        
        private void StopConsoleLogging()
        {
            _consoleLogger?.StopLogging();
        }

        public void InitInput(List<string> argsList)
        {
            // This would be in a config file - not used right now (4/26/24) -EM
            string basePath = Utilities.DevEnvironment.RepoDirectory;
            // DirectoryInfo testdir = DevEnvironment.testDirectory; 
            // string basePath = DevEnvironment.RepoDirectory; //Establsih the repo directory as the basePath
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

            // Default startup project
            if (argsList.Count == 0)
            {
                argsList.Add("-scen");
                // Set this to the default scenario you would like to run
                string scenarioName = "Aeolus_CS";
                argsList.Add(scenarioName);
                // This is the path or "subpath" to the Horizon/samples/ directory where the simulation input files are stored.
                subPath = Path.Combine(DevEnvironment.RepoDirectory, "samples");
            }

            bool simulationSet = false, targetSet = false, modelSet = false; 

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
                            case "Aeolus_scriptedpy":
                                // Set Defaults
                                //subpath = @"..\..\..\..\samples\Aeolus\";
                                subPath = Path.Combine(DevEnvironment.RepoDirectory, "samples");
                                subPath = Path.Combine(subPath, "Aeolus");
                                SimulationFilePath = Path.Combine(subPath, "AeolusSimulationInput.json");
                                TaskDeckFilePath = Path.Combine(subPath, "AeolusTasks.json");
                                // Asset 1 Scripted, Asset 2 Scripted
                                ModelFilePath = Path.Combine(subPath, "DSAC_Static_Scripted.json");
                                OutputPath = Path.Combine(subPath, argsList[i] + "_Output");
                                simulationSet = true;
                                targetSet = true;
                                modelSet = true;
                                outputSet = true;
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
                                //ModelInputFilePath = subpath + @"DSAC_Static_Mod_PartialScripted.xml"; a
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
            else
            {
                string msg = "No simulation file specified.";
                log.Fatal(msg);
                Console.WriteLine(msg);
                throw new ArgumentException(msg);
            }

            if (targetSet)
            {
                Console.WriteLine("Using target deck file: " + TaskDeckFilePath);
                log.Info("Using simulation file: " + TaskDeckFilePath);
            }
            else
            {
                string msg = "No target deck file specified.";
                log.Fatal(msg);
                Console.WriteLine(msg);
                throw new ArgumentException(msg);
            }

            if (modelSet)
            {
                Console.WriteLine("Using model file: " + ModelFilePath);
                log.Info("Using model file: " + ModelFilePath);
            }
            else
            {
                string msg = "No model file specified.";
                log.Fatal(msg);
                Console.WriteLine(msg);
                throw new ArgumentException(msg);
            }

            // This is redundat as we have initOutput
            // if (outputSet)
            // {
            //     Console.WriteLine("Using output path: " + OutputPath);
            //     log.Info("Using output path: " + OutputPath);
            // }
            // else
            // {
            //     if (OutputPath == null) { Console.WriteLine($"Default OutputPath used: {OutputPath}"); }
            //     else
            //     {
            //         string msg = "No output path specified.";
            //         log.Fatal(msg);
            //         Console.WriteLine(msg);
            //         throw new ArgumentException(msg);
            //     }
            // }

        }
        public void InitOutput(List<string> argsList)
        {
            // MERGE RESOLUTION: Kept enhanced version (jebeals)
            // New output system features:
            //   - Versioned run directories (last_run → Run_00A, Run_00B, etc.)
            //   - All outputs organized in single run directory
            //   - Default: <repo>/output/last_run_{timestamp}_{scenarioName}/
            //   - Supports custom output via -o flag
            //   - Sets static paths for AccessReport and other static methods
            // Eric's version: timestamp-based folders in output/HorizonLog (simpler, no versioning)
            
            // NOTE: Output path logic handled here. InitInput() may set outputSet flag but actual path creation happens here.

            // Detect if running from test runner and route to test output handler
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            if (assembly != null && (assembly.FullName.Contains("testhost") || assembly.FullName.Contains("NUnit")))
            {
                InitTestOutput(argsList);
                return;
            }

            string baseOutputDir = "";

            // Determine base output directory (parent of all run directories)
            if (argsList.Contains("-o"))
            {
                int indx = argsList.IndexOf("-o");
                baseOutputDir = argsList[indx + 1];
                outputSet = true;
            }
            else if (outputSet)
            {
                baseOutputDir = OutputPath;  // Set from scenario in InitInput
            }
            else
            {
                // MERGE RESOLUTION: Default output directory
                // Eric's version: output/HorizonLog (hardcoded)
                // Our version: output/ (cleaner, versioned subdirs handle organization)
                baseOutputDir = Path.Combine(DevEnvironment.RepoDirectory, "output");
            }

            // Create base output directory if it doesn't exist
            Directory.CreateDirectory(baseOutputDir);
            
            // Generate timestamp and scenario name for run directory
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string scenarioName = SimParameters.ScenarioName ?? "Unknown";
            
            // Handle last_run → Run_XXY versioning
            var lastRunDirs = Directory.GetDirectories(baseOutputDir, "last_run_*");
            
            if (lastRunDirs.Length > 0)
            {
                // Rename existing last_run to Run_XXY
                string lastRunDir = lastRunDirs[0];  // Should only be one
                string nextVersion = GetNextRunVersion(baseOutputDir);
                string versionedName = Path.GetFileName(lastRunDir).Replace("last_run_", $"Run_{nextVersion}_");
                string versionedPath = Path.Combine(baseOutputDir, versionedName);
                
                Directory.Move(lastRunDir, versionedPath);
                Console.WriteLine($"Archived previous run: {Path.GetFileName(versionedPath)}");
            }
            
            // Create new last_run directory with actual scenario name
            string runDirName = $"last_run_{timestamp}_{scenarioName}";
            string runDirPath = Path.Combine(baseOutputDir, runDirName);
            Directory.CreateDirectory(runDirPath);
            
            this.OutputPath = runDirPath;
            StaticOutputPath = runDirPath;  // Set static for Access.cs and other static methods
            SimParameters.OutputDirectory = runDirPath;  // Set for AccessReport and other static methods
            
            // Initialize hash history file tracking
            HSFScheduler.SystemScheduleInfo.InitializeHashHistoryFile(runDirPath);
            
            // MERGE RESOLUTION: Removed Eric's old directory filtering/numbering logic
            // No longer needed with our versioned run directory system (Run_00A, Run_00B, etc.)
            
            // Logging
            if (outputSet)
            {
                Console.WriteLine($"Using output directory: {runDirPath}");
                log.Info($"Using output directory: {runDirPath}");
            }
            else
            {
                Console.WriteLine($"Using default output directory: {runDirPath}");
                log.Info($"Using output directory: {runDirPath}");
            }
            // MERGE RESOLUTION: Kept enhanced version (jebeals)
            // Eric's removed code here was old timestamp-based naming logic
            // Already replaced by the versioning system above (Run_00A, Run_00B, etc.)
            
            // Start console logging now that output directory is created and scenario name is known
            _consoleLogger = new ConsoleLogger(OutputPath, SimParameters.ScenarioName ?? "Unknown", 
                                               SimulationFilePath, ModelFilePath, TaskDeckFilePath, _runDateTime);
            _consoleLogger.StartLogging();
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
                            if (asset.AssetDynamicState.Eoms != null)
                            {
                                asset.AssetDynamicState.Eoms.Environment = SystemUniverse;// SetEnvironment(SystemUniverse); 
                            }
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
                                            SubsystemFactory.SetInitialState(stateJson, subsys, InitialSysState);
                                            // Use key name and state type to set initial conditions 
                                            //InitialSysState.SetInitialSystemState(stateJson, stateVarKey);
                                        }
                                    }
                                    else
                                    {
                                        msg = $"Warning: Subsystem {subsys.Name} loaded with no states";
                                        Console.WriteLine(msg);
                                        log.Warn(msg);
                                    }

                                    // Load Subsystem Parameters
                                    if (subsys.Type == "scripted" || subsys.Type == "scriptedcs") // Need to include scriptedcs thing here too? 
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
                            {
                                foreach (JObject constraintJson in constraintListJson)
                                    ConstraintsList.Add(ConstraintFactory.GetConstraint(constraintJson, SubList, asset.Name));
                            }
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
                    // Load Evaluator
                    if(JsonLoader<JObject>.TryGetValue("Evaluator", modelJson, out JObject evaluatorJson))
                    {
                        SchedEvaluator = EvaluatorFactory.GetEvaluator(evaluatorJson, InitialSysState);
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

            
            this.scheduler = new Scheduler(SchedEvaluator); // Scheduler _scheduler = new Scheduler(SchedEvaluator);
            Schedules = this.scheduler.GenerateSchedules(SimSystem, SystemTasks, InitialSysState);
        }
        public double EvaluateSchedules()
        {
            // Evaluate the schedules and set their values
            // Note: Schedules were already evaluated during GenerateSchedules loop, so values won't change
            // Hash updates only occur when schedule data changes (new events), not on re-evaluation
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

            // Sort the sysScheds by their values, then by ScheduleID for deterministic ordering
            // This ensures schedules with the same value are ordered consistently across runs
            // OLD: Schedules.Sort((x, y) => x.ScheduleValue.CompareTo(y.ScheduleValue));
            // OLD: Schedules.Reverse();
            HSFScheduler.Scheduler.SortSchedulesDeterministic(Schedules, descending: true, context: "EvalSort");
            double maxSched = Schedules[0].ScheduleValue;
            return maxSched;
        }

        /// <summary>
        /// Test output: auto-detects test project root, simple "last_test_run" (no versioning)
        /// </summary>
        private void InitTestOutput(List<string> argsList)
        {
            // Auto-detect test project directory by finding .csproj from assembly location
            var testAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            string assemblyDir = Path.GetDirectoryName(testAssembly.Location) ?? DevEnvironment.RepoDirectory;
            string testProjectRoot = assemblyDir;
            bool foundCsproj = false;
            
            // Walk up until we find a .csproj file
            while (testProjectRoot != null && Directory.Exists(testProjectRoot))
            {
                if (Directory.GetFiles(testProjectRoot, "*.csproj").Length > 0)
                {
                    foundCsproj = true;
                    break;
                }
                var parent = Directory.GetParent(testProjectRoot);
                if (parent == null) break;
                testProjectRoot = parent.FullName;
            }
            
            // Fallback if .csproj not found
            if (!foundCsproj)
            {
                testProjectRoot = Path.Combine(DevEnvironment.RepoDirectory, "test");
                Console.WriteLine($"Test .csproj root directory not found, using fallback test output directory: {testProjectRoot}/output");
            }
            
            // Use -o if specified (test passes this), otherwise auto-detect
            string baseOutputDir = argsList.Contains("-o") 
                ? argsList[argsList.IndexOf("-o") + 1] 
                : Path.Combine(testProjectRoot, "output");
            
            Directory.CreateDirectory(baseOutputDir);
            
            // Check if full test output is requested via environment variable
            bool fullTestOutput = Environment.GetEnvironmentVariable("HORIZON_TEST_OUTPUT")?.ToLower() == "full";
            
            string testRunPath;
            if (fullTestOutput)
            {
                // Full output: unique directories with hash for parallel test execution
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
                string hash = Math.Abs($"{DateTime.Now.Ticks}_{System.Threading.Thread.CurrentThread.ManagedThreadId}".GetHashCode()).ToString("X6");
                testRunPath = Path.Combine(baseOutputDir, $"test_run_{hash}_{timestamp}");
            }
            else
            {
                // Default: simple "last_test_run" that overwrites (prevents directory bloat)
                testRunPath = Path.Combine(baseOutputDir, "last_test_run");
                if (Directory.Exists(testRunPath))
                {
                    try { Directory.Delete(testRunPath, true); } catch { /* Ignore deletion errors */ }
                }
            }
            
            Directory.CreateDirectory(testRunPath);
            
            this.OutputPath = testRunPath;
            StaticOutputPath = testRunPath;
            SimParameters.OutputDirectory = testRunPath;
            
            // Initialize hash history file tracking
            HSFScheduler.SystemScheduleInfo.InitializeHashHistoryFile(testRunPath);
            
            // Write test info file
            string infoPath = Path.Combine(testRunPath, "TEST_OUTPUT_INFO.txt");
            var info = new StringBuilder();
            info.AppendLine("Test Output Information");
            info.AppendLine("=======================");
            info.AppendLine($"Test Run Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            info.AppendLine();
            info.AppendLine("Default Action: Test results muted by Program.cs");
            info.AppendLine("  → InitOutput() detected test execution and routed to InitTestOutput()");
            info.AppendLine();
            info.AppendLine("To Enable Full Test Output:");
            info.AppendLine("  Set environment variable before running tests:");
            info.AppendLine("    export HORIZON_TEST_OUTPUT=full");
            info.AppendLine("    dotnet test <test-project>");
            info.AppendLine();
            info.AppendLine("WARNING: Full test output creates unique directories per test");
            info.AppendLine("         This can generate hundreds of directories if tests run in parallel.");
            info.AppendLine();
            info.AppendLine("TODO: Future implementation should capture NUnit test results");
            info.AppendLine("      (passed/failed counts) and write to test_summary.txt");
            info.AppendLine("      This requires test framework integration.");
            File.WriteAllText(infoPath, info.ToString());
            
            // Console logging for tests (only if full output enabled)
            if (fullTestOutput)
            {
                string scenarioName = SimParameters.ScenarioName ?? "Test";
                _consoleLogger = new ConsoleLogger(OutputPath, scenarioName, SimulationFilePath, ModelFilePath, TaskDeckFilePath, _runDateTime);
                _consoleLogger.StartLogging();
            }
        }

        /// <summary>
        /// Static method: Generates a hash set from all schedules and saves to file
        /// Uses events, event times, and asset->task pairs per schedule to create unique hash IDs
        /// Can be called from both Program.Main() and test runs
        /// </summary>
        public static void GenerateAndSaveScheduleHashSet(List<HSFScheduler.SystemSchedule> schedules, string outputPath)
        {
            var hashSet = new HashSet<string>();
            
            foreach (var schedule in schedules)
            {
                string hash = ComputeScheduleHash(schedule);
                hashSet.Add(hash);
            }
            
            // Save hash set to file (sorted for consistency)
            string hashFilePath = Path.Combine(outputPath, "schedule_hashes.txt");
            var sortedHashes = hashSet.OrderBy(h => h).ToList();
            File.WriteAllLines(hashFilePath, sortedHashes);
        }
        
        /// <summary>
        /// Static method: Computes a hash for a schedule based on schedule data only (NOT ScheduleID).
        /// Delegates to SystemSchedule.ComputeScheduleHash for consistent hash computation.
        /// </summary>
        public static string ComputeScheduleHash(HSFScheduler.SystemSchedule schedule)
        {
            return HSFScheduler.SystemSchedule.ComputeScheduleHash(schedule);
        }

        /// <summary>
        /// Static method: Saves blockchain schedule hash summary to file
        /// Outputs final ScheduleHash (top of last iteration's stack) for each schedule
        /// Can be called from both Program.Main() and test runs
        /// </summary>
        public static void SaveScheduleHashBlockchainSummary(List<HSFScheduler.SystemSchedule> schedules, string outputPath)
        {
            string summaryPath = Path.Combine(outputPath, "scheduleHashBlockchainSummary.txt");
            using (StreamWriter sw = File.CreateText(summaryPath))
            {
                sw.WriteLine($"ScheduleHash Blockchain Summary - {schedules.Count} schedules");
                sw.WriteLine(new string('=', 80));
                sw.WriteLine($"{"ScheduleID",-20} {"Value",-12} {"Events",-8} {"ScheduleHash",-20}");
                sw.WriteLine(new string('-', 80));
                
                foreach (var schedule in schedules)
                {
                    string scheduleHash = schedule.ScheduleInfo.ScheduleHash;
                    sw.WriteLine($"{schedule._scheduleID,-20} {schedule.ScheduleValue,-12:F2} {schedule.AllStates.Events.Count,-8} {scheduleHash,-20}");
                }
            }
        }

    }
}

