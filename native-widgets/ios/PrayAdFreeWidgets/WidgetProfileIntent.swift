import AppIntents

struct WidgetProfileEntity: AppEntity {
    static var typeDisplayRepresentation = TypeDisplayRepresentation(name: "Widget profile")
    static var defaultQuery = WidgetProfileQuery()
    let id: String
    let name: String
    var displayRepresentation: DisplayRepresentation { DisplayRepresentation(title: "\(name)") }
}

struct WidgetProfileQuery: EntityQuery {
    func entities(for identifiers: [String]) async throws -> [WidgetProfileEntity] {
        try suggestedEntities().filter { identifiers.contains($0.id) }
    }
    func suggestedEntities() throws -> [WidgetProfileEntity] {
        try SharedWidgetStore.load().profiles.map { WidgetProfileEntity(id: $0.id, name: $0.name) }
    }
}

struct SelectWidgetProfileIntent: WidgetConfigurationIntent {
    static var title: LocalizedStringResource = "Widget profile"
    static var description = IntentDescription("Selects the profile prepared by the app.")
    @Parameter(title: "Profile") var profile: WidgetProfileEntity?
}
