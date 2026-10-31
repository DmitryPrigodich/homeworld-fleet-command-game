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
        var box = Panel(70, 185, 800, 640);
        Text(box, "1. ИНИЦИАТИВА", 20, _colors.Highlight);
        Text(box, "В начале раунда рука пополняется до двух карт. Нажми «ГОТОВ / БЕЗ СТАВКИ» или потрать карту ради +1 к броску (максимум 6). При ничьей потребуются новые ставки. Победитель отдаёт первый приказ.", 17);
        Text(box, "2. ВЫБОР ПРИКАЗА", 20, _colors.Highlight);
        Text(box, "Выбери готовое подразделение и карту слева. Кнопки «ПЕРЕМЕЩЕНИЕ» и «АТАКА» используют карту как базовый приказ. Кнопка «СПЕЦПРИКАЗ» запускает её последовательность. Один приказ расходует одну карту и один Play.", 17);
        Text(box, "3. СПЕЦИАЛЬНЫЕ ДЕЙСТВИЯ", 20, _colors.Highlight);
        Text(box, "All Ahead Full: два перемещения в одном направлении. Stay in Formation: заранее назначь второй отряд; первый выполняет Move → Attack, второй — Lightning Move → Attack. Lightning Move использует Afterburners и должен закончиться рядом с первым отрядом.\n\nИгра показывает текущий шаг и выбирает нужный отряд. Можно пропустить действие или завершить приказ досрочно. Сохранение продолжит именно с этого шага.", 17);
        Button(box, "НАЧАТЬ КАРТОЧНЫЙ БОЙ", StartCardBattle);
        Button(box, "БАЗОВОЕ ОБУЧЕНИЕ", StartTutorial); MenuScene();
    }
    private void StartCardBattle() { StartBattle(BasicCardBattle.MapId, true); QueueBot(); }
    private BattleCommand WithCard(BattleCommand command)
    {
        if (_state?.Cards is not { Phase: "orders", Pending: null } cards || command.Kind is "pass" or "special") return command;
        var hand = LocalCommandSide == 0 ? cards.Hand0 : cards.Hand1;
        return command with { Card = hand.Contains(_selectedCard) ? _selectedCard : hand.FirstOrDefault() ?? "" };
    }
    private void DrawCards(Control parent)
    {
        if (_state?.Cards is not { } cards) return;
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
