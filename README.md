# PcDashboard

Windows sensor dashboard built with .NET 8 WPF and LibreHardwareMonitorLib.

## Download

Download the latest installer from the [Releases](https://github.com/b70554919-netizen/PcDashboard/releases) page:

```text
PcDashboard-Setup-0.2.0.exe
```

The installer is self-contained, so users do **not** need to install .NET separately.

The setup includes an optional **Create a desktop shortcut** task and adds a Start Menu shortcut.

## Features

- Live CPU temperature, load, and package power
- Live NVIDIA/AMD/Intel GPU temperature and load
- RAM usage and GPU VRAM usage
- Storage temperatures
- Temperature history chart with axes and grid
- Full sensor table
- Tray icon with open/settings/overlay/exit menu
- Minimize and close to tray
- Settings for refresh interval, alert thresholds, and Fahrenheit
- Overheat notifications with a five-minute cooldown
- Optional CSV logging
- In-game sensor overlay with selectable corner and font size
- Click-through overlay, `Ctrl+Alt+O` toggle
- Start with Windows
- Dark UI
- App icon
- Admin manifest for hardware sensor access

## In-game overlay

The overlay shows CPU/GPU temperature and load, RAM usage, and SSD temperature in a corner of the primary screen.

- Toggle with `Ctrl+Alt+O` or the tray menu.
- Choose top-left, top-right, bottom-left, or bottom-right in Settings.
- Font size is configurable from 10 to 28.
- It works in windowed and borderless fullscreen games.
- Exclusive fullscreen games may cover it; use borderless fullscreen or an external OSD for those.

## Requirements

- Windows 10 or 11 (64-bit)
- Administrator privileges for hardware sensor access

## Build

```powershell
dotnet restore
dotnet build -c Release
```

## Publish and installer

Self-contained publish:

```powershell
dotnet restore -r win-x64
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o publish
```

Build the Inno Setup installer:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\PcDashboard.iss
```

Output:

```text
dist\PcDashboard-Setup-0.2.0.exe
```

## Settings and logs

Settings are stored in:

```text
%AppData%\PcDashboard\settings.json
```

CSV logs are written to:

```text
%AppData%\PcDashboard\sensors.csv
```

CSV columns:

```text
timestamp,cpu_temp_c,cpu_load_pct,cpu_power_w,gpu_temp_c,gpu_load_pct,ram_used_gb,ram_total_gb,vram_used_mb,vram_total_mb,disk_temp_c
```

## Project layout

```text
PcDashboard/
├── .github/workflows/build.yml
├── Models/
│   ├── AppSettings.cs
│   └── SensorSnapshot.cs
├── Services/
│   ├── CsvLogger.cs
│   ├── SensorService.cs
│   ├── SettingsStore.cs
│   └── StartupService.cs
├── installer/
│   └── PcDashboard.iss
├── App.xaml
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── OverlayWindow.xaml
├── OverlayWindow.xaml.cs
├── SettingsWindow.xaml
├── SettingsWindow.xaml.cs
├── app.ico
├── app.manifest
└── PcDashboard.csproj
```

## Roadmap

- Light theme
- Auto-update check
- Per-sensor chart selection
- Process list with CPU and RAM usage
- DirectX/RTSS overlay for exclusive fullscreen

## License

MIT
