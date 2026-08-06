using System;
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
    private const string ItemDefinitionsTypeName = "ItemDefinitionCatalogSO";
    private const string WorldPresentationTypeName =
        "WorldInteractionPresentationCatalogSO";
    private const string CharacterSkillSettingsTypeName =
        "CharacterSkillSystemSettingsSO";
    private const string MediaTypeName = "GameMediaCatalogSO";

    [SerializeField] private ScriptableObject itemDefinitions;
    [SerializeField] private ScriptableObject worldPresentation;
    [SerializeField] private ScriptableObject characterSkillSettings;
    [SerializeField] private ScriptableObject media;
    [SerializeField] private List<ScriptableObject> domainCatalogs = new();

    public ScriptableObject ItemDefinitions => itemDefinitions;
    public ScriptableObject WorldPresentation => worldPresentation;
    public ScriptableObject CharacterSkillSettings => characterSkillSettings;
    public ScriptableObject Media => media;
    public IReadOnlyList<ScriptableObject> DomainCatalogs =>
        domainCatalogs == null
            ? (IReadOnlyList<ScriptableObject>)Array.Empty<ScriptableObject>()
            : domainCatalogs;

    public T GetItemDefinitions<T>() where T : ScriptableObject =>
        itemDefinitions as T;

    public T GetWorldPresentation<T>() where T : ScriptableObject =>
        worldPresentation as T;

    public T GetCharacterSkillSettings<T>() where T : ScriptableObject =>
        characterSkillSettings as T;

    public T GetMedia<T>() where T : ScriptableObject =>
        media as T;

    public IReadOnlyList<string> ValidateCatalog()
    {
        List<string> errors = new();
        ValidateReference(
            itemDefinitions,
            ItemDefinitionsTypeName,
            "Item-definition catalog",
            errors);
        ValidateReference(
            worldPresentation,
            WorldPresentationTypeName,
            "World-presentation catalog",
            errors);
        ValidateReference(
            characterSkillSettings,
            CharacterSkillSettingsTypeName,
            "Character-skill settings",
            errors);
        ValidateReference(media, MediaTypeName, "Media catalog", errors);

        HashSet<ScriptableObject> uniqueDomainCatalogs = new();
        IReadOnlyList<ScriptableObject> catalogs = DomainCatalogs;
        int authoredDomainCatalogs = 0;
        for (int index = 0; index < catalogs.Count; index++)
        {
            ScriptableObject catalog = catalogs[index];
            if (catalog == null)
            {
                errors.Add($"Domain catalog reference {index} is missing.");
            }
            else
            {
                if (!uniqueDomainCatalogs.Add(catalog))
                {
                    errors.Add(
                        $"Domain catalog reference '{catalog.name}' is duplicated.");
                }
                if (catalog is GameDomainContentCatalogSO)
                {
                    authoredDomainCatalogs++;
                }
                else
                {
                    errors.Add(
                        $"Domain catalog reference {index} has type "
                        + $"'{catalog.GetType().Name}'; expected "
                        + $"'{nameof(GameDomainContentCatalogSO)}'.");
                }
            }
        }

        if (authoredDomainCatalogs != 1)
        {
            errors.Add(
                $"Exactly one {nameof(GameDomainContentCatalogSO)} is required; "
                + $"found {authoredDomainCatalogs}.");
        }

        return errors;
    }

    private static void ValidateReference(
        ScriptableObject reference,
        string expectedTypeName,
        string label,
        ICollection<string> errors)
    {
        if (reference == null)
        {
            errors.Add(label + " reference is missing.");
            return;
        }

        string actualTypeName = reference.GetType().FullName
            ?? reference.GetType().Name;
        if (!string.Equals(actualTypeName, expectedTypeName, StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} reference has type '{actualTypeName}'; "
                + $"expected '{expectedTypeName}'.");
        }
    }

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
