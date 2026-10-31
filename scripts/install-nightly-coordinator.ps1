# OPS-COORDINATOR-01. Construct with -Preview; registration is a separate operator action.
[CmdletBinding()]
param(
    [string]$PythonPath,
    [switch]$Preview,
    [switch]$Enable,
    [switch]$Publish,
    [ValidateRange(1, 20)][int]$MaxJobs = 4,
    [ValidateRange(1, 40)][int]$MaxModelCalls = 8,
    [ValidateRange(0, 8)][double]$Hours = 8,
    [ValidateRange(1, 900)][int]$Timeout = 900
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $PythonPath) {
    $PythonPath = (& py -c 'import sys; print(sys.executable)')
    if ($LASTEXITCODE -ne 0) { throw 'Python discovery failed.' }
}
$python = (Resolve-Path -LiteralPath $PythonPath).Path
# pythonw runs without a console; do not add a shell or password-bearing wrapper.
if ([IO.Path]::GetFileName($python) -ieq 'python.exe') {
    $python = Join-Path ([IO.Path]::GetDirectoryName($python)) 'pythonw.exe'
}
if (-not (Test-Path -LiteralPath $python -PathType Leaf) -or
    [IO.Path]::GetFileName($python) -ine 'pythonw.exe') { throw 'An absolute pythonw.exe path is required.' }
$watch = (Resolve-Path -LiteralPath (Join-Path $repo 'scripts/nightly_coordinator.py')).Path
if ($python.Contains('"') -or $watch.Contains('"')) { throw 'Invalid executable path.' }
$arguments = '"{0}" --once --max-jobs {1} --max-model-calls {2} --hours {3} --timeout {4}' -f $watch, $MaxJobs, $MaxModelCalls, $Hours.ToString([Globalization.CultureInfo]::InvariantCulture), $Timeout
if ($Enable) { $arguments += ' --enable' }
if ($Publish) { $arguments += ' --publish' }
$action = New-ScheduledTaskAction -Execute $python -Argument $arguments -WorkingDirectory $repo
$trigger = New-ScheduledTaskTrigger -Once -At ((Get-Date).AddMinutes(1)) -RepetitionInterval (New-TimeSpan -Minutes 10)
$principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 45)
$definition = New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings
if ($Preview) { $definition; return }
Register-ScheduledTask -TaskName 'Poseidon-NightlyCoordinator' -InputObject $definition
