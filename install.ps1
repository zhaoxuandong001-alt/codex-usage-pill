[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$taskName = 'Codex Usage Pill - Start with Codex'
$sourcePath = Join-Path $PSScriptRoot 'CodexUsagePill.exe'
if (-not (Test-Path -LiteralPath $sourcePath)) {
    $sourcePath = Join-Path $PSScriptRoot 'dist\CodexUsagePill.exe'
}
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw 'CodexUsagePill.exe must be in the same folder as install.ps1.'
}

$installDirectory = Join-Path $env:LOCALAPPDATA 'CodexUsagePill'
$installedPath = Join-Path $installDirectory 'CodexUsagePill.exe'
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null

Get-CimInstance Win32_Process |
    Where-Object { $_.ExecutablePath -eq $installedPath } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
$deadline = (Get-Date).AddSeconds(5)
do {
    Start-Sleep -Milliseconds 100
    $running = Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $installedPath }
} while ($running -and (Get-Date) -lt $deadline)
Copy-Item -LiteralPath $sourcePath -Destination $installedPath -Force

$shell = New-Object -ComObject WScript.Shell
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Codex Usage Pill.lnk'
$shortcut = $shell.CreateShortcut($desktopShortcut)
$shortcut.TargetPath = $installedPath
$shortcut.WorkingDirectory = $installDirectory
$shortcut.IconLocation = "$installedPath,0"
$shortcut.Description = 'Show Codex usage remaining'
$shortcut.Save()

$oldStartupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'Codex Usage Pill.lnk'
if (Test-Path -LiteralPath $oldStartupShortcut) {
    Remove-Item -LiteralPath $oldStartupShortcut -Force
}

$subscription = @"
<QueryList><Query Id="0" Path="Microsoft-Windows-AppModel-Runtime/Admin"><Select Path="Microsoft-Windows-AppModel-Runtime/Admin">*[System[Provider[@Name='Microsoft-Windows-AppModel-Runtime'] and (EventID=201)]] and *[EventData[Data[@Name='ApplicationName']='OpenAI.Codex_2p2nqsd0c76g0!App' and Data[@Name='Message']='[LaunchProcess]']]</Select></Query></QueryList>
"@

$service = New-Object -ComObject 'Schedule.Service'
$service.Connect()
$root = $service.GetFolder('\')
$definition = $service.NewTask(0)
$definition.RegistrationInfo.Description = 'Starts Codex Usage Pill only when the Codex desktop app launches.'
$definition.RegistrationInfo.Author = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$definition.Settings.Enabled = $true
$definition.Settings.AllowDemandStart = $true
$definition.Settings.StartWhenAvailable = $false
$definition.Settings.DisallowStartIfOnBatteries = $false
$definition.Settings.StopIfGoingOnBatteries = $false
$definition.Settings.ExecutionTimeLimit = 'PT0S'
$definition.Settings.MultipleInstances = 2
$definition.Principal.UserId = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$definition.Principal.LogonType = 3
$definition.Principal.RunLevel = 0

$trigger = $definition.Triggers.Create(0)
$trigger.Enabled = $true
$trigger.Subscription = $subscription.Trim()

$action = $definition.Actions.Create(0)
$action.Path = $installedPath
$action.WorkingDirectory = $installDirectory
$root.RegisterTaskDefinition($taskName, $definition, 6, $null, $null, 3, $null) | Out-Null

$codexRunning = Get-Process -Name ChatGPT -ErrorAction SilentlyContinue | Where-Object {
    try { $_.MainModule.FileName -like '*\OpenAI.Codex_*' } catch { $false }
}
if ($codexRunning) {
    Start-ScheduledTask -TaskName $taskName
}

Write-Host "Installed $installedPath"
Write-Host "Registered task: $taskName"
Write-Host "Desktop shortcut: $desktopShortcut"
