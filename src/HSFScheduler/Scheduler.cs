// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.Reflection;
using Utilities;
using HSFSystem;
using UserModel;
using MissionElements;
using log4net;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HSFScheduler
{
    /// <summary>
    /// Creates valid schedules for a system
    /// </summary>
    [Serializable]
    public class Scheduler
    {
        //TODO:  Support monitoring of scheduler progress - Eric Mehiel
        #region Attributes
        private double _startTime;
        private double _stepLength;
        private double _endTime;
        private int _maxNumSchedules;
        private int _numSchedCropTo;

        public double TotalTime { get; }
        public double PregenTime { get; }
        public double SchedTime { get; }
        public double AccumSchedTime { get; }
                
        // Needed for schedule evaluation and computation:
        public static SystemSchedule? emptySchedule {get; private set; }
        public List<SystemSchedule> systemSchedules { get; private set; } = new List<SystemSchedule>();
        public bool canPregenAccess {get; private set; }
        public Stack<Stack<Access>> scheduleCombos { get; private set; }= new Stack<Stack<Access>>(); 
        public Stack<Access>? preGeneratedAccesses { get; private set; }
        public List<SystemSchedule> potentialSystemSchedules { get; private set; } = new List<SystemSchedule>();
        public List<SystemSchedule> systemCanPerformList { get; private set; } = new List<SystemSchedule>();
        
        public Evaluator ScheduleEvaluator { get; private set; }
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        /// <summary>
        /// Creates a scheduler for the given system and simulation scenario
        /// </summary>
        /// <param name="scheduleEvaluator"></param>
        public Scheduler(Evaluator scheduleEvaluator)
        {
            ScheduleEvaluator = scheduleEvaluator;
            _startTime = SimParameters.SimStartSeconds;
            _endTime = SimParameters.SimEndSeconds;
            _stepLength = SimParameters.SimStepSeconds;
            _maxNumSchedules = SchedParameters.MaxNumScheds;
            _numSchedCropTo = SchedParameters.NumSchedCropTo;
        }

        /// <summary>
        /// Generate schedules by adding a new event to the end of existing ones
        /// Create a new system schedule list by adding each of the new Task commands for the Assets onto each of the old schedules
        /// </summary>
        /// <param name="system"></param>
        /// <param name="tasks"></param>
        /// <param name="initialStateList"></param>
        /// <returns></returns>
        public virtual List<SystemSchedule> GenerateSchedules(SystemClass system, Stack<MissionElements.Task> tasks, SystemState initialStateList)
        {
            log.Info("SIMULATING... ");

            // Create empty systemSchedule with initial state set
            InitializeEmptySchedule(this.systemSchedules, initialStateList); // Add Unit Test #0 (test empty schedule) --- Or do it later after cropping? Can do both.

            // if all asset position types are not dynamic types, can pregenerate accesses for the simulation
            canPregenAccessLogic(system); // Unit Test Method #

            // Unit Test Method #2: Pregen access logic
            if (canPregenAccess) // If accesses can be pregenereated; do it now. 
            {
                log.Info("Pregenerating Accesses...");

                // This method completes the Access pregeneration for pre-determined orbital dynamics. Returns Stack<Access> that is not yet
                // a full combination of Assets and Tasks (it is just the pre-determined )
                preGeneratedAccesses = Access.pregenerateAccessesByAsset(system, tasks, _startTime, _endTime, _stepLength); //Technically doesnt need to take _endTime and_stepLengthbut its okay for now
                Access.writeAccessReport(preGeneratedAccesses); //- TODO:  Finish this code - EAM
                log.Info("Done pregenerating accesses. There are " + preGeneratedAccesses.Count + " accesses.");
            }
            // Otherwise, generate an exhaustive list of possibilities for assetTaskList:
            else
            {
                // This step is generates all combinations with the default assumotion that access is the entire simulation time... 
                log.Info("Generating Exhaustive Task Combinations... ");

                /* This method creates a shell for all (empty/not-yet-assessed) Accesses by Asset & Task combination.  Thus many of these potential schedules will be cropped out via non-accesses.
                Furthermore, can add a pre-cropping tool that shed the possible access combinations via restrictions levied by task type and asset class,
                or asset-asset interaction, or time-based restrictions (like assets/tasks required to act in serial versus parallel), etc.). */
                scheduleCombos = GenerateExhaustiveSystemSchedules(system,tasks,scheduleCombos,_startTime,_endTime); //Technically doesn't need to take scheduleCombos but its okay for now

                // Access.writeAccessReport(preGeneratedAccesses); //- TODO:  Finish this code - EAM
                log.Info("Done generating exhaustive task combinations");
            }

            /// TODO: Delete (or never create in the first place) schedules with inconsistent asset tasks (because of asset dependencies)

            // Initializations for the loop below
            //List<SystemSchedule> potentialSystemSchedules = new List<SystemSchedule>();
            
            //mainSchedulingLoop(double currentTime, double endTime, double timeStep)
            for (double currentTime = _startTime; currentTime < _endTime; currentTime += _stepLength)
            {
                log.Info("Simulation Time " + currentTime);
                // if accesses are pregenerated, look up the access information and update assetTaskList
                if (canPregenAccess)
                {
                    // This code: Generates the exhaustive system schedules (combinations of Asset & Task) for all Assets that have predetermined Accesses
                    // (at the current time). This is stepped through and passed currentTime as scheduleCombos is a Stack that you can continue adding to,
                    // and it is most convenient to just pull the current accesses and push. 
                    scheduleCombos = GenerateExhaustiveSystemSchedules(preGeneratedAccesses, system, currentTime);
                }
                
                // First, crop schedules to maxNumchedules: 
                systemSchedules = CropToMaxSchedules(systemSchedules, Scheduler.emptySchedule);

                // Generate an exhaustive list of new tasks possible from the combinations of Assets and Tasks
                //TODO: Parallelize this.

                //Parallel.ForEach(systemSchedules, (oldSystemSchedule) =>
                //"Time Deconfliction" step --> we dont create possible schedules when a schedule is bust ()
                potentialSystemSchedules = TimeDeconfliction(systemSchedules, currentTime,scheduleCombos);
                //int k = 0; 
                // foreach(var oldSystemSchedule in systemSchedules)
                // {
                //     //potentialSystemSchedules.Add(new SystemSchedule( new StateHistory(oldSystemSchedule.AllStates)));
                //     foreach (var newAccessStack in scheduleCombos)
                //     {
                //         k++;
                //         if (oldSystemSchedule.CanAddTasks(newAccessStack, currentTime))
                //         {
                //             var CopySchedule = new StateHistory(oldSystemSchedule.AllStates);
                //             potentialSystemSchedules.Add(new SystemSchedule(CopySchedule, newAccessStack, currentTime));
                //             // oldSched = new SystemSchedule(CopySchedule);
                //         }

                //     }
                //}

                // "State Deconfliction" Step --> 
                systemCanPerformList = CheckAllPotentialSchedules(system, potentialSystemSchedules);

                systemCanPerformList = EvaluateAndSortCanPerformSchedules(ScheduleEvaluator, systemCanPerformList);

        
                systemSchedules = MergeAndClearSystemSchedules();

                // Print completion percentage in command window
                Console.WriteLine("Scheduler Status: {0:F}% done; {1} schedules generated.", 100 * currentTime / _endTime, systemSchedules.Count);
            }
            return systemSchedules;
        }

        /// <summary>
        /// Remove Schedules with the worst scores from the List of SystemSchedules so that there are only _maxNumSchedules.
        /// </summary>
        /// <param name="schedulesToCrop"></param>
        /// <param name="scheduleEvaluator"></param>
        /// <param name="emptySched"></param>
        /// 
        public static void InitializeEmptySchedule(List<SystemSchedule> systemSchedules, SystemState initialStateList)
        {
            string Name = "Empty Schedule"; 
            emptySchedule = new SystemSchedule(initialStateList, Name); // Create the first empty schedule. This should schange as things move forward. 
            systemSchedules.Add(emptySchedule);  

        }
        public virtual void canPregenAccessLogic(SystemClass system)
        {
            canPregenAccess = true;
            foreach (var asset in system.Assets)
            {
                if(asset.AssetDynamicState != null)
                    canPregenAccess &= asset.AssetDynamicState.Type != HSFUniverse.DynamicStateType.DYNAMIC_ECI && asset.AssetDynamicState.Type != HSFUniverse.DynamicStateType.DYNAMIC_LLA && asset.AssetDynamicState.Type != HSFUniverse.DynamicStateType.NULL_STATE;
                else
                    canPregenAccess = false;
            }
            //return canPregenAccess; 
        }

        public List<SystemSchedule> CropToMaxSchedules(List<SystemSchedule> systemSchedules, SystemSchedule emptySchedule)
        {
            if (systemSchedules.Count > _maxNumSchedules)
            {
                log.Info("Cropping " + systemSchedules.Count + " Schedules.");
                CropSchedules(systemSchedules, ScheduleEvaluator, emptySchedule);
                systemSchedules.Add(emptySchedule);
            }
            return systemSchedules; 
        }

        public void CropSchedules(List<SystemSchedule> schedulesToCrop, Evaluator scheduleEvaluator, SystemSchedule emptySched)
        {
            // Evaluate the schedules and set their values
            foreach (SystemSchedule systemSchedule in schedulesToCrop)
                systemSchedule.ScheduleValue = scheduleEvaluator.Evaluate(systemSchedule);

            // Sort the sysScheds by their values
            schedulesToCrop.Sort((x, y) => x.ScheduleValue.CompareTo(y.ScheduleValue));

            // Delete the sysScheds that don't fit
            int numSched = schedulesToCrop.Count;
            for (int i = 0; i < numSched - _numSchedCropTo; i++)
            {
                schedulesToCrop.Remove(schedulesToCrop[0]);
            }

            //schedulesToCrop.TrimExcess();
        }

        /// <summary>
        /// Return all possible combinations of performing Tasks by Asset at current simulation time
        /// </summary>
        /// <param name="currentAccessForAllAssets"></param>
        /// <param name="system"></param>
        /// <param name="currentTime"></param>
        /// <returns></returns>
        public static Stack<Stack<Access>> GenerateExhaustiveSystemSchedules(Stack<Access> currentAccessForAllAssets, SystemClass system, double currentTime)
        {
            // A stack of accesses stacked by asset
            Stack<Stack<Access>> currentAccessesByAsset = new Stack<Stack<Access>>();
            foreach (Asset asset in system.Assets)
                currentAccessesByAsset.Push(Access.getCurrentAccessesForAsset(currentAccessForAllAssets, asset, currentTime));

            IEnumerable<IEnumerable<Access>> allScheduleCombos = currentAccessesByAsset.CartesianProduct();

            Stack<Stack<Access>> allOfThem = new Stack<Stack<Access>>();
            foreach (var accessStack in allScheduleCombos)
            {
                Stack<Access> someOfThem = new Stack<Access>(accessStack);
                allOfThem.Push(someOfThem);
            }

            return allOfThem;
        }

        public static Stack<Stack<Access>> GenerateExhaustiveSystemSchedules(SystemClass system, Stack<MissionElements.Task> tasks, Stack<Stack<Access>> scheduleCombos, double currentTime, double endTime)
        {
            //GenerateExhaustiveSystemSchedules(SystemClass system, Stack<Task>)
            Stack<Stack<Access>> exhaustive = new Stack<Stack<Access>>();
            //Stack<Access> allAccesses = new Stack<Access>(tasks.Count);

            foreach (var asset in system.Assets)
            {
                Stack<Access> allAccesses = new Stack<Access>(tasks.Count);
                foreach (var task in tasks)
                {
                    allAccesses.Push(new Access(asset, task, currentTime, endTime)); //This generates acess for the current time to end of Sim, by default
                    //allAccesses.Push(new Access(asset, null));
                }
                exhaustive.Push(allAccesses);

                //allAccesses.Clear();
            }

            // Question: Can two assets do the same task in the same event? Where/how is this enforced/modeled?
            IEnumerable<IEnumerable<Access>> allScheduleCombos = exhaustive.CartesianProduct();

            foreach (var accessStack in allScheduleCombos)
            {
                Stack<Access> someOfThem = new Stack<Access>(accessStack); // Is this link of code necessary? 
                scheduleCombos.Push(someOfThem);
            }       

            return scheduleCombos; 
        }

        public virtual List<SystemSchedule> TimeDeconfliction(List<SystemSchedule> systemSchedules,double currentTime,Stack<Stack<Access>> scheduleCombos)
        {
            int k = 0; 
            foreach(var oldSystemSchedule in systemSchedules)
            {
                //potentialSystemSchedules.Add(new SystemSchedule( new StateHistory(oldSystemSchedule.AllStates)));
                foreach (var newAccessTaskStack in scheduleCombos)
                {
                    k++;
                    if (oldSystemSchedule.CanAddTasks(newAccessTaskStack, currentTime))
                    {
                        var CopySchedule = new StateHistory(oldSystemSchedule.AllStates);
                        potentialSystemSchedules.Add(new SystemSchedule(CopySchedule, newAccessTaskStack, currentTime));
                        // oldSched = new SystemSchedule(CopySchedule);
                    }

                }
            }
            return potentialSystemSchedules; 
        }
        public List<SystemSchedule> CheckAllPotentialSchedules(SystemClass system, List<SystemSchedule> potentialSystemSchedules)
        {
                int numSched = 0;
                foreach (var potentialSchedule in potentialSystemSchedules)
                {


                    if (Checker.CheckSchedule(system, potentialSchedule)) {
                        //potentialSchedule.GetEndState().GetLastValue()

                        
                        systemCanPerformList.Add(potentialSchedule);
                        numSched++;
                    }
                }
                return systemCanPerformList;
        }

        public static List<SystemSchedule> EvaluateAndSortCanPerformSchedules(Evaluator scheduleEvaluator, List<SystemSchedule> systemCanPerformList)
        {
           // Evaluate Schedule Step --> 
            foreach (SystemSchedule systemSchedule in systemCanPerformList)
                systemSchedule.ScheduleValue = scheduleEvaluator.Evaluate(systemSchedule);

            // Sort the schedule by their values:
            systemCanPerformList.Sort((x, y) => x.ScheduleValue.CompareTo(y.ScheduleValue));
            systemCanPerformList.Reverse();
            
            // Return the sorted list back to the scheduler (caller):
            return systemCanPerformList;
        }

        public List<SystemSchedule> MergeAndClearSystemSchedules()
        {
            // Merge old and new systemSchedules
            var oldSystemCanPerfrom = new List<SystemSchedule>(this.systemCanPerformList);
            this.systemSchedules.InsertRange(0, oldSystemCanPerfrom);//<--This was potentialSystemSchedule doubling stuff up
            this.potentialSystemSchedules.Clear();
            this.systemCanPerformList.Clear();

            return this.systemSchedules;
        }

        #region GenerateSchedules() sub methods 


        public void MainSchedulingLoop(double currentTime, double endTime, double timeStep)
        {

        }

        // // Generic method to get the value of a private field by name
        // protected static T GetPrivateAttribute<T>(string attributeName)
        // {
        //     // Get the type of the current instance (this will be the derived class type)
        //     Type type = this.GetType();
            
        //     // Find the field by name, considering non-public instance fields
        //     FieldInfo fieldInfo = type.GetField(attributeName, BindingFlags.NonPublic | BindingFlags.Instance);

        //     if (fieldInfo != null)
        //     {
        //         // Return the value of the private field
        //         return (T)fieldInfo.GetValue(this);
        //     }

        //     throw new ArgumentException("No private attribute with the specified name found.");
        // }


        #endregion 

    }
    
}


