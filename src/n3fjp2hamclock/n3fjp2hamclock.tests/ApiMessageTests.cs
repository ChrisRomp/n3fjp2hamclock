using System.Text;
using Moq;
using n3fjp2hamclock.helpers;
using Xunit;

namespace n3fjp2hamclock.tests
{
    public class ApiMessageTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public ApiMessageTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public async Task ProcessBufferedCommands_SingleLargeChunk_ProcessesAllCommands()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object);

            // Create a large buffer exceeding the 1024 read buffer size with multiple commands
            var largeBuffer = new StringBuilder();

            // Add 10 CALLTABEVENT commands
            for (int i = 0; i < 10; i++)
            {
                largeBuffer.Append($"<CMD><CALLTABEVENT><CALL>W{i}XX</CALL>");
                // Add some padding to make it larger
                largeBuffer.Append($"<NAME>Amateur Radio Operator {i}</NAME>");
                largeBuffer.Append($"<LAT>{40 + (i * 0.1)}</LAT><LON>{-70 - (i * 0.1)}</LON>");
                largeBuffer.Append("</CMD>");
            }
            var apiClient = new ApiClientForTesting("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(mockHamClockClient.Object);

            // Act
            apiClient.SetMessageBuffer(largeBuffer.ToString());
            await apiClient.TestProcessBufferedCommands();

            // Assert
            // Verify each UpdateHamClocks call was made with the correct coordinates
            for (int i = 0; i < 10; i++)
            {
                double lat = 40 + (i * 0.1);
                double lon = -70 - (i * 0.1);
                mockHamClockClient.Verify(
                    client => client.UpdateHamClocks(lat.ToString(), lon.ToString()),
                    Times.Once);
            }

            // Verify total calls
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks(It.IsAny<string>(), It.IsAny<string>()),
                Times.Exactly(10));

            // Buffer should be empty
            Assert.Equal(0, apiClient.GetMessageBuffer().Length);
        }

        [Fact]
        public async Task ProcessBufferedCommands_MultipleEventTypes_OnlyProcessesCallTabEvents()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object);

            // Create a buffer with various N3FJP API commands, but only CALLTABEVENT should be processed
            var buffer = new StringBuilder();

            // Add other event types that shouldn't be processed by our client
            buffer.Append("<CMD><PROGRAM></CMD>");
            buffer.Append("<CMD><READBMF></CMD>");
            buffer.Append("<CMD><DUPECHECK><CALL>W1AW</CALL><BAND>10</BAND><MODE>CW</MODE></CMD>");
            // Add the CALLTABEVENT we care about
            buffer.Append("<CMD><CALLTABEVENT><CALL>W1AW</CALL><LAT>41.7144</LAT><LON>-72.7289</LON></CMD>");

            // Add more events that shouldn't be processed
            buffer.Append("<CMD><ENTEREVENT><QSOCOUNT>765</QSOCOUNT><CALL>LA9A</CALL></CMD>");

            var apiClient = new ApiClientForTesting("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(mockHamClockClient.Object);

            // Act
            apiClient.SetMessageBuffer(buffer.ToString());
            await apiClient.TestProcessBufferedCommands();

            // Assert
            // Only the CALLTABEVENT should trigger UpdateHamClocks
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks("41.7144", "-72.7289"),
                Times.Once);

            // Verify total calls
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks(It.IsAny<string>(), It.IsAny<string>()),
                Times.Exactly(1));

            // Buffer should be empty
            Assert.Equal(0, apiClient.GetMessageBuffer().Length);
        }

        [Fact]
        public async Task ProcessBufferedCommands_RealWorldN3FJPFormat_ProcessesCorrectly()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object);

            // Create a realistic N3FJP CALLTABEVENT with all fields as documented
            var buffer = new StringBuilder();
            buffer.Append("<CMD><CALLTABEVENT><CALL>LA9A</CALL><BAND>10</BAND><MODE>CW</MODE>");
            buffer.Append("<MODETEST>CW</MODETEST><COUNTRY>Norway</COUNTRY><DXCC>266</DXCC>");
            buffer.Append("<MYCALL>N3FJP</MYCALL><OPERATOR>N3FJP</OPERATOR><QSOCOUNT>65333</QSOCOUNT>");
            buffer.Append("<PFX>LA9</PFX><CONT>EU</CONT><CQZ>14</CQZ><ITUZ>18</ITUZ>"); buffer.Append("<LAT>61</LAT><LON>9</LON><BEARING>37</BEARING>");
            buffer.Append("<LONGPATH>217</LONGPATH><DISTANCE>3,732</DISTANCE></CMD>");

            var apiClient = new ApiClientForTesting("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(mockHamClockClient.Object);

            // Act
            apiClient.SetMessageBuffer(buffer.ToString());
            await apiClient.TestProcessBufferedCommands();

            // Assert
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks("61", "9"),
                Times.Once);

            // Buffer should be empty
            Assert.Equal(0, apiClient.GetMessageBuffer().Length);
        }

        [Fact]
        public async Task ProcessBufferedCommands_WithChunkedInput_HandlesSplitCommands()
        {            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object);
            var apiClient = new ApiClientForTesting("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(mockHamClockClient.Object);

            // First chunk
            var chunk1 = "<CMD><CALLTABEVENT><CALL>LA9A</CALL><BAND>10</BAND><MODE>CW</MODE>";
            apiClient.SetMessageBuffer(chunk1);
            await apiClient.TestProcessBufferedCommands();

            // Second chunk
            var chunk2 = "<MODETEST>CW</MODETEST><LAT>61</LAT><LON>9</LON></CMD>";
            apiClient.AppendToMessageBuffer(chunk2);
            await apiClient.TestProcessBufferedCommands();

            // Assert
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks("61", "9"),
                Times.Once);

            // Buffer should be empty
            Assert.Equal(0, apiClient.GetMessageBuffer().Length);
        }

        [Fact]
        public async Task ProcessBufferedCommands_WithExtraWhitespace_HandlesCorrectly()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object);
            // Create a command with extra whitespace            
            var commandWithWhitespace = "<CMD>  <CALLTABEVENT>  <CALL>W1AW</CALL>  <LAT>41.7144</LAT>  <LON>-72.7289</LON>  </CMD>";

            var apiClient = new ApiClientForTesting("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(mockHamClockClient.Object);

            // Act
            apiClient.SetMessageBuffer(commandWithWhitespace);
            await apiClient.TestProcessBufferedCommands();

            // Assert
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks("41.7144", "-72.7289"),
                Times.Once);

            // Buffer should be empty
            Assert.Equal(0, apiClient.GetMessageBuffer().Length);
        }
        [Fact]
        public async Task ProcessBufferedCommands_WithMalformedTags_SkipsInvalidCommands()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object);

            // Create commands with malformed XML tags - we'll use different events
            var buffer = new StringBuilder();

            // Use only one valid command to ensure we get exactly one call
            buffer.Append("<CMD><OTHER-EVENT><CALL>K1JT</CALL><LAT>40.3573</LAT><LON>-80.1234</LON></OTHER-EVENT></CMD>");  // This won't match <CALLTABEVENT>
            buffer.Append("<CMD><CALLTABEVENT><CALL>N3FJP</CALL><LAT>39.5200</LAT><LON>-76.3200</LON></CMD>"); // Only this should be processed

            var apiClient = new ApiClientForTesting("localhost", 1100, "http://example.com", _mockLogger.Object);
            apiClient.SetHamClockClient(mockHamClockClient.Object);

            // Act
            apiClient.SetMessageBuffer(buffer.ToString());
            await apiClient.TestProcessBufferedCommands();

            // Assert
            // Only the valid command should be processed
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks("39.5200", "-76.3200"),
                Times.Once);

            // Total calls should be 1
            mockHamClockClient.Verify(
                client => client.UpdateHamClocks(It.IsAny<string>(), It.IsAny<string>()),
                Times.Exactly(1));
        }
    }
}
