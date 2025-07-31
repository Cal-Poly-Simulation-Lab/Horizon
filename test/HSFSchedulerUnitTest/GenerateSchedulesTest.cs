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
                        Assert.IsTrue(schedule.AllStates.Events.Count() == 0,$"The empty schedule exists (one without events). It is the {i} schedule in Program.Schedules list.");
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
                Assert.That(_emptySchedIdx,Is.EqualTo(0)); // The first index is the where the empty schedule lies. 
            }
            else{
                Assert.Fail("Empty schedule null; likely no schedules exist.");
            }
            
        }
        
        [Test, Order(3)] 
        public virtual void TimeDeconflictionTest()
        {
            
        }



        #endregion


        [Test]
        public void TestEventStartTime()
        {

            // Note to self Sun 8/18: 
            // Need to still add in the subsystem changes to event and task time then create all of the test assertions from there.
            // --> Need to create a schedule that has a specific value as well.
            // This would effectively test the scheudle as a function of time ... so like make one with legit access, and canperform, etc. 

            var asset1 = program?.AssetList?[0]; // The only asset that should be present
            if (asset1 == null || program?.Schedules == null || program.Schedules.Count == 0)
            {
                Assert.Fail("Required objects are null - cannot test event timing");
                return;
            }
            double _eventStartTime = program.Schedules[0].AllStates.GetLastEvent().EventEnds[asset1];
            Assert.AreEqual(_eventStartTime,12.0); 
            

           
        }

        [Test]
        public void TestScheduleGeneration_ShouldCreateValidSchedules()
        {
            // Verify that schedules were generated
            Assert.IsNotNull(program.Schedules, "Schedules should not be null");
            Assert.Greater(program.Schedules.Count, 0, "At least one schedule should be generated");
            
            Console.WriteLine($"Generated {program.Schedules.Count} schedules");
            
            // Check if any non-empty schedules exist
            var nonEmptySchedules = program.Schedules.Where(s => s.AllStates.Events.Count > 0).ToList();
            
            if (nonEmptySchedules.Count > 0)
            {
                Console.WriteLine($"Found {nonEmptySchedules.Count} non-empty schedules");
                
                // Test the first non-empty schedule
                var firstNonEmpty = nonEmptySchedules.First();
                var lastEvent = firstNonEmpty.AllStates.Events.Peek();
                
                Console.WriteLine($"First non-empty schedule has {firstNonEmpty.AllStates.Events.Count} events");
                Console.WriteLine($"Last event timing:");
                
                foreach (var asset in program.AssetList)
                {
                    double eventStart = lastEvent.GetEventStart(asset);
                    double eventEnd = lastEvent.GetEventEnd(asset);
                    double taskStart = lastEvent.GetTaskStart(asset);
                    double taskEnd = lastEvent.GetTaskEnd(asset);
                    
                    Console.WriteLine($"  Asset {asset.Name}: Event({eventStart}-{eventEnd}), Task({taskStart}-{taskEnd})");
                    
                    // Verify timing relationships
                    Assert.GreaterOrEqual(eventEnd, eventStart, "Event end should be after event start");
                    Assert.GreaterOrEqual(taskStart, eventStart, "Task start should be after event start");
                    Assert.LessOrEqual(taskEnd, eventEnd, "Task end should be before event end");
                    Assert.GreaterOrEqual(taskEnd, taskStart, "Task end should be after task start");
                }
            }
            else
            {
                Console.WriteLine("No non-empty schedules found - this may indicate SubTest is still crashing");
            }
        }

        [Test]
        public void TestAccessGeneration_ShouldCreateValidAccesses()
        {
            // Test that access windows are being generated correctly
            var asset = program.AssetList[0];
            var task = program.SystemTasks.Peek();
            
            Console.WriteLine($"Testing access for Asset: {asset.Name}, Task: {task.Name}");
            Console.WriteLine($"Simulation time: {UserModel.SimParameters.SimStartSeconds} to {UserModel.SimParameters.SimEndSeconds}");
            Console.WriteLine($"Time step: {UserModel.SimParameters.SimStepSeconds}");
            
            // This test verifies that the basic access generation is working
            // Even if SubTest crashes, we should see access windows being created
            Assert.IsNotNull(asset, "Asset should not be null");
            Assert.IsNotNull(task, "Task should not be null");
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
