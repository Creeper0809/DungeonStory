using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

internal sealed class V20ContentTypedEffectRuntime
{
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterBodyHealthCommand bodyHealth;
    private readonly IGriefTraumaService grief;
    private readonly IPopulationHealthService populationHealth;
    private readonly ICharacterNarrativeCommand narrative;
    private readonly ICharacterNarrativeCatalog narrativeCatalog;
    private readonly ICharacterNarrativeQuery narrativeQuery;

    internal V20ContentTypedEffectRuntime(
        ICharacterWorldQuery characters,
        ICharacterBodyHealthCommand bodyHealth,
        IGriefTraumaService grief,
        IPopulationHealthService populationHealth,
        ICharacterNarrativeCommand narrative,
        ICharacterNarrativeCatalog narrativeCatalog,
        ICharacterNarrativeQuery narrativeQuery)
    {
        this.characters = characters;
        this.bodyHealth = bodyHealth;
        this.grief = grief;
        this.populationHealth = populationHealth;
        this.narrative = narrative;
        this.narrativeCatalog = narrativeCatalog;
        this.narrativeQuery = narrativeQuery;
    }

    internal void ApplyTypedDomainEffects(
        int requestedAbsoluteDay,
        IReadOnlyList<V20ResolvedEventResult> resolutions)
    {
        int absoluteDay = Math.Max(1, requestedAbsoluteDay);
        Dictionary<string, CharacterActor> actors = characters.Characters
            .Where(value => value != null && value.Identity != null)
            .GroupBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (V20ResolvedEventResult resolution in resolutions)
        {
            CharacterActor[] participants = resolution.ParticipantCharacterIds
                .Where(actors.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .Select(value => actors[value])
                .ToArray();
            foreach (V20ContentEffect effect in resolution.Effects
                         .Where(value => value != null && value.IsValid))
            {
                switch (effect.kind)
                {
                    case V20ContentEffectKind.Mood:
                        foreach (CharacterActor actor in participants)
                            actor.ApplyMoodFactor(
                                $"content:{resolution.DefinitionId}:{effect.targetId}",
                                effect.targetId,
                                effect.amount,
                                Math.Max(1, effect.durationDays) * 180f);
                        break;
                    case V20ContentEffectKind.Trauma:
                        foreach (CharacterActor actor in participants)
                            grief.ApplyTraumaDelta(
                                actor.Identity.TypedPersistentId,
                                $"content:{resolution.DefinitionId}:{effect.targetId}",
                                absoluteDay,
                                effect.amount);
                        break;
                    case V20ContentEffectKind.SkillExperience:
                        foreach (CharacterActor actor in participants)
                            actor.Progression?.AddExperience(Math.Max(0, Mathf.RoundToInt(effect.amount)));
                        break;
                    case V20ContentEffectKind.Health:
                        foreach (CharacterActor actor in participants)
                        {
                            if (effect.amount >= 0f)
                                bodyHealth.HealLegacyVitals(actor, effect.amount);
                            else
                                bodyHealth.ApplyLegacyDamage(
                                    actor,
                                    -effect.amount,
                                    resolution.DefinitionId,
                                    allowDeath: false);
                        }
                        break;
                    case V20ContentEffectKind.Relationship:
                        float sentiment = Mathf.Clamp(effect.amount / 10f, -1f, 1f);
                        for (int index = 0; index < participants.Length; index++)
                        for (int other = index + 1; other < participants.Length; other++)
                        {
                            participants[index].SocialMemory.RememberCharacterExperience(
                                participants[other], sentiment, resolution.DefinitionId);
                            participants[other].SocialMemory.RememberCharacterExperience(
                                participants[index], sentiment, resolution.DefinitionId);
                        }
                        break;
                    case V20ContentEffectKind.DiseaseExposure:
                        populationHealth.RecordExposure(
                            effect.targetId,
                            participants.Select(value => new PopulationExposureTarget(
                                value.Identity.TypedPersistentId,
                                1f)).ToArray(),
                            Math.Max(0.1f, Math.Abs(effect.amount)),
                            1f);
                        break;
                    case V20ContentEffectKind.AmbitionProgress:
                        foreach (CharacterActor actor in participants)
                        {
                            int amount = Mathf.RoundToInt(effect.amount);
                            if (narrativeQuery.TryPreviewAmbitionProgress(
                                    actor.Identity.TypedPersistentId,
                                    amount,
                                    out _))
                            {
                                narrative.AddAmbitionProgress(
                                    actor.Identity.TypedPersistentId,
                                    amount,
                                    absoluteDay);
                            }
                        }
                        break;
                }
            }
            if (resolution.DefinitionId.StartsWith("life-event:", StringComparison.Ordinal))
            {
                foreach (CharacterActor actor in participants)
                    narrative.RecordResolvedEvent(
                        actor.Identity.TypedPersistentId,
                        new NarrativeEventId(resolution.DefinitionId),
                        resolution.ResolutionId,
                        absoluteDay);
            }
            if (resolution.DefinitionId.StartsWith(
                    "practice:",
                    StringComparison.Ordinal))
            {
                CulturalPracticeDefinitionSO practice =
                    narrativeCatalog.Practices.Single(value => string.Equals(
                        value.StableId,
                        resolution.DefinitionId,
                        StringComparison.Ordinal));
                SpeciesCultureId practiceCultureId =
                    new(practice.cultureId);
                int assimilationDays = narrativeCatalog
                    .Require(practiceCultureId)
                    .assimilationDays;
                foreach (CharacterActor actor in participants)
                {
                    if (string.Equals(
                            resolution.ResolutionId,
                            "performed",
                            StringComparison.Ordinal))
                    {
                        narrative.RecordPracticeParticipation(
                            actor.Identity.TypedPersistentId,
                            practice.StableId,
                            practiceCultureId,
                            assimilationDays,
                            absoluteDay);
                    }
                    else
                    {
                        narrative.RecordPracticeNeglect(
                            actor.Identity.TypedPersistentId,
                            practice.StableId,
                            absoluteDay);
                    }
                }
            }
        }
    }
}
