namespace FleetCommand.Core;

public sealed record PendingHeroicInterrupt(int Side, string Card, string Unit, string Target, int ParentSide,
    PendingResponse Response, PendingOrder? Order, PendingReinforce? Reinforce, PendingMoveInterrupt? MoveInterrupt, MovementStep ParentPose,
    PendingHeroicInterrupt? Parent = null, bool Moved = false, string Weapon = "");

public static class HeroicInterrupts
{
    public static bool IsHeroic(ContentCatalog c, BattleState s, string card) => CommandCards.Find(c, s, card) is { Kind: "interrupt", Interrupt: "heroic" };
    public static bool Active(BattleState s, PendingOrder? order) => order is { BasicAction: "" } && s.Cards?.HeroicInterrupt is { } frame
        && order.Card == frame.Card && order.Side == frame.Side && order.Units is { Length: 1 } && order.Units[0] == frame.Unit;
    public static BattleUnit? Owner(ContentCatalog c, BattleState s, string card) => s.Cards != null && HeroicCommands.Pool(s.Cards).FirstOrDefault(h => h.Id == card) is { } hero
        ? s.Units.Find(u => u.Side == hero.Side && !u.Hulk && u.Patrol == "" && FleetRuleTools.LeaderIds(u).Contains(hero.Leader)) : null;
    public static bool CanDeclare(ContentCatalog c, BattleState s, int side, BattleCommand cmd, bool spent = false)
    {
        var owner = Owner(c, s, cmd.Card);
        return cmd.Kind == "heroic-interrupt" && !cmd.Power && !s.Finished && s.Cards?.Phase == "orders" && IsHeroic(c, s, cmd.Card) && s.Cards?.Response is { Replan: false } pause && side == 1 - pause.Side
            && (!spent ? s.Cards.Counter == null && HeroicCommands.CanUse(c, s, side, cmd.Card) && (side == 0 ? s.Cards.Hand0 : s.Cards.Hand1).Contains(cmd.Card) : (s.Cards.HeroicUsed ?? []).Contains(cmd.Card))
            && owner != null && (CommandCards.Find(c, s, cmd.Card)!.Lines![0].Actions[0] != "power" || FleetPlanets.IsPlanet(c, owner)) && (!FleetPlanets.IsPlanet(c, owner) || FleetPlanets.HeroWeapon(c, owner, CommandCards.Find(c, s, cmd.Card)!.HeroicOwner) is { Length: > 0 } weapon && (cmd.Weapon == "" || cmd.Weapon == weapon)) && owner.Side == side && owner.Id == cmd.Unit && cmd.Target == pause.Command.Unit
            && InterruptRules.Eligible(c, s, cmd.Card, pause) && Depth(s.Cards.HeroicInterrupt) < 4;
    }
    private static int Depth(PendingHeroicInterrupt? frame) { int n = 0; for (; frame != null && n <= 4; frame = frame.Parent) n++; return n; }
    // Registry/spending are public. Offers must not reveal whether a hidden hand contains a card.
    internal static bool Offer(ContentCatalog c, BattleState s, PendingResponse pause) => HeroicCommands.Pool(s.Cards!).Any(h => h.Side == 1 - pause.Side
        && !(s.Cards!.HeroicUsed ?? []).Contains(h.Id) && IsHeroic(c, s, h.Id) && InterruptRules.Eligible(c, s, h.Id, pause)
        && Owner(c, s, h.Id) is { } owner && (!FleetPlanets.IsPlanet(c, owner) || FleetPlanets.HeroWeapon(c, owner, CommandCards.Find(c, s, h.Id)!.HeroicOwner) != "") && owner.Side == h.Side && Depth(s.Cards.HeroicInterrupt) < 4);
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        var frame = s.Cards!.HeroicInterrupt; if (frame == null) return;
        if (Depth(frame) > 4) throw new InvalidDataException("Heroic reaction nesting exceeds its limit.");
        bool active = Active(s, s.Cards.Pending);
        bool suspended = false;
        for (var move = s.Cards.MoveInterrupt; move != null; move = move.Parent) if (Active(s, move.Order)) suspended = true;
        if (active && s.Cards.Pending!.Weapon != frame.Weapon) throw new InvalidDataException("Heroic reaction lost its bound facility weapon.");
        if (!active && !suspended) throw new InvalidDataException("Heroic reaction has no active or suspended action line.");
        for (var f = frame; f != null; f = f.Parent)
        {
            if (f.Side is < 0 or > 1 || f.ParentSide != 1 - f.Side || !IsHeroic(c, s, f.Card) || !(s.Cards.HeroicUsed ?? []).Contains(f.Card)
                || f.Response == null || f.Response.Side != f.ParentSide || f.Response.Command == null || f.Response.Command.Unit != f.Target
                || f.Weapon == null || f.Weapon.Length > 80 || f.ParentPose.Facing is < 0 or > 5 || !FleetFootprint.Inside(c.Maps[s.Map], f.ParentPose.Cell)
                || string.IsNullOrWhiteSpace(f.Unit) || f.Unit.Length > 80 || string.IsNullOrWhiteSpace(f.Target) || f.Target.Length > 80
                || s.Units.Any(u => u.Id == f.Unit && (u.Side != f.Side || !FleetRuleTools.LeaderIds(u).Contains(CommandCards.Find(c, s, f.Card)!.HeroicOwner) && !(u.DestroyedFacilities ?? []).Contains(FleetPlanets.FacilityId(f.Weapon)))) || f.Parent != null && f.Order?.Card != f.Parent.Card)
                throw new InvalidDataException("Invalid suspended Heroic reaction.");
            var owner = s.Units.Find(u => u.Id == f.Unit);
            if (owner != null && FleetPlanets.IsPlanet(c, owner) && !(owner.DestroyedFacilities ?? []).Contains(FleetPlanets.FacilityId(f.Weapon))
                && (owner.FacilityLeaders?.GetValueOrDefault(FleetPlanets.FacilityId(f.Weapon)) != CommandCards.Find(c, s, f.Card)!.HeroicOwner || !FleetWeapons.Weapons(c, owner).Any(w => w.Id == f.Weapon)))
                throw new InvalidDataException("Heroic reaction weapon is not assigned to its facility leader.");
            var restored = s with { ActiveSide = f.ParentSide, Reinforce = f.Reinforce,
                Cards = s.Cards with { Pending = f.Order, Response = f.Response, MoveInterrupt = f.MoveInterrupt, HeroicInterrupt = f.Parent, Counter = null } };
            if (!InterruptRules.Eligible(c, restored, f.Card, f.Response)) throw new InvalidDataException("Invalid parent trigger for Heroic reaction.");
            SpecialOrders.Validate(c, restored, suspended: true);
            if (s.Units.Any(u => u.Id == f.Target)) InterruptRules.Validate(c, restored with { Units = restored.Units.Select(u => u.Id == f.Target ? u with { Q = f.ParentPose.Cell.Q, R = f.ParentPose.Cell.R, Facing = f.ParentPose.Facing } : u).ToList() });
            if (!f.Response.Basic && !f.Response.ReactionMove && (f.Order == null || SpecialOrders.Actor(c, restored, f.Order) != f.Target))
                throw new InvalidDataException("Heroic reaction lost its parent action.");
        }
    }
}

public static partial class InterruptRules
{
    private static CommandResult BeginHeroic(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        if (!HeroicInterrupts.CanDeclare(c, s, side, cmd) || cmd.Power || s.Combat != null || s.Defense != null || s.Hazard != null || s.Repairs is { Length: > 0 })
            return new(false, "Select the matching live Hero during the printed enemy action to declare a Heroic reaction.", s);
        var next = s with { Cards = HeroicCommands.Spend(s.Cards!, cmd.Card) };
        return CanOfferCounter(c, next, 1 - side)
            ? new(true, "Heroic reaction declared; the opponent may counter before its actions.", next with { Sequence = s.Sequence + 1, Cards = next.Cards! with { Counter = new(side, cmd) } })
            : ExecuteHeroic(c, s, next, side, cmd);
    }
    private static CommandResult ExecuteHeroic(ContentCatalog c, BattleState before, BattleState s, int side, BattleCommand cmd)
    {
        var cards = s.Cards!;
        var target = s.Units.Single(u => u.Id == cmd.Target);
        string weapon = FleetPlanets.IsPlanet(c, s.Units.Single(u => u.Id == cmd.Unit)) ? FleetPlanets.HeroWeapon(c, s.Units.Single(u => u.Id == cmd.Unit), CommandCards.Find(c, s, cmd.Card)!.HeroicOwner) : "";
        var frame = new PendingHeroicInterrupt(side, cmd.Card, cmd.Unit, cmd.Target, s.ActiveSide, cards.Response!, cards.Pending, s.Reinforce, cards.MoveInterrupt, new(new(target.Q, target.R), target.Facing), cards.HeroicInterrupt, Weapon: weapon);
        return new(true, CommandCards.Find(c, s, cmd.Card)!.Lines![0].Actions[0] == "power" ? "Planetary Overwatch: Power Attack only the acting enemy with the Hero's facility. No Play or activation spent." : "Heroic reaction: Move and then Attack only the moving enemy. No Play or activation spent.", s with { ActiveSide = side, Reinforce = null, Sequence = before.Sequence + 1,
            Cards = cards with { Pending = new(cmd.Card, side, [cmd.Unit], 0, Weapon: weapon), Response = null, MoveInterrupt = null, HeroicInterrupt = frame } });
    }
    internal static CommandResult FinishHeroic(ContentCatalog c, BattleState before, BattleState s, string message)
    {
        var f = s.Cards!.HeroicInterrupt!; var response = f.Response; var mover = s.Units.Find(u => u.Id == f.Target);
        if (f.Moved && mover != null && response.Command.Kind == "move") response = response with { RemainingPath = [], PathFacings = [], ReplanAfterResponse = true,
            Command = response.Command with { Q = mover.Q, R = mover.R, Embark = "" } };
        var restored = s with { ActiveSide = f.ParentSide, Reinforce = f.Reinforce,
            Cards = s.Cards with { Pending = f.Order, Response = response, MoveInterrupt = f.MoveInterrupt, HeroicInterrupt = f.Parent } };
        return AfterInterrupt(c, before, restored, message + " The interrupted enemy action resumes.");
    }
}
