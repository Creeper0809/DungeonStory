using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

internal sealed class V20ContentEffectRollback
{
    private readonly V20CampaignRuntime live;
    private readonly ICharacterWorldQuery characters;
    private readonly ICareerPersistence careers;
    private readonly ICharacterBodyHealthCommand bodyHealth;
    private readonly IGriefTraumaService grief;
    private readonly IPopulationHealthService populationHealth;
    private readonly ICharacterNarrativeCommand narrative;
    private readonly IWorldItemStackRuntime items;
    private readonly IGameMoneyAccount money;

    internal V20ContentEffectRollback(
        V20CampaignRuntime live,
        ICharacterWorldQuery characters,
        ICareerPersistence careers,
        ICharacterBodyHealthCommand bodyHealth,
        IGriefTraumaService grief,
        IPopulationHealthService populationHealth,
        ICharacterNarrativeCommand narrative,
        IWorldItemStackRuntime items,
        IGameMoneyAccount money)
    {
        this.live = live;
        this.characters = characters;
        this.careers = careers;
        this.bodyHealth = bodyHealth;
        this.grief = grief;
        this.populationHealth = populationHealth;
        this.narrative = narrative;
        this.items = items;
        this.money = money;
    }

    internal V20ContentEffectOwnerSnapshot CaptureEffectOwners(
        IReadOnlyList<V20ResolvedEventResult> resolutions)
    {
        V20ResolvedEventResult[] concrete = (resolutions
                ?? Array.Empty<V20ResolvedEventResult>())
            .ToArray();
        HashSet<string> participantIds = concrete
            .SelectMany(value => value.ParticipantCharacterIds)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        V20CharacterEffectOwnerSnapshot[] actorSnapshots = characters.Characters
            .Where(actor => actor?.Identity != null
                && participantIds.Contains(actor.Identity.PersistentId))
            .GroupBy(actor => actor.Identity.PersistentId, StringComparer.Ordinal)
            .Select(group => new V20CharacterEffectOwnerSnapshot(group.First()))
            .ToArray();

        bool HasEffect(V20ContentEffectKind kind) => concrete.Any(value =>
            value.Effects.Any(effect => effect?.kind == kind));
        ICharacterBodyHealthPersistence bodyPersistence =
            bodyHealth as ICharacterBodyHealthPersistence;
        IPsychosocialPersistence psychosocialPersistence =
            grief as IPsychosocialPersistence;
        IPopulationHealthPersistence populationPersistence =
            populationHealth as IPopulationHealthPersistence;
        ICharacterNarrativePersistence narrativePersistence =
            narrative as ICharacterNarrativePersistence;
        bool requiresNarrative = HasEffect(V20ContentEffectKind.AmbitionProgress)
            || concrete.Any(value =>
                value.DefinitionId.StartsWith(
                    "life-event:",
                    StringComparison.Ordinal)
                || value.DefinitionId.StartsWith(
                    "practice:",
                    StringComparison.Ordinal));
        if (HasEffect(V20ContentEffectKind.Health) && bodyPersistence == null
            || HasEffect(V20ContentEffectKind.Trauma)
                && psychosocialPersistence == null
            || HasEffect(V20ContentEffectKind.DiseaseExposure)
                && populationPersistence == null
            || requiresNarrative && narrativePersistence == null)
        {
            throw new InvalidOperationException(
                "V20 content effects require rollback-capable persistence owners.");
        }

        return new V20ContentEffectOwnerSnapshot
        {
            Seasonal = live.CaptureSeasonal(),
            Society = live.CaptureSociety(),
            Factions = live.CaptureFactions(),
            Milestones = live.CaptureMilestones(),
            Items = items?.Capture(),
            MoneyBalance = money?.Balance ?? 0,
            Careers = careers?.Capture(),
            BodyHealth = bodyPersistence?.Capture(),
            Psychosocial = psychosocialPersistence?.Capture(),
            PopulationHealth = populationPersistence?.Capture(),
            Narrative = narrativePersistence?.Capture(),
            Characters = actorSnapshots
        };
    }

    internal void RestoreEffectOwners(
        V20ContentEffectOwnerSnapshot snapshot,
        string operationId)
    {
        snapshot = snapshot
            ?? throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.Items != null)
            items.Restore(snapshot.Items);
        if (money != null)
        {
            money.SetBalance(
                snapshot.MoneyBalance,
                new EconomyTransactionContext(
                    EconomyTransactionKind.LegacyExpense,
                    "content-rollback:" + (operationId?.Trim() ?? string.Empty),
                    description: "v20-content-resolution-rollback"));
        }
        foreach (V20CharacterEffectOwnerSnapshot character in
                 snapshot.Characters)
        {
            if (character.Stats != null)
                character.Actor.Stats?.RestoreMoodDeliveryTransactionState(
                    character.Stats);
            if (character.Progression != null)
                character.Actor.Progression?.RestorePersistentState(
                    character.Progression);
            if (character.Social != null)
                character.Actor.SocialMemory?.RestoreSnapshot(character.Social);
        }
        if (snapshot.BodyHealth != null
            && bodyHealth is ICharacterBodyHealthPersistence bodyPersistence)
        {
            bodyPersistence.PublishRestore(
                bodyPersistence.PrepareRestore(snapshot.BodyHealth));
        }
        if (snapshot.Psychosocial != null
            && grief is IPsychosocialPersistence psychosocialPersistence)
        {
            psychosocialPersistence.PublishRestore(
                psychosocialPersistence.PrepareRestore(
                    snapshot.Psychosocial));
        }
        if (snapshot.PopulationHealth != null
            && populationHealth is IPopulationHealthPersistence
                populationPersistence)
        {
            populationPersistence.PublishRestore(
                populationPersistence.PrepareRestore(
                    snapshot.PopulationHealth));
        }
        if (snapshot.Narrative != null
            && narrative is ICharacterNarrativePersistence
                narrativePersistence)
        {
            narrativePersistence.PublishRestore(
                narrativePersistence.PrepareRestore(snapshot.Narrative));
        }
        if (snapshot.Careers != null && careers != null)
            careers.PublishRestore(careers.PrepareRestore(snapshot.Careers));
        SeasonalEventAggregateState seasonal = live.PrepareSeasonal(snapshot.Seasonal);
        live.PublishContentResolution(
            seasonal,
            live.PrepareSociety(snapshot.Society),
            live.PrepareFactions(snapshot.Factions, seasonal),
            live.PrepareMilestones(snapshot.Milestones));
    }

    internal void RestoreTypedEffectOwners(V20ContentEffectOwnerSnapshot snapshot)
    {
        snapshot = snapshot
            ?? throw new ArgumentNullException(nameof(snapshot));
        foreach (V20CharacterEffectOwnerSnapshot character in snapshot.Characters)
        {
            if (character.Stats != null)
                character.Actor.Stats?.RestoreMoodDeliveryTransactionState(
                    character.Stats);
            if (character.Progression != null)
                character.Actor.Progression?.RestorePersistentState(
                    character.Progression);
            if (character.Social != null)
                character.Actor.SocialMemory?.RestoreSnapshot(character.Social);
        }
        if (snapshot.BodyHealth != null
            && bodyHealth is ICharacterBodyHealthPersistence bodyPersistence)
        {
            bodyPersistence.PublishRestore(
                bodyPersistence.PrepareRestore(snapshot.BodyHealth));
        }
        if (snapshot.Psychosocial != null
            && grief is IPsychosocialPersistence psychosocialPersistence)
        {
            psychosocialPersistence.PublishRestore(
                psychosocialPersistence.PrepareRestore(snapshot.Psychosocial));
        }
        if (snapshot.PopulationHealth != null
            && populationHealth is IPopulationHealthPersistence populationPersistence)
        {
            populationPersistence.PublishRestore(
                populationPersistence.PrepareRestore(snapshot.PopulationHealth));
        }
        if (snapshot.Narrative != null
            && narrative is ICharacterNarrativePersistence narrativePersistence)
        {
            narrativePersistence.PublishRestore(
                narrativePersistence.PrepareRestore(snapshot.Narrative));
        }
        if (snapshot.Careers != null && careers != null)
            careers.PublishRestore(careers.PrepareRestore(snapshot.Careers));
    }
}
