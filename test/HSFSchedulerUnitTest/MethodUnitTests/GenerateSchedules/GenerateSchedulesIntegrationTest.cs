using Horizon;
using HSFScheduler;
using MissionElements;
using UserModel;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Integration tests for Scheduler.GenerateSchedules() - the complete scheduling loop
    /// Tests iteration-by-iteration using MainSchedulingLoopHelper and verifies against observed baseline data
    /// </summary>
    [TestFixture]
    public class GenerateSchedulesIntegrationTest : SchedulerUnitTest
    {
        private string CurrentTestDir = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "MethodUnitTests", "GenerateSchedules"));

        protected override string SimInputFile { get; set; }
        protected override string TaskInputFile { get; set; }
        protected override string ModelInputFile { get; set; }

        [SetUp]
        public void SetupDefaults()
        {
            // Reset Scheduler static state
            Scheduler.SchedulerStep = -1;
            Scheduler._schedID = 0;
        }

        [TearDown]
        public void ResetSchedulerAttributes()
        {
            SchedulerStep = -1;
            CurrentTime = SimParameters.SimStartSeconds;
            NextTime = SimParameters.SimStartSeconds + SimParameters.SimStepSeconds;
            _schedID = 0;
            _SchedulesGenerated = 0;
            _SchedulesCarriedOver = 0;
            _SchedulesCropped = 0;
            _emptySchedule = null;

            _systemSchedules.Clear();
            _canPregenAccess = false;
            _scheduleCombos.Clear();
            _preGeneratedAccesses = null;
            _potentialSystemSchedules.Clear();
            _systemCanPerformList.Clear();
            _ScheduleEvaluator = null;

            program = new Horizon.Program();
            _testSimSystem = null;
            _testSystemTasks.Clear();
            _testInitialSysState = new SystemState();
        }

        #region Helper Methods

        private ObservationDataCapture.ScenarioData LoadExpectedData(string scenarioName)
        {
            string jsonPath = Path.Combine(CurrentTestDir, "ExpectedResults", $"{scenarioName}.json");
            
            if (!File.Exists(jsonPath))
            {
                Assert.Fail($"Expected results file not found: {jsonPath}");
            }
            
            string jsonString = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ObservationDataCapture.ScenarioData>(jsonString, options);
        }

        private void AssertInputFilesMatch(ObservationDataCapture.ScenarioData expected)
        {
            Assert.That(SimInputFile, Is.EqualTo(expected.SimInputFile), 
                "SimInputFile mismatch - test is not using the same inputs as expected results");
            Assert.That(TaskInputFile, Is.EqualTo(expected.TaskInputFile), 
                "TaskInputFile mismatch - test is not using the same inputs as expected results");
            Assert.That(ModelInputFile, Is.EqualTo(expected.ModelInputFile), 
                "ModelInputFile mismatch - test is not using the same inputs as expected results");
        }

        #endregion

        #region Step-by-Step Integration Tests

        // /// <summary>
        // /// Test each iteration step-by-step using MainSchedulingLoopHelper
        // /// Verifies schedule count, IDs, and values at each iteration
        // /// </summary>
        // [TestCase("OneAsset_OneTask", "OneTaskTestFile.json", "OneAssetTestModel_AlwaysTrue.json", TestName = "1A1T_StepByStep")]
        // [TestCase("OneAsset_ThreeTasks", "ThreeTaskTestFile.json", "OneAssetTestModel_AlwaysTrue.json", TestName = "1A3T_StepByStep")]
        // [TestCase("TwoAssets_OneTask", "OneTaskTestFile.json", "TwoAssetTestModel_AlwaysTrue.json", TestName = "2A1T_StepByStep")]
        // [TestCase("TwoAssets_ThreeTasks", "ThreeTaskTestFile.json", "TwoAssetTestModel_AlwaysTrue.json", TestName = "2A3T_StepByStep")]
        // public void GenerateSchedules_StepByStep_VerifyEachIteration(string scenarioName, string taskFile, string modelFile)
        // {
        //     // Load expected results
        //     var expected = LoadExpectedData(scenarioName);
            
        //     // Setup input files
        //     string inputsDir = Path.Combine(CurrentTestDir, "Inputs");
        //     SimInputFile = Path.Combine(inputsDir, "SimInput_MaxSched100_CropTo50.json");
        //     TaskInputFile = Path.Combine(inputsDir, taskFile);
        //     ModelInputFile = Path.Combine(inputsDir, modelFile);
            
        //     // Verify we're using the same inputs as expected results
        //     AssertInputFilesMatch(expected);
            
        //     // Build program
        //     program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);
            
        //     double simStart = SimParameters.SimStartSeconds;
        //     double simStep = SimParameters.SimStepSeconds;
        //     double simEnd = SimParameters.SimEndSeconds;
            
        //     // Initialize
        //     Scheduler.InitializeEmptySchedule(_systemSchedules, _testInitialSysState);
        //     SchedulerUnitTest._emptySchedule = Scheduler.emptySchedule;
        //     _scheduleCombos = Scheduler.GenerateExhaustiveSystemSchedules(_testSimSystem, _testSystemTasks, _scheduleCombos, simStart, simEnd);
            
        //     Assert.Multiple(() =>
        //     {
        //         // Verify each iteration
        //         for (int i = 0; i < expected.Iterations.Count; i++)
        //         {
        //             double currentTime = i * simStep;
        //             var expectedIter = expected.Iterations[i];
                    
        //             // Run one iteration
        //             _systemSchedules = MainSchedulingLoopHelper(_systemSchedules, _scheduleCombos, _testSimSystem, 
        //                                                          _ScheduleEvaluator, _emptySchedule, currentTime, simStep, 1);
                    
        //             // i. Assert schedule count
        //             Assert.That(_systemSchedules.Count, Is.EqualTo(expectedIter.CountAfter), 
        //                 $"[DEBUG] i={i}: Schedule count | Expected: {expectedIter.CountAfter} | Found: {_systemSchedules.Count}");
                    
        //             // ii. Assert each schedule value
        //             foreach (var expectedSched in expectedIter.Schedules)
        //             {
        //                 var actualSched = _systemSchedules.FirstOrDefault(s => s._scheduleID == expectedSched.ID);
                        
        //                 // Get Asset→Task info for debugging
        //                 string assetTaskInfo = "";
        //                 if (actualSched != null && expectedSched.AssetTaskPairs != null && expectedSched.AssetTaskPairs.Count > 0)
        //                 {
        //                     assetTaskInfo = $" | Tasks: [{string.Join(", ", expectedSched.AssetTaskPairs)}]";
        //                 }
                        
        //                 Assert.That(actualSched, Is.Not.Null, 
        //                     $"[DEBUG] i={i}: Schedule missing | ID: {expectedSched.ID} | Expected Value: {expectedSched.Value} | Events: {expectedSched.Events}{assetTaskInfo}");
                        
        //                 Assert.That(actualSched.ScheduleValue, Is.EqualTo(expectedSched.Value).Within(0.001), 
        //                     $"[DEBUG] i={i}: Value mismatch | ID: {expectedSched.ID} | Expected: {expectedSched.Value} | Found: {actualSched.ScheduleValue} | Events: {actualSched.AllStates.Events.Count}{assetTaskInfo}");
        //             }
                    
        //             // iii. Assert exact ID↔Value combinations (no extras, no missing)
        //             var expectedIDs = expectedIter.Schedules.Select(s => s.ID).OrderBy(id => id).ToList();
        //             var actualIDs = _systemSchedules.Select(s => s._scheduleID).OrderBy(id => id).ToList();
        //             Assert.That(actualIDs, Is.EqualTo(expectedIDs), 
        //                 $"[DEBUG] i={i}: Schedule ID mismatch | Expected IDs: [{string.Join(", ", expectedIDs)}] | Found IDs: [{string.Join(", ", actualIDs)}]");
        //         }
                
        //         // Verify final crop
        //         _systemSchedules = Scheduler.CropToMaxSchedules(_systemSchedules, _emptySchedule, _ScheduleEvaluator);
                
        //         Assert.That(_systemSchedules.Count, Is.EqualTo(expected.FinalCrop.CountAfter), 
        //             $"[DEBUG] Final Crop: Schedule count | Expected: {expected.FinalCrop.CountAfter} | Found: {_systemSchedules.Count}");
                
        //         foreach (var expectedSched in expected.FinalCrop.Schedules)
        //         {
        //             var actualSched = _systemSchedules.FirstOrDefault(s => s._scheduleID == expectedSched.ID);
                    
        //             // Get Asset→Task info for debugging
        //             string assetTaskInfo = "";
        //             if (actualSched != null && expectedSched.AssetTaskPairs != null && expectedSched.AssetTaskPairs.Count > 0)
        //             {
        //                 assetTaskInfo = $" | Tasks: [{string.Join(", ", expectedSched.AssetTaskPairs)}]";
        //             }
                    
        //             Assert.That(actualSched, Is.Not.Null, 
        //                 $"[DEBUG] Final: Schedule missing | ID: {expectedSched.ID} | Expected Value: {expectedSched.Value} | Events: {expectedSched.Events}{assetTaskInfo}");
                    
        //             Assert.That(actualSched.ScheduleValue, Is.EqualTo(expectedSched.Value).Within(0.001), 
        //                 $"[DEBUG] Final: Value mismatch | ID: {expectedSched.ID} | Expected: {expectedSched.Value} | Found: {actualSched.ScheduleValue} | Events: {actualSched.AllStates.Events.Count}{assetTaskInfo}");
        //         }
                
        //         var finalExpectedIDs = expected.FinalCrop.Schedules.Select(s => s.ID).OrderBy(id => id).ToList();
        //         var finalActualIDs = _systemSchedules.Select(s => s._scheduleID).OrderBy(id => id).ToList();
        //         Assert.That(finalActualIDs, Is.EqualTo(finalExpectedIDs), 
        //             $"[DEBUG] Final: Schedule ID mismatch | Expected IDs: [{string.Join(", ", finalExpectedIDs)}] | Found IDs: [{string.Join(", ", finalActualIDs)}]");
        //     });
            
        //     Console.WriteLine($"✅ {scenarioName}: All iterations and final crop verified");
        // }

        // #endregion

        // #region Full GenerateSchedules() Call Tests

        // /// <summary>
        // /// Test calling GenerateSchedules() directly and verifying final output only
        // /// Should match the final crop result from step-by-step test
        // /// </summary>
        // [TestCase("OneAsset_OneTask", "OneTaskTestFile.json", "OneAssetTestModel_AlwaysTrue.json", TestName = "1A1T_FullCall")]
        // [TestCase("OneAsset_ThreeTasks", "ThreeTaskTestFile.json", "OneAssetTestModel_AlwaysTrue.json", TestName = "1A3T_FullCall")]
        // [TestCase("TwoAssets_OneTask", "OneTaskTestFile.json", "TwoAssetTestModel_AlwaysTrue.json", TestName = "2A1T_FullCall")]
        // [TestCase("TwoAssets_ThreeTasks", "ThreeTaskTestFile.json", "TwoAssetTestModel_AlwaysTrue.json", TestName = "2A3T_FullCall")]
        // public void GenerateSchedules_FullCall_VerifyFinalOutput(string scenarioName, string taskFile, string modelFile)
        // {
        //     // Load expected results
        //     var expected = LoadExpectedData(scenarioName);
            
        //     // Setup input files
        //     string inputsDir = Path.Combine(CurrentTestDir, "Inputs");
        //     SimInputFile = Path.Combine(inputsDir, "SimInput_MaxSched100_CropTo50.json");
        //     TaskInputFile = Path.Combine(inputsDir, taskFile);
        //     ModelInputFile = Path.Combine(inputsDir, modelFile);
            
        //     // Verify we're using the same inputs as expected results
        //     AssertInputFilesMatch(expected);
            
        //     // Build program
        //     program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);
            
        //     // Call GenerateSchedules directly
        //     var scheduler = new Scheduler(program.SchedEvaluator);
        //     var finalSchedules = scheduler.GenerateSchedules(_testSimSystem, _testSystemTasks, _testInitialSysState);
            
        //     Assert.Multiple(() =>
        //     {
        //         // Assert final count
        //         Assert.That(finalSchedules.Count, Is.EqualTo(expected.FinalCrop.CountAfter), 
        //             $"[DEBUG] GenerateSchedules() Final: Schedule count | Expected: {expected.FinalCrop.CountAfter} | Found: {finalSchedules.Count}");
                
        //         // Assert each schedule exists with correct value
        //         foreach (var expectedSched in expected.FinalCrop.Schedules)
        //         {
        //             var actualSched = finalSchedules.FirstOrDefault(s => s._scheduleID == expectedSched.ID);
                    
        //             // Get Asset→Task info for debugging
        //             string assetTaskInfo = "";
        //             if (actualSched != null && expectedSched.AssetTaskPairs != null && expectedSched.AssetTaskPairs.Count > 0)
        //             {
        //                 assetTaskInfo = $" | Tasks: [{string.Join(", ", expectedSched.AssetTaskPairs)}]";
        //             }
                    
        //             Assert.That(actualSched, Is.Not.Null, 
        //                 $"[DEBUG] GenerateSchedules() Final: Schedule missing | ID: {expectedSched.ID} | Expected Value: {expectedSched.Value} | Events: {expectedSched.Events}{assetTaskInfo}");
                    
        //             Assert.That(actualSched.ScheduleValue, Is.EqualTo(expectedSched.Value).Within(0.001), 
        //                 $"[DEBUG] GenerateSchedules() Final: Value mismatch | ID: {expectedSched.ID} | Expected: {expectedSched.Value} | Found: {actualSched.ScheduleValue} | Events: {actualSched.AllStates.Events.Count}{assetTaskInfo}");
        //         }
                
        //         // Assert exact ID match
        //         var expectedIDs = expected.FinalCrop.Schedules.Select(s => s.ID).OrderBy(id => id).ToList();
        //         var actualIDs = finalSchedules.Select(s => s._scheduleID).OrderBy(id => id).ToList();
        //         Assert.That(actualIDs, Is.EqualTo(expectedIDs), 
        //             $"[DEBUG] GenerateSchedules() Final: Schedule ID mismatch | Expected IDs: [{string.Join(", ", expectedIDs)}] | Found IDs: [{string.Join(", ", actualIDs)}]");
        //     });
            
        //     Console.WriteLine($"✅ {scenarioName}: GenerateSchedules() final output verified");
        // }

        #endregion
    }
}
