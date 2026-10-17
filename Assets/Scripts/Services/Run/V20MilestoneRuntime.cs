using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using static V20CampaignIdentity;
using static V20ContentDefinitionRules;
using static V20CampaignSaveCodec;

// Owns run progression commands and the endless-crisis lifecycle in the milestone aggregate.
internal sealed class V20MilestoneRuntime : IRunMilestoneQuery, ICommittedRunResultQuery,
    IMilestoneGameplayModifierQuery, IRunMilestoneCommand, IEndlessCrisisCommand, IEndlessCrisisQuery
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly V20StoryContentCatalog catalog;
    private readonly V20CampaignSaveCodec saveCodec;
    private readonly IPhysicalItemBatchDispositionService physicalDispositions;
    internal V20MilestoneRuntime(DungeonRuntimeAggregateRootStore rootStore,
        V20StoryContentCatalog catalog, V20CampaignSaveCodec saveCodec,
        IPhysicalItemBatchDispositionService physicalDispositions)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.saveCodec = saveCodec ?? throw new ArgumentNullException(nameof(saveCodec));
        this.physicalDispositions = physicalDispositions;
    }
    private RunMilestoneAggregateState Milestones =>
        rootStore.GetOrCreate(() => new RunMilestoneAggregateState());

    public RunProgressionPhase Phase => Milestones.Data.phase;
    public int EndlessCycle => Milestones.Data.endlessCycle;
    public IReadOnlyCollection<string> CompletedMilestoneIds => Milestones.Data.completedMilestoneIds.AsReadOnly();
    public IReadOnlyCollection<string> GrantedRewardIds =>
        Milestones.Data.grantedRewardIds.AsReadOnly();
    public IReadOnlyCollection<string> ActivePressureIds =>
        Milestones.Data.activePressureIds.AsReadOnly();
    public IReadOnlyCollection<string> WorldFlags =>
        Milestones.Data.worldFlags.AsReadOnly();
    public EndlessCrisisSnapshot CurrentEndlessCrisis =>
        ProjectEndlessCrisis(Milestones.Data);
    public bool EnemyCounterIntelVisible => HasReward("ending:truth-revealed");
    public float ExpeditionTravelTimeMultiplier =>
        HasReward("ending:surface-hegemony") ? 0.9f : 1f;
    public float FacilityMaintenanceGoldMultiplier =>
        HasReward("ending:dungeon-sovereignty") ? 0.9f : 1f;
    public float WaterAndFertilizerConsumptionMultiplier =>
        HasReward("ending:sealed-paradise") ? 0.9f : 1f;
    public int MentorshipDailyXpCap =>
        HasReward("ending:eternal-lineage")
            ? 15
            : CareerRules.MaximumDailyMentoringXp;
    public int TemporalStasisWarningDays =>
        HasReward("ending:timeless-sanctuary") ? 3 : 0;
    public float ManaTransferLossMultiplier =>
        HasReward("ending:arcane-ascension") ? 0.9f : 1f;
    public float AutomaticMaintenanceWorkMultiplier =>
        HasReward("ending:steel-apotheosis") ? 0.85f : 1f;
    private RunMilestoneAggregateState WritableMilestones => rootStore.GetOrCreateWritable(
        () => new RunMilestoneAggregateState(), value => new RunMilestoneAggregateState { Data = Clone(value.Data) });
    public float GetEndlessCrisisMultiplier(EndlessCrisisAxis axis)
    {
        if (!Enum.IsDefined(typeof(EndlessCrisisAxis), axis)
            || axis == EndlessCrisisAxis.None)
            throw new ArgumentOutOfRangeException(nameof(axis));
        EndlessCrisisAxisSaveData modifier = Milestones.Data.endlessCrisisPhase
                == EndlessCrisisLifecyclePhase.Active
            ? Milestones.Data.endlessCrisisAxes.FirstOrDefault(value =>
                value != null && value.axis == axis)
            : null;
        return modifier?.multiplier ?? 1f;
    }
    public bool TryGetActiveEndlessCrisisEffectOwner(
        EndlessCrisisAxis axis,
        out string effectOwnerId)
    {
        effectOwnerId = string.Empty;
        if (!Enum.IsDefined(typeof(EndlessCrisisAxis), axis)
            || axis == EndlessCrisisAxis.None)
            return false;
        EndlessCrisisAxisSaveData modifier = Milestones.Data.endlessCrisisPhase
                == EndlessCrisisLifecyclePhase.Active
            ? Milestones.Data.endlessCrisisAxes.FirstOrDefault(value =>
                value != null && value.axis == axis)
            : null;
        effectOwnerId = modifier?.effectOwnerId ?? string.Empty;
        return effectOwnerId.Length > 0;
    }
    public CommittedRunResultSnapshot CaptureCommittedRunResult()
    {
        RunMilestoneWorldSaveData state = Milestones.Data;
        return new CommittedRunResultSnapshot(
            state.completedMilestoneIds,
            state.committedChoices
                .OrderBy(value => value.ordinal)
                .Select(ToCommittedChoiceSnapshot));
    }

    internal void RecordCommittedChoice(
        CommittedRunChoiceKind kind,
        string ownerId,
        string definitionId,
        string instanceId,
        string choiceId,
        string operationId)
    {
        V20CommittedRunChoiceSaveData incoming = new()
        {
            kind = kind,
            ownerId = ownerId,
            definitionId = definitionId,
            instanceId = instanceId,
            choiceId = choiceId,
            operationId = operationId
        };
        saveCodec.ValidateCommittedChoice(incoming, validateOrdinal: false);

        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        V20CommittedRunChoiceSaveData operationMatch = state.committedChoices
            .FirstOrDefault(value => string.Equals(
                value.operationId,
                incoming.operationId,
                StringComparison.Ordinal));
        if (operationMatch != null)
        {
            if (SameCommittedDecision(operationMatch, incoming)) return;
            throw new InvalidOperationException(
                $"Committed choice operation '{incoming.operationId}' conflicts with its existing decision.");
        }

        V20CommittedRunChoiceSaveData instanceMatch = state.committedChoices
            .FirstOrDefault(value => value.kind == incoming.kind
                && string.Equals(value.ownerId, incoming.ownerId, StringComparison.Ordinal)
                && string.Equals(value.instanceId, incoming.instanceId, StringComparison.Ordinal));
        if (instanceMatch != null)
        {
            if (SameCommittedDecision(instanceMatch, incoming)) return;
            throw new InvalidOperationException(
                $"Committed choice instance '{incoming.instanceId}' already has a different decision.");
        }

        incoming.ordinal = checked(state.nextCommittedChoiceOrdinal + 1L);
        state.nextCommittedChoiceOrdinal = incoming.ordinal;
        state.committedChoices.Add(incoming);
    }
    public bool IsLandmarkBuilding(string id)
    {
        string normalized = Normalize(id);
        return catalog.All.Any(value => string.Equals(
            Normalize(value.landmarkBuildingId),
            normalized,
            StringComparison.Ordinal));
    }

    public bool IsLandmarkUnlocked(string id) => Milestones.Data.unlockedLandmarkIds.Contains(Normalize(id), StringComparer.Ordinal);
    public bool IsAccordSignalSupportDay(int absoluteDay) =>
        absoluteDay > 0
        && absoluteDay % GameCalendarRules.DaysPerSeason == 0
        && HasReward("ending:monster-accord");
    public bool IsAccordSignalSupportActive(int absoluteDay) =>
        IsAccordSignalSupportDay(absoluteDay)
        && Milestones.Data.lastAccordSignalSupportAbsoluteDay == absoluteDay;
    public bool TryActivateAccordSignalSupport(int absoluteDay, string sourceStackId)
    {
        if (!IsAccordSignalSupportDay(absoluteDay))
        {
            return false;
        }

        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        string operationId = $"accord-signal-support:{absoluteDay:D8}";
        if (state.pendingAccordSignalOperationId.Length > 0)
        {
            if (physicalDispositions == null) return false;
            if (!string.Equals(state.pendingAccordSignalOperationId, operationId, StringComparison.Ordinal)) return false;
            if (physicalDispositions.TryGetPending(operationId, out _)
                && !physicalDispositions.Acknowledge(state.pendingAccordSignalCommitId, out _)) return false;
            state.pendingAccordSignalOperationId = state.pendingAccordSignalCommitId = state.pendingAccordSignalSourceStackId = string.Empty;
            state.pendingAccordSignalMassGrams = 0;
            return state.lastAccordSignalSupportAbsoluteDay == absoluteDay;
        }
        if (state.lastAccordSignalSupportAbsoluteDay == absoluteDay)
        {
            return true;
        }
        if (physicalDispositions == null || string.IsNullOrWhiteSpace(sourceStackId)
            || !physicalDispositions.TryCommitPending(new[] { new PhysicalItemTransformInput(sourceStackId, 1) }, PhysicalItemDispositionKind.Sink,
                operationId, "alliance-signal-kit-consumed", out PhysicalItemBatchDispositionReceipt receipt, out _)) return false;
        state.pendingAccordSignalOperationId = operationId;
        state.pendingAccordSignalCommitId = receipt.CommitId;
        state.pendingAccordSignalSourceStackId = sourceStackId;
        state.pendingAccordSignalMassGrams = receipt.InputMassGrams;
        state.lastAccordSignalSupportAbsoluteDay = absoluteDay;
        return TryActivateAccordSignalSupport(absoluteDay, sourceStackId);
    }
    public bool HasReward(string milestoneId) =>
        Milestones.Data.grantedRewardIds.Contains(
            RewardId(milestoneId),
            StringComparer.Ordinal);
    public bool HasPressure(string milestoneId) =>
        Milestones.Data.activePressureIds.Contains(
            PressureId(milestoneId),
            StringComparer.Ordinal);

    public IReadOnlyList<string> Evaluate(RunMilestoneEvaluationSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        foreach (string flag in state.worldFlags)
            snapshot.WorldFlags.Add(flag);
        UpdateSelfSufficiency(state, snapshot);
        List<string> completed = new();
        foreach (EndingDefinitionSO definition in catalog.All)
        {
            if (state.completedMilestoneIds.Contains(definition.StableId, StringComparer.Ordinal)
                || !RequirementsSatisfied(definition.completionRequirements, snapshot)) continue;
            state.completedMilestoneIds.Add(definition.StableId);
            state.unlockedLandmarkIds.Add(definition.landmarkBuildingId);
            state.grantedRewardIds.Add(definition.permanentRewards[0].targetId);
            state.activePressureIds.Add(definition.counterPressures[0].targetId);
            completed.Add(definition.StableId);
            if (definition.tier == RunMilestoneTier.Legacy && state.phase == RunProgressionPhase.Founding)
                state.phase = RunProgressionPhase.LegacyAge;
            if (definition.tier == RunMilestoneTier.Grand)
                state.phase = RunProgressionPhase.EndlessAge;
        }
        return completed;
    }

    public int AdvanceEndlessCycle()
    {
        if (WritableMilestones.Data.phase != RunProgressionPhase.EndlessAge)
            throw new InvalidOperationException("Endless cycles require EndlessAge.");
        return ++WritableMilestones.Data.endlessCycle;
    }

    public IReadOnlyList<string> ComposeNextEndlessCrisis(
        int absoluteDay,
        int runSeed)
    {
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        if (state.phase != RunProgressionPhase.EndlessAge)
            throw new InvalidOperationException("Endless crisis composition requires EndlessAge.");
        if (absoluteDay <= state.lastEndlessCrisisEvaluationAbsoluteDay)
            return state.endlessCrisisAxes
                .Select(value => value.effectOwnerId)
                .ToArray();

        state.lastEndlessCrisisEvaluationAbsoluteDay = absoluteDay;
        if (state.endlessCrisisPhase != EndlessCrisisLifecyclePhase.None)
            return state.endlessCrisisAxes
                .Select(value => value.effectOwnerId)
                .ToArray();

        EndlessCrisisPolicyDefinitionSO policy = catalog.EndlessCrisisPolicy;
        int cycle = state.endlessCycle + 1;
        string key = $"{runSeed}:endless:{absoluteDay}:{cycle}";
        bool single = PersistentEntityId.GetStableHash32(key + ":count")
            % 100u < (uint)policy.singleAxisProbabilityPercent;
        int axisCount = single ? 1 : 2;
        List<EndlessCrisisAxisPolicy> available = policy.axes
            .OrderBy(value => value.axis)
            .ToList();
        List<EndlessCrisisAxisPolicy> selected = new();
        for (int index = 0; index < axisCount; index++)
        {
            int selectedIndex = (int)(PersistentEntityId.GetStableHash32(
                    key + ":axis:" + index)
                % (uint)available.Count);
            selected.Add(available[selectedIndex]);
            available.RemoveAt(selectedIndex);
        }

        state.endlessCycle = cycle;
        state.endlessCrisisPhase = EndlessCrisisLifecyclePhase.Active;
        state.endlessCrisisInstanceId =
            $"endless-crisis:{cycle}:{absoluteDay}";
        state.endlessCrisisAxes = selected
            .OrderBy(value => value.axis)
            .Select(value => new EndlessCrisisAxisSaveData
            {
                axis = value.axis,
                effectOwnerId = EffectOwnerId(
                    state.endlessCrisisInstanceId,
                    value.axis),
                multiplier = single
                    ? value.singleAxisMultiplier
                    : value.compoundAxisMultiplier
            })
            .ToList();
        state.endlessCrisisStartedAbsoluteDay = absoluteDay;
        state.endlessCrisisPressureEndAbsoluteDay = checked(
            absoluteDay + policy.pressureDurationDays);
        state.endlessCrisisLastActualEndAbsoluteDay = -1;
        state.endlessCrisisRecoveryEndAbsoluteDay = -1;
        state.endlessCrisisAuthoredRecoveryDays = single
            ? policy.singleAxisRecoveryDays
            : policy.compoundAxisRecoveryDays;
        state.endlessCrisisPendingInvasionSourceOwnerId = string.Empty;
        state.endlessCrisisDirectResponseOwnerId = string.Empty;
        return state.endlessCrisisAxes
            .Select(value => value.effectOwnerId)
            .ToArray();
    }

    public EndlessCrisisSnapshot AdvanceEndlessCrisis(int absoluteDay)
    {
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        if (state.endlessCrisisPhase == EndlessCrisisLifecyclePhase.Active
            && absoluteDay >= state.endlessCrisisPressureEndAbsoluteDay)
        {
            state.endlessCrisisPendingInvasionSourceOwnerId = string.Empty;
            if (state.endlessCrisisDirectResponseOwnerId.Length > 0)
            {
                state.endlessCrisisPhase =
                    EndlessCrisisLifecyclePhase.AwaitingDirectResponse;
            }
            else
            {
                BeginEndlessCrisisRecovery(
                    state,
                    state.endlessCrisisPressureEndAbsoluteDay);
            }
        }
        else if (state.endlessCrisisPhase
                    == EndlessCrisisLifecyclePhase.Recovery
            && absoluteDay >= state.endlessCrisisRecoveryEndAbsoluteDay)
        {
            state.endlessCrisisPhase = EndlessCrisisLifecyclePhase.None;
        }
        return ProjectEndlessCrisis(state);
    }

    public bool TryOwnInvasionCandidate(string effectOwnerId)
    {
        if (!IsCanonicalRequiredId(effectOwnerId))
            return false;
        RunMilestoneWorldSaveData current = Milestones.Data;
        bool ownsCombat = current.endlessCrisisPhase
                == EndlessCrisisLifecyclePhase.Active
            && current.endlessCrisisAxes.Any(value =>
                value != null
                && value.axis == EndlessCrisisAxis.Combat
                && string.Equals(
                    value.effectOwnerId,
                    effectOwnerId,
                    StringComparison.Ordinal));
        if (!ownsCombat
            || current.endlessCrisisDirectResponseOwnerId.Length > 0)
            return false;
        if (current.endlessCrisisPendingInvasionSourceOwnerId.Length > 0)
            return string.Equals(
                current.endlessCrisisPendingInvasionSourceOwnerId,
                effectOwnerId,
                StringComparison.Ordinal);
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        state.endlessCrisisLastActualEndAbsoluteDay = -1;
        state.endlessCrisisPendingInvasionSourceOwnerId = effectOwnerId;
        return true;
    }

    public bool TryBeginOwnedInvasion(
        string effectOwnerId,
        int absoluteDay,
        out string directResponseOwnerId)
    {
        directResponseOwnerId = string.Empty;
        if (absoluteDay < 1 || !IsCanonicalRequiredId(effectOwnerId))
            return false;
        RunMilestoneWorldSaveData current = Milestones.Data;
        bool ownsCombat = current.endlessCrisisAxes.Any(value =>
            value != null
            && value.axis == EndlessCrisisAxis.Combat
            && string.Equals(
                value.effectOwnerId,
                effectOwnerId,
                StringComparison.Ordinal));
        if (current.endlessCrisisPhase
                != EndlessCrisisLifecyclePhase.Active
            || !ownsCombat
            || !string.Equals(
                current.endlessCrisisPendingInvasionSourceOwnerId,
                effectOwnerId,
                StringComparison.Ordinal)
            || current.endlessCrisisDirectResponseOwnerId.Length > 0)
            return false;
        AdvanceEndlessCrisis(absoluteDay);
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        ownsCombat = state.endlessCrisisAxes.Any(value =>
            value != null
            && value.axis == EndlessCrisisAxis.Combat
            && string.Equals(
                value.effectOwnerId,
                effectOwnerId,
                StringComparison.Ordinal));
        if (state.endlessCrisisPhase != EndlessCrisisLifecyclePhase.Active
            || !ownsCombat
            || !string.Equals(
                state.endlessCrisisPendingInvasionSourceOwnerId,
                effectOwnerId,
                StringComparison.Ordinal))
            return false;
        string expected = effectOwnerId + ":invasion";
        if (state.endlessCrisisDirectResponseOwnerId.Length > 0
            && !string.Equals(
                state.endlessCrisisDirectResponseOwnerId,
                expected,
                StringComparison.Ordinal))
            return false;
        state.endlessCrisisPendingInvasionSourceOwnerId = string.Empty;
        state.endlessCrisisLastActualEndAbsoluteDay = -1;
        state.endlessCrisisDirectResponseOwnerId = expected;
        directResponseOwnerId = expected;
        return true;
    }

    public bool TryResolveOwnedInvasion(
        string directResponseOwnerId,
        int absoluteDay)
    {
        if (absoluteDay < 1 || string.IsNullOrWhiteSpace(directResponseOwnerId))
            return false;
        AdvanceEndlessCrisis(absoluteDay);
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        if (!string.Equals(
                state.endlessCrisisDirectResponseOwnerId,
                directResponseOwnerId,
                StringComparison.Ordinal))
            return false;
        state.endlessCrisisDirectResponseOwnerId = string.Empty;
        if (state.endlessCrisisPhase
            == EndlessCrisisLifecyclePhase.AwaitingDirectResponse)
        {
            BeginEndlessCrisisRecovery(state, absoluteDay);
        }
        else if (state.endlessCrisisPhase
            == EndlessCrisisLifecyclePhase.Active)
        {
            state.endlessCrisisLastActualEndAbsoluteDay = absoluteDay;
        }
        return true;
    }

    private static void BeginEndlessCrisisRecovery(
        RunMilestoneWorldSaveData state,
        int absoluteDay)
    {
        state.endlessCrisisPhase = EndlessCrisisLifecyclePhase.Recovery;
        state.endlessCrisisLastActualEndAbsoluteDay = absoluteDay;
        state.endlessCrisisRecoveryEndAbsoluteDay = checked(
            absoluteDay + state.endlessCrisisAuthoredRecoveryDays);
        state.endlessCrisisPendingInvasionSourceOwnerId = string.Empty;
        state.endlessCrisisDirectResponseOwnerId = string.Empty;
    }

    private EndlessCrisisSnapshot ProjectEndlessCrisis(
        RunMilestoneWorldSaveData state)
    {
        EndlessCrisisAxisModifierSnapshot[] axes =
            (state.endlessCrisisAxes ?? new List<EndlessCrisisAxisSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.axis)
            .Select(value =>
            {
                EndlessCrisisAxisPolicy authored =
                    catalog.EndlessCrisisPolicy.Require(value.axis);
                return new EndlessCrisisAxisModifierSnapshot(
                    value.axis,
                    authored.displayName,
                    value.effectOwnerId,
                    value.multiplier);
            })
            .ToArray();
        return new EndlessCrisisSnapshot(
            state.endlessCrisisPhase,
            state.endlessCrisisInstanceId,
            Array.AsReadOnly(axes),
            state.endlessCrisisStartedAbsoluteDay,
            state.endlessCrisisPressureEndAbsoluteDay,
            state.endlessCrisisLastActualEndAbsoluteDay,
            state.endlessCrisisRecoveryEndAbsoluteDay,
            state.lastEndlessCrisisEvaluationAbsoluteDay,
            state.endlessCrisisAuthoredRecoveryDays,
            state.endlessCrisisPendingInvasionSourceOwnerId,
            state.endlessCrisisDirectResponseOwnerId);
    }

    private static string Pick<T>(
        IReadOnlyList<T> values,
        Func<T, string> id,
        string key)
    {
        if (values == null || values.Count == 0)
            throw new InvalidOperationException("Endless crisis content pool is empty.");
        int index = (int)(PersistentEntityId.GetStableHash32(key)
            % (uint)values.Count);
        return id(values[index]);
    }

    private static CommittedRunChoiceSnapshot ToCommittedChoiceSnapshot(
        V20CommittedRunChoiceSaveData choice) =>
        new(
            choice.kind,
            choice.ownerId,
            choice.definitionId,
            choice.instanceId,
            choice.choiceId,
            choice.operationId,
            choice.ordinal);

    private static bool SameCommittedDecision(
        V20CommittedRunChoiceSaveData left,
        V20CommittedRunChoiceSaveData right) =>
        left.kind == right.kind
        && string.Equals(left.ownerId, right.ownerId, StringComparison.Ordinal)
        && string.Equals(left.definitionId, right.definitionId, StringComparison.Ordinal)
        && string.Equals(left.instanceId, right.instanceId, StringComparison.Ordinal)
        && string.Equals(left.choiceId, right.choiceId, StringComparison.Ordinal);

    private static void UpdateSelfSufficiency(
        RunMilestoneWorldSaveData state,
        RunMilestoneEvaluationSnapshot snapshot)
    {
        if (snapshot.AbsoluteDay < 0
            || state.lastMilestoneEvaluationAbsoluteDay >= snapshot.AbsoluteDay)
        {
            snapshot.WorldMetrics[V20WorldMetricKind.SelfSufficiencyDays] =
                state.selfSufficiencyStreakDays;
            snapshot.WorldMetrics[V20WorldMetricKind.ProductivityCoverageDays] =
                state.productivityCoverageStreakDays;
            if (state.selfSufficiencyStreakDays >= 120)
                snapshot.WorldFlags.Add("ecology:closed-cycle");
            if (state.productivityCoverageStreakDays >= 120)
                snapshot.WorldFlags.Add("temporal:productivity-sustained");
            return;
        }

        bool consecutive = state.lastMilestoneEvaluationAbsoluteDay < 0
            || snapshot.AbsoluteDay
                == state.lastMilestoneEvaluationAbsoluteDay + 1;
        bool sufficientToday = snapshot.WorldFlags.Contains(
            "ecology:self-sufficient-today");
        state.selfSufficiencyStreakDays = sufficientToday
            ? (consecutive ? state.selfSufficiencyStreakDays + 1 : 1)
            : 0;
        bool productivityQualifiedToday = snapshot.WorldFlags.Contains(
            "temporal:productivity-qualified-today");
        state.productivityCoverageStreakDays = productivityQualifiedToday
            ? (consecutive ? state.productivityCoverageStreakDays + 1 : 1)
            : 0;
        state.lastMilestoneEvaluationAbsoluteDay = snapshot.AbsoluteDay;
        snapshot.WorldMetrics[V20WorldMetricKind.SelfSufficiencyDays] =
            state.selfSufficiencyStreakDays;
        if (state.selfSufficiencyStreakDays >= 120)
            snapshot.WorldFlags.Add("ecology:closed-cycle");
        snapshot.WorldMetrics[V20WorldMetricKind.ProductivityCoverageDays] =
            state.productivityCoverageStreakDays;
        if (state.productivityCoverageStreakDays >= 120)
            snapshot.WorldFlags.Add("temporal:productivity-sustained");
    }
    private static string RewardId(string milestoneId)
    {
        string normalized = Normalize(milestoneId);
        return normalized.StartsWith("reward:", StringComparison.Ordinal)
            ? normalized
            : $"reward:{normalized}";
    }

    private static string PressureId(string milestoneId)
    {
        string normalized = Normalize(milestoneId);
        return normalized.StartsWith("pressure:", StringComparison.Ordinal)
            ? normalized
            : $"pressure:{normalized}";
    }
    internal void AddWorldFlag(string flag)
    {
        if (!WritableMilestones.Data.worldFlags.Contains(flag, StringComparer.Ordinal))
            WritableMilestones.Data.worldFlags.Add(flag);
    }
    internal void AddPressure(string pressure)
    {
        if (!WritableMilestones.Data.activePressureIds.Contains(pressure, StringComparer.Ordinal))
            WritableMilestones.Data.activePressureIds.Add(pressure);
    }
}
