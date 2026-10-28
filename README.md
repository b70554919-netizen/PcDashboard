# PcDashboard

Windows sensor dashboard built with .NET 8 WPF and LibreHardwareMonitorLib.

## Features

- Live CPU temperature, load, and package power
- Live NVIDIA/AMD/Intel GPU temperature and load
- RAM usage and GPU VRAM usage
- Storage temperatures
- Temperature history chart
- Full sensor table
- Dark UI
- Admin manifest for hardware sensor access

## Requirements

- Windows 10 or 11
- .NET 8 Desktop Runtime
- Administrator privileges
- LibreHardwareMonitorLib 0.9.6

## Build

```powershell
dotnet restore
dotnet build -c Release
```

## Run

```powershell
dotnet run -c Release
```

Or launch the built executable:

```text
bin\Release\net8.0-windows\PcDashboard.exe
```

The app requests administrator rights on startup so LibreHardwareMonitor can read motherboard and CPU sensors.

## Project layout

```text
PcDashboard/
├── Models/
│   └── SensorSnapshot.cs
├── Services/
│   └── SensorService.cs
├── App.xaml
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── app.manifest
└── PcDashboard.csproj
```

## Roadmap

- Tray icon and minimize-to-tray
- Settings for refresh interval, thresholds, and units
- Overheat notifications
- CSV logging
- Chart axes and grid
- Start with Windows
- GitHub Actions build

## License

MIT
