using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NotepadMinus.Services;
using Xunit;

namespace NotepadMinus.Tests;

public class BackupServiceTests
{
    private static string MakeTempFolder()
    {
        var p = Path.Combine(Path.GetTempPath(), "nm-bk-" + Path.GetRandomFileName());
        Directory.CreateDirectory(p);
        return p;
    }

    [Fact]
    public async Task ExportThenImport_RoundTrips_AllNotes_Without_Overwriting()
    {
        var src = MakeTempFolder();
        var dst = MakeTempFolder();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(src, "alpha 2026-01-01.txt"), "alpha body");
            await File.WriteAllTextAsync(Path.Combine(src, "config 2026-01-02.json"), "{\"k\":1}");
            await File.WriteAllTextAsync(Path.Combine(src, "notes 2026-01-03.md"), "# note");

            var backup = Path.Combine(src, "export.json");
            var svc = new BackupService();
            var exported = await svc.ExportAsync(src, backup);
            Assert.Equal(3, exported);

            // Pre-seed a collision in the destination.
            await File.WriteAllTextAsync(Path.Combine(dst, "alpha 2026-01-01.txt"), "EXISTING");

            var imported = await svc.ImportAsync(backup, dst);
            Assert.Equal(3, imported);

            // Original was not overwritten.
            Assert.Equal("EXISTING", await File.ReadAllTextAsync(Path.Combine(dst, "alpha 2026-01-01.txt")));
            // Disambiguated copy exists.
            Assert.True(File.Exists(Path.Combine(dst, "alpha 2026-01-01 (imported).txt")));
            // Other notes round-tripped.
            Assert.Equal("{\"k\":1}", await File.ReadAllTextAsync(Path.Combine(dst, "config 2026-01-02.json")));
            Assert.Equal("# note", await File.ReadAllTextAsync(Path.Combine(dst, "notes 2026-01-03.md")));
        }
        finally
        {
            Directory.Delete(src, true);
            Directory.Delete(dst, true);
        }
    }

    [Fact]
    public void ResolveCollision_AppendsImportedThenNumericSuffix()
    {
        var existing = new System.Collections.Generic.HashSet<string>(
            new[] { "a 2026-01-01.txt", "a 2026-01-01 (imported).txt" },
            System.StringComparer.OrdinalIgnoreCase);

        Assert.Equal("a 2026-01-01 (imported) (2).txt",
            BackupService.ResolveCollision("a 2026-01-01.txt", existing));
    }
}
