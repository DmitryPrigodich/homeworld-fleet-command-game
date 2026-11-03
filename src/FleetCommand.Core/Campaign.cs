using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace FleetCommand.Core;

public sealed record CampaignProgress(string Campaign, string ContentFingerprint, string CurrentMission,
    string[] CompletedMissions, int Medals = 0, string[]? Unlocks = null, bool Completed = false,
    string[]? Participants = null, BattleState? Battle = null, int OpponentMedals = 0, MissionResult[]? Results = null);
public sealed record MissionResult(string Mission, string ResultId, int? Winner, int Medals0, int Medals1, int Round);

public static class Campaign
{
    public static CampaignProgress Start(ContentCatalog content, string id, string[]? participants = null)
        => new(id, content.Fingerprint, content.Campaigns[id].Start, [], Participants: participants ?? []);

    public static CampaignProgress CompleteMission(ContentCatalog content, CampaignProgress progress, BattleState battle)
    {
        if (progress.ContentFingerprint != content.Fingerprint || battle.ContentFingerprint != content.Fingerprint)
            throw new InvalidDataException("Campaign content mismatch.");
        if (progress.Completed || !battle.Finished || battle.Cards?.IsView == true) throw new InvalidOperationException("Mission is not ready to complete.");
        var mission = content.Campaigns[progress.Campaign].Missions.Single(m => m.Id == progress.CurrentMission);
        if (mission.Map != battle.Map) throw new InvalidDataException("Battle is not the current campaign mission.");
        if (FleetObjectives.Enabled(content, battle)) FleetObjectives.Validate(content, battle);
        string resultId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(battle, JsonData.Options))));
        if ((progress.Results ?? []).Any(r => r.ResultId == resultId)) throw new InvalidOperationException("This mission result was already recorded.");
        bool objectiveMode = FleetObjectives.Enabled(content, battle);
        int medals0 = objectiveMode ? battle.Medals0 : battle.Winner == 0 ? 1 : 0;
        int medals1 = objectiveMode ? battle.Medals1 : battle.Winner == 1 ? 1 : 0;
        bool victory = battle.Winner == 0;
        string? next = victory ? mission.OnVictory : battle.Winner == null ? mission.OnDraw ?? mission.OnDefeat : mission.OnDefeat;
        return progress with
        {
            CurrentMission = next ?? progress.CurrentMission,
            CompletedMissions = victory ? progress.CompletedMissions.Append(mission.Id).Distinct().ToArray() : progress.CompletedMissions,
            Medals = progress.Medals + medals0,
            OpponentMedals = progress.OpponentMedals + medals1,
            Results = (progress.Results ?? []).Append(new MissionResult(mission.Id, resultId, battle.Winner, medals0, medals1, battle.Round)).ToArray(),
            Completed = next is null,
            Battle = null
        };
    }
}
