using System.Text.Json;
using FleetCommand.Core;
internal static class ObjectiveScenarios
{
    private const string Actor = "s0-0", Enemy = "s1-3", Station = "s1-6";
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly List<object> _trace = []; private readonly string _path;
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C);
            C.Ships["practice:kushan-boarder"] = C.Ships["practice:kushan-boarder"] with { Success = 2, Armor = 20, Crew = 6, Weapons = [new("gun", "Gun", 3, 3)] };
            C.Ships["practice:taiidan-boarder"] = C.Ships["practice:taiidan-boarder"] with { Success = 2, Armor = 0, Crew = 6, Weapons = [new("gun", "Gun", 3, 3)] };
            C.Ships["practice:station"] = C.Ships["practice:station"] with { Armor = 0 };
            S = E.Create(BasicCardBattle.ObjectivesMapId, 1);
            string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
            S = S with { ActiveSide = 0, ValorOwner = 0, RandomState = 1, Units = S.Units.Select(u => u.Id == Actor ? u with { Q = 2, R = 3 } : u.Id == Station ? u with { Q = 3, R = 3 } : u).ToList(),
                Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"), Deposits = new() { [0] = new(2), [1] = new(3), [2] = new(0) } };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json"); Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        }
        public void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
        public void Goals(params ObjectiveDefinition[] goals) { C.Maps[S.Map] = C.Maps[S.Map] with { Objectives = goals }; S = S with { Claims = [], Medals0 = 0, Medals1 = 0 }; }
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, "Objective command: " + a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Objective input mutated or replay diverged");
            S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, viewer))); Check(view.Medals0 == S.Medals0 && view.Medals1 == S.Medals1 && view.RandomState == 1 && !E.Apply(view, viewer, cmd).Accepted, "Public score or private authority was not preserved"); }
        }
        public void Attack(int side, string actor, string target, string card, string kind = "ranged")
        {
            Do(side, new("attack", actor, target, Card: card, AttackKind: kind)); if (S.Cards!.Response != null && S.Defense == null && S.Combat == null) Do(1 - side, new("decline"));
        }
        public void Capture(int side, string actor, string card) { Attack(side, actor, Station, card, "board"); Check(S.Combat?.Kind == "board", "Expected a saved boarding result"); Do(side, new("capture-result")); }
        public void TimeLimit(int stars0, int stars1, bool medal0, bool medal1)
        {
            Goals(new ObjectiveDefinition("a", "Stars 0", 0, "destroy-stars", 1, 1), new("b", "Stars 1", 1, "destroy-stars", 1, 1));
            if (!medal0) C.Maps[S.Map] = C.Maps[S.Map] with { Objectives = C.Maps[S.Map].Objectives!.Select(o => o.Side == 0 ? o with { Threshold = 1000 } : o).ToArray() };
            if (!medal1) C.Maps[S.Map] = C.Maps[S.Map] with { Objectives = C.Maps[S.Map].Objectives!.Select(o => o.Side == 1 ? o with { Threshold = 1000 } : o).ToArray() };
            C.Rules[C.Maps[S.Map].Rules] = C.Rules[C.Maps[S.Map].Rules] with { RoundLimit = 1 };
            S = S with { Stars0 = stars0, Stars1 = stars1, Claims = (medal0 ? new[] { "a" } : []).Concat(medal1 ? ["b"] : []).ToArray(), Medals0 = medal0 ? 1 : 0, Medals1 = medal1 ? 1 : 0 };
            Do(0, new("pass")); Do(1, new("pass"));
        }
    }
    public static void Run(string root)
    {
        void Case(string name, Action<Session> test) { test(new(root, name)); Console.WriteLine("OBJECTIVE SCENARIO PASS " + name); }
        Case("objective-boarding-reaches-three-and-closes-every-window", s =>
        {
            s.Goals(new ObjectiveDefinition("station", "Capture", 0, "capture", 3, Target: Station)); s.C.Validate(); s.Capture(0, Actor, "order-01");
            s.Check(s.S is { Finished: true, Winner: 0, Medals0: 3, EndReason: "medals", Combat: null, Hazard: null, Overrun: null } && s.S.Cards!.Pending == null && s.S.Cards.Response == null && s.S.Stars0 == 0 && s.S.Units.Single(u => u.Id == Station).Side == 0, "Capture did not grant immediate medal victory without destruction score");
            s.Do(1, new("pass"), false);
        });
        Case("objective-starting-control-does-not-count-as-capture", s =>
        {
            s.Goals(new ObjectiveDefinition("station", "Capture", 1, "capture", 2, Target: Station)); s.Do(0, new("pass")); s.Check(s.S.Medals1 == 0 && s.S.Claims!.Length == 0, "Initial ownership was treated as a successful boarding action");
        });
        Case("objective-recapture-transfers-reversible-medals-without-farming", s =>
        {
            s.Goals(new ObjectiveDefinition("a", "Capture A", 0, "capture", 2, Target: Station, Reversible: true), new("b", "Capture B", 1, "capture", 2, Target: Station, Reversible: true));
            s.Capture(0, Actor, "order-01"); s.Check(s.S.Medals0 == 2, "First capture was not scored");
            s.C.Ships["practice:taiidan-boarder"] = s.C.Ships["practice:taiidan-boarder"] with { Armor = 20 };
            s.S = s.S with { ValorOwner = 1, RandomState = 1, Units = s.S.Units.Select(u => u.Id == Enemy ? u with { Q = 4, R = 3 } : u).ToList() };
            s.Capture(1, Enemy, "order-21"); s.Check(s.S.Medals0 == 0 && s.S.Medals1 == 2, "Recapture did not remove the reversible medals");
            s.S = s.S with { ActiveSide = 0, Orders0 = 0, Orders1 = 0, ValorOwner = 0, RandomState = 1, Cards = s.S.Cards! with { Phase = "orders" }, Units = s.S.Units.Select(u => u with { Activated = false }).ToList() };
            s.Capture(0, Actor, "order-09"); s.Check(s.S.Medals0 == 2 && s.S.Medals1 == 0 && s.S.Claims!.Length == 2, "Repeated capture farmed the same objective");
        });
        Case("objective-permanent-capture-medals-survive-recapture", s =>
        {
            s.Goals(new ObjectiveDefinition("a", "Capture", 0, "capture", 2, Target: Station)); s.Capture(0, Actor, "order-01");
            s.S = s.S with { ValorOwner = 1, RandomState = 1, Units = s.S.Units.Select(u => u.Id == Enemy ? u with { Q = 4, R = 3 } : u).ToList() };
            s.Capture(1, Enemy, "order-21"); s.Check(s.S.Medals0 == 2, "A permanent objective was revoked without a scenario rule");
        });
        Case("objective-harvest-wins-with-units-still-alive-and-no-kill-stars", s =>
        {
            s.S = s.S with { Captures = new() { [Station] = 0 }, Claims = ["station"], Medals0 = 2 };
            s.Do(0, new("begin-order", "s0-1", "harvest", Card: "order-01")); s.Do(0, new("skip")); s.Do(0, new("harvest", "s0-1", Object: 0));
            s.Check(s.S.Finished && s.S.Winner == 0 && s.S.Medals0 == 3 && s.S.Stars0 == 0 && s.S.Units.Any(u => u.Side == 1) && s.S.Units.Single(u => u.Id == "s0-1").Cargo == 2, "Harvest failed to award a scenario medal independently of elimination/kill stars");
        });
        Case("objective-destroy-unit-and-stars-stack-on-the-same-kill", s =>
        {
            s.Goals(new ObjectiveDefinition("kill", "Target", 0, "destroy-unit", 2, Target: Enemy), new("stars", "Stars", 0, "destroy-stars", 1, 3));
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Enemy ? u with { Q = 3, R = 2 } : u).ToList() };
            s.Attack(0, Actor, Enemy, "order-01"); s.Do(0, new("resolve"));
            s.Check(s.S.Finished && s.S.Medals0 == 3 && s.S.Losses![Enemy] is { Stars: 3, Credit: 0 } && s.S.Claims!.Length == 2, "Specific-target and star objectives were not scored together");
        });
        Case("objective-class-filter-uses-loss-records-not-all-stars", s =>
        {
            s.Goals(new ObjectiveDefinition("fighters", "Fighters", 0, "destroy-stars", 1, 2, Classes: ["fighter"]));
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Enemy ? u with { Q = 3, R = 2 } : u).ToList() };
            s.Attack(0, Actor, Enemy, "order-01"); s.Do(0, new("resolve"));
            s.Check(s.S.Stars0 == 3 && FleetObjectives.Progress(s.C, s.S, s.C.Maps[s.S.Map].Objectives![0]) == 0 && s.S.Medals0 == 0, "A frigate contributed to a fighter-only objective");
        });
        Case("objective-medal-priority-at-round-limit", s => { s.TimeLimit(1, 100, true, false); s.Check(s.S.Finished && s.S.Winner == 0 && s.S.EndReason == "round-limit", "Kill stars outranked medals"); });
        Case("objective-star-tiebreak-at-round-limit", s => { s.TimeLimit(6, 5, true, true); s.Check(s.S.Winner == 0, "Equal medals did not use destroyed stars"); });
        Case("objective-full-tie-remains-a-draw", s => { s.TimeLimit(5, 5, true, true); s.Check(s.S.Finished && s.S.Winner == null, "Equal medals and stars did not draw"); });
        Case("objective-no-prototype-elimination-before-medals-or-time", s =>
        {
            s.Goals(new ObjectiveDefinition("a", "Stars", 0, "destroy-stars", 1, 100)); s.S = s.S with { Units = [s.S.Units.Single(u => u.Id == Actor), s.S.Units.Single(u => u.Id == Enemy) with { Q = 3, R = 3 }] };
            s.Attack(0, Actor, Enemy, "order-01"); s.Do(0, new("resolve")); s.Check(!s.S.Finished && !s.S.Units.Any(u => u.Side == 1), "Scenario was ended by prototype elimination");
        });
        Case("objective-defense-victory-stops-the-original-incoming-attack", s =>
        {
            s.Goals(new ObjectiveDefinition("a", "Defense", 0, "destroy-stars", 3, 2));
            s.C.Ships["practice:kushan-boarder"] = s.C.Ships["practice:kushan-boarder"] with { Weapons = [new("guard", "Guard", 8, 2, DefensiveFire: true)] };
            s.C.Ships["practice:taiidan-wing"] = s.C.Ships["practice:taiidan-wing"] with { Success = 2, Weapons = [new("gun", "Gun", 8, 2, Kind: "fighter")] };
            s.S = s.S with { ActiveSide = 1, FactionUsed1 = true, Units = s.S.Units.Select(u => u.Id == "s1-5" ? u with { Q = 3, R = 2, Models = 1 } : u).ToList() };
            s.Attack(1, "s1-5", Actor, "order-21"); s.Do(0, new("defend", Weapon: "guard")); s.Do(0, new("resolve"));
            s.Check(s.S.Finished && s.S.Winner == 0 && s.S.Defense == null && s.S.Combat == null && s.S.Units.Single(u => u.Id == Actor) is { Models: 1, FocusFire: 0 }, "Defense medal victory still allowed the incoming attack to hit");
        });
        Case("objective-cap-simultaneous-third-medals-resolve-both-volleys", s =>
        {
            s.Goals(new ObjectiveDefinition("a", "Stars 0", 0, "destroy-stars", 3, 2), new("b", "Stars 1", 1, "destroy-stars", 3, 2));
            s.C.Ships["practice:kushan-wing"] = s.C.Ships["practice:kushan-wing"] with { Success = 2 }; s.C.Ships["practice:taiidan-wing"] = s.C.Ships["practice:taiidan-wing"] with { Success = 2 };
            s.S = s.S with { ActiveSide = 1, FactionUsed1 = true, Units = s.S.Units.Select(u => u.Id == "s0-2" ? u with { Q = 2, R = 3, Models = 1, Patrol = Actor } : u.Id == "s1-5" ? u with { Q = 3, R = 2, Models = 1 } : u).ToList() };
            s.Attack(1, "s1-5", Actor, "order-21"); s.Do(0, new("cap", Card: "order-09", Weapon: "gun")); s.Do(0, new("resolve"));
            s.Check(s.S.Finished && s.S.Winner == null && s.S.Medals0 == 3 && s.S.Medals1 == 3 && !s.S.Units.Any(u => u.Id is "s0-2" or "s1-5") && s.S.Units.Single(u => u.Id == Actor).FocusFire == 0, "Simultaneous CAP medals favored the first processed volley");
        });
        Case("objective-campaign-records-real-medals-and-rejects-duplicate-result", s =>
        {
            s.TimeLimit(1, 100, true, false); var campaign = Campaign.Start(s.C, "practice:objectives", ["host", "friend"]);
            bool viewRejected = false; try { Campaign.CompleteMission(s.C, campaign, BasicCardRules.View(s.S, 0)); } catch (InvalidOperationException) { viewRejected = true; } s.Check(viewRejected, "Client view advanced authoritative campaign progress");
            var first = Campaign.CompleteMission(s.C, campaign, s.S); bool reused = false; try { Campaign.CompleteMission(s.C, first, s.S); } catch (InvalidOperationException) { reused = true; } s.Check(reused, "Same battle was reused for the next mission on the same map"); s.Check(first.CurrentMission == "second" && first.Medals == 1 && first.OpponentMedals == 0 && first.Results!.Length == 1 && first.Participants!.Length == 2, "Campaign did not preserve scenario medals/participants");
            var loop = s.C.Campaigns["practice:objectives"]; s.C.Campaigns[loop.Id] = loop with { Missions = loop.Missions.Select(m => m.Id == "first" ? m with { OnVictory = "first" } : m).ToArray() };
            var repeated = Campaign.CompleteMission(s.C, campaign, s.S); bool rejected = false; try { Campaign.CompleteMission(s.C, repeated, s.S); } catch (InvalidOperationException) { rejected = true; } s.Check(rejected, "Same finished result could award medals repeatedly");
        });
        Case("objective-losing-campaign-retains-earned-medals-and-draw-branch", s =>
        {
            s.TimeLimit(4, 5, true, true); var campaign = Campaign.Start(s.C, "practice:objectives"); var loss = Campaign.CompleteMission(s.C, campaign, s.S);
            s.Check(loss.Medals == 1 && loss.OpponentMedals == 1 && loss.Completed && loss.Results![0].Winner == 1, "Defeat discarded earned scenario medals");
            var def = s.C.Campaigns["practice:objectives"]; s.C.Campaigns[def.Id] = def with { Missions = def.Missions.Select(m => m.Id == "first" ? m with { OnDraw = "second" } : m).ToArray() };
            s.S = s.S with { Stars1 = 4, Winner = null }; var draw = Campaign.CompleteMission(s.C, campaign, s.S); s.Check(draw.CurrentMission == "second" && !draw.Completed, "Explicit campaign draw transition was ignored");
        });
        Case("objective-owned-mines-win-without-leaving-a-hazard-window", s =>
        {
            s.Goals(new ObjectiveDefinition("a", "Mine defense", 1, "destroy-stars", 3, 3));
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("practice:mines", 2, 3, 1)], AsteroidStars = null };
            s.C.Ships["practice:kushan-boarder"] = s.C.Ships["practice:kushan-boarder"] with { Armor = 0, Evasive = 1, Weapons = [] };
            s.S = s.S with { Deposits = null }; s.Do(0, new("begin-order", Actor, "move", Card: "order-01")); s.Do(0, new("ability-reroll", Dice: []));
            s.Check(s.S.Finished && s.S.Winner == 1 && s.S.Medals1 == 3 && s.S.Hazard == null && s.S.Cards!.Pending == null, "Owned mine victory left a pending terrain/order frame");
        });
        Case("objective-neutral-terrain-awards-enemy-medals-for-any-unit-loss", s =>
        {
            s.Goals(new ObjectiveDefinition("a", "Enemy losses", 1, "destroy-stars", 3, 3));
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("core:asteroids", 2, 3)], AsteroidStars = null };
            s.C.Ships["practice:kushan-boarder"] = s.C.Ships["practice:kushan-boarder"] with { Armor = 0, Evasive = 1, Weapons = [] };
            s.S = s.S with { Deposits = null, RandomState = 3 }; s.Do(0, new("begin-order", Actor, "move", Card: "order-01")); s.Do(0, new("ability-reroll", Dice: []));
            s.Check(s.S.Finished && s.S.Winner == 1 && s.S.Stars1 == 3 && s.S.Losses![Actor].Credit == 1, "Destroyed through neutral terrain did not count as an enemy loss");
        });
        Case("objective-third-medal-from-ram-stops-return-collision", s =>
        {
            s.Goals(new ObjectiveDefinition("a", "Station destroyed", 0, "destroy-unit", 3, Target: Station));
            s.C.Ships["practice:kushan-boarder"] = s.C.Ships["practice:kushan-boarder"] with { Ram = 6, Armor = 0 };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Actor ? u with { Facing = 0 } : u).ToList() };
            s.Attack(0, Actor, Station, "order-01", "ram"); s.Do(0, new("resolve"));
            s.Check(s.S.Finished && s.S.Winner == 0 && s.S.Combat == null && s.S.Units.Single(u => u.Id == Actor).Models == 1, "Return collision continued after immediate medal victory");
        });
        Case("objective-malformed-score-and-result-are-rejected", s =>
        {
            foreach (var bad in new[] { s.S with { Medals0 = 3 }, s.S with { Claims = ["missing"] }, s.S with { Finished = true, EndReason = "round-limit" }, s.S with { Losses = new() { [Actor] = new("practice:kushan-boarder", 1, 3) } } })
            { bool rejected = false; try { s.E.Load(s.E.Save(bad)); } catch (InvalidDataException) { rejected = true; } s.Check(rejected, "Malformed objective state was accepted"); }
        });
    }
}
