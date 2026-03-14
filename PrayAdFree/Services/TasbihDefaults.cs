using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public static class TasbihDefaults {
    public static TasbihSettings BuildDefaults() {
        var presets = new List<TasbihPresetSettings> {
            new TasbihPresetSettings {
                Name = "TasbihPreset_AfterPrayer",
                RepeatMode = TasbihRepeatMode.RepeatReset,
                Items = new List<TasbihItemSettings> {
                    new TasbihItemSettings {
                        Text = "Tasbih_SubhanAllah",
                        TargetCount = 33
                    },
                    new TasbihItemSettings {
                        Text = "Tasbih_Alhamdulillah",
                        TargetCount = 33
                    },
                    new TasbihItemSettings {
                        Text = "Tasbih_AllahuAkbar",
                        TargetCount = 34
                    }
                }
            },
            new TasbihPresetSettings {
                Name = "TasbihPreset_Hundred",
                RepeatMode = TasbihRepeatMode.None,
                Items = new List<TasbihItemSettings> {
                    new TasbihItemSettings {
                        Text = "Tasbih_SubhanAllah",
                        TargetCount = 100
                    }
                }
            },
            new TasbihPresetSettings {
                Name = "TasbihPreset_Salawat",
                RepeatMode = TasbihRepeatMode.None,
                Items = new List<TasbihItemSettings> {
                    new TasbihItemSettings {
                        Text = "Tasbih_Salawat",
                        TargetCount = 100
                    }
                }
            }
        };

        return new TasbihSettings {
            Presets = presets,
            SelectedPresetIndex = 0
        };
    }
}
