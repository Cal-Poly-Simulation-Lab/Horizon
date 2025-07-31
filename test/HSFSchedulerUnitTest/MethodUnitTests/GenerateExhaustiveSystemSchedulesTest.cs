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

namespace HSFSchedulerUnitTest
{
    [TestFixture]
    public class GenerateExhaustiveSystemSchedulesTest : SchedulerUnitTest
    {

        [SetUp]
        public void SetupGenerateExhaustiveSystemSchedules()
        {
            // Declare all files used for this test
            SimInputFile = "SchedulerTestSimulationInput.json";
            TaskInputFile = "SchedulerTestTasks.json";
            ModelInputFile = "SchedulerTestModel.json";

            // Load all files and create a new Horizon Program
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

        }

        [Test]
        public void TestGenerateExhaustiveSystemSchedules()
        {
            // Test that the method generates all possible schedules
        }
    }
}
