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

    private const string CustomPolicyPrefix = DefenseResponsePolicyIds.CustomPrefix;
    private const string EngagementPrefix = "defense-engagement:";
    private const string OperationPrefix = "human-operation:";

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

        ValidateThreat(payload.threat, report);
        HashSet<string> intruderIds = ValidateIntruders(
            payload.activeIntruders,
            patterns,
            report);
        ValidatePolicies(payload.responsePolicies, report);
        ValidateEngagements(payload.engagements, intruderIds, report);
        ValidateOwnerEvacuation(payload.ownerEvacuation, report);
        ValidateCampaign(payload.campaign, report);
    }

    private static void ValidateThreat(
        DungeonInvasionThreatSaveData threat,
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
        DungeonGameRestoreReport report)
    {
        if (evacuation.statusText == null
            || evacuation.active && string.IsNullOrWhiteSpace(evacuation.statusText))
        {
            report.AddError("Owner evacuation snapshot has invalid status text.");
        }
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
