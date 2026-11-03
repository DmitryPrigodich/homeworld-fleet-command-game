using FleetCommand.Core;
using System.Text.Json;

internal static class EconomyScenarios
{
    private const string Actor = "s0-0", Other = "s0-1";
    private sealed class Session
    {
        public readonly ContentCatalog C;
        public readonly BattleEngine E;
        public BattleState S;
        private readonly string _path;
        private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C);
            C.Ships["practice:kushan-collector"] = C.Ships["practice:kushan-collector"] with { Armor = 20, Success = 2, Evasive = 1, Move = 5 };
            S = E.Create(BasicCardBattle.EconomyMapId, 1);
            string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
            S = S with { RandomState = 1, ActiveSide = 0, Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"),
                Deposits = new() { [0] = new(3), [1] = new(2), [2] = new(0) } };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json"); Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        }
        public void Check(bool value, string why) { if (!value) throw new Exception(why); }
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string original = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(original), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, state = a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, "Economy command: " + a.Message);
            Check(E.Save(S) == original && E.Save(a.State) == E.Save(b.State), "Economy input mutated or diverged after restore");
            S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 })
            {
                var view = E.Load(E.Save(BasicCardRules.View(S, viewer)));
                Check(view.RandomState == 1 && !E.Apply(view, viewer, cmd).Accepted, "Resource view received authoritative RNG/commands");
                foreach (var (index, deposit) in S.Deposits!)
                    Check(view.Deposits![index].Stars == (deposit.Harvested || (deposit.SurveyMask & (1 << viewer)) != 0 ? deposit.Stars : -1)
                        && view.Deposits[index].SurveyMask == (deposit.SurveyMask & (1 << viewer)), "Hidden resource value or opponent knowledge leaked");
            }
        }
        public void Begin() => Do(0, new("begin-order", Actor, "harvest", Card: "order-01"));
        public void ResetOrder(string card = "order-09") => S = S with { ActiveSide = 0, Orders0 = 0, Units = S.Units.Select(u => u with { Activated = false }).ToList() };
        public void MoveOther(int q, int r) => S = S with { Units = S.Units.Select(u => u.Id == Other ? u with { Q = q, R = r } : u).ToList() };
        public void Cargo(int amount) => S = S with { Units = S.Units.Select(u => u.Id == Actor ? u with { Cargo = amount } : u).ToList() };
    }
    public static void Run(string root)
    {
        void Case(string name, Action<Session> run) { run(new(root, name)); Console.WriteLine("ECONOMY SCENARIO PASS " + name); }
        Case("economy-secret-bag-is-deterministic-and-shuffled", s =>
        {
            var a = s.E.Create(BasicCardBattle.EconomyMapId, 321); var b = s.E.Create(BasicCardBattle.EconomyMapId, 321);
            s.Check(s.E.Save(a) == s.E.Save(b), "Setup shuffle is not reproducible");
            var layouts = new HashSet<string>();
            for (uint seed = 1; seed <= 30; seed++)
            {
                var state = s.E.Create(BasicCardBattle.EconomyMapId, seed);
                s.Check(state.Deposits!.Values.Select(d => d.Stars).Order().SequenceEqual(new[] { 0, 2, 3 }), "Shuffle changed the resource bag");
                layouts.Add(string.Join(",", state.Deposits.Values.Select(d => d.Stars)));
                s.Check(BasicCardRules.View(state, 0).Deposits!.Values.All(d => d.Stars == -1), "Unscouted setup exposed values");
            }
            s.Check(layouts.Count > 1, "Resource values have a fixed spatial assignment");
        });
        Case("economy-survey-is-private-and-only-at-movement-end", s =>
        {
            s.Do(0, new("move", Actor, Q: 2, R: 2, Card: "order-01"));
            s.Check(FleetEconomy.KnownStars(s.S, 0, 0) == 3 && FleetEconomy.KnownStars(s.S, 0, 1) == null && s.S.Units.Single(u => u.Id == Actor).Cargo == 0, "Survey revealed to the wrong side or collected resources");
            s.Check(!s.S.Log!.Any(e => e.Text.Contains("3 ★")), "Survey value leaked in the public log");
        });
        Case("economy-survey-does-not-reveal-passed-fields", s =>
        {
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("core:asteroids", 2, 1), new("core:asteroids", 2, 0), new("core:asteroids", 6, 2)] };
            s.Do(0, new("move", Actor, Q: 1, R: 0, Card: "order-01"));
            s.Check(FleetEconomy.KnownStars(s.S, 0, 0) == null && FleetEconomy.KnownStars(s.S, 1, 0) == 2, "Survey ran while passing a field rather than at movement end");
        });
        Case("economy-survey-reveals-all-adjacent-fields", s =>
        {
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("core:asteroids", 3, 2), new("core:asteroids", 2, 3), new("core:asteroids", 1, 3)] };
            s.Do(0, new("move", Actor, Q: 2, R: 2, Card: "order-01"));
            s.Check(s.S.Deposits!.Values.All(d => d.SurveyMask == 1) && s.S.Units.Single(u => u.Id == Actor).Cargo == 0, "Survey omitted neighboring fields or collected resources");
        });
        Case("economy-interrupt-kills-free-mover-before-survey-or-harvest", s =>
        {
            s.C.Ships["practice:kushan-collector"] = s.C.Ships["practice:kushan-collector"] with { Armor = 0 };
            s.C.Ships["core:taiidan-interceptor"] = s.C.Ships["core:taiidan-interceptor"] with { Success = 2, Abilities = [], Weapons = [new("gun", "Gun", 4, 3)] };
            string[] a = ["order-01", "order-09"], b = ["order-17", "order-22"];
            s.S = s.S with { FactionUsed1 = true, Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"),
                Units = s.S.Units.Select(u => u.Id == "s1-4" ? u with { Q = 2, R = 1, Models = 1 } : u).ToList() };
            s.Begin(); s.Do(0, new("move", Actor, Q: 2, R: 2));
            s.Check(s.S.Cards!.Response != null && FleetEconomy.KnownStars(s.S, 0, 0) == null, "Interrupted movement already revealed adjacent resources");
            s.Do(1, new("interrupt", "s1-4", Actor, Card: "order-17")); s.Do(0, new("ability-reroll", Dice: []));
            s.Check(!s.S.Units.Any(u => u.Id == Actor) && s.S.Cards!.Pending == null && s.S.Cards.Response == null && FleetEconomy.KnownStars(s.S, 0, 0) == null && !s.S.Deposits![0].Harvested && s.S.Orders0 == 1, "Destroyed free mover surveyed, harvested or retained an order");
        });
        Case("economy-free-move-surveys-before-harvest", s =>
        {
            s.Begin(); s.Do(0, new("harvest", Actor, Object: 0), false);
            s.Do(0, new("move", Actor, Q: 2, R: 2));
            s.Check(s.S.Cards!.Pending is { BasicAction: "harvest", Step: 1 } && FleetEconomy.KnownStars(s.S, 0, 0) == 3 && s.S.Orders0 == 1, "Free move finished or double-paid Harvest");
            string before = s.E.Save(s.S); var preview = s.E.GetActions(s.S, 0, Actor);
            s.Check(preview.MoveCells.Length == 0 && preview.HarvestObjects!.Contains(0) && s.E.Save(s.S) == before, "Harvest preview mutated state or omitted the target");
            s.Do(1, new("harvest", Actor, Object: 0), false); s.Do(0, new("harvest", Actor, Object: 0));
            s.Check(s.S.Units.Single(u => u.Id == Actor).Cargo == 3 && s.S.Harvested0 == 3 && s.S.Stars0 == 0 && s.S.Cards!.Pending == null && s.S.Orders0 == 1, "Harvest cargo, objective total or Play accounting is incorrect");
            s.Check(BasicCardRules.View(s.S, 1).Deposits![0] is { Harvested: true, Stars: 3 }, "Harvested field did not become public");
        });
        Case("economy-harvest-zero-and-retain-dangerous-terrain", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Actor ? u with { Q = 5, R = 2 } : u).ToList() };
            s.Begin(); s.Do(0, new("skip")); s.Do(0, new("harvest", Actor, Object: 2));
            s.Check(s.S.Deposits![2].Harvested && s.S.Units.Single(u => u.Id == Actor).Cargo == 0 && FleetTerrain.Visible(s.C, s.S, 2) && FleetTerrain.Attack(s.C.Objects["core:asteroids"])!.Dice == 3, "A zero deposit was reusable or terrain disappeared");
            s.ResetOrder(); s.Do(0, new("begin-order", Actor, "harvest", Card: "order-09")); s.Do(0, new("skip"));
            s.Do(0, new("harvest", Actor, Object: 2), false);
        });
        Case("economy-harvest-occupied-field-and-range-rejections", s =>
        {
            s.MoveOther(3, 2); s.Begin(); s.Do(0, new("skip"));
            s.Do(0, new("harvest", Actor, Object: -1), false); s.Do(0, new("harvest", Actor, Object: 1), false); s.Do(0, new("harvest", Other, Object: 0), false);
            s.Do(0, new("harvest", Actor, Object: 0)); s.Check(s.S.Units.Single(u => u.Id == Actor).Cargo == 3, "Occupied asteroid could not be harvested");
        });
        Case("economy-free-move-waits-for-entry-attack", s =>
        {
            s.S = s.S with { RandomState = 3 }; s.Begin(); s.Do(0, new("move", Actor, Q: 3, R: 2));
            s.Check(s.S.Hazard?.Kind == "asteroid" && s.S.Cards!.Pending!.Step == 0 && s.S.Cards.Response != null, "Terrain did not suspend the free move");
            s.S = s.S with { Combat = s.S.Combat! with { Rolls = [6, 2, 2] } };
            s.Do(0, new("ability-reroll", Dice: []));
            s.Check(s.S.Cards!.Pending?.Step == 1 && s.S.Units.Single(u => u.Id == Actor).Cargo == 0, "Terrain skipped the harvest choice");
            s.Do(0, new("harvest", Actor, Object: 0));
        });
        Case("economy-transfer-sender-or-recipient-activates-once", s =>
        {
            s.MoveOther(2, 2); s.Cargo(3);
            s.Do(0, new("transfer-stars", Actor, Other, Amount: 2, Card: "order-01"));
            s.Check(s.S.Units.Single(u => u.Id == Actor) is { Cargo: 1, Activated: true } && s.S.Units.Single(u => u.Id == Other) is { Cargo: 2, Activated: false } && s.S.Orders0 == 1, "Sender transfer activated both units or spent extra Play");
            s.ResetOrder(); s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Other ? u with { Activated = true } : u).ToList() };
            s.Do(0, new("transfer-stars", Actor, Other, Amount: 2, Receive: true, Card: "order-09"));
            s.Check(s.S.Units.Single(u => u.Id == Actor).Cargo == 3 && s.S.Units.Single(u => u.Id == Other).Cargo == 0 && s.S.Harvested0 == 0 && s.S.Stars0 == 0, "Recipient could not retrieve from an activated neighbor or transfer changed score");
        });
        Case("economy-transfer-invalid-inputs-spend-nothing", s =>
        {
            s.MoveOther(2, 2); s.Cargo(3);
            foreach (int amount in new[] { -1, 0, 4, int.MaxValue }) s.Do(0, new("transfer-stars", Actor, Other, Amount: amount, Card: "order-01"), false);
            s.Do(1, new("transfer-stars", Actor, Other, Amount: 1, Card: "order-21"), false);
            s.Do(0, new("transfer-stars", Actor, "s1-3", Amount: 1, Card: "order-01"), false);
            s.MoveOther(4, 1); s.Do(0, new("transfer-stars", Actor, Other, Amount: 1, Card: "order-01"), false);
            s.Check(s.S.Orders0 == 0 && s.S.Units.Single(u => u.Id == Actor).Cargo == 3, "Invalid transfer changed cargo or orders");
        });
        Case("economy-transfer-waits-for-repair-and-terrain", s =>
        {
            s.MoveOther(2, 2); s.Cargo(3); s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = s.C.Maps[s.S.Map].Objects.Select((o, i) => i == 0 ? o with { Q = 1, R = 2 } : o).ToArray() };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Actor ? u with { FocusFire = 1 } : u).ToList() };
            s.Do(0, new("transfer-stars", Actor, Other, Amount: 3, Card: "order-01", Repair: "ask"));
            s.Check(s.S.Repairs is { Length: 1 } && s.S.Units.Single(u => u.Id == Other).Cargo == 0, "Resources moved before activation repair");
            s.Do(0, new("repair-choice", Repair: "focus")); s.Check(s.S.Hazard != null && s.S.Units.Single(u => u.Id == Other).Cargo == 0, "Resources moved before terrain");
            s.S = s.S with { Combat = s.S.Combat! with { Rolls = [6, 2, 2] } }; s.Do(0, new("ability-reroll", Dice: []));
            s.Check(s.S.Cards!.Pending == null && s.S.Units.Single(u => u.Id == Other).Cargo == 3 && s.S.Orders0 == 1, "Deferred transfer failed after terrain");
        });
        Case("economy-cargo-destroyed-before-transfer-is-lost", s =>
        {
            s.S = s.S with { RandomState = 3 }; s.MoveOther(2, 2); s.Cargo(3); s.C.Ships["practice:kushan-collector"] = s.C.Ships["practice:kushan-collector"] with { Armor = 0 };
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = s.C.Maps[s.S.Map].Objects.Select((o, i) => i == 0 ? o with { Q = 1, R = 2 } : o).ToArray() };
            s.Do(0, new("transfer-stars", Actor, Other, Amount: 3, Card: "order-01"));
            s.S = s.S with { Combat = s.S.Combat! with { Rolls = [5, 2, 2] } }; s.Do(0, new("ability-reroll", Dice: []));
            s.Check(!s.S.Units.Any(u => u.Id == Actor) && s.S.Units.Single(u => u.Id == Other).Cargo == 0 && s.S.Stars1 == 0 && s.S.Cards!.Pending == null, "Destroyed cargo was transferred or credited as victory stars");
        });
        Case("economy-missing-harvest-ability-and-malformed-save", s =>
        {
            s.Do(0, new("begin-order", Other, "harvest", Card: "order-01"), false);
            foreach (var bad in new[] { s.S with { Deposits = null }, s.S with { Deposits = new() { [0] = new(3), [1] = new(3), [2] = new(0) } }, s.S with { Harvested0 = 3 }, s.S with { Units = s.S.Units.Select(u => u.Id == Actor ? u with { Cargo = -1 } : u).ToList() } })
            {
                bool rejected = false; try { s.E.Load(s.E.Save(bad)); } catch (InvalidDataException) { rejected = true; }
                s.Check(rejected, "Malformed resource save was accepted");
            }
        });
    }
}

