#Requires -RunAsAdministrator
<#
.SYNOPSIS
    LANType Windows Service & Application Uninstaller
#>

param(
    [string]$InstallDir = "C:\Program Files\LANType",
    [string]$DataDir = "C:\ProgramData\LANType",
    [switch]$KeepLogs = $false
)

$ErrorActionPreference = "Continue"

Write-Host "==========================================================" -ForegroundColor Yellow
Write-Host "       LANType Windows Service & App Uninstaller          " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Yellow

# 1. Stop and Delete Windows Service
$svc = Get-Service -Name "LANTypeService" -ErrorAction SilentlyContinue
if ($svc) {
    Write-Host "[*] Stopping LANTypeService..."
    Stop-Service -Name "LANTypeService" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "[*] Unregistering LANTypeService from SCM..."
    sc.exe delete "LANTypeService" | Out-Null
}

# 2. Remove Shortcuts
$desktopShortcut = "$([Environment]::GetFolderPath('Desktop'))\LANType.lnk"
if (Test-Path $desktopShortcut) {
    Remove-Item $desktopShortcut -Force -ErrorAction SilentlyContinue
}

$startMenuFolder = "$([Environment]::GetFolderPath('CommonStartMenu'))\Programs\LANType"
if (Test-Path $startMenuFolder) {
    Remove-Item $startMenuFolder -Recurse -Force -ErrorAction SilentlyContinue
}

# 3. Remove Program Files Directory
if (Test-Path $InstallDir) {
    Write-Host "[*] Removing application files from $InstallDir..."
    Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
}

# 4. Remove or Preserve Logs
if (!$KeepLogs -and (Test-Path $DataDir)) {
    Write-Host "[*] Cleaning up data directory $DataDir..."
    Remove-Item -Path $DataDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "       LANType Uninstallation Completed.                  " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
