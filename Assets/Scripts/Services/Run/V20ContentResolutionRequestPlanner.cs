using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

internal sealed class V20ContentResolutionRequestPlanner
{
    private readonly V20CampaignRuntime live;
    private readonly V20StoryContentCatalog catalog;
    private readonly ICharacterNarrativeCatalog narrativeCatalog;
    private readonly ICharacterNarrativeQuery narrativeQuery;
    private readonly IWorldDropZoneQuery dropZones;

    internal V20ContentResolutionRequestPlanner(
        V20CampaignRuntime live,
        V20StoryContentCatalog catalog,
        ICharacterNarrativeCatalog narrativeCatalog,
        ICharacterNarrativeQuery narrativeQuery,
        IWorldDropZoneQuery dropZones)
    {
        this.live = live;
        this.catalog = catalog;
        this.narrativeCatalog = narrativeCatalog;
        this.narrativeQuery = narrativeQuery;
        this.dropZones = dropZones;
    }

    internal bool TryResolveCandidate(
        V20CampaignRuntime candidate,
        ContentResolutionRequest request,
        out IReadOnlyList<V20ResolvedEventResult> resolved,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        resolved = Array.Empty<V20ResolvedEventResult>();
        bool succeeded;
        string rawFailure;
        V20ResolvedEventResult one;
        switch (request.Kind)
        {
            case ContentResolutionRequestKind.DailyEvaluation:
                if (request.DailyContext == null)
                {
                    failure = new DomainFailure(FailureCode.OperatingDayNotStarted);
                    return false;
                }
                resolved = candidate.EvaluateDaily(request.DailyContext);
                return true;
            case ContentResolutionRequestKind.SocietyChoice:
                succeeded = candidate.TryResolveSocietyEvent(
                    request.InstanceId,
                    request.ChoiceId,
                    request.Requirements,
                    out one,
                    out rawFailure);
                break;
            case ContentResolutionRequestKind.GuestRequestDeliveryOutcome:
                succeeded = candidate.TryPublishGuestRequestDeliveryOutcome(
                    request.InstanceId,
                    request.ChoiceId,
                    out one,
                    out rawFailure);
                break;
            case ContentResolutionRequestKind.FactionChapterChoice:
                succeeded = candidate.TryResolveChapter(
                    request.FactionId,
                    request.ChoiceId,
                    request.Requirements,
                    out one,
                    out rawFailure);
                break;
            case ContentResolutionRequestKind.FactionContractAccept:
                FactionContractDefinitionSO acceptedContract = catalog.Contracts
                    .FirstOrDefault(value => string.Equals(
                        value.StableId,
                        request.ContractId?.Trim(),
                        StringComparison.Ordinal)
                        && string.Equals(
                            value.factionId,
                            request.FactionId?.Trim(),
                            StringComparison.Ordinal));
                if (FactionContractMaterialRules.HasMaterialRequirements(
                        acceptedContract))
                {
                    if (!dropZones.TryGetDeliveryDropoff(
                            out Vector2Int contractDropoff))
                    {
                        failure = new DomainFailure(
                            FailureCode.ProductionOutputUnavailable);
                        return false;
                    }
                    FactionContractDeliveryTarget target = new(
                        EconomyProjectInputOwnerAuthority
                            .BuildFactionContractDestinationId(
                                acceptedContract.StableId),
                        contractDropoff);
                    succeeded = ((IFactionCampaignDeliveryCommand)candidate)
                        .TryAcceptContract(
                            request.FactionId,
                            request.ContractId,
                            request.AbsoluteDay,
                            target,
                            out rawFailure);
                }
                else
                {
                    succeeded = candidate.TryAcceptContract(
                        request.FactionId,
                        request.ContractId,
                        request.AbsoluteDay,
                        out rawFailure);
                }
                one = default;
                break;
            case ContentResolutionRequestKind.SeasonalFactionContractAccept:
                V20ActiveEventSaveData seasonalOccurrence = candidate
                    .ActiveSeasonalEvents.FirstOrDefault(value => value != null
                        && !value.resolved
                        && string.Equals(
                            value.instanceId,
                            request.InstanceId?.Trim(),
                            StringComparison.Ordinal));
                FactionContractDefinitionSO seasonalContract = catalog.Contracts
                    .FirstOrDefault(value => seasonalOccurrence != null
                        && string.Equals(
                            value.StableId,
                            request.ContractId?.Trim(),
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.factionId,
                            seasonalOccurrence.contextFactionId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.seasonalEventId,
                            seasonalOccurrence.definitionId,
                            StringComparison.Ordinal));
                if (seasonalContract == null
                    || !FactionContractMaterialRules.HasMaterialRequirements(
                        seasonalContract)
                    || !dropZones.TryGetDeliveryDropoff(
                        out Vector2Int seasonalDropoff))
                {
                    failure = new DomainFailure(
                        FailureCode.ProductionOutputUnavailable);
                    return false;
                }
                request.FactionId = seasonalContract.factionId;
                succeeded = ((IFactionCampaignDeliveryCommand)candidate)
                    .TryAcceptSeasonalContract(
                        seasonalOccurrence.instanceId,
                        seasonalContract.StableId,
                        new FactionContractDeliveryTarget(
                            FactionContractDeliveryOutbox.FormatDestinationId(
                                seasonalContract.StableId,
                                seasonalOccurrence.instanceId),
                            seasonalDropoff),
                        out rawFailure);
                one = default;
                break;
            case ContentResolutionRequestKind.FactionContractOutcome:
                bool deliveredMaterial = request.ContractSucceeded
                    && candidate.TryGetFaction(
                        request.FactionId,
                        out FactionCampaignStateSaveData deliveryState)
                    && FactionContractDeliveryOutbox.HasPending(deliveryState);
                succeeded = deliveredMaterial
                    ? ((IFactionCampaignDeliveryCommand)candidate)
                        .TryResolveDeliveredContract(
                            request.FactionId,
                            request.Requirements,
                            out one,
                            out rawFailure)
                    : candidate.TryResolveContract(
                        request.FactionId,
                        request.ContractSucceeded,
                        request.Requirements,
                        out one,
                        out rawFailure);
                break;
            case ContentResolutionRequestKind.CulturalPractice:
            case ContentResolutionRequestKind.CulturalPracticeNeglect:
                CulturalPracticeDefinitionSO practice =
                    narrativeCatalog.Practices.FirstOrDefault(value =>
                        string.Equals(
                            value.StableId,
                            request.DefinitionId?.Trim(),
                            StringComparison.Ordinal));
                if (practice == null)
                {
                    failure = new DomainFailure(
                        FailureCode.ExternalInfluenceUnavailable);
                    return false;
                }
                bool neglected = request.Kind
                    == ContentResolutionRequestKind.CulturalPracticeNeglect;
                IReadOnlyList<V20ContentEffect> practiceEffects = neglected
                    ? practice.neglectedEffects
                        .Where(value => value != null)
                        .ToArray()
                    : WithConsumedRequirementItems(
                        practice.successEffects,
                        practice.requirements);
                candidate.ApplyResolvedEffects(practiceEffects);
                resolved = new[]
                {
                    new V20ResolvedEventResult(
                        practice.StableId,
                        neglected ? "neglected" : "performed",
                        practiceEffects,
                        request.ParticipantCharacterIds)
                };
                return true;
            default:
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
        }
        if (!succeeded)
        {
            failure = rawFailure?.StartsWith(
                    "BLOCKED_CONTRACT:",
                    StringComparison.Ordinal) == true
                ? new DomainFailure(
                    FailureCode.ServiceProcessContractMissing,
                    "wim040-incident-response",
                    rawFailure)
                : new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        resolved = (request.Kind is ContentResolutionRequestKind.FactionContractAccept
                or ContentResolutionRequestKind.SeasonalFactionContractAccept
            || request.Kind == ContentResolutionRequestKind.SocietyChoice
                && string.IsNullOrEmpty(one.DefinitionId))
            ? Array.Empty<V20ResolvedEventResult>()
            : new[] { one };
        return true;
    }

    internal IReadOnlyList<V20ResolvedEventResult>
        ExpandAmbitionCompletionRewards(
            V20CampaignRuntime candidate,
            IReadOnlyList<V20ResolvedEventResult> resolved)
    {
        List<V20ResolvedEventResult> expanded = (resolved
                ?? Array.Empty<V20ResolvedEventResult>())
            .ToList();
        foreach (V20ResolvedEventResult resolution in
                 expanded.ToArray())
        {
            int progress = resolution.Effects
                .Where(value => value != null
                    && value.kind == V20ContentEffectKind.AmbitionProgress)
                .Sum(value => Math.Max(0, Mathf.RoundToInt(value.amount)));
            if (progress <= 0)
            {
                continue;
            }
            foreach (string participantId in
                     resolution.ParticipantCharacterIds
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.Ordinal))
            {
                if (!narrativeQuery.TryPreviewAmbitionProgress(
                        new CharacterId(participantId),
                        progress,
                        out AmbitionProgressPreview preview)
                    || !preview.Completes)
                {
                    continue;
                }
                V20ContentEffect[] rewards = preview.CompletionRewards
                    .Where(value => value != null && value.IsValid)
                    .ToArray();
                if (rewards.Length == 0)
                {
                    continue;
                }
                candidate.ApplyResolvedEffects(
                    rewards,
                    resolution.ContextFactionId);
                expanded.Add(new V20ResolvedEventResult(
                    $"ambition-reward:{preview.AmbitionId.Value}",
                    "completed",
                    rewards,
                    resolution.ParticipantCharacterIds,
                    resolution.ContextFactionId));
            }
        }
        return expanded.AsReadOnly();
    }

    internal bool TryResolveRequestRequirements(
        ContentResolutionRequest request,
        RunMilestoneEvaluationSnapshot world,
        out V20ContentRequirementSet resolved,
        out IReadOnlyList<string> participants,
        out DomainFailure failure)
    {
        resolved = new V20ContentRequirementSet();
        participants = Array.Empty<string>();
        failure = DomainFailure.None;
        if (world == null)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (request.Kind == ContentResolutionRequestKind.DailyEvaluation
            || (request.Kind is ContentResolutionRequestKind.FactionContractAccept
                or ContentResolutionRequestKind.SeasonalFactionContractAccept))
        {
            return true;
        }
        if (request.Kind == ContentResolutionRequestKind.CulturalPractice
            || request.Kind == ContentResolutionRequestKind.CulturalPracticeNeglect)
        {
            CulturalPracticeDefinitionSO practice =
                narrativeCatalog.Practices.FirstOrDefault(value =>
                    string.Equals(
                        value.StableId,
                        request.DefinitionId?.Trim(),
                        StringComparison.Ordinal));
            if (practice == null)
            {
                return false;
            }
            participants = (request.ParticipantCharacterIds
                    ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (participants.Count == 0)
            {
                return false;
            }
            foreach (string participant in participants)
            {
                CharacterId characterId = new(participant);
                if (!narrativeQuery.TryGet(characterId, out _)
                    || !narrativeQuery.CanPerformPractice(
                            characterId,
                            practice.StableId,
                            request.AbsoluteDay,
                            out _))
                {
                    return false;
                }
            }
            resolved = request.Kind == ContentResolutionRequestKind.CulturalPractice
                ? practice.requirements
                : new V20ContentRequirementSet();
            return true;
        }
        if (request.Kind == ContentResolutionRequestKind.SocietyChoice)
        {
            V20ActiveEventSaveData active = live.ActiveSocietyEvents.FirstOrDefault(value =>
                string.Equals(value.instanceId, request.InstanceId?.Trim(), StringComparison.Ordinal));
            if (active == null) return false;
            participants = active.participantCharacterIds.AsReadOnly();
            V20AuthoredContentSO definition = ((ISocietyEventCatalog)catalog)
                .Require(active.definitionId);
            if (definition is GuestRequestDefinitionSO guest)
            {
                resolved = string.Equals(
                        request.ChoiceId,
                        "fulfill",
                        StringComparison.Ordinal)
                    && GuestRequestDeliveryMaterialRules.HasPhysicalDelivery(
                        guest)
                    ? GuestRequestDeliveryMaterialRules
                        .BuildNonDeliveryRequirements(guest)
                    : string.Equals(
                            request.ChoiceId,
                            "fulfill",
                            StringComparison.Ordinal)
                        ? guest.serviceRequirements
                    : new V20ContentRequirementSet();
                return true;
            }
            V20ChoiceDefinition choice = Choices(definition).FirstOrDefault(value =>
                string.Equals(value.choiceId, request.ChoiceId?.Trim(), StringComparison.Ordinal));
            if (choice == null) return false;
            resolved = choice.requirements;
            return true;
        }
        if (request.Kind ==
            ContentResolutionRequestKind.GuestRequestDeliveryOutcome)
        {
            V20ActiveEventSaveData active = live.ActiveSocietyEvents
                .FirstOrDefault(value => value != null && string.Equals(
                    value.instanceId,
                    request.InstanceId?.Trim(),
                    StringComparison.Ordinal));
            GuestRequestDefinitionSO guest = active == null
                ? null
                : ((ISocietyEventCatalog)catalog).Require(active.definitionId)
                    as GuestRequestDefinitionSO;
            if (guest == null)
                return false;
            participants = active.participantCharacterIds.AsReadOnly();
            resolved = new V20ContentRequirementSet();
            return true;
        }
        if (!live.TryGetFaction(request.FactionId, out FactionCampaignStateSaveData faction))
            return false;
        if (request.Kind == ContentResolutionRequestKind.FactionChapterChoice)
        {
            FactionChapterDefinitionSO chapter = catalog.Chapters.Single(value =>
                string.Equals(value.factionId, faction.factionId, StringComparison.Ordinal)
                && value.chapterNumber == faction.currentChapter);
            V20ChoiceDefinition choice = chapter.choices.FirstOrDefault(value =>
                string.Equals(value.choiceId, request.ChoiceId?.Trim(), StringComparison.Ordinal));
            if (choice == null) return false;
            resolved = MergeRequirements(chapter.triggerRequirements, choice.requirements);
            return true;
        }
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, faction.activeContractId, StringComparison.Ordinal));
        if (contract == null) return false;
        resolved = request.ContractSucceeded
            ? FactionContractMaterialRules.HasMaterialRequirements(contract)
                ? new V20ContentRequirementSet()
                : contract.completionRequirements
            : new V20ContentRequirementSet();
        return true;
    }

    internal V20ContentRequirementSet RequirementsForResolvedEffect(
        V20ResolvedEventResult result)
    {
        if (result.ResolutionId == "started")
        {
            SeasonalWorldEventDefinitionSO seasonal = catalog.SeasonalEvents
                .FirstOrDefault(value => string.Equals(
                    value.StableId,
                    result.DefinitionId,
                    StringComparison.Ordinal));
            return seasonal?.triggerRequirements ?? new V20ContentRequirementSet();
        }
        if (result.ResolutionId == "automatic")
        {
            LifeEventDefinitionSO lifeEvent = catalog.LifeEvents.FirstOrDefault(value =>
                string.Equals(value.StableId, result.DefinitionId, StringComparison.Ordinal));
            return lifeEvent?.triggerRequirements ?? new V20ContentRequirementSet();
        }
        return new V20ContentRequirementSet();
    }

    private static IReadOnlyList<V20ContentEffect> WithConsumedRequirementItems(
        IEnumerable<V20ContentEffect> effects,
        V20ContentRequirementSet requirements)
    {
        List<V20ContentEffect> result = (effects
                ?? Array.Empty<V20ContentEffect>())
            .Where(value => value != null)
            .ToList();
        foreach (IGrouping<string, V20ItemAmountRequirement> group in
                 (requirements?.items
                     ?? new List<V20ItemAmountRequirement>())
                 .Where(value => value != null
                     && value.consume
                     && !string.IsNullOrWhiteSpace(
                         value.itemDefinitionId))
                 .GroupBy(
                     value => value.itemDefinitionId.Trim(),
                     StringComparer.Ordinal))
        {
            result.Add(new V20ContentEffect
            {
                kind = V20ContentEffectKind.ItemConsume,
                targetId = group.Key,
                amount = group.Sum(value => Math.Max(0, value.amount))
            });
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<V20ChoiceDefinition> Choices(
        V20AuthoredContentSO definition) => definition switch
        {
            LifeEventDefinitionSO value => value.choices,
            ServiceIncidentDefinitionSO value => value.responses,
            _ => Array.Empty<V20ChoiceDefinition>()
        };

    private static V20ContentRequirementSet MergeRequirements(
        V20ContentRequirementSet first,
        V20ContentRequirementSet second) => new()
        {
            items = (first?.items ?? new()).Concat(second?.items ?? new()).ToList(),
            facilities = (first?.facilities ?? new()).Concat(second?.facilities ?? new()).ToList(),
            research = (first?.research ?? new()).Concat(second?.research ?? new()).ToList(),
            characters = (first?.characters ?? new()).Concat(second?.characters ?? new()).ToList(),
            factions = (first?.factions ?? new()).Concat(second?.factions ?? new()).ToList(),
            worldMetrics = (first?.worldMetrics ?? new()).Concat(second?.worldMetrics ?? new()).ToList(),
            requiredFlags = (first?.requiredFlags ?? new()).Concat(second?.requiredFlags ?? new()).ToList(),
            excludedFlags = (first?.excludedFlags ?? new()).Concat(second?.excludedFlags ?? new()).ToList()
        };
}
