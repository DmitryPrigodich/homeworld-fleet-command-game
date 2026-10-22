using System.Security.Cryptography;
using System.Text.Json;

namespace FleetCommand.Import;

public sealed record BadgeReceipt(string Id, string File, string SourceEntry, string Sha256);

public static class BadgeImporter
{
    private static readonly (string Id, string Entry)[] Badges =
    [
        ("kushan", "badges/kushan.tga"), ("taiidan", "badges/taiidan_white.tga"),
        ("kiith-sjet", "badges/kiith sjet.tga"), ("kiith-naabal", "badges/kiith naabal.tga"),
        ("kiith-manaan", "badges/kiith manaan.tga"), ("kiith-paktu", "badges/kiith paktu.tga"), ("kiith-soban", "badges/kiith soban.tga")
    ];

    public static IReadOnlyList<BadgeReceipt> Import(string installation, string cache)
    {
        string archivePath = Path.Combine(installation, "HomeworldRM", "Data", "Homeworld2.big");
        using var archive = new BigArchive(archivePath);
        Directory.CreateDirectory(cache);
        var receipts = new List<BadgeReceipt>();
        foreach (var (id, entryPath) in Badges)
        {
            var entry = archive.Entries.SingleOrDefault(e => e.Path.Equals(entryPath, StringComparison.OrdinalIgnoreCase));
            if (entry == null) continue;
            if (entry.Size > 4_000_000) throw new InvalidDataException("Badge image exceeds supported size.");
            byte[] bytes = archive.Read(entry);
            string file = id + ".tga";
            File.WriteAllBytes(Path.Combine(cache, file), bytes);
            receipts.Add(new(id, file, entryPath, Convert.ToHexString(SHA256.HashData(bytes))));
        }
        File.WriteAllText(Path.Combine(cache, "provenance.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
        return receipts;
    }
}
