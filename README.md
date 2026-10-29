# Windows UI Validator

Automated layout testing for Windows desktop applications (Win32, WinForms, WPF, UWP/WinUI)
across display resolutions, DPI scales and window sizes.

> Full documentation is written as the phases are completed; see `docs/` and the sections below.

## Requirements

- Windows 10 2004+ / Windows 11
- .NET 8 SDK

## Build

```bash
dotnet build -c Release
```

## Quick start

```bash
dotnet build tests/SampleApp -c Release
dotnet run --project src/UIValidator.Cli -c Release -- validate configs/example.yaml
```
