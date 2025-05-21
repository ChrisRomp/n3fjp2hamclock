using n3fjp2hamclock.helpers;

namespace n3fjp2hamclock.tests
{
    /// <summary>
    /// Test implementation of ILogger
    /// </summary>
    public class TestLogger : ILogger
    {
        private readonly List<(string, LogLevel)> _logs = new();

        public void Log(string message, LogLevel logLevel)
        {
            _logs.Add((message, logLevel));
        }

        public List<(string Message, LogLevel Level)> GetLogs()
        {
            return _logs;
        }

        public bool ContainsLog(string messageSubstring, LogLevel level)
        {
            return _logs.Any(log => log.Item1.Contains(messageSubstring) && log.Item2 == level);
        }

        public int CountLogs(string messageSubstring, LogLevel level)
        {
            return _logs.Count(log => log.Item1.Contains(messageSubstring) && log.Item2 == level);
        }

        public void Clear()
        {
            _logs.Clear();
        }
    }
}
