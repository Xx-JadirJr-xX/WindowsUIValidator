<#
.SYNOPSIS
    Runs the validator inside Windows Sandbox, isolated from the host display configuration.

.DESCRIPTION
    Publishes a self-contained build of the validator, maps it together with the application
    folder and the configuration into a disposable Windows Sandbox and runs it there.
    Resolution and DPI changes only affect the sandbox, so the host desktop is never touched.
    Results are written back to -OutputDirectory on the host.

    Inside the sandbox the application folder is available at C:\sandbox\app, so the config
    must reference the executable as e.g.  app: { path: "C:/sandbox/app/MyApp.exe" }.
    The application must be self-contained or its runtime must be installed by -SetupCommand.

    Requires the "Windows Sandbox" optional feature (Windows 10/11 Pro or Enterprise).

.EXAMPLE
    ./scripts/Invoke-SandboxRun.ps1 -AppDirectory C:\build\MyApp -Config .\configs\myapp-sandbox.yaml
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $AppDirectory,
    [Parameter(Mandatory)] [string] $Config,
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\ui-results\sandbox'),
    [string] $SetupCommand = '',
    [switch] $KeepOpen
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$work = Join-Path $root 'artifacts\sandbox'
$tool = Join-Path $work 'tool'
New-Item -ItemType Directory -Force -Path $tool, $OutputDirectory | Out-Null

Write-Host 'Publishing self-contained validator...'
dotnet publish (Join-Path $root 'src\UIValidator.Cli') -c Release -r win-x64 --self-contained -o $tool | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Copy-Item $Config (Join-Path $work 'config.yaml') -Force

$shutdown = if ($KeepOpen) { '' } else { 'shutdown /s /t 5' }
@"
@echo off
$SetupCommand
C:\sandbox\work\tool\uivalidator.exe run C:\sandbox\work\config.yaml --out C:\sandbox\out\$(Get-Date -Format 'yyyyMMdd-HHmmss') > C:\sandbox\out\console.log 2>&1
$shutdown
"@ | Set-Content -Encoding ascii (Join-Path $work 'run.cmd')

$wsb = Join-Path $work 'validator.wsb'
@"
<Configuration>
  <VGpu>Enable</VGpu>
  <Networking>Disable</Networking>
  <MappedFolders>
    <MappedFolder><HostFolder>$work</HostFolder><SandboxFolder>C:\sandbox\work</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$(Resolve-Path $AppDirectory)</HostFolder><SandboxFolder>C:\sandbox\app</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$(Resolve-Path $OutputDirectory)</HostFolder><SandboxFolder>C:\sandbox\out</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>C:\sandbox\work\run.cmd</Command></LogonCommand>
</Configuration>
"@ | Set-Content -Encoding utf8 $wsb

Write-Host "Starting Windows Sandbox; results will appear in $OutputDirectory"
Start-Process $wsb
