using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private LoadedCardStyle[] _cardStyles = [];
    private void ReloadCardStyles() => _cardStyles = CardStyleCatalog.Discover([System.IO.Path.Combine(_root, "card-styles"), System.IO.Path.Combine(_data, "card-styles")]);
    private void CardStylesMenu()
    {
        ReloadCardStyles(); ClearScreen(); Heading("Card appearance"); var box = Panel(70, 190, 700, 490);
        Text(box, "APPEARANCE IS A SEPARATE MODULE", 18, _colors.Highlight);
        Text(box, "Text mode works without images. Appearance packs may be added or removed independently of rules, saves and gameplay mods.", 17);
        var options = new OptionButton(); options.AddItem("Text only / artwork disabled"); foreach (var pack in _cardStyles) options.AddItem(pack.Definition.Name);
        options.Select(Math.Max(0, Array.FindIndex(_cardStyles, p => p.Definition.Id == _settings.CardStyle) + 1)); box.AddChild(options);
        options.ItemSelected += i => { _settings = _settings with { CardStyle = i == 0 ? "text" : _cardStyles[(int)i - 1].Definition.Id }; SaveSettings(); CardStylesMenu(); };
        Text(box, "Local packs: .local/card-styles/<pack>/style.json. Frames from your PDFs are imported by a separate script and are not uploaded to GitHub.", 15, _colors.Muted);
        Button(box, "BACK", OptionsMenu); MenuScene();
    }
    private void DrawCardPreview(Control parent, string card)
    {
        var pack = _cardStyles.FirstOrDefault(p => p.Definition.Id == _settings.CardStyle); if (pack == null || card == "") return;
        var def = pack.Definition; var preview = new PanelContainer { CustomMinimumSize = new(0, 210) };
        string frame = pack.Frame(InterruptRules.IsAttack(_content, _state, card));
        if (frame == "") preview.AddThemeStyleboxOverride("panel", Box(def.Background, def.Accent));
        else
        {
            var image = Image.LoadFromFile(frame);
            if (image == null || image.IsEmpty()) throw new InvalidDataException("Could not read the card frame.");
            preview.AddThemeStyleboxOverride("panel", new StyleBoxTexture { Texture = ImageTexture.CreateFromImage(image) });
        }
        parent.AddChild(preview); var margin = new MarginContainer(); foreach (string edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 16); preview.AddChild(margin);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 12); margin.AddChild(body);
        Text(body, SpecialOrders.Name(_content, _state!, card), 16, def.Accent);
        Text(body, InterruptRules.IsAttack(_content, _state, card) ? "INTERRUPT" : "ORDER", 13, def.Text);
        Text(body, CommandCards.Description(_content, _state, card), 14, def.Text);
    }
}
