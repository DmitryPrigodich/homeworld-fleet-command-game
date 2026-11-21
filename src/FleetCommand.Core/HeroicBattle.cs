namespace FleetCommand.Core;

// Published leaders and command faces, with deliberately synthetic ship profiles for practice.
public static class HeroicBattle
{
    public const string MapId = "practice:heroes";
    public const string ReactionNetworkMapId = "practice:heroic-reaction-network";
    public const string ReactionMapId = "practice:heroic-reaction";
    public const string PilotLaunchSoloMapId = "practice:pilot-launch-heroes-solo";
    public const string PilotLaunchMapId = "practice:pilot-launch-heroes";
    public const string AdvancedMapId = "practice:advanced-heroes";
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
        c.Ships["practice:hero-boarding-frigate"] = c.Ships["practice:orders-kushan-boarder"] with { Id = "practice:hero-boarding-frigate", Name = "Hero drill: boarding frigate", Label = "Hero drill: boarding frigate", Tabs = null, Afterburners = 1 };
        c.Ships["practice:hero-kushan-fighter"] = c.Ships["practice:hero-fighter"] with { Id = "practice:hero-kushan-fighter", Faction = "kushan", Name = "Hero drill: allied fighter", Label = "Hero drill: allied fighter" };
        c.Fleets["practice:advanced-hero-kushan"] = new("practice:advanced-hero-kushan", "More Heroic commands / Kushan", [
            new("practice:hero-boarding-frigate", 2, 2, Leader: "core:hero-kushan-master-of-arms", AdditionalLeaders: ["core:hero-kushan-medical-chief"]),
            new("practice:hero-kushan-fighter", 1, 4, Leader: "core:hero-kushan-fighter-ace"),
            new("practice:hero-boarding-frigate", 2, 5, Leader: "core:hero-kushan-grizzled-veteran"),
            new("practice:hero-kushan-fighter", 2, 4)]);
        c.Fleets["practice:advanced-hero-taiidan"] = new("practice:advanced-hero-taiidan", "More Heroic commands / Taiidan", [
            new("practice:taiidan-carrier", 8, 1, 3, "core:hero-taiidan-hunter"),
            new("practice:hero-frigate", 8, 3, 3, "core:hero-taiidan-veteran-captain"),
            new("practice:hero-frigate", 8, 5, 3)]);
        c.Ships["practice:hero-pilot-corvette"] = c.Ships["practice:orders-taiidan-boarder"] with { Id = "practice:hero-pilot-corvette", Name = "Hero drill: pilot corvette", Label = "Hero drill: pilot corvette", Class = "corvette", Move = 3, Afterburners = 2, Tabs = null };
        c.Ships["practice:hero-launch-fighter"] = c.Ships["practice:taiidan-wing"] with { Id = "practice:hero-launch-fighter", Name = "Hero drill: launch fighter", Label = "Hero drill: launch fighter", Afterburners = 2 };
        c.Fleets["practice:pilot-launch-kushan"] = new("practice:pilot-launch-kushan", "Pilot and launch targets", [new("practice:orders-kushan-boarder", 6, 2), new("practice:orders-kushan-boarder", 5, 5)]);
        c.Fleets["practice:pilot-launch-taiidan"] = new("practice:pilot-launch-taiidan", "Pilot and launch Heroes", [
            new("practice:taiidan-carrier", 8, 5, 3, "core:hero-taiidan-launch-commander"),
            new("practice:hero-pilot-corvette", 8, 2, 3, "core:hero-taiidan-veteran-commander"),
            new("practice:hero-launch-fighter", -1, -1, 3, EmbarkedOn: 0),
            new("practice:hero-launch-fighter", -1, -1, 3, EmbarkedOn: 0),
            new("practice:taiidan-carrier", 10, 5, 3),
            new("practice:hero-pilot-corvette", -1, -1, 3, EmbarkedOn: 0)]);
        c.Objects["practice:hero-asteroid"] = c.Objects["core:asteroids"] with { Id = "practice:hero-asteroid", Kind = "asteroid", MoveCost = 1 };
        c.Maps[PilotLaunchMapId] = c.Maps[MapId] with { Id = PilotLaunchMapId, Name = "Pilot and launch Heroes", Width = 12, Height = 8,
            Fleets = ["practice:pilot-launch-kushan", "practice:pilot-launch-taiidan"], Objects = [new("practice:hero-asteroid", 7, 2), new("practice:mines", 5, 2), new("practice:hero-asteroid", 4, 2)] };
        c.Maps[PilotLaunchSoloMapId] = c.Maps[PilotLaunchMapId] with { Id = PilotLaunchSoloMapId, Fleets = ["practice:pilot-launch-taiidan", "practice:pilot-launch-kushan"] };
        c.Ships["practice:hero-recon-corvette"] = c.Ships["practice:orders-kushan-boarder"] with { Id = "practice:hero-recon-corvette", Name = "Hero drill: recon corvette", Label = "Hero drill: recon corvette", Class = "corvette", Move = 3, Afterburners = 1, Tabs = null };
        c.Fleets["practice:reaction-kushan"] = new("practice:reaction-kushan", "Heroic reaction / Kushan", [new("practice:hero-recon-corvette", 2, 3, Leader: "core:hero-kushan-recon-commander"), new("practice:orders-kushan-boarder", 2, 5)]);
        c.Fleets["practice:reaction-taiidan"] = new("practice:reaction-taiidan", "Heroic reaction / Taiidan", [new("practice:orders-taiidan-boarder", 6, 3, 3), new("practice:orders-taiidan-boarder", 6, 5, 3)]);
        c.Maps[ReactionMapId] = c.Maps[MapId] with { Id = ReactionMapId, Name = "Heroic reaction", Width = 10, Height = 8, Fleets = ["practice:reaction-kushan", "practice:reaction-taiidan"] };
        c.Maps[ReactionNetworkMapId] = c.Maps[ReactionMapId] with { Id = ReactionNetworkMapId, Fleets = ["practice:reaction-taiidan", "practice:reaction-kushan"] };
        c.Maps[AdvancedMapId] = c.Maps[MapId] with { Id = AdvancedMapId, Name = "More Heroic commands", Width = 12, Height = 8,
            Fleets = ["practice:advanced-hero-kushan", "practice:advanced-hero-taiidan"] };
    }
}
