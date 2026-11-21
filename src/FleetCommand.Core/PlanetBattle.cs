namespace FleetCommand.Core;

public static class PlanetBattle
{
    public const string MapId = "practice:planetary-overwatch", NetworkMapId = "practice:planetary-overwatch-network";
    public static void Register(ContentCatalog c)
    {
        c.Ships["practice:industrial-planet"] = new("practice:industrial-planet", "Industrial World / Taiidan", "taiidan", "", 1, 0, 0, 5, 5, 0,
            Class: "planet", Stars: 4, Label: "Industrial World", TacticalIcon: "circle",
            Weapons: [new("defense", "Defence Batteries", 6, 0, Kind: "close", DefensiveFire: true, DefenseOnly: true)],
            Facilities: [new("hardened", "Hardened Defences", Armor: 2, Hardened: true, Stars: 2),
                new("ions", "Ion Batteries", [new("heavy-ion", "Heavy Ion Cannon", 6, 3, Penetrating: 1, PowerPenetrating: 1, Targets: ["corvette", "platform", "frigate", "capital", "station", "planet"])], Stars: 3),
                new("missiles", "Missile Defence", [new("ballistic", "Ballistic Missiles", 6, 3, Kind: "missile", PowerDice: 2)], Stars: 3)]);
        c.Fleets["practice:planet-taiidan"] = new("practice:planet-taiidan", "Planet defences / Taiidan", [
            new("practice:industrial-planet", 3, 3, FacilityLeaders: new() { ["ions"] = "core:hero-taiidan-battery-coordinator" }),
            new("practice:orders-taiidan-boarder", 1, 5)]);
        c.Fleets["practice:planet-kushan"] = new("practice:planet-kushan", "Planet targets / Kushan", [new("practice:orders-kushan-boarder", 6, 3, 3), new("practice:orders-kushan-boarder", 6, 5, 3)]);
        c.Maps[MapId] = c.Maps[BasicCardBattle.OrdersMapId] with { Id = MapId, Name = "Planetary Overwatch and facilities", Width = 10, Height = 8,
            Fleets = ["practice:planet-taiidan", "practice:planet-kushan"], Objects = [new("core:planet", 3, 3)] };
        c.Fleets["practice:planet-network-source"] = new("practice:planet-network-source", "Planet attack source", [new("practice:industrial-planet", 6, 3, 3), new("practice:orders-taiidan-boarder", 8, 5, 3)]);
        c.Maps[NetworkMapId] = c.Maps[MapId] with { Id = NetworkMapId, Fleets = ["practice:planet-network-source", "practice:planet-taiidan"], Objects = [new("core:planet", 3, 3), new("core:planet", 6, 3)] };
    }
}
