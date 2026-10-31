# OPS-DASH-RUNTIME-01. Preview constructs definitions only; never registers.
[CmdletBinding()]
param(
    [string]$PythonPath,
    [switch]$Preview,
    [switch]$Develop,
    [switch]$Publish,
    [switch]$WakeOnly,
    [long]$WakeAt = 0
)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Windows Task Scheduler is required.' }
if ($Publish -and -not $Develop) { throw 'Publish requires Develop.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $PythonPath) {
    $PythonPath = (& py -c 'import sys; print(sys.executable)')
    if ($LASTEXITCODE -ne 0) { throw 'Python discovery failed.' }
}
$python = (Resolve-Path -LiteralPath $PythonPath).Path
if ([IO.Path]::GetFileName($python) -ieq 'python.exe') {
    $python = Join-Path ([IO.Path]::GetDirectoryName($python)) 'pythonw.exe'
}
if (-not (Test-Path -LiteralPath $python -PathType Leaf) -or
    [IO.Path]::GetFileName($python) -ine 'pythonw.exe') { throw 'Absolute pythonw.exe required.' }
$script = (Resolve-Path -LiteralPath (Join-Path $repo 'scripts/ops_runtime.py')).Path
if ($python.Contains('"') -or $script.Contains('"')) { throw 'Invalid path.' }
$principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
if (-not $WakeOnly) {
    $arguments = '"{0}" --serve --manage-wake' -f $script
    if ($Develop) { $arguments += ' --develop' }
    if ($Publish) { $arguments += ' --publish' }
    $action = New-ScheduledTaskAction -Execute $python -Argument $arguments -WorkingDirectory $repo
    $triggers = @((New-ScheduledTaskTrigger -AtLogOn),
        (New-ScheduledTaskTrigger -Once -At ((Get-Date).AddMinutes(1)) -RepetitionInterval (New-TimeSpan -Minutes 5)))
    $settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
    $task = New-ScheduledTask -Action $action -Trigger $triggers -Principal $principal -Settings $settings
    if ($Preview) { $task } else { Register-ScheduledTask -TaskName 'Poseidon-DevelopmentRuntime' -InputObject $task -Force }
}
if ($WakeAt -gt [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) {
    $action = New-ScheduledTaskAction -Execute $python -Argument ('"{0}" --wake-check' -f $script) -WorkingDirectory $repo
    $trigger = New-ScheduledTaskTrigger -Once -At ([DateTimeOffset]::FromUnixTimeSeconds($WakeAt).LocalDateTime)
    $settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable -WakeToRun -ExecutionTimeLimit (New-TimeSpan -Minutes 5)
    $task = New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings
    if ($Preview) { $task } else { Register-ScheduledTask -TaskName 'Poseidon-QuotaReset' -InputObject $task -Force }
} elseif ($WakeOnly -and -not $Preview) {
    if (Get-ScheduledTask -TaskName 'Poseidon-QuotaReset' -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName 'Poseidon-QuotaReset' -Confirm:$false
    }
}
