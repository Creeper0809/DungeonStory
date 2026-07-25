using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum MetaProgressionBranch
{
    OperationKnowledge,
    DesignPreservation,
    OwnerSurvival,
    CommerceLogistics,
    FortressDefense,
    ArcaneResearch
}

public sealed class MetaUpgradeDefinition
{
    public MetaUpgradeDefinition(
        string id,
        MetaProgressionBranch branch,
        string title,
        string detail,
        int cost,
        int maxLevel,
        IReadOnlyList<IMetaUpgradeEffect> effects)
    {
        this.id = id?.Trim() ?? string.Empty;
        this.branch = branch;
        this.title = title?.Trim() ?? string.Empty;
        this.detail = detail?.Trim() ?? string.Empty;
        this.cost = Mathf.Max(0, cost);
        this.maxLevel = Mathf.Max(1, maxLevel);
        this.effects = EventPayloadSnapshot.Copy(effects);
    }

    public string id { get; }
    public MetaProgressionBranch branch { get; }
    public string title { get; }
    public string detail { get; }
    public int cost { get; }
    public int maxLevel { get; }
    public IReadOnlyList<IMetaUpgradeEffect> effects { get; }
}

public sealed class MetaProgressionState
{
    private readonly Dictionary<string, int> upgradeLevels = new Dictionary<string, int>(System.StringComparer.Ordinal);
    private readonly HashSet<string> preservedRecipeIds = new HashSet<string>();
    private readonly IReadOnlyDictionary<string, int> upgradeLevelsView;
    private readonly IReadOnlyCollection<string> preservedRecipeIdsView;

    public MetaProgressionState()
    {
        upgradeLevelsView = ReadOnlyView.Dictionary(upgradeLevels);
        preservedRecipeIdsView = ReadOnlyView.Collection(preservedRecipeIds);
    }

    public int LifetimeEarnedCurrency { get; private set; }
    public int SpentCurrency { get; private set; }
    public int CompletedRunCount { get; private set; }
    public int AvailableCurrency => Mathf.Max(0, LifetimeEarnedCurrency - SpentCurrency);
    public IReadOnlyDictionary<string, int> UpgradeLevels => upgradeLevelsView;
    public IReadOnlyCollection<string> PreservedRecipeIds => preservedRecipeIdsView;

    public void AddCurrency(int amount)
    {
        LifetimeEarnedCurrency += Mathf.Max(0, amount);
    }

    public void RecordRunCompleted()
    {
        CompletedRunCount++;
    }

    public int GetUpgradeLevel(string id)
    {
        return !string.IsNullOrWhiteSpace(id)
            && upgradeLevels.TryGetValue(id, out int level)
                ? level
                : 0;
    }

    public bool TryPurchaseUpgrade(string id, out string message)
    {
        MetaUpgradeDefinition definition = MetaProgressionCatalog.Get(id);
        if (definition == null)
        {
            message = "강화 정보가 없습니다";
            return false;
        }

        int currentLevel = GetUpgradeLevel(id);
        if (currentLevel >= Mathf.Max(1, definition.maxLevel))
        {
            message = "이미 최대 레벨입니다";
            return false;
        }

        int cost = Mathf.Max(0, definition.cost);
        if (AvailableCurrency < cost)
        {
            message = "계승 재화가 부족합니다";
            return false;
        }

        SpentCurrency += cost;
        upgradeLevels[id] = currentLevel + 1;
        message = $"{definition.title} Lv.{currentLevel + 1}";
        return true;
    }

    public void SetUpgradeLevelForDebug(string id, int level)
    {
        MetaUpgradeDefinition definition = MetaProgressionCatalog.Get(id);
        if (definition == null) return;

        upgradeLevels[id] = Mathf.Clamp(level, 0, Mathf.Max(1, definition.maxLevel));
    }

    public void PreserveRecipes(IEnumerable<string> recipeIds, int maxCount)
    {
        if (recipeIds == null || maxCount <= 0)
        {
            return;
        }

        foreach (string recipeId in recipeIds.Where((id) => !string.IsNullOrWhiteSpace(id)).Take(maxCount))
        {
            preservedRecipeIds.Add(recipeId);
        }
    }

    public void Restore(
        int lifetimeEarnedCurrency,
        int spentCurrency,
        IEnumerable<KeyValuePair<string, int>> savedUpgradeLevels,
        IEnumerable<string> savedRecipeIds,
        int completedRunCount = 0)
    {
        LifetimeEarnedCurrency = Mathf.Max(0, lifetimeEarnedCurrency);
        SpentCurrency = Mathf.Clamp(spentCurrency, 0, LifetimeEarnedCurrency);
        CompletedRunCount = Mathf.Max(0, completedRunCount);
        upgradeLevels.Clear();
        preservedRecipeIds.Clear();

        foreach (KeyValuePair<string, int> pair in savedUpgradeLevels
            ?? System.Array.Empty<KeyValuePair<string, int>>())
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            {
                MetaUpgradeDefinition definition = MetaProgressionCatalog.Get(pair.Key);
                int maximum = definition != null ? Mathf.Max(1, definition.maxLevel) : pair.Value;
                upgradeLevels[pair.Key] = Mathf.Clamp(pair.Value, 0, maximum);
            }
        }

        foreach (string recipeId in savedRecipeIds ?? System.Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(recipeId))
            {
                preservedRecipeIds.Add(recipeId.Trim());
            }
        }
    }

    public void Merge(
        int lifetimeEarnedCurrency,
        int spentCurrency,
        IEnumerable<KeyValuePair<string, int>> savedUpgradeLevels,
        IEnumerable<string> savedRecipeIds,
        int completedRunCount = 0)
    {
        LifetimeEarnedCurrency = Mathf.Max(LifetimeEarnedCurrency, lifetimeEarnedCurrency);
        CompletedRunCount = Mathf.Max(CompletedRunCount, completedRunCount);
        SpentCurrency = Mathf.Clamp(
            Mathf.Max(SpentCurrency, spentCurrency),
            0,
            LifetimeEarnedCurrency);

        foreach (KeyValuePair<string, int> pair in savedUpgradeLevels
            ?? System.Array.Empty<KeyValuePair<string, int>>())
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
            {
                continue;
            }

            MetaUpgradeDefinition definition = MetaProgressionCatalog.Get(pair.Key);
            int maximum = definition != null ? Mathf.Max(1, definition.maxLevel) : pair.Value;
            int current = GetUpgradeLevel(pair.Key);
            upgradeLevels[pair.Key] = Mathf.Max(current, Mathf.Clamp(pair.Value, 0, maximum));
        }

        foreach (string recipeId in savedRecipeIds ?? System.Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(recipeId))
            {
                preservedRecipeIds.Add(recipeId.Trim());
            }
        }
    }
}

public sealed class RunResultSnapshot
{
    public RunResultSnapshot(
        string ownerName = "",
        string endReason = "",
        float survivalSeconds = 0f,
        int survivedOperatingDays = 0,
        int settlementCount = 0,
        int defendedInvasionCount = 0,
        InvasionThreatStage maxThreatStage = InvasionThreatStage.Peaceful,
        float finalInvasionThreat = 0f,
        int firstDiscoveredFacilityCount = 0,
        int firstUnlockedRecipeCount = 0,
        int offenseSuccessCount = 0,
        float difficultyMultiplier = 1f,
        int legacyCurrency = 0,
        DungeonRunOutcome outcome = DungeonRunOutcome.Defeat,
        DungeonDifficulty difficulty = DungeonDifficulty.Normal)
    {
        this.ownerName = ownerName ?? string.Empty;
        this.endReason = endReason ?? string.Empty;
        this.survivalSeconds = Mathf.Max(0f, survivalSeconds);
        this.survivedOperatingDays = Mathf.Max(0, survivedOperatingDays);
        this.settlementCount = Mathf.Max(0, settlementCount);
        this.defendedInvasionCount = Mathf.Max(0, defendedInvasionCount);
        this.maxThreatStage = maxThreatStage;
        this.finalInvasionThreat = Mathf.Max(0f, finalInvasionThreat);
        this.firstDiscoveredFacilityCount = Mathf.Max(0, firstDiscoveredFacilityCount);
        this.firstUnlockedRecipeCount = Mathf.Max(0, firstUnlockedRecipeCount);
        this.offenseSuccessCount = Mathf.Max(0, offenseSuccessCount);
        this.difficultyMultiplier = Mathf.Max(0.1f, difficultyMultiplier);
        this.legacyCurrency = Mathf.Max(0, legacyCurrency);
        this.outcome = outcome == DungeonRunOutcome.None ? DungeonRunOutcome.Defeat : outcome;
        this.difficulty = DungeonDifficultyRules.Normalize((int)difficulty);
    }

    public string ownerName { get; }
    public string endReason { get; }
    public float survivalSeconds { get; }
    public int survivedOperatingDays { get; }
    public int settlementCount { get; }
    public int defendedInvasionCount { get; }
    public InvasionThreatStage maxThreatStage { get; }
    public float finalInvasionThreat { get; }
    public int firstDiscoveredFacilityCount { get; }
    public int firstUnlockedRecipeCount { get; }
    public int offenseSuccessCount { get; }
    public float difficultyMultiplier { get; }
    public int legacyCurrency { get; }
    public DungeonRunOutcome outcome { get; }
    public DungeonDifficulty difficulty { get; }

    public RunResultSnapshot WithLegacyCurrency(int value)
    {
        return new RunResultSnapshot(
            ownerName,
            endReason,
            survivalSeconds,
            survivedOperatingDays,
            settlementCount,
            defendedInvasionCount,
            maxThreatStage,
            finalInvasionThreat,
            firstDiscoveredFacilityCount,
            firstUnlockedRecipeCount,
            offenseSuccessCount,
            difficultyMultiplier,
            value,
            outcome,
            difficulty);
    }

    public string ToDetailText()
    {
        return string.Join("\n", new[]
        {
            outcome == DungeonRunOutcome.Victory ? "런 승리" : "런 패배",
            string.Empty,
            $"사장: {TextOrDefault(ownerName, "미상")}",
            $"결과: {TextOrDefault(endReason, outcome == DungeonRunOutcome.Victory ? "오펜스 완수와 진실 공개" : "사장 쓰러짐")}",
            $"생존 시간: {FormatTime(survivalSeconds)}",
            $"생존 운영일: {survivedOperatingDays}",
            $"운영일 정산 횟수: {settlementCount}",
            $"막아낸 침입: {defendedInvasionCount}",
            $"최대 위협 단계: {FormatThreatStage(maxThreatStage)}",
            $"최종 방어 위협: {finalInvasionThreat:0.#}",
            $"최초 발견 시설: {firstDiscoveredFacilityCount}",
            $"최초 해금 조합식: {firstUnlockedRecipeCount}",
            $"오펜스 성공: {offenseSuccessCount}",
            $"난이도: {difficulty} / 보상 배율 x{difficultyMultiplier:0.##}",
            string.Empty,
            $"획득 계승 재화: {legacyCurrency}",
            string.Empty,
            "계승되지 않음: 현재 런의 돈, 재고, 배치 시설"
        });
    }

    private static string FormatTime(float seconds)
    {
        int safeSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
        int minutes = safeSeconds / 60;
        int remainSeconds = safeSeconds % 60;
        return $"{minutes:0}:{remainSeconds:00}";
    }

    private static string TextOrDefault(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static string FormatThreatStage(InvasionThreatStage stage)
    {
        return stage switch
        {
            InvasionThreatStage.Peaceful => "평온",
            InvasionThreatStage.Warning => "경고",
            InvasionThreatStage.Candidate => "침입 후보",
            InvasionThreatStage.Safety => "안전 시간",
            _ => stage.ToString()
        };
    }
}
