using HSFScheduler;
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

        }

        [Test]
        public void TestConstructor()
        {
            StateVariableKey<double> testKey = new StateVariableKey<double>("TestState");
            SystemState initialState = new SystemState(); // Start as null
            // Set up values in SystemState dictionaries (dictionaries of StateVariableKeys & Values) 
            double initialTime = 0.0;
            double testStateValue   = 0.0; 
            initialState.AddValue(testKey, initialTime, testStateValue);
        
            StateHistory history     = new StateHistory(initialState);
            Stack<Access> access     = 
            double newEventStartTime = 

            SystemSchedule systemShcedule = new SystemSchedule(history, access, newEventStartTime);

            Assert.Pass();
        }
    }
};