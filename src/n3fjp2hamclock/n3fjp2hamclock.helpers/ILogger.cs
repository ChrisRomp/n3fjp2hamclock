using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
