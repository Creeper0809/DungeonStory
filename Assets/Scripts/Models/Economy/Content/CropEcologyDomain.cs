using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CropFamilyGroup
{
    Grain = 0,
    Fiber = 1,
    Root = 2,
    Leaf = 3,
    Vine = 4,
    Fungus = 5
}

public enum CropDiseaseKind
{
    None = 0,
    GrainFiberRust = 1,
    RootRot = 2,
    LeafVinePowderyMildew = 3,
    MushroomSporeMold = 4
}

public enum CropGenomeLocus
{
    ColdTolerance = 0,
    HeatTolerance = 1,
    GrowthSpeed = 2,
    Yield = 3,
    DiseaseResistance = 4,
    SeedYield = 5
}

public readonly struct CropGenomePhenotype
{
    public CropGenomePhenotype(
        float coldToleranceDegrees,
        float heatToleranceDegrees,
        float growthMultiplier,
        float yieldMultiplier,
        float diseaseRiskMultiplier,
        int seedYieldBonus)
    {
        ColdToleranceDegrees = Mathf.Clamp(coldToleranceDegrees, -5f, 5f);
        HeatToleranceDegrees = Mathf.Clamp(heatToleranceDegrees, -5f, 5f);
        GrowthMultiplier = Mathf.Clamp(growthMultiplier, 0.84f, 1.16f);
        YieldMultiplier = Mathf.Clamp(yieldMultiplier, 0.90f, 1.10f);
        DiseaseRiskMultiplier = Mathf.Clamp(diseaseRiskMultiplier, 0.76f, 1.24f);
        SeedYieldBonus = Mathf.Clamp(seedYieldBonus, -1, 1);
    }

    public float ColdToleranceDegrees { get; }
    public float HeatToleranceDegrees { get; }
    public float GrowthMultiplier { get; }
    public float YieldMultiplier { get; }
    public float DiseaseRiskMultiplier { get; }
    public int SeedYieldBonus { get; }
}

/// <summary>
/// Single phenotype projection shared by the live ecology runtime and the
/// reachable-maximum capacity witness. A capacity proof may only claim the
/// factors produced from an actual registered genome payload.
/// </summary>
public static class CropGenomePhenotypeAuthority
{
    public static CropGenomePhenotype Create(CultivarGenomeSaveData genome)
    {
        if (genome?.loci == null
            || genome.loci.Count != Enum.GetValues(typeof(CropGenomeLocus)).Length
            || genome.loci.Select(value => value.locus).Distinct().Count()
                != genome.loci.Count)
        {
            throw new InvalidOperationException(
                "A complete, distinct crop genome is required.");
        }

        float cold = GetLocusMean(genome, CropGenomeLocus.ColdTolerance);
        float heat = GetLocusMean(genome, CropGenomeLocus.HeatTolerance);
        float growth = GetLocusMean(genome, CropGenomeLocus.GrowthSpeed);
        float yield = GetLocusMean(genome, CropGenomeLocus.Yield);
        float disease = GetLocusMean(genome, CropGenomeLocus.DiseaseResistance);
        float seeds = GetLocusMean(genome, CropGenomeLocus.SeedYield);
        return new CropGenomePhenotype(
            cold * 2.5f,
            heat * 2.5f,
            1f + growth * 0.08f,
            1f + yield * 0.05f,
            1f - disease * 0.12f,
            Mathf.RoundToInt(seeds * 0.5f));
    }

    private static float GetLocusMean(
        CultivarGenomeSaveData genome,
        CropGenomeLocus locus)
    {
        DiploidLocusSaveData value = genome.loci.Single(entry =>
            entry.locus == locus);
        return (value.alleleA + value.alleleB) * 0.5f;
    }
}

[Serializable]
public sealed class DiploidLocusSaveData
{
    public CropGenomeLocus locus;
    [Range(-2, 2)] public int alleleA;
    [Range(-2, 2)] public int alleleB;
}

[Serializable]
public sealed class CultivarGenomeSaveData
{
    public string genomeId = string.Empty;
    public string cropId = string.Empty;
    public int generation;
    public List<DiploidLocusSaveData> loci = new();
}

[Serializable]
public sealed class SeedLotState
{
    public string cropId = string.Empty;
    public string cultivarGenomeId = string.Empty;
    public int generation;
    [Range(0f, 100f)] public float pathogenLoad;

    public SeedLotState Clone() => new()
    {
        cropId = cropId,
        cultivarGenomeId = cultivarGenomeId,
        generation = generation,
        pathogenLoad = pathogenLoad
    };
}

public static class SeedLotItemStateCodec
{
    public const string ComponentTypeId = "item-state:seed-lot";
    public const int SchemaVersion = 2;

    public static ItemInstanceComponentSaveData Encode(SeedLotState state)
    {
        Validate(state);
        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ComponentTypeId,
            schemaVersion = SchemaVersion,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                Text("crop-id", state.cropId),
                Text("genome-id", state.cultivarGenomeId),
                Integer("generation", state.generation),
                Decimal("pathogen-load", state.pathogenLoad)
            }
        };
    }

    public static SeedLotState Decode(IEnumerable<ItemInstanceComponentSaveData> components)
    {
        ItemInstanceComponentSaveData component = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .SingleOrDefault(value => value != null
                && string.Equals(value.componentTypeId, ComponentTypeId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Physical seed lot has no seed-lot state component.");
        if (component.schemaVersion != SchemaVersion)
            throw new InvalidOperationException(
                $"Seed-lot component version {component.schemaVersion} is unsupported.");
        Dictionary<string, ItemStateValueSaveData> fields = (component.values ?? new())
            .ToDictionary(value => value.key, StringComparer.Ordinal);
        SeedLotState state = new()
        {
            cropId = Require(fields, "crop-id", ItemStateValueKind.String).stringValue,
            cultivarGenomeId = Require(fields, "genome-id", ItemStateValueKind.String).stringValue,
            generation = checked((int)Require(fields, "generation", ItemStateValueKind.Integer).integerValue),
            pathogenLoad = (float)Require(fields, "pathogen-load", ItemStateValueKind.Decimal).decimalValue
        };
        Validate(state);
        return state;
    }

    private static void Validate(SeedLotState state)
    {
        if (state == null
            || string.IsNullOrWhiteSpace(state.cropId)
            || string.IsNullOrWhiteSpace(state.cultivarGenomeId)
            || state.generation < 0
            || state.pathogenLoad is < 0f or > 100f)
            throw new InvalidOperationException("Seed-lot state is invalid.");
    }

    private static ItemStateValueSaveData Require(
        IReadOnlyDictionary<string, ItemStateValueSaveData> fields,
        string key,
        ItemStateValueKind kind)
    {
        if (!fields.TryGetValue(key, out ItemStateValueSaveData value) || value.kind != kind)
            throw new InvalidOperationException($"Seed-lot component field '{key}' is missing or invalid.");
        return value;
    }

    private static ItemStateValueSaveData Text(string key, string value) => new()
    {
        key = key, kind = ItemStateValueKind.String, stringValue = value?.Trim() ?? string.Empty
    };
    private static ItemStateValueSaveData Integer(string key, long value) => new()
    {
        key = key, kind = ItemStateValueKind.Integer, integerValue = value
    };
    private static ItemStateValueSaveData Decimal(string key, double value) => new()
    {
        key = key, kind = ItemStateValueKind.Decimal, decimalValue = value
    };
}

[Serializable]
public sealed class CropEcologyPlotSaveData
{
    public string plotId = string.Empty;
    public string cropId = string.Empty;
    public string cultivarGenomeId = string.Empty;
    public CropFamilyGroup currentGroup;
    public CropFamilyGroup previousGroup;
    public bool hasPreviousGroup;
    [Range(0f, 100f)] public float fertility = 100f;
    [Range(0f, 100f)] public float pestPressure;
    [Range(0f, 100f)] public float diseasePressure;
    public CropDiseaseKind disease;
    public int consecutiveLethalTemperatureDays;
    public bool cropDead;
}

[Serializable]
public sealed class CropEcologyWorldSaveData
{
    public const int CurrentVersion = 3;
    public int version = CurrentVersion;
    public bool initialSeedGrantIssued;
    public List<CropEcologyPlotSaveData> plots = new();
    public List<CultivarGenomeSaveData> activeCultivars = new();
    public List<CultivarGenomeSaveData> frozenCultivars = new();
    public List<CropEcologyPreparedHarvestSaveData> preparedHarvests = new();
}

public readonly struct CropHarvestEcologyResult
{
    public CropHarvestEcologyResult(float yieldMultiplier, int returnedSeedCount, SeedLotState seeds)
    {
        YieldMultiplier = Mathf.Clamp(yieldMultiplier, 0f, 3f);
        ReturnedSeedCount = Mathf.Clamp(returnedSeedCount, 0, 4);
        ReturnedSeedLot = seeds;
    }
    public float YieldMultiplier { get; }
    public int ReturnedSeedCount { get; }
    public SeedLotState ReturnedSeedLot { get; }
}

[Serializable]
public sealed class CropEcologyPreparedHarvestSaveData
{
    public string operationId = string.Empty;
    public string plotId = string.Empty;
    public CropEcologyPlotSaveData plotBefore;
    public string plotBeforeFingerprint = string.Empty;
    public float yieldMultiplier;
    public int returnedSeedCount;
    public SeedLotState returnedSeedLot;
    public CultivarGenomeSaveData generatedGenome;
    public CropEcologyPlotSaveData plotAfter;
    public string outcomeFingerprint = string.Empty;
    public bool committed;

    public CropEcologyPreparedHarvestSaveData Clone() => new()
    {
        operationId = operationId ?? string.Empty,
        plotId = plotId ?? string.Empty,
        plotBefore = ClonePlot(plotBefore),
        plotBeforeFingerprint = plotBeforeFingerprint ?? string.Empty,
        yieldMultiplier = yieldMultiplier,
        returnedSeedCount = returnedSeedCount,
        returnedSeedLot = returnedSeedLot?.Clone(),
        generatedGenome = CloneGenome(generatedGenome),
        plotAfter = ClonePlot(plotAfter),
        outcomeFingerprint = outcomeFingerprint ?? string.Empty,
        committed = committed
    };

    private static CultivarGenomeSaveData CloneGenome(
        CultivarGenomeSaveData value) => value == null ? null : new()
    {
        genomeId = value.genomeId,
        cropId = value.cropId,
        generation = value.generation,
        loci = (value.loci ?? new List<DiploidLocusSaveData>())
            .Select(locus => new DiploidLocusSaveData
            {
                locus = locus.locus,
                alleleA = locus.alleleA,
                alleleB = locus.alleleB
            })
            .ToList()
    };

    private static CropEcologyPlotSaveData ClonePlot(
        CropEcologyPlotSaveData value) => value == null ? null : new()
    {
        plotId = value.plotId,
        cropId = value.cropId,
        cultivarGenomeId = value.cultivarGenomeId,
        currentGroup = value.currentGroup,
        previousGroup = value.previousGroup,
        hasPreviousGroup = value.hasPreviousGroup,
        fertility = value.fertility,
        pestPressure = value.pestPressure,
        diseasePressure = value.diseasePressure,
        disease = value.disease,
        consecutiveLethalTemperatureDays = value.consecutiveLethalTemperatureDays,
        cropDead = value.cropDead
    };
}

public readonly struct CropEcologyPreparedHarvestSnapshot
{
    public CropEcologyPreparedHarvestSnapshot(
        string operationId,
        string plotId,
        string outcomeFingerprint,
        bool committed,
        CropHarvestEcologyResult result)
    {
        OperationId = operationId ?? string.Empty;
        PlotId = plotId ?? string.Empty;
        OutcomeFingerprint = outcomeFingerprint ?? string.Empty;
        Committed = committed;
        Result = result;
    }

    public string OperationId { get; }
    public string PlotId { get; }
    public string OutcomeFingerprint { get; }
    public bool Committed { get; }
    public CropHarvestEcologyResult Result { get; }
}

public sealed class CropEcologyAggregateState
{
    public const int MaximumActiveCultivarsPerCrop = 12;
    public const int MaximumUnreferencedFrozenCultivarsPerCrop = 32;
    private readonly Dictionary<string, CropEcologyPlotSaveData> plots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CultivarGenomeSaveData> active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CultivarGenomeSaveData> frozen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CropEcologyPreparedHarvestSaveData>
        preparedHarvests = new(StringComparer.Ordinal);
    private bool initialSeedGrantIssued;

    public IReadOnlyList<CropEcologyPlotSaveData> Plots => plots.Values
        .OrderBy(value => value.plotId, StringComparer.Ordinal).ToArray();

    public bool TryClaimInitialSeedGrant(out IReadOnlyList<SeedLotState> seedLots)
    {
        if (initialSeedGrantIssued)
        {
            seedLots = Array.Empty<SeedLotState>();
            return false;
        }
        CultivarGenomeSaveData[] bases = active.Values
            .Where(value => value.generation == 0
                && value.genomeId.EndsWith(
                    ":base",
                    StringComparison.Ordinal))
            .OrderBy(value => value.cropId, StringComparer.Ordinal).ToArray();
        if (bases.Length != 12)
            throw new InvalidOperationException(
                $"V22 initial seed grant requires 12 base cultivars, found {bases.Length}.");
        initialSeedGrantIssued = true;
        seedLots = bases.Select(value => new SeedLotState
        {
            cropId = value.cropId,
            cultivarGenomeId = value.genomeId,
            generation = 0,
            pathogenLoad = 0f
        }).ToArray();
        return true;
    }

    public void RegisterBaseGenome(CultivarGenomeSaveData genome)
    {
        ValidateGenome(genome);
        if (active.TryGetValue(genome.genomeId, out CultivarGenomeSaveData existing))
        {
            if (!GenomeEquals(existing, genome))
                throw new InvalidOperationException($"Cultivar genome ID '{genome.genomeId}' is duplicated.");
            return;
        }
        active.Add(genome.genomeId, CloneGenome(genome));
        EnforceCultivarCaps(genome.cropId);
    }

    public void Sow(string plotId, CropFamilyGroup group, SeedLotState seed)
    {
        if (string.IsNullOrWhiteSpace(plotId))
            throw new ArgumentException("A crop plot ID is required.", nameof(plotId));
        SeedLotItemStateCodec.Encode(seed);
        CultivarGenomeSaveData genome = RequireGenome(seed.cultivarGenomeId);
        if (!string.Equals(genome.cropId, seed.cropId, StringComparison.Ordinal))
            throw new InvalidOperationException("Seed lot and cultivar crop IDs do not match.");
        if (!plots.TryGetValue(plotId, out CropEcologyPlotSaveData plot))
        {
            plot = new CropEcologyPlotSaveData { plotId = plotId, fertility = 100f };
            plots.Add(plotId, plot);
        }
        bool sameFamily = plot.hasPreviousGroup && plot.previousGroup == group;
        plot.pestPressure = Mathf.Clamp(plot.pestPressure + (sameFamily ? 15f : -10f), 0f, 100f);
        plot.diseasePressure = Mathf.Clamp(plot.diseasePressure + (sameFamily ? 10f : -5f), 0f, 100f);
        plot.cropId = seed.cropId;
        plot.cultivarGenomeId = seed.cultivarGenomeId;
        plot.currentGroup = group;
        plot.diseasePressure = Mathf.Clamp(plot.diseasePressure + seed.pathogenLoad * 0.25f, 0f, 100f);
        plot.consecutiveLethalTemperatureDays = 0;
        plot.cropDead = false;
    }

    public bool AdvanceDay(string plotId, bool lethalTemperature, Func<double> nextUnitRandom)
    {
        CropEcologyPlotSaveData plot = RequirePlot(plotId);
        if (plot.cropDead) return false;
        Func<double> random = nextUnitRandom
            ?? throw new ArgumentNullException(nameof(nextUnitRandom));
        plot.consecutiveLethalTemperatureDays = lethalTemperature
            ? plot.consecutiveLethalTemperatureDays + 1
            : 0;
        if (plot.consecutiveLethalTemperatureDays >= 3)
        {
            plot.cropDead = true;
            return false;
        }
        if (plot.pestPressure >= 85f
            && random() < 0.25d)
        {
            plot.cropDead = true;
            return false;
        }

        CropGenomePhenotype phenotype = GetPhenotype(plotId);
        float diseaseChance = Mathf.Clamp01(plot.diseasePressure / 500f)
            * phenotype.DiseaseRiskMultiplier;
        if (plot.disease == CropDiseaseKind.None
            && diseaseChance > 0f
            && random() < diseaseChance)
        {
            plot.disease = DiseaseFor(plot.currentGroup);
            plot.diseasePressure = Mathf.Min(100f, plot.diseasePressure + 15f);
        }
        else if (plot.disease != CropDiseaseKind.None)
        {
            plot.diseasePressure = Mathf.Min(
                100f,
                plot.diseasePressure + 4f * phenotype.DiseaseRiskMultiplier);
        }
        return true;
    }

    public CropGenomePhenotype GetPhenotype(string plotId)
    {
        CropEcologyPlotSaveData plot = RequirePlot(plotId);
        CultivarGenomeSaveData genome = RequireGenome(plot.cultivarGenomeId);
        return CreatePhenotype(genome);
    }

    public CropHarvestEcologyResult Harvest(
        string plotId,
        Func<double> nextUnitRandom,
        IReadOnlyCollection<string> externallyReferencedGenomeIds = null)
    {
        CropEcologyPlotSaveData plot = RequirePlot(plotId);
        if (plot.cropDead) throw new InvalidOperationException("A dead crop cannot be harvested.");
        CultivarGenomeSaveData parent = RequireGenome(plot.cultivarGenomeId);
        float yieldMultiplier = ComputeYieldMultiplier(plot, parent);

        CultivarGenomeSaveData child = Mutate(parent, nextUnitRandom);
        AddGeneratedGenome(child, externallyReferencedGenomeIds);
        int seedBonus = CreatePhenotype(child).SeedYieldBonus;
        int returnedSeeds = Mathf.Clamp(2 + (int)Math.Floor(ClampUnit(nextUnitRandom()) * 3d) + seedBonus, 2, 4);
        SeedLotState seedLot = new()
        {
            cropId = plot.cropId,
            cultivarGenomeId = child.genomeId,
            generation = child.generation,
            pathogenLoad = Mathf.Clamp(plot.diseasePressure * 0.5f, 0f, 100f)
        };

        plot.fertility = Mathf.Max(0f, plot.fertility - 15f);
        plot.previousGroup = plot.currentGroup;
        plot.hasPreviousGroup = true;
        plot.cropId = string.Empty;
        plot.cultivarGenomeId = string.Empty;
        plot.disease = CropDiseaseKind.None;
        plot.consecutiveLethalTemperatureDays = 0;
        return new CropHarvestEcologyResult(yieldMultiplier, returnedSeeds, seedLot);
    }

    public CropEcologyPreparedHarvestSnapshot PrepareHarvest(
        string operationId,
        string plotId,
        Func<double> nextUnitRandom)
    {
        string operation = RequireCanonical(operationId, nameof(operationId));
        string plotKey = RequireCanonical(plotId, nameof(plotId));
        if (preparedHarvests.TryGetValue(
                operation,
                out CropEcologyPreparedHarvestSaveData existing))
        {
            if (!string.Equals(existing.plotId, plotKey, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Crop ecology harvest operation was reused for another plot.");
            return Snapshot(existing);
        }
        if (preparedHarvests.Values.Any(value =>
                string.Equals(value.plotId, plotKey, StringComparison.Ordinal)))
            throw new InvalidOperationException(
                "Crop ecology plot already has a prepared harvest.");

        CropEcologyPlotSaveData plot = RequirePlot(plotKey);
        if (plot.cropDead)
            throw new InvalidOperationException("A dead crop cannot be harvested.");
        CultivarGenomeSaveData parent = RequireGenome(plot.cultivarGenomeId);
        Func<double> random = nextUnitRandom
            ?? throw new ArgumentNullException(nameof(nextUnitRandom));
        float yieldMultiplier = ComputeYieldMultiplier(plot, parent);
        CultivarGenomeSaveData child = Mutate(parent, random);
        int seedBonus = CreatePhenotype(child).SeedYieldBonus;
        int returnedSeeds = Mathf.Clamp(
            2 + (int)Math.Floor(ClampUnit(random()) * 3d) + seedBonus,
            2,
            4);
        SeedLotState seedLot = new()
        {
            cropId = plot.cropId,
            cultivarGenomeId = child.genomeId,
            generation = child.generation,
            pathogenLoad = Mathf.Clamp(plot.diseasePressure * 0.5f, 0f, 100f)
        };
        CropEcologyPlotSaveData after = ClonePlot(plot);
        after.fertility = Mathf.Max(0f, after.fertility - 15f);
        after.previousGroup = after.currentGroup;
        after.hasPreviousGroup = true;
        after.cropId = string.Empty;
        after.cultivarGenomeId = string.Empty;
        after.disease = CropDiseaseKind.None;
        after.consecutiveLethalTemperatureDays = 0;
        CropEcologyPreparedHarvestSaveData receipt = new()
        {
            operationId = operation,
            plotId = plotKey,
            plotBefore = ClonePlot(plot),
            plotBeforeFingerprint = CapturePlotFingerprint(plot),
            yieldMultiplier = yieldMultiplier,
            returnedSeedCount = returnedSeeds,
            returnedSeedLot = seedLot.Clone(),
            generatedGenome = CloneGenome(child),
            plotAfter = after,
            committed = false
        };
        receipt.outcomeFingerprint = CapturePreparedHarvestFingerprint(receipt);
        preparedHarvests.Add(operation, receipt);
        return Snapshot(receipt);
    }

    public CropEcologyPreparedHarvestSnapshot CommitPreparedHarvest(
        string operationId,
        IReadOnlyCollection<string> externallyReferencedGenomeIds = null)
    {
        string operation = RequireCanonical(operationId, nameof(operationId));
        if (!preparedHarvests.TryGetValue(
                operation,
                out CropEcologyPreparedHarvestSaveData receipt))
            throw new KeyNotFoundException(
                "Unknown prepared crop ecology harvest '" + operation + "'.");
        if (receipt.committed)
            return Snapshot(receipt);
        CropEcologyPlotSaveData plot = RequirePlot(receipt.plotId);
        if (!string.Equals(
                CapturePlotFingerprint(plot),
                receipt.plotBeforeFingerprint,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Prepared crop ecology harvest plot state drifted before commit.");
        AddGeneratedGenome(
            CloneGenome(receipt.generatedGenome),
            externallyReferencedGenomeIds);
        CopyPlot(receipt.plotAfter, plot);
        receipt.committed = true;
        return Snapshot(receipt);
    }

    public bool AcknowledgePreparedHarvest(string operationId)
    {
        string operation = RequireCanonical(operationId, nameof(operationId));
        return preparedHarvests.TryGetValue(
                operation,
                out CropEcologyPreparedHarvestSaveData receipt)
            && receipt.committed
            && preparedHarvests.Remove(operation);
    }

    public bool AbortPreparedHarvest(string operationId)
    {
        string operation = RequireCanonical(operationId, nameof(operationId));
        return preparedHarvests.TryGetValue(
                operation,
                out CropEcologyPreparedHarvestSaveData receipt)
            && !receipt.committed
            && preparedHarvests.Remove(operation);
    }

    public bool TryGetPreparedHarvest(
        string operationId,
        out CropEcologyPreparedHarvestSnapshot snapshot)
    {
        if (preparedHarvests.TryGetValue(
                operationId ?? string.Empty,
                out CropEcologyPreparedHarvestSaveData receipt))
        {
            snapshot = Snapshot(receipt);
            return true;
        }
        snapshot = default;
        return false;
    }

    public IReadOnlyList<CropEcologyPreparedHarvestSnapshot>
        CapturePreparedHarvests() => preparedHarvests.Values
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .Select(Snapshot)
            .ToArray();

    public void ApplyCompost(string plotId) =>
        RequirePlot(plotId).fertility = Mathf.Min(100f, RequirePlot(plotId).fertility + 25f);
    public void ApplyPestControl(string plotId, float amount) =>
        RequirePlot(plotId).pestPressure = Mathf.Max(0f, RequirePlot(plotId).pestPressure - Mathf.Max(0f, amount));
    public void ApplyFungicide(string plotId, float amount)
    {
        CropEcologyPlotSaveData plot = RequirePlot(plotId);
        plot.diseasePressure = Mathf.Max(0f, plot.diseasePressure - Mathf.Max(0f, amount));
        if (plot.diseasePressure <= 0f) plot.disease = CropDiseaseKind.None;
    }

    public bool AbandonPlot(string plotId)
    {
        if (string.IsNullOrWhiteSpace(plotId)
            || !string.Equals(plotId, plotId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException(
                "A canonical crop plot ID is required.",
                nameof(plotId));
        if (preparedHarvests.Values.Any(value => string.Equals(
                value.plotId,
                plotId,
                StringComparison.Ordinal)))
            throw new InvalidOperationException(
                "A crop plot with a prepared harvest cannot be abandoned.");
        return plots.Remove(plotId);
    }

    public CropEcologyWorldSaveData Capture() => new()
    {
        initialSeedGrantIssued = initialSeedGrantIssued,
        plots = plots.Values.OrderBy(value => value.plotId, StringComparer.Ordinal).Select(ClonePlot).ToList(),
        activeCultivars = active.Values.OrderBy(value => value.genomeId, StringComparer.Ordinal).Select(CloneGenome).ToList(),
        frozenCultivars = frozen.Values.OrderBy(value => value.genomeId, StringComparer.Ordinal).Select(CloneGenome).ToList(),
        preparedHarvests = preparedHarvests.Values
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToList()
    };

    public static CropEcologyAggregateState Restore(CropEcologyWorldSaveData data)
    {
        if (data == null || data.version != CropEcologyWorldSaveData.CurrentVersion)
            throw new InvalidOperationException("Crop-ecology payload is missing or invalid.");
        CropEcologyAggregateState state = new();
        state.initialSeedGrantIssued = data.initialSeedGrantIssued;
        foreach (CultivarGenomeSaveData genome in data.activeCultivars ?? new())
        {
            ValidateGenome(genome);
            if (!state.active.TryAdd(genome.genomeId, CloneGenome(genome)))
                throw new InvalidOperationException("Active cultivar genome IDs are duplicated.");
        }
        foreach (CultivarGenomeSaveData genome in data.frozenCultivars ?? new())
        {
            ValidateGenome(genome);
            if (state.active.ContainsKey(genome.genomeId)
                || !state.frozen.TryAdd(genome.genomeId, CloneGenome(genome)))
                throw new InvalidOperationException("Frozen cultivar genome IDs are duplicated.");
        }
        foreach (CropEcologyPlotSaveData plot in data.plots ?? new())
        {
            ValidatePlot(plot, state);
            if (!state.plots.TryAdd(plot.plotId, ClonePlot(plot)))
                throw new InvalidOperationException("Crop-ecology plot IDs are duplicated.");
        }
        HashSet<string> preparedPlotIds = new(StringComparer.Ordinal);
        foreach (CropEcologyPreparedHarvestSaveData receipt in
                 data.preparedHarvests ?? new())
        {
            ValidatePreparedHarvest(receipt, state);
            if (!state.preparedHarvests.TryAdd(
                    receipt.operationId,
                    receipt.Clone())
                || !preparedPlotIds.Add(receipt.plotId))
                throw new InvalidOperationException(
                    "Prepared crop ecology harvest operation or plot IDs are duplicated.");
        }
        return state;
    }

    private void AddGeneratedGenome(
        CultivarGenomeSaveData genome,
        IReadOnlyCollection<string> externallyReferencedGenomeIds)
    {
        if (active.TryGetValue(
                genome.genomeId,
                out CultivarGenomeSaveData existing))
        {
            if (!GenomeEquals(existing, genome))
                throw new InvalidOperationException(
                    "Generated cultivar genome identity was reused with another payload.");
        }
        else if (frozen.TryGetValue(genome.genomeId, out existing))
        {
            if (!GenomeEquals(existing, genome))
                throw new InvalidOperationException(
                    "Frozen cultivar genome identity was reused with another payload.");
            frozen.Remove(genome.genomeId);
            active.Add(genome.genomeId, CloneGenome(genome));
        }
        else
        {
            active.Add(genome.genomeId, CloneGenome(genome));
        }
        EnforceCultivarCaps(genome.cropId, genome.genomeId, externallyReferencedGenomeIds);
    }

    private void EnforceCultivarCaps(
        string cropId,
        string protectedGenomeId = "",
        IReadOnlyCollection<string> externalReferences = null)
    {
        HashSet<string> referenced = plots.Values
            .Where(value => !string.IsNullOrWhiteSpace(value.cultivarGenomeId))
            .Select(value => value.cultivarGenomeId).ToHashSet(StringComparer.Ordinal);
        foreach (string id in externalReferences ?? Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(id)) referenced.Add(id);
        if (!string.IsNullOrWhiteSpace(protectedGenomeId)) referenced.Add(protectedGenomeId);
        foreach (CultivarGenomeSaveData genome in active.Values
                     .Where(value => value.cropId == cropId && !referenced.Contains(value.genomeId))
                     .OrderBy(value => value.generation)
                     .ThenBy(value => value.genomeId, StringComparer.Ordinal)
                     .Take(Math.Max(0, active.Values.Count(value => value.cropId == cropId)
                         - MaximumActiveCultivarsPerCrop)).ToArray())
        {
            active.Remove(genome.genomeId);
            frozen[genome.genomeId] = genome;
        }
        foreach (CultivarGenomeSaveData genome in frozen.Values
                     .Where(value => value.cropId == cropId && !referenced.Contains(value.genomeId))
                     .OrderByDescending(value => value.generation)
                     .ThenByDescending(value => value.genomeId, StringComparer.Ordinal)
                     .Skip(MaximumUnreferencedFrozenCultivarsPerCrop).ToArray())
            frozen.Remove(genome.genomeId);
    }

    private CultivarGenomeSaveData RequireGenome(string genomeId)
    {
        if (active.TryGetValue(genomeId ?? string.Empty, out CultivarGenomeSaveData genome)) return genome;
        if (frozen.TryGetValue(genomeId ?? string.Empty, out genome)) return genome;
        throw new KeyNotFoundException($"Unknown cultivar genome '{genomeId}'.");
    }
    private CropEcologyPlotSaveData RequirePlot(string plotId) =>
        plots.TryGetValue(plotId?.Trim() ?? string.Empty, out CropEcologyPlotSaveData plot)
            ? plot : throw new KeyNotFoundException($"Unknown crop plot '{plotId}'.");

    private static CultivarGenomeSaveData Mutate(CultivarGenomeSaveData parent, Func<double> random)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        CultivarGenomeSaveData child = CloneGenome(parent);
        child.generation = parent.generation + 1;
        foreach (DiploidLocusSaveData locus in child.loci)
        {
            if (ClampUnit(random()) >= 0.01d) continue;
            bool mutateA = ClampUnit(random()) < 0.5d;
            int current = mutateA ? locus.alleleA : locus.alleleB;
            int direction = ClampUnit(random()) < 0.5d ? -1 : 1;
            int mutated = Mathf.Clamp(current + direction, -2, 2);
            if (mutated == current) mutated = Mathf.Clamp(current - direction, -2, 2);
            if (mutateA) locus.alleleA = mutated; else locus.alleleB = mutated;
        }
        child.genomeId = CreateGeneratedGenomeId(child);
        return child;
    }

    private static string CreateGeneratedGenomeId(CultivarGenomeSaveData genome)
    {
        string signature = string.Join(".", genome.loci
            .OrderBy(value => value.locus)
            .Select(value => $"{value.alleleA + 2}{value.alleleB + 2}"));
        string cropSuffix = genome.cropId.StartsWith(
                "crop:",
                StringComparison.Ordinal)
            ? genome.cropId.Substring("crop:".Length)
            : genome.cropId;
        return $"genome:{cropSuffix}:g{genome.generation}:{signature}";
    }

    private static float ComputeYieldMultiplier(
        CropEcologyPlotSaveData plot,
        CultivarGenomeSaveData parent)
    {
        float pestMultiplier = plot.pestPressure >= 60f ? 0.70f
            : plot.pestPressure >= 30f ? 0.90f : 1f;
        float rotationMultiplier = plot.hasPreviousGroup
                && plot.previousGroup == plot.currentGroup
            ? 0.85f
            : 1f;
        float fertilityMultiplier = Mathf.Lerp(
            0.55f,
            1f,
            plot.fertility / 100f);
        CropGenomePhenotype parentPhenotype = CreatePhenotype(parent);
        float diseaseMultiplier = 1f
            - Mathf.Clamp(plot.diseasePressure, 0f, 100f) * 0.003f;
        return pestMultiplier
            * rotationMultiplier
            * fertilityMultiplier
            * parentPhenotype.YieldMultiplier
            * diseaseMultiplier;
    }

    private static float GetLocusMean(CultivarGenomeSaveData genome, CropGenomeLocus locus)
    {
        DiploidLocusSaveData value = genome.loci.Single(entry => entry.locus == locus);
        return (value.alleleA + value.alleleB) * 0.5f;
    }

    private static CropGenomePhenotype CreatePhenotype(CultivarGenomeSaveData genome)
        => CropGenomePhenotypeAuthority.Create(genome);

    private static CropDiseaseKind DiseaseFor(CropFamilyGroup group) => group switch
    {
        CropFamilyGroup.Grain or CropFamilyGroup.Fiber => CropDiseaseKind.GrainFiberRust,
        CropFamilyGroup.Root => CropDiseaseKind.RootRot,
        CropFamilyGroup.Leaf or CropFamilyGroup.Vine => CropDiseaseKind.LeafVinePowderyMildew,
        CropFamilyGroup.Fungus => CropDiseaseKind.MushroomSporeMold,
        _ => CropDiseaseKind.None
    };

    private static void ValidateGenome(CultivarGenomeSaveData genome)
    {
        CropGenomeLocus[] expectedLoci = Enum.GetValues(typeof(CropGenomeLocus))
            .Cast<CropGenomeLocus>()
            .OrderBy(value => value)
            .ToArray();
        if (genome == null || !Canonical(genome.genomeId)
            || !Canonical(genome.cropId) || genome.generation < 0
            || genome.loci == null || genome.loci.Count != 6
            || !genome.loci.Select(value => value.locus)
                .OrderBy(value => value)
                .SequenceEqual(expectedLoci)
            || genome.loci.Any(value => value.alleleA is < -2 or > 2 || value.alleleB is < -2 or > 2))
            throw new InvalidOperationException("Cultivar genome is invalid.");
    }
    private static void ValidatePlot(CropEcologyPlotSaveData plot, CropEcologyAggregateState state)
    {
        if (plot == null || !Canonical(plot.plotId)
            || !Enum.IsDefined(typeof(CropFamilyGroup), plot.currentGroup)
            || !Enum.IsDefined(typeof(CropFamilyGroup), plot.previousGroup)
            || !Enum.IsDefined(typeof(CropDiseaseKind), plot.disease)
            || !FiniteRange(plot.fertility, 0f, 100f)
            || !FiniteRange(plot.pestPressure, 0f, 100f)
            || !FiniteRange(plot.diseasePressure, 0f, 100f)
            || plot.consecutiveLethalTemperatureDays < 0
            || string.IsNullOrEmpty(plot.cropId)
                != string.IsNullOrEmpty(plot.cultivarGenomeId)
            || !string.IsNullOrEmpty(plot.cropId) && !Canonical(plot.cropId)
            || !string.IsNullOrEmpty(plot.cultivarGenomeId)
                && !Canonical(plot.cultivarGenomeId))
            throw new InvalidOperationException("Crop-ecology plot state is invalid.");
        if (!string.IsNullOrWhiteSpace(plot.cultivarGenomeId))
        {
            CultivarGenomeSaveData genome = state.RequireGenome(plot.cultivarGenomeId);
            if (!string.Equals(genome.cropId, plot.cropId, StringComparison.Ordinal))
                throw new InvalidOperationException("Crop plot and cultivar crop IDs do not match.");
        }
    }
    private static bool GenomeEquals(CultivarGenomeSaveData left, CultivarGenomeSaveData right) =>
        left.cropId == right.cropId && left.generation == right.generation
        && left.loci.OrderBy(value => value.locus).Zip(right.loci.OrderBy(value => value.locus),
            (a, b) => a.locus == b.locus && a.alleleA == b.alleleA && a.alleleB == b.alleleB).All(value => value);
    private static CultivarGenomeSaveData CloneGenome(CultivarGenomeSaveData value) => new()
    {
        genomeId = value.genomeId, cropId = value.cropId, generation = value.generation,
        loci = value.loci.Select(locus => new DiploidLocusSaveData
        { locus = locus.locus, alleleA = locus.alleleA, alleleB = locus.alleleB }).ToList()
    };
    private static CropEcologyPlotSaveData ClonePlot(CropEcologyPlotSaveData value) => new()
    {
        plotId = value.plotId, cropId = value.cropId, cultivarGenomeId = value.cultivarGenomeId,
        currentGroup = value.currentGroup, previousGroup = value.previousGroup,
        hasPreviousGroup = value.hasPreviousGroup, fertility = value.fertility,
        pestPressure = value.pestPressure, diseasePressure = value.diseasePressure,
        disease = value.disease, consecutiveLethalTemperatureDays = value.consecutiveLethalTemperatureDays,
        cropDead = value.cropDead
    };
    private static void CopyPlot(
        CropEcologyPlotSaveData source,
        CropEcologyPlotSaveData destination)
    {
        CropEcologyPlotSaveData clone = ClonePlot(source);
        destination.plotId = clone.plotId;
        destination.cropId = clone.cropId;
        destination.cultivarGenomeId = clone.cultivarGenomeId;
        destination.currentGroup = clone.currentGroup;
        destination.previousGroup = clone.previousGroup;
        destination.hasPreviousGroup = clone.hasPreviousGroup;
        destination.fertility = clone.fertility;
        destination.pestPressure = clone.pestPressure;
        destination.diseasePressure = clone.diseasePressure;
        destination.disease = clone.disease;
        destination.consecutiveLethalTemperatureDays =
            clone.consecutiveLethalTemperatureDays;
        destination.cropDead = clone.cropDead;
    }

    private static CropEcologyPreparedHarvestSnapshot Snapshot(
        CropEcologyPreparedHarvestSaveData receipt) => new(
        receipt.operationId,
        receipt.plotId,
        receipt.outcomeFingerprint,
        receipt.committed,
        new CropHarvestEcologyResult(
            receipt.yieldMultiplier,
            receipt.returnedSeedCount,
            receipt.returnedSeedLot?.Clone()));

    private static string CapturePlotFingerprint(CropEcologyPlotSaveData plot)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-ecology-plot@1");
        digest.Append(plot?.plotId ?? string.Empty);
        digest.Append(plot?.cropId ?? string.Empty);
        digest.Append(plot?.cultivarGenomeId ?? string.Empty);
        digest.Append((int)(plot?.currentGroup ?? default));
        digest.Append((int)(plot?.previousGroup ?? default));
        digest.Append(plot?.hasPreviousGroup ?? false);
        digest.Append((plot?.fertility ?? 0f).ToString(
            "R",
            System.Globalization.CultureInfo.InvariantCulture));
        digest.Append((plot?.pestPressure ?? 0f).ToString(
            "R",
            System.Globalization.CultureInfo.InvariantCulture));
        digest.Append((plot?.diseasePressure ?? 0f).ToString(
            "R",
            System.Globalization.CultureInfo.InvariantCulture));
        digest.Append((int)(plot?.disease ?? default));
        digest.Append(plot?.consecutiveLethalTemperatureDays ?? 0);
        digest.Append(plot?.cropDead ?? false);
        return digest.ComputeSha256();
    }

    private static string CapturePreparedHarvestFingerprint(
        CropEcologyPreparedHarvestSaveData receipt)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-ecology-prepared-harvest@2");
        digest.Append(receipt.operationId);
        digest.Append(receipt.plotId);
        digest.Append(CapturePlotFingerprint(receipt.plotBefore));
        digest.Append(receipt.plotBeforeFingerprint);
        digest.Append(receipt.yieldMultiplier.ToString(
            "R",
            System.Globalization.CultureInfo.InvariantCulture));
        digest.Append(receipt.returnedSeedCount);
        digest.Append(SeedLotItemStateCodec.Encode(receipt.returnedSeedLot)
            .ToCanonicalString());
        digest.Append(receipt.generatedGenome?.genomeId ?? string.Empty);
        digest.Append(receipt.generatedGenome?.cropId ?? string.Empty);
        digest.Append(receipt.generatedGenome?.generation ?? 0);
        foreach (DiploidLocusSaveData locus in
                 (receipt.generatedGenome?.loci
                     ?? new List<DiploidLocusSaveData>())
                 .OrderBy(value => value.locus))
        {
            digest.Append((int)locus.locus);
            digest.Append(locus.alleleA);
            digest.Append(locus.alleleB);
        }
        digest.Append(CapturePlotFingerprint(receipt.plotAfter));
        return digest.ComputeSha256();
    }

    private static void ValidatePreparedHarvest(
        CropEcologyPreparedHarvestSaveData receipt,
        CropEcologyAggregateState state)
    {
        if (receipt == null
            || !Canonical(receipt.operationId)
            || !Canonical(receipt.plotId)
            || receipt.plotBefore == null
            || receipt.returnedSeedLot == null
            || receipt.generatedGenome == null
            || receipt.plotAfter == null
            || receipt.returnedSeedCount is < 2 or > 4
            || float.IsNaN(receipt.yieldMultiplier)
            || float.IsInfinity(receipt.yieldMultiplier)
            || receipt.yieldMultiplier <= 0f
            || !string.Equals(
                receipt.outcomeFingerprint,
                CapturePreparedHarvestFingerprint(receipt),
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Prepared crop ecology harvest is invalid.");
        ValidateGenome(receipt.generatedGenome);
        ValidatePlot(receipt.plotBefore, state);
        SeedLotItemStateCodec.Encode(receipt.returnedSeedLot);
        if (!string.Equals(
                receipt.plotBefore.plotId,
                receipt.plotId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.plotBeforeFingerprint,
                CapturePlotFingerprint(receipt.plotBefore),
                StringComparison.Ordinal)
            || receipt.plotBefore.cropDead
            || string.IsNullOrWhiteSpace(receipt.plotBefore.cropId)
            || string.IsNullOrWhiteSpace(receipt.plotBefore.cultivarGenomeId)
            || !string.Equals(
                receipt.plotBefore.cropId,
                receipt.returnedSeedLot.cropId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.plotAfter.plotId,
                receipt.plotId,
                StringComparison.Ordinal)
            || !string.IsNullOrEmpty(receipt.plotAfter.cropId)
            || !string.IsNullOrEmpty(receipt.plotAfter.cultivarGenomeId)
            || !string.Equals(
                receipt.returnedSeedLot.cropId,
                receipt.generatedGenome.cropId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.returnedSeedLot.cultivarGenomeId,
                receipt.generatedGenome.genomeId,
                StringComparison.Ordinal)
            || receipt.returnedSeedLot.generation
                != receipt.generatedGenome.generation)
            throw new InvalidOperationException(
                "Prepared crop ecology harvest result is internally inconsistent.");
        CultivarGenomeSaveData parent = state.RequireGenome(
            receipt.plotBefore.cultivarGenomeId);
        ValidateGeneratedGenomeTransition(parent, receipt.generatedGenome);
        if (receipt.yieldMultiplier > 3f
            || receipt.yieldMultiplier != ComputeYieldMultiplier(
                receipt.plotBefore,
                parent))
            throw new InvalidOperationException(
                "Prepared crop ecology harvest yield contradicts its source state.");
        float expectedPathogenLoad = Mathf.Clamp(
            receipt.plotBefore.diseasePressure * 0.5f,
            0f,
            100f);
        if (receipt.returnedSeedLot.pathogenLoad != expectedPathogenLoad)
            throw new InvalidOperationException(
                "Prepared crop ecology seed pathogen load is invalid.");
        int seedBonus = CreatePhenotype(receipt.generatedGenome).SeedYieldBonus;
        bool possibleSeedCount = Enumerable.Range(0, 3).Any(roll =>
            Mathf.Clamp(2 + roll + seedBonus, 2, 4)
                == receipt.returnedSeedCount);
        if (!possibleSeedCount)
            throw new InvalidOperationException(
                "Prepared crop ecology returned seed count is impossible.");
        ValidatePreparedHarvestTransition(
            receipt.plotBefore,
            receipt.plotAfter);
        if (!state.plots.TryGetValue(
                receipt.plotId,
                out CropEcologyPlotSaveData livePlot))
            throw new InvalidOperationException(
                "Prepared crop ecology harvest references an unknown plot.");
        if (!receipt.committed
            && !string.Equals(
                livePlot.cropId,
                receipt.returnedSeedLot.cropId,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Prepared crop ecology harvest seed crop contradicts its live plot.");
        bool generatedGenomeExists = state.active.TryGetValue(
                receipt.generatedGenome.genomeId,
                out CultivarGenomeSaveData activeGenome)
            || state.frozen.TryGetValue(
                receipt.generatedGenome.genomeId,
                out activeGenome);
        if (receipt.committed && !generatedGenomeExists)
            throw new InvalidOperationException(
                "Prepared crop ecology harvest genome publication contradicts its phase.");
        if (generatedGenomeExists
            && (!string.Equals(
                    activeGenome.genomeId,
                    receipt.generatedGenome.genomeId,
                    StringComparison.Ordinal)
                || !GenomeEquals(activeGenome, receipt.generatedGenome)))
            throw new InvalidOperationException(
                "Prepared crop ecology harvest genome payload drifted after commit.");
        string expectedPlotFingerprint = receipt.committed
            ? CapturePlotFingerprint(receipt.plotAfter)
            : receipt.plotBeforeFingerprint;
        if (!string.Equals(
                CapturePlotFingerprint(livePlot),
                expectedPlotFingerprint,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Prepared crop ecology harvest contradicts its plot state.");
    }

    private static void ValidatePreparedHarvestTransition(
        CropEcologyPlotSaveData before,
        CropEcologyPlotSaveData after)
    {
        if (!string.Equals(before.plotId, after.plotId, StringComparison.Ordinal)
            || before.currentGroup != after.currentGroup
            || after.previousGroup != before.currentGroup
            || !after.hasPreviousGroup
            || after.fertility != Mathf.Max(0f, before.fertility - 15f)
            || after.pestPressure != before.pestPressure
            || after.diseasePressure != before.diseasePressure
            || after.disease != CropDiseaseKind.None
            || after.consecutiveLethalTemperatureDays != 0
            || after.cropDead != before.cropDead)
            throw new InvalidOperationException(
                "Prepared crop ecology harvest transition is invalid.");
    }

    private static void ValidateGeneratedGenomeTransition(
        CultivarGenomeSaveData parent,
        CultivarGenomeSaveData child)
    {
        if (!string.Equals(parent.cropId, child.cropId, StringComparison.Ordinal)
            || child.generation != checked(parent.generation + 1)
            || !string.Equals(
                child.genomeId,
                CreateGeneratedGenomeId(child),
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Prepared crop ecology generated genome identity is invalid.");
        Dictionary<CropGenomeLocus, DiploidLocusSaveData> parentLoci =
            parent.loci.ToDictionary(value => value.locus);
        foreach (DiploidLocusSaveData childLocus in child.loci)
        {
            DiploidLocusSaveData parentLocus = parentLoci[childLocus.locus];
            int deltaA = Math.Abs(childLocus.alleleA - parentLocus.alleleA);
            int deltaB = Math.Abs(childLocus.alleleB - parentLocus.alleleB);
            if (deltaA > 1 || deltaB > 1 || deltaA > 0 && deltaB > 0)
                throw new InvalidOperationException(
                    "Prepared crop ecology generated genome mutation is invalid.");
        }
    }

    private static bool FiniteRange(float value, float minimum, float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static string RequireCanonical(string value, string parameterName)
    {
        if (!Canonical(value))
            throw new ArgumentException(
                "A canonical crop ecology operation identifier is required.",
                parameterName);
        return value;
    }
    private static double ClampUnit(double value) => Math.Max(0d, Math.Min(0.999999999999d, value));
}

public interface ICropEcologyService
{
    int Version { get; }
    void Sow(string plotId, CropFamilyGroup group, SeedLotState seed);
    bool AdvanceDay(string plotId, bool lethalTemperature);
    CropGenomePhenotype GetPhenotype(string plotId);
    CropHarvestEcologyResult Harvest(string plotId);
    void ApplyCompost(string plotId);
    void ApplyPestControl(string plotId, float amount);
    void ApplyFungicide(string plotId, float amount);
    bool AbandonPlot(string plotId);
    IReadOnlyList<CropEcologyPlotSaveData> Plots { get; }
}

public interface ICropEcologyHarvestTransactionService
{
    CropEcologyPreparedHarvestSnapshot PrepareHarvest(
        string operationId,
        string plotId);
    CropEcologyPreparedHarvestSnapshot CommitPreparedHarvest(
        string operationId);
    bool AcknowledgePreparedHarvest(string operationId);
    bool AbortPreparedHarvest(string operationId);
    bool TryGetPreparedHarvest(
        string operationId,
        out CropEcologyPreparedHarvestSnapshot snapshot);
    IReadOnlyList<CropEcologyPreparedHarvestSnapshot>
        CapturePreparedHarvests();
}

public interface ICropEcologyPersistence
{
    CropEcologyWorldSaveData Capture();
    CropEcologyAggregateState PrepareRestore(CropEcologyWorldSaveData data);
    void PublishRestore(CropEcologyAggregateState candidate);
}

public interface IInitialCropSeedGrant
{
    bool TryClaim(out IReadOnlyList<SeedLotState> seedLots);
}
