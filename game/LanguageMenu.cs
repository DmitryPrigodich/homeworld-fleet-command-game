using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private readonly Localizer _locale = new();
    private readonly Dictionary<(ulong Node, string Property), (string Source, string Display)> _localizedControls = [];
    private void LoadLanguages()
    {
        var directories = new List<string> { System.IO.Path.Combine(_root, "content", "locales") };
        directories.AddRange(ModDirectories().Where(d => (_settings.EnabledMods ?? []).Contains(JsonData.Read<ModManifest>(System.IO.Path.Combine(d, "mod.json")).Id)).Select(d => System.IO.Path.Combine(d, "locales")));
        directories.Add(System.IO.Path.Combine(_data, "locales")); _locale.Load(directories); _locale.SetLanguage(_settings.Language);
    }
    private void LanguageMenu()
    {
        ClearScreen(); Heading("Язык / Language"); var box = Panel(70, 180, 650, 530);
        var languages = _locale.Languages.ToArray(); var dropdown = new OptionButton { Name = "LanguagePicker" };
        foreach (var language in languages) dropdown.AddItem(language.Name + " / " + language.Code);
        dropdown.Select(Math.Max(0, Array.FindIndex(languages, p => p.Code == _locale.Language))); box.AddChild(dropdown);
        dropdown.ItemSelected += i => { _settings = _settings with { Language = languages[(int)i].Code }; _locale.SetLanguage(_settings.Language); SaveSettings(); LanguageMenu(); };
        Text(box, "Язык применяется к меню, подсказкам и журналу. Сохранения и сетевая игра от языка не зависят.", 18);
        Text(box, "Другие языки можно добавить файлом JSON в папку locales рядом с локальными сохранениями или в мод. Недостающие строки используют резервный язык.", 16, _colors.Muted);
        Button(box, "НАЗАД", OptionsMenu); MenuScene();
    }
    private void LocalizeInterface()
    {
        void Property(GodotObject obj, string property)
        {
            string current = obj.Get(property).AsString(); var key = (obj.GetInstanceId(), property);
            string source = _localizedControls.TryGetValue(key, out var old) && current == old.Display ? old.Source : current;
            string translated = _locale.Text(source); if (current != translated) obj.Set(property, translated);
            _localizedControls[key] = (source, translated);
        }
        void Visit(Node node)
        {
            if (node.IsQueuedForDeletion()) return;
            if (node is Control control)
            {
                Property(control, "tooltip_text");
                if (control is Label or Godot.Button or RichTextLabel) Property(control, "text");
                if (control is LineEdit) Property(control, "placeholder_text"); // User-entered names and paths are never translated.
                if (control is SpinBox) { Property(control, "prefix"); Property(control, "suffix"); }
                if (control is OptionButton option) for (int i = 0; i < option.ItemCount; i++)
                {
                    var key = (option.GetInstanceId(), "item:" + i); string current = option.GetItemText(i);
                    string source = _localizedControls.TryGetValue(key, out var old) && current == old.Display ? old.Source : current;
                    string translated = _locale.Text(source); if (current != translated) option.SetItemText(i, translated); _localizedControls[key] = (source, translated);
                }
            }
            foreach (Node child in node.GetChildren()) Visit(child);
        }
        Visit(_ui);
    }
}
