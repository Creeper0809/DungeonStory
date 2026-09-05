using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Owns equipment crafting orders, concrete material policies, unlock checks,
/// and creation of repository-owned equipment instances.
/// </summary>
public sealed class CombatEquipmentCraftingRuntime
{
    private readonly ICombatEquipmentCatalog catalog;
    private readonly IItemInstanceRepository itemInstances;
    private readonly IResourceEconomyContentCatalog materialCatalog;
    private readonly BlueprintResearchRuntime research;
    private readonly IEquipmentPhysicalItemGateway physicalItems;
    private readonly CombatEquipmentStatProjector statProjector;
    private readonly CombatEquipmentPhysicalStateWriter physicalState;
    private readonly CombatEquipmentRuntimeStateStore stateStore;
    private readonly IProductionFacilityMutationEpochQuery facilityMutations;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IBalanceWorkCalculator balanceWorkCalculator;
    private readonly ICraftQualityResolver qualityResolver;
    private readonly IRunSeedProvider runSeedProvider;
    private readonly IWorkerNarrativeQualificationQuery narrativeQualification;
    private readonly ICombatRejectedRecoveryProjector rejectedRecoveryProjector;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly ExtremeCraftInspirationRuntime inspirationRuntime;
    private readonly IGameClock gameClock;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly CombatEquipmentCraftOutputTransaction outputTransaction;
    private readonly ICombatCraftDefinitionCatalog craftDefinitions;
    private readonly ICombatEquipmentCraftInputDestinationRuntime
        inputDestinations;

    private List<CombatEquipmentCraftOrderSaveData> orders =>
        stateStore.Current.CraftOrders;
    private Dictionary<string, CombatEquipmentCraftMaterialPolicySaveData>
        materialPolicies => stateStore.Current.CraftMaterialPolicies;

    private IDictionary<string, CombatEquipmentInstance> Instances =>
        itemInstances.EquipmentInstances;

    public CombatEquipmentCraftingRuntime(
        ICombatEquipmentCatalog catalog,
        IItemInstanceRepository itemInstances,
        IResourceEconomyContentCatalog materialCatalog,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IEquipmentPhysicalItemGateway physicalItems,
        CombatEquipmentStatProjector statProjector,
        CombatEquipmentPhysicalStateWriter physicalState,
        IFacilityCapabilityQuery facilities,
        CombatEquipmentRuntimeStateStore stateStore,
        IProductionFacilityMutationEpochQuery facilityMutations,
        IBalanceWorkCalculator balanceWorkCalculator = null,
        ICraftQualityResolver qualityResolver = null,
        IRunSeedProvider runSeedProvider = null,
        IWorkerNarrativeQualificationQuery narrativeQualification = null,
        ICombatRejectedRecoveryProjector rejectedRecoveryProjector = null,
        ICharacterWorldQuery characterWorld = null,
        ExtremeCraftInspirationRuntime inspirationRuntime = null,
        IGameClock gameClock = null,
        CharacterIdentityEventPublisher identityEvents = null,
        CombatEquipmentCraftOutputTransaction outputTransaction = null,
        ICombatCraftDefinitionCatalog craftDefinitions = null,
        ICombatEquipmentCraftInputDestinationRuntime inputDestinations = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.materialCatalog = materialCatalog
            ?? throw new ArgumentNullException(nameof(materialCatalog));
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(CombatEquipmentCraftingRuntime)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.statProjector = statProjector
            ?? throw new ArgumentNullException(nameof(statProjector));
        this.physicalState = physicalState
            ?? throw new ArgumentNullException(nameof(physicalState));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.facilityMutations = facilityMutations
            ?? throw new ArgumentNullException(nameof(facilityMutations));
        this.balanceWorkCalculator = balanceWorkCalculator;
        this.qualityResolver = qualityResolver
            ?? new DeterministicCraftQualityResolver();
        this.runSeedProvider = runSeedProvider;
        this.narrativeQualification = narrativeQualification;
        this.rejectedRecoveryProjector = rejectedRecoveryProjector;
        this.characterWorld = characterWorld;
        this.inspirationRuntime = inspirationRuntime;
        this.gameClock = gameClock;
        this.identityEvents = identityEvents;
        this.outputTransaction = outputTransaction;
        this.craftDefinitions = craftDefinitions
            ?? new CombatCraftDefinitionCatalog(catalog);
        this.inputDestinations = inputDestinations;
    }

    public IReadOnlyList<CombatEquipmentCraftOrderSaveData> Queue =>
        orders.AsReadOnly();

    public bool IsDefinitionUnlocked(string definitionId, out string failureReason)
    {
        failureReason = string.Empty;
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "equipment.definition.unknown";
            return false;
        }
        if (string.IsNullOrWhiteSpace(definition.RequiredResearchId))
        {
            return true;
        }
        if (research.State.Projects.IsCompleted(
                new ResearchProjectId(definition.RequiredResearchId)))
        {
            return true;
        }
        failureReason = $"equipment.research.required:{definition.RequiredResearchId}";
        return false;
    }

    public IReadOnlyList<CraftMaterialDefinitionSO> GetAllowedMaterials(
        string definitionId)
    {
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition))
        {
            return Array.Empty<CraftMaterialDefinitionSO>();
        }

        return materialCatalog.Materials
            .Where(definition.AllowsMaterial)
            .OrderBy(material => material.RareMaterial ? 1 : 0)
            .ThenBy(material => material.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public CombatEquipmentCraftMaterialPolicySaveData GetMaterialPolicy(
        string definitionId,
        BuildableObject craftingFacility)
    {
        return TryGetOrCreateMaterialPolicy(
                definitionId,
                craftingFacility,
                out CombatEquipmentCraftMaterialPolicySaveData policy,
                out _)
            ? policy.Clone()
            : new CombatEquipmentCraftMaterialPolicySaveData();
    }

    public bool SetMaterialAllowed(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        bool allowed,
        out string failureReason)
    {
        if (!TryGetOrCreateMaterialPolicy(
                definitionId,
                craftingFacility,
                out CombatEquipmentCraftMaterialPolicySaveData policy,
                out failureReason))
        {
            return false;
        }

        string normalizedMaterialId = materialId?.Trim() ?? string.Empty;
        if (!policy.priorityMaterialIds.Contains(
                normalizedMaterialId,
                StringComparer.Ordinal))
        {
            failureReason = "equipment.material.not_allowed";
            return false;
        }

        if (allowed)
        {
            if (!policy.allowedMaterialIds.Contains(
                    normalizedMaterialId,
                    StringComparer.Ordinal))
            {
                policy.allowedMaterialIds.Add(normalizedMaterialId);
            }
        }
        else
        {
            policy.allowedMaterialIds.RemoveAll(id =>
                string.Equals(id, normalizedMaterialId, StringComparison.Ordinal));
        }

        failureReason = string.Empty;
        return true;
    }

    public bool MoveMaterialPriority(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        int offset,
        out string failureReason)
    {
        if (!TryGetOrCreateMaterialPolicy(
                definitionId,
                craftingFacility,
                out CombatEquipmentCraftMaterialPolicySaveData policy,
                out failureReason))
        {
            return false;
        }

        string normalizedMaterialId = materialId?.Trim() ?? string.Empty;
        int currentIndex = policy.priorityMaterialIds.FindIndex(id =>
            string.Equals(id, normalizedMaterialId, StringComparison.Ordinal));
        if (currentIndex < 0)
        {
            failureReason = "equipment.material.not_allowed";
            return false;
        }

        int targetIndex = Mathf.Clamp(
            currentIndex + Math.Sign(offset),
            0,
            policy.priorityMaterialIds.Count - 1);
        if (targetIndex != currentIndex)
        {
            policy.priorityMaterialIds.RemoveAt(currentIndex);
            policy.priorityMaterialIds.Insert(targetIndex, normalizedMaterialId);
        }
        failureReason = string.Empty;
        return true;
    }

    public bool TryGetPreviewStats(
        string definitionId,
        string materialId,
        out CombatEquipmentDerivedStats stats)
    {
        stats = default;
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition)
            || !TryResolveMaterial(definition, materialId, out CraftMaterialDefinitionSO material, out _))
        {
            return false;
        }

        stats = statProjector.Build(definition, material);
        return true;
    }

    public bool TryQueue(
        string definitionId,
        BuildableObject craftingFacility,
        out string failureReason)
    {
        string normalizedId = definitionId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedId)
            || !string.Equals(
                normalizedId,
                normalizedId.Trim(),
                StringComparison.Ordinal))
        {
            failureReason = "equipment.definition.unknown-or-noncanonical";
            return false;
        }
        if (!TryRequireMutable(craftingFacility, out failureReason))
        {
            return false;
        }
        string defaultMaterialId = string.Empty;
        if (catalog.TryGet(normalizedId, out CombatEquipmentDefinitionSO definition))
        {
            if (TryGetOrCreateMaterialPolicy(
                    normalizedId,
                    craftingFacility,
                    out CombatEquipmentCraftMaterialPolicySaveData policy,
                    out failureReason))
            {
                defaultMaterialId = policy.priorityMaterialIds.FirstOrDefault(id =>
                    policy.allowedMaterialIds.Contains(id, StringComparer.Ordinal))
                    ?? string.Empty;
                if (string.IsNullOrWhiteSpace(defaultMaterialId))
                {
                    failureReason = "equipment.material.none_enabled";
                    return false;
                }
            }
            else if (materialCatalog.Materials.Count > 0)
            {
                return false;
            }
            else
            {
                defaultMaterialId = definition.DefaultMaterialId;
            }
        }

        return TryQueue(
            normalizedId,
            defaultMaterialId,
            craftingFacility,
            out failureReason);
    }

    public bool TryQueue(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedId = definitionId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedId)
            || !string.Equals(
                normalizedId,
                normalizedId.Trim(),
                StringComparison.Ordinal)
            || !craftDefinitions.TryGetExact(
                normalizedId,
                out CombatCraftDefinitionSnapshot craftDefinition))
        {
            failureReason = "equipment.definition.unknown-or-noncanonical";
            return false;
        }
        bool ammunitionRecipe = craftDefinition.Kind
            == CombatCraftOutputKind.GenericAmmunition;
        CombatEquipmentDefinitionSO definition = null;
        if (!ammunitionRecipe && !catalog.TryGet(normalizedId, out definition))
        {
            failureReason = "equipment.definition.unknown";
            return false;
        }
        if (!ammunitionRecipe && !IsDefinitionUnlocked(normalizedId, out failureReason))
        {
            return false;
        }
        if (!ammunitionRecipe
            && string.Equals(
                definition.RequiredResearchId,
                "research:equipment:weapon-patterns",
                StringComparison.Ordinal)
            && facilities.FindOperational(
                ResearchFacilityCommandKind.WeaponPatternAccess).Count == 0)
        {
            failureReason = "equipment.craft.weapon_pattern_facility_required";
            return false;
        }

        CraftMaterialDefinitionSO material = null;
        if (!ammunitionRecipe
            && !TryResolveMaterial(definition, materialId, out material, out failureReason))
        {
            return false;
        }
        if (craftingFacility == null)
        {
            failureReason = "equipment.craft.facility_required";
            return false;
        }
        CombatCraftFacilityEligibilitySnapshot facilityEligibility =
            CombatCraftFacilityEligibility.Capture(
                craftingFacility.BuildingData,
                craftDefinitions);
        if (!facilityEligibility.Contains(normalizedId))
        {
            failureReason = "equipment.craft.facility_definition_not_allowed";
            return false;
        }
        if (!TryRequireMutable(craftingFacility, out failureReason))
        {
            return false;
        }
        if (!TryBuildConcreteMaterials(
                definition,
                normalizedId,
                material,
                out IReadOnlyDictionary<string, int> materials,
                out failureReason))
        {
            return false;
        }

        int orderSequence = Math.Max(0, stateStore.Current.NextCraftSequence);
        string orderId = $"combat-craft:{orderSequence:D8}";
        string destinationId = CombatEquipmentCraftInputDestinationAuthority
            .FormatDestinationId(orderId);

        int attemptIndex = 0;
        float requiredWork = ammunitionRecipe
            ? CombatCraftCycleMaximumAuthority.ResolveAmmunitionPrimaryWork()
            : balanceWorkCalculator?.CalculateEquipment(
                definition,
                material?.ItemId)
                ?? definition.RequiredCraftWork;
        CombatEquipmentCraftOrderSaveData order = new()
        {
            orderId = orderId,
            definitionId = normalizedId,
            materialId = material?.MaterialId
                ?? ResolveRequestedMaterialId(definition, materialId),
            requiredWork = requiredWork,
            craftWorkPerAttempt = requiredWork,
            completedWork = 0f,
            materialsReady = materials.Count == 0,
            materialDestinationId = destinationId,
            facilityPersistentId =
                craftingFacility.RequirePersistentInstanceId().Value,
            destinationX = craftingFacility.centerPos.x,
            destinationY = craftingFacility.centerPos.y,
            workerPolicy = WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality),
            qualityRoll = qualityResolver.Roll(
                unchecked((ulong)(uint)(runSeedProvider?.RunSeed ?? 1)),
                orderId,
                normalizedId,
                attemptIndex),
            minimumQuality = CraftsmanshipQualityTier.Awful,
            rejectedDisposition = RejectedOutputDisposition.AutoDismantle,
            repeatLimitMode = QualityRepeatLimitMode.SafeLimits,
            maximumAttempts = 10,
            qualityAttemptIndex = attemptIndex,
            requiredAcceptedCount = 1,
            facilityQualityBonus = Mathf.Max(
                0f,
                (craftingFacility.FacilityLevel - 1) * 2f)
        };
        if (inputDestinations == null
            || !inputDestinations.TryOpen(
                order,
                craftingFacility,
                materials,
                out failureReason))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "equipment.craft.input_destination_unavailable"
                : failureReason;
            return false;
        }
        if (!inputDestinations.TryRequest(
                order,
                materials,
                out string requestFailure))
        {
            if (!inputDestinations.TryClose(
                    order,
                    "combat-craft-queue-rollback",
                    out string closeFailure))
            {
                throw new InvalidOperationException(
                    "Combat craft input rollback failed: " + closeFailure);
            }
            failureReason = string.IsNullOrWhiteSpace(requestFailure)
                ? "equipment.craft.materials_missing"
                : requestFailure;
            return false;
        }
        stateStore.Current.NextCraftSequence = checked(orderSequence + 1);
        orders.Add(order);
        return true;
    }

    public WorkerSelectionPolicySaveData GetWorkerPolicy(string orderId)
    {
        CombatEquipmentCraftOrderSaveData order = orders.FirstOrDefault(value =>
            value != null && string.Equals(
                value.orderId,
                orderId?.Trim() ?? string.Empty,
                StringComparison.Ordinal));
        return order?.workerPolicy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality);
    }

    public bool SetWorkerPolicy(
        string orderId,
        WorkerSelectionPolicySaveData policy,
        out string failureReason)
    {
        CombatEquipmentCraftOrderSaveData order = orders.FirstOrDefault(value =>
            value != null && string.Equals(
                value.orderId,
                orderId?.Trim() ?? string.Empty,
                StringComparison.Ordinal));
        if (order == null)
        {
            failureReason = "equipment.craft.order_missing";
            return false;
        }
        if (!TryRequireMutable(order, out failureReason))
        {
            return false;
        }
        order.workerPolicy = policy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        RevalidateQualityBlocker(order);
        failureReason = string.Empty;
        return true;
    }

    public bool SetQualityTarget(
        string orderId,
        CraftsmanshipQualityTier minimumQuality,
        RejectedOutputDisposition rejectedDisposition,
        QualityRepeatLimitMode repeatLimitMode,
        int maximumAttempts,
        float workBudget,
        int requiredAcceptedCount,
        out string failureReason)
    {
        CombatEquipmentCraftOrderSaveData order = orders.FirstOrDefault(value =>
            value != null && string.Equals(
                value.orderId,
                orderId?.Trim() ?? string.Empty,
                StringComparison.Ordinal));
        if (order == null)
        {
            failureReason = "equipment.craft.quality_target_invalid";
            return false;
        }
        if (!TryRequireMutable(order, out failureReason))
        {
            return false;
        }
        if (IsAmmunitionRecipe(order.definitionId)
            || order.attemptOutcomeResolved
            || order.outputPhase != CombatEquipmentCraftOutputPhase.None
            || order.outputPublication is { IsEmpty: false }
            || !Enum.IsDefined(typeof(CraftsmanshipQualityTier), minimumQuality)
            || !Enum.IsDefined(typeof(RejectedOutputDisposition), rejectedDisposition)
            || !Enum.IsDefined(typeof(QualityRepeatLimitMode), repeatLimitMode)
            || maximumAttempts <= 0
            || requiredAcceptedCount <= 0)
        {
            failureReason = "equipment.craft.quality_target_invalid";
            return false;
        }
        order.minimumQuality = minimumQuality;
        order.rejectedDisposition = rejectedDisposition;
        order.repeatLimitMode = repeatLimitMode;
        order.maximumAttempts = maximumAttempts;
        order.workBudget = Mathf.Max(0f, workBudget);
        order.requiredAcceptedCount = requiredAcceptedCount;
        RevalidateQualityBlocker(order);
        failureReason = string.Empty;
        return true;
    }

    public bool HasPendingWork(IEnumerable<string> craftableDefinitionIds)
    {
        return orders.Any(order =>
            order != null
            && IsCraftable(order.definitionId, craftableDefinitionIds)
            && (IsTerminalConvergenceRetry(order)
                || IsOrderFacilityMutable(order))
            && (order.dismantlingRejectedOutput
                || AreMaterialsAvailable(order)));
    }

    public bool TryGetNextCraftMaterialContext(
        IEnumerable<string> craftableDefinitionIds,
        CharacterActor worker,
        out string definitionId,
        out string materialId,
        out bool usesSubstituteMaterial)
    {
        definitionId = string.Empty;
        materialId = string.Empty;
        usesSubstituteMaterial = false;
        for (int index = 0; index < orders.Count; index++)
        {
            CombatEquipmentCraftOrderSaveData order = orders[index];
            if (order == null
                || order.dismantlingRejectedOutput
                || !IsCraftable(order.definitionId, craftableDefinitionIds)
                || !AreMaterialsAvailable(order)
                || !IsOrderFacilityMutable(order))
            {
                continue;
            }
            if (worker != null
                && !WorkerSelectionPolicyRules.IsEligible(
                    order.workerPolicy,
                    worker,
                    narrativeQualification,
                    out _))
            {
                continue;
            }
            if (worker == null
                && order.workerPolicy?.mode != WorkerSelectionMode.Anyone)
            {
                continue;
            }

            definitionId = order.definitionId?.Trim() ?? string.Empty;
            materialId = order.materialId?.Trim() ?? string.Empty;
            if (catalog.TryGet(definitionId, out CombatEquipmentDefinitionSO definition))
            {
                string resolvedMaterial = string.IsNullOrWhiteSpace(materialId)
                    ? definition.DefaultMaterialId
                    : materialId;
                usesSubstituteMaterial = !string.Equals(
                    resolvedMaterial,
                    definition.DefaultMaterialId,
                    StringComparison.Ordinal);
                materialId = resolvedMaterial;
            }
            return true;
        }
        return false;
    }

    private bool AreMaterialsAvailable(CombatEquipmentCraftOrderSaveData order)
    {
        if (order == null)
        {
            return false;
        }
        if (order.materialsReady)
        {
            return true;
        }

        CombatEquipmentDefinitionSO definition = null;
        if (!IsAmmunitionRecipe(order.definitionId)
            && !catalog.TryGet(order.definitionId, out definition))
        {
            return false;
        }
        CraftMaterialDefinitionSO material = null;
        if (definition != null
            && !TryResolveMaterial(definition, order.materialId, out material, out _))
        {
            return false;
        }
        if (!TryBuildConcreteMaterials(
                definition,
                order.definitionId,
                material,
                out IReadOnlyDictionary<string, int> materials,
                out _))
        {
            return false;
        }
        if (materials.Count == 0)
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(order.materialDestinationId))
        {
            return false;
        }

        IReadOnlyList<WorldItemStackSnapshot> stacks = physicalItems.GetAllStacks();
        foreach (KeyValuePair<string, int> requirement in materials)
        {
            int available = 0;
            for (int index = 0; index < stacks.Count; index++)
            {
                WorldItemStackSnapshot stack = stacks[index];
                if (stack != null
                    && stack.State == WorldItemStackState.FacilityBuffer
                    && !stack.Forbidden
                    && string.Equals(
                        stack.DestinationId,
                        order.materialDestinationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemId,
                        requirement.Key,
                        StringComparison.Ordinal))
                {
                    available += stack.Quantity;
                }
            }
            if (available < requirement.Value)
            {
                return false;
            }
        }
        return true;
    }

    public int ApplyWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId,
        out string completedMaterialId)
    {
        return ApplyWork(
            craftableDefinitionIds,
            workUnits,
            null,
            0f,
            out completedDefinitionId,
            out completedMaterialId,
            out _);
    }

    public int ApplyWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        CharacterActor worker,
        float relevantSkill,
        out string completedDefinitionId,
        out string completedMaterialId,
        out CombatEquipmentQuality completedQuality)
    {
        return ApplyWork(
            craftableDefinitionIds,
            workUnits,
            worker,
            relevantSkill,
            out completedDefinitionId,
            out completedMaterialId,
            out completedQuality,
            out _);
    }

    public int ApplyWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        CharacterActor worker,
        float relevantSkill,
        out string completedDefinitionId,
        out string completedMaterialId,
        out CombatEquipmentQuality completedQuality,
        out MythicProvenanceSaveData completedMythicProvenance)
    {
        completedDefinitionId = string.Empty;
        completedMaterialId = string.Empty;
        completedQuality = CombatEquipmentQuality.Normal;
        completedMythicProvenance = null;
        float safeWork = Mathf.Max(0f, workUnits);
        if (safeWork <= 0f)
        {
            return 0;
        }

        for (int index = 0; index < orders.Count; index++)
        {
            CombatEquipmentCraftOrderSaveData order = orders[index];
            if (order == null
                || !IsCraftable(order.definitionId, craftableDefinitionIds))
            {
                continue;
            }

            // A resolved attempt owns both its pending material receipt and its
            // fixed output. Finish that transaction before accepting more work
            // or consulting a newly selected worker.
            if (order.attemptOutcomeResolved)
            {
                return TryFinalizeResolvedAttempt(
                    order,
                    index,
                    out completedDefinitionId,
                    out completedMaterialId,
                    out completedQuality,
                    out completedMythicProvenance)
                    ? 1
                    : 0;
            }

            // A fully-worked rejected output already owns its exact physical
            // disposition and recovery projection. It may finish that terminal
            // transaction while the facility is frozen; all other productive
            // work remains blocked until the mutation closes.
            bool terminalConvergenceRetry = IsTerminalConvergenceRetry(order);
            if (!terminalConvergenceRetry && !IsOrderFacilityMutable(order))
            {
                continue;
            }

            if (worker != null
                && !WorkerSelectionPolicyRules.IsEligible(
                    order.workerPolicy,
                    worker,
                    narrativeQualification,
                    out _))
            {
                continue;
            }
            if (worker == null
                && order.workerPolicy?.mode != WorkerSelectionMode.Anyone)
            {
                continue;
            }
            if (!order.dismantlingRejectedOutput
                && !RevalidateQualityBlocker(order, worker))
            {
                continue;
            }
            if (!EnsureMaterialsReady(order))
            {
                continue;
            }

            float acceptedWork = Mathf.Min(
                safeWork,
                Mathf.Max(0f, order.requiredWork - order.completedWork));
            order.completedWork = Mathf.Min(
                Mathf.Max(0.1f, order.requiredWork),
                order.completedWork + acceptedWork);
            order.qualityStage = QualityTargetPipelineStage.Working;
            if (worker != null && acceptedWork > 0f)
            {
                CraftContributionAccumulator contributions = new(order.contributions);
                contributions.Add(
                    worker.Identity?.PersistentId,
                    acceptedWork,
                    relevantSkill);
                order.contributions = contributions.Capture();
            }
            if (order.RemainingWork > 0.001f)
            {
                return 0;
            }

            if (order.dismantlingRejectedOutput)
            {
                if (!TryResolveRejectedEquipmentDismantle(order))
                {
                    return 0;
                }
                return 0;
            }
            ResolveAttemptOutcome(order, worker);
            return TryFinalizeResolvedAttempt(
                order,
                index,
                out completedDefinitionId,
                out completedMaterialId,
                out completedQuality,
                out completedMythicProvenance)
                ? 1
                : 0;
        }
        return 0;
    }

    private void ResolveAttemptOutcome(
        CombatEquipmentCraftOrderSaveData order,
        CharacterActor worker)
    {
        if (order.attemptOutcomeResolved)
        {
            return;
        }

        CraftContributionAccumulator completedContributions =
            new(order.contributions);
        CraftQualityResolution resolution = qualityResolver.Resolve(
            order.qualityRoll ?? qualityResolver.Roll(
                unchecked((ulong)(uint)(runSeedProvider?.RunSeed ?? 1)),
                order.orderId,
                order.definitionId,
                order.qualityAttemptIndex),
            completedContributions.WeightedRelevantSkill > 0f
                ? completedContributions.WeightedRelevantSkill
                : 50f,
            order.facilityQualityBonus,
            0f,
            Mathf.Clamp(order.requiredWork / 20f, 0f, 25f));
        CombatEquipmentQuality resolvedQuality =
            (CombatEquipmentQuality)(int)resolution.Tier;
        string makerCharacterId = worker?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        float totalContribution = order.contributions
            .Where(value => value != null)
            .Sum(value => Mathf.Max(0f, value.contributedWork));
        float makerContribution = order.contributions
            .Where(value => value != null && string.Equals(
                value.characterId,
                makerCharacterId,
                StringComparison.Ordinal))
            .Sum(value => Mathf.Max(0f, value.contributedWork));
        float makerShare = totalContribution <= 0f
            ? 0f
            : makerContribution / totalContribution;
        bool hasInspiration = ExtremeCraftInspirationRuntime.TryResolveRule(
            worker,
            out ExtremeCraftInspirationRule inspirationRule);
        MythicProvenanceSaveData mythicProvenance = null;
        if (hasInspiration
            && makerShare + 0.0001f >= inspirationRule.minimumContributionShare
            && catalog.TryGet(
                order.definitionId,
                out CombatEquipmentDefinitionSO completedDefinition)
            && completedDefinition.AllowMythicInspiration)
        {
            ulong fixedRollHash = MythicCraftInspirationRules.ResolveFixedRollHash(
                unchecked((ulong)(uint)(runSeedProvider?.RunSeed ?? 1)),
                order.orderId,
                order.definitionId,
                order.qualityRoll?.attemptIndex ?? order.qualityAttemptIndex,
                makerCharacterId);
            if (MythicCraftInspirationRules.IsMythic(
                    fixedRollHash,
                    inspirationRule.mythicChance))
            {
                resolvedQuality = CombatEquipmentQuality.Mythic;
                mythicProvenance = new MythicProvenanceSaveData
                {
                    makerCharacterId = makerCharacterId,
                    sourceTraitId = MythicCraftInspirationRules.SourceTraitId,
                    originalQuality = resolution.Tier,
                    fixedRollHash = fixedRollHash,
                    createdDay = Mathf.FloorToInt(
                        (gameClock?.Time ?? 0f)
                        / GameCalendarRules.SecondsPerDay),
                    createdFacilityId = order.facilityPersistentId
                };
            }
        }

        order.resolvedQuality = resolvedQuality;
        order.resolvedMythicProvenance = mythicProvenance;
        order.resolvedMakerCharacterId = makerCharacterId;
        order.resolvedHadInspiration = hasInspiration;
        order.outputOperationId = CombatEquipmentCraftOutputOutbox
            .FormatOperationId(order.orderId, order.qualityAttemptIndex);
        if (CombatAmmunitionCraftDefinitions.TryGetExact(
                order.definitionId,
                out CombatCraftDefinitionSnapshot ammunitionDefinition))
        {
            order.outputItemId = ammunitionDefinition.OutputItemId.Value;
            order.outputQuantity = ammunitionDefinition.OutputQuantity;
        }
        else
        {
            order.outputItemId = PhysicalItemIds.ForEquipment(order.definitionId);
            order.outputQuantity = 1;
        }
        bool ammunitionOutput = IsAmmunitionRecipe(order.definitionId);
        if (!ammunitionOutput && outputTransaction != null)
        {
            CombatEquipmentInstance prepared = BuildPreparedInstance(
                order.definitionId,
                order.resolvedQuality,
                CombatEquipmentWorldState.Loose,
                order.materialId,
                order.resolvedMythicProvenance,
                itemInstances.AllocateItemInstanceId().Value);
            order.outputInstanceId = prepared.instanceId;
            order.outputPreparedComponent =
                EquipmentItemStateCodec.Encode(prepared);
        }
        ProductionOutputCapabilityDescriptor outputCapability = physicalState
            .CaptureOutputCapability(
                ammunitionOutput
                    ? CombatAmmunitionCraftOutputCapability.OutputLineId
                    : CombatEquipmentCraftOutputCapability.OutputLineId,
                order.outputItemId,
                ammunitionOutput
                    ? ProductionOutputCapabilityIds.CombatAmmunitionCraft
                    : ProductionOutputCapabilityIds.CombatEquipmentCraft);
        order.outputCapability =
            ProductionOutputCapabilitySaveData.Freeze(outputCapability);
        order.outputPhase = outputTransaction != null
            ? CombatEquipmentCraftOutputPhase.ResolvedWaitingForPublication
            : CombatEquipmentCraftOutputPhase.LegacyUniqueOutput;
        order.attemptOutcomeResolved = true;

        // Quality resolution is a deterministic authored outcome. Publish its
        // side effects once and persist the marker before output retries.
        if (hasInspiration)
        {
            inspirationRuntime?.RecordEligibleCompletion(
                worker,
                order.definitionId,
                resolvedQuality == CombatEquipmentQuality.Mythic,
                gameClock?.Time ?? 0f);
        }
        if (identityEvents != null
            && CharacterPersistentIdentity.TryGet(
                worker,
                out CharacterId qualityMakerId))
        {
            identityEvents.Publish(new ProductQualityResolvedEvent(
                qualityMakerId,
                order.definitionId,
                (CraftsmanshipQualityTier)(int)resolvedQuality,
                order.qualityRoll?.attemptIndex ?? order.qualityAttemptIndex,
                Mathf.FloorToInt(
                    (gameClock?.Time ?? 0f)
                    / GameCalendarRules.SecondsPerDay),
                rejectedBelowMinimum: (int)resolvedQuality
                    < (int)order.minimumQuality));
        }
        order.completionEffectsPublished = true;
    }

    private bool TryFinalizeResolvedAttempt(
        CombatEquipmentCraftOrderSaveData order,
        int orderIndex,
        out string completedDefinitionId,
        out string completedMaterialId,
        out CombatEquipmentQuality completedQuality,
        out MythicProvenanceSaveData completedMythicProvenance)
    {
        completedDefinitionId = string.Empty;
        completedMaterialId = string.Empty;
        completedQuality = CombatEquipmentQuality.Normal;
        completedMythicProvenance = null;
        if (order == null
            || !order.attemptOutcomeResolved
            || !order.completionEffectsPublished
            || !TryValidateResolvedOutputCapability(order, out _)
            || !TryGetConcreteMaterials(
                order,
                out IReadOnlyDictionary<string, int> materials))
        {
            return false;
        }

        bool accepted = (int)order.resolvedQuality >= (int)order.minimumQuality;
        bool markForSale = !accepted
            && order.rejectedDisposition == RejectedOutputDisposition.MarkForSale;
        string outputDestination = markForSale
            ? QualityRejectedOutputRules.MarketDestinationId
            : ProductionBillRuntime.OutputDestinationPrefix
                + order.facilityPersistentId;
        Vector2Int position = new(order.destinationX, order.destinationY);
        bool commonOutput = outputTransaction != null
            && order.outputPhase !=
                CombatEquipmentCraftOutputPhase.LegacyUniqueOutput;
        bool outputReady = commonOutput
            ? order.outputPhase is CombatEquipmentCraftOutputPhase
                    .PublishedAwaitingInputAcknowledgement
                    or CombatEquipmentCraftOutputPhase
                        .RestoredOutputAwaitingInputAcknowledgement
                || outputTransaction.EnsureCommitted(order).IsCommitted
            : IsAmmunitionRecipe(order.definitionId)
                ? CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                    order,
                    physicalItems,
                    position,
                    outputDestination,
                    out _)
            : TryEnsureUniqueCraftOutput(
                order,
                position,
                outputDestination,
                out _);
        if (!outputReady)
        {
            return false;
        }

        if (materials.Count > 0
            && !CombatEquipmentCraftMaterialOutbox.TryAcknowledgeOutcome(
                order,
                materials,
                physicalItems,
                out _))
        {
            return false;
        }

        if (commonOutput
            && !outputTransaction.TryAcknowledgeAndRoute(
                order,
                markForSale,
                out _))
        {
            return false;
        }

        order.consumedWork += Mathf.Max(0f, order.craftWorkPerAttempt);
        if (!accepted)
        {
            order.rejectedInstanceId = order.outputInstanceId;
            order.rejectedStackId = order.outputStackId;
            if (HasReachedEquipmentRepeatLimit(order))
            {
                RequireClosedInputDestination(
                    order,
                    "combat-craft-repeat-limit-reached");
                orders.RemoveAt(orderIndex);
                return false;
            }
            if (order.rejectedDisposition == RejectedOutputDisposition.AutoDismantle)
            {
                PrepareRejectedEquipmentDismantle(order);
                return false;
            }
            PrepareNextEquipmentAttempt(order);
            return false;
        }

        completedDefinitionId = order.definitionId;
        completedMaterialId = order.materialId;
        completedQuality = order.resolvedQuality;
        completedMythicProvenance = order.resolvedMythicProvenance?.Clone();
        int nextAcceptedCount = checked(order.acceptedCount + 1);
        if (nextAcceptedCount >= Mathf.Max(1, order.requiredAcceptedCount))
        {
            RequireClosedInputDestination(
                order,
                "combat-craft-order-completed");
            order.acceptedCount = nextAcceptedCount;
            orders.RemoveAt(orderIndex);
        }
        else
        {
            order.acceptedCount = nextAcceptedCount;
            PrepareNextEquipmentAttempt(order);
        }
        return true;
    }

    internal bool TryValidateResolvedOutputCapability(
        CombatEquipmentCraftOrderSaveData order,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order == null || !order.attemptOutcomeResolved)
        {
            bool empty = order?.outputCapability == null
                || order.outputCapability.IsEmpty;
            if (empty)
                return true;
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                order?.outputItemId ?? string.Empty,
                "combat-output-capability-without-outcome");
            return false;
        }

        bool ammunition = IsAmmunitionRecipe(order.definitionId);
        ProductionOutputCapabilitySaveData frozen = order.outputCapability;
        string expectedLine = ammunition
            ? CombatAmmunitionCraftOutputCapability.OutputLineId
            : CombatEquipmentCraftOutputCapability.OutputLineId;
        string expectedCapability = ammunition
            ? ProductionOutputCapabilityIds.CombatAmmunitionCraft
            : ProductionOutputCapabilityIds.CombatEquipmentCraft;
        if (frozen == null
            || frozen.IsEmpty
            || !string.Equals(
                frozen.outputLineId,
                expectedLine,
                StringComparison.Ordinal)
            || !string.Equals(
                frozen.itemId,
                order.outputItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                frozen.capabilityId,
                expectedCapability,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                order.outputItemId ?? string.Empty,
                "combat-output-capability-owner-mismatch");
            return false;
        }
        return physicalState.TryValidateOutputCapability(frozen, out failure);
    }

    private bool TryEnsureUniqueCraftOutput(
        CombatEquipmentCraftOrderSaveData order,
        Vector2Int position,
        string destinationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrEmpty(order.outputInstanceId))
        {
            CombatEquipmentInstance created = CreateInstance(
                order.definitionId,
                order.resolvedQuality,
                CombatEquipmentWorldState.Loose,
                order.materialId,
                order.resolvedMythicProvenance);
            order.outputInstanceId = created.instanceId;
        }
        if (!Instances.TryGetValue(
                order.outputInstanceId,
                out CombatEquipmentInstance instance)
            || !string.Equals(
                instance.definitionId,
                order.definitionId,
                StringComparison.Ordinal)
            || !string.Equals(
                instance.materialId,
                order.materialId,
                StringComparison.Ordinal)
            || instance.quality != order.resolvedQuality)
        {
            failureReason = "combat-craft-output-instance-conflict";
            return false;
        }

        WorldItemStackSnapshot[] existing = physicalItems.GetAllStacks()
            .Where(stack => stack != null && string.Equals(
                stack.ItemInstanceId,
                order.outputInstanceId,
                StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        if (existing.Length == 0)
        {
            if (!physicalItems.SpawnExistingUniqueItemAt(
                    order.outputItemId,
                    (ItemInstanceId)order.outputInstanceId,
                    position,
                    WorldItemStackState.FacilityOutputBuffer,
                    destinationId,
                    out string stackId))
            {
                failureReason = "combat-craft-output-space-unavailable";
                return false;
            }
            order.outputStackId = stackId;
            existing = physicalItems.GetAllStacks()
                .Where(stack => stack != null && string.Equals(
                    stack.ItemInstanceId,
                    order.outputInstanceId,
                    StringComparison.Ordinal))
                .ToArray();
        }
        if (existing.Length != 1
            || !string.Equals(
                existing[0].ItemId,
                order.outputItemId,
                StringComparison.Ordinal)
            || existing[0].State != WorldItemStackState.FacilityOutputBuffer
            || existing[0].Position != position
            || !string.Equals(
                existing[0].DestinationId,
                destinationId,
                StringComparison.Ordinal)
            || (!string.IsNullOrEmpty(order.outputStackId)
                && !string.Equals(
                    order.outputStackId,
                    existing[0].StackId,
                    StringComparison.Ordinal)))
        {
            failureReason = "combat-craft-output-stack-conflict";
            return false;
        }

        order.outputStackId = existing[0].StackId;
        string commitId =
            $"physical-source:{order.outputOperationId}:{order.outputInstanceId}";
        if (!physicalItems.TrySetInstanceComponent(
                order.outputStackId,
                ProductionOutputCommitComponentCodec.Create(commitId)))
        {
            failureReason = "combat-craft-output-marker-failed";
            return false;
        }
        instance.sourceStackId = order.outputStackId;
        instance.worldState = CombatEquipmentWorldState.Loose;
        try
        {
            physicalState.Persist(instance);
        }
        catch (InvalidOperationException)
        {
            failureReason = "combat-craft-output-state-persist-failed";
            return false;
        }
        order.outputCommitId = commitId;
        order.outputPublished = true;
        return true;
    }

    internal bool TryGetConcreteMaterials(
        CombatEquipmentCraftOrderSaveData order,
        out IReadOnlyDictionary<string, int> materials)
    {
        materials = new Dictionary<string, int>();
        CombatEquipmentDefinitionSO definition = null;
        if (!IsAmmunitionRecipe(order.definitionId)
            && !catalog.TryGet(order.definitionId, out definition))
        {
            return false;
        }
        CraftMaterialDefinitionSO material = null;
        return (definition == null
                || TryResolveMaterial(
                    definition,
                    order.materialId,
                    out material,
                    out _))
            && TryBuildConcreteMaterials(
                definition,
                order.definitionId,
                material,
                out materials,
                out _);
    }

    public CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState,
        string materialId)
    {
        return CreateInstance(
            definitionId,
            quality,
            worldState,
            materialId,
            null);
    }

    public CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState,
        string materialId,
        MythicProvenanceSaveData mythicProvenance)
    {
        CombatEquipmentInstance instance = BuildPreparedInstance(
            definitionId,
            quality,
            worldState,
            materialId,
            mythicProvenance,
            itemInstances.AllocateItemInstanceId().Value);
        Instances.Add(instance.instanceId, instance);
        return instance.Clone();
    }

    private CombatEquipmentInstance BuildPreparedInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState,
        string materialId,
        MythicProvenanceSaveData mythicProvenance,
        string instanceId)
    {
        if (!catalog.TryGet(definitionId, out CombatEquipmentDefinitionSO definition))
        {
            throw new KeyNotFoundException(
                $"Unknown combat equipment definition '{definitionId}'.");
        }
        if (!IsDefinitionUnlocked(definitionId, out string lockedReason))
        {
            throw new InvalidOperationException(lockedReason);
        }
        if (!TryResolveMaterial(
                definition,
                materialId,
                out CraftMaterialDefinitionSO material,
                out string failureReason))
        {
            throw new ArgumentException(failureReason, nameof(materialId));
        }

        CombatEquipmentInstance instance = new CombatEquipmentInstance
        {
            instanceId = instanceId,
            definitionId = definition.EquipmentId,
            materialId = material?.MaterialId
                ?? ResolveRequestedMaterialId(definition, materialId),
            quality = quality,
            mythicProvenance = mythicProvenance?.Clone(),
            durabilityRatio = 1f,
            powerCharge = 100f,
            loadedAmmunition = new LoadedAmmunitionBatch(),
            worldState = worldState,
            moduleSlots = Enumerable.Range(0, definition.ModuleSlotCount)
                .Select(index => new EquipmentModuleSlotState { slotIndex = index })
                .ToList()
        };
        if (quality == CombatEquipmentQuality.Mythic
            && (instance.mythicProvenance == null
                || instance.mythicProvenance.sourceTraitId
                    != MythicCraftInspirationRules.SourceTraitId
                || string.IsNullOrWhiteSpace(
                    instance.mythicProvenance.makerCharacterId)))
            throw new InvalidOperationException(
                "Mythic equipment requires trait-300 provenance.");
        if (quality != CombatEquipmentQuality.Mythic
            && instance.mythicProvenance != null)
            throw new InvalidOperationException(
                "Non-Mythic equipment cannot carry Mythic provenance.");
        return instance;
    }

    private void PrepareRejectedEquipmentDismantle(
        CombatEquipmentCraftOrderSaveData order)
    {
        string rejectedInstanceId = order.rejectedInstanceId;
        string rejectedStackId = order.rejectedStackId;
        CombatEquipmentCraftMaterialOutbox.ClearCompletedAttempt(order);
        order.rejectedInstanceId = rejectedInstanceId;
        order.rejectedStackId = rejectedStackId;
        order.dismantlingRejectedOutput = true;
        order.rejectedOutputConsumed = false;
        order.completedWork = 0f;
        order.requiredWork = CombatCraftCycleMaximumAuthority
            .ResolveRejectedRecoveryWork(order.craftWorkPerAttempt);
        order.materialsReady = true;
        order.contributions.Clear();
        order.recoveryOutputs.Clear();
        order.spawnedRecoveryAmounts.Clear();
        ResetRejectedRecoveryProjection(order);
        CombatEquipmentRejectedDismantleOutbox.Clear(order);
    }

    private bool TryResolveRejectedEquipmentDismantle(
        CombatEquipmentCraftOrderSaveData order)
    {
        if (rejectedRecoveryProjector == null)
        {
            throw new InvalidOperationException(
                "Combat rejected recovery requires its shared projector before input consumption.");
        }
        CaptureRejectedRecoveryFactors(order);
        if (!order.rejectedOutputConsumed)
        {
            if (!Instances.ContainsKey(order.rejectedInstanceId)
                || string.IsNullOrWhiteSpace(order.rejectedStackId))
            {
                return false;
            }
        }
        if (!CombatEquipmentRejectedDismantleOutbox.TryCommitOrResume(
                order,
                physicalItems,
                out _))
        {
            return false;
        }
        if (!order.rejectedRecoveryProjected)
        {
            BuildRejectedEquipmentRecovery(order);
        }
        if (!TryValidateFrozenRejectedRecovery(order, out string recoveryFailure))
        {
            throw new InvalidOperationException(recoveryFailure);
        }
        Instances.Remove(order.rejectedInstanceId);
        Vector2Int position = new(order.destinationX, order.destinationY);
        string destination = ProductionBillRuntime.OutputDestinationPrefix
            + order.facilityPersistentId;
        for (int outputIndex = 0;
             outputIndex < order.recoveryOutputs.Count;
             outputIndex++)
        {
            CombatCraftRecoveryOutputSaveData output =
                order.recoveryOutputs[outputIndex];
            int spawned = outputIndex < order.spawnedRecoveryAmounts.Count
                ? order.spawnedRecoveryAmounts[outputIndex]
                : 0;
            if (spawned >= output.amount)
            {
                continue;
            }
            string operationId = CombatEquipmentRejectedDismantleOutbox
                .FormatRecoveryOperationId(
                    order.orderId,
                    order.qualityAttemptIndex,
                    outputIndex);
            if (!CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                output.itemId,
                output.amount,
                operationId,
                physicalItems,
                position,
                destination,
                out _,
                out _))
            {
                return false;
            }
            while (order.spawnedRecoveryAmounts.Count <= outputIndex)
            {
                order.spawnedRecoveryAmounts.Add(0);
            }
            order.spawnedRecoveryAmounts[outputIndex] = output.amount;
        }
        order.rejectedRecoveryPublished = true;
        if (!CombatEquipmentRejectedDismantleOutbox.TryAcknowledgeRecovery(
                order,
                physicalItems,
                out _))
        {
            return false;
        }
        order.consumedWork += Mathf.Max(0f, order.requiredWork);
        order.dismantlingRejectedOutput = false;
        order.rejectedOutputConsumed = false;
        order.rejectedInstanceId = string.Empty;
        order.rejectedStackId = string.Empty;
        order.recoveryOutputs.Clear();
        order.spawnedRecoveryAmounts.Clear();
        ResetRejectedRecoveryProjection(order);
        CombatEquipmentRejectedDismantleOutbox.Clear(order);
        PrepareNextEquipmentAttempt(order);
        return true;
    }

    private void CaptureRejectedRecoveryFactors(
        CombatEquipmentCraftOrderSaveData order)
    {
        if (order.rejectedRecoveryFactorsCaptured)
            return;
        CraftContributionAccumulator recoveryContributions =
            new(order.contributions);
        string salvageWorkerId = order.contributions
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.characterId))
            .OrderByDescending(value => value.contributedWork)
            .ThenBy(value => value.characterId, StringComparer.Ordinal)
            .Select(value => value.characterId.Trim())
            .FirstOrDefault() ?? string.Empty;
        CharacterActor salvageWorker = characterWorld?.Characters
            .FirstOrDefault(actor => actor != null && string.Equals(
                actor.Identity?.PersistentId,
                salvageWorkerId,
                StringComparison.Ordinal));
        float salvageYield = salvageWorker != null
            ? salvageWorker.GetDetailedStatMultiplier(
                GameplayEffectTargetIds.SalvageYield)
            : 1f;
        float workerSkill = recoveryContributions.WeightedRelevantSkill;
        if (float.IsNaN(workerSkill)
            || float.IsInfinity(workerSkill)
            || workerSkill < 0f
            || float.IsNaN(salvageYield)
            || float.IsInfinity(salvageYield)
            || salvageYield < 0f)
        {
            throw new InvalidOperationException(
                "Combat rejected recovery factors are invalid.");
        }
        rejectedRecoveryProjector.ValidateActualFactors(
            workerSkill,
            salvageYield);
        order.rejectedRecoveryWorkerSkill = workerSkill;
        order.rejectedRecoverySalvageMultiplier = salvageYield;
        order.rejectedRecoveryFactorsCaptured = true;
    }

    private void BuildRejectedEquipmentRecovery(
        CombatEquipmentCraftOrderSaveData order)
    {
        ICombatRejectedRecoveryProjector projector = rejectedRecoveryProjector
            ?? throw new InvalidOperationException(
                "Combat rejected recovery requires its shared projector.");
        if (order.rejectedDismantleInputMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                "Combat rejected recovery has no committed input-mass receipt.");
        }
        CombatRejectedRecoveryProjection projection = projector.ProjectActual(
            order.definitionId,
            order.materialId,
            order.rejectedRecoveryWorkerSkill,
            order.rejectedRecoverySalvageMultiplier,
            new PhysicalMassGrams(order.rejectedDismantleInputMassGrams));
        order.recoveryOutputs.Clear();
        order.spawnedRecoveryAmounts.Clear();
        foreach (CombatRejectedRecoveryOutput output in projection.Outputs)
        {
            order.recoveryOutputs.Add(new CombatCraftRecoveryOutputSaveData
            {
                itemId = output.ItemId,
                amount = output.Quantity
            });
            order.spawnedRecoveryAmounts.Add(0);
        }
        order.rejectedRecoveryProjected = true;
        order.rejectedRecoveryDesiredMassGrams =
            projection.DesiredOutputMassGrams;
        order.rejectedRecoveryOutputMassGrams =
            projection.ClampedOutputMassGrams;
        order.rejectedRecoverySourceDigest = projection.SourceDigest;
    }

    public bool TryValidateFrozenRejectedRecovery(
        CombatEquipmentCraftOrderSaveData order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || !order.dismantlingRejectedOutput)
        {
            failureReason = "combat-craft-rejected-recovery-owner-invalid";
            return false;
        }
        if (!order.rejectedRecoveryProjected)
        {
            bool factorsValid = order.rejectedRecoveryFactorsCaptured
                ? IsValidRejectedRecoveryFactor(
                    order.rejectedRecoveryWorkerSkill)
                    && IsValidRejectedRecoveryFactor(
                        order.rejectedRecoverySalvageMultiplier)
                : order.rejectedRecoveryWorkerSkill == 0f
                    && order.rejectedRecoverySalvageMultiplier == 0f;
            bool empty = factorsValid
                && order.recoveryOutputs.Count == 0
                && order.spawnedRecoveryAmounts.Count == 0
                && order.rejectedRecoveryDesiredMassGrams == 0L
                && order.rejectedRecoveryOutputMassGrams == 0L
                && string.IsNullOrEmpty(order.rejectedRecoverySourceDigest)
                && string.IsNullOrEmpty(order.rejectedDismantleOperationId);
            if (!empty)
            {
                failureReason =
                    "combat-craft-rejected-recovery-unprojected-state";
            }
            return empty;
        }
        if (!order.rejectedRecoveryFactorsCaptured
            || rejectedRecoveryProjector == null
            || order.rejectedDismantleInputMassGrams <= 0L
            || !IsValidRejectedRecoveryFactor(
                order.rejectedRecoveryWorkerSkill)
            || !IsValidRejectedRecoveryFactor(
                order.rejectedRecoverySalvageMultiplier))
        {
            failureReason = "combat-craft-rejected-recovery-authority-invalid";
            return false;
        }

        CombatRejectedRecoveryProjection expected;
        try
        {
            expected = rejectedRecoveryProjector.ProjectActual(
                order.definitionId,
                order.materialId,
                order.rejectedRecoveryWorkerSkill,
                order.rejectedRecoverySalvageMultiplier,
                new PhysicalMassGrams(order.rejectedDismantleInputMassGrams));
        }
        catch (Exception exception)
        {
            failureReason =
                "combat-craft-rejected-recovery-projection-failed:"
                + exception.Message;
            return false;
        }
        bool matches = order.rejectedRecoveryDesiredMassGrams
                == expected.DesiredOutputMassGrams
            && order.rejectedRecoveryOutputMassGrams
                == expected.ClampedOutputMassGrams
            && string.Equals(
                order.rejectedRecoverySourceDigest,
                expected.SourceDigest,
                StringComparison.Ordinal)
            && order.recoveryOutputs.Count == expected.Outputs.Count
            && order.recoveryOutputs.Select((output, index) =>
                    output != null
                    && string.Equals(
                        output.itemId,
                        expected.Outputs[index].ItemId,
                        StringComparison.Ordinal)
                    && output.amount == expected.Outputs[index].Quantity)
                .All(value => value);
        if (!matches)
        {
            failureReason = "combat-craft-rejected-recovery-frozen-drift";
        }
        return matches;
    }

    internal bool TryValidateEmptyRejectedRecovery(
        CombatEquipmentCraftOrderSaveData order,
        out string failureReason)
    {
        bool empty = order != null
            && !order.rejectedRecoveryFactorsCaptured
            && !order.rejectedRecoveryProjected
            && order.rejectedRecoveryWorkerSkill == 0f
            && order.rejectedRecoverySalvageMultiplier == 0f
            && order.rejectedRecoveryDesiredMassGrams == 0L
            && order.rejectedRecoveryOutputMassGrams == 0L
            && string.IsNullOrEmpty(order.rejectedRecoverySourceDigest)
            && order.recoveryOutputs != null
            && order.recoveryOutputs.Count == 0
            && order.spawnedRecoveryAmounts != null
            && order.spawnedRecoveryAmounts.Count == 0
            && string.IsNullOrEmpty(order.rejectedDismantleOperationId)
            && string.IsNullOrEmpty(order.rejectedDismantleCommitId)
            && string.IsNullOrEmpty(
                order.rejectedDismantleRequestFingerprint)
            && order.rejectedDismantleInputMassGrams == 0L
            && !order.rejectedRecoveryPublished
            && !order.rejectedDismantleAcknowledged;
        failureReason = empty
            ? string.Empty
            : "combat-craft-rejected-recovery-stale-state";
        return empty;
    }

    private static bool IsValidRejectedRecoveryFactor(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static void ResetRejectedRecoveryProjection(
        CombatEquipmentCraftOrderSaveData order)
    {
        order.rejectedRecoveryFactorsCaptured = false;
        order.rejectedRecoveryProjected = false;
        order.rejectedRecoveryWorkerSkill = 0f;
        order.rejectedRecoverySalvageMultiplier = 0f;
        order.rejectedRecoveryDesiredMassGrams = 0L;
        order.rejectedRecoveryOutputMassGrams = 0L;
        order.rejectedRecoverySourceDigest = string.Empty;
    }

    private void PrepareNextEquipmentAttempt(
        CombatEquipmentCraftOrderSaveData order)
    {
        CombatEquipmentCraftMaterialOutbox.ClearCompletedAttempt(order);
        order.rejectedInstanceId = string.Empty;
        order.rejectedStackId = string.Empty;
        order.rejectedOutputConsumed = false;
        order.recoveryOutputs.Clear();
        order.spawnedRecoveryAmounts.Clear();
        ResetRejectedRecoveryProjection(order);
        CombatEquipmentRejectedDismantleOutbox.Clear(order);
        order.qualityAttemptIndex++;
        if (HasReachedEquipmentRepeatLimit(order))
        {
            RequireClosedInputDestination(
                order,
                "combat-craft-repeat-limit-reached");
            orders.Remove(order);
            return;
        }
        order.qualityRoll = qualityResolver.Roll(
            unchecked((ulong)(uint)(runSeedProvider?.RunSeed ?? 1)),
            order.orderId,
            order.definitionId,
            order.qualityAttemptIndex);
        order.requiredWork = Mathf.Max(0.1f, order.craftWorkPerAttempt);
        order.completedWork = 0f;
        order.materialsReady = false;
        order.contributions.Clear();
        order.qualityStage = QualityTargetPipelineStage.WaitingForMaterials;
        if (RevalidateQualityBlocker(order))
        {
            RequestEquipmentMaterials(order);
        }
    }

    private bool RevalidateQualityBlocker(
        CombatEquipmentCraftOrderSaveData order,
        CharacterActor currentWorker = null)
    {
        if (order == null || IsAmmunitionRecipe(order.definitionId))
        {
            return true;
        }
        if (!TryGetBestEligibleEquipmentSkill(
                order.workerPolicy,
                currentWorker,
                out float bestSkill))
        {
            order.qualityStage =
                QualityTargetPipelineStage.WaitingForEligibleWorker;
            if (string.IsNullOrEmpty(order.materialTransferOperationId))
            {
                ReleaseOrderMaterials(order);
            }
            return false;
        }
        CraftQualityResolution theoreticalBest = qualityResolver.Resolve(
            new CraftQualityRollSaveData
            {
                attemptIndex = order.qualityAttemptIndex,
                randomA = 10,
                randomB = 10,
                randomC = 10
            },
            bestSkill,
            order.facilityQualityBonus,
            toolBonus: 0f,
            complexityPenalty: Mathf.Clamp(
                order.craftWorkPerAttempt / 20f,
                0f,
                25f));
        if ((int)theoreticalBest.Tier < (int)order.minimumQuality)
        {
            order.qualityStage =
                QualityTargetPipelineStage.TargetCurrentlyUnreachable;
            if (string.IsNullOrEmpty(order.materialTransferOperationId))
            {
                ReleaseOrderMaterials(order);
            }
            return false;
        }
        if (order.qualityStage is
                QualityTargetPipelineStage.WaitingForEligibleWorker
            or QualityTargetPipelineStage.TargetCurrentlyUnreachable)
        {
            order.qualityStage = QualityTargetPipelineStage.WaitingForMaterials;
            RequestEquipmentMaterials(order);
        }
        return true;
    }

    private bool TryGetBestEligibleEquipmentSkill(
        WorkerSelectionPolicySaveData policy,
        CharacterActor currentWorker,
        out float bestSkill)
    {
        bestSkill = currentWorker != null
            && WorkerSelectionPolicyRules.IsEligible(
                policy,
                currentWorker,
                narrativeQualification,
                out _)
                ? GetEquipmentQualitySkill(currentWorker)
                : 50f;
        if (characterWorld == null)
        {
            return true;
        }
        bestSkill = currentWorker != null
            && WorkerSelectionPolicyRules.IsEligible(
                policy,
                currentWorker,
                narrativeQualification,
                out _)
                ? GetEquipmentQualitySkill(currentWorker)
                : -1f;
        foreach (CharacterActor actor in characterWorld.Characters)
        {
            if (actor != null
                && WorkerSelectionPolicyRules.IsEligible(
                    policy,
                    actor,
                    narrativeQualification,
                    out _))
            {
                bestSkill = Mathf.Max(
                    bestSkill,
                    GetEquipmentQualitySkill(actor));
            }
        }
        return bestSkill >= 0f;
    }

    private float GetEquipmentQualitySkill(CharacterActor actor)
    {
        if (actor == null) return 25f;
        int experience = narrativeQualification?.GetSkillExperience(
            actor.Identity?.PersistentId ?? string.Empty,
            BuiltInCharacterProficiencyIds.Crafting.Value) ?? 0;
        float baseQuality = ProficiencyProgressionRules.ResolveEffects(
            Math.Max(0L, experience)
                * ProficiencyProgressionRules.MilliPerExperience).QualityScore;
        return actor.ProjectDetailedStat(
            GameplayEffectTargetIds.CraftQualityScore,
            baseQuality,
            new[] { "work:craft-finished" }).Value;
    }

    private void ReleaseOrderMaterials(CombatEquipmentCraftOrderSaveData order)
    {
        if (inputDestinations != null
            && inputDestinations.TryClear(
                order,
                "combat-craft-quality-blocked",
                out _))
        {
            order.materialsReady = false;
        }
    }

    private void RequestEquipmentMaterials(
        CombatEquipmentCraftOrderSaveData order)
    {
        if (!TryGetConcreteMaterials(
                order,
                out IReadOnlyDictionary<string, int> inputs))
        {
            return;
        }
        inputDestinations?.TryRequest(order, inputs, out _);
    }

    private void RequireClosedInputDestination(
        CombatEquipmentCraftOrderSaveData order,
        string reasonCode)
    {
        string failureReason = "combat-craft-input-runtime-unavailable";
        if (inputDestinations != null
            && inputDestinations.TryClose(
                order,
                reasonCode,
                out failureReason))
        {
            return;
        }
        throw new InvalidOperationException(
            "Combat craft input destination could not close for order '"
            + (order?.orderId ?? "<null>") + "': "
            + (inputDestinations == null
                ? "combat-craft-input-runtime-unavailable"
                : failureReason));
    }

    private static bool HasReachedEquipmentRepeatLimit(
        CombatEquipmentCraftOrderSaveData order)
    {
        return order.repeatLimitMode == QualityRepeatLimitMode.SafeLimits
            && (order.qualityAttemptIndex + 1
                    >= Mathf.Max(1, order.maximumAttempts)
                || (order.workBudget > 0f
                    && order.consumedWork >= order.workBudget));
    }

    public CombatEquipmentInstance CreateExternalInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        string materialId)
    {
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition))
        {
            throw new KeyNotFoundException(
                $"Unknown external combat equipment definition '{definitionId}'.");
        }
        if (!TryResolveMaterial(
                definition,
                materialId,
                out CraftMaterialDefinitionSO material,
                out string failureReason))
        {
            throw new ArgumentException(failureReason, nameof(materialId));
        }

        CombatEquipmentInstance instance = new CombatEquipmentInstance
        {
            instanceId = itemInstances.AllocateItemInstanceId().Value,
            definitionId = definition.EquipmentId,
            materialId = material?.MaterialId
                ?? ResolveRequestedMaterialId(definition, materialId),
            quality = quality,
            durabilityRatio = 1f,
            powerCharge = 100f,
            loadedAmmunition = new LoadedAmmunitionBatch(),
            worldState = CombatEquipmentWorldState.Equipped,
            moduleSlots = Enumerable.Range(0, definition.ModuleSlotCount)
                .Select(index => new EquipmentModuleSlotState { slotIndex = index })
                .ToList()
        };
        Instances.Add(instance.instanceId, instance);
        return instance.Clone();
    }

    public CraftMaterialDefinitionSO ResolveInstanceMaterial(
        CombatEquipmentInstance instance,
        CombatEquipmentDefinitionSO definition)
    {
        if (materialCatalog.Materials.Count == 0 || definition == null)
        {
            return null;
        }
        string materialId = ResolveRequestedMaterialId(definition, instance?.materialId);
        return materialCatalog.TryGetMaterial(materialId, out CraftMaterialDefinitionSO material)
            && definition.AllowsMaterial(material)
                ? material
                : null;
    }

    public string NormalizeRestoredMaterialId(
        CombatEquipmentDefinitionSO definition,
        string materialId)
    {
        string normalized = ResolveRequestedMaterialId(definition, materialId);
        if (materialCatalog.Materials.Count == 0)
        {
            return normalized;
        }
        return materialCatalog.TryGetMaterial(normalized, out CraftMaterialDefinitionSO material)
            && definition.AllowsMaterial(material)
                ? material.MaterialId
                : definition.DefaultMaterialId;
    }

    public IReadOnlyList<CombatEquipmentCraftOrderSaveData> CaptureOrders() =>
        orders
            .Where(order => order != null)
            .Select(order => order.Clone())
            .ToArray();

    internal void ValidateInputDestinationsBeforeCapture()
    {
        if (inputDestinations == null)
        {
            throw new InvalidOperationException(
                "Combat craft input destination runtime is unavailable.");
        }
        foreach (CombatEquipmentCraftOrderSaveData order in orders
                     .Where(value => value != null)
                     .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            string failureReason = "combat-craft-materials-unavailable";
            if (!TryGetConcreteMaterials(
                    order,
                    out IReadOnlyDictionary<string, int> requirements)
                || !inputDestinations.TryValidateAuthority(
                    order,
                    requirements,
                    out failureReason))
            {
                throw new InvalidOperationException(
                    "Combat craft input authority is invalid for order '"
                    + order.orderId + "': " + failureReason);
            }
        }
    }

    internal bool TryValidateInputDestinationProjection(
        CombatEquipmentCraftOrderSaveData order,
        out string failureReason)
    {
        failureReason = string.Empty;
        return inputDestinations != null
            && TryGetConcreteMaterials(
                order,
                out IReadOnlyDictionary<string, int> requirements)
            && inputDestinations.TryValidateProjection(
                order,
                requirements,
                out failureReason);
    }

    internal bool TryReplaceInputDestinations(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> restoredOrders,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (inputDestinations == null)
        {
            failureReason = "combat-craft-input-runtime-unavailable";
            return false;
        }
        List<CombatEquipmentCraftInputDestinationProjection> desired = new();
        foreach (CombatEquipmentCraftOrderSaveData order in
                 (restoredOrders ?? Array.Empty<
                     CombatEquipmentCraftOrderSaveData>())
                 .Where(value => value != null)
                 .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            if (!TryGetConcreteMaterials(
                    order,
                    out IReadOnlyDictionary<string, int> requirements))
            {
                failureReason =
                    "combat-craft-input-restore-requirements-invalid:"
                    + order.orderId;
                return false;
            }
            desired.Add(new CombatEquipmentCraftInputDestinationProjection(
                order,
                requirements));
        }
        return inputDestinations.TryReplace(desired, out failureReason);
    }

    public IReadOnlyList<CombatEquipmentCraftMaterialPolicySaveData> CapturePolicies() =>
        materialPolicies.Values.Select(policy => policy.Clone()).ToArray();

    internal void PopulateRestoreState(
        CombatEquipmentRuntimeState target,
        IEnumerable<CombatEquipmentCraftOrderSaveData> savedOrders,
        IEnumerable<CombatEquipmentCraftMaterialPolicySaveData> savedPolicies)
    {
        CombatEquipmentRuntimeState requiredTarget = target
            ?? throw new ArgumentNullException(nameof(target));
        HashSet<string> orderIds = new(StringComparer.Ordinal);
        foreach (CombatEquipmentCraftOrderSaveData source in savedOrders
                     ?? Array.Empty<CombatEquipmentCraftOrderSaveData>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.orderId)
                || !orderIds.Add(source.orderId)
                || (!IsAmmunitionRecipe(source.definitionId)
                    && (!catalog.TryGet(source.definitionId, out _)
                        || !IsDefinitionUnlocked(source.definitionId, out _))))
            {
                continue;
            }

            CombatEquipmentCraftOrderSaveData restored = source.Clone();
            if (catalog.TryGet(restored.definitionId, out CombatEquipmentDefinitionSO definition))
            {
                restored.materialId = NormalizeRestoredMaterialId(
                    definition,
                    restored.materialId);
            }
            requiredTarget.CraftOrders.Add(restored);
        }

        foreach (CombatEquipmentCraftMaterialPolicySaveData source in savedPolicies
                     ?? Array.Empty<CombatEquipmentCraftMaterialPolicySaveData>())
        {
            CombatEquipmentCraftMaterialPolicySaveData restored =
                NormalizeMaterialPolicy(source);
            string key = BuildMaterialPolicyKey(
                restored.facilityKey,
                restored.definitionId);
            if (string.IsNullOrWhiteSpace(restored.facilityKey)
                || string.IsNullOrWhiteSpace(restored.definitionId)
                || requiredTarget.CraftMaterialPolicies.ContainsKey(key))
            {
                continue;
            }
            requiredTarget.CraftMaterialPolicies.Add(key, restored);
        }
    }

    private bool EnsureMaterialsReady(CombatEquipmentCraftOrderSaveData order)
    {
        if (order == null)
        {
            return false;
        }
        if (order.materialsReady)
        {
            return true;
        }

        CombatEquipmentDefinitionSO definition = null;
        if (!IsAmmunitionRecipe(order.definitionId)
            && !catalog.TryGet(order.definitionId, out definition))
        {
            return false;
        }
        CraftMaterialDefinitionSO material = null;
        if (definition != null
            && !TryResolveMaterial(definition, order.materialId, out material, out _))
        {
            return false;
        }
        if (!TryBuildConcreteMaterials(
                definition,
                order.definitionId,
                material,
                out IReadOnlyDictionary<string, int> materials,
                out _))
        {
            return false;
        }
        if (materials.Count == 0)
        {
            order.materialsReady = true;
            return true;
        }
        if (string.IsNullOrWhiteSpace(order.materialDestinationId)
            || !CombatEquipmentCraftMaterialOutbox.TryCommitOrResume(
                order,
                materials,
                physicalItems.GetAllStacks(),
                physicalItems,
                out _))
        {
            return false;
        }
        return true;
    }

    private static bool TryBuildConcreteMaterials(
        CombatEquipmentDefinitionSO definition,
        string definitionId,
        CraftMaterialDefinitionSO material,
        out IReadOnlyDictionary<string, int> result,
        out string failureReason)
    {
        if (!CombatCraftConcreteInputProjection.TryCapture(
                definition,
                definitionId,
                material,
                out CombatCraftConcreteInputSnapshot snapshot,
                out failureReason))
        {
            result = new Dictionary<string, int>(StringComparer.Ordinal);
            return false;
        }
        result = snapshot.Inputs.ToDictionary(
            value => value.ItemId,
            value => value.Amount,
            StringComparer.Ordinal);
        return true;
    }

    private bool TryResolveMaterial(
        CombatEquipmentDefinitionSO definition,
        string requestedMaterialId,
        out CraftMaterialDefinitionSO material,
        out string failureReason)
    {
        material = null;
        failureReason = string.Empty;
        if (definition == null)
        {
            failureReason = "equipment.definition.unknown";
            return false;
        }
        string normalizedId = ResolveRequestedMaterialId(
            definition,
            requestedMaterialId);
        if (materialCatalog.Materials.Count == 0)
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(normalizedId)
            || !materialCatalog.TryGetMaterial(normalizedId, out material))
        {
            failureReason = "equipment.material.unknown";
            return false;
        }
        if (!definition.AllowsMaterial(material))
        {
            failureReason = "equipment.material.not_allowed";
            material = null;
            return false;
        }
        return true;
    }

    private bool TryGetOrCreateMaterialPolicy(
        string definitionId,
        BuildableObject craftingFacility,
        out CombatEquipmentCraftMaterialPolicySaveData policy,
        out string failureReason)
    {
        policy = null;
        failureReason = string.Empty;
        if (craftingFacility == null)
        {
            failureReason = "equipment.craft.facility_required";
            return false;
        }
        if (!TryRequireMutable(craftingFacility, out failureReason))
        {
            return false;
        }
        string normalizedDefinitionId = definitionId?.Trim() ?? string.Empty;
        if (!catalog.TryGet(normalizedDefinitionId, out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "equipment.definition.unknown";
            return false;
        }
        IReadOnlyList<CraftMaterialDefinitionSO> allowedMaterials =
            GetAllowedMaterials(normalizedDefinitionId);
        if (allowedMaterials.Count == 0)
        {
            failureReason = "equipment.material.none_available";
            return false;
        }

        string facilityKey = craftingFacility.RequirePersistentInstanceId().Value;
        string policyKey = BuildMaterialPolicyKey(facilityKey, normalizedDefinitionId);
        if (materialPolicies.TryGetValue(policyKey, out policy))
        {
            policy = NormalizeMaterialPolicy(policy);
            materialPolicies[policyKey] = policy;
            return true;
        }

        List<string> priority = allowedMaterials
            .OrderBy(material => string.Equals(
                    material.MaterialId,
                    definition.DefaultMaterialId,
                    StringComparison.Ordinal)
                ? 0
                : 1)
            .ThenBy(material => material.RareMaterial ? 1 : 0)
            .ThenBy(material => material.DisplayName, StringComparer.Ordinal)
            .Select(material => material.MaterialId)
            .ToList();
        List<string> allowed = allowedMaterials
            .Where(material => !material.RareMaterial)
            .Select(material => material.MaterialId)
            .ToList();
        if (allowed.Count == 0 && priority.Count > 0)
        {
            allowed.Add(priority[0]);
        }
        policy = new CombatEquipmentCraftMaterialPolicySaveData
        {
            facilityKey = facilityKey,
            definitionId = normalizedDefinitionId,
            priorityMaterialIds = priority,
            allowedMaterialIds = allowed
        };
        materialPolicies.Add(policyKey, policy);
        return true;
    }

    private bool TryRequireMutable(
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (facility == null)
        {
            failureReason = "equipment.craft.facility_required";
            return false;
        }

        if (ProductionFacilityMutationWorkPolicy.TryRequireMutable(
                facilityMutations,
                facility.RequirePersistentInstanceId(),
                out DomainFailure failure))
        {
            return true;
        }

        failureReason = failure.Parameters.Length > 0
            ? failure.Parameters[failure.Parameters.Length - 1]
            : "production-facility-mutation-open";
        return false;
    }

    private bool IsOrderFacilityMutable(
        CombatEquipmentCraftOrderSaveData order) => order != null
        && ProductionFacilityMutationWorkPolicy.IsMutable(
            facilityMutations,
            new BuildingInstanceId(order.facilityPersistentId));

    private bool TryRequireMutable(
        CombatEquipmentCraftOrderSaveData order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || string.IsNullOrWhiteSpace(order.facilityPersistentId))
        {
            failureReason = "equipment.craft.facility_required";
            return false;
        }
        if (ProductionFacilityMutationWorkPolicy.TryRequireMutable(
                facilityMutations,
                new BuildingInstanceId(order.facilityPersistentId),
                out DomainFailure failure))
        {
            return true;
        }
        failureReason = failure.Parameters.Length > 0
            ? failure.Parameters[failure.Parameters.Length - 1]
            : "production-facility-mutation-open";
        return false;
    }

    private static bool IsTerminalConvergenceRetry(
        CombatEquipmentCraftOrderSaveData order) => order != null
        && (order.attemptOutcomeResolved
            || (order.dismantlingRejectedOutput
                && order.completedWork + 0.0001f
                    >= Mathf.Max(0f, order.requiredWork)));

    private CombatEquipmentCraftMaterialPolicySaveData NormalizeMaterialPolicy(
        CombatEquipmentCraftMaterialPolicySaveData source)
    {
        CombatEquipmentCraftMaterialPolicySaveData clone =
            source?.Clone() ?? new CombatEquipmentCraftMaterialPolicySaveData();
        if (!catalog.TryGet(clone.definitionId, out CombatEquipmentDefinitionSO definition))
        {
            return new CombatEquipmentCraftMaterialPolicySaveData();
        }
        Dictionary<string, CraftMaterialDefinitionSO> allowedById =
            GetAllowedMaterials(definition.EquipmentId)
                .ToDictionary(
                    material => material.MaterialId,
                    material => material,
                    StringComparer.Ordinal);
        List<string> priority = clone.priorityMaterialIds
            .Where(allowedById.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (CraftMaterialDefinitionSO material in allowedById.Values
                     .OrderBy(candidate => string.Equals(
                             candidate.MaterialId,
                             definition.DefaultMaterialId,
                             StringComparison.Ordinal)
                         ? 0
                         : 1)
                     .ThenBy(candidate => candidate.RareMaterial ? 1 : 0)
                     .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal))
        {
            if (!priority.Contains(material.MaterialId, StringComparer.Ordinal))
            {
                priority.Add(material.MaterialId);
            }
        }
        return new CombatEquipmentCraftMaterialPolicySaveData
        {
            facilityKey = clone.facilityKey,
            definitionId = definition.EquipmentId,
            priorityMaterialIds = priority,
            allowedMaterialIds = clone.allowedMaterialIds
                .Where(allowedById.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };
    }

    private static string ResolveRequestedMaterialId(
        CombatEquipmentDefinitionSO definition,
        string requestedMaterialId)
    {
        return string.IsNullOrWhiteSpace(requestedMaterialId)
            ? definition?.DefaultMaterialId ?? string.Empty
            : requestedMaterialId.Trim();
    }

    private static string BuildMaterialPolicyKey(string facilityKey, string definitionId)
    {
        return $"{facilityKey?.Trim() ?? string.Empty}|"
            + $"{definitionId?.Trim() ?? string.Empty}";
    }

    private static bool IsCraftable(
        string definitionId,
        IEnumerable<string> craftableDefinitionIds)
    {
        if (string.IsNullOrWhiteSpace(definitionId)
            || !string.Equals(
                definitionId,
                definitionId.Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }
        IReadOnlyList<string> allowed = CombatCraftAllowlist.Capture(
            craftableDefinitionIds);
        return allowed.Contains(definitionId, StringComparer.Ordinal);
    }

    public static bool IsAmmunitionRecipe(string definitionId)
    {
        return CombatAmmunitionCraftDefinitions.TryGetExact(
            definitionId,
            out _);
    }
}
