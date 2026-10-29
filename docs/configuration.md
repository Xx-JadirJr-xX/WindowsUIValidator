# Configuration reference

Configurations are YAML (or JSON with the same structure). Relative paths are resolved against
the directory of the configuration file.

## `app`

| Key | Default | Description |
|---|---|---|
| `path` | *(required)* | Executable to launch |
| `args` | `""` | Command-line arguments |
| `workingDirectory` | exe folder | Working directory |
| `startupTimeoutSec` | `20` | Time to wait for the main window |
| `restartPerScenario` | `true` | Start a fresh process for every scenario × screen. When `false`, the process is reused and each screen is reset by its `teardown` steps (or by closing its dialog) |
| `mainWindowTitle` | first window | Title substring identifying the main window |

The application is always restarted when the display configuration changes.

## `matrix`

Every combination becomes a scenario. Order: resolution → scale → window size.

| Key | Default | Description |
|---|---|---|
| `resolutions` | `[]` (current) | `WIDTHxHEIGHT` values; must be supported by the primary display (`uivalidator displays`) |
| `dpiScales` | `[]` (current) | `100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500`; limited by the resolution |
| `windowSizes` | `[maximized]` | `maximized`, `current` or `WIDTHxHEIGHT` (visible size in physical pixels, centered on the primary monitor) |

A scenario whose resolution or scale cannot be applied is reported as an error and skipped.

## `screens`

| Key | Default | Description |
|---|---|---|
| `name` | `Main` | Column name in the report |
| `window` | `main` | Window to validate: `main`, `foreground`, or a title substring (dialogs included) |
| `steps` | `[]` | Navigation steps run after the window size is applied |
| `teardown` | `[]` | Steps to return to the initial state (only with `restartPerScenario: false`) |

### Steps

Each step has exactly one action:

```yaml
- click: { automationId: btnSave }            # real mouse click
- doubleClick: { name: "Item 1", controlType: ListItem }
- focus: { automationId: txtName }
- type: { target: { automationId: txtName }, text: "Hello" }   # target is optional
- keys: ctrl+s                                # chords: ctrl, alt, shift, win, enter, esc, tab, f1..f24, a..z, 0..9, arrows...
- waitFor: { name: "Settings", controlType: Window }
- wait: 500                                   # milliseconds
```

Optional per step: `timeoutSec` (default 10) for finding the element.

### Selectors

All given properties must match: `automationId`, `name`, `controlType` (UIA control type name,
e.g. `Button`, `Edit`, `Window`), `className`. Elements are searched in every window of the app.

## `rules`

Every rule has `enabled` (default `true`).

| Rule | Options |
|---|---|
| `clipping` | — |
| `offscreen` | — |
| `overlap` | `minIntersectionPx` (4), `controlTypes` (interactive controls and Text) |
| `minSize` | `minWidth`, `minHeight` (16 logical px, scaled by the window DPI) |
| `windowOnScreen` | — |
| `textTruncation` | `useOcr` (false), `ocrLanguage` ("" = user profile), `fontFamily` ("Segoe UI"), `fontSizePt` (9), `tolerancePx` (2), `controlTypes` |
| `visualDiff` | `threshold` (0.98 minimum SSIM), `maxChangedPixelRatio` (0.002), `pixelTolerance` (16 per channel), `maskAutomationIds` |

`ignoreAutomationIds` excludes elements from every rule.

Elements inside scroll containers (lists, trees, grids, documents, menus, toolbars) are not
checked for clipping; internal parts of composite controls (combo boxes, spinners, scroll bars)
are not validated individually; title bar and menu bar chrome is excluded.

## `output`

| Key | Default | Description |
|---|---|---|
| `directory` | `ui-results` | Each run creates a `<timestamp>` folder here (unless `--out` is given) |
| `baselineDirectory` | `baselines` | Approved screenshots for `visualDiff` |
| `embedImages` | `true` | Embed screenshots in `report.html` so it is a single portable file |

## Run artifacts

```
<run>/report.html     interactive report
<run>/results.json    full results (input for `report`, `approve`, `--compare`)
<run>/junit.xml       CI test results
<run>/images/         screenshots, baselines and diffs
<run>/trees/          raw UI Automation trees
```
