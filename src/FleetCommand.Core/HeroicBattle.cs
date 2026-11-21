namespace FleetCommand.Core;

// Published leaders and command faces, with deliberately synthetic ship profiles for practice.
public static class HeroicBattle
{
    public const string MapId = "practice:heroes";
    public static void Register(ContentCatalog c)
    {
        c.Ships["practice:hero-capital"] = c.Ships["practice:kushan-carrier"] with { Id = "practice:hero-capital", Name = "Hero drill: capital ship", Label = "Hero drill: capital ship", Tabs = null };
        c.Ships["practice:hero-station"] = c.Ships["practice:station"] with { Id = "practice:hero-station", Faction = "kushan", Name = "Hero drill: station", Label = "Hero drill: station", Tabs = null, Weapons = [new("main", "Training station guns", 3, 6, DefensiveFire: true)] };
        c.Ships["practice:hero-fighter"] = c.Ships["practice:taiidan-wing"] with { Id = "practice:hero-fighter", Name = "Hero drill: fighter", Label = "Hero drill: fighter", Tabs = null };
        c.Ships["practice:hero-frigate"] = c.Ships["practice:orders-taiidan-boarder"] with { Id = "practice:hero-frigate", Name = "Hero drill: engineer frigate", Label = "Hero drill: engineer frigate", Tabs = null };
        c.Fleets["practice:hero-kushan"] = new("practice:hero-kushan", "Hero practice / Kushan", [
            new("practice:hero-capital", 1, 1, Leader: "core:hero-kushan-strategist"),
            new("practice:hero-station", 1, 4, Leader: "core:hero-kushan-station-overseer"),
            new("practice:orders-kushan-boarder", 2, 4)]);
        c.Fleets["practice:hero-taiidan"] = new("practice:hero-taiidan", "Hero practice / Taiidan", [
            new("practice:hero-fighter", 7, 1, 3, "core:hero-taiidan-hot-shot-pilot"),
            new("practice:hero-frigate", 7, 4, 3, "practice:taiidan-officer", AdditionalLeaders: ["core:hero-taiidan-master-engineer"]),
            new("practice:orders-taiidan-boarder", 8, 4, 3)]);
        c.Maps[MapId] = c.Maps[BasicCardBattle.OrdersMapId] with { Id = MapId, Name = "Heroes and Heroic commands", Width = 10, Height = 7, Fleets = ["practice:hero-kushan", "practice:hero-taiidan"] };
    }
}
