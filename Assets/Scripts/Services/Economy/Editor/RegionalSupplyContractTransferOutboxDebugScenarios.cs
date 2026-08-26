#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RegionalSupplyContractTransferOutboxDebugScenarios
{
    private const string ItemId = "material:lumber";
    private const string ContractId = "contract:1:1";
    private const string DestinationId = "regional-contract:contract:1:1";

    [MenuItem("DungeonStory/Debug/Economy/Run Regional Supply Transfer Outbox Contracts")]
    public static void RunAll()
    {
        Debug.Log(Verify());
    }

    public static string Verify()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(catalog);
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalItemBatchDispositionService dispositions = new(
            repository,
            massQuery,
            EditorNullItemMarkerPresenter.Instance);
        PhysicalFacilityItemSinkGateway gateway = new(
            new PhysicalStockQuery(repository, catalog, massQuery),
            dispositions);
        string sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            quantity: 3,
            state: WorldItemStackState.FacilityBuffer,
            destinationId: DestinationId,
            position: new Vector2Int(4, 2));

        string operationId =
            RegionalSupplyContractDeliveryOutbox.FormatOperationId(ContractId);
        Require(gateway.TryCommitTransferPending(
                DestinationId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [ItemId] = 2
                },
                operationId,
                RegionalSupplyContractDeliveryOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt physicalReceipt,
                out string commitFailure),
            "Regional supply physical transfer failed: " + commitFailure);

        RegionalSupplyContractState contract = CreateContract();
        RegionalSupplyContractDeliveryOutbox.RecordPending(
            contract,
            ToContractReceipt(physicalReceipt));
        PhysicalItemRestoreCandidateDispositionSnapshot candidateReceipt =
            ToCandidateReceipt(physicalReceipt);
        DungeonRegionalSupplyContractSaveData pendingPayload = new()
        {
            contracts = new List<RegionalSupplyContractState>
            {
                contract.Clone()
            }
        };
        RegionalSupplyContractSaveSection.ValidatePhysicalRestoreCandidate(
            pendingPayload,
            new CandidateQuery(candidateReceipt));
        Require(Throws(() =>
                RegionalSupplyContractSaveSection
                    .ValidatePhysicalRestoreCandidate(
                        pendingPayload,
                        CandidateQuery.Empty)),
            "Regional supply restore accepted a missing incoming physical Transfer.");
        Require(Throws(() =>
                RegionalSupplyContractSaveSection
                    .ValidatePhysicalRestoreCandidate(
                        new DungeonRegionalSupplyContractSaveData(),
                        new CandidateQuery(candidateReceipt))),
            "Regional supply restore accepted an orphan incoming physical Transfer.");
        PhysicalItemRestoreCandidateDispositionSnapshot mismatchedReceipt = new(
            candidateReceipt.Kind,
            candidateReceipt.OperationId,
            candidateReceipt.ReasonCode,
            candidateReceipt.RequestFingerprint,
            candidateReceipt.SourceStackIds,
            candidateReceipt.Quantity,
            candidateReceipt.InputMassGrams + 1L,
            candidateReceipt.CommitId);
        Require(Throws(() =>
                RegionalSupplyContractSaveSection
                    .ValidatePhysicalRestoreCandidate(
                        pendingPayload,
                        new CandidateQuery(mismatchedReceipt))),
            "Regional supply restore accepted mismatched physical mass provenance.");
        VerifyPhysicalCandidateViewLifetime(candidateReceipt);
        VerifyWholeSaveRegistryJoin(contract, physicalReceipt);
        FailOnceCommandPort commands = new(gateway)
        {
            FailNextAcknowledgement = true
        };

        Require(!RegionalSupplyContractDeliveryOutbox.TryFinalizePending(
                contract,
                commands,
                out _)
            && contract.status == RegionalSupplyContractStatus.Completed
            && contract.deliveryCommitPhase ==
                RegionalSupplyDeliveryCommitPhase.RewardPublished
            && commands.IncomePublicationCount == 1
            && commands.PublishedIncome == contract.rewardGold
            && repository.GetEditorPendingBatchDispositionCount() == 1
            && repository.GetEditorTestQuantity(sourceStackId) == 1,
            "Acknowledgement fault did not retain exact transfer/reward outbox state.");

        RegionalSupplyContractState restored =
            JsonUtility.FromJson<RegionalSupplyContractState>(
                JsonUtility.ToJson(contract));
        RegionalSupplyContractState tampered = restored.Clone();
        tampered.deliveryMassGrams++;
        Require(!RegionalSupplyContractDeliveryOutbox.TryFinalizePending(
                tampered,
                commands,
                out _)
            && commands.IncomePublicationCount == 1
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "Tampered regional-supply provenance mutated physical or reward authority.");

        Require(RegionalSupplyContractDeliveryOutbox.TryFinalizePending(
                restored,
                commands,
                out string recoveryFailure),
            "Regional supply acknowledgement-only recovery failed: "
            + recoveryFailure);
        Require(restored.status == RegionalSupplyContractStatus.Completed
            && RegionalSupplyContractDeliveryOutbox.HasCanonicalEmpty(restored)
            && commands.IncomePublicationCount == 1
            && commands.SuccessfulAcknowledgements == 1
            && repository.GetEditorPendingBatchDispositionCount() == 0
            && repository.GetEditorTestQuantity(sourceStackId) == 1,
            "Regional supply recovery duplicated income or lost exact transfer quantity.");

        return "Regional supply transfer outbox contracts PASS.";
    }

    private static RegionalSupplyContractState CreateContract() => new()
    {
        contractId = ContractId,
        title = "QA lumber export",
        regionName = "QA",
        offeredDay = 1,
        deadlineDay = 4,
        rewardGold = 25,
        status = RegionalSupplyContractStatus.Delivering,
        destinationId = DestinationId,
        lastStatus = "delivery",
        requirements = new List<RegionalSupplyContractRequirement>
        {
            new RegionalSupplyContractRequirement
            {
                itemId = ItemId,
                amount = 2
            }
        }
    };

    private static RegionalSupplyDeliveryTransferReceipt ToContractReceipt(
        PhysicalItemBatchDispositionReceipt receipt) => new()
    {
        operationId = receipt.OperationId,
        reasonCode = receipt.ReasonCode,
        commitId = receipt.CommitId,
        sourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList(),
        quantity = receipt.Quantity,
        inputMassGrams = receipt.InputMassGrams
    };

    private static PhysicalItemRestoreCandidateDispositionSnapshot
        ToCandidateReceipt(PhysicalItemBatchDispositionReceipt receipt) => new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            "regional-supply-focused-fixture",
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static void VerifyPhysicalCandidateViewLifetime(
        PhysicalItemRestoreCandidateDispositionSnapshot receipt)
    {
        WorldItemStackRuntime candidateRuntime =
            PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture();
        DungeonPhysicalItemSaveData snapshot = candidateRuntime.Capture();
        snapshot.pendingBatchDispositions.Add(
            new PhysicalItemBatchDispositionSaveData
            {
                kind = (int)receipt.Kind,
                operationId = receipt.OperationId,
                reasonCode = receipt.ReasonCode,
                requestFingerprint = receipt.RequestFingerprint,
                sourceStackIds = receipt.SourceStackIds.ToList(),
                quantity = receipt.Quantity,
                inputMassGrams = receipt.InputMassGrams,
                commitId = receipt.CommitId
            });

        IPhysicalItemRestoreStaging staging = candidateRuntime;
        IPhysicalItemRestoreCandidateQuery query =
            (candidateRuntime as object) as IPhysicalItemRestoreCandidateQuery;
        Require(query != null && !query.IsCandidateAvailable,
            "Physical restore candidate query leaked outside staging.");

        IDungeonSaveRestoreStage discarded = staging.StageTransactionalRestore(
            snapshot,
            EmptyRestoreWorldCandidates.Instance);
        Require(query.IsCandidateAvailable
            && query.TryGetPendingBatchDisposition(
                receipt.OperationId,
                out PhysicalItemRestoreCandidateDispositionSnapshot stagedReceipt)
            && stagedReceipt.InputMassGrams == receipt.InputMassGrams,
            "Physical restore candidate query did not expose the detached receipt.");
        ((IDungeonDiscardableSaveRestoreStage)discarded).Discard();
        Require(!query.IsCandidateAvailable,
            "Physical restore candidate query survived stage discard.");

        IDungeonSaveRestoreStage committed = staging.StageTransactionalRestore(
            snapshot,
            EmptyRestoreWorldCandidates.Instance);
        IDungeonRestoreTransactionParticipant lifetime = candidateRuntime;
        lifetime.BeginRestoreCandidate();
        DungeonGameRestoreReport report = new();
        committed.Commit(report);
        Require(report.Success
            && query.IsCandidateAvailable
            && candidateRuntime.TryGetPendingBatchPhysicalDisposition(
                receipt.OperationId,
                out PhysicalItemBatchDispositionReceipt restored)
            && restored.CommitId == receipt.CommitId,
            "Physical restore candidate query did not survive commit until cross-section publication.");
        lifetime.PublishRestoreCandidate();
        lifetime.CompleteRestoreCandidate();
        Require(!query.IsCandidateAvailable,
            "Physical restore candidate query survived transaction completion.");
    }

    private static void VerifyWholeSaveRegistryJoin(
        RegionalSupplyContractState contract,
        PhysicalItemBatchDispositionReceipt sourceReceipt)
    {
        IDungeonItemCatalogProvider itemCatalog =
            EditorItemCatalogFactory.Create();
        DungeonRuntimeAggregateRootStore aggregateRootStore = new();
        WorldItemStackRuntime items =
            PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                itemCatalog,
                aggregateRootStore,
                out WorldItemRepository repository,
                out _,
                out _,
                out _,
                out _,
                out IPhysicalItemBatchDispositionService batch);
        try
        {
            string stackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                ItemId,
                quantity: 3,
                state: WorldItemStackState.FacilityBuffer,
                destinationId: DestinationId,
                position: new Vector2Int(4, 2));
            PhysicalFacilityItemSinkGateway gateway = new(
                new PhysicalStockQuery(
                    repository,
                    itemCatalog,
                    items.MassQuery),
                batch);
            Require(gateway.TryCommitTransferPending(
                    DestinationId,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [ItemId] = 2
                    },
                    sourceReceipt.OperationId,
                    RegionalSupplyContractDeliveryOutbox.TransferReason,
                    out PhysicalItemBatchDispositionReceipt receipt,
                    out string commitFailure),
                "Whole-save regional physical transfer failed: "
                + commitFailure);

            RegionalSupplyContractState owner = contract.Clone();
            owner.deliveryCommitPhase = RegionalSupplyDeliveryCommitPhase.None;
            owner.deliveryOperationId = string.Empty;
            owner.deliveryCommitId = string.Empty;
            owner.deliverySourceStackIds.Clear();
            owner.deliveryQuantity = 0;
            owner.deliveryMassGrams = 0L;
            RegionalSupplyContractDeliveryOutbox.RecordPending(
                owner,
                ToContractReceipt(receipt));
            DungeonRegionalSupplyContractSaveData regionalPayload = new()
            {
                currentDay = 1,
                nextOfferDay = 4,
                nextSequence = 2,
                contracts = new List<RegionalSupplyContractState>
                {
                    owner
                }
            };
            RegistryRegionalRuntime regionalRuntime = new(regionalPayload);
            IResourceEconomyContentCatalog economyCatalog =
                new ResourceEconomyContentCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader()));
            PhysicalItemsSaveSection physicalSection = new(
                items,
                items,
                EmptyRestoreWorldCandidates.Instance,
                ProductionOutputLifecycleRestoreCandidatePublisher
                    .IsolatedSectionFixtureOnly);
            RegionalSupplyContractSaveSection regionalSection = new(
                regionalRuntime,
                economyCatalog,
                items);
            IDungeonSaveSection[] sections =
            {
                new RegistryDependencyStubSection(
                    ModularFacilityWorldSaveSection.Id,
                    DungeonSaveRestorePhase.World),
                new RegistryDependencyStubSection(
                    CharacterWorldSaveSection.Id,
                    DungeonSaveRestorePhase.Characters),
                physicalSection,
                new RegistryDependencyStubSection(
                    ResourceStockPolicySaveSection.Id,
                    DungeonSaveRestorePhase.RuntimeState,
                    PhysicalItemsSaveSection.Id),
                regionalSection
            };
            DungeonSaveSectionRegistry registry = new(
                sections,
                aggregateRootStore,
                new[]
                {
                    (IDungeonRestoreTransactionParticipant)items
                });
            List<DungeonSaveSectionEnvelope> valid = registry.CaptureAll();
            DungeonGameRestoreReport validReport = new();
            Require(registry.RestoreAll(valid, validReport)
                    && validReport.Success
                    && !items.IsCandidateAvailable
                    && repository.GetEditorTestQuantity(stackId) == 1
                    && regionalRuntime.Contracts.Count == 1
                    && RegionalSupplyContractDeliveryOutbox.HasPending(
                        regionalRuntime.Contracts[0]),
                "Valid pending regional whole-save did not restore atomically: "
                + string.Join(" | ", validReport.Errors));

            RequireWholeSaveRejectedWithoutLeak(
                registry,
                RemovePhysicalPending(valid),
                items,
                "missing receipt");
            RequireWholeSaveRejectedWithoutLeak(
                registry,
                MutateRegionalOwner(valid, value =>
                    value.deliveryMassGrams = checked(
                        value.deliveryMassGrams + 1L)),
                items,
                "mismatched owner mass");
            RequireWholeSaveRejectedWithoutLeak(
                registry,
                MutateRegionalOwner(valid, ClearRegionalPendingOwner),
                items,
                "orphan incoming receipt");
        }
        finally
        {
            items.Dispose();
        }
    }

    private static void RequireWholeSaveRejectedWithoutLeak(
        DungeonSaveSectionRegistry registry,
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        IPhysicalItemRestoreCandidateQuery query,
        string caseName)
    {
        DungeonGameRestoreReport report = new();
        Require(!registry.RestoreAll(envelopes, report)
                && !report.Success
                && !query.IsCandidateAvailable,
            "Regional whole-save " + caseName
            + " did not reject atomically or leaked its candidate index.");
    }

    private static List<DungeonSaveSectionEnvelope> RemovePhysicalPending(
        IReadOnlyList<DungeonSaveSectionEnvelope> source)
    {
        List<DungeonSaveSectionEnvelope> clone = CloneEnvelopes(source);
        DungeonSaveSectionEnvelope physical = clone.Single(value =>
            string.Equals(
                value.sectionId,
                PhysicalItemsSaveSection.Id,
                StringComparison.Ordinal));
        DungeonPhysicalItemSaveData payload =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                physical.payloadJson);
        payload.pendingBatchDispositions.Clear();
        physical.payloadJson = JsonUtility.ToJson(payload);
        return clone;
    }

    private static List<DungeonSaveSectionEnvelope> MutateRegionalOwner(
        IReadOnlyList<DungeonSaveSectionEnvelope> source,
        Action<RegionalSupplyContractState> mutation)
    {
        List<DungeonSaveSectionEnvelope> clone = CloneEnvelopes(source);
        DungeonSaveSectionEnvelope regional = clone.Single(value =>
            string.Equals(
                value.sectionId,
                RegionalSupplyContractSaveSection.Id,
                StringComparison.Ordinal));
        DungeonRegionalSupplyContractSaveData payload =
            JsonUtility.FromJson<DungeonRegionalSupplyContractSaveData>(
                regional.payloadJson);
        mutation(payload.contracts.Single());
        regional.payloadJson = JsonUtility.ToJson(payload);
        return clone;
    }

    private static List<DungeonSaveSectionEnvelope> CloneEnvelopes(
        IReadOnlyList<DungeonSaveSectionEnvelope> source) =>
        source.Select(value => new DungeonSaveSectionEnvelope
        {
            sectionId = value.sectionId,
            sectionVersion = value.sectionVersion,
            restorePhase = value.restorePhase,
            optional = value.optional,
            payloadJson = value.payloadJson
        }).ToList();

    private static void ClearRegionalPendingOwner(
        RegionalSupplyContractState owner)
    {
        owner.deliveryCommitPhase = RegionalSupplyDeliveryCommitPhase.None;
        owner.deliveryOperationId = string.Empty;
        owner.deliveryCommitId = string.Empty;
        owner.deliverySourceStackIds.Clear();
        owner.deliveryQuantity = 0;
        owner.deliveryMassGrams = 0L;
    }

    private sealed class RegistryRegionalRuntime :
        IRegionalSupplyContractRuntime
    {
        private DungeonRegionalSupplyContractSaveData state;

        internal RegistryRegionalRuntime(
            DungeonRegionalSupplyContractSaveData state)
        {
            this.state = Clone(state);
        }

        public int Version { get; private set; }
        public bool IsUnlocked => true;
        public IReadOnlyList<RegionalSupplyContractState> Contracts =>
            state.contracts;
        public bool Accept(string contractId, out string message)
        {
            message = "fixture-only";
            return false;
        }
        public bool Decline(string contractId, out string message)
        {
            message = "fixture-only";
            return false;
        }
        public DungeonRegionalSupplyContractSaveData Capture() => Clone(state);
        public RegionalSupplyContractRestoreCandidate PrepareRestoreCandidate(
            DungeonRegionalSupplyContractSaveData saveData) =>
            new(Clone(saveData));
        public void PublishRestoreCandidate(
            RegionalSupplyContractRestoreCandidate candidate)
        {
            state = Clone(candidate.Payload);
            Version++;
        }

        private static DungeonRegionalSupplyContractSaveData Clone(
            DungeonRegionalSupplyContractSaveData value) =>
            JsonUtility.FromJson<DungeonRegionalSupplyContractSaveData>(
                JsonUtility.ToJson(value));
    }

    private sealed class RegistryDependencyStubSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly IReadOnlyList<string> dependencies;

        internal RegistryDependencyStubSection(
            string sectionId,
            DungeonSaveRestorePhase phase,
            params string[] dependencies)
        {
            SectionId = sectionId;
            RestorePhase = phase;
            this.dependencies = dependencies ?? Array.Empty<string>();
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => dependencies;
        public string Capture() => "{\"fixture\":true}";
        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError("Invalid registry dependency fixture payload.");
            }
        }
        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FailOnceCommandPort :
        IRegionalSupplyContractCommandPort
    {
        private readonly IPhysicalFacilityItemBatchTransferGateway gateway;

        internal FailOnceCommandPort(
            IPhysicalFacilityItemBatchTransferGateway gateway) =>
            this.gateway = gateway
                ?? throw new ArgumentNullException(nameof(gateway));

        internal bool FailNextAcknowledgement { get; set; }
        internal int IncomePublicationCount { get; private set; }
        internal int PublishedIncome { get; private set; }
        internal int SuccessfulAcknowledgements { get; private set; }

        public bool RequestDelivery(
            string itemId,
            int amount,
            Vector2Int dropoff,
            string destinationId,
            out int requested)
        {
            requested = 0;
            return false;
        }

        public bool TryCommitDeliveryTransferPending(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            string operationId,
            string reasonCode,
            out RegionalSupplyDeliveryTransferReceipt receipt,
            out string failureReason)
        {
            receipt = null;
            failureReason = "not used by focused recovery fixture";
            return false;
        }

        public bool TryGetPendingDeliveryTransfer(
            string operationId,
            out RegionalSupplyDeliveryTransferReceipt receipt)
        {
            receipt = null;
            if (!gateway.TryGetPending(
                    operationId,
                    out PhysicalItemBatchDispositionReceipt physical))
            {
                return false;
            }
            receipt = ToContractReceipt(physical);
            return true;
        }

        public bool AcknowledgeDeliveryTransfer(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected-regional-supply-ack-failure";
                return false;
            }
            bool acknowledged = gateway.Acknowledge(
                commitId,
                out failureReason);
            if (acknowledged)
            {
                SuccessfulAcknowledgements++;
            }
            return acknowledged;
        }

        public int ReleaseDestination(
            string destinationId,
            Vector2Int releasePosition) => 0;

        public void PrioritizeDestination(string destinationId)
        {
        }

        public void RequestHauler()
        {
        }

        public bool TryAddContractIncome(
            int amount,
            string operationId,
            out string failureReason)
        {
            IncomePublicationCount++;
            PublishedIncome += amount;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        internal static readonly CandidateQuery Empty = new();

        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> pending;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] pending)
        {
            this.pending = pending ?? Array.Empty<
                PhysicalItemRestoreCandidateDispositionSnapshot>();
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => pending;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = pending.SingleOrDefault(candidate =>
                string.Equals(
                    candidate.OperationId,
                    operationId,
                    StringComparison.Ordinal));
            return disposition != null;
        }
    }

    private sealed class EmptyRestoreWorldCandidates :
        IRestoreWorldCandidateQuery
    {
        internal static readonly EmptyRestoreWorldCandidates Instance = new();

        public int Revision => 0;
        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }

        public bool TryGetBuildings(
            out IReadOnlyList<BuildableObject> buildings)
        {
            buildings = Array.Empty<BuildableObject>();
            return true;
        }

        public bool TryGetCharacters(
            out IReadOnlyList<CharacterActor> characters)
        {
            characters = Array.Empty<CharacterActor>();
            return false;
        }

        public bool TryGetWildlife(
            out IReadOnlyList<WildlifeActor> wildlife)
        {
            wildlife = Array.Empty<WildlifeActor>();
            return false;
        }

        public bool TryGetExteriorZones(
            out IReadOnlyList<ExteriorZoneMarker> zones)
        {
            zones = Array.Empty<ExteriorZoneMarker>();
            return false;
        }
    }
}
#endif
