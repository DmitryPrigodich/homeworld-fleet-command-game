using System.Text.RegularExpressions;
namespace FleetCommand.Core;

// Cosmetic packs deliberately live outside ContentCatalog and the match fingerprint.
public sealed record CardStyleDefinition(string Id, string Name, string Background = "#102436", string Accent = "#e2b361", string Text = "#b4d9e7", string OrderFrame = "", string InterruptFrame = "");
public sealed record LoadedCardStyle(CardStyleDefinition Definition, string Directory)
{
    public string Frame(bool interrupt) => CardStyleCatalog.Asset(Directory, interrupt ? Definition.InterruptFrame : Definition.OrderFrame);
}
public static class CardStyleCatalog
{
    public static string Asset(string directory, string relative)
    {
        if (relative == "") return "";
        if (relative == null) throw new InvalidDataException("Missing card artwork path.");
        string root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        string file = Path.GetFullPath(Path.Combine(directory, relative));
        if (Path.IsPathRooted(relative) || !file.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || !new[] { ".png", ".webp", ".jpg", ".jpeg" }.Contains(Path.GetExtension(file).ToLowerInvariant())) throw new InvalidDataException("Card artwork must be an image inside its pack.");
        return file;
    }
    public static LoadedCardStyle Load(string directory)
    {
        var definition = JsonData.Read<CardStyleDefinition>(Path.Combine(directory, "style.json"));
        if (string.IsNullOrEmpty(definition.Id) || !Regex.IsMatch(definition.Id, "^[a-z0-9][a-z0-9._-]{0,63}$") || definition.Id == "text" || string.IsNullOrWhiteSpace(definition.Name) || definition.Name.Length > 80
            || new[] { definition.Background, definition.Accent, definition.Text }.Any(color => color == null || !Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$"))) throw new InvalidDataException("Invalid card style pack.");
        var pack = new LoadedCardStyle(definition, Path.GetFullPath(directory));
        foreach (string file in new[] { pack.Frame(false), pack.Frame(true) }) if (file != "" && !File.Exists(file)) throw new InvalidDataException("Card artwork is missing.");
        return pack;
    }
    public static LoadedCardStyle[] Discover(IEnumerable<string> roots)
    {
        var result = new Dictionary<string, LoadedCardStyle>();
        foreach (var root in roots.Where(System.IO.Directory.Exists))
            foreach (var directory in System.IO.Directory.GetDirectories(root))
                if (File.Exists(Path.Combine(directory, "style.json")))
                {
                    try { var pack = Load(directory); result[pack.Definition.Id] = pack; }
                    catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { /* An optional cosmetic pack cannot prevent game startup. */ }
                }
        return result.Values.OrderBy(p => p.Definition.Name, StringComparer.Ordinal).ToArray();
    }
}
