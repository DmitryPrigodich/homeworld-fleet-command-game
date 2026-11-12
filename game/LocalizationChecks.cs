using Godot;
using System.Text.RegularExpressions;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private async Task CheckLocalization()
    {
        try
        {
            void Require(bool ok, string message) { if (!ok) throw new Exception(message); GD.Print("LOCALE_UI_PASS " + message); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            LanguageMenu(); await Frames(); var picker = Nodes(_ui).OfType<OptionButton>().Single(o => o.Name == "LanguagePicker");
            int en = Array.FindIndex(_locale.Languages.ToArray(), p => p.Code == "en"); picker.Select(en); picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)en); await Frames();
            Require(_locale.Language == "en" && JsonData.Read<AppSettings>(System.IO.Path.Combine(_data, "settings.json")).Language == "en", "Language choice persists");
            foreach (Action menu in new Action[] { MainMenu, OptionsMenu, PlayerMenu, SinglePlayerMenu, ScenarioMenu, NetworkMenu, () => ForceMenu() })
            {
                menu(); await Frames();
                var strings = Nodes(_ui).OfType<Control>().SelectMany(n => n switch { Label l => new[] { l.Text }, Godot.Button b => new[] { b.Text }, _ => Array.Empty<string>() });
                Require(!strings.Any(t => Regex.IsMatch(t, "[А-Яа-яЁё]")), "English screen: " + menu.Method.Name);
            }
            _settings = _settings with { Player = FleetPresentation.Normalize(_settings.Player) with { Name = "Дмитрий" } }; PlayerMenu(); await Frames();
            Require(Nodes(_ui).OfType<LineEdit>().Any(l => l.Text == "Дмитрий"), "User-entered name remains unchanged");
            _settings = _settings with { Player = FleetPresentation.Normalize(null) }; StartBattle("core:first-contact", false, persist: false); await Frames();
            string snapshot = _engine.Save(_state!); Require(!Regex.IsMatch(_eventLog.Text, "[А-Яа-яЁё]"), "Event log is translated at display time");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "first-contact-en.png"));
            _settings = _settings with { Language = "ru" }; _locale.SetLanguage("ru"); LocalizeInterface(); await Frames();
            Require(_engine.Save(_state!) == snapshot && _eventLog.Text.Contains("Раунд"), "Switching language keeps the same battle and restores Russian log");
            GD.Print("LOCALIZATION_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
