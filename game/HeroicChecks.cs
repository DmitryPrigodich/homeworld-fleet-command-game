using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private const string HeroPilot = "core:hero-taiidan-hot-shot-pilot#hero-1";
    private int _heroicNetworkStep;
    private void ConfigureHeroicContent()
    {
        var fighter = _content.Ships["practice:hero-fighter"];
        _content.Ships[fighter.Id] = fighter with { Success = 2, Weapons = [new("gun", "Hero test gun", 2, 4, PowerDice: 2)] };
        var target = _content.Ships["practice:orders-kushan-boarder"];
        _content.Ships[target.Id] = target with { Armor = 50, Weapons = [] };
    }
    private void ConfigureHeroicBattle()
    {
        _state = _state! with { ActiveSide = 1, ValorOwner = 1, Cards = _state.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] },
            Units = _state.Units.Select(u => u.Id == "s1-3" ? u with { Q = 3, R = 4 } : u).ToList() };
        DrawBattle();
    }
    private void HeroicNetworkHost(BattleCommand? command)
    {
        string before = _engine.Save(_state!); LoadGame(false);
        if (before != _engine.Save(_state!)) throw new InvalidDataException("Heroic disk restore diverged.");
        if (command?.Kind == "special")
        {
            if (_state!.Cards!.HeroicUsed?.Contains(HeroPilot) != true || !FleetRuleTools.Has(_state.Units.Single(u => u.Id == "s1-3"), "hero-evasive")) throw new InvalidDataException("Remote Heroic start effect missing.");
            GD.Print("HEROIC_ORDER_NETWORK_RESUMED");
        }
        if (command?.Kind == "attack")
        {
            if (_state!.Combat?.Power != true || _state.Cards!.Pending?.Step != 1) throw new InvalidDataException("Remote Heroic Power Attack missing.");
            GD.Print("HEROIC_ROLL_NETWORK_RESUMED");
        }
        if (command?.Kind == "resolve" && _state!.Cards!.Pending == null)
        {
            if (_state.Orders1 != 1 || _state!.Cards!.Discard.Contains(HeroPilot) || _state!.Cards!.Hand1.Contains(HeroPilot)) throw new InvalidDataException("Heroic payment was recycled.");
            GD.Print("HEROIC_FINISH_NETWORK_RESUMED"); GD.Print("HEROIC_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
        }
    }
    private void HeroicNetworkClient()
    {
        if (_heroicNetworkStep == 0)
        {
            if (_state!.Cards!.Hand1.Length != _state.Cards.HandLimit1 + 2 || _state.Cards.HeroicCards?.Length != 4) throw new InvalidDataException("Heroic extra hand or public registry missing.");
            _heroicNetworkStep = 1; Submit(new("special", "s1-3", Card: HeroPilot));
        }
        else if (_heroicNetworkStep == 1 && _state!.Cards!.Pending?.Step == 0)
        { _heroicNetworkStep = 2; Submit(new("skip")); }
        else if (_heroicNetworkStep == 2 && _state!.Cards!.Pending?.Step == 1)
        { _heroicNetworkStep = 3; Submit(new("attack", "s1-3", "s0-2", Weapon: "gun")); }
        else if (_heroicNetworkStep == 3 && _state!.Combat != null)
        { if (!_state.Combat.Power) throw new InvalidDataException("Heroic Power flag lost in private view."); _heroicNetworkStep = 4; Submit(new("resolve")); }
        else if (_heroicNetworkStep == 4 && _state!.Cards!.Pending == null)
        {
            if (_state.Orders1 != 1 || _state.Cards.HeroicUsed?.Contains(HeroPilot) != true || !FleetRuleTools.Has(_state.Units.Single(u => u.Id == "s1-3"), "hero-evasive")) throw new InvalidDataException("Heroic result diverged.");
            _heroicNetworkStep = 5; GD.Print("HEROIC_NETWORK_OK"); GetTree().Quit();
        }
    }
    private async Task CheckHeroic()
    {
        try
        {
            void Require(bool ok, string why) { if (!ok) throw new Exception(why); GD.Print("HEROIC_UI_PASS " + why); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var button = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!button.Disabled, "Enabled: " + text); button.EmitSignal(BaseButton.SignalName.Pressed); }
            void Click(Hex cell) { var point = _camera.UnprojectPosition(World(cell)); _UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point }); }
            SinglePlayerMenu(); await Frames(); Require(Nodes(_ui).OfType<Button>().Any(b => b.Text == "HEROES AND HEROIC COMMANDS"), "Heroic practice has a menu entry");
            StartBattle(HeroicBattle.MapId, false, persist: false); ConfigureHeroicContent(); ConfigureHeroicBattle(); _selectedCard = HeroPilot; SelectUnit("s1-3"); await Frames();
            Require(Nodes(_ui).OfType<Label>().Any(l => l.Text == "HEROIC COMMANDS / ONCE PER SCENARIO") && _state!.Cards!.Hand1.Length == _state.Cards.HandLimit1 + 2, "Heroic cards are visibly separate from the normal Hand limit");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "heroic-hand.png"));
            Press("SPECIAL ORDER"); await Frames(); Require(FleetRuleTools.Has(_state!.Units.Single(u => u.Id == "s1-3"), "hero-evasive") && _state.Cards!.HeroicUsed!.Contains(HeroPilot), "Heroic effect starts before Move and Power Attack");
            string before = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(before == _engine.Save(_state!), "Heroic activation and spent card restore from disk");
            _locale.SetLanguage("ru"); LocalizeInterface(); await Frames(); Require(_locale.Text("Best of the Best") == "Лучший из лучших", "Heroic names are localized");
            _locale.SetLanguage("en"); LocalizeInterface(); await Frames(); Press("SKIP ACTION"); await Frames(); Click(new(2, 4)); await Frames();
            Require(_state.Combat?.Power == true && _state!.Cards!.Pending?.Step == 1, "Field click executes the printed Power Attack");
            before = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(before == _engine.Save(_state!), "Heroic post-roll decision restores");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "heroic-power-attack.png"));
            Press("ACCEPT ROLL"); await Frames(); Require(_state!.Cards!.Pending == null && _state.Orders1 == 1 && !_state!.Cards!.Discard.Contains(HeroPilot) && !_state!.Cards!.Hand1.Contains(HeroPilot), "Heroic order finishes once and never enters discard");
            NetworkMenu(); await Frames(); Require(Nodes(_ui).OfType<CheckBox>().Any(b => b.Text == "Heroes and Heroic commands"), "Heroic practice can be hosted for PvP and co-op");
            GD.Print("HEROIC_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
