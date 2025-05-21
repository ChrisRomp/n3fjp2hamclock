using System.Text;
using Moq;
using n3fjp2hamclock.helpers;
using Xunit;

namespace n3fjp2hamclock.tests
{
    public class ApiClientTests
    {
        private readonly Mock<ILogger> _mockLogger;
        
        public ApiClientTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        /// <summary>
        /// Helper method to create a mock socket stream for testing
        /// </summary>
        private MemoryStream CreateMockStream(string[] commands)
        {
            // Create a mock stream to simulate incoming data
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
            
            foreach (var command in commands)
            {
                writer.Write(command);
            }
            
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        [Fact]
        public async Task ProcessBufferedCommands_SingleCallTabEvent_ProcessedCorrectly()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object);
            
            // Create test data
            var callTabEvent = "<CMD><CALLTABEVENT><CALL>W1AW</CALL><LAT>41.7144</LAT><LON>-72.7289</LON></CMD>";
            // Using reflection to test private method
            var apiClient = new ApiClientForTesting("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(mockHamClockClient.Object);
            
            // Act
            apiClient.SetMessageBuffer(callTabEvent);
            await apiClient.TestProcessBufferedCommands();
            
            // Assert
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks("41.7144", "-72.7289"), 
                Times.Once);
        }

        [Fact]
        public async Task ProcessBufferedCommands_MultipleCallTabEvents_ProcessedIndividually()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object);
            // Create test data with multiple call tab events
            var callTabEvents = 
                "<CMD><CALLTABEVENT><CALL>W1AW</CALL><LAT>41.7144</LAT><LON>-72.7289</LON></CMD>" +
                "<CMD><CALLTABEVENT><CALL>K1JT</CALL><LAT>40.3573</LAT><LON>-74.6672</LON></CMD>" +
                "<CMD><CALLTABEVENT><CALL>N3FJP</CALL><LAT>39.5200</LAT><LON>-76.3200</LON></CMD>";
            
            var apiClient = new ApiClientForTesting("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(mockHamClockClient.Object);
            
            // Act
            apiClient.SetMessageBuffer(callTabEvents);
            await apiClient.TestProcessBufferedCommands();
            
            // Assert
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks("41.7144", "-72.7289"), 
                Times.Once);
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks("40.3573", "-74.6672"), 
                Times.Once);
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks("39.5200", "-76.3200"), 
                Times.Once);
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks(It.IsAny<string>(), It.IsAny<string>()), 
                Times.Exactly(3));
        }
        
        [Fact]
        public async Task ProcessBufferedCommands_IncompleteCommand_RetainedInBuffer()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object);
            // Create test data with incomplete command
            var incompleteCommand = "<CMD><CALLTABEVENT><CALL>W1AW</CALL><LAT>41.7144</LAT><LON>-72.7289";
            
            var apiClient = new ApiClientForTesting("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(mockHamClockClient.Object);
            
            // Act
            apiClient.SetMessageBuffer(incompleteCommand);
            await apiClient.TestProcessBufferedCommands();
            
            // Assert
            // No calls should be made since the command is incomplete
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks(It.IsAny<string>(), It.IsAny<string>()), 
                Times.Never);
                
            // Buffer should still contain the incomplete command
            Assert.Equal(incompleteCommand, apiClient.GetMessageBuffer());
        }
        
        [Fact]
        public async Task ProcessBufferedCommands_CompleteThenIncomplete_ProcessesOnlyComplete()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object);
            
            // Create test data with complete command followed by incomplete command
            var commands = 
                "<CMD><CALLTABEVENT><CALL>W1AW</CALL><LAT>41.7144</LAT><LON>-72.7289</LON></CMD>" +
                "<CMD><CALLTABEVENT><CALL>K1JT</CALL><LAT>40.3573";
            
            var apiClient = new ApiClientForTesting("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(mockHamClockClient.Object);
            
            // Act
            apiClient.SetMessageBuffer(commands);
            await apiClient.TestProcessBufferedCommands();
            
            // Assert
            // First complete command should be processed
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks("41.7144", "-72.7289"), 
                Times.Once);
            
            // Buffer should contain only the incomplete command
            Assert.Contains("<CALL>K1JT</CALL>", apiClient.GetMessageBuffer());
            Assert.Contains("<LAT>40.3573", apiClient.GetMessageBuffer());
        }
    }

    public class ApiClientForTesting : ApiClient
    {
        private StringBuilder _testMessageBuffer = new StringBuilder();

        public ApiClientForTesting(string host, int port, string hamClockUris, ILogger logger) 
            : base(host, port, hamClockUris, logger)
        {
        }
        
        /// <summary>
        /// Set the HamClockClient for testing
        /// </summary>
        public void SetHamClockClient(HamClockClient hamClockClient)
        {
            if (hamClockClient == null)
            {
                throw new ArgumentNullException(nameof(hamClockClient));
            }
            
            // Use reflection to replace the internal HamClockClient
            var fieldInfo = typeof(ApiClient).GetField("_hamClockClient", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fieldInfo == null)
            {
                throw new InvalidOperationException("_hamClockClient field not found in ApiClient");
            }
            
            fieldInfo.SetValue(this, hamClockClient);
        }

        /// <summary>
        /// Set the message buffer directly for testing
        /// </summary>
        public void SetMessageBuffer(string message)
        {
            _testMessageBuffer.Clear();
            _testMessageBuffer.Append(message);
            
            // Use reflection to set the private field in the base class
            var fieldInfo = typeof(ApiClient).GetField("_messageBuffer", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fieldInfo == null)
            {
                throw new InvalidOperationException("_messageBuffer field not found in ApiClient");
            }
            
            fieldInfo.SetValue(this, _testMessageBuffer);
        }
        
        /// <summary>
        /// Append to the message buffer for testing
        /// </summary>
        public void AppendToMessageBuffer(string message)
        {
            _testMessageBuffer.Append(message);
            
            // Use reflection to set the private field in the base class
            var fieldInfo = typeof(ApiClient).GetField("_messageBuffer", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fieldInfo == null)
            {
                throw new InvalidOperationException("_messageBuffer field not found in ApiClient");
            }
            
            fieldInfo.SetValue(this, _testMessageBuffer);
        }
        
        /// <summary>
        /// Get current buffer content for assertions
        /// </summary>
        public string GetMessageBuffer()
        {
            // Use reflection to get the private field value
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
        /// Expose the ProcessBufferedCommands method for testing
        /// </summary>
        public async Task TestProcessBufferedCommands()
        {
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
    }
}
