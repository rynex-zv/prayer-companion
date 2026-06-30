# Current Job: Clean Theme B XAML

+ Remove Theme A XAML pages and shell.
+ Route startup and shell reloads through Theme B only.
+ Remove Theme Variant selector from diagnostics XAML.
+ Remove unused Theme Variant picker state from settings view model.
+ Remove obsolete Theme Variant labels from localization files.
+ Simplify ThemeManager so Theme B is the only applied style/color set.
+ Verify there are no Theme A or Theme Variant UI references left.
+ Build and run tests.
+ Remove obsolete ThemeVariant from AppSettings and all settings clone paths.
+ Delete unused ThemeVariant enum file.
+ Remove unused MAUI template MainPage XAML/code-behind.
+ Remove unused MainPage localization keys.
+ Remove unused dotnet_bot template image and project item.
+ Inventory hard-coded Theme B XAML colors that should become resources.
+ Move repeated SettingsPage icon colors into named page resources.
+ Re-run full reference scan after cleanup.
+ Re-run build and tests after second cleanup pass.
