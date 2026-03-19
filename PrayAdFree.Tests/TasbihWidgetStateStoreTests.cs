using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class TasbihWidgetStateStoreTests {
    [Fact]
    public void TasbihProgressCalculator_RepeatResetWrapsToZero() {
        var preset = new TasbihPresetSettings {
            RepeatMode = TasbihRepeatMode.RepeatReset,
            Items = [
                new TasbihItemSettings { Text = "A", TargetCount = 2 }
            ]
        };
        var calculator = new TasbihProgressCalculator();

        var next = calculator.GetNextCount(preset, 1);

        Assert.Equal(0, next);
    }

    [Fact]
    public void TasbihProgressCalculator_RepeatContinueCyclesPhrase() {
        var preset = new TasbihPresetSettings {
            RepeatMode = TasbihRepeatMode.RepeatContinue,
            Items = [
                new TasbihItemSettings { Text = "A", TargetCount = 2 },
                new TasbihItemSettings { Text = "B", TargetCount = 1 }
            ]
        };
        var calculator = new TasbihProgressCalculator();

        var snapshot = calculator.BuildSnapshot(preset, 4);

        Assert.Equal("A", snapshot.CurrentText);
        Assert.Equal(1, snapshot.LocalCount);
        Assert.Equal(2, snapshot.LocalTarget);
    }

    [Fact]
    public void TasbihWidgetStateStore_PersistsIndependentWidgetStates() {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try {
            var store = new TasbihWidgetStateStore(path);
            store.Save(new TasbihWidgetState {
                AppWidgetId = 10,
                PresetIndex = 1,
                Count = 7,
                LastUpdatedUtc = DateTime.UtcNow
            });
            store.Save(new TasbihWidgetState {
                AppWidgetId = 11,
                PresetIndex = 2,
                Count = 4,
                LastUpdatedUtc = DateTime.UtcNow
            });

            var states = store.Load();

            Assert.Equal(2, states.Count);
            Assert.Equal(7, states[10].Count);
            Assert.Equal(4, states[11].Count);
        } finally {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        }
    }
}
