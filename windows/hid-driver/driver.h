#pragma once

#include <ntddk.h>
#include <wdf.h>
#include <initguid.h>
#include "ioctl.h"
#include "vhf_keyboard.h"

#define DRIVER_VERSION_MAJOR 1
#define DRIVER_VERSION_MINOR 0

typedef struct _DEVICE_CONTEXT {
    WDFDEVICE Device;
    WDFQUEUE DefaultQueue;
    PVHF_KEYBOARD_CONTEXT VhfKeyboardContext;
} DEVICE_CONTEXT, *PDEVICE_CONTEXT;

WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(DEVICE_CONTEXT, DeviceGetContext)

// Prototypes
DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD LANTypeEvtDeviceAdd;
EVT_WDF_DRIVER_UNLOAD LANTypeEvtDriverUnload;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL LANTypeEvtIoDeviceControl;
EVT_WDF_DEVICE_D0_ENTRY LANTypeEvtDeviceD0Entry;
EVT_WDF_DEVICE_D0_EXIT LANTypeEvtDeviceD0Exit;
