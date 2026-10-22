using Godot;
using FleetCommand.Core;
using FleetCommand.Import;

namespace FleetCommand.Game;

public static class TacticalTextures
{
    private static readonly Dictionary<string, Texture2D> Cache = [];
    public static Texture2D Icon(string shape, Color color)
    {
        string key = shape + color.ToHtml();
        if (Cache.TryGetValue(key, out var cached)) return cached;
        string geometry = shape switch
        {
            "triangle" => "<path d='M32 9 L56 53 L8 53 Z'/>",
            "square" => "<path d='M12 12 H52 V52 H12 Z'/>",
            "diamond" => "<path d='M32 6 L56 32 L32 58 L8 32 Z'/>",
            "parallelogram" => "<path d='M23 10 H51 L41 54 H13 Z'/>",
            "hexagon" => "<path d='M20 9 H44 L58 32 L44 55 H20 L6 32 Z'/>",
            "circle" => "<circle cx='32' cy='32' r='22'/>",
            _ => "<path d='M12 12 L52 52 M52 12 L12 52'/><circle cx='32' cy='32' r='25'/>"
        };
        return Cache[key] = Svg($"<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64'><g fill='none' stroke='#{color.ToHtml(false)}' stroke-width='5' stroke-linejoin='round'>{geometry}</g></svg>");
    }

    public static Texture2D FallbackEmblem(string id)
    {
        string key = "badge:" + id;
        if (Cache.TryGetValue(key, out var texture)) return texture;
        string shape = id == "taiidan" ? "<path d='M32 9 L53 23 L44 49 L20 49 L11 23 Z M11 23 H53 M20 49 L32 9 L44 49'/>" :
            "<path d='M32 10 V53 M9 20 L32 33 L55 20 M12 32 L32 43 L52 32 M25 53 H39'/>";
        return Cache[key] = Svg($"<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64'><g stroke='#dcebf0' fill='none' stroke-width='4'>{shape}</g></svg>");
    }
    private static Texture2D Svg(string source)
    {
        var image = new Image();
        if (image.LoadSvgFromString(source) != Error.Ok) throw new InvalidDataException("Cannot draw tactical icon.");
        return ImageTexture.CreateFromImage(image);
    }
}

public partial class Main
{
    private readonly Dictionary<string, Texture2D> _emblems = [];
    private CommanderAppearance[] _commanders = [];
    private string BadgeCache => System.IO.Path.Combine(_data, "imported", "badges");

    private void LoadEmblems(bool import = false)
    {
        if ((import || !System.IO.File.Exists(System.IO.Path.Combine(BadgeCache, "kushan.tga"))) &&
            System.IO.File.Exists(System.IO.Path.Combine(_settings.HwrmInstallation, "HomeworldRM", "Data", "Homeworld2.big")))
            BadgeImporter.Import(_settings.HwrmInstallation, BadgeCache);
        _emblems.Clear();
        foreach (string id in FleetPresentation.Emblems.Where(id => id != "auto"))
        {
            string file = System.IO.Path.Combine(BadgeCache, id + ".tga");
            if (!System.IO.File.Exists(file)) continue;
            var image = Image.LoadFromFile(file);
            if (image != null && !image.IsEmpty()) _emblems[id] = ImageTexture.CreateFromImage(image);
        }
    }
    private Texture2D EmblemTexture(string id) => _emblems.GetValueOrDefault(id) ?? TacticalTextures.FallbackEmblem(id);

    private void InitializeCommanders()
    {
        var player = FleetPresentation.Normalize(_settings.Player);
        _commanders = Enumerable.Range(0, 2).Select(side =>
        {
            string faction = _state?.Units.Where(u => u.Side == side).Select(u => _content.Ships[u.Definition].Faction).FirstOrDefault() ?? "";
            if (faction is not ("kushan" or "taiidan")) faction = side == 0 ? "kushan" : "taiidan";
            bool npc = side == 1 && (_bot || _tutorial != null);
            string emblem = side == 0 && player.Emblem != "auto" ? player.Emblem : FleetPresentation.DefaultEmblem(faction);
            return new CommanderAppearance(side, npc ? "НПЦ" : side == 0 ? player.Name : "Игрок 2",
                npc ? FleetPresentation.NeutralColor : side == 0 ? player.Color : FleetPresentation.DistinctColor(FleetPresentation.PlayerColors[1], [player.Color]), emblem, faction, npc);
        }).ToArray();
    }
    private CommanderAppearance Commander(int side) => _commanders.Single(c => c.Side == side);
    private string SideName(int side) => Commander(side).Name;

    private CommanderAppearance[] ValidateCommanders(CommanderAppearance[]? incoming)
    {
        if (incoming == null) { InitializeCommanders(); return _commanders; }
        if (incoming.Length != 2 || incoming.Select(c => c.Side).Order().SequenceEqual(new[] { 0, 1 }) == false)
            throw new InvalidDataException("Invalid commander appearance data.");
        var used = new List<string>();
        return incoming.OrderBy(c => c.Side).Select(c =>
        {
            var profile = FleetPresentation.Normalize(new(c.Name, c.Color, c.Emblem));
            string color = c.IsNpc ? FleetPresentation.NeutralColor : FleetPresentation.DistinctColor(profile.Color, used);
            if (!c.IsNpc) used.Add(color);
            return c with { Name = profile.Name, Color = color, Emblem = profile.Emblem == "auto" ? FleetPresentation.DefaultEmblem(c.Faction) : profile.Emblem };
        }).ToArray();
    }
}
