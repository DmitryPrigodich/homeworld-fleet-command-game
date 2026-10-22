using System.Security.Cryptography;
using System.Text;

namespace FleetCommand.Core;

public sealed class ContentCatalog
{
    public Dictionary<string, ShipDefinition> Ships { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, AbilityDefinition> Abilities { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, FleetDefinition> Fleets { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, ObjectDefinition> Objects { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, MapDefinition> Maps { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, RulesDefinition> Rules { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, ThemeDefinition> Themes { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, CampaignDefinition> Campaigns { get; } = new(StringComparer.Ordinal);
    public List<ModManifest> Mods { get; } = [];
    public string Fingerprint { get; private set; } = "";

    // Definitions are records; a separate set of dictionaries isolates temporary scenarios.
    public ContentCatalog CopyForRuntime()
    {
        var copy = new ContentCatalog { Fingerprint = Fingerprint };
        void Copy<T>(Dictionary<string, T> source, Dictionary<string, T> target)
        {
            foreach (var pair in source) target.Add(pair.Key, pair.Value);
        }
        Copy(Ships, copy.Ships); Copy(Abilities, copy.Abilities); Copy(Fleets, copy.Fleets);
        Copy(Objects, copy.Objects); Copy(Maps, copy.Maps); Copy(Rules, copy.Rules);
        Copy(Themes, copy.Themes); Copy(Campaigns, copy.Campaigns);
        copy.Mods.AddRange(Mods);
        return copy;
    }

    public static ContentCatalog Load(IEnumerable<string> directories)
    {
        var catalog = new ContentCatalog();
        var pending = directories.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => (Path: p, Manifest: JsonData.Read<ModManifest>(Path.Combine(p, "mod.json"))))
            .OrderBy(p => p.Manifest.Id, StringComparer.Ordinal).ToList();
        if (pending.Select(p => p.Manifest.Id).Distinct().Count() != pending.Count)
            throw new InvalidDataException("Duplicate mod IDs.");
        var digest = new StringBuilder();
        while (pending.Count > 0)
        {
            var index = pending.FindIndex(p => (p.Manifest.Requires ?? []).All(id => catalog.Mods.Any(m => m.Id == id)));
            if (index < 0) throw new InvalidDataException("Missing mod dependency or dependency cycle.");
            var (path, mod) = pending[index];
            pending.RemoveAt(index);
            if (mod.SchemaVersion != 1 || string.IsNullOrWhiteSpace(mod.Id) || !Version.TryParse(mod.Version, out _))
                throw new InvalidDataException($"Unsupported or invalid mod manifest: {path}");
            var contentPath = Path.Combine(path, "content.json");
            var data = JsonData.Read<ContentPack>(contentPath);
            void Add<T>(Dictionary<string, T> target, IEnumerable<T>? values, Func<T, string> getId)
            {
                foreach (var value in values ?? [])
                {
                    var id = getId(value);
                    if (string.IsNullOrWhiteSpace(id) || !id.Contains(':')) throw new InvalidDataException($"ID must be namespaced: {id}");
                    if (target.ContainsKey(id) && !(mod.Overrides ?? []).Contains(id, StringComparer.Ordinal))
                        throw new InvalidDataException($"{mod.Id}: duplicate {id}; declare an explicit override.");
                    if (!target.ContainsKey(id) && !id.StartsWith(mod.Id + ":", StringComparison.Ordinal))
                        throw new InvalidDataException($"{mod.Id}: new ID belongs to another mod: {id}");
                    target[id] = value;
                }
            }
            Add(catalog.Ships, data.Ships, v => v.Id);
            Add(catalog.Abilities, data.Abilities, v => v.Id);
            Add(catalog.Fleets, data.Fleets, v => v.Id);
            Add(catalog.Objects, data.Objects, v => v.Id);
            Add(catalog.Maps, data.Maps, v => v.Id);
            Add(catalog.Rules, data.Rules, v => v.Id);
            Add(catalog.Themes, data.Themes, v => v.Id);
            Add(catalog.Campaigns, data.Campaigns, v => v.Id);
            catalog.Mods.Add(mod);
            digest.Append(mod.Id).Append('\n').Append(File.ReadAllText(Path.Combine(path, "mod.json")))
                .Append('\n').Append(File.ReadAllText(contentPath)).Append('\n');
        }
        catalog.Validate();
        catalog.Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digest.ToString())));
        return catalog;
    }

    public void Validate()
    {
        foreach (var t in Themes.Values)
            foreach (var color in new[] { t.Background, t.Panel, t.Text, t.Muted, t.Accent, t.Highlight, t.Enemy, t.Grid })
                if (!System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$"))
                    throw new InvalidDataException($"Invalid color in {t.Id}: {color}");
        foreach (var a in Abilities.Values)
            if (a.Handler is not ("penetrating" or "rapid-fire" or "move-bonus") || a.Value is < 0 or > 20)
                throw new InvalidDataException($"Unknown ability handler or invalid value: {a.Id}");
        foreach (var s in Ships.Values)
        {
            if (s.Models < 1 || s.Models > 100 || s.Move < 0 || s.Move > 50 || s.DicePerModel < 0 || s.DicePerModel > 50
                || s.Success is < 2 or > 6 || s.Armor < 0 || s.Range < 0 || !float.IsFinite(s.VisualSize) || s.VisualSize is <= 0 or > 2)
                throw new InvalidDataException($"Invalid ship statistics: {s.Id}");
            foreach (var a in s.Abilities ?? []) if (!Abilities.ContainsKey(a)) throw new InvalidDataException($"{s.Id}: missing ability {a}");
        }
        foreach (var f in Fleets.Values)
            foreach (var u in f.Units)
                if (!Ships.ContainsKey(u.Ship) || u.Facing is < 0 or > 5) throw new InvalidDataException($"Invalid fleet unit: {f.Id}");
        foreach (var o in Objects.Values)
            if (o.MoveCost < 1 || o.AttackPenalty < 0) throw new InvalidDataException($"Invalid object: {o.Id}");
        foreach (var rule in Rules.Values)
            if (rule.RoundLimit < 1 || rule.OrdersPerSide < 1) throw new InvalidDataException($"Invalid rules: {rule.Id}");
        foreach (var map in Maps.Values)
        {
            if (map.Width is < 2 or > 100 || map.Height is < 2 or > 100 || map.Fleets.Length != 2 || !Rules.ContainsKey(map.Rules))
                throw new InvalidDataException($"Invalid map configuration: {map.Id}");
            bool Inside(int q, int r) => q >= 0 && q < map.Width && r >= 0 && r < map.Height;
            var occupied = new HashSet<Hex>();
            foreach (var objectPlacement in map.Objects)
            {
                if (!Objects.ContainsKey(objectPlacement.Definition) || !Inside(objectPlacement.Q, objectPlacement.R))
                    throw new InvalidDataException($"Invalid map object: {map.Id}");
                if (Objects[objectPlacement.Definition].BlocksMovement) occupied.Add(new(objectPlacement.Q, objectPlacement.R));
            }
            foreach (var f in map.Fleets)
            {
                if (!Fleets.TryGetValue(f, out var fleet)) throw new InvalidDataException($"Missing fleet: {f}");
                foreach (var unit in fleet.Units)
                    if (!Inside(unit.Q, unit.R) || !occupied.Add(new(unit.Q, unit.R))) throw new InvalidDataException($"Invalid/overlapping deployment: {map.Id}");
            }
        }
        foreach (var campaign in Campaigns.Values)
        {
            var ids = campaign.Missions.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            if (ids.Count != campaign.Missions.Length || !ids.Contains(campaign.Start)) throw new InvalidDataException($"Invalid campaign: {campaign.Id}");
            foreach (var mission in campaign.Missions)
                if (!Maps.ContainsKey(mission.Map) || mission.OnVictory != null && !ids.Contains(mission.OnVictory)
                    || mission.OnDefeat != null && !ids.Contains(mission.OnDefeat)) throw new InvalidDataException($"Invalid mission: {mission.Id}");
        }
    }
}
