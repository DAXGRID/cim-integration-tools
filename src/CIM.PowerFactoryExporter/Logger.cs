using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace CIM.PowerFactoryExporter
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
            System.Diagnostics.Debug.WriteLine(text);
        }

     

        public static void Log(Exception ex)
        {
            Log(LogLevel.Error, ex.ToString());
        }

        /*
        public static void Log(LogLevel logLevel, string text) 
        {
            string logLevelConfigParam = Configuration.GetConfiguration()["LogLevel"];
            if (logLevelConfigParam != null)
            {
                logLevelConfigParam = logLevelConfigParam.ToLower();

                if (logLevelConfigParam == "verbose")
                    Level = LogLevel.Verbose;
                else if (logLevelConfigParam == "debug")
                    Level = LogLevel.Debug;
                else if (logLevelConfigParam == "warning")
                    Level = LogLevel.Warning;
                else if (logLevelConfigParam == "error")
                    Level = LogLevel.Warning;
                else if (logLevelConfigParam == "info")
                    Level = LogLevel.Info;
            }

            if (logLevel >= Level)
            {
                string logTxt = String.Format("{0} {1} {2} {3}", DateTime.Now.ToShortDateString(), DateTime.Now.ToLongTimeString(), logLevel.ToString().ToUpper(), text);

                System.Diagnostics.Debug.WriteLine(logTxt);

                string logFileName = Configuration.GetConfiguration()["LogFileName"];

                if (UseLogFileName != null)
                    logFileName = UseLogFileName;

                if (logFileName != null)
                {
                        using (StreamWriter w = File.AppendText(logFileName))
                        {
                            w.WriteLine(logTxt);
                        }
                }

                if (WriteToConsole)
                    System.Console.WriteLine(logTxt);

                if (logLevel == LogLevel.Error && WriteErrorsToEventLog != null)
                {
                    string eventIdParam = Configuration.GetConfiguration()["EventLogErrorId"];

                     if (eventIdParam != null)
                         EventId = Convert.ToInt32(eventIdParam);

                    System.Diagnostics.EventLog appLog = new System.Diagnostics.EventLog();
                    appLog.Source = WriteErrorsToEventLog;
                    appLog.WriteEntry(text, System.Diagnostics.EventLogEntryType.Error, EventId);
                }

            }
        }

        public static void Log(Exception ex)
        {
            Log(LogLevel.Error, ex.ToString());
        }
        */

    }
}