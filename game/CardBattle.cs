using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private string _selectedCard = "";
    private string _secondOrderUnit = "";
    private string _formationLines = "both";
    private void CardGuide()
    {
        EndTutorial(); _state = null; ClearScreen(); Heading("Tutorial: cards and special orders");
        var box = Panel(70, 175, 1000, 660);
        Text(box, "1. INITIATIVE", 20, _colors.Highlight);
        Text(box, "At the start of a round your hand is replenished to two cards. Click READY / NO BID or spend a card for +1 to the roll (maximum 6). Ties require fresh bids. The winner issues the first order.", 17);
        Text(box, "2. CHOOSE AN ORDER", 20, _colors.Highlight);
        Text(box, "Select a ready unit and a card on the left. MOVE and ATTACK use the card as a basic order. SPECIAL ORDER starts its action sequence. Each order spends one card and one Play.", 17);
        Text(box, "3. SPECIAL ACTIONS", 20, _colors.Highlight);
        Text(box, "All Ahead Full: two moves in the same direction. Stay in Formation: assign the second unit beforehand; the first performs Move → Attack, the second Lightning Move → Attack. Lightning Move uses Afterburners and must end adjacent to the first unit.\n\nThe game displays the current step and selects its actor. You may skip an action or finish the order early. Saving preserves the current step.", 17);
        Text(box, "4. RETURN FIRE", 20, _colors.Highlight);
        Text(box, "Interrupt Attack: during a response window, select your unit and click the red target, the acting enemy unit. Activated units may respond without spending Play, once per round. SKIP WINDOW resumes the enemy action. Movement responses occur after entering a free hex; attack responses occur before rolling.", 17);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 10); box.AddChild(actions);
        Button(actions, "TRAINING: INTERRUPT", StartInterruptLesson);
        Button(actions, "START CARD BATTLE", StartCardBattle);
        Button(actions, "BASIC TUTORIAL", StartTutorial); MenuScene();
    }
    private void StartCardBattle() { StartBattle(BasicCardBattle.MapId, true); QueueBot(); }
    private void StartInterruptLesson()
    {
        StopNetwork(); StartBattle(BasicCardBattle.MapId, false, persist: false); _interruptLesson = true;
        var state = _state!;
        string[] hand0 = ["order-17", "order-21"], hand1 = ["order-22", "order-23"];
        var deck = Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(hand0).Except(hand1).ToArray();
        _state = state with { ActiveSide = 1, Units = state.Units.Where(u => u.Id is "s0-0" or "s0-1" or "s1-3")
            .Select(u => u.Id == "s0-0" ? u with { Q = 2, R = 3, Activated = true } : u.Id == "s0-1" ? u with { Q = 1, R = 4 } : u with { Q = 3, R = 3 }).ToList(),
            Cards = new(deck, hand0, hand1, [], Phase: "orders") };
        ApplyCommand(1, new("attack", "s1-3", "s0-0", Card: "order-22"));
        _status = "Lesson: the enemy declared an attack. Click the red target to respond with your already activated Interceptor unit #1. Main game saves are not affected.";
        DrawBattle();
    }
    private BattleCommand WithCard(BattleCommand command)
    {
        if (FleetBattle)
        {
            if (command.Kind is "begin-order" or "special" or "move" or "attack")
            {
                var actors = command.Kind == "special" && SpecialOrders.Formation(command.Card) ? new[] { command.Unit, command.OtherUnit } : new[] { command.Unit };
                var deployments = new List<UnitDeployment>();
                foreach (var unit in _state!.Units.Where(u => actors.Contains(u.Id) && u.Patrol != ""))
                {
                    var cells = FleetCap.ExitCells(_content, _state, unit);
                    if (cells.Length == 0) continue;
                    var cell = _capExits.TryGetValue(unit.Id, out var chosen) && cells.Contains(chosen) ? chosen : cells[0];
                    deployments.Add(new(unit.Id, cell.Q, cell.R));
                }
                command = command with { Deployments = deployments.ToArray() };
            }
            if (command.Kind is "attack" or "interrupt") command = command with { Weapon = _weaponChoice, AttackKind = _attackKindChoice };
            if (command.Kind == "move" && command.AutoFacing) command = command with { AutoFacing = _facingChoice < 0, Facing = Math.Max(0, _facingChoice) };
        }
        if (_state?.Cards?.Response != null && command.Kind == "attack")
        {
            var responseHand = LocalCommandSide == 0 ? _state.Cards.Hand0 : _state.Cards.Hand1;
            return command with { Kind = "interrupt", Card = responseHand.Contains(_selectedCard) && InterruptRules.IsAttack(_selectedCard) ? _selectedCard : responseHand.FirstOrDefault(InterruptRules.IsAttack) ?? "" };
        }
        if (_state?.Cards is not { Phase: "orders", Pending: null, Response: null } cards || command.Kind is "pass" or "special" or "faction") return command;
        var hand = LocalCommandSide == 0 ? cards.Hand0 : cards.Hand1;
        return command with { Card = hand.Contains(_selectedCard) ? _selectedCard : hand.FirstOrDefault() ?? "" };
    }
    private void DrawCards(Control parent)
    {
        if (_state?.Cards is not { } cards) return;
        if (DrawFleetPhase(parent)) return;
        if (cards.Response is { } pause)
        {
            int responder = 1 - pause.Side;
            Text(parent, "RETURN FIRE", 15, _colors.Highlight);
            Text(parent, "Target: " + FleetPresentation.UnitName(_content, _state, _state.Units.Single(u => u.Id == pause.Command.Unit)), 13);
            if (LocalCommandSide != responder) { Text(parent, "Waiting for response: " + SideName(responder), 14); return; }
            var responseHand = (responder == 0 ? cards.Hand0 : cards.Hand1).Where(InterruptRules.IsAttack).ToArray();
            if (!responseHand.Contains(_selectedCard)) _selectedCard = responseHand.FirstOrDefault() ?? "";
            Text(parent, responseHand.Length == 0 ? "No Interrupt Attack card. Skip the response window." : "Select your unit and click the red target. Activated units may also respond. No Play is spent.", 13);
            foreach (var id in responseHand)
            {
                string card = id;
                var b = Button(parent, (_selectedCard == card ? "▶ " : "") + "Interrupt Attack", () => { _selectedCard = card; DrawBattle(); });
                b.CustomMinimumSize = new(0, 30); b.AddThemeFontSizeOverride("font_size", 13);
            }
            Button(parent, "SKIP WINDOW", () => Submit(new("decline")));
            Text(parent, "1 Interrupt Attack per round. The order resumes after the response.", 12, _colors.Muted);
            DrawCardPreview(parent, _selectedCard);
            return;
        }
        if (cards.Pending is { } pending)
        {
            Text(parent, pending.BasicAction == "" ? SpecialOrders.Name(pending.Card) : "BASIC ORDER", 15, _colors.Highlight);
            Text(parent, $"Action {pending.Step + 1}/{SpecialOrders.Steps(pending)}: " + SpecialOrders.ActionName(pending) + "\n" +
                FleetPresentation.UnitName(_content, _state, _state.Units.Single(u => u.Id == SpecialOrders.Actor(pending))), 13);
            Text(parent, pending.BasicAction == "harvest" ? "Free movement → harvest. Both steps may be skipped." : pending.BasicAction != "" ? "Card and Play already spent. Choose an action target or skip the action." : SpecialOrders.Formation(pending.Card) ? "Move → Attack / Afterburners → Attack\nLightning Move: end adjacent to the first unit." : "Two Moves in the same direction.\nEither action may be skipped.", 12, _colors.Muted);
            Button(parent, "SKIP ACTION", () => Submit(new("skip")), LocalCommandSide != pending.Side);
            Button(parent, "FINISH ORDER", () => Submit(new("finish-order")), LocalCommandSide != pending.Side);
            DrawCardPreview(parent, pending.Card);
            return;
        }
        int side = LocalCommandSide;
        var hand = side == 0 ? cards.Hand0 : cards.Hand1;
        if (!hand.Contains(_selectedCard)) _selectedCard = hand.FirstOrDefault() ?? "";
        Text(parent, cards.Phase == "initiative" ? "INITIATIVE / bid 0–1 card" : "HAND / basic orders", 13, _colors.Highlight);
        bool ready = side == 0 ? cards.Ready0 : cards.Ready1;
        if (cards.Phase == "initiative")
            Button(parent, ready ? "BID CONFIRMED" : "READY / NO BID", () => Submit(new("initiative")), ready);
        foreach (string id in hand)
        {
            string title = SpecialOrders.Name(id);
            var button = Button(parent, cards.Phase == "initiative" ? "BID: " + title : (_selectedCard == id ? "▶ " : "") + title,
                () => { if (cards.Phase == "initiative") Submit(new("initiative", Card: id)); else { _selectedCard = id; DrawBattle(); } }, cards.Phase == "initiative" && ready);
            button.CustomMinimumSize = new(0, 30); button.AddThemeFontSizeOverride("font_size", 13);
        }
        if (cards.Phase == "orders" && SpecialOrders.Supported(_selectedCard))
        {
            var selected = _state.Units.Find(u => u.Id == _selected && u.Side == side && !u.Activated && !u.Hulk);
            if (FleetBattle && SpecialOrders.Formation(_selectedCard))
            {
                string[] modes = ["both", "first", "second"]; var lines = new OptionButton(); lines.AddItem("Both order lines"); lines.AddItem("First line only"); lines.AddItem("Second line only");
                lines.Select(Array.IndexOf(modes, _formationLines)); lines.ItemSelected += i => { _formationLines = modes[(int)i]; DrawBattle(); }; parent.AddChild(lines);
            }
            if (SpecialOrders.Formation(_selectedCard) && selected != null && (!FleetBattle || _formationLines == "both"))
            {
                var others = _state.Units.Where(u => u.Side == side && !u.Activated && !u.Hulk && u.Id != selected.Id).ToArray();
                if (!others.Any(u => u.Id == _secondOrderUnit)) _secondOrderUnit = others.FirstOrDefault()?.Id ?? "";
                var other = new OptionButton();
                foreach (var u in others) other.AddItem("Second: " + FleetPresentation.UnitName(_content, _state, u));
                if (others.Length > 0) other.Select(Array.FindIndex(others, u => u.Id == _secondOrderUnit));
                other.ItemSelected += index => _secondOrderUnit = others[(int)index].Id; parent.AddChild(other);
            }
            var play = Button(parent, "SPECIAL ORDER", () => Submit(new("special", FleetBattle && SpecialOrders.Formation(_selectedCard) && _formationLines == "second" ? "" : _selected,
                Card: _selectedCard, OtherUnit: FleetBattle && SpecialOrders.Formation(_selectedCard) && _formationLines == "first" ? "" : FleetBattle && SpecialOrders.Formation(_selectedCard) && _formationLines == "second" ? _selected : _secondOrderUnit,
                Repair: _repairChoice, OtherRepair: _repairOtherChoice)), selected == null || side != _state.ActiveSide);
            play.CustomMinimumSize = new(0, 32);
        }
        Text(parent, $"Hand {(side == 0 ? cards.HandLimit0 : cards.HandLimit1)} / Play {(side == 0 ? cards.PlayLimit0 : cards.PlayLimit1)} · Move / Attack / Harvest", 11, _colors.Muted);
        DrawCardPreview(parent, _selectedCard);
    }
}
