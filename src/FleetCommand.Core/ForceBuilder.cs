using System.Text.RegularExpressions;
namespace FleetCommand.Core;

public sealed record ForceEntry(string Id, string Ship, string Placement = "board", int Column = 0, int Row = 0, string Parent = "", int Facing = -1);
public sealed record ForceLeader(string Leader, string Unit = "");
public sealed record ForcePlan(string Id, string Name, string Faction, int Budget, ForceEntry[] Units, ForceLeader[] Leaders, int ReserveStars = 0, bool CheckTabs = true);
public sealed record ForceLimit(int Budget = 0, string[]? Ships = null, string[]? Leaders = null, int ReserveStars = -1, bool AllowReserve = true);
public sealed record ForceUnit(ForceEntry Entry, bool Free = false);
public sealed record ForceSlot(string Key, string Source, ForceTab Tab, string Unit = "");
public sealed record ForceReport(long Spent, long Remaining, ForceUnit[] Units, ForceSlot[] Slots, string[] Errors) { public bool Valid => Errors.Length == 0; }

public static class ForceBuilder
{
    public static bool SafeId(string? id) => id != null && Regex.IsMatch(id, "^[a-zA-Z0-9_-]{1,48}$");
    public static bool Fits(ContentCatalog c, ForceTab tab, string ship)
    {
        var definition = c.Ships[ship]; string cls = FleetRuleTools.Class(definition);
        return (tab.Color == "blue" ? FleetRuleTools.Strike(definition) : !FleetRuleTools.Strike(definition)) && (tab.Classes is not { Length: > 0 } || tab.Classes.Contains(cls));
    }
    public static ForceReport Validate(ContentCatalog c, ForcePlan plan, ForceLimit? limit = null)
    {
        var errors = new List<string>(); var units = new List<ForceUnit>(); var slots = new List<ForceSlot>();
        if (plan == null || !SafeId(plan.Id) || string.IsNullOrWhiteSpace(plan.Name) || plan.Name.Length > 60 || plan.Name.Any(char.IsControl) || plan.Units == null || plan.Leaders == null || plan.Units.Length is < 1 or > 64 || plan.Leaders.Length > 16)
            return new(0, 0, [], [], ["Неверный формат состава: нужны имя, подразделения и корректный идентификатор."]);
        if (plan.Budget is < 1 or > 1000000 || plan.ReserveStars < 0 || plan.ReserveStars > plan.Budget) errors.Add("Неверный бюджет или резерв ресурсов.");
        if (limit != null && (limit.Budget > 0 && plan.Budget > limit.Budget || limit.ReserveStars >= 0 && plan.ReserveStars > limit.ReserveStars)) errors.Add("Превышены ограничения сценария по бюджету/резерву.");
        if (plan.Units.Any(u => u == null || !SafeId(u.Id) || string.IsNullOrEmpty(u.Ship) || u.Parent == null || !c.Ships.ContainsKey(u.Ship)) || plan.Units.Select(u => u.Id).Distinct().Count() != plan.Units.Length
            || plan.Leaders.Any(l => l == null || string.IsNullOrEmpty(l.Leader) || l.Unit == null || !c.Leaders.ContainsKey(l.Leader)) || plan.Leaders.Select(l => l.Leader).Distinct().Count() != plan.Leaders.Length)
            return new(0, plan.Budget, [], [], errors.Append("Неизвестные корабли/лидеры или повторяющиеся идентификаторы.").ToArray());
        foreach (var entry in plan.Units)
        {
            var ship = c.Ships[entry.Ship]; units.Add(new(entry));
            if (ship.Faction != plan.Faction || ship.Stars <= 0) errors.Add($"{ship.Name}: другая фракция или не указана стоимость покупки.");
            foreach (var tab in (ship.Tabs ?? []).Where(t => t.GrantedUnit != ""))
                units.Add(new(new(entry.Id + "~" + tab.Id, tab.GrantedUnit, "bay", Parent: entry.Id), true));
        }
        if (units.Count > 128) errors.Add("Слишком много отрядов с учётом приписанных подразделений.");
        long spent = plan.Units.Sum(u => (long)c.Ships[u.Ship].Stars) + plan.Leaders.Sum(l => (long)c.Leaders[l.Leader].Stars) + plan.ReserveStars;
        if (spent > plan.Budget) errors.Add($"Превышен бюджет на {spent - plan.Budget} ★.");
        foreach (var item in units)
        {
            var u = item.Entry; var ship = c.Ships[u.Ship];
            if (limit?.Ships != null && !limit.Ships.Contains(u.Ship)) errors.Add($"{ship.Name}: недоступен в этом сценарии.");
            if (u.Facing is < -1 or > 5 || u.Placement is not ("board" or "reserve" or "bay")) errors.Add($"{u.Id}: неверное размещение или курс.");
            if (u.Placement == "board" && (u.Column is < 0 or > 1 || u.Row is < 0 or > 29)) errors.Add($"{u.Id}: корабль должен находиться в своей зоне развёртывания.");
            if (u.Placement == "reserve" && (FleetRuleTools.Class(ship) is not ("frigate" or "capital") || limit?.AllowReserve == false)) errors.Add($"{u.Id}: этот отряд нельзя оставить в резерве.");
            if (u.Placement == "bay")
            {
                var parent = units.FirstOrDefault(p => p.Entry.Id == u.Parent);
                if (!FleetRuleTools.Strike(ship) || parent == null || parent.Entry.Id == u.Id || parent.Entry.Placement == "bay") errors.Add($"{u.Id}: нужен подходящий носитель; вложенные ангары недоступны.");
                else if (units.Count(p => p.Entry.Placement == "bay" && p.Entry.Parent == u.Parent) > c.Ships[parent.Entry.Ship].Bays) errors.Add($"{parent.Entry.Id}: недостаточно мест в ангарах.");
            }
        }
        if (units.Where(u => u.Entry.Placement == "board").GroupBy(u => (u.Entry.Column, u.Entry.Row)).Any(g => g.Count() > 1)) errors.Add("Два отряда занимают один гекс развёртывания.");
        foreach (var choice in plan.Leaders)
        {
            var leader = c.Leaders[choice.Leader]; var assigned = units.FirstOrDefault(u => u.Entry.Id == choice.Unit);
            if (leader.Faction != plan.Faction || assigned == null || !leader.Classes.Contains(FleetRuleTools.Class(c.Ships[assigned.Entry.Ship]))) errors.Add($"{leader.Name}: назначьте лидера подходящему отряду своей фракции.");
            if (limit?.Leaders != null && !limit.Leaders.Contains(leader.Id)) errors.Add($"{leader.Name}: недоступен в этом сценарии.");
            foreach (var tab in leader.Tabs ?? []) slots.Add(new("leader:" + leader.Id + ":" + tab.Id, leader.Name, tab));
        }
        foreach (var item in units.Where(u => !u.Free)) foreach (var tab in c.Ships[item.Entry.Ship].Tabs ?? [])
            slots.Add(new("unit:" + item.Entry.Id + ":" + tab.Id, c.Ships[item.Entry.Ship].Name + " · " + item.Entry.Id, tab, tab.GrantedUnit == "" ? "" : item.Entry.Id + "~" + tab.Id));
        if (2L + plan.Leaders.Sum(l => (long)c.Leaders[l.Leader].Hand) > 100 || 1L + plan.Leaders.Sum(l => (long)c.Leaders[l.Leader].Play) > 100
            || plan.Leaders.GroupBy(l => l.Unit).Any(g => g.Sum(l => (long)c.Leaders[l.Leader].CoolUnderPressure) > 99)) errors.Add("Суммарные бонусы лидеров превышают поддерживаемые лимиты боя.");
        var purchased = units.Where(u => !u.Free).ToArray(); var available = slots.Where(s => s.Tab.GrantedUnit == "").ToArray();
        var matchUnit = Enumerable.Repeat(-1, purchased.Length).ToArray(); var matchSlot = Enumerable.Repeat(-1, available.Length).ToArray();
        bool Eligible(int u, int s)
        {
            var slot = available[s]; if (!Fits(c, slot.Tab, purchased[u].Entry.Ship)) return false;
            if (slot.Key.StartsWith("unit:") && (purchased[u].Entry.Placement != "bay" || !slot.Key.StartsWith("unit:" + purchased[u].Entry.Parent + ":"))) return false;
            return !slot.Tab.Required || !slot.Key.StartsWith("leader:") || plan.Leaders.Any(l => slot.Key == "leader:" + l.Leader + ":" + slot.Tab.Id && l.Unit == purchased[u].Entry.Id);
        }
        bool AssignSlot(int slot, HashSet<int> seen)
        {
            if (!seen.Add(slot)) return false;
            for (int u = 0; u < purchased.Length; u++) if (Eligible(u, slot) && (matchUnit[u] < 0 || AssignSlot(matchUnit[u], seen))) { matchUnit[u] = slot; matchSlot[slot] = u; return true; }
            return false;
        }
        foreach (int slot in Enumerable.Range(0, available.Length).Where(s => available[s].Tab.Required))
            if (!AssignSlot(slot, []) && plan.CheckTabs) errors.Add($"Не выполнена обязательная покупка: {available[slot].Source} / {available[slot].Tab.Id}.");
        bool AssignUnit(int unit, HashSet<int> seen)
        {
            if (!seen.Add(unit)) return false;
            for (int s = 0; s < available.Length; s++) if (Eligible(unit, s) && (matchSlot[s] < 0 || AssignUnit(matchSlot[s], seen))) { matchSlot[s] = unit; matchUnit[unit] = s; return true; }
            return false;
        }
        for (int u = 0; u < purchased.Length; u++) if (matchUnit[u] < 0 && !c.Ships[purchased[u].Entry.Ship].TabExempt && !AssignUnit(u, []) && plan.CheckTabs) errors.Add($"Нет подходящей вкладки для {c.Ships[purchased[u].Entry.Ship].Name} · {purchased[u].Entry.Id}.");
        for (int s = 0; s < available.Length; s++) if (matchSlot[s] >= 0) { int index = slots.FindIndex(t => t.Key == available[s].Key); slots[index] = slots[index] with { Unit = purchased[matchSlot[s]].Entry.Id }; }
        return new(spent, plan.Budget - spent, units.ToArray(), slots.ToArray(), errors.Distinct().ToArray());
    }
    internal static void ValidateDefinitions(ContentCatalog c)
    {
        void Tabs(ForceTab[]? tabs, string faction, bool ship, int bays)
        {
            if (tabs == null) return;
            if (tabs.Length > 32 || tabs.Any(t => t == null || !SafeId(t.Id)) || tabs.Select(t => t.Id).Distinct().Count() != tabs.Length) throw new InvalidDataException("Invalid force tabs.");
            foreach (var t in tabs)
                if (t.Color is not ("blue" or "yellow") || t.GrantedUnit == null || !ship && t.GrantedUnit != "" || ship && t.Color != "blue"
                    || (t.Classes ?? []).Any(cls => !FleetRuleTools.Classes.Contains(cls) || (t.Color == "blue" ? cls is not ("fighter" or "corvette" or "platform") : cls is "fighter" or "corvette" or "platform"))
                    || t.GrantedUnit != "" && (!c.Ships.TryGetValue(t.GrantedUnit, out var free) || free.Faction != faction || !Fits(c, t, free.Id) || (free.Tabs ?? []).Any(tab => tab.GrantedUnit != ""))) throw new InvalidDataException("Invalid force tab contents.");
            if (ship && tabs.Count(t => t.GrantedUnit != "") > bays) throw new InvalidDataException("Assigned units exceed bays.");
        }
        foreach (var ship in c.Ships.Values) Tabs(ship.Tabs, ship.Faction, true, ship.Bays);
        foreach (var leader in c.Leaders.Values) Tabs(leader.Tabs, leader.Faction, false, 0);
    }
}

public static class ForceLibrary
{
    public static string Save(string directory, ForcePlan plan)
    {
        if (!ForceBuilder.SafeId(plan.Id)) throw new InvalidDataException("Invalid fleet file ID.");
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, plan.Id + ".json"), pending = path + ".pending";
        File.WriteAllText(pending, System.Text.Json.JsonSerializer.Serialize(plan, JsonData.Options)); File.Move(pending, path, true); return path;
    }
    public static ForcePlan[] Read(string directory) => !Directory.Exists(directory) ? [] : Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal).Select(path =>
    {
        var p = JsonData.Read<ForcePlan>(path);
        if (!ForceBuilder.SafeId(p.Id) || p.Name == null || p.Faction == null || p.Units == null || p.Leaders == null || p.Units.Length > 64 || p.Leaders.Length > 16
            || p.Units.Any(u => u == null || u.Id == null || u.Ship == null || u.Parent == null) || p.Leaders.Any(l => l == null || l.Leader == null || l.Unit == null)) throw new InvalidDataException("Неверный формат состава: " + Path.GetFileName(path));
        return p;
    }).ToArray();
}
