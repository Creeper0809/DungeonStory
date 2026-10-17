using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Infrastructure;
using UnityEngine;

/// <summary>
/// Materializes an arrived reinforcement route through the production character
/// factory and lifecycle. The faction domain retains route and actor identities.
/// </summary>
internal sealed class FactionReinforcementMaterializer
{
    private readonly FactionDomainRuntime domain;
    private readonly IReadOnlyList<FactionDefinitionSnapshot> definitions;
    private readonly IRunCharacterCatalog characterCatalog;
    private readonly ICharacterSpawnerProvider spawnerProvider;
    private readonly ICharacterSpawnObjectFactory characterFactory;
    private readonly ICharacterAiWorldRegistry worldRegistry;

    internal FactionReinforcementMaterializer(
        FactionDomainRuntime domain,
        IReadOnlyList<FactionDefinitionSnapshot> definitions,
        IRunCharacterCatalog characterCatalog,
        ICharacterSpawnerProvider spawnerProvider,
        ICharacterSpawnObjectFactory characterFactory,
        ICharacterAiWorldRegistry worldRegistry)
    {
        this.domain = domain;
        this.definitions = definitions;
        this.characterCatalog = characterCatalog;
        this.spawnerProvider = spawnerProvider;
        this.characterFactory = characterFactory;
        this.worldRegistry = worldRegistry;
    }

    internal void MaterializeReinforcements(FactionRouteState route)
    {
        if (route.actorsSpawned
            || !spawnerProvider.TryGetSpawner(out CharacterSpawner spawner)
            || spawner == null
            || spawner.characterPrefab == null
            || !spawner.TryGetEntryGridPosition(out Vector2Int entry))
        {
            return;
        }

        FactionDefinitionSnapshot definition = FindDefinition(route.factionId);
        CharacterSO template = characterCatalog.Characters
            .Where(value => value != null)
            .Where(value => string.Equals(
                value.SpeciesTag,
                definition?.SpeciesTag,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value.id)
            .FirstOrDefault();
        if (template == null)
        {
            return;
        }

        int count = Mathf.Clamp(
            Mathf.CeilToInt(route.strength / 40f),
            1,
            3);
        if (route.reinforcementActorIds == null || route.reinforcementActorIds.Count > count)
        {
            throw new InvalidOperationException("faction-reinforcement-invalid-progress");
        }
        IReadOnlyList<string> committedIds = FactionPayloadValidation
            .CanonicalizeReinforcementActorIdsForRestore(route);
        foreach (string committedId in committedIds)
        {
            CharacterActor[] matches = worldRegistry.AllCharacters.Where(value => value != null
                && string.Equals(value.Identity?.PersistentId, committedId, StringComparison.Ordinal)).Take(2).ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    "faction-reinforcement-receipt-actor-unresolved:" + committedId);
            }
            StartEntry(matches[0], spawner, entry);
        }

        // Each published actor has persistent life/narrative state. Keep its
        // canonical ID as a child receipt and resume only the uncommitted suffix.
        for (int index = committedIds.Count; index < count; index++)
        {
            CharacterId actorId = CharacterId.FromStableSuffix(
                $"{route.routeId}:ally:{index + 1}");
            GameObject instance = characterFactory.CreateInactive(spawner.characterPrefab, candidate =>
            {
                if (candidate.GetComponent<AbilityWork>() == null) candidate.AddComponent<AbilityWork>();
            });
            CharacterActor actor = instance.GetComponent<CharacterActor>();
            bool recorded = false;
            try
            {
                if (actor == null)
                    throw new InvalidOperationException("Reinforcement composition requires CharacterActor.");
                instance.name =
                    $"{definition?.DisplayName ?? route.factionId} 지원군 {index + 1}";
                instance.transform.position = spawner.GetOutsideSpawnWorldPosition()
                    + Vector3.right * (index * 0.35f);
                // Initialize binds growth and vitals to the persistent identity.
                actor.Identity.SetPersistentId(actorId);
                actor.Initialize(template);
                actor.characterType = CharacterType.NPC;
                actor.Identity.SetCharacterType(CharacterType.NPC);
                actor.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
                if (actor.TryGetAbility(out AbilityWork work))
                    work.WorkPriorities.SetPriority(BuiltInWorkTypeIds.Guard, WorkPriorityLevel.Priority1);
                ConfigureMarker(actor, route);
                characterFactory.Publish(instance);
                domain.AddReinforcementActor(route, actorId.Value);
                recorded = true;
                StartEntry(actor, spawner, entry);
            }
            catch (Exception failure)
            {
                // A publisher can lose its response after registering the actor.
                // Preserve that committed identity instead of creating it again.
                try
                {
                    bool published = actor != null && actor.HasBeenPublished
                        && worldRegistry.AllCharacters.Any(value => ReferenceEquals(value, actor));
                    if (published)
                    {
                        if (!recorded) domain.AddReinforcementActor(route, actorId.Value);
                    }
                    else
                    {
                        if (instance.activeSelf) instance.SetActive(false);
                        characterFactory.Destroy(instance);
                    }
                }
                catch (Exception cleanup)
                { throw new AggregateException("Reinforcement publication and recovery both failed.", failure, cleanup); }
                throw;
            }
        }

        domain.FinishReinforcementMaterialization(route);
    }

    internal void RebindRestoredActors()
    {
        // Route receipts remain the saved authority. Marker and entry routines
        // are scene projections rebuilt only after the official world commits.
        var routesByActor = domain.Routes
            .Where(route => route != null && route.kind == FactionRouteKind.Reinforcement)
            .SelectMany(route => route.reinforcementActorIds.Select(id => (Id: id, Route: route)))
            .ToDictionary(value => value.Id, value => value.Route, StringComparer.Ordinal);
        foreach (CharacterActor actor in worldRegistry.AllCharacters.Where(value => value != null))
        {
            if (!routesByActor.TryGetValue(actor.Identity.PersistentId, out FactionRouteState route)) continue;
            ConfigureMarker(actor, route);
            if (actor.CurrentLifecycleState is not (CharacterLifecycleState.SpawningOutside or CharacterLifecycleState.EnteringDungeon)) continue;
            if (actor.TryGetAbility(out AbilityMove move) && move.HasActiveMovement) continue;
            if (!spawnerProvider.TryGetSpawner(out CharacterSpawner spawner) || spawner == null
                || !spawner.TryGetEntryGridPosition(out Vector2Int entry))
                throw new InvalidOperationException("faction-reinforcement-restore-entry-unavailable:" + actor.Identity.PersistentId);
            StartEntry(actor, spawner, entry, restoringEntry: true);
        }
    }

    private static void ConfigureMarker(CharacterActor actor, FactionRouteState route)
    {
        FactionReinforcementMarker marker = actor.GetComponent<FactionReinforcementMarker>()
            ?? actor.gameObject.AddComponent<FactionReinforcementMarker>();
        marker.Configure(route.routeId, route.factionId, route.strength);
    }

    private void StartEntry(CharacterActor actor, CharacterSpawner spawner, Vector2Int entry, bool restoringEntry = false)
    {
        if (actor.CurrentLifecycleState != CharacterLifecycleState.SpawningOutside
            && (!restoringEntry || actor.CurrentLifecycleState != CharacterLifecycleState.EnteringDungeon)) return;
        if (actor.TryGetAbility(out AbilityMove move))
            move.StartEnterDungeon(spawner.GetEntryDoorWorldPosition(), entry);
        else if (worldRegistry.TryGetGrid(out Grid grid))
        {
            actor.transform.position = grid.GetWorldPos(entry);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
        }
    }

    private FactionDefinitionSnapshot FindDefinition(string factionId)
    {
        return definitions.FirstOrDefault(value =>
            string.Equals(value.StableId, factionId, StringComparison.Ordinal));
    }
}
