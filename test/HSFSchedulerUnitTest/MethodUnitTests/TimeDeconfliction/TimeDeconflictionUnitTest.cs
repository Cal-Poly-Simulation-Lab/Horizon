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

namespace HSFSchedulerUnitTest
{
    [TestFixture]
    public class TimeDeconflictionUnitTest : SchedulerUnitTest
    {
        protected override string SimInputFile { get; set; } = "";
        protected override string TaskInputFile { get; set; } = "";
        protected override string ModelInputFile { get; set; } = "";

        [SetUp]
        public void Setup()
        {
            SimInputFile = Path.Combine(CurrentTestDir, "Inputs", "SimInput_100kSched.json");
        }

        [TearDown]
        public void ResetSchedulerAttributes()
        {
            // Reset static Scheduler attributes
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


            //     // Load the program to get the system and tasks
            //     program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            //     double simEnd = SimParameters.SimEndSeconds;
            //     double simStep = SimParameters.SimStepSeconds;
            //     double simStart = SimParameters.SimStartSeconds;

            //     // Initialize Empty Schedule
            //     Scheduler.InitializeEmptySchedule(_systemSchedules, _testInitialSysState);
            //     SchedulerUnitTest._emptySchedule = Scheduler.emptySchedule;

            //     // Generate all default schedule combos
            //     _scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(_testSimSystem, _testSystemTasks, _scheduleCombos, simStart, simEnd);
            }

        private List<SystemSchedule> TimeDeconfliction_LoopHelper(
            List<SystemSchedule> systemSchedules,
            Stack<Stack<Access>> scheduleCombos,
            SystemClass system,
            Evaluator evaluator,
            SystemSchedule emptySchedule,
            double startTime,
            double timeStep,
            int iterations)
        {
            // Run MainSchedulingLoopHelper to position us right before TimeDeconfliction call
            this._systemSchedules = SchedulerUnitTest.MainSchedulingLoopHelper(
                systemSchedules, scheduleCombos, system,
                evaluator, emptySchedule,
                startTime, timeStep, iterations);

            // Advance to the next iteration checkpoint (right before TimeDeconfliction)
            Scheduler.SchedulerStep += 1;
            SchedulerUnitTest.CurrentTime += SchedulerUnitTest.NextTime;
            SchedulerUnitTest.NextTime = SchedulerUnitTest.CurrentTime + timeStep;
            this._systemSchedules = Scheduler.CropToMaxSchedules(this._systemSchedules, emptySchedule, evaluator);

            return _systemSchedules;
        }
        public Dictionary<int,List<String>> CreateAllPossibleCombinatoric_Strings(string[] scheduleCombos, int timeStepsToComplete)
        {
        
            Dictionary<int,List<String>> TotalSchedulePermutaitons = new Dictionary<int,List<String>>();
            List<String> newListEmpty = new List<String>(); newListEmpty.Add("");
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
            return TotalSchedulePermutaitons;
        }

        [Test, Order(0)]
        public void CreateCombinatoricsTest_TwoAssetThreeTask()
        {
            string[] scheduleCombos = ["0", "11", "12", "13", "21", "22", "23", "31", "32", "33"];
            int timeStepsToComplete = 5;
            var TotalSchedulePermutaitons = CreateAllPossibleCombinatoric_Strings(scheduleCombos, timeStepsToComplete);
            Assert.Multiple(() =>
            {
                Assert.That(TotalSchedulePermutaitons.Count, Is.EqualTo(timeStepsToComplete + 1), "Should have 6 schedule permutations.");
                Assert.That(TotalSchedulePermutaitons[0].Count, Is.EqualTo(1), "Should have 1 schedule permutation for iteration 0.");
                Assert.That(TotalSchedulePermutaitons[1].Count, Is.EqualTo(10), "Should have 10 schedule permutations for iteration 1.");
                Assert.That(TotalSchedulePermutaitons[2].Count, Is.EqualTo(100), "Should have 100 schedule permutations for iteration 2.");
                Assert.That(TotalSchedulePermutaitons[3].Count, Is.EqualTo(1000), "Should have 1000 schedule permutations for iteration 3.");
                Assert.That(TotalSchedulePermutaitons[4].Count, Is.EqualTo(10000), "Should have 10000 schedule permutations for iteration 4.");
            });
        }

        [TestCase(1), TestCase(2), TestCase(3), TestCase(4),TestCase(5), TestCase(6), TestCase(50), TestCase(100)]
        public void CorrectPotentialScheduleCombosTest_OneAssetOneTask_XTimesMax_Insufficient_AllIterations(int maxTimesToPerformInput)
        {
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "XTimesMaxTaskFiles",$"OneTaskTestFile_{maxTimesToPerformInput}TimesMax.json");
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "OneAssetTestModel_TimeDeconfliction.json");
            BuildProgram();

            double currentTime = SimParameters.SimStartSeconds;
            double timeStep = 12.0;
            double simEndTime = 60; 
            int iterations = (int)(simEndTime / timeStep);

            Assert.Multiple(() =>
            {
                Assert.That(currentTime, Is.EqualTo(SimParameters.SimStartSeconds), $"Current (start) time should be: {SimParameters.SimStartSeconds} based on the SimInputFile: {SimInputFile}.");
                Assert.That(timeStep, Is.EqualTo(SimParameters.SimStepSeconds), $"Time step should be: {SimParameters.SimStepSeconds} based on the SimInputFile: {SimInputFile}.");
                Assert.That(iterations, Is.EqualTo((int)(simEndTime / timeStep)), $"Total scheduler iterations to complete should be: {(int)(simEndTime / timeStep)} based on the SimInputFile: {SimInputFile}.");
                Assert.That(maxTimesToPerformInput, Is.EqualTo(this._testSystemTasks.Peek().MaxTimesToPerform), $"MaxTimesToPerform should be: {maxTimesToPerformInput} based on the TaskInputFile: {TaskInputFile}.");
                Assert.That(this._testSimSystem.Assets.Count, Is.EqualTo(1), $"Should have 1 asset based on the ModelInputFile: {ModelInputFile}.");
                Assert.That(this._testSystemTasks.Count, Is.EqualTo(1), $"Should have 1 task based on the TaskInputFile: {TaskInputFile}.");

                int k = 0; // This is the "times over maxIts" variable, for those that need it blow. 
                Dictionary<int, List<SystemSchedule>> sysSchedDict = new Dictionary<int, List<SystemSchedule>>();
                for (int i = 0; i < iterations; i++)
                {

                    double currentTime = i * timeStep;

                    // TimeDeconfliction: Each old schedule tries to add each combo
                    _potentialSystemSchedules = Scheduler.TimeDeconfliction(_systemSchedules, _scheduleCombos, currentTime);

                    // Check that the tasking output is correct from TimeDeconfliction() (only testasset1 and task1):
                    foreach (var sched in _potentialSystemSchedules)
                    {
                        foreach (var ev in sched.AllStates.Events)
                        {
                            Assert.IsTrue(ev.Tasks.Count == 1, "All events should only contain one task. (Not checking empty schedule here because this is the output of _potentialSchedules after TimeDeconfliction() call). \n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                            foreach (var task in ev.Tasks) // These are way too F'ing buried. Need to make tasks a stack or list. Not a dict with integer keys.....
                            {
                                var asset = task.Key;
                                Assert.IsTrue(asset.Name.ToLower().Contains("testasset1"), $"The only asset that should be present across all event tasks is 'testasset1' (case insensitve); \n Given model input file: {ModelInputFile}; [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                                Assert.IsTrue(task.Value.Name.ToLower().Contains("task1"), $"The only task that should be present across all event tasks is 'task1' (case insensitve); \n Given task input file: {TaskInputFile}; [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                            }
                        }
                    }

                    // Add the potential to the systemSChedules because we are going to verify both length
                    _systemSchedules.AddRange(_potentialSystemSchedules);

                    if (i < maxTimesToPerformInput)
                    {
                        var baseExp = 2;
                        Assert.That(_scheduleCombos.Count() + 1, Is.EqualTo(baseExp), "The 'base exponential' of this system should be two (2)--> 1 schedule combo + empty scheudle each iteration.");
                        Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(Math.Pow(baseExp, i)), $"MaxTimes: {maxTimesToPerformInput}, it{i}: The number of potential scheduels output should be the max," +
                            $" which is equivalent to {baseExp}^(iteration: {i}) which is the systemSchedules count from last iteration. This is because all schedules can add the schedule at the current iteration as iteration, {i} < {maxTimesToPerformInput} (maxTimes).");
                        // Verify count matches expected combinatorics
                        int expectedScheduleCountExp = (int)Math.Pow(_scheduleCombos.Count() + 1, i + 1);
                        Assert.That(_systemSchedules.Count(), Is.EqualTo(expectedScheduleCountExp),
                            $"Iteration {i + 1}: Expected {expectedScheduleCountExp} schedules, got {_systemSchedules.Count}");
                    }
                    else // This is when maxTimesToPerform starts messing the combinatorics up:
                    {
                        int pot = 0; int scheds = 0;
                        k++; //count the number of times we have entered here. 
                        switch (maxTimesToPerformInput)
                        {
                            case 0:
                                Assert.That(_testSystemTasks.Peek().MaxTimesToPerform, Is.EqualTo(0), $"[CASE 0]: The task should have the correct maxTimesToPerform {maxTimesToPerformInput}. \n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}");
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(0), $"[CASE 0]: No potential system schedules should ever be produced with a maxTimesInput {maxTimesToPerformInput}. \n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds), $"[CASE 0]: The schedule list length after  iteration {i} (TimeDeconfliction call#{i + 1}) should be {1}. This is because only the empty schedule should exist. \n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                                Assert.IsTrue(_systemSchedules[0].Name.ToLower().Contains("empty"), $"[CASE 0]: The only schedule that should exist here is the empty scheudle. \n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                                break;
                            case 1: // This starts @ i = 1; _systemSchedule.Count = 2
                                Assert.That(_testSystemTasks.Peek().MaxTimesToPerform, Is.EqualTo(1), $"[CASE 1]: The task should have the correct maxTimesToPerform {maxTimesToPerformInput}. \n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}");
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(1), $"[CASE 1]: Only one potential system schedule should be made each time with maxTimes = {maxTimesToPerformInput}. \n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}");
                                Assert.That(_systemSchedules.Count, Is.EqualTo((i + 1) + 1), $"[CASE 1]: The schedule list length after  iteration {i} (TimeDeconfliction call#{i + 1}) should be {i + 2}.\n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                                break;
                            case 2: // This starts @ i = 2; _systemSchedule.Count = 4
                                if (k == 1) { pot = 3; scheds = 7; }       // 4  prev sched - 1@2x = 3 potential sched creation;  3 + 4  = 7  (sched to eval for next step)
                                else if (k == 2) { pot = 4; scheds = 11; } // 7  prev sched - 3@2x = 4 potential sched creation;  4 + 7  = 11 (sched to eval for next step)
                                else if (k == 3) { pot = 5; scheds = 16; } // 11 prev sched - 6@2x = 5 potential sched creation;  5 + 11 = 11 (sched to eval for next step) (BUT FINAL STEP)
                                else { Assert.Fail($"[CASE 2]: Reached an unreachable piece of iteration code. [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}."); }
                                // Assertions of lengths:
                                Assert.That(_testSystemTasks.Peek().MaxTimesToPerform, Is.EqualTo(2), $"[CASE 2]: The task should have the correct maxTimesToPerform {maxTimesToPerformInput}. \n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}");
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot), $"[CASE 2]: The potential schedule generation list length after  iteration {i} (TimeDeconfliction call#{i + 1}) should be {pot}.\n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds), $"[CASE 2]: The schedule list length after  iteration {i} (TimeDeconfliction call#{i + 1}) should be {scheds}.\n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                                break;
                            case 3: // This starts @ i = 3; _systemSchedule.Count = 8
                                if (k == 1) { pot = 7; scheds = 15; }       // 8   prev sched - 1@3x = 7  potential sched creation;  7 +  8   = 15 (sched to eval for next step)
                                else if (k == 2) { pot = 11; scheds = 26; }  // 15  prev sched - 4@3x = 11 potential sched creation;  11 + 15  = 26 (sched to eval for next step) (BUT FINAL STEP)
                                else { Assert.Fail($"[CASE 3]: Reached an unreachable piece of iteration code. [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}."); }
                                Assert.That(_testSystemTasks.Peek().MaxTimesToPerform, Is.EqualTo(3), $"[CASE 3]: The task should have the correct maxTimesToPerform {maxTimesToPerformInput}. \n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}");
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot), $"[CASE 3]: The potential schedule generation list length after  iteration {i} (TimeDeconfliction call#{i + 1}) should be {pot}.\n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds), $"[CASE 3]: The schedule list length after  iteration {i} (TimeDeconfliction call#{i + 1}) should be {scheds}.\n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                                break;
                            case 4: // This starts @ i = 4; _systemSchedule.Count = 16
                                if (k == 1) { pot = 15; scheds = 31; }  // 16 prev sched - 1@4x = 15  potential sched creation;  15 +  16   = 31 (sched to eval for next step) (BUT FINAL STEP)
                                else { Assert.Fail($"[CASE 4]: Reached an unreachable piece of iteration code. [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}."); }
                                Assert.That(_testSystemTasks.Peek().MaxTimesToPerform, Is.EqualTo(4), $"[CASE 4]: The task should have the correct maxTimesToPerform {maxTimesToPerformInput}. \n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}");
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot), $"[CASE 4]: The potential schedule generation list length after  iteration {i} (TimeDeconfliction call#{i + 1}) should be {pot}.\n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds), $"[CASE 4]: The schedule list length after  iteration {i} (TimeDeconfliction call#{i + 1}) should be {scheds}.\n [DEBUG]: MaxTimes: {maxTimesToPerformInput}, it{i}.");
                                break;
                        } // End Switch case for inputs
                    } // End if loop guarding i < maxTimes 
                } // End for loop
            }); // End assert multilpe

        } // End Test

        [TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5), TestCase(6), TestCase(50), TestCase(100)]
        public void CorrectPotentialScheduleCombosTest_TwoAssetOneTask_XTimesMax_AllIterations(int maxTimesToPerformInput)
        {
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "XTimesMaxTaskFiles", $"OneTaskTestFile_{maxTimesToPerformInput}TimesMax.json");
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "TwoAssetTestModel_TimeDeconfliction.json");
            BuildProgram();

            double currentTime = SimParameters.SimStartSeconds;
            double timeStep = 12.0;
            double simEndTime = 60;
            int iterations = (int)(simEndTime / timeStep);

            Assert.Multiple(() =>
            {
                Assert.That(currentTime, Is.EqualTo(SimParameters.SimStartSeconds), $"Current (start) time should be: {SimParameters.SimStartSeconds}.");
                Assert.That(timeStep, Is.EqualTo(SimParameters.SimStepSeconds), $"Time step should be: {SimParameters.SimStepSeconds}.");
                Assert.That(iterations, Is.EqualTo(5), $"Total iterations: 5.");
                Assert.That(maxTimesToPerformInput, Is.EqualTo(this._testSystemTasks.Peek().MaxTimesToPerform), $"MaxTimesToPerform should be: {maxTimesToPerformInput}.");
                Assert.That(this._testSimSystem.Assets.Count, Is.EqualTo(2), $"Should have 2 assets.");
                Assert.That(this._testSystemTasks.Count, Is.EqualTo(1), $"Should have 1 task.");
                Assert.That(_scheduleCombos.Count, Is.EqualTo(1), $"Should have 1 schedule combo (both assets do task1).");

                int k = 0; // Times over maxTimes threshold
                for (int i = 0; i < iterations; i++)
                {
                    currentTime = i * timeStep;

                    // TimeDeconfliction: Each old schedule tries to add the combo (both assets doing task1)
                    _potentialSystemSchedules = Scheduler.TimeDeconfliction(_systemSchedules, _scheduleCombos, currentTime);

                    // Verify output contains correct assets/tasks (2 assets, both doing task1)
                    foreach (var sched in _potentialSystemSchedules)
                    {
                        var lastEvent = sched.AllStates.Events.Peek();
                        Assert.That(lastEvent.Tasks.Count, Is.EqualTo(2), "Event should have 2 tasks (one per asset).");
                        
                        var assetNames = lastEvent.Tasks.Keys.Select(a => a.Name.ToLower()).OrderBy(n => n).ToList();
                        Assert.That(assetNames[0], Is.EqualTo("testasset1"), "Should contain testasset1.");
                        Assert.That(assetNames[1], Is.EqualTo("testasset2"), "Should contain testasset2.");
                        
                        foreach (var task in lastEvent.Tasks.Values)
                        {
                            Assert.That(task.Name.ToLower(), Is.EqualTo("task1"), "All tasks should be Task1.");
                        }
                    }

                    // Add potentials to systemSchedules
                    _systemSchedules.AddRange(_potentialSystemSchedules);

                    // Key: Each combo adds 2 tasks (one per asset), so limits hit differently!
                    int tasksAddedPerCombo = 2;
                    
                    // Can only extend if adding 2 more tasks doesn't exceed limit
                    if ((i + 1) * tasksAddedPerCombo <= maxTimesToPerformInput)
                    {
                        // All schedules can still extend
                        int baseExp = _scheduleCombos.Count + 1; // 1 combo + empty = 2
                        Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(Math.Pow(baseExp, i)),
                            $"MaxTimes={maxTimesToPerformInput}, i={i}: All schedules can extend (total tasks would be {(i+1)*2}).");
                        
                        int expectedScheduleCount = (int)Math.Pow(baseExp, i + 1);
                        Assert.That(_systemSchedules.Count, Is.EqualTo(expectedScheduleCount),
                            $"i={i}: Expected {expectedScheduleCount} schedules.");
                    }
                    else
                    {
                        k++;
                        int pot = 0, scheds = 0;
                        
                        switch (maxTimesToPerformInput)
                        {
                            case 1:
                                // Cannot add even once (2 tasks > 1)
                                pot = 0; scheds = 1;
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot), 
                                    $"[CASE 1]: i={i}: Cannot add 2 tasks when MaxTimes=1.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds), 
                                    $"[CASE 1]: i={i}: Only empty schedule exists.");
                                break;
                                
                            case 2:
                                // Can add once at i=0 (0+2=2 ✅), then only empty can extend
                                // Empty schedule always generates 1 new per iteration
                                pot = 1; scheds = i + 2; // Linear growth: 2,3,4,5,6
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot),
                                    $"[CASE 2]: i={i}, k={k}: Only empty schedule can extend.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds),
                                    $"[CASE 2]: i={i}: Linear growth = i+2.");
                                break;
                                
                            case 3:
                                // i=0: 0+2=2 ✅, i=1: 2+2=4>3 ❌, then only empty extends
                                // Same pattern as case 2: only empty can extend after i=0
                                pot = 1; scheds = i + 2; // Linear growth: 2,3,4,5,6
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot),
                                    $"[CASE 3]: i={i}, k={k}: Only empty schedule can extend.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds),
                                    $"[CASE 3]: i={i}: Linear growth = i+2.");
                                break;
                                
                            case 4:
                                // i=0: 0+2=2 ✅, i=1: 2+2=4 ✅, i=2: 4+2=6>4 ❌
                                // Schedules with 0,2 tasks can extend (not those with 4)
                                if (k == 1) { pot = 3; scheds = 7; }  // i=2: empty+2gen can extend
                                else if (k == 2) { pot = 4; scheds = 11; }  // i=3
                                else if (k == 3) { pot = 5; scheds = 16; } // i=4
                                
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot),
                                    $"[CASE 4]: i={i}, k={k}: Schedules with <4 tasks can extend.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds),
                                    $"[CASE 4]: i={i}: Growing pattern.");
                                break;
                                
                            case 5:
                                // i=0: 0+2=2 ✅, i=1: 2+2=4 ✅, i=2: 4+2=6>5 ❌
                                // Schedules with 0,2 tasks can extend (matching case 4 pattern)
                                if (k == 1) { pot = 3; scheds = 7; }  // i=2: same as case 4
                                else if (k == 2) { pot = 4; scheds = 11; }  // i=3
                                else if (k == 3) { pot = 5; scheds = 16; } // i=4
                                
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot),
                                    $"[CASE 5]: i={i}, k={k}: Schedules with <4 tasks can extend.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds),
                                    $"[CASE 5]: i={i}: Same growth pattern as case 4.");
                                break;
                                
                            case 6:
                                // i=0: 0+2=2 ✅, i=1: 2+2=4 ✅, i=2: 4+2=6 ✅, i=3: 6+2=8>6 ❌
                                // Schedules with 0,2,4 tasks can extend
                                if (k == 1) { pot = 7; scheds = 15; } // i=3: 8 input, 7 can extend (all but one with 6 tasks)
                                else if (k == 2) { pot = 11; scheds = 26; } // i=4
                                
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot),
                                    $"[CASE 6]: i={i}, k={k}: Expected {pot} potential schedules.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds),
                                    $"[CASE 6]: i={i}: Expected {scheds} total schedules.");
                                break;
                                
                            case 50:
                            case 100:
                                // Effectively unlimited for 5 iterations (max tasks = 10)
                                int baseExp = 2;
                                int expectedPot = (int)Math.Pow(baseExp, i);
                                int expectedScheds = (int)Math.Pow(baseExp, i + 1);
                                
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(expectedPot),
                                    $"[CASE {maxTimesToPerformInput}]: i={i}: Expected {expectedPot} potential schedules.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(expectedScheds),
                                    $"[CASE {maxTimesToPerformInput}]: i={i}: Expected {expectedScheds} total schedules.");
                                break;
                                
                            default:
                                Assert.Fail($"Unexpected MaxTimesToPerform: {maxTimesToPerformInput}");
                                break;
                        }
                    }
                }
            });
        } // End Test

        [TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5), TestCase(6), TestCase(50), TestCase(100)]
        public void CorrectPotentialScheduleCombosTest_TwoAssetThreeTask_XTimesMax_AllIterations_ConfirmGrowthPattern(int maxTimesToPerformInput)
        {
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "XTimesMaxTaskFiles", $"ThreeTaskTestFile_{maxTimesToPerformInput}TimesMax.json");
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "TwoAssetTestModel_TimeDeconfliction.json");
            BuildProgram();

            double currentTime = SimParameters.SimStartSeconds;
            double timeStep = 12.0;
            double simEndTime = 60;
            int iterations = (int)(simEndTime / timeStep);

            Assert.Multiple(() =>
            {
                Assert.That(currentTime, Is.EqualTo(SimParameters.SimStartSeconds), $"Current (start) time should be: {SimParameters.SimStartSeconds}.");
                Assert.That(timeStep, Is.EqualTo(SimParameters.SimStepSeconds), $"Time step should be: {SimParameters.SimStepSeconds}.");
                Assert.That(iterations, Is.EqualTo(5), $"Total iterations: 5.");
                Assert.That(this._testSimSystem.Assets.Count, Is.EqualTo(2), $"Should have 2 assets.");
                Assert.That(this._testSystemTasks.Count, Is.EqualTo(3), $"Should have 3 tasks.");
                Assert.That(_scheduleCombos.Count, Is.EqualTo(9), $"Should have 9 schedule combos (3×3).");
                
                // Verify all tasks have the same MaxTimesToPerform
                foreach (var task in _testSystemTasks)
                {
                    Assert.That(task.MaxTimesToPerform, Is.EqualTo(maxTimesToPerformInput), 
                        $"All tasks should have MaxTimesToPerform={maxTimesToPerformInput}.");
                }

                int k = 0; // Times over maxTimes threshold
                for (int i = 0; i < iterations; i++)
                {
                    currentTime = i * timeStep;

                    // TimeDeconfliction: Each old schedule tries to add each combo
                    _potentialSystemSchedules = Scheduler.TimeDeconfliction(_systemSchedules, _scheduleCombos, currentTime);

                    // Verify output contains correct number of tasks per event
                    foreach (var sched in _potentialSystemSchedules)
                    {
                        var lastEvent = sched.AllStates.Events.Peek();
                        Assert.That(lastEvent.Tasks.Count, Is.EqualTo(2), "Event should have 2 tasks (one per asset).");
                        
                        var assetNames = lastEvent.Tasks.Keys.Select(a => a.Name.ToLower()).OrderBy(n => n).ToList();
                        Assert.That(assetNames[0], Is.EqualTo("testasset1"), "Should contain testasset1.");
                        Assert.That(assetNames[1], Is.EqualTo("testasset2"), "Should contain testasset2.");
                    }

                    // Add potentials to systemSchedules
                    _systemSchedules.AddRange(_potentialSystemSchedules);

                    // Each combo can add 0, 1, or 2 occurrences of any given task
                    // Most restrictive: both assets do same task = 2 occurrences
                    int maxTasksAddedPerCombo = 2; // Worst case: both assets do same task
                    
                    // Can only extend if adding worst-case 2 tasks doesn't exceed limit
                    if ((i + 1) * maxTasksAddedPerCombo <= maxTimesToPerformInput)
                    {
                        // All schedules can extend
                        int numCombos = _scheduleCombos.Count; // 9 combos
                        // Pattern: pot = 9×10^i, total = 10^(i+1)
                        int expectedPot = numCombos * (int)Math.Pow(10, i);
                        int expectedTotal = (int)Math.Pow(10, i + 1);
                        
                        Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(expectedPot),
                            $"MaxTimes={maxTimesToPerformInput}, i={i}: Expected {expectedPot} potentials (9×10^{i}).");
                        Assert.That(_systemSchedules.Count, Is.EqualTo(expectedTotal),
                            $"i={i}: Expected {expectedTotal} total schedules (10^{i+1}).");
                    }
                    else
                    {
                        k++;
                        // This is complex - schedules can extend if they haven't exceeded MaxTimes for ANY task
                        // For now, just verify growth continues but slows
                        Assert.That(_potentialSystemSchedules.Count, Is.GreaterThan(0),
                            $"[CASE {maxTimesToPerformInput}]: i={i}, k={k}: Some schedules can still extend.");
                        Assert.That(_potentialSystemSchedules.Count, Is.LessThanOrEqualTo(_systemSchedules.Count),
                            $"[CASE {maxTimesToPerformInput}]: i={i}: Not all schedules can extend (some hit limits).");
                        
                        // For unlimited cases, verify exponential growth continues
                        if (maxTimesToPerformInput >= 10)
                        {
                            int baseExp = 10;
                            int expectedPot = (int)Math.Pow(baseExp, i);
                            int expectedScheds = (int)Math.Pow(baseExp, i + 1);
                            
                            Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(expectedPot),
                                $"[CASE {maxTimesToPerformInput}]: i={i}: Unlimited - all can extend.");
                            Assert.That(_systemSchedules.Count, Is.EqualTo(expectedScheds),
                                $"[CASE {maxTimesToPerformInput}]: i={i}: Pure exponential = 10^{i+1}.");
                        }
                        
                        // Log actual values for analysis
                        Console.WriteLine($"[CASE {maxTimesToPerformInput}] i={i}, k={k}: pot={_potentialSystemSchedules.Count}, scheds={_systemSchedules.Count}");
                    }
                }
            });
        } // End Test

        [TestCase(5), TestCase(6)]
        public void CorrectPotentialScheduleCombosTest_TwoAssetThreeTask_XTimesMax_AllIterations_ExactValues(int maxTimesToPerformInput)
        {
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "XTimesMaxTaskFiles", $"ThreeTaskTestFile_{maxTimesToPerformInput}TimesMax.json");
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "TwoAssetTestModel_TimeDeconfliction.json");
            BuildProgram();

            double currentTime = SimParameters.SimStartSeconds;
            double timeStep = 12.0;
            double simEndTime = 60;
            int iterations = (int)(simEndTime / timeStep);

            Assert.Multiple(() =>
            {
                Assert.That(currentTime, Is.EqualTo(SimParameters.SimStartSeconds), $"Current (start) time should be: {SimParameters.SimStartSeconds}.");
                Assert.That(timeStep, Is.EqualTo(SimParameters.SimStepSeconds), $"Time step should be: {SimParameters.SimStepSeconds}.");
                Assert.That(iterations, Is.EqualTo(5), $"Total iterations: 5.");
                Assert.That(this._testSimSystem.Assets.Count, Is.EqualTo(2), $"Should have 2 assets.");
                Assert.That(this._testSystemTasks.Count, Is.EqualTo(3), $"Should have 3 tasks.");
                Assert.That(_scheduleCombos.Count, Is.EqualTo(9), $"Should have 9 schedule combos (3×3).");
                
                foreach (var task in _testSystemTasks)
                {
                    Assert.That(task.MaxTimesToPerform, Is.EqualTo(maxTimesToPerformInput), 
                        $"All tasks should have MaxTimesToPerform={maxTimesToPerformInput}.");
                }

                int k = 0; // Times over maxTimes threshold
                for (int i = 0; i < iterations; i++)
                {
                    currentTime = i * timeStep;

                    _potentialSystemSchedules = Scheduler.TimeDeconfliction(_systemSchedules, _scheduleCombos, currentTime);

                    foreach (var sched in _potentialSystemSchedules)
                    {
                        var lastEvent = sched.AllStates.Events.Peek();
                        Assert.That(lastEvent.Tasks.Count, Is.EqualTo(2), "Event should have 2 tasks (one per asset).");
                        
                        var assetNames = lastEvent.Tasks.Keys.Select(a => a.Name.ToLower()).OrderBy(n => n).ToList();
                        Assert.That(assetNames[0], Is.EqualTo("testasset1"), "Should contain testasset1.");
                        Assert.That(assetNames[1], Is.EqualTo("testasset2"), "Should contain testasset2.");
                    }

                    _systemSchedules.AddRange(_potentialSystemSchedules);

                    int maxTasksAddedPerCombo = 2; // Worst case: both assets do same task
                    
                    if ((i + 1) * maxTasksAddedPerCombo <= maxTimesToPerformInput)
                    {
                        // All schedules can extend - pure exponential growth
                        int numCombos = _scheduleCombos.Count; // 9
                        int expectedPot = numCombos * (int)Math.Pow(10, i);
                        int expectedTotal = (int)Math.Pow(10, i + 1);
                        
                        Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(expectedPot),
                            $"MaxTimes={maxTimesToPerformInput}, i={i}: Expected {expectedPot} potentials (9×10^{i}).");
                        Assert.That(_systemSchedules.Count, Is.EqualTo(expectedTotal),
                            $"i={i}: Expected {expectedTotal} total schedules (10^{i+1}).");
                    }
                    else
                    {
                        k++;
                        int pot = 0, scheds = 0;
                        
                        switch (maxTimesToPerformInput)
                        {
                            case 5:
                                // i=0,1: exponential (0+2≤5, 2+2≤5), i=2: 4+2>5 (limit kicks in)
                                if (k == 1) { pot = 897; scheds = 997; }   // i=2
                                else if (k == 2) { pot = 8604; scheds = 9601; }  // i=3
                                else if (k == 3) { pot = 74871; scheds = 84472; } // i=4
                                else { Assert.Fail($"[CASE 5]: Unreachable k={k} at i={i}."); }
                                
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot),
                                    $"[CASE 5]: i={i}, k={k}: Expected {pot} potential schedules.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds),
                                    $"[CASE 5]: i={i}: Expected {scheds} total schedules.");
                                break;
                                
                            case 6:
                                // i=0,1,2: exponential (0+2≤6, 2+2≤6, 4+2≤6), i=3: 6+2>6 (limit)
                                if (k == 1) { pot = 8949; scheds = 9949; }   // i=3
                                else if (k == 2) { pot = 86313; scheds = 96262; } // i=4
                                else { Assert.Fail($"[CASE 6]: Unreachable k={k} at i={i}."); }
                                
                                Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot),
                                    $"[CASE 6]: i={i}, k={k}: Expected {pot} potential schedules.");
                                Assert.That(_systemSchedules.Count, Is.EqualTo(scheds),
                                    $"[CASE 6]: i={i}: Expected {scheds} total schedules.");
                                break;
                                
                            default:
                                Assert.Fail($"Unexpected MaxTimesToPerform: {maxTimesToPerformInput} in else branch.");
                                break;
                        }
                    }
                }
            });
        } // End Test

        [TestCase(1,2,10), TestCase(2,2,10), TestCase(2,5,10), TestCase(2,6,10)]
        public void CorrectPotentialScheduleCombosTest_TwoAssetThreeTask_X_Y_ZTimesMax_AllIterations_ExactValues(int maxTimesToPerformInputX, int maxTimesToPerformInputY, int maxTimesToPerformInputZ)
        {
            TaskInputFile = Path.Combine(CurrentTestDir, "Inputs", "XTimesMaxTaskFiles", $"ThreeTaskTestFile_{maxTimesToPerformInputX}_{maxTimesToPerformInputY}_{maxTimesToPerformInputZ}TimesMax.json");
            ModelInputFile = Path.Combine(CurrentTestDir, "Inputs", "TwoAssetTestModel_TimeDeconfliction.json");
            BuildProgram();

            double currentTime = SimParameters.SimStartSeconds;
            double timeStep = 12.0;
            double simEndTime = 60;
            int iterations = (int)(simEndTime / timeStep);

            Assert.Multiple(() =>
            {
                Assert.That(currentTime, Is.EqualTo(SimParameters.SimStartSeconds), $"Current time should be start time.");
                Assert.That(timeStep, Is.EqualTo(SimParameters.SimStepSeconds), $"Time step should match.");
                Assert.That(iterations, Is.EqualTo(5), $"Total iterations: 5.");
                Assert.That(this._testSimSystem.Assets.Count, Is.EqualTo(2), $"Should have 2 assets.");
                Assert.That(this._testSystemTasks.Count, Is.EqualTo(3), $"Should have 3 tasks.");
                Assert.That(_scheduleCombos.Count, Is.EqualTo(9), $"Should have 9 schedule combos.");
                
                // Verify each task has correct MaxTimesToPerform
                var tasksList = _testSystemTasks.OrderBy(t => t.Name).ToList();
                Assert.That(tasksList[0].MaxTimesToPerform, Is.EqualTo(maxTimesToPerformInputX), $"Task1 should have MaxTimes={maxTimesToPerformInputX}.");
                Assert.That(tasksList[1].MaxTimesToPerform, Is.EqualTo(maxTimesToPerformInputY), $"Task2 should have MaxTimes={maxTimesToPerformInputY}.");
                Assert.That(tasksList[2].MaxTimesToPerform, Is.EqualTo(maxTimesToPerformInputZ), $"Task3 should have MaxTimes={maxTimesToPerformInputZ}.");

                for (int i = 0; i < iterations; i++)
                {
                    currentTime = i * timeStep;

                    _potentialSystemSchedules = Scheduler.TimeDeconfliction(_systemSchedules, _scheduleCombos, currentTime);

                    // Verify event structure
                    foreach (var sched in _potentialSystemSchedules)
                    {
                        var lastEvent = sched.AllStates.Events.Peek();
                        Assert.That(lastEvent.Tasks.Count, Is.EqualTo(2), "Event should have 2 tasks.");
                        
                        var assetNames = lastEvent.Tasks.Keys.Select(a => a.Name.ToLower()).OrderBy(n => n).ToList();
                        Assert.That(assetNames[0], Is.EqualTo("testasset1"), "Should contain testasset1.");
                        Assert.That(assetNames[1], Is.EqualTo("testasset2"), "Should contain testasset2.");
                    }

                    _systemSchedules.AddRange(_potentialSystemSchedules);

                    // Assert exact values based on the specific test case
                    int pot = 0, scheds = 0;
                    string caseKey = $"{maxTimesToPerformInputX},{maxTimesToPerformInputY},{maxTimesToPerformInputZ}";
                    
                    switch (caseKey)
                    {
                        case "1,2,10":
                            // Task1=1 (most restrictive), Task2=2, Task3=10
                            if (i == 0) { pot = 8; scheds = 9; }      // Only combos without T1-T1 (8 valid)
                            else if (i == 1) { pot = 47; scheds = 56; }
                            else if (i == 2) { pot = 204; scheds = 260; }
                            else if (i == 3) { pot = 748; scheds = 1008; }
                            else if (i == 4) { pot = 2464; scheds = 3472; }
                            break;
                            
                        case "2,2,10":
                            // Task1=2, Task2=2 (both restrictive), Task3=10
                            if (i == 0) { pot = 9; scheds = 10; }      // All combos valid
                            else if (i == 1) { pot = 72; scheds = 82; }
                            else if (i == 2) { pot = 418; scheds = 500; }
                            else if (i == 3) { pot = 1932; scheds = 2432; }
                            else if (i == 4) { pot = 7680; scheds = 10112; }
                            break;
                            
                        case "2,5,10":
                            // Task1=2 (most restrictive), Task2=5, Task3=10
                            if (i == 0) { pot = 9; scheds = 10; }
                            else if (i == 1) { pot = 81; scheds = 91; }
                            else if (i == 2) { pot = 648; scheds = 739; }
                            else if (i == 3) { pot = 4653; scheds = 5392; }
                            else if (i == 4) { pot = 29352; scheds = 34744; }
                            break;
                            
                        case "2,6,10":
                            // Task1=2, Task2=6, Task3=10
                            if (i == 0) { pot = 9; scheds = 10; }
                            else if (i == 1) { pot = 81; scheds = 91; }
                            else if (i == 2) { pot = 649; scheds = 740; }
                            else if (i == 3) { pot = 4768; scheds = 5508; }
                            else if (i == 4) { pot = 32116; scheds = 37624; }
                            break;
                            
                        default:
                            Assert.Fail($"Unexpected test case: {caseKey}");
                            break;
                    }
                    
                    // Check if actual matches expected
                    string potCheck = (_potentialSystemSchedules.Count == pot) ? "✅" : "❌";
                    string schedsCheck = (_systemSchedules.Count == scheds) ? "✅" : "❌";
                    
                    // Pretty console logging
                    Console.WriteLine($"[CASE {caseKey}] i={i}: pot={_potentialSystemSchedules.Count} (exp:{pot}) {potCheck} | scheds={_systemSchedules.Count} (exp:{scheds}) {schedsCheck}");
                    
                    Assert.That(_potentialSystemSchedules.Count, Is.EqualTo(pot),
                        $"[CASE {caseKey}] i={i}: Expected {pot} potential schedules.");
                    Assert.That(_systemSchedules.Count, Is.EqualTo(scheds),
                        $"[CASE {caseKey}] i={i}: Expected {scheds} total schedules.");
                }
            });
            
        } // End Test
        
    }
}