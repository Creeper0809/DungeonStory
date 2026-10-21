#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RegularCustomerCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:regular-customer-progression";
    internal const string FirstVisitActionId =
        "action:service-regular-customer-first-visit";
    internal const string CandidateVisitActionId =
        "action:service-regular-customer-candidate-visit";
    internal const string RecruitActionId =
        "action:service-regular-customer-recruit";

    private const string OwnerKind =
        "owner-kind:regular-customer-progression";
    private const string DomainStateSchema =
        "dungeonstory-causal-regular-customer-owner-state@1";
    private const string CustomerId =
        "character:world:causal:regular-customer";
    private const string CustomerName = "연화";
    private const string SpeciesTag = "Slime";
    private const int AbsoluteDay = 20;
    private const int CharacterDefinitionId = 9911;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException("A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A regular-customer checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            3,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                FirstVisitActionId,
                "service",
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.RegularCustomerVisitResult)
                    .OutcomeTypeId,
                context.CaptureState,
                context.RecordFirstVisit,
                context.RequireFirstVisitOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                CandidateVisitActionId,
                "service",
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.RegularCustomerVisitResult)
                    .OutcomeTypeId,
                context.CaptureState,
                context.RecordCandidateVisit,
                context.RequireCandidateVisitOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                RecruitActionId,
                "service",
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.RegularCustomerRecruitResult)
                    .OutcomeTypeId,
                context.CaptureState,
                context.Recruit,
                context.RequireRecruitOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerIdValue =
            "regular-customer-owner:causal-progression:focused";
        JObject firstVisitCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-regular-customer:first",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstVisitActionId);
            IReadOnlyDictionary<string, object> before = first.CaptureState();
            Require((int)before["regularCustomer.stage"] == 0
                    && (int)before["regularCustomer.visitCount"] == 0
                    && (int)before["regularCustomer.exactOutcomeCount"] == 0,
                "Regular-customer owner did not start empty.");
            Require(first.Execute(), "The real first customer visit did not complete.");
            Require(first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The first customer-visit outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require((int)after["regularCustomer.stage"] == 1
                    && (int)after["regularCustomer.visitCount"] == 1
                    && !(bool)after["regularCustomer.isRegular"]
                    && !(bool)after["regularCustomer.isRecruitCandidate"]
                    && (int)after["regularCustomer.exactOutcomeCount"] == 1,
                "First visit did not retain its real customer state.");
            firstVisitCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-regular-customer:recruit",
                   OwnerIdValue,
                   firstVisitCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstVisitActionId);
            IProductionCausalActionFixture visit = fixtures.Get(
                CandidateVisitActionId);
            IProductionCausalActionFixture recruit = fixtures.Get(RecruitActionId);
            IReadOnlyDictionary<string, object> restored = visit.CaptureState();
            Require((int)restored["regularCustomer.stage"] == 1
                    && !first.Execute(),
                "Fresh composition restore lost or duplicated the first visit.");
            Require(visit.Execute(),
                "The restored customer did not complete the candidate visit.");
            Require(visit.RequireExactOutcome().HasCanonicalPayloadHash,
                "The candidate-visit outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> candidate = visit.CaptureState();
            Require((int)candidate["regularCustomer.stage"] == 2
                    && (int)candidate["regularCustomer.visitCount"] == 2
                    && (bool)candidate["regularCustomer.isRegular"]
                    && (bool)candidate["regularCustomer.isRecruitCandidate"]
                    && !(bool)candidate["regularCustomer.isRecruited"],
                "Second visit did not promote the real recruit candidate.");
            Require(recruit.Execute(),
                "The visit-qualified regular customer was not recruited.");
            Require(recruit.RequireExactOutcome().HasCanonicalPayloadHash,
                "The regular-customer recruit outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = recruit.CaptureState();
            Require((int)after["regularCustomer.stage"] == 3
                    && (bool)after["regularCustomer.isRecruited"]
                    && (int)after["regularCustomer.recruitedAbsoluteDay"]
                        == AbsoluteDay
                    && string.Equals(
                        (string)after["regularCustomer.actorType"],
                        CharacterType.NPC.ToString(),
                        StringComparison.Ordinal)
                    && (bool)after["regularCustomer.deliveryApplied"]
                    && string.Equals(
                        (string)after["regularCustomer.deliveryActorId"],
                        CustomerId,
                        StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(
                        (string)after["regularCustomer.deliveryResultKey"])
                    && (int)after["regularCustomer.deliveryOutcomeCount"] == 1
                    && (int)after["regularCustomer.exactOutcomeCount"] == 4,
                "Recruitment did not preserve both visits and activate the customer.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)recruit)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-regular-customer:terminal",
                   OwnerIdValue,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstVisitActionId);
            IProductionCausalActionFixture visit = fixtures.Get(
                CandidateVisitActionId);
            IProductionCausalActionFixture recruit = fixtures.Get(RecruitActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            Require(!first.Execute() && !visit.Execute() && !recruit.Execute(),
                "Terminal regular-customer owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)recruit)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected regular-customer replay mutated the owner checkpoint.");
        }

        JObject visitTamper = (JObject)firstVisitCheckpoint.DeepClone();
        ((JObject)visitTamper["domainState"])["regularCustomers"]["records"]![0]![
            "visitCount"] = 2;
        Require(RejectCheckpoint(OwnerIdValue, visitTamper, "visit-tamper"),
            "A regular-customer checkpoint with changed visits was accepted.");

        JObject identityTamper = (JObject)firstVisitCheckpoint.DeepClone();
        identityTamper["ownerId"] =
            "regular-customer-owner:causal-progression:tampered";
        Require(RejectCheckpoint(OwnerIdValue, identityTamper, "identity-tamper"),
            "A regular-customer checkpoint with changed owner identity was accepted.");

        if (log)
        {
            Debug.Log(
                "Regular customer first visit -> restored candidate visit -> recruit causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Regular-customer causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-regular-customer:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly string ledgerRunId;
        private readonly GameObject root;
        private readonly GameObject actorObject;
        private readonly GameObject facilityObject;
        private readonly CharacterSO characterData;
        private readonly BuildingSO facilityData;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly RegularCustomerFactEditorFixture facts;
        private readonly RegularCustomerRuntime runtime;
        private readonly CharacterActor actor;
        private readonly BuildableObject facility;
        private readonly RegularCustomerSaveSection saveSection;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":regular-customer-ledger";
            var aggregateRootStore = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ledgerRunId,
                aggregateRootStore);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            facts = new RegularCustomerFactEditorFixture(outcomes, probe);

            root = new GameObject("Causal regular customer owner");
            runtime = root.AddComponent<RegularCustomerRuntime>();
            runtime.ConstructRecruitmentRuntime(
                new DungeonStory.Foundation.UnityUiClock(),
                new RecruitActivation(),
                new GameEventBus(),
                new FixedGameSessionStateProvider(AbsoluteDay),
                facts.Publisher,
                facts.Recovery);

            actorObject = new GameObject("Causal regular customer actor");
            actorObject.AddComponent<SpriteRenderer>();
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(actorObject);
            actor = actorObject.AddComponent<CharacterActor>();
            characterData =
                CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                    CharacterType.Customer,
                    CustomerName,
                    SpeciesTag);
            characterData.id = CharacterDefinitionId;
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Initialization(characterData);
            actor.characterType = CharacterType.Customer;
            actor.Identity.SetPersistentId(CustomerId);
            actor.stats ??= new Dictionary<CharacterCondition, float>();
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 90f;

            facilityObject = new GameObject("Causal regular customer facility");
            facility = facilityObject.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            facilityData = ScriptableObject.CreateInstance<BuildingSO>();
            facilityData.id = 9912;
            facilityData.objectName = "달빛 식당";
            facilityData.width = 1;
            facilityData.height = 1;
            facilityData.category = BuildingCategory.Shop;
            facilityData.Facility = new FacilityData
            {
                roles = FacilityRole.Meal,
                capacity = 1
            };
            facilityData.Facility.SetSupportedWorkTypeIds(
                new[] { BuiltInWorkTypeIds.Operate });
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(facilityData, Vector2Int.zero);

            var persistence = new RegularCustomerPersistenceAdapter(
                runtime,
                new FixedRunCharacterCatalog(characterData));
            saveSection = new RegularCustomerSaveSection(
                persistence,
                persistence);
            try
            {
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                probe.ClearObservations();
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner
            ? OwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            RegularCustomerRecord record = Record;
            return new Dictionary<string, object>
            {
                ["regularCustomer.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "regular-customer-owner:ephemeral",
                ["regularCustomer.stage"] = Stage,
                ["regularCustomer.absoluteDay"] = AbsoluteDay,
                ["regularCustomer.customerId"] = CustomerId,
                ["regularCustomer.displayName"] = CustomerName,
                ["regularCustomer.visitCount"] = record?.VisitCount ?? 0,
                ["regularCustomer.averageSatisfaction"] =
                    record?.AverageSatisfaction ?? 0f,
                ["regularCustomer.status"] =
                    (record?.Status ?? RegularCustomerStatus.Visitor).ToString(),
                ["regularCustomer.isRegular"] = record?.IsRegular ?? false,
                ["regularCustomer.isRecruitCandidate"] =
                    record?.IsRecruitCandidate ?? false,
                ["regularCustomer.isRecruited"] =
                    record?.IsRecruited ?? false,
                ["regularCustomer.recruitedAbsoluteDay"] =
                    record?.RecruitedAbsoluteDay ?? 0,
                ["regularCustomer.actorType"] = actor.characterType.ToString(),
                ["regularCustomer.deliveryPending"] =
                    record?.RecruitDeliveryPending ?? false,
                ["regularCustomer.deliveryApplied"] =
                    record?.RecruitDeliveryApplied ?? false,
                ["regularCustomer.deliveryActorId"] =
                    record?.RecruitDeliveryActorId ?? string.Empty,
                ["regularCustomer.deliveryResultKey"] =
                    record?.RecruitDeliveryOutcomeResultKey ?? string.Empty,
                ["regularCustomer.visitOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.RegularCustomerVisitResult),
                ["regularCustomer.recruitOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.RegularCustomerRecruitResult),
                ["regularCustomer.deliveryOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .RegularCustomerRecruitDeliveryResult),
                ["regularCustomer.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool RecordFirstVisit() => RecordVisitAtStage(0, 1);
        internal bool RecordCandidateVisit() => RecordVisitAtStage(1, 2);

        internal bool Recruit()
        {
            ThrowIfDisposed();
            if (Stage != 2)
                return false;
            bool recruited = runtime.TryRecruit(CustomerId, out _);
            bool valid = recruited && Stage == 3;
            if (valid)
                ValidateState();
            return valid;
        }

        internal GameplayOutcomeReplayIdentity RequireFirstVisitOutcome() =>
            RequireVisitOutcome(1);

        internal GameplayOutcomeReplayIdentity RequireCandidateVisitOutcome() =>
            RequireVisitOutcome(2);

        internal GameplayOutcomeReplayIdentity RequireRecruitOutcome() =>
            RequireOutcome(
                MigratedProducerOutcomeKind.RegularCustomerRecruitResult,
                expectedLedgerSequence: 3,
                expectedSummary:
                    CustomerName + " 영입이 확정되었습니다. 배치=Staff.");

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral regular-customer fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = DomainStateSchema,
                    ["stage"] = Stage,
                    ["customerId"] = CustomerId,
                    ["absoluteDay"] = AbsoluteDay,
                    ["actorType"] = actor.characterType.ToString(),
                    ["regularCustomers"] = JObject.Parse(saveSection.Capture()),
                    ["outcomeOwners"] = JObject.Parse(JsonUtility.ToJson(
                        outcomes.Transaction.Capture()))
                },
                outcomes.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            facts.Dispose();
            if (facilityObject != null)
                Object.DestroyImmediate(facilityObject);
            if (facilityData != null)
                Object.DestroyImmediate(facilityData);
            if (actorObject != null)
                Object.DestroyImmediate(actorObject);
            if (characterData != null)
                Object.DestroyImmediate(characterData);
            if (root != null)
                Object.DestroyImmediate(root);
        }

        private RegularCustomerRecord Record =>
            runtime.State.TryGetRecord(CustomerId, out RegularCustomerRecord record)
                ? record
                : null;

        private int Stage
        {
            get
            {
                RegularCustomerRecord record = Record;
                int visits = OutcomeCount(
                    MigratedProducerOutcomeKind.RegularCustomerVisitResult);
                int recruits = OutcomeCount(
                    MigratedProducerOutcomeKind.RegularCustomerRecruitResult);
                int deliveries = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .RegularCustomerRecruitDeliveryResult);
                if (record == null
                    && visits == 0
                    && recruits == 0
                    && deliveries == 0
                    && actor.characterType == CharacterType.Customer)
                    return 0;
                if (record != null
                    && record.VisitCount == 1
                    && !record.IsRegular
                    && !record.IsRecruitCandidate
                    && !record.IsRecruited
                    && visits == 1
                    && recruits == 0
                    && deliveries == 0
                    && actor.characterType == CharacterType.Customer)
                    return 1;
                if (record != null
                    && record.VisitCount == 2
                    && record.IsRegular
                    && record.IsRecruitCandidate
                    && !record.IsRecruited
                    && visits == 2
                    && recruits == 0
                    && deliveries == 0
                    && actor.characterType == CharacterType.Customer)
                    return 2;
                if (record != null
                    && record.VisitCount == 2
                    && record.IsRegular
                    && record.IsRecruitCandidate
                    && record.IsRecruited
                    && !record.RecruitDeliveryPending
                    && record.RecruitDeliveryApplied
                    && string.Equals(
                        record.RecruitDeliveryActorId,
                        CustomerId,
                        StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(
                        record.RecruitDeliveryOutcomeResultKey)
                    && visits == 2
                    && recruits == 1
                    && deliveries == 1
                    && actor.characterType == CharacterType.NPC)
                    return 3;
                return -1;
            }
        }

        private bool RecordVisitAtStage(int expectedStage, int expectedAfter)
        {
            ThrowIfDisposed();
            if (Stage != expectedStage)
                return false;
            runtime.OnTriggerEvent(
                new FacilityVisitEvent(CharacterActor.From(actor), facility));
            bool valid = Stage == expectedAfter;
            if (valid)
                ValidateState();
            return valid;
        }

        private GameplayOutcomeReplayIdentity RequireVisitOutcome(int sequence) =>
            RequireOutcome(
                MigratedProducerOutcomeKind.RegularCustomerVisitResult,
                expectedLedgerSequence: sequence,
                expectedSummary: CustomerName + "의 방문 " + sequence
                    + "회차가 기록되었습니다. 상태="
                    + (sequence == 1
                        ? RegularCustomerStatus.Visitor
                        : RegularCustomerStatus.RecruitCandidate)
                    + ".");

        private GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind,
            int expectedLedgerSequence,
            string expectedSummary)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    kind,
                    probe);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact.sequence != expectedLedgerSequence
                || exact.absoluteDay != AbsoluteDay
                || exact.status != GameplayOutcomeStatus.Succeeded
                || !string.Equals(
                    exact.producerId,
                    definition.ProducerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                || exact.participants == null
                || exact.participants.Count != 1
                || exact.subjects == null
                || exact.subjects.Count != 1
                || !string.Equals(
                    exact.participants[0].entityKindId,
                    MigratedProducerOutcomeIds.CharacterKind.Value,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.participants[0].entityId,
                    CustomerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.participants[0].roleId,
                    MigratedProducerOutcomeIds.CustomerRole.Value,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.participants[0].displayText,
                    CustomerName,
                    StringComparison.Ordinal)
                || exact.facts == null
                || exact.facts.Count != 2
                || !exact.facts.Any(value =>
                    string.Equals(value.factId, "summary", StringComparison.Ordinal)
                    && string.Equals(value.value, expectedSummary,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Regular-customer production entry did not publish its exact outcome: "
                    + kind);
            }
            return identity;
        }

        private int OutcomeCount(MigratedProducerOutcomeKind kind)
        {
            string type = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        type,
                        StringComparison.Ordinal));
        }

        private int ExactOutcomeCount() => outcomes.Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes.Count;

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                outcomes.Ledger);
            RequireFields(
                state,
                "regular-customer owner state",
                "schemaVersion",
                "schema",
                "stage",
                "customerId",
                "absoluteDay",
                "actorType",
                "regularCustomers",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals((string)state["schema"], DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["customerId"], CustomerId,
                    StringComparison.Ordinal)
                || (int)state["absoluteDay"] != AbsoluteDay
                || !Enum.TryParse(
                    (string)state["actorType"],
                    out CharacterType restoredActorType)
                || state["regularCustomers"] is not JObject customerJson
                || state["outcomeOwners"] is not JObject ownerJson)
            {
                throw new InvalidOperationException(
                    "Regular-customer checkpoint identity changed.");
            }

            DungeonMigratedProducerOutcomeSaveData ownerData =
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    ownerJson.ToString(Formatting.None));
            outcomes.Transaction.Restore(
                outcomes.Transaction.PrepareRestore(ownerData));
            IDungeonSaveRestoreStage staged = saveSection.StageRestore(
                customerJson.ToString(Formatting.None),
                saveSection.SectionVersion,
                new DungeonGameRestoreReport());
            staged.Commit(new DungeonGameRestoreReport());
            actor.characterType = restoredActorType;
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.RefreshAbilityCache();
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Regular-customer checkpoint stage disagrees with its saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            RegularCustomerRecord record = Record;
            bool valid = stage is >= 0 and <= 3
                && ExactOutcomeCount() == stage + (stage == 3 ? 1 : 0)
                && (record?.VisitCount ?? 0) == Math.Min(stage, 2)
                && Math.Abs((record?.AverageSatisfaction ?? 0f)
                    - (stage == 0 ? 0f : 90f)) < 0.0001f;
            if (stage == 0)
            {
                valid &= record == null;
            }
            else
            {
                valid &= record != null
                    && string.Equals(record.CustomerId, CustomerId,
                        StringComparison.Ordinal)
                    && string.Equals(record.DisplayName, CustomerName,
                        StringComparison.Ordinal)
                    && string.Equals(record.SpeciesTag, SpeciesTag,
                        StringComparison.Ordinal)
                    && record.IsRegular == (stage >= 2)
                    && record.IsRecruitCandidate == (stage >= 2)
                    && record.IsRecruited == (stage == 3)
                    && record.RecruitedAbsoluteDay == (stage == 3
                        ? AbsoluteDay
                        : 0)
                    && !record.RecruitDeliveryPending
                    && record.RecruitDeliveryApplied == (stage == 3)
                    && string.Equals(
                        record.RecruitDeliveryActorId,
                        stage == 3 ? CustomerId : string.Empty,
                        StringComparison.Ordinal)
                    && (stage == 3
                        ? !string.IsNullOrWhiteSpace(
                            record.RecruitDeliveryOutcomeResultKey)
                        : string.IsNullOrEmpty(
                            record.RecruitDeliveryOutcomeResultKey));
            }
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Regular-customer state, actor and exact ledger diverged.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures ?? throw new ArgumentNullException(nameof(fixtures));

        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.Single(value => string.Equals(
                value.ActionId,
                actionId,
                StringComparison.Ordinal));

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private sealed class FixedRunCharacterCatalog : IRunCharacterCatalog
    {
        internal FixedRunCharacterCatalog(params CharacterSO[] characters) =>
            Characters = characters ?? Array.Empty<CharacterSO>();

        public IReadOnlyCollection<CharacterSO> Characters { get; }
    }

    private sealed class RecruitActivation : IRecruitedCharacterActivationService
    {
        public bool TryValidateActivation(
            RegularCustomerRecord record,
            out string message)
        {
            message = record?.ActiveActor == null ? "actor missing" : string.Empty;
            return record?.ActiveActor != null;
        }

        public bool TryActivate(
            RegularCustomerRecord record,
            out CharacterActor actor,
            out string message)
        {
            actor = record?.ActiveActor;
            if (actor != null)
            {
                actor.characterType = CharacterType.NPC;
                actor.SetLifecycleState(CharacterLifecycleState.Active);
                if (actor.GetComponent<AbilityWork>() == null)
                    actor.gameObject.AddComponent<AbilityWork>();
                actor.RefreshAbilityCache();
            }
            message = actor == null ? "actor missing" : "activated";
            return actor != null;
        }
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
