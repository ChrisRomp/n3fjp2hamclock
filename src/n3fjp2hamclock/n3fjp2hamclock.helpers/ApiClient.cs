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
        private StringBuilder _messageBuffer = new StringBuilder();

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
                
                // Clear any previous data in the buffer
                _messageBuffer.Clear();

                _cancellationTokenSource = new CancellationTokenSource();

                try
                {
                    while ((bytesRead = await stream.ReadAsync(buffer, _cancellationTokenSource.Token)) > 0)
                    {
                        string chunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        Logger.Log("[N3FJP API] " + chunk, LogLevel.Trace);
                        
                        // Append the received chunk to our buffer
                        _messageBuffer.Append(chunk);
                        
                        // Process complete commands in the buffer
                        await ProcessBufferedCommands();
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
        
        /// <summary>
        /// Process complete commands in the message buffer
        /// </summary>
        private async Task ProcessBufferedCommands()
        {
            string bufferContent = _messageBuffer.ToString();
            int cmdStartPos = 0;
            int cmdEndPos = 0;
            
            // Find complete <CMD>...</CMD> blocks in the buffer
            while ((cmdStartPos = bufferContent.IndexOf("<CMD>", cmdStartPos)) != -1)
            {
                cmdEndPos = bufferContent.IndexOf("</CMD>", cmdStartPos);
                if (cmdEndPos == -1)
                {
                    // No complete command found, keep data in buffer and wait for more
                    break;
                }
                
                // Include the </CMD> tag in the extracted command
                cmdEndPos += "</CMD>".Length;
                
                // Extract the complete command
                string completeCommand = bufferContent.Substring(cmdStartPos, cmdEndPos - cmdStartPos);
                
                try
                {
                    // Process the command
                    if (completeCommand.Contains("<CALLTABEVENT>"))
                    {
                        // CallTab event received
                        var callSign = CallSignRegEx().Match(completeCommand).Value;
                        //var frequency = FrequencyRegEx().Match(completeCommand).Value;
                        //var mode = ModeRegEx().Match(completeCommand).Value;
                        var lat = LatRegEx().Match(completeCommand).Value;
                        var lon = LonRegEx().Match(completeCommand).Value;

                        // Print to console
                        Logger.Log("CallTab event received:", LogLevel.Info);
                        Logger.Log("  Call: " + callSign, LogLevel.Info);
                        //Logger.Log("  Frequency: " + frequency, LogLevel.Trace);
                        //Logger.Log("  Mode: " + mode, LogLevel.Trace);
                        Logger.Log("  Lat: " + lat, LogLevel.Info);
                        Logger.Log("  Lon: " + lon, LogLevel.Info);

                        // Update HamClock(s) only if we have valid lat/lon
                        if (!string.IsNullOrEmpty(lat) && !string.IsNullOrEmpty(lon))
                        {
                            await _hamClockClient.UpdateHamClocks(lat, lon);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error processing command: {ex.Message}", LogLevel.Error);
                    // Continue processing other commands even if one fails
                }
                
                // Move to the position after this command
                cmdStartPos = cmdEndPos;
            }
            
            // Remove processed commands from the buffer
            if (cmdStartPos > 0)
            {
                _messageBuffer.Remove(0, cmdStartPos);
                
                // Safety check: if buffer gets too large (likely due to malformed data),
                // clear it to prevent memory issues
                if (_messageBuffer.Length > 32768) // 32 KB
                {
                    Logger.Log("Buffer too large, clearing to prevent memory issues", LogLevel.Error);
                    _messageBuffer.Clear();
                }
            }
        }

        public void Disconnect()
        {
            Logger.Log("Disconnecting from N3FJP API...", LogLevel.Trace);
            _cancellationTokenSource?.Cancel();
            
            // Clear the message buffer
            _messageBuffer.Clear();

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
