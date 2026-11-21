using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private bool DrawPlanetDamage(Control parent)
    {
        if (_state?.PlanetDamage is not { } damage) return false;
        var unit = _state.Units.Single(u => u.Id == damage.Target);
        Text(parent, "PLANET FACILITY DAMAGE", 16, _colors.Highlight);
        Text(parent, FleetPresentation.UnitName(_content, _state, unit), 14);
        Text(parent, $"Facility hits remaining: {damage.Remaining}", 14);
        Text(parent, "The attacker chooses. Hardened Defences must be destroyed first. The physical planet remains on the map.", 13, _colors.Muted);
        foreach (string id in FleetPlanets.DamageChoices(_content, _state))
        {
            string facility = id; var definition = FleetPlanets.Live(_content, unit).Single(f => f.Id == id);
            Button(parent, (damage.Remaining > 0 ? "DESTROY: " : "CRIPPLE: ") + definition.Name,
                () => Submit(new("planet-facility", unit.Id, facility)), LocalCommandSide != damage.Side);
        }
        return true;
    }
    private void DrawPlanetFacilities(Control parent, BattleUnit unit)
    {
        if (!FleetPlanets.IsPlanet(_content, unit)) return;
        Text(parent, "PLANET FACILITIES", 14, _colors.Highlight);
        Text(parent, "Each weapon facility has its own activation. Choose a battery before starting an Attack order.", 12, _colors.Muted);
        foreach (var facility in _content.Ships[unit.Definition].Facilities ?? [])
        {
            bool destroyed = (unit.DestroyedFacilities ?? []).Contains(facility.Id);
            string status = destroyed ? "Destroyed" : FleetPlanets.Crippled(unit, facility.Id) ? "Crippled weapons" : (unit.ActivatedFacilities ?? []).Contains(facility.Id) ? "Activated this round" : "Ready";
            Text(parent, facility.Name + " · " + status, 13, destroyed ? _colors.Muted : _colors.Highlight);
            if (unit.FacilityLeaders?.TryGetValue(facility.Id, out string? id) == true)
            { var leader = _content.Leaders[id]; Text(parent, "Leader: " + leader.Name, 12); if (leader.AttackDice > 0) Text(parent, $"Facility attack dice: +{leader.AttackDice}", 12, _colors.Muted); string abilities = FleetPresentation.LeaderAbilities(leader); if (abilities != "") Text(parent, abilities, 12, _colors.Muted); }
        }
    }
}
