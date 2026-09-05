using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorkerSelectionMode
{
    Anyone = 0,
    SpecificCharacters = 1,
    RuleSet = 2,
    SpecificOrRuleSet = 3
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorkerRequirementMatchMode
{
    All = 0,
    Any = 1
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorkerCandidateSortMode
{
    BestExpectedQuality = 0,
    Fastest = 1,
    Nearest = 2,
    LeastWorkload = 3,
    SpecificThenBestExpectedQuality = 4
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WorkerSelectionPolicySaveData
{
    public WorkerSelectionMode mode = WorkerSelectionMode.Anyone;
    public WorkerRequirementMatchMode matchMode = WorkerRequirementMatchMode.All;
    public WorkerCandidateSortMode sortMode = WorkerCandidateSortMode.Fastest;
    public List<string> specificCharacterIds = new();
    public List<string> excludedCharacterIds = new();
    public string minimumSkillId = string.Empty;
    [Min(0)] public int minimumSkillExperience;
    [Min(0)] public int minimumCareerRank;
    public List<string> requiredTraitIds = new();
    public List<string> excludedTraitIds = new();

    public static WorkerSelectionPolicySaveData Anyone(
        WorkerCandidateSortMode sort = WorkerCandidateSortMode.Fastest) => new()
    {
        mode = WorkerSelectionMode.Anyone,
        sortMode = sort
    };

    public WorkerSelectionPolicySaveData CloneNormalized() => new()
    {
        mode = mode,
        matchMode = matchMode,
        sortMode = sortMode,
        specificCharacterIds = NormalizeIds(specificCharacterIds),
        excludedCharacterIds = NormalizeIds(excludedCharacterIds),
        minimumSkillId = minimumSkillId?.Trim() ?? string.Empty,
        minimumSkillExperience = Mathf.Max(0, minimumSkillExperience),
        minimumCareerRank = Mathf.Max(0, minimumCareerRank),
        requiredTraitIds = NormalizeIds(requiredTraitIds),
        excludedTraitIds = NormalizeIds(excludedTraitIds)
    };

    private static List<string> NormalizeIds(IEnumerable<string> source) =>
        (source ?? Array.Empty<string>())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToList();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CraftContributionSaveData
{
    public string characterId = string.Empty;
    [Min(0f)] public float contributedWork;
    [Min(0f)] public float relevantSkill;

    public CraftContributionSaveData Clone() => new()
    {
        characterId = characterId?.Trim() ?? string.Empty,
        contributedWork = Mathf.Max(0f, contributedWork),
        relevantSkill = Mathf.Max(0f, relevantSkill)
    };
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CraftContributionAccumulator
{
    private readonly Dictionary<string, CraftContributionSaveData> byCharacter =
        new(StringComparer.Ordinal);

    public CraftContributionAccumulator(IEnumerable<CraftContributionSaveData> saved = null)
    {
        foreach (CraftContributionSaveData value in saved
                     ?? Array.Empty<CraftContributionSaveData>())
        {
            Add(value?.characterId, value?.contributedWork ?? 0f,
                value?.relevantSkill ?? 0f);
        }
    }

    public void Add(string characterId, float work, float relevantSkill)
    {
        string id = characterId?.Trim() ?? string.Empty;
        float acceptedWork = Mathf.Max(0f, work);
        if (id.Length == 0 || acceptedWork <= 0f)
        {
            return;
        }
        if (!byCharacter.TryGetValue(id, out CraftContributionSaveData value))
        {
            value = new CraftContributionSaveData { characterId = id };
            byCharacter.Add(id, value);
        }

        float total = value.contributedWork + acceptedWork;
        value.relevantSkill = total <= 0f
            ? 0f
            : ((value.relevantSkill * value.contributedWork)
                + (Mathf.Max(0f, relevantSkill) * acceptedWork)) / total;
        value.contributedWork = total;
    }

    public float WeightedRelevantSkill
    {
        get
        {
            float work = byCharacter.Values.Sum(value => value.contributedWork);
            return work <= 0f ? 0f : byCharacter.Values.Sum(value =>
                value.relevantSkill * value.contributedWork) / work;
        }
    }

    public List<CraftContributionSaveData> Capture() => byCharacter.Values
        .OrderBy(value => value.characterId, StringComparer.Ordinal)
        .Select(value => value.Clone())
        .ToList();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CraftsmanshipQualityTier
{
    Awful = 0,
    Poor = 1,
    Normal = 2,
    Good = 3,
    Excellent = 4,
    Masterwork = 5,
    Legendary = 6,
    Mythic = 7
}

public static class QualityRejectedOutputRules
{
    public const string MarketDestinationId = "sale:quality-rejected";
    public const int MaximumSettlementsPerEvaluation = 4;
}

public static class CraftsmanshipQualityRules
{
    public static float ProjectionMultiplier(CraftsmanshipQualityTier tier) => tier switch
    {
        CraftsmanshipQualityTier.Awful => 0.70f,
        CraftsmanshipQualityTier.Poor => 0.82f,
        CraftsmanshipQualityTier.Normal => 1f,
        CraftsmanshipQualityTier.Good => 1.08f,
        CraftsmanshipQualityTier.Excellent => 1.16f,
        CraftsmanshipQualityTier.Masterwork => 1.26f,
        CraftsmanshipQualityTier.Legendary => 1.40f,
        CraftsmanshipQualityTier.Mythic => 1.60f,
        _ => throw new ArgumentOutOfRangeException(
            nameof(tier),
            tier,
            "Craftsmanship quality has no authored projection multiplier.")
    };
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MythicProvenanceSaveData
{
    public string makerCharacterId = string.Empty;
    public int sourceTraitId;
    public CraftsmanshipQualityTier originalQuality;
    public ulong fixedRollHash;
    public int createdDay;
    public string createdFacilityId = string.Empty;

    public MythicProvenanceSaveData Clone() => new()
    {
        makerCharacterId = makerCharacterId?.Trim() ?? string.Empty,
        sourceTraitId = sourceTraitId,
        originalQuality = originalQuality,
        fixedRollHash = fixedRollHash,
        createdDay = Mathf.Max(0, createdDay),
        createdFacilityId = createdFacilityId?.Trim() ?? string.Empty
    };
}

public static class MythicCraftInspirationRules
{
    public const int SourceTraitId = 300;
    public const ulong RollScale = 1_000_000UL;

    public static ulong ResolveFixedRollHash(
        ulong runSeed,
        string pipelineId,
        string definitionId,
        int attemptIndex,
        string makerCharacterId,
        int traitId = SourceTraitId)
    {
        ulong hash = 14695981039346656037UL;
        Append(ref hash, runSeed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(ref hash, pipelineId);
        Append(ref hash, definitionId);
        Append(ref hash, Mathf.Max(0, attemptIndex).ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Append(ref hash, makerCharacterId);
        Append(ref hash, traitId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return hash;
    }

    public static bool IsMythic(ulong fixedRollHash, float authoredChance)
    {
        ulong threshold = (ulong)Math.Round(
            Mathf.Clamp01(authoredChance) * RollScale,
            MidpointRounding.AwayFromZero);
        return fixedRollHash % RollScale < threshold;
    }

    private static void Append(ref ulong hash, string value)
    {
        foreach (char character in value?.Trim() ?? string.Empty)
        {
            unchecked
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }
        }
        unchecked
        {
            hash ^= 0x1FUL;
            hash *= 1099511628211UL;
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CraftQualityRollSaveData
{
    public int attemptIndex;
    public int randomA;
    public int randomB;
    public int randomC;

    public int RandomTotal => Mathf.Clamp(randomA, -10, 10)
        + Mathf.Clamp(randomB, -10, 10)
        + Mathf.Clamp(randomC, -10, 10);

    public CraftQualityRollSaveData Clone() => new()
    {
        attemptIndex = Mathf.Max(0, attemptIndex),
        randomA = Mathf.Clamp(randomA, -10, 10),
        randomB = Mathf.Clamp(randomB, -10, 10),
        randomC = Mathf.Clamp(randomC, -10, 10)
    };
}

public readonly struct CraftQualityResolution
{
    public CraftQualityResolution(float score, CraftsmanshipQualityTier tier)
    {
        Score = score;
        Tier = tier;
    }

    public float Score { get; }
    public CraftsmanshipQualityTier Tier { get; }
}

public interface ICraftQualityResolver
{
    CraftQualityRollSaveData Roll(
        ulong runSeed,
        string pipelineId,
        string definitionId,
        int attemptIndex);
    CraftQualityResolution Resolve(
        CraftQualityRollSaveData roll,
        float weightedSkill,
        float facilityBonus,
        float toolBonus,
        float complexityPenalty);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DeterministicCraftQualityResolver : ICraftQualityResolver
{
    public CraftQualityRollSaveData Roll(
        ulong runSeed,
        string pipelineId,
        string definitionId,
        int attemptIndex)
    {
        ulong state = StableHash(runSeed, pipelineId, definitionId, attemptIndex);
        return new CraftQualityRollSaveData
        {
            attemptIndex = Mathf.Max(0, attemptIndex),
            randomA = NextRange(ref state, -10, 10),
            randomB = NextRange(ref state, -10, 10),
            randomC = NextRange(ref state, -10, 10)
        };
    }

    public CraftQualityResolution Resolve(
        CraftQualityRollSaveData roll,
        float weightedSkill,
        float facilityBonus,
        float toolBonus,
        float complexityPenalty)
    {
        float score = 50f
            + 0.7f * (Mathf.Max(0f, weightedSkill) - 50f)
            + facilityBonus
            + toolBonus
            - Mathf.Max(0f, complexityPenalty)
            + (roll?.RandomTotal ?? 0);
        return new CraftQualityResolution(score, FromScore(score));
    }

    public static CraftsmanshipQualityTier FromScore(float score)
    {
        if (score < 20f) return CraftsmanshipQualityTier.Awful;
        if (score < 35f) return CraftsmanshipQualityTier.Poor;
        if (score < 55f) return CraftsmanshipQualityTier.Normal;
        if (score < 70f) return CraftsmanshipQualityTier.Good;
        if (score < 83f) return CraftsmanshipQualityTier.Excellent;
        if (score < 95f) return CraftsmanshipQualityTier.Masterwork;
        return CraftsmanshipQualityTier.Legendary;
    }

    private static ulong StableHash(ulong seed, string first, string second, int attempt)
    {
        ulong hash = 14695981039346656037UL ^ seed;
        Append(ref hash, first);
        Append(ref hash, second);
        unchecked
        {
            hash ^= (uint)attempt;
            hash *= 1099511628211UL;
        }
        return hash == 0UL ? 0x9E3779B97F4A7C15UL : hash;
    }

    private static void Append(ref ulong hash, string value)
    {
        foreach (char character in value ?? string.Empty)
        {
            unchecked
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }
        }
    }

    private static int NextRange(ref ulong state, int minimum, int maximum)
    {
        unchecked
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            ulong value = state * 2685821657736338717UL;
            int span = maximum - minimum + 1;
            return minimum + (int)(value % (uint)span);
        }
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum RejectedOutputDisposition
{
    AutoDismantle = 0,
    KeepInStorage = 1,
    MarkForSale = 2,
    KeepFacilityAndStop = 3,
    DismantleFacilityAndRetry = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum QualityRepeatLimitMode
{
    SafeLimits = 0,
    UnlimitedUntilSuccess = 1
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum QualityTargetPipelineStage
{
    WaitingForMaterials = 0,
    WaitingForEligibleWorker = 1,
    Working = 2,
    ResolvingQuality = 3,
    WaitingForOutputSpace = 4,
    Dismantling = 5,
    Recovering = 6,
    Rebuilding = 7,
    TargetCurrentlyUnreachable = 8,
    Paused = 9,
    Completed = 10,
    Cancelled = 11
}
