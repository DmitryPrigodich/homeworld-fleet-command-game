using System.Text.Json;
using System.Text.RegularExpressions;
using FleetCommand.Core;
internal static class ScenarioFeatureChecks
{
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly string _path; private readonly List<object> _trace = [];
        public Session(string root, string map, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C); S = E.Create(map, 1); Orders();
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
        }
        public void Orders(int side = 0)
        {
            string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
            S = S with { ActiveSide = side, Orders0 = 0, Orders1 = 0, Units = S.Units.Select(u => u with { Activated = false }).ToList(),
                Cards = S.Cards! with { Phase = "orders", Hand0 = a, Hand1 = b, Deck = Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), Discard = [], Ready0 = false, Ready1 = false } };
        }
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Input changed or save/replay diverged"); S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, viewer))); Check(view.RandomState == 1 && view.Cards!.Deck.Length == 0 && !E.Apply(view, viewer, cmd).Accepted, "Private view acquired authority"); }
        }
        public void Settle()
        {
            for (int i = 0; i < 30; i++)
            {
                if (S.Combat is { } combat) Do(FleetCombat.DecisionSide(combat), new(combat.AbilityRerolls > 0 ? "ability-reroll" : "resolve", Dice: []));
                else if (S.Defense is { } defense) Do(1 - defense.Side, new("decline-defense"));
                else if (S.CapEntry is { } cap) Do(cap.Side, new("decline-cap"));
                else if (S.Overrun is { } overrun) Do(overrun.Side, new("decline-overrun"));
                else if (S.Cards?.Response is { } response) Do(1 - response.Side, new("decline"));
                else return;
            }
            throw new Exception("Decision sequence did not settle");
        }
        public void At(string id, Hex hex) => S = S with { Units = S.Units.Select(u => u.Id == id ? u with { Q = hex.Q, R = hex.R } : u).ToList() };
        public void Mark()
        {
            Orders(1); At("s1-3", new(5, 2)); Do(1, new("move", "s1-3", Q: 4, R: 2, Card: "order-21")); Settle();
            Check(ScenarioEvents.HasToken(S, "s1-3", "data"), "Data was not collected at order completion");
        }
    }
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    private static void Reject(Action action) { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Expected invalid-data rejection"); }
    public static void Run(string root)
    {
        int count = 0;
        void Case(string name, string map, Action<Session> test)
        {
            try { test(new(root, map, name)); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); }
            count++; Console.WriteLine("SCENARIO FEATURE PASS " + name);
        }
        Case("large-footprint-load-and-overlap", "practice:large", s =>
        {
            var u = s.S.Units.Single(u => u.Id == "s0-0"); Check(FleetFootprint.Cells(s.C, u).SequenceEqual([new Hex(2, 2), new(1, 2)]), "Incorrect bow/stern cells");
            s.At("s0-1", new(1, 2)); Reject(() => s.E.Load(s.E.Save(s.S)));
        });
        Case("large-each-end-has-separate-budget", "practice:large", s =>
        {
            var u = s.S.Units[0]; var moves = s.E.GetActions(s.S, 0, u.Id).MoveCells; Check(moves.Length > 0, "No wide-ship moves");
            foreach (var h in moves)
            {
                var steps = FleetMovement.Path(s.C, s.S, u, h)!; int front = 0, rear = 0; var prior = new MovementStep(new(u.Q, u.R), u.Facing);
                foreach (var step in steps) { var cost = FleetLargeMovement.Cost(s.C, s.S, u, prior, step); front += cost.Front; rear += cost.Rear; prior = step; }
                Check(front <= 2 && rear <= 2 && FleetFootprint.CanPlace(s.C, s.S, u, h, steps[^1].Facing), "A highlighted move exceeds one end's budget");
                Check(s.E.Apply(s.S, 0, new("move", u.Id, Q: h.Q, R: h.R, Card: "order-01")).Accepted, "Highlighted destination rejected");
            }
        });
        Case("large-pivot-checks-the-stern", "practice:large", s =>
        {
            s.Do(0, new("move", "s0-0", Q: 2, R: 2, Facing: 1, AutoFacing: false, Card: "order-01")); s.Settle();
            Check(FleetFootprint.Cells(s.C, s.S.Units.Single(u => u.Id == "s0-0")).Contains(new(2, 1)), "Stern did not pivot");
            s.Orders(); s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("core:planet", 1, 2)] };
            Check(FleetMovement.Path(s.C, s.S, s.S.Units.Single(u => u.Id == "s0-0"), new(2, 2), 0) == null, "Pivot put stern inside planet");
        });
        Case("large-boundaries-and-enemy-stern-occupancy", "practice:large", s =>
        {
            var u = s.S.Units[0]; Check(FleetMovement.Path(s.C, s.S, u, new(0, 2), 0) == null, "Stern left board");
            s.At("s0-1", new(3, 2)); Check(!s.E.GetActions(s.S, 0, "s0-1").MoveCells.Contains(new(1, 2)), "Move ended in another ship's stern");
        });
        Case("large-ranged-and-boarding-reach-the-stern", "practice:large", s =>
        {
            s.At("s1-2", new(0, 2)); var enemy = s.S.Units.Single(u => u.Id == "s1-2"); s.C.Ships[enemy.Definition] = s.C.Ships[enemy.Definition] with { Footprint = 1, Crew = 3, Range = 1, Class = "frigate", Weapons = [new("test", "Test", 1, 1)] };
            Check(FleetWeapons.Attack(s.C, s.S, enemy, s.S.Units[0]).Allowed && FleetClose.Allowed(s.C, s.S, enemy, s.S.Units[0], "board"), "Only bow could be targeted");
        });
        Case("large-terrain-on-pivoted-stern-restores", "practice:large", s =>
        {
            s.C.Ships["practice:large-ship"] = s.C.Ships["practice:large-ship"] with { Armor = 50, Weapons = [new("defense", "Defense", 1, 1, DefensiveFire: true)] };
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("core:asteroids", 2, 1)] };
            s.Do(0, new("move", "s0-0", Q: 2, R: 2, Facing: 1, AutoFacing: false, Card: "order-01"));
            Check(s.S.Hazard != null && s.S.Cards!.Response!.MoveStart != null, "Stern entry did not produce a saved hazard"); s.Settle(); Check(s.S.Hazard == null, "Pivot never resumed");
        });
        Case("large-rear-shot-exemption", "practice:large", s =>
        {
            s.At("s1-2", new(0, 2)); var enemy = s.S.Units.Single(u => u.Id == "s1-2"); s.C.Ships[enemy.Definition] = s.C.Ships[enemy.Definition] with { Footprint = 1, DicePerModel = 1, Weapons = [new("test", "Test", 1, 3)], Evasive = 0 };
            s.C.Ships["practice:large-ship"] = s.C.Ships["practice:large-ship"] with { Armor = 2, Evasive = 0 };
            // A controlled pool of two hits kills this one-model ship only if an illegal rear penalty is applied.
            s.Orders(1); s.S = s.S with { ValorOwner = 1 }; s.Do(1, new("attack", enemy.Id, "s0-0", Card: "order-21"));
            if (s.S.Combat == null) s.Do(0, new("decline"));
            s.S = s.S with { Combat = s.S.Combat! with { Rolls = [6, 6], Rerolled = true } };
            s.Do(1, new("resolve")); Check(s.S.Units.Any(u => u.Id == "s0-0"), "Two-hex target received rear-shot armor penalty");
        });
        Case("large-launch-and-cap-adjacency-at-stern", "practice:large", s =>
        {
            var u = s.S.Units[0]; Check(FleetLogistics.Adjacent(s.C, s.S, u).Contains(new(0, 2)), "Launch cells omit the stern");
            s.At("s0-1", new(0, 2)); Check(FleetCap.Hosts(s.C, s.S, s.S.Units.Single(u => u.Id == "s0-1")).Any(h => h.Id == "s0-0"), "CAP cannot attach next to stern");
        });
        Case("first-contact-source-layout-and-profiles", "core:first-contact", s =>
        {
            var m = s.C.Maps[s.S.Map]; Check(m.Cells!.Length == 122 && m.Objects.Length == 7 && s.S.Units.Count == 6 && s.C.Rules[m.Rules].RoundLimit == 7, "Incorrect published setup");
            Check(s.S.Cards!.HandLimit0 == 5 && s.S.Cards.PlayLimit0 == 3 && s.S.Units.All(u => u.Models == 6), "Illustrated leaders/Interceptors were not applied");
            Check(s.E.GetActions(s.S, 0, "s0-0").MoveCells.All(h => FleetFootprint.Inside(m, h)), "Movement leaves the scenario mask");
        });
        Case("first-contact-scan-after-order-and-save", "core:first-contact", s => { s.Mark(); Check(s.S.Medals1 == 0 && s.S.Scenario!.Fired.Contains("scan"), "Scanning awarded delivery medals prematurely"); });
        Case("first-contact-delivery-keeps-unit-and-awards-once", "core:first-contact", s =>
        {
            s.Mark(); s.Orders(1); s.At("s1-3", new(14, 4)); s.Do(1, new("move", "s1-3", Q: 15, R: 4, Card: "order-21")); s.Settle();
            Check(s.S.Medals1 == 2 && s.S.Scenario!.Fired.Contains("delivered") && s.S.Units.Any(u => u.Id == "s1-3") && !ScenarioEvents.HasToken(s.S, "s1-3", "data"), "Delivery removed the unit or lost its award");
            s.Orders(1); s.At("s1-3", new(14, 4)); s.Do(1, new("move", "s1-3", Q: 15, R: 4, Card: "order-21")); s.Settle(); Check(s.S.Medals1 == 2, "Delivery medals farmed twice");
        });
        Case("first-contact-no-delivery-without-data", "core:first-contact", s =>
        { s.Orders(1); s.At("s1-3", new(14, 4)); s.Do(1, new("move", "s1-3", Q: 15, R: 4, Card: "order-21")); s.Settle(); Check(s.S.Medals1 == 0 && s.S.Scenario?.Fired.Contains("delivered") != true, "Empty unit delivered data"); });
        Case("first-contact-data-lost-on-destruction", "core:first-contact", s =>
        {
            s.Mark(); s.Orders(); s.At("s0-0", new(5, 2)); s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { Models = 1 } : u).ToList() };
            s.C.Ships["core:kushan-contact-interceptor"] = s.C.Ships["core:kushan-contact-interceptor"] with { Success = 2, Weapons = [new("test", "Test", 20, 2)] };
            s.C.Ships["core:taiidan-contact-interceptor"] = s.C.Ships["core:taiidan-contact-interceptor"] with { Evasive = 0 };
            s.Do(0, new("attack", "s0-0", "s1-3", Card: "order-01")); s.Settle();
            Check(!s.S.Units.Any(u => u.Id == "s1-3") && !ScenarioEvents.HasToken(s.S, "s1-3", "data") && s.S.Log!.Any(e => e.Text.Contains("данные", StringComparison.OrdinalIgnoreCase)), "Destroyed scout retained its data");
        });
        Case("first-contact-seven-round-defense-objective", "core:first-contact", s =>
        {
            for (int i = 0; i < 120 && !s.S.Finished; i++)
            {
                var cards = s.S.Cards!; int side = cards.Phase is "initiative" or "cleanup" ? cards.Ready0 ? 1 : 0 : s.S.ActiveSide;
                s.Do(side, new(cards.Phase == "initiative" ? "initiative" : cards.Phase == "cleanup" ? "cleanup" : "pass"));
            }
            Check(s.S is { Finished: true, Winner: 0, Medals0: 2, EndReason: "round-limit", Round: 8 } && s.S.Scenario!.Fired.Contains("prevented"), "Failure to deliver did not award the defense objective");
        });
        Case("scenario-round-reinforcements-are-once-only", "practice:large", s =>
        {
            s.C.Fleets["test:arrival"] = new("test:arrival", "Arrival", [new("practice:kushan-reserve-frigate", -1, -1, Reserve: true)]);
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Events = [new("arrivals", "round-start", 0, "reinforce", "Arrivals", Round: 2, Fleet: "test:arrival")] }; s.C.Validate();
            s.Do(0, new("pass")); s.Do(1, new("pass")); Check(s.S.Round == 2 && s.S.Reserves?.Count == 1 && s.S.Scenario!.Fired.SequenceEqual(["arrivals"]), "Scheduled reinforcements missing");
            s.Do(0, new("cleanup")); s.Do(1, new("cleanup")); Check(s.S.Reserves?.Count == 1, "Reinforcements duplicated after resume");
        });
        Case("scenario-invalid-definitions-and-state", "core:first-contact", s =>
        {
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Events = [new("bad", "end-order", 1, "mark", "Bad", Token: "data", Object: 999)] }; Reject(s.C.Validate);
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Events = [] }; Reject(() => s.E.Load(s.E.Save(s.S with { Scenario = new(["missing"], [], []) })));
        });
        Case("localization-catalogs-and-live-messages", "practice:large", s =>
        {
            var l = new Localizer(); l.Load([Path.Combine(root, "content", "locales")]); l.SetLanguage("en");
            var ru = JsonData.Read<LanguagePack>(Path.Combine(root, "content", "locales", "ru.json"));
            foreach (string key in ru.Messages.Keys) Check(!Regex.IsMatch(l.Text(key), "[А-Яа-яЁё]"), "Missing English translation: " + key);
            Check(l.Text("Раунд 3 · Бой: Бой начался.") == "Round 3 · Battle: Battle started.", "Dynamic log localization failed");
            string before = s.E.Save(s.S); l.SetLanguage("ru"); Check(l.Text("ОПЦИИ") == "ОПЦИИ" && s.E.Save(s.S) == before, "Language changed gameplay state");
            l.SetLanguage("unknown"); Check(l.Language == "ru", "Unknown language has no fallback");
        });
        Case("localization-extra-language-and-fallback", "practice:large", s =>
        {
            string dir = Path.Combine(root, "tmp", "test-results", "extra-language"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "de.json"), JsonSerializer.Serialize(new LanguagePack("de", "Deutsch", new() { ["ОПЦИИ"] = "Optionen" }, "en"), JsonData.Options));
            var l = new Localizer(); l.Load([Path.Combine(root, "content", "locales"), dir]); l.SetLanguage("de");
            Check(l.Text("ОПЦИИ") == "OPTIONEN" && l.Text("ВЫХОД") == "EXIT", "Additional language or fallback failed");
        });
        Console.WriteLine($"SCENARIO_FEATURES_OK {count} scenarios");
    }
}
