using System;
using System.Collections.Generic;

/// <summary>
/// Projects the same facility-evolution service-speed term used by live work
/// into the execution-free recipe throughput envelope.
/// </summary>
public sealed class ProductionFacilityEvolutionWorkRateMaximumContributor :
    IProductionRecipeWorkRateMaximumContributor
{
    public const string Schema =
        "production-facility-evolution-work-rate-maximum-contributor@1";
    public const string StableContributorId =
        "work-rate:facility-evolution-definition";

    private readonly ProductionFacilityDefinitionCatalog definitions;
    private readonly IFacilityEvolutionWorkSpeedDefinitionMaximumQuery maximums;

    public ProductionFacilityEvolutionWorkRateMaximumContributor(
        ProductionFacilityDefinitionCatalog definitions,
        IFacilityEvolutionWorkSpeedDefinitionMaximumQuery maximums)
    {
        this.definitions = definitions
            ?? throw new ArgumentNullException(nameof(definitions));
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
        FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot snapshot;
        try
        {
            BuildingSO definition = definitions.Require(
                context.FacilityDefinitionId);
            snapshot = maximums.Capture(definition, workTypeId);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is InvalidOperationException
            || exception is KeyNotFoundException)
        {
            return Missing(
                context,
                workTypeId,
                exception.GetType().FullName,
                exception.Message);
        }

        if (!string.Equals(
                snapshot.BuildingDefinitionId,
                context.FacilityDefinitionId,
                StringComparison.Ordinal))
        {
            return Missing(
                context,
                workTypeId,
                "FACILITY_ID_MISMATCH",
                snapshot.BuildingDefinitionId);
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
        digest.Append((int)snapshot.FacilityRoles);
        digest.Append(snapshot.AppliesServiceSpeed);
        digest.AppendDouble(snapshot.MaximumMultiplier);
        digest.Append(snapshot.MaximumActiveNodeCount);
        digest.Append(upperBound.ScaledValue);
        digest.Append(definitions.SourceDigest);
        digest.Append(snapshot.SourceDigest);
        return ProductionWorkRateMaximumContributorResult.Complete(
            upperBound,
            digest.ComputeSha256());
    }

    private ProductionWorkRateMaximumContributorResult Missing(
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
        digest.Append(definitions.SourceDigest);
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
