using System;
using System.IO;

namespace WindowsHost.Engine
{
    public static class ScrcpyInstallationLocator
    {
        public static (string? ExecutablePath, string? ServerPath, string? RepoRoot, bool IsBundle) Locate()
        {
            return LocateInternal(AppContext.BaseDirectory);
        }

        public static (string? ExecutablePath, string? ServerPath, string? RepoRoot, bool IsBundle) LocateInternal(string baseDir)
        {
            // A. Check bundled runtime
            string bundledExe = Path.Combine(baseDir, "scrcpy", "scrcpy.exe");
            string bundledServer = Path.Combine(baseDir, "scrcpy", "scrcpy-server");

            if (File.Exists(bundledExe)) // Server might be missing but let's assume if exe is there it's a bundle attempt
            {
                return (bundledExe, bundledServer, null, true);
            }

            // B. Development repository
            string? current = baseDir;
            string? repoRoot = null;

            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, "Windows-host")) &&
                    Directory.Exists(Path.Combine(current, "third_party")))
                {
                    repoRoot = current;
                    break;
                }
                current = Path.GetDirectoryName(current);
            }

            if (repoRoot != null)
            {
                string devExe = Path.Combine(repoRoot, "third_party", "scrcpy-ezacross", "bin", "scrcpy.exe");
                string devServer = Path.Combine(repoRoot, "third_party", "scrcpy-ezacross", "bin", "scrcpy-server");
                return (devExe, devServer, repoRoot, false);
            }

            return (null, null, null, false);
        }
    }
}
