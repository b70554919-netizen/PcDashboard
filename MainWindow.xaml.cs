using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;
using PcDashboard.Models;
using PcDashboard.Services;
using Forms = System.Windows.Forms;

namespace PcDashboard;

public partial class MainWindow : Window
{
    private const int HistoryLimit = 60;
    private const int OverlayHotKeyId = 0x5044;
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkO = 0x4F;

    private readonly SensorService _sensors = new();
    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<SensorRow> _rows = new();
    private readonly List<double> _cpuHistory = new();
    private readonly List<double> _gpuHistory = new();

    private AppSettings _settings;
    private Forms.NotifyIcon _tray = null!;
    private CsvLogger? _csvLogger;
    private OverlayWindow? _overlay;
    private HwndSource? _hwndSource;
    private bool _busy;
    private bool _isExiting;
    private DateTime _lastCpuAlert = DateTime.MinValue;
    private DateTime _lastGpuAlert = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        SensorGrid.ItemsSource = _rows;

        _settings = SettingsStore.Load();
        SetupTray();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshAsync();

        Loaded += async (_, _) =>
        {
            ApplySettings();
            _timer.Start();
            await RefreshAsync();
        };

        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
        SourceInitialized += MainWindow_SourceInitialized;
    }

    private void SetupTray()
    {
        var executable = Environment.ProcessPath;
        var trayIcon = executable is null
            ? System.Drawing.SystemIcons.Application
            : System.Drawing.Icon.ExtractAssociatedIcon(executable) ?? System.Drawing.SystemIcons.Application;

        _tray = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "PcDashboard",
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowFromTray());
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add("Toggle overlay (Ctrl+Alt+O)", null, (_, _) => ToggleOverlay());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowFromTray();
    }

    private void ApplySettings()
    {
        _timer.Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.RefreshSeconds, 1, 10));
        Topmost = _settings.AlwaysOnTop;

        if (_settings.CsvLogging)
        {
            _csvLogger ??= new CsvLogger();
        }
        else
        {
            _csvLogger = null;
        }

        StartupService.SetEnabled(_settings.StartWithWindows);
        ApplyOverlaySettings();
        RenderChart();
    }

    private void ApplyOverlaySettings()
    {
        if (!_settings.OverlayEnabled)
        {
            _overlay?.Hide();
            return;
        }

        if (_overlay is null)
        {
            _overlay = new OverlayWindow();
            _overlay.Closed += (_, _) => _overlay = null;
        }

        _overlay.SetCorner(_settings.OverlayCorner);
        _overlay.SetFontSize(_settings.OverlayFontSize);

        if (!_overlay.IsVisible)
        {
            _overlay.Show();
        }
    }

    private void ToggleOverlay()
    {
        _settings.OverlayEnabled = !_settings.OverlayEnabled;
        SettingsStore.Save(_settings);
        ApplyOverlaySettings();
        StatusText.Text = _settings.OverlayEnabled ? "overlay on" : "overlay off";
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void OpenSettings()
    {
        var dialog = new SettingsWindow(_settings)
        {
            Owner = IsVisible ? this : null
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _settings = dialog.Result;
        SettingsStore.Save(_settings);
        ApplySettings();
        StatusText.Text = "settings saved";
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = _settings.AlwaysOnTop;
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _timer.Stop();
        _tray.Visible = false;
        Close();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExiting || !_settings.MinimizeToTray || Environment.HasShutdownStarted)
        {
            Cleanup();
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void Cleanup()
    {
        _timer.Stop();

        _tray.Visible = false;
        _tray.Dispose();

        _overlay?.Close();
        _overlay = null;

        _sensors.Dispose();

        if (_hwndSource is not null)
        {
            UnregisterHotKey(_hwndSource.Handle, OverlayHotKeyId);
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
        {
            Hide();
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        if (_hwndSource is null)
        {
            return;
        }

        _hwndSource.AddHook(WndProc);
        RegisterHotKey(_hwndSource.Handle, OverlayHotKeyId, ModControl | ModAlt, VkO);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == OverlayHotKeyId)
        {
            ToggleOverlay();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private async Task RefreshAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            var snapshot = await Task.Run(_sensors.Read);
            Apply(snapshot);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"sensor error: {ex.Message}";
        }
        finally
        {
            _busy = false;
        }
    }

    private void Apply(SensorSnapshot snapshot)
    {
        StatusText.Text = $"updated {snapshot.Timestamp:HH:mm:ss} • {snapshot.Readings.Count} sensors";

        var readings = snapshot.Readings;
        var cpuTemp = Max(readings, HardwareType.Cpu, SensorType.Temperature);
        var gpuTemp = MaxGpu(readings, SensorType.Temperature);
        var cpuLoad = FindLoad(readings, HardwareType.Cpu);
        var gpuLoad = FindGpuLoad(readings);
        var cpuPower = Max(readings, HardwareType.Cpu, SensorType.Power);
        var diskTemp = MaxStorageTemp(readings);

        CpuTempText.Text = FormatTemperature(cpuTemp, 1);
        GpuTempText.Text = FormatTemperature(gpuTemp, 1);
        CpuLoadText.Text = Format(cpuLoad, "%", 0);
        GpuLoadText.Text = Format(gpuLoad, "%", 0);
        CpuPowerText.Text = Format(cpuPower, "W", 1);
        DiskTempText.Text = FormatTemperature(diskTemp, 1);

        var memUsed = FindByName(readings, HardwareType.Memory, SensorType.Data, "Memory Used");
        var memAvailable = FindByName(readings, HardwareType.Memory, SensorType.Data, "Memory Available");
        float? memTotal = null;
        if (memUsed is not null && memAvailable is not null && memUsed.Value + memAvailable.Value > 0)
        {
            memTotal = memUsed.Value + memAvailable.Value;
            RamText.Text = $"{memUsed.Value:0.0} / {memTotal.Value:0.0} GB ({memUsed.Value / memTotal.Value * 100:0}%)";
        }
        else
        {
            RamText.Text = "—";
        }

        var vramUsed = FindByName(readings, HardwareType.GpuNvidia, SensorType.SmallData, "GPU Memory Used");
        var vramTotal = FindByName(readings, HardwareType.GpuNvidia, SensorType.SmallData, "GPU Memory Total");
        if (vramUsed is not null && vramTotal is not null && vramTotal.Value > 0)
        {
            VramText.Text = $"{vramUsed.Value:0} / {vramTotal.Value:0} MB ({vramUsed.Value / vramTotal.Value * 100:0}%)";
        }
        else
        {
            VramText.Text = "—";
        }

        if (cpuTemp.HasValue)
        {
            AddHistory(_cpuHistory, cpuTemp.Value);
        }

        if (gpuTemp.HasValue)
        {
            AddHistory(_gpuHistory, gpuTemp.Value);
        }

        CheckTemperatureAlerts(cpuTemp, gpuTemp);
        _csvLogger?.Append(
            snapshot.Timestamp,
            cpuTemp,
            cpuLoad,
            cpuPower,
            gpuTemp,
            gpuLoad,
            memUsed?.Value,
            memTotal,
            vramUsed?.Value,
            vramTotal?.Value,
            diskTemp);

        _overlay?.UpdateValues(
            cpuTemp,
            cpuLoad,
            cpuPower,
            gpuTemp,
            gpuLoad,
            memUsed?.Value,
            memTotal,
            diskTemp,
            _settings.Fahrenheit);

        RenderChart();
        UpdateRows(readings);
    }

    private void CheckTemperatureAlerts(float? cpuTemp, float? gpuTemp)
    {
        var now = DateTime.UtcNow;

        if (cpuTemp >= _settings.CpuTempThreshold && now - _lastCpuAlert > TimeSpan.FromMinutes(5))
        {
            _lastCpuAlert = now;
            _tray.ShowBalloonTip(
                5000,
                "CPU temperature",
                $"CPU is at {FormatTemperature(cpuTemp, 0)} (threshold {FormatThreshold(_settings.CpuTempThreshold)}).",
                Forms.ToolTipIcon.Warning);
        }

        if (gpuTemp >= _settings.GpuTempThreshold && now - _lastGpuAlert > TimeSpan.FromMinutes(5))
        {
            _lastGpuAlert = now;
            _tray.ShowBalloonTip(
                5000,
                "GPU temperature",
                $"GPU is at {FormatTemperature(gpuTemp, 0)} (threshold {FormatThreshold(_settings.GpuTempThreshold)}).",
                Forms.ToolTipIcon.Warning);
        }
    }

    private string FormatThreshold(double celsius)
    {
        return _settings.Fahrenheit
            ? $"{celsius * 9 / 5 + 32:0} °F"
            : $"{celsius:0} °C";
    }

    private string FormatTemperature(float? celsius, int decimals)
    {
        if (!celsius.HasValue)
        {
            return "—";
        }

        var value = _settings.Fahrenheit ? celsius.Value * 9 / 5 + 32 : celsius.Value;
        var unit = _settings.Fahrenheit ? "°F" : "°C";
        return $"{value.ToString($"F{decimals}")} {unit}";
    }

    private static void AddHistory(List<double> history, double value)
    {
        history.Add(value);
        if (history.Count > HistoryLimit)
        {
            history.RemoveAt(0);
        }
    }

    private static float? Max(IReadOnlyList<SensorReading> readings, HardwareType hardwareType, SensorType sensorType)
    {
        return readings
            .Where(r => r.HardwareType == hardwareType && r.SensorType == sensorType)
            .Select(r => (float?)r.Value)
            .Max();
    }

    private static float? MaxGpu(IReadOnlyList<SensorReading> readings, SensorType sensorType)
    {
        return readings
            .Where(r => IsGpu(r.HardwareType) && r.SensorType == sensorType)
            .Select(r => (float?)r.Value)
            .Max();
    }

    private static float? FindLoad(IReadOnlyList<SensorReading> readings, HardwareType hardwareType)
    {
        var loads = readings
            .Where(r => r.HardwareType == hardwareType && r.SensorType == SensorType.Load)
            .ToList();

        var total = loads.FirstOrDefault(r => r.Name.Contains("Total", StringComparison.OrdinalIgnoreCase));
        return total?.Value ?? loads.Select(r => (float?)r.Value).Max();
    }

    private static float? FindGpuLoad(IReadOnlyList<SensorReading> readings)
    {
        var loads = readings
            .Where(r => IsGpu(r.HardwareType) && r.SensorType == SensorType.Load)
            .ToList();

        var core = loads.FirstOrDefault(r => r.Name.Contains("Core", StringComparison.OrdinalIgnoreCase));
        return core?.Value ?? loads.Select(r => (float?)r.Value).Max();
    }

    private static SensorReading? FindByName(
        IReadOnlyList<SensorReading> readings,
        HardwareType hardwareType,
        SensorType sensorType,
        string nameContains)
    {
        return readings.FirstOrDefault(r =>
            r.HardwareType == hardwareType &&
            r.SensorType == sensorType &&
            r.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
    }

    private static float? MaxStorageTemp(IReadOnlyList<SensorReading> readings)
    {
        return readings
            .Where(r => r.HardwareType == HardwareType.Storage && r.SensorType == SensorType.Temperature)
            .Select(r => (float?)r.Value)
            .Max();
    }

    private static bool IsGpu(HardwareType type)
    {
        return type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
    }

    private static string Format(float? value, string unit, int decimals)
    {
        return value.HasValue ? $"{value.Value.ToString($"F{decimals}")} {unit}" : "—";
    }

    private void UpdateRows(IReadOnlyList<SensorReading> readings)
    {
        _rows.Clear();

        foreach (var reading in readings
                     .OrderBy(r => r.HardwareType.ToString())
                     .ThenBy(r => r.Hardware)
                     .ThenBy(r => r.SensorType.ToString())
                     .ThenBy(r => r.Name))
        {
            _rows.Add(new SensorRow
            {
                Hardware = reading.Hardware,
                Type = reading.HardwareType.ToString(),
                Sensor = $"{reading.Name} [{reading.SensorType}]",
                Value = $"{reading.Value:0.##} {reading.Unit}".Trim()
            });
        }
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderChart();
    }

    private void RenderChart()
    {
        var width = ChartCanvas.ActualWidth;
        var height = ChartCanvas.ActualHeight;
        if (width <= 1 || height <= 1)
        {
            return;
        }

        GridTop.X1 = 0;
        GridTop.X2 = width;
        GridTop.Y1 = 0;
        GridTop.Y2 = 0;

        GridMid.X1 = 0;
        GridMid.X2 = width;
        GridMid.Y1 = height / 2;
        GridMid.Y2 = height / 2;

        GridBottom.X1 = 0;
        GridBottom.X2 = width;
        GridBottom.Y1 = height;
        GridBottom.Y2 = height;

        ChartMaxLabel.Text = _settings.Fahrenheit ? "212°F" : "100°C";
        ChartMidLabel.Text = _settings.Fahrenheit ? "122°F" : "50°C";
        ChartMinLabel.Text = _settings.Fahrenheit ? "32°F" : "0°C";

        Canvas.SetLeft(ChartMaxLabel, Math.Max(0, width - 42));
        Canvas.SetTop(ChartMaxLabel, 0);
        Canvas.SetLeft(ChartMidLabel, Math.Max(0, width - 38));
        Canvas.SetTop(ChartMidLabel, height / 2 - 12);
        Canvas.SetLeft(ChartMinLabel, Math.Max(0, width - 30));
        Canvas.SetTop(ChartMinLabel, height - 14);

        CpuLine.Points = BuildPoints(_cpuHistory, width, height, _settings.Fahrenheit);
        GpuLine.Points = BuildPoints(_gpuHistory, width, height, _settings.Fahrenheit);
    }

    private static PointCollection BuildPoints(
        IReadOnlyList<double> values,
        double width,
        double height,
        bool fahrenheit)
    {
        var points = new PointCollection();
        if (values.Count == 0)
        {
            return points;
        }

        var maxTemperature = fahrenheit ? 212.0 : 100.0;
        var step = values.Count > 1 ? width / (values.Count - 1) : width;

        for (var i = 0; i < values.Count; i++)
        {
            var value = fahrenheit ? values[i] * 9 / 5 + 32 : values[i];
            var x = i * step;
            var y = height - Math.Clamp(value / maxTemperature, 0.0, 1.0) * height;
            points.Add(new System.Windows.Point(x, y));
        }

        return points;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

public sealed class SensorRow
{
    public string Hardware { get; init; } = "";
    public string Type { get; init; } = "";
    public string Sensor { get; init; } = "";
    public string Value { get; init; } = "";
}
