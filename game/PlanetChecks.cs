using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private int _planetNetStep, _planetNetSequence = -1;
    private const string PlanetHero = "core:hero-taiidan-battery-coordinator", PlanetIon = "ions/heavy-ion", PlanetMissiles = "missiles/ballistic";
    private void ConfigurePlanetContent(bool network = false)
    {
        foreach (string id in _content.Maps[PlanetBattle.MapId].Fleets.SelectMany(f => _content.Fleets[f].Units).Select(u => u.Ship).Distinct())
            _content.Ships[id] = _content.Ships[id] with { Armor = _content.Ships[id].Class == "planet" ? network ? 0 : 5 : 50, Success = 2, Evasive = 0,
                Weapons = _content.Ships[id].Class == "planet" ? _content.Ships[id].Weapons : [new("gun", "Planet test gun", 11, 6)] };
    }
    private void ConfigurePlanetBattle(bool network = false)
    {
        _state = _state! with { ActiveSide = network ? 0 : 1, ValorOwner = network ? 1 : 0, RandomState = 3,
            Cards = _state.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] } }; DrawBattle();
    }
    private void StartPlanetNetwork()
    {
        string card = _state!.Cards!.Hand0.First(id => !HeroicCommands.IsHeroic(_state, id));
        if (!ApplyCommand(0, new("begin-order", "s0-0", "attack", Card: card, Weapon: PlanetIon)) || !ApplyCommand(0, new("attack", "s0-0", "s1-2", Weapon: PlanetIon))) throw new InvalidDataException("Host could not start planet attack.");
    }
    private void PlanetNetworkHost(BattleCommand? command)
    {
        string before = _engine.Save(_state!); LoadGame(false); if (before != _engine.Save(_state!)) throw new InvalidDataException("Planet disk restore diverged.");
        if (command?.Kind == "heroic-interrupt") GD.Print("PLANET_HERO_NETWORK_RESUMED");
        if (command?.Kind == "attack") GD.Print("PLANET_ROLL_NETWORK_RESUMED");
        if (command?.Kind == "resolve") { if (_state!.PlanetDamage == null) throw new InvalidDataException("Expected a facility decision."); GD.Print("PLANET_DAMAGE_NETWORK_RESUMED"); }
        if (command?.Kind == "planet-facility")
        {
            GD.Print("PLANET_FACILITY_NETWORK_RESUMED " + command.Target);
            if (_state!.PlanetDamage == null)
            {
                if (_state.Units.Any(u => u.Id == "s0-0") || _state.Cards!.HeroicInterrupt != null || _state.Cards.Response != null || _state.Cards.Pending != null) throw new InvalidDataException("Lost attacking planet did not finish its suspended order.");
                GD.Print("PLANET_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
            }
        }
    }
    private void PlanetNetworkClient()
    {
        if (_planetNetSequence == _state!.Sequence) return; _planetNetSequence = _state.Sequence;
        string hero = PlanetHero + "#hero-1"; var cards = _state.Cards!;
        if (_planetNetStep == 0 && cards.Response?.Side == 0) { _planetNetStep = 1; Submit(new("heroic-interrupt", "s1-2", "s0-0", Card: hero)); }
        else if (_planetNetStep == 1 && cards.Pending?.Card == hero) { _planetNetStep = 2; Submit(new("attack", "s1-2", "s0-0", Weapon: PlanetIon)); }
        else if (_planetNetStep == 2 && _state.Combat != null) { _planetNetStep = 3; Submit(new("resolve")); }
        else if (_planetNetStep == 3 && _state.PlanetDamage is { } damage) Submit(new("planet-facility", damage.Target, FleetPlanets.DamageChoices(_content, _state).First()));
        else if (_planetNetStep == 3 && cards.HeroicInterrupt == null && cards.Response == null && cards.Pending == null)
        {
            var planet = _state.Units.Single(u => u.Id == "s1-2");
            if (_state.Units.Any(u => u.Id == "s0-0") || _state.Orders1 != 0 || _state.Orders0 != 1 || planet.Activated || (planet.ActivatedFacilities ?? []).Length != 0 || cards.HeroicUsed?.Contains(hero) != true || cards.Hand0.Length != 0 || _content.Maps[_state.Map].Objects.Count(o => o.Definition == "core:planet") != 2) throw new InvalidDataException("Planet result, private hand or readiness diverged.");
            _planetNetStep = 4; GD.Print("PLANET_NETWORK_OK"); GetTree().Quit();
        }
    }
    private async Task CheckPlanets()
    {
        try
        {
            void Require(bool ok, string why) { if (!ok) throw new Exception(why); GD.Print("PLANET_UI_PASS " + why); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text || b.Text == "▶ " + text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            void Click(Hex cell) { var p = _camera.UnprojectPosition(World(cell)); _UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = p }); }
            void Restore() { string before = _engine.Save(_state!); SaveGame(); LoadGame(false); Require(before == _engine.Save(_state!), "Planet and facilities restore from disk"); }
            void Capture(string name) { if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", name + ".png")); }
            SinglePlayerMenu(); await Frames(); Press("PLANETARY OVERWATCH AND FACILITIES"); _bot = false; ConfigurePlanetContent(); ConfigurePlanetBattle();
            Require(ApplyCommand(1, new("begin-order", "s1-2", "move", Card: _state!.Cards!.Hand1.First())) && ApplyCommand(1, new("move", "s1-2", Q: 4, R: 3)), "Enemy Move opens reaction");
            _selectedCard = PlanetHero + "#hero-0"; SelectUnit("s0-0"); await Frames(); Press("BEGIN HEROIC REACTION"); await Frames(); Restore(); await Frames();
            Require(_state!.Cards!.Pending?.Weapon == PlanetIon && _state.Orders0 == 0, "Overwatch binds the Hero's battery without Play");
            Capture("planetary-overwatch"); _locale.SetLanguage("ru"); LocalizeInterface(); await Frames(); Capture("planetary-overwatch-ru"); _locale.SetLanguage("en"); LocalizeInterface(); await Frames(); Click(new(5, 3)); await Frames(); Require(_state.Combat is { Power: true, Weapon: PlanetIon }, "Field click fires bound Power Attack"); Restore(); await Frames(); Press("ACCEPT ROLL"); await Frames();
            Require(_state.Cards.HeroicInterrupt == null && !_state.Units.Single(u => u.Id == "s0-0").Activated, "Overwatch preserves ordinary battery activations");
            StartBattle(PlanetBattle.MapId, false); ConfigurePlanetBattle(); _state = _state! with { ActiveSide = 0 }; SelectUnit("s0-0"); _weaponChoice = PlanetMissiles; DrawBattle(); await Frames(); Press("ATTACK"); await Frames();
            Require(_state!.Units.Single(u => u.Id == "s0-0").ActiveFacility == "missiles", "Chosen battery starts its own activation"); Require(ApplyCommand(0, new("skip")), "Finish first battery"); Require(ApplyCommand(1, new("pass")), "Return turn"); _weaponChoice = PlanetIon; SelectUnit("s0-0"); await Frames(); Press("ATTACK"); await Frames();
            Require(_state.Units.Single(u => u.Id == "s0-0").ActivatedFacilities!.Length == 2 && _state.Orders0 == 2, "Second battery activates independently"); Restore();
            StartBattle(PlanetBattle.MapId, false); ConfigurePlanetBattle(); _state = _state! with { ValorOwner = 1, Cards = HeroicCommands.Spend(_state.Cards!, PlanetHero + "#hero-0") };
            Require(ApplyCommand(1, new("begin-order", "s1-2", "attack", Card: _state.Cards.Hand1.First())) && ApplyCommand(1, new("attack", "s1-2", "s0-0", Weapon: "gun")), "Enemy attack rolls against planet");
            _state = _state! with { Combat = _state.Combat! with { Rolls = Enumerable.Repeat(6, 11).ToArray(), AbilityRerolls = 0, RerollSide = -1 } }; DrawBattle(); await Frames(); Press("ACCEPT ROLL"); await Frames(); Restore(); await Frames();
            Require(_state.PlanetDamage?.Remaining == 4 && Nodes(_ui).OfType<Button>().Any(b => b.Text == "DESTROY: Hardened Defences") && !Nodes(_ui).OfType<Button>().Any(b => b.Text == "DESTROY: Ion Batteries"), "Damage UI enforces Hardened first"); Capture("planet-facility-damage");
            Press("DESTROY: Hardened Defences"); await Frames(); Restore(); await Frames(); Press("DESTROY: Ion Batteries"); await Frames(); Restore(); await Frames();
            Require(!_state.Units.Single(u => u.Id == "s0-0").FacilityLeaders!.ContainsKey("ions"), "Destroyed facility loses its Hero"); Press("DESTROY: Missile Defence"); await Frames(); Restore(); await Frames();
            Require(!_state.Units.Any(u => u.Id == "s0-0") && _content.Maps[_state.Map].Objects.Any(o => o.Definition == "core:planet" && o.Q == 3 && o.R == 3), "Military planet removed and physical token retained");
            _locale.SetLanguage("ru"); LocalizeInterface(); await Frames(); Require(_locale.Text("Planetary Overwatch") == "Планетарное прикрытие" && _locale.Text("Ion Batteries") == "Ионные батареи", "Planet facilities are localized"); _locale.SetLanguage("en");
            NetworkMenu(); await Frames(); Require(Nodes(_ui).OfType<CheckBox>().Any(b => b.Text == "Planetary Overwatch and facilities"), "Planet facility drill is hostable"); GD.Print("PLANET_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
