#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;

public static class CharacterMedicalOutcomeProductionEntryDebugScenarios
{
    [MenuItem("DungeonStory/Debug/V27/Run Character Medical Outcome Production Entry Contracts")]
    public static void RunOrThrow() =>
        CharacterMedicalSupplyDestinationDrainRuntimeDebugScenarios
            .RunMedicalOutcomeProductionEntryOrThrow();
}

public static partial class CharacterMedicalSupplyDestinationDrainRuntimeDebugScenarios
{
    internal static void RunMedicalOutcomeProductionEntryOrThrow()
    {
        VerifyMedicalProcedureFactRecovery(false);
        VerifyMedicalProcedureFactRecovery(true);
        using (MedicalOutcomeFixture success = new("success"))
        {
            Require(success.Runtime.TryGetOrder(
                        "medical:1", out CharacterMedicalOrder initialOrder)
                    && initialOrder.IsActive
                    && !success.Rescuer.IsDead
                    && string.Equals(
                        initialOrder.rescuerId,
                        CharacterPersistentIdentity.Require(success.Rescuer).Value,
                        StringComparison.Ordinal)
                    && success.Runtime.TryGetPatient(initialOrder, out _),
                "medical success fixture is not a valid reserved production entry");
            float progress = success.Runtime.AdvanceStabilization(
                "medical:1", success.Rescuer, 1f);
            GameplayOutcomeLedgerSaveData successLedger =
                success.Ledger.CaptureGameplayOutcomes();
            bool committed = progress == 1f
                && success.Mutation.State.anatomyNodes[0].bleedingPerSecond == 0f
                && success.Mutation.CompletedCount == 1
                && success.Runtime.TryGetOrder("medical:1", out CharacterMedicalOrder order)
                && order.stabilized
                && order.nextTerminalOutcomeSequence == 2
                && success.Runtime.Capture().orders.Single()
                    .nextTerminalOutcomeSequence == 2
                && success.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(10), OutcomeFilter.All).Items.Count == 1;
            if (!committed)
            {
                throw new InvalidOperationException(
                    "medical stabilization terminal mismatch: progress=" + progress
                    + ", mutationCompletions=" + success.Mutation.CompletedCount
                    + ", exact=" + successLedger.exactOutcomes.Count
                    + ", outbox=" + successLedger.outbox.Count
                    + ", reserved=" + success.Ledger.GetDiagnostics().ReservedCount
                    + ", lastWrite=" + success.Recorder.LastWriteCode
                    + ":" + success.Recorder.LastWriteDetail
                    + ".");
            }
            GameplayOutcomeSnapshot exact = success.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10), OutcomeFilter.All)
                .Items[0].Exact;
            Require(exact != null
                && exact.facts.Any(fact =>
                    fact.factId == CharacterMedicalOutcomeIds.Procedure.Value
                    && fact.value == "stabilization")
                && exact.facts.Any(fact =>
                    fact.factId == CharacterMedicalOutcomeIds.ClinicalResult.Value
                    && fact.value == "bleeding-stabilized")
                && exact.facts.Any(fact =>
                    fact.factId == CharacterMedicalOutcomeIds.AnatomyTransition.Value
                    && fact.value.Contains("torso=20/40:1:0 -> torso=20/40:0:0",
                        StringComparison.Ordinal))
                && exact.metrics.Any(metric =>
                    metric.metricId == CharacterMedicalOutcomeIds.AnatomyBleedingBefore.Value
                    && metric.value == 1d)
                && exact.metrics.Any(metric =>
                    metric.metricId == CharacterMedicalOutcomeIds.AnatomyBleedingAfter.Value
                    && metric.value == 0d),
                "medical receipt lost actual before/after anatomy or procedure fields");
        }

        using (MedicalOutcomeFixture rejected = new("rejected"))
        {
            rejected.Recorder.RejectReserve = true;
            float progress = rejected.Runtime.AdvanceStabilization(
                "medical:1", rejected.Rescuer, 1f);
            Require(progress == 0f
                && rejected.Mutation.State.anatomyNodes[0].bleedingPerSecond == 1f
                && rejected.Mutation.CompletedCount == 0
                && rejected.Runtime.TryGetOrder("medical:1", out CharacterMedicalOrder order)
                && !order.stabilized
                && order.completedStabilizationWork == 0f
                && order.nextTerminalOutcomeSequence == 1
                && rejected.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(10), OutcomeFilter.All).Items.Count == 0,
                "medical outcome prepare rejection mutated gameplay state");
        }

        using (MedicalOutcomeFixture rejectedWrite = new("rejected-write"))
        {
            rejectedWrite.Recorder.RejectWrite = true;
            rejectedWrite.Runtime.AdvanceStabilization(
                "medical:1", rejectedWrite.Rescuer, 1f);
            Require(rejectedWrite.Mutation.State.anatomyNodes[0]
                    .bleedingPerSecond == 1f
                && rejectedWrite.Mutation.CompletedCount == 0
                && rejectedWrite.Ledger.GetDiagnostics().ReservedCount == 0
                && rejectedWrite.Runtime.TryGetOrder(
                    "medical:1", out CharacterMedicalOrder order)
                && !order.stabilized
                && order.nextTerminalOutcomeSequence == 1,
                "medical receipt write rejection did not roll back staged health");
        }

        using (MedicalOutcomeFixture smallTreatment = new(
                   "small-treatment", treatment: true))
        {
            float progress = smallTreatment.Runtime.AdvanceTreatment(
                "medical:1", smallTreatment.Rescuer, 0.01f);
            GameplayOutcomeSnapshot exact = smallTreatment.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10), OutcomeFilter.All)
                .Items.Single().Exact;
            Require(progress == 1f
                && smallTreatment.Mutation.State.currentHealth > 40f
                && smallTreatment.Mutation.CompletedCount == 1
                && exact.facts.Any(fact =>
                    fact.factId == CharacterMedicalOutcomeIds.Procedure.Value
                    && fact.value == "treatment")
                && exact.facts.Any(fact =>
                    fact.factId == CharacterMedicalOutcomeIds.ClinicalResult.Value
                    && fact.value == "additional-treatment-required")
                && exact.metrics.Any(metric =>
                    metric.metricId == CharacterMedicalOutcomeIds.CompletedWork.Value
                    && metric.value > 0d),
                "sub-threshold medical treatment did not create its own partial clinical result");
        }

        using (MedicalOutcomeFixture pending = new("pending"))
        {
            pending.Recorder.FaultDelivery = true;
            float progress = pending.Runtime.AdvanceStabilization(
                "medical:1", pending.Rescuer, 1f);
            GameplayOutcomeLedgerSaveData checkpoint =
                pending.Ledger.CaptureGameplayOutcomes();
            Require(progress == 1f
                && pending.Mutation.CompletedCount == 1
                && checkpoint.outbox.Count == 1
                && checkpoint.exactOutcomes.Count == 0,
                "medical delivery fault did not persist a pending ledger row");
            GameplayOutcomeLedger restored = new(
                pending.Registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 8, largePageCount: 0,
                    knownResultKeyCapacity: 128),
                new GameplayOutcomeRunId("run:medical-restored"),
                1L);
            GameplayOutcomeLedgerRestoreCandidate candidate =
                restored.PrepareGameplayOutcomeRestore(checkpoint);
            restored.BeginRestoreCandidate();
            restored.PublishGameplayOutcomeRestore(candidate);
            restored.PublishRestoreCandidate();
            restored.CompleteRestoreCandidate();
            new GameplayOutcomeRecorder(
                restored, pending.Registry, new GameEventBus())
                .RetryPendingDeliveries(8);
            Require(restored.GetGlobal(
                OutcomeCursor.FirstPage(10), OutcomeFilter.All).Items.Count == 1,
                "medical pending outcome did not survive ledger restore/retry");
            pending.Recorder.FaultDelivery = false;
            pending.Recorder.RetryPendingDeliveries(8);
            pending.Runtime.AdvanceStabilization(
                "medical:1", pending.Rescuer, 1f);
            Require(pending.Mutation.CompletedCount == 1
                && pending.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(10), OutcomeFilter.All).Items.Count == 1,
                "medical delivery retry duplicated a terminal procedure");
        }

        using (MedicalOutcomeFixture interrupted = new("interrupted"))
        {
            Require(interrupted.Runtime.TryGetOrder(
                    "medical:1", out CharacterMedicalOrder order),
                "medical interruption fixture is missing its order");
            order.requiredStabilizationWork = 2f;
            float progress = interrupted.Runtime.AdvanceStabilization(
                "medical:1", interrupted.Rescuer, 1f);
            DungeonCharacterMedicalSaveData checkpoint =
                interrupted.Runtime.Capture();
            string json = JsonUtility.ToJson(checkpoint);
            DungeonCharacterMedicalSaveData roundTrip =
                JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(json);
            interrupted.Runtime.ValidatePayload(roundTrip);
            Require(progress > 0f && progress < 1f
                && checkpoint.orders.Single()
                    .currentReservationStabilizationWork == 1f
                && checkpoint.orders.Single().stabilizationLaborSequence == 1
                && interrupted.Labor.Contributions.Count == 1
                && interrupted.Labor.Contributions[0].MilliWu == 1_000L,
                "partial rescue labor was not persisted and accounted exactly");
            Require(interrupted.Runtime.TryReleaseReservation(
                    "medical:1",
                    interrupted.Rescuer,
                    CharacterMedicalStatusCode.StabilizationInterrupted,
                    out DomainFailure releaseFailure)
                && !releaseFailure.IsFailure,
                "partial rescue reservation release failed");
            Require(interrupted.Checkpoints.Count == 1
                && interrupted.Checkpoints[0].WorkId
                    == BuiltInWorkTypeIds.Treat.Value
                && interrupted.Checkpoints[0].ProductId.StartsWith(
                    "checkpoint:medical-work-suspended:",
                    StringComparison.Ordinal)
                && interrupted.Checkpoints[0].ContributionWorkUnits == 1f
                && interrupted.Checkpoints[0].TargetEntityId
                    == order.patientId
                && interrupted.Runtime.Capture().orders.Single()
                    .currentReservationStabilizationWork == 0f
                && interrupted.Runtime.Capture().orders.Single()
                    .nextLaborCheckpointSequence == 2,
                "partial rescue release did not publish one resumable labor checkpoint");
        }
    }

    private static void VerifyMedicalProcedureFactRecovery(bool loseAfterPublication)
    {
        string suffix = loseAfterPublication ? "unknown-after" : "unknown-before";
        using var original = new MedicalOutcomeFixture(suffix);
        original.Publication.HideReply = true;
        original.Publication.CommitBeforeHiding = loseAfterPublication;
        Require(original.Runtime.AdvanceStabilization("medical:1", original.Rescuer, 1f) == 1f,
            "Unknown medical publication response undid the performed procedure.");
        var saved = original.Runtime.Capture();
        var originalLedger = original.Ledger.CaptureGameplayOutcomes();
        Require(saved.pendingProcedureFacts.Count == 1 && saved.orders.Single().stabilized
            && saved.orders.Single().nextTerminalOutcomeSequence == 2 && original.Mutation.CompletedCount == 1
            && original.Facts.ActiveLeaseCount == 0,
            "Unknown medical response lost its fact, completion, or reservation cleanup.");
        string json = JsonUtility.ToJson(saved);
        CharacterMedicalSaveSection.ValidateProcedureFactJson(json);
        var raw = Newtonsoft.Json.Linq.JObject.Parse(json);
        ((Newtonsoft.Json.Linq.JObject)raw["pendingProcedureFacts"][0]["fact"]["before"]).Remove("bloodLoss");
        bool rejected = false;
        try { CharacterMedicalSaveSection.ValidateProcedureFactJson(raw.ToString()); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Medical current save accepted missing clinical evidence.");
        CheckJoin(saved, originalLedger, true);
        var tampered = JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(json);
        tampered.pendingProcedureFacts[0].publicationKey += ":wrong";
        CheckJoin(tampered, originalLedger, false);
        tampered = JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(json);
        tampered.orders[0].nextTerminalOutcomeSequence = 1;
        CheckJoin(tampered, originalLedger, false);
        if (loseAfterPublication)
        {
            tampered = JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(json);
            tampered.pendingProcedureFacts[0].fact.before.health++;
            CheckJoin(tampered, originalLedger, false);
        }
        original.Runtime.AdvanceStabilization("medical:1", original.Rescuer, 1f);
        Require(original.Mutation.CompletedCount == 1, "Pending procedure repeated its effect.");

        // Fresh domain owner + actual aggregate DTO/ledger restore, not a whole-world/UI claim.
        using var restored = new MedicalOutcomeFixture(suffix);
        restored.LoadProcedureCheckpoint(JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(json), originalLedger);
        string frozen = JsonUtility.ToJson(restored.Runtime.Capture().pendingProcedureFacts[0].fact);
        restored.Rescuer.name = "나중에 바뀐 이름";
        restored.Mutation.State.currentHealth = 17f;
        restored.Runtime.RecoverPendingProcedureFacts();
        restored.Runtime.RecoverPendingProcedureFacts();
        restored.Runtime.AdvanceStabilization("medical:1", restored.Rescuer, 1f);
        Require(restored.Runtime.Capture().pendingProcedureFacts.Count == 0
            && restored.Mutation.State.currentHealth == 17f && restored.Mutation.CompletedCount == 0
            && restored.Runtime.Capture().orders.Single().nextTerminalOutcomeSequence == 2,
            "Recovered observation repeated medical effects or changed its domain sequence.");
        var rows = restored.Ledger.CaptureGameplayOutcomes();
        Require(rows.exactOutcomes.Count == 1 && rows.outbox.Count == 0
            && frozen == JsonUtility.ToJson(saved.pendingProcedureFacts[0].fact),
            "Medical saved fact was regenerated or delivered twice.");
        CheckJoin(saved, rows, true);
        var acknowledged = restored.Runtime.Capture();
        acknowledged.orders[0].nextTerminalOutcomeSequence = 1;
        CheckJoin(acknowledged, rows, false);
        // A completed order may retire before its observation is acknowledged.
        saved.orders.Clear();
        CheckJoin(saved, rows, true);

        static void CheckJoin(DungeonCharacterMedicalSaveData data, GameplayOutcomeLedgerSaveData ledger, bool expected)
        {
            var report = new DungeonGameRestoreReport();
            CharacterMedicalFactPreflight.Validate(data, ledger, report);
            Require(report.Success == expected, "Unexpected medical canonical fact join result.");
        }
    }

    private sealed class MedicalReplyFaults : IDomainFactPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal MedicalReplyFaults(DomainFactDispatcher inner) => this.inner = inner;
        internal bool HideReply;
        internal bool CommitBeforeHiding;
        public bool TryReserve<T>(in T proposed, out DomainFactLease lease, out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T actual)
        {
            if (!HideReply) return inner.Publish(lease, actual);
            if (CommitBeforeHiding) inner.Publish(lease, actual);
            else inner.Cancel(lease);
            return DomainFactDelivery.Uncertain("injected-medical-reply-loss");
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class MedicalOutcomeFixture : IDisposable
    {
        private readonly GameObject patientObject;
        private readonly GameObject rescuerObject;
        private readonly GameObject facilityObject;
        private readonly BuildingSO facilityDefinition;
        private readonly IDisposable checkpointSubscription;
        private readonly DungeonRuntimeAggregateRootStore roots;
        private readonly IDisposable factObservationLifetime;

        internal MedicalOutcomeFixture(string suffix, bool treatment = false)
        {
            patientObject = new GameObject("환자");
            rescuerObject = new GameObject("구조자");
            CharacterActor patient = patientObject.AddComponent<CharacterActor>();
            Rescuer = rescuerObject.AddComponent<CharacterActor>();
            patient.EnsureRuntimeState();
            Rescuer.EnsureRuntimeState();
            patient.Identity.SetPersistentId("character:medical-patient-" + suffix);
            Rescuer.Identity.SetPersistentId("character:medical-rescuer-" + suffix);
            patient.SetLifecycleState(CharacterLifecycleState.Downed);
            RuntimeWorldRegistry world = new(patient);
            world.RegisterCharacter(Rescuer);
            string facilityId = string.Empty;
            if (treatment)
            {
                facilityObject = new GameObject("의료 시설");
                BuildableObject facility =
                    facilityObject.AddComponent<BuildableObject>();
                facilityDefinition = ScriptableObject.CreateInstance<BuildingSO>();
                facilityDefinition.AbilityModules.Add(new BuildingMedicalAbility
                {
                    requiresMedicine = false,
                    severityReduction = 0.05f
                });
                PropertyInfo dataProperty = typeof(BuildableObject).GetProperty(
                    nameof(BuildableObject.BuildingData),
                    BindingFlags.Instance | BindingFlags.Public);
                if (dataProperty == null)
                    throw new InvalidOperationException(
                        "Medical test facility BuildingData property is missing.");
                dataProperty.SetValue(facility, facilityDefinition);
                facilityId = "building:medical-facility-" + suffix;
                facility.RestorePersistentIdentity(
                    new BuildingInstanceId(facilityId));
                world.RegisterBuilding(facility);
            }
            roots = new DungeonRuntimeAggregateRootStore();
            CharacterMedicalAggregateState aggregate = new();
            aggregate.Orders.Add(new CharacterMedicalOrder
            {
                orderId = "medical:1",
                patientId = CharacterPersistentIdentity.Require(patient).Value,
                rescuerId = CharacterPersistentIdentity.Require(Rescuer).Value,
                state = treatment
                    ? CharacterMedicalOrderState.Treating
                    : CharacterMedicalOrderState.AwaitingStabilization,
                statusCode = treatment
                    ? CharacterMedicalStatusCode.Treating
                    : CharacterMedicalStatusCode.AwaitingStabilization,
                stabilized = treatment,
                treatmentFacilityId = facilityId,
                requiredStabilizationWork = 1f,
                requiredTreatmentWork = treatment ? 0.01f : 0f
            });
            aggregate.OrderSequence = 1;
            roots.Replace(aggregate);
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CharacterMedicalOutcomeDescriptor(new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CharacterMedicalOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 8, largePageCount: 0,
                    knownResultKeyCapacity: 128),
                new GameplayOutcomeRunId("run:medical-" + suffix),
                1L);
            GameEventBus eventBus = new();
            checkpointSubscription = eventBus.Subscribe<WorkCompletedIdentityEvent>(
                value => Checkpoints.Add(value));
            Recorder = new MedicalFaultingRecorder(
                new GameplayOutcomeRecorder(Ledger, Registry, eventBus));
            Facts = new DomainFactDispatcher();
            factObservationLifetime = Facts.Subscribe(
                "log:character.medical-procedure",
                new GameplayDomainFactObserver<CharacterMedicalProcedureFact,
                    CharacterMedicalOutcomeReceipt>(
                    CharacterMedicalFactObservation.Capture,
                    Registry,
                    Ledger,
                    Recorder,
                    new GameplayDomainFactCommitGroup(Recorder, Ledger),
                    CharacterMedicalFactObservation.Reserve));
            Mutation = new MedicalTestMutation(
                CharacterPersistentIdentity.Require(patient));
            Publication = new MedicalReplyFaults(Facts);
            Runtime = new CharacterMedicalRuntime(
                new FixedBodyHealthQuery(true),
                new CharacterMedicalWorldServices(
                    new UnavailableGridProvider(),
                    world,
                    CreateDefaultProxy<IWorldItemStackRuntime>()),
                eventBus,
                new FixedCarePriorityQuery(),
                EmptyResourceEconomyContentCatalog.Instance,
                new ResourceItemDefinitionCatalog(
                    Array.Empty<ItemDefinitionSO>()),
                roots,
                new FixedMedicalPerformanceQuery(),
                CreateDefaultProxy<IPhysicalFacilityItemSinkGateway>(),
                new RecordingTareDisposition(),
                new AlwaysValidDestinationRuntime(),
                new RecordingOwnerDrain(new List<string>(), true),
                new EmptyDrainRestoreCandidateQuery(),
                Mutation,
                Publication,
                Facts,
                new MedicalFixedCalendar(),
                Labor);
        }

        internal CharacterActor Rescuer { get; }
        internal CharacterMedicalRuntime Runtime { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal MedicalFaultingRecorder Recorder { get; }
        internal DomainFactDispatcher Facts { get; }
        internal MedicalReplyFaults Publication { get; }
        internal MedicalTestMutation Mutation { get; }
        internal RecordingLaborAccounting Labor { get; } = new();
        internal List<WorkCompletedIdentityEvent> Checkpoints { get; } = new();

        internal void LoadProcedureCheckpoint(DungeonCharacterMedicalSaveData data, GameplayOutcomeLedgerSaveData ledger)
        {
            Runtime.ValidatePayload(data);
            roots.Replace(CharacterMedicalSaveValidation.CreateState(data));
            var candidate = Ledger.PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate(); Ledger.PublishGameplayOutcomeRestore(candidate);
            Ledger.PublishRestoreCandidate(); Ledger.CompleteRestoreCandidate();
        }

        public void Dispose()
        {
            factObservationLifetime?.Dispose();
            Facts?.Dispose();
            checkpointSubscription?.Dispose();
            UnityEngine.Object.DestroyImmediate(patientObject);
            UnityEngine.Object.DestroyImmediate(rescuerObject);
            if (facilityObject != null)
                UnityEngine.Object.DestroyImmediate(facilityObject);
            if (facilityDefinition != null)
                UnityEngine.Object.DestroyImmediate(facilityDefinition);
        }
    }

    private sealed class RecordingLaborAccounting :
        ISettlementLaborAccountingService
    {
        internal List<SettlementLaborContribution> Contributions { get; } =
            new();

        public EmergencyAccountingResult Record(
            SettlementLaborContribution contribution)
        {
            Contributions.Add(contribution);
            return EmergencyAccountingResult.Ok();
        }

        public SettlementLaborAccountingSnapshot Capture() => default;
    }

    private sealed class FixedMedicalPerformanceQuery :
        ICharacterPerformanceQuery
    {
        public CharacterFunctionalCapacitySnapshot GetFunctionalCapacities(
            CharacterActor actor) => default;
        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            float contextFactor = 1f,
            GameplayEffectContext effectContext = null) => new();
        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            CharacterPerformanceEvaluationContext context) => new();
        public CharacterPerformanceSnapshot EvaluateWork(
            CharacterActor actor,
            WorkTypeId workTypeId,
            CharacterPerformanceResultChannel resultChannel,
            CharacterPerformanceEvaluationContext context) => new();
        public IReadOnlyList<CharacterPerformanceSnapshot> EvaluateDomain(
            CharacterActor actor,
            CharacterPerformanceFormulaDomain domain) =>
            Array.Empty<CharacterPerformanceSnapshot>();
    }

    private sealed class MedicalFixedCalendar : IGameCalendar
    {
        public int Day => 1;
        public int Hour => 0;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => default;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int day, int hour) =>
            throw new NotSupportedException();
    }

    private sealed class MedicalTestMutation :
        ICharacterBodyHealthMutationTransaction
    {
        private readonly CharacterId patientId;
        internal MedicalTestMutation(CharacterId patientId)
        {
            this.patientId = patientId;
            State = new CharacterBodyHealthState
            {
                characterId = patientId.Value,
                maxHealth = 100f,
                currentHealth = 40f,
                bloodLoss = 10f,
                downed = true,
                parts = new List<CharacterBodyPartHealthState>
                {
                    new()
                    {
                        bodyPart = CombatBodyPart.Torso,
                        maxHealth = 40f,
                        currentHealth = 20f,
                        bleedingPerSecond = 1f
                    }
                },
                anatomyNodes = new List<AnatomyNodeHealthState>
                {
                    new()
                    {
                        nodeId = "torso",
                        maxHealth = 40f,
                        currentHealth = 20f,
                        bleedingPerSecond = 1f
                    }
                }
            };
        }

        internal CharacterBodyHealthState State { get; private set; }
        internal int CompletedCount { get; private set; }
        public CharacterBodyHealthMutationSnapshot CaptureCombatMutation(
            CharacterActor actor) => new(patientId, Clone(State));
        public void RestoreCombatMutation(
            CharacterActor actor,
            in CharacterBodyHealthMutationSnapshot snapshot,
            string reason) => State = Clone(snapshot.State);
        public void ApplyPreparedMedicalStabilization(CharacterActor actor)
        {
            State.parts[0].bleedingPerSecond = 0f;
            State.anatomyNodes[0].bleedingPerSecond = 0f;
        }
        public void ApplyPreparedMedicalTreatment(
            CharacterActor actor, float partHealthAmount,
            float bloodLossReduction)
        {
            float restored = Math.Min(
                partHealthAmount,
                State.parts[0].maxHealth - State.parts[0].currentHealth);
            State.parts[0].currentHealth += restored;
            State.anatomyNodes[0].currentHealth += restored;
            State.currentHealth += restored;
            State.bloodLoss = Math.Max(0f, State.bloodLoss - bloodLossReduction);
        }
        public void CompletePreparedMedicalMutation(
            CharacterActor actor,
            in CharacterBodyHealthMutationSnapshot before) => CompletedCount++;
        public void ApplyPreparedCombatMutation(
            CharacterActor actor, CombatAttackResult result, string reason) =>
            throw new NotSupportedException();
        public void CompletePreparedCombatMutation(
            CharacterActor actor,
            in CharacterBodyHealthMutationSnapshot before,
            CombatAttackResult result,
            string reason) => throw new NotSupportedException();
        public CharacterPreparedAggregateDamageReceipt ApplyPreparedAggregateDamage(
            CharacterActor actor, float amount,
            CharacterDeathCauseCode deathCause,
            string reasonCode, bool allowDeath) => throw new NotSupportedException();
        public void CompletePreparedAggregateDamage(
            CharacterActor actor,
            in CharacterPreparedAggregateDamageReceipt receipt) =>
            throw new NotSupportedException();
        public void ApplyPreparedSuppressionReduction(
            CharacterActor actor, float amount) => throw new NotSupportedException();
        public void CompletePreparedSuppressionReduction(
            CharacterActor actor,
            in CharacterBodyHealthMutationSnapshot before) =>
            throw new NotSupportedException();

        private static CharacterBodyHealthState Clone(CharacterBodyHealthState source) => new()
        {
            characterId = source.characterId,
            maxHealth = source.maxHealth,
            currentHealth = source.currentHealth,
            bloodLoss = source.bloodLoss,
            downed = source.downed,
            parts = new List<CharacterBodyPartHealthState>
            {
                new()
                {
                    bodyPart = source.parts[0].bodyPart,
                    maxHealth = source.parts[0].maxHealth,
                    currentHealth = source.parts[0].currentHealth,
                    bleedingPerSecond = source.parts[0].bleedingPerSecond
                }
            },
            anatomyNodes = new List<AnatomyNodeHealthState>
            {
                new()
                {
                    nodeId = source.anatomyNodes[0].nodeId,
                    maxHealth = source.anatomyNodes[0].maxHealth,
                    currentHealth = source.anatomyNodes[0].currentHealth,
                    bleedingPerSecond = source.anatomyNodes[0].bleedingPerSecond
                }
            }
        };
    }

    private sealed class MedicalFaultingRecorder : IGameplayOutcomeRecorder
    {
        private readonly IGameplayOutcomeRecorder inner;
        internal MedicalFaultingRecorder(IGameplayOutcomeRecorder inner) =>
            this.inner = inner;
        internal bool RejectReserve { get; set; }
        internal bool RejectWrite { get; set; }
        internal bool FaultDelivery { get; set; }
        internal OutcomePrepareCode LastWriteCode { get; private set; }
        internal string LastWriteDetail { get; private set; } = string.Empty;
        public OutcomePrepareResult TryPrepare<TReceipt>(
            in TReceipt receipt, out PreparedOutcomeToken prepared) =>
            inner.TryPrepare(receipt, out prepared);
        public OutcomePrepareResult TryReserve(
            in OutcomeWriteRequirements requirements,
            out PreparedOutcomeReservation reservation)
        {
            if (RejectReserve)
            {
                reservation = default;
                return new OutcomePrepareResult(
                    OutcomePrepareCode.CapacityDeferred,
                    "injected-medical-prepare-rejection");
            }
            return inner.TryReserve(requirements, out reservation);
        }
        public OutcomePrepareResult TryWriteReserved<TReceipt>(
            in TReceipt receipt,
            in PreparedOutcomeReservation reservation,
            out PreparedOutcomeToken prepared)
        {
            if (RejectWrite)
            {
                prepared = default;
                OutcomePrepareResult rejected = new(
                    OutcomePrepareCode.AdapterWriteFailed,
                    "injected-medical-write-rejection");
                LastWriteCode = rejected.Code;
                LastWriteDetail = rejected.DetailCode;
                return rejected;
            }
            OutcomePrepareResult result = inner.TryWriteReserved(
                receipt, reservation, out prepared);
            LastWriteCode = result.Code;
            LastWriteDetail = result.DetailCode;
            return result;
        }
        public void CancelReservation(
            in PreparedOutcomeReservation reservation) =>
            inner.CancelReservation(reservation);
        public void CancelPrepared(in PreparedOutcomeToken prepared) =>
            inner.CancelPrepared(prepared);
        public OutcomeCommitResult CommitPrepared(
            in PreparedOutcomeToken prepared,
            long expectedOwnerRevision,
            out CommittedOutcomeToken committed) =>
            inner.CommitPrepared(prepared, expectedOwnerRevision, out committed);
        public OutcomeCommitResult CommitPreparedBatch(
            PreparedOutcomeToken[] prepared,
            long[] expectedOwnerRevisions,
            CommittedOutcomeToken[] committed) =>
            inner.CommitPreparedBatch(prepared, expectedOwnerRevisions, committed);
        public OutcomeDeliveryResult TryDeliver(in CommittedOutcomeToken committed) =>
            FaultDelivery
                ? new OutcomeDeliveryResult(
                    OutcomeDeliveryCode.PendingDeliveryFault,
                    committed.OutcomeId,
                    "injected-medical-delivery-fault")
                : inner.TryDeliver(committed);
        public OutcomeAcknowledgeResult Acknowledge(
            in CommittedOutcomeToken committed) =>
            inner.Acknowledge(committed);
        public int RetryPendingDeliveries(int maximumCount) =>
            inner.RetryPendingDeliveries(maximumCount);
    }
}
#endif
