using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FleetCommand.Import;

public sealed record ShipSource(string Id, string Archive, BigEntry Entry);
public sealed record ImportReceipt(int SchemaVersion, string AssetId, string Archive, string ArchiveEntry,
    string SourceSha256, string OutputSha256, DateTimeOffset ImportedAt);

public static partial class HwrmImporter
{
    [GeneratedRegex("^[a-z0-9_]+$")] private static partial Regex AssetId();
    public static IReadOnlyList<ShipSource> Scan(string installation)
    {
        string data = Path.Combine(Path.GetFullPath(installation), "HomeworldRM", "Data");
        if (!Directory.Exists(data)) throw new DirectoryNotFoundException("Select the Homeworld installation root containing HomeworldRM/Data.");
        var list = new List<ShipSource>();
        foreach (var filename in new[] { "HW1Ships.big", "HW2Ships.big" })
        {
            string path = Path.Combine(data, filename);
            if (!File.Exists(path)) continue;
            using var archive = new BigArchive(path);
            foreach (var entry in archive.Entries.Where(e => e.Path.StartsWith("ship/", StringComparison.OrdinalIgnoreCase) && e.Path.EndsWith(".hod", StringComparison.OrdinalIgnoreCase)))
            {
                string id = Path.GetFileNameWithoutExtension(entry.Path).ToLowerInvariant();
                if (AssetId().IsMatch(id)) list.Add(new(id, path, entry));
            }
        }
        return list.OrderBy(s => s.Id, StringComparer.Ordinal).ToArray();
    }
    public static ImportReceipt Import(ShipSource source, string cacheRoot)
    {
        if (!AssetId().IsMatch(source.Id)) throw new InvalidDataException("Invalid asset ID.");
        string root = Path.GetFullPath(cacheRoot), output = Path.Combine(root, source.Id);
        Directory.CreateDirectory(root);
        using var archive = new BigArchive(source.Archive);
        var entry = archive.Entries.Single(e => e.Path == source.Entry.Path);
        byte[] bytes = archive.Read(entry);
        string staging = Path.Combine(root, ".pending-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var model = HodModel.Convert(bytes, staging);
            var json = JsonSerializer.SerializeToUtf8Bytes(model, options);
            File.WriteAllBytes(Path.Combine(staging, "model.json"), json);
            var receipt = new ImportReceipt(1, "hwrm:" + source.Id, Path.GetFileName(source.Archive), source.Entry.Path,
                model.SourceSha256, Convert.ToHexString(SHA256.HashData(json)), DateTimeOffset.UtcNow);
            File.WriteAllText(Path.Combine(staging, "receipt.json"), JsonSerializer.Serialize(receipt, options));
            // Existing imports remain intact. A separate cache-clearing action can be added later.
            if (Directory.Exists(output)) throw new IOException($"Already imported: {source.Id}");
            Directory.Move(staging, output);
            return receipt;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
}
