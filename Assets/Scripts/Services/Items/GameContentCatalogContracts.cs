using DungeonStory.Content.CoreSession;

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
