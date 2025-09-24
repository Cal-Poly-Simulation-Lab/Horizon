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
    public class GenerateSchedulesTest : SchedulerUnitTest
    {


        [SetUp] // Kind of like construction
        public void SetupGenerateSchedules()
        {

            // Declare all files used for this test
            SimInputFile = "SchedulerTestSimulationInput.json";
            TaskInputFile = "SchedulerTestTasks.json";
            ModelInputFile = "SchedulerTestModel.json";

            //Set up the StringWrite so we can see what the Horizon Program is doing from a Console.WriteLine() POV:
            // StringWriter stringWriter = new StringWriter();
            // Console.SetOut(stringWriter);

            // Load all files and create a new Horizon Program

            program = HorizonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            // Now it is time to test the scheduler: 
            program.CreateSchedules();
            //double maxSched = program.EvaluateSchedules();


            //Console.WriteLine("Break");
            //Assert.AreEqual(program.System)
            //

            // Test if the Empty Schedule Exists and retrieve its location in the list ... 
            //EmptyScheduleExists(); // Does this actually need to exist in the setup context?
            // it appears that this may be run given it's (current) status as a [Test] fixute as part of
            // of the abstract HSFSchedulerUnitTest class... It was called twice? 


        }

        #region InitializeEmptySchedule
        [Test, Order(1)]
        public virtual void EmptyScheduleExists() // This test should be ran on every schedule test
        {

            Console.WriteLine("This is the EmptyScheduleTest..."); // Not sure if Console.WriteLine works here
            if (program?.Schedules?.Count() > 0 == false)
            {
                _emptySchedIdx = null;
                Assert.Fail("The program has no schedules.");
            }
            else
            {
                for (int i = 0; i < (program?.Schedules?.Count() ?? 0); i++)
                {
                    var schedule = program?.Schedules?[i];
                    if (schedule == null) continue;
                    if (!(schedule.AllStates.Events.Count() > 0))
                    {
                        _emptySchedIdx = i; //Save the idx for the future...
                        Assert.IsTrue(schedule.AllStates.Events.Count() == 0, $"The empty schedule exists (one without events). It is the {i} schedule in Program.Schedules list.");
                        Console.WriteLine("EmptyScheduleSxists() NUnit Test passed! ~~ ");
                    }
                }
            }

        }

        [Test, Order(2)]
        public virtual void IsEmptyScheduleFirstSchedule()
        {
            if (_emptySchedIdx is not null)
            {
                Assert.That(_emptySchedIdx, Is.EqualTo(0)); // The first index is the where the empty schedule lies. 
            }
            else
            {
                Assert.Fail("Empty schedule null; likely no schedules exist.");
            }

        }
        #endregion
        
        



    }
}
