using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private const string HeroCaptain = "core:hero-taiidan-veteran-captain#hero-1";
    private int _moreHeroicNetworkStep;
    private void ConfigureMoreHeroicContent()
    {
        foreach (string id in _content.Maps[HeroicBattle.AdvancedMapId].Fleets.SelectMany(f => _content.Fleets[f].Units).Select(u => u.Ship).Distinct())
            _content.Ships[id] = _content.Ships[id] with { Armor = 50, Success = 2, Evasive = 0, Weapons = [new("gun", "Hero sequence test gun", 2, 6, PowerDice: 2)] };
    }
    private void ConfigureMoreHeroicBattle()
    {
        _state = _state! with { ActiveSide = 1, ValorOwner = 1, RandomState = 3,
            Cards = _state.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] },
            Units = _state.Units.Select(u => u.Id == "s1-5" ? u with { Q = 5, R = 2 } : u.Id == "s1-6" ? u with { Q = 5, R = 3 } : u).ToList() };
        DrawBattle();
    }
    private void MoreHeroicNetworkHost(BattleCommand? command)
    {
        string before = _engine.Save(_state!); LoadGame(false);
        if (before != _engine.Save(_state!)) throw new InvalidDataException("Two-unit Heroic disk restore diverged.");
        if (command?.Kind == "special")
        {
            if (!_state!.Cards!.Pending!.Units.SequenceEqual(["s1-5", "s1-6"]) || _state.Orders1 != 1) throw new InvalidDataException("Remote two-unit assignment lost.");
            GD.Print("MORE_HEROIC_ASSIGNMENT_NETWORK_RESUMED");
        }
        if (command?.Kind == "attack")
        {
            if (_state!.Combat?.Power != (_state.Cards!.Pending!.Step == 1)) throw new InvalidDataException("Two-unit Power flags changed.");
            GD.Print("MORE_HEROIC_ROLL_NETWORK_RESUMED");
        }
        if (command?.Kind == "resolve" && _state!.Cards!.Pending?.Step == 2) GD.Print("MORE_HEROIC_SECOND_NETWORK_RESUMED");
        if (command?.Kind == "resolve" && _state!.Cards!.Pending == null)
        {
            if (_state.Orders1 != 1 || _state.Cards.HeroicUsed?.Contains(HeroCaptain) != true || _state.Cards.Discard.Contains(HeroCaptain)) throw new InvalidDataException("Two-unit Heroic spending diverged.");
            GD.Print("MORE_HEROIC_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
        }
    }
    private void MoreHeroicNetworkClient()
    {
        if (_moreHeroicNetworkStep == 0)
        {
            if (_state!.Cards!.Hand1.Length != _state.Cards.HandLimit1 + 2 || _state.Cards.HeroicCards?.Length != 6 || _state.Cards.Hand0.Length != 0) throw new InvalidDataException("New Heroic private hand diverged.");
            _moreHeroicNetworkStep = 1; Submit(new("special", "s1-5", Card: HeroCaptain, OtherUnit: "s1-6"));
        }
        else if (_moreHeroicNetworkStep == 1 && _state!.Cards!.Pending?.Step == 0)
        { _moreHeroicNetworkStep = 2; Submit(new("skip")); }
        else if (_moreHeroicNetworkStep == 2 && _state!.Cards!.Pending?.Step == 1)
        { _moreHeroicNetworkStep = 3; Submit(new("attack", "s1-5", "s0-0", Weapon: "gun")); }
        else if (_moreHeroicNetworkStep == 3 && _state!.Combat != null)
        { _moreHeroicNetworkStep = 4; Submit(new("resolve")); }
        else if (_moreHeroicNetworkStep == 4 && _state!.Cards!.Pending?.Step == 2)
        { _moreHeroicNetworkStep = 5; Submit(new("skip")); }
        else if (_moreHeroicNetworkStep == 5 && _state!.Cards!.Pending?.Step == 3)
        { _moreHeroicNetworkStep = 6; Submit(new("attack", "s1-6", "s0-0", Weapon: "gun")); }
        else if (_moreHeroicNetworkStep == 6 && _state!.Combat != null)
        { _moreHeroicNetworkStep = 7; Submit(new("resolve")); }
        else if (_moreHeroicNetworkStep == 7 && _state!.Cards!.Pending == null)
        {
            if (_state.Orders1 != 1 || _state.Cards.HeroicUsed?.Contains(HeroCaptain) != true || !_state.Units.Single(u => u.Id == "s1-6").Activated) throw new InvalidDataException("Remote second actor was not activated.");
            _moreHeroicNetworkStep = 8; GD.Print("MORE_HEROIC_NETWORK_OK"); GetTree().Quit();
        }
    }
    private async Task CheckMoreHeroic()
    {
        try
        {
            void Require(bool ok, string why) { if (!ok) throw new Exception(why); GD.Print("MORE_HEROIC_UI_PASS " + why); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var button = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!button.Disabled, "Enabled: " + text); button.EmitSignal(BaseButton.SignalName.Pressed); }
            void Click(Hex cell) { var point = _camera.UnprojectPosition(World(cell)); _UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point }); }
            void Restore() { string before = _engine.Save(_state!); SaveGame(); LoadGame(false); Require(before == _engine.Save(_state!), "Two-unit continuation restores from disk"); }
            SinglePlayerMenu(); await Frames(); Require(Nodes(_ui).OfType<Button>().Any(b => b.Text == "MORE HEROIC COMMANDS"), "Six more Heroic commands have a menu entry");
            ConfigureMoreHeroicContent(); StartBattle(HeroicBattle.AdvancedMapId, false, persist: false); ConfigureMoreHeroicBattle();
            _selectedCard = HeroCaptain; _formationLines = "both"; SelectUnit("s1-5"); await Frames();
            var second = Nodes(_ui).OfType<OptionButton>().Single(b => b.ItemCount > 0 && b.GetItemText(0).StartsWith("Second: "));
            Require(second.ItemCount == 1 && _secondOrderUnit == "s1-6", "Second line picker excludes the capital ship");
            Press("SPECIAL ORDER"); await Frames(); Require(_state!.Orders1 == 1 && _state.Units.Where(u => u.Activated).Count() == 2, "Both actors activate for one Play"); Restore(); await Frames();
            Press("SKIP ACTION"); await Frames(); Click(new(2, 2)); await Frames(); Require(_state.Combat?.Power == true, "Hero's printed Power Attack starts from a field click"); Restore(); await Frames();
            Press("ACCEPT ROLL"); await Frames(); Require(_state.Cards!.Pending?.Step == 2 && _selected == "s1-6", "Second frigate becomes the selected actor"); Restore(); await Frames();
            _locale.SetLanguage("ru"); LocalizeInterface(); await Frames(); Require(_locale.Text("Frigate Attack") == "Атака фрегатов", "New Heroic commands are localized");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "more-heroic-second-line.png"));
            _locale.SetLanguage("en"); LocalizeInterface(); await Frames(); Press("SKIP ACTION"); await Frames(); Click(new(2, 2)); await Frames(); Require(_state.Combat?.Power == false, "Second frigate makes an ordinary Attack"); Press("ACCEPT ROLL"); await Frames();
            Require(_state.Cards!.Pending == null && _state.Orders1 == 1 && _state.Cards.HeroicUsed!.Contains(HeroCaptain), "Two-unit command completes with one permanent payment");
            StartBattle(HeroicBattle.AdvancedMapId, false, persist: false); ConfigureMoreHeroicBattle(); _state = _state! with { ActiveSide = 0 }; _selectedCard = "core:hero-kushan-fighter-ace#hero-0"; _formationLines = "second"; SelectUnit("s0-3"); await Frames();
            Press("SPECIAL ORDER"); await Frames(); Require(_state.Cards!.Pending?.Step == 2 && !_state.Units.Single(u => u.Id == "s0-1").Activated, "UI allows second fighter line alone");
            StartBattle(HeroicBattle.AdvancedMapId, false, persist: false); ConfigureMoreHeroicBattle(); _state = _state! with { ActiveSide = 0, Units = _state.Units.Select(u => u.Id == "s0-0" ? u with { CrewDamage = 3 } : u).ToList() }; _selectedCard = "core:hero-kushan-medical-chief#hero-0"; SelectUnit("s0-0"); await Frames();
            Press("SPECIAL ORDER"); await Frames(); Require(_state.Units.Single(u => u.Id == "s0-0").CrewDamage == 1, "Patch and Medic heal before the first action"); Restore(); await Frames();
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "more-heroic-medic.png"));
            NetworkMenu(); await Frames(); Require(Nodes(_ui).OfType<CheckBox>().Any(b => b.Text == "More Heroic commands"), "New drill can be hosted for PvP or co-op");
            GD.Print("MORE_HEROIC_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
