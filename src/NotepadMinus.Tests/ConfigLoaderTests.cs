using System;
using System.IO;
using NotepadMinus.Config;

namespace NotepadMinus.Tests;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "NotepadMinus.Cfg." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void MissingFile_WritesAndReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "config.json");
        var cfg = ConfigLoader.Load(path);
        Assert.True(File.Exists(path));
        Assert.Equal(10, cfg.Autosave.IntervalSeconds);
        Assert.True(cfg.Startup.PromptOnLaunch);
        Assert.Equal("Consolas", cfg.Editor.FontFamily);
    }

    [Fact]
    public void PartialFile_MergesWithDefaults()
    {
        var path = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(path, "{ \"editor\": { \"fontSize\": 16 } }");
        var cfg = ConfigLoader.Load(path);
        Assert.Equal(16, cfg.Editor.FontSize);
        Assert.Equal("Consolas", cfg.Editor.FontFamily);
        Assert.Equal(10, cfg.Autosave.IntervalSeconds);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaults()
    {
        var path = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(path, "not json at all");
        var cfg = ConfigLoader.Load(path);
        Assert.Equal(10, cfg.Autosave.IntervalSeconds);
    }

    [Fact]
    public void ExpandPath_ResolvesUserProfile()
    {
        var expanded = ConfigLoader.ExpandPath("%USERPROFILE%\\Documents\\Notepad");
        Assert.DoesNotContain("%USERPROFILE%", expanded);
    }
}
