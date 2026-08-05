using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[CreateAssetMenu(
    fileName = "GameContentCatalog",
    menuName = "DungeonStory/Content/Game Content Catalog",
    order = -100)]
public sealed class GameContentCatalogSO : ScriptableObject
{
    public const string ResourcePath = "SO/GameContentCatalog";

    [SerializeField] private ScriptableObject itemDefinitions;
    [SerializeField] private ScriptableObject worldPresentation;
    [SerializeField] private ScriptableObject characterSkillSettings;
    [SerializeField] private ScriptableObject media;
    [SerializeField] private List<ScriptableObject> domainCatalogs = new();

    public ScriptableObject ItemDefinitions => itemDefinitions;
    public ScriptableObject WorldPresentation => worldPresentation;
    public ScriptableObject CharacterSkillSettings => characterSkillSettings;
    public ScriptableObject Media => media;
    public IReadOnlyList<ScriptableObject> DomainCatalogs => domainCatalogs;

    public T GetItemDefinitions<T>() where T : ScriptableObject =>
        itemDefinitions as T;

    public T GetWorldPresentation<T>() where T : ScriptableObject =>
        worldPresentation as T;

    public T GetCharacterSkillSettings<T>() where T : ScriptableObject =>
        characterSkillSettings as T;

    public T GetMedia<T>() where T : ScriptableObject =>
        media as T;

#if UNITY_EDITOR
    public void Configure(
        ScriptableObject items,
        ScriptableObject presentation,
        ScriptableObject skillSettings,
        ScriptableObject mediaCatalog,
        IEnumerable<ScriptableObject> additionalDomainCatalogs = null)
    {
        itemDefinitions = items;
        worldPresentation = presentation;
        characterSkillSettings = skillSettings;
        media = mediaCatalog;
        domainCatalogs = additionalDomainCatalogs != null
            ? new List<ScriptableObject>(additionalDomainCatalogs)
            : new List<ScriptableObject>();
    }
#endif
}
