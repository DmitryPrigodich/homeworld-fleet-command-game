namespace FleetCommand.Core;

public sealed record CampaignProgress(string Campaign, string ContentFingerprint, string CurrentMission,
    string[] CompletedMissions, int Medals = 0, string[]? Unlocks = null, bool Completed = false,
    string[]? Participants = null, BattleState? Battle = null);

public static class Campaign
{
    public static CampaignProgress Start(ContentCatalog content, string id, string[]? participants = null)
        => new(id, content.Fingerprint, content.Campaigns[id].Start, [], Participants: participants ?? []);

    public static CampaignProgress CompleteMission(ContentCatalog content, CampaignProgress progress, BattleState battle)
    {
        if (progress.ContentFingerprint != content.Fingerprint || battle.ContentFingerprint != content.Fingerprint)
            throw new InvalidDataException("Campaign content mismatch.");
        if (progress.Completed || !battle.Finished) throw new InvalidOperationException("Mission is not ready to complete.");
        var mission = content.Campaigns[progress.Campaign].Missions.Single(m => m.Id == progress.CurrentMission);
        if (mission.Map != battle.Map) throw new InvalidDataException("Battle is not the current campaign mission.");
        bool victory = battle.Winner == 0;
        string? next = victory ? mission.OnVictory : mission.OnDefeat;
        return progress with
        {
            CurrentMission = next ?? progress.CurrentMission,
            CompletedMissions = victory ? progress.CompletedMissions.Append(mission.Id).Distinct().ToArray() : progress.CompletedMissions,
            Medals = progress.Medals + (victory ? 1 : 0),
            Completed = next is null,
            Battle = null
        };
    }
}
