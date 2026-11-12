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
            _settings = _settings with { Player = FleetPresentation.Normalize(null) };
            LanguageMenu(); await Frames(); var picker = Nodes(_ui).OfType<OptionButton>().Single(o => o.Name == "LanguagePicker");
            int en = Array.FindIndex(_locale.Languages.ToArray(), p => p.Code == "en"); picker.Select(en); picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)en); await Frames();
            Require(_locale.Language == "en" && JsonData.Read<AppSettings>(System.IO.Path.Combine(_data, "settings.json")).Language == "en", "Language choice persists");
            string localDirectory = System.IO.Path.Combine(_data, "locales"), overrideFile = System.IO.Path.Combine(localDirectory, "english-ui-check.json");
            System.IO.Directory.CreateDirectory(localDirectory);
            try
            {
                System.IO.File.WriteAllText(overrideFile, System.Text.Json.JsonSerializer.Serialize(new LanguagePack("en", "English", new() { ["OPTIONS"] = "Preferences" }, ""), JsonData.Options));
                Nodes(_ui).OfType<Godot.Button>().Single(b => b.Text == "RELOAD LANGUAGES").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
                MainMenu(); await Frames(); Require(Nodes(_ui).OfType<Godot.Button>().Any(b => b.Text == "Preferences"), "English caption reloads from JSON without recompilation");
            }
            finally { System.IO.File.Delete(overrideFile); LoadLanguages(); }
            foreach (Action menu in new Action[] { MainMenu, OptionsMenu, PlayerMenu, SinglePlayerMenu, ScenarioMenu, NetworkMenu, () => ForceMenu() })
            {
                menu(); await Frames();
                var strings = Nodes(_ui).OfType<Control>().SelectMany(n => n switch { Label l => new[] { l.Text }, Godot.Button b => new[] { b.Text }, _ => Array.Empty<string>() });
                Require(!strings.Any(t => Regex.IsMatch(t, @"\p{IsCyrillic}")), "English screen: " + menu.Method.Name);
            }
            _settings = _settings with { Player = FleetPresentation.Normalize(_settings.Player) with { Name = "\u0414\u043c\u0438\u0442\u0440\u0438\u0439" } }; PlayerMenu(); await Frames();
            Require(Nodes(_ui).OfType<LineEdit>().Any(l => l.Text == "\u0414\u043c\u0438\u0442\u0440\u0438\u0439"), "User-entered name remains unchanged");
            _settings = _settings with { Player = FleetPresentation.Normalize(null) }; StartBattle("core:first-contact", false, persist: false); await Frames();
            string snapshot = _engine.Save(_state!); Require(!Regex.IsMatch(_eventLog.Text, @"\p{IsCyrillic}"), "Event log is translated at display time");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "first-contact-en.png"));
            _settings = _settings with { Language = "ru" }; _locale.SetLanguage("ru"); LocalizeInterface(); await Frames();
            Require(_engine.Save(_state!) == snapshot && _eventLog.Text.Contains("\u0420\u0430\u0443\u043d\u0434"), "Switching language keeps the same battle and restores Russian log");
            GD.Print("LOCALIZATION_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
