using System.Text.RegularExpressions;

namespace FleetCommand.Core;

public sealed record PlayerProfile(string Name = "Командир", string Color = "#39a4c5", string Emblem = "auto");
public sealed record CommanderAppearance(int Side, string Name, string Color, string Emblem, string Faction, bool IsNpc = false);

public static class FleetPresentation
{
    public const string NeutralColor = "#87929c";
    public static readonly string[] PlayerColors = ["#39a4c5", "#ee873c", "#68ca77", "#b787e6", "#e1c44a", "#ed647f", "#527fe3", "#36cab7"];
    public static readonly string[] Emblems = ["auto", "kushan", "taiidan", "kiith-sjet", "kiith-naabal", "kiith-manaan", "kiith-paktu", "kiith-soban"];
    public static readonly string[] TacticalIcons = ["triangle", "square", "diamond", "parallelogram", "hexagon", "circle", "unknown"];

    private static (int R, int G, int B) Rgb(string color) =>
        (Convert.ToInt32(color.Substring(1, 2), 16), Convert.ToInt32(color.Substring(3, 2), 16), Convert.ToInt32(color.Substring(5, 2), 16));

    public static bool IsPlayerColor(string? color)
    {
        if (color == null || !Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) return false;
        var (r, g, b) = Rgb(color);
        int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        return max >= 100 && max - min >= Math.Max(55, max * 0.35);
    }

    public static PlayerProfile Normalize(PlayerProfile? profile)
    {
        profile ??= new();
        string name = new((profile.Name ?? "").Where(c => !char.IsControl(c)).Take(24).ToArray());
        return new(string.IsNullOrWhiteSpace(name) ? "Командир" : name.Trim(),
            IsPlayerColor(profile.Color) ? profile.Color.ToLowerInvariant() : PlayerColors[0],
            Emblems.Contains(profile.Emblem) ? profile.Emblem : "auto");
    }

    public static string DistinctColor(string requested, IEnumerable<string> used)
    {
        var existing = used.ToArray();
        bool Distinct(string candidate) => existing.All(other =>
        {
            var a = Rgb(candidate); var b = Rgb(other);
            return Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2) >= 10000;
        });
        if (IsPlayerColor(requested) && Distinct(requested)) return requested;
        return PlayerColors.First(c => Distinct(c));
    }

    public static string DefaultEmblem(string faction) => faction.Equals("taiidan", StringComparison.OrdinalIgnoreCase) ? "taiidan" : "kushan";
    public static string EmblemName(string id) => id switch
    {
        "auto" => "По фракции", "kushan" => "Кушане", "taiidan" => "Таиидане", "kiith-sjet" => "Киит С'джет",
        "kiith-naabal" => "Киит Наабал", "kiith-manaan" => "Киит Манаан", "kiith-paktu" => "Киит Пакту", "kiith-soban" => "Киит Собан", _ => id
    };

    public static string ShipIcon(ShipDefinition ship)
    {
        if (TacticalIcons.Contains(ship.TacticalIcon)) return ship.TacticalIcon;
        // Legacy definitions predate tacticalIcon. Mods can supply an explicit shape.
        if (ship.Id.Contains("interceptor") || ship.Id == "tutorial:scouts") return "triangle";
        if (ship.Id.Contains("frigate")) return "diamond";
        return "unknown";
    }
    public static string ShipName(ShipDefinition ship) => !string.IsNullOrWhiteSpace(ship.Label) ? ship.Label : ship.Id switch
    {
        "core:kushan-interceptor" or "core:taiidan-interceptor" => "Перехватчики",
        "core:kushan-frigate" or "core:taiidan-frigate" => "Штурмовые фрегаты",
        "tutorial:scouts" => "Учебные перехватчики", "tutorial:frigate" => "Учебный фрегат", _ => ship.Name
    };
    public static int UnitNumber(ContentCatalog content, BattleState state, BattleUnit unit)
    {
        int offset = content.Maps[state.Map].Fleets.Take(unit.Side).Sum(id => content.Fleets[id].Units.Length);
        return int.TryParse(unit.Id.AsSpan(unit.Id.LastIndexOf('-') + 1), out int index) ? Math.Max(1, index - offset + 1) : 1;
    }
    public static string UnitName(ContentCatalog content, BattleState state, BattleUnit unit) =>
        $"{ShipName(content.Ships[unit.Definition])} №{UnitNumber(content, state, unit)}";
    public static string ObjectName(ObjectDefinition obj) => !string.IsNullOrWhiteSpace(obj.Label) ? obj.Label : obj.Id switch
    {
        "core:asteroids" => "Астероиды", "core:planet" => "Планета", _ => obj.Name
    };
    public static string ObjectEffects(ObjectDefinition obj) =>
        (obj.BlocksMovement ? "Нельзя пройти; перекрывает огонь." : $"Стоимость движения: {obj.MoveCost}.") +
        (obj.AttackPenalty > 0 ? $" Штраф к кубикам атаки: {obj.AttackPenalty}." : "");
}
