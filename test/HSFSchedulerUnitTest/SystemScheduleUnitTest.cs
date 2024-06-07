using HSFScheduler;
using HSFUniverse;
using MissionElements;
using Utilities;

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
            Assert.That(topEvent.EventEnds[testAsset],Is.EqualTo(0));
            Assert.That(topEvent.TaskStarts[testAsset],Is.EqualTo(0));
            Assert.That(topEvent.TaskEnds[testAsset],Is.EqualTo(0));
 
        }

        public void TestFundamentalTimeStep()
        {
            Console.Write("TestFundamentalTimestep() ... "); 

            /* So here we go through each Access in Stack<Access> newAccessStack and create a list of 
            List<SystemSchedule> potentialSystemSchdules
            Then, the Checker is called foreach potentialSchedule in potentialSystemSchedules ... */

            //Thus, create a list of Accesses ... 
            int numTestAccesses = 4; //How many accesses are we going to create?
            List<int> testTargetValues = [5,8,11,14];
            List<Target> testTargetList = new();
            List<MissionElements.Task> testTaskList = new(); int maxTimes = 1;
            Stack<Access> testAccessStack = new();

            // Though we set up Tasks so we can create an Access with an Asset and a Task.
            for (int i = 0; i < testTargetValues.Count(); i++)
            {
                 // So First, we must set up a Target and task with that target
                testTargetList.Add(new Target("testTarget" + Convert.ToString(i+1),"testType",testDynamicState,testTargetValues[i]));
                testTaskList.Add(new MissionElements.Task("Task" + Convert.ToString(i+1),"testTaskType",testTargetList[i],maxTimes));
                
                // Now we create a test Access and push it on top of the stack 
                testAccessStack.Push(new Access(testAsset, testTask)); //create the testAccessStack

                //Can change Access Times here ....
            }



        }
    }
};