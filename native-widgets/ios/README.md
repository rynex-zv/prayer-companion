# iOS/iPadOS WidgetKit extension source

This directory is intentionally isolated from production until it is added as an Xcode Widget Extension target on a Mac, signed with the main app, and accepted on a real iPhone/iPad.

- Bundle ID: `com.rynex.prayer.widgets`
- App Group: `group.com.rynex.prayer.widgets`
- Shared payload: `widget-render-trees.json`
- The extension decodes ready `WidgetRenderTree` payloads only. It contains no prayer calculation and has no network fallback.
- Add `PrayAdFreeWidgets.entitlements` to both the extension and the main app before enabling the target.
- The target must include WidgetKit, SwiftUI, and AppIntents and support iOS/iPadOS 17 or later for AppIntent configuration.

Do not include this extension in a Release until its Mac build, update install, App Group, timelines, every family, RTL, Dynamic Type, reduced luminance, and a real lock/home-screen test pass.
