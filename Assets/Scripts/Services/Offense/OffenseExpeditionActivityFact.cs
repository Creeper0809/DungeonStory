using System;
using DungeonStory.Foundation.Facts;

public enum OffenseExpeditionActivity
{
    RegionBossSealAwarded,
    PartyStranded,
    ObjectiveDestroyed,
    RouteBattleCompleted,
    DepartureRestorationFailed,
    PackingResumptionFailed,
    DepartureResumptionFailed
}

/// <summary>Immutable activity data. UI wording and importance belong to an observer.</summary>
public sealed class OffenseExpeditionActivityFact
{
    public OffenseExpeditionActivityFact(OffenseExpeditionActivity activity, string expeditionId,
        string targetTitle = "", string nodeTitle = "", string message = "", int count = 0)
    {
        if (!Enum.IsDefined(typeof(OffenseExpeditionActivity), activity)
            || !DomainFactValueSyntax.IsIdentity(expeditionId)
            || !Text(targetTitle) || !Text(nodeTitle) || !Text(message) || count < 0)
            throw new ArgumentException("offense-expedition-activity-invalid");
        Activity = activity; ExpeditionId = expeditionId; TargetTitle = targetTitle;
        NodeTitle = nodeTitle; Message = message; Count = count;
    }
    public OffenseExpeditionActivity Activity { get; }
    public string ExpeditionId { get; }
    public string TargetTitle { get; }
    public string NodeTitle { get; }
    public string Message { get; }
    public int Count { get; }
    private static bool Text(string value) => DomainFactValueSyntax.IsText(value, 16384, true);
}
