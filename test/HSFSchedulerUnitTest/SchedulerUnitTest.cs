using Microsoft.VisualStudio.TestPlatform.TestHost;
using Horizon;
using Utilities;
using NUnit.Framework.Internal;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using System.Runtime.InteropServices.Marshalling;
using log4net;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using log4net.Appender;
using System.Runtime.CompilerServices;
using System.Text;
using System.IO;
using System.Collections.Generic;

namespace HSFSchedulerUnitTest
{
    public abstract class SchedulerUnitTest // This is the base class that all other SchedulerUnitTests will derive from. 
    // Place common "HSFSchedulerUnitTest" functionality here to be used in other classes and/or overriden. 
    {
        // Attributes that can be inherited to other Test Classes (fixtures) 
        protected string? SimInputFile { get; set; }
        protected string? TaskInputFile { get; set; }
        protected string? ModelInputFile { get; set; }
        protected Horizon.Program? program { get; set; }
        protected int? _emptySchedIdx { get; set; }

        // //String Writer & Thread attributes
        // //protected static StringWriter? stringWriter {get; set; }
        // //private static string _previousStringWriterOutput = string.Empty;
        // //private static Thread? _outputThread;
        // private static bool _keepRunning = true; 
        // private static MemoryStream _memoryStream = new MemoryStream();
        // private static StreamWriter _streamWriter;
        // private static long _previousPosition; 


        // [Test]
        // public virtual void EmptyScheduleExists() // This test should be ran on every schedule test
        // {
        //     Console.WriteLine("This is the EmptyScheduleTest...");
        //     for (int i = 0; i < program.Schedules.Count(); i++)
        //     {
        //         var schedule = program.Schedules[i];
        //         if (!(schedule.AllStates.Events.Count() > 0))
        //         {
        //             _emptySchedIdx = i; //Save the idx for the future...
        //             Assert.IsTrue(schedule.AllStates.Events.Count() == 0,$"The empty schedule exists (one without events). It is the {i} schedule in Program.Schedules list.");
        //         }
        //     }

        // }

        public virtual Horizon.Program HorizonLoadHelper(string SimInputFile, string TaskInputFile, string ModelInputFile)
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

        // [OneTimeSetUp]
        // public static void BaseClassSetUp()
        // {
        //     // Initialize MemoryStream and StreamWriter, set console output to it
        //     _streamWriter = new StreamWriter(_memoryStream) { AutoFlush = true };
        //     Console.SetOut(_streamWriter);

        //     // Start a background thread to monitor the MemoryStream for new output
        //     _outputThread = new Thread(() =>
        //     {
        //         using (var reader = new StreamReader(_memoryStream, Encoding.UTF8))
        //         {
        //             while (_keepRunning)
        //             {
        //                 lock (_memoryStream)
        //                 {
        //                     if (_memoryStream.Length > _previousPosition)
        //                     {
        //                         // Move the stream position to where we last read
        //                         _memoryStream.Position = _previousPosition;
                                
        //                         // Read new content from the stream
        //                         var newContent = reader.ReadToEnd();

        //                         // Output the captured content
        //                         TestContext.Progress.WriteLine(newContent);

        //                         // Update the previous position to the current end
        //                         _previousPosition = _memoryStream.Length;
        //                     }
        //                 }
        //                 Thread.Sleep(100); // Check every 100ms
        //             }
        //         }
        //     });

        //     _outputThread.Start();
        // }


        // [OneTimeTearDown]
        // public void BaseClassTearDown()
        // {
            // // Stop the monitoring thread
            // _keepRunning = false;
            // _outputThread.Join();  // Ensure the thread has stopped

            // //if (_memoryStream != null)
            // lock (_memoryStream)
            // {
            //     // Output any remaining content before disposing
            //     _memoryStream.Position = _previousPosition;
            //     using (var reader = new StreamReader(_memoryStream, Encoding.UTF8))
            //     {
            //         var remainingContent = reader.ReadToEnd();
            //         if (!string.IsNullOrEmpty(remainingContent))
            //         {
            //             TestContext.Progress.WriteLine(remainingContent);
            //         }
            //     }
            //     // Dispose of resources --> TearDownStreamWriter() (wants to be flagged with "[OneTimeTearDownAttriute]")
            //     _memoryStream.Dispose();
            // }
        // }

        // [OneTimeTearDownAttribute]
        // public static void TearDownStreams()
        // {
        //     // Stop the monitoring thread
        //     _keepRunning = false;
        //     _outputThread.Join();  // Ensure the thread has stopped

        //     //if (_memoryStream != null)
        //     // lock (_memoryStream)
        //     // {
        //     //     // Output any remaining content before disposing
        //     //     _memoryStream.Position = _previousPosition;
        //     //     using (var reader = new StreamReader(_memoryStream, Encoding.UTF8))
        //     //     {
        //     //         var remainingContent = reader.ReadToEnd();
        //     //         if (!string.IsNullOrEmpty(remainingContent))
        //     //         {
        //     //             TestContext.Progress.WriteLine(remainingContent);
        //     //         }
        //     //     }
        //     //     // Dispose of resources --> TearDownStreamWriter() (wants to be flagged with "[OneTimeTearDownAttriute]")
        //     //     _memoryStream.Dispose();
                
        //     // }
            
        //     _memoryStream.Dispose();
        //     _streamWriter.Dispose();
            
        // }

    

    //     public static void InitializeStreamWriter() // This is invoked before every [Test], even in derived classes. 
    //     {
    //         // Set up a String Writer to capture all program console outputs...
    //         using(var memoryStream = new MemoryStream()) {
    //             var streamWriter = new StreamWriter(memoryStream);
    //             Console.SetOut(streamWriter); 
    //             Console.WriteLine("Initializing StreamWriter...");
            
    //         }
    //         //Start a background thread to continuously monitor output of the streamwriter (to capture & output in near real-time)...
    //         // Lambda function to monitor the StringWriter for new output
    //         _outputThread = new Thread(() =>
    //         {
    //             while (_keepRunningMonitorThread)
    //             {
    //                 string currentOutput = stringWriter.ToString();
    //                 if (currentOutput != _previousStringWriterOutput)
    //                 {
    //                     string newContent = currentOutput.Substring(_previousStringWriterOutput.Length);
    //                     TestContext.WriteLine(newContent);
    //                     _previousStringWriterOutput = currentOutput;
    //                 }
    //                 Thread.Sleep(100); // Check every 100ms
    //             }
    //         });

    //         // Start up the thread 
    //         _outputThread.Start();

    //         //End Generic Base [Setup] 
    //     }
    //     public static void WriteStreamWriter()
    //     {
    //         string output = stringWriter.ToString();
    //         TestContext.WriteLine(output);
    //     }

    //     [TearDown]
    //     public virtual void BaseTearDown()
    //     {
    //         // Stop the monitoring thread
    //         _keepRunningMonitorThread = false;
    //         _outputThread.Join();  // Ensure the thread has stopped

    //         if (stringWriter != null)
    //         {
    //             // Output any remaining content
    //             lock (stringWriter)  // Ensure thread safety
    //             {
    //                 string currentOutput = stringWriter.ToString();
    //                 if (currentOutput != _previousStringWriterOutput)
    //                 {
    //                     string newContent = currentOutput.Substring(_previousStringWriterOutput.Length);
    //                     TestContext.WriteLine(newContent);
    //                 }

    //                 TearDownStringWriter();
    //             }
    //         }
    //     }

    //     [TearDownAttribute]
    //     public void TearDownStringWriter()
    //     {
    //         stringWriter.Dispose();
    //     }

    // }




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