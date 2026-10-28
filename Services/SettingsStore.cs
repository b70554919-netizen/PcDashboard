using System.IO;
using System.Text.Json;
using PcDashboard.Models;

namespace PcDashboard.Services;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PcDashboard");

    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DataDirectory);
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(SettingsPath, json);
    }
}
