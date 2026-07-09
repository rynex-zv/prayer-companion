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
[JsonSerializable(typeof(TasbihWidgetStorePayload))]
[JsonSerializable(typeof(AladhanPrayerTimesClient.AladhanCalendarResponse))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
