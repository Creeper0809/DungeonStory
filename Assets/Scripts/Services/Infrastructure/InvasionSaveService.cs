using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IInvasionSaveService
{
    DungeonInvasionSaveData Capture();
    void Restore(DungeonInvasionSaveData source, DungeonGameRestoreReport report);
}

[Serializable]
public sealed class DungeonInvasionSaveData
{
    public DungeonInvasionThreatSaveData threat = new DungeonInvasionThreatSaveData();
    public List<DungeonInvasionIntruderSaveData> activeIntruders =
        new List<DungeonInvasionIntruderSaveData>();
    public DefenseResponsePolicySaveSnapshot responsePolicies = new DefenseResponsePolicySaveSnapshot();
    public DefenseEngagementSaveSnapshot engagements = new DefenseEngagementSaveSnapshot();
    public OwnerEvacuationSaveSnapshot ownerEvacuation = new OwnerEvacuationSaveSnapshot();
    public InvasionCampaignSaveData campaign = new InvasionCampaignSaveData();
}

[Serializable]
public sealed class DungeonInvasionThreatSaveData
{
    public float currentThreat;
    public float secondsSinceLastInvasion;
    public float safetyRemaining;
    public float candidateDelayRemaining = -1f;
    public float warningCooldownRemaining;
    public bool warningRaisedThisCycle;
    public bool candidateRaisedThisCycle;
    public float residualRisk;
    public float dungeonValueFactor;
    public float reputationFactor;
    public float timeFactor;
    public float riskFactor;
}

[Serializable]
public sealed class DungeonInvasionIntruderSaveData
{
    public string runtimeId = string.Empty;
    public int dataId = -1;
    public float worldX;
    public float worldY;
    public float worldZ;
    public int gridX;
    public int gridY;
    public InvasionIntruderState state;
    public float elapsedSeconds;
    public float rallyRemainingSeconds;
    public bool hasBreachedDungeonInterior;
    public float damageDelayRemaining;
    public int facilityDamageCount;
    public float currentHealth;
    public float injurySeverity;
    public float baseMood;
    public DungeonInvasionIntruderSettingsSaveData settings =
        new DungeonInvasionIntruderSettingsSaveData();
    public List<DungeonCharacterConditionSaveData> conditions =
        new List<DungeonCharacterConditionSaveData>();
    public List<DungeonDefenseStatusSaveData> defenseStatuses =
        new List<DungeonDefenseStatusSaveData>();
}

[Serializable]
public sealed class DungeonInvasionIntruderSettingsSaveData
{
    public string patternId = InvasionIntruderPatternIds.Hunter;
    public float rallyDurationSeconds = 12f;
    public float secondsToFullFocus = 30f;
    public float repathIntervalSeconds = 1.5f;
    public float facilityDamageIntervalSeconds = 5f;
    public float finalCombatDamage = 45f;
    public float finalCombatWindupSeconds = 0.7f;
    public float healthMultiplier = 1f;
    public float meleeDamageMultiplier = 1f;
    public float attackSpeedMultiplier = 1f;
}

[Serializable]
public sealed class DungeonDefenseStatusSaveData
{
    public DefenseStatusKind kind;
    public float value;
    public float remainingSeconds;
    public int stacks;
}

public sealed class InvasionSaveService : IInvasionSaveService
{
    private readonly IInvasionThreatRuntimeProvider threatProvider;
    private readonly IInvasionDirectorRuntimeProvider directorProvider;
    private readonly IGridSystemProvider gridProvider;
    private readonly IDefenseResponsePolicyRuntime responsePolicyRuntime;
    private readonly IDefenseEngagementRuntime engagementRuntime;
    private readonly IInvasionOwnerEvacuationService ownerEvacuationService;
    private readonly IInvasionCampaignRuntime campaignRuntime;
    private readonly IOffenseRegionRuntime offenseRegions;

    public InvasionSaveService(
        IInvasionThreatRuntimeProvider threatProvider,
        IInvasionDirectorRuntimeProvider directorProvider,
        IGridSystemProvider gridProvider,
        IDefenseResponsePolicyRuntime responsePolicyRuntime,
        IDefenseEngagementRuntime engagementRuntime,
        IInvasionOwnerEvacuationService ownerEvacuationService,
        IInvasionCampaignRuntime campaignRuntime,
        IOffenseRegionRuntime offenseRegions)
    {
        this.threatProvider = threatProvider
            ?? throw new ArgumentNullException(nameof(threatProvider));
        this.directorProvider = directorProvider
            ?? throw new ArgumentNullException(nameof(directorProvider));
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
        this.offenseRegions = offenseRegions
            ?? throw new ArgumentNullException(nameof(offenseRegions));
    }

    public DungeonInvasionSaveData Capture()
    {
        DungeonInvasionSaveData result = new DungeonInvasionSaveData();
        if (threatProvider.TryGetRuntime(out InvasionThreatRuntime threatRuntime))
        {
            InvasionThreatPersistenceState state = threatRuntime.CapturePersistentState();
            result.threat = new DungeonInvasionThreatSaveData
            {
                currentThreat = state.CurrentThreat,
                secondsSinceLastInvasion = state.SecondsSinceLastInvasion,
                safetyRemaining = state.SafetyRemaining,
                candidateDelayRemaining = state.CandidateDelayRemaining,
                warningCooldownRemaining = state.WarningCooldownRemaining,
                warningRaisedThisCycle = state.WarningRaisedThisCycle,
                candidateRaisedThisCycle = state.CandidateRaisedThisCycle,
                residualRisk = state.ResidualRisk,
                dungeonValueFactor = state.LastFactors.dungeonValue,
                reputationFactor = state.LastFactors.reputation,
                timeFactor = state.LastFactors.time,
                riskFactor = state.LastFactors.risk
            };
        }

        if (directorProvider.TryGetRuntime(out InvasionDirectorRuntime director)
            && gridProvider.TryGetGrid(out Grid grid))
        {
            result.activeIntruders = director.CapturePersistentState(grid)
                .Select(ToSaveData)
                .ToList();
        }

        result.responsePolicies = responsePolicyRuntime.Capture();
        result.engagements = engagementRuntime.Capture();
        result.ownerEvacuation = ownerEvacuationService.Capture();
        result.campaign = campaignRuntime.Capture();

        return result;
    }

    public void Restore(DungeonInvasionSaveData source, DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        source ??= new DungeonInvasionSaveData();
        if (source.campaign != null
            && source.campaign.branches != null
            && source.campaign.branches.Count > 0)
        {
            campaignRuntime.Restore(source.campaign);
        }
        else
        {
            campaignRuntime.RestoreFromLegacyPressure(offenseRegions);
            report.AddWarning(
                "Invasion V1 regional pressure migrated to five human campaign branches.");
        }
        DungeonInvasionThreatSaveData threat = source.threat ?? new DungeonInvasionThreatSaveData();
        if (threatProvider.TryGetRuntime(out InvasionThreatRuntime threatRuntime))
        {
            threatRuntime.RestorePersistentState(new InvasionThreatPersistenceState(
                threat.currentThreat,
                threat.secondsSinceLastInvasion,
                threat.safetyRemaining,
                threat.candidateDelayRemaining,
                threat.warningCooldownRemaining,
                threat.warningRaisedThisCycle,
                threat.candidateRaisedThisCycle,
                threat.residualRisk,
                new InvasionThreatFactors(
                    threat.dungeonValueFactor,
                    threat.reputationFactor,
                    threat.timeFactor,
                    threat.riskFactor)));
        }
        else
        {
            report.AddWarning("Invasion threat runtime was not present; threat state was skipped.");
        }

        List<string> warnings = new List<string>();
        responsePolicyRuntime.Restore(source.responsePolicies, warnings);

        if (!directorProvider.TryGetRuntime(out InvasionDirectorRuntime director))
        {
            report.AddWarning("Invasion director runtime was not present; active intruders were skipped.");
            foreach (string warning in warnings)
            {
                report.AddWarning(warning);
            }
            return;
        }

        report.RecordRestoredIntruders(director.RestorePersistentState(
            (source.activeIntruders ?? new List<DungeonInvasionIntruderSaveData>())
                .Where(saved => saved != null)
                .Select(ToRuntimeState),
            warnings));
        ownerEvacuationService.Restore(source.ownerEvacuation, warnings);
        engagementRuntime.Restore(source.engagements, warnings);
        foreach (string warning in warnings)
        {
            report.AddWarning(warning);
        }
    }

    private static DungeonInvasionIntruderSaveData ToSaveData(InvasionIntruderPersistenceState source)
    {
        InvasionIntruderSettings settings = source.Settings ?? new InvasionIntruderSettings();
        return new DungeonInvasionIntruderSaveData
        {
            runtimeId = source.RuntimeId,
            dataId = source.DataId,
            worldX = source.WorldPosition.x,
            worldY = source.WorldPosition.y,
            worldZ = source.WorldPosition.z,
            gridX = source.GridPosition.x,
            gridY = source.GridPosition.y,
            state = source.State,
            elapsedSeconds = source.ElapsedSeconds,
            rallyRemainingSeconds = source.RallyRemainingSeconds,
            hasBreachedDungeonInterior = source.HasBreachedDungeonInterior,
            damageDelayRemaining = source.DamageDelayRemaining,
            facilityDamageCount = source.FacilityDamageCount,
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
                finalCombatDamage = settings.finalCombatDamage,
                finalCombatWindupSeconds = settings.finalCombatWindupSeconds,
                healthMultiplier = settings.healthMultiplier,
                meleeDamageMultiplier = settings.meleeDamageMultiplier,
                attackSpeedMultiplier = settings.attackSpeedMultiplier
            },
            conditions = source.Conditions
                .OrderBy(pair => pair.Key)
                .Select(pair => new DungeonCharacterConditionSaveData
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

    private static InvasionIntruderPersistenceState ToRuntimeState(DungeonInvasionIntruderSaveData source)
    {
        DungeonInvasionIntruderSettingsSaveData settings = source.settings
            ?? new DungeonInvasionIntruderSettingsSaveData();
        Dictionary<CharacterCondition, float> conditions = (source.conditions
                ?? new List<DungeonCharacterConditionSaveData>())
            .Where(condition => condition != null)
            .GroupBy(condition => condition.condition)
            .ToDictionary(group => group.Key, group => group.Last().value);
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
                rallyDurationSeconds = settings.rallyDurationSeconds > 0f
                    ? settings.rallyDurationSeconds
                    : 12f,
                secondsToFullFocus = settings.secondsToFullFocus,
                repathIntervalSeconds = settings.repathIntervalSeconds,
                facilityDamageIntervalSeconds = settings.facilityDamageIntervalSeconds,
                finalCombatDamage = settings.finalCombatDamage,
                finalCombatWindupSeconds = settings.finalCombatWindupSeconds,
                healthMultiplier = settings.healthMultiplier > 0f ? settings.healthMultiplier : 1f,
                meleeDamageMultiplier = settings.meleeDamageMultiplier > 0f ? settings.meleeDamageMultiplier : 1f,
                attackSpeedMultiplier = settings.attackSpeedMultiplier > 0f ? settings.attackSpeedMultiplier : 1f
            },
            (source.defenseStatuses ?? new List<DungeonDefenseStatusSaveData>())
                .Where(status => status != null)
                .Select(status => new DefenseStatusSnapshot(
                    status.kind,
                    status.value,
                    status.remainingSeconds,
                    status.stacks)),
            source.runtimeId,
            source.rallyRemainingSeconds,
            source.hasBreachedDungeonInterior);
    }
}
