namespace n3fjp2hamclock.helpers
{
    public interface ILogger
    {
        public void Log(string message, LogLevel logLevel = LogLevel.Info);
    }

    public enum LogLevel
    {
        Trace = 0,
        Info = 1,
        Error = 2
    }
}
