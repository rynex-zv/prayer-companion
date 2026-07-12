using PrayAdFree.Core.Models;
using Pray_Ad_Free.Models;

namespace Pray_Ad_Free.Services;

public interface ITodayProjectionSource {
    string LocationTitle { get; }
    string HijriDate { get; }
    string GregorianDate { get; }
    PrayerId NextPrayerId { get; }
    string NextPrayerClock { get; }
    string NextPrayerBaseClock { get; }
    bool ShowNextPrayerBaseClock { get; }
    string NextPrayerDayId { get; }
    string Countdown { get; }
    string StatusMessage { get; }
    string ImsakTime { get; }
    string IftarTime { get; }
    bool IsImsakNext { get; }
    bool IsIftarNext { get; }
    string NextFastingCountdown { get; }
    ClockFormat CurrentClockFormat { get; }
    IEnumerable<PrayerTimeRow> TodayTimings { get; }
    Task RefreshAsync();
    void UpdateCountdown(DateTime now);
}

public interface ICalendarProjectionSource {
    DateTime SelectedMonth { get; set; }
    bool IsBusy { get; }
    string StatusMessage { get; }
    IReadOnlyList<CalendarDayRow> Days { get; }
    Task LoadAsync();
}

public interface IQiblaProjectionSource {
    double Bearing { get; }
    double Heading { get; }
    double NeedleRotation { get; }
    double CompassRotation { get; }
    string LocationTitle { get; }
    string DirectionLabel { get; }
    string StatusMessage { get; }
    LocationSettings? Location { get; }
    bool IsManualHeadingMode { get; }
    IEnumerable<OptionItem<QiblaHeadingMode>> HeadingModes { get; }
    OptionItem<QiblaHeadingMode>? SelectedHeadingMode { get; set; }
    Task LoadAsync();
    void UpdateHeading(double heading);
    void AdjustManualHeading(double delta);
    void CommitManualHeading();
}

public interface ITasbihProjectionSource {
    int Count { get; }
    string CurrentPhrase { get; }
    string ProgressText { get; }
    bool IsPresetSelectionEnabled { get; }
    IReadOnlyList<TasbihPresetItem> Presets { get; }
    TasbihPresetItem? SelectedPreset { get; }
    void Increment();
    void Reset();
    void SelectPreset(int index);
}
