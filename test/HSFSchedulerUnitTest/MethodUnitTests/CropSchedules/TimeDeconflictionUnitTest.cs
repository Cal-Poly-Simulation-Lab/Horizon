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
    public class CropSchedulesUnitTest : SchedulerUnitTest
    {
        private string testDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../MethodUnitTests/GenerateExhaustiveSystemSchedulesTest"));
        private string? DefaultSimInputFile;
        private string? DefaultModelInputFile;
        private string? DefaultTaskInputFile;
        private SystemClass? testSystem;
        private Stack<MissionElements.Task>? testTasks;
        private Stack<Stack<Access>>? scheduleCombos;

        [SetUp]
        public void SetupDefaults()
        {
            // Set up the test directory for the input files:

            // // Use the existing test files for the 1 asset, 3 tasks scenario
            SimInputFile = "SchedulerTestSimulationInput.json"; // Using default HSFSchedulerUnitTest Simulation Input File. 
            TaskInputFile = Path.Combine(testDir, "ThreeTaskTestInput.json");
            ModelInputFile = Path.Combine(testDir, "OneAssetTestModel.json");

            // Load the program to get the system and tasks & Create the system and tasks for testing      
            BuildProgram();
        }

        private void BuildProgram()
        {
            // Load the program to get the system and tasks
            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);


            // Create the system and tasks for testing
            testSystem = new SystemClass(program.AssetList, program.SubList, program.ConstraintsList, program.SystemUniverse);
            testTasks = new Stack<MissionElements.Task>(program.SystemTasks);
            scheduleCombos = new Stack<Stack<Access>>();
        }

        [Test]
        public void Test1()
        {

        }

        
    }
}