using n3fjp2hamclock.helpers;
using Xunit;

namespace n3fjp2hamclock.tests
{
    public class HamClockIntegrationTests
    {
        private readonly TestLogger _logger;

        public HamClockIntegrationTests()
        {
            _logger = new TestLogger();
        }

        [Fact]
        public void Constructor_WithMultipleUris_InitializesCorrectly()
        {
            // Arrange & Act
            var client = new HamClockClient(
                "http://hamclock1.local, http://hamclock2.local, http://hamclock3.local",
                _logger
            );

            // Assert
            Assert.True(_logger.ContainsLog("Initialized HamClock client with 3 HamClock(s).", LogLevel.Trace));
        }

        [Fact]
        public void Constructor_WithNoUris_ThrowsException()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<Exception>(() =>
                new HamClockClient("", _logger));

            Assert.Equal("No HamClock URIs specified.", exception.Message);
        }

        [Fact]
        public async Task UpdateHamClocks_WithInvalidLatLon_DoesNotCallUpdateDx()
        {
            // Arrange
            var client = new HamClockClient("http://example.com", _logger);
            _logger.Clear(); // Clear logs from initialization

            // Act - test with invalid lat/lon values
            await client.UpdateHamClocks("invalid", "-72.7289");

            // Assert
            Assert.True(_logger.ContainsLog("Invalid lat/lon: invalid/-72.7289", LogLevel.Error));
            // Since we can't easily verify internal methods weren't called, we rely on logs
            Assert.False(_logger.ContainsLog("Calling HamClock API", LogLevel.Trace));
        }

        [Fact]
        public async Task UpdateHamClocks_WithMultipleInvalidLatLon_LogsAllErrors()
        {
            // Arrange
            var client = new HamClockClient("http://example.com", _logger);
            _logger.Clear(); // Clear logs from initialization

            // Act - test with various invalid lat/lon values
            await client.UpdateHamClocks("invalid", "-72.7289");
            await client.UpdateHamClocks("41.7144", "invalid");
            await client.UpdateHamClocks("", "-72.7289");
            await client.UpdateHamClocks("41.7144", "");

            // Assert - check all errors were logged
            Assert.Equal(4, _logger.CountLogs("Invalid lat/lon", LogLevel.Error));
        }

        [Fact]
        public async Task UpdateHamClocks_WithValidLatLon_LogsUpdateAttempt()
        {
            // Arrange
            var client = new HamClockClient("http://example.com", _logger);
            _logger.Clear(); // Clear logs from initialization

            // Act
            // Even though this will try to make an HTTP call that will fail,
            // we can still verify that we got past the validation stage
            await client.UpdateHamClocks("41.7144", "-72.7289");

            // Assert
            Assert.True(_logger.ContainsLog("Updating 1 HamClock(s).", LogLevel.Trace));
        }
    }
}
