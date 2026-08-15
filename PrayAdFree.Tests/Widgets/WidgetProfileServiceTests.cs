using System.Diagnostics;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Widgets;

public sealed class WidgetProfileServiceTests {
    [Fact]
    public void Assigning_identical_instance_twice_does_not_rewrite_or_increment_revision() {
        var repository = new CountingWidgetRepository();
        var service = new WidgetProfileService(repository);
        var assignment = new WidgetInstanceAssignment {
            InstanceId = "windows-1",
            ProfileId = "default-next-prayer",
            Platform = WidgetPlatform.WindowsSystem,
            Surface = WidgetSurface.Board,
            Family = WidgetFamily.Medium
        };

        service.Assign(assignment);
        var revision = service.Snapshot().Revision;
        var saves = repository.SaveCount;
        service.Assign(assignment);

        Assert.Equal(revision, service.Snapshot().Revision);
        Assert.Equal(saves, repository.SaveCount);
    }

    [Fact]
    public void ExistingAndroidTasbihStateSurvivesDefaultProfileAssignmentMigration() {
        var profiles = new WidgetProfileService(new InMemoryWidgetProfileRepository());
        var stateStore = new TasbihWidgetStateStore(Path.Combine(Path.GetTempPath(), $"tasbih-widget-{Guid.NewGuid():N}.json"));
        var before = new TasbihWidgetState { AppWidgetId = 44, PresetIndex = 2, Count = 87, LastUpdatedUtc = DateTime.UtcNow };
        stateStore.Save(before);
        profiles.Assign(new WidgetInstanceAssignment {
            InstanceId = "android:tasbih:44",
            ProfileId = "default-tasbih",
            Platform = WidgetPlatform.Android,
            Surface = WidgetSurface.Home,
            Family = WidgetFamily.Medium
        });
        var after = stateStore.GetOrCreate(44, () => throw new InvalidOperationException("Existing state was lost."));
        Assert.Equal(87, after.Count);
        Assert.Equal(2, after.PresetIndex);
        Assert.Contains(profiles.Snapshot().Assignments, item => item.InstanceId == "android:tasbih:44");
    }

    [Fact]
    public void RemovingAndroidInstanceAlsoRemovesItsAssignment() {
        var profiles = new WidgetProfileService(new InMemoryWidgetProfileRepository());
        profiles.Assign(new WidgetInstanceAssignment { InstanceId = "android:prayer:7", ProfileId = "default-daily-prayer" });
        var result = profiles.Unassign("android:prayer:7");
        Assert.DoesNotContain(result.Assignments, item => item.InstanceId == "android:prayer:7");
    }

    [Fact]
    public void CreatesSixStrictBuiltInProfiles() {
        var service = Create();

        Assert.Equal(6, WidgetProfileService.Catalog.Count);
        Assert.Equal(6, service.Snapshot().Profiles.Count);
        Assert.All(service.Snapshot().Profiles, profile => Assert.True(profile.IsBuiltIn));
        Assert.Equal(6, service.Snapshot().Profiles.Select(item => item.Template).Distinct().Count());
    }

    [Fact]
    public void MutationReturnsCompleteProfileAndAdvancesRevision() {
        var service = Create();
        var created = service.Create(WidgetTemplateKind.NextPrayer, "Morning");
        var updated = service.Update(created.Id, new WidgetProfilePatch {
            ExpectedRevision = created.Revision,
            Name = "Morning prayer",
            Density = WidgetDensity.Detailed
        });

        Assert.Equal(created.Revision + 1, updated.Revision);
        Assert.Equal("Morning prayer", updated.Name);
        Assert.Equal(WidgetDensity.Detailed, updated.Density);
        Assert.NotEmpty(updated.Projection);
        Assert.NotNull(updated.Style);
        Assert.NotNull(updated.Privacy);
        Assert.Throws<InvalidOperationException>(() => service.Update(created.Id, new WidgetProfilePatch {
            ExpectedRevision = created.Revision,
            Name = "stale write"
        }));
    }

    [Fact]
    public void AssignedProfileCannotBeDeleted() {
        var service = Create();
        var profile = service.Create(WidgetTemplateKind.Tasbih, "Counter");
        service.Assign(new WidgetInstanceAssignment {
            InstanceId = "android-42",
            ProfileId = profile.Id,
            Platform = WidgetPlatform.Android,
            Surface = WidgetSurface.Home,
            Family = WidgetFamily.Medium
        });

        Assert.Throws<InvalidOperationException>(() => service.Delete(profile.Id));
    }

    [Fact]
    public void RejectsOverflowingProjectionAndUnreadableContrast() {
        var service = Create();
        var profile = service.Create(WidgetTemplateKind.NextPrayer);

        Assert.Throws<ArgumentException>(() => service.Update(profile.Id, new WidgetProfilePatch {
            Projection = ["location"]
        }));
        Assert.Throws<ArgumentException>(() => service.Update(profile.Id, new WidgetProfilePatch {
            Style = profile.Style with { PrimaryTextColor = "#FF777777", BackgroundColor = "#FF777777" }
        }));
    }

    [Fact]
    public void LocalMutationsStayBelowThreeHundredMilliseconds() {
        var service = Create();
        var watch = Stopwatch.StartNew();
        var profile = service.Create(WidgetTemplateKind.DateAndPrayer, "Fast");
        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds < 300, $"create took {watch.ElapsedMilliseconds} ms");

        watch.Restart();
        _ = service.Update(profile.Id, new WidgetProfilePatch { ExpectedRevision = profile.Revision, Name = "Faster" });
        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds < 300, $"update took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void FileRepositoryDoesNotReplaceCorruptStateWithDefaults() {
        var directory = Path.Combine(Path.GetTempPath(), "prayadfree-widget-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "profiles.json");
        File.WriteAllText(path, "{broken");
        try {
            var repository = new JsonFileWidgetProfileRepository(path);
            Assert.Throws<InvalidDataException>(() => new WidgetProfileService(repository));
            Assert.Equal("{broken", File.ReadAllText(path));
        } finally {
            Directory.Delete(directory, true);
        }
    }

    private static WidgetProfileService Create() => new(new InMemoryWidgetProfileRepository());
}

internal sealed class CountingWidgetRepository : IWidgetProfileRepository {
    private WidgetProfileDocument? _document;
    public int SaveCount { get; private set; }
    public WidgetProfileDocument? Load() => _document;
    public void Save(WidgetProfileDocument document) {
        SaveCount++;
        _document = document;
    }
}
