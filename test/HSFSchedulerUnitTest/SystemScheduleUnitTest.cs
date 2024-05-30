using HSFScheduler;
using HSFUniverse;
using MissionElements;
using Utilities;

namespace HSFSchedulerUnitTest
{
    [TestFixture]
    public class SystemScheduleUnitTest
    {
        [SetUp]
        public void Setup()
        {
            // Set up Asset --> Need Dynamic State, Asset Name, and Taskability
            string testAssetName = "testAsset"; // Asset Name
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
            SystemSchedule systemSchedule = new SystemSchedule(history, testAccessStack, newEventStartTime);

            Console.WriteLine("Break");
            // return systemSchedule;

        }

        [Test]
        public void TestConstructor()
        {

        
            Console.WriteLine("this is not the main one.");
            Assert.Pass();
        }
    }
};