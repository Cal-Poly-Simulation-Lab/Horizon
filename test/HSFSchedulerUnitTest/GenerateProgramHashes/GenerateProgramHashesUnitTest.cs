using NUnit.Framework;
using System.IO;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Unit test for generating hash summaries using RunScenarioWithHashSummaries.
    /// This test runs the toy example scenario two ways:
    /// 1. Direct GenerateSchedules call - generates hash summary after full run
    /// 2. MainSchedulingLoopHelper iteration-by-iteration - generates hash summary after each iteration
    /// All outputs are saved to test/output/ for comparison.
    /// </summary>
    [TestFixture]
    public class GenerateProgramHashesUnitTest : SchedulerUnitTest
    {
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            
            // Get the directory where this test file is located
            string testDir = GetClassSourceDirectory();
            string inputsDir = Path.Combine(testDir, "Inputs");
            
            // Set input file paths (using base class fields)
            SimInputFile = Path.Combine(inputsDir, "SimInput_TwoAssetImaging_ToyExample.json");
            TaskInputFile = Path.Combine(inputsDir, "TwoAsset_Imaging_Tasks.json");
            ModelInputFile = Path.Combine(inputsDir, "TwoAsset_Imaging_Model.json");
        }

        [Test]
        public void GenerateProgramHashes_ToyExample_GeneratesHashSummaries()
        {
            // Get the directory where this test file is located
            string testDir = GetClassSourceDirectory();
            string outputDir = Path.Combine(testDir, "output");
            
            // This test calls RunScenarioWithHashSummaries which:
            // 1. Loads the scenario
            // 2. Runs GenerateSchedules and saves hash summary to GenerateProgramHashes/output/direct_generateschedules/
            // 3. Runs MainSchedulingLoopHelper iteration-by-iteration and saves hash summary to GenerateProgramHashes/output/iteration_N/ for each iteration
            RunScenarioWithHashSummaries(SimInputFile!, TaskInputFile!, ModelInputFile!, outputDir);
        }
    }
}

