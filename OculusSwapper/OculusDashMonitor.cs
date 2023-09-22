using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class OculusDashMonitor
{
    private static readonly string OculusDashPath = @"C:\Program Files\Oculus\Support\oculus-dash\dash\bin\OculusDash.exe";
    private static CancellationTokenSource cts = new CancellationTokenSource();

    public static void StartMonitoring()
    {
        Task.Run(() => MonitorOculusDash(cts.Token));

        Console.WriteLine("Monitoring OculusDash.exe. Press any key to stop...");
    }

    private static async Task MonitorOculusDash(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!IsOculusDashRunning())
            {
                Console.WriteLine("OculusDash.exe is not running. Restarting...");
                StartOculusDash();
            }

            await Task.Delay(5000, cancellationToken); // Check every 5 seconds
        }
    }

    private static bool IsOculusDashRunning()
    {
        return Process.GetProcessesByName("OculusDash").Length > 0;
    }

    private static void StartOculusDash()
    {
        try
        {
            Process.Start(OculusDashPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start OculusDash.exe: {ex.Message}");
        }
    }
}
