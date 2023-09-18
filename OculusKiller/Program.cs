using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace OculusKiller
{
    public class Program
    {
        private static string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OculusKiller", "logs.txt");

        public static void Main()
        {
            try
            {
                InitializeLogging();
                Log("Application started.");

                string oculusPath = GetOculusPath();
                var result = GetSteamPaths();
                if (result == null || String.IsNullOrEmpty(oculusPath))
                {
                    return;
                }
                string startupPath = result.Item1;
                string vrServerPath = result.Item2;

                Process.Start(startupPath).WaitForExit();

                MonitorProcesses(oculusPath, vrServerPath);
            }
            catch (Exception e)
            {
                Log($"An exception occurred: {e.Message}");
                MessageBox.Show($"An exception occured while attempting to find/start SteamVR...\n\nMessage: {e}");
            }
        }

        private static void InitializeLogging()
        {
            if (!Directory.Exists(Path.GetDirectoryName(logPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            }
        }

        private static void Log(string message)
        {
            File.AppendAllText(logPath, DateTime.Now + ": " + message + "\n");
        }

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
                        Log("Killing Oculus runtime...");
                        ovrServerProcess.Kill();
                    }
                };
            }
            catch (Exception e)
            {
                Log($"Error during process monitoring: {e.Message}");
                MessageBox.Show($"Error during process monitoring:\n\nMessage: {e}");
            }
        }

        private static Process GetProcessByNameAndPath(string processName, string processPath)
        {
            return Array.Find(Process.GetProcessesByName(processName), process => process.MainModule.FileName == processPath);
        }

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
