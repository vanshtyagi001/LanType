#pragma once

#ifdef _KERNEL_MODE
#include <ntddk.h>
#include <wdf.h>
#else
#include <windows.h>
#include <winioctl.h>
#endif

// {7A5B536E-6F31-4F98-84CE-D790BC914C7A}
// LANType Virtual HID Keyboard Device Interface GUID
#ifdef DEFINE_GUID
DEFINE_GUID(GUID_DEVINTERFACE_LANTYPE_VHID,
    0x7a5b536e, 0x6f31, 0x4f98, 0x84, 0xce, 0xd7, 0x90, 0xbc, 0x91, 0x4c, 0x7a);
#endif

#define LANTYPE_DEVICE_NAME        L"\\Device\\LANTypeVhidDevice"
#define LANTYPE_DOS_DEVICE_NAME    L"\\DosDevices\\LANTypeVhidDevice"
#define LANTYPE_USERMODE_SYMLINK   L"\\\\.\\LANTypeVhidDevice"

// Standard 8-byte USB HID Keyboard Report:
// Byte 0: Modifiers bitmask (Ctrl, Shift, Alt, GUI)
// Byte 1: Reserved (0x00)
// Bytes 2-7: Up to 6 simultaneous key usage codes
#pragma pack(push, 1)
typedef struct _LANTYPE_HID_KEYBOARD_REPORT
{
    UCHAR Modifiers;
    UCHAR Reserved;
    UCHAR KeyCodes[6];
} LANTYPE_HID_KEYBOARD_REPORT, *PLANTYPE_HID_KEYBOARD_REPORT;

typedef struct _LANTYPE_DRIVER_STATUS
{
    ULONG DriverVersion;
    BOOLEAN IsVhfInitialized;
    BOOLEAN IsDeviceEnumerated;
    ULONG TotalReportsSubmitted;
} LANTYPE_DRIVER_STATUS, *PLANTYPE_DRIVER_STATUS;
#pragma pack(pop)

// IOCTL Definitions using FILE_DEVICE_KEYBOARD (0x0000000B)
#define IOCTL_LANTYPE_SEND_REPORT \
    CTL_CODE(FILE_DEVICE_KEYBOARD, 0x801, METHOD_BUFFERED, FILE_WRITE_ACCESS)

#define IOCTL_LANTYPE_RESET_KEYS \
    CTL_CODE(FILE_DEVICE_KEYBOARD, 0x802, METHOD_BUFFERED, FILE_WRITE_ACCESS)

#define IOCTL_LANTYPE_GET_STATUS \
    CTL_CODE(FILE_DEVICE_KEYBOARD, 0x803, METHOD_BUFFERED, FILE_READ_ACCESS)
