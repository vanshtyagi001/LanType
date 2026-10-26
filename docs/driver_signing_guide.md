# LANType Virtual HID Keyboard Driver: Signing & Installation Guide

This document describes the step-by-step procedure to build, test-sign, install, and verify the `LANTypeVhid` KMDF driver on Windows 10 and Windows 11.

---

## 1. Prerequisites

1. **Windows 10 / 11 (64-bit)**
2. **Visual Studio 2022 / 2026** (with C++ Desktop workload)
3. **Windows Driver Kit (WDK)** matching your Windows SDK build
4. **Administrator Elevation** in PowerShell / Command Prompt

---

## 2. Driver Architecture Overview

The `LANTypeVhid` driver is a KMDF (Kernel-Mode Driver Framework) driver that leverages Microsoft's **Virtual HID Framework (VHF)** (`vhf.h` / `vhfkm.lib`).

- **Hardware ID**: `root\LANTypeVhidDevice`
- **Class**: `Keyboard` ({4D36E96B-E325-11CE-BFC1-08002BE10318})
- **Symlink**: `\\.\LANTypeVhidDevice`
- **Security**: Restricted to `SYSTEM` and `Administrators` via SDDL `D:P(A;;GA;;;SY)(A;;GA;;;BA)` to prevent unprivileged processes from injecting keystrokes.
- **Enumeration**: Appears in Windows **Device Manager** as a genuine HID Keyboard under the **Keyboards** branch.

---

## 3. Development Build & Test Signing

Windows 64-bit kernel requires all loaded drivers to have a valid digital signature. For development and local testing, Windows supports **Test Signing** mode.

### Step 3.1: Enable Test Signing on the Test Machine

Open PowerShell as **Administrator**:

```powershell
bcdedit /set testsigning on
```

> **Note**: Reboot the PC after running this command. Windows will display "Test Mode" watermark in the bottom-right corner of the desktop.

### Step 3.2: Create a Self-Signed Test Certificate

Run the following in Administrator PowerShell:

```powershell
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=LANType Test Driver Signing" `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -HashAlgorithm SHA256

# Install the certificate into the Trusted Root Certification Authorities store
$rootStore = Get-Item "Cert:\LocalMachine\Root"
$rootStore.Open("ReadWrite")
$rootStore.Add($cert)
$rootStore.Close()

# Install into Trusted Publishers store
$publisherStore = Get-Item "Cert:\LocalMachine\TrustedPublisher"
$publisherStore.Open("ReadWrite")
$publisherStore.Add($cert)
$publisherStore.Close()

Write-Host "Created and installed LANType test certificate with thumbprint: $($cert.Thumbprint)"
```

### Step 3.3: Sign the Driver Binary and Catalog File

Using `signtool.exe` (located in `C:\Program Files (x86)\Windows Kits\10\bin\<version>\x64\`):

```powershell
# Sign LANTypeVhid.sys
signtool sign /v /s "My" /n "LANType Test Driver Signing" /fd sha256 /tr http://timestamp.digicert.com /td sha256 windows\hid-driver\bin\Release\LANTypeVhid.sys

# Create and sign catalog file
Inf2Cat.exe /driver:windows\hid-driver\ /os:10_X64
signtool sign /v /s "My" /n "LANType Test Driver Signing" /fd sha256 /tr http://timestamp.digicert.com /td sha256 windows\hid-driver\LANTypeVhid.cat
```

---

## 4. Driver Installation & Verification

### Step 4.1: Install with `pnputil` (Recommended)

In Administrator PowerShell:

```powershell
pnputil /add-driver windows\hid-driver\LANTypeVhid.inf /install
```

### Step 4.2: Alternatively Install with `devcon`

```powershell
devcon.exe install windows\hid-driver\LANTypeVhid.inf root\LANTypeVhidDevice
```

### Step 4.3: Verify Enumeration in Device Manager

1. Press `Win + X` and select **Device Manager**.
2. Expand the **Keyboards** section.
3. You should see `LANType Virtual HID Keyboard Device` enumerated.
4. Right-click the device and choose **Properties**:
   - Status should show: *"This device is working properly."*

---

## 5. Uninstallation

To remove the virtual device and driver package:

```powershell
# Remove device instance
devcon.exe remove root\LANTypeVhidDevice

# Delete driver package from the driver store
pnputil /delete-driver oemXX.inf /uninstall /force
```

---

## 6. Production Distribution Signing Requirements

For production releases to end users without requiring Test Signing or disabling driver-signing enforcement:

1. **EV Code Signing Certificate**: Acquire an Extended Validation (EV) hardware-token certificate from an authorized CA (DigiCert, Sectigo).
2. **Microsoft Partner Center Account**: Register hardware developer account at [Microsoft Hardware Dev Center](https://partner.microsoft.com/dashboard/hardware).
3. **Attestation Signing**:
   - Package driver files and CAB package.
   - Submit CAB to Microsoft Hardware Dev Center for Attestation Signing.
   - Download the Microsoft-signed catalog file signed by Microsoft Windows Hardware Compatibility Publisher.
   - This signature is trusted by all Windows 10 and Windows 11 PCs with Secure Boot enabled!
