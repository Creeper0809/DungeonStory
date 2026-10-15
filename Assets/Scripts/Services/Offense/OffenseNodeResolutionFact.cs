using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class OffenseNodeParticipantData
{
    public string characterId = string.Empty, displayName = string.Empty;
    public bool experienceEligible;
    public string experienceDisplayName = string.Empty;
    public OffenseNodeParticipantData Clone() => new() { characterId = characterId, displayName = displayName,
        experienceEligible = experienceEligible, experienceDisplayName = experienceDisplayName };
}

[Serializable]
public sealed class OffenseNodeResolutionFactData
{
    public string expeditionId = string.Empty, targetId = string.Empty, targetTitle = string.Empty;
    public string regionId = string.Empty, regionDisplayName = string.Empty, factionId = string.Empty;
    public string nodeId = string.Empty, nodeTitle = string.Empty, nodeDescription = string.Empty, message = string.Empty;
    public OffenseRouteNodeKind nodeKind;
    public int absoluteDay, experienceReward;
    public long sequence;
    public bool resolved, useSupply, usedSupply, gainedLoot;
    public List<OffenseNodeParticipantData> participants = new();
    public OffenseNodeResolutionFactData Clone()
    {
        var copy = (OffenseNodeResolutionFactData)MemberwiseClone();
        copy.participants = participants.Select(value => value.Clone()).ToList();
        return copy;
    }
}

/// <summary>Frozen proposal or actual node resolution, without logging, actor references or importance policy.</summary>
public sealed class OffenseNodeResolutionFact
{
    private readonly OffenseNodeResolutionFactData data;
    public OffenseNodeResolutionFact(OffenseNodeResolutionFactData source)
    {
        if (source == null || !DomainFactValueSyntax.IsIdentity(source.expeditionId)
            || !DomainFactValueSyntax.IsIdentity(source.targetId) || !DomainFactValueSyntax.IsIdentity(source.nodeId)
            || source.absoluteDay < 1 || source.sequence < 1 || source.experienceReward < 0
            || source.nodeKind is not (OffenseRouteNodeKind.Event or OffenseRouteNodeKind.Camp or OffenseRouteNodeKind.Cache)
            || !Text(source.targetTitle) || !Text(source.regionDisplayName) || !Text(source.nodeTitle)
            || !Text(source.nodeDescription) || !Text(source.message)
            || !OptionalId(source.regionId) || !OptionalId(source.factionId)
            || source.participants == null || source.participants.Count > 256
            || source.participants.Any(value => value == null || !DomainFactValueSyntax.IsIdentity(value.characterId)
                || !Text(value.displayName) || !Text(value.experienceDisplayName))
            || source.participants.Select(value => value.characterId).Distinct(StringComparer.Ordinal).Count() != source.participants.Count
            || !source.resolved && (source.usedSupply || source.gainedLoot || source.message != string.Empty)
            || source.usedSupply && !source.useSupply)
            throw new ArgumentException("offense-node-fact-invalid");
        data = source.Clone();
    }
    public OffenseNodeResolutionFactData Snapshot() => data.Clone();
    public bool MatchesProposal(OffenseNodeResolutionFact proposed)
    {
        if (proposed == null) return false;
        var before = proposed.data;
        return data.expeditionId == before.expeditionId && data.targetId == before.targetId && data.targetTitle == before.targetTitle
            && data.regionId == before.regionId && data.regionDisplayName == before.regionDisplayName && data.factionId == before.factionId
            && data.nodeId == before.nodeId && data.nodeTitle == before.nodeTitle && data.nodeDescription == before.nodeDescription
            && data.nodeKind == before.nodeKind && data.absoluteDay == before.absoluteDay && data.sequence == before.sequence
            && data.useSupply == before.useSupply && data.experienceReward == before.experienceReward && data.participants.Count == before.participants.Count
            && data.participants.Select(value => (value.characterId, value.displayName, value.experienceEligible, value.experienceDisplayName))
                .SequenceEqual(before.participants.Select(value => (value.characterId, value.displayName, value.experienceEligible, value.experienceDisplayName)));
    }
    public static OffenseNodeResolutionFact Capture(OffenseExpeditionRun expedition, OffenseRouteNode node,
        int day, long sequence, bool useSupply)
    {
        if (expedition?.Target == null || node == null) throw new ArgumentException("offense-node-context-missing");
        return new(new OffenseNodeResolutionFactData
        {
            expeditionId = expedition.ExpeditionId, targetId = expedition.Target.id, targetTitle = expedition.Target.title ?? string.Empty,
            regionId = expedition.Target.regionId ?? string.Empty, regionDisplayName = expedition.Target.regionDisplayName ?? string.Empty,
            factionId = expedition.Target.factionId ?? string.Empty, nodeId = node.Id, nodeTitle = node.Title ?? string.Empty,
            nodeDescription = node.Description ?? string.Empty, nodeKind = node.Kind, absoluteDay = day, sequence = sequence, useSupply = useSupply,
            experienceReward = new OffenseExpeditionExperienceRules().CalculateNodeExperience(node, expedition.Target.campaignOrder),
            participants = expedition.MemberActors.Concat(expedition.ProtectedRescueActors)
                .Where(value => value?.Identity != null).GroupBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
                .Select(group => new OffenseNodeParticipantData { characterId = group.Key, displayName = group.First().name,
                    experienceDisplayName = group.First().BuildingDisplayName,
                    experienceEligible = !group.First().IsDead && group.First().Progression != null
                        && expedition.MemberStates.Any(member => member?.Actor == group.First()) }).ToList()
        });
    }
    public OffenseNodeResolutionFact Resolve(OffenseExpeditionNodeResult result)
    {
        if (data.resolved || result == null) throw new ArgumentException("offense-node-result-invalid");
        var actual = Snapshot();
        actual.resolved = true; actual.usedSupply = result.UsedSupply; actual.gainedLoot = result.GainedLoot; actual.message = result.Message;
        return new(actual);
    }
    private static bool Text(string value) => DomainFactValueSyntax.IsText(value, 16384, true);
    private static bool OptionalId(string value) => value == string.Empty || DomainFactValueSyntax.IsIdentity(value);
}

[Serializable]
public sealed class OffenseNodeExperienceData
{
    public string characterId = string.Empty, displayName = string.Empty;
    public int requestedAmount, levelBefore, levelAfter, experienceBefore, experienceAfter;
    public OffenseNodeExperienceData Clone() => (OffenseNodeExperienceData)MemberwiseClone();
}

public sealed class OffenseNodeExperienceObservedFact
{
    private readonly OffenseNodeExperienceData experience;
    public OffenseNodeExperienceObservedFact(OffenseNodeResolutionFact resolution, OffenseNodeExperienceData experience)
    {
        Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        var context = resolution.Snapshot();
        if (!context.resolved || experience == null || experience.requestedAmount < 1
            || experience.levelBefore < 1 || experience.levelAfter < experience.levelBefore
            || experience.experienceBefore < 0 || experience.experienceAfter < 0
            || experience.requestedAmount != context.experienceReward
            || !context.participants.Any(value => value.characterId == experience.characterId && value.experienceEligible
                && value.experienceDisplayName == experience.displayName)
            || !DomainFactValueSyntax.IsText(experience.displayName, 16384, true))
            throw new ArgumentException("offense-node-experience-invalid");
        this.experience = experience.Clone();
    }
    public OffenseNodeResolutionFact Resolution { get; }
    public OffenseNodeExperienceData Experience => experience.Clone();
}
