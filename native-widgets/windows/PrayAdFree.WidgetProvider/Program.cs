using System.Runtime.InteropServices;

namespace PrayAdFree.WidgetProvider;

internal static class Program {
    internal static readonly Guid ProviderClassId = Guid.Parse("9d71dc86-64bb-48d2-a842-9df78ddf86f7");

    [STAThread]
    private static int Main() {
        var result = CoRegisterClassObject(ProviderClassId, new WidgetProviderFactory<PrayerWidgetProvider>(), 0x4, 0x1, out var cookie);
        if (result < 0) return result;
        try {
            PrayerWidgetProvider.ExitEvent.WaitOne();
            return 0;
        } finally {
            CoRevokeClassObject(cookie);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        [MarshalAs(UnmanagedType.LPStruct)] Guid classId,
        [MarshalAs(UnmanagedType.IUnknown)] object classFactory,
        uint classContext,
        uint registrationFlags,
        out uint cookie);

    [DllImport("ole32.dll")]
    private static extern int CoRevokeClassObject(uint cookie);
}
