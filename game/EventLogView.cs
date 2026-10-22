using Godot;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private RichTextLabel _eventLog = null!;

    private void DrawEventLog()
    {
        var bottom = Panel(30, 740, 1380, 140);
        bottom.AddThemeConstantOverride("separation", 3);
        _battleStatus = Text(bottom, _status, 13, _colors.Highlight);
        _battleStatus.AutowrapMode = TextServer.AutowrapMode.Off;
        _battleStatus.ClipText = true;
        _battleStatus.TooltipText = _status;
        Text(bottom, "ЖУРНАЛ СОБЫТИЙ  ·  зелёный — ход  ·  красный — цель  ·  Alt — названия  ·  колесо над журналом — история", 12, _colors.Muted);
        _eventLog = new RichTextLabel { BbcodeEnabled = false, ScrollActive = true, ScrollFollowing = true,
            SelectionEnabled = true, ContextMenuEnabled = true, CustomMinimumSize = new(0, 75), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _eventLog.AddThemeFontSizeOverride("normal_font_size", 14);
        _eventLog.AddThemeColorOverride("default_color", C(_colors.Text));
        bottom.AddChild(_eventLog);
        _eventLog.Text = string.Join('\n', (_state?.Log ?? []).Select(entry =>
            $"Раунд {entry.Round} · {(entry.Side < 0 ? "Бой" : SideName(entry.Side))}: {entry.Text}"));
        if (_eventLog.Text.Length == 0) _eventLog.Text = "История начнётся со следующего приказа.";
    }
}
