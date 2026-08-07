using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class InvasionSaveRuntimeAdapter : IInvasionSaveRuntimePort
{
    internal const string CampaignPublicationCheckpoint = "campaign";
    internal const string IntruderPublicationCheckpoint = "intruders";
    internal const string OwnerEvacuationPublicationCheckpoint =
        "owner-evacuation";
    internal const string EngagementPublicationCheckpoint = "engagements";
    internal const string EngagementRetiredCompletionCheckpoint =
        "engagements-retired";
    internal const string OwnerEvacuationRetiredCompletionCheckpoint =
        "owner-evacuation-retired";
    internal const string IntruderActivatedCompletionCheckpoint =
        "intruders-activated";
    internal const string OwnerEvacuationActivatedCompletionCheckpoint =
        "owner-evacuation-activated";
    internal const string EngagementActivatedCompletionCheckpoint =
        "engagements-activated";

    private sealed class PreparedRestoreCandidate :
        IInvasionPreparedRestoreRuntimeCandidate
    {
        private InvasionSaveRuntimeAdapter owner;

        internal PreparedRestoreCandidate(
            InvasionSaveRuntimeAdapter owner,
            InvasionAggregateState state)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        internal InvasionAggregateState State { get; }
        internal bool IsStaged { get; set; }

        public void Stage() => owner?.Stage(this);

        public void Discard() => owner?.Discard(this);

        internal void Detach() => owner = null;
    }

    private readonly InvasionThreatRuntime threatRuntime;
    private readonly InvasionDirectorRuntime director;
    private readonly IGridSystemProvider gridProvider;
    private readonly IDefenseResponsePolicyRuntime responsePolicyRuntime;
    private readonly IDefenseEngagementRuntime engagementRuntime;
    private readonly IInvasionOwnerEvacuationService ownerEvacuationService;
    private readonly IInvasionCampaignRuntime campaignRuntime;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private PreparedRestoreCandidate preparedCandidate;
    private int publishedProjectionCount;
    private bool restorePublicationPending;

    internal Action<string> RestorePublicationCheckpoint { get; set; }
    internal Action<string> RestoreRollbackCheckpoint { get; set; }
    internal Action<string> RestoreCompletionCheckpoint { get; set; }

    public InvasionSaveRuntimeAdapter(
        InvasionSceneRuntimeReferences invasionRuntimes,
        IGridSystemProvider gridProvider,
        IDefenseResponsePolicyRuntime responsePolicyRuntime,
        IDefenseEngagementRuntime engagementRuntime,
        IInvasionOwnerEvacuationService ownerEvacuationService,
        IInvasionCampaignRuntime campaignRuntime,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        invasionRuntimes = invasionRuntimes
            ?? throw new ArgumentNullException(nameof(invasionRuntimes));
        threatRuntime = invasionRuntimes.Threat
            ?? throw new InvalidOperationException(
                $"{nameof(InvasionSaveRuntimeAdapter)} requires a loaded {nameof(InvasionThreatRuntime)}.");
        director = invasionRuntimes.Director
            ?? throw new InvalidOperationException(
                $"{nameof(InvasionSaveRuntimeAdapter)} requires a loaded {nameof(InvasionDirectorRuntime)}.");
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        this.responsePolicyRuntime = responsePolicyRuntime
            ?? throw new ArgumentNullException(nameof(responsePolicyRuntime));
        this.engagementRuntime = engagementRuntime
            ?? throw new ArgumentNullException(nameof(engagementRuntime));
        this.ownerEvacuationService = ownerEvacuationService
            ?? throw new ArgumentNullException(nameof(ownerEvacuationService));
        this.campaignRuntime = campaignRuntime
            ?? throw new ArgumentNullException(nameof(campaignRuntime));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public DungeonInvasionSaveData Capture()
    {
        InvasionThreatPersistenceState threat =
            threatRuntime.CapturePersistentState();
        DungeonInvasionSaveData result = new DungeonInvasionSaveData
        {
            threat = new DungeonInvasionThreatSaveData
            {
                currentThreat = threat.CurrentThreat,
                secondsSinceLastInvasion = threat.SecondsSinceLastInvasion,
                safetyRemaining = threat.SafetyRemaining,
                candidateDelayRemaining = threat.CandidateDelayRemaining,
                warningCooldownRemaining = threat.WarningCooldownRemaining,
                warningRaisedThisCycle = threat.WarningRaisedThisCycle,
                candidateRaisedThisCycle = threat.CandidateRaisedThisCycle,
                residualRisk = threat.ResidualRisk,
                dungeonValueFactor = threat.LastFactors.dungeonValue,
                reputationFactor = threat.LastFactors.reputation,
                timeFactor = threat.LastFactors.time,
                riskFactor = threat.LastFactors.risk
            },
            responsePolicies = responsePolicyRuntime.Capture(),
            engagements = engagementRuntime.Capture(),
            ownerEvacuation = ownerEvacuationService.Capture(),
            campaign = ToSaveData(campaignRuntime.Capture())
        };

        if (gridProvider.TryGetGrid(out Grid grid))
        {
            result.activeIntruders = director.CapturePersistentState(grid)
                .Select(ToIntruderSaveData)
                .ToList();
        }

        return result;
    }

    public IInvasionPreparedRestoreRuntimeCandidate PrepareRestore(
        DungeonInvasionSaveData source,
        DungeonGameRestoreReport report)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (preparedCandidate != null)
        {
            throw new InvalidOperationException(
                "An invasion runtime restore candidate is already prepared.");
        }

        try
        {
            InvasionAggregateState restored = CreateAggregateState(source);
            director.PrepareRestoreCandidates(
                source.activeIntruders.Select(ToRuntimeState),
                report);
            if (report.Success)
            {
                ownerEvacuationService.PrepareRestoreCandidate(
                    source.ownerEvacuation,
                    report);
            }
            if (report.Success)
            {
                engagementRuntime.PrepareRestoreCandidate(
                    source.engagements,
                    report);
            }
            if (!report.Success)
            {
                DiscardPreparedWorldCandidates();
                return null;
            }

            preparedCandidate = new PreparedRestoreCandidate(this, restored);
            return preparedCandidate;
        }
        catch
        {
            DiscardPreparedWorldCandidates();
            preparedCandidate = null;
            throw;
        }
    }

    public void PublishRestoreCandidate()
    {
        if (preparedCandidate == null
            || !preparedCandidate.IsStaged
            || restorePublicationPending)
        {
            throw new InvalidOperationException(
                "No staged invasion runtime restore candidate is ready to publish.");
        }

        publishedProjectionCount = 0;
        try
        {
            campaignRuntime.PublishRestoreProjection();
            publishedProjectionCount = 1;
            InvokePublicationCheckpoint(CampaignPublicationCheckpoint);

            director.PublishRestoreCandidates();
            publishedProjectionCount = 2;
            InvokePublicationCheckpoint(IntruderPublicationCheckpoint);

            ownerEvacuationService.PublishRestoreCandidate();
            publishedProjectionCount = 3;
            InvokePublicationCheckpoint(
                OwnerEvacuationPublicationCheckpoint);

            engagementRuntime.PublishRestoreCandidate();
            publishedProjectionCount = 4;
            InvokePublicationCheckpoint(EngagementPublicationCheckpoint);

            restorePublicationPending = true;
            preparedCandidate.Detach();
            preparedCandidate = null;
        }
        catch (Exception publicationFailure)
        {
            try
            {
                RollbackPublishedRestoreCandidate();
            }
            catch (Exception rollbackFailure)
            {
                throw new AggregateException(
                    "Invasion publication and automatic rollback both failed.",
                    publicationFailure,
                    rollbackFailure);
            }

            throw;
        }
    }

    public void RollbackPublishedRestoreCandidate()
    {
        List<Exception> failures = new();
        void Attempt(Action rollback)
        {
            try
            {
                rollback();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        int attemptedProjectionCount = publishedProjectionCount;
        try
        {
            if (attemptedProjectionCount >= 4)
            {
                Attempt(() =>
                {
                    engagementRuntime.RollbackPublishedRestoreCandidate();
                    InvokeRollbackCheckpoint(
                        EngagementPublicationCheckpoint);
                });
            }
            else
            {
                Attempt(engagementRuntime.DiscardRestoreCandidate);
            }

            if (attemptedProjectionCount >= 3)
            {
                Attempt(() =>
                {
                    ownerEvacuationService
                        .RollbackPublishedRestoreCandidate();
                    InvokeRollbackCheckpoint(
                        OwnerEvacuationPublicationCheckpoint);
                });
            }
            else
            {
                Attempt(ownerEvacuationService.DiscardRestoreCandidate);
            }

            if (attemptedProjectionCount >= 2)
            {
                Attempt(() =>
                {
                    director.RollbackPublishedRestoreCandidates();
                    InvokeRollbackCheckpoint(
                        IntruderPublicationCheckpoint);
                });
            }
            else
            {
                Attempt(director.DiscardRestoreCandidates);
            }

            if (attemptedProjectionCount >= 1)
            {
                Attempt(() =>
                {
                    campaignRuntime.RollbackPublishedRestoreProjection();
                    InvokeRollbackCheckpoint(
                        CampaignPublicationCheckpoint);
                });
            }
        }
        finally
        {
            try
            {
                preparedCandidate?.Detach();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            preparedCandidate = null;
            publishedProjectionCount = 0;
            restorePublicationPending = false;
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Invasion publication rollback encountered one or more failures after attempting every reversal.",
                failures);
        }
    }

    public void CompleteRestoreCandidate()
    {
        if (!restorePublicationPending)
        {
            return;
        }

        campaignRuntime.CompleteRestoreProjection();

        engagementRuntime.RetirePreviousRestoreProjection();
        InvokeCompletionCheckpoint(
            EngagementRetiredCompletionCheckpoint);

        ownerEvacuationService.RetirePreviousRestoreProjection();
        InvokeCompletionCheckpoint(
            OwnerEvacuationRetiredCompletionCheckpoint);

        director.CompleteRestoreCandidates();
        InvokeCompletionCheckpoint(
            IntruderActivatedCompletionCheckpoint);

        ownerEvacuationService.ActivateRestoreProjection();
        InvokeCompletionCheckpoint(
            OwnerEvacuationActivatedCompletionCheckpoint);

        engagementRuntime.ActivateRestoreProjection();
        InvokeCompletionCheckpoint(
            EngagementActivatedCompletionCheckpoint);

        publishedProjectionCount = 0;
        restorePublicationPending = false;
    }

    public void DiscardRestoreCandidate()
    {
        if (restorePublicationPending || publishedProjectionCount > 0)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }

        DiscardPreparedWorldCandidates();
        preparedCandidate?.Detach();
        preparedCandidate = null;
    }

    private void InvokePublicationCheckpoint(string checkpoint)
    {
        RestorePublicationCheckpoint?.Invoke(checkpoint);
    }

    private void InvokeRollbackCheckpoint(string checkpoint)
    {
        RestoreRollbackCheckpoint?.Invoke(checkpoint);
    }

    private void InvokeCompletionCheckpoint(string checkpoint)
    {
        RestoreCompletionCheckpoint?.Invoke(checkpoint);
    }

    private void Stage(PreparedRestoreCandidate candidate)
    {
        if (!ReferenceEquals(candidate, preparedCandidate)
            || candidate.IsStaged)
        {
            throw new InvalidOperationException(
                "The invasion runtime restore candidate is stale or already staged.");
        }
        if (!aggregateRootStore.IsRestoreStaging)
        {
            throw new InvalidOperationException(
                "Invasion aggregate replacement requires the V18 staging boundary.");
        }

        aggregateRootStore.Replace(candidate.State);
        candidate.IsStaged = true;
    }

    private void Discard(PreparedRestoreCandidate candidate)
    {
        if (!ReferenceEquals(candidate, preparedCandidate))
        {
            candidate?.Detach();
            return;
        }

        DiscardRestoreCandidate();
        candidate.Detach();
    }

    private void DiscardPreparedWorldCandidates()
    {
        engagementRuntime.DiscardRestoreCandidate();
        ownerEvacuationService.DiscardRestoreCandidate();
        director.DiscardRestoreCandidates();
    }

    private static InvasionAggregateState CreateAggregateState(
        DungeonInvasionSaveData source)
    {
        DungeonInvasionThreatSaveData threat = source.threat;
        InvasionAggregateState restored = new InvasionAggregateState
        {
            Threat = new InvasionThreatAggregateState
            {
                CurrentThreat = threat.currentThreat,
                SecondsSinceLastInvasion = threat.secondsSinceLastInvasion,
                SafetyRemaining = threat.safetyRemaining,
                CandidateDelayRemaining = threat.candidateDelayRemaining,
                WarningCooldownRemaining = threat.warningCooldownRemaining,
                WarningRaisedThisCycle = threat.warningRaisedThisCycle,
                CandidateRaisedThisCycle = threat.candidateRaisedThisCycle,
                ResidualRisk = threat.residualRisk,
                LastFactors = new InvasionThreatFactors(
                    threat.dungeonValueFactor,
                    threat.reputationFactor,
                    threat.timeFactor,
                    threat.riskFactor)
            }
        };

        DefensePolicyAggregateState policies = new DefensePolicyAggregateState();
        foreach (DefenseResponsePolicyData policy in source.responsePolicies.policies)
        {
            DefenseResponsePolicyData clone = policy.Clone();
            policies.Policies.Add(clone);
            if (clone.kind == DefenseResponsePolicyKind.Custom
                && clone.id.StartsWith(
                    DefenseResponsePolicyIds.CustomPrefix,
                    StringComparison.Ordinal)
                && int.TryParse(
                    clone.id.Substring(DefenseResponsePolicyIds.CustomPrefix.Length),
                    out int sequence))
            {
                policies.CustomSequence = Math.Max(
                    policies.CustomSequence,
                    sequence);
            }
        }
        foreach (DefensePolicyAssignmentSaveData assignment in
                 source.responsePolicies.assignments)
        {
            policies.AssignmentByCharacterId.Add(
                assignment.characterId,
                assignment.policyId);
        }
        restored.Policies = policies;

        InvasionCampaignAggregateState campaign = new InvasionCampaignAggregateState
        {
            CurrentDay = source.campaign.currentDay,
            OperationSequence = source.campaign.operationSequence
        };
        foreach (DungeonInvasionBranchSaveData branch in source.campaign.branches)
        {
            campaign.Branches.Add(branch.branchId, new HumanInvasionBranchState
            {
                branchId = branch.branchId,
                displayName = branch.displayName,
                strength = branch.strength,
                operational = branch.operational,
                lastRecoveryAmount = branch.lastRecoveryAmount,
                recoveryReason = branch.recoveryReason
            });
        }
        campaign.SupportSites.AddRange(source.campaign.supportSites.Select(site =>
            new HumanSupportSiteState
            {
                siteId = site.siteId,
                branchId = site.branchId,
                displayName = site.displayName,
                q = site.q,
                r = site.r,
                alive = site.alive,
                connected = site.connected,
                destroyedDay = site.destroyedDay
            }));
        campaign.Operations.AddRange(source.campaign.operations.Select(operation =>
            new ScheduledInvasionOperationState
            {
                operationId = operation.operationId,
                kind = operation.kind,
                primaryBranchId = operation.primaryBranchId,
                participatingBranchIds = operation.participatingBranchIds.ToList(),
                objectiveId = operation.objectiveId,
                scheduledDay = operation.scheduledDay,
                intelligenceConfidence = operation.intelligenceConfidence
            }));
        restored.Campaign = campaign;
        return restored;
    }

    private static DungeonInvasionCampaignSaveData ToSaveData(
        InvasionCampaignSaveData source)
    {
        return new DungeonInvasionCampaignSaveData
        {
            currentDay = source.currentDay,
            operationSequence = source.operationSequence,
            branches = source.branches.Select(branch =>
                new DungeonInvasionBranchSaveData
                {
                    branchId = branch.branchId,
                    displayName = branch.displayName,
                    strength = branch.strength,
                    operational = branch.operational,
                    lastRecoveryAmount = branch.lastRecoveryAmount,
                    recoveryReason = branch.recoveryReason
                }).ToList(),
            supportSites = source.supportSites.Select(site =>
                new DungeonInvasionSupportSiteSaveData
                {
                    siteId = site.siteId,
                    branchId = site.branchId,
                    displayName = site.displayName,
                    q = site.q,
                    r = site.r,
                    alive = site.alive,
                    connected = site.connected,
                    destroyedDay = site.destroyedDay
                }).ToList(),
            operations = source.operations.Select(operation =>
                new DungeonInvasionOperationSaveData
                {
                    operationId = operation.operationId,
                    kind = operation.kind,
                    primaryBranchId = operation.primaryBranchId,
                    participatingBranchIds = operation.participatingBranchIds.ToList(),
                    objectiveId = operation.objectiveId,
                    scheduledDay = operation.scheduledDay,
                    intelligenceConfidence = operation.intelligenceConfidence
                }).ToList()
        };
    }

    private static DungeonInvasionIntruderSaveData ToIntruderSaveData(
        InvasionIntruderPersistenceState source)
    {
        InvasionIntruderSettings settings = source.Settings
            ?? new InvasionIntruderSettings();
        return new DungeonInvasionIntruderSaveData
        {
            runtimeId = source.RuntimeId,
            dataId = source.DataId,
            enemyIndividual = source.EnemyIndividual?.Clone(),
            worldX = source.WorldPosition.x,
            worldY = source.WorldPosition.y,
            worldZ = source.WorldPosition.z,
            gridX = source.GridPosition.x,
            gridY = source.GridPosition.y,
            state = source.State,
            elapsedSeconds = source.ElapsedSeconds,
            rallyRemainingSeconds = source.RallyRemainingSeconds,
            hasBreachedDungeonInterior = source.HasBreachedDungeonInterior,
            breachTargetBuildingId = source.BreachTargetBuildingId,
            breachTargetX = source.BreachTargetPosition.x,
            breachTargetY = source.BreachTargetPosition.y,
            breachAttackX = source.BreachAttackCell.x,
            breachAttackY = source.BreachAttackCell.y,
            structureAttackDelayRemaining = source.StructureAttackDelayRemaining,
            trappedSeconds = source.TrappedSeconds,
            enragedBreach = source.EnragedBreach,
            raidAwareness = ToSaveData(source.RaidAwareness),
            damageDelayRemaining = source.DamageDelayRemaining,
            facilityDamageCount = source.FacilityDamageCount,
            damagedFacilityBuildingInstanceIds = source.DamagedFacilityIds
                .Select(id => id.Value)
                .ToList(),
            currentHealth = source.CurrentHealth,
            injurySeverity = source.InjurySeverity,
            baseMood = source.BaseMood,
            settings = new DungeonInvasionIntruderSettingsSaveData
            {
                patternId = settings.patternId,
                rallyDurationSeconds = settings.rallyDurationSeconds,
                secondsToFullFocus = settings.secondsToFullFocus,
                repathIntervalSeconds = settings.repathIntervalSeconds,
                facilityDamageIntervalSeconds = settings.facilityDamageIntervalSeconds,
                structureAttackIntervalSeconds = settings.structureAttackIntervalSeconds,
                finalCombatDamage = settings.finalCombatDamage,
                finalCombatWindupSeconds = settings.finalCombatWindupSeconds,
                healthMultiplier = settings.healthMultiplier,
                meleeDamageMultiplier = settings.meleeDamageMultiplier,
                attackSpeedMultiplier = settings.attackSpeedMultiplier,
                riskTolerance = settings.riskTolerance,
                routeCommitmentSeconds = settings.routeCommitmentSeconds,
                structureDamageMultiplier = settings.structureDamageMultiplier,
                operationKind = settings.operationKind,
                raidId = settings.raidId
            },
            conditions = source.Conditions
                .OrderBy(pair => pair.Key)
                .Select(pair => new DungeonInvasionConditionSaveData
                {
                    condition = pair.Key,
                    value = pair.Value
                })
                .ToList(),
            defenseStatuses = source.DefenseStatuses
                .Select(status => new DungeonDefenseStatusSaveData
                {
                    kind = status.Kind,
                    value = status.Value,
                    remainingSeconds = status.RemainingSeconds,
                    stacks = status.Stacks
                })
                .ToList()
        };
    }

    private static DungeonInvasionRaidAwarenessSaveData ToSaveData(
        DefenseRaidAwarenessSaveData source)
    {
        source ??= new DefenseRaidAwarenessSaveData();
        return new DungeonInvasionRaidAwarenessSaveData
        {
            raidId = source.raidId,
            identificationStage = source.identificationStage,
            routeChangeReason = source.routeChangeReason,
            breachTargetBuildingInstanceId = source.breachTargetBuildingInstanceId,
            knownRisks = (source.knownRisks ?? new List<DefenseKnownRiskSaveData>())
                .Select(risk => new DungeonInvasionKnownRiskSaveData
                {
                    x = risk.x,
                    y = risk.y,
                    severity = risk.severity,
                    facilityBuildingInstanceId = risk.facilityBuildingInstanceId
                }).ToList(),
            expectedPath = (source.expectedPath
                    ?? new List<DefenseExpectedPathCellSaveData>())
                .Select(cell => new DungeonInvasionExpectedPathCellSaveData
                {
                    x = cell.x,
                    y = cell.y
                }).ToList()
        };
    }

    private static InvasionIntruderPersistenceState ToRuntimeState(
        DungeonInvasionIntruderSaveData source)
    {
        DungeonInvasionIntruderSettingsSaveData settings = source.settings;
        Dictionary<CharacterCondition, float> conditions = source.conditions
            .ToDictionary(condition => condition.condition, condition => condition.value);
        return new InvasionIntruderPersistenceState(
            source.dataId,
            new Vector3(source.worldX, source.worldY, source.worldZ),
            new Vector2Int(source.gridX, source.gridY),
            source.state,
            source.elapsedSeconds,
            source.damageDelayRemaining,
            source.facilityDamageCount,
            source.currentHealth,
            source.injurySeverity,
            source.baseMood,
            conditions,
            new InvasionIntruderSettings
            {
                patternId = settings.patternId,
                rallyDurationSeconds = settings.rallyDurationSeconds,
                secondsToFullFocus = settings.secondsToFullFocus,
                repathIntervalSeconds = settings.repathIntervalSeconds,
                facilityDamageIntervalSeconds = settings.facilityDamageIntervalSeconds,
                structureAttackIntervalSeconds = settings.structureAttackIntervalSeconds,
                finalCombatDamage = settings.finalCombatDamage,
                finalCombatWindupSeconds = settings.finalCombatWindupSeconds,
                healthMultiplier = settings.healthMultiplier,
                meleeDamageMultiplier = settings.meleeDamageMultiplier,
                attackSpeedMultiplier = settings.attackSpeedMultiplier,
                riskTolerance = settings.riskTolerance,
                routeCommitmentSeconds = settings.routeCommitmentSeconds,
                structureDamageMultiplier = settings.structureDamageMultiplier,
                operationKind = settings.operationKind,
                raidId = settings.raidId
            },
            source.defenseStatuses.Select(status => new DefenseStatusSnapshot(
                status.kind,
                status.value,
                status.remainingSeconds,
                status.stacks)),
            source.runtimeId,
            source.rallyRemainingSeconds,
            source.hasBreachedDungeonInterior,
            source.breachTargetBuildingId,
            new Vector2Int(source.breachTargetX, source.breachTargetY),
            new Vector2Int(source.breachAttackX, source.breachAttackY),
            source.structureAttackDelayRemaining,
            source.trappedSeconds,
            source.enragedBreach,
            ToRuntimeData(source.raidAwareness),
            source.damagedFacilityBuildingInstanceIds.Select(
                value => new BuildingInstanceId(value)),
            source.enemyIndividual?.Clone());
    }

    private static DefenseRaidAwarenessSaveData ToRuntimeData(
        DungeonInvasionRaidAwarenessSaveData source)
    {
        source ??= new DungeonInvasionRaidAwarenessSaveData();
        return new DefenseRaidAwarenessSaveData
        {
            raidId = source.raidId,
            identificationStage = source.identificationStage,
            routeChangeReason = source.routeChangeReason,
            breachTargetBuildingInstanceId = source.breachTargetBuildingInstanceId,
            knownRisks = (source.knownRisks
                    ?? new List<DungeonInvasionKnownRiskSaveData>())
                .Select(risk => new DefenseKnownRiskSaveData
                {
                    x = risk.x,
                    y = risk.y,
                    severity = risk.severity,
                    facilityBuildingInstanceId = risk.facilityBuildingInstanceId
                }).ToList(),
            expectedPath = (source.expectedPath
                    ?? new List<DungeonInvasionExpectedPathCellSaveData>())
                .Select(cell => new DefenseExpectedPathCellSaveData
                {
                    x = cell.x,
                    y = cell.y
                }).ToList()
        };
    }
}
