using FleetCommand.Core;

internal static class ActionPreviewChecks
{
    public static void Run(ContentCatalog source, Action<bool, string> check)
    {
        var content = source.CopyForRuntime();
        content.Ships["preview:ship"] = new("preview:ship", "Test ship", "test", "", 1, 2, 1, 4, 0, 4);
        content.Objects["preview:rock"] = new("preview:rock", "Rock", BlocksMovement: true);
        content.Objects["preview:rough"] = new("preview:rough", "Rough terrain", MoveCost: 2, AttackPenalty: 2);
        content.Rules["preview:rules"] = new("preview:rules", "Preview fixture");
        content.Fleets["preview:blue"] = new("preview:blue", "Blue", [new("preview:ship", 0, 1), new("preview:ship", 1, 1)]);
        content.Fleets["preview:red"] = new("preview:red", "Red", [new("preview:ship", 4, 1), new("preview:ship", 6, 2)]);
        MapObject[] objects = [new("preview:rock", 0, 0), new("preview:rock", 0, 2), new("preview:rock", 1, 0), new("preview:rough", 2, 1)];
        content.Maps["preview:map"] = new("preview:map", "Preview test", 7, 3, "", objects, ["preview:blue", "preview:red"], "preview:rules");
        content.Validate();
        var engine = new BattleEngine(content);
        var state = engine.Create("preview:map", 87);
        var unit = state.Units[0]; var enemy = state.Units[2];
        string before = engine.Save(state);
        var options = engine.GetActions(state, 0, unit.Id);
        check(options.MoveCells.Contains(new(2, 0)) && !options.MoveCells.Contains(new(1, 1)),
            "preview permits passing through friendly units without marking occupied destinations");
        check(!options.MoveCells.Contains(new(2, 1)) && !options.MoveCells.Contains(new(1, 0)),
            "movement preview respects terrain costs and impassable objects");
        check(options.AttackTargets.SequenceEqual(new[] { enemy.Id }),
            "attack preview includes in-range enemies through cover and excludes distant targets");
        check(options.MoveCells.All(h => engine.Apply(state, 0, new("move", unit.Id, Q: h.Q, R: h.R)).Accepted)
            && Enumerable.Range(0, 7).SelectMany(q => Enumerable.Range(0, 3).Select(r => new Hex(q, r))).All(h =>
                engine.Apply(state, 0, new("move", unit.Id, Q: h.Q, R: h.R)).Accepted == options.MoveCells.Contains(h)),
            "every highlighted move agrees with command validation across the board");
        check(state.Units.All(target => engine.Apply(state, 0, new("attack", unit.Id, target.Id)).Accepted == options.AttackTargets.Contains(target.Id)),
            "attack highlights agree with actual target validation");
        check(engine.Save(state) == before, "previews leave random state, orders, units and history unchanged");
        bool Empty(UnitActions actions) => actions.MoveCells.Length == 0 && actions.AttackTargets.Length == 0;
        check(Empty(engine.GetActions(state, 1, unit.Id)) && Empty(engine.GetActions(state, 0, enemy.Id))
            && Empty(engine.GetActions(state with { Orders0 = 3 }, 0, unit.Id))
            && Empty(engine.GetActions(state with { Finished = true }, 0, unit.Id))
            && Empty(engine.GetActions(state with { Units = state.Units.Select(u => u.Id == unit.Id ? u with { Activated = true } : u).ToList() }, 0, unit.Id)),
            "no legal-action highlights for wrong side, exhausted units, spent orders or finished battles");
        content.Rules["preview:rules"] = content.Rules["preview:rules"] with { FriendlyPassThrough = false };
        check(engine.GetActions(state, 0, unit.Id).MoveCells.Length == 0, "preview honors rules that prevent passing through friendly units");
        content.Rules["preview:rules"] = content.Rules["preview:rules"] with { FriendlyPassThrough = true };
        content.Ships["preview:ship"] = content.Ships["preview:ship"] with { Abilities = ["core:fast"] };
        check(engine.GetActions(state, 0, unit.Id).MoveCells.Contains(new(2, 1)), "movement ability bonuses extend the preview through costly terrain");
        content.Maps["preview:map"] = content.Maps["preview:map"] with { Objects = [.. objects, new("preview:rock", 3, 1)] };
        check(!engine.GetActions(state, 0, unit.Id).AttackTargets.Contains(enemy.Id), "objects blocking line of sight remove red attack targets");
        content.Maps["preview:map"] = content.Maps["preview:map"] with { Objects = objects };
        content.Ships["preview:ship"] = content.Ships["preview:ship"] with { Range = 1 };
        check(engine.GetActions(state, 0, unit.Id).AttackTargets.Length == 0,
            "red targets use the current position rather than hypothetical positions after moving");
        content.Ships["preview:ship"] = content.Ships["preview:ship"] with { Range = 4, DicePerModel = 0 };
        check(engine.GetActions(state, 0, unit.Id).AttackTargets.Length == 0, "unarmed ships have no attack highlights");
    }
}
