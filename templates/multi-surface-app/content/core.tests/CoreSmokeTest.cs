using TemplateApp.Core;

namespace TemplateApp.CoreTests;

public static class CoreSmokeTest {
    public static bool CanReadAppName() => AppCore.AppName.Length > 0;
}
