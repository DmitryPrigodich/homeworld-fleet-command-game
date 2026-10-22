using System.Text.Json;
using FleetCommand.Core;
using FleetCommand.Import;

var root = Path.GetFullPath(args.Length > 1 ? args[1] : ".");
if (args.FirstOrDefault() == "scan")
{
    foreach (var ship in HwrmImporter.Scan(root)) Console.WriteLine($"{ship.Id} | {Path.GetFileName(ship.Archive)} | {ship.Entry.Size:N0} bytes");
    return;
}
if (args.FirstOrDefault() == "import")
{
    var ships = HwrmImporter.Scan(root);
    string cache = Path.GetFullPath(args[2]);
    foreach (var id in args.Skip(3))
    {
        var receipt = HwrmImporter.Import(ships.Single(s => s.Id == id), cache);
        Console.WriteLine($"Imported {receipt.AssetId}: {receipt.SourceSha256}");
    }
    return;
}

var content = ContentCatalog.Load([Path.Combine(root, "content", "base")]);
int checks = 0;
void Check(bool value, string description) { if (!value) throw new Exception("FAIL: " + description); Console.WriteLine("PASS: " + description); checks++; }
void Reject(Action action, string description)
{
    try { action(); } catch (Exception e) when (e is InvalidDataException or InvalidOperationException or JsonException)
    { Check(true, description); return; }
    throw new Exception("FAIL: " + description);
}
var engine = new BattleEngine(content);
ActionPreviewChecks.Run(content, Check);
Check(!FleetPresentation.IsPlayerColor("#87929c") && !FleetPresentation.IsPlayerColor("#111111") && !FleetPresentation.IsPlayerColor("#abcdef00"), "neutral, dark and transparent player colors are rejected");
Check(FleetPresentation.PlayerColors.All(FleetPresentation.IsPlayerColor), "all suggested player colors are visible and chromatic");
var identity = FleetPresentation.Normalize(new("\nCommander", "#87929c", "../../foreign-file"));
Check(identity.Name == "Commander" && identity.Color == FleetPresentation.PlayerColors[0] && identity.Emblem == "auto", "untrusted profile fields are normalized without allowing file paths");
Check(FleetPresentation.DistinctColor("#39a4c5", []) == "#39a4c5"
    && FleetPresentation.DistinctColor("#39a4c5", ["#39a4c5"]) != "#39a4c5", "player colors survive sync and collisions receive a distinct alternative");
Check(FleetPresentation.DefaultEmblem("kushan") == "kushan" && FleetPresentation.DefaultEmblem("taiidan") == "taiidan", "default faction emblems distinguish Kushan and Taiidan");
Check(FleetPresentation.ShipIcon(content.Ships["core:kushan-interceptor"]) == "triangle"
    && FleetPresentation.ShipIcon(content.Ships["core:taiidan-frigate"]) == "diamond", "legacy ship definitions resolve to Homeworld tactical classes");
Check(FleetPresentation.ShipIcon(content.Ships["core:kushan-interceptor"] with { TacticalIcon = "square" }) == "square", "mods can explicitly select a tactical symbol");
Check(FleetPresentation.ObjectEffects(content.Objects["core:planet"]).Contains("перекрывает огонь")
    && FleetPresentation.ObjectEffects(content.Objects["core:asteroids"]).Contains("2"), "object inspection explains movement and attack effects");
var state = engine.Create("core:training", 17);
var before = engine.Save(state);
var unit = state.Units.First(u => u.Side == 0);
Check(!engine.Apply(state, 1, new("move", unit.Id, Q: unit.Q + 1, R: unit.R)).Accepted, "wrong-side commands rejected");
Check(!engine.Apply(state, 0, new("move", unit.Id, Q: -1)).Accepted, "off-map movement rejected");
Check(before == engine.Save(state), "rejected commands do not mutate state");
var moved = engine.Apply(state, 0, new("move", unit.Id, Q: unit.Q + 1, R: unit.R));
Check(moved.Accepted && moved.State.Sequence == 1 && moved.State.ActiveSide == 1, "legal move advances authoritative sequence and turn");
var movementLog = moved.State.Log!;
Check(movementLog.Any(e => e.Text.Contains("перемещение")) && movementLog.Last().Side == 1
    && state.Log!.Count == 2, "accepted movement appends immutable action and turn log entries");
Check(!movementLog.Any(e => e.Text.Contains(unit.Id)) && FleetPresentation.UnitNumber(content, state, state.Units.First(u => u.Side == 1)) == 1,
    "player-facing names replace technical IDs and number each fleet separately");
Check(before == engine.Save(state), "accepted commands preserve prior snapshot");
Check(engine.Save(engine.Load(engine.Save(moved.State))) == engine.Save(moved.State), "save/load preserves exact state");
Reject(() => engine.Load(before.Replace(content.Fingerprint, "wrong")), "save with other mods rejected");
var modified = ContentCatalog.Load([Path.Combine(root, "mods", "example"), Path.Combine(root, "content", "base")]);
Check(modified.Mods[0].Id == "core" && modified.Themes.ContainsKey("example:amber"), "dependencies load in order and theme mods work");
Check(modified.Fingerprint != content.Fingerprint, "network fingerprint changes with mods");
var campaign = Campaign.Start(content, "core:patrol", ["commander-1", "commander-2"]);
Reject(() => Campaign.CompleteMission(content, campaign, state), "unfinished campaign battle cannot advance");
campaign = Campaign.CompleteMission(content, campaign, state with { Finished = true, Winner = 0 });
Check(campaign.CurrentMission == "second" && campaign.Medals == 1 && campaign.Participants!.Length == 2, "campaign advances and retains co-op participants");
var attackState = state with { Units = [unit with { Q = 2, R = 2 }, state.Units.First(u => u.Side == 1) with { Q = 3, R = 2 }] };
var attack = new BattleCommand("attack", attackState.Units[0].Id, attackState.Units[1].Id);
var a = engine.Apply(attackState, 0, attack);
var b = engine.Apply(engine.Load(engine.Save(attackState)), 0, attack);
Check(a.Accepted && engine.Save(a.State) == engine.Save(b.State), "random state survives saves and reproduces combat");
Check(a.State.Log!.Any(e => e.Text.Contains("Броски:") && e.Text.Contains("Потери цели:")), "attack log reports dice and actual casualties");
Check(FleetPresentation.UnitNumber(content, state with { Units = state.Units.Skip(1).ToList() }, state.Units[1]) == 2,
    "unit labels remain stable after earlier units are lost");
var legacyState = state with { Log = null };
Check(engine.Load(engine.Save(legacyState)).Log == null && engine.Apply(legacyState, 0, new("pass")).State.Log!.Count > 0,
    "old saves without history remain loadable and begin recording new events");
var overflowing = state with { Log = Enumerable.Range(0, BattleLog.Capacity).Select(i => new BattleLogEntry(i, 1, 0, "Prior event")).ToList() };
var boundedLog = engine.Apply(overflowing, 0, new("pass")).State.Log!;
Check(boundedLog.Count == BattleLog.Capacity && boundedLog.Last().Number > overflowing.Log!.Last().Number,
    "log retention is bounded and preserves event order");
var generated = new GenerationOptions(12, 8, 5, 7, 18, 85);
MapGenerator.Generate(content, generated);
var generatedMap = JsonSerializer.Serialize(content.Maps[MapGenerator.MapId], JsonData.Options);
var generatedUnits = JsonSerializer.Serialize(content.Fleets["generated:friendly"], JsonData.Options);
MapGenerator.Generate(content, generated);
Check(generatedMap == JsonSerializer.Serialize(content.Maps[MapGenerator.MapId], JsonData.Options)
    && generatedUnits == JsonSerializer.Serialize(content.Fleets["generated:friendly"], JsonData.Options), "seed reproduces terrain and force placement");
Check(content.Maps[MapGenerator.MapId].Objects.Length == 18 && content.Fleets["generated:enemy"].Units.Length == 7, "generator respects requested force and object counts");
Check(content.Fleets["generated:friendly"].Units.All(u => u.Q < 2) && content.Fleets["generated:enemy"].Units.All(u => u.Q >= 10), "generator keeps opposing deployment zones separate");
Reject(() => MapGenerator.Generate(content, generated with { Height = 4, FriendlyUnits = 30 }), "generator rejects impossible deployment density");
var generatedState = engine.Create(MapGenerator.MapId);
Check(engine.Load(engine.Save(generatedState)).Units.Count == 12, "generated battle can be saved and restored");
var trainingContent = content.CopyForRuntime();
TutorialLesson.Register(trainingContent);
trainingContent.Validate();
Check(!content.Maps.ContainsKey(TutorialLesson.MapId) && !content.Objects.ContainsKey("tutorial:obstacle"), "tutorial definitions are isolated from skirmish generation");
var trainingEngine = new BattleEngine(trainingContent);
var lesson = new TutorialLesson();
var training = trainingEngine.Create(TutorialLesson.MapId);
var trainingMove = new BattleCommand("move", TutorialLesson.ScoutId, Q: TutorialLesson.Destination.Q, R: TutorialLesson.Destination.R);
Check(!lesson.Allows(trainingMove), "tutorial blocks orders before explaining selection and movement");
lesson.Continue(); lesson.Continue(); lesson.Select("s0-1");
Check(lesson.Step == TutorialStep.Select, "tutorial requires selecting the demonstrated unit");
lesson.Select(TutorialLesson.ScoutId);
Check(lesson.Allows(trainingMove) && !lesson.Allows(new("move", TutorialLesson.ScoutId, Q: 0, R: 0)), "tutorial accepts only the highlighted destination");
var trainingMoved = trainingEngine.Apply(training, 0, trainingMove);
Check(trainingMoved.Accepted && trainingMoved.State.Units.Single(u => u.Id == TutorialLesson.ScoutId).Activated, "tutorial movement uses real activation rules");
lesson.Accepted(trainingMove);
training = trainingEngine.Apply(trainingMoved.State, 1, new("pass")).State;
var trainingPass = new BattleCommand("pass");
Check(lesson.Allows(trainingPass), "tutorial teaches ending a turn after moving");
training = trainingEngine.Apply(training, 0, trainingPass).State;
lesson.Accepted(trainingPass);
training = trainingEngine.Apply(training, 1, trainingPass).State;
Check(training.Round == 2 && training.ActiveSide == 0 && !training.Units.Single(u => u.Id == TutorialLesson.ScoutId).Activated,
    "passive tutorial opponent permits a new round with refreshed units");
Check(training.Log!.Any(e => e.Round == 2 && e.Text.Contains("Начался раунд")), "round transitions are recorded in the battle history");
var trainingShot = new BattleCommand("attack", TutorialLesson.ScoutId, TutorialLesson.TargetId);
var trainingFired = trainingEngine.Apply(training, 0, trainingShot);
Check(lesson.Allows(trainingShot) && trainingFired.Accepted, "tutorial target is in range after the demonstrated move");
lesson.Accepted(trainingShot);
Check(lesson.Step == TutorialStep.Complete && !lesson.Allows(trainingShot), "accepted shot completes tutorial regardless of random losses");
var temporary = Path.Combine(root, "tmp", "checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
File.WriteAllText(Path.Combine(temporary, "mod.json"), "{\"id\":\"broken\",\"version\":\"1.0.0\",\"requires\":[\"missing\"]}");
File.WriteAllText(Path.Combine(temporary, "content.json"), "{}");
Reject(() => ContentCatalog.Load([temporary]), "missing dependencies rejected");
File.WriteAllText(Path.Combine(temporary, "mod.json"), "{\"id\":\"core\",\"version\":\"1.0.0\"}");
File.WriteAllText(Path.Combine(temporary, "content.json"), "{\"typo\":[]}");
Reject(() => ContentCatalog.Load([temporary]), "unknown JSON properties rejected");
File.WriteAllBytes(Path.Combine(temporary, "invalid.big"), new byte[180]);
Reject(() => { using var archive = new BigArchive(Path.Combine(temporary, "invalid.big")); }, "unsupported archives rejected");
Console.WriteLine($"{checks} checks passed.");
