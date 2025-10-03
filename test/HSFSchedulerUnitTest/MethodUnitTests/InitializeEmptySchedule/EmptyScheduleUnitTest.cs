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
    public class EmptyScheduleUnitTest : SchedulerUnitTest
    {


        [SetUp] // Kind of like construction
        public void SetupGenerateSchedules()
        {

            // Declare all files used for this test
            SimInputFile = "InputFiles/SchedulerTestSimulationInput.json";
            TaskInputFile = "InputFiles/SchedulerTestTasks.json";
            ModelInputFile = "InputFiles/SchedulerTestModel.json";

            //Set up the StringWrite so we can see what the Horizon Program is doing from a Console.WriteLine() POV:
            // StringWriter stringWriter = new StringWriter();
            // Console.SetOut(stringWriter);

            // Load all files and create a new Horizon Program

            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            // Create (a copy of) the system and tasks for testing -- These are created by the program, under program.SimSystem and program.SystemTasks
            _testSimSystem = new SystemClass(program.AssetList, program.SubList, program.ConstraintsList, program.SystemUniverse);
            _testSystemTasks = new Stack<MissionElements.Task>(program.SystemTasks);

            // Initialize the (test-internal) _scheduleCombos stack, in preparation for the method call
            _scheduleCombos = new Stack<Stack<Access>>();

        }

        [Test, Order(1)]
        public void EmptyScheduleInitialized()
        {
            // Check if the system schedules list is empty before the empty schedule is initialized.
            if (_systemSchedules.Count() > 0)
            {
                Assert.Fail("AssertFail 1: The system schedules list is not empty before the empty schedule is initialized. This is not allowed.");
            }
            // Initialize the empty schedule.
            Scheduler.InitializeEmptySchedule(_systemSchedules, program.InitialSysState);

            // Check if the system schedules list is not empty after the empty schedule is initialized.
            if (_systemSchedules.Count() == 0)
            {
                Assert.Fail("AssertFail 2: The system schedules list is empty after the empty schedule is initialized. This is not allowed.");
            }

            // Check if the empty schedule is in the system schedules list.
            Assert.Multiple(() =>
            {
                Assert.IsTrue(_systemSchedules.Count() == 1, "Assert 1: The system schedules list should have one schedule after the empty schedule is initialized.");
                Assert.IsTrue(_systemSchedules[0].Name == "Empty Schedule", "Assert 2: The empty schedule should be named 'Empty Schedule'.");
                Assert.IsTrue(_systemSchedules[0].AllStates.Events.Count() == 0, "Assert 3: The empty schedule should have no events.");
            });
        }
        
        [Test, Order(2)]
        public void EmptyScheduleExistsInProgram()
        {
            var systemScheds = program.scheduler.systemSchedules;
            // Check if the system schedules list is empty before the empty schedule is initialized.
            if (systemScheds.Count() > 0)
            {
                Assert.Fail("AssertFail 1: The system schedules list is not empty before the empty schedule is initialized. This is not allowed.");
            }
            // Initialize the empty schedule.
            Scheduler.InitializeEmptySchedule(systemScheds,program.InitialSysState);

            // Check if the system schedules list is not empty after the empty schedule is initialized.
            if (systemScheds.Count() == 0)
            {
                Assert.Fail("AssertFail 2: The system schedules list is empty after the empty schedule is initialized. This is not allowed.");
            }

            // Check if the empty schedule is in the system schedules list.
            Assert.Multiple(() =>
            {
                Assert.IsTrue(systemScheds.Count() == 1,"Assert 1: The system schedules list should have one schedule after the empty schedule is initialized.");
                Assert.IsTrue(systemScheds[0].Name == "Empty Schedule","Assert 2: The empty schedule should be named 'Empty Schedule'.");
                Assert.IsTrue(systemScheds[0].AllStates.Events.Count() == 0,"Assert 3: The empty schedule should have no events.");
            });


        }

    }
}
