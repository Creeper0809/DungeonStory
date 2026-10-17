using System;
using System.Globalization;

public static class SpeciesActionFactMapping
{
    public static GolemUpkeepOutcomeReceipt Capture(GolemUpkeepFact fact)
    {
        GolemUpkeepFactSaveData source = fact.Snapshot();
        source.Validate();
        string stateBefore = State(
            source.chargeBefore,
            source.integrityBefore,
            source.healthBefore);
        string stateAfter = State(
            source.chargeAfter,
            source.integrityAfter,
            source.healthAfter);
        string physicalChild;
        string workChild;
        string bodyChild;
        string summary;
        switch (source.kind)
        {
            case "recharge":
                physicalChild = source.physicalEvidence;
                workChild = "worker=" + source.characterId
                    + "|work=" + Number(source.completedWork);
                bodyChild = "none";
                summary = "charge=" + Number(source.chargeBefore)
                    + "->" + Number(source.chargeAfter);
                break;
            case "wear":
                physicalChild = "none";
                workChild = "source=" + source.workSourceId
                    + "|type=" + source.workTypeId
                    + "|work=" + Number(source.completedWork);
                bodyChild = "nodes=" + string.Join(",", source.wornNodes);
                summary = "integrity=" + Number(source.integrityBefore)
                    + "->" + Number(source.integrityAfter);
                break;
            case "discharge":
                physicalChild = "none";
                workChild = "none";
                bodyChild = "subject=" + source.characterId
                    + "|reason=species:golem-discharge"
                    + "|requested=" + Number(source.requestedDamage)
                    + "|applied=" + Number(source.appliedDamage)
                    + "|health=" + Number(source.healthBefore)
                    + "->" + Number(source.healthAfter);
                summary = "charge=" + Number(source.chargeBefore) + "->0";
                break;
            default:
                throw new InvalidOperationException(
                    "golem-upkeep-kind-invalid");
        }

        return WildlifeOutcomeReceiptFactory.CreateGolemUpkeep(
            GolemKey(source),
            source.revision,
            new CharacterId(source.characterId),
            source.characterDisplayName,
            new CharacterSpeciesId(source.speciesId),
            source.speciesDisplayName,
            source.kind,
            stateBefore,
            stateAfter,
            physicalChild,
            workChild,
            bodyChild,
            summary,
            new CoreGridCell(source.x, source.y),
            source.absoluteDay);
    }

    public static SpeciesIncidentOutcomeReceipt Capture(
        SpeciesIncidentFact fact)
    {
        SpeciesIncidentFactSaveData source = fact.Snapshot();
        source.Validate();
        return WildlifeOutcomeReceiptFactory.CreateSpeciesIncident(
            SpeciesIncidentKey(source),
            source.revision,
            new CharacterId(source.characterId),
            source.characterDisplayName,
            new CharacterSpeciesId(source.speciesId),
            source.speciesDisplayName,
            source.incidentId,
            source.summary,
            new CoreGridCell(source.x, source.y),
            source.absoluteDay);
    }

    public static HarpyGaleRelocationOutcomeReceipt Capture(
        HarpyGaleFact fact)
    {
        HarpyGaleFactSaveData source = fact.Snapshot();
        source.Validate();
        return WildlifeOutcomeReceiptFactory.CreateHarpyGaleRelocation(
            new GameplayResultKey(
                EnvironmentOutcomeIds.SpeciesIncidentProducer,
                new GameplayOperationId(source.operationId),
                source.revision,
                0),
            source.revision,
            new CharacterId(source.characterId),
            source.characterDisplayName,
            new CharacterSpeciesId(source.speciesId),
            source.speciesDisplayName,
            fact.ToPhysicalPreview(),
            source.absoluteDay);
    }

    public static OutcomeWriteRequirements Reserve(
        GolemUpkeepFact fact,
        long worldEpoch)
    {
        GolemUpkeepFactSaveData source = fact.Snapshot();
        source.Validate(proposed: true);
        return new OutcomeWriteRequirements(
            GolemKey(source),
            EnvironmentOutcomeIds.GolemUpkeepEpisode,
            source.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            worldEpoch,
            source.revision,
            participantCount: 2,
            metricCount: 1,
            subjectCount: 2,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 1,
            factCount: 8);
    }

    public static OutcomeWriteRequirements Reserve(
        SpeciesIncidentFact fact,
        long worldEpoch)
    {
        SpeciesIncidentFactSaveData source = fact.Snapshot();
        source.Validate(proposed: true);
        return new OutcomeWriteRequirements(
            SpeciesIncidentKey(source),
            EnvironmentOutcomeIds.SpeciesIncidentTriggered,
            source.absoluteDay,
            GameplayOutcomeStatus.Failed,
            worldEpoch,
            source.revision,
            participantCount: 2,
            metricCount: 1,
            subjectCount: 2,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 1,
            factCount: 3);
    }

    public static GameplayDomainFactObserverBinding<
        GolemUpkeepFact,
        GolemUpkeepOutcomeReceipt> CreateGolemBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits) => new(
            "log:character.golem-upkeep",
            Capture,
            registry,
            ledger,
            recorder,
            commits,
            Reserve);

    public static GameplayDomainFactObserverBinding<
        SpeciesIncidentFact,
        SpeciesIncidentOutcomeReceipt> CreateSpeciesIncidentBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits) => new(
            "log:character.species-incident",
            Capture,
            registry,
            ledger,
            recorder,
            commits,
            Reserve);

    public static GameplayDomainFactObserverBinding<
        HarpyGaleFact,
        HarpyGaleRelocationOutcomeReceipt> CreateHarpyGaleBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits) => new(
            "log:character.harpy-gale-relocation",
            Capture,
            registry,
            ledger,
            recorder,
            commits);

    private static GameplayResultKey GolemKey(
        GolemUpkeepFactSaveData source) => new(
        EnvironmentOutcomeIds.GolemUpkeepProducer,
        new GameplayOperationId(source.operationId),
        source.revision,
        0);

    private static GameplayResultKey SpeciesIncidentKey(
        SpeciesIncidentFactSaveData source) => new(
        EnvironmentOutcomeIds.SpeciesIncidentProducer,
        new GameplayOperationId(source.operationId),
        source.revision,
        0);

    private static string State(
        float charge,
        float integrity,
        float health) => string.Join(
        "|",
        "charge=" + Number(charge),
        "integrity=" + Number(integrity),
        "health=" + Number(health));

    private static string Number(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);
}
