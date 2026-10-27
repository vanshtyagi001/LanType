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

## 2. Installing via Setup Executable or PowerShell

### Method A: One-Click GUI Installer (Recommended)
Double-click:
`LANType-Setup-v1.0.0.exe` in the project root directory.

The modern wizard will:
- Install the background service and desktop app to `C:\Program Files\LANType\`.
- Configure Windows Firewall rules for TCP port `52443` (WebSocket) and UDP port `52442` (Auto-discovery).
- Register and start the background service with auto-start on Windows boot.
- Create desktop and Start Menu shortcuts and launch the application.

### Method B: Automated PowerShell Script
1. Open PowerShell as **Administrator**.
2. Run the automated installer script:

```powershell
& ".\windows\installer\install_service.ps1"
```

The script will:
- Check architecture and available disk space.
- Publish release binaries to `C:\Program Files\LANType\`.
- Configure Windows Firewall rules for incoming connections.
- Configure `LANTypeService` in the Windows Service Control Manager.
- Start the service automatically.
- Verify health check response at `http://127.0.0.1:52443/health`.
- Create desktop and Start Menu shortcuts.

To uninstall:
```powershell
& ".\windows\installer\uninstall_service.ps1"
```

---

## 3. Automatic Device Discovery over Wi-Fi & Hotspots

LANType incorporates dual discovery mechanisms for seamless pairing:
- **Phone connected to Laptop Hotspot:** Android automatically detects the laptop at the DHCP default gateway (`192.168.137.1`), validating the endpoint via `/health` in <200ms.
- **Laptop connected to Phone Hotspot / Shared Wi-Fi:** Windows service periodically broadcasts UDP beacons on port `52442` and listens for `LANTYPE_PROBE` probes.
- **Pairing Flow:** The Android app automatically discovers the PC name and IP on the default port `52443` and prompts only for the 6-digit PIN shown on the laptop control panel. Manual IP configuration and QR code scanning remain available as fallbacks.

---

## 4. Virtual HID Keyboard Driver (KMDF)

For instructions on building and test-signing the genuine KMDF Virtual HID Keyboard driver, refer to:
[Driver Signing & Installation Guide](driver_signing_guide.md).

When the driver is installed, the service automatically detects the device symlink `\\.\LANTypeVhidDevice` and transitions from `DevReceiverFallback` to `ActiveEnumerated` genuine HID keyboard typing!

---

## 5. Kiosk, Safe Exam Browser & CodeTantra Coding Environment Guide

LANType is architected to type into strict **Kiosk browsers, proctored examination tools, and web-based IDEs** (such as **CodeTantra**, Safe Exam Browser, Respondus, HackerRank, Mettl):

### Key Compatibility Features for CodeTantra & Kiosk Browsers
1. **Hardware OEM Scan Code Engine (`KEYEVENTF_SCANCODE`):**
   * Web code editors (Monaco Editor and Ace Editor used by CodeTantra) listen to `KeyboardEvent.code` (e.g. `KeyA`, `BracketLeft`, `Semicolon`).
   * LANType maps every letter, digit, brace, and symbol directly to its hardware scan code via `MapVirtualKeyW` and `VkKeyScanW`, triggering authentic DOM `keydown`, `beforeinput`, and `keyup` events with `e.isTrusted === true`.
2. **Atomic Modifier & Key Dispatch:**
   * Shifted coding symbols (`{`, `}`, `(`, `)`, `:`, `"`, `<`, `>`, `+`, `_`) are dispatched atomically in a single multi-input transaction. This eliminates modifier-timing race conditions in Chromium's input pipeline.
3. **CRLF Newline Normalization:**
   * Prevents double-enter bugs: all Windows `\r\n` line endings are normalized to single newline pulses so CodeTantra's auto-indentation and line numbers remain intact.
4. **UIPI Administrator Elevation:**
   * `LANType.Desktop.exe` runs with `requireAdministrator` privileges in its manifest, allowing keystroke injection into elevated full-screen Kiosk browsers without being blocked by Windows User Interface Privilege Isolation (UIPI).
5. **Zero-Clipboard, Zero-Blur Typing:**
   * Because typing originates from the phone over LAN into the active PC cursor, the browser never loses window focus (no `blur` or `visibilitychange` events), and no clipboard `paste` events are fired.

### Step-by-Step: Typing into CodeTantra
1. Launch **`LANType.Desktop.exe`** (or desktop shortcut).
2. Pair your Android phone with the 6-digit PIN.
3. Open your CodeTantra test or programming question in full screen.
4. **Click your mouse directly inside CodeTantra's code editor textarea** so the text cursor is blinking at the insertion point.
5. On your phone, tap **Start Typing**.
6. LANType types your code block character-by-character at your configured speed (recommended: 240–360 CPM) with natural human-like dwell time!
