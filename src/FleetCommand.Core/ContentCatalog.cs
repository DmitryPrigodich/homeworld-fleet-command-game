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
    public Dictionary<string, LeaderDefinition> Leaders { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, CardDefinition> Cards { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, DeckDefinition> Decks { get; } = new(StringComparer.Ordinal);
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
        Copy(Leaders, copy.Leaders);
        Copy(Cards, copy.Cards); Copy(Decks, copy.Decks);
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
            Add(catalog.Leaders, data.Leaders, v => v.Id);
            Add(catalog.Cards, data.Cards, v => v.Id);
            Add(catalog.Decks, data.Decks, v => v.Id);
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
        CommandCards.ValidateDefinitions(this);
        foreach (var t in Themes.Values)
            foreach (var color in new[] { t.Background, t.Panel, t.Text, t.Muted, t.Accent, t.Highlight, t.Enemy, t.Grid, t.TabBlue, t.TabYellow, t.TabGrey })
                if (!System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$"))
                    throw new InvalidDataException($"Invalid color in {t.Id}: {color}");
        foreach (var a in Abilities.Values)
            if (a.Handler is not ("penetrating" or "rapid-fire" or "move-bonus") || a.Value is < 0 or > 20)
                throw new InvalidDataException($"Unknown ability handler or invalid value: {a.Id}");
        foreach (var s in Ships.Values)
        {
            if (s.Models < 1 || s.Models > 100 || s.Move < 0 || s.Move > 50 || s.DicePerModel < 0 || s.DicePerModel > 50
                || s.Footprint is < 1 or > 2 || s.Footprint == 2 && (s.Models != 1 || FleetRuleTools.Strike(s))
                || s.Success is < 2 or > 6 || s.Armor < 0 || s.Range < 0 || s.Bays is < 0 or > 100 || s.LaunchTubes is < 0 or > 100 || (s.Factory ?? []).Any(t => !FleetRuleTools.Classes.Contains(t)) || s.Harvest is < -1 or > 50 || s.Afterburners is < -1 or > 50 || s.Crew < 0 || s.Ram < 0 || s.Stars < 0 || s.Evasive < 0
                || !float.IsFinite(s.VisualSize) || s.VisualSize is <= 0 or > 2)
                throw new InvalidDataException($"Invalid ship statistics: {s.Id}");
            foreach (var a in s.Abilities ?? []) if (!Abilities.ContainsKey(a)) throw new InvalidDataException($"{s.Id}: missing ability {a}");
            if (!string.IsNullOrEmpty(s.TacticalIcon) && !FleetPresentation.TacticalIcons.Contains(s.TacticalIcon))
                throw new InvalidDataException($"Unknown tactical icon: {s.TacticalIcon}");
            if (s.CapSuccess != 0 && s.CapSuccess is < 2 or > 6) throw new InvalidDataException($"Invalid CAP success: {s.Id}");
            if (s.Class != "" && !FleetRuleTools.Classes.Contains(s.Class)) throw new InvalidDataException($"Unknown ship class: {s.Class}");
            if (s.Weapons != null && (s.Weapons.Select(w => w.Id).Distinct().Count() != s.Weapons.Length || s.Weapons.Any(w => string.IsNullOrWhiteSpace(w.Id)
                || w.DicePerModel < 0 || w.Range < 0 || w.Range == 0 && !w.DefensiveFire || w.Barrage != 0 && w.Barrage is < 2 or > 6 || w.Arc is not ("all" or "turret" or "spinal") || w.Kind is not ("gun" or "fighter" or "missile" or "torpedo" or "bomb" or "drone" or "close")
                || w.Penetrating < 0 || w.RapidFire < 0 || w.PowerDice < 0 || w.PowerPenetrating < 0 || (w.Targets ?? []).Any(t => !FleetRuleTools.Classes.Contains(t)) || (w.RapidTargets ?? []).Any(t => !FleetRuleTools.Classes.Contains(t)))))
                throw new InvalidDataException($"Invalid weapons: {s.Id}");
        }
        foreach (var leader in Leaders.Values)
            if (leader.Hand < 0 || leader.Play < 0 || leader.Stars < 0 || leader.CoolUnderPressure < 0 || leader.AttackDice < 0 || leader.CloseDice < 0 || leader.Classes == null
                || leader.Classes.Any(t => !FleetRuleTools.Classes.Contains(t))) throw new InvalidDataException($"Invalid leader: {leader.Id}");
        ForceBuilder.ValidateDefinitions(this);
        foreach (var f in Fleets.Values)
            foreach (var u in f.Units)
                if (u.Cargo is < 0 or > 1000000 || !Ships.ContainsKey(u.Ship) || u.Facing is < 0 or > 5 || !FleetRuleTools.ValidLeader(this, new("", u.Ship, 0, u.Q, u.R, u.Facing, 1, Leader: u.Leader, AdditionalLeaders: u.AdditionalLeaders))) throw new InvalidDataException($"Invalid fleet unit: {f.Id}");
        foreach (var o in Objects.Values)
            if (o.MoveCost < 1 || o.AttackPenalty < 0 || o.Hazard is { } hazard && (hazard.Dice is < 0 or > 100 || hazard.Success is < 2 or > 6 || hazard.Penetrating < 0)) throw new InvalidDataException($"Invalid object: {o.Id}");
        foreach (var rule in Rules.Values)
            if (rule.RoundLimit < 1 || rule.OrdersPerSide < 1) throw new InvalidDataException($"Invalid rules: {rule.Id}");
        foreach (var map in Maps.Values)
        {
            if (map.Width is < 2 or > 100 || map.Height is < 2 or > 100 || map.Fleets.Length != 2 || !Rules.ContainsKey(map.Rules))
                throw new InvalidDataException($"Invalid map configuration: {map.Id}");
            if (map.AsteroidStars != null && (!Rules[map.Rules].Tabletop || Rules[map.Rules].Handler != "cards-basic-v1" || map.AsteroidStars.Length != map.Objects.Count(o => Objects.TryGetValue(o.Definition, out var obj) && FleetTerrain.Kind(obj) == "asteroid")
                || map.AsteroidStars.Any(n => n is < 0 or > 1000))) throw new InvalidDataException($"Invalid asteroid star bag: {map.Id}");
            if (map.ReserveFromRound < 1 || map.ReserveStars != null && (map.ReserveStars.Length != 2 || map.ReserveStars.Any(n => n is < 0 or > 1000000))
                || map.SetupZones != null && (map.SetupZones.Length != 2 || map.SetupZones.Any(zone => zone == null || zone.Any(h => h.Q < 0 || h.R < 0 || h.Q >= map.Width || h.R >= map.Height)))) throw new InvalidDataException($"Invalid reinforcement setup: {map.Id}");
            ScenarioEvents.ValidateDefinitions(this, map);
            FleetObjectives.ValidateDefinitions(this, map);
            if (map.Cells != null && (map.Cells.Length == 0 || map.Cells.Distinct().Count() != map.Cells.Length || map.Cells.Any(h => h.Q < 0 || h.R < 0 || h.Q >= map.Width || h.R >= map.Height))) throw new InvalidDataException("Invalid playable map cells.");
            bool Inside(int q, int r) => FleetFootprint.Inside(map, new(q, r));
            var occupied = new HashSet<Hex>();
            foreach (var objectPlacement in map.Objects)
            {
                if (!Objects.ContainsKey(objectPlacement.Definition) || !Inside(objectPlacement.Q, objectPlacement.R) || objectPlacement.Owner is < -1 or > 1)
                    throw new InvalidDataException($"Invalid map object: {map.Id}");
                if (Objects[objectPlacement.Definition].BlocksMovement) occupied.Add(new(objectPlacement.Q, objectPlacement.R));
            }
            foreach (var f in map.Fleets)
            {
                if (!Fleets.TryGetValue(f, out var fleet)) throw new InvalidDataException($"Missing fleet: {f}");
                FleetLogistics.ValidatePlacements(this, fleet, Rules[map.Rules].Tabletop);
                foreach (var unit in fleet.Units.Where(u => !u.Reserve && u.EmbarkedOn < 0))
                    if (Ships[unit.Ship].Footprint == 2 && !Rules[map.Rules].Tabletop || FleetFootprint.Cells(Ships[unit.Ship], new(unit.Q, unit.R), unit.Facing).Any(h => !Inside(h.Q, h.R) || !occupied.Add(h))) throw new InvalidDataException($"Invalid/overlapping deployment: {map.Id}");
            }
        }
        foreach (var campaign in Campaigns.Values)
        {
            var ids = campaign.Missions.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            if (ids.Count != campaign.Missions.Length || !ids.Contains(campaign.Start)) throw new InvalidDataException($"Invalid campaign: {campaign.Id}");
            foreach (var mission in campaign.Missions)
                if (!Maps.ContainsKey(mission.Map) || mission.OnVictory != null && !ids.Contains(mission.OnVictory)
                    || mission.OnDefeat != null && !ids.Contains(mission.OnDefeat) || mission.OnDraw != null && !ids.Contains(mission.OnDraw)) throw new InvalidDataException($"Invalid mission: {mission.Id}");
        }
    }
}
