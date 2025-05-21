using n3fjp2hamclock.helpers;
using Xunit;

namespace n3fjp2hamclock.tests
{
    public class BaseTests
    {
        /// <summary>
        /// Basic test to verify the test setup is working
        /// </summary>
        [Fact]
        public void TestFrameworkSetup_IsValid()
        {
            // Just a basic test to verify the test framework setup
            Assert.True(true);
        }

        /// <summary>
        /// Verify we can create a logger
        /// </summary>
        [Fact] 
        public void TestLogger_CanCreateAndUse()
        {
            var logger = new TestLogger();
            logger.Log("Test message", LogLevel.Info);
            
            Assert.True(logger.ContainsLog("Test message", LogLevel.Info));
        }
    }
}
