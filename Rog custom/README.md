# ROG Custom (PCBooster)

A lightweight, open-source alternative to ASUS Armoury Crate for Windows. ROG Custom provides real-time hardware monitoring, performance mode switching, GPU overclocking, fan curve control, and GPU stress testing through a modern hybrid WPF + WebView2 architecture.

## Features

### Performance Mode Switching
- **6 modes**: Silent, Windows, Balanced, Performance, Turbo, Manual
- Each mode configures: Windows power plan, CPU boost policy, max processor state, core parking, fan curve, GPU power limit
- Profiles persisted as JSON with schema versioning and automatic migration

### Real-Time Hardware Monitoring
- CPU and GPU temperatures, usage, power draw, and clock speeds
- RAM and VRAM usage tracking
- CPU and GPU fan RPM monitoring
- Live 60-second performance telemetry graph in the UI
- Uses LibreHardwareMonitor with fallback to PerformanceCounter / kernel32 when the LHM driver is blocked

### GPU Overclocking (NVIDIA)
- Lock GPU core clocks and memory clocks via nvidia-smi
- Hard safety caps: +100MHz core, +300MHz memory
- Set GPU power limit within hardware min/max bounds
- Reset all overclocks to defaults
- **AI OC Scanner**: Automated scan that steps clock by 10MHz increments, reads real-time clocks, detects throttling, and finds max stable frequency

### Fan Curve Control (FanControl Bridge)
- Auto-detects FanControl installation
- Hot-switches profiles via FanControl CLI
- Falls back to config file swap + FanControl restart
- Custom fan curve support via JSON

### GPU Stress Test
- Launches WinSAT D3D workload for real GPU load
- Real-time temperature monitoring during test with CSV logging
- Thermal kill-switch at configurable max temperature
- Throttle detection when GPU clock drops >15% or temp exceeds 88C
- Calculates a "RIG Score" based on usage, clocks, temps, and throttle events

### Game Auto-Detection
- Polls running processes every 5 seconds against a list of popular games
- Automatically switches to Performance mode when a game is detected
- Reverts to Windows mode when the game closes

### Profile Management
- Create, save, and delete named GPU profiles
- Each profile stores: GPU core offset, memory offset, power limit, fan curve
- Apply profiles to instantly configure all GPU and fan settings

### Process Manager
- Lists top 25 processes by memory usage
- Shows system info: OS version, CPU cores, machine name, uptime
- Kill processes by PID

## Architecture

Built in **.NET 8 / C# with WPF**, using **WebView2** to render a modern HTML/CSS/JS frontend. The C# backend handles all hardware interactions via COM interop exposed to the JavaScript UI through `AddHostObjectToScript`.

### Solution Structure

| Project | Purpose |
|---------|---------|
| `RogCustom.Core` | Domain models: PerformanceMode enum, PerformanceProfile, ProfileStore, config helpers |
| `RogCustom.Hardware` | All hardware services: HardwareMonitor, GPU control, fan bridge, CPU boost, power plans, game detection, stress test |
| `RogCustom.App` | WPF application with WebView2, ViewModels, Views, InteropWrapper, DI setup |
| `RogCustom.ConsolePoC` | Console proof-of-concept for testing |

## Quick Start

### Prerequisites
- Windows 10/11
- .NET 8.0 SDK
- WebView2 Runtime (included in Windows 11, downloadable for Windows 10)
- NVIDIA GPU + nvidia-smi (for GPU overclocking features)
- FanControl (optional, for fan curve management)

### Build and Run

```powershell
# Navigate to the source directory
cd "Rog custom/src"

# Build the solution
dotnet build RogCustom.sln

# Run the application (requires Administrator for hardware access)
dotnet run --project RogCustom.App
```

### Using the Pre-built Executable

```powershell
# Run the batch file (requests admin elevation automatically)
cd "Rog custom"
run.bat
```

## UI Design
- Dark ROG-themed design with gradient accents
- Mode-dependent particle effects (snowflakes, orbs, embers, warp stars)
- Sections: Dashboard, AI Assistant, Overclock, Fan Curves
- Toast notifications for user feedback

## System Requirements

### Minimum
- Windows 10 version 1903+
- .NET 8.0 Runtime
- 4GB RAM
- Any GPU (NVIDIA recommended for OC features)

### Recommended
- Windows 11
- NVIDIA GPU with latest drivers
- FanControl installed for fan curve management
- 1920x1080 resolution or higher
