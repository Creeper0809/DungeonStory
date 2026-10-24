using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using static V20ContentEffectEvidence;
using Kind = V20ContentEffectKind;
using Stage = V20ContentEffectStage;
using Owners = V20ContentEffectPersistenceOwners;

// Existing effects stay together. Each row supplies the complete extension
// contract; shared dispatch/preflight/evidence code does not enumerate kinds.
internal static class V20ContentEffectHandlers
{
    internal static IEnumerable<V20ContentEffectHandler> Create()
    {
        yield return new(Kind.Mood, "character mood command / characters", "mood-factor", Stage.Typed,
            o => FormatFloat(MoodFactorValue(o.Snapshot, o.TargetId, o.Effect)),
            (b, a) => MoodFactorValue(a.Snapshot, a.TargetId, a.Effect) - MoodFactorValue(b.Snapshot, b.TargetId, b.Effect),
            minimumParticipants: 1, participantTargeted: true,
            apply: (_, c) =>
            {
                foreach (CharacterActor actor in c.Participants)
                    actor.ApplyMoodFactor($"content:{c.Resolution.DefinitionId}:{c.Effect.targetId}",
                        c.Effect.targetId, c.Effect.amount, Math.Max(1, c.Effect.durationDays) * 180f);
            });
        yield return new(Kind.Trauma, "grief-trauma command / characters.psychosocial", "trauma", Stage.Typed,
            o => FormatFloat(TraumaValue(o.Snapshot, o.TargetId)),
            (b, a) => TraumaValue(a.Snapshot, a.TargetId) - TraumaValue(b.Snapshot, b.TargetId),
            minimumParticipants: 1, participantTargeted: true, forwardRecovery: true,
            persistenceOwners: Owners.Psychosocial,
            apply: (r, c) =>
            {
                foreach (CharacterActor actor in c.Participants)
                    r.grief.ApplyTraumaDelta(actor.Identity.TypedPersistentId,
                        $"content:{c.Resolution.DefinitionId}:{c.Effect.targetId}", c.AbsoluteDay, c.Effect.amount);
            });
        yield return new(Kind.SkillExperience, "character progression command / characters", "skill-experience", Stage.Typed,
            o => ProgressionValue(o.Snapshot, o.TargetId),
            (b, a) => Changed(ProgressionValue(b.Snapshot, b.TargetId), ProgressionValue(a.Snapshot, a.TargetId))
                ? Math.Max(0, Mathf.RoundToInt(b.Effect.amount)) : 0f,
            minimumParticipants: 1, participantTargeted: true,
            expectedAmount: e => Math.Max(0, Mathf.RoundToInt(e.amount)),
            apply: (_, c) =>
            {
                foreach (CharacterActor actor in c.Participants)
                    actor.Progression?.AddExperience(Math.Max(0, Mathf.RoundToInt(c.Effect.amount)));
            });
        yield return new(Kind.ProficiencyExperience, "proficiency command / characters.narrative", "proficiency-experience", Stage.Typed,
            o => FormatFloat(ProficiencyExperience(o.Snapshot, o.TargetId, o.Effect.targetId)),
            (b, a) => ProficiencyExperience(a.Snapshot, a.TargetId, a.Effect.targetId)
                - ProficiencyExperience(b.Snapshot, b.TargetId, b.Effect.targetId),
            minimumParticipants: 1, participantTargeted: true, forwardRecovery: true,
            persistenceOwners: Owners.Narrative,
            preflight: (r, c) => r.narrative is ICharacterProficiencyCommand && r.calendar != null
                && r.narrativeCatalog != null && r.narrativeQuery != null
                && r.narrativeCatalog.Proficiencies.Any(value => value.StableId == c.Effect.targetId)
                && c.Participants.All(actor => r.narrativeQuery.TryGet(actor.Identity.TypedPersistentId, out _))
                ? DomainFailure.None : new DomainFailure(FailureCode.ServiceFeatureMissing, "content-proficiency-experience-authority"),
            apply: (r, c) =>
            {
                foreach (CharacterActor actor in c.Participants)
                    ((ICharacterProficiencyCommand)r.narrative).AddDirectExperience(
                        actor.Identity.TypedPersistentId, new CharacterProficiencyId(c.Effect.targetId),
                        c.Effect.amount, c.AbsoluteHour, applyLearningMultiplier: false);
            });
        yield return new(Kind.Health, "body-health command / characters.health", "health-change-not-pressure", Stage.Typed,
            o => FormatFloat(HealthValue(o.Snapshot, o.TargetId)),
            (b, a) => HealthValue(a.Snapshot, a.TargetId) - HealthValue(b.Snapshot, b.TargetId),
            minimumParticipants: 1, participantTargeted: true, forwardRecovery: true,
            persistenceOwners: Owners.BodyHealth,
            apply: (r, c) =>
            {
                foreach (CharacterActor actor in c.Participants)
                    if (c.Effect.amount >= 0f) r.bodyHealth.HealLegacyVitals(actor, c.Effect.amount);
                    else r.bodyHealth.ApplyLegacyDamage(actor, -c.Effect.amount, c.Resolution.DefinitionId, allowDeath: false);
            });
        yield return new(Kind.Relationship, "social-memory command / characters", "relationship-sentiment", Stage.Typed,
            o => RelationshipValue(o.Snapshot, o.TargetId, o.Participants),
            (b, a) => Changed(RelationshipValue(b.Snapshot, b.TargetId, b.Participants), RelationshipValue(a.Snapshot, a.TargetId, a.Participants))
                ? Mathf.Clamp(b.Effect.amount / 10f, -1f, 1f) : 0f,
            minimumParticipants: 2, participantTargeted: true,
            expectedAmount: e => Mathf.Clamp(e.amount / 10f, -1f, 1f),
            apply: (_, c) =>
            {
                float sentiment = Mathf.Clamp(c.Effect.amount / 10f, -1f, 1f);
                for (int index = 0; index < c.Participants.Length; index++)
                for (int other = index + 1; other < c.Participants.Length; other++)
                {
                    c.Participants[index].SocialMemory.RememberCharacterExperience(c.Participants[other], sentiment, c.Resolution.DefinitionId);
                    c.Participants[other].SocialMemory.RememberCharacterExperience(c.Participants[index], sentiment, c.Resolution.DefinitionId);
                }
            });
        yield return new(Kind.DiseaseExposure, "population-health command / characters.health", "exposure-pending-not-infection", Stage.Typed,
            o => DiseaseExposureValue(o.Snapshot, o.TargetId, o.Effect.targetId),
            (b, a) => ExposureValue(a.Snapshot, a.TargetId, a.Effect.targetId) - ExposureValue(b.Snapshot, b.TargetId, b.Effect.targetId),
            minimumParticipants: 1, participantTargeted: true, forwardRecovery: true,
            persistenceOwners: Owners.PopulationHealth, skipUnchanged: true,
            expectedAmount: e => Math.Max(0.1f, Math.Abs(e.amount)),
            preflight: (r, c) => r.diseases.Definitions.Any(value => string.Equals(value.Id, c.Effect.targetId, StringComparison.Ordinal))
                ? DomainFailure.None : new DomainFailure(FailureCode.VaccineDefinitionMissing),
            apply: (r, c) => r.populationHealth.RecordExposure(c.Effect.targetId,
                c.Participants.Select(value => new PopulationExposureTarget(value.Identity.TypedPersistentId, 1f)).ToArray(),
                Math.Max(0.1f, Math.Abs(c.Effect.amount)), 1f));
        yield return new(Kind.AmbitionProgress, "narrative command / characters.narrative", "ambition-progress", Stage.Typed,
            o => AmbitionValue(o.Snapshot, o.TargetId),
            (b, a) => AmbitionProgress(a.Snapshot, a.TargetId) - AmbitionProgress(b.Snapshot, b.TargetId),
            minimumParticipants: 1, participantTargeted: true, forwardRecovery: true,
            persistenceOwners: Owners.Narrative, expectedAmount: e => Mathf.RoundToInt(e.amount),
            apply: (r, c) =>
            {
                foreach (CharacterActor actor in c.Participants)
                {
                    int amount = Mathf.RoundToInt(c.Effect.amount);
                    if (r.narrativeQuery.TryPreviewAmbitionProgress(actor.Identity.TypedPersistentId, amount, out _))
                        r.narrative.AddAmbitionProgress(actor.Identity.TypedPersistentId, amount, c.AbsoluteDay);
                }
            });

        yield return Faction(Kind.FactionRapport, "faction-rapport",
            (r, id, amount) => r.ApplyFactionChange(id, amount, 0, 0));
        yield return Faction(Kind.FactionGrievance, "faction-grievance",
            (r, id, amount) => r.ApplyFactionChange(id, 0, amount, 0));
        yield return Faction(Kind.FactionObligation, "faction-obligation",
            (r, id, amount) => r.ApplyFactionChange(id, 0, 0, amount));
        yield return new(Kind.WorldFlag, "campaign command / society.events", "world-flag", Stage.Campaign,
            o => BoolValue(o.Snapshot.Milestones?.worldFlags?.Contains(o.TargetId, StringComparer.Ordinal) == true),
            (b, a) => (b.Snapshot.Milestones?.worldFlags?.Contains(b.TargetId, StringComparer.Ordinal) == true)
                == (a.Snapshot.Milestones?.worldFlags?.Contains(a.TargetId, StringComparer.Ordinal) == true) ? 0f : 1f,
            expectedAmount: _ => 1f,
            applyCampaign: (r, e, _) => r.milestoneRuntime.AddWorldFlag(e.targetId));
        yield return new(Kind.WorkDelayDays, "campaign work-delay command / society.events", "work-delay", Stage.Campaign,
            o => WorkDelayValue(o.Snapshot, o.TargetId),
            (b, a) => Math.Max(a.Snapshot.EffectAbsoluteDay, int.Parse(WorkDelayValue(a.Snapshot, a.TargetId), System.Globalization.CultureInfo.InvariantCulture))
                - Math.Max(b.Snapshot.EffectAbsoluteDay, int.Parse(WorkDelayValue(b.Snapshot, b.TargetId), System.Globalization.CultureInfo.InvariantCulture)),
            applyCampaign: (r, e, _) => r.ApplyWorkDelay(e.targetId, (int)Math.Round(e.amount), r.EffectAbsoluteDay),
            resolveTarget: (b, a) => ResolveWorkDelayTarget(b.Resolution, b.Effect, b.Snapshot, a.Snapshot),
            expectedAmount: e => Mathf.RoundToInt(e.amount),
            metricUnit: MigratedProducerOutcomeIds.DayUnit);
        foreach (Kind kind in new[] { Kind.Threat, Kind.MilestonePressure })
            yield return new(kind, "milestone pressure command / run.milestones", "pressure-activated-not-health-damage", Stage.Campaign,
                o => BoolValue(o.Snapshot.Milestones?.activePressureIds?.Contains(o.TargetId, StringComparer.Ordinal) == true),
                (b, a) => (b.Snapshot.Milestones?.activePressureIds?.Contains(b.TargetId, StringComparer.Ordinal) == true)
                    == (a.Snapshot.Milestones?.activePressureIds?.Contains(a.TargetId, StringComparer.Ordinal) == true) ? 0f : 1f,
                expectedAmount: _ => 1f,
                applyCampaign: (r, e, _) => r.milestoneRuntime.AddPressure(e.targetId));
        yield return new(Kind.Money, "money account command / session", "treasury-balance", Stage.Physical,
            o => o.Snapshot.MoneyBalance.ToString(System.Globalization.CultureInfo.InvariantCulture), RequestedInteger,
            resolveTarget: (_, _) => "treasury", metricUnit: MigratedProducerOutcomeIds.CurrencyUnit);
        foreach (Kind kind in new[] { Kind.ItemGrant, Kind.ItemConsume })
            yield return new(kind, kind == Kind.ItemGrant ? "physical item command / items.world-stacks" : "atomic item consumption / items.world-stacks",
                kind == Kind.ItemGrant ? "item-grant" : "item-consumption", Stage.Physical,
                o => ItemQuantity(o.Snapshot, o.TargetId).ToString(System.Globalization.CultureInfo.InvariantCulture), RequestedInteger,
                metricUnit: MigratedProducerOutcomeIds.CountUnit,
                targetKind: MigratedProducerOutcomeIds.ItemDefinitionKind,
                targetRole: kind == Kind.ItemGrant ? MigratedProducerOutcomeIds.RewardRole : MigratedProducerOutcomeIds.SourceRole);
        yield return new(Kind.RetirementSchedule, "career retirement schedule / characters.career", "retirement-scheduled-not-character-exit", Stage.Retirement,
            o => RetirementValue(o.Snapshot, o.TargetId),
            (b, a) => Changed(RetirementValue(b.Snapshot, b.TargetId), RetirementValue(a.Snapshot, a.TargetId)) ? b.Effect.durationDays : 0f,
            participantTargeted: true, expectedAmount: e => e.durationDays);
    }

    private static V20ContentEffectHandler Faction(Kind kind, string semantic,
        Action<V20CampaignEffectRuntime, string, int> apply) => new(kind,
        "faction campaign command / factions.campaign", semantic, Stage.Campaign,
        o => FactionValue(o.Snapshot, o.TargetId, kind),
        (b, a) => int.Parse(FactionValue(a.Snapshot, a.TargetId, kind), System.Globalization.CultureInfo.InvariantCulture)
            - int.Parse(FactionValue(b.Snapshot, b.TargetId, kind), System.Globalization.CultureInfo.InvariantCulture),
        applyCampaign: (r, e, faction) => apply(r, r.ResolveFactionTarget(e.targetId, faction), (int)Math.Round(e.amount)),
        expectedAmount: e => Mathf.RoundToInt(e.amount),
        resolveTarget: (b, a) => ResolveFactionEffectTarget(b.Resolution, b.Effect, b.Snapshot, a.Snapshot),
        targetKind: MigratedProducerOutcomeIds.FactionKind, targetRole: MigratedProducerOutcomeIds.FactionRole);

    private static bool Changed(string before, string after) => !string.Equals(before, after, StringComparison.Ordinal);
    private static float RequestedInteger(V20ContentEffectObservation before, V20ContentEffectObservation after) =>
        Mathf.RoundToInt(before.Effect.amount);
}
