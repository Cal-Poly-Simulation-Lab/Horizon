using NUnit.Framework;
using System.IO;
using System.Linq;

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

        [Test]
        public void GenerateProgramHashes_ToyExample_HashDataMatchesBaseline()
        {
            // Get the directory where this test file is located
            string testDir = GetClassSourceDirectory();
            string outputDir = Path.Combine(testDir, "output");
            string baselineDir = Path.Combine(testDir, "PreRefactorOutput");
            
            // Find the baseline directory (should be last_run_*)
            var baselineDirs = Directory.GetDirectories(baselineDir, "last_run_*");
            Assert.That(baselineDirs.Length, Is.GreaterThan(0), 
                $"No baseline directory found in {baselineDir}. Expected a directory matching 'last_run_*'");
            
            string baselineHashDataDir = Path.Combine(baselineDirs[0], "HashData");
            Assert.That(Directory.Exists(baselineHashDataDir), Is.True, 
                $"Baseline HashData directory not found: {baselineHashDataDir}");
            
            // Run the hash generation
            RunScenarioWithHashSummaries(SimInputFile!, TaskInputFile!, ModelInputFile!, outputDir);
            
            // Find the generated directory (should be last_run_*)
            var generatedDirs = Directory.GetDirectories(outputDir, "last_run_*");
            Assert.That(generatedDirs.Length, Is.GreaterThan(0), 
                $"No generated directory found in {outputDir}. Expected a directory matching 'last_run_*'");
            
            string generatedHashDataDir = Path.Combine(generatedDirs[0], "direct_generateschedules", "HashData");
            Assert.That(Directory.Exists(generatedHashDataDir), Is.True, 
                $"Generated HashData directory not found: {generatedHashDataDir}");
            
            // List of hash data files to compare
            string[] hashDataFiles = {
                "FullScheduleHashHistory.txt",
                "FullStateHistoryHash.txt",
                "FullScheduleStateHashHistory.txt",
                "scheduleHashBlockchainSummary.txt"
            };
            
            // Compare each file using direct C# file comparison
            foreach (string fileName in hashDataFiles)
            {
                string baselineFile = Path.Combine(baselineHashDataDir, fileName);
                string generatedFile = Path.Combine(generatedHashDataDir, fileName);
                
                Assert.That(File.Exists(baselineFile), Is.True, 
                    $"Baseline file not found: {baselineFile}");
                Assert.That(File.Exists(generatedFile), Is.True, 
                    $"Generated file not found: {generatedFile}");
                
                // Compare files directly using C# (cross-platform, no external dependencies)
                string diffMessage = CompareFiles(baselineFile, generatedFile);
                
                // Assert that files are identical (empty message means files are identical)
                Assert.That(diffMessage, Is.Empty, 
                    $"Files differ: {fileName}\n" +
                    $"Baseline: {baselineFile}\n" +
                    $"Generated: {generatedFile}\n" +
                    $"{diffMessage}");
            }
        }
        
        /// <summary>
        /// Compares two files byte-by-byte and returns a detailed diff message if they differ.
        /// Returns empty string if files are identical.
        /// Cross-platform C# implementation - no external dependencies.
        /// </summary>
        private string CompareFiles(string file1, string file2)
        {
            byte[] bytes1 = File.ReadAllBytes(file1);
            byte[] bytes2 = File.ReadAllBytes(file2);
            
            // First check: byte-by-byte comparison (most accurate)
            if (bytes1.SequenceEqual(bytes2))
            {
                return string.Empty;
            }
            
            // Files differ - provide detailed information
            var diffInfo = new System.Text.StringBuilder();
            diffInfo.AppendLine($"Files differ:");
            diffInfo.AppendLine($"  Baseline size: {bytes1.Length} bytes");
            diffInfo.AppendLine($"  Generated size: {bytes2.Length} bytes");
            
            // If files are text-based (hash files are), show line-by-line comparison
            try
            {
                string[] lines1 = File.ReadAllLines(file1);
                string[] lines2 = File.ReadAllLines(file2);
                
                diffInfo.AppendLine($"  Baseline lines: {lines1.Length}");
                diffInfo.AppendLine($"  Generated lines: {lines2.Length}");
                
                // Find first differing line
                int minLines = Math.Min(lines1.Length, lines2.Length);
                for (int i = 0; i < minLines; i++)
                {
                    if (lines1[i] != lines2[i])
                    {
                        diffInfo.AppendLine($"  First difference at line {i + 1}:");
                        diffInfo.AppendLine($"    Baseline:   {lines1[i]}");
                        diffInfo.AppendLine($"    Generated: {lines2[i]}");
                        break;
                    }
                }
                
                if (lines1.Length != lines2.Length)
                {
                    diffInfo.AppendLine($"  Line count mismatch: baseline has {lines1.Length} lines, generated has {lines2.Length} lines");
                }
            }
            catch
            {
                // If file can't be read as text, just report byte difference
                // Find first differing byte
                int minLength = Math.Min(bytes1.Length, bytes2.Length);
                for (int i = 0; i < minLength; i++)
                {
                    if (bytes1[i] != bytes2[i])
                    {
                        diffInfo.AppendLine($"  First difference at byte {i}: baseline=0x{bytes1[i]:X2}, generated=0x{bytes2[i]:X2}");
                        break;
                    }
                }
            }
            
            return diffInfo.ToString();
        }
    }
}

