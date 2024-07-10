using HSFScheduler;
using HSFUniverse;
using MissionElements;
using Utilities;
using UserModel;

namespace HSFSchedulerUnitTest
{
    [TestFixture]
    public class SystemScheduleUnitTest
    {
        public string ClassName = "SystemScheduleUnitTest";
        public DynamicEOMS testDynamicEOMS {get; set;}
        public DynamicState testDynamicState {get; set;}
        public Asset testAsset {get; set;}
        public Target testTarget {get; set;}
        public MissionElements.Task testTask {get; set;}
        public Access testAccess {get; set;}
        public Stack<Access> testAccessStack {get; set;}
        public SystemState systemState {get; set;}
        public StateVariableKey<double> testSVKey {get; set;}
        public SystemState initialState {get; set;}
        public StateHistory stateHistory {get; set;}
        
        //public SystemSchedule systemSchedule {get; set; }


        [SetUp]
        public void Setup()
        {
            
            // Print to Console the test class name we are working on.
            Console.WriteLine(ClassName + "...");

            /*
            // Set up Asset --> Need Dynamic State, Asset Name, and Taskability
            string testAssetName = "testAsset"; // Asset Name
            //NOTE: EOMS ay be changed to allow for null EOMS; can change in future 
            DynamicEOMS testDynamicEOMS = new OrbitalEOMS(); // Set up built-in Orbital EOMS 
            DynamicState testDynamicState = new DynamicState(DynamicStateType.NULL_STATE, testDynamicEOMS,new Vector(1)); // Set Dynamic State
            bool isTaskable = true; // isTaskable
            Asset testAsset = new Asset(testDynamicState, testAssetName, isTaskable); // Instantiate test Asset

            // Next, we set up a Task so we can create an Access with an Asset and a Task.
            // First, we must set up a Target
            int testTargetValue = 5; // Give the Target a Value
            Target testTarget = new Target("testTarget","testType",testDynamicState,testTargetValue); // Can use the same dnymicstate for the test
            // Target testTarget = new Target("testTarget","testType",DynamicStateType.NULL_STATE,testTargetValue);
            int maxTimes = 1;
            MissionElements.Task testTask = new MissionElements.Task("Task1","testTaskType",testTarget,maxTimes);
            
            // Now we create a test Access and an Access Stack 
            Access testAccess = new Access(testAsset, testTask); //create a testAccess
            Stack<Access> testAccessStack = new Stack<Access>();
            testAccessStack.Push(testAccess); //create the testAccessStack

            // Before we can create a System Schedule, we need a SystemState and StateHistory...
            StateVariableKey<double> testKey = new StateVariableKey<double>("TestState");
            SystemState initialState = new SystemState(); // Start as null
            // Set up values in SystemState dictionaries (dictionaries of StateVariableKeys & Values) 
            double initialTime    = 0.0;
            double testStateValue = 0.0; 
            initialState.AddValue(testKey, initialTime, testStateValue);
            StateHistory history     = new StateHistory(initialState);

            // Now that we have an Access, Asset, Task, and StateHistory/SystemState we can create a System Schedule:
            double newEventStartTime = 0.0;
            // Call this in the test themselves !!!
            systemSchedule = new SystemSchedule(history, testAccessStack, newEventStartTime);

            // return systemSchedule;
            */

            // Set up Asset --> Need Dynamic State, Asset Name, and Taskability
            string testAssetName = "testAsset"; // Asset Name
            //NOTE: EOMS ay be changed to allow for null EOMS; can change in future 
            testDynamicEOMS = new OrbitalEOMS(); // Set up built-in Orbital EOMS 
            testDynamicState = new DynamicState(DynamicStateType.NULL_STATE, testDynamicEOMS,new Vector(1)); // Set Dynamic State
            bool isTaskable = true; // isTaskable
            testAsset = new Asset(testDynamicState, testAssetName, isTaskable); // Instantiate test Asset

            // Next, we set up a Task so we can create an Access with an Asset and a Task.
            // First, we must set up a Target
            int testTargetValue = 5; // Give the Target a Value
            testTarget = new Target("testTarget","testType",testDynamicState,testTargetValue); // Can use the same dnymicstate for the test
            // Target testTarget = new Target("testTarget","testType",DynamicStateType.NULL_STATE,testTargetValue);
            int maxTimes = 1;
            testTask = new MissionElements.Task("Task1","testTaskType",testTarget,maxTimes);
            
            // Now we create a test Access and an Access Stack 
            testAccess = new Access(testAsset, testTask); //create a testAccess
            testAccessStack = new Stack<Access>();
            testAccessStack.Push(testAccess); //create the testAccessStack

            // Before we can create a System Schedule, we need a SystemState and StateHistory...
            testSVKey = new StateVariableKey<double>("TestState");
            initialState = new SystemState(); // Start as null
            // Set up values in SystemState dictionaries (dictionaries of StateVariableKeys & Values) 
            double initialTime    = 0.0;
            double testStateValue = 0.0; 
            initialState.AddValue(testSVKey, initialTime, testStateValue);
            stateHistory     = new StateHistory(initialState);

        }

        [Test]
        public void TestConstructor()
        {

            // Write which test we are on to the Console
            Console.Write("TestConstructor() ... ");
            
            
            // Now that we have an Access, Asset, Task, and StateHistory/SystemState we can create a System Schedule:
            double newEventStartTime = 0.0;
            SystemSchedule systemSchedule = new SystemSchedule(stateHistory, testAccessStack, newEventStartTime);

            // This is the event on top of the stack.
            // Because this is the first time it is contructed, there will only be one event (given setup of null states and initial state)
            var topEvent = systemSchedule.AllStates.Events.Peek(); //This is the event on top of the stack.

            // Check if the TaskStart, TaskEnd, EventStart, EventEnd are 0 when exiting the constructor.
            Assert.That(topEvent.EventStarts[testAsset],Is.EqualTo(0)); //Assert.AreEqual(topEvent.EventStarts[testAsset],0);
            Assert.That(topEvent.EventEnds[testAsset],Is.EqualTo(12.0));
            Assert.That(topEvent.TaskStarts[testAsset],Is.EqualTo(0));
            Assert.That(topEvent.TaskEnds[testAsset],Is.EqualTo(0));
 
        }

        [Test]
        public void TestFundamentalTimeStep()
        {
            Console.Write("TestFundamentalTimestep() ... "); 
            double eventStartTime = 0.0; 

            // Make SystemSchedule with default state History (mostly null & with null initial state) and the access stack
            List<Stack<Access>> testAccessStackList = CreateAccessStackCases();
            List<SystemSchedule> sslist = new();
            foreach (Stack<Access> tas in testAccessStackList)
            {
                // Create the four different system schedules (because all using the same asset)
                // All using the same default StateHistory as well
                SystemSchedule systemSchedule = new SystemSchedule(stateHistory,tas,eventStartTime);
                var topEvent = systemSchedule.AllStates.Events.Peek(); 
                sslist.Add(systemSchedule);


            }
            

        }

        public List<Stack<Access>> CreateAccessStackCases()
        {
            /* So here we go through each Access in Stack<Access> newAccessStack and create a list of 
            List<SystemSchedule> potentialSystemSchdules
            Then, the Checker is called foreach potentialSchedule in potentialSystemSchedules ... */

            //Thus, create a list of Accesses ... 
            int numTestAccesses = 4; //How many accesses are we going to create?
            List<int> testTargetValues = [5,8,11,14];
            List<Target> testTargetList = new();
            List<MissionElements.Task> testTaskList = new(); int maxTimes = 1;

            // Though we set up Tasks so we can create an Access with an Asset and a Task.
            for (int i = 0; i < testTargetValues.Count(); i++)
            {
                 // So first, we must set up a Target
                testTargetList.Add(new Target("testTarget" + Convert.ToString(i+1),"testType",testDynamicState,testTargetValues[i]));
                // Then use th target to generate a Task
                testTaskList.Add(new MissionElements.Task("Task" + Convert.ToString(i+1),"testTaskType",testTargetList[i],maxTimes));
            
            }

                // Create four different access (cases):
                double stepEndTime = SimParameters.SimStepSeconds;

                // These cases will all only be a stack of one since we only have one asset here. 
                // Thus create a List for all of these to output in:
                List<Stack<Access>> testAccessStackList = new(); 
                
                // Case #1: Access start at event start, ends longer than Event End time
                Access access = new Access(testAsset, testTaskList[0]);
                access.AccessEnd = stepEndTime + 2.0; // Ends 2 seconds after event end time.
                Stack<Access> testAccessStack1 = new();
                testAccessStack1.Push(access);
                testAccessStackList.Add(testAccessStack1);

                // Case #2: Access Starts at Event Start; Ends before Event End
                access = new Access(testAsset, testTaskList[1]);
                access.AccessEnd = stepEndTime - 2.0; // Ends 2 before after event end time.
                Stack<Access> testAccessStack2 = new(); 
                testAccessStack2.Push(access);
                testAccessStackList.Add(testAccessStack2);

                // Case #3: Access Starts at After Event Start; Ends before Event End
                access = new Access(testAsset, testTaskList[2]);
                access.AccessStart = 2.0; 
                access.AccessEnd = stepEndTime - 2.0; // Ends 2 seconds before event end time. 
                Stack<Access> testAccessStack3 = new();
                testAccessStack3.Push(access);
                testAccessStackList.Add(testAccessStack3);

                // Case #4: Access Starts after Event Start; Ends after Event End
                access = new Access(testAsset, testTaskList[3]);
                access.AccessStart = 2.0; 
                access.AccessEnd = stepEndTime + 2.0; // Ends 2 seconds after event end time. 
                Stack<Access> testAccessStack4 = new();
                testAccessStack4.Push(access);
                testAccessStackList.Add(testAccessStack4);
                
                // Return out
                return testAccessStackList;


            // Now need to use Scheduler.CanAddTasks() to check if the schedules work (or does SystemSchedule do this)


            // Then Scheudler.Checker() to add to list of schedules (maybe in seperate test/this is main Scheduler Unit Test scope). 

        }
    }
};