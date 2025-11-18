// Copyright (c) 2025 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using NUnit.Framework;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HSFSystem;
using UserModel;
using HSFScheduler;
using Utilities;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Runner for ScheduleDataCapture - NOT part of normal test suite
    /// Run manually to generate comprehensive schedule observation data
    /// </summary>
    [TestFixture, Category("ScheduleDataCapture"), Explicit]
    public class ScheduleDataCaptureRunner : SchedulerUnitTest
    {
        [TestCase("AeolusTasks_3.json", "Aeolus_3Tasks", TestName = "Aeolus_3Tasks")]
        [TestCase("AeolusTasks_30.json", "Aeolus_30Tasks", TestName = "Aeolus_30Tasks")]
        [TestCase("AeolusTasks_300.json", "Aeolus_300Tasks", TestName = "Aeolus_300Tasks")]
        public void CaptureAeolus_FinalOutput(string taskInputFile, string scenarioName)
        {
            var capture = new ScheduleDataCapture();
            
            // Load scenario inputs
            string inputsDir = Path.Combine(CurrentTestDir, "Inputs");
            string simInput = Path.Combine(inputsDir, "AeolusSim_150sec_max10_cropTo5.json");
            string taskInput = Path.Combine(inputsDir, taskInputFile);
            string modelInput = Path.Combine(inputsDir, "DSAC_Static_ScriptedCS.json");
            
            // Load scenario up to main loop
            capture.LoadScenarioToMainLoop(simInput, taskInput, modelInput);
            
            // Set output directory (with task count for clarity - extracted from taskInput filename)
            capture.SetOutputDirectory();

            // Split up CreateSchedules() steps using SchedulerUnitTest vars (enables per-iteration capture later):
            // 1. Create SimSystem (same as CreateSchedules) - use program's lists but assign to both
            capture.Program.SimSystem = new SystemClass(
                capture.Program.AssetList, 
                capture.Program.SubList, 
                capture.Program.ConstraintsList, 
                capture.Program.SystemUniverse);
            
            // Also update test system (matches what HorizonLoadHelper does)
            // This ensures _testSimSystem matches SimSystem for per-iteration access
            if (capture.TestSimSystem != capture.Program.SimSystem)
            {
                // They should already match from HorizonLoadHelper, but ensure consistency
                // _testSimSystem is already set by HorizonLoadHelper
            }

            if (capture.Program.SimSystem.CheckForCircularDependencies())
                throw new NotFiniteNumberException("System has circular dependencies! Please correct then try again.");

            // 2. Create scheduler (same as CreateSchedules)
            capture.Program.scheduler = new Scheduler(capture.Program.SchedEvaluator);

            // CRITICAL: Reset static Scheduler fields to match fresh process state (Program.Main() gets fresh process, tests share process)
            // This ensures deterministic behavior matching Program.Main() execution
            Scheduler.SchedulerStep = -1;  // Default initial value (GenerateSchedules will += 1 on first iteration)
            Scheduler._schedID = 0;        // Default initial value (starts at 0, gets incremented in UpdateScheduleID)

            // 3. Call GenerateSchedules using test vars (same as CreateSchedules, but enables per-iteration capture)
            // Use test vars so we can step through iterations manually later
            List<SystemSchedule> finalSchedules = capture.Program.scheduler.GenerateSchedules(
                capture.TestSimSystem!,  // Use test var (matches SimSystem but allows per-iteration control)
                capture.TestSystemTasks,  // Use test var (Stack from program.SystemTasks)
                capture.TestInitialSysState);  // Use test var (matches program.InitialSysState)
            
            // Store schedules in program (same as CreateSchedules does)
            capture.Program.Schedules = finalSchedules;
            
            // Generate hash set right after GenerateSchedules() returns (before EvaluateSchedules() sorts them)
            // This matches Program.Main() - computes hashes on schedules as they come out of GenerateSchedules()
            capture.GenerateAndSaveScheduleHashSet(capture.Program.Schedules);
            capture.SaveScheduleHashBlockchainSummary(capture.Program.Schedules);
            
            // Evaluate schedules (same as Program.Main())
            // This sorts capture.Program.Schedules by ScheduleValue (descending) - same as Program.Main()
            double maxSched = capture.Program.EvaluateSchedules();
            
            // Process and capture schedules (generic method - saves normal output + hashes)
            // Use capture.Program.Schedules (same reference as finalSchedules, now sorted by EvaluateSchedules)
            List<ScheduleDataCapture.PassingScheduleInfo> capturedSchedules = capture.ProcessAndCaptureSchedules(
                capture.Program.Schedules, SimParameters.SimEndSeconds, SimParameters.OutputDirectory, saveNormalProgramOutput: true);
            
            // Build capture object
            var finalOutputCapture = new ScheduleDataCapture.FinalOutputCapture
            {
                ScenarioName = scenarioName,
                SimInputFile = simInput,
                TaskInputFile = taskInput,
                ModelInputFile = modelInput,
                MaxNumScheds = SchedParameters.MaxNumScheds,
                NumSchedCropTo = SchedParameters.NumSchedCropTo,
                FinalSchedules = capturedSchedules
            };
            
            // Save capture JSON
            capture.SaveFinalOutputToFile(finalOutputCapture);
            
            Assert.Pass($"{scenarioName} final output capture completed successfully");
        }
        
        // [TestCase("AeolusTasks_3.json", "Aeolus_3Tasks", TestName = "Aeolus_3Tasks_Iterations")]
        // [TestCase("AeolusTasks_30.json", "Aeolus_30Tasks", TestName = "Aeolus_30Tasks_Iterations")]
        // [TestCase("AeolusTasks_300.json", "Aeolus_300Tasks", TestName = "Aeolus_300Tasks_Iterations")]
        // public void CaptureAeolus_Iterations(string taskInputFile, string scenarioName)
        // {
        //     var capture = new ScheduleDataCapture();
            
        //     // Use CurrentTestDir from inherited property (auto-detects class directory)
        //     string inputsDir = Path.Combine(CurrentTestDir, "Inputs");
            
        //     string simInput = Path.Combine(inputsDir, "AeolusSim_150sec_max10_cropTo5.json");
        //     string taskInput = Path.Combine(inputsDir, taskInputFile);
        //     string modelInput = Path.Combine(inputsDir, "DSAC_Static_ScriptedCS.json");
            
        //     capture.LoadScenarioToMainLoop(simInput, taskInput, modelInput);
            
        //     // Iteration-by-iteration capture (limited to 5 for testing)
        //     var scenarioCapture = capture.CaptureScheduleIterations(scenarioName, maxIterations: 5);
            
        //     // Save to file: {scenarioName}_full_trace.json
        //     capture.SaveCaptureToFile(scenarioCapture);
            
        //     Assert.Pass($"{scenarioName} iteration capture completed successfully");
        // }
    }
}

