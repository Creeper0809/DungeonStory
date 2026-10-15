using System;
using System.Collections.Generic;
using DungeonStory.Narrative.Korean;

// Observer-owned identity, causal links, Korean rendering and memory policy.
public sealed class ReproductionTransitionFactMapping
{
    private readonly Func<string, bool, GameplayOutcomeId> references;
    public ReproductionTransitionFactMapping(IGameplayOutcomeDiagnosticsQuery observations)
        : this((key, death) => ResolveReference(observations, key, death)) { }
    internal ReproductionTransitionFactMapping(Func<string, bool, GameplayOutcomeId> references) =>
        this.references = references ?? throw new ArgumentNullException(nameof(references));

    public ReproductionProcessTransitionOutcomeReceipt Capture(ReproductionTransitionFact fact)
    {
        var data = fact.Snapshot();
        var participants = new List<ReproductionOutcomeParticipant>();
        foreach (var person in data.people)
        {
            if (person.role is "medical-responder" or "offspring") continue;
            AddPerson(person);
        }
        participants.Add(new ReproductionOutcomeParticipant(
            new GameplayEntityId(ReproductionProcessTransitionOutcomeIds.FacilityKind, data.facilityId),
            ReproductionProcessTransitionOutcomeIds.FacilityRole, Name(data.facilityName, data.facilityId, false),
            0.56f, NarrativeMemoryTier.Episodic));
        foreach (var person in data.people)
            if (person.role is "medical-responder" or "offspring") AddPerson(person);
        string cause = data.cause == "daily-transition" ? AdvanceCause(data) : data.cause;
        return new ReproductionProcessTransitionOutcomeReceipt(data.processId, data.transitionKind, data.mode,
            data.phenotypeSpeciesId, data.hasBefore ? Status(data.statusBefore) : "none", Status(data.statusAfter),
            data.hasBefore ? Failure(data.failureBefore) : "none", Failure(data.failureAfter), cause,
            data.facilityId, data.costs, data.absoluteDay, data.revisionBefore, data.revisionAfter,
            data.phaseBefore, data.phaseAfter, data.phaseElapsedBefore, data.phaseElapsedAfter,
            data.progressBefore, data.progressAfter, data.emergencyDeadlineDay, participants,
            new GameplayLocationReference(data.locationInstanceId, data.locationRegionId, data.locationX, data.locationY),
            Resolve(data.parentPublicationKey, true), Resolve(data.previousPublicationKey, false));

        GameplayOutcomeId Resolve(string key, bool death) => string.IsNullOrEmpty(key) ? default : references(key, death);
        void AddPerson(ReproductionTransitionPersonData person)
        {
            float importance = person.role == "offspring" ? 1f : person.role == "medical-responder" ? 0.88f
                : person.role == "reproduction-carrier" ? 0.9f : 0.82f;
            participants.Add(new ReproductionOutcomeParticipant(
                new GameplayEntityId(ReproductionProcessTransitionOutcomeIds.CharacterKind, person.characterId),
                new GameplayRoleId(person.role), Name(person.name, person.characterId, true), importance, NarrativeMemoryTier.Core));
        }
    }

    internal static GameplayOutcomeId ResolveReference(IGameplayOutcomeDiagnosticsQuery observations, string text, bool death)
    {
        if (!GameplayDomainFactDeliveryQuery.TryReadKey(text, out var key)
            || key.ProducerId != (death ? MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.CharacterDeathEvent).ProducerId
                : ReproductionProcessTransitionOutcomeIds.ProducerId))
            throw new InvalidOperationException("reproduction-transition-reference-invalid");
        if (!observations.TryGetResultIdentity(key, out var reference) || !reference.ResultKey.Equals(key)
            || !reference.OutcomeId.IsValid || reference.State < GameplayOutcomeReplayState.Committed
            || !reference.HasCanonicalPayloadHash)
            throw new InvalidOperationException("reproduction-transition-reference-pending");
        return reference.OutcomeId;
    }

    internal static GameplayResultKey Key(ReproductionTransitionFactData data) => new(
        ReproductionProcessTransitionOutcomeIds.ProducerId,
        new GameplayOperationId(ReproductionProcessTransitionOutcomeIds.OperationId(data.processId, data.transitionKind, data.revisionAfter)),
        data.revisionAfter, 0);

    private static KoreanNameSnapshot Name(string name, string id, bool character)
    {
        string kind = character ? "character" : "facility";
        return new KoreanNameSnapshot(name, "runtime-" + kind + "-current-v1:" + id,
            KoreanPronunciationHint.AutoHangulDisplay("runtime-" + kind + "-pronunciation-v1:" + id), "ko-KR");
    }

    private static string AdvanceCause(ReproductionTransitionFactData data) =>
        data.failureAfter != ReproductionFailureCode.None ? Failure(data.failureAfter)
        : data.statusAfter == ReproductionProcessStatus.Completed ? "duration-completed"
        : data.statusAfter == ReproductionProcessStatus.WaitingForEnvironment ? "unsafe-environment"
        : data.phaseBefore != data.phaseAfter ? "phase-advanced" : "daily-progress";

    internal static string Status(ReproductionProcessStatus value) => value switch
    {
        ReproductionProcessStatus.Planned => "planned",
        ReproductionProcessStatus.Active => "active",
        ReproductionProcessStatus.WaitingForEnvironment => "waiting-environment",
        ReproductionProcessStatus.WaitingForEmergencyExtraction =>
            "waiting-emergency-extraction",
        ReproductionProcessStatus.Completed => "completed",
        ReproductionProcessStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    internal static string Failure(ReproductionFailureCode value) => value switch
    {
        ReproductionFailureCode.None => "none",
        ReproductionFailureCode.KinshipRestricted => "kinship-restricted",
        ReproductionFailureCode.IncompatibleRole => "incompatible-role",
        ReproductionFailureCode.CrossLineageIncubatorRequired =>
            "cross-lineage-incubator-required",
        ReproductionFailureCode.GolemGeneticHybridForbidden =>
            "golem-genetic-hybrid-forbidden",
        ReproductionFailureCode.ConceptionFailed => "conception-failed",
        ReproductionFailureCode.Miscarriage => "miscarriage",
        ReproductionFailureCode.IncubationEnvironmentFailed =>
            "incubation-environment-failed",
        ReproductionFailureCode.CarrierDied => "carrier-died",
        ReproductionFailureCode.EmergencyExtractionExpired =>
            "emergency-extraction-expired",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}

