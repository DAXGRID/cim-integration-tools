using Serilog.Events;

namespace DAX.Util
{
    public enum LogLevel
    {
        Verbose = 1,
        Debug = 2,
        Warning = 3,
        Info = 4,
        Error = 5
    }

    public static class Logger
    {
        public static int EventId = 12001;
        public static bool WriteToConsole = false;
        public static string WriteErrorsToEventLog = null;
        public static LogLevel Level = LogLevel.Debug;
        public static string UseLogFileName = null;

        public static void Log(LogLevel logLevel, string text)
        {
            Serilog.Log.Write(MapLevel(logLevel), text);
        }

        private static LogEventLevel MapLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Verbose:
                    return LogEventLevel.Verbose;
                case LogLevel.Debug:
                    return LogEventLevel.Debug;
                case LogLevel.Warning:
                    return LogEventLevel.Warning;
                case LogLevel.Info:
                    return LogEventLevel.Information;
                case LogLevel.Error:
                    return LogEventLevel.Error;
                default:
                    throw new ArgumentException($"Unknown log level: {logLevel}");
            }
        }

        public static void Log(Exception ex)
        {
            Log(LogLevel.Error, ex.ToString());
        }
    }
}