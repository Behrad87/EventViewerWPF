<#
.SYNOPSIS
    Exports Windows Event Viewer logs from a remote server (e.g. 10.131.3.34) for 2026-09-27 to JSON or TXT.

.EXAMPLE
    .\Export-RemoteEventLog.ps1 -ComputerName "10.131.3.34" -LogName "Application" -Format JSON
#>
[CmdletBinding()]
param(
    [string]$ComputerName = "10.131.3.34",
    [string]$LogName = "Application",
    [datetime]$StartTime = [datetime]"2026-09-27 00:00:00",
    [datetime]$EndTime = [datetime]"2026-09-27 23:59:59",
    [ValidateSet("JSON", "TXT", "Both")]
    [string]$Format = "Both",
    [string]$OutputDir = "C:\repos\EventViewerWPF\Exports",
    [System.Management.Automation.PSCredential]$Credential = $null
)

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$dateTag = $StartTime.ToString("yyyyMMdd")
$safeComputer = $ComputerName -replace '[\.\\/:]', '_'
$baseFile = Join-Path $OutputDir "EventLog_${LogName}_${dateTag}_${safeComputer}"

Write-Host "Querying Event Log '$LogName' on '$ComputerName' from $StartTime to $EndTime..." -ForegroundColor Cyan

$filter = @{
    LogName = $LogName
    StartTime = $StartTime
    EndTime = $EndTime
}

try {
    $params = @{
        FilterHashtable = $filter
        ErrorAction = 'Stop'
    }

    if ($ComputerName -and $ComputerName -ne "localhost" -and $ComputerName -ne ".") {
        $params['ComputerName'] = $ComputerName
    }
    if ($Credential) {
        $params['Credential'] = $Credential
    }

    $events = Get-WinEvent @params
    Write-Host "Fetched $($events.Count) event(s)." -ForegroundColor Green

    $mapped = $events | ForEach-Object {
        [PSCustomObject]@{
            TimeCreated = $_.TimeCreated
            Level       = $_.LevelDisplayName
            EventId     = $_.Id
            Source      = $_.ProviderName
            MachineName = $_.MachineName
            Message     = $_.Message
        }
    }

    if ($Format -in @("JSON", "Both")) {
        $jsonPath = "$baseFile.json"
        $mapped | ConvertTo-Json -Depth 5 | Set-Content -Path $jsonPath -Encoding utf8
        Write-Host "Saved JSON to: $jsonPath" -ForegroundColor Yellow
    }

    if ($Format -in @("TXT", "Both")) {
        $txtPath = "$baseFile.txt"
        $sb = [System.Text.StringBuilder]::new()
        foreach ($ev in $mapped) {
            $sb.AppendLine("--------------------------------------------------------------------------------") | Out-Null
            $sb.AppendLine("Time: $($ev.TimeCreated) | Level: $($ev.Level) | Event ID: $($ev.EventId) | Source: $($ev.Source)") | Out-Null
            $sb.AppendLine("Machine: $($ev.MachineName)") | Out-Null
            $sb.AppendLine("Message:") | Out-Null
            $sb.AppendLine($ev.Message) | Out-Null
            $sb.AppendLine() | Out-Null
        }
        $sb.ToString() | Set-Content -Path $txtPath -Encoding utf8
        Write-Host "Saved TXT to: $txtPath" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "Error fetching event logs: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "If 'Unauthorized / Access Denied', provide credentials with -Credential (Get-Credential)." -ForegroundColor DarkYellow
}
