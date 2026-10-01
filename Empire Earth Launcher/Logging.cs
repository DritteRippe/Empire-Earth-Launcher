using System;
using System.Diagnostics;
using System.IO;

namespace Empire_Earth_Launcher
{
    class Logging
    {

        public enum LogLevel
        {
            Info, Warning, Error
        }

        /// <summary>
        /// Launcher logging, this will redirect the console to log file
        /// <br>Don't call it multiple time or previous the one will not work !</br>
        /// </summary>
        public Logging(string log_file)
        {
            Trace.Listeners.Clear();

            // When > 1 Mo => Clean and keep 500 lines
            if (File.Exists(log_file))
            {
                if (new FileInfo(log_file).Length > 1048576 /* 1 Mo */)
                {
                    string[] lines = File.ReadAllLines(log_file);
                    File.Delete(log_file);
                    for (int i = (lines.Length - 500); i != lines.Length; ++i)
                    {
                        Console.WriteLine(lines[i]);
                        File.AppendAllText(log_file, lines[i] + "\n");
                    }
                }
            }

            TextWriterTraceListener twtl = new TextWriterTraceListener(log_file);
            twtl.Name = "Empire Earth Launcher Logger";

            ConsoleTraceListener ctl = new ConsoleTraceListener(false);
            ctl.TraceOutputOptions = TraceOptions.DateTime;

            Trace.Listeners.Add(twtl);
            Trace.Listeners.Add(ctl);
            Trace.AutoFlush = true;

            Trace.WriteLine("");
        }

        public void Log(object log, LogLevel level)
        {
            Trace.WriteLine("[" + DateTime.Now + "] " + level + " : " + log);
        }

        public void Log(object log)
        {
            Log(log, LogLevel.Info);
        }

        public void Log(object log, Exception ex)
        {
            Log(log + "\n" + ex.Message + "\n" + ex.StackTrace, LogLevel.Error);
        }
    }
}
