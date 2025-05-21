using System.Net;

namespace n3fjp2hamclock.helpers
{
    public class HamClockClient
    {
        private readonly List<string> _hamClocks = [];
        private readonly ILogger Logger;

        public HamClockClient(string hamClockUris, ILogger logger)
        {
            Logger = logger;

            string[] hamClockUrisArray = hamClockUris.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (hamClockUrisArray.Length == 0)
            {
                throw new Exception("No HamClock URIs specified.");
            }

            foreach (string hamClockUri in hamClockUrisArray)
            {
                _hamClocks.Add(hamClockUri.Trim().TrimEnd('/'));
            }

            Logger.Log("Initialized HamClock client with " + _hamClocks.Count + " HamClock(s).", LogLevel.Trace);
        }
        public virtual async Task UpdateHamClocks(string lat, string lon)
        {
            Logger.Log("Updating " + _hamClocks.Count + " HamClock(s).", LogLevel.Trace);

            // Ensure lat/lon are numbers
            if (!double.TryParse(lat, out _) || !double.TryParse(lon, out _))
            {
                Logger.Log("Invalid lat/lon: " + lat + "/" + lon, LogLevel.Error);
                return;
            }

            foreach (string hamClockUri in _hamClocks)
            {
                await UpdateDx(hamClockUri, lat, lon);
            }
        }

        public virtual async Task UpdateDx(string hamClockUri, string lat, string lon)
        {
            var commandRoute = "/set_newdx";

            var commandUri = new UriBuilder(hamClockUri + commandRoute)
            {
                Query = "lat=" + lat + "&lng=" + lon
            };
            Logger.Log("Calling HamClock API: " + commandUri.Uri.ToString(), LogLevel.Trace);

            // Call URI via HTTP GET asynchronously
            try
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(commandUri.Uri);
                using var content = response.Content;

                // Check for 200 response
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Logger.Log($"Error calling hamClock API at {hamClockUri}: " + response.StatusCode, LogLevel.Error);
                }

                Logger.Log("HamClock API response code: " + response.StatusCode.ToString(), LogLevel.Trace);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error calling hamClock API at {hamClockUri}: " + ex.Message, LogLevel.Error);
            }
        }
    }
}
