using System.Runtime.InteropServices.JavaScript;
using TemplateApp.Core;

namespace TemplateApp.WebBridge;

public static partial class WebBridge {
    [JSExport]
    public static string Ping() => AppCore.AppName;
}
