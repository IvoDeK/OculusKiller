using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace OculusKiller
{
    public class Program
    {
        // Define the path for logging
        private static string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OculusKiller", "logs.txt");

        public static async Task Main()
        {
            try
            {
                // Initialize the logging system
                InitializeLogging();
                Log("Application started.");

                // Get paths for Oculus and SteamVR
                string oculusPath = GetOculusPath();
                var result = GetSteamPaths();
                if (result == null || String.IsNullOrEmpty(oculusPath))
                {
                    return;
                }
                string startupPath = result.Item1;
                string vrServerPath = result.Item2;

                // Retry mechanism for starting SteamVR
                const int maxRetries = 2;
                const int delayBetweenRetries = 2000; // 2 seconds
                bool processStarted = false;

                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        var process = Process.Start(startupPath);
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                            processStarted = true;
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        Log($"Attempt {retry + 1} to start SteamVR failed: {e.Message}");
                        if (retry < maxRetries - 1)
                        {
                            Log("Retrying...");
                            await Task.Delay(delayBetweenRetries);
                        }
                    }
                }

                if (!processStarted)
                {
                    Log("Failed to start SteamVR after multiple attempts.");
                    MessageBox.Show("Failed to start SteamVR after multiple attempts.");
                    return;
                }

                // Monitor the processes to ensure proper shutdown
                MonitorProcesses(oculusPath, vrServerPath);
            }
            catch (Exception e)
            {
                Log($"An unexpected exception occurred: {e.Message}");
                MessageBox.Show($"An unexpected exception occurred: {e.Message}");
            }
        }

        // Initialize the logging directory
        private static void InitializeLogging()
        {
            if (!Directory.Exists(Path.GetDirectoryName(logPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            }
        }

        // Log messages to the defined log path
        private static void Log(string message)
        {
            File.AppendAllText(logPath, DateTime.Now + ": " + message + "\n");
        }

        // Monitor Oculus and SteamVR processes for proper shutdown
        private static void MonitorProcesses(string oculusPath, string vrServerPath)
        {
            try
            {
                Process vrServerProcess = GetProcessByNameAndPath("vrserver", vrServerPath);
                if (vrServerProcess == null)
                {
                    Log("SteamVR vrserver not found. Exiting...");
                    MessageBox.Show("SteamVR vrserver not found... (Did SteamVR crash?)");
                    return;
                }

                vrServerProcess.EnableRaisingEvents = true;
                vrServerProcess.Exited += (sender, e) =>
                {
                    Log("SteamVR vrserver exited.");
                    Process ovrServerProcess = GetProcessByNameAndPath("OVRServer_x64", oculusPath);
                    if (ovrServerProcess != null)
                    {
                        Log("Attempting graceful shutdown of Oculus runtime...");
                        ovrServerProcess.CloseMainWindow();

                        if (!ovrServerProcess.WaitForExit(3000))
                        {
                            Log("Oculus runtime did not shut down gracefully. Forcing shutdown...");
                            ovrServerProcess.Kill();
                        }
                    }
                };
            }
            catch (Exception e)
            {
                Log($"Error during process monitoring: {e.Message}");
                MessageBox.Show($"Error during process monitoring:\n\nMessage: {e}");
            }
        }

        // Get a specific process by its name and path
        private static Process GetProcessByNameAndPath(string processName, string processPath)
        {
            return Array.Find(Process.GetProcessesByName(processName), process => process.MainModule.FileName == processPath);
        }

        // Get the path for Oculus
        static string GetOculusPath()
        {
            string oculusPath = Environment.GetEnvironmentVariable("OculusBase");
            if (string.IsNullOrEmpty(oculusPath))
            {
                MessageBox.Show("Oculus installation environment not found...");
                return null;
            }

            oculusPath = Path.Combine(oculusPath, @"Support\oculus-runtime\OVRServer_x64.exe");
            if (!File.Exists(oculusPath))
            {
                MessageBox.Show("Oculus server executable not found...");
                return null;
            }

            return oculusPath;
        }

        // Get the paths for SteamVR
        public static Tuple<string, string> GetSteamPaths()
        {
            string openVrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"openvr\openvrpaths.vrpath");
            if (!File.Exists(openVrPath))
            {
                MessageBox.Show("OpenVR Paths file not found... (Has SteamVR been run once?)");
                return null;
            }

            try
            {
                JavaScriptSerializer jss = new JavaScriptSerializer();
                string openvrJsonString = File.ReadAllText(openVrPath);
                dynamic openvrPaths = jss.DeserializeObject(openvrJsonString);

                string location = openvrPaths["runtime"][0].ToString();
                string startupPath = Path.Combine(location, @"bin\win64\vrstartup.exe");
                string serverPath = Path.Combine(location, @"bin\win64\vrserver.exe");

                if (!File.Exists(startupPath))
                {
                    MessageBox.Show("SteamVR startup executable does not exist... (Has SteamVR been run once?)");
                    return null;
                }
                if (!File.Exists(serverPath))
                {
                    MessageBox.Show("SteamVR server executable does not exist... (Has SteamVR been run once?)");
                    return null;
                }

                return new Tuple<string, string>(startupPath, serverPath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Corrupt OpenVR Paths file found... (Has SteamVR been run once?)\n\nMessage: {e}");
            }
            return null;
        }
    }
}
