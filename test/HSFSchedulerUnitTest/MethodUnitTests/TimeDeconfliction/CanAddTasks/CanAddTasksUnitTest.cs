using Microsoft.VisualStudio.TestPlatform.TestHost;
using Horizon;
using Utilities;
using NUnit.Framework.Internal;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using System.Runtime.InteropServices.Marshalling;
using log4net;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Transactions;
using UserModel;
using log4net.Appender;
using IronPython.Runtime.Operations;

namespace HSFSchedulerUnitTest
{
    [TestFixture]
    public class CanAddTasksUnitTest : SchedulerUnitTest
    {
        protected override string SimInputFile { get; set; } = "InputFiles/SchedulerTestSimulationInput.json";
        protected override string TaskInputFile { get; set; } = Path.Combine(ProjectTestDir, "InputFiles", "ThreeTaskTestInput.json");
        protected override string ModelInputFile { get; set; } = Path.Combine(ProjectTestDir, "InputFiles", "TwoAssetTestModel.json");

        // private SystemClass? testSystem;
        // private Stack<MissionElements.Task>? testTasks;
        // private SystemSchedule? testSchedule;
        // private Asset? testAsset;
        // private MissionElements.Task? testTask;
        // private double currentTime = SimParameters.SimStartSeconds;
        // private double endTime = SimParameters.SimEndSeconds;
        // private double nextTime = SimParameters.SimStartSeconds + SimParameters.SimStepSeconds;

        [SetUp]
        public void SetupDefaults()
        {
            // Use the existing test files for the 1 asset, 3 tasks scenario
            // SimInputFile = "InputFiles/SchedulerTestSimulationInput.json";
            //TaskInputFile = Path.Combine(ProjectTestDir, "InputFiles", "ThreeTaskTestInput.json");
            // ModelInputFile = Path.Combine(ProjectTestDir, "InputFiles", "TwoAssetTestModel.json");

            // Load the program to get the system and tasks
            // BuildProgram();
        }

        [TearDown]
        public void ResetSchedulerAttributes()
        {
            // Reset static Scheduler attributes that mirror the Scheduler class
            SchedulerStep = -1;
            CurrentTime = SimParameters.SimStartSeconds;
            NextTime = SimParameters.SimStartSeconds + SimParameters.SimStepSeconds;
            _schedID = 0;
            _SchedulesGenerated = 0;
            _SchedulesCarriedOver = 0;
            _SchedulesCropped = 0;
            _emptySchedule = null;

            // Reset instance attributes
            _systemSchedules.Clear();
            _canPregenAccess = false;
            _scheduleCombos.Clear();
            _preGeneratedAccesses = null;
            _potentialSystemSchedules.Clear();
            _systemCanPerformList.Clear();
            _ScheduleEvaluator = null;

            // Reset program attributes
            program = new Horizon.Program();
            _testSimSystem = null;
            _testSystemTasks.Clear();
            _testInitialSysState = new SystemState();

            // Reset local test attributes
            // currentTime = SimParameters.SimStartSeconds;
            // endTime = SimParameters.SimEndSeconds;
            // nextTime = SimParameters.SimStartSeconds + SimParameters.SimStepSeconds;
        }

        private void BuildProgram()
        {
            // Load the program to get the system and tasks
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            // SimParameters are read-only, use the values from the loaded program
            double simEnd = SimParameters.SimEndSeconds;
            double simStep = SimParameters.SimStepSeconds;
            double simStart = SimParameters.SimStartSeconds;

            // GenerateSchedules() Method Flow Stop #1: Initialize Empty Shchedule
            Scheduler.InitializeEmptySchedule(_systemSchedules, _testInitialSysState); // Create the empty schedule and add it to the systemSchedules list
            SchedulerUnitTest._emptySchedule = Scheduler.emptySchedule;
            //Sccheduler.InitializeEmptySchedule(_systemSchedules, program.InitialSysState); // Create the empty schedule and add it to the systemSchedules list

            // Make sure the Test Attributes and Program Attributes are loaded together
            // GenerateSchedules() Method Flow Stop #2: Generate all default schedule combos
            //program.scheduler.scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(program.SimSystem, program.SystemTasks, program.scheduler.scheduleCombos, simStart, simEnd);
            _scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(_testSimSystem, _testSystemTasks, _scheduleCombos, simStart, simEnd);

        }

        private List<SystemSchedule> CanAddTasks_MainSchedulingLoop(
            List<SystemSchedule> systemSchedules,
            Stack<Stack<Access>> scheduleCombos,
            SystemClass system,
            Evaluator evaluator,
            SystemSchedule emptySchedule,
            double startTime, 
            double timeStep, 
            int iterations)
        {
            // Main Scheduling Loop Helper: Make sure to use all the mirrored attributes for the SchedulerUnitTest class. 
            this._systemSchedules = SchedulerUnitTest.MainSchedulingLoopHelper(systemSchedules, scheduleCombos, system,
                                                        evaluator, emptySchedule,
                                                        startTime, timeStep, iterations);

            // Start the beginning of the next iteration before CanAddTasks is called (within TimeDeconfliction): 
            Scheduler.SchedulerStep += 1;
            SchedulerUnitTest.CurrentTime += SchedulerUnitTest.NextTime;
            SchedulerUnitTest.NextTime = SchedulerUnitTest.CurrentTime + timeStep;
            this._systemSchedules = Scheduler.CropToMaxSchedules(this._systemSchedules, emptySchedule, evaluator);

            // Now we are moments before stepping into TimeDeconfliction() method (return out):
            return _systemSchedules; 

        }
        private string PrintAttemptedTaskAdditionInfo(SystemSchedule _oldSystemSchedule, Stack<Access> _newAccessStack) // Stack<Access> _scheduleComboToAdd)
        {
            string output = "";
            output += $" SchedID: {_oldSystemSchedule._scheduleID}: \n";
            string hasEvents = ""; int e = 0;
            foreach (var ev in _oldSystemSchedule.AllStates.Events)
            {
                hasEvents += $"Event [{e.ToString()}]: (";
                foreach (var task in ev.Tasks)
                {
                    string taskStr = $"{task.Key.Name}->{task.Value.Name}";
                    hasEvents += $"{taskStr},";
                }
                hasEvents = hasEvents[..^1];
                e++;
            }
            hasEvents += ")\n";
            output += hasEvents; // .TrimEnd()
            output += " Tried to add:\n";
            string accToAddSrt = "(";
            foreach (var acc in _newAccessStack)
            {
                accToAddSrt += $"{acc.Asset.Name}->{acc.Task.Name},";
            }
            accToAddSrt = accToAddSrt[..^1]; // Trim off final comma
            accToAddSrt += ")\n";
            output += accToAddSrt;
            return output;

        }
        
        #region Time Tests + Combinatorics
        [Test]
        public void TestCanAddTasks_EventTime_OneAsset_ThreeTask_100TimesMax()
        {
         // Set Inputs and call the build program
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "OneAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "ThreeTaskTestFile_100TimeMax_CanAddTasks.json");
            BuildProgram();

            double currentTime = 0.0;
            double timeStep = 12.0;
            int iterations = 1;
            // Main Scheduling Loop Helper: Make sure to use all the mirrored attributes for the SchedulerUnitTest class. 
            this._systemSchedules = CanAddTasks_MainSchedulingLoop(_systemSchedules, _scheduleCombos, _testSimSystem,
                                                        _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                                                        currentTime, timeStep, iterations);

            // Now we would enter Time Deconfliction Step:
            Assert.Multiple(() =>
            {
                // Ensure that the schedule Parameters are correct here:
                Assert.That(SchedParameters.MaxNumScheds, Is.EqualTo(1000), "The max number of schedules should be 100 per the input file.");
                Assert.That(_scheduleCombos.Count(), Is.EqualTo(3), "The schedule combos should have three (3) accesses stack given it is only one asset and three (3) tasks.");
                Assert.That(_systemSchedules.Count(), Is.EqualTo(4), $"The total system schedules after {iterations} iterations should be {Math.Pow(3, iterations)+1}.");

                // int i = 0; 
                foreach (var _oldSystemSchedule in _systemSchedules)
                {
                    foreach (var _newAccessStack in _scheduleCombos)
                    {
                        Assert.IsTrue(_newAccessStack.Count() == 1, "The access stack should have one access given it is only one asset."); //
                        Assert.IsTrue(_newAccessStack.First().Asset.Name.ToLower() == "testasset1", "The asset should be TestAsset1 (case in-sensitive).");
                        Assert.IsTrue(_newAccessStack.First().Task.MaxTimesToPerform == 100, "The task should have a MaxTimesToPerform of 100");

                        if (_oldSystemSchedule.Name.ToLower().Contains("empty"))
                        {
                            // This is the empty schedule:
                            Assert.That(_oldSystemSchedule.AllStates.Events.Count(), Is.EqualTo(0), "The empty schedule should have no events.");
                            Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), "The empty schedule should always allow task addition; given the MaxTimesToPerform >= 2. (This is because there are no matching Tasks in the StateHistory as there is no StateHistory for the EmptySchedule).,");
                        }
                        else
                        {
                            var asset = _newAccessStack.Peek().Asset;
                            double eventStarts = _oldSystemSchedule.AllStates.GetLastEvent().EventStarts[asset];
                            double eventEnds = _oldSystemSchedule.AllStates.GetLastEvent().EventEnds[asset];
                            Assert.IsTrue(eventEnds <= SchedulerUnitTest.CurrentTime, $"SchedID: {_oldSystemSchedule._scheduleID}\n Ensure that system is running fine to begin with (Event End <= currentTime)...\n Scheduler.CurentTime={SchedulerUnitTest.CurrentTime}s; eventStarts={eventStarts}s, eventEnds={eventEnds}s...\n" +
                            $"{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");

                            Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), $"Esnuring CanAddTasks passes... Scheduler.CurentTime={SchedulerUnitTest.CurrentTime}s; eventStarts={eventStarts}s, eventEnds={eventEnds}s...{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");

                            //Manipulate Old system schedule event End time:
                            double newEventEnd = SchedulerUnitTest.CurrentTime + 1.7;
                            _oldSystemSchedule.AllStates.GetLastEvent().EventEnds[asset] = newEventEnd;
                            Assert.IsFalse(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), $"CanAddTasks should fail its tmeporal check after event time was changed from {eventEnds} to {newEventEnd} (when Scheduler.CurrentTime = {SchedulerUnitTest.CurrentTime}.\n{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");
                            // double eventStarts = _oldSystemSchedule.AllStates.GetLastEvent().EventStart

                            // Change it back and verify its working again:
                            _oldSystemSchedule.AllStates.GetLastEvent().EventEnds[asset] = eventEnds;
                            Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), $"Esnuring CanAddTasks passes after it is changed back... Scheduler.CurentTime={SchedulerUnitTest.CurrentTime}s; eventStarts={eventStarts}s, eventEnds={eventEnds}s...{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");

                        }
                    }
                }
            });
        }
        
        [Test, Order(0)]
        public void Create_Combinatorics_TwoAssetThreeTask()
        {
            int timeStepsToComplete = 5;

            int timestep = 0;
            
            List<String> newListEmpty = new List<String>();
            newListEmpty.Add("");
            Dictionary<int,List<String>> TotalSchedulePermutaitons = new Dictionary<int,List<String>>();
            List<String> scheduleCombos = ["0","11", "12", "13", "21", "22", "23", "31", "32", "33"];
            TotalSchedulePermutaitons.Add(0,newListEmpty);
            int numFnCalls = 1;
            for (int i = 0; i < timeStepsToComplete; i++)
            {
                // Initialize the new list for the upcoming CanAddTasks function call (soon to be populated).
                TotalSchedulePermutaitons.Add(numFnCalls, new List<String>());

                // Populate the new list with the previous list's permutations, plus the new schedule combos.
                foreach (var str in TotalSchedulePermutaitons[numFnCalls-1]){
                    foreach (var combo in scheduleCombos){
                        string newStr = str + "-" + combo;
                        if (newStr[0] == '-'){ newStr = newStr[1..]; }
                        if (newStr[newStr.Length-1] == '-'){ newStr = newStr[..^1]; }
                        TotalSchedulePermutaitons[numFnCalls].Add(newStr);
                    }
                }
                numFnCalls++;
            }

            // Console.WriteLine("Total Schedule Permutations: " + TotalSchedulePermutaitons.Count());
            
        }
        #endregion

        #region SimpleTests: One Asset
        [Test, Order(1)]
        public void OneAssetOneTask_OneTimeMax_FirstIterationReturnsTrue()
        {
            /*
            Given there is one asset here this function should ALWAYS return true when calling CanAddTasks on the empty schedule
            (assuming event times are correct, which are checked by the SystemSchedule Contructor test).
            */

            // Set Inputs and call the build program
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "OneAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "OneTaskTestFile_OneTimeMax_CanAddTasks.json");
            BuildProgram();
            //Now that we have generated the exhaustive system schedules, we can be at the CURRENT TIME: SIM START, and execute the first CropToMaxSchedules call.:
            // Start the beginning of the next iteration before CanAddTasks is called (within TimeDeconfliction): 
            Scheduler.SchedulerStep += 1;
            SchedulerUnitTest.CurrentTime = 0;
            SchedulerUnitTest.NextTime = 0 + SimParameters.SimStepSeconds;
            this._systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, Scheduler.emptySchedule, program.SchedEvaluator); //bump


            var _sched = _systemSchedules[0]; // This is the empty schedule here
            var _newAccessStack = _scheduleCombos.First(); // This is the one and only 


            Assert.Multiple(() =>
            {
                //First Ensure that there is only one task and one asset and that they have been loaded properly.
                Assert.IsTrue(_newAccessStack.Count() == 1, "The access stack should have one access"); //falining
                Assert.IsTrue(_newAccessStack.First().Asset.Name.ToLower() == "testasset1", "The asset should be TestAsset1 (case in-sensitive).");
                Assert.IsTrue(_newAccessStack.First().Task.Name.ToLower() == "task1", "The task should be Task1 (case in-sensitive).");
                Assert.IsTrue(_newAccessStack.First().Task.MaxTimesToPerform == 1, "The task should have a MaxTimesToPerform of 1");

                // The first call should return true
                Assert.IsTrue(_sched.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), "The empty schedule should always allow task addition; given the MaxTimesToPerform == 1 .... INFO: AccessStack {k},");
                Assert.That(_sched.AllStates.timesCompletedTask(_newAccessStack.First().Task), Is.EqualTo(0), "The timesCompletedTask should return 0 since it has not been added to an Event yet, and would not yet exist in this potential schedule's StateHistory."); // failing
            });
        }

        
        [Test, Order(2)]
        public void OneAssetOneTask_OneTimeMax_SecondIterationReturnsFalse()
        {
            /* Similarly, this function should always pass given the presence of one asset (as long as Task.MaxTimesToPerform ==1)
            This is because after the first iteration, the task has been performed and cannot be performed again, no matter on which schedule.
            */

            // Set Inputs and call the build program
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "OneAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "OneTaskTestFile_OneTimeMax_CanAddTasks.json");
            BuildProgram();

            double currentTime = 0.0;
            double timeStep = 12.0;
            int iterations = 1;
            // Main Scheduling Loop Helper: Make sure to use all the mirrored attributes for the SchedulerUnitTest class. 
            this._systemSchedules = CanAddTasks_MainSchedulingLoop(_systemSchedules, _scheduleCombos, _testSimSystem,
                                                        _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                                                        currentTime, timeStep, iterations);

            // Now we would enter Time Deconfliction Step:
            Assert.Multiple(() =>
            {
                // Ensure that the schedule Parameters are correct here:
                Assert.That(SchedParameters.MaxNumScheds, Is.EqualTo(1000), "The max number of schedules should be 100 per the input file.");
                Assert.That(_scheduleCombos.Count(), Is.EqualTo(1), "The schedule combos should have only one access stack given it is only one asset and one task.");
                Assert.That(_systemSchedules.Count(), Is.EqualTo(2), $"The total system schedules after {iterations} should be {Math.Pow(1, iterations)+1}.");

                // int i = 0; 
                foreach (var _oldSystemSchedule in _systemSchedules)
                {
                    foreach (var _newAccessStack in _scheduleCombos)
                    {
                        Assert.IsTrue(_newAccessStack.Count() == 1, "The access stack should have one access given it is only one asset."); //falining
                        Assert.IsTrue(_newAccessStack.First().Asset.Name.ToLower() == "testasset1", "The asset should be TestAsset1 (case in-sensitive).");
                        Assert.IsTrue(_newAccessStack.First().Task.Name.ToLower() == "task1", "The task should be Task1 (case in-sensitive).");
                        Assert.IsTrue(_newAccessStack.First().Task.MaxTimesToPerform == 1, "The task should have a MaxTimesToPerform of 1");

                        if (_oldSystemSchedule.Name.ToLower().Contains("empty"))
                        {
                            // This is the empty schedule:
                            Assert.That(_oldSystemSchedule.AllStates.Events.Count(), Is.EqualTo(0), "The empty schedule should have no events.");
                            Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), "The empty schedule should always allow task addition; given the MaxTimesToPerform > 1. (This is because there are no matching Tasks in the StateHistory as there is no StateHistory for the EmptySchedule).,");
                        }
                        else
                        {
                            // This is all other schedules (with StateHistory):
                            Assert.That(_oldSystemSchedule.AllStates.Events.Count(), Is.EqualTo(1), "The schedule should have one event (asset1-->target1).");
                            Assert.That(_oldSystemSchedule.AllStates.timesCompletedTask(_newAccessStack.First().Task), Is.EqualTo(1), "The task should have been completed once.");
                            Assert.IsFalse(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), "The schedule should not allow task addition; given the MaxTimesToPerform = 1. (This is because there is a matching Task in the StateHistory as there is a StateHistory for the Non-EmptySchedule).,");
                        }
                    }
                }
            });
        } // End Test

        [Test, Order(3)]
        public void OneAssetThreeTask_ThreeMaxTimes_ThirdIterationAlwaysTrue()
        {
            /* This test should always pass given the presence of one Asset. If there were more (like 2) then edge cases where in one schedule combo
            two assets performed the task, the it would not encessarily be true that the Thirs Iteration would always return true. Because there is only
            one asset (one thing doing any one thing at one time on any given schedule branch) the third iteration should always return false.
            */

            // Set Inputs and call the build program
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "OneAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "ThreeTaskTestInput_ThreeTimesMax.json");
            BuildProgram();

            double currentTime = SimParameters.SimStartSeconds; // 0.0s
            double timeStep = SimParameters.SimStepSeconds; // 12.0s
            int iterations = 2;
            // Main Scheduling Loop Helper: Make sure to use all the mirrored attributes for the SchedulerUnitTest class. 
            this._systemSchedules = CanAddTasks_MainSchedulingLoop(_systemSchedules, _scheduleCombos, _testSimSystem,
                                                        _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                                                        currentTime, timeStep, iterations);

            // // Start the second iteration before CanAddTasks: 
            // _systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, SchedulerUnitTest._emptySchedule, _ScheduleEvaluator);
            // double thirdStepTime = currentTime + (timeStep*(iterations+1)); // This is the current Time

            // Now Time Deconfliction is Stepped into... 
            Assert.Multiple(() =>
            {
                // Ensure things are loaded in correctly, and sched parametr and schedule combos are as expected:
                Assert.That(SchedParameters.MaxNumScheds, Is.EqualTo(1000), "The max number of schedules should be 100 per the input file.");
                Assert.That(_testSimSystem.Assets.Count(), Is.EqualTo(1), "There should be one (1) asset loaded in this test simulation.");
                Assert.That(_testSystemTasks.Count(), Is.EqualTo(3), "There should be three (3) tasks loaded in this test simulation.");
                Assert.That(_scheduleCombos.Count(), Is.EqualTo(Math.Pow(_testSystemTasks.Count(), _testSimSystem.Assets.Count())), "The schedule combo is three given 1 asset and 3 tasks");
                foreach (var task in _testSystemTasks)
                { Assert.That(task.MaxTimesToPerform, Is.EqualTo(3), "It should be three (3) times max to perform for each Task."); }

                foreach (var _oldSystemSchedule in _systemSchedules)
                {
                    foreach (var _newAccessStack in _scheduleCombos)
                    {
                        Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime));
                    }
                }
            });
        }
        #endregion
        
        #region AdvancedTests: Two Assets, MultipleTasks
        [Test, Order(4)]
        public void EmptySchedule_CanAddTasks_ReturnsTrue_TwoAssetThreeTask_2TimesMax()
        {
            /* This test is only valid given the idea that an entire schedule fails if one asset cannot add the task...
            Specifically, in the case that the both assets have the same Task T, and T.MaxTimesToPerform < 2 (eg. = 1) then it would fail the whole schedule, 
            even though one Asset could have added it and the other do nothing. 

            This would have to be refactored if that functionality is ever added in future verisons.  
            */
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "ThreeTaskTestInput_TwoTimesMax.json");
            // Have to call the build manually
            BuildProgram();
            //Now that we have generated the exhaustive system schedules, we can be at the CURRENT TIME: SIM START, and execute the first CropToMaxSchedules call.:
            //Now that we have generated the exhaustive system schedules, we can be at the CURRENT TIME: SIM START, and execute the first CropToMaxSchedules call.:
            // Start the beginning of the next iteration before CanAddTasks is called (within TimeDeconfliction): 
            Scheduler.SchedulerStep += 1;
            SchedulerUnitTest.CurrentTime = 0;
            SchedulerUnitTest.NextTime = 0 + SimParameters.SimStepSeconds;
            _systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, Scheduler.emptySchedule, program.SchedEvaluator); //bump
            var _emptySchedule = _systemSchedules[0]; // Define the empty Schedule. It is the first one in Scheduler.systemSchedules after InitializeEmptyShecule() has been called. 

            Assert.Multiple(() =>
            {
                // Just a copy of the empty schedule test... But Oh well, we can test it here too.
                Assert.IsTrue(_systemSchedules.Count() == 1, "Assert 0a: The system schedules list should have one schedule after the empty schedule is initialized.");
                Assert.IsTrue(_systemSchedules[0].Name == "Empty Schedule", "Assert 0b: The empty schedule should be named 'Empty Schedule'.");
                Assert.IsTrue(_systemSchedules[0].AllStates.Events.Count() == 0, "Assert 0c: The empty schedule should have no events.");

                //
                // CurrentTime here is the Start Time of the Simulation, 0.0, as set in the initialziation of the attributes of this class. 
                int k = 0;
                foreach (var _newAccessStack in _scheduleCombos)
                {
                    // Ensure that EVERY Task has MaxTimesToPerform > 0. 
                    int a = 0; // Iterator to track asset
                    foreach (var access in _newAccessStack)
                    {
                        Assert.IsTrue(access.Task.MaxTimesToPerform >= 2,
                            $"AccessStack {k}, Access {a}: Task {access.Task.Name}: MaxTimesToPerform, {access.Task.MaxTimesToPerform} must be greater than or equal to 2 to always return true .... " +
                            $"INFO: {access.Asset.Name}_to_{access.Task.Target.Name}. ");
                        a++;
                    }
                    // Call CanAddTasks() forn the empty schedule across all schedule combos. 
                    Assert.IsTrue(_emptySchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), $"The empty schedule should always allow task addition, given the MaxTimesToPerform >= 2 for all tasks .... INFO: AccessStack {k}\n{PrintAttemptedTaskAdditionInfo(_emptySchedule, _newAccessStack)}");
                    k++;
                }
            });

        }

        [Test, Order(5)]
        public void TwoAssetThreeTask_OneTimeMax_FirstIterationReturnsCorrectCombinations()
        {
            /*
            This test is only valid given the idea that an entire schedule fails if one asset cannot add the task...
            Specifically, in the case that the both assets have the same Task T, and T.MaxTimesToPerform < 2 (eg. = 1) then it would fail the whole schedule, 
            even though one Asset could have added it and the other do nothing. 

            This would have to be refactored if that functionality is ever added in future verisons.  
            i.e. if it is ever allowed for one asset to add a task and the other to do nothing there would be a few more scheudles available;
            specifically the schedules that have combos with double up tasks... One could do something and the other do nothing. 
            That would have its own set of combinatorics and is not implemented here.
            */

            // Set Inputs and call the build program
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "TwoAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "ThreeTaskTestInput_OneTimeMax.json");
            BuildProgram();

            // Start the beginning of the next iteration before CanAddTasks is called (within TimeDeconfliction): 
            Scheduler.SchedulerStep += 1;
            SchedulerUnitTest.CurrentTime = 0;
            SchedulerUnitTest.NextTime = 0 + SimParameters.SimStepSeconds;
            this._systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, Scheduler.emptySchedule, program.SchedEvaluator); //bump


            // Now we would enter Time Deconfliction Step:
            Assert.Multiple(() =>
            {
                // Ensure things are loaded in correctly, and sched parametr and schedule combos are as expected:
                Assert.That(SchedParameters.MaxNumScheds, Is.EqualTo(1000), "The max number of schedules should be 100 per the input file.");
                Assert.That(_testSimSystem.Assets.Count(), Is.EqualTo(2), "There should be two (2) assets loaded in this test simulation.");
                Assert.That(_testSystemTasks.Count(), Is.EqualTo(3), "There should be three (3) tasks loaded in this test simulation.");
                Assert.That(_scheduleCombos.Count(), Is.EqualTo(Math.Pow(_testSystemTasks.Count(), _testSimSystem.Assets.Count())), "The schedule combo is nine (9) given 2 asset and 3 tasks");
                // int i = 0; 

                foreach (var _oldSystemSchedule in _systemSchedules)
                {
                    string _schedule_name = _oldSystemSchedule._scheduleID; // Name the schedule by its ID for debugging. The 0 ID is the empty schedule. 
                    foreach (var _newAccessStack in _scheduleCombos)
                    {
                        Assert.IsTrue(_newAccessStack.Count() == 2, "The access stack should have two (2) given it has two (2) assets."); //
                        foreach (var _newAccess in _newAccessStack) { Assert.That(_newAccess.Task.MaxTimesToPerform, Is.EqualTo(1), "All tasks should have a MaxTimesToPreform of one (1)."); }

                        Assert.That(_oldSystemSchedule.AllStates.Events.Count(), Is.EqualTo(0), $"SchedID_{_schedule_name}: The empty schedule should have no events.");
                        Assert.IsTrue(_oldSystemSchedule.Name.ToLower().Contains("empty"), "The only schedule present here at the start of the first iteration is the empty schedule.");


                        // Check if both assets are trying to do the same task
                        bool sameTask = _newAccessStack.First().Task == _newAccessStack.Last().Task;

                        if (sameTask)
                        {
                            // Both assets doing the same task with MaxTimesToPerform=1 should fail
                            Assert.IsFalse(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), $"SchedID_{_schedule_name}: Empty schedule should NOT allow both assets to add the same task when MaxTimesToPerform=1.\n{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");
                        }
                        else
                        {
                            // Different tasks should be allowed
                            Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), $"SchedID_{_schedule_name}: Empty schedule should allow different tasks to be added.\n{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");
                        }
                    }
                }
            }); // End Assertion Multiple
        } // End test
        
        [Test, Order(6)]
        public void TwoAssetThreeTask_OneTimeMax_SecondIterationReturnsFalse_ExceptForSelectEmptyCombos()
        {
            /*
            This test is only valid given the idea that an entire schedule fails if one asset cannot add the task...
            Specifically, in the case that the both assets have the same Task T, and T.MaxTimesToPerform < 2 (eg. = 1) then it would fail the whole schedule, 
            even though one Asset could have added it and the other do nothing. 

            This would have to be refactored if that functionality is ever added in future verisons.  
            Specifcally, if some tasks were not added in the previous iteration, one of them could be added in the current where one asset does it and the other nothing.
            */

            // Set Inputs and call the build program
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "TwoAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "ThreeTaskTestInput_OneTimeMax.json");
            BuildProgram();

            double startTime = 0.0;
            double timeStep = 12.0;
            int iterations = 1;
            // Main Scheduling Loop Helper: Make sure to use all the mirrored attributes for the SchedulerUnitTest class. 
            this._systemSchedules = CanAddTasks_MainSchedulingLoop(_systemSchedules, _scheduleCombos, _testSimSystem,
                                                        _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                                                        startTime, timeStep, iterations);


            // Now we would enter Time Deconfliction Step:
            Assert.Multiple(() =>
            {
                // Ensure things are loaded in correctly, and sched parametr and schedule combos are as expected:
                Assert.That(SchedParameters.MaxNumScheds, Is.EqualTo(1000), "The max number of schedules should be 1000 per the input file.");
                Assert.That(_testSimSystem.Assets.Count(), Is.EqualTo(2), "There should be two (2) assets loaded in this test simulation.");
                Assert.That(_testSystemTasks.Count(), Is.EqualTo(3), "There should be three (3) tasks loaded in this test simulation.");
                Assert.That(_scheduleCombos.Count(), Is.EqualTo(Math.Pow(_testSystemTasks.Count(), _testSimSystem.Assets.Count())), "The schedule combo is nine (9) given 2 asset and 3 tasks");
                // int i = 0; 
                
                foreach (var _oldSystemSchedule in _systemSchedules)
                {
                    string _schedule_name = _oldSystemSchedule._scheduleID; // Name the schedule by its ID for debugging. The 0 ID is the empty schedule. 
                    foreach (var _newAccessStack in _scheduleCombos)
                    {
                        Assert.IsTrue(_newAccessStack.Count() == 2, "The access stack should have two (2) given it has two (2) assets."); //
                        foreach (var _newAccess in _newAccessStack) { Assert.That(_newAccess.Task.MaxTimesToPerform, Is.EqualTo(1), "All tasks should have a MaxTimesToPreform of one (1)."); }


                        if (_oldSystemSchedule.Name.ToLower().Contains("empty"))
                        {
                            // This is the empty schedule:
                            Assert.That(_oldSystemSchedule.AllStates.Events.Count(), Is.EqualTo(0), $"SchedID_{_schedule_name}: The empty schedule should have no events.");
                                                    
                            // Check if both assets are trying to do the same task
                            bool sameTask = _newAccessStack.First().Task == _newAccessStack.Last().Task;
                        
                            if (sameTask)
                            {
                                // Both assets doing the same task with MaxTimesToPerform=1 should fail
                                Assert.IsFalse(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), $"SchedID_{_schedule_name}: Empty schedule should NOT allow both assets to add the same task when MaxTimesToPerform=1.\n{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule,_newAccessStack)}");
                            }
                            else
                            {
                                // Different tasks should be allowed
                                Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime), $"SchedID_{_schedule_name}: Empty schedule should allow different tasks to be added to the empty schedule.\n{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule,_newAccessStack)}");
                            }
                        }
                        else 
                        {
                            // This is all other schedules (with StateHistory):
                            Assert.That(_oldSystemSchedule.AllStates.Events.Count(), Is.EqualTo(1), $"SchedID_{_schedule_name}: All other schedules should have one event after the first step (if not the empty schedule).\n{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule,_newAccessStack)}");
                            Assert.IsFalse(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime),
                                $"All other (non-empty) schedule branches should not be able to add tasks given MaxTimesToPerform=1 (for all tasks).\n" +
                                $"{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");
                        }
                    }
                }
            });
        } // End Test

        [Test, Order(7)]
        public void TwoAssetThreeTask_ThreeMaxTimes_ThirdIterationAlwaysTrue()
        {
            /* this is most complicated test in the CanAddTasks unit tests.
            It tests the ability of the scheduler to add tasks to a schedule that has already been through two iterations.
            If the CanAddTasks method were allowing one asset to do something while the other not, the combinatorics would be completely differnet.
            I would recommend writing another new test in place of that if this is the case (because this is the third, complicated iteration). 
            The other two above this could be used as a jumping off point for that future case and be feactored. This one would just be dropped and restarted since there
            are chains of dependents events leading to the combinatorics.
            */
            
            // Set Inputs and call the build program
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "TwoAssetTestModel_CanAddTasks.json");
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "ThreeTaskTestInput_ThreeTimesMax.json");
            BuildProgram();

            double currentTime = SimParameters.SimStartSeconds; // 0.0s
            double timeStep = SimParameters.SimStepSeconds; // 12.0s
            int iterations = 2;
            // Main Scheduling Loop Helper: Make sure to use all the mirrored attributes for the SchedulerUnitTest class. 
            this._systemSchedules = CanAddTasks_MainSchedulingLoop(_systemSchedules, _scheduleCombos, _testSimSystem,
                                                        _ScheduleEvaluator, SchedulerUnitTest._emptySchedule,
                                                        currentTime, timeStep, iterations);

            // Start the third iteration before CanAddTasks: 
            // Scheduler.SchedulerStep += 1;
            //double thirdStepTime = currentTime + (timeStep*(iterations+1)); // This is the current Time

            // Now Time Deconfliction is Stepped into... 
            Assert.Multiple(() =>
            {
                // Ensure things are loaded in correctly, and sched parametr and schedule combos are as expected:
                Assert.That(SchedParameters.MaxNumScheds, Is.GreaterThanOrEqualTo(1000), "Max schedules should be at least 1000 to avoid cropping permutations.");
                Assert.That(_testSimSystem.Assets.Count(), Is.EqualTo(2), "There should be two (2) assets loaded in this test simulation.");
                Assert.That(_testSystemTasks.Count(), Is.EqualTo(3), "There should be three (3) tasks loaded in this test simulation.");
                Assert.That(_scheduleCombos.Count(), Is.EqualTo(Math.Pow(_testSystemTasks.Count(), _testSimSystem.Assets.Count())), "The schedule combo is nine (9) given 2 assets and 3 tasks");
                foreach (var task in _testSystemTasks)
                { Assert.That(task.MaxTimesToPerform, Is.EqualTo(3), "It should be three (3) times max to perform for each Task."); }

                string _schedule_name = "";
                foreach (var _oldSystemSchedule in _systemSchedules)
                {
                    _schedule_name = _oldSystemSchedule._scheduleID;
                    foreach (var _newAccessStack in _scheduleCombos)
                    {
                        Assert.IsTrue(_newAccessStack.Count() == 2, "The access stack should have two (2) given it has two (2) assets.");
                        foreach (var _newAccess in _newAccessStack)
                        { Assert.That(_newAccess.Task.MaxTimesToPerform, Is.EqualTo(3), "All tasks should have a MaxTimesToPerform of three (3)."); }

                        // Check if both assets are trying to do the same task
                        bool sameTask = _newAccessStack.First().Task == _newAccessStack.Last().Task;

                        // Collect all tasks from all events in this schedule
                        var allTasksInSchedule = new List<MissionElements.Task>();
                        foreach (var ev in _oldSystemSchedule.AllStates.Events)
                        {
                            allTasksInSchedule.AddRange(ev.Tasks.Values);
                        }

                        // Count how many times each task appears in the schedule
                        var taskCounts = allTasksInSchedule.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

                        if (_oldSystemSchedule.Name.ToLower().Contains("empty"))
                        {
                            // Empty schedule should have no events
                            Assert.That(_oldSystemSchedule.AllStates.Events.Count(), Is.EqualTo(0), $"SchedID_{_schedule_name}: Empty schedule should have no events.");

                            if (sameTask)
                            {
                                // Both assets doing same task: 0 history + 2 new = 2 <= 3, should pass
                                Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime),
                                    $"SchedID_{_schedule_name}: Empty schedule should allow same task (0+2=2<=3).\n{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");
                            }
                            else
                            {
                                Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime),
                                    $"SchedID_{_schedule_name}: Empty schedule should allow different tasks.\n{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");
                            }
                        }
                        else
                        {
                            // Non-empty schedules - check what's been completed (could be 1 or 2 events depending on cropping)
                            int eventCount = _oldSystemSchedule.AllStates.Events.Count();
                            Assert.That(eventCount, Is.GreaterThanOrEqualTo(1).And.LessThanOrEqualTo(2),
                                $"SchedID_{_schedule_name}: Should have 1 or 2 events after 2 iterations.\n{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");

                            // Check if this newAccessStack would exceed any task's limit
                            // Use the same logic as CanAddTasks: count unique tasks in newAccessStack
                            bool wouldExceedLimit = false;
                            string failingTask = "";
                            HashSet<MissionElements.Task> checkedTasks = new HashSet<MissionElements.Task>();

                            foreach (var access in _newAccessStack)
                            {
                                if (access.Task != null && !checkedTasks.Contains(access.Task))
                                {
                                    checkedTasks.Add(access.Task);

                                    // Count TOTAL occurrences of this task historically (across ALL events and ALL assets)
                                    int historicalCount = 0;
                                    foreach (var ev in _oldSystemSchedule.AllStates.Events)
                                    {
                                        foreach (var task in ev.Tasks.Values)
                                        {
                                            if (task == access.Task)
                                                historicalCount++;
                                        }
                                    }

                                    // Count how many times we're adding it in the new access stack
                                    int newCount = 0;
                                    foreach (var a in _newAccessStack)
                                    {
                                        if (a.Task == access.Task)
                                            newCount++;
                                    }

                                    if (historicalCount + newCount > access.Task.MaxTimesToPerform)
                                    {
                                        wouldExceedLimit = true;
                                        failingTask = access.Task.Name;
                                        break;
                                    }
                                }
                            }

                            if (wouldExceedLimit)
                            {
                                Assert.IsFalse(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime),
                                    $"SchedID_{_schedule_name}: CanAddTasks should be False - {failingTask} would exceed limit.\n{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");
                            }
                            else
                            {
                                Assert.IsTrue(_oldSystemSchedule.CanAddTasks(_newAccessStack, SchedulerUnitTest.CurrentTime),
                                    $"SchedID_{_schedule_name}: CanAddTasks should be True (all tasks within limit).\n{PrintAttemptedTaskAdditionInfo(_oldSystemSchedule, _newAccessStack)}");
                            }
                        }
                    }
                }
            });
        } // End Test 6
        #endregion


    } // End Class

}    // End Namespace
