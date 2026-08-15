import AppIntents
import SwiftUI
import WidgetKit

struct RenderEntry: TimelineEntry {
    let date: Date
    let tree: SharedRenderTree?
    let lastUpdated: Date?
    let error: String?
}

struct RenderTimelineProvider: AppIntentTimelineProvider {
    func placeholder(in context: Context) -> RenderEntry { RenderEntry(date: .now, tree: nil, lastUpdated: nil, error: "Open the app to prepare widget data.") }
    func snapshot(for configuration: SelectWidgetProfileIntent, in context: Context) async -> RenderEntry { load(configuration, family: context.family) }
    func timeline(for configuration: SelectWidgetProfileIntent, in context: Context) async -> Timeline<RenderEntry> {
        let entry = load(configuration, family: context.family)
        let target = entry.tree?.countdownTargetUnixMilliseconds.map { Date(timeIntervalSince1970: Double($0) / 1000) }
        let refresh = target.map { max($0, Date().addingTimeInterval(60)) } ?? Date().addingTimeInterval(15 * 60)
        return Timeline(entries: [entry], policy: .after(refresh))
    }
    private func load(_ configuration: SelectWidgetProfileIntent, family: WidgetFamily) -> RenderEntry {
        do {
            let payload = try SharedWidgetStore.load()
            let profileId = configuration.profile?.id ?? payload.profiles.first?.id
            let tree = profileId.flatMap { SharedWidgetStore.tree(in: payload, profileId: $0, family: Self.familyKey(family)) }
            return RenderEntry(date: .now, tree: tree, lastUpdated: Date(timeIntervalSince1970: Double(payload.generatedAtUnixMilliseconds) / 1000), error: tree == nil ? "No widget projection is available." : nil)
        } catch {
            return RenderEntry(date: .now, tree: nil, lastUpdated: nil, error: "Widget data is unavailable.")
        }
    }
    private static func familyKey(_ family: WidgetFamily) -> String {
        switch family {
        case .accessoryInline: return "Inline"
        case .accessoryCircular: return "Circular"
        case .accessoryRectangular: return "Rectangular"
        case .systemSmall: return "Small"
        case .systemMedium: return "Medium"
        case .systemLarge: return "Large"
        default: return "Medium"
        }
    }
}

struct RenderTreeView: View {
    let entry: RenderEntry
    @Environment(\.widgetFamily) private var family
    @Environment(\.isLuminanceReduced) private var luminanceReduced

    var body: some View {
        if let tree = entry.tree, tree.status == "ready" {
            VStack(alignment: tree.isRtl ? .trailing : .leading, spacing: 3) {
                ForEach(Array(tree.texts.prefix(textLimit))) { item in
                    Text(item.text ?? "")
                        .font(font(for: item, scale: tree.style.textScale))
                        .foregroundStyle(Color(argb: item.key == "nextPrayerName" ? tree.style.primaryTextColor : tree.style.secondaryTextColor))
                        .accessibilityLabel(item.accessibilityLabel)
                }
                if let target = tree.countdownTargetUnixMilliseconds {
                    Text(timerInterval: Date.now...Date(timeIntervalSince1970: Double(target) / 1000), countsDown: true)
                        .monospacedDigit()
                        .accessibilityLabel("Countdown")
                }
                if let progress = tree.progress { ProgressView(value: min(1, max(0, progress))).tint(Color(argb: tree.style.accentColor)) }
                ForEach(Array(tree.rows.prefix(rowLimit))) { item in HStack { Text(item.label ?? ""); Spacer(); Text(item.value ?? "") }.font(.caption).accessibilityLabel(item.accessibilityLabel) }
                ForEach(Array(tree.actions.prefix(actionLimit))) { action in
                    if let url = URL(string: action.deepLink) { Link(action.label, destination: url).accessibilityLabel(action.accessibilityLabel) }
                }
            }
            .environment(\.layoutDirection, tree.isRtl ? .rightToLeft : .leftToRight)
            .opacity(luminanceReduced ? 0.75 : 1)
        } else {
            VStack { Text(entry.error ?? entry.tree?.error ?? "Widget data is unavailable."); if let date = entry.lastUpdated { Text(date, style: .relative).font(.caption2) } }.accessibilityElement(children: .combine)
        }
    }
    private var textLimit: Int { family == .accessoryInline ? 1 : family == .accessoryCircular ? 1 : family == .systemLarge ? 6 : 3 }
    private var rowLimit: Int { family == .systemLarge ? 6 : family == .systemMedium ? 4 : family == .accessoryRectangular ? 2 : 0 }
    private var actionLimit: Int { family == .systemMedium || family == .systemLarge ? 2 : 1 }
    private func font(for item: SharedRenderItem, scale: String) -> Font {
        let base: Font = item.key.contains("Name") || item.key == "qiblaBearing" ? .headline : .body
        switch scale { case "Small": return .caption; case "Large": return .title3; case "ExtraLarge": return .title2; default: return base }
    }
}

private extension Color {
    init(argb: String) {
        let cleaned = argb.trimmingCharacters(in: CharacterSet(charactersIn: "#"))
        let value = UInt64(cleaned, radix: 16) ?? 0xFFFFFFFF
        let hasAlpha = cleaned.count == 8
        let alpha = hasAlpha ? Double((value >> 24) & 0xFF) / 255 : 1
        let red = Double((value >> 16) & 0xFF) / 255
        let green = Double((value >> 8) & 0xFF) / 255
        let blue = Double(value & 0xFF) / 255
        self.init(.sRGB, red: red, green: green, blue: blue, opacity: alpha)
    }
}

struct PrayAdFreeWidget: Widget {
    let kind = "PrayAdFreeWidget"
    var body: some WidgetConfiguration {
        AppIntentConfiguration(kind: kind, intent: SelectWidgetProfileIntent.self, provider: RenderTimelineProvider()) { entry in
            RenderTreeView(entry: entry).containerBackground(
                entry.tree.map { Color(argb: $0.style.backgroundColor).opacity(Double($0.style.backgroundOpacity) / 100) } ?? Color.clear,
                for: .widget)
        }
        .configurationDisplayName("Pray Ad Free")
        .description("Shows a profile calculated by Pray Ad Free.")
        .supportedFamilies([.accessoryInline, .accessoryCircular, .accessoryRectangular, .systemSmall, .systemMedium, .systemLarge])
    }
}

@main struct PrayAdFreeWidgetBundle: WidgetBundle { var body: some Widget { PrayAdFreeWidget() } }
