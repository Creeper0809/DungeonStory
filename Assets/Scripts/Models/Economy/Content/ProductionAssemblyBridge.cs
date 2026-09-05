using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Declares which execution authority owns a workstation's lanes. Zero is
/// invalid for a production-capable facility.
/// </summary>
public enum ProductionWorkstationLanePolicy
{
    Unspecified = 0,
    ManualWithDetachedBatchProcessors = 1,
    ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors = 2
}

/// <summary>
/// Scene-detached authored workstation concurrency. This is immutable
/// definition provenance, not runtime occupancy: live and save-only capacity
/// projection must therefore resolve the same lane contract.
/// </summary>
public sealed class ProductionFacilityWorkstationLaneCapacityProfile :
    IEquatable<ProductionFacilityWorkstationLaneCapacityProfile>
{
    public const string Schema =
        "production-facility-workstation-lane-capacity-profile@1";

    private ProductionFacilityWorkstationLaneCapacityProfile(
        ProductionWorkstationLanePolicy policy,
        int manualWorkLaneCount,
        int automaticWorkLaneCount,
        bool allowUnspecified)
    {
        bool specified = policy != ProductionWorkstationLanePolicy.Unspecified;
        if ((!allowUnspecified || specified)
            && (manualWorkLaneCount <= 0
                || automaticWorkLaneCount < 0
                || policy == ProductionWorkstationLanePolicy.Unspecified
                || policy == ProductionWorkstationLanePolicy
                    .ManualWithDetachedBatchProcessors
                    && automaticWorkLaneCount != 0
                || policy == ProductionWorkstationLanePolicy
                    .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors
                    && automaticWorkLaneCount <= 0))
        {
            throw new ArgumentException(
                "Production workstation lane profile is invalid.");
        }
        if (allowUnspecified && !specified
            && (manualWorkLaneCount != 0 || automaticWorkLaneCount != 0))
        {
            throw new ArgumentException(
                "An unspecified production lane profile must be empty.");
        }

        Policy = policy;
        ManualWorkLaneCount = manualWorkLaneCount;
        AutomaticWorkLaneCount = automaticWorkLaneCount;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append((int)Policy);
        digest.Append(ManualWorkLaneCount);
        digest.Append(AutomaticWorkLaneCount);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionFacilityWorkstationLaneCapacityProfile(
        ProductionWorkstationLanePolicy policy,
        int manualWorkLaneCount,
        int automaticWorkLaneCount)
        : this(
            policy,
            manualWorkLaneCount,
            automaticWorkLaneCount,
            allowUnspecified: false)
    {
    }

    public static ProductionFacilityWorkstationLaneCapacityProfile Empty { get; }
        = new(
            ProductionWorkstationLanePolicy.Unspecified,
            0,
            0,
            allowUnspecified: true);

    public static ProductionFacilityWorkstationLaneCapacityProfile
        SingleManualWithDetachedBatchProcessors { get; } = new(
            ProductionWorkstationLanePolicy
                .ManualWithDetachedBatchProcessors,
            1,
            0);

    public ProductionWorkstationLanePolicy Policy { get; }
    public int ManualWorkLaneCount { get; }
    public int AutomaticWorkLaneCount { get; }
    public bool IsSpecified =>
        Policy != ProductionWorkstationLanePolicy.Unspecified;
    public string SourceDigest { get; }

    public bool Equals(
        ProductionFacilityWorkstationLaneCapacityProfile other) =>
        other != null
        && string.Equals(SourceDigest, other.SourceDigest,
            StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is ProductionFacilityWorkstationLaneCapacityProfile other
        && Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(SourceDigest);
}

public enum ProductionWorkstationExecutionAuthority
{
    ManualActor = 1,
    AutomaticExecutor = 2
}

/// <summary>
/// Pure admission matrix shared by preview, begin and execute. Current
/// automation mode and immutable lane authoring must agree; callers must not
/// switch execution authority as a fallback.
/// </summary>
public static class ProductionWorkstationExecutionModeRules
{
    public const string MissingLaneProfile =
        "production-lane-profile-missing";
    public const string ManualLaneUnavailable =
        "production-manual-lane-unavailable";
    public const string ManualDisabledByAutomaticMode =
        "production-manual-disabled-by-automatic-mode";
    public const string AutomaticLaneUnavailable =
        "production-automatic-lane-unavailable";
    public const string AutomaticModeRequired =
        "production-automatic-mode-required";
    public const string InvalidExecutionAuthority =
        "production-execution-authority-invalid";

    public static bool RequiresLaneAuthorization(
        bool isProductionWorkstation,
        AutomationMode mode,
        ProductionWorkstationExecutionAuthority authority) =>
        isProductionWorkstation
        || mode == AutomationMode.Automatic
        || authority == ProductionWorkstationExecutionAuthority.AutomaticExecutor;

    public static bool TryAuthorize(
        ProductionFacilityWorkstationLaneCapacityProfile profile,
        AutomationMode mode,
        ProductionWorkstationExecutionAuthority authority,
        out string failureReason)
    {
        failureReason = string.Empty;
        switch (authority)
        {
            case ProductionWorkstationExecutionAuthority.ManualActor:
                if (mode == AutomationMode.Automatic)
                {
                    failureReason = ManualDisabledByAutomaticMode;
                    return false;
                }
                if (profile == null || !profile.IsSpecified)
                {
                    failureReason = MissingLaneProfile;
                    return false;
                }
                if (profile.ManualWorkLaneCount <= 0)
                {
                    failureReason = ManualLaneUnavailable;
                    return false;
                }
                return true;

            case ProductionWorkstationExecutionAuthority.AutomaticExecutor:
                if (profile == null || !profile.IsSpecified)
                {
                    failureReason = MissingLaneProfile;
                    return false;
                }
                if (profile.Policy != ProductionWorkstationLanePolicy
                        .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors
                    || profile.AutomaticWorkLaneCount <= 0)
                {
                    failureReason = AutomaticLaneUnavailable;
                    return false;
                }
                if (mode != AutomationMode.Automatic)
                {
                    failureReason = AutomaticModeRequired;
                    return false;
                }
                return true;

            default:
                failureReason = InvalidExecutionAuthority;
                return false;
        }
    }

    public static bool BlocksManualProductionFallback(DomainFailure failure)
    {
        if (!failure.IsFailure)
        {
            return false;
        }

        ReadOnlySpan<string> parameters = failure.Parameters;
        for (int index = 0; index < parameters.Length; index++)
        {
            string parameter = parameters[index];
            if (string.Equals(
                    parameter,
                    MissingLaneProfile,
                    StringComparison.Ordinal)
                || string.Equals(
                    parameter,
                    ManualLaneUnavailable,
                    StringComparison.Ordinal)
                || string.Equals(
                    parameter,
                    ManualDisabledByAutomaticMode,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Opaque scene reference captured at the Assembly-CSharp composition boundary.
/// Economy code owns the stable identity and authored values; only the adapter
/// may inspect <see cref="RuntimeObject"/>.
/// </summary>
public sealed class ProductionFacilityHandle
{
    public ProductionFacilityHandle(
        object runtimeObject,
        BuildingInstanceId instanceId,
        Vector2Int position,
        bool isDestroyed,
        string stockSensorInstallationItemId,
        bool allowsOverflowDump,
        Vector2Int overflowOffset,
        string definitionId,
        string workstationTag,
        int outputBufferCycleCapacity,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile = null,
        ProductionFacilityWorkstationLaneCapacityProfile
            workstationLaneProfile = null)
    {
        RuntimeObject = runtimeObject
            ?? throw new ArgumentNullException(nameof(runtimeObject));
        InstanceId = instanceId;
        Position = position;
        IsDestroyed = isDestroyed;
        StockSensorInstallationItemId =
            stockSensorInstallationItemId?.Trim() ?? string.Empty;
        AllowsOverflowDump = allowsOverflowDump;
        OverflowOffset = overflowOffset;
        DefinitionId = RequireCanonicalOptional(
            definitionId,
            nameof(definitionId));
        WorkstationTag = RequireCanonicalOptional(
            workstationTag,
            nameof(workstationTag));
        if (outputBufferCycleCapacity is < 2 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputBufferCycleCapacity));
        }
        OutputBufferCycleCapacity = outputBufferCycleCapacity;
        ProcessFluidProfile = processFluidProfile
            ?? ProductionFacilityProcessFluidCapacityProfile.Empty;
        WorkstationLaneProfile = workstationLaneProfile
            ?? ProductionFacilityWorkstationLaneCapacityProfile.Empty;
    }

    public object RuntimeObject { get; }
    public BuildingInstanceId InstanceId { get; }
    public Vector2Int Position { get; }
    public bool IsDestroyed { get; }
    public string StockSensorInstallationItemId { get; }
    public bool AllowsOverflowDump { get; }
    public Vector2Int OverflowOffset { get; }
    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public int OutputBufferCycleCapacity { get; }
    public ProductionFacilityProcessFluidCapacityProfile ProcessFluidProfile { get; }
    public ProductionFacilityWorkstationLaneCapacityProfile
        WorkstationLaneProfile { get; }

    private static string RequireCanonical(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Production facility semantic identity must be canonical.",
                parameter);
        }
        return value;
    }

    private static string RequireCanonicalOptional(string value, string parameter)
    {
        string token = value ?? string.Empty;
        if (token.Length > 0
            && (string.IsNullOrWhiteSpace(token)
                || !string.Equals(token, token.Trim(), StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Production facility semantic identity must be canonical.",
                parameter);
        }
        return token;
    }
}

/// <summary>
/// Canonical, scene-detached process-fluid authoring used by capacity
/// projection. The profile binds facility-authored clean-water and wastewater
/// mass to the exact work types that consume it, so live and save-only
/// portfolio projections cannot disagree about a ruined WIP branch.
/// </summary>
public sealed class ProductionFacilityProcessFluidCapacityProfile :
    IEquatable<ProductionFacilityProcessFluidCapacityProfile>
{
    public const string Schema =
        "production-facility-process-fluid-capacity-profile@1";

    private readonly IReadOnlyList<string> workTypeIds;

    public ProductionFacilityProcessFluidCapacityProfile(
        IEnumerable<string> workTypeIds,
        float cleanWaterAuthoredUnitsPerCycle,
        float wastewaterAuthoredUnitsPerCycle)
    {
        string[] ordered = (workTypeIds ?? Array.Empty<string>()).ToArray();
        for (int index = 0; index < ordered.Length; index++)
        {
            string value = ordered[index];
            if (string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Process-fluid work type IDs must be canonical.",
                    nameof(workTypeIds));
            }
        }
        ordered = ordered
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new ArgumentException(
                "Process-fluid work type IDs must be unique.",
                nameof(workTypeIds));
        }
        if (float.IsNaN(cleanWaterAuthoredUnitsPerCycle)
            || float.IsInfinity(cleanWaterAuthoredUnitsPerCycle)
            || cleanWaterAuthoredUnitsPerCycle < 0f
            || float.IsNaN(wastewaterAuthoredUnitsPerCycle)
            || float.IsInfinity(wastewaterAuthoredUnitsPerCycle)
            || wastewaterAuthoredUnitsPerCycle < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cleanWaterAuthoredUnitsPerCycle));
        }
        if (ordered.Length == 0
            && (cleanWaterAuthoredUnitsPerCycle != 0f
                || wastewaterAuthoredUnitsPerCycle != 0f))
        {
            throw new ArgumentException(
                "A process-fluid mass requires at least one supported work type.",
                nameof(workTypeIds));
        }

        this.workTypeIds = Array.AsReadOnly(ordered);
        CleanWaterAuthoredUnitsPerCycle = cleanWaterAuthoredUnitsPerCycle;
        WastewaterAuthoredUnitsPerCycle = wastewaterAuthoredUnitsPerCycle;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(ordered.Length);
        foreach (string workTypeId in ordered)
            digest.Append(workTypeId);
        digest.AppendFloat(cleanWaterAuthoredUnitsPerCycle);
        digest.AppendFloat(wastewaterAuthoredUnitsPerCycle);
        SourceDigest = digest.ComputeSha256();
    }

    public static ProductionFacilityProcessFluidCapacityProfile Empty { get; } =
        new(Array.Empty<string>(), 0f, 0f);

    public IReadOnlyList<string> WorkTypeIds => workTypeIds;
    public float CleanWaterAuthoredUnitsPerCycle { get; }
    public float WastewaterAuthoredUnitsPerCycle { get; }
    public string SourceDigest { get; }

    public bool Supports(WorkTypeId workTypeId) => workTypeId.IsValid
        && workTypeIds.Contains(workTypeId.Value, StringComparer.Ordinal);

    public bool Equals(ProductionFacilityProcessFluidCapacityProfile other) =>
        other != null
        && string.Equals(SourceDigest, other.SourceDigest, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is ProductionFacilityProcessFluidCapacityProfile other
        && Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(SourceDigest);
}

/// <summary>
/// Immutable authored identity consumed by output-capacity projection. It is
/// deliberately detached from scene objects so live and current-format save
/// candidates run through the same calculation.
/// </summary>
public readonly struct ProductionFacilityCapacitySubject : IEquatable<ProductionFacilityCapacitySubject>
{
    public ProductionFacilityCapacitySubject(
        BuildingInstanceId facilityId,
        Vector2Int position,
        string definitionId,
        string workstationTag,
        int outputBufferCycleCapacity,
        ProductionFacilityWorkstationLaneCapacityProfile workstationLaneProfile,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile = null)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("Capacity subject requires a valid facility ID.", nameof(facilityId));
        if (string.IsNullOrWhiteSpace(definitionId)
            || !string.Equals(definitionId, definitionId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Capacity definition ID must be canonical.", nameof(definitionId));
        if (string.IsNullOrWhiteSpace(workstationTag)
            || !string.Equals(workstationTag, workstationTag.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Capacity workstation tag must be canonical.", nameof(workstationTag));
        if (outputBufferCycleCapacity is < 2 or > 4)
            throw new ArgumentOutOfRangeException(nameof(outputBufferCycleCapacity));
        if (workstationLaneProfile == null
            || !workstationLaneProfile.IsSpecified)
        {
            throw new ArgumentException(
                "Capacity subject requires explicit workstation lane authority.",
                nameof(workstationLaneProfile));
        }

        FacilityId = facilityId;
        Position = position;
        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        OutputBufferCycleCapacity = outputBufferCycleCapacity;
        WorkstationLaneProfile = workstationLaneProfile;
        ProcessFluidProfile = processFluidProfile
            ?? ProductionFacilityProcessFluidCapacityProfile.Empty;
    }

    public BuildingInstanceId FacilityId { get; }
    public Vector2Int Position { get; }
    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public int OutputBufferCycleCapacity { get; }
    public ProductionFacilityWorkstationLaneCapacityProfile
        WorkstationLaneProfile { get; }
    public ProductionFacilityProcessFluidCapacityProfile ProcessFluidProfile { get; }

    public static ProductionFacilityCapacitySubject FromLive(
        ProductionFacilityHandle facility)
    {
        if (facility == null || facility.IsDestroyed)
            throw new ArgumentException("A live capacity facility is required.", nameof(facility));
        return new ProductionFacilityCapacitySubject(
            facility.InstanceId,
            facility.Position,
            facility.DefinitionId,
            facility.WorkstationTag,
            facility.OutputBufferCycleCapacity,
            facility.WorkstationLaneProfile,
            facility.ProcessFluidProfile);
    }

    public bool Equals(ProductionFacilityCapacitySubject other) =>
        FacilityId.Equals(other.FacilityId)
        && Position == other.Position
        && string.Equals(DefinitionId, other.DefinitionId, StringComparison.Ordinal)
        && string.Equals(WorkstationTag, other.WorkstationTag, StringComparison.Ordinal)
        && OutputBufferCycleCapacity == other.OutputBufferCycleCapacity
        && WorkstationLaneProfile.Equals(other.WorkstationLaneProfile)
        && ProcessFluidProfile.Equals(other.ProcessFluidProfile);

    public override bool Equals(object obj) =>
        obj is ProductionFacilityCapacitySubject other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        FacilityId,
        Position,
        DefinitionId,
        WorkstationTag,
        OutputBufferCycleCapacity,
        WorkstationLaneProfile.SourceDigest,
        ProcessFluidProfile.SourceDigest);
}

public enum ProductionWorkerAuthorityKind
{
    Actor = 1,
    AutomaticExecutor = 2,
    PassiveProcessor = 3
}

public sealed class ProductionWorkerHandle
{
    public ProductionWorkerHandle(object runtimeObject, string persistentId)
        : this(
            runtimeObject,
            persistentId,
            ProductionWorkerAuthorityKind.Actor)
    {
    }

    private ProductionWorkerHandle(
        object runtimeObject,
        string persistentId,
        ProductionWorkerAuthorityKind authorityKind)
    {
        RuntimeObject = runtimeObject;
        PersistentId = persistentId?.Trim() ?? string.Empty;
        AuthorityKind = authorityKind;
    }

    public static ProductionWorkerHandle AutomaticExecutor { get; } = new(
        null,
        string.Empty,
        ProductionWorkerAuthorityKind.AutomaticExecutor);

    public static ProductionWorkerHandle PassiveProcessor { get; } = new(
        null,
        string.Empty,
        ProductionWorkerAuthorityKind.PassiveProcessor);

    public object RuntimeObject { get; }
    public string PersistentId { get; }
    public ProductionWorkerAuthorityKind AuthorityKind { get; }
}

public enum ProductionSupportModifierKind
{
    WorkSpeed = 0,
    Output = 1,
    Quality = 2
}

/// <summary>
/// Anti-corruption port for scene actors and legacy production implementations.
/// It is implemented only in the default composition assembly; the production
/// aggregate never depends on BuildableObject, CharacterActor, or their services.
/// </summary>
public interface IProductionFacilityHandleQuery
{
    ProductionFacilityHandle CaptureFacility(object runtimeObject);
}

/// <summary>
/// Economy-owned restore boundary that consumes immutable production handles
/// projected from the detached facility-world candidate. The lower Production
/// assembly remains independent of Economy handle types.
/// </summary>
public interface IProductionBillDetachedFacilityPersistence :
    IProductionBillPersistence
{
    void Restore(
        ProductionBillRestoreCandidate candidate,
        IReadOnlyList<ProductionFacilityHandle> detachedFacilities);
}

public interface IProductionAssemblyBridge : IProductionFacilityHandleQuery
{
    int BuildingVersion => 0;
    IReadOnlyList<ProductionFacilityHandle> Facilities { get; }
    IReadOnlyList<ProductionOutputCapabilityContractSnapshot>
        OutputCapabilityContracts =>
        Array.Empty<ProductionOutputCapabilityContractSnapshot>();
    ProductionWorkerHandle CaptureWorker(object runtimeObject);
    bool IsWorkerEligible(
        ProductionWorkerHandle worker,
        WorkerSelectionPolicySaveData policy,
        out string failureReason)
    {
        failureReason = string.Empty;
        return true;
    }
    float GetRelevantCraftSkill(
        ProductionWorkerHandle worker,
        ProductionRecipeSO recipe) => 50f;

    int CountDelivered(string itemId, string destinationId);
    int CountPending(string itemId, string destinationId);
    int CountAvailableStock(string itemId, string excludedDestinationId);
    int CountBufferedOutput(string itemId);
    int CountBufferedOutput(string itemId, string destinationId);
    bool RequestDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);
    bool ConsumeDeliveredToWip(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        string operationId,
        out ProductionWipInputReceipt receipt,
        out string failureReason);
    bool AcknowledgeWipInput(
        string commitId,
        out string failureReason);
    bool CommitStockSensorInstallPending(
        string destinationId,
        string itemId,
        string operationId,
        string reasonCode,
        out ProductionStockSensorPhysicalReceipt receipt,
        out string failureReason);
    bool TryGetPendingStockSensorInstall(
        string operationId,
        out ProductionStockSensorPhysicalReceipt receipt);
    bool AcknowledgeStockSensorInstall(
        string commitId,
        out string failureReason);
    bool SpawnOutput(string itemId, int amount, Vector2Int position);
    bool SpawnBufferedOutput(
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId);
    bool TryCommitBufferedOutput(
        string commitId,
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId,
        out DomainFailure failure);
    bool AcknowledgeBufferedOutput(
        string commitId,
        out DomainFailure failure);
    bool TryRouteBufferedOutput(
        string sourceDestinationId,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int routed,
        out DomainFailure failure);
    void PrioritizeDestination(string destinationId);
    int ReleaseDestination(string destinationId, Vector2Int releasePosition);
    bool TryReleaseDestinationAtomically(
        string destinationId,
        Vector2Int releasePosition,
        out int released,
        out string failureReason);
    int RemoveDestination(string destinationId);
    string GetOldestAvailableStackId(
        string itemId,
        string excludedDestinationId);

    ProductionBillRecord FindRunnableBill(
        IReadOnlyList<ProductionBillRecord> bills,
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId,
        bool requireDeliveredInputs,
        out DomainFailure failure);
    bool HasDeliveredInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out DomainFailure failure);
    void RequestMissingInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility);
    long ResolveInputBufferMassCapacity(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility);
    void RecalculatePrefetch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionWorkerHandle worker);
    bool ShouldRunAnotherCycle(
        ProductionBillRecord record,
        ProductionRecipeSO recipe);
    bool IsResearchUnlocked(
        ProductionRecipeSO recipe,
        out DomainFailure failure);
    Dictionary<string, int> ToCycleInputMap(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility);

    bool ValidateCycleRequirements(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out string failureReason);
    bool ValidateProcessingUtilities(
        string occupiedSupportNodeId,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason);
    bool TryConsumeCycleUtilities(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out ProductionProcessFluidReceipt receipt,
        out string failureReason);
    bool AcknowledgeCycleUtilities(
        ProductionProcessFluidReceipt receipt,
        out string failureReason);
    bool TryResolveBatchSupport(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out string supportNodeId,
        out string failureReason);
    float ResolveTemperatureSpeed(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out bool dangerous);
    ProductionFacilityHandle ResolveOccupiedBatchSupport(
        string occupiedSupportNodeId,
        ProductionFacilityHandle facility);

    int ResolveOutputCapacity(
        ProductionFacilityHandle facility,
        string itemId,
        int outputPerBatch,
        int stackLimit);
    int ResolveOutputBufferCycleCapacity(
        ProductionFacilityHandle facility) => 4;
    float ResolveSupportModifier(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe,
        ProductionSupportModifierKind kind,
        float defaultValue,
        bool multiply);
    ProductionOutputCapabilityDescriptor CaptureOutputCapability(
        string outputLineId,
        string itemId);
    bool TryValidateOutputCapability(
        ProductionOutputCapabilityDescriptor capability,
        out DomainFailure failure);
    bool TryHandleOutput(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        ProductionOutputCapabilityDescriptor capability,
        int amount,
        string outputDestinationId,
        float qualityModifier,
        float workerQuality,
        string commitId,
        out bool handled,
        out DomainFailure failure);
    bool AcknowledgeHandledOutput(
        ProductionOutputCapabilityDescriptor capability,
        string commitId,
        out DomainFailure failure);
    bool TryCaptureCommittedOutput(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        ProductionOutputCapabilityDescriptor capability,
        int amount,
        string outputDestinationId,
        float qualityModifier,
        float workerQuality,
        string commitId,
        out ProductionCommittedOutputSnapshot snapshot,
        out DomainFailure failure);

    bool MatchesWorkstation(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe);
    bool HasRequiredSupports(
        ProductionFacilityHandle facility,
        IReadOnlyList<string> requiredFeatureTags,
        out string failureReason);
    bool HasCompatibleWarehouse(string itemId);
    void RequestWorkReplan(WorkTypeId workTypeId);
    void RequestOneHaulerToReplan(bool forceInterrupt);
}

public interface IProductionInputDestinationClaimRuntime
{
    bool TryValidateClaim(
        ProductionBillRecord record,
        out string failureReason);

    bool TryClaim(
        ProductionBillRecord record,
        ProductionFacilityHandle facility,
        long maxInputBufferMassGrams,
        out string failureReason);

    bool TryEnsureCapacity(
        ProductionBillRecord record,
        long minimumInputBufferMassGrams,
        out string failureReason);

    bool TryRevoke(
        ProductionBillRecord record,
        out string failureReason);

    bool TryRevokeIfPresent(
        ProductionBillRecord record,
        out string failureReason);

    bool TryReplace(
        IReadOnlyList<ProductionBillRecord> records,
        IReadOnlyList<ProductionFacilityHandle> facilities,
        IReadOnlyDictionary<string, long> inputBufferMassGramsByBillId,
        out string failureReason);
}

public interface IProductionBillCoreQuery
{
    int Version { get; }
    IReadOnlyList<ProductionBillSnapshot> GetBills(
        ProductionFacilityHandle facility);
    ProductionFacilityBillLifecycleSnapshot CaptureFacilityLifecycle(
        BuildingInstanceId facilityId);
    bool HasStockSensor(ProductionFacilityHandle facility);
}

public interface IProductionBillCoreOrderCommand
{
    ProductionBillCommandResult AddBill(
        ProductionFacilityHandle facility,
        string recipeId,
        ProductionOrderMode mode,
        int amount);
    ProductionBillCommandResult RemoveBill(
        ProductionBillId billId,
        bool returnMaterials);
    ProductionBillCommandResult MoveBill(
        ProductionBillId billId,
        int targetIndex);
    ProductionBillCommandResult SetSuspended(
        ProductionBillId billId,
        bool suspended);
    ProductionBillCommandResult SetStockPolicy(
        ProductionBillId billId,
        int minimumReserve,
        int targetStock);
    ProductionBillCommandResult SetOrderMode(
        ProductionBillId billId,
        ProductionOrderMode mode,
        int amount);
    ProductionBillCommandResult SetDistributionPolicy(
        ProductionBillId billId,
        ProductionDistributionMode mode,
        IReadOnlyList<ProductionConsumerRoutePolicy> routes);
    ProductionBillCommandResult SetWorkerPolicy(
        ProductionBillId billId,
        WorkerSelectionPolicySaveData policy);
    ProductionBillCommandResult SetEmergencyWorker(
        ProductionBillId billId,
        string characterId);
    ProductionBillCommandResult RequestStockSensorInstallation(
        ProductionFacilityHandle facility);
    ProductionBillCommandResult AcknowledgeStockSensorUnlock(
        ProductionFacilityHandle facility);
    ProductionBillCommandResult RemoveStockSensor(
        ProductionFacilityHandle facility);
}

public interface IProductionBillCoreWorkExecution
{
    ProductionWorkAvailabilityResult CheckWorkAvailability(
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId);
    ProductionWorkBeginResult BeginWork(
        ProductionWorkerHandle worker,
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId);
    ProductionWorkExecutionResult ExecuteWork(
        ProductionWorkerHandle worker,
        ProductionFacilityHandle facility,
        ProductionBillId billId,
        float amount);
}
