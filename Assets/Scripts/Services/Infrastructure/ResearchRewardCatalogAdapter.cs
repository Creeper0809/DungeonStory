using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ResearchRewardCatalog : IResearchRewardCatalog
{
    private readonly IReadOnlyList<ResearchRewardEntry> all;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ResearchRewardEntry>> byResearch;
    private readonly IReadOnlyDictionary<string, ResearchProjectId> requirementByReward;
    private readonly IResearchProjectCatalog researchProjects;

    public ResearchRewardCatalog(
        IResearchProjectCatalog researchProjects,
        IFacilityShopCatalog facilities,
        IResourceEconomyContentCatalog economy,
        ICombatEquipmentCatalog equipment,
        ISurgicalProcedureCatalog surgicalProcedures)
    {
        this.researchProjects = researchProjects
            ?? throw new ArgumentNullException(nameof(researchProjects));
        List<ResearchRewardEntry> entries = new List<ResearchRewardEntry>();
        foreach (ResearchProjectSO project in researchProjects.Projects)
        {
            foreach (BlueprintBuildingUnlock unlock in project.Unlocks
                         .OfType<BlueprintBuildingUnlock>())
            {
                BuildingSO building = facilities?.FindBuildingById(unlock.buildingId);
                entries.Add(new ResearchRewardEntry(
                    project.ProjectId.Value,
                    ResearchRewardKind.Facility,
                    unlock.buildingId.ToString(),
                    building != null
                        ? FacilityShopService.GetBuildingName(building)
                        : $"시설 {unlock.buildingId}"));
            }
            foreach (BlueprintRecipeUnlock unlock in project.Unlocks
                         .OfType<BlueprintRecipeUnlock>())
            {
                ProductionRecipeSO recipe = null;
                economy?.TryGetRecipe(unlock.recipeId, out recipe);
                entries.Add(new ResearchRewardEntry(
                    project.ProjectId.Value,
                    ResearchRewardKind.ProductionRecipe,
                    unlock.recipeId,
                    recipe?.DisplayName ?? unlock.recipeId));
            }
        }

        foreach (ResourceItemDefinitionSO item in economy?.Items
                     ?? Array.Empty<ResourceItemDefinitionSO>())
        {
            AddRequired(entries, item.RequiredResearchId,
                ResearchRewardKind.ProductionItem, item.ItemId, item.DisplayName);
        }
        foreach (ProductionRecipeSO recipe in economy?.Recipes
                     ?? Array.Empty<ProductionRecipeSO>())
        {
            AddRequired(entries, recipe.RequiredResearchId,
                ResearchRewardKind.ProductionRecipe, recipe.RecipeId, recipe.DisplayName);
        }
        foreach (CombatEquipmentDefinitionSO definition in equipment?.All
                     ?? Array.Empty<CombatEquipmentDefinitionSO>())
        {
            AddRequired(entries, definition.RequiredResearchId,
                ResearchRewardKind.CombatEquipment,
                definition.EquipmentId,
                definition.DisplayName);
        }
        foreach (SurgicalProcedureSO procedure in surgicalProcedures?.Procedures
                     ?? Array.Empty<SurgicalProcedureSO>())
        {
            AddRequired(entries, procedure.RequiredResearchId,
                ResearchRewardKind.MedicalProcedure,
                procedure.ProcedureId,
                procedure.DisplayName);
        }

        all = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ResearchId)
                && !string.IsNullOrWhiteSpace(entry.RewardId))
            .GroupBy(
                entry => $"{entry.ResearchId}|{(int)entry.Kind}|{entry.RewardId}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.ResearchId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Kind)
            .ThenBy(entry => entry.RewardId, StringComparer.Ordinal)
            .ToArray();
        byResearch = all
            .GroupBy(entry => entry.ResearchId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ResearchRewardEntry>)group.ToArray(),
                StringComparer.Ordinal);
        requirementByReward = all
            .GroupBy(entry => RewardKey(entry.Kind, entry.RewardId), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new ResearchProjectId(group.First().ResearchId),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<ResearchRewardEntry> All => all;

    public IReadOnlyList<ResearchRewardEntry> GetRewards(ResearchProjectId researchId) =>
        byResearch.TryGetValue(researchId.Value, out IReadOnlyList<ResearchRewardEntry> rewards)
            ? rewards
            : Array.Empty<ResearchRewardEntry>();

    public bool TryGetRequiredResearch(
        ResearchRewardKind kind,
        string rewardId,
        out ResearchProjectId researchId) =>
        requirementByReward.TryGetValue(RewardKey(kind, rewardId), out researchId);

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = new List<string>();
        foreach (ResearchProjectSO project in researchProjects.Projects)
        {
            if (GetRewards(project.ProjectId).Count == 0)
            {
                errors.Add($"{project.ProjectId}: 직접 해금 보상이 없습니다.");
            }
        }

        foreach (IGrouping<string, ResearchRewardEntry> duplicateAuthority in all
                     .GroupBy(
                         reward => RewardKey(reward.Kind, reward.RewardId),
                         StringComparer.Ordinal)
                     .Where(group => group.Select(entry => entry.ResearchId)
                         .Distinct(StringComparer.Ordinal).Count() > 1))
        {
            errors.Add($"보상 '{duplicateAuthority.Key}'에 연구 요구 ID가 둘 이상 지정되었습니다.");
        }
        return errors;
    }

    private static void AddRequired(
        ICollection<ResearchRewardEntry> entries,
        string researchId,
        ResearchRewardKind kind,
        string rewardId,
        string displayName)
    {
        if (!string.IsNullOrWhiteSpace(researchId))
        {
            entries.Add(new ResearchRewardEntry(
                researchId, kind, rewardId, displayName));
        }
    }

    private static string RewardKey(ResearchRewardKind kind, string rewardId) =>
        $"{(int)kind}:{rewardId?.Trim() ?? string.Empty}";
}
