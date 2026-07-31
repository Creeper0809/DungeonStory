using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class ProductionBillRecord
{
    public string billId = string.Empty;
    public string recipeId = string.Empty;
    public int buildingId;
    public Vector2Int position;
    public ProductionOrderMode mode;
    public int remainingCycles;
    public int targetStock;
    public int minimumReserve;
    public bool suspended;
    public bool materialsConsumed;
    public bool processFluidConsumed;
    public float completedWork;
    public ProductionBatchStage batchStage;
    public float remainingProcessingHours;
    public float batchIntegrity = 100f;
    public float utilityOutageHours;
    public float temperatureOutageHours;
    public string occupiedSupportNodeId = string.Empty;
    public string blockedReason = string.Empty;
    public string reservedWorkerId = string.Empty;
    public string materialDestinationId = string.Empty;
    public readonly HashSet<string> allowedMaterialIds =
        new HashSet<string>(StringComparer.Ordinal);
    public readonly HashSet<string> allowedWorkerIds =
        new HashSet<string>(StringComparer.Ordinal);
}

public sealed class ProductionBillRuntime : IProductionBillRuntime, ITickable
{
    public const string DestinationPrefix = "production:";
    private const float SecondsPerGameHour = 7.5f;
    private const float SafeUtilityOutageHours = 6f;
    private const float DangerousTemperatureGraceHours = 3f;

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionItemGateway items;
    private readonly IBlueprintResearchRuntimeProvider researchProvider;
    private readonly IWorkforceReplanService workforceReplanService;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly IProcessFluidUseRuntime processFluids;
    private readonly IProductionWorkshopRuntime workshops;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IElectricalNetworkRuntime power;
    private readonly IWaterNetworkRuntime water;
    private readonly IWastewaterNetworkRuntime wastewater;
    private readonly IEnvironmentalFieldRuntime environment;
    private readonly IGameClock clock;
    private readonly IReadOnlyList<IProductionOutputHandler> outputHandlers;
    private readonly IRandomStream random;
    private readonly List<ProductionBillRecord> bills =
        new List<ProductionBillRecord>();
    private int nextBillSequence = 1;

    public ProductionBillRuntime(
        IResourceEconomyContentCatalog catalog,
        IProductionItemGateway items,
        IRandomStreamProvider randomStreamProvider,
        IBlueprintResearchRuntimeProvider researchProvider = null,
        IWorkforceReplanService workforceReplanService = null,
        IGrandProjectBenefitQuery grandProjectBenefits = null,
        IReadOnlyList<IProductionOutputHandler> outputHandlers = null,
        IProcessFluidUseRuntime processFluids = null,
        IProductionWorkshopRuntime workshops = null,
        IBuildingWorldQuery buildingWorld = null,
        IElectricalNetworkRuntime power = null,
        IWaterNetworkRuntime water = null,
        IWastewaterNetworkRuntime wastewater = null,
        IEnvironmentalFieldRuntime environment = null,
        IGameClock clock = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        random = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("economy:production");
        this.researchProvider = researchProvider;
        this.workforceReplanService = workforceReplanService;
        this.grandProjectBenefits = grandProjectBenefits;
        this.outputHandlers = outputHandlers ?? Array.Empty<IProductionOutputHandler>();
        this.processFluids = processFluids;
        this.workshops = workshops;
        this.buildingWorld = buildingWorld;
        this.power = power;
        this.water = water;
        this.wastewater = wastewater;
        this.environment = environment;
        this.clock = clock;
    }

    public int Version { get; private set; }

    public IReadOnlyList<ProductionBillSnapshot> GetBills(BuildableObject facility)
    {
        if (facility == null)
        {
            return Array.Empty<ProductionBillSnapshot>();
        }

        return bills
            .Where(record => MatchesFacility(record, facility))
            .Select(record => ToSnapshot(record, facility))
            .ToArray();
    }

    public ProductionBillCommandResult AddBill(
        BuildableObject facility,
        string recipeId,
        ProductionOrderMode mode,
        int amount)
    {
        if (facility == null || facility.IsGridDestroyed)
        {
            return ProductionBillCommandResult.Failure("생산 시설이 없습니다.");
        }

        if (!catalog.TryGetRecipe(recipeId, out ProductionRecipeSO recipe))
        {
            return ProductionBillCommandResult.Failure("알 수 없는 생산 조합입니다.");
        }

        if (!MatchesRecipeWorkstation(facility, recipe))
        {
            return ProductionBillCommandResult.Failure(
                $"{facility.BuildingData?.objectName ?? "시설"}에서는 이 조합을 만들 수 없습니다.");
        }

        if (!IsResearchUnlocked(recipe, out string researchReason))
        {
            return ProductionBillCommandResult.Failure(researchReason);
        }

        if (workshops != null
            && !workshops.HasRequiredSupports(
                facility,
                recipe.RequiredSupportTags,
                out string supportReason))
        {
            return ProductionBillCommandResult.Failure(supportReason);
        }

        string billId = $"production-bill:{nextBillSequence++}";
        ProductionBillRecord record = new ProductionBillRecord
        {
            billId = billId,
            recipeId = recipe.RecipeId,
            buildingId = facility.id,
            position = facility.centerPos,
            mode = mode,
            remainingCycles = mode == ProductionOrderMode.RepeatCount
                ? Mathf.Max(1, amount)
                : -1,
            targetStock = mode == ProductionOrderMode.MaintainStock
                ? Mathf.Max(1, amount)
                : 0,
            batchStage = recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                ? ProductionBatchStage.Preparing
                : ProductionBatchStage.None,
            batchIntegrity = 100f,
            materialDestinationId = DestinationPrefix + billId
        };
        bills.Add(record);
        RequestMissingInputs(record, recipe, facility);
        Touch(recipe.WorkTypeId, requestWorker: false);
        return ProductionBillCommandResult.Success(billId, "생산 주문을 등록했습니다.");
    }

    public ProductionBillCommandResult RemoveBill(
        string billId,
        bool returnMaterials)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failure("생산 주문을 찾을 수 없습니다.");
        }

        if (returnMaterials && !record.materialsConsumed)
        {
            items.ReleaseDestination(
                record.materialDestinationId,
                record.position);
        }
        else
        {
            items.RemoveDestination(record.materialDestinationId);
        }

        bills.Remove(record);
        Touch(default, requestWorker: false);
        return ProductionBillCommandResult.Success(billId, "생산 주문을 취소했습니다.");
    }

    public ProductionBillCommandResult MoveBill(string billId, int targetIndex)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failure("생산 주문을 찾을 수 없습니다.");
        }

        List<ProductionBillRecord> facilityBills = bills
            .Where(candidate => candidate.buildingId == record.buildingId
                && candidate.position == record.position)
            .ToList();
        int currentLocalIndex = facilityBills.IndexOf(record);
        int clampedTarget = Mathf.Clamp(targetIndex, 0, facilityBills.Count - 1);
        if (currentLocalIndex == clampedTarget)
        {
            return ProductionBillCommandResult.Success(billId);
        }

        ProductionBillRecord anchor = facilityBills[clampedTarget];
        bills.Remove(record);
        int anchorIndex = bills.IndexOf(anchor);
        bills.Insert(
            currentLocalIndex < clampedTarget ? anchorIndex + 1 : anchorIndex,
            record);
        Touch(default, requestWorker: false);
        return ProductionBillCommandResult.Success(billId);
    }

    public ProductionBillCommandResult SetSuspended(
        string billId,
        bool suspended)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failure("생산 주문을 찾을 수 없습니다.");
        }

        record.suspended = suspended;
        record.reservedWorkerId = string.Empty;
        Touch(ResolveRecipe(record)?.WorkTypeId ?? default, requestWorker: !suspended);
        return ProductionBillCommandResult.Success(billId);
    }

    public ProductionBillCommandResult SetStockPolicy(
        string billId,
        int minimumReserve,
        int targetStock)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failure("생산 주문을 찾을 수 없습니다.");
        }

        record.minimumReserve = Mathf.Max(0, minimumReserve);
        record.targetStock = Mathf.Max(record.minimumReserve, targetStock);
        record.mode = ProductionOrderMode.MaintainStock;
        Touch(ResolveRecipe(record)?.WorkTypeId ?? default, requestWorker: true);
        return ProductionBillCommandResult.Success(billId);
    }

    public bool HasWorkAvailable(
        BuildableObject facility,
        WorkTypeId workTypeId,
        out string reason)
    {
        reason = string.Empty;
        ProductionBillRecord record = FindRunnableBill(
            facility,
            workTypeId,
            requireDeliveredInputs: true,
            out reason);
        return record != null;
    }

    public bool TryBeginWork(
        CharacterActor worker,
        BuildableObject facility,
        WorkTypeId workTypeId,
        out ProductionBillSnapshot bill,
        out string failureReason)
    {
        bill = null;
        ProductionBillRecord record = FindRunnableBill(
            facility,
            workTypeId,
            requireDeliveredInputs: true,
            out failureReason);
        if (record == null)
        {
            return false;
        }

        ProductionRecipeSO recipe = ResolveRecipe(record);
        if (!TryValidateCycleStart(
                record,
                recipe,
                facility,
                out failureReason))
        {
            record.blockedReason = failureReason;
            Touch(default, requestWorker: false);
            return false;
        }

        if (!record.materialsConsumed
            && !items.ConsumeDelivered(
                record.materialDestinationId,
                ToCycleInputMap(recipe, facility),
                out failureReason))
        {
            RequestMissingInputs(record, recipe, facility);
            return false;
        }

        record.materialsConsumed = true;
        if (!record.processFluidConsumed
            && !TryConsumeCycleUtilities(
                record,
                recipe,
                facility,
                out failureReason))
        {
            record.blockedReason = failureReason;
            return false;
        }

        record.processFluidConsumed = true;
        record.blockedReason = string.Empty;
        record.reservedWorkerId = worker?.Identity?.PersistentId ?? string.Empty;
        Touch(default, requestWorker: false);
        bill = ToSnapshot(record, facility);
        return true;
    }

    public bool ApplyWork(
        CharacterActor worker,
        BuildableObject facility,
        string billId,
        float amount,
        out bool cycleCompleted,
        out string message)
    {
        cycleCompleted = false;
        message = string.Empty;
        ProductionBillRecord record = Find(billId);
        ProductionRecipeSO recipe = ResolveRecipe(record);
        if (record == null
            || recipe == null
            || !MatchesFacility(record, facility)
            || record.suspended
            || !record.materialsConsumed)
        {
            message = "생산 주문을 계속할 수 없습니다.";
            return false;
        }

        string workerId = worker?.Identity?.PersistentId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(record.reservedWorkerId)
            && !string.Equals(record.reservedWorkerId, workerId, StringComparison.Ordinal))
        {
            message = "다른 작업자가 사용 중입니다.";
            return false;
        }

        record.reservedWorkerId = workerId;
        float requiredWork = ResolveCurrentRequiredWork(record, recipe);
        float supportWorkMultiplier =
            ResolveSupportModifier(
                facility,
                recipe,
                ability => ability.workSpeedMultiplier,
                1f,
                multiply: true);
        record.completedWork = Mathf.Clamp(
            record.completedWork
                + Mathf.Max(0f, amount) * supportWorkMultiplier,
            0f,
            requiredWork);
        if (record.completedWork + 0.001f < requiredWork)
        {
            message =
                $"{recipe.DisplayName} {Mathf.RoundToInt(record.completedWork / requiredWork * 100f)}%";
            return true;
        }

        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            && record.batchStage == ProductionBatchStage.Preparing)
        {
            if (!TryOccupyBatchSupport(
                    record,
                    recipe,
                    facility,
                    out string supportFailure))
            {
                record.blockedReason = supportFailure;
                record.reservedWorkerId = string.Empty;
                message = supportFailure;
                return false;
            }

            record.batchStage = ProductionBatchStage.Processing;
            record.remainingProcessingHours = recipe.ProcessingGameHours;
            record.completedWork = 0f;
            record.reservedWorkerId = string.Empty;
            record.blockedReason = string.Empty;
            Touch(recipe.WorkTypeId, requestWorker: false);
            message = $"{recipe.DisplayName} 시간 공정을 시작했습니다.";
            return true;
        }

        foreach (ProductionOutputDefinition output in recipe.Outputs)
        {
            if (output == null || !random.Chance(output.Probability))
            {
                continue;
            }

            int outputAmount = ResolveOutputAmount(
                output.Amount,
                (grandProjectBenefits?.GetProductionOutputMultiplier(
                    recipe.FacilityTag) ?? 1f)
                * ResolveSupportModifier(
                    facility,
                    recipe,
                    ability => ability.outputMultiplier,
                    1f,
                    multiply: true));
            if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                && record.batchIntegrity < 50f)
            {
                outputAmount = Mathf.Max(1, Mathf.FloorToInt(outputAmount * 0.5f));
            }
            ProductionOutputContext outputContext = new ProductionOutputContext(
                recipe,
                facility,
                worker,
                output.ItemId,
                outputAmount,
                ResolveSupportModifier(
                    facility,
                    recipe,
                    ability => ability.qualityModifier,
                    0f,
                    multiply: false));
            IProductionOutputHandler handler = outputHandlers.FirstOrDefault(
                candidate => candidate != null
                    && candidate.CanHandle(output.ItemId));
            if (handler != null)
            {
                if (!handler.TryProduce(outputContext, out string outputFailure))
                {
                    message = string.IsNullOrWhiteSpace(outputFailure)
                        ? $"{recipe.DisplayName} 결과물 생성 실패"
                        : outputFailure;
                    record.reservedWorkerId = string.Empty;
                    return false;
                }
            }
            else
            {
                items.SpawnOutput(
                    output.ItemId,
                    outputAmount,
                    facility.centerPos);
            }
        }

        cycleCompleted = true;
        record.completedWork = 0f;
        record.materialsConsumed = false;
        record.processFluidConsumed = false;
        record.batchStage = recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            ? ProductionBatchStage.Preparing
            : ProductionBatchStage.None;
        record.remainingProcessingHours = 0f;
        record.batchIntegrity = 100f;
        record.utilityOutageHours = 0f;
        record.temperatureOutageHours = 0f;
        record.occupiedSupportNodeId = string.Empty;
        record.blockedReason = string.Empty;
        record.reservedWorkerId = string.Empty;
        if (record.mode == ProductionOrderMode.RepeatCount)
        {
            record.remainingCycles = Mathf.Max(0, record.remainingCycles - 1);
        }

        bool finished = !ShouldRunAnotherCycle(record, recipe);
        if (finished && record.mode == ProductionOrderMode.RepeatCount)
        {
            bills.Remove(record);
        }
        else
        {
            RequestMissingInputs(record, recipe, facility);
        }

        Touch(recipe.WorkTypeId, requestWorker: !finished);
        workforceReplanService?.RequestOneHaulerToReplan(forceInterrupt: false);
        message = $"{recipe.DisplayName} 생산 완료";
        return true;
    }

    public void Tick()
    {
        if (clock == null || clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        float elapsedHours = clock.DeltaTime / SecondsPerGameHour;
        foreach (ProductionBillRecord record in bills.ToArray())
        {
            ProductionRecipeSO recipe = ResolveRecipe(record);
            if (recipe == null
                || recipe.ProcessKind != ProductionProcessKind.PassiveBatch
                || record.batchStage != ProductionBatchStage.Processing)
            {
                continue;
            }

            BuildableObject facility = ResolveFacility(record);
            if (facility == null)
            {
                record.blockedReason = "주 작업대를 찾을 수 없습니다.";
                continue;
            }

            if (!TryValidateProcessingUtilities(
                    record,
                    recipe,
                    facility,
                    out string utilityFailure))
            {
                record.blockedReason = utilityFailure;
                ApplyOutageDecay(
                    ref record.utilityOutageHours,
                    elapsedHours,
                    SafeUtilityOutageHours,
                    5f,
                    record);

                TryConvertRuinedBatch(record, recipe, facility);
                continue;
            }

            record.utilityOutageHours = 0f;
            BuildableObject temperatureTarget =
                ResolveOccupiedBatchSupport(record, facility) ?? facility;
            float temperatureSpeed = ResolveTemperatureSpeed(
                recipe,
                temperatureTarget,
                out bool dangerous);
            if (dangerous)
            {
                record.blockedReason = "위험 온도라 시간 공정이 정지했습니다.";
                ApplyOutageDecay(
                    ref record.temperatureOutageHours,
                    elapsedHours,
                    DangerousTemperatureGraceHours,
                    5f,
                    record);

                TryConvertRuinedBatch(record, recipe, facility);
                continue;
            }

            record.temperatureOutageHours = 0f;
            record.blockedReason = temperatureSpeed < 1f
                ? "주의 온도 범위: 처리 속도 50%"
                : string.Empty;
            if (temperatureSpeed < 1f)
            {
                record.batchIntegrity = Mathf.Max(
                    0f,
                    record.batchIntegrity - elapsedHours);
            }

            record.remainingProcessingHours = Mathf.Max(
                0f,
                record.remainingProcessingHours
                    - elapsedHours * temperatureSpeed);
            if (TryConvertRuinedBatch(record, recipe, facility)
                || record.remainingProcessingHours > 0.001f)
            {
                continue;
            }

            record.batchStage = ProductionBatchStage.Finishing;
            record.completedWork = 0f;
            record.reservedWorkerId = string.Empty;
            record.blockedReason = string.Empty;
            if (recipe.FinishingWork > 0f)
            {
                Touch(recipe.WorkTypeId, requestWorker: true);
            }
            else
            {
                ApplyWork(
                    null,
                    facility,
                    record.billId,
                    0f,
                    out _,
                    out _);
            }
        }
    }

    private bool TryValidateCycleStart(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (recipe == null || facility == null)
        {
            failureReason = "생산 주문 또는 작업대가 없습니다.";
            return false;
        }

        if (workshops != null
            && !workshops.HasRequiredSupports(
                facility,
                recipe.RequiredSupportTags,
                out failureReason))
        {
            return false;
        }

        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            && !TryResolveAvailableBatchSupport(
                record,
                recipe,
                facility,
                out _,
                out _,
                out failureReason))
        {
            return false;
        }

        return TryValidateUtilities(recipe, facility, out failureReason)
            && TryValidateLinkedSupportUtilities(
                recipe,
                facility,
                out failureReason);
    }

    private bool TryValidateProcessingUtilities(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (workshops == null)
        {
            return true;
        }

        ProductionSupportLinkSnapshot occupiedLink = workshops
            .GetLinks(facility)
            .FirstOrDefault(link =>
            {
                BuildingProductionSupportAbility candidate =
                    link.Support?.BuildingData.GetProductionSupportAbility();
                return candidate != null
                    && candidate.kind == ProductionSupportKind.BatchProcessor
                    && candidate.Provides(recipe.BatchSupportTag)
                    && string.Equals(
                        IndustrialInfrastructureIdentity.GetNodeId(link.Support),
                        record.occupiedSupportNodeId,
                        StringComparison.Ordinal);
            });
        if (occupiedLink == null)
        {
            failureReason = "사용 중이던 배치 처리 시설 연결이 끊겼습니다.";
            return false;
        }

        return TryValidateSupportUtilities(
            occupiedLink.Support,
            occupiedLink.Support.BuildingData.GetProductionSupportAbility(),
            out failureReason);
    }

    private bool TryValidateUtilities(
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (recipe.WastewaterPerCycle > 0f
            && (wastewater == null
                || !wastewater.CanAcceptWastewater(
                    facility,
                    recipe.WastewaterPerCycle,
                    out failureReason)))
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "배수 공간이 부족합니다."
                : failureReason;
            return false;
        }

        if (recipe.CleanWaterPerCycle <= 0f)
        {
            return true;
        }

        if (water != null
            && water.CanConsume(
                facility,
                WorldWaterQuality.Clean,
                recipe.CleanWaterPerCycle,
                out failureReason))
        {
            return true;
        }

        if (recipe.AllowsManualWaterFallback)
        {
            failureReason = string.Empty;
            return true;
        }

        failureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "깨끗한 상수가 부족합니다."
            : failureReason;
        return false;
    }

    private bool TryValidateLinkedSupportUtilities(
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (workshops == null)
        {
            return true;
        }

        HashSet<string> checkedSupports =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (string tag in recipe.RequiredSupportTags)
        {
            if (!workshops.TryResolveSupport(
                    facility,
                    tag,
                    null,
                    out BuildableObject support,
                    out BuildingProductionSupportAbility ability))
            {
                failureReason = $"연결 시설 부족: {tag}";
                return false;
            }

            string supportId =
                IndustrialInfrastructureIdentity.GetNodeId(support);
            if (!checkedSupports.Add(supportId))
            {
                continue;
            }

            if (!TryValidateSupportUtilities(
                    support,
                    ability,
                    out failureReason))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryValidateSupportUtilities(
        BuildableObject support,
        BuildingProductionSupportAbility ability,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (support == null || ability == null || support.IsGridDestroyed)
        {
            failureReason = "연결 시설을 사용할 수 없습니다.";
            return false;
        }

        if (ability.requiresPower
            && (power == null || !power.IsPowered(support)))
        {
            failureReason = $"{support.BuildingData?.objectName ?? "연결 시설"}: 전력 부족";
            return false;
        }

        if (ability.wastewaterPerCycle > 0f
            && (wastewater == null
                || !wastewater.CanAcceptWastewater(
                    support,
                    ability.wastewaterPerCycle,
                    out failureReason)))
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? $"{support.BuildingData?.objectName ?? "연결 시설"}: 배수 불가"
                : failureReason;
            return false;
        }

        if (ability.cleanWaterPerCycle <= 0f)
        {
            return true;
        }

        if (water != null
            && water.CanConsume(
                support,
                WorldWaterQuality.Clean,
                ability.cleanWaterPerCycle,
                out failureReason))
        {
            return true;
        }

        if (ability.allowsManualWaterFallback)
        {
            failureReason = string.Empty;
            return true;
        }

        failureReason = string.IsNullOrWhiteSpace(failureReason)
            ? $"{support.BuildingData?.objectName ?? "연결 시설"}: 상수 부족"
            : failureReason;
        return false;
    }

    private bool TryConsumeCycleUtilities(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (processFluids != null
            && !processFluids.TryConsumeCycle(
                facility,
                recipe.WorkTypeId,
                out failureReason))
        {
            return false;
        }

        if (processFluids != null
            && (recipe.CleanWaterPerCycle > 0f
                || recipe.WastewaterPerCycle > 0f)
            && !processFluids.TryConsumeCycle(
                facility,
                recipe.WorkTypeId,
                recipe.CleanWaterPerCycle,
                recipe.WastewaterPerCycle,
                recipe.AllowsManualWaterFallback,
                out failureReason))
        {
            return false;
        }

        if (workshops == null || processFluids == null)
        {
            return true;
        }

        HashSet<string> consumedSupports =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (string tag in recipe.RequiredSupportTags)
        {
            if (!workshops.TryResolveSupport(
                    facility,
                    tag,
                    null,
                    out BuildableObject support,
                    out BuildingProductionSupportAbility ability))
            {
                failureReason = $"연결 시설 부족: {tag}";
                return false;
            }

            string supportId =
                IndustrialInfrastructureIdentity.GetNodeId(support);
            if (!consumedSupports.Add(supportId)
                || ability.cleanWaterPerCycle <= 0f
                    && ability.wastewaterPerCycle <= 0f)
            {
                continue;
            }

            if (!processFluids.TryConsumeCycle(
                    support,
                    recipe.WorkTypeId,
                    ability.cleanWaterPerCycle,
                    ability.wastewaterPerCycle,
                    ability.allowsManualWaterFallback,
                    out failureReason))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryResolveAvailableBatchSupport(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out BuildableObject support,
        out BuildingProductionSupportAbility ability,
        out string failureReason)
    {
        support = null;
        ability = null;
        failureReason = string.Empty;
        if (workshops == null)
        {
            failureReason = $"배치 처리 시설 부족: {recipe.BatchSupportTag}";
            return false;
        }

        foreach (ProductionSupportLinkSnapshot link in workshops
                     .GetLinks(facility))
        {
            BuildingProductionSupportAbility candidateAbility =
                link.Support?.BuildingData.GetProductionSupportAbility();
            if (candidateAbility == null
                || candidateAbility.kind != ProductionSupportKind.BatchProcessor
                || !candidateAbility.Provides(recipe.BatchSupportTag))
            {
                continue;
            }

            string nodeId =
                IndustrialInfrastructureIdentity.GetNodeId(link.Support);
            int occupied = bills.Count(candidate =>
                candidate != record
                && candidate.batchStage == ProductionBatchStage.Processing
                && string.Equals(
                    candidate.occupiedSupportNodeId,
                    nodeId,
                    StringComparison.Ordinal));
            if (occupied >= candidateAbility.BatchCapacity)
            {
                continue;
            }

            support = link.Support;
            ability = candidateAbility;
            return true;
        }

        bool hasMatchingSupport = workshops.GetLinks(facility).Any(link =>
            link.Support?.BuildingData.GetProductionSupportAbility()
                is BuildingProductionSupportAbility candidate
            && candidate.kind == ProductionSupportKind.BatchProcessor
            && candidate.Provides(recipe.BatchSupportTag));
        failureReason = hasMatchingSupport
            ? "연결된 배치 처리 시설의 용량이 가득 찼습니다."
            : $"배치 처리 시설 부족: {recipe.BatchSupportTag}";
        return false;
    }

    private bool TryOccupyBatchSupport(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string failureReason)
    {
        if (!TryResolveAvailableBatchSupport(
                record,
                recipe,
                facility,
                out BuildableObject support,
                out _,
                out failureReason))
        {
            return false;
        }

        record.occupiedSupportNodeId =
            IndustrialInfrastructureIdentity.GetNodeId(support);
        return true;
    }

    private float ResolveTemperatureSpeed(
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out bool dangerous)
    {
        dangerous = false;
        if (environment == null
            || !environment.TryGetCell(
                facility.centerPos,
                out EnvironmentalCellSnapshot cell))
        {
            return 1f;
        }

        float temperature = cell.TemperatureC;
        if (temperature >= recipe.OptimalTemperatureMinimum
            && temperature <= recipe.OptimalTemperatureMaximum)
        {
            return 1f;
        }

        if (temperature >= recipe.WarningTemperatureMinimum
            && temperature <= recipe.WarningTemperatureMaximum)
        {
            return 0.5f;
        }

        dangerous = true;
        return 0f;
    }

    private BuildableObject ResolveOccupiedBatchSupport(
        ProductionBillRecord record,
        BuildableObject facility)
    {
        if (workshops == null
            || record == null
            || facility == null
            || string.IsNullOrWhiteSpace(record.occupiedSupportNodeId))
        {
            return null;
        }

        return workshops.GetLinks(facility)
            .Select(link => link?.Support)
            .FirstOrDefault(support => support != null
                && string.Equals(
                    IndustrialInfrastructureIdentity.GetNodeId(support),
                    record.occupiedSupportNodeId,
                    StringComparison.Ordinal));
    }

    private bool TryConvertRuinedBatch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility)
    {
        if (record.batchIntegrity > 0f)
        {
            return false;
        }

        items.SpawnOutput(
            recipe.SpoilageItemId,
            Mathf.Max(1, recipe.Inputs.Sum(input => input?.Amount ?? 0)),
            facility.centerPos);
        record.completedWork = 0f;
        record.materialsConsumed = false;
        record.processFluidConsumed = false;
        record.batchStage = ProductionBatchStage.Preparing;
        record.remainingProcessingHours = 0f;
        record.batchIntegrity = 100f;
        record.utilityOutageHours = 0f;
        record.temperatureOutageHours = 0f;
        record.occupiedSupportNodeId = string.Empty;
        record.reservedWorkerId = string.Empty;
        record.blockedReason = "배치가 부패물로 전환되었습니다.";
        if (record.mode == ProductionOrderMode.RepeatCount)
        {
            record.remainingCycles = Mathf.Max(0, record.remainingCycles - 1);
        }

        if (!ShouldRunAnotherCycle(record, recipe)
            && record.mode == ProductionOrderMode.RepeatCount)
        {
            bills.Remove(record);
        }
        else
        {
            RequestMissingInputs(record, recipe, facility);
        }

        Touch(recipe.WorkTypeId, requestWorker: false);
        return true;
    }

    private static float ResolveCurrentRequiredWork(
        ProductionBillRecord record,
        ProductionRecipeSO recipe)
    {
        if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch)
        {
            return recipe.RequiredWork;
        }

        return record.batchStage == ProductionBatchStage.Finishing
            ? recipe.FinishingWork
            : recipe.PreparationWork;
    }

    private static void ApplyOutageDecay(
        ref float accumulatedHours,
        float elapsedHours,
        float graceHours,
        float integrityLossPerHour,
        ProductionBillRecord record)
    {
        float previous = Mathf.Max(0f, accumulatedHours);
        accumulatedHours = previous + Mathf.Max(0f, elapsedHours);
        float damagingHours = Mathf.Max(0f, accumulatedHours - graceHours)
            - Mathf.Max(0f, previous - graceHours);
        if (damagingHours <= 0f)
        {
            return;
        }

        record.batchIntegrity = Mathf.Max(
            0f,
            record.batchIntegrity - damagingHours * integrityLossPerHour);
    }

    private BuildableObject ResolveFacility(ProductionBillRecord record)
    {
        return buildingWorld?.Buildings?.FirstOrDefault(building =>
            MatchesFacility(record, building));
    }

    private int ResolveOutputAmount(int baseAmount, float multiplier)
    {
        float scaled = Mathf.Max(0f, baseAmount) * Mathf.Max(0f, multiplier);
        int whole = Mathf.FloorToInt(scaled);
        float remainder = scaled - whole;
        return Mathf.Max(
            1,
            whole + (remainder > 0f && random.Chance(remainder) ? 1 : 0));
    }

    private float ResolveSupportModifier(
        BuildableObject facility,
        ProductionRecipeSO recipe,
        Func<BuildingProductionSupportAbility, float> selector,
        float defaultValue,
        bool multiply)
    {
        if (workshops == null
            || facility == null
            || recipe == null
            || selector == null)
        {
            return defaultValue;
        }

        float result = defaultValue;
        HashSet<string> appliedSupports =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (string tag in recipe.RequiredSupportTags
                     .Where(tag => !string.IsNullOrWhiteSpace(tag))
                     .Distinct(StringComparer.Ordinal))
        {
            if (!workshops.TryResolveSupport(
                    facility,
                    tag,
                    null,
                    out BuildableObject support,
                    out BuildingProductionSupportAbility ability))
            {
                continue;
            }

            string nodeId =
                IndustrialInfrastructureIdentity.GetNodeId(support);
            if (!appliedSupports.Add(nodeId))
            {
                continue;
            }

            float value = selector(ability);
            result = multiply
                ? result * Mathf.Max(0.01f, value)
                : result + value;
        }
        return result;
    }

    public DungeonProductionBillSaveData Capture()
    {
        return new DungeonProductionBillSaveData
        {
            nextBillSequence = nextBillSequence,
            bills = bills.Select(ToSaveData).ToList()
        };
    }

    public void Restore(DungeonProductionBillSaveData snapshot)
    {
        bills.Clear();
        nextBillSequence = Mathf.Max(1, snapshot?.nextBillSequence ?? 1);
        foreach (ProductionBillSaveData saved in snapshot?.bills
                 ?? new List<ProductionBillSaveData>())
        {
            if (saved == null
                || !catalog.TryGetRecipe(saved.recipeId, out _)
                || string.IsNullOrWhiteSpace(saved.billId))
            {
                continue;
            }

            ProductionBillRecord record = new ProductionBillRecord
            {
                billId = saved.billId,
                recipeId = saved.recipeId,
                buildingId = saved.buildingId,
                position = new Vector2Int(saved.gridX, saved.gridY),
                mode = saved.mode,
                remainingCycles = saved.remainingCycles,
                targetStock = Mathf.Max(0, saved.targetStock),
                minimumReserve = Mathf.Max(0, saved.minimumReserve),
                suspended = saved.suspended,
                materialsConsumed = saved.materialsConsumed,
                processFluidConsumed = saved.processFluidConsumed,
                completedWork = Mathf.Max(0f, saved.completedWork),
                batchStage = saved.batchStage,
                remainingProcessingHours =
                    Mathf.Max(0f, saved.remainingProcessingHours),
                batchIntegrity = saved.batchIntegrity <= 0f
                    && saved.batchStage == ProductionBatchStage.None
                        ? 100f
                        : Mathf.Clamp(saved.batchIntegrity, 0f, 100f),
                utilityOutageHours = Mathf.Max(0f, saved.utilityOutageHours),
                temperatureOutageHours =
                    Mathf.Max(0f, saved.temperatureOutageHours),
                occupiedSupportNodeId =
                    saved.occupiedSupportNodeId?.Trim() ?? string.Empty,
                blockedReason = saved.blockedReason ?? string.Empty,
                reservedWorkerId = string.Empty,
                materialDestinationId = string.IsNullOrWhiteSpace(saved.materialDestinationId)
                    ? DestinationPrefix + saved.billId
                    : saved.materialDestinationId
            };
            ProductionRecipeSO restoredRecipe = ResolveRecipe(record);
            if (restoredRecipe != null
                && restoredRecipe.ProcessKind
                    == ProductionProcessKind.PassiveBatch
                && record.batchStage == ProductionBatchStage.None)
            {
                // V1 orders had no batch stage and remain unconsumed work orders.
                record.batchStage = ProductionBatchStage.Preparing;
                record.materialsConsumed = false;
                record.processFluidConsumed = false;
                record.completedWork = 0f;
            }
            record.allowedMaterialIds.UnionWith(
                saved.allowedMaterialIds ?? new List<string>());
            record.allowedWorkerIds.UnionWith(
                saved.allowedWorkerIds ?? new List<string>());
            bills.Add(record);
        }

        Touch(default, requestWorker: false);
    }

    private ProductionBillRecord FindRunnableBill(
        BuildableObject facility,
        WorkTypeId workTypeId,
        bool requireDeliveredInputs,
        out string reason)
    {
        reason = "등록된 생산 주문이 없습니다.";
        if (facility == null || !workTypeId.IsValid)
        {
            return null;
        }

        foreach (ProductionBillRecord record in bills)
        {
            if (!MatchesFacility(record, facility)
                || record.suspended
                || ResolveRecipe(record) is not ProductionRecipeSO recipe
                || recipe.WorkTypeId != workTypeId)
            {
                continue;
            }

            if (!IsResearchUnlocked(recipe, out reason))
            {
                continue;
            }

            if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                && record.batchStage == ProductionBatchStage.Processing)
            {
                reason = string.IsNullOrWhiteSpace(record.blockedReason)
                    ? "시간 공정이 진행 중입니다."
                    : record.blockedReason;
                continue;
            }

            if (!ShouldRunAnotherCycle(record, recipe))
            {
                reason = "목표 재고를 충족했습니다.";
                continue;
            }

            if (record.materialsConsumed)
            {
                return record;
            }

            if (!HasDeliveredInputs(record, recipe, facility, out reason))
            {
                RequestMissingInputs(record, recipe, facility);
                if (requireDeliveredInputs)
                {
                    continue;
                }
            }

            return record;
        }

        return null;
    }

    private bool HasDeliveredInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string reason)
    {
        reason = string.Empty;
        foreach (KeyValuePair<string, int> requirement in ToCycleInputMap(
                     recipe,
                     facility))
        {
            if (items.CountDelivered(
                    requirement.Key,
                    record.materialDestinationId)
                < requirement.Value)
            {
                reason = $"재료 운반 대기: {requirement.Key}";
                return false;
            }
        }

        return true;
    }

    private void RequestMissingInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility = null)
    {
        if (record == null || recipe == null || record.materialsConsumed)
        {
            return;
        }

        bool requestedAny = false;
        facility ??= ResolveFacility(record);
        foreach (KeyValuePair<string, int> requirement in ToCycleInputMap(
                     recipe,
                     facility))
        {
            int pending = items.CountPending(
                requirement.Key,
                record.materialDestinationId);
            int missing = Mathf.Max(0, requirement.Value - pending);
            if (missing <= 0)
            {
                continue;
            }

            items.RequestDelivery(
                requirement.Key,
                missing,
                record.position,
                record.materialDestinationId,
                out int requested,
                out _);
            requestedAny |= requested > 0;
        }

        if (!requestedAny)
        {
            return;
        }

        items.PrioritizeDestination(record.materialDestinationId);
        workforceReplanService?.RequestOneHaulerToReplan(forceInterrupt: false);
    }

    private bool ShouldRunAnotherCycle(
        ProductionBillRecord record,
        ProductionRecipeSO recipe)
    {
        if (record == null || recipe == null)
        {
            return false;
        }

        if (record.mode == ProductionOrderMode.RepeatCount)
        {
            return record.remainingCycles > 0;
        }

        string primaryOutput = recipe.Outputs
            .FirstOrDefault(output => output != null)?.ItemId;
        if (string.IsNullOrWhiteSpace(primaryOutput))
        {
            return false;
        }

        int stock = items.CountAvailableStock(
            primaryOutput,
            record.materialDestinationId);
        return stock < Mathf.Max(record.minimumReserve, record.targetStock);
    }

    private bool IsResearchUnlocked(
        ProductionRecipeSO recipe,
        out string reason)
    {
        reason = string.Empty;
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.RequiredResearchId))
        {
            return true;
        }

        if (researchProvider == null
            || !researchProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            reason = $"연구 필요: {recipe.RequiredResearchId}";
            return false;
        }

        bool unlocked = runtime.State.Projects.IsCompleted(
            new ResearchProjectId(recipe.RequiredResearchId));
        if (!unlocked)
        {
            reason = $"연구 필요: {recipe.RequiredResearchId}";
        }
        return unlocked;
    }

    private Dictionary<string, int> ToCycleInputMap(
        ProductionRecipeSO recipe,
        BuildableObject facility)
    {
        Dictionary<string, int> costs =
            (recipe?.Inputs ?? Array.Empty<ItemAmountDefinition>())
            .Where(input => input != null && !string.IsNullOrWhiteSpace(input.ItemId))
            .GroupBy(input => input.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(input => input.Amount),
                StringComparer.Ordinal);
        if (workshops == null || facility == null || recipe == null)
        {
            return costs;
        }

        HashSet<string> checkedSupports =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (string tag in recipe.RequiredSupportTags
                     .Where(tag => !string.IsNullOrWhiteSpace(tag))
                     .Distinct(StringComparer.Ordinal))
        {
            if (!workshops.TryResolveSupport(
                    facility,
                    tag,
                    null,
                    out BuildableObject support,
                    out BuildingProductionSupportAbility ability)
                || ability == null
                || !ability.requiresFuel)
            {
                continue;
            }

            string nodeId =
                IndustrialInfrastructureIdentity.GetNodeId(support);
            string fuelItemId = ability.fuelItemId?.Trim();
            if (!checkedSupports.Add(nodeId)
                || string.IsNullOrWhiteSpace(fuelItemId))
            {
                continue;
            }

            costs[fuelItemId] =
                (costs.TryGetValue(fuelItemId, out int current)
                    ? current
                    : 0)
                + Mathf.Max(1, ability.fuelPerCycle);
        }
        return costs;
    }

    private ProductionBillSnapshot ToSnapshot(
        ProductionBillRecord record,
        BuildableObject facility)
    {
        ProductionRecipeSO recipe = ResolveRecipe(record);
        ProductionBillStatus status;
        string blockedReason = record.blockedReason ?? string.Empty;
        if (record.suspended)
        {
            status = ProductionBillStatus.Suspended;
        }
        else if (record.batchStage == ProductionBatchStage.Processing)
        {
            status = record.utilityOutageHours > 0f
                || record.temperatureOutageHours > 0f
                    ? ProductionBillStatus.WaitingForUtilities
                    : ProductionBillStatus.Processing;
        }
        else if (record.batchStage == ProductionBatchStage.Finishing)
        {
            status = ProductionBillStatus.WaitingForFinishing;
        }
        else if (record.materialsConsumed)
        {
            status = record.completedWork > 0f
                ? ProductionBillStatus.InProgress
                : ProductionBillStatus.Ready;
        }
        else if (recipe != null
            && HasDeliveredInputs(record, recipe, facility, out blockedReason))
        {
            status = ProductionBillStatus.Ready;
        }
        else
        {
            status = ProductionBillStatus.WaitingForMaterials;
        }

        float requiredWork = recipe == null
            ? 0f
            : ResolveCurrentRequiredWork(record, recipe);
        return new ProductionBillSnapshot
        {
            BillId = record.billId,
            RecipeId = record.recipeId,
            RecipeName = recipe?.DisplayName ?? record.recipeId,
            BuildingId = record.buildingId,
            Position = record.position,
            WorkTypeId = recipe?.WorkTypeId ?? default,
            Mode = record.mode,
            Status = status,
            RemainingCycles = record.remainingCycles,
            TargetStock = record.targetStock,
            MinimumReserve = record.minimumReserve,
            RequiredWork = requiredWork,
            CompletedWork = record.completedWork,
            MaterialsConsumed = record.materialsConsumed,
            ProcessFluidConsumed = record.processFluidConsumed,
            BatchStage = record.batchStage,
            RemainingProcessingHours = record.remainingProcessingHours,
            BatchIntegrity = record.batchIntegrity,
            UtilityOutageHours = record.utilityOutageHours,
            TemperatureOutageHours = record.temperatureOutageHours,
            OccupiedSupportNodeId = record.occupiedSupportNodeId,
            ReservedWorkerId = record.reservedWorkerId,
            MaterialDestinationId = record.materialDestinationId,
            BlockedReason = blockedReason,
            Inputs = recipe?.Inputs ?? Array.Empty<ItemAmountDefinition>(),
            Outputs = recipe?.Outputs ?? Array.Empty<ProductionOutputDefinition>(),
            ProcessingProgressRatio = recipe == null
                || recipe.ProcessingGameHours <= 0f
                    ? 0f
                    : Mathf.Clamp01(
                        1f - record.remainingProcessingHours
                            / recipe.ProcessingGameHours)
        };
    }

    private static ProductionBillSaveData ToSaveData(ProductionBillRecord record)
    {
        return new ProductionBillSaveData
        {
            billId = record.billId,
            recipeId = record.recipeId,
            buildingId = record.buildingId,
            gridX = record.position.x,
            gridY = record.position.y,
            mode = record.mode,
            remainingCycles = record.remainingCycles,
            targetStock = record.targetStock,
            minimumReserve = record.minimumReserve,
            suspended = record.suspended,
            materialsConsumed = record.materialsConsumed,
            processFluidConsumed = record.processFluidConsumed,
            completedWork = record.completedWork,
            batchStage = record.batchStage,
            remainingProcessingHours = record.remainingProcessingHours,
            batchIntegrity = record.batchIntegrity,
            utilityOutageHours = record.utilityOutageHours,
            temperatureOutageHours = record.temperatureOutageHours,
            occupiedSupportNodeId = record.occupiedSupportNodeId,
            blockedReason = record.blockedReason,
            reservedWorkerId = record.reservedWorkerId,
            materialDestinationId = record.materialDestinationId,
            allowedMaterialIds = record.allowedMaterialIds.ToList(),
            allowedWorkerIds = record.allowedWorkerIds.ToList()
        };
    }

    private ProductionRecipeSO ResolveRecipe(ProductionBillRecord record)
    {
        return record != null
            && catalog.TryGetRecipe(record.recipeId, out ProductionRecipeSO recipe)
                ? recipe
                : null;
    }

    private ProductionBillRecord Find(string billId)
    {
        return string.IsNullOrWhiteSpace(billId)
            ? null
            : bills.FirstOrDefault(record =>
                string.Equals(record.billId, billId, StringComparison.Ordinal));
    }

    private static bool MatchesFacility(
        ProductionBillRecord record,
        BuildableObject facility)
    {
        return record != null
            && facility != null
            && !facility.IsGridDestroyed
            && record.buildingId == facility.id
            && record.position == facility.centerPos;
    }

    private static bool MatchesRecipeWorkstation(
        BuildableObject facility,
        ProductionRecipeSO recipe)
    {
        return facility.MatchesProductionWorkstation(recipe);
    }

    private void Touch(WorkTypeId workTypeId, bool requestWorker)
    {
        unchecked
        {
            Version++;
        }
        if (requestWorker && workTypeId.IsValid)
        {
            workforceReplanService?.RequestOneWorkerToReplanFor(workTypeId);
        }
    }
}
