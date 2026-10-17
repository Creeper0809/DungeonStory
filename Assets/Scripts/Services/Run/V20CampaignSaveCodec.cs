using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Foundation.Facts;
using static V20CampaignIdentity;
using static V20ContentDefinitionRules;
using static V20ObservedEventEvidenceRules;

internal sealed class V20CampaignSaveCodec
{
    private readonly V20StoryContentCatalog catalog;
    private readonly V20EventSaveValidation events;
    internal V20CampaignSaveCodec(V20StoryContentCatalog catalog, V20EventSaveValidation events)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    internal SeasonalEventWorldSaveData ValidateSeasonal(SeasonalEventWorldSaveData data)
    {
        if (data == null || data.version != SeasonalEventWorldSaveData.CurrentVersion || data.activeEvents == null || data.completedEventIds == null || data.lastEvaluationAbsoluteDay < -1)
            throw new InvalidOperationException("Seasonal-event save payload is invalid.");
        events.RequireValidEvents(data.activeEvents, seasonal: true);
        foreach (string id in data.completedEventIds) ((IWorldEventCatalog)catalog).Require(id);
        RequireUniqueIds(data.completedEventIds, "seasonal completion");
        return data;
    }
    internal FactionCampaignWorldSaveData ValidateFactions(
        FactionCampaignWorldSaveData data,
        SeasonalEventWorldSaveData seasonalData)
    {
        if (data == null
            || data.version != FactionCampaignWorldSaveData.CurrentVersion
            || data.factions == null
            || seasonalData != null && seasonalData.activeEvents == null)
            throw new InvalidOperationException("Faction-campaign save payload is invalid.");
        if (data.factions.Count != catalog.Arcs.Count)
            throw new InvalidOperationException("Faction-campaign save must contain all six authored factions.");
        foreach (FactionCampaignStateSaveData value in data.factions)
        {
            FactionArcDefinitionSO arc = catalog.Arcs.FirstOrDefault(candidate =>
                value != null && string.Equals(
                    candidate.factionId,
                    value.factionId,
                    StringComparison.Ordinal));
            if (value != null)
            {
                value.activeContractOccurrenceId ??= string.Empty;
                value.lastSeasonalContractId ??= string.Empty;
                value.lastSeasonalOccurrenceId ??= string.Empty;
            }
            if (value == null || arc == null || value.rapport < -100 || value.rapport > 100 || value.grievance < 0 || value.grievance > 100 || value.obligationTokens < 0 || value.obligationTokens > 5 || value.currentChapter < 1 || value.currentChapter > 7
                || value.completedContractIds == null
                || value.failedContractIds == null
                || value.majorChoiceFlags == null
                || value.deliverySourceStackIds == null
                || value.activeContractDestinationOwnerId == null
                || value.activeContractDestinationId == null
                || value.activeContractInputCapacityFingerprint == null
                || value.deliveryContractId == null
                || value.deliveryOperationId == null
                || value.deliveryCommitId == null
                || value.deliveryFailureReason == null)
                throw new InvalidOperationException("Faction-campaign state is invalid.");
            bool hasLastSeasonal = !string.IsNullOrEmpty(
                value.lastSeasonalOccurrenceId);
            if (hasLastSeasonal != !string.IsNullOrEmpty(
                    value.lastSeasonalContractId)
                || hasLastSeasonal
                    && !catalog.Contracts.Any(contract => contract != null
                        && !string.IsNullOrEmpty(contract.seasonalEventId)
                        && string.Equals(
                            contract.StableId,
                            value.lastSeasonalContractId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            contract.factionId,
                            value.factionId,
                            StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "Faction seasonal contract history is invalid.");
            foreach (string id in (value.completedContractIds ?? new()).Concat(value.failedContractIds ?? new())) ((IFactionStoryCatalog)catalog).Require(id);
            if (!string.IsNullOrWhiteSpace(value.activeContractId))
            {
                FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(candidate =>
                    string.Equals(candidate.StableId, value.activeContractId, StringComparison.Ordinal)
                    && string.Equals(candidate.factionId, value.factionId, StringComparison.Ordinal));
                bool seasonal = contract != null
                    && !string.IsNullOrEmpty(contract.seasonalEventId);
                if (contract == null
                    || value.activeContractDeadlineAbsoluteDay < 0
                    || seasonal != !string.IsNullOrEmpty(
                        value.activeContractOccurrenceId)
                    || seasonal
                        && (!string.Equals(
                                value.lastSeasonalContractId,
                                contract.StableId,
                                StringComparison.Ordinal)
                            || !string.Equals(
                                value.lastSeasonalOccurrenceId,
                                value.activeContractOccurrenceId,
                                StringComparison.Ordinal)
                            || seasonalData != null
                                && !seasonalData.activeEvents.Any(occurrence =>
                                occurrence != null
                                && !occurrence.resolved
                                && string.Equals(
                                    occurrence.instanceId,
                                    value.activeContractOccurrenceId,
                                    StringComparison.Ordinal)
                                && string.Equals(
                                    occurrence.definitionId,
                                    contract.seasonalEventId,
                                    StringComparison.Ordinal)
                                && string.Equals(
                                    occurrence.contextFactionId,
                                    value.factionId,
                                    StringComparison.Ordinal)
                                && occurrence.deadlineAbsoluteDay ==
                                    value.activeContractDeadlineAbsoluteDay)))
                    throw new InvalidOperationException("Faction active contract is invalid.");
                bool material = FactionContractMaterialRules
                    .HasMaterialRequirements(contract);
                if (material
                    && (!value.activeContractInputOwnerActive
                        || value.deliveryTerminalCleanupPending
                        || !string.Equals(
                            value.activeContractDestinationOwnerId,
                            FactionContractDeliveryOutbox.FormatOwnerId(
                                contract.StableId,
                                value.activeContractOccurrenceId),
                            StringComparison.Ordinal)
                        || !string.Equals(
                            value.activeContractDestinationId,
                            FactionContractDeliveryOutbox.FormatDestinationId(
                                contract.StableId,
                                value.activeContractOccurrenceId),
                            StringComparison.Ordinal)
                        || value.activeContractInputCapacityGrams <= 0L
                        || value.activeContractInputMassAuthorityRevision <= 0L
                        || string.IsNullOrWhiteSpace(
                            value.activeContractInputCapacityFingerprint)
                        || value.deliveryCommitPhase ==
                            FactionContractDeliveryCommitPhase.RewardPublished))
                    throw new InvalidOperationException(
                        "Faction material contract destination is invalid.");
                if (!material
                    && (!FactionContractDeliveryOutbox.HasCanonicalEmpty(value)
                        || value.activeContractInputOwnerActive
                        || value.deliveryTerminalCleanupPending
                        || !string.IsNullOrEmpty(
                            value.activeContractDestinationOwnerId)
                        || !string.IsNullOrEmpty(
                            value.activeContractDestinationId)))
                    throw new InvalidOperationException(
                        "Faction non-material contract owns a delivery target.");
            }
            else if (value.deliveryCommitPhase ==
                    FactionContractDeliveryCommitPhase.PhysicalCommitted
                || value.activeContractInputOwnerActive
                    != value.deliveryTerminalCleanupPending)
                throw new InvalidOperationException(
                    "Faction terminal delivery state is invalid.");

            bool pending = FactionContractDeliveryOutbox.HasPending(value);
            if (pending
                ? !FactionContractDeliveryOutbox.HasCanonicalPending(value)
                : !FactionContractDeliveryOutbox.HasCanonicalEmpty(value))
                throw new InvalidOperationException(
                    "Faction contract delivery outbox is invalid.");
            if (pending)
            {
                FactionContractDefinitionSO deliveryContract = catalog.Contracts
                    .FirstOrDefault(candidate => string.Equals(
                        candidate.StableId,
                        value.deliveryContractId,
                        StringComparison.Ordinal)
                        && string.Equals(
                            candidate.factionId,
                            value.factionId,
                            StringComparison.Ordinal));
                int requiredQuantity = FactionContractMaterialRules
                    .BuildMaterialRequirements(deliveryContract)
                    .Values.Sum();
                if (deliveryContract == null
                    || value.deliveryQuantity != requiredQuantity
                    || !string.Equals(
                        value.activeContractDestinationOwnerId,
                        FactionContractDeliveryOutbox.FormatOwnerId(
                            deliveryContract.StableId,
                            value.activeContractOccurrenceId),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        value.activeContractDestinationId,
                        FactionContractDeliveryOutbox.FormatDestinationId(
                            deliveryContract.StableId,
                            value.activeContractOccurrenceId),
                        StringComparison.Ordinal)
                    || value.deliveryCommitPhase ==
                            FactionContractDeliveryCommitPhase.RewardPublished
                        && !value.completedContractIds.Contains(
                            deliveryContract.StableId,
                            StringComparer.Ordinal))
                    throw new InvalidOperationException(
                        "Faction contract delivery receipt identity is invalid.");
            }
            if (!value.activeContractInputOwnerActive
                && !pending
                && (!string.IsNullOrEmpty(value.activeContractDestinationId)
                    || !string.IsNullOrEmpty(
                        value.activeContractDestinationOwnerId)))
                throw new InvalidOperationException(
                    "Faction contract retained an ownerless destination.");
            if (value.activeContractInputOwnerActive
                && (string.IsNullOrWhiteSpace(
                        value.activeContractDestinationOwnerId)
                    || !string.Equals(
                        value.activeContractDestinationId,
                        EconomyProjectInputOwnerAuthority
                            .BuildFactionContractDestinationId(
                                value.activeContractDestinationOwnerId),
                        StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "Faction contract input-owner identity is invalid.");
            bool ownsLifecycle = !string.IsNullOrEmpty(value.activeContractId)
                || value.activeContractInputOwnerActive
                || value.deliveryTerminalCleanupPending
                || pending;
            if (!ownsLifecycle
                && !string.IsNullOrEmpty(value.activeContractOccurrenceId))
                throw new InvalidOperationException(
                    "Faction contract retained ownerless occurrence provenance.");
            if (!string.IsNullOrEmpty(value.activeContractOccurrenceId))
            {
                string lifecycleContractId = !string.IsNullOrEmpty(
                        value.activeContractId)
                    ? value.activeContractId
                    : !string.IsNullOrEmpty(value.deliveryContractId)
                        ? value.deliveryContractId
                        : FactionContractDeliveryOutbox.TryGetContractIdFromOwnerId(
                            value.activeContractDestinationOwnerId,
                            out string ownerContractId)
                                ? ownerContractId
                                : string.Empty;
                FactionContractDefinitionSO lifecycleContract = catalog.Contracts
                    .FirstOrDefault(contract => contract != null
                        && string.Equals(
                            contract.StableId,
                            lifecycleContractId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            contract.factionId,
                            value.factionId,
                            StringComparison.Ordinal));
                if (lifecycleContract == null
                    || string.IsNullOrEmpty(lifecycleContract.seasonalEventId)
                    || !string.Equals(
                        value.lastSeasonalContractId,
                        lifecycleContract.StableId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        value.lastSeasonalOccurrenceId,
                        value.activeContractOccurrenceId,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Faction seasonal delivery provenance is invalid.");
            }
            if (value.completedContractIds.Intersect(
                    value.failedContractIds,
                    StringComparer.Ordinal).Any())
                throw new InvalidOperationException(
                    "Faction contract is both completed and failed.");
            RequireUniqueIds(value.completedContractIds, "completed faction contract");
            RequireUniqueIds(value.failedContractIds, "failed faction contract");
            RequireUniqueIds(value.majorChoiceFlags, "faction choice flag");
        }
        RequireUniqueIds(data.factions.Select(value => value.factionId), "faction campaign");
        return data;
    }
    internal RunMilestoneWorldSaveData ValidateMilestones(RunMilestoneWorldSaveData data)
    {
        if (data == null || data.version != RunMilestoneWorldSaveData.CurrentVersion || data.completedMilestoneIds == null || data.grantedRewardIds == null || data.unlockedLandmarkIds == null || data.activePressureIds == null || data.endlessCrisisAxes == null || data.worldFlags == null || data.committedChoices == null || data.pendingContentResolutions == null || data.nextCommittedChoiceOrdinal < 0 || data.endlessCycle < 0 || data.selfSufficiencyStreakDays < 0 || data.productivityCoverageStreakDays < 0 || data.lastMilestoneEvaluationAbsoluteDay < -1 || data.lastAccordSignalSupportAbsoluteDay < -1 || data.lastEndlessCrisisEvaluationAbsoluteDay < -1 || data.endlessCrisisInstanceId == null || data.endlessCrisisPendingInvasionSourceOwnerId == null || data.endlessCrisisDirectResponseOwnerId == null
            || (data.pendingAccordSignalOperationId.Length == 0) != (data.pendingAccordSignalCommitId.Length == 0)
            || data.pendingAccordSignalOperationId.Length > 0 && (data.pendingAccordSignalSourceStackId.Length == 0 || data.pendingAccordSignalMassGrams <= 0 || data.lastAccordSignalSupportAbsoluteDay < 1))
            throw new InvalidOperationException("Milestone save payload is invalid.");
        foreach (string id in data.completedMilestoneIds) catalog.Require(id);
        if (data.completedMilestoneIds.Distinct(StringComparer.Ordinal).Count() != data.completedMilestoneIds.Count)
            throw new InvalidOperationException("Milestone completion ids are duplicated.");
        ValidateEndlessCrisis(data);
        RequireUniqueIds(data.grantedRewardIds, "milestone reward");
        RequireUniqueIds(data.unlockedLandmarkIds, "milestone landmark");
        RequireUniqueIds(data.activePressureIds, "milestone pressure");
        RequireUniqueIds(data.worldFlags, "milestone world flag");
        long expectedOrdinal = 0;
        HashSet<string> operations = new(StringComparer.Ordinal);
        HashSet<string> decisions = new(StringComparer.Ordinal);
        foreach (V20CommittedRunChoiceSaveData choice in data.committedChoices)
        {
            ValidateCommittedChoice(choice, validateOrdinal: true);
            expectedOrdinal++;
            if (choice.ordinal != expectedOrdinal
                || !operations.Add(choice.operationId)
                || !decisions.Add(CommittedDecisionKey(choice)))
            {
                throw new InvalidOperationException(
                    "Committed run choices contain an invalid order or duplicate identity.");
            }
        }
        if (data.nextCommittedChoiceOrdinal != expectedOrdinal)
        {
            throw new InvalidOperationException(
                "Committed run choice sequence does not match its immutable history.");
        }
        V20ContentRecoveryEvidence.ValidateMetadata(data.pendingContentResolutions);
        return data;
    }

    internal void ValidateEndlessCrisis(RunMilestoneWorldSaveData data)
    {
        if (!Enum.IsDefined(
                typeof(EndlessCrisisLifecyclePhase),
                data.endlessCrisisPhase))
            throw new InvalidOperationException(
                "Endless crisis has an invalid lifecycle phase.");

        bool hasHistory = data.endlessCrisisInstanceId.Length > 0;
        if (!hasHistory)
        {
            if (data.endlessCrisisPhase != EndlessCrisisLifecyclePhase.None
                || data.endlessCrisisAxes.Count != 0
                || data.endlessCrisisStartedAbsoluteDay != -1
                || data.endlessCrisisPressureEndAbsoluteDay != -1
                || data.endlessCrisisLastActualEndAbsoluteDay != -1
                || data.endlessCrisisRecoveryEndAbsoluteDay != -1
                || data.endlessCrisisAuthoredRecoveryDays != 0
                || data.endlessCrisisPendingInvasionSourceOwnerId.Length > 0
                || data.endlessCrisisDirectResponseOwnerId.Length > 0)
                throw new InvalidOperationException(
                    "Empty endless crisis state contains lifecycle data.");
            return;
        }

        if (!IsCanonicalRequiredId(data.endlessCrisisInstanceId)
            || data.endlessCycle < 1
            || data.endlessCrisisStartedAbsoluteDay < 1
            || data.lastEndlessCrisisEvaluationAbsoluteDay
                < data.endlessCrisisStartedAbsoluteDay
            || !string.Equals(
                data.endlessCrisisInstanceId,
                $"endless-crisis:{data.endlessCycle}:{data.endlessCrisisStartedAbsoluteDay}",
                StringComparison.Ordinal)
            || data.endlessCrisisAxes.Count is not (1 or 2))
            throw new InvalidOperationException(
                "Endless crisis identity, dates, or axis count are invalid.");

        bool compound = data.endlessCrisisAxes.Count == 2;
        int expectedRecoveryDays = compound
            ? catalog.EndlessCrisisPolicy.compoundAxisRecoveryDays
            : catalog.EndlessCrisisPolicy.singleAxisRecoveryDays;
        if (data.endlessCrisisPressureEndAbsoluteDay != checked(
                data.endlessCrisisStartedAbsoluteDay
                + catalog.EndlessCrisisPolicy.pressureDurationDays)
            || data.endlessCrisisAuthoredRecoveryDays
                != expectedRecoveryDays
            || data.endlessCrisisAxes.Any(value => value == null)
            || data.endlessCrisisAxes.Select(value => value.axis)
                .Distinct().Count() != data.endlessCrisisAxes.Count)
            throw new InvalidOperationException(
                "Endless crisis authored lifetime or axes are invalid.");

        foreach (EndlessCrisisAxisSaveData modifier in
                 data.endlessCrisisAxes)
        {
            EndlessCrisisAxisPolicy authored =
                catalog.EndlessCrisisPolicy.Require(modifier.axis);
            float expectedMultiplier = compound
                ? authored.compoundAxisMultiplier
                : authored.singleAxisMultiplier;
            if (!string.Equals(
                    modifier.effectOwnerId,
                    EffectOwnerId(
                        data.endlessCrisisInstanceId,
                        modifier.axis),
                    StringComparison.Ordinal)
                || Math.Abs(modifier.multiplier - expectedMultiplier) > 0.000001f)
                throw new InvalidOperationException(
                    "Endless crisis modifier owner or authored strength is invalid.");
        }

        EndlessCrisisAxisSaveData combat = data.endlessCrisisAxes
            .SingleOrDefault(value => value.axis == EndlessCrisisAxis.Combat);
        string combatOwner = combat?.effectOwnerId ?? string.Empty;
        bool validPending = data.endlessCrisisPendingInvasionSourceOwnerId
                .Length == 0
            || (combatOwner.Length > 0
            && string.Equals(
                data.endlessCrisisPendingInvasionSourceOwnerId,
                combatOwner,
                StringComparison.Ordinal));
        bool validResponse = data.endlessCrisisDirectResponseOwnerId.Length == 0
            || (combatOwner.Length > 0
            && string.Equals(
                data.endlessCrisisDirectResponseOwnerId,
                combatOwner + ":invasion",
                StringComparison.Ordinal));
        if (!validPending
            || !validResponse
            || data.endlessCrisisPendingInvasionSourceOwnerId.Length > 0
                && data.endlessCrisisDirectResponseOwnerId.Length > 0)
            throw new InvalidOperationException(
                "Endless crisis invasion ownership is invalid.");

        switch (data.endlessCrisisPhase)
        {
            case EndlessCrisisLifecyclePhase.Active:
                bool earlyDirectResponseEnded =
                    data.endlessCrisisLastActualEndAbsoluteDay >=
                        data.endlessCrisisStartedAbsoluteDay
                    && data.endlessCrisisLastActualEndAbsoluteDay <
                        data.endlessCrisisPressureEndAbsoluteDay
                    && data.endlessCrisisPendingInvasionSourceOwnerId.Length == 0
                    && data.endlessCrisisDirectResponseOwnerId.Length == 0;
                if (data.endlessCrisisRecoveryEndAbsoluteDay != -1
                    || data.endlessCrisisLastActualEndAbsoluteDay != -1
                        && !earlyDirectResponseEnded)
                    throw new InvalidOperationException(
                        "Active endless crisis contains terminal dates.");
                break;
            case EndlessCrisisLifecyclePhase.AwaitingDirectResponse:
                if (data.endlessCrisisPendingInvasionSourceOwnerId.Length > 0
                    || data.endlessCrisisDirectResponseOwnerId.Length == 0
                    || data.endlessCrisisLastActualEndAbsoluteDay != -1
                    || data.endlessCrisisRecoveryEndAbsoluteDay != -1)
                    throw new InvalidOperationException(
                        "Awaiting endless crisis has invalid response ownership.");
                break;
            case EndlessCrisisLifecyclePhase.Recovery:
            case EndlessCrisisLifecyclePhase.None:
                if (data.endlessCrisisPendingInvasionSourceOwnerId.Length > 0
                    || data.endlessCrisisDirectResponseOwnerId.Length > 0
                    || data.endlessCrisisLastActualEndAbsoluteDay
                        < data.endlessCrisisPressureEndAbsoluteDay
                    || data.endlessCrisisRecoveryEndAbsoluteDay != checked(
                        data.endlessCrisisLastActualEndAbsoluteDay
                        + data.endlessCrisisAuthoredRecoveryDays))
                    throw new InvalidOperationException(
                        "Completed endless crisis recovery lifetime is invalid.");
                break;
            default:
                throw new InvalidOperationException(
                    "Endless crisis lifecycle phase is unsupported.");
        }
    }

    internal void ValidateCommittedChoice(
        V20CommittedRunChoiceSaveData choice,
        bool validateOrdinal)
    {
        if (choice == null
            || !Enum.IsDefined(typeof(CommittedRunChoiceKind), choice.kind)
            || choice.kind == CommittedRunChoiceKind.None
            || !IsCanonicalRequiredId(choice.ownerId)
            || !IsCanonicalRequiredId(choice.definitionId)
            || !IsCanonicalRequiredId(choice.instanceId)
            || !IsCanonicalRequiredId(choice.choiceId)
            || !IsCanonicalRequiredId(choice.operationId)
            || validateOrdinal && choice.ordinal <= 0)
        {
            throw new InvalidOperationException(
                "Committed run choice contains an invalid kind, identifier, or ordinal.");
        }

        V20AuthoredContentSO definition = choice.kind switch
        {
            CommittedRunChoiceKind.SocietyEventChoice =>
                ((ISocietyEventCatalog)catalog).Require(choice.definitionId),
            CommittedRunChoiceKind.FactionChapterChoice =>
                ((IFactionStoryCatalog)catalog).Require(choice.definitionId),
            _ => throw new InvalidOperationException(
                "Committed run choice kind is unsupported.")
        };
        switch (choice.kind)
        {
            case CommittedRunChoiceKind.SocietyEventChoice:
                bool validSocietyDefinition = definition is LifeEventDefinitionSO
                    or GuestRequestDefinitionSO
                    or ServiceIncidentDefinitionSO;
                bool validSocietyChoice = definition is GuestRequestDefinitionSO
                    ? choice.choiceId is "fulfill" or "decline"
                    : GetChoices(definition).Any(value => value != null
                        && string.Equals(
                            value.choiceId,
                            choice.choiceId,
                            StringComparison.Ordinal));
                if (!validSocietyDefinition
                    || !validSocietyChoice
                    || !string.Equals(
                        choice.ownerId,
                        SocietyChoiceOwnerId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Committed society choice does not match authored content or its owner.");
                }
                break;
            case CommittedRunChoiceKind.FactionChapterChoice:
                if (definition is not FactionChapterDefinitionSO chapter
                    || !string.Equals(
                        choice.ownerId,
                        chapter.factionId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        choice.instanceId,
                        chapter.StableId,
                        StringComparison.Ordinal)
                    || !chapter.choices.Any(value => value != null
                        && string.Equals(
                            value.choiceId,
                            choice.choiceId,
                            StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        "Committed faction choice does not match authored content or its owner.");
                }
                break;
            default:
                throw new InvalidOperationException(
                    "Committed run choice kind is unsupported.");
        }
    }

    internal static SeasonalEventWorldSaveData Clone(SeasonalEventWorldSaveData value) => JsonClone(value);
    internal static SocietyEventWorldSaveData Clone(SocietyEventWorldSaveData value) => JsonClone(value);
    internal static FactionCampaignWorldSaveData Clone(FactionCampaignWorldSaveData value) => JsonClone(value);
    internal static RunMilestoneWorldSaveData Clone(RunMilestoneWorldSaveData value) => JsonClone(value);
    internal static T JsonClone<T>(T value) where T : class =>
        UnityEngine.JsonUtility.FromJson<T>(UnityEngine.JsonUtility.ToJson(value ?? throw new ArgumentNullException(nameof(value))));
}
