using System;
using System.Collections.Generic;
using System.Linq;

public static class InvasionSaveValidation
{
    public const int MaximumIntruders = 64;
    public const int MaximumPolicies = 64;
    public const int MaximumAssignments = 2048;
    public const int MaximumEngagements = 128;
    public const int MaximumSupportSites = 256;
    public const int MaximumOperations = 256;
    public const int MaximumKnownRisks = 512;
    public const int MaximumExpectedPathCells = 4096;
    public const int MaximumResidentEvacuees = 2048;
    public const int MaximumResidentEvacuationRoomSpans = 4096;

    private const string CustomPolicyPrefix = DefenseResponsePolicyIds.CustomPrefix;
    private const string EngagementPrefix = "defense-engagement:";
    private const string OperationPrefix = "human-operation:";
    private const int EndlessCombatAxisProtocolValue = 5;

    private static readonly IReadOnlyDictionary<string, DefenseResponsePolicyKind>
        BuiltInPolicyKinds =
            new Dictionary<string, DefenseResponsePolicyKind>(StringComparer.Ordinal)
            {
                [DefenseResponsePolicyIds.Standard] =
                    DefenseResponsePolicyKind.Standard,
                [DefenseResponsePolicyIds.SurvivalFirst] =
                    DefenseResponsePolicyKind.SurvivalFirst,
                [DefenseResponsePolicyIds.HoldTheLine] =
                    DefenseResponsePolicyKind.HoldTheLine
            };

    private static readonly HashSet<string> RequiredBranchIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            HumanInvasionBranchIds.RoyalArmy,
            HumanInvasionBranchIds.PioneerSupply,
            HumanInvasionBranchIds.RoyalOrdnance,
            HumanInvasionBranchIds.IntelligenceHunters,
            HumanInvasionBranchIds.RadiantOrder
        };

    public static void Validate(
        DungeonInvasionSaveData payload,
        IInvasionIntruderPatternDefinitionCatalog patterns,
        DungeonGameRestoreReport report)
    {
        if (patterns == null)
        {
            throw new ArgumentNullException(nameof(patterns));
        }
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (payload == null)
        {
            report.AddError("Invasion payload is null.");
            return;
        }
        if (payload.version != DungeonInvasionSaveData.CurrentVersion)
        {
            report.AddError(
                $"Invasion payload version {payload.version} is invalid.");
        }
        if (payload.threat == null
            || payload.activeIntruders == null
            || payload.responsePolicies == null
            || payload.engagements == null
            || payload.ownerEvacuation == null
            || payload.campaign == null)
        {
            report.AddError("Invasion payload is missing a required component.");
            return;
        }

        ValidateThreat(
            payload.threat,
            payload.activeIntruders,
            report);
        HashSet<string> intruderIds = ValidateIntruders(
            payload.activeIntruders,
            patterns,
            report);
        ValidatePolicies(payload.responsePolicies, report);
        ValidateEngagements(payload.engagements, intruderIds, report);
        ValidateOwnerEvacuation(
            payload.ownerEvacuation,
            payload.activeIntruders.Count > 0,
            report);
        ValidateCampaign(payload.campaign, report);
    }

    private static void ValidateThreat(
        DungeonInvasionThreatSaveData threat,
        IReadOnlyList<DungeonInvasionIntruderSaveData> activeIntruders,
        DungeonGameRestoreReport report)
    {
        if (!IsFiniteNonNegative(threat.currentThreat)
            || !IsFiniteNonNegative(threat.secondsSinceLastInvasion)
            || !IsFiniteNonNegative(threat.safetyRemaining)
            || !IsFinite(threat.candidateDelayRemaining)
            || threat.candidateDelayRemaining < -1f
            || !IsFiniteNonNegative(threat.warningCooldownRemaining)
            || !IsFiniteNonNegative(threat.residualRisk)
            || !IsFiniteNonNegative(threat.dungeonValueFactor)
            || !IsFiniteNonNegative(threat.reputationFactor)
            || !IsFiniteNonNegative(threat.timeFactor)
            || !IsFiniteNonNegative(threat.riskFactor))
        {
            report.AddError("Invasion threat payload contains invalid numeric state.");
        }
        if (threat.candidateRaisedThisCycle
            && threat.candidateDelayRemaining >= 0f)
        {
            report.AddError(
                "Invasion threat cannot be pending after its candidate was raised.");
        }

        string candidateOwner =
            threat.endlessCrisisCandidateEffectOwnerId ?? string.Empty;
        string responseOwner =
            threat.endlessCrisisDirectResponseOwnerId ?? string.Empty;
        string responseRuntimeId =
            threat.endlessCrisisDirectResponseRuntimeId ?? string.Empty;
        bool hasCandidateOwner = candidateOwner.Length > 0;
        bool hasResponseOwner = responseOwner.Length > 0;
        if (hasCandidateOwner
            && (!IsEndlessCombatEffectOwner(candidateOwner)
                || hasResponseOwner
                || !threat.candidateRaisedThisCycle
                || responseRuntimeId.Length > 0))
        {
            report.AddError(
                "Invasion threat contains a malformed endless-crisis candidate owner.");
        }
        if (hasResponseOwner
            && (!responseOwner.EndsWith(":invasion", StringComparison.Ordinal)
                || !IsEndlessCombatEffectOwner(responseOwner.Substring(
                    0,
                    responseOwner.Length - ":invasion".Length))
                || hasCandidateOwner
                || !IsCanonicalId(responseRuntimeId)
                || activeIntruders.Count(value => value != null
                    && string.Equals(
                        value.runtimeId,
                        responseRuntimeId,
                        StringComparison.Ordinal)) != 1))
        {
            report.AddError(
                "Invasion threat contains a malformed endless-crisis direct-response owner.");
        }
        if (!hasResponseOwner && responseRuntimeId.Length > 0)
        {
            report.AddError(
                "Invasion threat contains a detached endless-crisis response runtime id.");
        }
    }

    private static HashSet<string> ValidateIntruders(
        List<DungeonInvasionIntruderSaveData> intruders,
        IInvasionIntruderPatternDefinitionCatalog patterns,
        DungeonGameRestoreReport report)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        if (intruders.Count > MaximumIntruders)
        {
            report.AddError(
                $"Invasion payload exceeds {MaximumIntruders} active intruders.");
        }

        foreach (DungeonInvasionIntruderSaveData intruder in intruders)
        {
            string id = intruder?.runtimeId ?? string.Empty;
            if (intruder == null || !IsCanonicalId(id) || !ids.Add(id))
            {
                report.AddError($"Invasion payload contains invalid intruder '{id}'.");
                continue;
            }
            if (intruder.dataId < 0
                || intruder.enemyIndividual == null
                || !IsCanonicalId(intruder.enemyIndividual.characterId)
                || !IsCanonicalId(intruder.enemyIndividual.enemyArchetypeId)
                || !string.Equals(
                    intruder.enemyIndividual.characterId,
                    CharacterId.FromStableSuffix(id).Value,
                    StringComparison.Ordinal)
                || !Enum.IsDefined(typeof(InvasionIntruderState), intruder.state)
                || intruder.state is InvasionIntruderState.None
                    or InvasionIntruderState.Finished)
            {
                report.AddError(
                    $"Invasion intruder '{id}' has invalid identity or state.");
            }
            if (!IsFinite(intruder.worldX)
                || !IsFinite(intruder.worldY)
                || !IsFinite(intruder.worldZ)
                || !IsFiniteNonNegative(intruder.elapsedSeconds)
                || !IsFiniteNonNegative(intruder.rallyRemainingSeconds)
                || !IsFiniteNonNegative(intruder.structureAttackDelayRemaining)
                || !IsFiniteNonNegative(intruder.trappedSeconds)
                || !IsFiniteNonNegative(intruder.damageDelayRemaining)
                || intruder.facilityDamageCount < 0
                || !IsFiniteNonNegative(intruder.currentHealth)
                || !IsFiniteRange(intruder.injurySeverity, 0f, 1f)
                || !IsFiniteRange(intruder.baseMood, 0f, 100f))
            {
                report.AddError(
                    $"Invasion intruder '{id}' contains invalid numeric state.");
            }
            ValidateDamagedFacilityIds(intruder, id, report);
            if (!intruder.hasBreachedDungeonInterior
                && intruder.breachTargetBuildingId >= 0)
            {
                report.AddError(
                    $"Invasion intruder '{id}' has a breach target before breaching.");
            }

            ValidateIntruderSettings(id, intruder.settings, patterns, report);
            ValidateConditions(id, intruder.conditions, report);
            ValidateDefenseStatuses(id, intruder.defenseStatuses, report);
            ValidateRaidAwareness(id, intruder.raidAwareness, intruder.settings, report);
        }
        return ids;
    }

    private static void ValidateDamagedFacilityIds(
        DungeonInvasionIntruderSaveData intruder,
        string intruderId,
        DungeonGameRestoreReport report)
    {
        if (intruder.damagedFacilityBuildingInstanceIds == null)
        {
            report.AddError(
                $"Invasion intruder '{intruderId}' has no damaged-facility identity list.");
            return;
        }

        HashSet<BuildingInstanceId> ids = new HashSet<BuildingInstanceId>();
        foreach (string value in intruder.damagedFacilityBuildingInstanceIds)
        {
            BuildingInstanceId id = new BuildingInstanceId(value);
            if (!id.IsValid
                || !string.Equals(id.Value, value, StringComparison.Ordinal)
                || !ids.Add(id))
            {
                report.AddError(
                    $"Invasion intruder '{intruderId}' has an invalid or duplicate damaged facility id '{value}'.");
            }
        }

        if (intruder.facilityDamageCount != ids.Count)
        {
            report.AddError(
                $"Invasion intruder '{intruderId}' damage count does not match its canonical facility ids.");
        }
    }

    private static void ValidateIntruderSettings(
        string intruderId,
        DungeonInvasionIntruderSettingsSaveData settings,
        IInvasionIntruderPatternDefinitionCatalog patterns,
        DungeonGameRestoreReport report)
    {
        if (settings == null)
        {
            report.AddError($"Invasion intruder '{intruderId}' has no settings.");
            return;
        }
        if (!IsCanonicalId(settings.patternId)
            || patterns.Get(settings.patternId) == null
            || !Enum.IsDefined(typeof(InvasionOperationKind), settings.operationKind))
        {
            report.AddError(
                $"Invasion intruder '{intruderId}' has an invalid pattern or operation.");
        }
        if (!IsFinitePositive(settings.rallyDurationSeconds)
            || !IsFiniteNonNegative(settings.secondsToFullFocus)
            || !IsFinitePositive(settings.repathIntervalSeconds)
            || !IsFinitePositive(settings.facilityDamageIntervalSeconds)
            || !IsFinitePositive(settings.structureAttackIntervalSeconds)
            || !IsFiniteNonNegative(settings.finalCombatDamage)
            || !IsFiniteNonNegative(settings.finalCombatWindupSeconds)
            || !IsFinitePositive(settings.healthMultiplier)
            || !IsFinitePositive(settings.meleeDamageMultiplier)
            || !IsFinitePositive(settings.attackSpeedMultiplier)
            || !IsFiniteRange(settings.riskTolerance, 0f, 1f)
            || !IsFiniteNonNegative(settings.routeCommitmentSeconds)
            || !IsFinitePositive(settings.structureDamageMultiplier))
        {
            report.AddError(
                $"Invasion intruder '{intruderId}' has invalid settings.");
        }
        if (!string.IsNullOrEmpty(settings.raidId)
            && !IsCanonicalId(settings.raidId))
        {
            report.AddError(
                $"Invasion intruder '{intruderId}' has an invalid raid id.");
        }
    }

    private static void ValidateConditions(
        string intruderId,
        List<DungeonInvasionConditionSaveData> conditions,
        DungeonGameRestoreReport report)
    {
        if (conditions == null)
        {
            report.AddError($"Invasion intruder '{intruderId}' has no condition list.");
            return;
        }
        HashSet<CharacterCondition> kinds = new HashSet<CharacterCondition>();
        foreach (DungeonInvasionConditionSaveData condition in conditions)
        {
            if (condition == null
                || !Enum.IsDefined(typeof(CharacterCondition), condition.condition)
                || !kinds.Add(condition.condition)
                || !IsFinite(condition.value))
            {
                report.AddError(
                    $"Invasion intruder '{intruderId}' has an invalid or duplicate condition.");
            }
        }
    }

    private static void ValidateDefenseStatuses(
        string intruderId,
        List<DungeonDefenseStatusSaveData> statuses,
        DungeonGameRestoreReport report)
    {
        if (statuses == null)
        {
            report.AddError($"Invasion intruder '{intruderId}' has no defense status list.");
            return;
        }
        HashSet<DefenseStatusKind> kinds = new HashSet<DefenseStatusKind>();
        foreach (DungeonDefenseStatusSaveData status in statuses)
        {
            if (status == null
                || !Enum.IsDefined(typeof(DefenseStatusKind), status.kind)
                || !kinds.Add(status.kind)
                || !IsFinite(status.value)
                || !IsFinitePositive(status.remainingSeconds)
                || status.stacks < 1)
            {
                report.AddError(
                    $"Invasion intruder '{intruderId}' has an invalid or duplicate defense status.");
            }
        }
    }

    private static void ValidateRaidAwareness(
        string intruderId,
        DungeonInvasionRaidAwarenessSaveData awareness,
        DungeonInvasionIntruderSettingsSaveData settings,
        DungeonGameRestoreReport report)
    {
        if (awareness == null
            || awareness.knownRisks == null
            || awareness.expectedPath == null
            || awareness.routeChangeReason == null
            || awareness.breachTargetBuildingInstanceId == null)
        {
            report.AddError(
                $"Invasion intruder '{intruderId}' has incomplete raid awareness.");
            return;
        }
        if (!string.IsNullOrEmpty(awareness.breachTargetBuildingInstanceId))
        {
            BuildingInstanceId breachTargetId = new BuildingInstanceId(
                awareness.breachTargetBuildingInstanceId);
            if (!breachTargetId.IsValid
                || !string.Equals(
                    breachTargetId.Value,
                    awareness.breachTargetBuildingInstanceId,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Invasion intruder '{intruderId}' has an invalid breach target BuildingInstanceId.");
            }
        }
        if (awareness.identificationStage < 0
            || awareness.knownRisks.Count > MaximumKnownRisks
            || awareness.expectedPath.Count > MaximumExpectedPathCells
            || (!string.IsNullOrEmpty(awareness.raidId)
                && !IsCanonicalId(awareness.raidId))
            || (!string.IsNullOrEmpty(awareness.raidId)
                && settings != null
                && !string.Equals(
                    awareness.raidId,
                    settings.raidId,
                    StringComparison.Ordinal)))
        {
            report.AddError(
                $"Invasion intruder '{intruderId}' has invalid raid awareness metadata.");
        }

        HashSet<(int x, int y)> riskCells = new HashSet<(int x, int y)>();
        foreach (DungeonInvasionKnownRiskSaveData risk in awareness.knownRisks)
        {
            if (risk == null
                || !riskCells.Add((risk.x, risk.y))
                || !IsFiniteNonNegative(risk.severity)
                || !IsCanonicalBuildingId(
                    risk.facilityBuildingInstanceId))
            {
                report.AddError(
                    $"Invasion intruder '{intruderId}' has invalid or duplicate known risk data.");
            }
        }
        if (awareness.expectedPath.Any(cell => cell == null))
        {
            report.AddError(
                $"Invasion intruder '{intruderId}' has a null expected path cell.");
        }
    }

    private static bool IsCanonicalBuildingId(string value)
    {
        BuildingInstanceId id = new BuildingInstanceId(value);
        return id.IsValid
            && string.Equals(id.Value, value, StringComparison.Ordinal);
    }

    private static void ValidatePolicies(
        DefenseResponsePolicySaveSnapshot snapshot,
        DungeonGameRestoreReport report)
    {
        if (snapshot.policies == null || snapshot.assignments == null)
        {
            report.AddError("Defense policy snapshot is missing a required list.");
            return;
        }
        if (snapshot.policies.Count > MaximumPolicies
            || snapshot.assignments.Count > MaximumAssignments)
        {
            report.AddError("Defense policy snapshot exceeds its collection limit.");
        }

        HashSet<string> policyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DefenseResponsePolicyData policy in snapshot.policies)
        {
            string id = policy?.id ?? string.Empty;
            if (policy == null || !IsCanonicalId(id) || !policyIds.Add(id))
            {
                report.AddError($"Defense policy snapshot contains invalid policy '{id}'.");
                continue;
            }
            bool builtIn = BuiltInPolicyKinds.TryGetValue(id, out DefenseResponsePolicyKind kind);
            if (!Enum.IsDefined(typeof(DefenseResponsePolicyKind), policy.kind)
                || builtIn && policy.kind != kind
                || !builtIn && (policy.kind != DefenseResponsePolicyKind.Custom
                    || !TryParsePositiveSequence(id, CustomPolicyPrefix, out _))
                || !IsCanonicalText(policy.displayName)
                || !IsFiniteRange(policy.minimumDispatchHealthRatio, 0f, 1f)
                || !IsFiniteRange(policy.retreatHealthRatio, 0f, 1f)
                || !IsFiniteRange(policy.rejoinHealthRatio, 0f, 1f)
                || policy.rejoinHealthRatio < policy.minimumDispatchHealthRatio)
            {
                report.AddError($"Defense policy '{id}' contains invalid data.");
            }
        }
        foreach (string builtInId in BuiltInPolicyKinds.Keys)
        {
            if (!policyIds.Contains(builtInId))
            {
                report.AddError($"Defense policy snapshot is missing '{builtInId}'.");
            }
        }

        HashSet<string> assignedCharacters = new HashSet<string>(StringComparer.Ordinal);
        foreach (DefensePolicyAssignmentSaveData assignment in snapshot.assignments)
        {
            if (assignment == null
                || !IsCanonicalId(assignment.characterId)
                || !assignedCharacters.Add(assignment.characterId)
                || !policyIds.Contains(assignment.policyId))
            {
                report.AddError("Defense policy snapshot contains an invalid assignment.");
            }
        }
    }

    private static void ValidateEngagements(
        DefenseEngagementSaveSnapshot snapshot,
        ISet<string> intruderIds,
        DungeonGameRestoreReport report)
    {
        if (snapshot.engagements == null)
        {
            report.AddError("Defense engagement snapshot is missing its list.");
            return;
        }
        if (snapshot.engagements.Count > MaximumEngagements)
        {
            report.AddError(
                $"Defense engagement snapshot exceeds {MaximumEngagements} entries.");
        }

        HashSet<string> engagementIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> engagedIntruders = new HashSet<string>(StringComparer.Ordinal);
        foreach (DefenseEngagementSaveData engagement in snapshot.engagements)
        {
            string id = engagement?.id ?? string.Empty;
            if (engagement == null
                || !TryParsePositiveSequence(id, EngagementPrefix, out _)
                || !engagementIds.Add(id))
            {
                report.AddError(
                    $"Defense engagement snapshot contains invalid engagement '{id}'.");
                continue;
            }
            if (!IsCanonicalId(engagement.intruderId)
                || !intruderIds.Contains(engagement.intruderId)
                || !engagedIntruders.Add(engagement.intruderId)
                || !IsCanonicalId(engagement.leadGuardId)
                || !Enum.IsDefined(typeof(DefenseEngagementState), engagement.state)
                || engagement.state == DefenseEngagementState.Completed)
            {
                report.AddError(
                    $"Defense engagement '{id}' has invalid participants or state.");
            }
            ValidateDistinctGuards(engagement, id, report);
            if (!IsFiniteNonNegative(engagement.guardAttackRemaining)
                || !IsFiniteNonNegative(engagement.intruderAttackRemaining)
                || !IsFiniteNonNegative(engagement.rangedAttackRemaining)
                || !IsFiniteNonNegative(
                    engagement.secondaryRangedAttackRemaining)
                || engagement.exchangeCount < 0)
            {
                report.AddError(
                    $"Defense engagement '{id}' has invalid combat timing.");
            }
        }
    }

    private static void ValidateDistinctGuards(
        DefenseEngagementSaveData engagement,
        string engagementId,
        DungeonGameRestoreReport report)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        string[] guardIds =
        {
            engagement.leadGuardId,
            engagement.reserveGuardId,
            engagement.rangedGuardId,
            engagement.secondaryRangedGuardId
        };
        foreach (string guardId in guardIds)
        {
            if (string.IsNullOrEmpty(guardId))
            {
                continue;
            }
            if (!IsCanonicalId(guardId) || !ids.Add(guardId))
            {
                report.AddError(
                    $"Defense engagement '{engagementId}' has an invalid or duplicate guard.");
            }
        }
        if (engagement.hasReserveCell != !string.IsNullOrEmpty(
                engagement.reserveGuardId))
        {
            report.AddError(
                $"Defense engagement '{engagementId}' has inconsistent reserve state.");
        }
    }

    private static void ValidateOwnerEvacuation(
        OwnerEvacuationSaveSnapshot evacuation,
        bool hasActiveIntruders,
        DungeonGameRestoreReport report)
    {
        if (evacuation.statusText == null
            || evacuation.active && string.IsNullOrWhiteSpace(evacuation.statusText))
        {
            report.AddError("Owner evacuation snapshot has invalid status text.");
        }

        ValidateResidentEvacuation(evacuation, hasActiveIntruders, report);
    }

    private static void ValidateResidentEvacuation(
        OwnerEvacuationSaveSnapshot evacuation,
        bool hasActiveIntruders,
        DungeonGameRestoreReport report)
    {
        ResidentEvacuationZoneSaveData zone = evacuation.residentZone;
        if (zone == null || evacuation.residentParticipants == null)
        {
            report.AddError("Resident evacuation snapshot is missing required state.");
            return;
        }
        if (!Enum.IsDefined(typeof(ResidentEvacuationZoneStatus), zone.status))
        {
            report.AddError("Resident evacuation zone has an invalid status.");
            return;
        }
        if (zone.cellSpans == null
            || zone.cellSpans.Count > MaximumResidentEvacuationRoomSpans
            || evacuation.residentParticipants.Count > MaximumResidentEvacuees)
        {
            report.AddError("Resident evacuation snapshot exceeds its collection limits.");
            return;
        }

        bool hasZone = zone.status != ResidentEvacuationZoneStatus.None;
        if (hasZone != (zone.cellSpans.Count > 0)
            || zone.status != ResidentEvacuationZoneStatus.Active
                && evacuation.residentParticipants.Count > 0)
        {
            report.AddError("Resident evacuation zone and participant state are inconsistent.");
        }
        if (!hasActiveIntruders && evacuation.residentParticipants.Count > 0)
        {
            report.AddError(
                "Resident evacuation participants require an active invasion.");
        }
        if (!hasZone && (zone.anchorX != 0 || zone.anchorY != 0))
        {
            report.AddError(
                "An empty resident evacuation zone cannot retain an anchor.");
        }

        bool anchorIncluded = false;
        int previousY = int.MinValue;
        int previousEndX = int.MinValue;
        foreach (ResidentEvacuationRoomCellSpanSaveData span in zone.cellSpans)
        {
            if (span == null || span.length <= 0)
            {
                report.AddError("Resident evacuation zone contains an invalid cell span.");
                continue;
            }

            long endExclusive = (long)span.startX + span.length;
            if (endExclusive > int.MaxValue + 1L
                || span.y < previousY
                || span.y == previousY
                    && (long)span.startX <= (long)previousEndX + 1L)
            {
                report.AddError("Resident evacuation zone spans are not canonical and disjoint.");
            }
            previousY = span.y;
            previousEndX = (int)(endExclusive - 1L);
            anchorIncluded |= zone.anchorY == span.y
                && zone.anchorX >= span.startX
                && (long)zone.anchorX < endExclusive;
        }
        if (hasZone && !anchorIncluded)
        {
            report.AddError("Resident evacuation zone anchor is outside its canonical cells.");
        }

        HashSet<string> characterIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<(int x, int y)> targetCells = new HashSet<(int x, int y)>();
        foreach (ResidentEvacuationParticipantSaveData participant in
                 evacuation.residentParticipants)
        {
            string characterId = participant?.characterId ?? string.Empty;
            if (participant == null
                || !IsCanonicalId(characterId)
                || !characterIds.Add(characterId)
                || !Enum.IsDefined(
                    typeof(ResidentEvacuationParticipantStatus),
                    participant.status))
            {
                report.AddError(
                    $"Resident evacuation contains invalid participant '{characterId}'.");
                continue;
            }

            bool hasTarget = participant.status is
                ResidentEvacuationParticipantStatus.PendingRoute
                or ResidentEvacuationParticipantStatus.Moving
                or ResidentEvacuationParticipantStatus.Holding
                or ResidentEvacuationParticipantStatus.PreemptedEmergency;
            if (hasTarget
                && (!Contains(zone.cellSpans, participant.targetX, participant.targetY)
                    || !targetCells.Add((participant.targetX, participant.targetY))))
            {
                report.AddError(
                    $"Resident evacuation participant '{characterId}' has an invalid or duplicate target.");
            }
            else if (!hasTarget
                && (participant.targetX != 0 || participant.targetY != 0))
            {
                report.AddError(
                    $"Resident evacuation participant '{characterId}' has a target without a routable status.");
            }
        }
    }

    private static bool Contains(
        IReadOnlyList<ResidentEvacuationRoomCellSpanSaveData> spans,
        int x,
        int y)
    {
        if (spans == null)
        {
            return false;
        }
        for (int index = 0; index < spans.Count; index++)
        {
            ResidentEvacuationRoomCellSpanSaveData span = spans[index];
            if (span != null
                && span.y == y
                && x >= span.startX
                && (long)x < (long)span.startX + span.length)
            {
                return true;
            }
        }
        return false;
    }

    private static void ValidateCampaign(
        DungeonInvasionCampaignSaveData campaign,
        DungeonGameRestoreReport report)
    {
        if (campaign.currentDay < 1
            || campaign.operationSequence < 0
            || campaign.branches == null
            || campaign.supportSites == null
            || campaign.operations == null)
        {
            report.AddError("Invasion campaign is missing required state.");
            return;
        }
        if (campaign.branches.Count != RequiredBranchIds.Count
            || campaign.supportSites.Count > MaximumSupportSites
            || campaign.operations.Count > MaximumOperations)
        {
            report.AddError("Invasion campaign has invalid collection sizes.");
        }

        HashSet<string> branchIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonInvasionBranchSaveData branch in campaign.branches)
        {
            string id = branch?.branchId ?? string.Empty;
            if (branch == null
                || !RequiredBranchIds.Contains(id)
                || !branchIds.Add(id)
                || !IsCanonicalText(branch.displayName)
                || !IsFiniteRange(branch.strength, 0f, 100f)
                || branch.operational != (branch.strength > 0f)
                || !IsFiniteNonNegative(branch.lastRecoveryAmount)
                || branch.recoveryReason == null)
            {
                report.AddError($"Invasion campaign contains invalid branch '{id}'.");
            }
        }
        if (!branchIds.SetEquals(RequiredBranchIds))
        {
            report.AddError("Invasion campaign does not contain every required branch.");
        }

        HashSet<string> siteIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonInvasionSupportSiteSaveData site in campaign.supportSites)
        {
            string id = site?.siteId ?? string.Empty;
            if (site == null
                || !IsCanonicalId(id)
                || !siteIds.Add(id)
                || !branchIds.Contains(site.branchId)
                || !IsCanonicalText(site.displayName)
                || site.destroyedDay < 0
                || site.destroyedDay > campaign.currentDay)
            {
                report.AddError(
                    $"Invasion campaign contains invalid support site '{id}'.");
            }
        }

        int highestOperationSequence = 0;
        HashSet<string> operationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonInvasionOperationSaveData operation in campaign.operations)
        {
            string id = operation?.operationId ?? string.Empty;
            if (operation == null
                || !TryParsePositiveSequence(id, OperationPrefix, out int sequence)
                || !operationIds.Add(id))
            {
                report.AddError(
                    $"Invasion campaign contains invalid operation '{id}'.");
                continue;
            }
            highestOperationSequence = Math.Max(highestOperationSequence, sequence);
            if (!Enum.IsDefined(typeof(InvasionOperationKind), operation.kind)
                || !branchIds.Contains(operation.primaryBranchId)
                || operation.participatingBranchIds == null
                || operation.participatingBranchIds.Count == 0
                || operation.participatingBranchIds.Distinct(
                    StringComparer.Ordinal).Count()
                    != operation.participatingBranchIds.Count
                || operation.participatingBranchIds.Any(
                    branchId => !branchIds.Contains(branchId))
                || !operation.participatingBranchIds.Contains(
                    operation.primaryBranchId)
                || !IsCanonicalId(operation.objectiveId)
                || operation.scheduledDay < 1
                || operation.scheduledDay > campaign.currentDay
                || !IsFiniteRange(operation.intelligenceConfidence, 0f, 1f))
            {
                report.AddError(
                    $"Invasion campaign operation '{id}' contains invalid data.");
            }
        }
        if (campaign.operationSequence < highestOperationSequence)
        {
            report.AddError(
                "Invasion campaign sequence is below a saved operation sequence.");
        }
    }

    private static bool TryParsePositiveSequence(
        string id,
        string prefix,
        out int sequence)
    {
        sequence = 0;
        return IsCanonicalId(id)
            && id.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(id.Substring(prefix.Length), out sequence)
            && sequence > 0;
    }

    private static bool IsEndlessCombatEffectOwner(string value)
    {
        if (!IsCanonicalId(value))
        {
            return false;
        }

        string[] segments = value.Split(':');
        return segments.Length == 5
            && string.Equals(segments[0], "endless-crisis", StringComparison.Ordinal)
            && int.TryParse(segments[1], out int cycle)
            && cycle > 0
            && int.TryParse(segments[2], out int startedAbsoluteDay)
            && startedAbsoluteDay > 0
            && string.Equals(segments[3], "axis", StringComparison.Ordinal)
            && int.TryParse(segments[4], out int axis)
            && axis == EndlessCombatAxisProtocolValue;
    }

    private static bool IsCanonicalId(string value)
    {
        return value != null
            && value.Length is > 0 and <= 256
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && !value.Any(char.IsControl);
    }

    private static bool IsCanonicalText(string value)
    {
        return value != null
            && value.Length is > 0 and <= 512
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && !value.Any(char.IsControl);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinitePositive(float value)
    {
        return IsFinite(value) && value > 0f;
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return IsFinite(value) && value >= 0f;
    }

    private static bool IsFiniteRange(float value, float minimum, float maximum)
    {
        return IsFinite(value) && value >= minimum && value <= maximum;
    }
}
