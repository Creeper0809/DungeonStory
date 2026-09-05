using System;
using System.Collections.Generic;
using System.Linq;

public enum ProductionOutputCapabilityRoute
{
    None = 0,
    PreparedBatch = 1,
    ExactCapability = 2
}

/// <summary>
/// Selects the common prepared-output transaction from the frozen output
/// capabilities of a recipe. Content IDs, prefixes and migration cardinality
/// are deliberately absent: a future definition-only recipe is admitted by
/// the same standard capability, while a stateful/special capability remains
/// on its own exact-once producer until it supplies a prepared batch codec.
/// </summary>
public static class ProductionPreparedOutputCapabilitySelection
{
    public static bool UsesPreparedOutputMaterializer(
        ProductionRecipeSO recipe,
        IProductionAssemblyBridge bridge)
    {
        if (recipe == null)
            return false;
        if (bridge == null)
            throw new ArgumentNullException(nameof(bridge));

        ProductionOutputDefinition[] physical = recipe
            .CaptureCanonicalOutputs()
            .Where(value => value != null
                && ProductionOutputRoleRules.IsPhysical(value.Role)
                && value.Amount > 0
                && value.Probability > 0f)
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        if (physical.Length == 0)
            return false;

        ProductionOutputCapabilityDescriptor[] descriptors =
            new ProductionOutputCapabilityDescriptor[physical.Length];
        for (int i = 0; i < physical.Length; i++)
        {
            ProductionOutputDefinition output = physical[i];
            try
            {
                descriptors[i] = bridge.CaptureOutputCapability(
                    output.OutputLineId,
                    output.ItemId);
            }
            catch (InvalidOperationException)
            {
                // Read-only portfolio projections may be built with a partial
                // domain registry. The executable bill path captures the full
                // vector again before RNG and fails loudly if it is genuinely
                // unsupported in the live composition root.
                return false;
            }
        }
        return ClassifyPhysicalCapabilities(
                descriptors,
                bridge.OutputCapabilityContracts)
            == ProductionOutputCapabilityRoute.PreparedBatch;
    }

    /// <summary>
    /// Classifies the complete authored physical-output vector before any RNG
    /// draw or publication. Standard definition output is transaction-owned by
    /// the prepared batch coordinator and may never be mixed into the legacy
    /// per-line exact-capability route.
    /// </summary>
    public static ProductionOutputCapabilityRoute ClassifyPhysicalCapabilities(
        IReadOnlyList<ProductionOutputCapabilityDescriptor> descriptors,
        IReadOnlyList<ProductionOutputCapabilityContractSnapshot> contracts)
    {
        if (descriptors == null)
            throw new ArgumentNullException(nameof(descriptors));
        if (contracts == null)
            throw new ArgumentNullException(nameof(contracts));
        if (descriptors.Count == 0)
            return ProductionOutputCapabilityRoute.None;

        Dictionary<string, ProductionOutputCapabilityContractSnapshot>
            contractById = new(StringComparer.Ordinal);
        for (int index = 0; index < contracts.Count; index++)
        {
            ProductionOutputCapabilityContractSnapshot contract =
                contracts[index];
            if (!contractById.TryAdd(contract.CapabilityId, contract))
            {
                throw new InvalidOperationException(
                    "duplicate-output-capability-contract");
            }
        }

        bool hasPreparedMaterializer = false;
        bool hasExactCapability = false;
        for (int i = 0; i < descriptors.Count; i++)
        {
            ProductionOutputCapabilityDescriptor descriptor = descriptors[i];
            if (!contractById.TryGetValue(
                    descriptor.CapabilityId,
                    out ProductionOutputCapabilityContractSnapshot contract)
                || contract.ContractVersion != descriptor.CapabilityVersion
                || !string.Equals(
                    contract.ComponentCodecId,
                    descriptor.ComponentCodecId,
                    StringComparison.Ordinal)
                || contract.ComponentCodecVersion !=
                    descriptor.ComponentCodecVersion)
            {
                throw new InvalidOperationException(
                    "output-capability-contract-missing-or-drifted");
            }
            hasPreparedMaterializer |= contract.ParticipatesInPreparedOutput;
            hasExactCapability |= !contract.ParticipatesInPreparedOutput;
        }

        if (hasPreparedMaterializer && hasExactCapability)
        {
            throw new InvalidOperationException(
                "mixed-prepared-output-capability-route-unsupported");
        }
        return hasPreparedMaterializer
            ? ProductionOutputCapabilityRoute.PreparedBatch
            : ProductionOutputCapabilityRoute.ExactCapability;
    }
}

/// <summary>
/// Canonical profile authority for a capability-selected prepared-output
/// recipe. The profile is derived from live recipe semantics; it never owns a
/// recipe registry or content-ID allowlist.
/// </summary>
public static class ProductionPreparedOutputMigrationScope
{
    public const string ProfileDigestSchemaToken =
        "production-prepared-output-migration-profile@2";

    public static string CaptureProfileDigest(ProductionRecipeSO recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        ValidateCanonicalProfileOrThrow(recipe);
        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(ProfileDigestSchemaToken);
        canonical.Append(ProductionRecipeSemanticDigest.Capture(recipe));
        return canonical.ComputeSha256();
    }

    public static void ValidateSavedProfileDigest(
        ProductionPreparedOutputBatchSaveData batch,
        ProductionRecipeSO recipe,
        string context)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        if (batch.phase == ProductionPreparedOutputPhase.Unresolved)
            return;
        string current = CaptureProfileDigest(recipe);
        if (!string.Equals(
                batch.migrationProfileDigest,
                current,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                (context ?? string.Empty)
                + ":prepared-output-capability-profile-stale");
        }
    }

    public static void ValidateCanonicalProfileOrThrow(
        ProductionRecipeSO recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        if (string.IsNullOrEmpty(recipe.RecipeId)
            || recipe.CaptureCanonicalOutputs().Count == 0)
        {
            throw new InvalidOperationException(
                $"Recipe '{recipe.RecipeId}' has no canonical prepared-output profile.");
        }
    }

    public static bool HasLegacyOutputAuthority(
        ProductionBillRecord record) => record == null
        || record.outputOutcomeResolved
        || record.resolvedOutputs.Count != 0
        || record.outputReservations.Count != 0;

    public static bool HasLegacyOutputAuthority(
        ProductionBillSaveData saved) => saved == null
        || saved.outputOutcomeResolved
        || (saved.resolvedOutputs?.Count ?? 0) != 0
        || (saved.outputReservations?.Count ?? 0) != 0;

    public static DomainFailure CreateLegacyAuthorityConflict(
        ProductionBillRecord record) => new(
        FailureCode.ProductionOutputUnavailable,
        record == null ? string.Empty : record.billId.Value,
        "prepared-output-legacy-authority-conflict");

    public static bool RequiresCycleStartCapacity(
        ProductionBillRecord record) => record != null
        && !record.materialsConsumed
        && (record.preparedOutput?.phase
            ?? ProductionPreparedOutputPhase.Unresolved)
            == ProductionPreparedOutputPhase.Unresolved;

    public static bool RequiresAdditionalOutputCapacity(
        ProductionBillRecord record)
    {
        if (record == null)
            return false;
        ProductionPreparedOutputPhase phase = record.preparedOutput?.phase
            ?? ProductionPreparedOutputPhase.Unresolved;
        return phase ==
                ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace
            || phase == ProductionPreparedOutputPhase.Unresolved
                && !record.materialsConsumed;
    }

}

public readonly struct ProductionPreparedOutputCapacityResult
{
    private ProductionPreparedOutputCapacityResult(
        bool isValid,
        bool canBeginCycle,
        long maximumMassGrams,
        long occupiedMassGrams,
        long reservedMassGrams,
        DomainFailure failure)
    {
        IsValid = isValid;
        CanBeginCycle = canBeginCycle;
        MaximumMassGrams = maximumMassGrams;
        OccupiedMassGrams = occupiedMassGrams;
        ReservedMassGrams = reservedMassGrams;
        Failure = failure;
    }

    public bool IsValid { get; }
    public bool CanBeginCycle { get; }
    public long MaximumMassGrams { get; }
    public long OccupiedMassGrams { get; }
    public long ReservedMassGrams { get; }
    public DomainFailure Failure { get; }

    public static ProductionPreparedOutputCapacityResult Available(
        long maximumMassGrams,
        long occupiedMassGrams,
        long reservedMassGrams)
    {
        ValidateMasses(
            maximumMassGrams,
            occupiedMassGrams,
            reservedMassGrams);
        if (checked(occupiedMassGrams + reservedMassGrams)
            > maximumMassGrams)
        {
            throw new ArgumentException(
                "An available prepared-output capacity result cannot be over capacity.");
        }
        return new ProductionPreparedOutputCapacityResult(
            true,
            true,
            maximumMassGrams,
            occupiedMassGrams,
            reservedMassGrams,
            DomainFailure.None);
    }

    public static ProductionPreparedOutputCapacityResult Blocked(
        long maximumMassGrams,
        long occupiedMassGrams,
        long reservedMassGrams,
        DomainFailure failure)
    {
        ValidateMasses(
            maximumMassGrams,
            occupiedMassGrams,
            reservedMassGrams);
        if (!failure.IsFailure)
        {
            throw new ArgumentException(
                "A blocked prepared-output capacity result requires a failure.",
                nameof(failure));
        }
        return new ProductionPreparedOutputCapacityResult(
            true,
            false,
            maximumMassGrams,
            occupiedMassGrams,
            reservedMassGrams,
            failure);
    }

    public static ProductionPreparedOutputCapacityResult Unavailable(
        DomainFailure failure)
    {
        if (!failure.IsFailure)
        {
            throw new ArgumentException(
                "An unavailable prepared-output capacity result requires a failure.",
                nameof(failure));
        }
        return new ProductionPreparedOutputCapacityResult(
            true,
            false,
            0L,
            0L,
            0L,
            failure);
    }

    private static void ValidateMasses(
        long maximumMassGrams,
        long occupiedMassGrams,
        long reservedMassGrams)
    {
        if (maximumMassGrams <= 0L
            || occupiedMassGrams < 0L
            || reservedMassGrams < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumMassGrams),
                "Prepared-output capacity masses are invalid.");
        }
    }
}

public readonly struct ProductionPreparedOutputExecutionResult
{
    private ProductionPreparedOutputExecutionResult(
        bool isValid,
        bool cycleOutputCompleted,
        ProductionPreparedOutputPhase phase,
        DomainFailure failure)
    {
        IsValid = isValid;
        CycleOutputCompleted = cycleOutputCompleted;
        Phase = phase;
        Failure = failure;
    }

    public bool IsValid { get; }
    public bool CycleOutputCompleted { get; }
    public ProductionPreparedOutputPhase Phase { get; }
    public DomainFailure Failure { get; }

    public static ProductionPreparedOutputExecutionResult Completed() => new(
        true,
        true,
        ProductionPreparedOutputPhase.Completed,
        DomainFailure.None);

    public static ProductionPreparedOutputExecutionResult Blocked(
        ProductionPreparedOutputPhase phase,
        DomainFailure failure)
    {
        if (!Enum.IsDefined(typeof(ProductionPreparedOutputPhase), phase)
            || phase == ProductionPreparedOutputPhase.Completed)
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }
        if (!failure.IsFailure)
        {
            throw new ArgumentException(
                "A blocked prepared-output execution result requires a failure.",
                nameof(failure));
        }
        return new ProductionPreparedOutputExecutionResult(
            true,
            false,
            phase,
            failure);
    }
}

public readonly struct ProductionPreparedOutputReleaseResult
{
    private ProductionPreparedOutputReleaseResult(
        bool isValid,
        bool released,
        bool physicalBatchCommitted,
        DomainFailure failure)
    {
        IsValid = isValid;
        Released = released;
        PhysicalBatchCommitted = physicalBatchCommitted;
        Failure = failure;
    }

    public bool IsValid { get; }
    public bool Released { get; }
    public bool PhysicalBatchCommitted { get; }
    public DomainFailure Failure { get; }

    public static ProductionPreparedOutputReleaseResult ReleasedUnpublished() =>
        new(true, true, false, DomainFailure.None);

    public static ProductionPreparedOutputReleaseResult Blocked(
        bool physicalBatchCommitted,
        DomainFailure failure)
    {
        if (!failure.IsFailure)
        {
            throw new ArgumentException(
                "A blocked prepared-output release result requires a failure.",
                nameof(failure));
        }
        return new ProductionPreparedOutputReleaseResult(
            true,
            false,
            physicalBatchCommitted,
            failure);
    }
}

/// <summary>
/// Economy-owned boundary implemented by the composition-root assembly. The
/// implementation owns all prepared-output phase transitions; the bill runtime
/// only admits a cycle reset after a validated Completed result. Capacity
/// assessments are read-only. Release is idempotent for Unresolved/unpublished state and may
/// clear unpublished authority, but must return Blocked once a physical batch
/// exists; deleting physical output is never a release fallback.
/// </summary>
public interface IProductionPreparedOutputExecutionPort
{
    void RestoreDestinationAuthorities(
        System.Collections.Generic.IReadOnlyList<ProductionBillRecord> records,
        System.Collections.Generic.IReadOnlyList<ProductionFacilityHandle> facilities);

    ProductionPreparedOutputCapacityResult AssessCycleStart(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility);

    ProductionPreparedOutputCapacityResult AssessCurrentCapacity(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility);

    ProductionPreparedOutputExecutionResult Execute(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker);

    ProductionPreparedOutputReleaseResult Release(
        ProductionBillRecord record,
        ProductionWipTerminalReason reason);
}
