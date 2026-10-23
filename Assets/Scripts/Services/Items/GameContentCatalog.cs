using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using UnityEngine;

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
        ValidateNarrativeReferences();
    }

    public GameContentCatalogSO Root { get; }
    public ItemDefinitionCatalogSO Items { get; }
    public WorldInteractionPresentationCatalogSO WorldPresentation { get; }
    public CharacterSkillSystemSettingsSO CharacterSkillSettings { get; }
    public GameMediaCatalogSO Media { get; }
    public GameDomainContentCatalogSO Domain { get; }
    public CoreSessionRulesDefinition CoreSessionRules { get; }

    // Validate these authored relations before a player can start an impossible
    // ritual or finish an ambition whose reward cannot resolve its target.
    public void ValidateNarrativeReferences()
    {
        var items = Items.Definitions.Select(value => value.ItemId).ToHashSet(StringComparer.Ordinal);
        var cultures = GetAll<SpeciesCultureDefinitionSO>().ToDictionary(value => value.StableId, StringComparer.Ordinal);
        var practices = GetAll<CulturalPracticeDefinitionSO>().ToDictionary(value => value.StableId, StringComparer.Ordinal);
        var ambitions = GetAll<CharacterAmbitionDefinitionSO>().ToDictionary(value => value.StableId, StringComparer.Ordinal);
        var events = GetAll<LifeEventDefinitionSO>().Select(value => value.StableId).ToHashSet(StringComparer.Ordinal);
        foreach (var culture in cultures.Values)
        {
            foreach (string id in culture.preferredItemIds.Concat(culture.forbiddenItemIds))
                RequireReference(items.Contains(id), culture.StableId, "item", id);
            foreach (string id in culture.ceremonyIds)
                RequireReference(practices.TryGetValue(id, out var practice) && practice.cultureId == culture.StableId,
                    culture.StableId, "cultural practice", id);
        }
        foreach (var practice in practices.Values)
        {
            RequireReference(cultures.ContainsKey(practice.cultureId), practice.StableId, "culture", practice.cultureId);
            foreach (var item in practice.requirements.items)
                RequireReference(item != null && items.Contains(item.itemDefinitionId), practice.StableId, "item", item?.itemDefinitionId);
        }
        foreach (var ambition in ambitions.Values)
        {
            foreach (var weighted in ambition.relatedEventWeights)
                RequireReference(weighted != null && events.Contains(weighted.id), ambition.StableId, "life event", weighted?.id);
            foreach (string id in ambition.cooperationAmbitionIds.Concat(ambition.conflictAmbitionIds))
                RequireReference(ambitions.ContainsKey(id), ambition.StableId, "ambition", id);
        }
    }

    private static void RequireReference(bool valid, string owner, string kind, string id)
    {
        if (!valid) throw new InvalidOperationException($"Content '{owner}' has an unknown {kind} reference '{id}'.");
    }
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
        if (typeof(ItemDefinitionSO).IsAssignableFrom(typeof(T)))
            return Items.Definitions.OfType<T>().ToArray();
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
