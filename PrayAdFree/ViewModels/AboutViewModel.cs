namespace Pray_Ad_Free.ViewModels;

public sealed class AboutViewModel : ViewModelBase {
    public string AppName => "Pray Ad Free";
    public string Tagline => "Free prayer times for everyone.";
    public string Source => "Prayer times source: Aladhan API";
    public string Privacy => "No tracking. No ads. Location is only used to calculate times.";
    public string Maintainer => "Built and maintained by Rynex.";
    public string Contact => "Questions, bug reports, and feedback are welcome.";
    public string Email => "rynex@rynex.nl";
    public string Phone => "+31 6 10331734";
    public string Website => "https://rynex.nl/cv";
    public string WebsiteNote => "Website is still in development.";
}
