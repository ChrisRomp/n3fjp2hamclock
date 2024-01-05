using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace n3fjp2hamclock.helpers
{
    public partial class ApiClient
    {
        private readonly string Host;
        private readonly int Port;
        private TcpClient? _tcpClient;
        private readonly ILogger Logger;
        private CancellationTokenSource? _cancellationTokenSource;

        private readonly HamClockClient _hamClockClient;

        public ApiClient(string host, int port, string hamClockUris, ILogger logger)
        {
            Host = host;
            Port = port;
            Logger = logger;

            Logger.Log("Initializing API client...", LogLevel.Trace);

            _hamClockClient = new HamClockClient(hamClockUris, logger);
        }

        public async Task Connect()
        {
            Logger.Log("Connecting to N3FJP API on " + Host + ":" + Port + "...", LogLevel.Trace);

            try
            {
                _tcpClient = new TcpClient(Host, Port);
                Logger.Log("Connected to N3FJP API server.", LogLevel.Info);

                using var stream = _tcpClient.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead;

                _cancellationTokenSource = new CancellationTokenSource();

                try
                {
                    while ((bytesRead = await stream.ReadAsync(buffer, _cancellationTokenSource.Token)) > 0)
                    {
                        string message = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                        Logger.Log("[N3FJP API] " + message, LogLevel.Trace);

                        // Verify message starts with <CMD>
                        if (!message.StartsWith("<CMD>"))
                        {
                            Logger.Log("Invalid message received: " + message, LogLevel.Error);
                            continue;
                        }

                        // Check for <CALLTABEVENT> message
                        if (!message.Contains("<CALLTABEVENT>"))
                        {
                            continue;
                        }

                        // CallTab event received
                        var callSign = CallSignRegEx().Match(message).Value;
                        //var frequency = FrequencyRegEx().Match(message).Value;
                        //var mode = ModeRegEx().Match(message).Value;
                        var lat = LatRegEx().Match(message).Value;
                        var lon = LonRegEx().Match(message).Value;

                        // Print to console
                        Logger.Log("CallTab event received:", LogLevel.Info);
                        Logger.Log("  Call: " + callSign, LogLevel.Info);
                        //Logger.Log("  Frequency: " + frequency, LogLevel.Trace);
                        //Logger.Log("  Mode: " + mode, LogLevel.Trace);
                        Logger.Log("  Lat: " + lat, LogLevel.Info);
                        Logger.Log("  Lon: " + lon, LogLevel.Info);

                        // Update HamClock(s)
                        await _hamClockClient.UpdateHamClocks(lat, lon);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            catch (SocketException)
            {
                Logger.Log("Error connecting to N3FJP API", LogLevel.Error);
                throw;
            }

            _tcpClient = new TcpClient(Host, Port);
        }

        public void Disconnect()
        {
            Logger.Log("Disconnecting from N3FJP API...", LogLevel.Trace);
            _cancellationTokenSource?.Cancel();

            if (_tcpClient == null)
            {
                Logger.Log("Already disconnected.", LogLevel.Trace);
                return;
            }
            _tcpClient.Close();
            _tcpClient = null;

            Logger.Log("Disconnected from N3FJP API.", LogLevel.Info);
        }

        [GeneratedRegex(@"(?<=<CALL>)(.*?)(?=</CALL>)")]
        private static partial Regex CallSignRegEx();
        //[GeneratedRegex(@"(?<=<FREQ>)(.*?)(?=</FREQ>)")]
        //private static partial Regex FrequencyRegEx();
        //[GeneratedRegex(@"(?<=<MODE>)(.*?)(?=</MODE>)")]
        //private static partial Regex ModeRegEx();
        [GeneratedRegex(@"(?<=<LAT>)(.*?)(?=</LAT>)")]
        private static partial Regex LatRegEx();
        [GeneratedRegex(@"(?<=<LON>)(.*?)(?=</LON>)")]
        private static partial Regex LonRegEx();
    }
}
