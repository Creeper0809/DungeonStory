using System;

public sealed class ProductionCharacterWorkContextMaximumContributor :
    IProductionRecipeWorkRateMaximumContributor
{
    public const string Schema =
        "production-character-work-context-maximum-contributor@1";
    public const string StableContributorId =
        "work-rate:character-context-definition";

    private readonly ICharacterWorkContextDefinitionMaximumQuery maximums;

    public ProductionCharacterWorkContextMaximumContributor(
        ICharacterWorkContextDefinitionMaximumQuery maximums)
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
        CharacterWorkContextDefinitionMaximumSnapshot snapshot;
        try
        {
            snapshot = maximums.Capture(workTypeId);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is InvalidOperationException)
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
        digest.AppendDouble(snapshot.ResearchSharedMaximum);
        digest.AppendDouble(snapshot.FatigueMaximum);
        digest.AppendDouble(snapshot.DiscontentMaximum);
        digest.AppendDouble(snapshot.TransientSkillMaximum);
        digest.AppendDouble(snapshot.DeprivationMaximum);
        digest.AppendDouble(snapshot.SubstanceMaximum);
        digest.AppendDouble(snapshot.ExposureMaximum);
        digest.AppendDouble(snapshot.EquipmentBurdenMaximum);
        digest.AppendDouble(snapshot.ContentDelayMaximum);
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
