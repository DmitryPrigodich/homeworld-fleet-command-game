using System.Text.Json;
using System.Text.Json.Serialization;

namespace FleetCommand.Core;

public static class JsonData
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };
    public static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException($"Empty JSON document: {path}");
}

public sealed record ModManifest(string Id, string Version, int SchemaVersion = 1,
    string[]? Requires = null, string[]? Overrides = null);
public sealed record ShipDefinition(string Id, string Name, string Faction, string Model,
    int Models, int Move, int DicePerModel, int Success, int Armor, int Range,
    string[]? Abilities = null, float VisualSize = 0.55f, string Color = "", string TacticalIcon = "", string Label = "");
public sealed record AbilityDefinition(string Id, string Handler, int Value = 1);
public sealed record FleetUnit(string Ship, int Q, int R, int Facing = 0);
public sealed record FleetDefinition(string Id, string Name, FleetUnit[] Units);
public sealed record ObjectDefinition(string Id, string Name, string Model = "", bool BlocksMovement = false,
    int AttackPenalty = 0, int MoveCost = 1, string Color = "#8792a2", string Label = "");
public sealed record MapObject(string Definition, int Q, int R);
public sealed record MapDefinition(string Id, string Name, int Width, int Height,
    string Background, MapObject[] Objects, string[] Fleets, string Rules);
public sealed record RulesDefinition(string Id, string Name, string Handler = "skirmish-v1",
    int RoundLimit = 12, int OrdersPerSide = 3, bool AlternatingOrders = true,
    bool FriendlyPassThrough = true, bool RearShots = true, bool FocusFire = true);
public sealed record ContentPack(ShipDefinition[]? Ships = null, AbilityDefinition[]? Abilities = null,
    FleetDefinition[]? Fleets = null, ObjectDefinition[]? Objects = null,
    MapDefinition[]? Maps = null, RulesDefinition[]? Rules = null, ThemeDefinition[]? Themes = null,
    CampaignDefinition[]? Campaigns = null);
public sealed record ThemeDefinition(string Id, string Name, string Background, string Panel,
    string Text, string Muted, string Accent, string Highlight, string Enemy, string Grid);
public sealed record CampaignMission(string Id, string Name, string Map, string? OnVictory = null, string? OnDefeat = null);
public sealed record CampaignDefinition(string Id, string Name, string Start, CampaignMission[] Missions);

public readonly record struct Hex(int Q, int R)
{
    public static readonly Hex[] Directions = [new(1, 0), new(0, 1), new(-1, 1), new(-1, 0), new(0, -1), new(1, -1)];
    public int Distance(Hex b) => (Math.Abs(Q - b.Q) + Math.Abs(R - b.R) + Math.Abs(Q + R - b.Q - b.R)) / 2;
    public Hex Neighbor(int direction) => new(Q + Directions[(direction % 6 + 6) % 6].Q, R + Directions[(direction % 6 + 6) % 6].R);
}
