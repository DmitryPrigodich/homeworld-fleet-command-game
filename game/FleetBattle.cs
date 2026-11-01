using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private readonly HashSet<string> _phaseCards = [];
    private readonly HashSet<int> _rerollDice = [];
    private int _rerollSequence = -1;
    private string _phaseKey = "", _weaponChoice = "", _repairChoice = "ask", _repairOtherChoice = "ask", _attackKindChoice = "ranged", _transferTarget = "";
    private int _transferCrew;
    private bool _transferLeader;
    private int _facingChoice = -1;
    private bool FleetBattle => _state != null && FleetRuleTools.Enabled(_content, _state);
    private bool FleetDecision => _state?.Combat != null || _state?.Defense != null || _state?.Overrun != null || _state?.Repairs is { Length: > 0 };
    private BattleCommand? BotAttack(BattleUnit unit, string target, bool interrupt = false)
    {
        string card = interrupt ? _state!.Cards!.Hand1.FirstOrDefault(InterruptRules.IsAttack) ?? "" : _state?.Cards?.Hand1.FirstOrDefault() ?? "";
        if (!FleetBattle) return new(interrupt ? "interrupt" : "attack", unit.Id, target, Card: card);
        foreach (string kind in new[] { "ranged", "board", "ram" })
        {
            var command = new BattleCommand(interrupt ? "interrupt" : "attack", unit.Id, target, Card: card, AttackKind: kind);
            if (FleetCombat.Allowed(_content, _state!, unit, _state!.Units.Find(u => u.Id == target), command)) return command;
        }
        return null;
    }
    private bool BotExpected => _state!.Overrun is { } overrun ? overrun.Side == 1 : _state.Repairs is { Length: > 0 } ? _state.Repairs[0].Side == 1 : _state.Combat is { } combat ? FleetCombat.DecisionSide(combat) == 1 : _state.Defense is { } defense ? defense.Side == 0 : _state.Cards?.Response != null ? InterruptRules.Responder(_state) == 1
        : _state.Cards?.Phase is "initiative" or "cleanup" ? !_state.Cards.Ready1 : _state.ActiveSide == 1;
    private VBoxContainer ScrollBattlePanel(VBoxContainer outer)
    {
        if (!FleetBattle && !_cardStyles.Any(p => p.Definition.Id == _settings.CardStyle)) return outer;
        var scroll = new ScrollContainer { CustomMinimumSize = new(0, 575), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        outer.AddChild(scroll); var inner = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; inner.AddThemeConstantOverride("separation", 8); scroll.AddChild(inner); return inner;
    }
    private bool DrawFleetPhase(Control parent)
    {
        if (!FleetBattle) return false;
        if (_state!.Overrun is { } overrun)
        {
            Text(parent, "ПРОДВИЖЕНИЕ ПОСЛЕ АТАКИ", 15, _colors.Highlight);
            Text(parent, "Занять освобождённый гекс или остаться на месте?", 14);
            if (LocalCommandSide == overrun.Side) { Button(parent, "ЗАНЯТЬ ГЕКС", () => Submit(new("overrun"))); Button(parent, "ОСТАТЬСЯ НА МЕСТЕ", () => Submit(new("decline-overrun"))); }
            return true;
        }
        if (_state!.Repairs is { Length: > 0 })
        {
            var repair = _state.Repairs[0]; var unit = _state.Units.Single(u => u.Id == repair.Unit);
            Text(parent, "DAMAGE CONTROL", 16, _colors.Highlight);
            Text(parent, FleetPresentation.UnitName(_content, _state, unit) + $"\nБроски [{string.Join(", ", repair.Rolls)}]\nОсталось успехов: {repair.Successes}", 14);
            if (LocalCommandSide == repair.Side)
            {
                if (unit.FocusFire > 0) Button(parent, "СНЯТЬ FOCUS FIRE", () => Submit(new("repair-choice", Repair: "focus")));
                foreach (string effect in (unit.Effects ?? []).Where(FleetRuleTools.Debuffs.Contains))
                { string e = effect; Button(parent, "СНЯТЬ: " + e, () => Submit(new("repair-choice", Repair: e))); }
                Button(parent, "ЗАВЕРШИТЬ РЕМОНТ", () => Submit(new("repair-stop")));
            }
            return true;
        }
        if (_state!.Combat is { } combat)
        {
            Text(parent, "РЕЗУЛЬТАТ БРОСКА", 16, _colors.Highlight);
            Text(parent, "Атакует: " + SideName(combat.Side) + "\n[" + string.Join(", ", combat.Rolls) + "]", 15);
            if (combat.AbilityRerolls > 0)
            {
                if (_rerollSequence != _state.Sequence) { _rerollDice.Clear(); _rerollSequence = _state.Sequence; }
                int abilitySide = FleetCombat.DecisionSide(combat);
                Text(parent, $"{(abilitySide == combat.Side ? "Rapid Fire" : "Evasive")}: до {combat.AbilityRerolls} кубиков. Выбирает {SideName(abilitySide)}.", 13);
                if (LocalCommandSide == abilitySide)
                {
                    foreach (int index in FleetCombat.EligibleDice(_content, _state))
                    {
                        int i = index; var choice = new CheckBox { Text = $"Кубик {i + 1}: {combat.Rolls[i]}", ButtonPressed = _rerollDice.Contains(i) };
                        choice.Toggled += on => { if (on) _rerollDice.Add(i); else _rerollDice.Remove(i); }; parent.AddChild(choice);
                    }
                    Button(parent, "ПРИМЕНИТЬ ПЕРЕБРОСЫ", () => Submit(new("ability-reroll", Dice: _rerollDice.Order().ToArray())));
                }
                return true;
            }
            Text(parent, combat.Rerolled ? "Переброс уже использован." : "Можно принять результат или перебросить все кубики.", 13);
            if (LocalCommandSide == combat.Side)
            {
                Button(parent, "ПРИНЯТЬ БРОСОК", () => Submit(new("resolve")));
                if (FleetCombat.CanCapture(_content, _state)) Button(parent, "ЗАХВАТИТЬ ЦЕЛЬ", () => Submit(new("capture-result")));
                if (FleetCombat.CanValor(_state, combat.Side)) Button(parent, "ПЕРЕБРОС: VALOR", () => Submit(new("valor")));
                if (FleetCombat.CanFaction(_content, _state, combat)) Button(parent, "ПЕРЕБРОС: ТАИИДАН", () => Submit(new("faction-reroll")));
                if (combat.Kind == "ram" && FleetRuleTools.Class(_content.Ships[FleetCombat.Actor(_state, combat).Definition]) == "fighter")
                {
                    Text(parent, "При успешном таране: " + combat.Effect, 13);
                    foreach (string effect in FleetRuleTools.Debuffs) { string e = effect; Button(parent, "ПОВРЕЖДЕНИЕ: " + e, () => Submit(new("cripple-choice", Effect: e))); }
                }
            }
            else Text(parent, "Ожидается решение атакующего.", 13);
            return true;
        }
        if (_state.Defense is { } defense)
        {
            var defender = _state.Units.Single(u => u.Id == defense.Attack.Target);
            Text(parent, "ЗАЩИТНЫЙ ОГОНЬ", 16, _colors.Highlight);
            Text(parent, FleetPresentation.UnitName(_content, _state, defender) + "\nУгроза: " + defense.Threat + "\nЗащита до основного броска. Карта и Play не расходуются.", 14);
            if (LocalCommandSide == 1 - defense.Side)
            {
                foreach (var weapon in FleetDefense.Weapons(_content, defender))
                {
                    string id = weapon.Id;
                    Button(parent, "ЗАЩИТА: " + weapon.Name + $" · {FleetDefense.Dice(_content, _state, weapon)}D", () => Submit(new("defend", Weapon: id)));
                }
                Button(parent, "ПРОПУСТИТЬ ЗАЩИТУ", () => Submit(new("decline-defense")));
            }
            else Text(parent, "Ожидается решение защищающегося.", 13);
            return true;
        }
        var cards = _state.Cards!;
        if (cards.Phase is not ("initiative" or "cleanup")) return false;
        int side = LocalCommandSide; var hand = side == 0 ? cards.Hand0 : cards.Hand1; bool ready = side == 0 ? cards.Ready0 : cards.Ready1;
        string key = $"{_state.Round}:{cards.Phase}:{side}"; if (_phaseKey != key) { _phaseCards.Clear(); _phaseKey = key; }
        _phaseCards.IntersectWith(hand);
        Text(parent, cards.Phase == "initiative" ? "ИНИЦИАТИВА" : "ОЧИСТКА РУКИ", 16, _colors.Highlight);
        Text(parent, cards.Phase == "initiative" ? "Отметь карты: +1 за каждую, итог не выше 6." : "Отметь карты для сброса. Затем рука пополнится до нового лимита.", 13);
        foreach (var card in hand)
        {
            string id = card; var check = new CheckBox { Text = SpecialOrders.Name(id), ButtonPressed = _phaseCards.Contains(id), Disabled = ready };
            check.AddThemeFontSizeOverride("font_size", 13); parent.AddChild(check);
            check.Toggled += on => { if (on) _phaseCards.Add(id); else _phaseCards.Remove(id); };
        }
        Button(parent, ready ? "ГОТОВНОСТЬ ПОДТВЕРЖДЕНА" : cards.Phase == "initiative" ? "ПОДТВЕРДИТЬ СТАВКУ" : "ПОДТВЕРДИТЬ СБРОС", () => Submit(new(cards.Phase == "initiative" ? "initiative" : "cleanup", Cards: _phaseCards.ToArray())), ready);
        Text(parent, $"Рука: {(side == 0 ? cards.HandLimit0 : cards.HandLimit1)} · Приказы: {(side == 0 ? cards.PlayLimit0 : cards.PlayLimit1)}", 12, _colors.Muted);
        return true;
    }
    private void ChooseBattleAction(string action)
    {
        if (FleetBattle && _state!.Cards is { Phase: "orders", Pending: null, Response: null } && _state.Combat == null)
            Submit(new("begin-order", _selected, action, Repair: _repairChoice));
        else SetBattleAction(action);
    }
    private void DrawFleetUnitOptions(Control parent, BattleUnit? unit)
    {
        if (!FleetBattle || unit == null || FleetDecision) return;
        string[] kinds = ["ranged", "ram", "board"];
        var kind = new OptionButton(); kind.AddItem("Атака: стрельба"); kind.AddItem("Атака: таран"); kind.AddItem("Атака: абордаж");
        kind.Select(Math.Max(0, Array.IndexOf(kinds, _attackKindChoice))); kind.ItemSelected += i => { _attackKindChoice = kinds[(int)i]; _action = "attack"; DrawBattle(); }; parent.AddChild(kind);
        var weapons = FleetWeapons.Weapons(_content, _content.Ships[unit.Definition]);
        if (!weapons.Any(w => w.Id == _weaponChoice)) _weaponChoice = weapons.FirstOrDefault()?.Id ?? "";
        if (weapons.Length > 0)
        {
            var dropdown = new OptionButton(); foreach (var w in weapons) dropdown.AddItem($"{w.Name} · {w.DicePerModel}D/{w.Range}");
            dropdown.Select(Array.FindIndex(weapons, w => w.Id == _weaponChoice)); dropdown.ItemSelected += i => { _weaponChoice = weapons[(int)i].Id; DrawBattle(); }; parent.AddChild(dropdown);
        }
        var facing = new OptionButton(); facing.AddItem("Курс: по маршруту"); for (int i = 0; i < 6; i++) facing.AddItem("Курс: " + (i * 60) + "°");
        facing.Select(_facingChoice + 1); facing.ItemSelected += i => { _facingChoice = (int)i - 1; DrawBattle(); }; parent.AddChild(facing);
        if (_state!.Cards?.Response == null)
            Button(parent, "ПОВОРОТ НА МЕСТЕ", () => Submit(new("move", unit.Id, Q: unit.Q, R: unit.R, Facing: _facingChoice, AutoFacing: false)), _facingChoice < 0 || unit.Side != LocalCommandSide);
        string[] repairs = ["ask", "", "focus", "engines", "weapons", "bays"];
        var repair = new OptionButton(); foreach (string r in repairs) repair.AddItem(r == "ask" ? "Ремонт: выбрать после броска" : r == "" ? "Damage Control: не делать" : "Damage Control: " + r);
        repair.Select(Math.Max(0, Array.IndexOf(repairs, _repairChoice))); repair.ItemSelected += i => { _repairChoice = repairs[(int)i]; DrawBattle(); }; parent.AddChild(repair);
        if (_state.Cards is { Pending: null, Response: null } && SpecialOrders.Formation(_selectedCard))
        {
            var other = new OptionButton(); foreach (string r in repairs) other.AddItem(r == "" ? "Второй: без Damage Control" : "Второй ремонт: " + r);
            other.Select(Math.Max(0, Array.IndexOf(repairs, _repairOtherChoice))); other.ItemSelected += i => _repairOtherChoice = repairs[(int)i]; parent.AddChild(other);
        }
        if (_state.Cards?.Pending is { } order && SpecialOrders.Action(order) == "attack" && FleetRuleTools.Has(unit, "weapons"))
            Button(parent, "РЕМОНТ ВМЕСТО АТАКИ", () => Submit(new("damage-control", Repair: _repairChoice)), _repairChoice == "");
        if (_content.Ships[unit.Definition].Faction == "kushan")
            Button(parent, "СПОСОБНОСТЬ КУШАН", () => Submit(new("faction", unit.Id, Repair: _repairChoice)), _repairChoice is "" or "ask" || _state.Cards?.Pending != null || _state.Cards?.Response != null || (unit.Side == 0 ? _state.FactionUsed0 : _state.FactionUsed1));
        Text(parent, $"Класс: {FleetRuleTools.Class(_content.Ships[unit.Definition])}\nЭффекты: {string.Join(", ", unit.Effects ?? [])}\nCrew Damage: {unit.CrewDamage}", 12, _colors.Muted);
        DrawCrewTransfer(parent, unit);
    }
    private void DrawCrewTransfer(Control parent, BattleUnit unit)
    {
        if (unit.Side != LocalCommandSide || unit.Activated || unit.Hulk || _state!.Cards is not { Phase: "orders", Pending: null, Response: null }) return;
        var targets = _state.Units.Where(u => u.Id != unit.Id && (u.Side == unit.Side || u.Hulk) && new Hex(unit.Q, unit.R).Distance(new(u.Q, u.R)) == 1).ToArray();
        if (targets.Length == 0) return;
        if (!targets.Any(u => u.Id == _transferTarget)) _transferTarget = targets[0].Id;
        var target = targets.Single(u => u.Id == _transferTarget);
        Text(parent, "ЭКИПАЖ И ЛИДЕРЫ", 14, _colors.Highlight);
        var dropdown = new OptionButton(); foreach (var t in targets) dropdown.AddItem(FleetPresentation.UnitName(_content, _state, t));
        dropdown.Select(Array.FindIndex(targets, t => t.Id == _transferTarget)); dropdown.ItemSelected += i => { _transferTarget = targets[(int)i].Id; DrawBattle(); }; parent.AddChild(dropdown);
        int maximum = Math.Max(0, Math.Min(target.CrewDamage, _content.Ships[unit.Definition].Armor - unit.CrewDamage));
        _transferCrew = Math.Clamp(_transferCrew, 0, maximum);
        var amount = new SpinBox { MinValue = 0, MaxValue = maximum, Step = 1, Value = _transferCrew, Prefix = "Crew Damage цели -" };
        amount.ValueChanged += n => _transferCrew = (int)n; parent.AddChild(amount);
        var leader = new CheckBox { Text = "Передать лидера", ButtonPressed = _transferLeader, Disabled = unit.Leader == "" || target.Leader != "" };
        leader.Toggled += on => _transferLeader = on; parent.AddChild(leader);
        Button(parent, "ПЕРЕДАТЬ ЭКИПАЖ / ЛИДЕРА", () => Submit(new("transfer-crew", unit.Id, _transferTarget, Amount: _transferCrew, TransferLeader: _transferLeader, Repair: _repairChoice)));
    }
}
