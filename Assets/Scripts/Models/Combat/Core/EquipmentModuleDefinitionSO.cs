using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[CreateAssetMenu(menuName = "DungeonStory/Combat/Equipment Module", order = 13)]
public sealed class EquipmentModuleDefinitionSO : ScriptableObject
{
    public const string ResourcePath = "SO/Combat/EquipmentModules";
    [SerializeField] private string moduleId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private EquipmentLineageKind lineageKind;
    [SerializeField] private EquipmentEra minimumEra = EquipmentEra.Medieval;
    [Min(0f), SerializeField] private float powerPerGrade = 0.04f;
    [Min(0f), SerializeField] private float utilityPerGrade = 0.03f;

    public string ModuleId => moduleId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? ModuleId
        : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public EquipmentLineageKind LineageKind => lineageKind;
    public EquipmentEra MinimumEra => minimumEra;
    public float PowerPerGrade => Mathf.Max(0f, powerPerGrade);
    public float UtilityPerGrade => Mathf.Max(0f, utilityPerGrade);
}

public interface IEquipmentModuleCatalog
{
    IReadOnlyList<EquipmentModuleDefinitionSO> All { get; }
    bool TryGet(string moduleId, out EquipmentModuleDefinitionSO definition);
}
