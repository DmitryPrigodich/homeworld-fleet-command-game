using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private int _heroReactionStep;
    private void ConfigureHeroicReactionContent()
    {
        foreach (string id in _content.Maps[HeroicBattle.ReactionMapId].Fleets.SelectMany(f => _content.Fleets[f].Units).Select(u => u.Ship).Distinct())
            _content.Ships[id] = _content.Ships[id] with { Armor = 50, Success = 2, Evasive = 0, Weapons = [new("gun", "Reaction test gun", 2, 6)] };
    }
    private void ConfigureHeroicReactionBattle(bool network = false)
    {
        _state = _state! with { ActiveSide = network ? 0 : 1, ValorOwner = network ? 1 : 0, RandomState = 3,
            Cards = _state.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] } }; DrawBattle();
    }
    private void StartHeroicReactionNetwork()
    {
        string card = _state!.Cards!.Hand0.First();
        if (!ApplyCommand(0, new("begin-order", "s0-0", "move", Card: card)) || !ApplyCommand(0, new("move", "s0-0", Q: 4, R: 3))) throw new InvalidDataException("Host could not start enemy Move.");
    }
    private void HeroicReactionNetworkHost(BattleCommand? command)
    {
        string before = _engine.Save(_state!); LoadGame(false); if (before != _engine.Save(_state!)) throw new InvalidDataException("Heroic reaction disk restore diverged.");
        if (command?.Kind == "heroic-interrupt") { if (_state!.Cards!.HeroicInterrupt?.Target != "s0-0" || _state.Orders1 != 0) throw new InvalidDataException("Remote Heroic parent context lost."); GD.Print("HEROIC_REACTION_PARENT_NETWORK_RESUMED"); }
        if (command?.Kind == "move") GD.Print("HEROIC_REACTION_MOVE_NETWORK_RESUMED");
        if (command?.Kind == "attack") GD.Print("HEROIC_REACTION_ROLL_NETWORK_RESUMED");
        if (command?.Kind == "resolve")
        {
            if (_state!.Cards!.Response is not { Replan: true } || _state.Cards.HeroicInterrupt != null || _state.Orders1 != 0) throw new InvalidDataException("Original enemy route was not restored.");
            GD.Print("HEROIC_REACTION_RETURN_NETWORK_RESUMED"); if (!ApplyCommand(0, new("stop-move"))) throw new InvalidDataException("Host could not finish the interrupted order.");
            GD.Print("HEROIC_REACTION_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
        }
    }
    private void HeroicReactionNetworkClient()
    {
        const string hero = "core:hero-kushan-recon-commander#hero-1"; var cards = _state!.Cards!;
        if (_heroReactionStep == 0 && cards.Response?.Side == 0) { _heroReactionStep = 1; Submit(new("heroic-interrupt", "s1-2", "s0-0", Card: hero)); }
        else if (_heroReactionStep == 1 && cards.Pending?.Card == hero && cards.Pending.Step == 0) { _heroReactionStep = 2; Submit(new("move", "s1-2", Q: 3, R: 3)); }
        else if (_heroReactionStep == 2 && cards.Pending?.Step == 1) { _heroReactionStep = 3; Submit(new("attack", "s1-2", "s0-0", Weapon: "gun")); }
        else if (_heroReactionStep == 3 && _state.Combat != null) { _heroReactionStep = 4; Submit(new("resolve")); }
        else if (_heroReactionStep == 4 && cards.Response == null && cards.Pending == null)
        {
            if (_state.Orders1 != 0 || _state.Orders0 != 1 || _state.Units.Single(u => u.Id == "s1-2").Activated || cards.HeroicUsed?.Contains(hero) != true || cards.Hand0.Length != 0) throw new InvalidDataException("Heroic reaction result or private view diverged.");
            _heroReactionStep = 5; GD.Print("HEROIC_REACTION_NETWORK_OK"); GetTree().Quit();
        }
    }
    private async Task CheckHeroicReaction()
    {
        try
        {
            void Require(bool ok, string why) { if (!ok) throw new Exception(why); GD.Print("HEROIC_REACTION_UI_PASS " + why); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            void Click(Hex cell) { var p = _camera.UnprojectPosition(World(cell)); _UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = p }); }
            void Restore() { string before = _engine.Save(_state!); SaveGame(); LoadGame(false); Require(before == _engine.Save(_state!), "Heroic reaction restores from disk"); }
            SinglePlayerMenu(); await Frames(); Press("HEROIC REACTION"); _bot = false; ConfigureHeroicReactionContent(); ConfigureHeroicReactionBattle();
            string card = _state!.Cards!.Hand1.First(); ApplyCommand(1, new("begin-order", "s1-2", "move", Card: card)); ApplyCommand(1, new("move", "s1-2", Q: 4, R: 3));
            _selectedCard = "core:hero-kushan-recon-commander#hero-0"; SelectUnit("s0-0"); await Frames(); Press("BEGIN HEROIC REACTION"); await Frames(); Restore(); await Frames();
            Require(_state.Cards!.HeroicInterrupt != null && _state.Orders0 == 0 && !_state.Units.Single(u => u.Id == "s0-0").Activated, "Heroic declaration preserves Play and readiness");
            Click(new(3, 3)); await Frames(); Require(_state.Cards.Pending?.Step == 1, "Field movement reaches the Hero's Attack"); Restore(); await Frames();
            _locale.SetLanguage("ru"); LocalizeInterface(); await Frames(); Require(_locale.Text("Rapid Reaction") == "Быстрая реакция", "Rapid Reaction is localized");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "rapid-reaction.png"));
            _locale.SetLanguage("en"); LocalizeInterface(); await Frames(); Click(new(5, 3)); await Frames(); Require(_state.Combat?.Target == "s1-2", "Field Attack targets the interrupted mover"); Restore(); await Frames(); Press("ACCEPT ROLL"); await Frames();
            Require(_state.Cards.HeroicInterrupt == null && _state.Cards.Response is { Replan: true, Side: 1 }, "Original enemy route resumes after the Heroic roll"); Restore(); await Frames(); Press("STOP HERE"); await Frames();
            Require(_state.Cards.Pending == null && _state.Orders0 == 0 && _state.Orders1 == 1 && !_state.Units.Single(u => u.Id == "s0-0").Activated, "Original paid order finishes without activating Hero");
            NetworkMenu(); await Frames(); Require(Nodes(_ui).OfType<CheckBox>().Any(b => b.Text == "Heroic reaction"), "Heroic reaction drill is hostable"); GD.Print("HEROIC_REACTION_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
