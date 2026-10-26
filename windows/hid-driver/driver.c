#include "driver.h"

// Restrict access to LocalSystem (SY) and Administrators (BA)
#define LANTYPE_DEVICE_SDDL L"D:P(A;;GA;;;SY)(A;;GA;;;BA)"

NTSTATUS
DriverEntry(
    _In_ PDRIVER_OBJECT DriverObject,
    _In_ PUNICODE_STRING RegistryPath
)
{
    NTSTATUS status;
    WDF_DRIVER_CONFIG config;

    KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "LANType: DriverEntry loading...\n"));

    WDF_DRIVER_CONFIG_INIT(&config, LANTypeEvtDeviceAdd);
    config.EvtDriverUnload = LANTypeEvtDriverUnload;

    status = WdfDriverCreate(
        DriverObject,
        RegistryPath,
        WDF_NO_OBJECT_ATTRIBUTES,
        &config,
        WDF_NO_HANDLE
    );

    if (!NT_SUCCESS(status)) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "LANType: WdfDriverCreate failed 0x%x\n", status));
    }

    return status;
}

NTSTATUS
LANTypeEvtDeviceAdd(
    _In_ WDFDRIVER Driver,
    _Inout_ PWDFDEVICE_INIT DeviceInit
)
{
    NTSTATUS status;
    WDF_OBJECT_ATTRIBUTES attributes;
    WDFDEVICE device;
    PDEVICE_CONTEXT context;
    WDF_IO_QUEUE_CONFIG queueConfig;
    DECLARE_CONST_UNICODE_STRING(ntDeviceName, LANTYPE_DEVICE_NAME);
    DECLARE_CONST_UNICODE_STRING(dosDeviceName, LANTYPE_DOS_DEVICE_NAME);
    DECLARE_CONST_UNICODE_STRING(sddlString, LANTYPE_DEVICE_SDDL);
    WDF_PNPPOWER_EVENT_CALLBACKS pnpPowerCallbacks;

    UNREFERENCED_PARAMETER(Driver);
    PAGED_CODE();

    KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "LANType: EvtDeviceAdd...\n"));

    // Set device name for user-mode IOCTL access
    status = WdfDeviceInitAssignName(DeviceInit, &ntDeviceName);
    if (!NT_SUCCESS(status)) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "LANType: WdfDeviceInitAssignName failed 0x%x\n", status));
        return status;
    }

    // Apply strict security descriptor (System and Admin only)
    status = WdfDeviceInitAssignSDDLString(DeviceInit, &sddlString);
    if (!NT_SUCCESS(status)) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "LANType: WdfDeviceInitAssignSDDLString failed 0x%x\n", status));
        return status;
    }

    WdfDeviceInitSetDeviceType(DeviceInit, FILE_DEVICE_KEYBOARD);
    WdfDeviceInitSetCharacteristics(DeviceInit, FILE_DEVICE_SECURE_OPEN, FALSE);

    // Register Power Management callbacks to prevent stuck keys on sleep/wake
    WDF_PNPPOWER_EVENT_CALLBACKS_INIT(&pnpPowerCallbacks);
    pnpPowerCallbacks.EvtDeviceD0Entry = LANTypeEvtDeviceD0Entry;
    pnpPowerCallbacks.EvtDeviceD0Exit = LANTypeEvtDeviceD0Exit;
    WdfDeviceInitSetPnpPowerEventCallbacks(DeviceInit, &pnpPowerCallbacks);

    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attributes, DEVICE_CONTEXT);

    status = WdfDeviceCreate(&DeviceInit, &attributes, &device);
    if (!NT_SUCCESS(status)) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "LANType: WdfDeviceCreate failed 0x%x\n", status));
        return status;
    }

    context = DeviceGetContext(device);
    context->Device = device;

    // Create user-mode symbolic link
    status = WdfDeviceCreateSymbolicLink(device, &dosDeviceName);
    if (!NT_SUCCESS(status)) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "LANType: WdfDeviceCreateSymbolicLink failed 0x%x\n", status));
        return status;
    }

    // Initialize Microsoft Virtual HID Framework (VHF) Virtual Keyboard
    status = VhfKeyboardInitialize(device, &context->VhfKeyboardContext);
    if (!NT_SUCCESS(status)) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "LANType: VhfKeyboardInitialize failed 0x%x\n", status));
        return status;
    }

    // Configure Default I/O Queue for IOCTL handling
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queueConfig, WdfIoQueueDispatchSequential);
    queueConfig.EvtIoDeviceControl = LANTypeEvtIoDeviceControl;

    status = WdfIoQueueCreate(device, &queueConfig, WDF_NO_OBJECT_ATTRIBUTES, &context->DefaultQueue);
    if (!NT_SUCCESS(status)) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "LANType: WdfIoQueueCreate failed 0x%x\n", status));
        return status;
    }

    KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "LANType: Device added and initialized successfully\n"));
    return STATUS_SUCCESS;
}

VOID
LANTypeEvtIoDeviceControl(
    _In_ WDFQUEUE Queue,
    _In_ WDFREQUEST Request,
    _In_ size_t OutputBufferLength,
    _In_ size_t InputBufferLength,
    _In_ ULONG IoControlCode
)
{
    NTSTATUS status = STATUS_INVALID_DEVICE_REQUEST;
    PDEVICE_CONTEXT context = DeviceGetContext(WdfIoQueueGetDevice(Queue));
    size_t bytesReturned = 0;

    switch (IoControlCode)
    {
    case IOCTL_LANTYPE_SEND_REPORT:
    {
        PLANTYPE_HID_KEYBOARD_REPORT report = NULL;

        if (InputBufferLength < sizeof(LANTYPE_HID_KEYBOARD_REPORT)) {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }

        status = WdfRequestRetrieveInputBuffer(
            Request,
            sizeof(LANTYPE_HID_KEYBOARD_REPORT),
            (PVOID*)&report,
            NULL
        );

        if (NT_SUCCESS(status) && report != NULL) {
            status = VhfKeyboardSubmitReport(context->VhfKeyboardContext, report);
        }
        break;
    }

    case IOCTL_LANTYPE_RESET_KEYS:
    {
        status = VhfKeyboardResetKeys(context->VhfKeyboardContext);
        break;
    }

    case IOCTL_LANTYPE_GET_STATUS:
    {
        PLANTYPE_DRIVER_STATUS driverStatus = NULL;

        if (OutputBufferLength < sizeof(LANTYPE_DRIVER_STATUS)) {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }

        status = WdfRequestRetrieveOutputBuffer(
            Request,
            sizeof(LANTYPE_DRIVER_STATUS),
            (PVOID*)&driverStatus,
            NULL
        );

        if (NT_SUCCESS(status) && driverStatus != NULL) {
            driverStatus->DriverVersion = (DRIVER_VERSION_MAJOR << 16) | DRIVER_VERSION_MINOR;
            driverStatus->IsVhfInitialized = (context->VhfKeyboardContext != NULL);
            driverStatus->IsDeviceEnumerated = (context->VhfKeyboardContext != NULL && context->VhfKeyboardContext->IsStarted);
            driverStatus->TotalReportsSubmitted = context->VhfKeyboardContext ? context->VhfKeyboardContext->ReportsSubmitted : 0;
            bytesReturned = sizeof(LANTYPE_DRIVER_STATUS);
        }
        break;
    }

    default:
        status = STATUS_INVALID_DEVICE_REQUEST;
        break;
    }

    WdfRequestCompleteWithInformation(Request, status, bytesReturned);
}

NTSTATUS
LANTypeEvtDeviceD0Entry(
    _In_ WDFDEVICE Device,
    _In_ WDF_POWER_DEVICE_STATE PreviousState
)
{
    PDEVICE_CONTEXT context = DeviceGetContext(Device);
    UNREFERENCED_PARAMETER(PreviousState);

    // On wake up, ensure all keys are in a released state
    if (context->VhfKeyboardContext != NULL) {
        VhfKeyboardResetKeys(context->VhfKeyboardContext);
    }
    return STATUS_SUCCESS;
}

NTSTATUS
LANTypeEvtDeviceD0Exit(
    _In_ WDFDEVICE Device,
    _In_ WDF_POWER_DEVICE_STATE TargetState
)
{
    PDEVICE_CONTEXT context = DeviceGetContext(Device);
    UNREFERENCED_PARAMETER(TargetState);

    // On system suspend / sleep, release all held keys to prevent stuck keys
    if (context->VhfKeyboardContext != NULL) {
        VhfKeyboardResetKeys(context->VhfKeyboardContext);
    }
    return STATUS_SUCCESS;
}

VOID
LANTypeEvtDriverUnload(
    _In_ WDFDRIVER Driver
)
{
    UNREFERENCED_PARAMETER(Driver);
    PAGED_CODE();

    KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "LANType: Driver unloaded\n"));
}
