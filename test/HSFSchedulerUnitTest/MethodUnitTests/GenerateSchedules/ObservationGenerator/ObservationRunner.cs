using NUnit.Framework;

namespace HSFSchedulerUnitTest
{
    /// <summary>
    /// Simple wrapper to run ObservationDataCapture
    /// This is NOT part of normal test suite - only run manually
    /// </summary>
    [TestFixture, Category("ObservationRunner"), Explicit]
    public class ObservationRunner
    {
        [Test]
        public void RunObservationCapture()
        {
            // Call the static entry point
            ObservationDataCapture.Main_ObservationCapture();
        }
    }
}

