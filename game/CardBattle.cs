using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private string _selectedCard = "";
    private void StartCardBattle() { StartBattle(BasicCardBattle.MapId, true); QueueBot(); }
    private BattleCommand WithCard(BattleCommand command)
    {
        if (_state?.Cards is not { Phase: "orders" } cards || command.Kind == "pass") return command;
        var hand = LocalCommandSide == 0 ? cards.Hand0 : cards.Hand1;
        return command with { Card = hand.Contains(_selectedCard) ? _selectedCard : hand.FirstOrDefault() ?? "" };
    }
    private void DrawCards(Control parent)
    {
        if (_state?.Cards is not { } cards) return;
        int side = LocalCommandSide;
        var hand = side == 0 ? cards.Hand0 : cards.Hand1;
        Text(parent, cards.Phase == "initiative" ? "ИНИЦИАТИВА / ставка 0–1 карта" : "РУКА / базовые приказы", 13, _colors.Highlight);
        bool ready = side == 0 ? cards.Ready0 : cards.Ready1;
        if (cards.Phase == "initiative")
            Button(parent, ready ? "СТАВКА ПОДТВЕРЖДЕНА" : "ГОТОВ / БЕЗ СТАВКИ", () => Submit(new("initiative")), ready);
        foreach (string id in hand)
        {
            string title = "Приказ " + id[6..];
            var button = Button(parent, cards.Phase == "initiative" ? "СТАВКА: " + title : (_selectedCard == id ? "▶ " : "") + title,
                () => { if (cards.Phase == "initiative") Submit(new("initiative", Card: id)); else { _selectedCard = id; DrawBattle(); } }, cards.Phase == "initiative" && ready);
            button.CustomMinimumSize = new(0, 30); button.AddThemeFontSizeOverride("font_size", 13);
        }
        Text(parent, "Рука 2 / Приказов 1 за раунд\nБез специальных эффектов карт", 12, _colors.Muted);
    }
}
