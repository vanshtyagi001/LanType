#Requires -RunAsAdministrator
<#
.SYNOPSIS
    LANType Windows Installation & Service Registration Script
.DESCRIPTION
    Installs LANType Desktop App and Windows Service into C:\Program Files\LANType\
    and initializes mutable log storage in C:\ProgramData\LANType\.
#>

param(
    [string]$InstallDir = "C:\Program Files\LANType",
    [string]$DataDir = "C:\ProgramData\LANType",
    [string]$StartupType = "auto" # auto or demand
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       LANType Windows Service & App Installer            " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Verify Architecture & OS
if (![Environment]::Is64BitOperatingSystem) {
    Write-Error "LANType requires a 64-bit Windows operating system (x64)."
    exit 1
}

$osVersion = [Environment]::OSVersion.Version
if ($osVersion.Major -lt 10) {
    Write-Error "LANType requires Windows 10 or Windows 11."
    exit 1
}

Write-Host "[✓] Windows OS Verified: Windows $($osVersion.Major) (Build $($osVersion.Build))" -ForegroundColor Green

# 2. Check Disk Space on C:
$drive = Get-PSDrive C
if ($drive.Free -lt 500MB) {
    Write-Error "Insufficient disk space on C: (at least 500MB required)."
    exit 1
}
Write-Host "[✓] Available disk space on C: $([math]::Round($drive.Free / 1GB, 2)) GB" -ForegroundColor Green

# 3. Create Target Directories
$serviceDir = Join-Path $InstallDir "Service"
$logsDir = Join-Path $DataDir "logs"

Write-Host "[*] Creating application and service directories..."
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
New-Item -ItemType Directory -Path $serviceDir -Force | Out-Null
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null

# 4. Publish / Copy Binaries
$scriptRoot = $PSScriptRoot
$sourceRoot = (Resolve-Path "$scriptRoot\..\..").Path
$desktopPublish = "$sourceRoot\windows\desktop-app\bin\Release\net9.0-windows"
$servicePublish = "$sourceRoot\windows\background-service\bin\Release\net9.0"

# Build Release if not already built
Write-Host "[*] Building release binaries..."
$env:PATH = "C:\Users\tyagi\.dotnet;" + $env:PATH
dotnet publish "$sourceRoot\windows\background-service\LANType.Service.csproj" -c Release -o "$serviceDir"
dotnet publish "$sourceRoot\windows\desktop-app\LANType.Desktop.csproj" -c Release -o "$InstallDir"

Write-Host "[✓] Binaries successfully deployed to $InstallDir" -ForegroundColor Green

# 5. Stop and Remove Existing Service if present
$existingService = Get-Service -Name "LANTypeService" -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "[*] Stopping existing LANTypeService..."
    Stop-Service -Name "LANTypeService" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete "LANTypeService" | Out-Null
    Start-Sleep -Seconds 1
}

# 6. Register Windows Service with Service Control Manager (SCM)
$serviceExe = Join-Path $serviceDir "LANType.Service.exe"
Write-Host "[*] Registering LANTypeService in Windows Service Control Manager..."
sc.exe create "LANTypeService" binPath= "`"$serviceExe`"" start= $StartupType DisplayName= "LANType Background Service" depend= "Tcpip"
sc.exe description "LANTypeService" "LANType background typing engine and authenticated WebSocket server for LAN keyboard emulation."

# 6b. Add Windows Firewall Inbound Rules
Write-Host "[*] Configuring Windows Firewall rules for TCP 52443 and UDP 52442..."
netsh advfirewall firewall add rule name="LANType WebSocket Port" dir=in action=allow protocol=TCP localport=52443 profile=any | Out-Null
netsh advfirewall firewall add rule name="LANType Discovery Port" dir=in action=allow protocol=UDP localport=52442 profile=any | Out-Null

# 7. Start the Service
Write-Host "[*] Starting LANTypeService..."
Start-Service -Name "LANTypeService"
Start-Sleep -Seconds 3

# 8. Verify Service Health Check
try {
    $health = Invoke-RestMethod -Uri "http://127.0.0.1:52443/health" -TimeoutSec 5
    Write-Host "[✓] Service Health Check Succeeded: $health" -ForegroundColor Green
} catch {
    Write-Warning "Service started, but health check endpoint did not respond immediately. Check $logsDir."
}

# 9. Create Application Shortcuts
$wsh = New-Object -ComObject WScript.Shell

# Desktop Shortcut
$desktopPath = [Environment]::GetFolderPath("Desktop")
$shortcut = $wsh.CreateShortcut("$desktopPath\LANType.lnk")
$shortcut.TargetPath = "$InstallDir\LANType.Desktop.exe"
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Description = "LANType Virtual HID Keyboard Control Panel"
$shortcut.Save()

# Start Menu Shortcut
$startMenuPath = [Environment]::GetFolderPath("CommonStartMenu")
$programsPath = Join-Path $startMenuPath "Programs\LANType"
New-Item -ItemType Directory -Path $programsPath -Force | Out-Null
$startShortcut = $wsh.CreateShortcut("$programsPath\LANType.lnk")
$startShortcut.TargetPath = "$InstallDir\LANType.Desktop.exe"
$startShortcut.WorkingDirectory = $InstallDir
$startShortcut.Description = "LANType Virtual HID Keyboard Control Panel"
$startShortcut.Save()

Write-Host "[✓] Shortcuts created on Desktop and Start Menu." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   LANType Installation Completed Successfully!           " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
