using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Research/Project", order = 0)]
public sealed class ResearchProjectSO : DataScriptableObject
{
    public const string ResourcePath = "SO/Research/Projects";

    [SerializeField] private string projectId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private ResearchField field;
    [Min(1f), SerializeField] private float requiredWork = 40f;
    [SerializeField] private List<ResearchProjectSO> prerequisites = new List<ResearchProjectSO>();
    [SerializeField] private ResearchBlueprintRule blueprintRule;
    [SerializeField] private FacilityBlueprintSO blueprint;
    [SerializeField] private BlueprintUnlockCollection unlocks = new BlueprintUnlockCollection();
    [SerializeField] private Sprite icon;

    public ResearchProjectId ProjectId => new ResearchProjectId(projectId);
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public ResearchField Field => field;
    public float RequiredWork => Mathf.Max(1f, requiredWork);
    public IReadOnlyList<ResearchProjectSO> Prerequisites =>
        prerequisites ??= new List<ResearchProjectSO>();
    public ResearchBlueprintRule BlueprintRule => blueprintRule;
    public FacilityBlueprintSO Blueprint => blueprint;
    public IReadOnlyList<BlueprintUnlock> Unlocks =>
        (unlocks ??= new BlueprintUnlockCollection()).Items;
    public BlueprintUnlockCollection UnlockCollection =>
        unlocks ??= new BlueprintUnlockCollection();
    public Sprite Icon => icon;

    public void Configure(
        string stableId,
        string name,
        string projectDescription,
        ResearchField researchField,
        float work,
        ResearchBlueprintRule rule,
        FacilityBlueprintSO requiredBlueprint,
        IEnumerable<ResearchProjectSO> requiredProjects,
        BlueprintUnlockCollection projectUnlocks = null,
        Sprite projectIcon = null)
    {
        projectId = ResearchProjectId.Normalize(stableId);
        displayName = name?.Trim() ?? string.Empty;
        description = projectDescription?.Trim() ?? string.Empty;
        field = researchField;
        requiredWork = Mathf.Max(1f, work);
        blueprintRule = rule;
        blueprint = requiredBlueprint;
        prerequisites = requiredProjects?
            .Where(candidate => candidate != null)
            .Distinct()
            .OrderBy(candidate => candidate.ProjectId.Value, StringComparer.Ordinal)
            .ToList()
            ?? new List<ResearchProjectSO>();
        unlocks = projectUnlocks ?? new BlueprintUnlockCollection();
        icon = projectIcon;
    }

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new List<string>();
        if (!ProjectId.IsValid)
        {
            errors.Add($"{name}: 프로젝트 ID가 없습니다.");
        }
        if (blueprintRule != ResearchBlueprintRule.None && blueprint == null)
        {
            errors.Add($"{ProjectId}: 설계도 규칙에 필요한 물리 설계도가 없습니다.");
        }
        if (blueprint != null
            && !string.Equals(
                blueprint.TargetResearchProjectId,
                ProjectId.Value,
                StringComparison.Ordinal))
        {
            errors.Add($"{ProjectId}: 설계도의 대상 프로젝트 ID가 일치하지 않습니다.");
        }
        if (prerequisites != null && prerequisites.Any(candidate => candidate == null))
        {
            errors.Add($"{ProjectId}: 누락된 선행 프로젝트 참조가 있습니다.");
        }
        if (prerequisites != null && prerequisites.Contains(this))
        {
            errors.Add($"{ProjectId}: 자기 자신을 선행 조건으로 참조합니다.");
        }
        return errors;
    }
}
