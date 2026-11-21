using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private string _selectedCard = "";
    private string _secondOrderUnit = "";
    private string _formationLines = "both";
    private string _cardRepairChoice = "auto";
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
        Text(box, "Interrupt Attack: during a response window, select your unit and click the red target, the acting enemy unit. Activated units may respond without spending Play, once per round. SKIP WINDOW resumes the enemy action. Published Attack and Move interrupts respond to enemy Move, Lightning Move or Reinforce. Move requires a friendly unit within 3 hexes. Only the legacy training deck also pauses ordinary attacks before rolling.", 17);
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
        ApplyCommand(1, new("move", "s1-3", Q: 2, R: 2, Card: "order-22"));
        _status = "Lesson: the enemy is moving. Click the red target to respond with your already activated Interceptor unit #1. Main game saves are not affected.";
        DrawBattle();
    }
    private BattleCommand WithCard(BattleCommand command)
    {
        if (command.Kind is "counter" or "interrupt-repairs" or "interrupt-move" or "resume-move" or "stop-move") return command;
        if (FleetBattle)
        {
            if (command.Kind is "begin-order" or "special" or "move" or "attack")
            {
                var actors = command.Kind == "special" && SpecialOrders.Formation(_content, _state!, command.Card) ? new[] { command.Unit, command.OtherUnit } : new[] { command.Unit };
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
            bool Valid(string id) => InterruptRules.IsAttack(_content, _state, id) && InterruptRules.Eligible(_content, _state, id, _state.Cards.Response);
            return command with { Kind = "interrupt", Card = responseHand.Contains(_selectedCard) && Valid(_selectedCard) ? _selectedCard : responseHand.FirstOrDefault(Valid) ?? "" };
        }
        if (_state?.Cards?.Response is { Replan: false } movingResponse && command.Kind == "move")
        {
            var responseHand = LocalCommandSide == 0 ? _state.Cards.Hand0 : _state.Cards.Hand1;
            bool Valid(string id) => InterruptRules.IsMove(_content, _state, id) && InterruptRules.Eligible(_content, _state, id, movingResponse);
            return command with { Kind = "interrupt-move", Card = responseHand.Contains(_selectedCard) && Valid(_selectedCard) ? _selectedCard : responseHand.FirstOrDefault(Valid) ?? "" };
        }
        if (_state?.Cards is not { Phase: "orders", Pending: null, Response: null } cards || command.Kind is "pass" or "special" or "faction") return command;
        var hand = LocalCommandSide == 0 ? cards.Hand0 : cards.Hand1;
        return command with { Card = hand.Contains(_selectedCard) && HeroicCommands.CanUse(_content, _state, LocalCommandSide, _selectedCard) ? _selectedCard : hand.FirstOrDefault(id => HeroicCommands.CanUse(_content, _state, LocalCommandSide, id)) ?? "" };
    }
    private void DrawCards(Control parent)
    {
        if (_state?.Cards is not { } cards) return;
        if (cards.Counter is { } counter)
        {
            int counterSide = 1 - counter.Side;
            Text(parent, "COUNTERINTELLIGENCE", 15, _colors.Highlight);
            Text(parent, "Declared interrupt: " + CommandCards.Name(_content, _state, counter.Command.Card), 13);
            Text(parent, "Cancel the interrupt before its effect, or let it proceed. Both cancelled cards are discarded; both types count as used this round.", 13);
            if (LocalCommandSide == counterSide)
            {
                string card = (counterSide == 0 ? cards.Hand0 : cards.Hand1).FirstOrDefault(id => InterruptRules.IsCounter(_content, _state, id)) ?? "";
                if (card != "") Button(parent, "CANCEL INTERRUPT", () => Submit(new("counter", Card: card)));
                Button(parent, "LET INTERRUPT PROCEED", () => Submit(new("decline-counter")));
            }
            else Text(parent, "Waiting for counter decision: " + SideName(counterSide), 13);
            return;
        }
        if (DrawFleetPhase(parent)) return;
        if (cards.Response is { } pause)
        {
            if (pause.Replan)
            {
                Text(parent, "RESUME MOVEMENT", 15, _colors.Highlight);
                Text(parent, "The field has changed. Choose a green destination using the remaining movement allowance, or stop here.", 13);
                if (LocalCommandSide == pause.Side) Button(parent, "STOP HERE", () => Submit(new("stop-move")));
                else Text(parent, "Waiting for the moving player's route choice.", 13);
                return;
            }
            int responder = InterruptRules.Responder(_state);
            Text(parent, "INTERRUPT WINDOW", 15, _colors.Highlight);
            Text(parent, "Target: " + FleetPresentation.UnitName(_content, _state, _state.Units.Single(u => u.Id == pause.Command.Unit)), 13);
            if (LocalCommandSide != responder) { Text(parent, "Waiting for response: " + SideName(responder), 14); return; }
            var responseHand = (responder == 0 ? cards.Hand0 : cards.Hand1).Where(id => (InterruptRules.IsAttack(_content, _state, id) && InterruptRules.Available(_content, _state, responder)
                || InterruptRules.IsMove(_content, _state, id) && InterruptRules.Available(_content, _state, responder, "move")) && InterruptRules.Eligible(_content, _state, id, pause)).ToArray();
            if (!responseHand.Contains(_selectedCard)) _selectedCard = responseHand.FirstOrDefault() ?? "";
            _action = InterruptRules.IsMove(_content, _state, _selectedCard) ? "move" : "attack";
            Text(parent, responseHand.Length == 0 ? "No usable reaction card. Skip the response window." : "Select a reaction card and your unit. Move to a green hex or attack the red enemy. No Play or activation is spent.", 13);
            foreach (var id in responseHand)
            {
                string card = id;
                var b = Button(parent, (_selectedCard == card ? "▶ " : "") + SpecialOrders.Name(_content, _state, card), () => { _selectedCard = card; DrawBattle(); });
                b.CustomMinimumSize = new(0, 30); b.AddThemeFontSizeOverride("font_size", 13);
            }
            Button(parent, "SKIP WINDOW", () => Submit(new("decline")));
            Text(parent, "One interrupt of each type per round. The original action resumes afterwards.", 12, _colors.Muted);
            DrawCardPreview(parent, _selectedCard);
            return;
        }
        if (cards.Pending is { } pending)
        {
            Text(parent, pending.BasicAction == "" ? SpecialOrders.Name(_content, _state!, pending.Card) : "BASIC ORDER", 15, _colors.Highlight);
            Text(parent, $"Action {pending.Step + 1}/{SpecialOrders.Steps(_content, _state!, pending)}: " + SpecialOrders.ActionName(_content, _state!, pending) + "\n" +
                FleetPresentation.UnitName(_content, _state, _state.Units.Concat(_state.Reserves ?? []).Single(u => u.Id == SpecialOrders.Actor(_content, _state!, pending))), 13);
            Text(parent, pending.BasicAction == "harvest" ? "Free movement → harvest. Both steps may be skipped." : pending.BasicAction != "" ? "Card and Play already spent. Choose an action target or skip the action." : CommandCards.Description(_content, _state, pending.Card), 12, _colors.Muted);
            if (SpecialOrders.Action(_content, _state, pending) == "choice") Text(parent, "Choose MOVE or ATTACK on the right, then select a target on the field.", 12, _colors.Highlight);
            if (SpecialOrders.Action(_content, _state, pending) == "launch") Text(parent, "Click a green exit hex to launch this squadron. The carrier keeps its activation state.", 12, _colors.Highlight);
            Button(parent, "SKIP ACTION", () => Submit(new("skip")), LocalCommandSide != pending.Side);
            Button(parent, "FINISH ORDER", () => Submit(new("finish-order")), LocalCommandSide != pending.Side);
            DrawCardPreview(parent, pending.Card);
            return;
        }
        int side = LocalCommandSide;
        var hand = side == 0 ? cards.Hand0 : cards.Hand1;
        if (!hand.Contains(_selectedCard) || !HeroicCommands.CanUse(_content, _state, side, _selectedCard)) _selectedCard = hand.FirstOrDefault(id => HeroicCommands.CanUse(_content, _state, side, id)) ?? "";
        Text(parent, cards.Phase == "initiative" ? "INITIATIVE / bid 0–1 card" : "HAND / basic orders", 13, _colors.Highlight);
        bool ready = side == 0 ? cards.Ready0 : cards.Ready1;
        if (cards.Phase == "initiative")
            Button(parent, ready ? "BID CONFIRMED" : "READY / NO BID", () => Submit(new("initiative")), ready);
        bool heroHeading = false;
        foreach (string id in hand.OrderBy(id => HeroicCommands.IsHeroic(_state, id)))
        {
            if (HeroicCommands.IsHeroic(_state, id) && !heroHeading)
            { heroHeading = true; Text(parent, "HEROIC COMMANDS / ONCE PER SCENARIO", 13, _colors.Highlight); Text(parent, "These cards do not count towards Hand. The matching Hero must remain in play.", 12, _colors.Muted); }
            string title = SpecialOrders.Name(_content, _state!, id);
            var button = Button(parent, cards.Phase == "initiative" ? "BID: " + title : (_selectedCard == id ? "▶ " : "") + title,
                () => { if (cards.Phase == "initiative") Submit(new("initiative", Card: id)); else { _selectedCard = id; DrawBattle(); } }, cards.Phase == "initiative" && ready || !HeroicCommands.CanUse(_content, _state, side, id));
            button.CustomMinimumSize = new(0, 30); button.AddThemeFontSizeOverride("font_size", 13);
        }
        if (cards.Phase == "orders" && SpecialOrders.Supported(_content, _state!, _selectedCard))
        {
            var cardDefinition = CommandCards.Find(_content, _state, _selectedCard)!;
            var selected = (FleetLaunchOrders.IsLaunch(cardDefinition) ? _state.Reserves ?? [] : _state.Units).Find(u => u.Id == _selected && u.Side == side && !u.Activated && !u.Hulk);
            if (cardDefinition.RepairAtStart && selected != null)
            {
                var values = new[] { "auto", "", "focus", "engines", "weapons", "bays" };
                var repair = new OptionButton();
                foreach (var value in values) repair.AddItem(value == "auto" ? "Card repair: first available token" : value == "" ? "Card repair: skip" : "Card repair: " + value);
                repair.Select(Math.Max(0, Array.IndexOf(values, _cardRepairChoice)));
                repair.ItemSelected += i => _cardRepairChoice = values[(int)i]; parent.AddChild(repair);
            }
            if (FleetBattle && SpecialOrders.Formation(_content, _state!, _selectedCard))
            {
                string[] modes = ["both", "first", "second"]; var lines = new OptionButton(); lines.AddItem("Both order lines"); lines.AddItem("First line only"); lines.AddItem("Second line only");
                lines.Select(Array.IndexOf(modes, _formationLines)); lines.ItemSelected += i => { _formationLines = modes[(int)i]; DrawBattle(); }; parent.AddChild(lines);
            }
            if (SpecialOrders.Formation(_content, _state!, _selectedCard) && selected != null && (!FleetBattle || _formationLines == "both"))
            {
                var others = _state.Units.Where(u => u.Side == side && !u.Activated && !u.Hulk && u.Id != selected.Id).ToArray();
                if (!others.Any(u => u.Id == _secondOrderUnit)) _secondOrderUnit = others.FirstOrDefault()?.Id ?? "";
                var other = new OptionButton();
                foreach (var u in others) other.AddItem("Second: " + FleetPresentation.UnitName(_content, _state, u));
                if (others.Length > 0) other.Select(Array.FindIndex(others, u => u.Id == _secondOrderUnit));
                other.ItemSelected += index => _secondOrderUnit = others[(int)index].Id; parent.AddChild(other);
            }
            var play = Button(parent, "SPECIAL ORDER", () => Submit(new("special", FleetBattle && SpecialOrders.Formation(_content, _state!, _selectedCard) && _formationLines == "second" ? "" : _selected,
                Card: _selectedCard, OtherUnit: FleetBattle && SpecialOrders.Formation(_content, _state!, _selectedCard) && _formationLines == "first" ? "" : FleetBattle && SpecialOrders.Formation(_content, _state!, _selectedCard) && _formationLines == "second" ? _selected : _secondOrderUnit,
                Repair: _repairChoice, OtherRepair: _repairOtherChoice, CardRepair: _cardRepairChoice)), selected == null || side != _state.ActiveSide || !SpecialOrders.CanAssign(_content, _state, cardDefinition, selected));
            play.CustomMinimumSize = new(0, 32);
        }
        Text(parent, $"Hand {(side == 0 ? cards.HandLimit0 : cards.HandLimit1)} / Play {(side == 0 ? cards.PlayLimit0 : cards.PlayLimit1)} · Move / Attack / Harvest", 11, _colors.Muted);
        DrawCardPreview(parent, _selectedCard);
    }
}
