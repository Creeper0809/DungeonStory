using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IInvasionSaveService
{
    DungeonInvasionSaveData Capture();
    void ValidateRestorePayload(
        DungeonInvasionSaveData source,
        DungeonGameRestoreReport report);
    InvasionRestoreCandidate PrepareRestore(DungeonInvasionSaveData source);
    void PublishRestore(InvasionRestoreCandidate candidate);
}

public interface IInvasionPreparedRestoreRuntimeCandidate :
    IDungeonDiscardableRestoreCandidate
{
    void Stage();
}

public interface IInvasionSaveRuntimePort
{
    DungeonInvasionSaveData Capture();
    IInvasionPreparedRestoreRuntimeCandidate PrepareRestore(
        DungeonInvasionSaveData source,
        DungeonGameRestoreReport report);
    void PublishRestoreCandidate();
    void RollbackPublishedRestoreCandidate();
    void CompleteRestoreCandidate();
    void DiscardRestoreCandidate();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class InvasionRestoreCandidate :
    IDungeonDiscardableRestoreCandidate,
    IDungeonRestoreReportContributor
{
    private InvasionSaveService owner;
    private readonly int restoredIntruderCount;

    internal InvasionRestoreCandidate(
        InvasionSaveService owner,
        IInvasionPreparedRestoreRuntimeCandidate runtimeCandidate,
        int restoredIntruderCount)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        RuntimeCandidate = runtimeCandidate
            ?? throw new ArgumentNullException(nameof(runtimeCandidate));
        this.restoredIntruderCount = Math.Max(0, restoredIntruderCount);
    }

    internal IInvasionPreparedRestoreRuntimeCandidate RuntimeCandidate { get; }

    public void Discard() => owner?.DiscardPreparedCandidate(this);

    public void RecordRestoreResult(DungeonGameRestoreReport report)
    {
        (report ?? throw new ArgumentNullException(nameof(report)))
            .RecordRestoredIntruders(restoredIntruderCount);
    }

    internal void Detach() => owner = null;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonInvasionSaveData
{
    public const int CurrentVersion = 6;

    public int version = CurrentVersion;
    public DungeonInvasionThreatSaveData threat = new DungeonInvasionThreatSaveData();
    public List<DungeonInvasionIntruderSaveData> activeIntruders =
        new List<DungeonInvasionIntruderSaveData>();
    public DefenseResponsePolicySaveSnapshot responsePolicies =
        new DefenseResponsePolicySaveSnapshot();
    public DefenseEngagementSaveSnapshot engagements =
        new DefenseEngagementSaveSnapshot();
    public OwnerEvacuationSaveSnapshot ownerEvacuation =
        new OwnerEvacuationSaveSnapshot();
    public DungeonInvasionCampaignSaveData campaign =
        new DungeonInvasionCampaignSaveData();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonInvasionIntruderSaveData
{
    public string runtimeId = string.Empty;
    public int dataId = -1;
    public EnemyIndividualSaveData enemyIndividual = new EnemyIndividualSaveData();
    public float worldX;
    public float worldY;
    public float worldZ;
    public int gridX;
    public int gridY;
    public InvasionIntruderState state;
    public float elapsedSeconds;
    public float rallyRemainingSeconds;
    public bool hasBreachedDungeonInterior;
    public int breachTargetBuildingId = -1;
    public int breachTargetX;
    public int breachTargetY;
    public int breachAttackX;
    public int breachAttackY;
    public float structureAttackDelayRemaining;
    public float trappedSeconds;
    public bool enragedBreach;
    public DungeonInvasionRaidAwarenessSaveData raidAwareness =
        new DungeonInvasionRaidAwarenessSaveData();
    public float damageDelayRemaining;
    public int facilityDamageCount;
    public List<string> damagedFacilityBuildingInstanceIds =
        new List<string>();
    public float currentHealth;
    public float injurySeverity;
    public float baseMood;
    public DungeonInvasionIntruderSettingsSaveData settings =
        new DungeonInvasionIntruderSettingsSaveData();
    public List<DungeonInvasionConditionSaveData> conditions =
        new List<DungeonInvasionConditionSaveData>();
    public List<DungeonDefenseStatusSaveData> defenseStatuses =
        new List<DungeonDefenseStatusSaveData>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonInvasionIntruderSettingsSaveData
{
    public string patternId = InvasionIntruderPatternIds.Hunter;
    public float rallyDurationSeconds = 12f;
    public float secondsToFullFocus = 30f;
    public float repathIntervalSeconds = 1.5f;
    public float facilityDamageIntervalSeconds = 5f;
    public float structureAttackIntervalSeconds = 1.25f;
    public float finalCombatDamage = 45f;
    public float finalCombatWindupSeconds = 0.7f;
    public float healthMultiplier = 1f;
    public float meleeDamageMultiplier = 1f;
    public float attackSpeedMultiplier = 1f;
    public float riskTolerance = 0.55f;
    public float routeCommitmentSeconds = 2f;
    public float structureDamageMultiplier = 1f;
    public InvasionOperationKind operationKind = InvasionOperationKind.FrontalAssault;
    public string raidId = string.Empty;
}

[Serializable]
public sealed class DungeonInvasionConditionSaveData
{
    public CharacterCondition condition;
    public float value;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonDefenseStatusSaveData
{
    public DefenseStatusKind kind;
    public float value;
    public float remainingSeconds;
    public int stacks;
}

[Serializable]
public sealed class DungeonInvasionKnownRiskSaveData
{
    public int x;
    public int y;
    public float severity;
    public string facilityBuildingInstanceId = string.Empty;
}

[Serializable]
public sealed class DungeonInvasionExpectedPathCellSaveData
{
    public int x;
    public int y;
}

[Serializable]
public sealed class DungeonInvasionRaidAwarenessSaveData
{
    public string raidId = string.Empty;
    public int identificationStage;
    public string routeChangeReason = string.Empty;
    public string breachTargetBuildingInstanceId = string.Empty;
    public List<DungeonInvasionKnownRiskSaveData> knownRisks =
        new List<DungeonInvasionKnownRiskSaveData>();
    public List<DungeonInvasionExpectedPathCellSaveData> expectedPath =
        new List<DungeonInvasionExpectedPathCellSaveData>();
}

[Serializable]
public sealed class DungeonInvasionBranchSaveData
{
    public string branchId = string.Empty;
    public string displayName = string.Empty;
    public float strength = 70f;
    public bool operational = true;
    public float lastRecoveryAmount;
    public string recoveryReason = string.Empty;
}

[Serializable]
public sealed class DungeonInvasionSupportSiteSaveData
{
    public string siteId = string.Empty;
    public string branchId = string.Empty;
    public string displayName = string.Empty;
    public int q;
    public int r;
    public bool alive = true;
    public bool connected = true;
    public int destroyedDay;
}

[Serializable]
public sealed class DungeonInvasionOperationSaveData
{
    public string operationId = string.Empty;
    public InvasionOperationKind kind;
    public string primaryBranchId = string.Empty;
    public List<string> participatingBranchIds = new List<string>();
    public string objectiveId = string.Empty;
    public int scheduledDay;
    public float intelligenceConfidence;
}

[Serializable]
public sealed class DungeonInvasionCampaignSaveData
{
    public int currentDay = 1;
    public int operationSequence;
    public List<DungeonInvasionBranchSaveData> branches =
        new List<DungeonInvasionBranchSaveData>();
    public List<DungeonInvasionSupportSiteSaveData> supportSites =
        new List<DungeonInvasionSupportSiteSaveData>();
    public List<DungeonInvasionOperationSaveData> operations =
        new List<DungeonInvasionOperationSaveData>();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class InvasionSaveService :
    IInvasionSaveService,
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId = "550.world.invasion";

    private readonly IInvasionSaveRuntimePort runtimePort;
    private readonly IInvasionIntruderPatternDefinitionCatalog patternCatalog;
    private bool restoreTransactionActive;
    private bool restoreCandidatePrepared;
    private bool restorePublicationPending;
    private InvasionRestoreCandidate preparedCandidate;

    public InvasionSaveService(
        IInvasionSaveRuntimePort runtimePort,
        IInvasionIntruderPatternDefinitionCatalog patternCatalog)
    {
        this.runtimePort = runtimePort
            ?? throw new ArgumentNullException(nameof(runtimePort));
        this.patternCatalog = patternCatalog
            ?? throw new ArgumentNullException(nameof(patternCatalog));
    }

    public string ParticipantId => RestoreParticipantId;

    public DungeonInvasionSaveData Capture() => runtimePort.Capture();

    public void ValidateRestorePayload(
        DungeonInvasionSaveData source,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        InvasionSaveValidation.Validate(source, patternCatalog, report);
    }

    public InvasionRestoreCandidate PrepareRestore(DungeonInvasionSaveData source)
    {
        if (preparedCandidate != null)
        {
            throw new InvalidOperationException(
                "An invasion restore candidate is already prepared.");
        }

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ValidateRestorePayload(source, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Invasion restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        try
        {
            IInvasionPreparedRestoreRuntimeCandidate runtimeCandidate =
                runtimePort.PrepareRestore(source, report);
            if (runtimeCandidate == null || !report.Success)
            {
                runtimeCandidate?.Discard();
                runtimePort.DiscardRestoreCandidate();
                throw new InvalidOperationException(
                    "Invasion world candidate is invalid: "
                    + string.Join(" | ", report.Errors));
            }

            preparedCandidate = new InvasionRestoreCandidate(
                this,
                runtimeCandidate,
                source.activeIntruders?.Count ?? 0);
            return preparedCandidate;
        }
        catch
        {
            runtimePort.DiscardRestoreCandidate();
            preparedCandidate = null;
            throw;
        }
    }

    public void PublishRestore(InvasionRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        if (!restoreTransactionActive)
        {
            throw new InvalidOperationException(
                "Invasion restore requires the V18 save registry transaction boundary.");
        }
        if (!ReferenceEquals(candidate, preparedCandidate)
            || restoreCandidatePrepared)
        {
            throw new InvalidOperationException(
                "The invasion restore candidate is missing, stale, or already staged.");
        }

        candidate.RuntimeCandidate.Stage();
        restoreCandidatePrepared = true;
        candidate.Detach();
    }

    public void BeginRestoreCandidate()
    {
        if (restoreTransactionActive || restorePublicationPending)
        {
            throw new InvalidOperationException(
                "An invasion restore transaction is already active.");
        }
        restoreTransactionActive = true;
        restoreCandidatePrepared = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreTransactionActive || !restoreCandidatePrepared)
        {
            throw new InvalidOperationException(
                "No invasion restore candidate is ready to publish.");
        }

        runtimePort.PublishRestoreCandidate();
        restorePublicationPending = true;
        restoreCandidatePrepared = false;
        restoreTransactionActive = false;
        preparedCandidate = null;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        try
        {
            runtimePort.RollbackPublishedRestoreCandidate();
        }
        finally
        {
            try
            {
                preparedCandidate?.Detach();
            }
            finally
            {
                restorePublicationPending = false;
                restoreCandidatePrepared = false;
                restoreTransactionActive = false;
                preparedCandidate = null;
            }
        }
    }

    public void CompleteRestoreCandidate()
    {
        if (!restorePublicationPending)
        {
            return;
        }

        runtimePort.CompleteRestoreCandidate();
        restorePublicationPending = false;
    }

    public void DiscardRestoreCandidate()
    {
        if (restorePublicationPending)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }

        runtimePort.DiscardRestoreCandidate();
        preparedCandidate?.Detach();
        restoreCandidatePrepared = false;
        restoreTransactionActive = false;
        preparedCandidate = null;
    }

    internal void DiscardPreparedCandidate(InvasionRestoreCandidate candidate)
    {
        if (!ReferenceEquals(candidate, preparedCandidate))
        {
            candidate?.Detach();
            return;
        }

        runtimePort.DiscardRestoreCandidate();
        preparedCandidate = null;
        candidate.Detach();
    }
}
