using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using WinRT;

namespace PrayAdFree.WidgetProvider;

internal static class ComIds {
    public const string ClassFactory = "00000001-0000-0000-C000-000000000046";
    public const string Unknown = "00000000-0000-0000-C000-000000000046";
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid(ComIds.ClassFactory)]
internal interface IClassFactory {
    [PreserveSig]
    int CreateInstance(nint outer, ref Guid interfaceId, out nint instance);

    [PreserveSig]
    int LockServer(bool locked);
}

[ComVisible(true)]
internal sealed class WidgetProviderFactory<T> : IClassFactory where T : IWidgetProvider, new() {
    public int CreateInstance(nint outer, ref Guid interfaceId, out nint instance) {
        instance = nint.Zero;
        if (outer != nint.Zero) return unchecked((int)0x80040110);
        if (interfaceId != typeof(T).GUID && interfaceId != Guid.Parse(ComIds.Unknown)) return unchecked((int)0x80004002);
        instance = MarshalInspectable<IWidgetProvider>.FromManaged(new T());
        return 0;
    }

    public int LockServer(bool locked) => 0;
}
