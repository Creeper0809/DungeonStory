using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SurvivalResourcesSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonSurvivalSaveData,
        SurvivalFoodRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "survival.resources";

    private static readonly string[] Dependencies =
    {
        CharacterWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        WildlifeSaveSection.Id,
        ServiceRoomsSaveSection.Id
    };
    private readonly ISurvivalFoodPersistence runtime;
    private readonly IItemDefinitionCatalog itemCatalog;
    private readonly IRestoreWorldCandidateQuery worldCandidates;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IServiceSessionRuntime serviceSessions;

    public SurvivalResourcesSaveSection(
        ISurvivalFoodPersistence runtime,
        IItemDefinitionCatalog itemCatalog = null,
        IRestoreWorldCandidateQuery worldCandidates = null,
        IPhysicalItemRestoreCandidateQuery physicalCandidates = null,
        IServiceSessionRuntime serviceSessions = null)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.itemCatalog = itemCatalog;
        this.worldCandidates = worldCandidates;
        this.physicalCandidates = physicalCandidates;
        this.serviceSessions = serviceSessions;
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonSurvivalSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonSurvivalSaveData CapturePayload() =>
        runtime.Capture();

    protected override void NormalizeRestorePayload(
        DungeonSurvivalSaveData payload,
        DungeonGameRestoreReport report) =>
        V18SurvivalEnvironmentCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override SurvivalFoodRestoreCandidate BuildRestoreCandidate(
        DungeonSurvivalSaveData payload)
    {
        SurvivalFoodRestoreCandidate candidate =
            runtime.BuildRestoreCandidate(payload);
        ValidateTreatmentRestoreJoins(payload);
        return candidate;
    }

    protected override void ValidateParsedPayload(
        DungeonSurvivalSaveData payload) =>
        runtime.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        SurvivalFoodRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);

    private void ValidateTreatmentRestoreJoins(
        DungeonSurvivalSaveData payload)
    {
        IReadOnlyList<SurvivalTreatmentPlanSaveData> plans =
            payload?.activeTreatmentPlans;
        if (plans == null)
        {
            return;
        }
        if (physicalCandidates == null)
        {
            if (plans.Count == 0)
            {
                return;
            }
            throw new InvalidOperationException(
                "Survival treatment restore physical joins are unavailable.");
        }
        if (!physicalCandidates.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Survival treatment restore physical candidate is unavailable.");
        }
        IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            pendingReceipts = physicalCandidates.PendingBatchDispositions
                ?? Array.Empty<
                    PhysicalItemRestoreCandidateDispositionSnapshot>();
        PhysicalItemRestoreCandidateDispositionSnapshot orphanReceipt =
            pendingReceipts.FirstOrDefault(receipt => receipt != null
                && string.Equals(
                    receipt.ReasonCode,
                    SurvivalFoodRuntime.TreatmentPhysicalSinkReason,
                    StringComparison.Ordinal));
        if (plans.Count == 0)
        {
            if (orphanReceipt != null)
            {
                throw new InvalidOperationException(
                    "Survival treatment restore found an orphan physical receipt: "
                    + orphanReceipt.OperationId);
            }
            return;
        }
        if (itemCatalog == null
            || worldCandidates == null
            || serviceSessions == null
            || !worldCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> buildings)
            || !worldCandidates.TryGetCharacters(
                out IReadOnlyList<CharacterActor> characters))
        {
            throw new InvalidOperationException(
                "Survival treatment restore joins are unavailable.");
        }

        Dictionary<string, BuildableObject> facilities = (buildings
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null)
            .ToDictionary(
                value => value.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal);
        Dictionary<string, CharacterActor> patients = (characters
                ?? Array.Empty<CharacterActor>())
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.Identity?.PersistentId))
            .ToDictionary(
                value => value.Identity.PersistentId,
                StringComparer.Ordinal);
        Dictionary<string, ServiceSessionSnapshot> activeSessions =
            serviceSessions.ActiveSessions
                .Where(value => value != null && value.IsActive)
                .ToDictionary(value => value.SessionId, StringComparer.Ordinal);
        HashSet<string> ownedPhysicalOperations = plans
            .Where(value => value != null)
            .Select(value => value.physicalCommitOperationId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (SurvivalTreatmentPlanSaveData plan in plans)
        {
            if (plan == null
                || !itemCatalog.TryGet(
                    (ItemDefinitionId)plan.itemDefinitionId,
                    out ItemDefinitionSO item)
                || item == null)
            {
                throw new InvalidOperationException(
                    "Survival treatment restore references a missing patient, medical facility, or item: "
                    + (plan?.operationId ?? "<null>"));
            }
            patients.TryGetValue(
                plan.patientId,
                out CharacterActor patient);
            facilities.TryGetValue(
                plan.facilityInstanceId,
                out BuildableObject facility);
            bool patientLive = patient != null && !patient.IsDead;
            bool facilityLive = facility != null
                && !facility.isDestroy
                && facility.BuildingData?
                    .GetAbility<BuildingMedicalAbility>() != null;
            bool ownerLost = !patientLive || !facilityLive;
            if (ownerLost
                && plan.phase == SurvivalTreatmentPlanPhase.IntentRecorded)
            {
                throw new InvalidOperationException(
                    "Survival treatment restore has an unconsumed plan with a lost owner: "
                    + plan.operationId);
            }
            if (facilityLive
                && (plan.physicalCommitPositionX != facility.centerPos.x
                    || plan.physicalCommitPositionY != facility.centerPos.y))
            {
                throw new InvalidOperationException(
                    "Survival treatment restore commit position does not match its facility: "
                    + plan.operationId);
            }
            if (!ownerLost
                && plan.phase < SurvivalTreatmentPlanPhase.EffectsPublished
                && !payload.health.Any(entry => entry != null
                    && SurvivalHealthStateRules.IsActiveIssue(entry)
                    && string.Equals(
                        entry.persistentId,
                        plan.patientId,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Survival treatment restore pre-effect plan has no treatable patient state: "
                    + plan.operationId);
            }

            bool facilityHasSession = facilityLive
                && facility.GetServiceHubAbility() != null;
            bool planHasSession = !string.IsNullOrEmpty(plan.serviceSessionId);
            if (!ownerLost && facilityHasSession != planHasSession)
            {
                throw new InvalidOperationException(
                    "Survival treatment restore service-session ownership does not match its facility: "
                    + plan.operationId);
            }
            bool sessionMustBeActive = plan.phase <
                SurvivalTreatmentPlanPhase.ServiceCompleted;
            ServiceSessionSnapshot session = null;
            bool hasActiveSession = planHasSession
                && activeSessions.TryGetValue(
                    plan.serviceSessionId,
                    out session);
            if (planHasSession
                && ((!ownerLost
                        && sessionMustBeActive != hasActiveSession)
                    || ownerLost
                        && !sessionMustBeActive
                        && hasActiveSession
                    || hasActiveSession
                    && (!string.Equals(
                            session.ActorId,
                            plan.patientId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            session.HubId,
                            plan.facilityInstanceId,
                            StringComparison.Ordinal))))
            {
                throw new InvalidOperationException(
                    "Survival treatment restore service-session join is invalid: "
                    + plan.operationId);
            }

            bool hasReceipt = physicalCandidates.TryGetPendingBatchDisposition(
                plan.physicalCommitOperationId,
                out PhysicalItemRestoreCandidateDispositionSnapshot receipt);
            bool receiptRequired = plan.phase >=
                SurvivalTreatmentPlanPhase.ItemCommitted;
            if (receiptRequired != hasReceipt
                || hasReceipt && !ReceiptMatches(plan, receipt))
            {
                throw new InvalidOperationException(
                    "Survival treatment restore physical receipt join is invalid: "
                    + plan.operationId);
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 pendingReceipts)
        {
            if (receipt != null
                && string.Equals(
                    receipt.ReasonCode,
                    SurvivalFoodRuntime.TreatmentPhysicalSinkReason,
                    StringComparison.Ordinal)
                && !ownedPhysicalOperations.Contains(receipt.OperationId))
            {
                throw new InvalidOperationException(
                    "Survival treatment restore found an orphan physical receipt: "
                    + receipt.OperationId);
            }
        }
    }

    private static bool ReceiptMatches(
        SurvivalTreatmentPlanSaveData plan,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            plan.physicalCommitOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            plan.physicalCommitReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            plan.physicalCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == plan.physicalCommitQuantity
        && receipt.InputMassGrams == plan.physicalCommitInputMassGrams
        && receipt.SourceStackIds.SequenceEqual(
            plan.physicalCommitSourceStackIds,
            StringComparer.Ordinal);
}

public sealed class DarkSurvivalSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonDarkSurvivalSaveData,
        DarkSurvivalRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "survival.deprivation";

    private static readonly string[] Dependencies =
    {
        CharacterWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        SurvivalResourcesSaveSection.Id
    };
    private readonly ICharacterDeprivationPersistence runtime;

    public DarkSurvivalSaveSection(ICharacterDeprivationPersistence runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonDarkSurvivalSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonDarkSurvivalSaveData CapturePayload() =>
        runtime.Capture();

    protected override void NormalizeRestorePayload(
        DungeonDarkSurvivalSaveData payload,
        DungeonGameRestoreReport report) =>
        V18SurvivalEnvironmentCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override void ValidateParsedPayload(
        DungeonDarkSurvivalSaveData payload) =>
        CharacterDeprivationPersistenceCoordinator.ValidatePayloadShape(payload);

    protected override DarkSurvivalRestoreCandidate BuildRestoreCandidate(
        DungeonDarkSurvivalSaveData payload) =>
        runtime.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        DarkSurvivalRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);
}
