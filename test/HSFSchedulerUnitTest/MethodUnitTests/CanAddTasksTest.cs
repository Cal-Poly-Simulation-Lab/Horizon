using Microsoft.VisualStudio.TestPlatform.TestHost;
using Horizon;
using Utilities;
using NUnit.Framework.Internal;
using HSFScheduler;
using HSFSystem;
using MissionElements;
using System.Runtime.InteropServices.Marshalling;
using log4net;

namespace HSFSchedulerUnitTest
{
    [TestFixture]
    public class CanAddTasksTest
    {


        [SetUp] // Kind of like construction
        public void SetupOneSchedule()
        {

            // Declare all files used for this test
            SimInputFile = "SchedulerTestSimulationInput.json";
            TaskInputFile = "SchedulerTestTasks.json";
            ModelInputFile = "SchedulerTestModel.json";

            //Set up the StringWrite so we can see what the Horizon Program is doing from a Console.WriteLine() POV:
            // StringWriter stringWriter = new StringWriter();
            // Console.SetOut(stringWriter);

            // Load all files and create a new Horizon Program

            program = HoirzonLoadHelper(SimInputFile, TaskInputFile, ModelInputFile);

            // Now it is time to test the scheduler: 
            program.CreateSchedules();
            //double maxSched = program.EvaluateSchedules();


            //Console.WriteLine("Break");
            //Assert.AreEqual(program.System)
            //

            // Test if the Empty Schedule Exists and retrieve its location in the list ... 
            EmptyScheduleExists(); // Does this actually need to exist in the setup context?
                                   // it appears that this may be run given it's (current) status as a [Test] fixute as part of
                                   // of the abstract HSFSchedulerUnitTest class... It was called twice? 


        }

        [Test]
        public void TestEventStartTime()
        {

            // Note to self Sun 8/18: 
            // Need to still add in the subsystem changes to event and task time then create all of the test assertions from there.
            // --> Need to create a schedule that has a specific value as well.
            // This would effectively test the scheudle as a function of time ... so like make one with legit access, and canperform, etc. 

            var asset1 = program.AssetList[0]; // The only asset that should be present
            double _eventStartTime = program.Schedules[0].AllStates.GetLastEvent().EventEnds[asset1];
            Assert.AreEqual(_eventStartTime,12.0); 
            

           
        }

        // [Test]
        // public void TestEventEndTime()
        // {
           
        // }

        // [Test]
        // public void TestEventEndTime()
        // {
           
        // }



    }
}
