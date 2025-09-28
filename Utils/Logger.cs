using System;
using System.IO;
using System.Text;

namespace pc_system_monitor_app.Utils
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string _dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "logs");
        private static readonly string _file = Path.Combine(_dir, "error.log");

        public static void Write(string text)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(_dir);
                    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {text}{Environment.NewLine}";
                    File.AppendAllText(_file, line, Encoding.UTF8);
                }
            }
            catch { }
        }

        public static void WriteException(Exception ex, string note = "")
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(_dir);
                    var sb = new StringBuilder();
                    sb.AppendLine("----- Exception -----");
                    sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    if (!string.IsNullOrEmpty(note)) sb.AppendLine($"Note: {note}");
                    sb.AppendLine(ex.ToString());
                    sb.AppendLine("---------------------");
                    File.AppendAllText(_file, sb.ToString(), Encoding.UTF8);
                }
            }
            catch { }
        }

        public static string GetLogPath()
        {
            try
            {
                Directory.CreateDirectory(_dir);
                return _file;
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "error.log");
            }
        }
    }
}