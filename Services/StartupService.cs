using Microsoft.Win32;

namespace PcDashboard.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PcDashboard";

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null)
            {
                return;
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return;
            }

            var executable = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executable))
            {
                key.SetValue(ValueName, $"\"{executable}\"");
            }
        }
        catch
        {
        }
    }
}
