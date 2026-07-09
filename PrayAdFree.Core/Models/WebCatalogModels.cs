namespace PrayAdFree.Core.Models;

public sealed record WebLanguageOption(string Code, string Name, string Direction);

public sealed record WebCountryOption(string Code, string Name, string[] Cities);

public sealed record WebPlaceOption(string Country, string CountryCode, string City, double Latitude, double Longitude);

public sealed record WebPermissionItem(string Id, string Title, string Role, string Description, string Fallback, string Status, string Action);

public sealed record WebAboutInfo(string Name, string Maintainer, string Email, string Phone, string Website, string RemoteWebUrl);

public sealed record WebAdhanSoundOption(string Id, string Label, bool Selected, bool IsCustom, bool CanPreview);

public sealed record WebReminderOption(string Id, string Text, bool Enabled);

public sealed record WebShellTabOption(string Id, string LabelKey, string Icon);

public sealed record WebLabeledOption(string Id, string LabelKey);

public sealed record WebNativeActionResult(string Action, bool Ok, string Platform, string MessageKey);

public sealed record WebAdhanDefaults(
    int Volume,
    string CalculationMethod,
    string Madhhab,
    string HighLatitudeRule,
    double FajrAngle,
    double IshaAngle,
    string ClockFormat);

public sealed record WebNotificationDefaults(
    bool EnableAdhan,
    string MobilePrimaryAdhanType,
    bool HideOnCloseWindows,
    bool RunBackgroundServiceWindows,
    bool Vibration,
    string VibrationStrength,
    string VibrationPattern,
    int MinutesBefore);
