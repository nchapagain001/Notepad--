using System;
using System.IO;
using System.Linq;
using NotepadMinus.Services;

namespace NotepadMinus.Tests;

public class SessionTrackerTests : IDisposable
{
    private readonly string _tempDir;

    public SessionTrackerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "NotepadMinus.Tests." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void LoadLastSession_NoFile_ReturnsEmpty()
    {
        var tracker = new SessionTracker(Path.Combine(_tempDir, "session.json"));
        Assert.Empty(tracker.LoadLastSession());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsPaths()
    {
        var tracker = new SessionTracker(Path.Combine(_tempDir, "session.json"));
        var paths = new[]
        {
            Path.Combine(_tempDir, "a.txt"),
            Path.Combine(_tempDir, "b.txt"),
        };
        tracker.SaveSession(paths);

        var loaded = tracker.LoadLastSession();
        Assert.Equal(paths.OrderBy(p => p), loaded.OrderBy(p => p));
    }

    [Fact]
    public void SaveSession_DropsBlanksAndDeduplicates()
    {
        var tracker = new SessionTracker(Path.Combine(_tempDir, "session.json"));
        tracker.SaveSession(new[]
        {
            Path.Combine(_tempDir, "a.txt"),
            "",
            null!,
            Path.Combine(_tempDir, "A.txt"), // dup, case-insensitive
        });

        var loaded = tracker.LoadLastSession();
        Assert.Single(loaded);
    }

    [Fact]
    public void FilterExisting_OmitsMissingFiles()
    {
        var present = Path.Combine(_tempDir, "present.txt");
        File.WriteAllText(present, "x");
        var missing = Path.Combine(_tempDir, "missing.txt");

        var filtered = SessionTracker.FilterExisting(new[] { present, missing });
        Assert.Single(filtered);
        Assert.Equal(present, filtered[0]);
    }

    [Fact]
    public void LoadLastSession_CorruptFile_ReturnsEmpty()
    {
        var path = Path.Combine(_tempDir, "session.json");
        File.WriteAllText(path, "{not valid json");
        var tracker = new SessionTracker(path);
        Assert.Empty(tracker.LoadLastSession());
    }

    [Fact]
    public void IsFirstLaunch_TrueWhenNoFile()
    {
        var tracker = new SessionTracker(Path.Combine(_tempDir, "session.json"));
        Assert.True(tracker.IsFirstLaunch());
        tracker.SaveSession(Array.Empty<string>());
        Assert.False(tracker.IsFirstLaunch());
    }

    [Fact]
    public void SidebarVisible_RoundTrips()
    {
        var tracker = new SessionTracker(Path.Combine(_tempDir, "session.json"));
        Assert.Null(tracker.LoadSidebarVisible());

        tracker.SaveSession(Array.Empty<string>(), sidebarVisible: true);
        Assert.True(tracker.LoadSidebarVisible());

        tracker.SaveSession(Array.Empty<string>(), sidebarVisible: false);
        Assert.False(tracker.LoadSidebarVisible());
    }

    [Fact]
    public void SaveSession_PreservesPreviousSidebarPrefWhenNoneProvided()
    {
        var tracker = new SessionTracker(Path.Combine(_tempDir, "session.json"));
        tracker.SaveSession(Array.Empty<string>(), sidebarVisible: true);

        // Subsequent save without an explicit sidebar pref should keep the prior value.
        tracker.SaveSession(new[] { Path.Combine(_tempDir, "a.txt") });
        Assert.True(tracker.LoadSidebarVisible());
    }
}
