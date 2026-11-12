namespace FleetCommand.Core;

public sealed record AsteroidDeposit(int Stars, bool Harvested = false, int SurveyMask = 0);

public static class FleetEconomy
{
    internal static BattleState Initialize(ContentCatalog c, BattleState s)
    {
        var bag = c.Maps[s.Map].AsteroidStars;
        if (bag == null || !FleetRuleTools.Enabled(c, s)) return s;
        var values = bag.ToArray(); uint random = s.RandomState;
        for (int i = values.Length - 1; i > 0; i--) { FleetRuleTools.Roll(ref random); int j = (int)(random % (i + 1)); (values[i], values[j]) = (values[j], values[i]); }
        int cursor = 0;
        return s with { RandomState = random, Deposits = c.Maps[s.Map].Objects.Select((o, i) => (o, i)).Where(p => FleetTerrain.Kind(c.Objects[p.o.Definition]) == "asteroid")
            .ToDictionary(p => p.i, _ => new AsteroidDeposit(values[cursor++])) };
    }
    public static Dictionary<int, AsteroidDeposit>? View(BattleState s, int side) => s.Deposits?.ToDictionary(p => p.Key,
        p => p.Value with { Stars = p.Value.Harvested || (p.Value.SurveyMask & (1 << side)) != 0 ? p.Value.Stars : -1, SurveyMask = p.Value.SurveyMask & (1 << side) });
    public static int? KnownStars(BattleState s, int index, int side) => s.Deposits?.TryGetValue(index, out var deposit) == true
        && deposit.Stars >= 0 && (deposit.Harvested || (deposit.SurveyMask & (1 << side)) != 0) ? deposit.Stars : null;
    internal static BattleState Survey(ContentCatalog c, BattleState s, BattleUnit unit)
    {
        if (!FleetRuleTools.Enabled(c, s) || s.Deposits == null || unit.Hulk || unit.Patrol != "" || !c.Ships[unit.Definition].Survey) return s;
        var map = c.Maps[s.Map]; var deposits = new Dictionary<int, AsteroidDeposit>(s.Deposits); bool changed = false;
        foreach (var (index, deposit) in s.Deposits)
            if (!deposit.Harvested && FleetFootprint.Distance(c, unit, new Hex(map.Objects[index].Q, map.Objects[index].R)) == 1)
            {
                if ((deposit.SurveyMask & (1 << unit.Side)) == 0) changed = true;
                deposits[index] = deposit with { SurveyMask = deposit.SurveyMask | (1 << unit.Side) };
            }
        return changed ? s with { Deposits = deposits } : s;
    }
    public static int[] Targets(ContentCatalog c, BattleState s, int side, string id)
    {
        if (s.Finished || s.Repairs is { Length: > 0 } || s.Hazard != null || s.Combat != null || s.Defense != null || s.CapEntry != null || s.Cards?.Response != null
            || s.Cards?.Pending is not { BasicAction: "harvest", Step: 1 } order || order.Side != side || SpecialOrders.Actor(order) != id || s.Deposits == null) return [];
        var unit = s.Units.Find(u => u.Id == id && u.Side == side && !u.Hulk && u.Patrol == "");
        if (unit == null || c.Ships[unit.Definition].Harvest < 0) return [];
        var map = c.Maps[s.Map];
        return s.Deposits.Where(p => !p.Value.Harvested && FleetFootprint.Distance(c, unit, new Hex(map.Objects[p.Key].Q, map.Objects[p.Key].R)) <= c.Ships[unit.Definition].Harvest).Select(p => p.Key).ToArray();
    }
    internal static CommandResult Harvest(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        if (!Targets(c, s, side, cmd.Unit).Contains(cmd.Object)) return new(false, "Выберите несобранное поле в пределах Harvest.", s);
        var deposit = s.Deposits![cmd.Object];
        if (s.Units.Single(u => u.Id == cmd.Unit).Cargo > 1000000 - deposit.Stars) return new(false, "Слишком большой груз ресурсов.", s);
        var deposits = new Dictionary<int, AsteroidDeposit>(s.Deposits) { [cmd.Object] = deposit with { Harvested = true } };
        var next = s with { Deposits = deposits, Units = s.Units.Select(u => u.Id == cmd.Unit ? u with { Cargo = u.Cargo + deposit.Stars } : u).ToList(),
            Harvested0 = s.Harvested0 + (side == 0 ? deposit.Stars : 0), Harvested1 = s.Harvested1 + (side == 1 ? deposit.Stars : 0) };
        next = FleetObjectives.Check(c, next);
        return SpecialOrders.Advance(c, s, next, side, $"Добыто ресурсов: {deposit.Stars} ★. Астероиды остаются на поле.");
    }
    internal static CommandResult BeginTransfer(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var actor = s.Units.Find(u => u.Id == cmd.Unit && u.Side == side && !u.Activated && !u.Hulk && u.Patrol == "");
        var other = s.Units.Find(u => u.Id == cmd.Target && u.Side == side && !u.Hulk && u.Patrol == ""); var cards = s.Cards!;
        if (!FleetRuleTools.Enabled(c, s) || side != s.ActiveSide || actor == null || other == null || FleetFootprint.Distance(c, actor, other) != 1
            || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(cmd.Card) || (side == 0 ? s.Orders0 : s.Orders1) >= FleetRuleTools.PlayLimit(c, s, side)
            || cmd.Amount < 1 || cmd.Amount > (cmd.Receive ? other.Cargo : actor.Cargo) || cmd.Amount > 1000000 - (cmd.Receive ? actor.Cargo : other.Cargo)) return new(false, "Передача требует готовый отряд, соседний союзный корабль и доступные ресурсы.", s);
        var next = s with { Sequence = s.Sequence + 1, Units = s.Units.Select(u => u.Id == actor.Id ? FleetRuleTools.NewOrder(u) with { Activated = true } : u).ToList(),
            Orders0 = s.Orders0 + (side == 0 ? 1 : 0), Orders1 = s.Orders1 + (side == 1 ? 1 : 0),
            Cards = cards with { Hand0 = cards.Hand0.Where(id => id != cmd.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != cmd.Card).ToArray(), Discard = cards.Discard.Append(cmd.Card).ToArray(),
                Pending = new(cmd.Card, side, [actor.Id], 0, BasicAction: "transfer-stars", Deferred: cmd) } };
        next = FleetRuleTools.DamageControl(c, next, actor.Id, cmd.Repair, out var repair);
        return FleetTerrain.Activation(c, s, next, [actor.Id], "order", repair);
    }
    internal static CommandResult CompleteTransfer(ContentCatalog c, BattleState before, BattleState s, string message)
    {
        var cmd = s.Cards!.Pending!.Deferred!; var actor = s.Units.Single(u => u.Id == cmd.Unit); var other = s.Units.Single(u => u.Id == cmd.Target);
        // Terrain resolves before cargo moves. A destroyed/disabled actor cancels its action.
        if (actor.Hulk || cmd.Amount > (cmd.Receive ? other.Cargo : actor.Cargo)) return SpecialOrders.Advance(c, before, s, actor.Side, message + " Передача отменена.");
        int delta = cmd.Receive ? cmd.Amount : -cmd.Amount;
        var next = s with { Cards = s.Cards with { Pending = s.Cards.Pending with { Deferred = null } },
            Units = s.Units.Select(u => u.Id == actor.Id ? u with { Cargo = u.Cargo + delta } : u.Id == other.Id ? u with { Cargo = u.Cargo - delta } : u).ToList() };
        return SpecialOrders.Advance(c, before, next, actor.Side, message + $" Передано ресурсов: {cmd.Amount} ★.");
    }
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        if (s.Harvested0 is < 0 or > 1000000 || s.Harvested1 is < 0 or > 1000000) throw new InvalidDataException("Invalid harvested totals.");
        if (s.Cards?.Pending is { BasicAction: "transfer-stars" } order)
        {
            if (order.Units is not { Length: 1 }) throw new InvalidDataException("Invalid resource transfer actor.");
            var cmd = order.Deferred; var actor = s.Units.Find(u => u.Id == order.Units[0]); var other = s.Units.Find(u => u.Id == cmd?.Target);
            if (cmd == null || cmd.Kind != "transfer-stars" || cmd.Unit != order.Units[0] || actor == null || other == null || actor.Side != order.Side || other.Side != order.Side
                || actor.Patrol != "" || other.Patrol != "" || cmd.Amount < 1 || cmd.Amount > (cmd.Receive ? other.Cargo : actor.Cargo) || FleetFootprint.Distance(c, actor, other) != 1)
                throw new InvalidDataException("Invalid pending resource transfer.");
        }
        var map = c.Maps[s.Map];
        if (s.Deposits == null) { if (map.AsteroidStars != null) throw new InvalidDataException("Missing asteroid deposits."); return; }
        bool view = s.Cards?.IsView == true;
        var indices = map.Objects.Select((o, i) => (o, i)).Where(p => FleetTerrain.Kind(c.Objects[p.o.Definition]) == "asteroid").Select(p => p.i).ToArray();
        if (!FleetRuleTools.Enabled(c, s) || map.AsteroidStars == null || s.Deposits.Count != indices.Length || indices.Any(i => !s.Deposits.ContainsKey(i))
            || s.Deposits.Any(p => p.Value == null || p.Value.Stars < (view ? -1 : 0) || p.Value.Stars > 1000 || p.Value.SurveyMask is < 0 or > 3 || p.Value.Harvested && p.Value.Stars < 0)
            || s.Harvested0 + s.Harvested1 != s.Deposits.Values.Where(d => d.Harvested).Sum(d => d.Stars)
            || !view && !s.Deposits.Values.Select(d => d.Stars).Order().SequenceEqual(map.AsteroidStars.Order())) throw new InvalidDataException("Invalid asteroid deposits.");
    }
}
