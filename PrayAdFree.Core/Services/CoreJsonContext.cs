using System.Text.Json.Serialization;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(PrayerMonth))]
[JsonSerializable(typeof(WebState))]
[JsonSerializable(typeof(WebExecutionState))]
[JsonSerializable(typeof(List<IslamicOccasionCatalog.Entry>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(TasbihWidgetStorePayload))]
[JsonSerializable(typeof(AladhanPrayerTimesClient.AladhanCalendarResponse))]
[JsonSerializable(typeof(WidgetProfileDocument))]
[JsonSerializable(typeof(WidgetProjection))]
[JsonSerializable(typeof(WidgetRenderTree))]
[JsonSerializable(typeof(WindowsAdaptiveCardDocument))]
[JsonSerializable(typeof(WindowsWidgetProjectionBundle))]
[JsonSerializable(typeof(WidgetHostCapabilities))]
[JsonSerializable(typeof(WidgetProfilePatch))]
[JsonSerializable(typeof(WidgetProfile))]
[JsonSerializable(typeof(WidgetInstanceAssignment))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
