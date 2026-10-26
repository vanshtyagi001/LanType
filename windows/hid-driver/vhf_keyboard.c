#include "vhf_keyboard.h"

#define LANTYPE_POOL_TAG 'KnaL'

NTSTATUS
VhfKeyboardInitialize(
    _In_ WDFDEVICE Device,
    _Out_ PVHF_KEYBOARD_CONTEXT* VhfContext
)
{
    NTSTATUS status;
    PVHF_KEYBOARD_CONTEXT context = NULL;
    VHF_CONFIG vhfConfig;
    WDF_OBJECT_ATTRIBUTES spinLockAttributes;

    PAGED_CODE();

    context = (PVHF_KEYBOARD_CONTEXT)ExAllocatePool2(
        POOL_FLAG_NON_PAGED,
        sizeof(VHF_KEYBOARD_CONTEXT),
        LANTYPE_POOL_TAG
    );

    if (context == NULL) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "LANType: Failed to allocate VHF context\n"));
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    RtlZeroMemory(context, sizeof(VHF_KEYBOARD_CONTEXT));
    context->Device = Device;

    WDF_OBJECT_ATTRIBUTES_INIT(&spinLockAttributes);
    spinLockAttributes.ParentObject = Device;
    status = WdfSpinLockCreate(&spinLockAttributes, &context->SpinLock);
    if (!NT_SUCCESS(status)) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "LANType: WdfSpinLockCreate failed 0x%x\n", status));
        ExFreePoolWithTag(context, LANTYPE_POOL_TAG);
        return status;
    }

    // Initialize Microsoft Virtual HID Framework (VHF) configuration
    VHF_CONFIG_INIT(
        &vhfConfig,
        WdfDeviceWdmGetDeviceObject(Device),
        sizeof(g_VhfKeyboardReportDescriptor),
        (PUCHAR)g_VhfKeyboardReportDescriptor
    );

    vhfConfig.VhfClientContext = context;
    vhfConfig.VendorID = 0x1234;
    vhfConfig.ProductID = 0x5678;
    vhfConfig.VersionNumber = 0x0100;

    // Create the virtual HID device
    status = VhfCreate(&vhfConfig, &context->VhfHandle);
    if (!NT_SUCCESS(status)) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "LANType: VhfCreate failed 0x%x\n", status));
        ExFreePoolWithTag(context, LANTYPE_POOL_TAG);
        return status;
    }

    // Start the device so Windows enumerates it in Device Manager
    status = VhfStart(context->VhfHandle);
    if (!NT_SUCCESS(status)) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "LANType: VhfStart failed 0x%x\n", status));
        VhfDelete(context->VhfHandle, TRUE);
        ExFreePoolWithTag(context, LANTYPE_POOL_TAG);
        return status;
    }

    context->IsStarted = TRUE;
    *VhfContext = context;

    KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "LANType: VHF Virtual Keyboard created and started successfully\n"));
    return STATUS_SUCCESS;
}

NTSTATUS
VhfKeyboardSubmitReport(
    _In_ PVHF_KEYBOARD_CONTEXT VhfContext,
    _In_ PLANTYPE_HID_KEYBOARD_REPORT Report
)
{
    NTSTATUS status;
    HID_XFER_PACKET xferPacket;

    if (VhfContext == NULL || !VhfContext->IsStarted) {
        return STATUS_DEVICE_NOT_READY;
    }

    RtlZeroMemory(&xferPacket, sizeof(HID_XFER_PACKET));
    xferPacket.reportBuffer = (PUCHAR)Report;
    xferPacket.reportBufferLen = sizeof(LANTYPE_HID_KEYBOARD_REPORT);
    xferPacket.reportId = 0; // No Report ID used in standard descriptor

    WdfSpinLockAcquire(VhfContext->SpinLock);
    
    // Submit the HID report to Windows Virtual HID Framework
    status = VhfReadReportSubmit(VhfContext->VhfHandle, &xferPacket);
    if (NT_SUCCESS(status)) {
        VhfContext->ReportsSubmitted++;
    }

    WdfSpinLockRelease(VhfContext->SpinLock);

    if (!NT_SUCCESS(status)) {
        KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_WARNING_LEVEL, "LANType: VhfReadReportSubmit failed 0x%x\n", status));
    }

    return status;
}

NTSTATUS
VhfKeyboardResetKeys(
    _In_ PVHF_KEYBOARD_CONTEXT VhfContext
)
{
    LANTYPE_HID_KEYBOARD_REPORT emptyReport;
    RtlZeroMemory(&emptyReport, sizeof(LANTYPE_HID_KEYBOARD_REPORT));
    return VhfKeyboardSubmitReport(VhfContext, &emptyReport);
}

VOID
VhfKeyboardCleanup(
    _In_ PVHF_KEYBOARD_CONTEXT VhfContext
)
{
    if (VhfContext != NULL) {
        if (VhfContext->IsStarted && VhfContext->VhfHandle != NULL) {
            // Delete the virtual device synchronously
            VhfDelete(VhfContext->VhfHandle, TRUE);
            VhfContext->VhfHandle = NULL;
            VhfContext->IsStarted = FALSE;
        }

        ExFreePoolWithTag(VhfContext, LANTYPE_POOL_TAG);
    }
}
