using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public enum ProductionRecipeWorkRateMaximumGapReason
{
    None = 0,
    MissingContributor = 1,
    ContributorRejected = 2,
    NonFiniteOrNonPositiveUpperBound = 3,
    FixedPointOverflow = 4,
    InvalidLaneProfile = 5,
    AutomaticAuthorityMissing = 6,
    AutomaticLaneMismatch = 7
}

/// <summary>
/// A positive, conservative fixed-point upper bound. Sources are rounded up to
/// one nano-unit. Products remain exact rationals until the final mWU/s ceil.
/// </summary>
public readonly struct ProductionWorkRateFixedPointUpperBound :
    IEquatable<ProductionWorkRateFixedPointUpperBound>
{
    public const long Scale = 1_000_000_000L;

    private ProductionWorkRateFixedPointUpperBound(long scaledValue)
    {
        ScaledValue = scaledValue;
    }

    public long ScaledValue { get; }
    public bool IsValid => ScaledValue > 0L;

    public static ProductionWorkRateFixedPointUpperBound ExactScaled(
        long scaledValue)
    {
        if (scaledValue <= 0L)
            throw new ArgumentOutOfRangeException(nameof(scaledValue));
        return new ProductionWorkRateFixedPointUpperBound(scaledValue);
    }

    public static bool TryFromDecimalUpperBound(
        decimal value,
        out ProductionWorkRateFixedPointUpperBound upperBound,
        out ProductionRecipeWorkRateMaximumGapReason failureReason)
    {
        if (value <= 0m)
        {
            upperBound = default;
            failureReason = ProductionRecipeWorkRateMaximumGapReason
                .NonFiniteOrNonPositiveUpperBound;
            return false;
        }

        int[] bits = decimal.GetBits(value);
        BigInteger significand = (uint)bits[0]
            | (BigInteger)(uint)bits[1] << 32
            | (BigInteger)(uint)bits[2] << 64;
        int decimalScale = (bits[3] >> 16) & 0x7f;
        BigInteger denominator = BigInteger.Pow(10, decimalScale);
        return TryCreateFromExactRational(
            significand,
            denominator,
            out upperBound,
            out failureReason);
    }

    public static bool TryFromDoubleUpperBound(
        double value,
        out ProductionWorkRateFixedPointUpperBound upperBound,
        out ProductionRecipeWorkRateMaximumGapReason failureReason)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
        {
            upperBound = default;
            failureReason = ProductionRecipeWorkRateMaximumGapReason
                .NonFiniteOrNonPositiveUpperBound;
            return false;
        }

        long raw = BitConverter.DoubleToInt64Bits(value);
        int exponentBits = (int)((raw >> 52) & 0x7ffL);
        long fractionBits = raw & 0x000f_ffff_ffff_ffffL;
        BigInteger significand;
        int binaryExponent;
        if (exponentBits == 0)
        {
            significand = fractionBits;
            binaryExponent = -1074;
        }
        else
        {
            significand = (1L << 52) | fractionBits;
            binaryExponent = exponentBits - 1023 - 52;
        }

        BigInteger numerator = significand;
        BigInteger denominator = BigInteger.One;
        if (binaryExponent >= 0)
            numerator <<= binaryExponent;
        else
            denominator <<= -binaryExponent;
        return TryCreateFromExactRational(
            numerator,
            denominator,
            out upperBound,
            out failureReason);
    }

    private static bool TryCreateFromExactRational(
        BigInteger numerator,
        BigInteger denominator,
        out ProductionWorkRateFixedPointUpperBound upperBound,
        out ProductionRecipeWorkRateMaximumGapReason failureReason)
    {
        if (numerator <= BigInteger.Zero || denominator <= BigInteger.Zero)
        {
            upperBound = default;
            failureReason = ProductionRecipeWorkRateMaximumGapReason
                .NonFiniteOrNonPositiveUpperBound;
            return false;
        }

        BigInteger scaledNumerator = numerator * Scale;
        BigInteger scaled = CeilingDivide(scaledNumerator, denominator);
        if (scaled <= BigInteger.Zero || scaled > long.MaxValue)
        {
            upperBound = default;
            failureReason = ProductionRecipeWorkRateMaximumGapReason
                .FixedPointOverflow;
            return false;
        }

        upperBound = new ProductionWorkRateFixedPointUpperBound((long)scaled);
        failureReason = ProductionRecipeWorkRateMaximumGapReason.None;
        return true;
    }

    internal static BigInteger CeilingDivide(
        BigInteger numerator,
        BigInteger denominator)
    {
        BigInteger quotient = BigInteger.DivRem(
            numerator,
            denominator,
            out BigInteger remainder);
        return remainder.IsZero ? quotient : quotient + BigInteger.One;
    }

    public bool Equals(ProductionWorkRateFixedPointUpperBound other) =>
        ScaledValue == other.ScaledValue;

    public override bool Equals(object obj) =>
        obj is ProductionWorkRateFixedPointUpperBound other && Equals(other);

    public override int GetHashCode() => ScaledValue.GetHashCode();
}

public sealed class ProductionWorkRateContributorManifest
{
    public const string Schema = "production-work-rate-contributor-manifest@1";

    public ProductionWorkRateContributorManifest(
        IEnumerable<string> requiredContributorIds)
    {
        string[] ordered = (requiredContributorIds
                ?? throw new ArgumentNullException(nameof(requiredContributorIds)))
            .ToArray();
        if (ordered.Length == 0)
            throw new ArgumentException(
                "At least one work-rate contributor is required.",
                nameof(requiredContributorIds));
        foreach (string id in ordered)
            ProductionAuthoredThroughputContractRules.RequireCanonical(
                id,
                nameof(requiredContributorIds));
        if (ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new ArgumentException(
                "Work-rate contributor IDs must be unique.",
                nameof(requiredContributorIds));

        Array.Sort(ordered, StringComparer.Ordinal);
        RequiredContributorIds = Array.AsReadOnly(ordered);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(ordered.Length);
        foreach (string id in ordered)
            digest.Append(id);
        SourceDigest = digest.ComputeSha256();
    }

    public IReadOnlyList<string> RequiredContributorIds { get; }
    public string SourceDigest { get; }

    public static ProductionWorkRateContributorManifest CreateCanonical() =>
        new(new[]
        {
            ProductionWorkStatPolicyMaximumContributor.StableContributorId,
            ProductionCharacterPerformanceMaximumContributor
                .StableContributorId,
            ProductionCharacterWorkContextMaximumContributor
                .StableContributorId,
            ProductionWorkEnvironmentMaximumContributor.StableContributorId,
            ProductionCraftsmanshipMaximumContributor.StableContributorId,
            ProductionFacilityEvolutionWorkRateMaximumContributor
                .StableContributorId,
            ProductionAutomationAssistedWorkMaximumContributor
                .StableContributorId
        });
}

public class ProductionWorkRateMaximumSubject
{
    public ProductionWorkRateMaximumSubject(
        string facilityDefinitionId,
        string workstationTag,
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
        WorkTypeId workTypeId,
        string operationDefinitionId,
        string operationSourceDigest)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            facilityDefinitionId,
            nameof(facilityDefinitionId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            workstationTag,
            nameof(workstationTag));
        FacilityDefinitionId = facilityDefinitionId;
        WorkstationTag = workstationTag;
        LaneProfile = laneProfile
            ?? throw new ArgumentNullException(nameof(laneProfile));
        if (!workTypeId.IsValid)
            throw new ArgumentException(
                "Work-rate subject requires a valid work type.",
                nameof(workTypeId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            operationDefinitionId,
            nameof(operationDefinitionId));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            operationSourceDigest,
            nameof(operationSourceDigest));
        WorkTypeId = workTypeId;
        OperationDefinitionId = operationDefinitionId;
        OperationSourceDigest = operationSourceDigest;
    }

    public string FacilityDefinitionId { get; }
    public string WorkstationTag { get; }
    public ProductionFacilityWorkstationLaneCapacityProfile LaneProfile { get; }
    public WorkTypeId WorkTypeId { get; }
    public string OperationDefinitionId { get; }
    public string OperationSourceDigest { get; }
}

public sealed class ProductionRecipeWorkRateMaximumContext :
    ProductionWorkRateMaximumSubject
{
    public ProductionRecipeWorkRateMaximumContext(
        string facilityDefinitionId,
        string workstationTag,
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
        ProductionRecipeSO recipe)
        : base(
            facilityDefinitionId,
            workstationTag,
            laneProfile,
            (recipe ?? throw new ArgumentNullException(nameof(recipe))).WorkTypeId,
            recipe.RecipeId,
            ProductionRecipeSemanticDigest.Capture(recipe))
    {
        Recipe = recipe;
    }

    public ProductionRecipeSO Recipe { get; }
    public string RecipeSourceDigest => OperationSourceDigest;
}

public sealed class ProductionWorkRateMaximumContributorResult
{
    private ProductionWorkRateMaximumContributorResult(
        bool hasUpperBound,
        ProductionWorkRateFixedPointUpperBound upperBound,
        ProductionRecipeWorkRateMaximumGapReason missingReason,
        string detail,
        string sourceDigest)
    {
        if (hasUpperBound != (missingReason
                == ProductionRecipeWorkRateMaximumGapReason.None)
            || hasUpperBound && !upperBound.IsValid
            || !hasUpperBound && upperBound.IsValid)
        {
            throw new ArgumentException(
                "Work-rate contributor result is invalid.");
        }
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        HasUpperBound = hasUpperBound;
        UpperBound = upperBound;
        MissingReason = missingReason;
        Detail = detail ?? string.Empty;
        SourceDigest = sourceDigest;
    }

    public bool HasUpperBound { get; }
    public ProductionWorkRateFixedPointUpperBound UpperBound { get; }
    public ProductionRecipeWorkRateMaximumGapReason MissingReason { get; }
    public string Detail { get; }
    public string SourceDigest { get; }

    public static ProductionWorkRateMaximumContributorResult Complete(
        ProductionWorkRateFixedPointUpperBound upperBound,
        string sourceDigest) => new(
        true,
        upperBound,
        ProductionRecipeWorkRateMaximumGapReason.None,
        string.Empty,
        sourceDigest);

    public static ProductionWorkRateMaximumContributorResult Missing(
        ProductionRecipeWorkRateMaximumGapReason reason,
        string detail,
        string sourceDigest)
    {
        if (reason == ProductionRecipeWorkRateMaximumGapReason.None)
            throw new ArgumentOutOfRangeException(nameof(reason));
        return new ProductionWorkRateMaximumContributorResult(
            false,
            default,
            reason,
            detail,
            sourceDigest);
    }
}

public interface IProductionRecipeWorkRateMaximumContributor
{
    string ContributorId { get; }
    ProductionWorkRateMaximumContributorResult Capture(
        ProductionWorkRateMaximumSubject context);
}

public interface IProductionAutomaticWorkRateMaximumQuery
{
    ProductionWorkRateMaximumContributorResult Capture(
        ProductionWorkRateMaximumSubject context);
}

public sealed class ProductionRecipeWorkRateMaximumAuthorityResult
{
    private ProductionRecipeWorkRateMaximumAuthorityResult(
        bool hasSnapshot,
        ProductionRecipeWorkRateMaximumSnapshot snapshot,
        ProductionRecipeWorkRateMaximumGapReason missingReason,
        string contributorId,
        string detail,
        string sourceDigest)
    {
        if (hasSnapshot != (missingReason
                == ProductionRecipeWorkRateMaximumGapReason.None))
            throw new ArgumentException("Work-rate authority result is invalid.");
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        HasSnapshot = hasSnapshot;
        Snapshot = snapshot;
        MissingReason = missingReason;
        ContributorId = contributorId ?? string.Empty;
        Detail = detail ?? string.Empty;
        SourceDigest = sourceDigest;
    }

    public bool HasSnapshot { get; }
    public ProductionRecipeWorkRateMaximumSnapshot Snapshot { get; }
    public ProductionRecipeWorkRateMaximumGapReason MissingReason { get; }
    public string ContributorId { get; }
    public string Detail { get; }
    public string SourceDigest { get; }

    internal static ProductionRecipeWorkRateMaximumAuthorityResult Complete(
        ProductionRecipeWorkRateMaximumSnapshot snapshot) => new(
        true,
        snapshot,
        ProductionRecipeWorkRateMaximumGapReason.None,
        string.Empty,
        string.Empty,
        snapshot.SourceDigest);

    internal static ProductionRecipeWorkRateMaximumAuthorityResult Missing(
        ProductionRecipeWorkRateMaximumGapReason reason,
        string contributorId,
        string detail,
        string sourceDigest) => new(
        false,
        default,
        reason,
        contributorId,
        detail,
        sourceDigest);

    internal ProductionRecipeWorkRateMaximumQueryResult ToContractResult() =>
        HasSnapshot
            ? ProductionRecipeWorkRateMaximumQueryResult.Complete(Snapshot)
            : ProductionRecipeWorkRateMaximumQueryResult.Missing(
                ProductionThroughputGapReason.RecipeWorkRateMaximumMissing,
                BuildTypedDetail(),
                SourceDigest);

    private string BuildTypedDetail()
    {
        string code = MissingReason.ToString();
        return ContributorId.Length == 0
            ? code + ":" + Detail
            : code + ":" + ContributorId + ":" + Detail;
    }
}

/// <summary>
/// Execution-free maximum query. Every manual factor must be published by a
/// declared contributor; the runtime ceiling never substitutes for a missing
/// contributor.
/// </summary>
public sealed class ProductionRecipeWorkRateMaximumAuthority :
    IProductionRecipeWorkRateMaximumQuery,
    IProductionWorkRateMaximumQuery
{
    public const string Schema = "production-recipe-work-rate-maximum@1";
    private const long MilliWuPerWu = 1_000L;

    private readonly ProductionWorkRateContributorManifest manifest;
    private readonly IReadOnlyDictionary<string,
        IProductionRecipeWorkRateMaximumContributor> contributors;
    private readonly IProductionAutomaticWorkRateMaximumQuery automaticRates;

    public ProductionRecipeWorkRateMaximumAuthority(
        ProductionWorkRateContributorManifest manifest,
        IEnumerable<IProductionRecipeWorkRateMaximumContributor> contributors,
        IProductionAutomaticWorkRateMaximumQuery automaticRates)
    {
        this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        this.automaticRates = automaticRates;

        Dictionary<string, IProductionRecipeWorkRateMaximumContributor> byId =
            new(StringComparer.Ordinal);
        HashSet<string> allowed = new(
            manifest.RequiredContributorIds,
            StringComparer.Ordinal);
        foreach (IProductionRecipeWorkRateMaximumContributor contributor in
                 contributors ?? Array.Empty<IProductionRecipeWorkRateMaximumContributor>())
        {
            if (contributor == null)
                throw new ArgumentException(
                    "A null work-rate contributor was registered.",
                    nameof(contributors));
            ProductionAuthoredThroughputContractRules.RequireCanonical(
                contributor.ContributorId,
                nameof(contributors));
            if (!allowed.Contains(contributor.ContributorId))
                throw new InvalidOperationException(
                    "An unmanifested work-rate contributor was registered: "
                    + contributor.ContributorId);
            if (!byId.TryAdd(contributor.ContributorId, contributor))
                throw new InvalidOperationException(
                    "A duplicate work-rate contributor was registered: "
                    + contributor.ContributorId);
        }
        this.contributors = byId;
    }

    public ProductionRecipeWorkRateMaximumQueryResult Capture(
        string facilityDefinitionId,
        string workstationTag,
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
        ProductionRecipeSO recipe) => CaptureDetailed(
            facilityDefinitionId,
            workstationTag,
            laneProfile,
            recipe)
        .ToContractResult();

    public ProductionRecipeWorkRateMaximumQueryResult Capture(
        ProductionWorkRateMaximumSubject subject) => CaptureDetailed(subject)
        .ToContractResult();

    public ProductionRecipeWorkRateMaximumAuthorityResult CaptureDetailed(
        string facilityDefinitionId,
        string workstationTag,
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
        ProductionRecipeSO recipe)
    {
        ProductionRecipeWorkRateMaximumContext context = new(
            facilityDefinitionId,
            workstationTag,
            laneProfile,
            recipe);
        return CaptureDetailed(context);
    }

    public ProductionRecipeWorkRateMaximumAuthorityResult CaptureDetailed(
        ProductionWorkRateMaximumSubject context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile =
            context.LaneProfile;
        if (!laneProfile.IsSpecified)
            return Missing(
                context,
                ProductionRecipeWorkRateMaximumGapReason.InvalidLaneProfile,
                string.Empty,
                "The workstation lane profile is unspecified.",
                string.Empty);

        CanonicalSemanticDigestBuilder digest = BeginDigest(context);
        BigInteger manualNumerator = BigInteger.One;
        BigInteger manualDenominator = BigInteger.One;
        foreach (string contributorId in manifest.RequiredContributorIds)
        {
            if (!contributors.TryGetValue(
                    contributorId,
                    out IProductionRecipeWorkRateMaximumContributor contributor))
            {
                return Missing(
                    context,
                    ProductionRecipeWorkRateMaximumGapReason.MissingContributor,
                    contributorId,
                    "The required work-rate contributor is not registered.",
                    string.Empty);
            }

            ProductionWorkRateMaximumContributorResult result = contributor
                .Capture(context);
            if (result == null)
            {
                return Missing(
                    context,
                    ProductionRecipeWorkRateMaximumGapReason.ContributorRejected,
                    contributorId,
                    "The contributor returned null.",
                    string.Empty);
            }
            if (!result.HasUpperBound)
            {
                return Missing(
                    context,
                    result.MissingReason,
                    contributorId,
                    result.Detail,
                    result.SourceDigest);
            }

            manualNumerator *= result.UpperBound.ScaledValue;
            manualDenominator *= ProductionWorkRateFixedPointUpperBound.Scale;
            digest.Append(contributorId);
            digest.Append(result.UpperBound.ScaledValue);
            digest.Append(result.SourceDigest);
        }

        Clamp(
            ref manualNumerator,
            ref manualDenominator,
            CaptureRuntimeBound(WorkRateBoundsAuthority.MinimumWorkPerSecond),
            CaptureRuntimeBound(WorkRateBoundsAuthority.MaximumWorkPerSecond));
        if (!TryCeilingMilliWu(
                manualNumerator,
                manualDenominator,
                out long manualMilliWu))
        {
            return Missing(
                context,
                ProductionRecipeWorkRateMaximumGapReason.FixedPointOverflow,
                string.Empty,
                "The manual work-rate maximum exceeds Int64 mWU/s.",
                string.Empty);
        }

        long automaticMilliWu = 0L;
        digest.Append((int)laneProfile.Policy);
        if (laneProfile.Policy == ProductionWorkstationLanePolicy
                .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors)
        {
            if (automaticRates == null)
            {
                return Missing(
                    context,
                    ProductionRecipeWorkRateMaximumGapReason
                        .AutomaticAuthorityMissing,
                    string.Empty,
                    "The automatic lane has no work-rate maximum query.",
                    string.Empty);
            }
            ProductionWorkRateMaximumContributorResult automatic = automaticRates
                .Capture(context);
            if (automatic == null)
            {
                return Missing(
                    context,
                    ProductionRecipeWorkRateMaximumGapReason
                        .AutomaticAuthorityMissing,
                    string.Empty,
                    "The automatic work-rate query returned null.",
                    string.Empty);
            }
            if (!automatic.HasUpperBound)
            {
                return Missing(
                    context,
                    automatic.MissingReason,
                    "automatic-rate",
                    automatic.Detail,
                    automatic.SourceDigest);
            }
            if (!TryCeilingMilliWu(
                    automatic.UpperBound.ScaledValue,
                    ProductionWorkRateFixedPointUpperBound.Scale,
                    out automaticMilliWu))
            {
                return Missing(
                    context,
                    ProductionRecipeWorkRateMaximumGapReason.FixedPointOverflow,
                    "automatic-rate",
                    "The automatic work-rate maximum exceeds Int64 mWU/s.",
                    automatic.SourceDigest);
            }
            digest.Append(automatic.UpperBound.ScaledValue);
            digest.Append(automatic.SourceDigest);
        }
        else if (laneProfile.Policy != ProductionWorkstationLanePolicy
                     .ManualWithDetachedBatchProcessors)
        {
            return Missing(
                context,
                ProductionRecipeWorkRateMaximumGapReason.InvalidLaneProfile,
                string.Empty,
                "The workstation lane policy is unsupported.",
                string.Empty);
        }
        else
        {
            digest.Append(0L);
            digest.Append("manual-only");
        }

        digest.Append(WorkRateBoundsAuthority.SourceDigest);
        digest.Append(manualMilliWu);
        digest.Append(automaticMilliWu);
        string sourceDigest = digest.ComputeSha256();
        return ProductionRecipeWorkRateMaximumAuthorityResult.Complete(
            new ProductionRecipeWorkRateMaximumSnapshot(
                manualMilliWu,
                automaticMilliWu,
                sourceDigest));
    }

    private CanonicalSemanticDigestBuilder BeginDigest(
        ProductionWorkRateMaximumSubject context)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(context.FacilityDefinitionId);
        digest.Append(context.WorkstationTag);
        digest.Append(context.LaneProfile.SourceDigest);
        digest.Append(context.WorkTypeId.Value);
        digest.Append(context.OperationDefinitionId);
        digest.Append(context.OperationSourceDigest);
        digest.Append(manifest.SourceDigest);
        digest.Append(manifest.RequiredContributorIds.Count);
        return digest;
    }

    private ProductionRecipeWorkRateMaximumAuthorityResult Missing(
        ProductionWorkRateMaximumSubject context,
        ProductionRecipeWorkRateMaximumGapReason reason,
        string contributorId,
        string detail,
        string upstreamDigest)
    {
        CanonicalSemanticDigestBuilder digest = BeginDigest(context);
        digest.Append("gap");
        digest.Append((int)reason);
        digest.Append(contributorId ?? string.Empty);
        digest.Append(detail ?? string.Empty);
        digest.Append(upstreamDigest ?? string.Empty);
        digest.Append(WorkRateBoundsAuthority.SourceDigest);
        return ProductionRecipeWorkRateMaximumAuthorityResult.Missing(
            reason,
            contributorId,
            detail,
            digest.ComputeSha256());
    }

    private static void Clamp(
        ref BigInteger numerator,
        ref BigInteger denominator,
        ProductionWorkRateFixedPointUpperBound minimum,
        ProductionWorkRateFixedPointUpperBound maximum)
    {
        BigInteger scale = ProductionWorkRateFixedPointUpperBound.Scale;
        if (numerator * scale < minimum.ScaledValue * denominator)
        {
            numerator = minimum.ScaledValue;
            denominator = scale;
        }
        else if (numerator * scale > maximum.ScaledValue * denominator)
        {
            numerator = maximum.ScaledValue;
            denominator = scale;
        }
    }

    private static ProductionWorkRateFixedPointUpperBound CaptureRuntimeBound(
        float value)
    {
        if (!ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                value,
                out ProductionWorkRateFixedPointUpperBound upperBound,
                out ProductionRecipeWorkRateMaximumGapReason reason))
        {
            throw new InvalidOperationException(
                "The shared runtime work-rate bound is invalid: " + reason);
        }
        return upperBound;
    }

    private static bool TryCeilingMilliWu(
        BigInteger numerator,
        BigInteger denominator,
        out long milliWu)
    {
        if (numerator <= BigInteger.Zero || denominator <= BigInteger.Zero)
        {
            milliWu = 0L;
            return false;
        }
        BigInteger value = ProductionWorkRateFixedPointUpperBound.CeilingDivide(
            numerator * MilliWuPerWu,
            denominator);
        if (value <= BigInteger.Zero || value > long.MaxValue)
        {
            milliWu = 0L;
            return false;
        }
        milliWu = (long)value;
        return true;
    }
}
