using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private readonly HashSet<string> _phaseCards = [];
    private string _phaseKey = "", _weaponChoice = "", _repairChoice = "ask", _repairOtherChoice = "ask";
    private int _facingChoice = -1;
    private bool FleetBattle => _state != null && FleetRuleTools.Enabled(_content, _state);
    private bool FleetDecision => _state?.Combat != null || _state?.Repairs is { Length: > 0 };
    private bool BotExpected => _state!.Repairs is { Length: > 0 } ? _state.Repairs[0].Side == 1 : _state.Combat is { } combat ? combat.Side == 1 : _state.Cards?.Response != null ? InterruptRules.Responder(_state) == 1
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
            Text(parent, combat.Rerolled ? "Переброс уже использован." : "Можно принять результат или перебросить все кубики.", 13);
            if (LocalCommandSide == combat.Side)
            {
                Button(parent, "ПРИНЯТЬ БРОСОК", () => Submit(new("resolve")));
                if (FleetCombat.CanValor(_state, combat.Side)) Button(parent, "ПЕРЕБРОС: VALOR", () => Submit(new("valor")));
                if (FleetCombat.CanFaction(_content, _state, combat)) Button(parent, "ПЕРЕБРОС: ТАИИДАН", () => Submit(new("faction-reroll")));
            }
            else Text(parent, "Ожидается решение атакующего.", 13);
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
    }
}
