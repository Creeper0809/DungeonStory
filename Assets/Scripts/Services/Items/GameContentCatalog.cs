using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using UnityEngine;

public interface IGameContentCatalog :
    IGameContentDefinitionSource,
    IServiceProcessAuthoredContentPort,
    IRoomEnvironmentAuthoredContentPort,
    IOffenseAuthoredContentPort
{
    GameContentCatalogSO Root { get; }
    ItemDefinitionCatalogSO Items { get; }
    WorldInteractionPresentationCatalogSO WorldPresentation { get; }
    CharacterSkillSystemSettingsSO CharacterSkillSettings { get; }
    GameMediaCatalogSO Media { get; }
    GameDomainContentCatalogSO Domain { get; }
}

public sealed class ResourceGameContentCatalog :
    IGameContentCatalog,
    ICoreSessionRulesProvider
{
    public ResourceGameContentCatalog(IGameContentRootLoader rootLoader)
    {
        if (rootLoader == null)
        {
            throw new ArgumentNullException(nameof(rootLoader));
        }

        Root = rootLoader.LoadRequiredRoot() as GameContentCatalogSO
            ?? throw new InvalidOperationException(
                "The root content asset is not a GameContentCatalogSO.");
        IReadOnlyList<string> rootErrors = Root.ValidateCatalog();
        if (rootErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Game content root catalog is invalid:\n"
                + string.Join("\n", rootErrors));
        }
        Items = Root.GetItemDefinitions<ItemDefinitionCatalogSO>() != null
            ? Root.GetItemDefinitions<ItemDefinitionCatalogSO>()
            : throw new InvalidOperationException(
                "Game content catalog has no item-definition catalog.");
        WorldPresentation = Root.GetWorldPresentation<WorldInteractionPresentationCatalogSO>() != null
            ? Root.GetWorldPresentation<WorldInteractionPresentationCatalogSO>()
            : throw new InvalidOperationException(
                "Game content catalog has no world-presentation catalog.");
        CharacterSkillSettings = Root.GetCharacterSkillSettings<CharacterSkillSystemSettingsSO>() != null
            ? Root.GetCharacterSkillSettings<CharacterSkillSystemSettingsSO>()
            : throw new InvalidOperationException(
                "Game content catalog has no character-skill settings.");
        Media = Root.GetMedia<GameMediaCatalogSO>() != null
            ? Root.GetMedia<GameMediaCatalogSO>()
            : throw new InvalidOperationException(
                "Game content catalog has no media catalog.");
        Media.ValidateRequiredReferences();
        Domain = Root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException(
                "Game content catalog has no domain-content catalog.");

        if (Items.ValidateCatalog().Count > 0)
        {
            throw new InvalidOperationException(
                "Game content item catalog is invalid:\n"
                + string.Join("\n", Items.ValidateCatalog()));
        }

        if (Domain.ValidateCatalog().Count > 0)
        {
            throw new InvalidOperationException(
                "Game domain content catalog is invalid:\n"
                + string.Join("\n", Domain.ValidateCatalog()));
        }

        CoreSessionRules = Domain.CoreSessionRules.CreateRuntimeDefinition();
    }

    public GameContentCatalogSO Root { get; }
    public ItemDefinitionCatalogSO Items { get; }
    public WorldInteractionPresentationCatalogSO WorldPresentation { get; }
    public CharacterSkillSystemSettingsSO CharacterSkillSettings { get; }
    public GameMediaCatalogSO Media { get; }
    public GameDomainContentCatalogSO Domain { get; }
    public CoreSessionRulesDefinition CoreSessionRules { get; }
    public IReadOnlyList<ServiceProcessSO> ServiceProcesses =>
        Domain.GetAll<ServiceProcessSO>();
    public RoomEnvironmentSettingsSO RoomEnvironmentSettings =>
        RequireSingle<RoomEnvironmentSettingsSO>();
    public IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes =>
        Domain.GetAll<OffenseSiteArchetypeSO>();
    public IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites =>
        Domain.GetAll<OffenseUrgentSiteDefinitionSO>();
    public IReadOnlyList<OffenseDecisionCardSO> DecisionCards =>
        Domain.GetAll<OffenseDecisionCardSO>();
    public IReadOnlyList<OffenseEncounterSO> Encounters =>
        Domain.GetAll<OffenseEncounterSO>();

    public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
    {
        if (typeof(T) == typeof(ItemDefinitionSO))
            return Items.Definitions.Cast<T>().ToArray();
        return Domain.GetAll<T>();
    }

    public T RequireSingle<T>() where T : ScriptableObject
    {
        IReadOnlyList<T> values = GetAll<T>();
        return values.Count == 1
            ? values[0]
            : throw new InvalidOperationException(
                $"Expected exactly one {typeof(T).Name} in the game content catalog, found {values.Count}.");
    }
}
