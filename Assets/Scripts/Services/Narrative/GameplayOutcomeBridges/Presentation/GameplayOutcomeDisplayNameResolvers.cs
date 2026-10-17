using System;
using System.Collections.Generic;
using DungeonStory.Narrative.Korean;

public interface IGameplayOutcomeEntityNameResolver
{
    GameplayEntityKindId EntityKind { get; }
    bool TryResolve(string stableEntityId, out KoreanNameSnapshot name);
}

public sealed class CharacterGameplayOutcomeEntityNameResolver :
    IGameplayOutcomeEntityNameResolver
{
    private static readonly GameplayEntityKindId Kind = new("character");
    private readonly ICharacterWorldQuery world;
    private readonly ICharacterLifetimeQuery lifetime;

    public CharacterGameplayOutcomeEntityNameResolver(
        ICharacterWorldQuery world,
        ICharacterLifetimeQuery lifetime)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    public GameplayEntityKindId EntityKind => Kind;

    public bool TryResolve(string stableEntityId, out KoreanNameSnapshot name)
    {
        CharacterActor actor = Find(
            world.Characters ?? Array.Empty<CharacterActor>(), stableEntityId);
        actor ??= Find(
            lifetime.AllCharacters ?? Array.Empty<CharacterActor>(), stableEntityId);
        string display = actor?.Identity?.DisplayName?.Trim() ?? string.Empty;
        if (display.Length == 0)
        {
            name = default;
            return false;
        }

        name = new KoreanNameSnapshot(
            display,
            "runtime-character-current-v1:" + stableEntityId,
            KoreanPronunciationHint.AutoHangulDisplay(
                "runtime-character-pronunciation-v1:" + stableEntityId),
            "ko-KR");
        return true;
    }

    private static CharacterActor Find(
        IReadOnlyList<CharacterActor> source,
        string stableEntityId)
    {
        for (int index = 0; index < source.Count; index++)
        {
            CharacterActor candidate = source[index];
            if (candidate?.Identity != null
                && string.Equals(candidate.Identity.PersistentId,
                    stableEntityId, StringComparison.Ordinal))
                return candidate;
        }
        return null;
    }
}

public sealed class FacilityGameplayOutcomeEntityNameResolver :
    IGameplayOutcomeEntityNameResolver
{
    private static readonly GameplayEntityKindId Kind = new("facility");
    private readonly IBuildingWorldQuery world;

    public FacilityGameplayOutcomeEntityNameResolver(IBuildingWorldQuery world)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public GameplayEntityKindId EntityKind => Kind;

    public bool TryResolve(string stableEntityId, out KoreanNameSnapshot name)
    {
        BuildableObject building = null;
        IReadOnlyList<BuildableObject> buildings =
            world.Buildings ?? Array.Empty<BuildableObject>();
        for (int index = 0; index < buildings.Count; index++)
        {
            BuildableObject candidate = buildings[index];
            if (candidate != null
                && candidate.PersistentInstanceId.IsValid
                && string.Equals(candidate.PersistentInstanceId.Value,
                    stableEntityId, StringComparison.Ordinal))
            {
                building = candidate;
                break;
            }
        }
        string display = building?.BuildingData?.objectName?.Trim() ?? string.Empty;
        if (display.Length == 0)
        {
            name = default;
            return false;
        }

        name = new KoreanNameSnapshot(
            display,
            "runtime-facility-current-v1:" + stableEntityId,
            KoreanPronunciationHint.AutoHangulDisplay(
                "runtime-facility-pronunciation-v1:" + stableEntityId),
            "ko-KR");
        return true;
    }
}

public sealed class GameplayOutcomeDisplayNameQuery :
    IGameplayOutcomeDisplayNameQuery
{
    private readonly Dictionary<GameplayEntityKindId,
        IGameplayOutcomeEntityNameResolver> resolvers = new();

    public GameplayOutcomeDisplayNameQuery(
        IEnumerable<IGameplayOutcomeEntityNameResolver> resolverSource)
    {
        foreach (IGameplayOutcomeEntityNameResolver resolver in
                 resolverSource ?? Array.Empty<IGameplayOutcomeEntityNameResolver>())
        {
            if (resolver == null || !resolver.EntityKind.IsValid)
                throw new InvalidOperationException(
                    "Gameplay outcome name resolvers require a valid entity kind.");
            if (!resolvers.TryAdd(resolver.EntityKind, resolver))
                throw new InvalidOperationException(
                    $"Duplicate gameplay outcome name resolver for '{resolver.EntityKind}'.");
        }
    }

    public bool TryGetCurrentName(
        GameplayEntityId entityId,
        out KoreanNameSnapshot name)
    {
        if (entityId.IsValid
            && resolvers.TryGetValue(entityId.Kind, out var resolver)
            && resolver.TryResolve(entityId.Value, out name))
        {
            return true;
        }

        name = default;
        return false;
    }
}
