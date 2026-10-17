using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

public sealed class ContentRequirementEvaluator : IContentRequirementEvaluator
{
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterLifeQuery life;
    private readonly IKinshipQuery kinship;
    private readonly ICharacterNarrativeQuery narrative;
    private readonly IFacilityCapabilityQuery facilities;

    public ContentRequirementEvaluator(
        ICharacterWorldQuery characters,
        ICharacterLifeQuery life,
        IKinshipQuery kinship,
        ICharacterNarrativeQuery narrative,
        IFacilityCapabilityQuery facilities)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
    }

    public bool TryEvaluate(
        V20ContentRequirementSet requirements,
        RunMilestoneEvaluationSnapshot world,
        IReadOnlyList<string> participantCharacterIds,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        requirements ??= new V20ContentRequirementSet();
        if (world == null)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (!(requirements.research ?? new()).All(value =>
                value != null && world.CompletedResearchIds.Contains(value.researchNumericId))
            || !(requirements.requiredFlags ?? new()).All(world.WorldFlags.Contains)
            || !(requirements.excludedFlags ?? new()).All(value => !world.WorldFlags.Contains(value))
            || !(requirements.worldMetrics ?? new()).All(value => value != null
                && world.WorldMetrics.TryGetValue(value.kind, out float actual)
                && actual >= value.minimumValue)
            || !(requirements.items ?? new()).All(value => value != null
                && world.ItemQuantities.TryGetValue(value.itemDefinitionId, out int count)
                && count >= value.amount)
            || !(requirements.factions ?? new()).All(value => value != null
                && world.Factions.TryGetValue(value.factionId, out FactionCampaignStateSaveData faction)
                && faction.rapport >= value.minimumRapport
                && faction.grievance <= value.maximumGrievance
                && faction.obligationTokens >= value.minimumObligationTokens))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        foreach (V20FacilityRequirement requirement in
                 requirements.facilities ?? new List<V20FacilityRequirement>())
        {
            if (requirement == null)
            {
                failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
                return false;
            }
            FacilityCapabilityKind capability = ParseCapability(
                requirement.capabilityId);
            if (!string.IsNullOrWhiteSpace(requirement.capabilityId)
                && capability == FacilityCapabilityKind.None)
            {
                failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
                return false;
            }
            int count = requirement.mustBeOperational
                ? facilities.FindOperational(
                    capability,
                    requirement.buildingDefinitionId).Count
                : world.FacilityCounts.TryGetValue(
                    V20CampaignRuntime.FacilityRequirementKey(requirement),
                    out int authoredCount)
                        ? authoredCount
                        : 0;
            if (count < requirement.minimumCount)
            {
                failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
                return false;
            }
        }

        CharacterActor[] candidates = ResolveParticipants(participantCharacterIds);
        foreach (V20CharacterRequirement requirement in
                 requirements.characters ?? new List<V20CharacterRequirement>())
        {
            int match = Array.FindIndex(candidates, actor =>
                Matches(actor, requirement));
            if (match < 0)
            {
                failure = new DomainFailure(FailureCode.CharacterMedicalPatientUnavailable);
                return false;
            }
            candidates = candidates.Where((_, index) => index != match).ToArray();
        }
        return true;
    }

    public bool IsLivingCharacterAtStage(
        CharacterId characterId,
        CharacterLifeStage lifeStage)
    {
        if (!characterId.IsValid
            || kinship.TryGetTombstone(characterId, out _)
            || !life.TryGet(characterId, out CharacterLifeRecord record)
            || record.LifeStage != lifeStage)
        {
            return false;
        }

        CharacterActor present = characters.Characters.FirstOrDefault(actor =>
            actor?.Identity != null
            && string.Equals(
                actor.Identity.PersistentId,
                characterId.Value,
                StringComparison.Ordinal));
        return present == null
            || (!present.IsDead
                && present.CurrentHealth > 0f
                && present.CurrentLifecycleState !=
                    CharacterLifecycleState.Despawned);
    }

    private CharacterActor[] ResolveParticipants(
        IReadOnlyList<string> participantCharacterIds)
    {
        HashSet<string> requested = (participantCharacterIds
                ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        return characters.Characters
            .Where(value => value != null
                && value.Identity != null
                && value.CurrentHealth > 0f
                && (requested.Count == 0
                    || requested.Contains(value.Identity.PersistentId)))
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToArray();
    }

    private bool Matches(
        CharacterActor actor,
        V20CharacterRequirement requirement)
    {
        if (actor == null || requirement == null
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            || !life.TryGet(id, out CharacterLifeRecord record)
            || record.LifeStage < requirement.minimumLifeStage
            || record.LifeStage > requirement.maximumLifeStage
            || actor.CurrentHealth < requirement.minimumHealth)
        {
            return false;
        }
        HashSet<string> traits = new(StringComparer.Ordinal);
        foreach (CharacterTraitSO trait in actor.Progression?.ResolveSelectedTraits()
                     ?? Array.Empty<CharacterTraitSO>())
        {
            traits.Add(trait.DefinitionId.Value);
            traits.Add(trait.id.ToString());
        }
        if (narrative.TryGet(id, out CharacterNarrativeSnapshot snapshot))
        {
            traits.UnionWith(snapshot.ExpressedHeritableTraitIds
                ?? Array.Empty<string>());
        }
        return (string.IsNullOrWhiteSpace(requirement.requiredTraitId)
                || traits.Contains(requirement.requiredTraitId.Trim()))
            && (string.IsNullOrWhiteSpace(requirement.excludedTraitId)
                || !traits.Contains(requirement.excludedTraitId.Trim()));
    }

    private static FacilityCapabilityKind ParseCapability(string id)
    {
        string normalized = id?.Trim() ?? string.Empty;
        if (normalized.Length == 0) return FacilityCapabilityKind.None;
        string leaf = normalized.Split(':', '.', '/').Last();
        return Enum.TryParse(leaf, true, out FacilityCapabilityKind result)
            ? result
            : FacilityCapabilityKind.None;
    }
}

