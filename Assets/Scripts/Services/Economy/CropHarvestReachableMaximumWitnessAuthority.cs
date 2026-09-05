using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CropHarvestReachableMaximumWitnessSnapshot
{
    public CropHarvestReachableMaximumWitnessSnapshot(
        string witnessId,
        string speciesId,
        IReadOnlyList<int> traitIds,
        IReadOnlyList<string> conditionIds,
        float workerYieldMultiplier,
        float returnedSeedMultiplier,
        string sourceDigest)
    {
        RequireCanonical(witnessId, nameof(witnessId));
        RequireCanonical(speciesId, nameof(speciesId));
        int[] canonicalTraits = (traitIds
                ?? throw new ArgumentNullException(nameof(traitIds)))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        string[] canonicalConditions = (conditionIds
                ?? throw new ArgumentNullException(nameof(conditionIds)))
            .Select(value => RequireCanonical(value, nameof(conditionIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (canonicalTraits.Length == 0
            || canonicalTraits.Length > 4
            || canonicalTraits.Any(value => value <= 0)
            || !FinitePositive(workerYieldMultiplier)
            || !FinitePositive(returnedSeedMultiplier)
            || sourceDigest == null
            || sourceDigest.Length != 64)
        {
            throw new ArgumentException(
                "Crop harvest reachable-maximum witness is invalid.");
        }
        WitnessId = witnessId;
        SpeciesId = speciesId;
        TraitIds = Array.AsReadOnly(canonicalTraits);
        ConditionIds = Array.AsReadOnly(canonicalConditions);
        WorkerYieldMultiplier = workerYieldMultiplier;
        ReturnedSeedMultiplier = returnedSeedMultiplier;
        SourceDigest = sourceDigest;
    }

    public string WitnessId { get; }
    public string SpeciesId { get; }
    public IReadOnlyList<int> TraitIds { get; }
    public IReadOnlyList<string> ConditionIds { get; }
    public float WorkerYieldMultiplier { get; }
    public float ReturnedSeedMultiplier { get; }
    public string SourceDigest { get; }

    private static string RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical crop witness token is required.",
                parameterName);
        }
        return value;
    }

    private static bool FinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
}

public interface ICropHarvestReachableMaximumWitnessContributor
{
    string ContributorId { get; }
    int ContractVersion { get; }
    CropHarvestReachableMaximumWitnessSnapshot Capture();
}

public sealed class CropGenomeReachableMaximumWitnessSnapshot
{
    internal CropGenomeReachableMaximumWitnessSnapshot(
        CropGenomeDefinitionSO definition,
        CropGenomePhenotype phenotype,
        string sourceDigest)
    {
        Definition = definition
            ?? throw new ArgumentNullException(nameof(definition));
        GenomeId = definition.GenomeId;
        CropId = definition.CropId;
        if (string.IsNullOrWhiteSpace(GenomeId)
            || string.IsNullOrWhiteSpace(CropId)
            || sourceDigest == null
            || sourceDigest.Length != 64)
        {
            throw new ArgumentException(
                "Crop genome reachable-maximum witness is invalid.");
        }
        Phenotype = phenotype;
        SourceDigest = sourceDigest;
    }

    public CropGenomeDefinitionSO Definition { get; }
    public string GenomeId { get; }
    public string CropId { get; }
    public CropGenomePhenotype Phenotype { get; }
    public float YieldMultiplier => Phenotype.YieldMultiplier;
    public float GrowthMultiplier => Phenotype.GrowthMultiplier;
    public string SourceDigest { get; }

    public SeedLotState CreatePhysicalSeedLot() => new()
    {
        cropId = CropId,
        cultivarGenomeId = GenomeId,
        generation = 0,
        pathogenLoad = 0f
    };
}

/// <summary>
/// Captures the strongest currently authored, runtime-registered cultivar for
/// every crop. Both capacity calculation and natural execution consume this
/// catalog, so a theoretical phenotype constant cannot exceed the physical
/// seed lot that the production path can actually sow.
/// </summary>
public sealed class CropGenomeReachableMaximumWitnessCatalog
{
    public const string Schema =
        "crop-genome-reachable-maximum-witness@1";

    private readonly IReadOnlyDictionary<string,
        CropGenomeReachableMaximumWitnessSnapshot> byCropId;

    public CropGenomeReachableMaximumWitnessCatalog(
        IGameContentDefinitionSource content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        CropDefinitionSO[] crops = content.GetAll<CropDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.CropId, StringComparer.Ordinal)
            .ToArray();
        CropGenomeDefinitionSO[] genomes = content
            .GetAll<CropGenomeDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.GenomeId, StringComparer.Ordinal)
            .ToArray();
        if (crops.Length == 0
            || crops.Select(value => value.CropId)
                .Distinct(StringComparer.Ordinal).Count() != crops.Length
            || genomes.Select(value => value.GenomeId)
                .Distinct(StringComparer.Ordinal).Count() != genomes.Length)
        {
            throw new InvalidOperationException(
                "Crop genome witness content is empty or ambiguous.");
        }

        Dictionary<string, CropGenomeReachableMaximumWitnessSnapshot>
            captured = new(StringComparer.Ordinal);
        foreach (CropDefinitionSO crop in crops)
        {
            CropGenomeCandidate[] candidates = genomes
                .Where(value => string.Equals(
                    value.CropId,
                    crop.CropId,
                    StringComparison.Ordinal))
                .Select(value => new CropGenomeCandidate(value))
                .OrderByDescending(value => value.Phenotype.YieldMultiplier)
                .ThenByDescending(value => value.Phenotype.GrowthMultiplier)
                .ThenByDescending(value => value.Phenotype.SeedYieldBonus)
                .ThenBy(value => value.Definition.GenomeId,
                    StringComparer.Ordinal)
                .ToArray();
            if (crop.BaseGenome == null
                || !string.Equals(
                    crop.BaseGenome.CropId,
                    crop.CropId,
                    StringComparison.Ordinal)
                || !candidates.Any(value => ReferenceEquals(
                    value.Definition,
                    crop.BaseGenome))
                || candidates.Length == 0)
            {
                throw new InvalidOperationException(
                    "Crop has no runtime-registered authored genome witness: "
                    + crop.CropId);
            }

            CropGenomeCandidate selected = candidates[0];
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append(Schema);
            digest.Append(crop.CropId);
            digest.Append(crop.BaseGenome.GenomeId);
            digest.Append(candidates.Length);
            foreach (CropGenomeCandidate candidate in candidates.OrderBy(
                         value => value.Definition.GenomeId,
                         StringComparer.Ordinal))
            {
                CultivarGenomeSaveData genome = candidate.Genome;
                digest.Append(candidate.Definition.GenomeId);
                digest.Append(candidate.Definition.AuthoringRevision);
                foreach (DiploidLocusSaveData locus in genome.loci
                             .OrderBy(value => value.locus))
                {
                    digest.AppendEnum(locus.locus);
                    digest.Append(locus.alleleA);
                    digest.Append(locus.alleleB);
                }
            }
            digest.Append(selected.Definition.GenomeId);
            digest.AppendFloat(selected.Phenotype.YieldMultiplier);
            digest.AppendFloat(selected.Phenotype.GrowthMultiplier);
            digest.Append(selected.Phenotype.SeedYieldBonus);
            captured.Add(
                crop.CropId,
                new CropGenomeReachableMaximumWitnessSnapshot(
                    selected.Definition,
                    selected.Phenotype,
                    digest.ComputeSha256()));
        }
        byCropId = captured;
    }

    public CropGenomeReachableMaximumWitnessSnapshot Capture(string cropId)
    {
        if (string.IsNullOrWhiteSpace(cropId)
            || !string.Equals(cropId, cropId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical crop ID is required.",
                nameof(cropId));
        }
        if (!byCropId.TryGetValue(
                cropId,
                out CropGenomeReachableMaximumWitnessSnapshot witness))
        {
            throw new InvalidOperationException(
                "Crop has no reachable maximum genome witness: " + cropId);
        }
        return witness;
    }

    private sealed class CropGenomeCandidate
    {
        internal CropGenomeCandidate(CropGenomeDefinitionSO definition)
        {
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            IReadOnlyList<string> errors = definition.ValidateDefinition();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Invalid authored crop genome '" + definition.GenomeId
                    + "': " + string.Join(" | ", errors));
            }
            Genome = definition.CreateRuntimeDefinition();
            Phenotype = CropGenomePhenotypeAuthority.Create(Genome);
        }

        internal CropGenomeDefinitionSO Definition { get; }
        internal CultivarGenomeSaveData Genome { get; }
        internal CropGenomePhenotype Phenotype { get; }
    }
}

/// <summary>
/// Current-content natural actor witness. It intentionally excludes installed
/// anatomy modules, equipment and transient status effects. Those domains can
/// contribute another independently executable witness without changing crop
/// capacity or measurement orchestration.
/// </summary>
public sealed class NaturalGoldenHarvestReachableMaximumWitnessContributor :
    ICropHarvestReachableMaximumWitnessContributor
{
    public const string Id =
        "crop-harvest-reachable-witness:natural-golden-master";
    public const int Version = 1;
    public const string WitnessId = "natural:beastkin:golden-master";
    public const string SpeciesTag = "Beastkin";
    public const int GoldenHarvestTraitId = 304;
    public const string GoldenHarvestConditionId =
        "state:golden-harvest-jackpot";

    private readonly IGameContentDefinitionSource content;
    private readonly CharacterPerformanceFormulaCatalog formulas;

    public NaturalGoldenHarvestReachableMaximumWitnessContributor(
        IGameContentDefinitionSource content,
        CharacterPerformanceFormulaCatalog formulas)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.formulas = formulas ?? throw new ArgumentNullException(nameof(formulas));
    }

    public string ContributorId => Id;
    public int ContractVersion => Version;

    public CropHarvestReachableMaximumWitnessSnapshot Capture()
    {
        CharacterPerformanceFormulaDefinitionSO formula = formulas.Require(
            CropHarvestOutputRules.PerformanceFormulaId);
        CharacterSpeciesSO species = content.GetAll<CharacterSpeciesSO>()
            .Single(value => value != null
                && string.Equals(
                    value.speciesTag,
                    SpeciesTag,
                    StringComparison.Ordinal));
        CharacterTraitSO trait = content.GetAll<CharacterTraitSO>()
            .Single(value => value != null
                && value.id == GoldenHarvestTraitId);
        if (!trait.IsEligibleForSpecies(SpeciesTag))
        {
            throw new InvalidOperationException(
                "Golden Harvest witness is not eligible for Beastkin.");
        }

        IGameplayEffectSource[] sources = { species, trait };
        GameplayEffectContext context = new(
            new[] { GoldenHarvestConditionId });
        double weightedTotal = 0d;
        double totalWeight = 0d;
        double bottleneck = double.PositiveInfinity;
        CharacterPerformanceCapacityInput[] inputs = formula.CapacityInputs
            .Where(value => value != null)
            .OrderBy(value => value.CapacityId)
            .ToArray();
        if (inputs.Length != formula.CapacityInputs.Count)
        {
            throw new InvalidOperationException(
                "Crop witness formula contains a null capacity input.");
        }
        Dictionary<CharacterFunctionalCapacityId, float> capacities = new();
        foreach (CharacterPerformanceCapacityInput input in inputs)
        {
            string targetId = CharacterFunctionalCapacityIds.GetStableId(
                input.CapacityId);
            float value = CharacterGameplayEffectProjector.Resolve(
                targetId,
                1f,
                sources,
                context).Value;
            capacities[input.CapacityId] = value;
            if ((input.Role & CharacterPerformanceInputRole.Required) != 0)
            {
                float threshold = input.RequiredThreshold > 0f
                    ? input.RequiredThreshold
                    : 0.10f;
                if (value < threshold)
                {
                    throw new InvalidOperationException(
                        "Crop witness does not satisfy required capacity '"
                        + targetId + "'.");
                }
            }
            if ((input.Role & CharacterPerformanceInputRole.Contribution) != 0
                && input.Weight > 0f)
            {
                weightedTotal += value * input.Weight;
                totalWeight += input.Weight;
            }
            if ((input.Role & CharacterPerformanceInputRole.Bottleneck) != 0)
                bottleneck = Math.Min(bottleneck, 0.25d + 0.75d * value);
        }
        if (totalWeight <= 0d)
        {
            throw new InvalidOperationException(
                "Crop witness formula has no weighted capacity input.");
        }
        double capacityFactor = Math.Min(
            weightedTotal / totalWeight,
            bottleneck);
        CharacterProficiencyEffectSnapshot master =
            ProficiencyProgressionRules.ResolveEffects(
                ProficiencyProgressionRules.MasterCurrentCap);
        double primary = CharacterPerformanceProficiencyFactorAuthority.Resolve(
            formula.ResultChannel,
            master);
        double secondary = string.IsNullOrEmpty(formula.SecondaryProficiencyId)
            ? 1d
            : primary;
        double proficiencyFactor = primary
            * (1d - formula.SecondaryProficiencyWeight)
            + secondary * formula.SecondaryProficiencyWeight;
        float effectFactor = string.IsNullOrEmpty(formula.GameplayEffectTargetId)
            ? 1f
            : CharacterGameplayEffectProjector.Resolve(
                formula.GameplayEffectTargetId,
                1f,
                sources,
                context).Value;
        float workerYield = checked((float)(
            formula.BaseValue
            * capacityFactor
            * proficiencyFactor
            * effectFactor));
        float seedMultiplier = CharacterGameplayEffectProjector.Resolve(
            CropHarvestOutputRules.SeedYieldEffectTargetId,
            1f,
            sources,
            context).Value;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-harvest-reachable-maximum-witness@1");
        digest.Append(Id);
        digest.Append(Version);
        digest.Append(WitnessId);
        digest.Append(species.DefinitionId.Value);
        digest.Append(trait.DefinitionId.Value);
        digest.Append(GoldenHarvestConditionId);
        digest.Append(formula.FormulaId);
        digest.AppendEnum(formula.ResultChannel);
        digest.AppendFloat(formula.BaseValue);
        digest.Append(inputs.Length);
        foreach (CharacterPerformanceCapacityInput input in inputs)
        {
            digest.AppendEnum(input.CapacityId);
            digest.AppendFloat(input.Weight);
            digest.AppendEnum(input.Role);
            digest.AppendFloat(input.RequiredThreshold);
            digest.AppendFloat(capacities[input.CapacityId]);
        }
        digest.Append(formula.PrimaryProficiencyId);
        digest.Append(formula.SecondaryProficiencyId);
        digest.AppendFloat(formula.SecondaryProficiencyWeight);
        digest.Append(formula.GameplayEffectTargetId);
        AppendSource(digest, species);
        AppendSource(digest, trait);
        digest.Append(ProficiencyProgressionRules.MasterCurrentCap);
        digest.AppendDouble(capacityFactor);
        digest.AppendDouble(proficiencyFactor);
        digest.AppendFloat(effectFactor);
        digest.AppendFloat(workerYield);
        digest.AppendFloat(seedMultiplier);
        return new CropHarvestReachableMaximumWitnessSnapshot(
            WitnessId,
            species.DefinitionId.Value,
            new[] { trait.id },
            new[] { GoldenHarvestConditionId },
            workerYield,
            seedMultiplier,
            digest.ComputeSha256());
    }

    private static void AppendSource(
        CanonicalSemanticDigestBuilder digest,
        IGameplayEffectSource source)
    {
        digest.AppendEnum(source.SourceRef.Kind);
        digest.Append(source.SourceRef.SourceId);
        GameplayEffectBinding[] bindings = (source.Effects
                ?? Array.Empty<GameplayEffectBinding>())
            .Where(value => value != null)
            .OrderBy(value => value.bindingId, StringComparer.Ordinal)
            .ToArray();
        digest.Append(bindings.Length);
        foreach (GameplayEffectBinding binding in bindings)
        {
            digest.Append(binding.bindingId);
            digest.Append(binding.definition?.EffectId);
            digest.Append(binding.definition?.TargetId);
            digest.AppendFloat(binding.value);
            digest.Append(binding.condition?.ConditionId);
        }
    }
}
