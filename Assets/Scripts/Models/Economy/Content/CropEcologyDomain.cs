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
    [Range(0f, 100f)] public float quality = 50f;
    public int generation;
    [Range(0f, 100f)] public float pathogenLoad;

    public SeedLotState Clone() => new()
    {
        cropId = cropId,
        cultivarGenomeId = cultivarGenomeId,
        quality = quality,
        generation = generation,
        pathogenLoad = pathogenLoad
    };
}

public static class SeedLotItemStateCodec
{
    public const string ComponentTypeId = "item-state:seed-lot";
    public const int SchemaVersion = 1;

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
                Decimal("quality", state.quality),
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
            quality = (float)Require(fields, "quality", ItemStateValueKind.Decimal).decimalValue,
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
            || state.quality is < 0f or > 100f
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
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public bool initialSeedGrantIssued;
    public List<CropEcologyPlotSaveData> plots = new();
    public List<CultivarGenomeSaveData> activeCultivars = new();
    public List<CultivarGenomeSaveData> frozenCultivars = new();
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

public sealed class CropEcologyAggregateState
{
    public const int MaximumActiveCultivarsPerCrop = 12;
    public const int MaximumUnreferencedFrozenCultivarsPerCrop = 32;
    private readonly Dictionary<string, CropEcologyPlotSaveData> plots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CultivarGenomeSaveData> active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CultivarGenomeSaveData> frozen = new(StringComparer.Ordinal);
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
        if (bases.Length != 8)
            throw new InvalidOperationException(
                $"Initial seed grant requires 8 base cultivars, found {bases.Length}.");
        initialSeedGrantIssued = true;
        seedLots = bases.Select(value => new SeedLotState
        {
            cropId = value.cropId,
            cultivarGenomeId = value.genomeId,
            quality = 70f,
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
        plot.consecutiveLethalTemperatureDays = lethalTemperature
            ? plot.consecutiveLethalTemperatureDays + 1
            : 0;
        if (plot.consecutiveLethalTemperatureDays >= 3)
        {
            plot.cropDead = true;
            return false;
        }
        if (plot.pestPressure >= 85f
            && (nextUnitRandom ?? throw new ArgumentNullException(nameof(nextUnitRandom)))() < 0.25d)
        {
            plot.cropDead = true;
            return false;
        }
        return true;
    }

    public CropHarvestEcologyResult Harvest(
        string plotId,
        Func<double> nextUnitRandom,
        IReadOnlyCollection<string> externallyReferencedGenomeIds = null)
    {
        CropEcologyPlotSaveData plot = RequirePlot(plotId);
        if (plot.cropDead) throw new InvalidOperationException("A dead crop cannot be harvested.");
        CultivarGenomeSaveData parent = RequireGenome(plot.cultivarGenomeId);
        float pestMultiplier = plot.pestPressure >= 60f ? 0.70f
            : plot.pestPressure >= 30f ? 0.90f : 1f;
        float rotationMultiplier = plot.hasPreviousGroup && plot.previousGroup == plot.currentGroup
            ? 0.85f : 1f;
        float fertilityMultiplier = Mathf.Lerp(0.55f, 1f, plot.fertility / 100f);
        float genomeYield = 1f + GetLocusMean(parent, CropGenomeLocus.Yield) * 0.05f;
        float diseaseMultiplier = 1f - Mathf.Clamp(plot.diseasePressure, 0f, 100f) * 0.003f;
        float yieldMultiplier = pestMultiplier * rotationMultiplier * fertilityMultiplier
            * genomeYield * diseaseMultiplier;

        CultivarGenomeSaveData child = Mutate(parent, nextUnitRandom);
        AddGeneratedGenome(child, externallyReferencedGenomeIds);
        int seedBonus = Mathf.RoundToInt(GetLocusMean(child, CropGenomeLocus.SeedYield) * 0.5f);
        int returnedSeeds = Mathf.Clamp(2 + (int)Math.Floor(ClampUnit(nextUnitRandom()) * 3d) + seedBonus, 2, 4);
        SeedLotState seedLot = new()
        {
            cropId = plot.cropId,
            cultivarGenomeId = child.genomeId,
            generation = child.generation,
            quality = Mathf.Clamp(50f + plot.fertility * 0.35f - plot.diseasePressure * 0.25f, 0f, 100f),
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

    public CropEcologyWorldSaveData Capture() => new()
    {
        initialSeedGrantIssued = initialSeedGrantIssued,
        plots = plots.Values.OrderBy(value => value.plotId, StringComparer.Ordinal).Select(ClonePlot).ToList(),
        activeCultivars = active.Values.OrderBy(value => value.genomeId, StringComparer.Ordinal).Select(CloneGenome).ToList(),
        frozenCultivars = frozen.Values.OrderBy(value => value.genomeId, StringComparer.Ordinal).Select(CloneGenome).ToList()
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
        return state;
    }

    private void AddGeneratedGenome(
        CultivarGenomeSaveData genome,
        IReadOnlyCollection<string> externallyReferencedGenomeIds)
    {
        if (!active.ContainsKey(genome.genomeId)) active.Add(genome.genomeId, CloneGenome(genome));
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
        string signature = string.Join(".", child.loci.OrderBy(value => value.locus)
            .Select(value => $"{value.alleleA + 2}{value.alleleB + 2}"));
        child.genomeId = $"genome:{parent.cropId.Replace("crop:", string.Empty)}:g{child.generation}:{signature}";
        return child;
    }

    private static float GetLocusMean(CultivarGenomeSaveData genome, CropGenomeLocus locus)
    {
        DiploidLocusSaveData value = genome.loci.Single(entry => entry.locus == locus);
        return (value.alleleA + value.alleleB) * 0.5f;
    }

    private static void ValidateGenome(CultivarGenomeSaveData genome)
    {
        if (genome == null || string.IsNullOrWhiteSpace(genome.genomeId)
            || string.IsNullOrWhiteSpace(genome.cropId) || genome.generation < 0
            || genome.loci == null || genome.loci.Count != 6
            || genome.loci.Select(value => value.locus).Distinct().Count() != 6
            || genome.loci.Any(value => value.alleleA is < -2 or > 2 || value.alleleB is < -2 or > 2))
            throw new InvalidOperationException("Cultivar genome is invalid.");
    }
    private static void ValidatePlot(CropEcologyPlotSaveData plot, CropEcologyAggregateState state)
    {
        if (plot == null || string.IsNullOrWhiteSpace(plot.plotId)
            || plot.fertility is < 0f or > 100f || plot.pestPressure is < 0f or > 100f
            || plot.diseasePressure is < 0f or > 100f || plot.consecutiveLethalTemperatureDays < 0)
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
    private static double ClampUnit(double value) => Math.Max(0d, Math.Min(0.999999999999d, value));
}

public interface ICropEcologyService
{
    int Version { get; }
    void Sow(string plotId, CropFamilyGroup group, SeedLotState seed);
    bool AdvanceDay(string plotId, bool lethalTemperature);
    CropHarvestEcologyResult Harvest(string plotId);
    void ApplyCompost(string plotId);
    void ApplyPestControl(string plotId, float amount);
    void ApplyFungicide(string plotId, float amount);
    IReadOnlyList<CropEcologyPlotSaveData> Plots { get; }
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
