param([ValidateSet('restrict','check','stop')][string]$Action, [string]$Directory, [int]$ProcessId, [string]$Dll)
$ErrorActionPreference = 'Stop'
if ($Action -eq 'restrict') {
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetOwner($sid)
    $acl.SetAccessRuleProtection($true, $false)
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    Set-Acl -LiteralPath $Directory -AclObject $acl
    exit 0
}
$process = Get-CimInstance Win32_Process -Filter "ProcessId=$ProcessId"
if (!$process -or !$process.CommandLine.Contains($Directory) -or !$process.CommandLine.Contains($Dll)) { exit 2 }
if ($Action -eq 'stop') { Stop-Process -Id $process.ProcessId -ErrorAction Stop }
