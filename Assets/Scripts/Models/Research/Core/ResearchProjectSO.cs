using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(menuName = "DungeonStory/Research/Project", order = 0)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
// Authored Unity content adapter; immutable rule contracts live in DungeonStory.Research.
public sealed class ResearchProjectSO : DataScriptableObject,
    IResearchProjectDefinition,
    IGameplayEffectSource
{
    public const string ResourcePath = "SO/Research/Projects";

    [SerializeField] private string projectId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private ResearchField field;
    [Min(1f), SerializeField] private float requiredWork = 40f;
    [Range(1, 4), SerializeField] private int maximumResearchers = 1;
    [SerializeField] private List<ResearchProjectSO> prerequisites = new List<ResearchProjectSO>();
    [SerializeField] private List<ResearchPrerequisiteLink> prerequisiteLinks =
        new List<ResearchPrerequisiteLink>();
    [SerializeField] private ResearchBlueprintRule blueprintRule;
    [SerializeField] private FacilityBlueprintSO blueprint;
    [SerializeField] private BlueprintUnlockCollection unlocks = new BlueprintUnlockCollection();
    [SerializeField] private List<ResearchFacilityRequirement> facilityRequirements =
        new List<ResearchFacilityRequirement>();
    [SerializeField] private Sprite icon;
    [SerializeField] private List<GameplayEffectBinding> effects = new();

    public ResearchProjectId ProjectId => new ResearchProjectId(projectId);
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public ResearchField Field => field;
    public float RequiredWork => Mathf.Max(1f, requiredWork);
    public int MaximumResearchers => Mathf.Clamp(maximumResearchers, 1, 4);
    public IReadOnlyList<ResearchProjectSO> Prerequisites =>
        (prerequisiteLinks ??= new List<ResearchPrerequisiteLink>()).Count > 0
            ? prerequisiteLinks
                .Where(link => link != null && link.Prerequisite != null)
                .Select(link => link.Prerequisite)
                .Distinct()
                .ToArray()
            : prerequisites ??= new List<ResearchProjectSO>();
    public IReadOnlyList<ResearchProjectId> PrerequisiteIds => Prerequisites
        .Where(project => project != null)
        .Select(project => project.ProjectId)
        .ToArray();
    public IReadOnlyList<ResearchPrerequisiteLink> PrerequisiteLinks =>
        prerequisiteLinks ??= new List<ResearchPrerequisiteLink>();
    public ResearchBlueprintRule BlueprintRule => blueprintRule;
    public FacilityBlueprintSO Blueprint => blueprint;
    public int BlueprintId => blueprint != null ? blueprint.id : -1;
    public IReadOnlyList<BlueprintUnlock> Unlocks =>
        (unlocks ??= new BlueprintUnlockCollection()).Items;
    public BlueprintUnlockCollection UnlockCollection =>
        unlocks ??= new BlueprintUnlockCollection();
    public IReadOnlyList<ResearchFacilityRequirement> FacilityRequirements =>
        facilityRequirements ??= new List<ResearchFacilityRequirement>();
    public Sprite Icon => icon;
    public GameplayEffectSourceRef SourceRef =>
        new(GameplayEffectSourceKind.Research, ProjectId.Value);
    public IReadOnlyList<GameplayEffectBinding> Effects =>
        effects ??= new List<GameplayEffectBinding>();

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
        Sprite projectIcon = null,
        IEnumerable<ResearchFacilityRequirement> requiredFacilityCapacity = null,
        IEnumerable<ResearchPrerequisiteLink> causalPrerequisites = null,
        int projectMaximumResearchers = 1)
    {
        projectId = ResearchProjectId.Normalize(stableId);
        displayName = name?.Trim() ?? string.Empty;
        description = projectDescription?.Trim() ?? string.Empty;
        field = researchField;
        requiredWork = Mathf.Max(1f, work);
        maximumResearchers = Mathf.Clamp(projectMaximumResearchers, 1, 4);
        blueprintRule = rule;
        blueprint = requiredBlueprint;
        prerequisites = requiredProjects?
            .Where(candidate => candidate != null)
            .Distinct()
            .OrderBy(candidate => candidate.ProjectId.Value, StringComparer.Ordinal)
            .ToList()
            ?? new List<ResearchProjectSO>();
        Dictionary<string, ResearchPrerequisiteLink> suppliedLinks =
            (causalPrerequisites ?? Array.Empty<ResearchPrerequisiteLink>())
            .Where(link => link != null && link.Prerequisite != null)
            .GroupBy(link => link.Prerequisite.ProjectId.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        prerequisiteLinks = prerequisites
            .Select(required => suppliedLinks.TryGetValue(
                    required.ProjectId.Value,
                    out ResearchPrerequisiteLink link)
                ? link.CloneFor(required)
                : new ResearchPrerequisiteLink(
                    required,
                    ResearchPrerequisiteKind.Engineering,
                    $"{required.DisplayName}의 구현 지식이 {displayName} 설계에 직접 필요하다."))
            .ToList();
        unlocks = projectUnlocks ?? new BlueprintUnlockCollection();
        facilityRequirements = (requiredFacilityCapacity
                ?? new[]
                {
                    new ResearchFacilityRequirement(
                        ResearchFacilityCapabilityId.Basic,
                        1)
                })
            .Where(requirement => requirement.requiredCount > 0)
            .GroupBy(requirement => requirement.capability)
            .Select(group => new ResearchFacilityRequirement(
                group.Key,
                group.Sum(requirement => Mathf.Max(1, requirement.requiredCount))))
            .OrderBy(requirement => requirement.capability)
            .ToList();
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
        if (Prerequisites.Count > 4)
        {
            errors.Add($"{ProjectId}: 직접 선행 연구는 최대 4개여야 합니다.");
        }
        if (maximumResearchers != 1
            && maximumResearchers != 2
            && maximumResearchers != 4)
        {
            errors.Add($"{ProjectId}: 동시 연구자는 1명, 2명 또는 4명이어야 합니다.");
        }
        if (Prerequisites.Count > 0 && PrerequisiteLinks.Count != Prerequisites.Count)
        {
            errors.Add($"{ProjectId}: 모든 직접 선행 연구에 인과 링크가 필요합니다.");
        }
        foreach (ResearchPrerequisiteLink link in PrerequisiteLinks)
        {
            if (link == null || !link.IsValid)
            {
                errors.Add($"{ProjectId}: 선행 연구 링크에 ID, 인과 유형, 한 문장 근거가 필요합니다.");
            }
        }
        if (FacilityRequirements.Count == 0)
        {
            errors.Add($"{ProjectId}: 연구 시설 수용력 요구 조건이 없습니다.");
        }
        if (FacilityRequirements.Any(requirement => requirement.requiredCount <= 0))
        {
            errors.Add($"{ProjectId}: 연구 시설 요구량은 1 이상이어야 합니다.");
        }
        if (FacilityRequirements.GroupBy(requirement => requirement.capability)
            .Any(group => group.Count() > 1))
        {
            errors.Add($"{ProjectId}: 같은 연구 시설 수용력 태그가 중복되었습니다.");
        }
        return errors;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ResearchPrerequisiteKind
{
    Theory = 0,
    Technique = 1,
    Engineering = 2,
    Safety = 3,
    Operations = 4
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchPrerequisiteLink
{
    [SerializeField] private string prerequisiteId = string.Empty;
    [SerializeField] private ResearchProjectSO prerequisite;
    [SerializeField] private ResearchPrerequisiteKind kind;
    [TextArea, SerializeField] private string reason = string.Empty;

    public ResearchPrerequisiteLink(
        ResearchProjectSO prerequisite,
        ResearchPrerequisiteKind kind,
        string reason)
    {
        this.prerequisite = prerequisite;
        prerequisiteId = prerequisite?.ProjectId.Value ?? string.Empty;
        this.kind = kind;
        this.reason = reason?.Trim() ?? string.Empty;
    }

    public string PrerequisiteId => string.IsNullOrWhiteSpace(prerequisiteId)
        ? prerequisite?.ProjectId.Value ?? string.Empty
        : prerequisiteId.Trim();
    public ResearchProjectSO Prerequisite => prerequisite;
    public ResearchPrerequisiteKind Kind => kind;
    public string Reason => reason?.Trim() ?? string.Empty;
    public bool IsValid => prerequisite != null
        && string.Equals(PrerequisiteId, prerequisite.ProjectId.Value, StringComparison.Ordinal)
        && Enum.IsDefined(typeof(ResearchPrerequisiteKind), kind)
        && Reason.Length > 0
        && (Reason.EndsWith(".", StringComparison.Ordinal)
            || Reason.EndsWith("다", StringComparison.Ordinal));

    public ResearchPrerequisiteLink CloneFor(ResearchProjectSO project) =>
        new ResearchPrerequisiteLink(project, kind, Reason);
}
