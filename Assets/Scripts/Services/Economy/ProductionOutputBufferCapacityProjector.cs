using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ProductionFacilityCapacitySubjectAdapter
{
    public static ProductionFacilityCapacitySubject FromSave(
        ModularFacilityBuildingSaveData source,
        IBuildingDefinitionLookup definitions)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));
        string instanceId = source.persistentInstanceId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(instanceId)
            || !string.Equals(instanceId, instanceId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Saved production facility ID is not canonical.");
        }

        BuildingSO definition = definitions.GetBuilding(source.buildingId)
            ?? throw new InvalidOperationException(
                "Saved production facility definition is missing.");
        if (definition.id != source.buildingId)
            throw new InvalidOperationException(
                "Saved production facility definition lookup returned the wrong ID.");
        BuildingProductionWorkstationAbility workstation =
            definition.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility buffer =
            definition.GetProductionBufferAbility();
        if (workstation == null || buffer == null)
        {
            throw new InvalidOperationException(
                "Saved building is not a production output-capacity facility.");
        }

        return new ProductionFacilityCapacitySubject(
            (BuildingInstanceId)instanceId,
            new Vector2Int(source.centerX, source.centerY),
            ProductionFacilityDefinitionIdentity.Resolve(definition),
            workstation.WorkstationTag,
            buffer.physicalOutputBufferCycleCapacity,
            CaptureWorkstationLaneProfile(definition),
            CaptureProcessFluidProfile(definition));
    }

    public static ProductionFacilityWorkstationLaneCapacityProfile
        CaptureWorkstationLaneProfile(BuildingSO definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        BuildingProductionWorkstationAbility workstation =
            definition.GetProductionWorkstationAbility();
        if (workstation == null || !workstation.IsValid)
        {
            throw new InvalidOperationException(
                "Production facility is missing valid authored workstation lane authority: "
                + ProductionFacilityDefinitionIdentity.Resolve(definition));
        }
        return new ProductionFacilityWorkstationLaneCapacityProfile(
            workstation.lanePolicy,
            workstation.ManualWorkLaneCount,
            workstation.AutomaticWorkLaneCount);
    }

    public static ProductionFacilityProcessFluidCapacityProfile
        CaptureProcessFluidProfile(BuildingSO definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        BuildingProcessFluidAbility ability =
            definition.GetAbility<BuildingProcessFluidAbility>();
        if (ability == null)
            return ProductionFacilityProcessFluidCapacityProfile.Empty;

        string[] authoredWorkTypes = ability.workTypeIds
            ?? Array.Empty<string>();
        if (authoredWorkTypes.Length == 0)
        {
            return ProductionFacilityProcessFluidCapacityProfile.Empty;
        }
        return new ProductionFacilityProcessFluidCapacityProfile(
            authoredWorkTypes,
            Mathf.Max(0f, ability.cleanWaterPerCycle),
            Mathf.Max(0f, ability.wastewaterPerCycle));
    }
}

public readonly struct ProductionOutputBufferCapacitySourceSnapshot
{
    public ProductionOutputBufferCapacitySourceSnapshot(
        int cycleCapacity,
        long maximumBatchMassGrams,
        long projectedPortfolioCapacityGrams,
        long batchMinimumCapacityGrams,
        long requiredMinimumCapacityGrams,
        string sourceDigest,
        string clearanceProfileDigest = "",
        string clearanceGateDigest = "",
        string clearanceAuthorityDigest = "")
    {
        if (cycleCapacity is < 2 or > 4
            || maximumBatchMassGrams < 0L
            || projectedPortfolioCapacityGrams < 0L
            || batchMinimumCapacityGrams < 0L
            || requiredMinimumCapacityGrams
                != Math.Max(
                    projectedPortfolioCapacityGrams,
                    batchMinimumCapacityGrams))
        {
            throw new ArgumentException(
                "Capacity source snapshot has invalid exact quantities.");
        }
        if (string.IsNullOrEmpty(sourceDigest) || sourceDigest.Length != 64)
            throw new ArgumentException(
                "Capacity source digest must be SHA-256.",
                nameof(sourceDigest));
        clearanceProfileDigest ??= string.Empty;
        clearanceGateDigest ??= string.Empty;
        clearanceAuthorityDigest ??= string.Empty;
        bool hasClearanceAuthority = clearanceProfileDigest.Length != 0
            || clearanceGateDigest.Length != 0
            || clearanceAuthorityDigest.Length != 0;
        if (hasClearanceAuthority
            && (!IsLowercaseSha256(clearanceProfileDigest)
                || !IsLowercaseSha256(clearanceGateDigest)
                || !IsLowercaseSha256(clearanceAuthorityDigest)))
        {
            throw new ArgumentException(
                "Capacity clearance digests must be complete lowercase SHA-256 values.");
        }
        CycleCapacity = cycleCapacity;
        MaximumBatchMassGrams = maximumBatchMassGrams;
        ProjectedPortfolioCapacityGrams = projectedPortfolioCapacityGrams;
        BatchMinimumCapacityGrams = batchMinimumCapacityGrams;
        RequiredMinimumCapacityGrams = requiredMinimumCapacityGrams;
        SourceDigest = sourceDigest;
        ClearanceProfileDigest = clearanceProfileDigest;
        ClearanceGateDigest = clearanceGateDigest;
        ClearanceAuthorityDigest = clearanceAuthorityDigest;
    }

    public int CycleCapacity { get; }
    public long MaximumBatchMassGrams { get; }
    public long ProjectedPortfolioCapacityGrams { get; }
    public long BatchMinimumCapacityGrams { get; }
    public long RequiredMinimumCapacityGrams { get; }
    public string SourceDigest { get; }
    public string ClearanceProfileDigest { get; }
    public string ClearanceGateDigest { get; }
    public string ClearanceAuthorityDigest { get; }

    private static bool IsLowercaseSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
}

/// <summary>
/// Immutable capability-owned upper bound for one complete domain output
/// batch. The proof is computed before capacity admission and is independent
/// from the actual prepared component mass. A domain output may be lighter
/// than this envelope, but may never expand the envelope after resolution.
/// </summary>
public sealed class ProductionOutputBatchMaximumMassProof
{
    public const string Schema =
        "production-output-batch-maximum-mass-proof@1";

    private readonly IReadOnlyList<ProductionOutputMaximumMassProjection>
        projections;

    public ProductionOutputBatchMaximumMassProof(
        IReadOnlyList<ProductionOutputMaximumMassProjection> projections)
    {
        ProductionOutputMaximumMassProjection[] ordered = (projections
                ?? throw new ArgumentNullException(nameof(projections)))
            .OrderBy(value => value.Descriptor.OutputLineId,
                StringComparer.Ordinal)
            .ThenBy(value => value.Descriptor.ItemId, StringComparer.Ordinal)
            .ThenBy(value => value.Descriptor.CapabilityId,
                StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0)
            throw new ArgumentException(
                "A domain output maximum-mass proof requires at least one line.",
                nameof(projections));
        if (ordered.Select(value => value.Descriptor.OutputLineId)
            .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "A domain output maximum-mass proof contains duplicate output lines.");
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(ordered.Length);
        long maximumBatchMassGrams = 0L;
        foreach (ProductionOutputMaximumMassProjection projection in ordered)
        {
            ProductionOutputCapabilityDescriptor descriptor =
                projection.Descriptor;
            if (!ProductionOutputDefinition.IsCanonicalOutputLineId(
                    descriptor.OutputLineId)
                || string.IsNullOrEmpty(descriptor.ItemId)
                || !string.Equals(
                    descriptor.ItemId,
                    descriptor.ItemId.Trim(),
                    StringComparison.Ordinal)
                || projection.MaximumQuantity <= 0
                || projection.DefinitionUnitMassGrams <= 0L
                || projection.MaximumMassGrams <= 0L
                || projection.MassAuthorityRevision < 0L
                || !IsSha256(projection.SourceDigest))
            {
                throw new InvalidOperationException(
                    "A domain output maximum-mass projection is not canonical.");
            }
            maximumBatchMassGrams = checked(
                maximumBatchMassGrams + projection.MaximumMassGrams);
            digest.Append(descriptor.OutputLineId);
            digest.Append(descriptor.ItemId);
            digest.Append(descriptor.CapabilityId);
            digest.Append(descriptor.CapabilityVersion);
            digest.Append(descriptor.ComponentCodecId);
            digest.Append(descriptor.ComponentCodecVersion);
            digest.Append(descriptor.Fingerprint);
            digest.Append(projection.MaximumQuantity);
            digest.Append(projection.DefinitionUnitMassGrams);
            digest.Append(projection.MaximumMassGrams);
            digest.Append(projection.MassAuthorityRevision);
            digest.Append(projection.SourceDigest);
        }
        if (maximumBatchMassGrams <= 0L)
            throw new InvalidOperationException(
                "A domain output maximum-mass proof must be positive.");

        MaximumBatchMassGrams = maximumBatchMassGrams;
        SourceDigest = digest.ComputeSha256();
        this.projections = Array.AsReadOnly(ordered);
    }

    public IReadOnlyList<ProductionOutputMaximumMassProjection> Projections =>
        projections;
    public long MaximumBatchMassGrams { get; }
    public string SourceDigest { get; }

    private static bool IsSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
}

/// <summary>
/// Immutable pre-publication authority for a ruined passive batch. It binds
/// the exact WIP/fluid provenance to the frozen waste capability and its
/// capability-owned maximum-mass proof. The claim is derived before any waste
/// output is materialized and is recomputed exactly on retry and restore.
/// </summary>
public sealed class ProductionRuinedOutputCapacityClaim
{
    public const string Schema =
        "production-ruined-output-capacity-claim@1";

    internal ProductionRuinedOutputCapacityClaim(
        ProductionRuinedBatchDispositionPlan disposition,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        string sourceDigest)
    {
        if (maximumMassProof == null)
            throw new ArgumentNullException(nameof(maximumMassProof));
        if (disposition.RecoverableWasteQuantity <= 0
            || disposition.RecoverableWasteMassGrams <= 0L
            || maximumMassProof.Projections.Count != 1
            || maximumMassProof.MaximumBatchMassGrams
                != disposition.RecoverableWasteMassGrams
            || !string.Equals(
                maximumMassProof.Projections[0].Descriptor.OutputLineId,
                ProductionRuinedBatchDispositionPlan
                    .RecoverableWasteOutputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                maximumMassProof.Projections[0].Descriptor.ItemId,
                disposition.SpoilageItemId,
                StringComparison.Ordinal)
            || maximumMassProof.Projections[0].MaximumQuantity
                != disposition.RecoverableWasteQuantity
            || !IsSha256(sourceDigest))
        {
            throw new InvalidOperationException(
                "Ruined-output capacity claim is inconsistent with its disposition or proof.");
        }

        Disposition = disposition;
        MaximumMassProof = maximumMassProof;
        SourceDigest = sourceDigest;
    }

    public ProductionRuinedBatchDispositionPlan Disposition { get; }
    public ProductionOutputBatchMaximumMassProof MaximumMassProof { get; }
    public string SourceDigest { get; }

    private static bool IsSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
}

/// <summary>
/// Immutable capacity authority for one already-resolved normal prepared
/// batch. It binds the bill/cycle/recipe identity and every frozen physical
/// output line to capability-owned maximum projections, so retry and detached
/// restore never use the actual mass as an admission authority.
/// </summary>
public sealed class ProductionPreparedOutputCapacityClaim
{
    public const string Schema =
        "production-prepared-output-capacity-claim@1";

    internal ProductionPreparedOutputCapacityClaim(
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        long exactBatchMassGrams,
        string sourceDigest)
    {
        if (maximumMassProof == null)
            throw new ArgumentNullException(nameof(maximumMassProof));
        if (exactBatchMassGrams <= 0L
            || exactBatchMassGrams > maximumMassProof.MaximumBatchMassGrams
            || !IsSha256(sourceDigest))
        {
            throw new InvalidOperationException(
                "Prepared-output capacity claim is inconsistent with its proof.");
        }
        MaximumMassProof = maximumMassProof;
        ExactBatchMassGrams = exactBatchMassGrams;
        SourceDigest = sourceDigest;
    }

    public ProductionOutputBatchMaximumMassProof MaximumMassProof { get; }
    public long ExactBatchMassGrams { get; }
    public string SourceDigest { get; }

    private static bool IsSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
}

/// <summary>
/// Pure source-revision gate shared by runtime resume/restore and focused
/// verification. It never rewrites saved output or accepts a recomputed
/// fallback when any exact capacity input has drifted.
/// </summary>
public static class ProductionOutputBufferCapacitySourceGuard
{
    public const string StaleFailureToken =
        "prepared-output-capacity-source-stale";

    public static ProductionOutputBufferCapacitySourceSnapshot ValidateSaved(
        ProductionPreparedOutputBatchSaveData batch,
        ProductionOutputBufferCapacitySourceSnapshot current,
        string context)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));
        if (!string.Equals(
                batch.capacitySourceDigest,
                current.SourceDigest,
                StringComparison.Ordinal)
            || batch.outputBufferCycleCapacity != current.CycleCapacity
            || batch.projectedPortfolioCapacityGrams
                != current.ProjectedPortfolioCapacityGrams
            || batch.requiredMinimumCapacityGrams
                != current.RequiredMinimumCapacityGrams)
        {
            throw new InvalidOperationException(
                (context ?? string.Empty) + ":" + StaleFailureToken);
        }
        return current;
    }
}

/// <summary>
/// Projects one facility's physical output-buffer capacity from the heaviest
/// reachable prepared-output branch. Legacy count capacity is deliberately not
/// an input to this authority.
/// </summary>
public interface IProductionOutputBufferCapacityProjector
{
    ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
        ProductionFacilityHandle facility,
        ProductionOutputBatchMaximumMassProof maximumMassProof);
}

public sealed class ProductionOutputBufferCapacityProjector :
    IProductionOutputBufferCapacityProjector
{
    public const string SourceDigestSchemaToken =
        "production-output-buffer-capacity-source@6";
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionMaximumOutputFactorCatalog maximumFactors;
    private readonly IProductionPreparedOutputComponentCodec componentCodec;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IProductionOutputClearanceProfileSource clearanceProfiles;
    private readonly IProductionPassiveBatchOutputPortfolioQuery
        passiveBatchOutputs;
    private readonly Func<ProductionFacilityHandle, int> resolveCycleCapacity;
    private readonly Func<ProductionFacilityHandle, ProductionRecipeSO, bool>
        matchesWorkstation;
    private readonly Func<string, string, int,
        ProductionOutputMaximumMassProjection> captureMaximumMass;
    private readonly Func<ProductionOutputCapabilityDescriptor, int,
        ProductionOutputMaximumMassProjection> captureDeclaredMaximumMass;
    private readonly Func<ProductionFacilityCapacitySubject,
        ProductionFacilityOutputCapacityAggregateSnapshot>
        captureFacilityContributions;

    /// <summary>
    /// Direct composition constructor retained for deterministic fixtures that
    /// already own fully constructed authorities. Runtime DI uses the lazy
    /// factory overload below so registry construction cannot recurse.
    /// </summary>
    public ProductionOutputBufferCapacityProjector(
        IResourceEconomyContentCatalog catalog,
        IProductionAssemblyBridge bridge,
        IProductionMaximumOutputFactorCatalog maximumFactors,
        IProductionPreparedOutputComponentCodec componentCodec,
        IPhysicalItemMassQuery massQuery,
        IProductionOutputMaximumMassRegistry maximumMassRegistry,
        IProductionFacilityOutputCapacityContributorRegistry contributorRegistry = null,
        IProductionPassiveBatchOutputPortfolioQuery passiveBatchOutputs = null,
        IProductionOutputClearanceProfileSource clearanceProfiles = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        IProductionAssemblyBridge requiredBridge = bridge
            ?? throw new ArgumentNullException(nameof(bridge));
        this.maximumFactors = maximumFactors
            ?? throw new ArgumentNullException(nameof(maximumFactors));
        this.componentCodec = componentCodec
            ?? throw new ArgumentNullException(nameof(componentCodec));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.clearanceProfiles = clearanceProfiles
            ?? new ProductionOutputClearanceProfileResourceSource();
        IProductionOutputMaximumMassRegistry requiredMaximumMassRegistry =
            maximumMassRegistry
            ?? throw new ArgumentNullException(nameof(maximumMassRegistry));
        ValidateCapabilityParity(
            requiredBridge.OutputCapabilityContracts,
            requiredMaximumMassRegistry.CapabilityContracts);
        resolveCycleCapacity = requiredBridge.ResolveOutputBufferCycleCapacity;
        matchesWorkstation = requiredBridge.MatchesWorkstation;
        captureMaximumMass = requiredMaximumMassRegistry.CaptureAutomatic;
        captureDeclaredMaximumMass =
            requiredMaximumMassRegistry.CaptureDeclared;
        captureFacilityContributions = (contributorRegistry
                ?? EmptyProductionFacilityOutputCapacityContributorRegistry.Instance)
            .Capture;
        this.passiveBatchOutputs = passiveBatchOutputs
            ?? new ProductionPassiveBatchOutputPortfolioAuthority(
                this.catalog,
                this.maximumFactors,
                this.massQuery,
                requiredMaximumMassRegistry);
    }

    [VContainer.Inject]
    public ProductionOutputBufferCapacityProjector(
        IResourceEconomyContentCatalog catalog,
        Func<IProductionAssemblyBridge> bridge,
        IProductionMaximumOutputFactorCatalog maximumFactors,
        IProductionPreparedOutputComponentCodec componentCodec,
        IPhysicalItemMassQuery massQuery,
        Func<IProductionOutputMaximumMassRegistry> maximumMassRegistry,
        Func<IProductionFacilityOutputCapacityContributorRegistry>
            contributorRegistry,
        IProductionPassiveBatchOutputPortfolioQuery passiveBatchOutputs,
        IProductionOutputClearanceProfileSource clearanceProfiles)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Func<IProductionAssemblyBridge> requiredBridge = bridge
            ?? throw new ArgumentNullException(nameof(bridge));
        this.maximumFactors = maximumFactors
            ?? throw new ArgumentNullException(nameof(maximumFactors));
        this.componentCodec = componentCodec
            ?? throw new ArgumentNullException(nameof(componentCodec));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.clearanceProfiles = clearanceProfiles
            ?? throw new ArgumentNullException(nameof(clearanceProfiles));
        this.passiveBatchOutputs = passiveBatchOutputs
            ?? throw new ArgumentNullException(nameof(passiveBatchOutputs));
        Func<IProductionOutputMaximumMassRegistry> requiredMaximumMassRegistry =
            maximumMassRegistry
            ?? throw new ArgumentNullException(nameof(maximumMassRegistry));
        Func<IProductionFacilityOutputCapacityContributorRegistry>
            requiredContributorRegistry = contributorRegistry
            ?? throw new ArgumentNullException(nameof(contributorRegistry));
        bool parityValidated = false;
        object parityGate = new();

        IProductionAssemblyBridge ResolveBridge() => requiredBridge()
            ?? throw new InvalidOperationException(
                "Production output capacity bridge factory returned null.");
        IProductionOutputMaximumMassRegistry ResolveMaximumMassRegistry() =>
            requiredMaximumMassRegistry()
            ?? throw new InvalidOperationException(
                "Production output maximum-mass registry factory returned null.");
        IProductionFacilityOutputCapacityContributorRegistry
            ResolveContributorRegistry() => requiredContributorRegistry()
            ?? throw new InvalidOperationException(
                "Production facility output-capacity contributor registry factory returned null.");
        void EnsureParity()
        {
            if (parityValidated)
                return;
            lock (parityGate)
            {
                if (parityValidated)
                    return;
                ValidateCapabilityParity(
                    ResolveBridge().OutputCapabilityContracts,
                    ResolveMaximumMassRegistry().CapabilityContracts);
                parityValidated = true;
            }
        }

        resolveCycleCapacity = facility =>
            ResolveBridge().ResolveOutputBufferCycleCapacity(facility);
        matchesWorkstation = (facility, recipe) =>
            ResolveBridge().MatchesWorkstation(facility, recipe);
        captureMaximumMass = (outputLineId, itemId, maximumQuantity) =>
        {
            EnsureParity();
            return ResolveMaximumMassRegistry().CaptureAutomatic(
                outputLineId,
                itemId,
                maximumQuantity);
        };
        captureDeclaredMaximumMass = (descriptor, maximumQuantity) =>
        {
            EnsureParity();
            return ResolveMaximumMassRegistry().CaptureDeclared(
                descriptor,
                maximumQuantity);
        };
        captureFacilityContributions = subject =>
            ResolveContributorRegistry().Capture(subject);
    }

    /// <summary>
    /// Deterministic audit constructor. Runtime composition uses the assembly
    /// bridge overload; Editor audits inject only the two pure facility reads
    /// consumed by this projector.
    /// </summary>
    public ProductionOutputBufferCapacityProjector(
        IResourceEconomyContentCatalog catalog,
        IProductionMaximumOutputFactorCatalog maximumFactors,
        IProductionPreparedOutputComponentCodec componentCodec,
        IPhysicalItemMassQuery massQuery,
        Func<ProductionFacilityHandle, int> resolveCycleCapacity,
        Func<ProductionFacilityHandle, ProductionRecipeSO, bool>
            matchesWorkstation,
        Func<string, string, int, ProductionOutputMaximumMassProjection>
            captureMaximumMass = null,
        Func<ProductionOutputCapabilityDescriptor, int,
            ProductionOutputMaximumMassProjection>
            captureDeclaredMaximumMass = null,
        IProductionFacilityOutputCapacityContributorRegistry
            contributorRegistry = null,
        IProductionPassiveBatchOutputPortfolioQuery passiveBatchOutputs = null,
        IProductionOutputClearanceProfileSource clearanceProfiles = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.maximumFactors = maximumFactors
            ?? throw new ArgumentNullException(nameof(maximumFactors));
        this.componentCodec = componentCodec
            ?? throw new ArgumentNullException(nameof(componentCodec));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.clearanceProfiles = clearanceProfiles
            ?? new ProductionOutputClearanceProfileResourceSource();
        this.resolveCycleCapacity = resolveCycleCapacity
            ?? throw new ArgumentNullException(nameof(resolveCycleCapacity));
        this.matchesWorkstation = matchesWorkstation
            ?? throw new ArgumentNullException(nameof(matchesWorkstation));
        this.captureMaximumMass = captureMaximumMass
            ?? CreateDefinitionOnlyCapture();
        this.captureDeclaredMaximumMass = captureDeclaredMaximumMass
            ?? ((descriptor, maximumQuantity) =>
            {
                ProductionOutputMaximumMassProjection projection =
                    this.captureMaximumMass(
                        descriptor.OutputLineId,
                        descriptor.ItemId,
                        maximumQuantity);
                if (!string.Equals(
                        projection.Descriptor.Fingerprint,
                        descriptor.Fingerprint,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Declared output maximum-mass capability drifted from the audit projection.");
                }
                return projection;
            });
        captureFacilityContributions = (contributorRegistry
                ?? EmptyProductionFacilityOutputCapacityContributorRegistry.Instance)
            .Capture;
        this.passiveBatchOutputs = passiveBatchOutputs
            ?? new ProductionPassiveBatchOutputPortfolioAuthority(
                this.catalog,
                this.maximumFactors,
                this.massQuery,
                this.captureMaximumMass,
                this.captureDeclaredMaximumMass);
    }

    /// <summary>
    /// Returns zero when the facility cannot execute a migrated prepared-output
    /// recipe. Any malformed reachable recipe fails loudly instead of silently
    /// publishing a smaller capacity.
    /// </summary>
    public long ResolveRequiredCapacityGrams(ProductionFacilityHandle facility)
        => CapturePortfolioSource(facility).ProjectedPortfolioCapacityGrams;

    public ProductionOutputBufferCapacitySourceSnapshot CapturePortfolioSource(
        ProductionFacilityHandle facility)
    {
        if (facility == null || facility.IsDestroyed || !facility.InstanceId.IsValid)
            throw new ArgumentException("A live production facility is required.", nameof(facility));
        ProductionFacilityCapacitySubject subject =
            ProductionFacilityCapacitySubject.FromLive(facility);
        ValidateLiveSubject(facility, subject);
        return CapturePortfolioSource(subject);
    }

    public ProductionOutputBufferCapacitySourceSnapshot CapturePortfolioSource(
        ProductionFacilityCapacitySubject subject) =>
        CaptureSourceCore(subject, 0L);

    public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
        ProductionFacilityHandle facility,
        ProductionOutputBatchMaximumMassProof maximumMassProof)
    {
        if (facility == null || facility.IsDestroyed || !facility.InstanceId.IsValid)
            throw new ArgumentException("A live production facility is required.", nameof(facility));
        if (maximumMassProof == null)
            throw new ArgumentNullException(nameof(maximumMassProof));
        ProductionFacilityCapacitySubject subject =
            ProductionFacilityCapacitySubject.FromLive(facility);
        ValidateLiveSubject(facility, subject);
        return CaptureSource(subject, maximumMassProof);
    }

    private void ValidateLiveSubject(
        ProductionFacilityHandle facility,
        ProductionFacilityCapacitySubject subject)
    {
        int resolvedCycle = resolveCycleCapacity(facility);
        if (resolvedCycle != subject.OutputBufferCycleCapacity)
        {
            throw new InvalidOperationException(
                "Live production output cycle authority drifted from its immutable subject.");
        }
        foreach (ProductionRecipeSO recipe in (catalog.Recipes
                     ?? Array.Empty<ProductionRecipeSO>())
                 .Where(value => value != null))
        {
            bool pure = string.Equals(
                subject.WorkstationTag,
                recipe.WorkstationTag,
                StringComparison.Ordinal);
            if (matchesWorkstation(facility, recipe) != pure)
            {
                throw new InvalidOperationException(
                    "Live workstation matching drifted from the immutable capacity subject: "
                    + recipe.RecipeId);
            }
        }
    }

    public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
        ProductionFacilityCapacitySubject subject,
        ProductionOutputBatchMaximumMassProof maximumMassProof)
    {
        if (maximumMassProof == null)
            throw new ArgumentNullException(nameof(maximumMassProof));
        ProductionOutputBufferCapacitySourceSnapshot portfolio =
            CapturePortfolioSource(subject);
        long proofCapacity = checked(
            maximumMassProof.MaximumBatchMassGrams * portfolio.CycleCapacity);
        long required = Math.Max(
            portfolio.ProjectedPortfolioCapacityGrams,
            proofCapacity);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(SourceDigestSchemaToken);
        digest.Append("capability-owned-domain-proof@1");
        digest.Append(portfolio.SourceDigest);
        digest.Append(maximumMassProof.SourceDigest);
        digest.Append(maximumMassProof.MaximumBatchMassGrams);
        digest.Append(proofCapacity);
        digest.Append(required);
        return new ProductionOutputBufferCapacitySourceSnapshot(
            portfolio.CycleCapacity,
            Math.Max(
                portfolio.MaximumBatchMassGrams,
                maximumMassProof.MaximumBatchMassGrams),
            portfolio.ProjectedPortfolioCapacityGrams,
            proofCapacity,
            required,
            digest.ComputeSha256(),
            portfolio.ClearanceProfileDigest,
            portfolio.ClearanceGateDigest,
            portfolio.ClearanceAuthorityDigest);
    }

    public ProductionRuinedOutputCapacityClaim CaptureRuinedClaim(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionOutputCapabilityDescriptor wasteCapability)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        return CaptureRuinedClaim(
            record.billId.Value,
            record.cycleSequence,
            record.recipeId,
            record.wipInputCommitId,
            record.wipInputQuantity,
            record.wipInputMassGrams,
            record.processFluidConsumed,
            record.processCleanWaterMassGrams,
            record.processWastewaterMassGrams,
            record.processWastewaterComponents,
            record.processManualWaterTransfers,
            recipe,
            wasteCapability);
    }

    public ProductionPreparedOutputCapacityClaim CapturePreparedClaim(
        string billId,
        int cycleSequence,
        ProductionRecipeSO recipe,
        IReadOnlyList<ProductionPreparedOutputLineSaveData> lines)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        if (string.IsNullOrEmpty(billId)
            || !string.Equals(billId, billId.Trim(), StringComparison.Ordinal)
            || cycleSequence < 1
            || lines == null)
        {
            throw new InvalidOperationException(
                "Prepared-output capacity claim source is incomplete.");
        }

        ProductionPreparedOutputLineSaveData[] allOrdered = lines
            .OrderBy(value => value?.outputLineId ?? string.Empty,
                StringComparer.Ordinal)
            .ToArray();
        if (allOrdered.Length == 0
            || allOrdered.Any(value => value == null)
            || allOrdered.Select(value => value.outputLineId)
                .Distinct(StringComparer.Ordinal).Count() != allOrdered.Length)
        {
            throw new InvalidOperationException(
                "Prepared-output capacity claim lines are missing or duplicated.");
        }

        ProductionPreparedOutputLineSaveData[] ordered = allOrdered
            .Where(value => ProductionOutputRoleRules.IsPhysical(value.role))
            .ToArray();
        if (ordered.Length == 0)
            throw new InvalidOperationException(
                "Prepared-output capacity claim has no physical line.");

        ValidatePreparedClaimAgainstAuthoredRecipe(recipe, ordered);

        List<ProductionOutputMaximumMassProjection> projections = new();
        long exactBatchMassGrams = 0L;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(ProductionPreparedOutputCapacityClaim.Schema);
        digest.Append(billId);
        digest.Append(cycleSequence);
        digest.Append(recipe.RecipeId);
        digest.Append(ProductionRecipeSemanticDigest.Capture(recipe));
        digest.Append(ordered.Length);
        foreach (ProductionPreparedOutputLineSaveData line in ordered)
        {
            if (line.quantity < 0
                || line.exactMassGrams < 0L)
            {
                throw new InvalidOperationException(
                    "Prepared-output capacity claim contains an invalid physical line.");
            }
            ProductionOutputCapabilityDescriptor descriptor = new(
                line.outputLineId,
                line.itemId,
                line.outputCapabilityId,
                line.outputCapabilityVersion,
                line.outputComponentCodecId,
                line.outputComponentCodecVersion,
                line.outputCapabilityFingerprint);
            digest.Append(descriptor.OutputLineId);
            digest.AppendEnum(line.role);
            digest.Append(descriptor.ItemId);
            digest.Append(descriptor.CapabilityId);
            digest.Append(descriptor.CapabilityVersion);
            digest.Append(descriptor.ComponentCodecId);
            digest.Append(descriptor.ComponentCodecVersion);
            digest.Append(descriptor.Fingerprint);
            digest.Append(line.quantity);
            digest.Append(line.componentFingerprint);
            digest.Append(line.exactMassGrams);
            if (line.quantity == 0)
            {
                if (line.exactMassGrams != 0L)
                {
                    throw new InvalidOperationException(
                        "A zero-quantity prepared line has physical mass.");
                }
                continue;
            }

            ProductionOutputMaximumMassProjection projection =
                captureDeclaredMaximumMass(descriptor, line.quantity);
            if (line.exactMassGrams <= 0L
                || line.exactMassGrams > projection.MaximumMassGrams)
            {
                throw new InvalidOperationException(
                    "Prepared output exceeds its capability maximum-mass projection.");
            }
            projections.Add(projection);
            exactBatchMassGrams = checked(
                exactBatchMassGrams + line.exactMassGrams);
            AppendProjection(digest, projection);
        }

        ProductionOutputBatchMaximumMassProof proof = new(projections);
        digest.Append(exactBatchMassGrams);
        digest.Append(proof.SourceDigest);
        digest.Append(proof.MaximumBatchMassGrams);
        return new ProductionPreparedOutputCapacityClaim(
            proof,
            exactBatchMassGrams,
            digest.ComputeSha256());
    }

    private void ValidatePreparedClaimAgainstAuthoredRecipe(
        ProductionRecipeSO recipe,
        IReadOnlyList<ProductionPreparedOutputLineSaveData> lines)
    {
        Dictionary<string, ProductionOutputDefinition> authored = recipe
            .CaptureCanonicalOutputs()
            .Where(value => value != null && value.Probability > 0f)
            .ToDictionary(
                value => value.OutputLineId,
                value => value,
                StringComparer.Ordinal);
        ProductionOutputFactor maximumFactor =
            maximumFactors.ResolveMaximum(recipe);
        foreach (ProductionPreparedOutputLineSaveData line in lines)
        {
            if (line.role is not (ProductionOutputRole.Main
                or ProductionOutputRole.Byproduct))
            {
                continue;
            }
            if (!authored.TryGetValue(
                    line.outputLineId,
                    out ProductionOutputDefinition definition)
                || definition.Role != line.role
                || !string.Equals(
                    definition.ItemId,
                    line.itemId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Prepared-output capacity claim adds a non-authored physical output line.");
            }

            int maximumQuantity = maximumFactor.CeilQuantity(
                definition.Amount);
            if (line.quantity > maximumQuantity)
            {
                throw new InvalidOperationException(
                    "Prepared-output capacity claim exceeds its authored output maximum.");
            }
        }
    }

    public ProductionPreparedOutputCapacityClaim CapturePreparedClaim(
        ProductionPreparedOutputBatchSaveData batch,
        ProductionRecipeSO recipe)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));
        return CapturePreparedClaim(
            batch.billId,
            batch.cycleSequence,
            recipe,
            batch.lines);
    }

    public ProductionPreparedOutputCapacityClaim CapturePreparedClaim(
        ProductionPreparedOutputBatchSaveData batch)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));
        if (!catalog.TryGetRecipe(
                batch.recipeId,
                out ProductionRecipeSO recipe))
        {
            throw new InvalidOperationException(
                "Prepared-output capacity claim recipe is missing: "
                + (batch.recipeId ?? string.Empty));
        }
        return CapturePreparedClaim(batch, recipe);
    }

    public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
        ProductionFacilityHandle facility,
        ProductionPreparedOutputCapacityClaim claim)
    {
        if (facility == null || facility.IsDestroyed || !facility.InstanceId.IsValid)
            throw new ArgumentException("A live production facility is required.", nameof(facility));
        if (claim == null)
            throw new ArgumentNullException(nameof(claim));
        ProductionFacilityCapacitySubject subject =
            ProductionFacilityCapacitySubject.FromLive(facility);
        ValidateLiveSubject(facility, subject);
        return CaptureSource(subject, claim);
    }

    public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
        ProductionFacilityCapacitySubject subject,
        ProductionPreparedOutputCapacityClaim claim)
    {
        if (claim == null)
            throw new ArgumentNullException(nameof(claim));
        ProductionOutputBufferCapacitySourceSnapshot proofSource =
            CaptureSource(subject, claim.MaximumMassProof);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(SourceDigestSchemaToken);
        digest.Append(ProductionPreparedOutputCapacityClaim.Schema);
        digest.Append(proofSource.SourceDigest);
        digest.Append(claim.SourceDigest);
        digest.Append(claim.MaximumMassProof.SourceDigest);
        digest.Append(claim.MaximumMassProof.MaximumBatchMassGrams);
        digest.Append(claim.ExactBatchMassGrams);
        digest.Append(proofSource.RequiredMinimumCapacityGrams);
        return new ProductionOutputBufferCapacitySourceSnapshot(
            proofSource.CycleCapacity,
            proofSource.MaximumBatchMassGrams,
            proofSource.ProjectedPortfolioCapacityGrams,
            proofSource.BatchMinimumCapacityGrams,
            proofSource.RequiredMinimumCapacityGrams,
            digest.ComputeSha256(),
            proofSource.ClearanceProfileDigest,
            proofSource.ClearanceGateDigest,
            proofSource.ClearanceAuthorityDigest);
    }

    public ProductionRuinedOutputCapacityClaim CaptureRuinedClaim(
        ProductionBillSaveData saved,
        ProductionRecipeSO recipe,
        ProductionOutputCapabilityDescriptor wasteCapability)
    {
        if (saved == null)
            throw new ArgumentNullException(nameof(saved));
        return CaptureRuinedClaim(
            saved.billId,
            saved.cycleSequence,
            saved.recipeId,
            saved.wipInputCommitId,
            saved.wipInputQuantity,
            saved.wipInputMassGrams,
            saved.processFluidConsumed,
            saved.processCleanWaterMassGrams,
            saved.processWastewaterMassGrams,
            saved.processWastewaterComponents,
            saved.processManualWaterTransfers,
            recipe,
            wasteCapability);
    }

    public ProductionRuinedOutputCapacityClaim CaptureRuinedClaim(
        ProductionBillSaveData saved,
        ProductionOutputCapabilityDescriptor wasteCapability)
    {
        if (saved == null)
            throw new ArgumentNullException(nameof(saved));
        if (!catalog.TryGetRecipe(
                saved.recipeId,
                out ProductionRecipeSO recipe))
        {
            throw new InvalidOperationException(
                "Ruined-output capacity claim recipe is missing: "
                + (saved.recipeId ?? string.Empty));
        }
        return CaptureRuinedClaim(saved, recipe, wasteCapability);
    }

    public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
        ProductionFacilityHandle facility,
        ProductionRuinedOutputCapacityClaim claim)
    {
        if (facility == null || facility.IsDestroyed || !facility.InstanceId.IsValid)
            throw new ArgumentException("A live production facility is required.", nameof(facility));
        if (claim == null)
            throw new ArgumentNullException(nameof(claim));
        ProductionFacilityCapacitySubject subject =
            ProductionFacilityCapacitySubject.FromLive(facility);
        ValidateLiveSubject(facility, subject);
        return CaptureSource(subject, claim);
    }

    public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
        ProductionFacilityCapacitySubject subject,
        ProductionRuinedOutputCapacityClaim claim)
    {
        if (claim == null)
            throw new ArgumentNullException(nameof(claim));
        ProductionOutputBufferCapacitySourceSnapshot portfolio =
            CapturePortfolioSource(subject);
        if (claim.MaximumMassProof.MaximumBatchMassGrams
            > portfolio.MaximumBatchMassGrams)
        {
            throw new InvalidOperationException(
                "ruined-wip-exceeds-authored-maximum");
        }
        return BindRuinedClaim(
            CaptureSource(subject, claim.MaximumMassProof),
            claim);
    }

    private static ProductionOutputBufferCapacitySourceSnapshot BindRuinedClaim(
        ProductionOutputBufferCapacitySourceSnapshot proofSource,
        ProductionRuinedOutputCapacityClaim claim)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(SourceDigestSchemaToken);
        digest.Append(ProductionRuinedOutputCapacityClaim.Schema);
        digest.Append(proofSource.SourceDigest);
        digest.Append(claim.SourceDigest);
        digest.Append(claim.MaximumMassProof.SourceDigest);
        digest.Append(claim.MaximumMassProof.MaximumBatchMassGrams);
        digest.Append(proofSource.RequiredMinimumCapacityGrams);
        return new ProductionOutputBufferCapacitySourceSnapshot(
            proofSource.CycleCapacity,
            proofSource.MaximumBatchMassGrams,
            proofSource.ProjectedPortfolioCapacityGrams,
            proofSource.BatchMinimumCapacityGrams,
            proofSource.RequiredMinimumCapacityGrams,
            digest.ComputeSha256(),
            proofSource.ClearanceProfileDigest,
            proofSource.ClearanceGateDigest,
            proofSource.ClearanceAuthorityDigest);
    }

    private ProductionOutputBufferCapacitySourceSnapshot CaptureSourceCore(
        ProductionFacilityCapacitySubject subject,
        long exactBatchMassGrams)
    {
        if (!subject.FacilityId.IsValid
            || string.IsNullOrEmpty(subject.DefinitionId)
            || string.IsNullOrEmpty(subject.WorkstationTag)
            || subject.OutputBufferCycleCapacity is < 2 or > 4
            || subject.ProcessFluidProfile == null
            || exactBatchMassGrams < 0L)
        {
            throw new InvalidOperationException(
                "Production facility capacity semantic identity is incomplete.");
        }

        int cycleCapacity = subject.OutputBufferCycleCapacity;

        ProductionRecipeSO[] matchingRecipes = (catalog.Recipes
                ?? Array.Empty<ProductionRecipeSO>())
            .Where(value => value != null
                && string.Equals(
                    subject.WorkstationTag,
                    value.WorkstationTag,
                    StringComparison.Ordinal))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();

        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(SourceDigestSchemaToken);
        canonical.Append(ProductionOutputDestinationAuthorityRuntime
            .CapacitySchemaRevision);
        canonical.Append(massQuery.AuthorityRevision);
        canonical.Append(subject.DefinitionId);
        canonical.Append(subject.FacilityId.Value);
        canonical.Append(subject.Position.x);
        canonical.Append(subject.Position.y);
        canonical.Append(subject.WorkstationTag);
        canonical.Append(subject.ProcessFluidProfile.SourceDigest);
        canonical.Append(ProductionBillRuntime.OutputDestinationPrefix
            + subject.FacilityId.Value);
        canonical.Append(ProductionOutputDestinationAuthorityRuntime.OwnerDomain);
        canonical.Append(ProductionBillRuntime.OutputDestinationPrefix
            + subject.FacilityId.Value);
        canonical.Append(subject.FacilityId.Value);
        canonical.Append(cycleCapacity);
        canonical.Append(matchingRecipes.Length);

        long maximumBatchMassGrams = 0L;
        foreach (ProductionRecipeSO recipe in matchingRecipes)
        {
            long branchMassGrams = ResolveMaximumBranchMassGrams(
                recipe,
                subject.ProcessFluidProfile,
                canonical);
            maximumBatchMassGrams = Math.Max(
                maximumBatchMassGrams,
                branchMassGrams);
        }

        long recipeMaximumBatchMassGrams = maximumBatchMassGrams;
        ProductionFacilityOutputCapacityAggregateSnapshot contribution =
            captureFacilityContributions(subject);
        maximumBatchMassGrams = Math.Max(
            recipeMaximumBatchMassGrams,
            contribution.MaximumBatchMassGrams);
        canonical.Append(recipeMaximumBatchMassGrams);
        canonical.Append(contribution.SourceDigest);
        canonical.Append(contribution.ApplicableContributorCount);
        canonical.Append(contribution.BranchCount);
        canonical.Append(contribution.MaximumBatchMassGrams);
        canonical.Append(contribution.WinningContributorId);
        canonical.Append(contribution.WinningBranchId);

        if (maximumBatchMassGrams <= 0L)
        {
            canonical.Append(0L);
            canonical.Append(0L);
            canonical.Append(0L);
            canonical.Append(0L);
            return new ProductionOutputBufferCapacitySourceSnapshot(
                cycleCapacity,
                0L,
                0L,
                0L,
                0L,
                canonical.ComputeSha256());
        }
        ProductionOutputClearanceProfileSnapshot clearanceProfile =
            clearanceProfiles.Capture(subject);
        ProductionOutputClearanceCapacityGateAssessment clearanceGate =
            ProductionOutputClearanceCapacityGate.Assess(
                subject,
                maximumBatchMassGrams,
                clearanceProfile);
        if (!clearanceGate.CanPublishBoundedCapacity)
        {
            throw new InvalidOperationException(
                clearanceGate.FailureCode + ":" + subject.DefinitionId + ":"
                + subject.WorkstationTag);
        }
        long projectedPortfolioCapacity = clearanceGate.AuthoredCapacityGrams;
        long batchMinimumCapacity = exactBatchMassGrams == 0L
            ? 0L
            : checked(exactBatchMassGrams * cycleCapacity);
        long requiredMinimumCapacity = Math.Max(
            projectedPortfolioCapacity,
            batchMinimumCapacity);
        canonical.Append(maximumBatchMassGrams);
        canonical.Append(projectedPortfolioCapacity);
        canonical.Append(exactBatchMassGrams);
        canonical.Append(batchMinimumCapacity);
        canonical.Append(requiredMinimumCapacity);
        canonical.Append(clearanceProfiles.AuthorityDigest);
        canonical.Append(clearanceProfile.SourceDigest);
        canonical.Append(clearanceGate.SourceDigest);
        canonical.Append(clearanceGate.Requirement.SourceDigest);
        canonical.Append(clearanceGate.Requirement.RequiredCapacityGrams);
        canonical.Append(clearanceGate.Requirement.RequiredWholeCycles);
        return new ProductionOutputBufferCapacitySourceSnapshot(
            cycleCapacity,
            maximumBatchMassGrams,
            projectedPortfolioCapacity,
            batchMinimumCapacity,
            requiredMinimumCapacity,
            canonical.ComputeSha256(),
            clearanceProfile.SourceDigest,
            clearanceGate.SourceDigest,
            clearanceProfiles.AuthorityDigest);
    }

    private long ResolveMaximumBranchMassGrams(
        ProductionRecipeSO recipe,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile,
        CanonicalSemanticDigestBuilder canonical)
    {
        ProductionPreparedOutputMigrationScope.ValidateCanonicalProfileOrThrow(recipe);
        canonical.Append(recipe.RecipeId);
        canonical.Append(ProductionRecipeSemanticDigest.Capture(recipe));
        canonical.Append(ProductionPreparedOutputMigrationScope
            .CaptureProfileDigest(recipe));
        canonical.Append(maximumFactors.CaptureRecipeSourceDigest(recipe));

        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch)
        {
            IReadOnlyList<ProductionAuthoredSupportAssignmentSnapshot>
                assignments = maximumFactors.CaptureFeasibleAssignments(recipe);
            if (assignments.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Passive recipe '{recipe.RecipeId}' has no feasible exact support assignment.");
            }

            canonical.Append(assignments.Count);
            long maximumPassiveMassGrams = 0L;
            foreach (ProductionAuthoredSupportAssignmentSnapshot assignment in
                     assignments.OrderBy(
                         value => value.SourceDigest,
                         StringComparer.Ordinal))
            {
                ProductionPassiveBatchOutputPortfolioSnapshot portfolio =
                    passiveBatchOutputs.Capture(
                        recipe,
                        processFluidProfile,
                        assignment);
                canonical.Append(assignment.SourceDigest);
                canonical.Append(portfolio.OutcomeRuleDigest);
                canonical.Append(portfolio.Normal.SourceDigest);
                canonical.Append(portfolio.Normal.TotalPhysicalMassGrams);
                canonical.Append(portfolio.Ruined.SourceDigest);
                canonical.Append(portfolio.Ruined.TotalPhysicalMassGrams);
                canonical.Append(portfolio.Ruined.DeclaredLossMassGrams);
                canonical.Append(portfolio.MaximumBufferMassGrams);
                canonical.Append(portfolio.SourceDigest);
                maximumPassiveMassGrams = Math.Max(
                    maximumPassiveMassGrams,
                    portfolio.MaximumBufferMassGrams);
            }
            if (maximumPassiveMassGrams <= 0L)
            {
                throw new InvalidOperationException(
                    $"Passive recipe '{recipe.RecipeId}' has no physical output mass.");
            }
            canonical.Append(maximumPassiveMassGrams);
            return maximumPassiveMassGrams;
        }
        if (recipe.ProcessKind != ProductionProcessKind.WorkOnly)
        {
            throw new InvalidOperationException(
                $"Reachable recipe '{recipe.RecipeId}' has unsupported process kind.");
        }

        ProductionOutputFactor multiplier = maximumFactors.ResolveMaximum(recipe);
        canonical.Append(multiplier.Numerator);
        canonical.Append(multiplier.Denominator);

        long normalBranchMassGrams = 0L;
        ProductionOutputDefinition[] physicalOutputs = recipe
            .CaptureCanonicalOutputs()
            .Where(value => value.Probability > 0f
                && ProductionOutputRoleRules.IsPhysical(value.Role))
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        canonical.Append(physicalOutputs.Length);
        foreach (ProductionOutputDefinition output in physicalOutputs)
        {
            int maximumQuantity = multiplier.CeilQuantity(output.Amount);
            ProductionOutputMaximumMassProjection projection =
                captureMaximumMass(
                    output.OutputLineId,
                    output.ItemId,
                    maximumQuantity);
            long outputMass = projection.MaximumMassGrams;
            canonical.Append(output.OutputLineId);
            canonical.Append(output.ItemId);
            canonical.Append(maximumQuantity);
            AppendProjection(canonical, projection);
            canonical.Append(outputMass);
            normalBranchMassGrams = checked(
                normalBranchMassGrams + outputMass);
        }

        canonical.Append(normalBranchMassGrams);
        if (normalBranchMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                $"Reachable recipe '{recipe.RecipeId}' has no physical output mass.");
        }
        return normalBranchMassGrams;
    }

    private ProductionRuinedOutputCapacityClaim CaptureRuinedClaim(
        string billId,
        int cycleSequence,
        string recordRecipeId,
        string wipInputCommitId,
        int wipInputQuantity,
        long wipInputMassGrams,
        bool processFluidConsumed,
        long processCleanWaterMassGrams,
        long processWastewaterMassGrams,
        IReadOnlyList<ProductionWastewaterComponentSaveData>
            wastewaterComponents,
        IReadOnlyList<ProductionManualWaterTransferSaveData>
            manualWaterTransfers,
        ProductionRecipeSO recipe,
        ProductionOutputCapabilityDescriptor wasteCapability)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        if (string.IsNullOrEmpty(billId)
            || !string.Equals(billId, billId.Trim(), StringComparison.Ordinal)
            || cycleSequence < 1
            || !string.Equals(recordRecipeId, recipe.RecipeId, StringComparison.Ordinal)
            || string.IsNullOrEmpty(wipInputCommitId)
            || !string.Equals(
                wipInputCommitId,
                wipInputCommitId.Trim(),
                StringComparison.Ordinal)
            || wipInputQuantity <= 0
            || wipInputMassGrams <= 0L
            || processCleanWaterMassGrams < 0L
            || processWastewaterMassGrams < 0L
            || wastewaterComponents == null
            || manualWaterTransfers == null
            || !string.Equals(
                wasteCapability.OutputLineId,
                ProductionRuinedBatchDispositionPlan
                    .RecoverableWasteOutputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                wasteCapability.ItemId,
                recipe.SpoilageItemId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Ruined-output capacity claim source is incomplete or non-canonical.");
        }

        ProductionOutputMaximumMassProjection unitProjection =
            captureDeclaredMaximumMass(wasteCapability, 1);
        ProductionRuinedBatchDispositionPlan disposition =
            ProductionRuinedBatchDispositionPlan.Create(
                wipInputMassGrams,
                processCleanWaterMassGrams,
                processWastewaterMassGrams,
                recipe.SpoilageItemId,
                unitProjection.DefinitionUnitMassGrams);
        ProductionOutputMaximumMassProjection maximumProjection =
            captureDeclaredMaximumMass(
                wasteCapability,
                disposition.RecoverableWasteQuantity);
        if (maximumProjection.DefinitionUnitMassGrams
                != disposition.SpoilageUnitMassGrams
            || maximumProjection.MaximumMassGrams
                != disposition.RecoverableWasteMassGrams)
        {
            throw new InvalidOperationException(
                "Ruined-output disposition drifted from its capability maximum-mass projection.");
        }
        ProductionOutputBatchMaximumMassProof maximumMassProof = new(
            new[] { maximumProjection });

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(ProductionRuinedOutputCapacityClaim.Schema);
        digest.Append(billId);
        digest.Append(cycleSequence);
        digest.Append(recordRecipeId);
        digest.Append(ProductionRecipeSemanticDigest.Capture(recipe));
        digest.Append(wipInputCommitId);
        digest.Append(wipInputQuantity);
        digest.Append(wipInputMassGrams);
        digest.Append(processFluidConsumed);
        digest.Append(processCleanWaterMassGrams);
        digest.Append(processWastewaterMassGrams);

        ProductionWastewaterComponentSaveData[] orderedWastewater =
            wastewaterComponents
                .OrderBy(value => (int)(value?.composition
                    ?? ProcessWastewaterComposition.None))
                .ThenBy(value => (int)(value?.sourceKind
                    ?? ProcessWastewaterSourceKind.Recipe))
                .ThenBy(value => value?.sourceStableId ?? string.Empty,
                    StringComparer.Ordinal)
                .ToArray();
        digest.Append(orderedWastewater.Length);
        foreach (ProductionWastewaterComponentSaveData component in
                 orderedWastewater)
        {
            if (component == null)
                throw new InvalidOperationException(
                    "Ruined-output wastewater provenance contains a null component.");
            digest.AppendEnum(component.composition);
            digest.AppendEnum(component.sourceKind);
            digest.Append(component.sourceStableId);
            digest.AppendFloat(component.authoredUnits);
            digest.Append(component.massGrams);
        }

        ProductionManualWaterTransferSaveData[] orderedTransfers =
            manualWaterTransfers
                .OrderBy(value => value?.operationId ?? string.Empty,
                    StringComparer.Ordinal)
                .ToArray();
        digest.Append(orderedTransfers.Length);
        foreach (ProductionManualWaterTransferSaveData transfer in
                 orderedTransfers)
        {
            if (transfer == null)
                throw new InvalidOperationException(
                    "Ruined-output manual-water provenance contains a null transfer.");
            digest.Append(transfer.operationId);
            digest.Append(transfer.physicalCommitId);
            digest.Append(transfer.destinationId);
            digest.AppendFloat(transfer.requestedWaterUnits);
            digest.Append(transfer.transferredWaterUnits);
            digest.Append(transfer.inputMassGrams);
            string[] sourceStackIds = (transfer.sourceStackIds
                    ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            digest.Append(sourceStackIds.Length);
            foreach (string sourceStackId in sourceStackIds)
                digest.Append(sourceStackId);
        }

        digest.Append(wasteCapability.OutputLineId);
        digest.Append(wasteCapability.ItemId);
        digest.Append(wasteCapability.CapabilityId);
        digest.Append(wasteCapability.CapabilityVersion);
        digest.Append(wasteCapability.ComponentCodecId);
        digest.Append(wasteCapability.ComponentCodecVersion);
        digest.Append(wasteCapability.Fingerprint);
        digest.Append(disposition.RecoverableWasteQuantity);
        digest.Append(disposition.RecoverableWasteMassGrams);
        digest.Append(disposition.ProcessWastewaterMassGrams);
        digest.Append(disposition.DeclaredLossMassGrams);
        digest.Append(maximumMassProof.SourceDigest);
        digest.Append(maximumMassProof.MaximumBatchMassGrams);
        return new ProductionRuinedOutputCapacityClaim(
            disposition,
            maximumMassProof,
            digest.ComputeSha256());
    }

    private Func<string, string, int, ProductionOutputMaximumMassProjection>
        CreateDefinitionOnlyCapture()
    {
        StandardDefinitionProductionOutputCapability capability = new(
            catalog,
            componentCodec);
        return (outputLineId, itemId, maximumQuantity) =>
        {
            if (maximumQuantity <= 0 || !capability.CanHandle(itemId))
            {
                throw new InvalidOperationException(
                    $"Physical output item '{itemId}' has no definition-only maximum projection.");
            }
            ProductionOutputCapabilityDescriptor descriptor = new(
                outputLineId,
                itemId,
                capability.CapabilityId,
                capability.ContractVersion,
                capability.ComponentCodecId,
                capability.ComponentCodecVersion,
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    outputLineId,
                    itemId,
                    capability.CapabilityId,
                    capability.ContractVersion,
                    capability.ComponentCodecId,
                    capability.ComponentCodecVersion));
            return capability.CaptureDefinitionMaximum(
                descriptor,
                maximumQuantity,
                massQuery);
        };
    }

    private static void AppendProjection(
        CanonicalSemanticDigestBuilder canonical,
        ProductionOutputMaximumMassProjection projection)
    {
        ProductionOutputCapabilityDescriptor descriptor = projection.Descriptor;
        canonical.Append(descriptor.CapabilityId);
        canonical.Append(descriptor.CapabilityVersion);
        canonical.Append(descriptor.ComponentCodecId);
        canonical.Append(descriptor.ComponentCodecVersion);
        canonical.Append(descriptor.Fingerprint);
        canonical.Append(projection.MaximumQuantity);
        canonical.Append(projection.DefinitionUnitMassGrams);
        canonical.Append(projection.MaximumMassGrams);
        canonical.Append(projection.MassAuthorityRevision);
        canonical.Append(projection.SourceDigest);
    }

    private static void ValidateCapabilityParity(
        IReadOnlyList<ProductionOutputCapabilityContractSnapshot> execution,
        IReadOnlyList<ProductionOutputCapabilityContractSnapshot> projection)
    {
        if (execution == null
            || projection == null
            || execution.Count == 0
            || execution.Count != projection.Count)
        {
            throw new InvalidOperationException(
                "Production output execution/maximum-mass capability sets are incomplete.");
        }
        for (int index = 0; index < execution.Count; index++)
        {
            if (!execution[index].Equals(projection[index]))
            {
                throw new InvalidOperationException(
                    "Production output execution/maximum-mass capability contract drifted at index "
                    + index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ".");
            }
        }
    }
}
