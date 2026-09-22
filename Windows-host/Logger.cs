using System;
using System.IO;
using System.Text;
using System.Threading;

namespace WindowsHost
{
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EZ Across Control", "Logs");
        private static readonly string LogFile = Path.Combine(LogDir, "host.log");
        private static readonly object _lock = new object();
        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

        static Logger()
        {
            try
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);
                
                RotateLogIfNeeded();
            }
            catch
            {
                // Ignore initialization errors
            }
        }

        public static void Log(string tag, string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{tag}] {message}";
                
                // Write to debug console if attached
                System.Diagnostics.Debug.WriteLine(line);

                lock (_lock)
                {
                    RotateLogIfNeeded();
                    File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Fail silently for logging
            }
        }

        public static void LogException(Exception ex, string context = "Unhandled")
        {
            Log("EXCEPTION", $"[{context}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Log("EXCEPTION", $"[Inner] {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}");
            }
        }

        private static void RotateLogIfNeeded()
        {
            if (File.Exists(LogFile))
            {
                var fileInfo = new FileInfo(LogFile);
                if (fileInfo.Length > MaxFileSize)
                {
                    string backupFile = Path.Combine(LogDir, "host_old.log");
                    if (File.Exists(backupFile))
                        File.Delete(backupFile);
                    
                    File.Move(LogFile, backupFile);
                }
            }
        }
    }
}
