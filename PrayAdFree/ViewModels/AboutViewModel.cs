namespace Pray_Ad_Free.ViewModels;

public sealed class AboutViewModel : ViewModelBase {
    public string AppName => "Pray Ad Free";
    public string Tagline => "Free prayer times for everyone.";
    public string Source => "Prayer times source: Aladhan API";
    public string Privacy => "No tracking. No ads. Location is only used to calculate times.";
    public string Contact => "Report issues at github.com/rynex-zv/PrayAdFree";
}
