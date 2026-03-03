using System.Collections.ObjectModel;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class TasbihViewModel : ViewModelBase {
    private int _count;

    public TasbihViewModel() {
        Phrases = new ObservableCollection<TasbihPhraseItem>();
        IncrementCommand = new Command(Increment);
        ResetCommand = new Command(Reset);
        BuildPhrases();
        LocalizationManager.LanguageChanged += (_, _) => BuildPhrases();
    }

    public ObservableCollection<TasbihPhraseItem> Phrases { get; }
    public Command IncrementCommand { get; }
    public Command ResetCommand { get; }

    public int Count {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    private void Increment() {
        Count++;
    }

    private void Reset() {
        Count = 0;
    }

    private void BuildPhrases() {
        Phrases.Clear();
        Phrases.Add(new TasbihPhraseItem(
            LocalizationManager.Translate("Tasbih_SubhanAllah"),
            LocalizationManager.Translate("Tasbih_SubhanAllah_Info")));
        Phrases.Add(new TasbihPhraseItem(
            LocalizationManager.Translate("Tasbih_Alhamdulillah"),
            LocalizationManager.Translate("Tasbih_Alhamdulillah_Info")));
        Phrases.Add(new TasbihPhraseItem(
            LocalizationManager.Translate("Tasbih_AllahuAkbar"),
            LocalizationManager.Translate("Tasbih_AllahuAkbar_Info")));
        Phrases.Add(new TasbihPhraseItem(
            LocalizationManager.Translate("Tasbih_LaIlahaIllaAllah"),
            LocalizationManager.Translate("Tasbih_LaIlahaIllaAllah_Info")));
        Phrases.Add(new TasbihPhraseItem(
            LocalizationManager.Translate("Tasbih_Astaghfirullah"),
            LocalizationManager.Translate("Tasbih_Astaghfirullah_Info")));
        Phrases.Add(new TasbihPhraseItem(
            LocalizationManager.Translate("Tasbih_LaHawla"),
            LocalizationManager.Translate("Tasbih_LaHawla_Info")));
        Phrases.Add(new TasbihPhraseItem(
            LocalizationManager.Translate("Tasbih_Salawat"),
            LocalizationManager.Translate("Tasbih_Salawat_Info")));
    }
}
