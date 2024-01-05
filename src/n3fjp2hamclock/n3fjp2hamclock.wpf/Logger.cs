using n3fjp2hamclock.helpers;

namespace n3fjp2hamclock.wpf
{
    internal class Logger : ILogger
    {
        private readonly MainWindow _mainWindow;

        public Logger(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void Log(string message, LogLevel logLevel = LogLevel.Info)
        {
            _mainWindow.LogMessage(message, logLevel);
        }
    }
}
