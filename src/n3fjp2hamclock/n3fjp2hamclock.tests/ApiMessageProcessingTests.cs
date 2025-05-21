using Moq;
using n3fjp2hamclock.helpers;
using System.Text;
using Xunit;

namespace n3fjp2hamclock.tests
{
    /// <summary>
    /// Test class for API Message processing
    /// Focuses on testing the buffer handling behavior in ApiClient
    /// </summary>
    public class ApiMessageProcessingTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<HamClockClient> _mockHamClockClient;

        public ApiMessageProcessingTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object);
            _mockHamClockClient.Setup(c => c.UpdateHamClocks(It.IsAny<string>(), It.IsAny<string>()))
                              .Returns(Task.CompletedTask);
        }
        /// <summary>
        /// Create a mock ApiClient extension for testing private methods
        /// </summary>
        private class TestableApiClient : ApiClient
        {
            private StringBuilder _testMessageBuffer = new StringBuilder();

            public TestableApiClient(string host, int port, string hamClockUris, ILogger logger)
                : base(host, port, hamClockUris, logger)
            {
            }

            /// <summary>
            /// Direct access to the ProcessBufferedCommands method for testing
            /// </summary>
            public async Task TestProcessBufferedCommands()
            {                // Use reflection to invoke the private method
                var methodInfo = typeof(ApiClient).GetMethod("ProcessBufferedCommands",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (methodInfo == null)
                {
                    throw new InvalidOperationException("ProcessBufferedCommands method not found in ApiClient");
                }

                var task = methodInfo.Invoke(this, null) as Task;
                if (task != null)
                {
                    await task;
                }
            }

            /// <summary>
            /// Set the message buffer directly for testing
            /// </summary>
            public void SetMessageBuffer(string content)
            {
                _testMessageBuffer.Clear();
                _testMessageBuffer.Append(content);

                // Use reflection to set the private field
                var fieldInfo = typeof(ApiClient).GetField("_messageBuffer",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(this, _testMessageBuffer);
                }
            }
            /// <summary>
            /// Get the current buffer content
            /// </summary>
            public string GetMessageBuffer()
            {
                // Use reflection to get the private field
                var fieldInfo = typeof(ApiClient).GetField("_messageBuffer",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo == null)
                {
                    return string.Empty;
                }

                var buffer = fieldInfo.GetValue(this) as StringBuilder;
                return buffer?.ToString() ?? string.Empty;
            }
            /// <summary>
            /// Get the internal HamClockClient for mocking
            /// </summary>
            public HamClockClient? GetHamClockClient()
            {
                // Use reflection to get the private field
                var fieldInfo = typeof(ApiClient).GetField("_hamClockClient",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return fieldInfo?.GetValue(this) as HamClockClient;
            }/// <summary>
             /// Replace the HamClockClient with a mock
             /// </summary>
            public void SetHamClockClient(HamClockClient client)
            {
                if (client == null)
                {
                    throw new ArgumentNullException(nameof(client));
                }

                // Use reflection to set the private field
                var fieldInfo = typeof(ApiClient).GetField("_hamClockClient",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo == null)
                {
                    throw new InvalidOperationException("_hamClockClient field not found in ApiClient");
                }

                fieldInfo.SetValue(this, client);
            }
        }

        [Fact]
        public async Task ProcessBufferedCommands_SingleCallTabEvent_ProcessedCorrectly()
        {
            // Arrange
            var apiClient = new TestableApiClient("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(_mockHamClockClient.Object);

            var callTabEvent = "<CMD><CALLTABEVENT><CALL>W1AW</CALL><LAT>41.7144</LAT><LON>-72.7289</LON></CMD>";

            // Act
            apiClient.SetMessageBuffer(callTabEvent);
            await apiClient.TestProcessBufferedCommands();

            // Assert
            _mockHamClockClient.Verify(
                client => client.UpdateHamClocks("41.7144", "-72.7289"),
                Times.Once);

            // Buffer should be empty after processing
            Assert.Equal(string.Empty, apiClient.GetMessageBuffer());
        }

        [Fact]
        public async Task ProcessBufferedCommands_MultipleCallTabEvents_ProcessesSeparately()
        {
            // Arrange
            var apiClient = new TestableApiClient("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(_mockHamClockClient.Object);

            var callTabEvents =
                "<CMD><CALLTABEVENT><CALL>W1AW</CALL><LAT>41.7144</LAT><LON>-72.7289</LON></CMD>" +
                "<CMD><CALLTABEVENT><CALL>K1JT</CALL><LAT>40.3573</LAT><LON>-74.6672</LON></CMD>" +
                "<CMD><CALLTABEVENT><CALL>N3FJP</CALL><LAT>39.5200</LAT><LON>-76.3200</LON></CMD>";

            // Act
            apiClient.SetMessageBuffer(callTabEvents);
            await apiClient.TestProcessBufferedCommands();

            // Assert
            _mockHamClockClient.Verify(
                client => client.UpdateHamClocks("41.7144", "-72.7289"),
                Times.Once);
            _mockHamClockClient.Verify(
                client => client.UpdateHamClocks("40.3573", "-74.6672"),
                Times.Once);
            _mockHamClockClient.Verify(
                client => client.UpdateHamClocks("39.5200", "-76.3200"),
                Times.Once);

            // Buffer should be empty after processing all commands
            Assert.Equal(string.Empty, apiClient.GetMessageBuffer());

            // Total calls should be exactly 3
            _mockHamClockClient.Verify(
                client => client.UpdateHamClocks(It.IsAny<string>(), It.IsAny<string>()),
                Times.Exactly(3));
        }

        [Fact]
        public async Task ProcessBufferedCommands_IncompleteCommand_RetainsInBuffer()
        {
            // Arrange
            var apiClient = new TestableApiClient("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(_mockHamClockClient.Object);

            // Set up incomplete command
            var incompleteCommand = "<CMD><CALLTABEVENT><CALL>W1AW</CALL><LAT>41.7144</LAT><LON>-72.7289";

            // Act
            apiClient.SetMessageBuffer(incompleteCommand);
            await apiClient.TestProcessBufferedCommands();

            // Assert
            // No calls should be made since command is incomplete
            _mockHamClockClient.Verify(
                client => client.UpdateHamClocks(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);

            // Buffer should still contain the incomplete command
            Assert.Equal(incompleteCommand, apiClient.GetMessageBuffer());
        }

        [Fact]
        public async Task ProcessBufferedCommands_CompleteThenIncomplete_ProcessesOnlyComplete()
        {
            // Arrange
            var apiClient = new TestableApiClient("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(_mockHamClockClient.Object);

            // Complete command followed by incomplete command
            var commands =
                "<CMD><CALLTABEVENT><CALL>W1AW</CALL><LAT>41.7144</LAT><LON>-72.7289</LON></CMD>" +
                "<CMD><CALLTABEVENT><CALL>K1JT</CALL><LAT>40.3573";

            // Act
            apiClient.SetMessageBuffer(commands);
            await apiClient.TestProcessBufferedCommands();

            // Assert
            // First command should be processed
            _mockHamClockClient.Verify(
                client => client.UpdateHamClocks("41.7144", "-72.7289"),
                Times.Once);

            // Only one call should be made total
            _mockHamClockClient.Verify(
                client => client.UpdateHamClocks(It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
            // Buffer should contain only the incomplete command
            string bufferContents = apiClient.GetMessageBuffer();
            Assert.Contains("<CALL>K1JT</CALL>", bufferContents);
            Assert.Contains("<LAT>40.3573", bufferContents);
        }
    }
}
