#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CombatEquipmentRepairTerminalAuthorityDebugScenarios
{
    private const int WipQuantity = 2;
    private const long WipMassGrams = 2_000L;

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Combat Repair Terminal Authority And Router")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_COMBAT_REPAIR_TERMINAL_AUTHORITY_ROUTER=PASS");
    }

    public static void RunAll()
    {
        VerifyRepairWipRowPrecedesRemoval();
        VerifyRepairZeroWipRemovalAndReceiptGc();
        VerifyRepairExactSourceDriftIsRejected();
        VerifyRouterDispatchesCraftAndRepairOwners();
        VerifyRouterRejectsDuplicateCommitCapture();
    }

    private static void VerifyRepairWipRowPrecedesRemoval()
    {
        using Fixture fixture = new();
        CombatEquipmentRepairOrder order = fixture.RepairOrder("wip", true);
        fixture.AddRepair(order);
        Require(fixture.Repair.TryCaptureLiveSourceForPreparation(
                RepairOwner(order.orderId),
                out CombatEquipmentTerminalPreparedSource prepared,
                out string captureFailure),
            "Repair WIP source capture failed: " + captureFailure);

        CombatEquipmentTerminalFrozenSubject source = prepared.Source;
        CombatEquipmentTerminalWipLossReceiptSaveData wip =
            CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(source);
        Require(source.WipInputQuantity == WipQuantity
            && source.WipInputMassGrams == WipMassGrams
            && source.DeclaredLossMassGrams == WipMassGrams
            && fixture.Repair.TryPublishWipLossReceipt(
                    wip,
                    EmptyEvidence()).Status ==
                CombatEquipmentTerminalEffectStatus.Applied,
            "Repair WIP row publication failed.");

        CombatEquipmentMaintenanceSaveData rowFirst = fixture.RepairSave();
        Require(rowFirst.orders.Any(value => value.orderId == order.orderId)
            && rowFirst.repairTerminalEffects.SingleOrDefault(
                value => value.sourceId == order.orderId) is
                CombatEquipmentRepairTerminalEffectSaveData effect
            && effect.phase == CombatEquipmentRepairTerminalEffectPhase
                .WipPreparedAwaitingOwnerDispositionAcknowledgement,
            "Repair WIP acknowledgement advanced before its durable row existed.");

        CombatEquipmentTerminalSourceRemovalReceiptSaveData removal =
            CombatEquipmentTerminalDrainCanonical.CreateSourceRemovalReceipt(
                source);
        Require(fixture.Repair.TryRemoveExactSource(
                    source,
                    removal,
                    EmptyEvidence()).Status ==
                CombatEquipmentTerminalEffectStatus.Applied,
            "Repair WIP source removal failed after row-first publication.");
        CombatEquipmentMaintenanceSaveData removed = fixture.RepairSave();
        Require(removed.orders.All(value => value.orderId != order.orderId)
            && removed.repairTerminalEffects.Single(
                value => value.sourceId == order.orderId).phase ==
                CombatEquipmentRepairTerminalEffectPhase.SourceRemoved
            && fixture.Claims.CaptureClaims().Count == 0
            && fixture.Capacities.CaptureAuthorityProfiles().Count == 0,
            "Repair terminal removal did not close the buffer and source atomically.");
    }

    private static void VerifyRepairZeroWipRemovalAndReceiptGc()
    {
        using Fixture fixture = new();
        CombatEquipmentRepairOrder order = fixture.RepairOrder("zero", false);
        fixture.AddRepair(order);
        Require(fixture.Repair.TryCaptureLiveSourceForPreparation(
                RepairOwner(order.orderId),
                out CombatEquipmentTerminalPreparedSource prepared,
                out string captureFailure),
            "Zero-WIP repair source capture failed: " + captureFailure);
        CombatEquipmentTerminalFrozenSubject source = prepared.Source;
        Require(source.WipInputQuantity == 0
            && source.WipInputMassGrams == 0L
            && source.PendingInputQuantity == 0
            && source.PendingInputMassGrams == 0L,
            "Zero-WIP repair source acquired unexpected physical mass.");

        CombatEquipmentTerminalSourceRemovalReceiptSaveData removal =
            CombatEquipmentTerminalDrainCanonical.CreateSourceRemovalReceipt(
                source);
        Require(fixture.Repair.TryRemoveExactSource(
                    source,
                    removal,
                    EmptyEvidence()).Status ==
                CombatEquipmentTerminalEffectStatus.Applied
            && fixture.Repair.TryCaptureSourceRemovalReceipt(
                removal.commitId,
                out CombatEquipmentTerminalSourceRemovalReceiptSaveData actual)
            && CombatEquipmentTerminalDrainCanonical.RemovalReceiptEquals(
                actual,
                removal),
            "Zero-WIP repair removal receipt was not published exactly once.");
        Require(fixture.Repair.TryGarbageCollectReceipts(
                    source,
                    string.Empty,
                    removal.receiptFingerprint).Status ==
                CombatEquipmentTerminalEffectStatus.Applied
            && fixture.RepairSave().repairTerminalEffects.Count == 0,
            "Repair source-removal receipt GC left the same-aggregate row alive.");
    }

    private static void VerifyRepairExactSourceDriftIsRejected()
    {
        using Fixture fixture = new();
        CombatEquipmentRepairOrder order = fixture.RepairOrder("drift", false);
        fixture.AddRepair(order);
        Require(fixture.Repair.TryCaptureLiveSourceForPreparation(
                RepairOwner(order.orderId),
                out CombatEquipmentTerminalPreparedSource prepared,
                out string captureFailure),
            "Repair drift source capture failed: " + captureFailure);
        CombatEquipmentRepairOrder drifted = order.Clone();
        drifted.completedWork += 1f;
        fixture.ReplaceRepairOrder(drifted);

        CombatEquipmentTerminalFrozenSubject source = prepared.Source;
        CombatEquipmentTerminalSourceRemovalReceiptSaveData removal =
            CombatEquipmentTerminalDrainCanonical.CreateSourceRemovalReceipt(
                source);
        CombatEquipmentTerminalEffectResult result = fixture.Repair
            .TryRemoveExactSource(source, removal, EmptyEvidence());
        CombatEquipmentMaintenanceSaveData after = fixture.RepairSave();
        Require(result.Status == CombatEquipmentTerminalEffectStatus.Conflict
            && after.orders.Any(value => value.orderId == order.orderId)
            && after.repairTerminalEffects.Count == 0,
            "A drifted repair order was removed or partially terminalized.");
    }

    private static void VerifyRouterDispatchesCraftAndRepairOwners()
    {
        using Fixture fixture = new();
        CombatEquipmentCraftOrderSaveData craft = fixture.CraftOrder(
            "router-craft",
            false);
        CombatEquipmentRepairOrder repair = fixture.RepairOrder(
            "router-repair",
            false);
        fixture.AddCraft(craft);
        fixture.AddRepair(repair);

        Require(fixture.Router.TryCaptureLiveSourceForPreparation(
                CraftOwner(craft.orderId),
                out CombatEquipmentTerminalPreparedSource craftPrepared,
                out string craftFailure)
            && craftPrepared.Source.SourceKind ==
                CombatEquipmentTerminalSourceKind.CraftOrder,
            "Router failed craft-owner dispatch: " + craftFailure);
        Require(fixture.Router.TryCaptureLiveSourceForPreparation(
                RepairOwner(repair.orderId),
                out CombatEquipmentTerminalPreparedSource repairPrepared,
                out string repairFailure)
            && repairPrepared.Source.SourceKind ==
                CombatEquipmentTerminalSourceKind.RepairOrder,
            "Router failed repair-owner dispatch: " + repairFailure);
        Require(!fixture.Router.TryCaptureLiveSource(
                "generic-bill:qa:unsupported",
                out _,
                out string unsupported)
            && string.Equals(
                unsupported,
                "combat-terminal-router-owner-kind-unsupported",
                StringComparison.Ordinal),
            "Router did not fail loudly for an unsupported owner kind.");
    }

    private static void VerifyRouterRejectsDuplicateCommitCapture()
    {
        using Fixture fixture = new();
        CombatEquipmentCraftOrderSaveData craft = fixture.CraftOrder(
            "duplicate-craft",
            true);
        CombatEquipmentRepairOrder repair = fixture.RepairOrder(
            "duplicate-repair",
            true);
        fixture.AddCraft(craft);
        fixture.AddRepair(repair);

        Require(fixture.Craft.TryCaptureLiveSourceForPreparation(
                CraftOwner(craft.orderId),
                out CombatEquipmentTerminalPreparedSource craftPrepared,
                out string craftFailure),
            "Duplicate fixture craft capture failed: " + craftFailure);
        Require(fixture.Repair.TryCaptureLiveSourceForPreparation(
                RepairOwner(repair.orderId),
                out CombatEquipmentTerminalPreparedSource repairPrepared,
                out string repairFailure),
            "Duplicate fixture repair capture failed: " + repairFailure);
        CombatEquipmentTerminalWipLossReceiptSaveData craftWip =
            CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(
                craftPrepared.Source);
        CombatEquipmentTerminalWipLossReceiptSaveData repairWip =
            CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(
                repairPrepared.Source);
        Require(fixture.Craft.TryPublishWipLossReceipt(
                    craftWip,
                    EmptyEvidence()).Status ==
                CombatEquipmentTerminalEffectStatus.Applied
            && fixture.Repair.TryPublishWipLossReceipt(
                    repairWip,
                    EmptyEvidence()).Status ==
                CombatEquipmentTerminalEffectStatus.Applied,
            "Duplicate fixture could not publish both valid WIP rows.");

        CombatEquipmentCraftTerminalEffectSaveData craftRow =
            CombatEquipmentCraftTerminalAuthorityEditorAccess.CaptureEffects(
                    fixture.CraftStore)
                .Single(value => value.sourceId == craft.orderId);
        CombatEquipmentRepairTerminalEffectSaveData repairRow = fixture
            .RepairSave().repairTerminalEffects.Single(
                value => value.sourceId == repair.orderId);
        repairRow.sourceFingerprint = craftRow.sourceFingerprint;
        repairRow.wipLossCommitId = craftRow.wipLossCommitId;
        CombatEquipmentTerminalWipLossReceiptSaveData corruptProjection = new()
        {
            commitId = repairRow.wipLossCommitId,
            sourceKind = CombatEquipmentTerminalSourceKind.RepairOrder,
            ownerStableId = repairRow.ownerStableId,
            sourceId = repairRow.sourceId,
            facilityId = repairRow.facilityId,
            sourceFingerprint = repairRow.sourceFingerprint,
            inputQuantity = repairRow.wipInputQuantity,
            inputMassGrams = repairRow.wipInputMassGrams,
            committedOutputMassGrams = repairRow.committedOutputMassGrams,
            declaredLossMassGrams = repairRow.declaredLossMassGrams,
            reason = (ProductionWipTerminalReason)repairRow.terminalReason,
            lossKind = (ProductionWipTerminalLossKind)repairRow.lossKind
        };
        corruptProjection.receiptFingerprint =
            CreateWipReceiptFingerprint(corruptProjection);
        fixture.RewriteRepairWipCapture(
            repair.orderId,
            repairRow.sourceFingerprint,
            repairRow.wipLossCommitId,
            corruptProjection.receiptFingerprint);

        Require(fixture.Craft.TryCaptureWipLossReceipt(
                craftRow.wipLossCommitId,
                out _)
            && fixture.Repair.TryCaptureWipLossReceipt(
                craftRow.wipLossCommitId,
                out _)
            && !fixture.Router.TryCaptureWipLossReceipt(
                craftRow.wipLossCommitId,
                out _),
            "Router accepted an ambiguous commit owned by both aggregates.");
    }

    private static string CreateWipReceiptFingerprint(
        CombatEquipmentTerminalWipLossReceiptSaveData receipt)
    {
        MethodInfo method = typeof(CombatEquipmentTerminalDrainCanonical)
            .GetMethod(
                "CreateWipLossReceiptFingerprint",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Canonical WIP fingerprint method is missing.");
        return method.Invoke(null, new object[] { receipt }) as string
            ?? throw new InvalidOperationException(
                "Canonical WIP fingerprint invocation returned no digest.");
    }

    private static string CraftOwner(string orderId) =>
        ProductionFacilityDestructiveDrainOwnerStableIds.CombatCraftOrder(
            orderId);

    private static string RepairOwner(string orderId) =>
        ProductionFacilityDestructiveDrainOwnerStableIds.EquipmentRepairOrder(
            orderId);

    private static CombatEquipmentTerminalInputDispositionEvidence
        EmptyEvidence() => new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0L);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DungeonRuntimeAggregateRootStore repairRoot = new();
        private readonly GameObject buildingObject;
        private readonly BuildingSO buildingData;
        private readonly CombatEquipmentInstance equipmentInstance;

        internal Fixture()
        {
            buildingData = ScriptableObject.CreateInstance<BuildingSO>();
            buildingData.hideFlags = HideFlags.HideAndDontSave;
            buildingData.id = 99127;
            buildingData.objectName = "QA Terminal Repair Bench";
            buildingData.width = 1;
            buildingData.height = 1;
            buildingData.layer = GridLayer.Building;
            buildingData.category = BuildingCategory.Production;
            buildingData.unlocked = true;
            buildingData.Facility = new FacilityData
            {
                roles = FacilityRole.Logistics,
                capacity = 1,
                useDuration = 1f,
                requiredWorkers = 1,
                disabledWhenDamaged = true
            };
            buildingData.Facility.SetSupportedWorkTypeIds(
                new[] { BuiltInWorkTypeIds.Repair });
            FacilityData facilitySettings = buildingData.Facility;
            buildingData.ReplaceAbilities(new BuildingAbilityCollection());
            buildingData.AbilityModules.Add(new BuildingFacilityAbility
            {
                settings = facilitySettings
            });
            buildingData.AbilityModules.Add(
                new BuildingEquipmentMaintenanceAbility());

            buildingObject = new GameObject("QA Terminal Repair Bench");
            BuildableObject building =
                buildingObject.AddComponent<BuildableObject>();
            building.ConstructPersistentIdentity(
                new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(building);
            building.Initialization(buildingData, new Vector2Int(4, 6));
            Building = building;

            equipmentInstance = new CombatEquipmentInstance
            {
                instanceId = "equipment-instance:qa:terminal-repair",
                definitionId = "weapon:qa:terminal-repair",
                materialId = "material:qa:iron",
                durabilityRatio = 0.5f,
                worldState = CombatEquipmentWorldState.MaintenanceBuffer,
                sourceStackId = "stack:qa:terminal-repair-equipment"
            };

            FixedMass mass = new();
            ICombatEquipmentRuntime equipment = CreateProxy<
                ICombatEquipmentRuntime>((method, args) =>
            {
                if (method.Name == "get_Instances")
                    return new[] { equipmentInstance };
                if (method.Name == "get_ModuleInstances")
                    return Array.Empty<EquipmentModuleInstance>();
                if (method.Name == "TryGetInstance")
                {
                    bool found = string.Equals(
                        args[0] as string,
                        equipmentInstance.instanceId,
                        StringComparison.Ordinal);
                    args[1] = found ? equipmentInstance : null;
                    return found;
                }
                return DefaultValue(method.ReturnType);
            });
            IWorldItemStackRuntime items = CreateProxy<
                IWorldItemStackRuntime>((method, _) =>
                method.Name == "get_MassQuery"
                    ? mass
                    : DefaultValue(method.ReturnType));
            ICharacterAiWorldRegistry world = CreateProxy<
                ICharacterAiWorldRegistry>((method, _) =>
                method.Name == "get_Buildings"
                    ? new[] { Building }
                    : DefaultValue(method.ReturnType));

            Claims = new FacilityBufferDestinationClaimRegistry();
            Capacities = new FacilityBufferMassAdmissionService(
                Claims,
                new EmptyOccupancy());
            FacilityBufferDestinationLifecycleService lifecycle = new(
                Claims,
                Claims,
                Capacities,
                Capacities);
            EquipmentMaintenanceItemServices itemServices = new(
                equipment,
                CreateProxy<ICombatEquipmentCatalog>(),
                CreateProxy<IResourceEconomyContentCatalog>(),
                items,
                new NoPendingDisposition(),
                CreateProxy<ICombatEquipmentPickupRuntime>(),
                Claims,
                lifecycle,
                Capacities);
            EquipmentMaintenanceWorldServices worldServices = new(
                world,
                CreateProxy<IDefenseEngagementStore>((method, _) =>
                    method.Name == "get_Engagements"
                        ? Array.Empty<DefenseEngagement>()
                        : DefaultValue(method.ReturnType)));
            EquipmentMaintenanceClockServices clocks = new(
                CreateProxy<IGameClock>(),
                CreateProxy<IUiClock>());
            Maintenance = new EquipmentMaintenancePolicyRuntime(
                itemServices,
                worldServices,
                clocks,
                repairRoot);
            InputDrain = new EmptyInputDrain();
            Repair = new CombatEquipmentRepairTerminalAuthority(
                Maintenance,
                InputDrain);

            CraftStore = new CombatEquipmentRuntimeStateStore(
                new DungeonRuntimeAggregateRootStore());
            Craft = new CombatEquipmentCraftTerminalAuthority(
                CraftStore,
                InputDrain,
                UnavailableEquipmentPhysicalItemGateway.Instance,
                mass);
            Router = new CombatEquipmentTerminalSourceAuthorityRouter(
                Craft,
                Repair);
        }

        internal BuildableObject Building { get; }
        internal EquipmentMaintenancePolicyRuntime Maintenance { get; }
        internal EmptyInputDrain InputDrain { get; }
        internal CombatEquipmentRepairTerminalAuthority Repair { get; }
        internal CombatEquipmentRuntimeStateStore CraftStore { get; }
        internal CombatEquipmentCraftTerminalAuthority Craft { get; }
        internal CombatEquipmentTerminalSourceAuthorityRouter Router { get; }
        internal FacilityBufferDestinationClaimRegistry Claims { get; }
        internal FacilityBufferMassAdmissionService Capacities { get; }

        internal CombatEquipmentRepairOrder RepairOrder(
            string suffix,
            bool withWip)
        {
            CombatEquipmentRepairOrder order = new()
            {
                orderId = "equipment-repair:qa:terminal:" + suffix,
                equipmentInstanceId = equipmentInstance.instanceId,
                originalOwnerCharacterId = "character:qa:smith",
                facilityBuildingId = Building.PersistentInstanceId.Value,
                materialItemId = "resource:qa:iron",
                requiredMaterialAmount = WipQuantity,
                requiredWork = 10f,
                completedWork = 4f,
                targetDurability = 0.9f,
                state = CombatEquipmentRepairOrderState.InProgress
            };
            if (!withWip)
                return order;

            order.materialsConsumed = true;
            order.materialTransferOperationId =
                EquipmentRepairMaterialOutbox.FormatOperationId(order.orderId);
            order.materialTransferCommitId =
                "physical-batch-disposition:0:"
                + order.materialTransferOperationId + ":2:2000";
            order.materialTransferInputs = new List<
                EquipmentRepairMaterialTransferInput>
            {
                new()
                {
                    itemId = order.materialItemId,
                    sourceStackId = "stack:qa:repair-material:" + suffix,
                    quantity = WipQuantity
                }
            };
            order.materialTransferRequestFingerprint =
                EquipmentRepairMaterialOutbox.CreateRequestFingerprint(
                    order.materialTransferInputs);
            order.materialTransferMassGrams = WipMassGrams;
            order.repairEquipmentSourceStackId =
                equipmentInstance.sourceStackId;
            order.repairDurabilityBefore = 0.5f;
            order.repairDurabilityAfter = 0.9f;
            return order;
        }

        internal CombatEquipmentCraftOrderSaveData CraftOrder(
            string suffix,
            bool withWip)
        {
            CombatEquipmentCraftOrderSaveData order = new()
            {
                orderId = "craft:qa:terminal-router:" + suffix,
                definitionId = "weapon:qa:terminal-router",
                materialId = "material:qa:iron",
                requiredWork = 10f,
                completedWork = 0f,
                facilityPersistentId = Building.PersistentInstanceId.Value,
                materialDestinationId = string.Empty,
                destinationX = Building.centerPos.x,
                destinationY = Building.centerPos.y
            };
            if (!withWip)
                return order;

            order.materialsReady = true;
            order.materialTransferOperationId = CombatEquipmentCraftMaterialOutbox
                .FormatOperationId(order.orderId, order.qualityAttemptIndex);
            order.materialTransferInputs = new List<
                CombatEquipmentCraftMaterialTransferInput>
            {
                new()
                {
                    itemId = "resource:qa:iron",
                    sourceStackId = "stack:qa:craft-material:" + suffix,
                    quantity = WipQuantity
                }
            };
            order.materialTransferMassGrams = WipMassGrams;
            order.materialTransferRequestFingerprint =
                CombatEquipmentCraftMaterialOutbox.CreateRequestFingerprint(
                    order.materialTransferInputs);
            order.materialTransferCommitId =
                $"physical-batch-disposition:"
                + $"{(int)PhysicalItemDispositionKind.Transfer}:"
                + $"{order.materialTransferOperationId}:2:2000";
            return order;
        }

        internal void AddRepair(CombatEquipmentRepairOrder order)
        {
            object state = CaptureRepairStateObject();
            GetStateMap(state, "Orders").Add(order.orderId, order.Clone());
            ReplaceRepairStateObject(state);
        }

        internal void AddCraft(CombatEquipmentCraftOrderSaveData order) =>
            CombatEquipmentCraftTerminalAuthorityEditorAccess.AddOrder(
                CraftStore,
                order);

        internal CombatEquipmentMaintenanceSaveData RepairSave() =>
            Maintenance.Capture();

        internal void ReplaceRepairOrder(CombatEquipmentRepairOrder order)
        {
            object state = CaptureRepairStateObject();
            System.Collections.IDictionary orders = GetStateMap(
                state,
                "Orders");
            if (!orders.Contains(order.orderId))
                throw new InvalidOperationException(
                    "Repair terminal fixture order is missing.");
            orders[order.orderId] = order.Clone();
            ReplaceRepairStateObject(state);
        }

        internal void RewriteRepairWipCapture(
            string sourceId,
            string sourceFingerprint,
            string commitId,
            string receiptFingerprint)
        {
            object state = CaptureRepairStateObject();
            System.Collections.IDictionary effects = GetStateMap(
                state,
                "TerminalEffects");
            if (!(effects[sourceId] is
                    CombatEquipmentRepairTerminalEffectSaveData row))
            {
                throw new InvalidOperationException(
                    "Repair terminal fixture effect is missing.");
            }
            row.sourceFingerprint = sourceFingerprint;
            row.wipLossCommitId = commitId;
            row.wipLossReceiptFingerprint = receiptFingerprint;
            ReplaceRepairStateObject(state);
        }

        private object CaptureRepairStateObject()
        {
            MethodInfo capture = typeof(EquipmentMaintenancePolicyRuntime)
                .GetMethod(
                    "CaptureTerminalState",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "Repair terminal state capture method is missing.");
            return capture.Invoke(Maintenance, null)
                ?? throw new InvalidOperationException(
                    "Repair terminal state capture returned null.");
        }

        private static System.Collections.IDictionary GetStateMap(
            object state,
            string propertyName)
        {
            PropertyInfo property = state.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "Repair terminal state map is missing: " + propertyName);
            return property.GetValue(state) as System.Collections.IDictionary
                ?? throw new InvalidOperationException(
                    "Repair terminal state map is not a dictionary: "
                    + propertyName);
        }

        private void ReplaceRepairStateObject(object state)
        {
            MethodInfo replace = typeof(DungeonRuntimeAggregateRootStore)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(method => method.Name == "Replace"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 1)
                .MakeGenericMethod(state.GetType());
            replace.Invoke(repairRoot, new[] { state });
        }

        public void Dispose()
        {
            if (buildingObject != null)
                UnityEngine.Object.DestroyImmediate(buildingObject);
            if (buildingData != null)
                UnityEngine.Object.DestroyImmediate(buildingData);
        }
    }

    private sealed class EmptyInputDrain :
        IProductionInputDestinationCustodyDrainService
    {
        private static readonly string EmptyOwnership = new('9', 64);

        public bool RequiresImmediateRecoveryBeforeGameplayTick => true;

        public bool TryCaptureSource(
            string sourceDestinationId,
            out ProductionInputDestinationCustodySourceSnapshot snapshot,
            out string failureReason)
        {
            snapshot = new ProductionInputDestinationCustodySourceSnapshot(
                sourceDestinationId,
                1L,
                EmptyOwnership,
                Array.Empty<ProductionInputDestinationDrainStackSaveData>(),
                Array.Empty<ProductionInputDestinationDrainOperationSaveData>(),
                Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
                0,
                0L);
            failureReason = string.Empty;
            return ProductionInputDestinationCustodyDrainContract
                .IsValidSourceSnapshot(snapshot);
        }

        public bool TryBuildRequest(
            string parentOperationId,
            string stepOperationId,
            string ownerStableId,
            string billId,
            string facilityId,
            Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            ProductionInputDestinationCustodySourceSnapshot snapshot,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        {
            request = null;
            failureReason = "repair-terminal-fixture-unexpected-build";
            return false;
        }

        public bool TryCaptureRequest(
            string parentOperationId,
            string stepOperationId,
            string ownerStableId,
            string billId,
            string facilityId,
            string sourceDestinationId,
            Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        {
            request = null;
            failureReason = "repair-terminal-fixture-unexpected-capture";
            return false;
        }

        public ProductionInputDestinationCustodyDrainResult TryPrepare(
            ProductionInputDestinationCustodyDrainRequest request) =>
            Unexpected();
        public ProductionInputDestinationCustodyDrainResult TryCommit(
            string stepOperationId,
            string requestFingerprint) => Unexpected();
        public ProductionInputDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint) => Unexpected();
        public ProductionInputDestinationCustodyDrainResult TryGarbageCollect(
            string stepOperationId,
            string receiptFingerprint) => Unexpected();
        public bool TryCapture(
            string stepOperationId,
            out ProductionInputDestinationCustodyDrainSaveData record)
        {
            record = null;
            return false;
        }

        private static ProductionInputDestinationCustodyDrainResult
            Unexpected() => new(
                ProductionInputDestinationCustodyDrainStatus.Conflict,
                string.Empty,
                string.Empty,
                "repair-terminal-fixture-unexpected-operation");
    }

    private sealed class NoPendingDisposition :
        IPhysicalItemBatchDispositionService
    {
        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => Unexpected(
                out receipt,
                out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => Unexpected(
                out receipt,
                out failureReason);

        public bool Acknowledge(
            string commitId,
            out string failureReason)
        {
            failureReason = "repair-terminal-fixture-no-pending-receipt";
            return false;
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt)
        {
            receipt = default;
            return false;
        }

        private static bool Unexpected(
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = "repair-terminal-fixture-unexpected-commit";
            return false;
        }
    }

    private sealed class EmptyOccupancy :
        IFacilityBufferPhysicalOccupancyQuery
    {
        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(0L, 0L);

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "repair-terminal-fixture-no-lot";
            return false;
        }
    }

    private sealed class FixedMass : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 1L;
        public PhysicalMassGrams GetDefinitionUnitMass(
            ItemDefinitionId itemId) => new(500L);
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => new(2_000L);
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => new(2_000L);
        public PhysicalMassGrams GetStackTotalMass(
            PhysicalItemLotSnapshot lot) => new(
                Math.Max(1, lot.Quantity) * 2_000L);
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new(Math.Max(1, quantity) * 2_000L);
    }

    public class ConfigurableDispatchProxy : DispatchProxy
    {
        public Func<MethodInfo, object[], object> Handler { get; set; }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                Type parameterType = parameters[index].ParameterType;
                if (!parameterType.IsByRef)
                    continue;
                Type elementType = parameterType.GetElementType();
                args[index] = DefaultValue(elementType);
            }
            return Handler?.Invoke(targetMethod, args)
                ?? DefaultValue(targetMethod.ReturnType);
        }
    }

    private static T CreateProxy<T>(
        Func<MethodInfo, object[], object> handler = null)
        where T : class
    {
        T proxy = DispatchProxy.Create<T, ConfigurableDispatchProxy>();
        ((ConfigurableDispatchProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static object DefaultValue(Type type) =>
        type == typeof(void)
            ? null
            : type != null && type.IsValueType
                ? Activator.CreateInstance(type)
                : null;
}
#endif
