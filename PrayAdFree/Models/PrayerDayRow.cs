namespace Pray_Ad_Free.Models;

public sealed class PrayerDayRow {
    public string Date { get; init; } = "";
    public string Hijri { get; init; } = "";

    public string Fajr { get; init; } = "";
    public string FajrBase { get; init; } = "";
    public bool ShowFajrBase { get; init; }

    public string Dhuhr { get; init; } = "";
    public string DhuhrBase { get; init; } = "";
    public bool ShowDhuhrBase { get; init; }

    public string Asr { get; init; } = "";
    public string AsrBase { get; init; } = "";
    public bool ShowAsrBase { get; init; }

    public string Maghrib { get; init; } = "";
    public string MaghribBase { get; init; } = "";
    public bool ShowMaghribBase { get; init; }

    public string Isha { get; init; } = "";
    public string IshaBase { get; init; } = "";
    public bool ShowIshaBase { get; init; }
}
