using System.Globalization;
using System.IO;

namespace PcDashboard.Services;

public sealed class CsvLogger
{
    private readonly string _path;

    public CsvLogger()
    {
        Directory.CreateDirectory(SettingsStore.DataDirectory);
        _path = Path.Combine(SettingsStore.DataDirectory, "sensors.csv");

        if (!File.Exists(_path))
        {
            File.WriteAllText(
                _path,
                "timestamp,cpu_temp_c,cpu_load_pct,cpu_power_w,gpu_temp_c,gpu_load_pct,ram_used_gb,ram_total_gb,vram_used_mb,vram_total_mb,disk_temp_c" +
                Environment.NewLine);
        }
    }

    public void Append(
        DateTime timestamp,
        float? cpuTemp,
        float? cpuLoad,
        float? cpuPower,
        float? gpuTemp,
        float? gpuLoad,
        float? ramUsed,
        float? ramTotal,
        float? vramUsed,
        float? vramTotal,
        float? diskTemp)
    {
        try
        {
            var line = string.Join(
                ',',
                timestamp.ToString("O", CultureInfo.InvariantCulture),
                F(cpuTemp),
                F(cpuLoad),
                F(cpuPower),
                F(gpuTemp),
                F(gpuLoad),
                F(ramUsed),
                F(ramTotal),
                F(vramUsed),
                F(vramTotal),
                F(diskTemp));

            File.AppendAllText(_path, line + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static string F(float? value)
    {
        return value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
    }
}
