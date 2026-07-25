using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

public interface IFacilityEvolutionResourceProvider
{
    bool HasMaterial(string materialId, int amount);
    bool ConsumeMaterial(string materialId, int amount);
}

public interface IFacilityEvolutionProposalProvider
{
    FacilityEvolutionProposal Propose(FacilityEvolutionContext context);
}

public readonly struct FacilityEvolutionProposal
{
    public FacilityEvolutionProposal(
        string facilityIdentitySummary,
        IReadOnlyList<string> proposalIds,
        IReadOnlyDictionary<string, string> proposalReasons,
        IReadOnlyList<string> mutationTagSuggestions,
        string flavorText,
        float confidence,
        string source = "",
        string statusMessage = "",
        IReadOnlyDictionary<string, string> rejectedHintTexts = null)
    {
        FacilityIdentitySummary = facilityIdentitySummary ?? string.Empty;
        ProposalIds = EventPayloadSnapshot.Copy(proposalIds);
        ProposalReasons = SnapshotDictionary(proposalReasons);
        MutationTagSuggestions = EventPayloadSnapshot.Copy(mutationTagSuggestions);
        FlavorText = flavorText ?? string.Empty;
        Confidence = Mathf.Clamp01(confidence);
        Source = source ?? string.Empty;
        StatusMessage = statusMessage ?? string.Empty;
        RejectedHintTexts = SnapshotDictionary(rejectedHintTexts);
    }

    public string FacilityIdentitySummary { get; }
    public IReadOnlyList<string> ProposalIds { get; }
    public IReadOnlyDictionary<string, string> ProposalReasons { get; }
    public IReadOnlyList<string> MutationTagSuggestions { get; }
    public string FlavorText { get; }
    public float Confidence { get; }
    public string Source { get; }
    public string StatusMessage { get; }
    public IReadOnlyDictionary<string, string> RejectedHintTexts { get; }
    public bool IsLlmBacked => string.Equals(Source, FacilityEvolutionProposalSources.LocalLlm, StringComparison.Ordinal);

    private static IReadOnlyDictionary<string, string> SnapshotDictionary(
        IReadOnlyDictionary<string, string> source)
    {
        Dictionary<string, string> copy = new Dictionary<string, string>(StringComparer.Ordinal);
        if (source != null)
        {
            foreach (KeyValuePair<string, string> pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    copy[pair.Key] = pair.Value ?? string.Empty;
                }
            }
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }
}

public static class FacilityEvolutionProposalSources
{
    public const string RuleBased = "RuleBased";
    public const string LocalLlm = "LocalLLM";
    public const string RuleBasedWhileLlmPending = "RuleBased+LLM Pending";
    public const string RuleBasedAfterLlmFailure = "RuleBased+LLM Failed";
}

public sealed class RuleBasedFacilityEvolutionProposalProvider : IFacilityEvolutionProposalProvider
{
    public FacilityEvolutionProposal Propose(FacilityEvolutionContext context)
    {
        if (context == null)
        {
            return new FacilityEvolutionProposal(string.Empty, Array.Empty<string>(), null, null, string.Empty, 0f);
        }

        List<FacilityEvolutionRecipeSO> orderedRecipes = context.CandidateRecipes
            .Where((recipe) => recipe != null)
            .OrderByDescending((recipe) => FacilityIdentityPressureUtility.ScoreRecipe(context.Profile, recipe).Score)
            .ThenBy((recipe) => recipe.id)
            .ToList();

        List<string> proposals = orderedRecipes
            .Select((recipe) => recipe.EffectiveId)
            .ToList();

        Dictionary<string, string> reasons = new Dictionary<string, string>();
        Dictionary<string, string> rejectedHints = new Dictionary<string, string>();
        foreach (FacilityEvolutionRecipeSO recipe in orderedRecipes)
        {
            reasons[recipe.EffectiveId] = BuildReason(context.Profile, recipe);
            rejectedHints[recipe.EffectiveId] = BuildRejectedHint(context.Profile, recipe);
        }

        string summary = BuildSummary(context);
        string flavor = !string.IsNullOrWhiteSpace(summary)
            ? $"{FacilityShopService.GetBuildingName(context.Facility.BuildingData)}은(는) {summary} 흐름을 보이고 있습니다."
            : $"{FacilityShopService.GetBuildingName(context.Facility.BuildingData)}의 다음 계보를 검토합니다.";

        return new FacilityEvolutionProposal(
            summary,
            proposals,
            reasons,
            GuessMutationTags(context.Profile),
            flavor,
            0.5f,
            FacilityEvolutionProposalSources.RuleBased,
            rejectedHintTexts: rejectedHints);
    }

    private static string BuildReason(RoomProfile profile, FacilityEvolutionRecipeSO recipe)
    {
        if (profile == null || recipe == null)
        {
            return "진화 후보를 검토합니다.";
        }

        List<string> reasons = new List<string>();
        FacilityEvolutionIdentityScore identityScore =
            FacilityIdentityPressureUtility.ScoreRecipe(profile, recipe);
        if (identityScore.UsesIdentityPressure)
        {
            reasons.Add(identityScore.ToMessage());
        }

        if (profile.GetMetric(FacilityEvolutionTerms.SeatDensity) >= 0.7f)
        {
            reasons.Add("좌석 밀도가 높음");
        }

        if (profile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat) >= 2f)
        {
            reasons.Add("좌석당 장식 점수가 높음");
        }

        if (profile.GetScore(FacilityEvolutionTerms.Combat) >= 20f)
        {
            reasons.Add("전투 환경이 강함");
        }

        if (profile.GetToken(FacilityEvolutionTerms.MercenaryHangout) > 0)
        {
            reasons.Add("용병 이용 기록 있음");
        }

        foreach (KeyValuePair<string, float> pressure in profile.IdentityPressures
            .Where((entry) => entry.Value >= 0.35f)
            .OrderByDescending((entry) => entry.Value)
            .Take(3))
        {
            reasons.Add($"{pressure.Key} 압력 {pressure.Value:0.##}");
        }

        return reasons.Count > 0
            ? string.Join(", ", reasons)
            : $"{recipe.DisplayName} 조건과 현재 방 맥락을 비교합니다.";
    }

    private static string BuildRejectedHint(RoomProfile profile, FacilityEvolutionRecipeSO recipe)
    {
        if (profile == null || recipe == null)
        {
            return "아직 이 계보를 판단할 방 맥락이 부족합니다.";
        }

        FacilityEvolutionIdentityScore identityScore =
            FacilityIdentityPressureUtility.ScoreRecipe(profile, recipe);
        if (identityScore.UsesIdentityPressure && !identityScore.Approved)
        {
            return $"{recipe.DisplayName}의 낌새는 있지만 {identityScore.ToMessage()} 상태입니다.";
        }

        if (profile.GetToken(FacilityEvolutionTerms.MercenaryHangout) <= 0
            && recipe.requiredRecordTokens != null
            && recipe.requiredRecordTokens.Any((token) => token.key == FacilityEvolutionTerms.MercenaryHangout))
        {
            return "전투형 손님이 이 시설을 반복 이용한 기록이 더 필요합니다.";
        }

        if (profile.GetToken(FacilityEvolutionTerms.NoblePatronage) <= 0
            && recipe.requiredRecordTokens != null
            && recipe.requiredRecordTokens.Any((token) => token.key == FacilityEvolutionTerms.NoblePatronage))
        {
            return "고소비 손님이나 귀족 후원 기록이 더 쌓이면 다른 길이 보일 수 있습니다.";
        }

        return $"{recipe.DisplayName}에 가까운 맥락이 있지만 조건이 아직 부족합니다.";
    }

    private static string BuildSummary(FacilityEvolutionContext context)
    {
        RoomProfile profile = context.Profile;
        if (profile == null)
        {
            return string.Empty;
        }

        KeyValuePair<string, float> dominantPressure = profile.IdentityPressures
            .OrderByDescending((entry) => entry.Value)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(dominantPressure.Key) && dominantPressure.Value >= 0.35f)
        {
            return $"{dominantPressure.Key} 정체성이 강한 운영";
        }

        if (profile.GetMetric(FacilityEvolutionTerms.SeatDensity) >= 0.75f)
        {
            return "빽빽한 좌석과 빠른 회전";
        }

        if (profile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat) >= 2f)
        {
            return "여유로운 좌석과 고급 분위기";
        }

        if (profile.GetScore(FacilityEvolutionTerms.Combat) >= 20f)
        {
            return "전투형 손님과 어울리는 거친 분위기";
        }

        return "현재 운영 기록";
    }

    private static IReadOnlyList<string> GuessMutationTags(RoomProfile profile)
    {
        List<string> tags = new List<string>();
        if (profile == null)
        {
            return tags;
        }

        if (profile.GetToken(FacilityEvolutionTerms.FrequentBrawls) > 0
            || profile.GetScore(FacilityEvolutionTerms.Brutal) > 0f)
        {
            tags.Add(FacilityEvolutionTerms.Brutal);
        }

        if (profile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat) >= 2f)
        {
            tags.Add(FacilityEvolutionTerms.Luxury);
        }

        return tags;
    }
}

public sealed class EmptyFacilityEvolutionResourceProvider : IFacilityEvolutionResourceProvider
{
    public bool HasMaterial(string materialId, int amount)
    {
        return string.IsNullOrWhiteSpace(materialId) || amount <= 0;
    }

    public bool ConsumeMaterial(string materialId, int amount)
    {
        return HasMaterial(materialId, amount);
    }
}

public sealed class MemoryFacilityEvolutionResourceProvider : IFacilityEvolutionResourceProvider
{
    private readonly Dictionary<string, int> materials = new Dictionary<string, int>();

    public void SetMaterial(string materialId, int amount)
    {
        if (!string.IsNullOrWhiteSpace(materialId))
        {
            materials[materialId] = Mathf.Max(0, amount);
        }
    }

    public bool HasMaterial(string materialId, int amount)
    {
        if (string.IsNullOrWhiteSpace(materialId) || amount <= 0)
        {
            return true;
        }

        return materials.TryGetValue(materialId, out int current) && current >= amount;
    }

    public bool ConsumeMaterial(string materialId, int amount)
    {
        if (!HasMaterial(materialId, amount))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(materialId) && amount > 0)
        {
            materials[materialId] -= amount;
        }

        return true;
    }
}

public sealed class FacilityEvolutionContext
{
    public FacilityEvolutionContext(
        BuildableObject facility,
        FacilityEvolutionStateComponent state,
        RoomProfile profile,
        IReadOnlyList<FacilityEvolutionRecipeSO> candidateRecipes)
    {
        Facility = facility;
        State = state;
        Profile = profile;
        CandidateRecipes = EventPayloadSnapshot.Copy(candidateRecipes);
    }

    public BuildableObject Facility { get; }
    public FacilityEvolutionStateComponent State { get; }
    public RoomProfile Profile { get; }
    public IReadOnlyList<FacilityEvolutionRecipeSO> CandidateRecipes { get; }
}

public sealed class FacilityEvolutionValidationResult
{
    private readonly List<string> reasons = new List<string>();
    private readonly List<FacilityEvolutionValidationCheck> checks = new List<FacilityEvolutionValidationCheck>();
    private readonly IReadOnlyList<string> reasonsView;
    private readonly IReadOnlyList<FacilityEvolutionValidationCheck> checksView;

    public FacilityEvolutionValidationResult()
    {
        reasonsView = ReadOnlyView.List(reasons);
        checksView = ReadOnlyView.List(checks);
    }

    public bool Approved => reasons.Count == 0;
    public IReadOnlyList<string> RejectionReasons => reasonsView;
    public IReadOnlyList<FacilityEvolutionValidationCheck> Checks => checksView;

    public void Reject(string reason)
    {
        if (!string.IsNullOrWhiteSpace(reason))
        {
            reasons.Add(reason);
            checks.Add(new FacilityEvolutionValidationCheck("기타", "검증", false, reason));
        }
    }

    public void AddCheck(string category, string label, bool passed, string detail = "")
    {
        string safeDetail = detail ?? string.Empty;
        checks.Add(new FacilityEvolutionValidationCheck(category, label, passed, safeDetail));
        if (!passed && !string.IsNullOrWhiteSpace(safeDetail))
        {
            reasons.Add(safeDetail);
        }
    }

    public string ToMessage()
    {
        return Approved ? "진화 가능" : string.Join(", ", reasons);
    }
}

public readonly struct FacilityEvolutionValidationCheck
{
    public FacilityEvolutionValidationCheck(
        string category,
        string label,
        bool passed,
        string detail)
    {
        Category = category ?? string.Empty;
        Label = label ?? string.Empty;
        Passed = passed;
        Detail = detail ?? string.Empty;
    }

    public string Category { get; }
    public string Label { get; }
    public bool Passed { get; }
    public string Detail { get; }
}

public sealed class FacilityEvolutionCandidate
{
    public FacilityEvolutionCandidate(
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionValidationResult validation,
        string reason)
        : this(recipe, validation, reason, false, string.Empty, string.Empty, string.Empty, default, string.Empty)
    {
    }

    public FacilityEvolutionCandidate(
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionValidationResult validation,
        string reason,
        bool proposed,
        string flavorText,
        string proposalSource = "",
        string proposalStatusMessage = "")
        : this(recipe, validation, reason, proposed, flavorText, proposalSource, proposalStatusMessage, default, string.Empty)
    {
    }

    public FacilityEvolutionCandidate(
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionValidationResult validation,
        string reason,
        bool proposed,
        string flavorText,
        string proposalSource,
        string proposalStatusMessage,
        FacilityEvolutionIdentityScore identityScore,
        string rejectedHintText = "")
    {
        Recipe = recipe;
        Validation = validation;
        Reason = reason ?? string.Empty;
        Proposed = proposed;
        FlavorText = flavorText ?? string.Empty;
        ProposalSource = proposalSource ?? string.Empty;
        ProposalStatusMessage = proposalStatusMessage ?? string.Empty;
        IdentityScore = identityScore;
        RejectedHintText = rejectedHintText ?? string.Empty;
    }

    public FacilityEvolutionRecipeSO Recipe { get; }
    public FacilityEvolutionValidationResult Validation { get; }
    public bool Approved => Validation != null && Validation.Approved;
    public string Reason { get; }
    public bool Proposed { get; }
    public string FlavorText { get; }
    public string ProposalSource { get; }
    public string ProposalStatusMessage { get; }
    public FacilityEvolutionIdentityScore IdentityScore { get; }
    public string RejectedHintText { get; }
}

public static class FacilityEvolutionUtility
{
    public static string GetFacilityId(BuildingSO building)
    {
        if (building == null)
        {
            return string.Empty;
        }

        return building.id > 0 ? building.id.ToString() : FacilityShopService.GetBuildingName(building);
    }

    public static IEnumerable<string> GetDefaultLineageTags(BuildingSO building)
    {
        if (building == null)
        {
            yield break;
        }

        FacilityData facility = building.Facility;
        if (facility != null)
        {
            if (facility.SupportsRole(FacilityRole.Meal)) yield return FacilityEvolutionTerms.Dining;
            if (facility.SupportsRole(FacilityRole.Purchase)) yield return "Shop";
            if (facility.SupportsRole(FacilityRole.Rest)) yield return FacilityEvolutionTerms.Rest;
            if (facility.SupportsRole(FacilityRole.Training)) yield return FacilityEvolutionTerms.Training;
            if (facility.SupportsRole(FacilityRole.Research)) yield return FacilityEvolutionTerms.Research;
            if (facility.SupportsRole(FacilityRole.Mana)) yield return FacilityEvolutionTerms.Mana;
            if (facility.SupportsRole(FacilityRole.Logistics)) yield return FacilityEvolutionTerms.Logistics;
        }

        DefenseFacilityData defense = building.Defense;
        if (defense != null && defense.IsDefenseFacility)
        {
            yield return FacilityEvolutionTerms.Defense;
            yield return FacilityEvolutionTerms.Combat;
        }
    }
}

public static class FacilityEvolutionService
{
    public static bool IsRecipeVisible(
        FacilityEvolutionRecipeSO recipe,
        BlueprintResearchState researchState,
        IMetaProgressionRuntimeReader metaProgressionReader)
    {
        if (recipe == null || !recipe.HasValidData)
        {
            return false;
        }

        if (recipe.publicByDefault && string.IsNullOrWhiteSpace(recipe.requiredResearchRecipeId))
        {
            return true;
        }

        if (metaProgressionReader != null
            && (metaProgressionReader.IsRecipePreserved(recipe.requiredResearchRecipeId)
                || metaProgressionReader.IsRecipePreserved(recipe.EffectiveId)))
        {
            return true;
        }

        return researchState != null
            && !string.IsNullOrWhiteSpace(recipe.requiredResearchRecipeId)
            && researchState.UnlockedRecipeIds.Contains(recipe.requiredResearchRecipeId);
    }

    public static IReadOnlyList<FacilityEvolutionRecipeSO> GetSourceCandidates(
        BuildableObject facility,
        IReadOnlyList<FacilityEvolutionRecipeSO> recipes,
        BlueprintResearchState researchState,
        IFacilityEvolutionRecipeQuery recipeQuery,
        IFacilityEvolutionStateComponentFactory stateComponentFactory)
    {
        if (facility == null || recipes == null)
        {
            return Array.Empty<FacilityEvolutionRecipeSO>();
        }

        if (stateComponentFactory == null)
        {
            throw new ArgumentNullException(nameof(stateComponentFactory));
        }

        FacilityEvolutionStateComponent state = stateComponentFactory.GetOrAdd(facility);
        return recipes
            .Where((recipe) => recipe != null
                && recipe.HasValidData
                && recipeQuery != null
                && recipeQuery.IsVisible(recipe, researchState)
                && recipe.MatchesSource(facility, state)
                && state.StarGrade >= Mathf.Max(1, recipe.requiredStarGrade))
            .ToList();
    }

    public static FacilityEvolutionValidationResult Validate(
        BuildableObject facility,
        FacilityEvolutionRecipeSO recipe,
        RoomProfile profile,
        BlueprintResearchState researchState,
        IFacilityEvolutionResourceProvider resources,
        IFacilityEvolutionRecipeQuery recipeQuery,
        IFacilityEvolutionStateComponentFactory stateComponentFactory)
    {
        FacilityEvolutionValidationResult result = new FacilityEvolutionValidationResult();
        if (facility == null || facility.isDestroy)
        {
            result.AddCheck("하드 조건", "대상 시설", false, "대상 시설 없음");
            return result;
        }

        if (recipe == null || !recipe.HasValidData)
        {
            result.AddCheck("하드 조건", "진화식", false, "진화식 오류");
            return result;
        }

        if (stateComponentFactory == null)
        {
            throw new ArgumentNullException(nameof(stateComponentFactory));
        }

        FacilityEvolutionStateComponent state = stateComponentFactory.GetOrAdd(facility);
        result.AddCheck(
            "하드 조건",
            "현재 시설/계보",
            recipe.MatchesSource(facility, state),
            "현재 시설 계보와 맞지 않음");

        int requiredStar = Mathf.Max(1, recipe.requiredStarGrade);
        result.AddCheck(
            "하드 조건",
            "성급",
            state.StarGrade >= requiredStar,
            $"성급 부족 {state.StarGrade}/{requiredStar}");

        result.AddCheck(
            "하드 조건",
            "연구/해금",
            recipeQuery != null && recipeQuery.IsVisible(recipe, researchState),
            "연구 필요");

        bool usableRoom = !recipe.requireUsableRoom || profile != null && profile.IsUsable;
        result.AddCheck(
            "방 조건",
            "사용 가능한 방",
            usableRoom,
            "사용 가능한 방 조건 미달");

        ValidateTags(recipe, profile, result);
        ValidateMetricRequirements("방 점수", recipe.requiredRoomScores, profile?.Scores, result);
        ValidateMetricRequirements("방 지표", recipe.requiredRoomMetrics, profile?.Metrics, result);
        ValidateTokenRequirements(recipe.requiredRecordTokens, profile?.RecordTokens, result);
        ValidateIdentityPressure(recipe, profile, result);
        ValidateUniqueFixtures(recipe, profile, result);
        ValidateMaterials(recipe.requiredMaterials, resources, result);
        return result;
    }

    private static void ValidateTags(
        FacilityEvolutionRecipeSO recipe,
        RoomProfile profile,
        FacilityEvolutionValidationResult result)
    {
        if (recipe.requiredRoomTags == null)
        {
            return;
        }

        foreach (string tag in recipe.requiredRoomTags.Where((tag) => !string.IsNullOrWhiteSpace(tag)))
        {
            if (profile == null || !profile.HasTag(tag))
            {
                result.AddCheck("방 태그", tag, false, $"태그 부족 {tag}");
            }
            else
            {
                result.AddCheck("방 태그", tag, true);
            }
        }
    }

    private static void ValidateMetricRequirements(
        string label,
        FacilityEvolutionMetricRequirement[] requirements,
        IReadOnlyDictionary<string, float> values,
        FacilityEvolutionValidationResult result)
    {
        if (requirements == null)
        {
            return;
        }

        foreach (FacilityEvolutionMetricRequirement requirement in requirements)
        {
            if (!requirement.IsSatisfied(values, out string reason))
            {
                result.AddCheck(label, requirement.key, false, $"{label} {reason}");
            }
            else if (!string.IsNullOrWhiteSpace(requirement.key))
            {
                result.AddCheck(label, requirement.key, true);
            }
        }
    }

    private static void ValidateTokenRequirements(
        FacilityEvolutionTokenRequirement[] requirements,
        IReadOnlyDictionary<string, int> tokens,
        FacilityEvolutionValidationResult result)
    {
        if (requirements == null)
        {
            return;
        }

        foreach (FacilityEvolutionTokenRequirement requirement in requirements)
        {
            if (!requirement.IsSatisfied(tokens, out string reason))
            {
                result.AddCheck("기록", requirement.key, false, $"기록 부족 {reason}");
            }
            else if (!string.IsNullOrWhiteSpace(requirement.key))
            {
                result.AddCheck("기록", requirement.key, true);
            }
        }
    }

    private static void ValidateIdentityPressure(
        FacilityEvolutionRecipeSO recipe,
        RoomProfile profile,
        FacilityEvolutionValidationResult result)
    {
        FacilityEvolutionIdentityScore score =
            FacilityIdentityPressureUtility.ScoreRecipe(profile, recipe);
        if (!score.UsesIdentityPressure)
        {
            return;
        }

        if (!score.Approved)
        {
            result.AddCheck("정체성", "정체성 점수", false, score.ToMessage());
        }
        else
        {
            result.AddCheck("정체성", "정체성 점수", true, score.ToMessage());
        }
    }

    private static void ValidateUniqueFixtures(
        FacilityEvolutionRecipeSO recipe,
        RoomProfile profile,
        FacilityEvolutionValidationResult result)
    {
        if (recipe.requiredUniqueFixtures == null)
        {
            return;
        }

        foreach (BuildingSO required in recipe.requiredUniqueFixtures.Where((building) => building != null))
        {
            bool found = profile != null
                && profile.Fixtures.Any((fixture) => fixture != null && fixture.id == required.id);
            if (!found)
            {
                result.AddCheck(
                    "고유 기물",
                    FacilityShopService.GetBuildingName(required),
                    false,
                    $"고유 기물 필요 {FacilityShopService.GetBuildingName(required)}");
            }
            else
            {
                result.AddCheck("고유 기물", FacilityShopService.GetBuildingName(required), true);
            }
        }
    }

    private static void ValidateMaterials(
        FacilityEvolutionMaterialRequirement[] requirements,
        IFacilityEvolutionResourceProvider resources,
        FacilityEvolutionValidationResult result)
    {
        if (requirements == null)
        {
            return;
        }

        IFacilityEvolutionResourceProvider provider = resources
            ?? throw new ArgumentNullException(nameof(resources));
        foreach (FacilityEvolutionMaterialRequirement requirement in requirements)
        {
            int amount = Mathf.Max(1, requirement.amount);
            if (!provider.HasMaterial(requirement.materialId, amount))
            {
                result.AddCheck(
                    "재료",
                    requirement.materialId,
                    false,
                    $"재료 부족 {requirement.materialId} x{amount}");
            }
            else
            {
                result.AddCheck("재료", requirement.materialId, true);
            }
        }
    }
}
