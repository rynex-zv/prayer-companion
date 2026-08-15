# Windows native widgets

`PrayAdFree.WidgetProvider` is the offline Windows 11 Widgets Board COM provider. It consumes only atomically published `WidgetRenderTree` values from `windows_widget_projections.json`; it does not call the web site, calculate prayer times, or invent missing sizes.

The provider and `Package.widget-fragment.xml` are intentionally not included in the current unpackaged production build. Production enablement requires a single signed PrayAdFree MSIX that contains the MAUI app and this provider, migration from the unpackaged data directory, install/upgrade acceptance, and real Widgets Board tests. Until those gates pass, the plan status remains **لم تتم الإضافة**.
