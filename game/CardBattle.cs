using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private string _selectedCard = "";
    private string _secondOrderUnit = "";
    private void CardGuide()
    {
        EndTutorial(); _state = null; ClearScreen(); Heading("Обучение: карты и специальные приказы");
        var box = Panel(70, 175, 1000, 660);
        Text(box, "1. ИНИЦИАТИВА", 20, _colors.Highlight);
        Text(box, "В начале раунда рука пополняется до двух карт. Нажми «ГОТОВ / БЕЗ СТАВКИ» или потрать карту ради +1 к броску (максимум 6). При ничьей потребуются новые ставки. Победитель отдаёт первый приказ.", 17);
        Text(box, "2. ВЫБОР ПРИКАЗА", 20, _colors.Highlight);
        Text(box, "Выбери готовое подразделение и карту слева. Кнопки «ПЕРЕМЕЩЕНИЕ» и «АТАКА» используют карту как базовый приказ. Кнопка «СПЕЦПРИКАЗ» запускает её последовательность. Один приказ расходует одну карту и один Play.", 17);
        Text(box, "3. СПЕЦИАЛЬНЫЕ ДЕЙСТВИЯ", 20, _colors.Highlight);
        Text(box, "All Ahead Full: два перемещения в одном направлении. Stay in Formation: заранее назначь второй отряд; первый выполняет Move → Attack, второй — Lightning Move → Attack. Lightning Move использует Afterburners и должен закончиться рядом с первым отрядом.\n\nИгра показывает текущий шаг и выбирает нужный отряд. Можно пропустить действие или завершить приказ досрочно. Сохранение продолжит именно с этого шага.", 17);
        Text(box, "4. ОТВЕТНЫЙ ОГОНЬ", 20, _colors.Highlight);
        Text(box, "Interrupt Attack: в окне ответа выбери свой отряд и нажми на красную цель — действующий отряд противника. Можно ответить уже активированным отрядом, без расхода Play, один раз за раунд. «ПРОПУСТИТЬ ОКНО» продолжает чужое действие. В движении ответ возможен после входа в свободный гекс, в атаке — до бросков.", 17);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 10); box.AddChild(actions);
        Button(actions, "ТРЕНИРОВКА: ПРЕРЫВАНИЕ", StartInterruptLesson);
        Button(actions, "НАЧАТЬ КАРТОЧНЫЙ БОЙ", StartCardBattle);
        Button(actions, "БАЗОВОЕ ОБУЧЕНИЕ", StartTutorial); MenuScene();
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
        _status = "Урок: враг объявил атаку. Нажми на красную цель, чтобы ответить уже активированными перехватчиками №1. Сохранения основной игры не затрагиваются.";
        DrawBattle();
    }
    private BattleCommand WithCard(BattleCommand command)
    {
        if (_state?.Cards?.Response != null && command.Kind == "attack")
        {
            var responseHand = LocalCommandSide == 0 ? _state.Cards.Hand0 : _state.Cards.Hand1;
            return command with { Kind = "interrupt", Card = responseHand.Contains(_selectedCard) && InterruptRules.IsAttack(_selectedCard) ? _selectedCard : responseHand.FirstOrDefault(InterruptRules.IsAttack) ?? "" };
        }
        if (_state?.Cards is not { Phase: "orders", Pending: null } cards || command.Kind is "pass" or "special") return command;
        var hand = LocalCommandSide == 0 ? cards.Hand0 : cards.Hand1;
        return command with { Card = hand.Contains(_selectedCard) ? _selectedCard : hand.FirstOrDefault() ?? "" };
    }
    private void DrawCards(Control parent)
    {
        if (_state?.Cards is not { } cards) return;
        if (cards.Response is { } pause)
        {
            int responder = 1 - pause.Side;
            Text(parent, "ОТВЕТНЫЙ ОГОНЬ", 15, _colors.Highlight);
            Text(parent, "Цель: " + FleetPresentation.UnitName(_content, _state, _state.Units.Single(u => u.Id == pause.Command.Unit)), 13);
            if (LocalCommandSide != responder) { Text(parent, "Ожидается ответ: " + SideName(responder), 14); return; }
            var responseHand = (responder == 0 ? cards.Hand0 : cards.Hand1).Where(InterruptRules.IsAttack).ToArray();
            if (!responseHand.Contains(_selectedCard)) _selectedCard = responseHand.FirstOrDefault() ?? "";
            Text(parent, responseHand.Length == 0 ? "Карты Interrupt Attack нет. Пропусти окно ответа." : "Выбери свой отряд и нажми на красную цель. Уже действовавшие отряды тоже могут ответить. Play не тратится.", 13);
            foreach (var id in responseHand)
            {
                string card = id;
                var b = Button(parent, (_selectedCard == card ? "▶ " : "") + "Interrupt Attack", () => { _selectedCard = card; DrawBattle(); });
                b.CustomMinimumSize = new(0, 30); b.AddThemeFontSizeOverride("font_size", 13);
            }
            Button(parent, "ПРОПУСТИТЬ ОКНО", () => Submit(new("decline")));
            Text(parent, "1 Interrupt Attack за раунд. После ответа приказ продолжится.", 12, _colors.Muted);
            return;
        }
        if (cards.Pending is { } pending)
        {
            Text(parent, SpecialOrders.Name(pending.Card), 15, _colors.Highlight);
            Text(parent, $"Действие {pending.Step + 1}/{SpecialOrders.Steps(pending)}: " + SpecialOrders.ActionName(pending) + "\n" +
                FleetPresentation.UnitName(_content, _state, _state.Units.Single(u => u.Id == SpecialOrders.Actor(pending))), 13);
            Text(parent, SpecialOrders.Formation(pending.Card) ? "Move → Attack / Afterburners → Attack\nLightning Move: закончить рядом с первым отрядом." : "Два Move в одном направлении.\nКаждое действие можно пропустить.", 12, _colors.Muted);
            Button(parent, "ПРОПУСТИТЬ ДЕЙСТВИЕ", () => Submit(new("skip")), LocalCommandSide != pending.Side);
            Button(parent, "ЗАВЕРШИТЬ ПРИКАЗ", () => Submit(new("finish-order")), LocalCommandSide != pending.Side);
            return;
        }
        int side = LocalCommandSide;
        var hand = side == 0 ? cards.Hand0 : cards.Hand1;
        if (!hand.Contains(_selectedCard)) _selectedCard = hand.FirstOrDefault() ?? "";
        Text(parent, cards.Phase == "initiative" ? "ИНИЦИАТИВА / ставка 0–1 карта" : "РУКА / базовые приказы", 13, _colors.Highlight);
        bool ready = side == 0 ? cards.Ready0 : cards.Ready1;
        if (cards.Phase == "initiative")
            Button(parent, ready ? "СТАВКА ПОДТВЕРЖДЕНА" : "ГОТОВ / БЕЗ СТАВКИ", () => Submit(new("initiative")), ready);
        foreach (string id in hand)
        {
            string title = SpecialOrders.Name(id);
            var button = Button(parent, cards.Phase == "initiative" ? "СТАВКА: " + title : (_selectedCard == id ? "▶ " : "") + title,
                () => { if (cards.Phase == "initiative") Submit(new("initiative", Card: id)); else { _selectedCard = id; DrawBattle(); } }, cards.Phase == "initiative" && ready);
            button.CustomMinimumSize = new(0, 30); button.AddThemeFontSizeOverride("font_size", 13);
        }
        if (cards.Phase == "orders" && SpecialOrders.Supported(_selectedCard))
        {
            var selected = _state.Units.Find(u => u.Id == _selected && u.Side == side && !u.Activated);
            if (SpecialOrders.Formation(_selectedCard) && selected != null)
            {
                var others = _state.Units.Where(u => u.Side == side && !u.Activated && u.Id != selected.Id).ToArray();
                if (!others.Any(u => u.Id == _secondOrderUnit)) _secondOrderUnit = others.FirstOrDefault()?.Id ?? "";
                var other = new OptionButton();
                foreach (var u in others) other.AddItem("Второй: " + FleetPresentation.UnitName(_content, _state, u));
                if (others.Length > 0) other.Select(Array.FindIndex(others, u => u.Id == _secondOrderUnit));
                other.ItemSelected += index => _secondOrderUnit = others[(int)index].Id; parent.AddChild(other);
            }
            var play = Button(parent, "СПЕЦПРИКАЗ", () => Submit(new("special", _selected, Card: _selectedCard, OtherUnit: _secondOrderUnit)), selected == null || side != _state.ActiveSide);
            play.CustomMinimumSize = new(0, 32);
        }
        Text(parent, "Рука 2 / Play 1 · Move/Attack = базовый приказ", 11, _colors.Muted);
    }
}
