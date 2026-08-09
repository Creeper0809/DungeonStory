using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ResearchUnlockBundleRole
{
    Foundation = 0,
    ProductionChain = 1,
    EquipmentFamily = 2,
    ServicePackage = 3,
    SystemFacility = 4,
    Capstone = 5
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchUnlockRewardGroup
{
    [SerializeField] private ResearchRewardKind kind;
    [Min(0), SerializeField] private int displayOrder;
    [SerializeField] private string heading = string.Empty;

    public ResearchRewardKind Kind => kind;
    public int DisplayOrder => Mathf.Max(0, displayOrder);
    public string Heading => heading?.Trim() ?? string.Empty;

    public ResearchUnlockRewardGroup()
    {
    }

#if UNITY_EDITOR
    public ResearchUnlockRewardGroup(
        ResearchRewardKind rewardKind,
        int order,
        string groupHeading)
    {
        kind = rewardKind;
        displayOrder = Mathf.Max(0, order);
        heading = groupHeading?.Trim() ?? string.Empty;
    }
#endif
}

[CreateAssetMenu(
    fileName = "ResearchUnlockBundle",
    menuName = "DungeonStory/Research/Unlock Bundle",
    order = 31)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchUnlockBundleDefinitionSO : DataScriptableObject
{
    [SerializeField] private string researchId = string.Empty;
    [SerializeField] private ResearchUnlockBundleRole role;
    [TextArea(2, 6), SerializeField] private string designIntent = string.Empty;
    [SerializeField] private List<ResearchUnlockRewardGroup> rewardGroups = new();
    [TextArea(2, 5), SerializeField] private string singletonReason = string.Empty;

    public string ResearchId => researchId?.Trim() ?? string.Empty;
    public ResearchUnlockBundleRole Role => role;
    public string DesignIntent => designIntent?.Trim() ?? string.Empty;
    public IReadOnlyList<ResearchUnlockRewardGroup> RewardGroups => rewardGroups;
    public string SingletonReason => singletonReason?.Trim() ?? string.Empty;

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (ResearchId.Length == 0)
        {
            errors.Add("Research unlock bundle requires a research id.");
        }
        if (DesignIntent.Length == 0)
        {
            errors.Add($"Research unlock bundle '{ResearchId}' requires a design intent.");
        }
        HashSet<ResearchRewardKind> kinds = new();
        foreach (ResearchUnlockRewardGroup group in rewardGroups
                     ?? new List<ResearchUnlockRewardGroup>())
        {
            if (group == null || !kinds.Add(group.Kind))
            {
                errors.Add($"Research unlock bundle '{ResearchId}' has a missing or duplicate reward group.");
            }
        }
        return errors;
    }

#if UNITY_EDITOR
    public void Configure(
        string requiredResearchId,
        ResearchUnlockBundleRole bundleRole,
        string intent,
        IEnumerable<ResearchUnlockRewardGroup> groups,
        string singleRewardReason)
    {
        researchId = requiredResearchId?.Trim() ?? string.Empty;
        role = bundleRole;
        designIntent = intent?.Trim() ?? string.Empty;
        rewardGroups = new List<ResearchUnlockRewardGroup>(
            groups ?? Array.Empty<ResearchUnlockRewardGroup>());
        singletonReason = singleRewardReason?.Trim() ?? string.Empty;
    }
#endif
}
