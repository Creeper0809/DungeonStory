using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class OffenseExpeditionParticipantFactData
{
    public string characterId = string.Empty;
    public string displayName = string.Empty;
    public bool identityParticipant;
    public OffenseExpeditionParticipantFactData Clone() => (OffenseExpeditionParticipantFactData)MemberwiseClone();
}

[Serializable]
public sealed class OffenseExpeditionResultFactData
{
    public int absoluteDay, memberCount;
    public bool campaignAdvanced, worldSiteResolved;
    public string regionId = string.Empty, regionDisplayName = string.Empty, factionId = string.Empty;
    public List<OffenseExpeditionParticipantFactData> participants = new();
    public DungeonOffenseExpeditionResultSaveData result = new();
    public OffenseExpeditionResultFactData Clone() => new()
    {
        absoluteDay = absoluteDay, memberCount = memberCount,
        campaignAdvanced = campaignAdvanced, worldSiteResolved = worldSiteResolved,
        regionId = regionId, regionDisplayName = regionDisplayName, factionId = factionId,
        participants = participants.Select(value => value.Clone()).ToList(),
        result = OffenseSaveService.CaptureResult(OffenseSaveService.RestoreResult(result))
    };
}

/// <summary>Historical result and participants, detached from actors, definitions and all logging contracts.</summary>
public sealed class OffenseExpeditionResolvedFact
{
    private readonly OffenseExpeditionResultFactData data;
    public OffenseExpeditionResolvedFact(OffenseExpeditionResultFactData source)
    {
        if (source == null || source.absoluteDay < 0 || source.memberCount < 0
            || source.regionId == null || source.regionDisplayName == null || source.factionId == null
            || source.participants == null || source.result == null
            || !DomainFactValueSyntax.IsIdentity(source.result.expeditionId)
            || !DomainFactValueSyntax.IsIdentity(source.result.targetId)
            || source.regionId.Length > 0 && !DomainFactValueSyntax.IsIdentity(source.regionId)
            || source.factionId.Length > 0 && !DomainFactValueSyntax.IsIdentity(source.factionId)
            || source.participants.Any(value => value == null || !DomainFactValueSyntax.IsIdentity(value.characterId)
                || !DomainFactValueSyntax.IsText(value.displayName, 256, false))
            || source.participants.Select(value => value.characterId).Distinct(StringComparer.Ordinal).Count() != source.participants.Count
            || source.participants.Count(value => value.identityParticipant) > source.memberCount)
            throw new ArgumentException("offense-result-fact-invalid");
        OffenseAggregateSaveValidation.ValidateHistoricalResult(source.result);
        data = source.Clone();
        Result = OffenseSaveService.RestoreResult(data.result);
    }
    public OffenseExpeditionResult Result { get; }
    public OffenseExpeditionResultFactData Snapshot() => data.Clone();

    public static OffenseExpeditionResolvedFact Capture(OffenseExpeditionRun expedition,
        OffenseExpeditionResult result, int day, bool campaignAdvanced, bool worldSiteResolved)
    {
        if (expedition == null || result == null || result.expeditionId != expedition.ExpeditionId
            || result.targetId != expedition.Target.id) throw new ArgumentException("offense-result-owner-conflict");
        var members = expedition.MemberActors.Where(value => value?.Identity != null)
            .Select(value => value.Identity.PersistentId).ToHashSet(StringComparer.Ordinal);
        return new OffenseExpeditionResolvedFact(new OffenseExpeditionResultFactData
        {
            absoluteDay = day, memberCount = expedition.MemberActors.Count,
            campaignAdvanced = campaignAdvanced, worldSiteResolved = worldSiteResolved,
            regionId = expedition.Target.regionId ?? string.Empty,
            regionDisplayName = expedition.Target.regionDisplayName ?? string.Empty,
            factionId = expedition.Target.factionId ?? string.Empty,
            result = OffenseSaveService.CaptureResult(result),
            participants = expedition.MemberActors.Concat(expedition.ProtectedRescueActors)
                .Where(value => value?.Identity != null).GroupBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
                .Select(group => new OffenseExpeditionParticipantFactData
                {
                    characterId = group.Key, displayName = group.First().name,
                    identityParticipant = members.Contains(group.Key)
                }).ToList()
        });
    }
}
