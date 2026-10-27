using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using LANType.Protocol;
using Microsoft.Extensions.Logging;

namespace LANType.Service.Keyboard;

/// <summary>
/// Development and user-mode keyboard input receiver.
/// Uses Windows SendInput and keybd_event to synthesize real keystrokes directly
/// into the focused window on the active interactive desktop.
/// </summary>
public sealed class DevTestKeyboardReceiver : IHidKeyboardInterface
{
    private readonly ILogger<DevTestKeyboardReceiver> _logger;
    private readonly ConcurrentQueue<HidKeyboardReport> _receivedReports = new();
    private long _totalReportsSent;
    private long _totalKeysPressed;

    private readonly object _stateLock = new();
    private ushort _heldVk;
    private bool _heldShift;
    private bool _heldCtrl;
    private bool _heldAlt;
    private bool _heldGui;
    private char _heldUnicodeChar;

    public bool IsConnected => true;
    public bool IsDevFallback => true;
    public string DeviceDescription => "LANType Windows User-Mode Input Receiver (SendInput)";

    public long TotalReportsSent => Interlocked.Read(ref _totalReportsSent);
    public long TotalKeysPressed => Interlocked.Read(ref _totalKeysPressed);

    #region Win32 P/Invoke & Structures (Exact x64 40-byte tagINPUT Layout)

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    private const int VK_CAPITAL = 0x14;
    private const ushort VK_LSHIFT = 0xA0;
    private const ushort VK_LCONTROL = 0xA2;
    private const ushort VK_LMENU = 0xA4;
    private const ushort VK_LWIN = 0x5B;

    private const uint DESKTOP_ALL = 0x01FF;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    // Must be 32 bytes on x64 so INPUT is 40 bytes
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyW(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern short VkKeyScanW(char ch);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr hDesktop);

    #endregion

    public DevTestKeyboardReceiver(ILogger<DevTestKeyboardReceiver> logger)
    {
        _logger = logger;
        _logger.LogInformation("Initialized LANType Windows Input Receiver (User-Mode SendInput active).");
    }

    private static void EnsureDesktopAttached()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            IntPtr hDesk = OpenInputDesktop(0, false, DESKTOP_ALL);
            if (hDesk == IntPtr.Zero)
            {
                hDesk = OpenDesktop("Default", 0, false, DESKTOP_ALL);
            }
            if (hDesk != IntPtr.Zero)
            {
                SetThreadDesktop(hDesk);
                CloseDesktop(hDesk);
            }
        }
        catch { }
    }

    public Task<bool> SendReportAsync(HidKeyboardReport report, CancellationToken cancellationToken = default)
    {
        _receivedReports.Enqueue(report);
        Interlocked.Increment(ref _totalReportsSent);

        while (_receivedReports.Count > 1000)
        {
            _receivedReports.TryDequeue(out _);
        }

        if (report.IsEmpty || report.Key1 == 0)
        {
            return ReleaseAllKeysAsync(cancellationToken);
        }

        lock (_stateLock)
        {
            EnsureDesktopAttached();
            ReleaseAllKeysInternal();

            ushort vk = HidToVirtualKey(report.Key1);
            if (vk == 0)
            {
                _logger.LogWarning("DevTestKeyboardReceiver: Unknown HID usage code 0x{Code:X2}", report.Key1);
                return Task.FromResult(false);
            }

            bool needShift = (report.Modifiers & (byte)(HidModifier.LeftShift | HidModifier.RightShift)) != 0;
            bool needCtrl = (report.Modifiers & (byte)(HidModifier.LeftControl | HidModifier.RightControl)) != 0;
            bool needAlt = (report.Modifiers & (byte)(HidModifier.LeftAlt | HidModifier.RightAlt)) != 0;
            bool needGui = (report.Modifiers & (byte)(HidModifier.LeftGUI | HidModifier.RightGUI)) != 0;

            // CapsLock compensation for letters
            if (report.Key1 >= HidUsageCodes.KeyA && report.Key1 <= HidUsageCodes.KeyZ)
            {
                bool isCapsLock = (GetKeyState(VK_CAPITAL) & 1) != 0;
                if (isCapsLock)
                {
                    needShift = !needShift;
                }
            }

            // 1. Build atomic down sequence with genuine hardware scan codes
            var inputsDown = new List<INPUT>(5);
            if (needShift)
            {
                inputsDown.Add(MakeKeyInput(VK_LSHIFT, isKeyUp: false));
                _heldShift = true;
            }
            if (needCtrl)
            {
                inputsDown.Add(MakeKeyInput(VK_LCONTROL, isKeyUp: false));
                _heldCtrl = true;
            }
            if (needAlt)
            {
                inputsDown.Add(MakeKeyInput(VK_LMENU, isKeyUp: false));
                _heldAlt = true;
            }
            if (needGui)
            {
                inputsDown.Add(MakeKeyInput(VK_LWIN, isKeyUp: false));
                _heldGui = true;
            }

            // 2. Target key down
            inputsDown.Add(MakeKeyInput(vk, isKeyUp: false));
            _heldVk = vk;

            SendInputsAtomic(inputsDown.ToArray());
            Interlocked.Increment(ref _totalKeysPressed);
        }

        return Task.FromResult(true);
    }

    public Task<bool> SendUnicodeCharAsync(char c, CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            EnsureDesktopAttached();
            ReleaseAllKeysInternal();

            if (OperatingSystem.IsWindows())
            {
                short vkScan = VkKeyScanW(c);
                if (vkScan != -1)
                {
                    byte vk = (byte)(vkScan & 0xFF);
                    byte shiftState = (byte)((vkScan >> 8) & 0xFF);
                    bool needShift = (shiftState & 1) != 0;
                    bool needCtrl = (shiftState & 2) != 0;
                    bool needAlt = (shiftState & 4) != 0;

                    var inputsDown = new List<INPUT>(4);
                    if (needShift) { inputsDown.Add(MakeKeyInput(VK_LSHIFT, isKeyUp: false)); _heldShift = true; }
                    if (needCtrl)  { inputsDown.Add(MakeKeyInput(VK_LCONTROL, isKeyUp: false)); _heldCtrl = true; }
                    if (needAlt)   { inputsDown.Add(MakeKeyInput(VK_LMENU, isKeyUp: false)); _heldAlt = true; }
                    inputsDown.Add(MakeKeyInput(vk, isKeyUp: false));
                    _heldVk = vk;

                    SendInputsAtomic(inputsDown.ToArray());
                }
                else
                {
                    var inputDown = new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = 0,
                                wScan = (ushort)c,
                                dwFlags = KEYEVENTF_UNICODE,
                                time = 0,
                                dwExtraInfo = UIntPtr.Zero
                            }
                        }
                    };

                    uint res = SendInput(1, new[] { inputDown }, Marshal.SizeOf<INPUT>());
                    if (res > 0)
                    {
                        _heldUnicodeChar = c;
                    }
                }
            }

            Interlocked.Increment(ref _totalReportsSent);
            Interlocked.Increment(ref _totalKeysPressed);
        }

        return Task.FromResult(true);
    }

    public Task<bool> ReleaseAllKeysAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            EnsureDesktopAttached();
            ReleaseAllKeysInternal();
        }
        return Task.FromResult(true);
    }

    private void ReleaseAllKeysInternal()
    {
        if (_heldUnicodeChar != '\0')
        {
            if (OperatingSystem.IsWindows())
            {
                var inputUp = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = (ushort)_heldUnicodeChar,
                            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = UIntPtr.Zero
                        }
                    }
                };
                SendInput(1, new[] { inputUp }, Marshal.SizeOf<INPUT>());
            }
            _heldUnicodeChar = '\0';
        }

        var inputsUp = new List<INPUT>(5);
        if (_heldVk != 0)
        {
            inputsUp.Add(MakeKeyInput(_heldVk, isKeyUp: true));
            _heldVk = 0;
        }
        if (_heldGui)
        {
            inputsUp.Add(MakeKeyInput(VK_LWIN, isKeyUp: true));
            _heldGui = false;
        }
        if (_heldAlt)
        {
            inputsUp.Add(MakeKeyInput(VK_LMENU, isKeyUp: true));
            _heldAlt = false;
        }
        if (_heldCtrl)
        {
            inputsUp.Add(MakeKeyInput(VK_LCONTROL, isKeyUp: true));
            _heldCtrl = false;
        }
        if (_heldShift)
        {
            inputsUp.Add(MakeKeyInput(VK_LSHIFT, isKeyUp: true));
            _heldShift = false;
        }

        if (inputsUp.Count > 0)
        {
            SendInputsAtomic(inputsUp.ToArray());
        }
    }

    private static bool IsExtendedKey(ushort vk)
    {
        return vk switch
        {
            0x21 => true, // VK_PRIOR (PageUp)
            0x22 => true, // VK_NEXT (PageDown)
            0x23 => true, // VK_END
            0x24 => true, // VK_HOME
            0x25 => true, // VK_LEFT
            0x26 => true, // VK_UP
            0x27 => true, // VK_RIGHT
            0x28 => true, // VK_DOWN
            0x2D => true, // VK_INSERT
            0x2E => true, // VK_DELETE
            0x5B => true, // VK_LWIN
            0x5C => true, // VK_RWIN
            0x5D => true, // VK_APPS
            0x6F => true, // VK_DIVIDE
            0xA3 => true, // VK_RCONTROL
            0xA5 => true, // VK_RMENU (Right Alt)
            _ => false
        };
    }

    private static INPUT MakeKeyInput(ushort vk, bool isKeyUp)
    {
        ushort scan = (ushort)MapVirtualKeyW(vk, 0); // MAPVK_VK_TO_VSC
        uint flags = KEYEVENTF_SCANCODE;
        if (isKeyUp) flags |= KEYEVENTF_KEYUP;
        if (IsExtendedKey(vk)) flags |= KEYEVENTF_EXTENDEDKEY;

        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };
    }

    private static void SendInputsAtomic(INPUT[] inputs)
    {
        if (!OperatingSystem.IsWindows() || inputs.Length == 0) return;
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent == 0)
        {
            foreach (var inp in inputs)
            {
                keybd_event((byte)inp.u.ki.wVk, (byte)inp.u.ki.wScan, inp.u.ki.dwFlags, UIntPtr.Zero);
            }
        }
    }

    private static ushort HidToVirtualKey(byte hidCode)
    {
        if (hidCode >= HidUsageCodes.KeyA && hidCode <= HidUsageCodes.KeyZ)
        {
            return (ushort)(0x41 + (hidCode - HidUsageCodes.KeyA));
        }

        if (hidCode >= HidUsageCodes.Key1 && hidCode <= HidUsageCodes.Key9)
        {
            return (ushort)(0x31 + (hidCode - HidUsageCodes.Key1));
        }

        if (hidCode == HidUsageCodes.Key0)
        {
            return 0x30;
        }

        return hidCode switch
        {
            HidUsageCodes.Enter => 0x0D,
            HidUsageCodes.Escape => 0x1B,
            HidUsageCodes.Backspace => 0x08,
            HidUsageCodes.Tab => 0x09,
            HidUsageCodes.Space => 0x20,
            HidUsageCodes.Minus => 0xBD,
            HidUsageCodes.Equal => 0xBB,
            HidUsageCodes.LeftBracket => 0xDB,
            HidUsageCodes.RightBracket => 0xDD,
            HidUsageCodes.Backslash => 0xDC,
            HidUsageCodes.Semicolon => 0xBA,
            HidUsageCodes.Quote => 0xDE,
            HidUsageCodes.Grave => 0xC0,
            HidUsageCodes.Comma => 0xBC,
            HidUsageCodes.Period => 0xBE,
            HidUsageCodes.Slash => 0xBF,
            _ => 0
        };
    }

    public DriverStatusMessage GetStatus()
    {
        return new DriverStatusMessage
        {
            State = DriverState.DevReceiverFallback,
            IsDeviceEnumerated = false,
            DevicePath = null,
            IsDevReceiverFallback = true,
            StatusDescription = "Using Windows SendInput user-mode input receiver. Virtual HID driver is not attached."
        };
    }

    public void ClearHistory()
    {
        _receivedReports.Clear();
        Interlocked.Exchange(ref _totalReportsSent, 0);
        Interlocked.Exchange(ref _totalKeysPressed, 0);
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            ReleaseAllKeysInternal();
        }
        _receivedReports.Clear();
    }
}
