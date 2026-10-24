using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

internal sealed class V20ContentTypedEffectRuntime
{
    internal readonly ICharacterWorldQuery characters;
    internal readonly ICharacterBodyHealthCommand bodyHealth;
    internal readonly IGriefTraumaService grief;
    internal readonly IPopulationHealthService populationHealth;
    internal readonly ICharacterNarrativeCommand narrative;
    internal readonly ICharacterNarrativeCatalog narrativeCatalog;
    internal readonly ICharacterNarrativeQuery narrativeQuery;

    internal readonly IDiseaseDefinitionCatalog diseases;
    internal readonly IGameCalendar calendar;
    internal V21ContentEffectExecutionRegistry Handlers { get; }

    internal V20ContentTypedEffectRuntime(
        ICharacterWorldQuery characters,
        ICharacterBodyHealthCommand bodyHealth,
        IGriefTraumaService grief,
        IPopulationHealthService populationHealth,
        ICharacterNarrativeCommand narrative,
        ICharacterNarrativeCatalog narrativeCatalog,
        ICharacterNarrativeQuery narrativeQuery,
        IDiseaseDefinitionCatalog diseases = null,
        V21ContentEffectExecutionRegistry handlers = null,
        IGameCalendar calendar = null)
    {
        this.calendar = calendar;
        this.diseases = diseases;
        Handlers = handlers ?? V21ContentEffectExecutionRegistry.Default;
        this.characters = characters;
        this.bodyHealth = bodyHealth;
        this.grief = grief;
        this.populationHealth = populationHealth;
        this.narrative = narrative;
        this.narrativeCatalog = narrativeCatalog;
        this.narrativeQuery = narrativeQuery;
    }

    internal bool TryPreflight(int absoluteDay, IReadOnlyList<V20ResolvedEventResult> resolutions,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        Dictionary<string, CharacterActor> actors = characters.Characters
            .Where(value => value?.Identity != null)
            .GroupBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (V20ResolvedEventResult resolution in resolutions)
        {
            CharacterActor[] participants = resolution.ParticipantCharacterIds.Where(actors.ContainsKey)
                .Distinct(StringComparer.Ordinal).Select(id => actors[id]).ToArray();
            foreach (V20ContentEffect effect in resolution.Effects)
            {
                if (effect == null || !effect.IsValid) continue;
                if (!Handlers.TryGet(effect.kind, out V20ContentEffectHandler handler))
                {
                    failure = new DomainFailure(FailureCode.ServiceFeatureMissing, $"content-effect-handler:{effect?.kind}");
                    return false;
                }
                if (participants.Length < handler.MinimumParticipants)
                {
                    failure = new DomainFailure(participants.Length == 0
                        ? FailureCode.CharacterMedicalPatientUnavailable : FailureCode.ExternalInfluenceUnavailable);
                    return false;
                }
                if (handler.Preflight != null)
                {
                    failure = handler.Preflight(this, new V20ContentEffectInvocation(absoluteDay, resolution, effect, participants));
                    if (failure.IsFailure) return false;
                }
            }
        }
        return true;
    }

    internal long? CaptureProficiencyAwardHour(IReadOnlyList<V20ResolvedEventResult> resolutions)
    {
        if (!resolutions.SelectMany(value => value.Effects)
            .Any(effect => effect.kind == V20ContentEffectKind.ProficiencyExperience))
            return null;
        long hour = calendar?.AbsoluteHour
            ?? throw new InvalidOperationException("Content proficiency award calendar is unavailable.");
        if (hour < 0L)
            throw new InvalidOperationException("Content proficiency award time is invalid.");
        return hour;
    }

    internal IReadOnlyList<V20ResolvedEventResult> ApplyTypedDomainEffects(
        int requestedAbsoluteDay,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        Func<IReadOnlyList<V20ResolvedEventResult>, V20ContentEffectOwnerSnapshot> capture,
        long? proficiencyAwardAbsoluteHour)
    {
        if (resolutions.SelectMany(value => value.Effects)
                .Any(effect => effect.kind == V20ContentEffectKind.ProficiencyExperience)
            && (!proficiencyAwardAbsoluteHour.HasValue || proficiencyAwardAbsoluteHour.Value < 0L))
            throw new InvalidOperationException("Content proficiency award has no frozen time.");
        int absoluteDay = Math.Max(1, requestedAbsoluteDay);
        Dictionary<string, CharacterActor> actors = characters.Characters
            .Where(value => value != null && value.Identity != null)
            .GroupBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var applied = new List<V20ResolvedEventResult>();
        foreach (V20ResolvedEventResult resolution in resolutions)
        {
            var applications = resolution.Applications.Select(value => value.Clone()).ToList();
            CharacterActor[] participants = resolution.ParticipantCharacterIds
                .Where(actors.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .Select(value => actors[value])
                .ToArray();
            for (int index = 0; index < resolution.Effects.Count; index++)
            {
                V20ContentEffect effect = resolution.Effects[index];
                if (effect == null || !effect.IsValid) continue;
                V20ContentEffectHandler handler = Handlers.Require(effect.kind);
                if (handler.Stage == V20ContentEffectStage.Typed)
                {
                    V20ContentEffectOwnerSnapshot before = capture(new[] { resolution });
                    handler.Apply(this, new V20ContentEffectInvocation(absoluteDay, resolution, effect, participants,
                        proficiencyAwardAbsoluteHour ?? -1L));
                    applications.AddRange(V20ContentEffectEvidence.ObserveEffect(resolution, index,
                        before, capture(new[] { resolution }), handler));
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
            applied.Add(resolution.WithApplications(applications));
        }
        return applied;
    }
}
