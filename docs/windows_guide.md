# LANType Windows Application: Quickstart & Operations Guide

## 1. Quickstart: Running in Development Mode

You can run and test the complete system immediately on your local machine without requiring administrator installation or a physical phone:

### Step 1: Start the Background Service
Open PowerShell in the project root:

```powershell
$env:PATH = "C:\Users\tyagi\.dotnet;" + $env:PATH
dotnet run --project windows/background-service/LANType.Service.csproj
```

The service will output:
```
==================================================
   LANType Windows Background Service Starting    
==================================================
Local Network Host IP: 192.168.x.x
Keyboard Interface Status: DevReceiverFallback (Fallback: True)
LANType WebSocket server listening on port 52443
Desktop IPC Server listening on http://127.0.0.1:52444/
Generated new pairing PIN: 849201
```

### Step 2: Launch the Desktop Control Panel
Open another terminal:

```powershell
$env:PATH = "C:\Users\tyagi\.dotnet;" + $env:PATH
dotnet run --project windows/desktop-app/LANType.Desktop.csproj
```

The control panel will appear, displaying:
- Green active badge for the background service
- The local Hotspot/Wi-Fi IP address and WebSocket port
- Dynamic Pairing QR code & 6-digit PIN
- Live speed and progress bars

### Step 3: Run Direct Typing Test
1. In the Desktop Control Panel, switch to the **Manual Typing Test** tab.
2. Open **Notepad** side-by-side.
3. Configure desired typing speed in CPM (e.g., 300 CPM).
4. Click **Start Typing (3s countdown)**.
5. Immediately click inside Notepad to focus it.
6. The service executes high-precision character scheduling and reports progress in real time!

---

## 2. Installing as a Managed Windows Service

To install the service permanently into `C:\Program Files\LANType\Service\` with Windows Service Control Manager:

1. Open PowerShell as **Administrator**.
2. Run the automated installer script:

```powershell
& ".\windows\installer\install_service.ps1"
```

The script will:
- Check architecture and available disk space.
- Publish release binaries to `C:\Program Files\LANType\`.
- Configure `LANTypeService` in the Windows Service Control Manager.
- Start the service automatically.
- Verify health check response at `http://127.0.0.1:52443/health`.
- Create desktop and Start Menu shortcuts.

To uninstall:
```powershell
& ".\windows\installer\uninstall_service.ps1"
```

---

## 3. Virtual HID Keyboard Driver (KMDF)

For instructions on building and test-signing the genuine KMDF Virtual HID Keyboard driver, refer to:
[Driver Signing & Installation Guide](driver_signing_guide.md).

When the driver is installed, the service automatically detects the device symlink `\\.\LANTypeVhidDevice` and transitions from `DevReceiverFallback` to `ActiveEnumerated` genuine HID keyboard typing!
