using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public enum ProductionThroughputProducerKind
{
    Recipe = 1,
    CapacityContributor = 2
}

public enum ProductionThroughputGapReason
{
    None = 0,
    RecipeOutputBranchAuthorityMissing = 1,
    RecipeWorkRateMaximumMissing = 2,
    SpecialThroughputProviderUnregistered = 3,
    AuthoredCycleAuthorityMissing = 4,
    ExecutionAuthorityUnsupported = 5,
    NonUnitSupportAssignmentUnsupported = 6,
    NoReachableThroughputCandidate = 7
}

public enum ProductionThroughputExecutionPath
{
    Manual = 1,
    Automatic = 2
}

public enum ProductionThroughputBottleneck
{
    WorkstationLane = 1,
    DetachedBatchProcessor = 2
}

internal static class ProductionAuthoredThroughputContractRules
{
    internal static void RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical non-empty identifier is required.",
                parameterName);
        }
    }

    internal static bool IsLowercaseSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');

    internal static void RequireDigest(string value, string parameterName)
    {
        if (!IsLowercaseSha256(value))
        {
            throw new ArgumentException(
                "A lowercase SHA-256 digest is required.",
                parameterName);
        }
    }

    internal static string DecimalToken(decimal value)
    {
        if (value <= 0m)
            throw new ArgumentOutOfRangeException(nameof(value));
        return value.ToString("G29", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// One physical output branch under one exact feasible support assignment.
/// The implementation is injected because the durable capacity projector owns
/// the actual normal/ruined branch mass authority.
/// </summary>
public readonly struct ProductionRecipeThroughputBranchSnapshot
{
    public ProductionRecipeThroughputBranchSnapshot(
        string recipeId,
        string branchId,
        string supportAssignmentSourceDigest,
        long maximumOutputMassGrams,
        IReadOnlyList<string> outputCapabilityIds,
        string sourceDigest)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            recipeId,
            nameof(recipeId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            supportAssignmentSourceDigest,
            nameof(supportAssignmentSourceDigest));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        if (maximumOutputMassGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(maximumOutputMassGrams));
        string[] orderedCapabilities = (outputCapabilityIds
                ?? throw new ArgumentNullException(nameof(outputCapabilityIds)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (orderedCapabilities.Length == 0
            || orderedCapabilities.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || orderedCapabilities.Distinct(StringComparer.Ordinal).Count()
                != orderedCapabilities.Length)
        {
            throw new InvalidOperationException(
                "Recipe throughput branch output capabilities are empty, invalid, or duplicated.");
        }

        RecipeId = recipeId;
        BranchId = branchId;
        SupportAssignmentSourceDigest = supportAssignmentSourceDigest;
        MaximumOutputMassGrams = maximumOutputMassGrams;
        OutputCapabilityIds = Array.AsReadOnly(orderedCapabilities);
        SourceDigest = sourceDigest;
    }

    public string RecipeId { get; }
    public string BranchId { get; }
    public string SupportAssignmentSourceDigest { get; }
    public long MaximumOutputMassGrams { get; }
    public IReadOnlyList<string> OutputCapabilityIds { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionRecipeThroughputBranchQueryResult
{
    private ProductionRecipeThroughputBranchQueryResult(
        IReadOnlyList<ProductionRecipeThroughputBranchSnapshot> branches,
        ProductionThroughputGapReason missingReason,
        string detail,
        string sourceDigest)
    {
        ProductionRecipeThroughputBranchSnapshot[] ordered = (branches
                ?? throw new ArgumentNullException(nameof(branches)))
            .OrderBy(value => value.BranchId, StringComparer.Ordinal)
            .ToArray();
        bool complete = missingReason == ProductionThroughputGapReason.None;
        if (complete == (ordered.Length == 0)
            || ordered.Select(value => value.BranchId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length
            || !complete && !string.IsNullOrEmpty(detail)
                && !string.Equals(detail, detail.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Recipe throughput branch query result is invalid.");
        }
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        Branches = Array.AsReadOnly(ordered);
        MissingReason = missingReason;
        Detail = detail ?? string.Empty;
        SourceDigest = sourceDigest;
    }

    public IReadOnlyList<ProductionRecipeThroughputBranchSnapshot> Branches { get; }
    public ProductionThroughputGapReason MissingReason { get; }
    public string Detail { get; }
    public string SourceDigest { get; }
    public bool IsComplete => MissingReason == ProductionThroughputGapReason.None;

    public static ProductionRecipeThroughputBranchQueryResult Complete(
        IReadOnlyList<ProductionRecipeThroughputBranchSnapshot> branches,
        string sourceDigest) => new(
        branches,
        ProductionThroughputGapReason.None,
        string.Empty,
        sourceDigest);

    public static ProductionRecipeThroughputBranchQueryResult Missing(
        ProductionThroughputGapReason reason,
        string detail,
        string sourceDigest)
    {
        if (reason == ProductionThroughputGapReason.None)
            throw new ArgumentOutOfRangeException(nameof(reason));
        return new ProductionRecipeThroughputBranchQueryResult(
            Array.Empty<ProductionRecipeThroughputBranchSnapshot>(),
            reason,
            detail,
            sourceDigest);
    }
}

public interface IProductionRecipeThroughputBranchQuery
{
    ProductionRecipeThroughputBranchQueryResult Capture(
        ProductionRecipeSO recipe,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile,
        ProductionAuthoredSupportAssignmentSnapshot supportAssignment);
}

/// <summary>
/// Execution-free work-rate maximum before recipe support work-speed factors
/// are applied. Rates use integer milli-WU/real-second so the core never
/// accumulates binary floating-point error.
/// </summary>
public readonly struct ProductionRecipeWorkRateMaximumSnapshot
{
    public ProductionRecipeWorkRateMaximumSnapshot(
        long manualMilliWuPerSecond,
        long automaticMilliWuPerSecond,
        string sourceDigest)
    {
        if (manualMilliWuPerSecond <= 0L
            || automaticMilliWuPerSecond < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(manualMilliWuPerSecond));
        }
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        ManualMilliWuPerSecond = manualMilliWuPerSecond;
        AutomaticMilliWuPerSecond = automaticMilliWuPerSecond;
        SourceDigest = sourceDigest;
    }

    public long ManualMilliWuPerSecond { get; }
    public long AutomaticMilliWuPerSecond { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionRecipeWorkRateMaximumQueryResult
{
    private ProductionRecipeWorkRateMaximumQueryResult(
        ProductionRecipeWorkRateMaximumSnapshot snapshot,
        bool hasSnapshot,
        ProductionThroughputGapReason missingReason,
        string detail,
        string sourceDigest)
    {
        if (hasSnapshot == (missingReason != ProductionThroughputGapReason.None)
            || !hasSnapshot && !string.IsNullOrEmpty(detail)
                && !string.Equals(detail, detail.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Recipe work-rate maximum query result is invalid.");
        }
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        Snapshot = snapshot;
        HasSnapshot = hasSnapshot;
        MissingReason = missingReason;
        Detail = detail ?? string.Empty;
        SourceDigest = sourceDigest;
    }

    public ProductionRecipeWorkRateMaximumSnapshot Snapshot { get; }
    public bool HasSnapshot { get; }
    public ProductionThroughputGapReason MissingReason { get; }
    public string Detail { get; }
    public string SourceDigest { get; }

    public static ProductionRecipeWorkRateMaximumQueryResult Complete(
        ProductionRecipeWorkRateMaximumSnapshot snapshot) => new(
        snapshot,
        true,
        ProductionThroughputGapReason.None,
        string.Empty,
        snapshot.SourceDigest);

    public static ProductionRecipeWorkRateMaximumQueryResult Missing(
        ProductionThroughputGapReason reason,
        string detail,
        string sourceDigest)
    {
        if (reason == ProductionThroughputGapReason.None)
            throw new ArgumentOutOfRangeException(nameof(reason));
        return new ProductionRecipeWorkRateMaximumQueryResult(
            default,
            false,
            reason,
            detail,
            sourceDigest);
    }
}

public interface IProductionRecipeWorkRateMaximumQuery
{
    ProductionRecipeWorkRateMaximumQueryResult Capture(
        string facilityDefinitionId,
        string workstationTag,
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
        ProductionRecipeSO recipe);
}

/// <summary>
/// Operation-polymorphic work-rate maximum. Recipe projection is an adapter
/// over this authority; special producers provide their own canonical
/// operation identity without fabricating a ProductionRecipeSO.
/// </summary>
public interface IProductionWorkRateMaximumQuery
{
    ProductionRecipeWorkRateMaximumQueryResult Capture(
        ProductionWorkRateMaximumSubject subject);
}

public sealed class ProductionThroughputCoverageGap
{
    public ProductionThroughputCoverageGap(
        string definitionId,
        string workstationTag,
        ProductionThroughputProducerKind producerKind,
        string producerId,
        string branchId,
        ProductionThroughputGapReason reason,
        string detail,
        string sourceDigest)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            definitionId,
            nameof(definitionId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            workstationTag,
            nameof(workstationTag));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            producerId,
            nameof(producerId));
        if (!string.IsNullOrEmpty(branchId))
        {
            ProductionAuthoredThroughputContractRules.RequireCanonical(
                branchId,
                nameof(branchId));
        }
        if (!Enum.IsDefined(typeof(ProductionThroughputProducerKind), producerKind)
            || !Enum.IsDefined(typeof(ProductionThroughputGapReason), reason)
            || reason == ProductionThroughputGapReason.None
            || !string.IsNullOrEmpty(detail)
                && !string.Equals(detail, detail.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Throughput coverage gap is invalid.");
        }
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));

        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        ProducerKind = producerKind;
        ProducerId = producerId;
        BranchId = branchId ?? string.Empty;
        Reason = reason;
        Detail = detail ?? string.Empty;
        SourceDigest = sourceDigest;
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public ProductionThroughputProducerKind ProducerKind { get; }
    public string ProducerId { get; }
    public string BranchId { get; }
    public ProductionThroughputGapReason Reason { get; }
    public string Detail { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionRecipeThroughputCycleCandidateSnapshot
{
    internal ProductionRecipeThroughputCycleCandidateSnapshot(
        string definitionId,
        string workstationTag,
        string recipeId,
        string branchId,
        string supportAssignmentSourceDigest,
        ProductionThroughputExecutionPath executionPath,
        ProductionThroughputBottleneck bottleneck,
        long maximumOutputMassGrams,
        string cyclesPerGameHourToken,
        long peakOutputMassGramsPerHour,
        string sourceDigest)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            definitionId,
            nameof(definitionId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            workstationTag,
            nameof(workstationTag));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            recipeId,
            nameof(recipeId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            supportAssignmentSourceDigest,
            nameof(supportAssignmentSourceDigest));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        if (executionPath == 0
            || bottleneck == 0
            || maximumOutputMassGrams <= 0L
            || peakOutputMassGramsPerHour <= 0L
            || !decimal.TryParse(
                cyclesPerGameHourToken,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out decimal cycles)
            || cycles <= 0m)
        {
            throw new ArgumentException(
                "Recipe throughput cycle candidate is invalid.");
        }

        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        RecipeId = recipeId;
        BranchId = branchId;
        SupportAssignmentSourceDigest = supportAssignmentSourceDigest;
        ExecutionPath = executionPath;
        Bottleneck = bottleneck;
        MaximumOutputMassGrams = maximumOutputMassGrams;
        CyclesPerGameHourToken = cyclesPerGameHourToken;
        PeakOutputMassGramsPerHour = peakOutputMassGramsPerHour;
        SourceDigest = sourceDigest;
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public string RecipeId { get; }
    public string BranchId { get; }
    public string SupportAssignmentSourceDigest { get; }
    public ProductionThroughputExecutionPath ExecutionPath { get; }
    public ProductionThroughputBottleneck Bottleneck { get; }
    public long MaximumOutputMassGrams { get; }
    public string CyclesPerGameHourToken { get; }
    public long PeakOutputMassGramsPerHour { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionRecipeThroughputProjectionResult
{
    internal ProductionRecipeThroughputProjectionResult(
        IReadOnlyList<ProductionRecipeThroughputCycleCandidateSnapshot> candidates,
        IReadOnlyList<ProductionThroughputCoverageGap> gaps)
    {
        Candidates = Array.AsReadOnly((candidates
                ?? throw new ArgumentNullException(nameof(candidates)))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ThenBy(value => value.BranchId, StringComparer.Ordinal)
            .ThenBy(value => value.SupportAssignmentSourceDigest,
                StringComparer.Ordinal)
            .ToArray());
        Gaps = Array.AsReadOnly((gaps
                ?? throw new ArgumentNullException(nameof(gaps)))
            .OrderBy(value => value.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value.BranchId, StringComparer.Ordinal)
            .ThenBy(value => (int)value.Reason)
            .ToArray());
    }

    public IReadOnlyList<ProductionRecipeThroughputCycleCandidateSnapshot>
        Candidates { get; }
    public IReadOnlyList<ProductionThroughputCoverageGap> Gaps { get; }
}

public sealed class ProductionAuthoredThroughputFacilitySubject
{
    public ProductionAuthoredThroughputFacilitySubject(
        string definitionId,
        string workstationTag,
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile,
        IReadOnlyList<ProductionRecipeSO> recipes,
        IReadOnlyList<ProductionSpecialThroughputCandidateSnapshot>
            specialCandidates = null,
        IReadOnlyList<ProductionThroughputCoverageGap> specialGaps = null)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            definitionId,
            nameof(definitionId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            workstationTag,
            nameof(workstationTag));
        if (laneProfile == null || !laneProfile.IsSpecified)
            throw new ArgumentException(
                "Throughput subject requires an authored lane profile.",
                nameof(laneProfile));
        ProcessFluidProfile = processFluidProfile
            ?? throw new ArgumentNullException(nameof(processFluidProfile));

        ProductionRecipeSO[] orderedRecipes = (recipes
                ?? throw new ArgumentNullException(nameof(recipes)))
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        if (orderedRecipes.Length != recipes.Count
            || orderedRecipes.Select(value => value.RecipeId)
                .Distinct(StringComparer.Ordinal).Count()
                != orderedRecipes.Length
            || orderedRecipes.Any(value => !string.Equals(
                value.WorkstationTag,
                workstationTag,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Throughput subject recipes are not canonical for the workstation.");
        }

        ProductionSpecialThroughputCandidateSnapshot[] orderedSpecial =
            (specialCandidates
                ?? Array.Empty<ProductionSpecialThroughputCandidateSnapshot>())
            .OrderBy(value => value.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value.BranchId, StringComparer.Ordinal)
            .ToArray();
        ProductionThroughputCoverageGap[] orderedGaps = (specialGaps
                ?? Array.Empty<ProductionThroughputCoverageGap>())
            .OrderBy(value => value.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value.BranchId, StringComparer.Ordinal)
            .ThenBy(value => (int)value.Reason)
            .ToArray();
        HashSet<string> specialCandidateKeys = new(
            orderedSpecial.Select(value => value.ProducerId + "\n"
                + value.BranchId),
            StringComparer.Ordinal);
        string[] specialGapKeys = orderedGaps
            .Select(value => value.ProducerId + "\n" + value.BranchId)
            .ToArray();
        if (orderedSpecial.Any(value => !value.Matches(
                definitionId,
                workstationTag))
            || orderedSpecial.Select(value => value.ProducerId + "\n"
                    + value.BranchId)
                .Distinct(StringComparer.Ordinal).Count()
                != orderedSpecial.Length
            || specialGapKeys.Distinct(StringComparer.Ordinal).Count()
                != specialGapKeys.Length
            || specialGapKeys.Any(specialCandidateKeys.Contains)
            || orderedGaps.Any(value => value.ProducerKind
                    != ProductionThroughputProducerKind.CapacityContributor
                || !string.Equals(value.DefinitionId, definitionId,
                    StringComparison.Ordinal)
                || !string.Equals(value.WorkstationTag, workstationTag,
                    StringComparison.Ordinal))
            || orderedRecipes.Length == 0
                && orderedSpecial.Length == 0
                && orderedGaps.Length == 0)
        {
            throw new InvalidOperationException(
                "Throughput subject special producer coverage is invalid.");
        }

        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        LaneProfile = laneProfile;
        Recipes = Array.AsReadOnly(orderedRecipes);
        SpecialCandidates = Array.AsReadOnly(orderedSpecial);
        SpecialGaps = Array.AsReadOnly(orderedGaps);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-authored-throughput-facility-subject@1");
        digest.Append(DefinitionId);
        digest.Append(WorkstationTag);
        digest.Append(LaneProfile.SourceDigest);
        digest.Append(ProcessFluidProfile.SourceDigest);
        digest.Append(Recipes.Count);
        foreach (ProductionRecipeSO recipe in Recipes)
        {
            digest.Append(recipe.RecipeId);
            digest.Append(ProductionRecipeSemanticDigest.Capture(recipe));
        }
        digest.Append(SpecialCandidates.Count);
        foreach (ProductionSpecialThroughputCandidateSnapshot candidate in
                 SpecialCandidates)
            digest.Append(candidate.SourceDigest);
        digest.Append(SpecialGaps.Count);
        foreach (ProductionThroughputCoverageGap gap in SpecialGaps)
            digest.Append(gap.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public ProductionFacilityWorkstationLaneCapacityProfile LaneProfile { get; }
    public ProductionFacilityProcessFluidCapacityProfile ProcessFluidProfile { get; }
    public IReadOnlyList<ProductionRecipeSO> Recipes { get; }
    public IReadOnlyList<ProductionSpecialThroughputCandidateSnapshot>
        SpecialCandidates { get; }
    public IReadOnlyList<ProductionThroughputCoverageGap> SpecialGaps { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionSpecialThroughputCandidateSnapshot
{
    public ProductionSpecialThroughputCandidateSnapshot(
        string definitionId,
        string workstationTag,
        string producerId,
        string branchId,
        long peakOutputMassGramsPerHour,
        string sourceDigest)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            definitionId,
            nameof(definitionId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            workstationTag,
            nameof(workstationTag));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            producerId,
            nameof(producerId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        if (peakOutputMassGramsPerHour <= 0L)
            throw new ArgumentOutOfRangeException(
                nameof(peakOutputMassGramsPerHour));
        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        ProducerId = producerId;
        BranchId = branchId;
        PeakOutputMassGramsPerHour = peakOutputMassGramsPerHour;
        SourceDigest = sourceDigest;
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public string ProducerId { get; }
    public string BranchId { get; }
    public long PeakOutputMassGramsPerHour { get; }
    public string SourceDigest { get; }

    internal bool Matches(string definitionId, string workstationTag) =>
        string.Equals(DefinitionId, definitionId, StringComparison.Ordinal)
        && string.Equals(WorkstationTag, workstationTag,
            StringComparison.Ordinal);
}

/// <summary>
/// Injected simulation time conversion. The runtime currently owns this value;
/// the throughput core must not introduce a second hard-coded game-hour scale.
/// </summary>
public readonly struct ProductionThroughputTimeScaleSnapshot
{
    public ProductionThroughputTimeScaleSnapshot(
        long realTimeMicrosecondsPerGameHour,
        string sourceDigest)
    {
        if (realTimeMicrosecondsPerGameHour <= 0L)
            throw new ArgumentOutOfRangeException(
                nameof(realTimeMicrosecondsPerGameHour));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        RealTimeMicrosecondsPerGameHour = realTimeMicrosecondsPerGameHour;
        SourceDigest = sourceDigest;
    }

    public long RealTimeMicrosecondsPerGameHour { get; }
    public string SourceDigest { get; }
    public decimal RealTimeSecondsPerGameHour => checked(
        RealTimeMicrosecondsPerGameHour / 1_000_000m);
}

/// <summary>
/// Projects the production throughput clock from the single live calendar
/// authority. Runtime production, crops, and automation use the same value, so
/// the audit cannot silently drift behind a private 7.5-second literal.
/// </summary>
public static class ProductionThroughputTimeScaleAuthority
{
    public const string Schema =
        "production-throughput-game-calendar-time-scale@1";
    private const decimal MicrosecondsPerSecond = 1_000_000m;

    public static ProductionThroughputTimeScaleSnapshot Capture()
    {
        decimal exactMicroseconds = checked(
            (decimal)GameSimulationTimeRules.SecondsPerDay
            * MicrosecondsPerSecond
            / GameSimulationTimeRules.HoursPerDay);
        if (exactMicroseconds <= 0m
            || exactMicroseconds != decimal.Truncate(exactMicroseconds))
        {
            throw new InvalidOperationException(
                "Game calendar hour cannot be represented as integer microseconds.");
        }

        long microseconds = checked((long)exactMicroseconds);
        decimal seconds = checked(exactMicroseconds / MicrosecondsPerSecond);
        if (seconds != (decimal)GameSimulationTimeRules.SecondsPerGameHour)
        {
            throw new InvalidOperationException(
                "Game calendar seconds-per-hour authority is internally inconsistent.");
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(GameSimulationTimeRules.HoursPerDay);
        digest.AppendFloat(GameSimulationTimeRules.SecondsPerDay);
        digest.AppendFloat(GameSimulationTimeRules.SecondsPerGameHour);
        digest.Append(microseconds);
        return new ProductionThroughputTimeScaleSnapshot(
            microseconds,
            digest.ComputeSha256());
    }
}

public readonly struct ProductionWorkCycleThroughputSnapshot
{
    public ProductionWorkCycleThroughputSnapshot(
        decimal cyclesPerGameHour,
        ProductionThroughputExecutionPath path,
        string sourceDigest)
    {
        if (cyclesPerGameHour <= 0m)
            throw new ArgumentOutOfRangeException(nameof(cyclesPerGameHour));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        CyclesPerGameHour = cyclesPerGameHour;
        Path = path;
        SourceDigest = sourceDigest;
    }

    public decimal CyclesPerGameHour { get; }
    public ProductionThroughputExecutionPath Path { get; }
    public string SourceDigest { get; }
}

/// <summary>
/// Shared lane-aware work-cycle projection for recipes and polymorphic special
/// producers. Mode-exclusive manual/automatic lanes compete by maximum and are
/// never summed.
/// </summary>
public static class ProductionWorkCycleThroughputAuthority
{
    public const string Schema = "production-work-cycle-throughput@2";
    private const decimal MilliWuPerWu = 1_000m;

    public static ProductionWorkCycleThroughputSnapshot Capture(
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
        ProductionRecipeWorkRateMaximumSnapshot rate,
        ProductionOutputFactor workFactor,
        decimal requiredWu,
        ProductionThroughputTimeScaleSnapshot timeScale) =>
        CaptureInternal(
            laneProfile,
            rate,
            workFactor,
            requiredWu,
            timeScale,
            allowAutomaticLane: true);

    /// <summary>
    /// Projects operations whose live executor can only use an actor/manual
    /// lane even when the authored workstation also exposes an alternative
    /// automatic lane. The automatic rate remains validated but is not a
    /// reachable execution path for this operation.
    /// </summary>
    public static ProductionWorkCycleThroughputSnapshot CaptureManualOnly(
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
        ProductionRecipeWorkRateMaximumSnapshot rate,
        ProductionOutputFactor workFactor,
        decimal requiredWu,
        ProductionThroughputTimeScaleSnapshot timeScale) =>
        CaptureInternal(
            laneProfile,
            rate,
            workFactor,
            requiredWu,
            timeScale,
            allowAutomaticLane: false);

    private static ProductionWorkCycleThroughputSnapshot CaptureInternal(
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
        ProductionRecipeWorkRateMaximumSnapshot rate,
        ProductionOutputFactor workFactor,
        decimal requiredWu,
        ProductionThroughputTimeScaleSnapshot timeScale,
        bool allowAutomaticLane)
    {
        if (laneProfile == null || !laneProfile.IsSpecified)
            throw new ArgumentException(
                "Work-cycle projection requires an explicit lane profile.",
                nameof(laneProfile));
        if (requiredWu <= 0m)
            throw new ArgumentOutOfRangeException(nameof(requiredWu));
        if (timeScale.RealTimeMicrosecondsPerGameHour <= 0L)
            throw new ArgumentException(
                "Work-cycle projection requires a valid time scale.",
                nameof(timeScale));
        if (laneProfile.Policy == ProductionWorkstationLanePolicy
                .ManualWithDetachedBatchProcessors
            && rate.AutomaticMilliWuPerSecond != 0L
            || laneProfile.Policy == ProductionWorkstationLanePolicy
                .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors
            && rate.AutomaticMilliWuPerSecond <= 0L)
        {
            throw new InvalidOperationException(
                "Work-rate maximum disagrees with lane policy.");
        }

        decimal manualRate = ScaleRate(
            rate.ManualMilliWuPerSecond,
            laneProfile.ManualWorkLaneCount,
            workFactor);
        decimal selectedRate = manualRate;
        ProductionThroughputExecutionPath path =
            ProductionThroughputExecutionPath.Manual;
        if (allowAutomaticLane
            && laneProfile.Policy == ProductionWorkstationLanePolicy
                .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors)
        {
            decimal automaticRate = ScaleRate(
                rate.AutomaticMilliWuPerSecond,
                laneProfile.AutomaticWorkLaneCount,
                workFactor);
            if (automaticRate > selectedRate)
            {
                selectedRate = automaticRate;
                path = ProductionThroughputExecutionPath.Automatic;
            }
        }

        decimal cycles = checked(
            selectedRate
            * timeScale.RealTimeSecondsPerGameHour
            / checked(requiredWu * MilliWuPerWu));
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(laneProfile.SourceDigest);
        digest.Append(rate.SourceDigest);
        digest.Append(workFactor.Numerator);
        digest.Append(workFactor.Denominator);
        digest.Append(ProductionAuthoredThroughputContractRules.DecimalToken(
            requiredWu));
        digest.Append(timeScale.SourceDigest);
        digest.Append(allowAutomaticLane);
        digest.Append((int)path);
        digest.Append(ProductionAuthoredThroughputContractRules.DecimalToken(
            cycles));
        return new ProductionWorkCycleThroughputSnapshot(
            cycles,
            path,
            digest.ComputeSha256());
    }

    private static decimal ScaleRate(
        long milliWuPerSecond,
        int laneCount,
        ProductionOutputFactor workFactor)
    {
        if (milliWuPerSecond <= 0L
            || laneCount <= 0
            || workFactor.Numerator <= 0L
            || workFactor.Denominator <= 0L)
        {
            throw new InvalidOperationException(
                "Throughput work-rate authority is invalid.");
        }
        return checked(
            milliWuPerSecond
            * (decimal)laneCount
            * workFactor.Numerator
            / workFactor.Denominator);
    }
}

/// <summary>
/// Pure recipe-cycle projector. It joins branch mass and work rate only inside
/// the same feasible support assignment. Manual and automatic lanes are
/// alternatives; passive processing is a pipeline bottleneck.
/// </summary>
public sealed class ProductionRecipeThroughputCycleProjector
{
    public const string Schema =
        "production-recipe-throughput-cycle-projector@1";
    private readonly IProductionMaximumOutputFactorCatalog maximumFactors;
    private readonly IProductionRecipeThroughputBranchQuery branchQuery;
    private readonly IProductionRecipeWorkRateMaximumQuery workRateQuery;
    private readonly ProductionThroughputTimeScaleSnapshot timeScale;

    public ProductionRecipeThroughputCycleProjector(
        IProductionMaximumOutputFactorCatalog maximumFactors,
        IProductionRecipeThroughputBranchQuery branchQuery,
        IProductionRecipeWorkRateMaximumQuery workRateQuery,
        ProductionThroughputTimeScaleSnapshot timeScale)
    {
        this.maximumFactors = maximumFactors
            ?? throw new ArgumentNullException(nameof(maximumFactors));
        this.branchQuery = branchQuery
            ?? throw new ArgumentNullException(nameof(branchQuery));
        this.workRateQuery = workRateQuery
            ?? throw new ArgumentNullException(nameof(workRateQuery));
        if (timeScale.RealTimeMicrosecondsPerGameHour <= 0L
            || !ProductionAuthoredThroughputContractRules.IsLowercaseSha256(
                timeScale.SourceDigest))
        {
            throw new ArgumentException(
                "Production throughput time scale is invalid.",
                nameof(timeScale));
        }
        this.timeScale = timeScale;
    }

    public ProductionRecipeThroughputProjectionResult Capture(
        ProductionAuthoredThroughputFacilitySubject facility)
    {
        if (facility == null)
            throw new ArgumentNullException(nameof(facility));
        List<ProductionRecipeThroughputCycleCandidateSnapshot> candidates =
            new();
        List<ProductionThroughputCoverageGap> gaps = new();
        foreach (ProductionRecipeSO recipe in facility.Recipes)
        {
            ProductionRecipeWorkRateMaximumQueryResult workRate =
                workRateQuery.Capture(
                    facility.DefinitionId,
                    facility.WorkstationTag,
                    facility.LaneProfile,
                    recipe)
                ?? throw new InvalidOperationException(
                    "Recipe work-rate query returned null: " + recipe.RecipeId);
            if (!workRate.HasSnapshot)
            {
                gaps.Add(CreateGap(
                    facility,
                    recipe.RecipeId,
                    string.Empty,
                    workRate.MissingReason,
                    workRate.Detail,
                    workRate.SourceDigest));
                continue;
            }
            IReadOnlyList<ProductionAuthoredSupportAssignmentSnapshot>
                assignments = maximumFactors.CaptureFeasibleAssignments(recipe);
            if (assignments == null || assignments.Count == 0)
            {
                throw new InvalidOperationException(
                    "Recipe has no feasible support assignment: "
                    + recipe.RecipeId);
            }
            foreach (ProductionAuthoredSupportAssignmentSnapshot assignment in
                     assignments.OrderBy(value => value.SourceDigest,
                         StringComparer.Ordinal))
            {
                ProductionRecipeThroughputBranchQueryResult branches =
                    branchQuery.Capture(
                        recipe,
                        facility.ProcessFluidProfile,
                        assignment)
                    ?? throw new InvalidOperationException(
                        "Recipe branch query returned null: " + recipe.RecipeId);
                if (!branches.IsComplete)
                {
                    gaps.Add(CreateGap(
                        facility,
                        recipe.RecipeId,
                        "support-assignment:" + assignment.SourceDigest,
                        branches.MissingReason,
                        branches.Detail,
                        branches.SourceDigest));
                    continue;
                }

                ProductionOutputFactor supportWorkFactor = assignment.Supports
                    .Aggregate(
                        ProductionOutputFactor.One,
                        (current, support) =>
                            current.Multiply(support.WorkSpeedFactor));
                ProductionWorkCycleThroughputSnapshot workstation =
                    ProductionWorkCycleThroughputAuthority.Capture(
                        facility.LaneProfile,
                        workRate.Snapshot,
                        supportWorkFactor,
                        (decimal)recipe.RequiredWork,
                        timeScale);
                decimal cycles = workstation.CyclesPerGameHour;
                ProductionThroughputExecutionPath path = workstation.Path;
                ProductionThroughputBottleneck bottleneck =
                    ProductionThroughputBottleneck.WorkstationLane;
                if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch)
                {
                    decimal processorCycles =
                        ResolveBatchProcessorCyclesPerHour(
                            recipe,
                            assignment);
                    if (processorCycles < cycles)
                    {
                        cycles = processorCycles;
                        bottleneck =
                            ProductionThroughputBottleneck.DetachedBatchProcessor;
                    }
                }

                foreach (ProductionRecipeThroughputBranchSnapshot branch in
                         branches.Branches)
                {
                    if (!string.Equals(
                            branch.RecipeId,
                            recipe.RecipeId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            branch.SupportAssignmentSourceDigest,
                            assignment.SourceDigest,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Recipe branch authority drifted from its support assignment: "
                            + recipe.RecipeId);
                    }
                    decimal exactPeak = checked(
                        branch.MaximumOutputMassGrams * cycles);
                    if (exactPeak <= 0m || exactPeak > long.MaxValue)
                        throw new OverflowException(
                            "Recipe throughput exceeds Int64 gram/hour capacity.");
                    long peak = checked((long)decimal.Ceiling(exactPeak));
                    string cyclesToken =
                        ProductionAuthoredThroughputContractRules.DecimalToken(
                            cycles);

                    CanonicalSemanticDigestBuilder digest = new();
                    digest.Append(Schema);
                    digest.Append(facility.SourceDigest);
                    digest.Append(recipe.RecipeId);
                    digest.Append(ProductionRecipeSemanticDigest.Capture(recipe));
                    digest.Append(assignment.SourceDigest);
                    digest.Append(branch.SourceDigest);
                    digest.Append(workRate.SourceDigest);
                    digest.Append(workstation.SourceDigest);
                    digest.Append(timeScale.SourceDigest);
                    digest.Append(supportWorkFactor.Numerator);
                    digest.Append(supportWorkFactor.Denominator);
                    digest.Append((int)path);
                    digest.Append((int)bottleneck);
                    digest.Append(branch.MaximumOutputMassGrams);
                    digest.Append(cyclesToken);
                    digest.Append(peak);
                    candidates.Add(
                        new ProductionRecipeThroughputCycleCandidateSnapshot(
                            facility.DefinitionId,
                            facility.WorkstationTag,
                            recipe.RecipeId,
                            branch.BranchId,
                            assignment.SourceDigest,
                            path,
                            bottleneck,
                            branch.MaximumOutputMassGrams,
                            cyclesToken,
                            peak,
                            digest.ComputeSha256()));
                }
            }
        }
        return new ProductionRecipeThroughputProjectionResult(
            candidates,
            gaps);
    }

    private decimal ResolveBatchProcessorCyclesPerHour(
        ProductionRecipeSO recipe,
        ProductionAuthoredSupportAssignmentSnapshot assignment)
    {
        if (assignment == null)
            throw new ArgumentNullException(nameof(assignment));
        ProductionAuthoredSupportProfileSnapshot[] supports = assignment.Supports
            .Where(value => value.Kind == ProductionSupportKind.BatchProcessor)
            .OrderBy(value => value.SupportId, StringComparer.Ordinal)
            .ToArray();
        if (supports.Length == 0)
        {
            throw new InvalidOperationException(
                "Passive recipe assignment has no authored batch processor: "
                + recipe.RecipeId);
        }
        long lanes = 0L;
        HashSet<string> supportIds = new(StringComparer.Ordinal);
        foreach (ProductionAuthoredSupportProfileSnapshot support in supports
                     .OrderBy(value => value.SupportId, StringComparer.Ordinal))
        {
            if (support == null
                || !supportIds.Add(support.SupportId)
                || support.Kind != ProductionSupportKind.BatchProcessor
                || support.BatchCapacity <= 0
                || support.MaximumLinkedInstancesPerWorkstation <= 0)
            {
                throw new InvalidOperationException(
                    "Passive batch support authority is invalid: "
                    + recipe.RecipeId);
            }
            lanes = checked(lanes + checked(
                (long)support.BatchCapacity
                * support.MaximumLinkedInstancesPerWorkstation));
        }
        decimal processingHours = checked((decimal)recipe.ProcessingGameHours);
        if (lanes <= 0L || processingHours <= 0m)
            throw new InvalidOperationException(
                "Passive recipe duration authority is invalid: "
                + recipe.RecipeId);
        return checked(lanes / processingHours);
    }

    private static ProductionThroughputCoverageGap CreateGap(
        ProductionAuthoredThroughputFacilitySubject facility,
        string recipeId,
        string branchId,
        ProductionThroughputGapReason reason,
        string detail,
        string upstreamDigest)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-throughput-coverage-gap@1");
        digest.Append(facility.SourceDigest);
        digest.Append((int)ProductionThroughputProducerKind.Recipe);
        digest.Append(recipeId);
        digest.Append(branchId);
        digest.Append((int)reason);
        digest.Append(detail ?? string.Empty);
        digest.Append(upstreamDigest);
        return new ProductionThroughputCoverageGap(
            facility.DefinitionId,
            facility.WorkstationTag,
            ProductionThroughputProducerKind.Recipe,
            recipeId,
            branchId,
            reason,
            detail,
            digest.ComputeSha256());
    }
}

public sealed class ProductionAuthoredThroughputCoverageSnapshot
{
    internal ProductionAuthoredThroughputCoverageSnapshot(
        IReadOnlyList<ProductionOutputThroughputEnvelopeSnapshot> envelopes,
        IReadOnlyList<ProductionThroughputCoverageGap> gaps,
        string sourceDigest)
    {
        ProductionOutputThroughputEnvelopeSnapshot[] orderedEnvelopes =
            (envelopes ?? throw new ArgumentNullException(nameof(envelopes)))
            .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        ProductionThroughputCoverageGap[] orderedGaps = (gaps
                ?? throw new ArgumentNullException(nameof(gaps)))
            .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value.WorkstationTag, StringComparer.Ordinal)
            .ThenBy(value => value.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value.BranchId, StringComparer.Ordinal)
            .ThenBy(value => (int)value.Reason)
            .ToArray();
        if (orderedEnvelopes
                .Select(value => value.DefinitionId + "\n" + value.WorkstationTag)
                .Distinct(StringComparer.Ordinal).Count()
            != orderedEnvelopes.Length)
        {
            throw new InvalidOperationException(
                "Throughput coverage contains duplicate envelope keys.");
        }
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        CompleteEnvelopes = Array.AsReadOnly(orderedEnvelopes);
        Gaps = Array.AsReadOnly(orderedGaps);
        SourceDigest = sourceDigest;
    }

    public IReadOnlyList<ProductionOutputThroughputEnvelopeSnapshot>
        CompleteEnvelopes { get; }
    public IReadOnlyList<ProductionThroughputCoverageGap> Gaps { get; }
    public string SourceDigest { get; }
    public bool IsComplete => Gaps.Count == 0;

    public void RequireComplete()
    {
        if (IsComplete)
            return;
        ProductionThroughputCoverageGap first = Gaps[0];
        throw new InvalidOperationException(
            "Authored throughput coverage is incomplete: "
            + first.DefinitionId + "/" + first.WorkstationTag + "/"
            + first.ProducerId + "/" + first.BranchId + ":"
            + first.Reason);
    }
}

public interface IProductionAuthoredThroughputEnvelopeQuery
{
    ProductionAuthoredThroughputCoverageSnapshot Capture(
        IReadOnlyList<ProductionAuthoredThroughputFacilitySubject> facilities);
}

/// <summary>
/// Aggregates complete facility-key envelopes. A key with even one typed gap
/// is intentionally withheld rather than publishing a partial maximum.
/// </summary>
public sealed class ProductionAuthoredThroughputEnvelopeAuthority :
    IProductionAuthoredThroughputEnvelopeQuery
{
    public const string Schema =
        "production-authored-throughput-envelope-authority@1";
    private readonly ProductionRecipeThroughputCycleProjector recipeProjector;

    public ProductionAuthoredThroughputEnvelopeAuthority(
        ProductionRecipeThroughputCycleProjector recipeProjector)
    {
        this.recipeProjector = recipeProjector
            ?? throw new ArgumentNullException(nameof(recipeProjector));
    }

    public ProductionAuthoredThroughputCoverageSnapshot Capture(
        IReadOnlyList<ProductionAuthoredThroughputFacilitySubject> facilities)
    {
        ProductionAuthoredThroughputFacilitySubject[] ordered = (facilities
                ?? throw new ArgumentNullException(nameof(facilities)))
            .OrderBy(value => value?.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value?.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => value == null)
            || ordered.Select(value => value.DefinitionId + "\n"
                    + value.WorkstationTag)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Authored throughput facility scope is empty or duplicated.");
        }

        List<ProductionOutputThroughputEnvelopeSnapshot> envelopes = new();
        List<ProductionThroughputCoverageGap> gaps = new();
        CanonicalSemanticDigestBuilder catalogDigest = new();
        catalogDigest.Append(Schema);
        catalogDigest.Append(ordered.Length);
        foreach (ProductionAuthoredThroughputFacilitySubject facility in ordered)
        {
            ProductionRecipeThroughputProjectionResult recipe =
                recipeProjector.Capture(facility);
            List<(long Peak, string Digest)> candidates = recipe.Candidates
                .Select(value => (
                    Peak: value.PeakOutputMassGramsPerHour,
                    Digest: value.SourceDigest))
                .Concat(facility.SpecialCandidates.Select(value => (
                    Peak: value.PeakOutputMassGramsPerHour,
                    Digest: value.SourceDigest)))
                .OrderByDescending(value => value.Peak)
                .ThenBy(value => value.Digest, StringComparer.Ordinal)
                .ToList();
            List<ProductionThroughputCoverageGap> facilityGaps = recipe.Gaps
                .Concat(facility.SpecialGaps)
                .OrderBy(value => value.ProducerId, StringComparer.Ordinal)
                .ThenBy(value => value.BranchId, StringComparer.Ordinal)
                .ThenBy(value => (int)value.Reason)
                .ToList();
            if (candidates.Count == 0 && facilityGaps.Count == 0)
            {
                CanonicalSemanticDigestBuilder missingDigest = new();
                missingDigest.Append("production-throughput-coverage-gap@1");
                missingDigest.Append(facility.SourceDigest);
                missingDigest.Append((int)ProductionThroughputGapReason
                    .NoReachableThroughputCandidate);
                facilityGaps.Add(new ProductionThroughputCoverageGap(
                    facility.DefinitionId,
                    facility.WorkstationTag,
                    ProductionThroughputProducerKind.CapacityContributor,
                    "facility-producer:" + facility.DefinitionId,
                    string.Empty,
                    ProductionThroughputGapReason
                        .NoReachableThroughputCandidate,
                    string.Empty,
                    missingDigest.ComputeSha256()));
            }

            catalogDigest.Append(facility.SourceDigest);
            catalogDigest.Append(candidates.Count);
            foreach ((long peak, string digest) in candidates)
            {
                catalogDigest.Append(peak);
                catalogDigest.Append(digest);
            }
            catalogDigest.Append(facilityGaps.Count);
            foreach (ProductionThroughputCoverageGap gap in facilityGaps)
            {
                catalogDigest.Append(gap.SourceDigest);
                gaps.Add(gap);
            }
            if (facilityGaps.Count != 0)
                continue;

            (long Peak, string Digest) winner = candidates[0];
            CanonicalSemanticDigestBuilder envelopeDigest = new();
            envelopeDigest.Append(
                "production-authored-throughput-envelope@1");
            envelopeDigest.Append(facility.SourceDigest);
            envelopeDigest.Append(candidates.Count);
            foreach ((long peak, string digest) in candidates)
            {
                envelopeDigest.Append(peak);
                envelopeDigest.Append(digest);
            }
            envelopeDigest.Append(winner.Peak);
            envelopeDigest.Append(winner.Digest);
            envelopes.Add(new ProductionOutputThroughputEnvelopeSnapshot(
                facility.DefinitionId,
                facility.WorkstationTag,
                winner.Peak,
                envelopeDigest.ComputeSha256()));
        }
        catalogDigest.Append(envelopes.Count);
        catalogDigest.Append(gaps.Count);
        return new ProductionAuthoredThroughputCoverageSnapshot(
            envelopes,
            gaps,
            catalogDigest.ComputeSha256());
    }
}
