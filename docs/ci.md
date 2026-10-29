# Running in CI

UI Automation needs an **interactive desktop session**. Windows services and session 0 cannot
drive UIs.

## GitHub Actions

GitHub-hosted `windows-latest` runners have an interactive session with a fixed virtual
display, so vary **window sizes** there and keep resolution/scale scenarios for self-hosted
runners. See [`.github/workflows/ui-validation.yml`](../.github/workflows/ui-validation.yml):

```yaml
- name: Run UI validation
  shell: pwsh
  run: |
    uivalidator.exe run configs/ci.yaml --out ui-results/ci --compare latest
    if ($LASTEXITCODE -gt 1) { exit $LASTEXITCODE }   # 1 = layout failures; decide your policy

- uses: actions/upload-artifact@v4
  if: always()
  with: { name: ui-validation-report, path: ui-results/ci }
```

Publish `junit.xml` with any JUnit reporter action to get per-scenario annotations.

## Azure DevOps

```yaml
- script: uivalidator.exe run configs/app.yaml --out $(Build.ArtifactStagingDirectory)/ui
- task: PublishTestResults@2
  condition: always()
  inputs: { testResultsFormat: JUnit, testResultsFiles: '$(Build.ArtifactStagingDirectory)/ui/junit.xml' }
- publish: $(Build.ArtifactStagingDirectory)/ui
  condition: always()
  artifact: ui-report
```

## Self-hosted runners

To test real resolutions and scales:

1. Run the agent **as a console application in a logged-in user session** (not as a service),
   with auto-logon and the screen saver/lock disabled.
2. Use a monitor, or a virtual display driver (for example an Indirect Display Driver) to get
   resolutions such as 2560x1440 or 3840x2160 on headless machines.
3. Run `uivalidator displays` once to see which modes and scales are available.

## Windows Sandbox

[`scripts/Invoke-SandboxRun.ps1`](../scripts/Invoke-SandboxRun.ps1) runs the validator inside a
disposable Windows Sandbox, so display changes never touch your desktop:

```powershell
./scripts/Invoke-SandboxRun.ps1 -AppDirectory C:\build\MyApp -Config .\configs\myapp-sandbox.yaml
```

The application folder is mapped to `C:\sandbox\app`; reference the executable from the
configuration as `C:/sandbox/app/MyApp.exe`. The sandbox has no .NET runtime: publish your
app self-contained, or install its prerequisites with `-SetupCommand`.

## Exit codes and policies

| Code | Meaning |
|---|---|
| 0 | No errors (warnings allowed; use `--fail-on warning` to make them fail) |
| 1 | Layout errors found |
| 2 | Invalid configuration |
| 3 | Run errors (app did not start, step failed, scenario not applicable) |

Use `--compare latest` (or a path to a previous run) to tag problems as **NEW** in the report
and see which ones were resolved.
