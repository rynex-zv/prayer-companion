using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Widgets;

public sealed class WidgetLayoutResolverTests {
    [Fact]
    public void NeverExceedsHostCapacity() {
        var profile = new WidgetProfileService(new InMemoryWidgetProfileRepository()).Snapshot().Profiles
            .Single(item => item.Template == WidgetTemplateKind.DailyPrayer);
        var projection = Projection();
        var host = new WidgetHostCapabilities { Family = WidgetFamily.Small, MaxTextItems = 4, MaxActions = 0 };

        var tree = new WidgetLayoutResolver().Resolve(profile, projection, host);

        Assert.True(tree.Texts.Count + tree.Rows.Count <= 4);
        Assert.Empty(tree.Actions);
    }

    [Fact]
    public void LockScreenPrivacyRemovesLocationFields() {
        var service = new WidgetProfileService(new InMemoryWidgetProfileRepository());
        var source = service.Snapshot().Profiles.Single(item => item.Template == WidgetTemplateKind.NextPrayer);
        var profile = service.Update(source.Id, new WidgetProfilePatch {
            Projection = ["nextPrayerName", "nextPrayerTime", "location", "locationSource"]
        });

        var tree = new WidgetLayoutResolver().Resolve(profile, Projection(), new WidgetHostCapabilities {
            Surface = WidgetSurface.LockScreen,
            Family = WidgetFamily.Rectangular
        });

        Assert.DoesNotContain(tree.Texts, item => item.Key is "location" or "locationSource");
        Assert.Empty(tree.OmittedProjection);
    }

    [Fact]
    public void UnsupportedVisualCapabilitiesAreReportedTruthfully() {
        var profile = new WidgetProfileService(new InMemoryWidgetProfileRepository()).Snapshot().Profiles[0];
        var tree = new WidgetLayoutResolver().Resolve(profile, Projection(), new WidgetHostCapabilities {
            SupportsFullColor = false,
            SupportsBackgroundColor = false,
            SupportsBackgroundOpacity = false
        });

        Assert.Contains("host-forces-tinted-rendering", tree.Warnings);
    }

    [Fact]
    public void MissingProjectionProducesExplicitErrorTree() {
        var profile = new WidgetProfileService(new InMemoryWidgetProfileRepository()).Snapshot().Profiles[0];
        var tree = new WidgetLayoutResolver().Resolve(profile, new WidgetProjection { Status = "error", Error = "missing location" }, new WidgetHostCapabilities());

        Assert.Equal("error", tree.Status);
        Assert.Equal("missing location", tree.Error);
        Assert.Empty(tree.Rows);
        Assert.Equal("missing location", tree.Texts.Single().Text);
        Assert.DoesNotContain(tree.Texts, item => item.Key == "lastUpdate");
    }

    [Theory]
    [InlineData("en", false)]
    [InlineData("ar", true)]
    public void EveryTemplateAndFamilyEitherFitsWithoutOverflowOrReturnsExplicitError(string language, bool isRtl) {
        var profiles = new WidgetProfileService(new InMemoryWidgetProfileRepository()).Snapshot().Profiles;
        var families = Enum.GetValues<WidgetFamily>();
        var resolver = new WidgetLayoutResolver();
        var projection = Projection() with { Language = language, IsRtl = isRtl };

        foreach (var profile in profiles) {
            foreach (var family in families) {
                var maximum = family switch {
                    WidgetFamily.Inline or WidgetFamily.Circular or WidgetFamily.Tiny => 2,
                    WidgetFamily.Compact or WidgetFamily.Small => 4,
                    WidgetFamily.Rectangular or WidgetFamily.Medium => 7,
                    _ => 12
                };
                var tree = resolver.Resolve(profile, projection, new WidgetHostCapabilities {
                    Family = family,
                    MaxTextItems = maximum,
                    MaxActions = 2
                });

                Assert.True(tree.Texts.Count + tree.Rows.Count <= maximum, $"{profile.Template}/{family} overflowed");
                Assert.Equal(isRtl, tree.IsRtl);
                if (tree.Status == "error") {
                    Assert.Equal("required-content-overflow", tree.Error);
                    Assert.NotEmpty(tree.OmittedProjection);
                } else {
                    var required = WidgetProfileService.Catalog.Single(item => item.Template == profile.Template).RequiredProjection;
                    Assert.DoesNotContain(required, item => tree.OmittedProjection.Contains(item));
                }
            }
        }
    }

    private static WidgetProjection Projection() => new() {
        Language = "en",
        LocationTitle = "Dubai, United Arab Emirates",
        LocationSource = "gps",
        HijriDate = "1 Ramadan 1448",
        GregorianDate = "Monday, 01 February 2027",
        NextPrayerName = "Fajr",
        NextPrayerTime = "05:30",
        NextPrayerAtUnixMilliseconds = 1_800_000_000_000,
        PrayerRows = Enumerable.Range(1, 7).Select(index => new WidgetPrayerRow {
            Id = $"p{index}", Name = $"Prayer {index}", Time = $"0{index}:00", IsNext = index == 1
        }).ToArray(),
        ImsakTime = "05:20",
        IftarTime = "18:10",
        TasbihCount = 10,
        TasbihTarget = 33,
        QiblaBearingDegrees = 258
    };
}
