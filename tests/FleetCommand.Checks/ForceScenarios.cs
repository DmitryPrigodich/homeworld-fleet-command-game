using System.Text.Json;
using FleetCommand.Core;
internal static class ForceScenarios
{
    public static void Run(string root)
    {
        int count = 0; var traces = new List<object>();
        void Case(string name, Action<ContentCatalog, ForcePlan> action)
        {
            var c = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(c);
            try { action(c, ForceBattle.PracticeForce("kushan")); }
            catch (Exception e) { throw new Exception("FORCE " + name + ": " + e.Message, e); }
            traces.Add(new { name, passed = true }); count++; Console.WriteLine("FORCE PASS " + name);
        }
        void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
        void Invalid(ContentCatalog c, ForcePlan p) => Check(!ForceBuilder.Validate(c, p).Valid, "Invalid force accepted");
        void Reject(Action action) { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Expected rejection"); }
        Case("budget-includes-reserve-and-leaders", (c, p) => { var r = ForceBuilder.Validate(c, p); Check(r.Valid && r.Spent == 12 && r.Remaining == 0, string.Join("; ", r.Errors)); Invalid(c, p with { Budget = 11 }); });
        Case("free-grey-unit-is-derived-and-not-charged", (c, p) => { var r = ForceBuilder.Validate(c, p); Check(r.Units.Length == 3 && r.Units.Single(u => u.Free).Entry is { Id: "u1~assigned", Parent: "u1", Placement: "bay" } && r.Slots.Single(s => s.Tab.GrantedUnit != "").Unit == "u1~assigned", "Missing assigned unit"); });
        Case("wrong-faction-and-unpriced-ship", (c, p) => { Invalid(c, p with { Faction = "taiidan" }); Invalid(c, p with { Units = [new("u1", "core:kushan-interceptor")] }); });
        Case("leader-must-be-assigned-compatible", (c, p) => { Invalid(c, p with { Leaders = [new(p.Leaders[0].Leader)] }); Invalid(c, p with { Leaders = [new(p.Leaders[0].Leader, "u2")] }); Invalid(c, p with { Leaders = [new("practice:taiidan-admiral", "u1")] }); });
        Case("required-leader-slot-is-bound-to-assignment", (c, p) =>
        {
            c.Leaders[p.Leaders[0].Leader] = c.Leaders[p.Leaders[0].Leader] with { Classes = ["capital", "fighter"] };
            Invalid(c, p with { Leaders = [new(p.Leaders[0].Leader, "u2")] });
        });
        Case("one-unit-cannot-fill-two-required-tabs", (c, p) => { var l = c.Leaders[p.Leaders[0].Leader]; c.Leaders[l.Id] = l with { Tabs = [new("a", "yellow", true), new("b", "yellow", true), new("c", "blue")] }; Invalid(c, p); });
        Case("matching-reassigns-broad-slot-for-specialized-one", (c, p) =>
        {
            var l = c.Leaders[p.Leaders[0].Leader]; c.Leaders[l.Id] = l with { Tabs = [new("broad", "yellow", true), new("capital", "yellow", true, ["capital"]), new("wing", "blue")] };
            c.Leaders["test:frigate"] = new("test:frigate", "Test", "kushan", ["frigate"], Tabs: []);
            // Required tabs on one leader can only use that leader's unit: use two leaders to make a nontrivial matching.
            c.Leaders[l.Id] = c.Leaders[l.Id] with { Tabs = [new("broad", "yellow"), new("capital", "yellow", true, ["capital"]), new("wing", "blue")] };
            var r = ForceBuilder.Validate(c, p with { Budget = 15, Units = p.Units.Append(new("u3", "practice:kushan-reserve-frigate", "reserve")).ToArray() });
            Check(r.Valid && r.Slots.Single(s => s.Tab.Id == "capital").Unit == "u1" && r.Slots.Single(s => s.Tab.Id == "broad").Unit == "u3", string.Join("; ", r.Errors));
        });
        Case("class-and-color-restrict-purchases", (c, p) => { var l = c.Leaders[p.Leaders[0].Leader]; c.Leaders[l.Id] = l with { Tabs = [new("capital", "yellow", true, ["capital"]), new("escort", "blue", Classes: ["corvette"])] }; Invalid(c, p); });
        Case("carrier-tabs-require-that-carriers-bay", (c, p) =>
        {
            var l = c.Leaders[p.Leaders[0].Leader]; c.Leaders[l.Id] = l with { Tabs = [new("capital", "yellow", true, ["capital"])] };
            Invalid(c, p); var embarked = p with { Units = [p.Units[0], p.Units[1] with { Placement = "bay", Parent = "u1" }] }; Check(ForceBuilder.Validate(c, embarked).Valid, "Carrier blue tab did not accept embarked craft");
        });
        Case("grey-slot-cannot-be-reused", (c, p) => { var carrier = c.Ships[p.Units[0].Ship]; c.Ships[carrier.Id] = carrier with { Tabs = [carrier.Tabs![0]] }; var l = c.Leaders[p.Leaders[0].Leader]; c.Leaders[l.Id] = l with { Tabs = [new("capital", "yellow", true)] }; Invalid(c, p with { Units = [p.Units[0], p.Units[1] with { Placement = "bay", Parent = "u1" }] }); });
        Case("bay-capacity-includes-free-craft", (c, p) => { c.Ships[p.Units[0].Ship] = c.Ships[p.Units[0].Ship] with { Bays = 1 }; Invalid(c, p with { Units = [p.Units[0], p.Units[1] with { Placement = "bay", Parent = "u1" }] }); });
        Case("invalid-placement-nested-bays-and-duplicate-cells", (c, p) => { Invalid(c, p with { Units = [p.Units[0], p.Units[1] with { Column = 0 }] }); Invalid(c, p with { Units = [p.Units[0], p.Units[1] with { Column = 2 }] }); Invalid(c, p with { Units = [p.Units[0], p.Units[1] with { Placement = "reserve" }] }); Invalid(c, p with { Units = [p.Units[0], p.Units[1] with { Placement = "bay", Parent = "u2" }] }); });
        Case("tab-exemption-and-free-composition", (c, p) => { var noLeader = p with { Leaders = [], ReserveStars = 0, CheckTabs = false }; Check(ForceBuilder.Validate(c, noLeader).Valid, "Free composition rejected"); Invalid(c, noLeader with { Budget = 1 }); Invalid(c, noLeader with { CheckTabs = true }); c.Ships[p.Units[0].Ship] = c.Ships[p.Units[0].Ship] with { TabExempt = true }; Check(ForceBuilder.Validate(c, noLeader with { CheckTabs = true, Units = [p.Units[0], p.Units[1] with { Placement = "bay", Parent = "u1" }] }).Valid, "Exemption failed"); });
        Case("scenario-restrictions", (c, p) => { Check(!ForceBuilder.Validate(c, p, new(Budget: 10)).Valid && !ForceBuilder.Validate(c, p, new(ReserveStars: 0)).Valid && !ForceBuilder.Validate(c, p, new(Ships: [p.Units[0].Ship])).Valid && !ForceBuilder.Validate(c, p, new(Leaders: [])).Valid, "Scenario limit ignored"); });
        Case("multiple-leaders-stack-and-survive-roundtrip", (c, p) =>
        {
            var officer = c.Leaders["practice:kushan-officer"]; c.Leaders[officer.Id] = officer with { Hand = 1, Play = 1 };
            p = p with { Budget = 13, Leaders = p.Leaders.Append(new(officer.Id, "u1")).ToArray() };
            ForceBattle.Register(c, new([p, ForceBattle.PracticeForce("taiidan")])); var e = new BattleEngine(c); var s = e.Create(ForceBattle.MapId);
            Check(s.Cards!.HandLimit0 == 4 && s.Cards.PlayLimit0 == 3 && FleetRuleTools.LeaderIds(s.Units[0]).Length == 2, "Leader bonuses did not stack");
            Check(e.Save(e.Load(e.Save(s))) == e.Save(s), "Multiple leaders lost on resume");
            var damaged = s with { Units = s.Units.Select(u => u.Id == s.Units[0].Id ? u with { FocusFire = 2 } : u).ToList() };
            uint expected = damaged.RandomState; for (int i = 0; i < 3; i++) FleetRuleTools.Roll(ref expected);
            var repaired = FleetRuleTools.DamageControl(c, damaged, s.Units[0].Id, "ask", out _); Check(repaired.RandomState == expected, "Repair bonuses did not stack");
        });
        Case("transferring-primary-leader-retains-other-assignments", (c, p) =>
        {
            c.Leaders["test:resident"] = new("test:resident", "Resident", "kushan", ["capital"], Hand: 1);
            p = p with { Budget = 20, ReserveStars = 0, CheckTabs = false, Units = [p.Units[0], p.Units[1] with { Ship = p.Units[0].Ship }], Leaders = [p.Leaders[0], new("practice:kushan-officer", "u1"), new("test:resident", "u2")] };
            ForceBattle.Register(c, new([p, ForceBattle.PracticeForce("taiidan")], Objects: 0)); var e = new BattleEngine(c); var s = e.Create(ForceBattle.MapId);
            s = s with { ActiveSide = 0, Cards = s.Cards! with { Phase = "orders", Hand0 = ["order-01"], Hand1 = ["order-21"], Deck = Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(["order-01", "order-21"]).ToArray(), Discard = [] } };
            string snapshot = e.Save(s); var a = e.Apply(s, 0, new("transfer-crew", "s0-0", "s0-2", Card: "order-01", TransferLeader: true)); var b = e.Apply(e.Load(snapshot), 0, new("transfer-crew", "s0-0", "s0-2", Card: "order-01", TransferLeader: true));
            Check(a.Accepted && e.Save(s) == snapshot && e.Save(a.State) == e.Save(b.State), "Transfer rejected, mutated input or diverged"); s = e.Load(e.Save(a.State));
            Check(FleetRuleTools.LeaderIds(s.Units.Single(u => u.Id == "s0-0")).SequenceEqual(["practice:kushan-officer"]) && FleetRuleTools.LeaderIds(s.Units.Single(u => u.Id == "s0-2")).SequenceEqual(["test:resident", "practice:kushan-admiral"]), "Transfer erased resident or remaining leaders");
            Check(s.Orders0 == 1 && s.Cards!.PlayLimit0 == 2, "Transfer changed frozen Play budget or spent twice");
        });
        Case("attack-bonuses-sum-on-the-same-unit", (c, p) =>
        {
            var admiral = c.Leaders[p.Leaders[0].Leader]; c.Leaders[admiral.Id] = admiral with { AttackDice = 1 };
            c.Ships[p.Units[0].Ship] = c.Ships[p.Units[0].Ship] with { Ram = 3 };
            p = p with { Budget = 13, Leaders = p.Leaders.Append(new("practice:kushan-officer", "u1")).ToArray() }; ForceBattle.Register(c, new([p, ForceBattle.PracticeForce("taiidan")])); var s = new BattleEngine(c).Create(ForceBattle.MapId);
            Check(FleetCombat.AttackDice(c, s, new("attack", "s0-0", "s1-3", AttackKind: "ram")) == 5, "Only one leader contributed attack dice");
        });
        Case("duplicate-leader-and-duplicate-unit-rejected", (c, p) => { Invalid(c, p with { Leaders = [p.Leaders[0], p.Leaders[0]] }); Invalid(c, p with { Units = [p.Units[0], p.Units[0]] }); Invalid(c, p with { Leaders = [new("practice:kushan-pilot", "u1~assigned"), new("practice:kushan-pilot", "u2")] }); });
        Case("leader-on-free-craft-cannot-satisfy-purchase-tab", (c, p) => { Invalid(c, p with { Budget = 13, Leaders = p.Leaders.Append(new("practice:kushan-pilot", "u1~assigned")).ToArray() }); });
        Case("definition-validation", (c, p) => { var carrier = c.Ships[p.Units[0].Ship]; c.Ships[carrier.Id] = carrier with { Tabs = [new("bad", "yellow")] }; Reject(c.Validate); c.Ships[carrier.Id] = carrier with { Tabs = [new("bad", "blue", GrantedUnit: carrier.Id)] }; Reject(c.Validate); });
        Case("deterministic-two-sided-map-and-embarked-indices", (c, p) =>
        {
            var setup = new PreparedBattle([p, ForceBattle.PracticeForce("taiidan")]); string original = JsonSerializer.Serialize(p, JsonData.Options);
            ForceBattle.Register(c, setup); var first = JsonSerializer.Serialize(c.Maps[ForceBattle.MapId], JsonData.Options); ForceBattle.Register(c, setup);
            Check(first == JsonSerializer.Serialize(c.Maps[ForceBattle.MapId], JsonData.Options), "Generator changed");
            var e = new BattleEngine(c); var s = e.Create(ForceBattle.MapId, 17); e.Load(e.Save(s));
            Check(s.Units.Count == 4 && s.Units.Where(u => u.Side == 0).All(u => u.Q < 2) && s.Units.Where(u => u.Side == 1).All(u => u.Q >= 10) && s.Reserves!.Count == 2 && s.Reserves.Single(u => u.Side == 1).Embarked == "s1-3" && s.Funds0 == 3, "Placement mapping incorrect");
            Check(c.Maps[ForceBattle.MapId].Objects.All(o => o.Q >= 2 && o.Q < 10) && original == JsonSerializer.Serialize(p, JsonData.Options), "Objects overlap deployment or input changed");
        });
        Case("reserve-carrier-keeps-its-air-wing", (c, p) => { p = p with { Units = [p.Units[0] with { Placement = "reserve" }, p.Units[1]] }; ForceBattle.Register(c, new([p, ForceBattle.PracticeForce("taiidan")])); var e = new BattleEngine(c); var s = e.Create(ForceBattle.MapId); e.Load(e.Save(s)); Check(s.Reserves!.Any(u => u.Embarked == "s0-0") && s.Reserves!.Any(u => u.Id == "s0-0" && u.Embarked == ""), "Reserve carrier lost child"); });
        Case("rejected-preparation-does-not-mutate-catalog", (c, p) => { int maps = c.Maps.Count, fleets = c.Fleets.Count; Reject(() => ForceBattle.Register(c, new([p, ForceBattle.PracticeForce("taiidan") with { Budget = 1 }]))); Check(c.Maps.Count == maps && c.Fleets.Count == fleets && !c.Maps.ContainsKey(ForceBattle.MapId), "Rejected preparation partially registered"); Reject(() => ForceBattle.Register(c, new([p, p], Objects: 1000))); });
        Case("out-of-map-and-malformed-rosters", (c, p) => { Reject(() => ForceBattle.Register(c, new([p with { Units = [p.Units[0] with { Row = 8 }, p.Units[1]] }, ForceBattle.PracticeForce("taiidan")]))); Invalid(c, p with { Id = "../../bad" }); Invalid(c, p with { Units = [] }); Invalid(c, p with { Units = [new("u1", "missing")] }); });
        Case("personal-library-roundtrip-and-path-safety", (c, p) => { string dir = Path.Combine(root, "tmp", "test-results", "force-library"); ForceLibrary.Save(dir, p); ForceLibrary.Save(dir, p with { Name = "Renamed draft", Budget = 1 }); var read = ForceLibrary.Read(dir).Single(x => x.Id == p.Id); Check(read.Name == "Renamed draft" && !ForceBuilder.Validate(c, read).Valid, "Draft failed to roundtrip"); Reject(() => ForceLibrary.Save(dir, p with { Id = "../escape" })); });
        Case("malformed-fields-rejected-without-crashing", (c, p) => { Invalid(c, p with { Units = [new("u1", null!)] }); Invalid(c, p with { Leaders = [new(null!)] }); string dir = Path.Combine(root, "tmp", "test-results", "bad-force-library"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "bad.json"), "{\"id\":\"bad\",\"units\":null}"); Reject(() => ForceLibrary.Read(dir)); });
        Case("fresh-catalog-restores-setup-and-private-state", (c, p) =>
        {
            var setup = new PreparedBattle([p, ForceBattle.PracticeForce("taiidan")]); ForceBattle.Register(c, setup); var e = new BattleEngine(c); var s = e.Create(ForceBattle.MapId);
            var fresh = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(fresh); ForceBattle.Register(fresh, JsonSerializer.Deserialize<PreparedBattle>(JsonSerializer.Serialize(setup, JsonData.Options), JsonData.Options)!); var restored = new BattleEngine(fresh);
            Check(restored.Save(restored.Load(e.Save(s))) == e.Save(s), "Fresh process restore diverged");
            for (int i = 0; i < 2; i++) { var view = restored.Load(e.Save(BasicCardRules.View(s, i))); Check(view.Cards!.Deck.Length == 0 && (i == 0 ? view.Cards.Hand1 : view.Cards.Hand0).Length == 0 && !restored.Apply(view, i, new("initiative")).Accepted, "Private snapshot leaked authority"); }
        });
        File.WriteAllText(Path.Combine(root, "tmp", "test-results", "forces.json"), JsonSerializer.Serialize(traces, JsonData.Options));
        Console.WriteLine($"FORCES_OK {count} scenarios");
    }
}
