namespace PcDashboard.Models;

public sealed class AppSettings
{
    public int RefreshSeconds { get; set; } = 2;
    public double CpuTempThreshold { get; set; } = 90;
    public double GpuTempThreshold { get; set; } = 85;
    public bool CsvLogging { get; set; }
    public bool StartWithWindows { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool Fahrenheit { get; set; }
    public bool OverlayEnabled { get; set; }
    public string OverlayCorner { get; set; } = "TopRight";
    public int OverlayFontSize { get; set; } = 14;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            RefreshSeconds = RefreshSeconds,
            CpuTempThreshold = CpuTempThreshold,
            GpuTempThreshold = GpuTempThreshold,
            CsvLogging = CsvLogging,
            StartWithWindows = StartWithWindows,
            AlwaysOnTop = AlwaysOnTop,
            MinimizeToTray = MinimizeToTray,
            Fahrenheit = Fahrenheit,
            OverlayEnabled = OverlayEnabled,
            OverlayCorner = OverlayCorner,
            OverlayFontSize = OverlayFontSize
        };
    }
}
