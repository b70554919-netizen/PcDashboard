using LibreHardwareMonitor.Hardware;
using PcDashboard.Models;

namespace PcDashboard.Services;

public sealed class SensorService : IDisposable
{
    private readonly Computer _computer;
    private bool _opened;

    public SensorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsNetworkEnabled = true,
            IsBatteryEnabled = true,
            IsPsuEnabled = true
        };
    }

    public SensorSnapshot Read()
    {
        if (!_opened)
        {
            _computer.Open();
            _opened = true;
        }

        var readings = new List<SensorReading>();
        foreach (var hardware in _computer.Hardware)
        {
            ReadHardware(hardware, readings);
        }

        return new SensorSnapshot(DateTime.Now, readings);
    }

    private static void ReadHardware(IHardware hardware, List<SensorReading> readings)
    {
        try
        {
            hardware.Update();
        }
        catch
        {
        }

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not float value || float.IsNaN(value) || float.IsInfinity(value))
            {
                continue;
            }

            readings.Add(new SensorReading(
                hardware.Name,
                hardware.HardwareType,
                sensor.Name,
                sensor.SensorType,
                value,
                UnitFor(sensor.SensorType)));
        }

        foreach (var subHardware in hardware.SubHardware)
        {
            ReadHardware(subHardware, readings);
        }
    }

    private static string UnitFor(SensorType type) => type switch
    {
        SensorType.Temperature => "°C",
        SensorType.Load => "%",
        SensorType.Power => "W",
        SensorType.Clock => "MHz",
        SensorType.Fan => "RPM",
        SensorType.Voltage => "V",
        SensorType.Current => "A",
        SensorType.Data => "GB",
        SensorType.SmallData => "MB",
        SensorType.Throughput => "B/s",
        SensorType.Level => "%",
        _ => ""
    };

    public void Dispose()
    {
        if (!_opened)
        {
            return;
        }

        _computer.Close();
        _opened = false;
    }
}
