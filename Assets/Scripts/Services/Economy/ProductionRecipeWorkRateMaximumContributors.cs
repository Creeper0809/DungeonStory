using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Publishes the definition-only work-stat multiplier used by live work
/// execution into the execution-free production throughput authority.
/// </summary>
public sealed class ProductionWorkStatPolicyMaximumContributor :
    IProductionRecipeWorkRateMaximumContributor
{
    public const string Schema =
        "production-work-stat-policy-maximum-contributor@1";
    public const string StableContributorId =
        "work-rate:stat-policy-definition";

    private readonly IWorkStatPolicyDefinitionMaximumQuery maximums;

    public ProductionWorkStatPolicyMaximumContributor(
        IWorkStatPolicyDefinitionMaximumQuery maximums)
    {
        this.maximums = maximums
            ?? throw new ArgumentNullException(nameof(maximums));
    }

    public string ContributorId => StableContributorId;

    public ProductionWorkRateMaximumContributorResult Capture(
        ProductionWorkRateMaximumSubject context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        WorkTypeId workTypeId = context.WorkTypeId;
        WorkStatPolicyDefinitionMaximumSnapshot snapshot;
        try
        {
            snapshot = maximums.CaptureDefinitionMaximum(workTypeId);
        }
        catch (InvalidOperationException exception)
        {
            return Missing(
                context,
                workTypeId,
                exception.GetType().FullName,
                exception.Message);
        }

        if (snapshot.WorkTypeId != workTypeId)
        {
            return Missing(
                context,
                workTypeId,
                "WORK_TYPE_MISMATCH",
                snapshot.WorkTypeId.Value);
        }
        if (!ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                snapshot.MaximumMultiplier,
                out ProductionWorkRateFixedPointUpperBound upperBound,
                out ProductionRecipeWorkRateMaximumGapReason failureReason))
        {
            return Missing(
                context,
                workTypeId,
                failureReason.ToString(),
                snapshot.SourceDigest);
        }

        CanonicalSemanticDigestBuilder digest = BeginDigest(
            context,
            workTypeId);
        digest.AppendDouble(snapshot.MaximumMultiplier);
        digest.Append(upperBound.ScaledValue);
        digest.Append(snapshot.SourceDigest);
        return ProductionWorkRateMaximumContributorResult.Complete(
            upperBound,
            digest.ComputeSha256());
    }

    private static ProductionWorkRateMaximumContributorResult Missing(
        ProductionWorkRateMaximumSubject context,
        WorkTypeId workTypeId,
        string code,
        string detail)
    {
        CanonicalSemanticDigestBuilder digest = BeginDigest(
            context,
            workTypeId);
        digest.Append("gap");
        digest.Append(code ?? string.Empty);
        digest.Append(detail ?? string.Empty);
        return ProductionWorkRateMaximumContributorResult.Missing(
            ProductionRecipeWorkRateMaximumGapReason.ContributorRejected,
            (code ?? string.Empty) + ":" + (detail ?? string.Empty),
            digest.ComputeSha256());
    }

    private static CanonicalSemanticDigestBuilder BeginDigest(
        ProductionWorkRateMaximumSubject context,
        WorkTypeId workTypeId)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(StableContributorId);
        digest.Append(context.FacilityDefinitionId);
        digest.Append(context.WorkstationTag);
        digest.Append(context.OperationDefinitionId);
        digest.Append(context.OperationSourceDigest);
        digest.Append(workTypeId.Value);
        return digest;
    }
}

/// <summary>
/// Publishes the authored Speed formula maximum used by
/// ICharacterPerformanceQuery.EvaluateWork. Actor context multipliers are a
/// separate contributor and are intentionally not inferred here.
/// </summary>
public sealed class ProductionCharacterPerformanceMaximumContributor :
    IProductionRecipeWorkRateMaximumContributor
{
    public const string Schema =
        "production-character-performance-maximum-contributor@1";
    public const string StableContributorId =
        "work-rate:character-performance-definition";

    private readonly CharacterPerformanceFormulaCatalog formulas;
    private readonly ICharacterPerformanceDefinitionMaximumQuery maximums;

    public ProductionCharacterPerformanceMaximumContributor(
        CharacterPerformanceFormulaCatalog formulas,
        ICharacterPerformanceDefinitionMaximumQuery maximums)
    {
        this.formulas = formulas ?? throw new ArgumentNullException(nameof(formulas));
        this.maximums = maximums
            ?? throw new ArgumentNullException(nameof(maximums));
    }

    public string ContributorId => StableContributorId;

    public ProductionWorkRateMaximumContributorResult Capture(
        ProductionWorkRateMaximumSubject context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        WorkTypeId workTypeId = context.WorkTypeId;
        CharacterPerformanceFormulaDefinitionSO formula;
        CharacterPerformanceDefinitionMaximumSnapshot snapshot;
        try
        {
            formula = formulas.RequireWork(
                workTypeId,
                CharacterPerformanceResultChannel.Speed);
            snapshot = maximums.Capture(formula.FormulaId);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            || exception is System.Collections.Generic.KeyNotFoundException)
        {
            return Missing(
                context,
                workTypeId,
                exception.GetType().FullName,
                exception.Message);
        }

        if (!string.Equals(
                snapshot.FormulaId,
                formula.FormulaId,
                StringComparison.Ordinal))
        {
            return Missing(
                context,
                workTypeId,
                "FORMULA_ID_MISMATCH",
                snapshot.FormulaId);
        }
        if (!ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                snapshot.MaximumValue,
                out ProductionWorkRateFixedPointUpperBound upperBound,
                out ProductionRecipeWorkRateMaximumGapReason failureReason))
        {
            return Missing(
                context,
                workTypeId,
                failureReason.ToString(),
                snapshot.SourceDigest);
        }

        CanonicalSemanticDigestBuilder digest = BeginDigest(
            context,
            workTypeId);
        digest.Append(formula.FormulaId);
        digest.AppendDouble(snapshot.MaximumValue);
        digest.AppendDouble(snapshot.FunctionalCapacityMaximum);
        digest.AppendDouble(snapshot.ProficiencyMaximum);
        digest.AppendDouble(snapshot.GameplayEffectMaximum);
        digest.Append(upperBound.ScaledValue);
        digest.Append(snapshot.SourceDigest);
        return ProductionWorkRateMaximumContributorResult.Complete(
            upperBound,
            digest.ComputeSha256());
    }

    private static ProductionWorkRateMaximumContributorResult Missing(
        ProductionWorkRateMaximumSubject context,
        WorkTypeId workTypeId,
        string code,
        string detail)
    {
        CanonicalSemanticDigestBuilder digest = BeginDigest(
            context,
            workTypeId);
        digest.Append("gap");
        digest.Append(code ?? string.Empty);
        digest.Append(detail ?? string.Empty);
        return ProductionWorkRateMaximumContributorResult.Missing(
            ProductionRecipeWorkRateMaximumGapReason.ContributorRejected,
            (code ?? string.Empty) + ":" + (detail ?? string.Empty),
            digest.ComputeSha256());
    }

    private static CanonicalSemanticDigestBuilder BeginDigest(
        ProductionWorkRateMaximumSubject context,
        WorkTypeId workTypeId)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(StableContributorId);
        digest.Append(context.FacilityDefinitionId);
        digest.Append(context.WorkstationTag);
        digest.Append(context.OperationDefinitionId);
        digest.Append(context.OperationSourceDigest);
        digest.Append(workTypeId.Value);
        return digest;
    }
}

/// <summary>
/// Publishes the same room/environment speed term that live work obtains by
/// inverting IRoomEnvironmentQuery.GetWorkDurationMultiplier.
/// </summary>
public sealed class ProductionWorkEnvironmentMaximumContributor :
    IProductionRecipeWorkRateMaximumContributor
{
    public const string Schema =
        "production-work-environment-maximum-contributor@1";
    public const string StableContributorId =
        "work-rate:room-environment-definition";

    private readonly IWorkEnvironmentDefinitionMaximumQuery maximums;

    public ProductionWorkEnvironmentMaximumContributor(
        IWorkEnvironmentDefinitionMaximumQuery maximums)
    {
        this.maximums = maximums
            ?? throw new ArgumentNullException(nameof(maximums));
    }

    public string ContributorId => StableContributorId;

    public ProductionWorkRateMaximumContributorResult Capture(
        ProductionWorkRateMaximumSubject context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        WorkTypeId workTypeId = context.WorkTypeId;
        WorkEnvironmentDefinitionMaximumSnapshot snapshot;
        try
        {
            snapshot = maximums.CaptureDefinitionMaximum(workTypeId);
        }
        catch (InvalidOperationException exception)
        {
            return Missing(
                context,
                workTypeId,
                exception.GetType().FullName,
                exception.Message);
        }

        if (snapshot.WorkTypeId != workTypeId)
        {
            return Missing(
                context,
                workTypeId,
                "WORK_TYPE_MISMATCH",
                snapshot.WorkTypeId.Value);
        }
        if (!ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                snapshot.MaximumSpeedMultiplier,
                out ProductionWorkRateFixedPointUpperBound upperBound,
                out ProductionRecipeWorkRateMaximumGapReason failureReason))
        {
            return Missing(
                context,
                workTypeId,
                failureReason.ToString(),
                snapshot.SourceDigest);
        }

        CanonicalSemanticDigestBuilder digest = BeginDigest(
            context,
            workTypeId);
        digest.AppendDouble(snapshot.MaximumSpeedMultiplier);
        digest.Append(upperBound.ScaledValue);
        digest.Append(snapshot.SourceDigest);
        return ProductionWorkRateMaximumContributorResult.Complete(
            upperBound,
            digest.ComputeSha256());
    }

    private static ProductionWorkRateMaximumContributorResult Missing(
        ProductionWorkRateMaximumSubject context,
        WorkTypeId workTypeId,
        string code,
        string detail)
    {
        CanonicalSemanticDigestBuilder digest = BeginDigest(
            context,
            workTypeId);
        digest.Append("gap");
        digest.Append(code ?? string.Empty);
        digest.Append(detail ?? string.Empty);
        return ProductionWorkRateMaximumContributorResult.Missing(
            ProductionRecipeWorkRateMaximumGapReason.ContributorRejected,
            (code ?? string.Empty) + ":" + (detail ?? string.Empty),
            digest.ComputeSha256());
    }

    private static CanonicalSemanticDigestBuilder BeginDigest(
        ProductionWorkRateMaximumSubject context,
        WorkTypeId workTypeId)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(StableContributorId);
        digest.Append(context.FacilityDefinitionId);
        digest.Append(context.WorkstationTag);
        digest.Append(context.OperationDefinitionId);
        digest.Append(context.OperationSourceDigest);
        digest.Append(workTypeId.Value);
        return digest;
    }
}

public sealed class ProductionCraftsmanshipMaximumContributor :
    IProductionRecipeWorkRateMaximumContributor
{
    public const string Schema =
        "production-craftsmanship-maximum-contributor@1";
    public const string StableContributorId =
        "work-rate:building-craftsmanship-definition";

    private readonly IBuildingCraftsmanshipDefinitionMaximumQuery maximums;

    public ProductionCraftsmanshipMaximumContributor(
        IBuildingCraftsmanshipDefinitionMaximumQuery maximums)
    {
        this.maximums = maximums
            ?? throw new ArgumentNullException(nameof(maximums));
    }

    public string ContributorId => StableContributorId;

    public ProductionWorkRateMaximumContributorResult Capture(
        ProductionWorkRateMaximumSubject context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        BuildingCraftsmanshipDefinitionMaximumSnapshot snapshot;
        try
        {
            snapshot = maximums.Capture(context.FacilityDefinitionId);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is InvalidOperationException)
        {
            return Missing(context, exception.GetType().FullName, exception.Message);
        }

        if (!string.Equals(
                snapshot.FacilityDefinitionId,
                context.FacilityDefinitionId,
                StringComparison.Ordinal))
        {
            return Missing(
                context,
                "FACILITY_ID_MISMATCH",
                snapshot.FacilityDefinitionId);
        }
        if (!ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                snapshot.MaximumMultiplier,
                out ProductionWorkRateFixedPointUpperBound upperBound,
                out ProductionRecipeWorkRateMaximumGapReason failureReason))
        {
            return Missing(
                context,
                failureReason.ToString(),
                snapshot.SourceDigest);
        }

        CanonicalSemanticDigestBuilder digest = BeginDigest(context);
        digest.AppendEnum(snapshot.MaximumTier);
        digest.AppendDouble(snapshot.MaximumMultiplier);
        digest.Append(upperBound.ScaledValue);
        digest.Append(snapshot.SourceDigest);
        return ProductionWorkRateMaximumContributorResult.Complete(
            upperBound,
            digest.ComputeSha256());
    }

    private static ProductionWorkRateMaximumContributorResult Missing(
        ProductionWorkRateMaximumSubject context,
        string code,
        string detail)
    {
        CanonicalSemanticDigestBuilder digest = BeginDigest(context);
        digest.Append("gap");
        digest.Append(code ?? string.Empty);
        digest.Append(detail ?? string.Empty);
        return ProductionWorkRateMaximumContributorResult.Missing(
            ProductionRecipeWorkRateMaximumGapReason.ContributorRejected,
            (code ?? string.Empty) + ":" + (detail ?? string.Empty),
            digest.ComputeSha256());
    }

    private static CanonicalSemanticDigestBuilder BeginDigest(
        ProductionWorkRateMaximumSubject context)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(StableContributorId);
        digest.Append(context.FacilityDefinitionId);
        digest.Append(context.WorkstationTag);
        digest.Append(context.OperationDefinitionId);
        digest.Append(context.OperationSourceDigest);
        return digest;
    }
}

public sealed class ProductionFacilityDefinitionCatalog
{
    public const string Schema = "production-facility-definition-catalog@2";

    private readonly IReadOnlyDictionary<string, BuildingSO> definitions;

    public ProductionFacilityDefinitionCatalog(
        IGameContentDefinitionSource content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        List<KeyValuePair<string, BuildingSO>> identifiedDefinitions = new();
        int ignoredRuntimeArchetypeCount = 0;
        foreach (BuildingSO definition in content.GetAll<BuildingSO>()
                     .Where(value => value != null))
        {
            bool hasNumericAuthority = definition.id >= 0;
            if (!hasNumericAuthority)
            {
                if (definition.GetProductionWorkstationAbility() != null)
                {
                    throw new InvalidOperationException(
                        "An authored production workstation has neither a "
                        + "definition ID nor numeric authority: "
                        + definition.name);
                }

                // Runtime-only world archetypes deliberately use a negative
                // numeric ID. Their stable building:runtime:* identity exists
                // for persistence and diagnostics; it does not promote them
                // into authored facility definitions or maximum-work inputs.
                ignoredRuntimeArchetypeCount++;
                continue;
            }

            identifiedDefinitions.Add(new KeyValuePair<string, BuildingSO>(
                ProductionFacilityDefinitionIdentity.Resolve(definition),
                definition));
        }

        KeyValuePair<string, BuildingSO>[] orderedDefinitions =
            identifiedDefinitions
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .ToArray();
        Dictionary<string, BuildingSO> byId = new(StringComparer.Ordinal);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(orderedDefinitions.Length);
        digest.Append(ignoredRuntimeArchetypeCount);
        foreach (KeyValuePair<string, BuildingSO> pair in orderedDefinitions)
        {
            if (!byId.TryAdd(pair.Key, pair.Value))
                throw new InvalidOperationException(
                    "Duplicate production facility definition ID: "
                    + pair.Key);
            digest.Append(pair.Key);
            digest.Append(pair.Value.AuthoringRevision);
        }
        definitions = byId;
        DefinitionCount = orderedDefinitions.Length;
        IgnoredRuntimeArchetypeCount = ignoredRuntimeArchetypeCount;
        SourceDigest = digest.ComputeSha256();
    }

    public string SourceDigest { get; }
    public int DefinitionCount { get; }
    public int IgnoredRuntimeArchetypeCount { get; }

    public BuildingSO Require(string facilityDefinitionId) =>
        definitions.TryGetValue(
            facilityDefinitionId ?? string.Empty,
            out BuildingSO definition)
            ? definition
            : throw new KeyNotFoundException(
                "Production facility definition is not authored: "
                + facilityDefinitionId);
}

public readonly struct AutomationAssistedWorkDefinitionMaximumSnapshot
{
    public AutomationAssistedWorkDefinitionMaximumSnapshot(
        string facilityDefinitionId,
        AutomationMode maximumMode,
        double maximumMultiplier,
        string sourceDigest)
    {
        if (string.IsNullOrWhiteSpace(facilityDefinitionId)
            || !string.Equals(
                facilityDefinitionId,
                facilityDefinitionId.Trim(),
                StringComparison.Ordinal)
            || !Enum.IsDefined(typeof(AutomationMode), maximumMode)
            || double.IsNaN(maximumMultiplier)
            || double.IsInfinity(maximumMultiplier)
            || maximumMultiplier <= 0d
            || sourceDigest == null
            || sourceDigest.Length != 64)
        {
            throw new ArgumentException(
                "Automation assisted-work definition maximum is invalid.");
        }
        FacilityDefinitionId = facilityDefinitionId;
        MaximumMode = maximumMode;
        MaximumMultiplier = maximumMultiplier;
        SourceDigest = sourceDigest;
    }

    public string FacilityDefinitionId { get; }
    public AutomationMode MaximumMode { get; }
    public double MaximumMultiplier { get; }
    public string SourceDigest { get; }
}

public interface IAutomationAssistedWorkDefinitionMaximumQuery
{
    AutomationAssistedWorkDefinitionMaximumSnapshot Capture(
        string facilityDefinitionId);
}

public sealed class ProductionAutomationAssistedWorkDefinitionMaximumQuery :
    IAutomationAssistedWorkDefinitionMaximumQuery
{
    public const string Schema =
        "production-automation-assisted-work-definition-maximum@1";

    private readonly ProductionFacilityDefinitionCatalog definitions;

    public ProductionAutomationAssistedWorkDefinitionMaximumQuery(
        ProductionFacilityDefinitionCatalog definitions)
    {
        this.definitions = definitions
            ?? throw new ArgumentNullException(nameof(definitions));
    }

    public AutomationAssistedWorkDefinitionMaximumSnapshot Capture(
        string facilityDefinitionId)
    {
        BuildingSO definition = definitions.Require(facilityDefinitionId);

        BuildingAutomationAbility ability = definition
            .GetAbility<BuildingAutomationAbility>();
        AutomationMode maximumMode = ability?.maximumMode
            ?? AutomationMode.Manual;
        if (!Enum.IsDefined(typeof(AutomationMode), maximumMode))
            throw new InvalidOperationException(
                "Automation maximum mode is invalid for facility: "
                + facilityDefinitionId);

        bool supportsAssist = ability != null
            && maximumMode >= AutomationMode.PoweredAssist;
        double authoredMultiplier = ability?.assistedWorkMultiplier ?? 1f;
        if (double.IsNaN(authoredMultiplier)
            || double.IsInfinity(authoredMultiplier))
        {
            throw new InvalidOperationException(
                "Automation assisted-work multiplier is not finite for facility: "
                + facilityDefinitionId);
        }
        double assistedMaximum = supportsAssist
            ? AutomationWorkRateAuthority.ResolveAssistedWorkMultiplier(
                ability.assistedWorkMultiplier)
            : 1d;
        double maximum = Math.Max(1d, assistedMaximum
            * AutomationWorkRateAuthority.MaximumConditionMultiplier);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(definitions.SourceDigest);
        digest.Append(facilityDefinitionId);
        digest.Append(ability != null);
        digest.AppendEnum(maximumMode);
        digest.Append(supportsAssist);
        digest.AppendDouble(authoredMultiplier);
        digest.Append(AutomationWorkRateAuthority.Schema);
        digest.AppendFloat(
            AutomationWorkRateAuthority.MinimumAssistedWorkMultiplier);
        digest.AppendFloat(AutomationWorkRateAuthority.MaintenanceFullCondition);
        digest.AppendFloat(
            AutomationWorkRateAuthority.MinimumMaintenanceMultiplier);
        digest.AppendFloat(
            AutomationWorkRateAuthority.MaximumMaintenanceMultiplier);
        digest.AppendFloat(AutomationWorkRateAuthority.MinimumFaultMultiplier);
        digest.AppendFloat(AutomationWorkRateAuthority.MaximumFaultMultiplier);
        digest.AppendFloat(
            AutomationWorkRateAuthority.MinimumConditionMultiplier);
        digest.AppendFloat(
            AutomationWorkRateAuthority.MaximumConditionMultiplier);
        digest.AppendDouble(maximum);
        return new AutomationAssistedWorkDefinitionMaximumSnapshot(
            facilityDefinitionId,
            maximumMode,
            maximum,
            digest.ComputeSha256());
    }
}

public sealed class ProductionAutomationAssistedWorkMaximumContributor :
    IProductionRecipeWorkRateMaximumContributor
{
    public const string Schema =
        "production-automation-assisted-work-maximum-contributor@1";
    public const string StableContributorId =
        "work-rate:automation-assisted-definition";

    private readonly IAutomationAssistedWorkDefinitionMaximumQuery maximums;

    public ProductionAutomationAssistedWorkMaximumContributor(
        IAutomationAssistedWorkDefinitionMaximumQuery maximums)
    {
        this.maximums = maximums
            ?? throw new ArgumentNullException(nameof(maximums));
    }

    public string ContributorId => StableContributorId;

    public ProductionWorkRateMaximumContributorResult Capture(
        ProductionWorkRateMaximumSubject context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        AutomationAssistedWorkDefinitionMaximumSnapshot snapshot;
        try
        {
            snapshot = maximums.Capture(context.FacilityDefinitionId);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is InvalidOperationException
            || exception is KeyNotFoundException)
        {
            return Missing(context, exception.GetType().FullName, exception.Message);
        }
        if (!string.Equals(
                snapshot.FacilityDefinitionId,
                context.FacilityDefinitionId,
                StringComparison.Ordinal))
        {
            return Missing(
                context,
                "FACILITY_ID_MISMATCH",
                snapshot.FacilityDefinitionId);
        }
        if (!ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                snapshot.MaximumMultiplier,
                out ProductionWorkRateFixedPointUpperBound upperBound,
                out ProductionRecipeWorkRateMaximumGapReason failureReason))
        {
            return Missing(
                context,
                failureReason.ToString(),
                snapshot.SourceDigest);
        }

        CanonicalSemanticDigestBuilder digest = BeginDigest(context);
        digest.AppendEnum(snapshot.MaximumMode);
        digest.AppendDouble(snapshot.MaximumMultiplier);
        digest.Append(upperBound.ScaledValue);
        digest.Append(snapshot.SourceDigest);
        return ProductionWorkRateMaximumContributorResult.Complete(
            upperBound,
            digest.ComputeSha256());
    }

    private static ProductionWorkRateMaximumContributorResult Missing(
        ProductionWorkRateMaximumSubject context,
        string code,
        string detail)
    {
        CanonicalSemanticDigestBuilder digest = BeginDigest(context);
        digest.Append("gap");
        digest.Append(code ?? string.Empty);
        digest.Append(detail ?? string.Empty);
        return ProductionWorkRateMaximumContributorResult.Missing(
            ProductionRecipeWorkRateMaximumGapReason.ContributorRejected,
            (code ?? string.Empty) + ":" + (detail ?? string.Empty),
            digest.ComputeSha256());
    }

    private static CanonicalSemanticDigestBuilder BeginDigest(
        ProductionWorkRateMaximumSubject context)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(StableContributorId);
        digest.Append(context.FacilityDefinitionId);
        digest.Append(context.WorkstationTag);
        digest.Append(context.OperationDefinitionId);
        digest.Append(context.OperationSourceDigest);
        return digest;
    }
}

public sealed class ProductionAutomaticWorkRateMaximumQuery :
    IProductionAutomaticWorkRateMaximumQuery
{
    public const string Schema =
        "production-automatic-work-rate-maximum-query@1";

    private readonly ProductionFacilityDefinitionCatalog definitions;

    public ProductionAutomaticWorkRateMaximumQuery(
        ProductionFacilityDefinitionCatalog definitions)
    {
        this.definitions = definitions
            ?? throw new ArgumentNullException(nameof(definitions));
    }

    public ProductionWorkRateMaximumContributorResult Capture(
        ProductionWorkRateMaximumSubject context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        CanonicalSemanticDigestBuilder digest = BeginDigest(context);
        if (context.LaneProfile.Policy != ProductionWorkstationLanePolicy
                .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors
            || context.LaneProfile.AutomaticWorkLaneCount <= 0)
        {
            digest.Append("automatic-lane-mismatch");
            return ProductionWorkRateMaximumContributorResult.Missing(
                ProductionRecipeWorkRateMaximumGapReason.AutomaticLaneMismatch,
                "The facility has no authored automatic execution lane.",
                digest.ComputeSha256());
        }

        BuildingSO definition;
        try
        {
            definition = definitions.Require(context.FacilityDefinitionId);
        }
        catch (KeyNotFoundException exception)
        {
            digest.Append("missing-facility");
            digest.Append(exception.Message);
            return ProductionWorkRateMaximumContributorResult.Missing(
                ProductionRecipeWorkRateMaximumGapReason.AutomaticAuthorityMissing,
                exception.Message,
                digest.ComputeSha256());
        }
        BuildingAutomationAbility ability = definition
            .GetAbility<BuildingAutomationAbility>();
        if (ability == null
            || !Enum.IsDefined(typeof(AutomationMode), ability.maximumMode)
            || ability.maximumMode < AutomationMode.Automatic)
        {
            digest.Append("automatic-ability-missing");
            digest.Append(ability != null);
            digest.Append(ability == null ? -1 : (int)ability.maximumMode);
            return ProductionWorkRateMaximumContributorResult.Missing(
                ProductionRecipeWorkRateMaximumGapReason.AutomaticAuthorityMissing,
                "The facility has no authored Automatic execution ability.",
                digest.ComputeSha256());
        }

        float authoredRate = ability.automaticWorkPerSecond;
        ProductionWorkRateFixedPointUpperBound upperBound = default;
        ProductionRecipeWorkRateMaximumGapReason failureReason =
            ProductionRecipeWorkRateMaximumGapReason
                .NonFiniteOrNonPositiveUpperBound;
        bool validRate = !float.IsNaN(authoredRate)
            && !float.IsInfinity(authoredRate)
            && authoredRate > 0f
            && ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                authoredRate
                * AutomationWorkRateAuthority.MaximumConditionMultiplier,
                out upperBound,
                out failureReason);
        if (!validRate)
        {
            digest.Append("invalid-automatic-rate");
            digest.AppendFloat(authoredRate);
            digest.Append((int)failureReason);
            return ProductionWorkRateMaximumContributorResult.Missing(
                ProductionRecipeWorkRateMaximumGapReason
                    .NonFiniteOrNonPositiveUpperBound,
                "The authored automatic work rate is not finite and positive.",
                digest.ComputeSha256());
        }

        digest.AppendEnum(ability.maximumMode);
        digest.AppendFloat(authoredRate);
        digest.Append(AutomationWorkRateAuthority.Schema);
        digest.AppendFloat(
            AutomationWorkRateAuthority.MaximumConditionMultiplier);
        digest.Append(upperBound.ScaledValue);
        return ProductionWorkRateMaximumContributorResult.Complete(
            upperBound,
            digest.ComputeSha256());
    }

    private CanonicalSemanticDigestBuilder BeginDigest(
        ProductionWorkRateMaximumSubject context)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(definitions.SourceDigest);
        digest.Append(context.FacilityDefinitionId);
        digest.Append(context.WorkstationTag);
        digest.Append(context.LaneProfile.SourceDigest);
        digest.Append(context.OperationDefinitionId);
        digest.Append(context.OperationSourceDigest);
        return digest;
    }
}
