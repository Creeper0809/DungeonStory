#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CropPhysicalTransactionFixture
{
    private const string PlotId = "building:crop-plot:qa";
    private const string DestinationId = "crop-plot|qa";
    private const string CropId = "crop:twilight-grain";
    private const string SeedItemId = "seed-lot:twilight-grain";
    private const string WaterItemId = "resource:clean-water";
    private const string CertificationKitItemId = "supply:certified-seed-kit";
    private const string TreatmentItemId = "supply:botanical-pesticide";

    [MenuItem("Tools/DungeonStory/Economy/Verify Crop Physical Transactions")]
    public static void VerifyFromMenu()
    {
        if (!Run())
            throw new InvalidOperationException(
                "Crop physical transaction fixture failed.");
        Debug.Log("Crop physical transaction fixture passed.");
    }

    public static bool Run()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [SeedItemId] = 1,
            [WaterItemId] = 2
        };
        FixtureGateway missing = new(catalog);
        CropPhysicalCommitSaveData missingOwner = new();
        string loneSeed = missing.AddSeed();
        if (CropPhysicalTransactionOutbox.TryCommitOrResume(
                missingOwner,
                CropPhysicalTransactionOutbox.FormatSowOperationId(PlotId, 0),
                CropPhysicalTransactionOutbox.SowReasonCode,
                0,
                DestinationId,
                requirements,
                SeedItemId,
                CropId,
                missing,
                out _,
                out _)
            || missing.Quantity(loneSeed) != 1
            || missingOwner.phase != CropPhysicalCommitPhase.None)
            return false;

        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        string seedStack = gateway.AddSeed();
        string waterA = gateway.Add(WaterItemId, 1);
        string waterB = gateway.Add(WaterItemId, 1);
        CropPhysicalCommitSaveData owner = new();
        string operation = CropPhysicalTransactionOutbox.FormatSowOperationId(
            PlotId,
            0);
        if (!CropPhysicalTransactionOutbox.TryCommitOrResume(
                owner,
                operation,
                CropPhysicalTransactionOutbox.SowReasonCode,
                0,
                DestinationId,
                requirements,
                SeedItemId,
                CropId,
                gateway,
                out SeedLotState seedLot,
                out _)
            || seedLot == null
            || owner.phase != CropPhysicalCommitPhase.InputCommitted
            || gateway.Quantity(seedStack) != 0
            || gateway.Quantity(waterA) != 0
            || gateway.Quantity(waterB) != 0)
            return false;

        owner.ecologyBeforeFingerprint = "absent";
        owner.ecologyAfterFingerprint = "qa-after";
        owner.phase = CropPhysicalCommitPhase.OutcomePublished;
        if (CropPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                owner,
                gateway,
                out _))
            return false;

        CropPlotSaveData serializedOwner = new()
        {
            buildingInstanceId = PlotId,
            cropId = CropId,
            phase = CropPlotPhase.ReadyToSow,
            materialsConsumed = true,
            nextSowOperationSequence = 0,
            pendingSow = owner.DeepClone()
        };
        CropPlotSaveData restored = JsonUtility.FromJson<CropPlotSaveData>(
            JsonUtility.ToJson(serializedOwner));
        CropPhysicalOwnerValidationSnapshot restoredOwner = new()
        {
            ExpectedOperationId = operation,
            Owner = restored.pendingSow.DeepClone()
        };
        PhysicalItemRestoreCandidateDispositionSnapshot candidate =
            ToCandidate(owner);
        Validate(new[] { restoredOwner }, candidate);
        if (!Reject(new[] { restoredOwner })
            || !Reject(
                Array.Empty<CropPhysicalOwnerValidationSnapshot>(),
                candidate)
            || !Reject(
                new[] { restoredOwner },
                Copy(candidate, candidate.InputMassGrams + 1L))
            || !CropPhysicalTransactionOutbox.TryCommitOrResume(
                restoredOwner.Owner,
                operation,
                CropPhysicalTransactionOutbox.SowReasonCode,
                0,
                DestinationId,
                requirements,
                SeedItemId,
                CropId,
                gateway,
                out _,
                out _)
            || !CropPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                restoredOwner.Owner,
                gateway,
                out _))
            return false;
        CropPhysicalTransactionOutbox.Clear(restoredOwner.Owner);
        Validate(Array.Empty<CropPhysicalOwnerValidationSnapshot>());
        return Reject(
            Array.Empty<CropPhysicalOwnerValidationSnapshot>(),
            candidate)
            && VerifyCertifiedOwner(catalog)
            && VerifyDestroyedPlotLoss(catalog)
            && VerifyCropTreatment(catalog);
    }

    private static bool VerifyCropTreatment(
        IDungeonItemCatalogProvider catalog)
    {
        const int Sequence = 7;
        const string TreatmentPlotId =
            "building:crop-plot:qa:treatment:colon-safe";
        const string TreatmentDestination =
            "crop-plot|qa|treatment";
        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        string treatmentStack = gateway.Add(
            TreatmentItemId,
            1,
            TreatmentDestination);
        CropEcologyPlotSaveData ecologyBefore = CreateTreatmentEcology(
            TreatmentPlotId,
            pestPressure: 55f);
        CropTreatmentOrderSaveData owner = CreateTreatmentOwner(
            TreatmentPlotId,
            TreatmentDestination,
            Sequence);
        owner.ecologyBeforeFingerprint =
            CropPhysicalTransactionOutbox.CreateEcologyFingerprint(
                new[] { ecologyBefore },
                TreatmentPlotId);

        if (!CropTreatmentPhysicalOutbox.TryCommitOrResume(
                owner,
                gateway,
                out _)
            || owner.phase != CropTreatmentOrderPhase.InputCommitted
            || gateway.Quantity(treatmentStack) != 0
            || owner.sourceStackIds.Count != 1
            || owner.inputMassGrams <= 0L
            || !gateway.HasPending(owner.operationId))
            return false;

        PhysicalItemRestoreCandidateDispositionSnapshot inputCandidate =
            ToTreatmentCandidate(owner);
        CropTreatmentOwnerValidationSnapshot inputState =
            CreateTreatmentSnapshot(
            TreatmentPlotId,
            Sequence,
            owner.DeepClone());
        CropPhysicalRestoreGuard.ValidateTreatmentOwnerSnapshots(
            new[] { inputState },
            new CandidateQuery(inputCandidate));
        CropPhysicalRestoreGuard.ValidateTreatmentEcologyEnvelope(
            TreatmentPlotId,
            inputState.Owner,
            new[] { ecologyBefore });
        CropEcologyPlotSaveData ecologyMismatch = CreateTreatmentEcology(
            TreatmentPlotId,
            pestPressure: 54f);
        if (!RejectTreatmentEcology(inputState, ecologyMismatch))
            return false;

        RecordingTreatmentTare tare = new();
        if (!CropTreatmentPhysicalOutbox.EnsureTareOutputs(
                owner,
                new Vector2Int(4, 9),
                tare,
                out _)
            || !CropTreatmentPhysicalOutbox.EnsureTareOutputs(
                owner,
                new Vector2Int(4, 9),
                tare,
                out _)
            || tare.CallCount != 2
            || !string.Equals(
                tare.LastParentCommitId,
                owner.commitId,
                StringComparison.Ordinal))
            return false;

        CropEcologyPlotSaveData ecologyAfter = CreateTreatmentEcology(
            TreatmentPlotId,
            pestPressure: 20f);
        owner.ecologyAfterFingerprint =
            CropPhysicalTransactionOutbox.CreateEcologyFingerprint(
                new[] { ecologyAfter },
                TreatmentPlotId);
        owner.phase = CropTreatmentOrderPhase.OutcomePublished;
        if (CropTreatmentPhysicalOutbox.TryAcknowledgeOutcome(
                owner,
                gateway,
                out _))
            return false;

        CropTreatmentOrderSaveData restored = JsonUtility.FromJson<
            CropTreatmentOrderSaveData>(JsonUtility.ToJson(owner));
        CropTreatmentOwnerValidationSnapshot restoredState =
            CreateTreatmentSnapshot(
            TreatmentPlotId,
            Sequence,
            restored);
        CropPhysicalRestoreGuard.ValidateTreatmentOwnerSnapshots(
            new[] { restoredState },
            new CandidateQuery(inputCandidate));
        if (!RejectTreatmentOwners(new[] { restoredState })
            || !RejectTreatmentOwners(
                Array.Empty<CropTreatmentOwnerValidationSnapshot>(),
                inputCandidate)
            || !RejectTreatmentOwners(
                new[] { restoredState },
                Copy(inputCandidate, inputCandidate.InputMassGrams + 1L))
            || !RejectTreatmentOwners(
                new[] { restoredState },
                Copy(
                    inputCandidate,
                    inputCandidate.InputMassGrams,
                    PhysicalItemDispositionKind.Transfer))
            || !RejectTreatmentOwners(
                new[] { restoredState },
                Copy(
                    inputCandidate,
                    inputCandidate.InputMassGrams,
                    inputCandidate.Kind,
                    inputCandidate.RequestFingerprint + ":tampered"))
            || !CropTreatmentPhysicalOutbox.TryCommitOrResume(
                restored,
                gateway,
                out _)
            || gateway.Quantity(treatmentStack) != 0
            || !CropTreatmentPhysicalOutbox.TryAcknowledgeOutcome(
                restored,
                gateway,
                out _)
            || gateway.HasPending(restored.operationId))
            return false;

        CropTreatmentPhysicalOutbox.Clear(restored);
        restoredState.Owner = restored;
        CropPhysicalRestoreGuard.ValidateTreatmentOwnerSnapshots(
            new[] { restoredState },
            new CandidateQuery());
        return VerifyDestroyedTreatmentLoss(catalog);
    }

    private static bool VerifyDestroyedTreatmentLoss(
        IDungeonItemCatalogProvider catalog)
    {
        const int Sequence = 11;
        const string TreatmentPlotId =
            "building:crop-plot:qa:treatment:destroyed";
        const string TreatmentDestination =
            "crop-plot|qa|treatment|destroyed";
        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        string treatmentStack = gateway.Add(
            TreatmentItemId,
            1,
            TreatmentDestination);
        CropTreatmentOrderSaveData owner = CreateTreatmentOwner(
            TreatmentPlotId,
            TreatmentDestination,
            Sequence);
        owner.ecologyBeforeFingerprint =
            CropPhysicalTransactionOutbox.CreateEcologyFingerprint(
                new[]
                {
                    CreateTreatmentEcology(TreatmentPlotId, 55f)
                },
                TreatmentPlotId);
        if (!CropTreatmentPhysicalOutbox.TryCommitOrResume(
                owner,
                gateway,
                out _)
            || gateway.Quantity(treatmentStack) != 0)
            return false;

        PhysicalItemRestoreCandidateDispositionSnapshot candidate =
            ToTreatmentCandidate(owner);
        if (CropTreatmentPhysicalOutbox.TryAcknowledgeDestroyedPlotLoss(
                owner,
                gateway,
                out _)
            || owner.phase
                != CropTreatmentOrderPhase.PlotDestroyedLossPending
            || owner.terminalDisposition
                != CropTreatmentTerminalDisposition.DestroyedWithPlotLoss
            || !CropTreatmentPhysicalOutbox.ValidateDestroyedPlotLoss(
                owner,
                out _))
            return false;

        CropTreatmentOrderSaveData restored = JsonUtility.FromJson<
            CropTreatmentOrderSaveData>(JsonUtility.ToJson(owner));
        CropTreatmentOwnerValidationSnapshot restoredState =
            CreateTreatmentSnapshot(
            TreatmentPlotId,
            Sequence,
            restored);
        CropPhysicalRestoreGuard.ValidateTreatmentOwnerSnapshots(
            new[] { restoredState },
            new CandidateQuery(candidate));
        CropTreatmentOrderSaveData tampered = restored.DeepClone();
        tampered.terminalLossMassGrams = checked(
            tampered.terminalLossMassGrams + 1L);
        if (CropTreatmentPhysicalOutbox.ValidateDestroyedPlotLoss(
                tampered,
                out _)
            || !CropTreatmentPhysicalOutbox.TryAcknowledgeDestroyedPlotLoss(
                restored,
                gateway,
                out _)
            || gateway.HasPending(restored.operationId))
            return false;

        CropTreatmentPhysicalOutbox.Clear(restored);
        return restored.phase == CropTreatmentOrderPhase.None
            && restored.terminalDisposition
                == CropTreatmentTerminalDisposition.None
            && restored.terminalLossMassGrams == 0L;
    }

    private static CropTreatmentOrderSaveData CreateTreatmentOwner(
        string plotId,
        string destinationId,
        int sequence) => new()
    {
        phase = CropTreatmentOrderPhase.Working,
        operationSequence = sequence,
        operationId = CropTreatmentPhysicalOutbox.FormatOperationId(
            plotId,
            sequence),
        reasonCode = CropTreatmentPhysicalOutbox.ReasonCode,
        destinationId = destinationId,
        itemId = TreatmentItemId,
        treatmentKind = CropTreatmentKind.BotanicalPesticide,
        quantity = 1,
        requiredWork = 5f,
        completedWork = 5f,
        effectAmount = 35f,
        cooldownDays = 2,
        scheduledAbsoluteDay = 9
    };

    private static CropTreatmentOwnerValidationSnapshot
        CreateTreatmentSnapshot(
        string plotId,
        int sequence,
        CropTreatmentOrderSaveData owner) => new()
    {
        PlotId = plotId,
        NextOperationSequence = sequence,
        Owner = owner
    };

    private static CropEcologyPlotSaveData CreateTreatmentEcology(
        string plotId,
        float pestPressure) => new()
    {
        plotId = plotId,
        cropId = CropId,
        cultivarGenomeId = "genome:twilight-grain:base",
        currentGroup = CropFamilyGroup.Grain,
        fertility = 80f,
        pestPressure = pestPressure,
        diseasePressure = 10f
    };

    private static bool RejectTreatmentOwners(
        IReadOnlyCollection<CropTreatmentOwnerValidationSnapshot> owners,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts)
    {
        try
        {
            CropPhysicalRestoreGuard.ValidateTreatmentOwnerSnapshots(
                owners,
                new CandidateQuery(receipts));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool RejectTreatmentEcology(
        CropTreatmentOwnerValidationSnapshot owner,
        CropEcologyPlotSaveData ecology)
    {
        try
        {
            CropPhysicalRestoreGuard.ValidateTreatmentEcologyEnvelope(
                owner.PlotId,
                owner.Owner,
                new[] { ecology });
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool VerifyDestroyedPlotLoss(
        IDungeonItemCatalogProvider catalog)
    {
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [SeedItemId] = 1,
            [WaterItemId] = 2
        };
        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        string seedStack = gateway.AddSeed();
        string waterStack = gateway.Add(WaterItemId, 2);
        CropPhysicalCommitSaveData owner = new();
        string operation = CropPhysicalTransactionOutbox.FormatSowOperationId(
            PlotId + ":destroyed",
            0);
        if (!CropPhysicalTransactionOutbox.TryCommitOrResume(
                owner,
                operation,
                CropPhysicalTransactionOutbox.SowReasonCode,
                0,
                DestinationId,
                requirements,
                SeedItemId,
                CropId,
                gateway,
                out _,
                out _)
            || gateway.Quantity(seedStack) != 0
            || gateway.Quantity(waterStack) != 0)
            return false;

        owner.ecologyBeforeFingerprint = "absent";
        PhysicalItemRestoreCandidateDispositionSnapshot pending =
            ToCandidate(owner);
        if (CropPhysicalTransactionOutbox.TryAcknowledgeDestroyedPlotLoss(
                owner,
                gateway,
                out _)
            || owner.phase
                != CropPhysicalCommitPhase.PlotDestroyedLossPending
            || owner.terminalDisposition
                != CropWipTerminalDisposition.DestroyedWithPlotLoss
            || owner.terminalLossQuantity != owner.inputQuantity
            || owner.terminalLossMassGrams != owner.inputMassGrams
            || !string.Equals(
                owner.terminalOperationId,
                CropPhysicalTransactionOutbox
                    .FormatDestroyedPlotLossOperationId(operation),
                StringComparison.Ordinal)
            || !CropPhysicalTransactionOutbox.ValidateDestroyedPlotLoss(
                owner,
                out _))
            return false;

        CropPhysicalCommitSaveData restored = JsonUtility.FromJson<
            CropPhysicalCommitSaveData>(JsonUtility.ToJson(owner));
        CropPhysicalOwnerValidationSnapshot restoredOwner = new()
        {
            ExpectedOperationId = operation,
            Owner = restored
        };
        Validate(new[] { restoredOwner }, pending);

        CropPhysicalCommitSaveData tampered = restored.DeepClone();
        tampered.terminalLossMassGrams = checked(
            tampered.terminalLossMassGrams + 1L);
        if (CropPhysicalTransactionOutbox.ValidateDestroyedPlotLoss(
                tampered,
                out _)
            || !CropPhysicalTransactionOutbox.TryAcknowledgeDestroyedPlotLoss(
                restored,
                gateway,
                out _)
            || gateway.HasPending(operation))
            return false;

        CropPhysicalTransactionOutbox.Clear(restored);
        return restored.phase == CropPhysicalCommitPhase.None
            && restored.terminalDisposition
                == CropWipTerminalDisposition.None
            && restored.terminalLossMassGrams == 0L;
    }

    private static bool VerifyCertifiedOwner(
        IDungeonItemCatalogProvider catalog)
    {
        const int Sequence = 3;
        const string OrderId = "certified-seed-order:00000003";
        const string Destination =
            "certified-seed|building%3Agreenhouse%3Aqa|crop%3Atwilight-grain|00000003";
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [SeedItemId] = 1,
            [CertificationKitItemId] = 1
        };
        FixtureGateway gateway = new(catalog);
        gateway.AddSeed(Destination);
        gateway.Add(CertificationKitItemId, 1, Destination);
        CropPhysicalCommitSaveData owner = new();
        string operation =
            CropPhysicalTransactionOutbox.FormatCertifiedOperationId(OrderId);
        if (!CropPhysicalTransactionOutbox.TryCommitOrResume(
                owner,
                operation,
                CropPhysicalTransactionOutbox.CertifiedReasonCode,
                Sequence,
                Destination,
                requirements,
                SeedItemId,
                CropId,
                gateway,
                out SeedLotState source,
                out _))
            return false;
        SeedLotState certified = source.Clone();
        certified.pathogenLoad = Mathf.Max(0f, certified.pathogenLoad - 30f);
        CertifiedSeedWorldSaveData payload = new()
        {
            nextOrderSequence = 4,
            orders = new List<CertifiedSeedOrderSaveData>
            {
                new()
                {
                    orderId = OrderId,
                    orderSequence = Sequence,
                    actionId = "qa:certified-seed",
                    facilityInstanceId = "building:greenhouse:qa",
                    cropId = CropId,
                    destinationId = Destination,
                    phase = CertifiedSeedOrderPhase.InputCommitted,
                    pendingInput = owner.DeepClone(),
                    certifiedSeedLot = certified
                }
            }
        };
        CertifiedSeedWorldSaveData restored =
            JsonUtility.FromJson<CertifiedSeedWorldSaveData>(
                JsonUtility.ToJson(payload));
        if (restored == null
            || restored.version != CertifiedSeedWorldSaveData.CurrentVersion
            || restored.orders.Count != 1
            || restored.orders[0].pendingInput.inputMassGrams
                != owner.inputMassGrams)
            return false;
        PhysicalItemRestoreCandidateDispositionSnapshot receipt =
            ToCandidate(owner);
        CropPhysicalOwnerValidationSnapshot snapshot = new()
        {
            ExpectedOperationId = operation,
            Owner = restored.orders[0].pendingInput
        };
        Validate(new[] { snapshot }, receipt);
        return Reject(
                Array.Empty<CropPhysicalOwnerValidationSnapshot>(),
                receipt)
            && !string.IsNullOrWhiteSpace(
                restored.orders[0].certifiedSeedLot.cultivarGenomeId);
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot ToCandidate(
        CropPhysicalCommitSaveData owner) => new(
        PhysicalItemDispositionKind.Transfer,
        owner.operationId,
        owner.reasonCode,
        ItemFingerprint(owner),
        owner.inputs.OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .Select(value => value.sourceStackId)
            .ToArray(),
        owner.inputQuantity,
        owner.inputMassGrams,
        owner.commitId);

    private static string ItemFingerprint(CropPhysicalCommitSaveData owner) =>
        $"{(int)PhysicalItemDispositionKind.Transfer}:{owner.reasonCode}:"
        + string.Join(",", owner.inputs
            .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .Select(value => $"{value.sourceStackId}={value.quantity}"));

    private static PhysicalItemRestoreCandidateDispositionSnapshot Copy(
        PhysicalItemRestoreCandidateDispositionSnapshot source,
        long mass,
        PhysicalItemDispositionKind? kind = null,
        string requestFingerprint = null) => new(
        kind ?? source.Kind,
        source.OperationId,
        source.ReasonCode,
        requestFingerprint ?? source.RequestFingerprint,
        source.SourceStackIds,
        source.Quantity,
        mass,
        source.CommitId);

    private static PhysicalItemRestoreCandidateDispositionSnapshot
        ToTreatmentCandidate(CropTreatmentOrderSaveData owner) => new(
            PhysicalItemDispositionKind.Sink,
            owner.operationId,
            owner.reasonCode,
            owner.requestFingerprint,
            owner.sourceStackIds.OrderBy(
                value => value,
                StringComparer.Ordinal).ToArray(),
            owner.quantity,
            owner.inputMassGrams,
            owner.commitId);

    private static void Validate(
        IReadOnlyCollection<CropPhysicalOwnerValidationSnapshot> owners,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts) =>
        CropPhysicalRestoreGuard.ValidateOwnerSnapshots(
            owners,
            new CandidateQuery(receipts));

    private static bool Reject(
        IReadOnlyCollection<CropPhysicalOwnerValidationSnapshot> owners,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts)
    {
        try
        {
            Validate(owners, receipts);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            values;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] values) =>
            this.values = values
                ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => values;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot value)
        {
            value = values.FirstOrDefault(candidate => string.Equals(
                candidate.OperationId,
                operationId,
                StringComparison.Ordinal));
            return value != null;
        }
    }

    private sealed class FixtureGateway :
        IPhysicalSeedLotGateway,
        IPhysicalFacilityItemSinkGateway
    {
        private readonly WorldItemRepository repository;
        private readonly WorldItemQueryService query;
        private readonly IPhysicalItemBatchDispositionService dispositions;
        private readonly PhysicalFacilityItemSinkGateway sink;

        internal FixtureGateway(IDungeonItemCatalogProvider catalog)
        {
            repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            PhysicalItemMassQuery mass = new(catalog);
            query = new WorldItemQueryService(
                catalog,
                mass,
                repository,
                EditorNullItemMarkerPresenter.Instance);
            dispositions = new PhysicalItemBatchDispositionService(
                repository,
                mass,
                EditorNullItemMarkerPresenter.Instance);
            sink = new PhysicalFacilityItemSinkGateway(
                new PhysicalStockQuery(repository, catalog, mass),
                dispositions);
        }

        internal bool FailNextAcknowledgement { get; set; }

        internal string Add(
            string itemId,
            int quantity,
            string destinationId = DestinationId) =>
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                quantity,
                WorldItemStackState.FacilityBuffer,
                destinationId: destinationId);

        internal string AddSeed(string destinationId = DestinationId) =>
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                SeedItemId,
                1,
                WorldItemStackState.FacilityBuffer,
                destinationId: destinationId,
                components: new[]
                {
                    SeedLotItemStateCodec.Encode(new SeedLotState
                    {
                        cropId = CropId,
                        cultivarGenomeId = "genome:twilight-grain:base",
                        generation = 0,
                        pathogenLoad = 20f
                    })
                });

        internal int Quantity(string stackId) =>
            repository.GetEditorTestQuantity(stackId);

        internal bool HasPending(string operationId) =>
            dispositions.TryGetPending(operationId, out _);

        public bool TryCommitSinkPending(
            string destinationId,
            string itemId,
            int quantity,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => sink.TryCommitSinkPending(
                destinationId,
                itemId,
                quantity,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            sink.TryGetPending(operationId, out receipt);

        public bool Acknowledge(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected-acknowledgement-failure";
                return false;
            }
            return sink.Acknowledge(commitId, out failureReason);
        }

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            query.GetAllStacks();

        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => dispositions.TryCommitPending(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool TryGetPendingBatchPhysicalDisposition(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            dispositions.TryGetPending(operationId, out receipt);

        public bool AcknowledgeBatchPhysicalDisposition(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected-acknowledgement-failure";
                return false;
            }
            return dispositions.Acknowledge(commitId, out failureReason);
        }

        public bool CanSpawnSeedLot(
            string seedItemId,
            int amount,
            Vector2Int position,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }

        public bool RequestBestSeedLot(
            string seedItemId,
            string cropId,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out DomainFailure failure)
        {
            requested = 0;
            failure = DomainFailure.None;
            return false;
        }

        public bool TryEnsureSeedLotOutput(
            string seedItemId,
            SeedLotState seedLot,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            string operationId,
            out string commitId,
            out string failureReason)
        {
            commitId = string.Empty;
            failureReason = "not-used";
            return false;
        }

        public bool SpawnSeedLot(
            string seedItemId,
            int amount,
            SeedLotState seedLot,
            Vector2Int position) => false;
    }

    private sealed class RecordingTreatmentTare :
        IPackagedLotTareDispositionService
    {
        internal int CallCount { get; private set; }
        internal string LastParentCommitId { get; private set; } = string.Empty;

        public bool EnsureTerminalSinkOutputs(
            IReadOnlyDictionary<string, int> consumedItems,
            Vector2Int outputPosition,
            string parentCommitId,
            out PackagedLotTareOutputReceipt receipt,
            out string failureReason)
        {
            CallCount++;
            LastParentCommitId = parentCommitId ?? string.Empty;
            receipt = default;
            failureReason = string.Empty;
            return consumedItems != null
                && consumedItems.TryGetValue(
                    TreatmentItemId,
                    out int quantity)
                && quantity == 1;
        }
    }
}
#endif
