using System.Text.RegularExpressions;
namespace FleetCommand.Core;

public sealed record LanguagePack(string Code, string Name, Dictionary<string, string> Messages, string Fallback = "ru");

// Presentation only: source messages stay unchanged in authoritative saves and snapshots.
public sealed class Localizer
{
    private readonly Dictionary<string, LanguagePack> _packs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);
    private Dictionary<string, string> _messages = new(StringComparer.OrdinalIgnoreCase);
    private Regex? _phrases;
    public string Language { get; private set; } = "ru";
    public IEnumerable<LanguagePack> Languages => _packs.Values.OrderBy(p => p.Code, StringComparer.Ordinal);
    public void Load(IEnumerable<string> directories)
    {
        _packs.Clear();
        foreach (string dir in directories.Where(Directory.Exists)) foreach (string file in Directory.GetFiles(dir, "*.json").Order(StringComparer.Ordinal))
        {
            var p = JsonData.Read<LanguagePack>(file);
            if (p.Code == null || !Regex.IsMatch(p.Code, "^[a-z]{2,3}(-[a-zA-Z0-9]{2,8})?$") || string.IsNullOrWhiteSpace(p.Name) || p.Messages == null || p.Fallback == null
                || p.Messages.Any(m => string.IsNullOrWhiteSpace(m.Key) || m.Value == null) || p.Messages.Count > 20000) throw new InvalidDataException("Invalid language pack: " + Path.GetFileName(file));
            var merged = _packs.TryGetValue(p.Code, out var old) ? new Dictionary<string, string>(old.Messages, StringComparer.Ordinal) : new(StringComparer.Ordinal);
            foreach (var message in p.Messages) merged[message.Key] = message.Value;
            _packs[p.Code] = p with { Messages = merged };
        }
        SetLanguage(Language);
    }
    public void SetLanguage(string? language)
    {
        Language = language != null && _packs.ContainsKey(language) ? language : "ru";
        _messages = new(StringComparer.OrdinalIgnoreCase); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string code)
        {
            if (!seen.Add(code)) throw new InvalidDataException("Language fallback cycle.");
            if (!_packs.TryGetValue(code, out var p)) return;
            if (p.Fallback != "" && !string.Equals(p.Fallback, code, StringComparison.OrdinalIgnoreCase)) Add(p.Fallback);
            foreach (var message in p.Messages)
                if (message.Key == message.Value) _messages.Remove(message.Key); else _messages[message.Key] = message.Value;
        }
        Add(Language); _cache.Clear();
        string Pattern(string key) => (char.IsLetter(key[0]) ? "(?<![\\p{L}])" : "") + Regex.Escape(key) + (char.IsLetter(key[^1]) ? "(?![\\p{L}])" : "");
        var changed = _messages.Where(p => p.Key != p.Value).Select(p => p.Key).OrderByDescending(k => k.Length).ThenBy(k => k, StringComparer.Ordinal).ToArray();
        _phrases = changed.Length == 0 ? null : new Regex(string.Join("|", changed.Select(Pattern)), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
    }
    public string Text(string source)
    {
        if (source == "" || _phrases == null) return source;
        if (_cache.TryGetValue(source, out string? cached)) return cached;
        string Render(string key)
        {
            string result = _messages[key];
            return key.Any(char.IsLetter) && key.Where(char.IsLetter).All(char.IsUpper) ? result.ToUpperInvariant() : result;
        }
        string translated = _messages.TryGetValue(source.Trim(), out _) ? source.Replace(source.Trim(), Render(source.Trim()), StringComparison.Ordinal) : _phrases.Replace(source, match => Render(match.Value));
        if (_cache.Count >= 4096) _cache.Clear(); _cache[source] = translated; return translated;
    }
}
