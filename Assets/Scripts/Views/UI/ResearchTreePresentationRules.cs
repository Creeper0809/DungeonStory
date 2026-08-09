using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class ResearchTreePresentationRules
{
    private readonly IResearchBlueprintArchiveQuery archiveQuery;
    private readonly IFacilityShopCatalog facilityCatalog;
    private readonly IResearchRewardCatalog rewardCatalog;

    public ResearchTreePresentationRules(
        IResearchBlueprintArchiveQuery archiveQuery,
        IFacilityShopCatalog facilityCatalog,
        IResearchRewardCatalog rewardCatalog)
    {
        this.archiveQuery = archiveQuery
            ?? throw new ArgumentNullException(nameof(archiveQuery));
        this.facilityCatalog = facilityCatalog
            ?? throw new ArgumentNullException(nameof(facilityCatalog));
        this.rewardCatalog = rewardCatalog
            ?? throw new ArgumentNullException(nameof(rewardCatalog));
    }

    public bool MatchesFilter(
        ResearchProjectSO project,
        ResearchField? selectedField,
        string search)
    {
        if (selectedField.HasValue && project.Field != selectedField.Value)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return project.DisplayName.Contains(
                   search,
                   StringComparison.OrdinalIgnoreCase)
            || project.Description.Contains(
                search,
                StringComparison.OrdinalIgnoreCase)
            || FormatUnlocks(project).Contains(
                search,
                StringComparison.OrdinalIgnoreCase);
    }

    public string FormatBlueprintDetail(ResearchProjectSO project)
    {
        if (project.BlueprintRule == ResearchBlueprintRule.None)
        {
            return "필요 없음";
        }

        ResearchBlueprintArchiveStatus status =
            archiveQuery.GetStatus(project.Blueprint);
        string rule = project.BlueprintRule == ResearchBlueprintRule.Required
            ? "필수"
            : "선행 우회";
        string location = status.IsArchived
            ? status.Location
            : status.IsInTransit
                ? "운반 중"
                : "미보유";
        return $"{project.Blueprint.DisplayName} ({rule}, {location})";
    }

    public string FormatUnlocks(ResearchProjectSO project)
    {
        IReadOnlyList<ResearchRewardEntry> rewards =
            rewardCatalog.GetRewards(project.ProjectId);
        if (rewards.Count > 0)
        {
            return string.Join("\n", rewards
                .GroupBy(reward => reward.Kind)
                .OrderBy(group => GetRewardDisplayOrder(group.Key))
                .Select(group =>
                    $"<b>{FormatRewardKind(group.Key)}</b>  "
                    + string.Join(", ", group.Select(reward => reward.DisplayName))));
        }

        List<string> values = new List<string>();
        foreach (BlueprintUnlock unlock in project.Unlocks.Where(
                     unlock => unlock != null))
        {
            switch (unlock)
            {
                case IBlueprintBuildingUnlock buildingUnlock:
                {
                    BuildingSO building = FacilityShopService.FindBuildingById(
                        facilityCatalog,
                        buildingUnlock.BuildingId);
                    values.Add(building != null
                        ? FacilityShopService.GetBuildingName(building)
                        : $"시설 {buildingUnlock.BuildingId}");
                    break;
                }
                case BlueprintRecipeUnlock recipe:
                    values.Add(recipe.recipeId);
                    break;
            }
        }

        return values.Count == 0
            ? "없음"
            : string.Join(", ", values.Distinct());
    }

    private static int GetRewardDisplayOrder(ResearchRewardKind kind) => kind switch
    {
        ResearchRewardKind.Facility => 0,
        ResearchRewardKind.CraftMaterial => 10,
        ResearchRewardKind.Crop => 15,
        ResearchRewardKind.ProductionRecipe => 20,
        ResearchRewardKind.ProductionItem => 30,
        ResearchRewardKind.InstallationComponent => 35,
        ResearchRewardKind.CombatEquipment => 40,
        ResearchRewardKind.EnvironmentalWorkwear => 45,
        ResearchRewardKind.Ammunition => 50,
        ResearchRewardKind.MedicalProcedure => 60,
        _ => 100
    };

    private static string FormatRewardKind(ResearchRewardKind kind) => kind switch
    {
        ResearchRewardKind.Facility => "핵심 시설",
        ResearchRewardKind.CraftMaterial => "신규 재료",
        ResearchRewardKind.Crop => "작물과 종자",
        ResearchRewardKind.ProductionRecipe => "생산 조합식",
        ResearchRewardKind.ProductionItem => "제작 아이템",
        ResearchRewardKind.InstallationComponent => "설치 부품",
        ResearchRewardKind.CombatEquipment => "무기·방어구·방패",
        ResearchRewardKind.EnvironmentalWorkwear => "환경 작업복",
        ResearchRewardKind.Ammunition => "탄약과 전투 소모품",
        ResearchRewardKind.MedicalProcedure => "의료 시술",
        _ => kind.ToString()
    };

    public static float CalculateRemainingPrerequisiteWork(
        ResearchProjectSO project,
        BlueprintResearchRuntime runtime)
    {
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        float total = 0f;
        void Visit(ResearchProjectSO current)
        {
            foreach (ResearchProjectSO prerequisite in current?.Prerequisites
                         ?? Array.Empty<ResearchProjectSO>())
            {
                if (prerequisite == null
                    || !visited.Add(prerequisite.ProjectId.Value)
                    || runtime.State.Projects.IsCompleted(
                        prerequisite.ProjectId))
                {
                    continue;
                }
                ResearchProjectProgressState progress =
                    runtime.State.Projects.GetProgress(prerequisite.ProjectId);
                total += Mathf.Max(
                    0f,
                    prerequisite.RequiredWork - progress.Progress);
                Visit(prerequisite);
            }
        }

        Visit(project);
        return total;
    }

    public static string FormatField(ResearchField field)
    {
        return field switch
        {
            ResearchField.LifeAndSurvival => "생활·생존",
            ResearchField.CommerceAndCraft => "상업·제작",
            ResearchField.DefenseAndTactics => "방어·전술",
            ResearchField.RecordsAndArcane => "기록·비전",
            ResearchField.CaptivityAndEntertainment => "포로·흥행",
            ResearchField.AuthorityAndHousing => "권위·주거",
            ResearchField.Agriculture => "재배",
            ResearchField.Forestry => "임업",
            ResearchField.Mining => "채광",
            ResearchField.Husbandry => "축산",
            ResearchField.Metallurgy => "금속",
            ResearchField.Textiles => "직물",
            ResearchField.Cuisine => "요리",
            ResearchField.Pharmacology => "약리",
            ResearchField.SurgeryAndTransplant => "외과·이식",
            _ => "기타"
        };
    }

    public static string FormatNodeState(ResearchNodeState state)
    {
        return state switch
        {
            ResearchNodeState.Completed => "완료",
            ResearchNodeState.Active => "진행 중",
            ResearchNodeState.Queued => "대기",
            ResearchNodeState.Suspended => "일시 중단",
            ResearchNodeState.Available => "연구 가능",
            ResearchNodeState.BlueprintInTransit => "설계도 운반 중",
            ResearchNodeState.ShortcutAvailable => "설계도 우회 가능",
            _ => "조건 부족"
        };
    }

    public static Color GetNodeColor(ResearchNodeState state)
    {
        return state switch
        {
            ResearchNodeState.Completed => new Color(0.16f, 0.34f, 0.27f, 1f),
            ResearchNodeState.Active => DungeonUiTheme.AccentPressed,
            ResearchNodeState.Queued => new Color(0.23f, 0.31f, 0.36f, 1f),
            ResearchNodeState.Suspended => new Color(0.34f, 0.27f, 0.2f, 1f),
            ResearchNodeState.Available => DungeonUiTheme.SurfaceRaised,
            ResearchNodeState.BlueprintInTransit =>
                new Color(0.28f, 0.31f, 0.22f, 1f),
            ResearchNodeState.ShortcutAvailable =>
                new Color(0.38f, 0.31f, 0.13f, 1f),
            _ => new Color(0.11f, 0.16f, 0.18f, 1f)
        };
    }

    public static Color GetStateTextColor(ResearchNodeState state)
    {
        return state switch
        {
            ResearchNodeState.Completed => DungeonUiTheme.Good,
            ResearchNodeState.Active => Color.white,
            ResearchNodeState.Suspended => DungeonUiTheme.Warning,
            ResearchNodeState.ShortcutAvailable => DungeonUiTheme.Warning,
            _ => DungeonUiTheme.TextSecondary
        };
    }

    public static Color GetConnectorColor(
        ResearchNodeState state,
        bool shortcut)
    {
        if (shortcut)
        {
            return new Color(0.83f, 0.64f, 0.23f, 0.9f);
        }
        return state is ResearchNodeState.Completed or ResearchNodeState.Active
            ? DungeonUiTheme.Accent
            : new Color(0.42f, 0.5f, 0.52f, 0.42f);
    }
}
