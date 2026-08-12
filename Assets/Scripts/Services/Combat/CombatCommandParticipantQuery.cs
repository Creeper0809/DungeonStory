using System;
using System.Linq;
using UnityEngine;

public sealed class CombatCommandParticipantQuery
{
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly ICharacterPerformanceQuery performance;

    public CombatCommandParticipantQuery(
        ICharacterAiWorldRegistry worldRegistry,
        ICharacterBodyHealthQuery bodyHealth,
        ICharacterPerformanceQuery performance)
    {
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
    }

    public CombatParticipantRef Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return default;
        }

        CharacterActor character = FindCharacter(id);
        if (character != null)
        {
            return new CombatParticipantRef(character);
        }

        WildlifeActor wildlife = worldRegistry.Wildlife.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.WildlifeId, id, StringComparison.Ordinal));
        return wildlife != null ? new CombatParticipantRef(wildlife) : default;
    }

    public CharacterActor FindCharacter(string id)
    {
        return worldRegistry.Characters.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                CharacterPersistentIdentity.Require(actor).Value,
                id,
                StringComparison.Ordinal));
    }

    public string FindTargetIdAt(Vector2Int cell)
    {
        CharacterActor character = worldRegistry.Characters.FirstOrDefault(actor =>
            actor != null && actor.GetNowXY() == cell && !actor.IsDead);
        if (character != null)
        {
            return CharacterPersistentIdentity.Require(character).Value;
        }

        WildlifeActor wildlife = worldRegistry.Wildlife.FirstOrDefault(actor =>
            actor != null && actor.IsAlive && actor.GridPosition == cell);
        return wildlife != null ? wildlife.WildlifeId : string.Empty;
    }

    public CombatStatSnapshot GetCombatStats(CombatParticipantRef participant)
    {
        return participant.IsCharacter
            ? CombatRuntimeStatFactory.Create(
                participant.Character,
                bodyHealth.GetSnapshot(participant.Character),
                performance)
            : CombatRuntimeStatFactory.Create(participant.Wildlife);
    }
}
