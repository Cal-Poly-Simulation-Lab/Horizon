using Microsoft.VisualStudio.TestPlatform.TestHost;
using Horizon;
using Utilities;
using NUnit.Framework.Internal;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using System.Runtime.InteropServices.Marshalling;
using log4net;

namespace HSFSchedulerUnitTest
{
    public class SchedulerUnitTest // This is the base class that all other SchedulerUnitTests will derive from. 
    // Place common "HSFSchedulerUnitTest" functionality here to be used in other classes and/or overriden. 
    {
        // Attributes that can be inherited to other Test Classes (fixtures) 
        protected string? SimInputFile {get; set; }
        protected string? TaskInputFile {get; set; }
        protected string? ModelInputFile {get; set; }
        protected Horizon.Program? program {get; set; }

        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public void TestCreateOneSchedule()
        {
            // Declare all files used for this test
            string SimInputFile = "SchedulerTestSimulationInput.json";
            string TaskInputFile = "SchedulerTestTasks.json";
            string ModelInputFile = "SchedulerTestModel.json";

            //Set up the StringWrite so we can see what the Horizon Program is doing from a Console.WriteLine() POV:
            StringWriter stringWriter = new StringWriter();
            Console.SetOut(stringWriter);

            // Load all files and create a new Horizon Program

            Horizon.Program program = HoirzonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            // Now it is time to test the scheduler: 
            program.CreateSchedules();
            //double maxSched = program.EvaluateSchedules();

            //Assert.AreEqual(program.Schedules[0], program.Schedules[1]);

            Console.WriteLine("Break");
            //Assert.AreEqual(program.System)
            //
        }

        public Horizon.Program HoirzonLoadHelper(string SimInputFile, string TaskInputFile, string ModelInputFile)
        {
            #region Input File (argsList) Pathing Setup & Validation

            // Get the test directory in the Horizon repo
            string TestDirectory = Utilities.DevEnvironment.GetTestDirectory();
            // Set default directory to the HSFSchedulerUnitTest
            string SchedulerTestDirectory = Path.Combine(TestDirectory, "HSFSchedulerUnitTest");

            // Check if the input files exist (full path was passed) if not, assume relative path from SchedulerTestDirectory
            if (!File.Exists(SimInputFile)) { SimInputFile = Path.Combine(SchedulerTestDirectory, SimInputFile); }
            if (!File.Exists(TaskInputFile)) { TaskInputFile = Path.Combine(SchedulerTestDirectory, TaskInputFile); }
            if (!File.Exists(ModelInputFile)) { ModelInputFile = Path.Combine(SchedulerTestDirectory, ModelInputFile); }


            // Initiate a (spoofed) argsList as if input from the CLI to the console application:
            List<string> argsList = new List<String>();

            // Check if the input files above exist before adding them to the argsList: 
            if (File.Exists(SimInputFile)) { argsList.Add("-s"); argsList.Add(SimInputFile); }
            else { Console.WriteLine("HSFSchedulerUnitTest: No valid Test Simulation Input file was found. Using default."); }
            if (File.Exists(TaskInputFile)) { argsList.Add("-t"); argsList.Add(TaskInputFile); }
            else { Console.WriteLine("HSFSchedulerUnitTest: No valid Test Task Input file was found. Using default."); }
            if (File.Exists(ModelInputFile)) { argsList.Add("-m"); argsList.Add(ModelInputFile); }
            else { Console.WriteLine("HSFSchedulerUnitTest: No valid Test Model Input file was found. Using default."); }

            // Check and create the test output directory. 
            string outputDir = Path.Combine(SchedulerTestDirectory, @"output/");
            if (!Directory.Exists(outputDir)) { Directory.CreateDirectory(outputDir); }
            // Add the output directory to the argsList
            argsList.Add("-o"); argsList.Add(outputDir);

            #endregion

            // Create a new Horizon program
            Horizon.Program program = new Horizon.Program();

            // Run Horizon like normal to load all necessary elements: 
            program.InitInput(argsList);
            program.InitOutput(argsList);
            program.LoadScenario();
            program.LoadTasks();
            program.LoadSubsystems();
            program.LoadEvaluator();

            // Now everything is loaded in like the normal sart to the program... 
            // Return to Test for further Scheduler method entrance / testing ...
            return program;

        }

    }
}
//     public class HorizonTestScheduler : Scheduler
//     {
//         // Retrieve all private values and construct inside of (derived) test case for use in methods (below) ... 
//         private double _startTime    = GetPrivateAttribute<double>("_startTime"); 
//         private double _endTime      = GetPrivateAttribute<double>("_endTime"); 
//         private double _stepLength   = GetPrivateAttribute<double>("_stepLength"); 
//         private int _maxNumSchedules = GetPrivateAttribute<int>("_maxNumSchedules"); 
//         private int _numSchedCropTo  = GetPrivateAttribute<int>("_numSchedCropTo"); 
//         private static readonly ILog log = GetPrivateAttribute<ILog>("log");
    
//     // Pass the required argument to the base class constructor
//         public HorizonTestScheduler(Evaluator scheduleEvaluator) : base(scheduleEvaluator)  
//         {
//             // No additional initialization for the HorizonTestScheduler class is necessary (on top of base Scheduler class)
//         }

//         public override List<SystemSchedule> GenerateSchedules(SystemClass system, Stack<MissionElements.Task> tasks, SystemState initialStateList)
//         {

//             log.Info("SIMULATING... ");
//             // Create empty systemSchedule with initial state set
//             SystemSchedule emptySchedule = new SystemSchedule(initialStateList);
//             List<SystemSchedule> systemSchedules = new List<SystemSchedule>();
//             systemSchedules.Add(emptySchedule);

//             // if all asset position types are not dynamic types, can pregenerate accesses for the simulation
//             bool canPregenAccess = true;

//             foreach (var asset in system.Assets)
//             {
//                 if(asset.AssetDynamicState != null)
//                     canPregenAccess &= asset.AssetDynamicState.Type != HSFUniverse.DynamicStateType.DYNAMIC_ECI && asset.AssetDynamicState.Type != HSFUniverse.DynamicStateType.DYNAMIC_LLA && asset.AssetDynamicState.Type != HSFUniverse.DynamicStateType.NULL_STATE;
//                 else
//                     canPregenAccess = false;
//             }

//             // if accesses can be pregenerated, do it now
//             Stack<Access> preGeneratedAccesses = new Stack<Access>();
//             Stack<Stack<Access>> scheduleCombos = new Stack<Stack<Access>>();

//             if (canPregenAccess)
//             {
//                 log.Info("Pregenerating Accesses...");
//                 //DWORD startPregenTickCount = GetTickCount();

//                 preGeneratedAccesses = Access.pregenerateAccessesByAsset(system, tasks, _startTime, _endTime, _stepLength);
//                 //DWORD endPregenTickCount = GetTickCount();
//                 //pregenTimeMs = endPregenTickCount - startPregenTickCount;
//                 Access.writeAccessReport(preGeneratedAccesses); //- TODO:  Finish this code - EAMxz
//                 log.Info("Done pregenerating accesses. There are " + preGeneratedAccesses.Count + " accesses.");
//             }
//             // otherwise generate an exhaustive list of possibilities for assetTaskList,
//             else
//             {
//                 log.Info("Generating Exhaustive Task Combinations... ");
//                 Stack<Stack<Access>> exhaustive = new Stack<Stack<Access>>();
//                 //Stack<Access> allAccesses = new Stack<Access>(tasks.Count);


//                 // JB 8/16:
//                 // Need to assess 


//                 foreach (var asset in system.Assets)
//                 {
//                     Stack<Access> allAccesses = new Stack<Access>(tasks.Count);
//                     foreach (var task in tasks)
//                         allAccesses.Push(new Access(asset, task));
//                     //allAccesses.Push(new Access(asset, null));
//                     exhaustive.Push(allAccesses);

//                     //allAccesses.Clear();
//                 }

//                 // Note to Jason:
//                 // Create a list of tasks (more than just one) and figure out what this allScheduleCombos is ~~ solved

//                 // Question: Can two assets do the same task in the same event? Where/how is this enforced/modeled?
//                 IEnumerable<IEnumerable<Access>> allScheduleCombos = exhaustive.CartesianProduct();

//                 foreach (var accessStack in allScheduleCombos)
//                 {
//                     Stack<Access> someOfThem = new Stack<Access>(accessStack); // Is this link of code necessary? 
//                     scheduleCombos.Push(someOfThem);
//                 }

//                 log.Info("Done generating exhaustive task combinations");
//             }

//             /// TODO: Delete (or never create in the first place) schedules with inconsistent asset tasks (because of asset dependencies)

//             // Find the next timestep for the simulation
//             //DWORD startSchedTickCount = GetTickCount();
//             // int i = 1;
//             List<SystemSchedule> potentialSystemSchedules = new List<SystemSchedule>();
//             List<SystemSchedule> systemCanPerformList = new List<SystemSchedule>();
//             for (double currentTime = _startTime; currentTime < _endTime; currentTime += _stepLength)
//             {
//                 log.Info("Simulation Time " + currentTime);
//                 // if accesses are pregenerated, look up the access information and update assetTaskList
//                 if (canPregenAccess)
//                     scheduleCombos = GenerateExhaustiveSystemSchedules(preGeneratedAccesses, system, currentTime);

//                 // Check if it's necessary to crop the systemSchedule list to a more managable number
//                 if (systemSchedules.Count > _maxNumSchedules)
//                 {
//                     log.Info("Cropping " + systemSchedules.Count + " Schedules.");
//                     CropSchedules(systemSchedules, ScheduleEvaluator, emptySchedule);
//                     systemSchedules.Add(emptySchedule);
//                 }

//                 // Generate an exhaustive list of new tasks possible from the combinations of Assets and Tasks
//                 //TODO: Parallelize this.
//                 int k = 0;

//                 //Parallel.ForEach(systemSchedules, (oldSystemSchedule) =>
//                 foreach(var oldSystemSchedule in systemSchedules)
//                 {
//                     //potentialSystemSchedules.Add(new SystemSchedule( new StateHistory(oldSystemSchedule.AllStates)));
//                     foreach (var newAccessStack in scheduleCombos)
//                     {
//                         k++;
//                         if (oldSystemSchedule.CanAddTasks(newAccessStack, currentTime))
//                         {
//                             var CopySchedule = new StateHistory(oldSystemSchedule.AllStates);
//                             potentialSystemSchedules.Add(new SystemSchedule(CopySchedule, newAccessStack, currentTime));
//                             // oldSched = new SystemSchedule(CopySchedule);
//                         }

//                     }
//                 }

//                 int numSched = 0;
//                 foreach (var potentialSchedule in potentialSystemSchedules)
//                 {


//                     if (Checker.CheckSchedule(system, potentialSchedule)) {
//                         //potentialSchedule.GetEndState().GetLastValue()
//                         systemCanPerformList.Add(potentialSchedule);
//                         numSched++;
//                     }
//                 }
//                 foreach (SystemSchedule systemSchedule in systemCanPerformList)
//                     systemSchedule.ScheduleValue = ScheduleEvaluator.Evaluate(systemSchedule);

//                 systemCanPerformList.Sort((x, y) => x.ScheduleValue.CompareTo(y.ScheduleValue));
//                 systemCanPerformList.Reverse();
//                 // Merge old and new systemSchedules
//                 var oldSystemCanPerfrom = new List<SystemSchedule>(systemCanPerformList);
//                 systemSchedules.InsertRange(0, oldSystemCanPerfrom);//<--This was potentialSystemSchedule doubling stuff up
//                 potentialSystemSchedules.Clear();
//                 systemCanPerformList.Clear();

//                 // Print completion percentage in command window
//                 Console.WriteLine("Scheduler Status: {0:F}% done; {1} schedules generated.", 100 * currentTime / _endTime, systemSchedules.Count);
//             }
//             return systemSchedules;
//             }
//             return base.GenerateSchedules(system, tasks, initialStateList);
//         }

// }