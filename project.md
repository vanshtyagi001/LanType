# Project: LANType — LAN-Based System-Wide HID Keyboard Typing System

## 1. Project Objective

Build a complete, production-oriented Windows application and a native Android application that work together to let a user type text from their Android phone into a Windows laptop through a local Wi-Fi network.

The user will open the mobile hotspot on their Windows laptop, connect their Android phone to that hotspot, open the Android app, paste or enter the desired text, configure typing speed in CPM or WPM, and start automatic typing.

The Windows application must deliver input through a **Windows-recognized HID keyboard device**, not merely by simulating JavaScript keyboard events, sending text through clipboard paste, or relying on `SendInput` as the primary input mechanism.

The objective is to make the receiving application observe input through the operating system's keyboard/HID input stack, as it would from a keyboard device.

The project must support system-wide typing in ordinary Windows applications and provide a documented, testable approach to kiosk compatibility. Do not promise universal compatibility with locked-down kiosks or applications that reject unauthorized input devices.

## 2. Required Applications and Technology Stack

Create two separate applications and the components needed to connect them.

### A. Windows application

Use the following architecture:

- Native Windows desktop control panel using C# and .NET.
- A Windows background service for communication, text scheduling, and session management.
- A separate KMDF-based virtual HID keyboard driver written in C/C++.
- Microsoft's Virtual HID Framework (VHF) for presenting the virtual keyboard device to Windows.
- WebSocket communication for receiving commands and text from Android.
- A local configuration file or equivalent Windows-supported storage for settings.
- A proper Windows installer supporting installation, repair, upgrades, and uninstall.

Keep the desktop interface, background service, and kernel-mode driver as separate components. The desktop window must not be responsible for keeping the typing engine alive.

### B. Android application

Use:

- Kotlin.
- Jetpack Compose for the native Android UI.
- OkHttp WebSocket or an equivalent maintained WebSocket implementation.
- Android's supported networking APIs.
- DataStore for local settings.

The Android app must operate on the same Wi-Fi network as the Windows laptop. It must not require a remote server, cloud account, external database, or internet connection for normal operation.

## 3. Required Architecture

Implement the following data flow:

Android app → Local Wi-Fi hotspot → Authenticated WebSocket → Windows background service → HID driver interface → Virtual HID keyboard device → Windows keyboard input stack → Focused application.

The Windows service should communicate with the driver through a documented, appropriately secured driver interface, such as an IOCTL-based device interface.

The driver must construct valid keyboard HID reports and submit them through VHF. Use a standard keyboard report descriptor, correctly maintain modifier state, and send proper key-down and key-up reports.

The background service should schedule the reports at the requested typing speed. The driver should remain responsible for the device-level HID behavior rather than implementing the mobile networking protocol or the complete typing scheduler.

Do not substitute `SendInput`, clipboard pasting, window-message injection, or application-specific scripting for the HID implementation.

If a development-only fallback is provided, label it clearly, keep it separate from the HID backend, and never silently use it as a replacement for the required device-level implementation.

## 4. Windows Application Requirements

### 4.1 Desktop control panel

Build a clean, professional Windows interface containing:

- Application connection status.
- HID keyboard device status.
- Service status.
- Hotspot/network status.
- Current paired phone.
- Current typing speed and selected unit.
- Typing session state.
- Characters typed and remaining.
- Estimated completion time.
- Start, pause, resume, and stop controls where appropriate.
- Configuration and troubleshooting pages.
- Clear driver installation and permission errors.

The application must clearly distinguish between the control-panel process, background-service process, and driver/device status.

Closing the control-panel window must not terminate an active or available background service. Closing the target application must not automatically terminate the typing service.

### 4.2 Windows background service

Implement a separate Windows Service that handles:

- Startup and shutdown.
- Local network discovery and connection management.
- WebSocket authentication.
- Receiving and validating text payloads.
- Typing queues and scheduling.
- Driver communication.
- Start, pause, resume, and stop commands.
- Connection-loss handling.
- Input-state cleanup.
- Structured diagnostic logging.
- Recovery from ordinary service or network errors.

Install the service with an appropriate Windows Service Control Manager configuration. Use least-privilege service permissions and the minimum required access to the HID device interface.

The service should run independently of the foreground desktop application and the target application receiving input.

Do not implement stealth persistence, hidden processes, process injection, disabling security software, tampering with kiosk configuration, or mechanisms that fight an administrator's attempt to stop or uninstall the software.

The supported persistence model is a visible, administrator-installed Windows service that can be managed normally through Windows.

### 4.3 Virtual HID keyboard driver

Implement a genuine virtual HID keyboard using KMDF and Microsoft's VHF.

Requirements:

- Expose a keyboard HID collection that Windows can enumerate.
- Use a valid keyboard HID report descriptor.
- Present keyboard usage codes and modifier states correctly.
- Send key-down and key-up reports.
- Support letters, digits, spaces, punctuation, Enter, Tab, Backspace, Shift, and the supported control keys.
- Correctly handle transitions between normal and modified characters.
- Prevent stuck keys following cancellation, disconnects, or unexpected errors.
- Handle simultaneous modifiers correctly.
- Provide meaningful driver/device status to the Windows service.
- Use a secure device interface so unauthorized local processes cannot arbitrarily control the keyboard.
- Support orderly device creation, startup, shutdown, and removal.

The device must appear as a keyboard in Windows Device Manager.

Keep the driver's responsibilities small. Networking, text storage, authentication, the user interface, and speed scheduling should remain in user mode.

Do not disable Windows driver-signing enforcement or require users to disable Secure Boot to make the application work.

Provide development instructions for using an isolated test machine and properly test-signed development builds. Explain the appropriate signing and distribution requirements for production releases.

### 4.4 Keyboard layouts and character encoding

Map supported characters into key codes and modifier states according to the configured or supported active Windows keyboard layout.

At a minimum, document and test the US English layout.

Requirements:

- Correctly handle uppercase and lowercase letters.
- Handle punctuation and symbols using modifiers as necessary.
- Handle spaces, tabs, and line breaks.
- Show a clear warning for characters that cannot be represented through the implemented HID key mapping.
- Define and document the limitations of Unicode characters, emoji, and non-Latin scripts.
- Never silently replace unsupported characters with incorrect keystrokes.

A hardware-style HID keyboard reports key usages and modifier states; it does not transmit arbitrary Unicode text directly. Do not claim universal text compatibility unless a separately designed, explicitly identified method is used.

## 5. Android Application Requirements

### 5.1 Main screen

Create a polished, responsive native Android app with:

- Connection indicator.
- Windows host name and paired-device information.
- Large text editor.
- Paste-from-clipboard functionality.
- Clear-text and select-all actions.
- CPM/WPM selector.
- Numeric typing-speed control.
- Text-length counter.
- Estimated duration.
- Start, pause, resume, and stop buttons.
- Live session progress.
- Connection-loss warnings.
- Settings and help screens.

The application should be usable on different Android screen sizes and should not require root access.

### 5.2 Connection and pairing

When the laptop service starts, it should expose a local connection endpoint on the appropriate laptop hotspot/network interface.

Provide a safe pairing flow using a QR code or an equivalent pairing mechanism. The QR code may encode the local endpoint and temporary pairing information; do not put permanent credentials in it.

The Android app should be able to:

1. Connect to the laptop hotspot.
2. Discover or scan the laptop's endpoint.
3. Pair securely.
4. Receive connection and session status.
5. Send the text and typing settings.
6. Control the typing session.

The Windows interface should show the network address or QR code where needed.

Use authenticated connections, short-lived pairing credentials, input validation, rate limits, and secure session handling. Bind the service to the intended local network interface rather than exposing it indiscriminately to every network interface.

Do not send text to a third-party server or retain typed content in diagnostic logs.

### 5.3 Session synchronization

Keep the Android display synchronized with the Windows service.

Show the correct state for:

- Connecting.
- Connected.
- Ready.
- Typing.
- Paused.
- Stopped.
- Completed.
- Error.
- Reconnecting.

When the connection is lost, pause typing by default. Provide a predictable resume procedure that does not duplicate previously typed characters.

## 6. Typing-Speed Engine

Support both CPM and WPM.

### CPM

CPM means characters per minute.

Calculate the nominal interval as:

`delay_ms = 60000 / CPM`

For example, 300 CPM corresponds to a nominal delay of 200 milliseconds per character.

### WPM

Define WPM using the standard convention of five characters per word for speed conversion.

`CPM = WPM × 5`

`delay_ms = 60000 / (WPM × 5)`

Allow users to configure sensible minimum and maximum speeds.

The scheduler must account for the actual keyboard report sequence, including key-down, key-up, modifiers, and any required inter-key delays. Document that very high speeds may be limited by Windows scheduling and the receiving application.

The engine must support:

- Start.
- Pause.
- Resume.
- Stop.
- Current position in the text.
- Progress reporting.
- Remaining-character count.
- Estimated time remaining.
- Queue cancellation.
- Safe cleanup of all active key states.

Pausing must prevent additional characters from being sent after the current safe input transition. Resuming must continue from the correct position. Stopping must release any held keys and discard or cancel the remaining queue according to the documented session policy.

Use deterministic scheduling and monotonic timing. Do not depend on a fixed-rate UI timer for precise input scheduling.

## 7. System-Wide Input and Kiosk Compatibility

The system must use the HID keyboard path so the receiving Windows application can process input using the standard keyboard input stack.

Test typing in several ordinary environments, including:

- Notepad.
- A browser text field.
- A native desktop application.
- An application running in full-screen mode.
- A test application launched with standard Windows kiosk configuration.
- An authorized test kiosk using the applicable Assigned Access or equivalent configuration.

Do not assume that HID enumeration guarantees successful typing into every application.

The implementation must explicitly document that:

- A newly installed driver and service may require administrator authorization.
- Windows security and device-management policies can prevent installation or operation.
- Certain kiosk configurations restrict which applications and interactions are permitted.
- An application may reject input from particular devices.
- Secure desktops and protected input surfaces may not accept the device's input.
- A third-party kiosk application cannot be guaranteed to accept typing from this system.

Do not modify or bypass a third-party kiosk's security policies. Do not attempt to hide the service from administrators or make the application impossible to stop.

Where a kiosk is managed by another party, require authorization from the system owner to install or approve the required components.

## 8. Windows Installation Requirements

The installer must support installation under the C: drive.

Use the following default locations:

- Application binaries and desktop application: `C:\Program Files\LANType\`
- Service binaries: `C:\Program Files\LANType\Service\`
- Application logs and mutable data: `C:\ProgramData\LANType\`
- User-specific preferences: the appropriate Windows user profile location.

Allow the installer to select a custom application directory under `C:\` where appropriate, while keeping driver installation and mutable-data locations consistent with Windows requirements.

Do not install executable files into arbitrary system directories merely to hide them or avoid user visibility.

Use a proper Windows installer with administrator elevation for service installation and any driver installation that requires it.

The installation process must:

1. Check Windows version, architecture, available disk space, and required dependencies.
2. Verify driver package signatures before installation.
3. Install the supported driver package through Windows' appropriate driver installation mechanism.
4. Register the Windows service.
5. Configure service startup according to the user's explicit installation choices.
6. Create application shortcuts.
7. Verify that the device enumerates successfully.
8. Verify that the service starts and responds to health checks.
9. Display meaningful error messages if permissions or policies prevent installation.
10. Provide a repair option.
11. Provide a complete, authorized uninstall procedure.

The installer must not silently disable Windows security settings, change kiosk policies, bypass application-control restrictions, or suppress administrator approval.

## 9. Security and Privacy

Implement the following safeguards:

- Pairing codes and authenticated sessions.
- Network-interface restrictions.
- Input validation and message-size limits.
- No unauthenticated remote commands.
- No public internet dependency.
- No hard-coded production credentials.
- No logging of complete text payloads by default.
- Safe storage of settings and secrets.
- No unexpected text transmission when the application starts.
- Clear visibility into whether typing is active.
- Emergency stop control from the Android app.
- Safe key-state release if a client disconnects.
- A service health/status interface.
- Normal administrator-controlled shutdown and uninstall.

Require an explicit user action before sending text to the focused application. Never activate a typing session silently.

## 10. Testing Requirements

Provide automated tests wherever possible, plus a reproducible manual validation checklist.

### Android tests

- Input and paste text.
- CPM/WPM conversion.
- Start, pause, resume, and stop.
- Connection recovery.
- Incorrect pairing credentials.
- Large text payloads.
- UI behavior during disconnection.

### Windows service tests

- WebSocket connection and authentication.
- Text validation.
- Typing queue and speed scheduling.
- Pause/resume position correctness.
- Cancellation and cleanup.
- Service restart and shutdown.
- Driver communication failures.
- Network adapter changes.
- Duplicate command prevention.

### Driver tests

- Device enumeration in Device Manager.
- Valid HID reports.
- Modifier combinations.
- Key-down/key-up sequences.
- Repeated characters.
- Disconnect and shutdown cleanup.
- Device removal and reinitialization.
- Compatibility with Windows keyboard layouts.

### End-to-end tests

- Pair a phone to the laptop hotspot.
- Send a short sample paragraph.
- Test different typing speeds.
- Type in multiple ordinary applications.
- Verify that closing the desktop control panel does not terminate the service.
- Test a supported, administrator-configured kiosk environment.
- Record limitations when the kiosk blocks input or prohibits the required service/driver.
- Confirm that the software does not type when stopped or disconnected.

Provide test results separately for the normal desktop environment and each kiosk configuration tested. Do not label compatibility as successful without reproducing the test.

## 11. Engineering and Code Quality

Use a maintainable repository structure, for example:

`/android-app/`

`/windows/desktop-app/`

`/windows/background-service/`

`/windows/hid-driver/`

`/windows/installer/`

`/shared/protocol/`

`/docs/`

`/tests/`

Requirements:

- Separate responsibilities into modules.
- Use typed messages and version the communication protocol.
- Handle exceptions and failures explicitly.
- Use structured logs without sensitive text.
- Avoid unnecessary dependencies.
- Keep networking and user-interface code out of the kernel-mode driver.
- Provide reproducible development and build instructions.
- Include dependency versions and required SDK/WDK versions.
- Provide configuration examples without secrets.
- Do not generate placeholder methods for core functionality and call the project complete.
- Do not claim the driver builds, installs, or works unless that has actually been verified.

## 12. Required Deliverables

Deliver the following:

1. Complete Windows desktop application source code.
2. Complete Windows background-service source code.
3. Virtual HID driver source code and its INF/package configuration.
4. Complete Android Studio project.
5. Shared communication protocol definition.
6. Windows installer scripts and configuration.
7. Debug-build and release-build instructions.
8. Driver development-signing and production-signing instructions.
9. Android APK build instructions.
10. Security and privacy documentation.
11. Architecture diagram and data-flow description.
12. Installation, repair, upgrade, and uninstall instructions.
13. Automated tests and manual test checklist.
14. Troubleshooting guide.
15. A compatibility matrix documenting tested Windows versions and kiosk configurations.

## 13. Development Phases

Implement the project incrementally.

**Phase 1:** Build the Android interface, pairing workflow, local WebSocket protocol, and Windows service with a test receiver.

**Phase 2:** Implement the VHF-based keyboard driver and verify that Windows enumerates the device and receives correct keyboard reports.

**Phase 3:** Connect the Windows service to the driver interface and implement the real typing scheduler.

**Phase 4:** Add pause/resume/stop synchronization, keyboard-layout handling, security, reconnection logic, and installer support.

**Phase 5:** Perform end-to-end testing on a normal Windows desktop and then on authorized kiosk test systems.

At the end of each phase, provide working code, build commands, verification steps, known limitations, and a list of incomplete requirements.

Prioritize functional correctness, security, reliable input delivery, and maintainability over visual effects or unnecessary features.

The final product must be a genuine LAN-controlled keyboard-input system, not merely a phone-based remote clipboard or an application-specific web autotyper.
