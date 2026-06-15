using System.IO;
using System.Linq;
using NotepadMinus.Services;
using Xunit;

namespace NotepadMinus.Tests;

public class WorkspaceStateStoreTests
{
    [Fact]
    public void SaveAndLoad_PerFolder_RoundTrips()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "nm-ws-" + Path.GetRandomFileName() + ".json");
        try
        {
            var store = new WorkspaceStateStore(tmp);
            store.Save(@"C:\Projects\A", new WorkspaceState
            {
                OpenFiles = new() { @"C:\Projects\A\one.txt", @"C:\Projects\A\two.md" },
                ActiveTabIndex = 1,
                SidebarWidth = 320,
                SidebarVisible = true,
            });
            store.Save(@"C:\Projects\B", new WorkspaceState
            {
                OpenFiles = new() { @"C:\Projects\B\notes.log" },
                SidebarVisible = false,
            });

            var a = store.Load(@"C:\projects\a"); // case-insensitive lookup
            Assert.NotNull(a);
            Assert.Equal(2, a!.OpenFiles.Count);
            Assert.Equal(1, a.ActiveTabIndex);
            Assert.Equal(320, a.SidebarWidth);
            Assert.True(a.SidebarVisible);

            var b = store.Load(@"C:\Projects\B");
            Assert.NotNull(b);
            Assert.Single(b!.OpenFiles);
            Assert.False(b.SidebarVisible);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void Load_Returns_Null_For_Unknown_Folder()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "nm-ws-" + Path.GetRandomFileName() + ".json");
        var store = new WorkspaceStateStore(tmp);
        Assert.Null(store.Load(@"C:\Nonexistent\Path"));
    }

    [Fact]
    public void FilterExisting_DropsMissingPaths()
    {
        var real = Path.GetTempFileName();
        try
        {
            var result = WorkspaceState.FilterExisting(new[] { real, @"C:\does\not\exist.txt", "" });
            Assert.Single(result);
            Assert.Equal(real, result[0]);
        }
        finally { File.Delete(real); }
    }
}
