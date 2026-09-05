using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CombatEquipmentCraftTransactionFixture
{
    public static string LastFailureReason { get; private set; } = string.Empty;

    private static readonly IReadOnlyDictionary<string, int> Requirements =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["material:lumber"] = 2,
            ["material:iron-ingot"] = 1
        };

    public static bool Run()
    {
        LastFailureReason = string.Empty;
        if (!VerifyCommonOutputTransaction())
        {
            return Fail("common output transaction");
        }
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        if (!VerifyMissingInputAtomic(catalog))
        {
            return Fail("missing-input atomicity");
        }
        if (!VerifyRejectedDismantleReplay(catalog))
        {
            return Fail("rejected-dismantle replay");
        }

        FixtureGateway gateway = new(catalog) { FailNextAcknowledgement = true };
        CombatEquipmentCraftOrderSaveData order = CreateOrder("qa");
        string lumberA = gateway.Add("material:lumber", 1, order.materialDestinationId);
        string lumberB = gateway.Add("material:lumber", 1, order.materialDestinationId);
        string iron = gateway.Add("material:iron-ingot", 1, order.materialDestinationId);
        if (!CombatEquipmentCraftMaterialOutbox.TryCommitOrResume(
                order,
                Requirements,
                gateway.GetAllStacks(),
                gateway,
                out _)
            || !order.materialsReady
            || order.materialTransferInputs.Count != 3
            || gateway.Quantity(lumberA) != 0
            || gateway.Quantity(lumberB) != 0
            || gateway.Quantity(iron) != 0
            || !gateway.TryGetPendingBatchPhysicalDisposition(
                order.materialTransferOperationId,
                out PhysicalItemBatchDispositionReceipt receipt))
        {
            return Fail("material Transfer commit");
        }

        order.attemptOutcomeResolved = true;
        order.resolvedQuality = CombatEquipmentQuality.Normal;
        order.completionEffectsPublished = true;
        order.outputOperationId = CombatEquipmentCraftOutputOutbox
            .FormatOperationId(order.orderId, order.qualityAttemptIndex);
        order.outputItemId = CombatItemDefinitions.ArrowItemId;
        order.outputQuantity = 20;
        const string outputDestination = "production-output:building:qa";
        bool firstOutput = CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                order,
                gateway,
                Vector2Int.zero,
                outputDestination,
                out string firstOutputFailure);
        int firstOutputCount = CountCommittedOutput(
            gateway,
            order.outputCommitId);
        bool replayOutput = CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                order,
                gateway,
                Vector2Int.zero,
                outputDestination,
                out string replayOutputFailure);
        int replayOutputCount = CountCommittedOutput(
            gateway,
            order.outputCommitId);
        if (!firstOutput
            || !order.outputPublished
            || firstOutputCount != 20
            || !replayOutput
            || replayOutputCount != 20)
        {
            return Fail(
                "generic output exact-once publication"
                + $":first={firstOutput}/{firstOutputFailure}"
                + $",published={order.outputPublished}"
                + $",firstCount={firstOutputCount}"
                + $",replay={replayOutput}/{replayOutputFailure}"
                + $",replayCount={replayOutputCount}"
                + $",commit={order.outputCommitId}");
        }

        if (CombatEquipmentCraftMaterialOutbox.TryAcknowledgeOutcome(
                order,
                Requirements,
                gateway,
                out _)
            || order.materialTransferAcknowledged)
        {
            return Fail("injected material acknowledgement fault");
        }

        PhysicalItemRestoreCandidateDispositionSnapshot candidate = new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            order.materialTransferRequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
        CombatEquipmentCraftOrderSaveData restored =
            JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                JsonUtility.ToJson(order));
        if (!CombatEquipmentCraftMaterialOutbox.TryCommitOrResume(
                restored,
                Requirements,
                gateway.GetAllStacks(),
                gateway,
                out _)
            || !CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                restored,
                gateway,
                Vector2Int.zero,
                outputDestination,
                out _)
            || !CombatEquipmentCraftMaterialOutbox.TryAcknowledgeOutcome(
                restored,
                Requirements,
                gateway,
                out _)
            || !restored.materialTransferAcknowledged
            || CountCommittedOutput(gateway, restored.outputCommitId) != 20
            || gateway.Quantity(lumberA) != 0
            || gateway.Quantity(lumberB) != 0
            || gateway.Quantity(iron) != 0)
        {
            return Fail("JSON replay acknowledgement/output recovery");
        }

        Validate(new[] { order }, candidate);
        if (!Reject(new[] { order })
            || !Reject(Array.Empty<CombatEquipmentCraftOrderSaveData>(), candidate)
            || !Reject(new[] { order }, Copy(candidate, mass: candidate.InputMassGrams + 1))
            || !Reject(new[] { order }, Copy(candidate, fingerprint: candidate.RequestFingerprint + ":bad")))
        {
            return Fail("incoming material receipt join rejection");
        }
        Validate(new[] { restored });
        return Reject(new[] { restored }, candidate)
            || Fail("terminal owner accepted stale incoming receipt");
    }

    private static bool Fail(string reason)
    {
        LastFailureReason = reason ?? "unknown";
        return false;
    }

    private static bool VerifyMissingInputAtomic(IDungeonItemCatalogProvider catalog)
    {
        FixtureGateway gateway = new(catalog);
        CombatEquipmentCraftOrderSaveData order = CreateOrder("missing");
        string lumber = gateway.Add("material:lumber", 2, order.materialDestinationId);
        return !CombatEquipmentCraftMaterialOutbox.TryCommitOrResume(
                order,
                Requirements,
                gateway.GetAllStacks(),
                gateway,
                out _)
            && gateway.Quantity(lumber) == 2
            && string.IsNullOrEmpty(order.materialTransferOperationId);
    }

    private static bool VerifyRejectedDismantleReplay(
        IDungeonItemCatalogProvider catalog)
    {
        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        CombatEquipmentCraftOrderSaveData order = CreateOrder("dismantle");
        order.definitionId = "weapon:dagger";
        order.dismantlingRejectedOutput = true;
        order.materialsReady = true;
        order.rejectedInstanceId = "equipment:rejected:qa";
        CombatEquipmentInstance rejectedEquipment = new()
        {
            instanceId = order.rejectedInstanceId,
            definitionId = order.definitionId,
            materialId = "material:iron",
            quality = CombatEquipmentQuality.Normal,
            durabilityRatio = 1f,
            powerCharge = 100f,
            loadedAmmunition = new LoadedAmmunitionBatch(),
            worldState = CombatEquipmentWorldState.Loose,
            ownerCharacterId = string.Empty,
            sourceStackId = string.Empty,
            evolution = new EquipmentEvolutionState(),
            moduleSlots = new List<EquipmentModuleSlotState>()
        };
        order.rejectedStackId = gateway.AddUnique(
            PhysicalItemIds.ForEquipment(order.definitionId),
            order.rejectedInstanceId,
            WorldItemStackState.FacilityOutputBuffer,
            "production-output:building:qa",
            new[] { EquipmentItemStateCodec.Encode(rejectedEquipment) });
        order.recoveryOutputs.Add(new CombatCraftRecoveryOutputSaveData
        {
            itemId = "material:iron-ingot",
            amount = 1
        });
        order.spawnedRecoveryAmounts.Add(0);
        long recoveryMass = new PhysicalItemMassQuery(catalog)
            .GetDefinitionUnitMass((ItemDefinitionId)"material:iron-ingot")
            .Value;
        order.rejectedRecoveryFactorsCaptured = true;
        order.rejectedRecoveryWorkerSkill = 0f;
        order.rejectedRecoverySalvageMultiplier = 1f;
        order.rejectedRecoveryProjected = true;
        order.rejectedRecoveryDesiredMassGrams = recoveryMass;
        order.rejectedRecoveryOutputMassGrams = recoveryMass;
        order.rejectedRecoverySourceDigest = "fixture:rejected-recovery";
        if (!CombatEquipmentRejectedDismantleOutbox.TryCommitOrResume(
                order,
                gateway,
                out _)
            || gateway.Quantity(order.rejectedStackId) != 0
            || !gateway.TryGetPendingBatchPhysicalDisposition(
                order.rejectedDismantleOperationId,
                out PhysicalItemBatchDispositionReceipt receipt))
        {
            return false;
        }
        string recoveryOperation = CombatEquipmentRejectedDismantleOutbox
            .FormatRecoveryOperationId(order.orderId, 0, 0);
        if (!CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                "material:iron-ingot",
                1,
                recoveryOperation,
                gateway,
                Vector2Int.zero,
                "production-output:building:qa",
                out string recoveryCommit,
                out _))
        {
            return false;
        }
        order.spawnedRecoveryAmounts[0] = 1;
        order.rejectedRecoveryPublished = true;
        if (CombatEquipmentRejectedDismantleOutbox.TryAcknowledgeRecovery(
                order,
                gateway,
                out _))
        {
            return false;
        }
        PhysicalItemRestoreCandidateDispositionSnapshot candidate = new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            order.rejectedDismantleRequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
        CombatEquipmentCraftOrderSaveData restored =
            JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                JsonUtility.ToJson(order));
        if (!CombatEquipmentRejectedDismantleOutbox.TryCommitOrResume(
                restored,
                gateway,
                out _)
            || !CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                "material:iron-ingot",
                1,
                recoveryOperation,
                gateway,
                Vector2Int.zero,
                "production-output:building:qa",
                out string replayCommit,
                out _)
            || !string.Equals(
                recoveryCommit,
                replayCommit,
                StringComparison.Ordinal)
            || !CombatEquipmentRejectedDismantleOutbox.TryAcknowledgeRecovery(
                restored,
                gateway,
                out _)
            || !restored.rejectedDismantleAcknowledged
            || CountCommittedOutput(gateway, recoveryCommit) != 1)
        {
            return false;
        }
        CandidateOutputQuery outputCandidate = CandidateOutputQuery.Capture(
            gateway,
            recoveryCommit,
            recoveryMass);
        CombatEquipmentCraftMaterialRestoreGuard.ValidateOwnerSet(
            new[] { order },
            GetRequirements,
            new CandidateQuery(new[] { candidate }),
            outputCandidate);
        CombatEquipmentCraftMaterialRestoreGuard.ValidateOwnerSet(
            new[] { restored },
            GetRequirements,
            new CandidateQuery(Array.Empty<
                PhysicalItemRestoreCandidateDispositionSnapshot>()),
            outputCandidate);
        return Reject(new[] { order })
            && Reject(Array.Empty<CombatEquipmentCraftOrderSaveData>(), candidate)
            && Reject(new[] { restored }, candidate);
    }

    private static bool VerifyCommonOutputTransaction()
    {
        GameObject facilityObject = new("CombatCommonOutputFixture");
        try
        {
            Facility facility = facilityObject.AddComponent<Facility>();
            facility.RestorePersistentIdentity(
                (BuildingInstanceId)"building:qa:combat-common-output");
            string facilityId = facility.RequirePersistentInstanceId().Value;

            ProductionDomainOutputPublicationDebugScenarios.DomainFixture unique =
                new(runtimeObject: facility);
            CombatEquipmentCraftOutputTransaction uniqueTransaction = new(
                new CombatEquipmentRuntimeStateStore(
                    new DungeonRuntimeAggregateRootStore()),
                new FixedBuildingWorld(facility),
                ProductionDomainOutputPublicationDebugScenarios.Service(unique),
                new FixedRejectedSaleDestination(
                    new Vector2Int(14, 6),
                    unique.Claims),
                UnavailableEquipmentPhysicalItemGateway.Instance);
            const string InstanceId =
                "item-instance:combat-common-output:dagger:001";
            CombatEquipmentInstance prepared = new()
            {
                instanceId = InstanceId,
                definitionId = "weapon:dagger",
                materialId = "material:iron",
                quality = CombatEquipmentQuality.Normal,
                durabilityRatio = 1f,
                worldState = CombatEquipmentWorldState.Loose,
                ownerCharacterId = string.Empty,
                sourceStackId = string.Empty,
                evolution = new EquipmentEvolutionState(),
                moduleSlots = new List<EquipmentModuleSlotState>()
            };
            CombatEquipmentCraftOrderSaveData equipmentOrder = new()
            {
                orderId = "combat-craft:common-equipment",
                definitionId = "weapon:dagger",
                facilityPersistentId = facilityId,
                qualityAttemptIndex = 0,
                attemptOutcomeResolved = true,
                resolvedQuality = CombatEquipmentQuality.Normal,
                minimumQuality = CraftsmanshipQualityTier.Good,
                rejectedDisposition = RejectedOutputDisposition.MarkForSale,
                outputItemId = PhysicalItemIds.ForEquipment("weapon:dagger"),
                outputQuantity = 1,
                outputCapability = FreezeCapability(
                    CombatEquipmentCraftOutputCapability.OutputLineId,
                    PhysicalItemIds.ForEquipment("weapon:dagger"),
                    ProductionOutputCapabilityIds.CombatEquipmentCraft,
                    ProductionOutputCapabilityIds.CombatEquipmentCraftVersion,
                    ProductionOutputCapabilityIds.CombatEquipmentStateCodec,
                    ProductionOutputCapabilityIds
                        .CombatEquipmentStateCodecVersion),
                outputInstanceId = InstanceId,
                outputPreparedComponent = EquipmentItemStateCodec.Encode(prepared),
                outputPhase = CombatEquipmentCraftOutputPhase
                    .ResolvedWaitingForPublication
            };
            ProductionDomainOutputPublicationResult equipmentCommit =
                uniqueTransaction.EnsureCommitted(equipmentOrder);
            if (!equipmentCommit.IsCommitted
                || equipmentOrder.outputPhase != CombatEquipmentCraftOutputPhase
                    .PublishedAwaitingInputAcknowledgement
                || equipmentOrder.outputPublication.stacks.Count != 1
                || equipmentOrder.outputPublication.stacks[0].itemInstanceId
                    != InstanceId
                || !equipmentOrder.outputPublication.releaseHasDestination
                || equipmentOrder.outputPublication.releaseDestinationId
                    != QualityRejectedOutputRules.MarketDestinationId
                || equipmentOrder.outputPublication.releaseDestinationX != 14
                || equipmentOrder.outputPublication.releaseDestinationY != 6
                || unique.Repository.EquipmentInstances.Count != 1
                || !uniqueTransaction.TryAcknowledgeAndRoute(
                    equipmentOrder,
                    markForSale: true,
                    out _)
                || !uniqueTransaction.TryAcknowledgeAndRoute(
                    equipmentOrder,
                    markForSale: true,
                    out _)
                || !equipmentOrder.outputPublication.outputAcknowledged
                || !equipmentOrder.outputMarketRouted
                || unique.Repository.EquipmentInstances.Count != 1
                || unique.Query.GetAllStacks().Any(stack =>
                    stack.State != WorldItemStackState.Loose
                    || stack.DestinationId
                        != QualityRejectedOutputRules.MarketDestinationId
                    || !stack.HasDestinationPosition
                    || stack.DestinationPosition != new Vector2Int(14, 6)))
            {
                return false;
            }

            ProductionDomainOutputPublicationDebugScenarios.DomainFixture ammo =
                new(runtimeObject: facility);
            CombatEquipmentCraftOutputTransaction ammoTransaction = new(
                new CombatEquipmentRuntimeStateStore(
                    new DungeonRuntimeAggregateRootStore()),
                new FixedBuildingWorld(facility),
                ProductionDomainOutputPublicationDebugScenarios.Service(ammo),
                new FixedRejectedSaleDestination(
                    new Vector2Int(14, 6),
                    ammo.Claims),
                UnavailableEquipmentPhysicalItemGateway.Instance);
            CombatEquipmentCraftOrderSaveData ammoOrder = new()
            {
                orderId = "combat-craft:common-ammunition",
                definitionId = CombatItemDefinitions.ArrowBundleRecipeId,
                facilityPersistentId = facilityId,
                qualityAttemptIndex = 0,
                attemptOutcomeResolved = true,
                resolvedQuality = CombatEquipmentQuality.Normal,
                outputItemId = "item:qa:a",
                outputQuantity = 5,
                outputCapability = FreezeCapability(
                    CombatAmmunitionCraftOutputCapability.OutputLineId,
                    "item:qa:a",
                    ProductionOutputCapabilityIds.CombatAmmunitionCraft,
                    ProductionOutputCapabilityIds.CombatAmmunitionCraftVersion,
                    ProductionOutputCapabilityIds.CombatAmmunitionStateCodec,
                    ProductionOutputCapabilityIds
                        .CombatAmmunitionStateCodecVersion),
                outputPhase = CombatEquipmentCraftOutputPhase
                    .ResolvedWaitingForPublication
            };
            ProductionDomainOutputPublicationResult ammoCommit =
                ammoTransaction.EnsureCommitted(ammoOrder);
            return ammoCommit.IsCommitted
                && ammoOrder.outputPublication.stacks.Count == 3
                && !ammoOrder.outputPublication.releaseHasDestination
                && ammoOrder.outputPublication.stacks.All(value =>
                    string.IsNullOrEmpty(value.itemInstanceId))
                && ammo.Repository.EquipmentInstances.Count == 0
                && ammoTransaction.TryAcknowledgeAndRoute(
                    ammoOrder,
                    markForSale: false,
                    out _)
                && ammoOrder.outputPublication.outputAcknowledged
                && !ammoOrder.outputMarketRouted
                && ammo.Query.GetAllStacks().All(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.IsNullOrEmpty(stack.DestinationId)
                    && !stack.HasDestinationPosition);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
        }
    }

    private static ProductionOutputCapabilitySaveData FreezeCapability(
        string outputLineId,
        string itemId,
        string capabilityId,
        int capabilityVersion,
        string codecId,
        int codecVersion) => ProductionOutputCapabilitySaveData.Freeze(
        new ProductionOutputCapabilityDescriptor(
            outputLineId,
            itemId,
            capabilityId,
            capabilityVersion,
            codecId,
            codecVersion,
            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                outputLineId,
                itemId,
                capabilityId,
                capabilityVersion,
                codecId,
                codecVersion)));

    private static CombatEquipmentCraftOrderSaveData CreateOrder(string suffix) =>
        new()
        {
            orderId = "combat-craft:" + suffix,
            definitionId = CombatItemDefinitions.ArrowBundleRecipeId,
            materialDestinationId = "facility-input:combat-craft:" + suffix,
            facilityPersistentId = "building:qa",
            requiredWork = 4f,
            craftWorkPerAttempt = 4f,
            qualityAttemptIndex = 0
        };

    private static int CountCommittedOutput(
        FixtureGateway gateway,
        string commitId) => gateway.GetAllStacks()
        .Where(stack => ProductionOutputCommitComponentCodec.Matches(
            stack.Components,
            commitId))
        .Sum(stack => stack.Quantity);

    private static void Validate(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> orders,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts) =>
        CombatEquipmentCraftMaterialRestoreGuard.ValidateOwnerSet(
            orders,
            GetRequirements,
            new CandidateQuery(receipts));

    private static bool Reject(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> orders,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts)
    {
        try
        {
            Validate(orders, receipts);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool GetRequirements(
        CombatEquipmentCraftOrderSaveData order,
        out IReadOnlyDictionary<string, int> requirements)
    {
        requirements = Requirements;
        return order != null;
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot Copy(
        PhysicalItemRestoreCandidateDispositionSnapshot value,
        string fingerprint = null,
        long? mass = null) => new(
        value.Kind,
        value.OperationId,
        value.ReasonCode,
        fingerprint ?? value.RequestFingerprint,
        value.SourceStackIds,
        value.Quantity,
        mass ?? value.InputMassGrams,
        value.CommitId);

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            values;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] values) =>
            this.values = values ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

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

    private sealed class CandidateOutputQuery :
        IPhysicalItemRestoreCandidateOutputQuery
    {
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateOutputSnapshot> values;

        private CandidateOutputQuery(
            IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot> values)
        {
            this.values = values;
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>
            CommittedOutputs => values;

        public bool TryGetCommittedOutput(
            string commitId,
            out IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot> outputs)
        {
            PhysicalItemRestoreCandidateOutputSnapshot[] matches = values
                .Where(value => value != null && string.Equals(
                    value.CommitId,
                    commitId,
                    StringComparison.Ordinal))
                .ToArray();
            outputs = Array.AsReadOnly(matches);
            return matches.Length > 0;
        }

        internal static CandidateOutputQuery Capture(
            FixtureGateway gateway,
            string commitId,
            long massGrams)
        {
            WorldItemStackSnapshot[] stacks = gateway.GetAllStacks()
                .Where(value => value != null
                    && ProductionOutputCommitComponentCodec.Matches(
                        value.Components,
                        commitId))
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            return new CandidateOutputQuery(Array.AsReadOnly(stacks
                .Select(value => new PhysicalItemRestoreCandidateOutputSnapshot(
                    commitId,
                    value.StackId,
                    value.ItemId,
                    value.Quantity,
                    massGrams,
                    value.State,
                    value.Position,
                    value.DestinationId))
                .ToArray()));
        }
    }

    private sealed class FixedRejectedSaleDestination :
        IQualityRejectedSaleDestinationAuthority
    {
        private readonly Vector2Int position;
        private readonly IFacilityBufferDestinationClaimCommand claims;

        internal FixedRejectedSaleDestination(
            Vector2Int position,
            IFacilityBufferDestinationClaimCommand claims)
        {
            this.position = position;
            this.claims = claims;
        }

        public bool TryEnsureTarget(
            out FacilityBufferAcknowledgedOutputReleaseTarget target,
            out string failureReason)
        {
            FacilityBufferDestinationClaim claim = new(
                QualityRejectedOutputRules.MarketDestinationId,
                position,
                QualityRejectedSaleDestinationAuthority.OwnerDomain,
                QualityRejectedSaleDestinationAuthority.OwnerOperationId,
                null,
                FacilityBufferDestinationAnchorKind.ReservedTarget);
            if (!claims.TryReplaceOwnedClaims(
                    QualityRejectedSaleDestinationAuthority.OwnerDomain,
                    new[] { claim },
                    out _,
                    out failureReason))
            {
                target = default;
                return false;
            }
            target = new FacilityBufferAcknowledgedOutputReleaseTarget(
                QualityRejectedOutputRules.MarketDestinationId,
                position);
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FixedBuildingWorld : IBuildingWorldQuery
    {
        internal FixedBuildingWorld(params BuildableObject[] buildings) =>
            Buildings = buildings ?? Array.Empty<BuildableObject>();

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    private sealed class FixtureGateway : IEquipmentPhysicalItemGateway
    {
        private readonly WorldItemRepository repository;
        private readonly WorldItemQueryService query;
        private readonly IPhysicalItemBatchDispositionService dispositions;

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
        }

        internal bool FailNextAcknowledgement { get; set; }

        internal string Add(string itemId, int quantity, string destinationId) =>
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                quantity,
                WorldItemStackState.FacilityBuffer,
                destinationId: destinationId);

        internal string AddUnique(
            string itemId,
            string itemInstanceId,
            WorldItemStackState state,
            string destinationId,
            IReadOnlyList<ItemInstanceComponentSaveData> components) =>
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                1,
                state,
                destinationId: destinationId,
                itemInstanceId: itemInstanceId,
                components: components);

        internal int Quantity(string stackId) =>
            repository.GetEditorTestQuantity(stackId);

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            query.GetAllStacks();

        public bool SpawnItemAtWithComponents(
            string itemId,
            int amount,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            IReadOnlyList<ItemInstanceComponentSaveData> components,
            out int spawned)
        {
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                amount,
                state,
                destinationId: destinationId,
                position: position,
                components: components);
            spawned = amount;
            return true;
        }

        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => dispositions.TryCommitPending(
            inputs, kind, operationId, reasonCode, out receipt, out failureReason);

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

        public bool SpawnItemAt(string itemId, int amount, Vector2Int position,
            WorldItemStackState state, string destinationId, out int spawned) =>
            SpawnItemAtWithComponents(itemId, amount, position, state,
                destinationId, Array.Empty<ItemInstanceComponentSaveData>(), out spawned);
        public bool SpawnExistingUniqueItemAt(string itemId, ItemInstanceId itemInstanceId,
            Vector2Int position, WorldItemStackState state, string destinationId,
            out string stackId)
        {
            stackId = WorldItemRepositoryEditorAccess.AddStack(repository, itemId, 1,
                state, destinationId: destinationId, position: position,
                itemInstanceId: itemInstanceId.Value);
            return true;
        }
        public bool TryAbsorbUniqueItemStack(string stackId, ItemInstanceId expectedInstanceId) => false;
        public bool TryRequestItemDelivery(string itemId, int amount,
            Vector2Int destinationPosition, string destinationId, out int requested,
            out string failureReason) { requested = 0; failureReason = "not-used"; return false; }
        public bool TryConsumeFacilityItemBuffer(string destinationId,
            IReadOnlyDictionary<string, int> costs, out string failureReason)
        { failureReason = "forbidden"; return false; }
        public bool DeleteStack(string stackId) =>
            WorldItemRepositoryEditorAccess.TryRemoveStack(repository, stackId);
        public bool TryConsumeStackQuantity(string stackId, int quantity,
            out WorldItemStackSnapshot consumed) { consumed = null; return false; }
        public bool TryCommitBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind, string operationId, string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt, out string failureReason) =>
            dispositions.TryCommit(inputs, kind, operationId, reasonCode,
                out receipt, out failureReason);
        public bool TrySetInstanceComponent(string stackId,
            ItemInstanceComponentSaveData component) => false;
        public bool TryRemoveInstanceComponent(string stackId, string componentTypeId) => false;
        public int ReleaseStacksByDestination(string destinationId,
            Vector2Int releasePosition) => 0;
    }
}
