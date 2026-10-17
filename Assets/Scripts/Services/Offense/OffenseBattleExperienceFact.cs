using System;
using DungeonStory.Foundation.Facts;

/// <summary>An observed surviving member after the battle's effects and experience were applied.</summary>
public sealed class OffenseBattleExperienceFact
{
    public OffenseBattleExperienceFact(string battleId, string expeditionId, string characterId,
        string characterName, string targetId, string targetTitle, string regionName,
        string factionId, string nodeId, string nodeTitle, bool victory, float damageTaken)
    {
        if (!DomainFactValueSyntax.IsIdentity(battleId) || !DomainFactValueSyntax.IsIdentity(expeditionId)
            || !DomainFactValueSyntax.IsIdentity(characterId) || !DomainFactValueSyntax.IsIdentity(targetId)
            || !OptionalId(factionId) || !OptionalId(nodeId)
            || !Text(characterName) || !Text(targetTitle) || !Text(regionName) || !Text(nodeTitle)
            || float.IsNaN(damageTaken) || float.IsInfinity(damageTaken) || damageTaken < 0f)
            throw new ArgumentException("offense-battle-experience-fact-invalid");
        BattleId = battleId; ExpeditionId = expeditionId; CharacterId = characterId;
        CharacterName = characterName; TargetId = targetId; TargetTitle = targetTitle;
        RegionName = regionName; FactionId = factionId; NodeId = nodeId; NodeTitle = nodeTitle;
        Victory = victory; DamageTaken = damageTaken;
    }

    public string BattleId { get; }
    public string ExpeditionId { get; }
    public string CharacterId { get; }
    public string CharacterName { get; }
    public string TargetId { get; }
    public string TargetTitle { get; }
    public string RegionName { get; }
    public string FactionId { get; }
    public string NodeId { get; }
    public string NodeTitle { get; }
    public bool Victory { get; }
    public float DamageTaken { get; }

    private static bool OptionalId(string value) => value == string.Empty || DomainFactValueSyntax.IsIdentity(value);
    private static bool Text(string value) => DomainFactValueSyntax.IsText(value, 16384, true);
}
