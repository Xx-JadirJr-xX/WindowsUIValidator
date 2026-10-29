# Windows UI Validator

Automated layout testing for Windows desktop applications (Win32, WinForms, WPF, UWP/WinUI)
across **display resolutions**, **DPI scales** and **window sizes**.

The validator launches your application, puts it in every combination of the scenario matrix,
navigates to the screens you describe, captures a screenshot and the UI Automation tree, and
detects layout problems. Everything ends up in a single, self-contained **HTML report**.

## What it detects

| Rule | Severity | Description |
|---|---|---|
| `clipping` | Error | Controls partially cut off by their container or the window client area |
| `offscreen` | Error | Controls lying entirely outside the visible area (unreachable) |
| `overlap` | Error / Warning | Sibling controls covering each other |
| `textTruncation` | Warning | Captions that do not fit their control (font measurement, optional OCR) |
| `minSize` | Warning | Clickable targets smaller than a DPI-scaled minimum |
| `windowOnScreen` | Error | Window larger than the work area, or title bar unreachable |
| `visualDiff` | Error | Screenshot differs from the approved baseline (SSIM + changed-pixel ratio) |

## Requirements

- Windows 10 2004+ or Windows 11
- .NET 8 SDK (build), .NET 8 Desktop Runtime (run)

## Quick start

```bash
dotnet build -c Release
```

```bash
./src/UIValidator.Cli/bin/Release/net8.0-windows10.0.19041.0/uivalidator.exe run configs/example.yaml --open
```

The bundled [sample app](tests/SampleApp) contains deliberately planted bugs, so the run reports
failures. The report is written to `ui-results/<timestamp>/report.html`.

> **Do not use the mouse or keyboard while a run is in progress.** The validator brings the
> window under test to the foreground and sends real input to it; your keystrokes would end up
> in the application. Resolution and scale changes affect the **primary monitor**, and the
> window under test is always moved there.

## Commands

| Command | Description |
|---|---|
| `run <config> [--out dir] [--open] [--update-baselines] [--compare dir\|latest] [--fail-on warning]` | Run the matrix and write the report |
| `validate <config>` | Check a configuration file |
| `approve <run-dir> [--filter text] [--baselines dir]` | Accept a run's screenshots as visual baselines |
| `report <run-dir> [--embed]` | Regenerate `report.html` from `results.json` |
| `displays` | List resolutions and scales supported by the primary display |
| `restore-display` | Restore the display after an interrupted run |

Exit codes: `0` passed (warnings allowed), `1` failures found, `2` invalid configuration, `3` run errors.

## Configuration

```yaml
app:
  path: "C:/MyApp/MyApp.exe"
matrix:
  resolutions: [1366x768, 1920x1080]
  dpiScales: [100, 150]
  windowSizes: [maximized, 1024x768]
screens:
  - name: Main
  - name: Settings
    window: "Settings"
    steps:
      - click: { automationId: btnSettings }
      - waitFor: { name: "Settings", controlType: Window }
```

See [docs/configuration.md](docs/configuration.md) for every option.

## The report

`report.html` is a single file (screenshots embedded by default) that opens in any browser
without a server:

- **Summary cards**: checks, passed, warnings, failed, errors; click to filter.
- **Matrix**: scenarios × screens with a status pill per cell.
- **By problem**: identical problems grouped across scenarios, so you can see that, for example,
  `btnOk` is clipped in 6 of 12 scenarios. Problems new since the compared run are tagged **NEW**.
- **All findings**: a flat, filterable list.
- **Detail view** (click a cell): screenshot with problem overlays, baseline / compare-slider /
  difference views, the finding list (hover to highlight), and the UI Automation tree (hover to
  locate the element). Navigate with ← and →; close with Esc.

`results.json` (machine-readable) and `junit.xml` (CI) are written alongside it.

## Visual regression

```bash
uivalidator run configs/example.yaml --update-baselines
```

```bash
uivalidator approve ui-results/20261028-120000 --filter 1920x1080
```

Baselines are PNG files in `output.baselineDirectory`, one per scenario and screen. Commit them
to share them with your team and CI.

## Safety of display changes

- Resolution changes are **dynamic** (never written to the registry): a reboot or log-off always
  returns to your settings.
- The original resolution and scale are saved to `%LOCALAPPDATA%\UIValidator\display-state.json`
  before the first change and restored at the end, on Ctrl+C, on process exit and, if the process
  was killed, at the start of the next run (or with `uivalidator restore-display`).
- Changing the display scale uses the same undocumented DisplayConfig API as the Settings app.
  It works on current Windows 10/11 builds, but has no public contract; for fully reproducible
  CI, run each scale in its own VM or [Windows Sandbox](docs/ci.md#windows-sandbox).

## Project layout

```
src/UIValidator.Core/       Engine: config, display, automation, capture, rules, visual, reporting
src/UIValidator.Cli/        uivalidator command-line tool
tests/UIValidator.Tests/    Unit tests (rules, comparer, report, config)
tests/SampleApp/            WinForms app with planted layout bugs
configs/                    Example and CI configurations
scripts/                    Windows Sandbox runner
docs/                       Configuration and CI guides
```

## Limitations

- Rules rely on UI Automation. Applications that draw their own UI without accessibility support
  (games, some custom-rendered UIs) expose few elements; only visual diff and OCR apply to them.
- Text truncation uses an assumed font (Segoe UI 9pt by default), because UI Automation does not
  expose the font of most controls. Findings are therefore warnings; enable OCR for confirmation.
- Only the primary monitor is controlled.
