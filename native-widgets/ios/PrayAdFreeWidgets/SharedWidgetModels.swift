import Foundation
import WidgetKit

struct SharedRenderItem: Codable, Identifiable {
    let key: String
    let text: String?
    let label: String?
    let value: String?
    let accessibilityLabel: String
    var id: String { key }
}

struct SharedRenderAction: Codable, Identifiable {
    let id: String
    let label: String
    let deepLink: String
    let accessibilityLabel: String
}

struct SharedWidgetStyle: Codable {
    let textScale: String
    let primaryTextColor: String
    let secondaryTextColor: String
    let backgroundColor: String
    let accentColor: String
    let backgroundOpacity: Int
    let followAppTheme: Bool
}

struct SharedRenderTree: Codable {
    let profileId: String
    let profileRevision: Int64
    let status: String
    let error: String
    let isRtl: Bool
    let texts: [SharedRenderItem]
    let rows: [SharedRenderItem]
    let actions: [SharedRenderAction]
    let countdownTargetUnixMilliseconds: Int64?
    let progress: Double?
    let style: SharedWidgetStyle
    let omittedProjection: [String]
    let warnings: [String]
}

struct SharedProfile: Codable, Hashable, Identifiable {
    let id: String
    let name: String
}

struct SharedWidgetPayload: Codable {
    let generatedAtUnixMilliseconds: Int64
    let profiles: [SharedProfile]
    let trees: [String: SharedRenderTree]
}

enum SharedWidgetStore {
    static let appGroup = "group.com.rynex.prayer.widgets"
    static let fileName = "widget-render-trees.json"

    static func load() throws -> SharedWidgetPayload {
        guard let root = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroup) else {
            throw CocoaError(.fileNoSuchFile)
        }
        return try JSONDecoder().decode(SharedWidgetPayload.self, from: Data(contentsOf: root.appendingPathComponent(fileName)))
    }

    static func tree(in payload: SharedWidgetPayload, profileId: String, family: String) -> SharedRenderTree? {
        payload.trees["\(profileId):\(family)"] ?? payload.trees[profileId]
    }
}
