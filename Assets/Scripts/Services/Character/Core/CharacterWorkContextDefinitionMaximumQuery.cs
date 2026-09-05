using System;

public readonly struct CharacterWorkContextDefinitionMaximumSnapshot
{
    public CharacterWorkContextDefinitionMaximumSnapshot(
        WorkTypeId workTypeId,
        double researchSharedMaximum,
        double fatigueMaximum,
        double discontentMaximum,
        double transientSkillMaximum,
        double deprivationMaximum,
        double substanceMaximum,
        double exposureMaximum,
        double equipmentBurdenMaximum,
        double contentDelayMaximum,
        double maximumMultiplier,
        string sourceDigest)
    {
        if (!workTypeId.IsValid
            || !PositiveFinite(researchSharedMaximum)
            || !PositiveFinite(fatigueMaximum)
            || !PositiveFinite(discontentMaximum)
            || !PositiveFinite(transientSkillMaximum)
            || !PositiveFinite(deprivationMaximum)
            || !PositiveFinite(substanceMaximum)
            || !PositiveFinite(exposureMaximum)
            || !PositiveFinite(equipmentBurdenMaximum)
            || !PositiveFinite(contentDelayMaximum)
            || !PositiveFinite(maximumMultiplier)
            || !LowercaseSha256(sourceDigest))
        {
            throw new ArgumentException(
                "Character work-context definition maximum is invalid.");
        }
        WorkTypeId = workTypeId;
        ResearchSharedMaximum = researchSharedMaximum;
        FatigueMaximum = fatigueMaximum;
        DiscontentMaximum = discontentMaximum;
        TransientSkillMaximum = transientSkillMaximum;
        DeprivationMaximum = deprivationMaximum;
        SubstanceMaximum = substanceMaximum;
        ExposureMaximum = exposureMaximum;
        EquipmentBurdenMaximum = equipmentBurdenMaximum;
        ContentDelayMaximum = contentDelayMaximum;
        MaximumMultiplier = maximumMultiplier;
        SourceDigest = sourceDigest;
    }

    public WorkTypeId WorkTypeId { get; }
    public double ResearchSharedMaximum { get; }
    public double FatigueMaximum { get; }
    public double DiscontentMaximum { get; }
    public double TransientSkillMaximum { get; }
    public double DeprivationMaximum { get; }
    public double SubstanceMaximum { get; }
    public double ExposureMaximum { get; }
    public double EquipmentBurdenMaximum { get; }
    public double ContentDelayMaximum { get; }
    public double MaximumMultiplier { get; }
    public string SourceDigest { get; }

    private static bool PositiveFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;

    private static bool LowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        foreach (char character in value)
        {
            if ((character < '0' || character > '9')
                && (character < 'a' || character > 'f'))
                return false;
        }
        return true;
    }
}

public interface ICharacterWorkContextDefinitionMaximumQuery
{
    CharacterWorkContextDefinitionMaximumSnapshot Capture(
        WorkTypeId workTypeId);
}

/// <summary>
/// Execution-free upper envelope for the exact nine-factor context product in
/// CharacterStatsProjectionService.GetWorkContextMultiplier.
/// </summary>
public sealed class CharacterWorkContextDefinitionMaximumQuery :
    ICharacterWorkContextDefinitionMaximumQuery
{
    public const string Schema =
        "character-work-context-definition-maximum@1";

    private readonly IGameplayEffectResultBoundsQuery effectBounds;

    public CharacterWorkContextDefinitionMaximumQuery(
        IGameplayEffectResultBoundsQuery effectBounds)
    {
        this.effectBounds = effectBounds
            ?? throw new ArgumentNullException(nameof(effectBounds));
    }

    public CharacterWorkContextDefinitionMaximumSnapshot Capture(
        WorkTypeId workTypeId)
    {
        if (!WorkTypeCatalog.TryGet(
                workTypeId,
                out WorkTypeDefinition workDefinition))
        {
            throw new InvalidOperationException(
                "Unknown work type has no character-context maximum: "
                + workTypeId.Value);
        }

        GameplayEffectResultBoundsSnapshot researchBounds = default;
        double researchSharedMaximum = 1d;
        if (workDefinition.WorkTypeId == BuiltInWorkTypeIds.Research)
        {
            researchBounds = effectBounds.CaptureFiniteBounds(
                GameplayEffectTargetIds.ResearchSpeed);
            researchSharedMaximum = CharacterIncrementalGameplayEffectAuthority
                .ResolveAbsoluteMaximum(researchBounds.AbsoluteMaximum);
        }

        double fatigueMaximum = CharacterFatigueWorkSpeedAuthority
            .MaximumMultiplier;
        double discontentMaximum = StaffDiscontentWorkSpeedAuthority
            .MaximumMultiplier;
        double transientSkillMaximum = CharacterSkillWorkSpeedAuthority
            .MaximumRuntimeMultiplier;
        double deprivationMaximum = CharacterDeprivationWorkSpeedAuthority
            .MaximumMultiplier;
        double substanceMaximum = CharacterSubstanceEffectMultiplierAuthority
            .MaximumMultiplier;
        double exposureMaximum = CharacterExposureWorkSpeedAuthority
            .MaximumMultiplier;
        double equipmentBurdenMaximum =
            CharacterEquipmentBurdenWorkSpeedAuthority.MaximumMultiplier;
        double contentDelayMaximum = ContentWorkDelaySpeedAuthority
            .MaximumMultiplier;

        double maximum = researchSharedMaximum
            * fatigueMaximum
            * discontentMaximum
            * transientSkillMaximum
            * deprivationMaximum
            * substanceMaximum
            * exposureMaximum
            * equipmentBurdenMaximum
            * contentDelayMaximum;
        if (double.IsNaN(maximum)
            || double.IsInfinity(maximum)
            || maximum <= 0d)
        {
            throw new InvalidOperationException(
                "Character work-context maximum is not finite and positive.");
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(workDefinition.WorkTypeId.Value);
        digest.Append(GameplayEffectTargetIds.ResearchSpeed);
        digest.Append(CharacterIncrementalGameplayEffectAuthority.Schema);
        digest.AppendFloat(
            CharacterIncrementalGameplayEffectAuthority
                .EmbeddedNeutralThreshold);
        digest.Append(workDefinition.WorkTypeId == BuiltInWorkTypeIds.Research);
        if (workDefinition.WorkTypeId == BuiltInWorkTypeIds.Research)
        {
            digest.AppendFloat(researchBounds.Minimum);
            digest.AppendFloat(researchBounds.Maximum);
            digest.Append(researchBounds.SourceDigest);
        }
        digest.AppendDouble(researchSharedMaximum);
        digest.Append(CharacterFatigueWorkSpeedAuthority.Schema);
        digest.AppendFloat(CharacterFatigueWorkSpeedAuthority.MinimumMultiplier);
        digest.AppendFloat(CharacterFatigueWorkSpeedAuthority.MaximumMultiplier);
        digest.AppendFloat(CharacterFatigueWorkSpeedAuthority.RestedStatValue);
        digest.Append(StaffDiscontentWorkSpeedAuthority.Schema);
        digest.AppendFloat(StaffDiscontentWorkSpeedAuthority.MaximumMultiplier);
        digest.Append(CharacterSkillWorkSpeedAuthority.Schema);
        digest.AppendFloat(
            CharacterSkillWorkSpeedAuthority.MinimumRuntimeMultiplier);
        digest.AppendFloat(
            CharacterSkillWorkSpeedAuthority.MaximumAuthoredBonus);
        digest.AppendFloat(
            CharacterSkillWorkSpeedAuthority.MaximumRuntimeMultiplier);
        digest.Append(CharacterDeprivationWorkSpeedAuthority.Schema);
        digest.AppendFloat(
            CharacterDeprivationWorkSpeedAuthority.ExhaustionPenaltyPerPoint);
        digest.AppendFloat(
            CharacterDeprivationWorkSpeedAuthority
                .HungerAndThirstPenaltyPerPoint);
        digest.AppendFloat(
            CharacterDeprivationWorkSpeedAuthority.MinimumMultiplier);
        digest.AppendFloat(
            CharacterDeprivationWorkSpeedAuthority.MaximumMultiplier);
        digest.Append(CharacterSubstanceEffectMultiplierAuthority.Schema);
        digest.AppendDouble(
            CharacterSubstanceEffectMultiplierAuthority
                .WithdrawalPenaltyPerPoint);
        digest.AppendDouble(
            CharacterSubstanceEffectMultiplierAuthority.MinimumMultiplier);
        digest.AppendDouble(
            CharacterSubstanceEffectMultiplierAuthority.MaximumMultiplier);
        digest.Append(CharacterExposureWorkSpeedAuthority.Schema);
        digest.Append(
            CharacterExposureWorkSpeedAuthority.UsesPrecisionProjection(
                workDefinition.WorkTypeId));
        digest.Append(CharacterEquipmentBurdenWorkSpeedAuthority.Schema);
        digest.AppendFloat(CharacterCarryTuning.NominalBaseCapacityKilograms);
        digest.AppendFloat(
            CharacterEquipmentBurdenWorkSpeedAuthority
                .MinimumFunctionalCapacityKilograms);
        digest.AppendFloat(
            CharacterEquipmentBurdenWorkSpeedAuthority
                .MinimumPositiveCapacityKilograms);
        digest.AppendFloat(
            CharacterEquipmentBurdenWorkSpeedAuthority.OverloadThresholdRatio);
        digest.AppendFloat(
            CharacterEquipmentBurdenWorkSpeedAuthority
                .OverloadPenaltyPerRatio);
        digest.Append(ContentWorkDelaySpeedAuthority.Schema);
        digest.AppendFloat(
            ContentWorkDelaySpeedAuthority.PerActiveDelayMultiplier);
        digest.AppendFloat(ContentWorkDelaySpeedAuthority.MinimumMultiplier);
        digest.AppendFloat(ContentWorkDelaySpeedAuthority.MaximumMultiplier);
        digest.AppendDouble(maximum);

        return new CharacterWorkContextDefinitionMaximumSnapshot(
            workDefinition.WorkTypeId,
            researchSharedMaximum,
            fatigueMaximum,
            discontentMaximum,
            transientSkillMaximum,
            deprivationMaximum,
            substanceMaximum,
            exposureMaximum,
            equipmentBurdenMaximum,
            contentDelayMaximum,
            maximum,
            digest.ComputeSha256());
    }
}
