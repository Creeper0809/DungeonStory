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
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IBalanceWorkCalculator balanceWorkCalculator;
    private readonly ICraftQualityResolver qualityResolver;
    private readonly IRunSeedProvider runSeedProvider;
    private readonly IWorkerNarrativeQualificationQuery narrativeQualification;
    private readonly IMaterialSalvageCalculator salvageCalculator;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly ExtremeCraftInspirationRuntime inspirationRuntime;
    private readonly IGameClock gameClock;
    private readonly CharacterIdentityEventPublisher identityEvents;

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
        IBalanceWorkCalculator balanceWorkCalculator = null,
        ICraftQualityResolver qualityResolver = null,
        IRunSeedProvider runSeedProvider = null,
        IWorkerNarrativeQualificationQuery narrativeQualification = null,
        IMaterialSalvageCalculator salvageCalculator = null,
        ICharacterWorldQuery characterWorld = null,
        ExtremeCraftInspirationRuntime inspirationRuntime = null,
        IGameClock gameClock = null,
        CharacterIdentityEventPublisher identityEvents = null)
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
        this.balanceWorkCalculator = balanceWorkCalculator;
        this.qualityResolver = qualityResolver
            ?? new DeterministicCraftQualityResolver();
        this.runSeedProvider = runSeedProvider;
        this.narrativeQualification = narrativeQualification;
        this.salvageCalculator = salvageCalculator;
        this.characterWorld = characterWorld;
        this.inspirationRuntime = inspirationRuntime;
        this.gameClock = gameClock;
        this.identityEvents = identityEvents;
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
        string normalizedId = definitionId?.Trim() ?? string.Empty;
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
        string normalizedId = definitionId?.Trim() ?? string.Empty;
        bool ammunitionRecipe = IsAmmunitionRecipe(normalizedId);
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
        string destinationId = WorldItemStackRuntime.FacilityInputDestinationPrefix + orderId;
        foreach (KeyValuePair<string, int> cost in materials)
        {
            if (!physicalItems.TryRequestItemDelivery(
                    cost.Key,
                    cost.Value,
                    craftingFacility.centerPos,
                    destinationId,
                    out int requested,
                    out string requestFailure)
                || requested < cost.Value)
            {
                physicalItems.ReleaseStacksByDestination(
                    destinationId,
                    craftingFacility.centerPos);
                failureReason = string.IsNullOrWhiteSpace(requestFailure)
                    ? "equipment.craft.materials_missing"
                    : requestFailure;
                return false;
            }
        }

        int attemptIndex = 0;
        float requiredWork = ammunitionRecipe
            ? 4f
            : balanceWorkCalculator?.CalculateEquipment(
                definition,
                material?.ItemId)
                ?? definition.RequiredCraftWork;
        stateStore.Current.NextCraftSequence = checked(orderSequence + 1);
        orders.Add(new CombatEquipmentCraftOrderSaveData
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
        });
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
        if (order == null
            || IsAmmunitionRecipe(order.definitionId)
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
                || !AreMaterialsAvailable(order))
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
        if (string.Equals(
                order.definitionId,
                CombatItemDefinitions.ArrowBundleRecipeId,
                StringComparison.Ordinal))
        {
            order.outputItemId = CombatItemDefinitions.ArrowItemId;
            order.outputQuantity = 20;
        }
        else if (string.Equals(
                     order.definitionId,
                     CombatItemDefinitions.BoltBundleRecipeId,
                     StringComparison.Ordinal))
        {
            order.outputItemId = CombatItemDefinitions.BoltItemId;
            order.outputQuantity = 12;
        }
        else
        {
            order.outputItemId = PhysicalItemIds.ForEquipment(order.definitionId);
            order.outputQuantity = 1;
        }
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
            || !TryGetConcreteMaterials(
                order,
                out IReadOnlyDictionary<string, int> materials))
        {
            return false;
        }

        bool accepted = (int)order.resolvedQuality >= (int)order.minimumQuality;
        string outputDestination = !accepted
            && order.rejectedDisposition == RejectedOutputDisposition.MarkForSale
                ? QualityRejectedOutputRules.MarketDestinationId
                : ProductionBillRuntime.OutputDestinationPrefix
                    + order.facilityPersistentId;
        Vector2Int position = new(order.destinationX, order.destinationY);
        bool outputReady = IsAmmunitionRecipe(order.definitionId)
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

        order.consumedWork += Mathf.Max(0f, order.craftWorkPerAttempt);
        if (!accepted)
        {
            order.rejectedInstanceId = order.outputInstanceId;
            order.rejectedStackId = order.outputStackId;
            if (HasReachedEquipmentRepeatLimit(order))
            {
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
        order.acceptedCount++;
        if (order.acceptedCount >= Mathf.Max(1, order.requiredAcceptedCount))
        {
            orders.RemoveAt(orderIndex);
        }
        else
        {
            PrepareNextEquipmentAttempt(order);
        }
        return true;
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
            instanceId = itemInstances.AllocateItemInstanceId().Value,
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
        Instances.Add(instance.instanceId, instance);
        return instance.Clone();
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
        order.requiredWork = Mathf.Max(
            0.1f,
            order.craftWorkPerAttempt * 0.25f);
        order.materialsReady = true;
        order.contributions.Clear();
        order.recoveryOutputs.Clear();
        order.spawnedRecoveryAmounts.Clear();
        CombatEquipmentRejectedDismantleOutbox.Clear(order);
    }

    private bool TryResolveRejectedEquipmentDismantle(
        CombatEquipmentCraftOrderSaveData order)
    {
        if (order.recoveryOutputs.Count == 0)
        {
            BuildRejectedEquipmentRecovery(order);
        }
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
        CombatEquipmentRejectedDismantleOutbox.Clear(order);
        PrepareNextEquipmentAttempt(order);
        return true;
    }

    private void BuildRejectedEquipmentRecovery(
        CombatEquipmentCraftOrderSaveData order)
    {
        if (!catalog.TryGet(
                order.definitionId,
                out CombatEquipmentDefinitionSO definition)
            || !TryResolveMaterial(
                definition,
                order.materialId,
                out CraftMaterialDefinitionSO material,
                out _)
            || !TryBuildConcreteMaterials(
                definition,
                order.definitionId,
                material,
                out IReadOnlyDictionary<string, int> inputs,
                out _))
        {
            return;
        }

        CraftContributionAccumulator contributions = new(order.contributions);
        string salvageWorkerId = order.contributions
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.characterId))
            .OrderByDescending(value => value.contributedWork)
            .ThenBy(value => value.characterId, StringComparer.Ordinal)
            .Select(value => value.characterId.Trim())
            .FirstOrDefault() ?? string.Empty;
        CharacterActor salvageWorker = characterWorld?.Characters.FirstOrDefault(actor =>
            actor != null && string.Equals(
                actor.Identity?.PersistentId,
                salvageWorkerId,
                StringComparison.Ordinal));
        float salvageYield = salvageWorker != null
            ? salvageWorker.GetDetailedStatMultiplier(
                GameplayEffectTargetIds.SalvageYield)
            : 1f;
        MaterialSalvageResult salvage = salvageCalculator?.Calculate(
                DismantleTargetKind.CombatEquipment,
                order.craftWorkPerAttempt,
                inputs.Select(pair => new ItemAmountDefinition(
                    pair.Key,
                    pair.Value)),
                contributions.WeightedRelevantSkill)
            ?? new MaterialSalvageResult(
                order.requiredWork,
                inputs.Select(pair => new ItemAmountDefinition(
                        pair.Key,
                        Mathf.FloorToInt(pair.Value * 0.60f)))
                    .Where(value => value.Amount > 0)
                    .ToArray());
        foreach (ItemAmountDefinition output in salvage.RecoveredMaterials)
        {
            order.recoveryOutputs.Add(new CombatCraftRecoveryOutputSaveData
            {
                itemId = output.ItemId,
                amount = Mathf.FloorToInt(
                    output.Amount * Mathf.Max(0f, salvageYield))
            });
            order.spawnedRecoveryAmounts.Add(0);
        }
    }

    private void PrepareNextEquipmentAttempt(
        CombatEquipmentCraftOrderSaveData order)
    {
        CombatEquipmentCraftMaterialOutbox.ClearCompletedAttempt(order);
        order.rejectedInstanceId = string.Empty;
        order.rejectedStackId = string.Empty;
        order.rejectedOutputConsumed = false;
        CombatEquipmentRejectedDismantleOutbox.Clear(order);
        order.qualityAttemptIndex++;
        if (HasReachedEquipmentRepeatLimit(order))
        {
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
        physicalItems.ReleaseStacksByDestination(
            order.materialDestinationId,
            new Vector2Int(order.destinationX, order.destinationY));
        order.materialsReady = false;
    }

    private void RequestEquipmentMaterials(
        CombatEquipmentCraftOrderSaveData order)
    {
        if (!catalog.TryGet(
                order.definitionId,
                out CombatEquipmentDefinitionSO definition)
            || !TryResolveMaterial(
                definition,
                order.materialId,
                out CraftMaterialDefinitionSO material,
                out _)
            || !TryBuildConcreteMaterials(
                definition,
                order.definitionId,
                material,
                out IReadOnlyDictionary<string, int> inputs,
                out _))
        {
            return;
        }
        Vector2Int position = new(order.destinationX, order.destinationY);
        foreach (KeyValuePair<string, int> input in inputs)
        {
            physicalItems.TryRequestItemDelivery(
                input.Key,
                input.Value,
                position,
                order.materialDestinationId,
                out _,
                out _);
        }
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
        Dictionary<string, int> materials = new(StringComparer.Ordinal);
        failureReason = string.Empty;
        if (string.Equals(
                definitionId,
                CombatItemDefinitions.ArrowBundleRecipeId,
                StringComparison.Ordinal))
        {
            materials["material:lumber"] = 1;
            materials["resource:feather"] = 1;
            result = materials;
            return true;
        }
        if (string.Equals(
                definitionId,
                CombatItemDefinitions.BoltBundleRecipeId,
                StringComparison.Ordinal))
        {
            materials["material:lumber"] = 1;
            materials["material:iron-ingot"] = 1;
            result = materials;
            return true;
        }
        if ((definition?.CraftMaterials?.Count ?? 0) > 0)
        {
            result = materials;
            failureReason = "equipment.craft.legacy_stock_category_input";
            return false;
        }
        if (material != null && !string.IsNullOrWhiteSpace(material.ItemId))
        {
            materials[material.ItemId] = Mathf.Max(1, definition.PrimaryMaterialAmount);
        }
        foreach (ItemAmountDefinition component in definition?.RequiredComponentInputs
                     ?? Array.Empty<ItemAmountDefinition>())
        {
            if (component == null
                || string.IsNullOrWhiteSpace(component.ItemId)
                || component.Amount <= 0)
            {
                continue;
            }
            materials.TryGetValue(component.ItemId, out int current);
            materials[component.ItemId] = current + component.Amount;
        }
        result = materials;
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
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            return false;
        }
        string[] allowed = craftableDefinitionIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        return allowed.Length == 0
            || allowed.Contains(definitionId, StringComparer.Ordinal);
    }

    public static bool IsAmmunitionRecipe(string definitionId)
    {
        return string.Equals(
                definitionId,
                CombatItemDefinitions.ArrowBundleRecipeId,
                StringComparison.Ordinal)
            || string.Equals(
                definitionId,
                CombatItemDefinitions.BoltBundleRecipeId,
                StringComparison.Ordinal);
    }
}
