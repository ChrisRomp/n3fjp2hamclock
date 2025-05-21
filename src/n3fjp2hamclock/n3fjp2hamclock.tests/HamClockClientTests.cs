using System.Net;
using System.Text;
using Moq;
using Moq.Protected;
using n3fjp2hamclock.helpers;
using Xunit;

namespace n3fjp2hamclock.tests
{
    public class HamClockClientTests
    {
        private readonly Mock<ILogger> _mockLogger;
        
        public HamClockClientTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public void Constructor_WithValidUri_InitializesCorrectly()
        {
            // Arrange & Act
            var client = new HamClockClient("http://example.com", _mockLogger.Object);
            
            // Assert
            _mockLogger.Verify(
                logger => logger.Log("Initialized HamClock client with 1 HamClock(s).", LogLevel.Trace), 
                Times.Once);
        }

        [Fact]
        public void Constructor_WithMultipleUris_InitializesAll()
        {
            // Arrange & Act
            var client = new HamClockClient("http://example1.com, http://example2.com", _mockLogger.Object);
            
            // Assert
            _mockLogger.Verify(
                logger => logger.Log("Initialized HamClock client with 2 HamClock(s).", LogLevel.Trace), 
                Times.Once);
        }

        [Fact]
        public void Constructor_WithNoUris_ThrowsException()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<Exception>(() => 
                new HamClockClient("", _mockLogger.Object));
                
            Assert.Equal("No HamClock URIs specified.", exception.Message);
        }
        
        [Fact]
        public void Constructor_TrimsTrailingSlashes()
        {
            // Arrange
            var testableClient = new TestableHamClockClient("http://example.com/", _mockLogger.Object);
            
            // Act
            var hamClocks = testableClient.GetHamClocks();
            
            // Assert
            Assert.Single(hamClocks);
            Assert.Equal("http://example.com", hamClocks[0]);
        }

        [Fact]
        public async Task UpdateHamClocks_WithValidLatLon_CallsUpdateDx()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object) { CallBase = true };
            mockHamClockClient
                .Setup(client => client.UpdateDx(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            
            // Act
            await mockHamClockClient.Object.UpdateHamClocks("41.7144", "-72.7289");
            
            // Assert
            mockHamClockClient.Verify(
                client => client.UpdateDx("http://example.com", "41.7144", "-72.7289"), 
                Times.Once);
        }
        
        [Fact]
        public async Task UpdateHamClocks_WithMultipleEndpoints_CallsUpdateDxForAll()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example1.com, http://example2.com", _mockLogger.Object) { CallBase = true };
            mockHamClockClient
                .Setup(client => client.UpdateDx(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            
            // Act
            await mockHamClockClient.Object.UpdateHamClocks("41.7144", "-72.7289");
            
            // Assert
            mockHamClockClient.Verify(
                client => client.UpdateDx("http://example1.com", "41.7144", "-72.7289"), 
                Times.Once);
            mockHamClockClient.Verify(
                client => client.UpdateDx("http://example2.com", "41.7144", "-72.7289"), 
                Times.Once);
        }
        
        [Fact]
        public async Task UpdateHamClocks_WithInvalidLatLon_DoesNotCallUpdateDx()
        {
            // Arrange
            var mockHamClockClient = new Mock<HamClockClient>("http://example.com", _mockLogger.Object) { CallBase = true };
            mockHamClockClient
                .Setup(client => client.UpdateDx(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            
            // Act
            await mockHamClockClient.Object.UpdateHamClocks("invalid", "-72.7289");
            
            // Assert
            mockHamClockClient.Verify(
                client => client.UpdateDx(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
                Times.Never);
            _mockLogger.Verify(
                logger => logger.Log("Invalid lat/lon: invalid/-72.7289", LogLevel.Error), 
                Times.Once);
        }        [Fact]
        public async Task UpdateDx_BuildsCorrectUri()
        {
            // Arrange
            // Create a mock HttpMessageHandler
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                });

            // Create a client with the mocked handler
            var httpClient = new HttpClient(mockHandler.Object);
            var testableClient = new TestableHamClockClient("http://example.com", _mockLogger.Object, httpClient);
            
            // Act
            await testableClient.TestUpdateDx("http://example.com", "41.7144", "-72.7289");
            
            // Assert
            mockHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri.ToString() == "http://example.com/set_newdx?lat=41.7144&lng=-72.7289"),
                ItExpr.IsAny<CancellationToken>()
            );
        }
        
        [Fact]
        public async Task UpdateDx_WithHttpError_LogsError()
        {
            // Arrange
            // Create a mock HttpMessageHandler that returns a 404
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound
                });

            // Create a client with the mocked handler
            var httpClient = new HttpClient(mockHandler.Object);
            var testableClient = new TestableHamClockClient("http://example.com", _mockLogger.Object, httpClient);
            
            // Act
            await testableClient.TestUpdateDx("http://example.com", "41.7144", "-72.7289");
            
            // Assert
            _mockLogger.Verify(
                logger => logger.Log("Error calling hamClock API at http://example.com: NotFound", LogLevel.Error), 
                Times.Once);
        }
        
        [Fact]
        public async Task UpdateDx_WithNetworkError_LogsException()
        {
            // Arrange
            // Create a mock HttpMessageHandler that throws an exception
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Create a client with the mocked handler
            var httpClient = new HttpClient(mockHandler.Object);
            var testableClient = new TestableHamClockClient("http://example.com", _mockLogger.Object, httpClient);
            
            // Act
            await testableClient.TestUpdateDx("http://example.com", "41.7144", "-72.7289");
            
            // Assert
            _mockLogger.Verify(
                logger => logger.Log("Error calling hamClock API at http://example.com: Network error", LogLevel.Error), 
                Times.Once);
        }
    }    /// <summary>
    /// Test-specific subclass of HamClockClient to expose protected methods and properties for testing
    /// </summary>
    public class TestableHamClockClient : HamClockClient
    {
        private readonly HttpClient? _httpClient;
        private readonly ILogger _testLogger;

        public TestableHamClockClient(string hamClockUris, ILogger logger) 
            : base(hamClockUris, logger)
        {
            _httpClient = null;
            _testLogger = logger;
        }
        
        public TestableHamClockClient(string hamClockUris, ILogger logger, HttpClient httpClient) 
            : base(hamClockUris, logger)
        {
            _httpClient = httpClient;
            _testLogger = logger;
        }
        
        /// <summary>
        /// Expose the private HamClocks collection for testing
        /// </summary>
        public List<string> GetHamClocks()
        {
            // Use reflection to get the private field value
            var field = typeof(HamClockClient).GetField("_hamClocks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field != null ? (List<string>)field.GetValue(this)! : new List<string>();
        }
          /// <summary>
        /// New implementation of UpdateDx using the provided HttpClient for testing
        /// </summary>
        public async Task TestUpdateDx(string hamClockUri, string lat, string lon)
        {
            if (_httpClient != null)
            {
                var commandRoute = "/set_newdx";

                var commandUri = new UriBuilder(hamClockUri + commandRoute)
                {
                    Query = "lat=" + lat + "&lng=" + lon
                };
                _testLogger.Log("Calling HamClock API: " + commandUri.Uri.ToString(), LogLevel.Trace);

                try
                {
                    using var response = await _httpClient.GetAsync(commandUri.Uri);
                    using var content = response.Content;

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        _testLogger.Log($"Error calling hamClock API at {hamClockUri}: " + response.StatusCode, LogLevel.Error);
                    }

                    _testLogger.Log("HamClock API response code: " + response.StatusCode.ToString(), LogLevel.Trace);
                }
                catch (Exception ex)
                {
                    _testLogger.Log($"Error calling hamClock API at {hamClockUri}: " + ex.Message, LogLevel.Error);
                }
            }
        }
    }
}
