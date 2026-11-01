using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private LoadedCardStyle[] _cardStyles = [];
    private void ReloadCardStyles() => _cardStyles = CardStyleCatalog.Discover([System.IO.Path.Combine(_root, "card-styles"), System.IO.Path.Combine(_data, "card-styles")]);
    private void CardStylesMenu()
    {
        ReloadCardStyles(); ClearScreen(); Heading("Оформление карточек"); var box = Panel(70, 190, 700, 490);
        Text(box, "ОФОРМЛЕНИЕ — ОТДЕЛЬНЫЙ МОДУЛЬ", 18, _colors.Highlight);
        Text(box, "Текстовый режим работает без изображений. Пакеты оформления можно добавлять и удалять независимо от правил, сохранений и сетевого набора модов.", 17);
        var options = new OptionButton(); options.AddItem("Только текст / оформление отключено"); foreach (var pack in _cardStyles) options.AddItem(pack.Definition.Name);
        options.Select(Math.Max(0, Array.FindIndex(_cardStyles, p => p.Definition.Id == _settings.CardStyle) + 1)); box.AddChild(options);
        options.ItemSelected += i => { _settings = _settings with { CardStyle = i == 0 ? "text" : _cardStyles[(int)i - 1].Definition.Id }; SaveSettings(); CardStylesMenu(); };
        Text(box, "Локальные пакеты: .local/card-styles/<пакет>/style.json. Рамки из твоих PDF импортируются отдельным скриптом; в GitHub они не попадают.", 15, _colors.Muted);
        Button(box, "НАЗАД", OptionsMenu); MenuScene();
    }
    private void DrawCardPreview(Control parent, string card)
    {
        var pack = _cardStyles.FirstOrDefault(p => p.Definition.Id == _settings.CardStyle); if (pack == null || card == "") return;
        var def = pack.Definition; var preview = new PanelContainer { CustomMinimumSize = new(0, 210) };
        string frame = pack.Frame(InterruptRules.IsAttack(card));
        if (frame == "") preview.AddThemeStyleboxOverride("panel", Box(def.Background, def.Accent));
        else
        {
            var image = Image.LoadFromFile(frame);
            if (image == null || image.IsEmpty()) throw new InvalidDataException("Не удалось прочитать рамку карточки.");
            preview.AddThemeStyleboxOverride("panel", new StyleBoxTexture { Texture = ImageTexture.CreateFromImage(image) });
        }
        parent.AddChild(preview); var margin = new MarginContainer(); foreach (string edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 16); preview.AddChild(margin);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 12); margin.AddChild(body);
        Text(body, SpecialOrders.Name(card), 16, def.Accent);
        Text(body, InterruptRules.IsAttack(card) ? "ПРЕРЫВАНИЕ" : "ПРИКАЗ", 13, def.Text);
        Text(body, InterruptRules.IsAttack(card) ? "Ответный выстрел по действующему врагу." : SpecialOrders.Formation(card) ? "Move → Attack\nLightning Move → Attack" : SpecialOrders.Supported(card) ? "Move → Move\nПо одной прямой." : "Move / Attack", 14, def.Text);
    }
}
