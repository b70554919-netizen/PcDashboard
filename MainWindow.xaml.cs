using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;
using PcDashboard.Models;
using PcDashboard.Services;

namespace PcDashboard;

public partial class MainWindow : Window
{
    private const int HistoryLimit = 60;

    private readonly SensorService _sensors = new();
    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<SensorRow> _rows = new();
    private readonly List<double> _cpuHistory = new();
    private readonly List<double> _gpuHistory = new();
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        SensorGrid.ItemsSource = _rows;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshAsync();

        Loaded += async (_, _) =>
        {
            _timer.Start();
            await RefreshAsync();
        };

        Closed += (_, _) =>
        {
            _timer.Stop();
            _sensors.Dispose();
        };
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

        CpuTempText.Text = Format(cpuTemp, "°C", 1);
        GpuTempText.Text = Format(gpuTemp, "°C", 1);
        CpuLoadText.Text = Format(cpuLoad, "%", 0);
        GpuLoadText.Text = Format(gpuLoad, "%", 0);
        CpuPowerText.Text = Format(cpuPower, "W", 1);

        var memUsed = FindByName(readings, HardwareType.Memory, SensorType.Data, "Memory Used");
        var memAvailable = FindByName(readings, HardwareType.Memory, SensorType.Data, "Memory Available");
        if (memUsed is not null && memAvailable is not null && memUsed.Value + memAvailable.Value > 0)
        {
            var total = memUsed.Value + memAvailable.Value;
            RamText.Text = $"{memUsed.Value:0.0} / {total:0.0} GB ({memUsed.Value / total * 100:0}%)";
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

        DiskTempText.Text = Format(MaxStorageTemp(readings), "°C", 1);

        if (cpuTemp.HasValue)
        {
            AddHistory(_cpuHistory, cpuTemp.Value);
        }

        if (gpuTemp.HasValue)
        {
            AddHistory(_gpuHistory, gpuTemp.Value);
        }

        RenderChart();
        UpdateRows(readings);
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

        CpuLine.Points = BuildPoints(_cpuHistory, width, height);
        GpuLine.Points = BuildPoints(_gpuHistory, width, height);
    }

    private static PointCollection BuildPoints(IReadOnlyList<double> values, double width, double height)
    {
        var points = new PointCollection();
        if (values.Count == 0)
        {
            return points;
        }

        const double maxTemperature = 100.0;
        var step = values.Count > 1 ? width / (values.Count - 1) : width;

        for (var i = 0; i < values.Count; i++)
        {
            var x = i * step;
            var y = height - Math.Clamp(values[i] / maxTemperature, 0.0, 1.0) * height;
            points.Add(new Point(x, y));
        }

        return points;
    }
}

public sealed class SensorRow
{
    public string Hardware { get; init; } = "";
    public string Type { get; init; } = "";
    public string Sensor { get; init; } = "";
    public string Value { get; init; } = "";
}
