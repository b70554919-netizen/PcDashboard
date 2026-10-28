using LibreHardwareMonitor.Hardware;

namespace PcDashboard.Models;

public sealed record SensorReading(
    string Hardware,
    HardwareType HardwareType,
    string Name,
    SensorType SensorType,
    float Value,
    string Unit);

public sealed record SensorSnapshot(DateTime Timestamp, IReadOnlyList<SensorReading> Readings);
