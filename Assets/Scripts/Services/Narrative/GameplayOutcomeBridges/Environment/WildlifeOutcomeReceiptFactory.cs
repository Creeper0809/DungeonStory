using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using static EnvironmentOutcomeReceiptFactorySupport;

public static class WildlifeOutcomeReceiptFactory
{
    public static SpeciesIncidentOutcomeReceipt CreateSpeciesIncident(
        GameplayResultKey resultKey,
        long ownerRevision,
        CharacterId characterId,
        string characterDisplayName,
        CharacterSpeciesId speciesId,
        string speciesDisplayName,
        string incidentId,
        string summary,
        CoreGridCell location,
        int absoluteDay)
    {
        var character = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value);
        var species = new GameplayEntityId(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.SpeciesIncidentTriggered,
            absoluteDay,
            GameplayOutcomeStatus.Failed,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            character,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(characterId.Value, characterDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            species,
            EnvironmentOutcomeIds.SpeciesRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(speciesId.Value, speciesDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value,
            0.82f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value,
            0.64f)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision,
            EnvironmentOutcomeIds.RevisionUnit,
            character)));
        Require(builder.AddTag(EnvironmentOutcomeIds.WildlifeTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "species-incident", incidentId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(SpeciesIncidentTriggeredEvent))));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.IncidentFact,
            incidentId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SummaryFact,
            summary)));
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
        return new SpeciesIncidentOutcomeReceipt(builder.Build());
    }

    public static GolemUpkeepOutcomeReceipt CreateGolemUpkeep(
        GameplayResultKey resultKey,
        long ownerRevision,
        CharacterId characterId,
        string characterDisplayName,
        CharacterSpeciesId speciesId,
        string speciesDisplayName,
        string episodeKind,
        string stateBefore,
        string stateAfter,
        string physicalChild,
        string workChild,
        string bodyChild,
        string summary,
        CoreGridCell location,
        int absoluteDay)
    {
        if (!resultKey.IsValid || ownerRevision <= 0L)
            throw new ArgumentException("A valid Golem episode identity is required.");
        string kind = RequireStable(episodeKind, nameof(episodeKind));
        string before = RequireFactValue(stateBefore, nameof(stateBefore));
        string after = RequireFactValue(stateAfter, nameof(stateAfter));
        string physical = RequireFactValue(physicalChild, nameof(physicalChild));
        string work = RequireFactValue(workChild, nameof(workChild));
        string body = RequireFactValue(bodyChild, nameof(bodyChild));
        string exactSummary = RequireFactValue(summary, nameof(summary));
        var character = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value);
        var species = new GameplayEntityId(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.GolemUpkeepEpisode,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            character,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(characterId.Value, characterDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            species,
            EnvironmentOutcomeIds.SpeciesRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(speciesId.Value, speciesDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value,
            0.90f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value,
            0.72f)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision,
            EnvironmentOutcomeIds.RevisionUnit,
            character)));
        Require(builder.AddTag(EnvironmentOutcomeIds.SpeciesTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "golem-upkeep", resultKey.OperationId.Value)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(GolemUpkeepOutcomeReceipt))));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.EpisodeKindFact, kind)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.StateBeforeFact, before)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.StateAfterFact, after)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.PhysicalChildFact, physical)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.WorkChildFact, work)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.BodyChildFact, body)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SummaryFact, exactSummary)));
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
        return new GolemUpkeepOutcomeReceipt(builder.Build());
    }

    public static HarpyGaleRelocationOutcomeReceipt
        CreateHarpyGaleRelocation(
            GameplayResultKey resultKey,
            long ownerRevision,
            CharacterId characterId,
            string characterDisplayName,
            CharacterSpeciesId speciesId,
            string speciesDisplayName,
            in PreparedPhysicalItemRelocationPreview physical,
            int absoluteDay)
    {
        if (!physical.IsValid || ownerRevision <= 0L)
            throw new ArgumentException(
                "A valid prepared relocation preview is required.",
                nameof(physical));
        PhysicalItemRelocationReceipt relocation = physical.Receipt;
        var character = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value);
        var species = new GameplayEntityId(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value);
        GameplayEntityId item = physical.ItemInstanceId.Length > 0
            ? new GameplayEntityId(
                EnvironmentOutcomeIds.ItemInstanceKind,
                physical.ItemInstanceId)
            : new GameplayEntityId(
                EnvironmentOutcomeIds.ItemStackKind,
                relocation.SourceStackId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.HarpyGaleRelocation,
            absoluteDay,
            GameplayOutcomeStatus.Failed,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            character,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(
                characterId.Value,
                characterDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            species,
            EnvironmentOutcomeIds.SpeciesRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(speciesId.Value, speciesDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            item,
            EnvironmentOutcomeIds.ItemRole,
            GameplayParticipationKind.Direct,
            true,
            PhysicalItemFactDisplayName.Snapshot(relocation.ItemId, physical.DisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value,
            0.82f)));
        Require(builder.AddSubject(new GameplayOutcomeSubjectLink(
            item,
            0.62f,
            NarrativeMemoryTier.Recent,
            false,
            false,
            0)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value,
            0.64f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision, EnvironmentOutcomeIds.RevisionUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.QuantityMetric,
            relocation.Quantity, EnvironmentOutcomeIds.CountUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.MassMetric,
            relocation.MassGrams, EnvironmentOutcomeIds.GramUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.SourceXMetric,
            relocation.SourcePosition.x, EnvironmentOutcomeIds.CellUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.SourceYMetric,
            relocation.SourcePosition.y, EnvironmentOutcomeIds.CellUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.DestinationXMetric,
            relocation.DestinationPosition.x, EnvironmentOutcomeIds.CellUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.DestinationYMetric,
            relocation.DestinationPosition.y, EnvironmentOutcomeIds.CellUnit, item);
        Require(builder.AddTag(EnvironmentOutcomeIds.WildlifeTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "physical-relocation",
            relocation.OperationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(PhysicalItemRelocationReceipt));
        AddFact(ref builder, EnvironmentOutcomeIds.IncidentFact,
            CharacterSpeciesIncidentIds.HarpyGaleCommotion);
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact,
            relocation.OperationId);
        AddFact(ref builder, EnvironmentOutcomeIds.SourceStackFact,
            relocation.SourceStackId);
        AddFact(ref builder, EnvironmentOutcomeIds.DestinationStackFact,
            relocation.DestinationStackId);
        AddFact(ref builder, EnvironmentOutcomeIds.ItemDefinitionFact,
            relocation.ItemId);
        AddFact(ref builder, EnvironmentOutcomeIds.ItemInstanceFact,
            physical.ItemInstanceId.Length > 0 ? physical.ItemInstanceId : "none");
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            $"{physical.DisplayName} {relocation.Quantity}개가 돌풍에 "
            + $"({relocation.SourcePosition.x},{relocation.SourcePosition.y})에서 "
            + $"({relocation.DestinationPosition.x},{relocation.DestinationPosition.y})로 흩어졌다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon",
            string.Empty,
            relocation.DestinationPosition.x,
            relocation.DestinationPosition.y));
        return new HarpyGaleRelocationOutcomeReceipt(builder.Build());
    }

    public static WildlifeFoodRaidOutcomeReceipt CreateWildlifeFoodRaid(
        WildlifeFoodRaidFactData source,
        string physicalChildResultKey,
        GameplayOutcomeId physicalChildOutcomeId)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate();
        if (!physicalChildOutcomeId.IsValid)
            throw new ArgumentException(
                "A valid physical child outcome is required.",
                nameof(physicalChildOutcomeId));

        string operationId = RequireStable(
            source.dispositionOperationId,
            nameof(source.dispositionOperationId));
        string raidId = RequireStable(source.raidId, nameof(source.raidId));
        string wildlifeId = RequireStable(
            source.wildlifeId,
            nameof(source.wildlifeId));
        string speciesId = RequireStable(
            source.wildlifeSpeciesId,
            nameof(source.wildlifeSpeciesId));
        string stackId = RequireStable(
            source.targetStackId,
            nameof(source.targetStackId));
        string itemId = RequireStable(
            source.targetItemId,
            nameof(source.targetItemId));
        string wildlifeName = RequireDisplay(
            source.wildlifeDisplayName,
            nameof(source.wildlifeDisplayName));
        string itemName = RequireDisplay(
            source.targetItemDisplayName,
            nameof(source.targetItemDisplayName));
        string childResultKey = physicalChildResultKey?.Trim() ?? string.Empty;
        if (childResultKey.Length == 0
            || !string.Equals(
                childResultKey,
                physicalChildResultKey,
                StringComparison.Ordinal))
            throw new ArgumentException(
                "A physical child result key is required.",
                nameof(physicalChildResultKey));
        long ownerRevision = source.dispositionOwnerRevision;

        var wildlife = new GameplayEntityId(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId);
        var species = new GameplayEntityId(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId);
        var stack = new GameplayEntityId(
            EnvironmentOutcomeIds.ItemStackKind,
            stackId);
        var item = new GameplayEntityId(
            EnvironmentOutcomeIds.ItemDefinitionKind,
            itemId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.WildlifeFoodRaidProducer,
                new GameplayOperationId(operationId),
                ownerRevision,
                0),
            EnvironmentOutcomeIds.WildlifeFoodRaidCompleted,
            source.completedAbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);

        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            wildlife,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(wildlifeId, wildlifeName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            species,
            EnvironmentOutcomeIds.SpeciesRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(speciesId, wildlifeName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            stack,
            EnvironmentOutcomeIds.TargetRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(stackId, itemName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            item,
            EnvironmentOutcomeIds.ItemRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(itemId, itemName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId,
            0.86f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId,
            0.48f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.ItemStackKind,
            stackId,
            0.54f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.ItemDefinitionKind,
            itemId,
            0.62f)));

        AddMetric(ref builder, EnvironmentOutcomeIds.RequestedQuantityMetric,
            source.requestedQuantity, EnvironmentOutcomeIds.CountUnit, stack);
        AddMetric(ref builder, EnvironmentOutcomeIds.StolenQuantityMetric,
            source.stolenQuantity, EnvironmentOutcomeIds.CountUnit, stack);
        AddMetric(ref builder, EnvironmentOutcomeIds.MassMetric,
            source.dispositionInputMassGrams, EnvironmentOutcomeIds.GramUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.SourceXMetric,
            source.startX, EnvironmentOutcomeIds.CellUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.SourceYMetric,
            source.startY, EnvironmentOutcomeIds.CellUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.DestinationXMetric,
            source.endX, EnvironmentOutcomeIds.CellUnit, stack);
        AddMetric(ref builder, EnvironmentOutcomeIds.DestinationYMetric,
            source.endY, EnvironmentOutcomeIds.CellUnit, stack);
        AddMetric(ref builder, EnvironmentOutcomeIds.StartedTimeMetric,
            source.startedGameTime, EnvironmentOutcomeIds.SecondUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.CompletedTimeMetric,
            source.completedGameTime, EnvironmentOutcomeIds.SecondUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.StartedDayMetric,
            source.startedAbsoluteDay, EnvironmentOutcomeIds.CountUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.CompletedDayMetric,
            source.completedAbsoluteDay, EnvironmentOutcomeIds.CountUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision, EnvironmentOutcomeIds.RevisionUnit, wildlife);

        Require(builder.AddTag(EnvironmentOutcomeIds.WildlifeTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.WildlifeFoodRaidProducer,
            operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(WildlifeFoodRaidOrderSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.RaidIdFact, raidId);
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.CommitFact,
            source.dispositionCommitId);
        AddFact(ref builder, EnvironmentOutcomeIds.SourceStackFact, stackId);
        AddFact(ref builder, EnvironmentOutcomeIds.ItemDefinitionFact, itemId);
        AddFact(ref builder, EnvironmentOutcomeIds.TerminalStateFact,
            WildlifeFoodRaidOrderState.Stolen.ToString());
        AddFact(ref builder, EnvironmentOutcomeIds.ReasonFact,
            RequireDisplay(source.outcomeReason, nameof(source.outcomeReason)));
        AddFact(ref builder, EnvironmentOutcomeIds.ChildResultKeyFact,
            childResultKey);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            $"{wildlifeName}이 {itemName} {source.stolenQuantity}개를 훔쳤다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, source.endX, source.endY));
        builder.SetCausation(new GameplayOutcomeCausation(
            physicalChildOutcomeId,
            new GameplayOperationId(operationId),
            "physical-theft-consumption"));
        return new WildlifeFoodRaidOutcomeReceipt(builder.Build());
    }

    public static WildlifeCommandOutcomeReceipt CreateWildlifeCommand(
        WildlifeGameplayOutcomeSaveData source)
    {
        ValidateWildlifeOutcomeBase(
            source,
            WildlifeGameplayOutcomeProfile.Command);
        string operationId = RequireStable(source.operationId, nameof(source.operationId));
        string wildlifeId = RequireStable(source.wildlifeId, nameof(source.wildlifeId));
        string wildlifeName = RequireDisplay(
            source.wildlifeDisplayName,
            nameof(source.wildlifeDisplayName));
        string eventKind = RequireStable(source.eventKind, nameof(source.eventKind));
        string reason = RequireStable(source.reasonCode, nameof(source.reasonCode));
        var wildlife = new GameplayEntityId(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.WildlifeCommandProducer,
                new GameplayOperationId(operationId),
                source.ownerRevision,
                0),
            EnvironmentOutcomeIds.WildlifeCommand,
            source.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            source.ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            wildlife,
            EnvironmentOutcomeIds.WildlifeRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(wildlifeId, wildlifeName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId,
            0.62f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.StateBeforeMetric,
            source.stateBefore, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.StateAfterMetric,
            source.stateAfter, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.FlagBeforeMetric,
            source.flagBefore ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.FlagAfterMetric,
            source.flagAfter ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.PriorityBeforeMetric,
            source.priorityBefore ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.PriorityAfterMetric,
            source.priorityAfter ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            source.ownerRevision, EnvironmentOutcomeIds.RevisionUnit, wildlife);
        Require(builder.AddTag(EnvironmentOutcomeIds.WildlifeTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.WildlifeCommandProducer,
            operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(WildlifeGameplayOutcomeSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.EventKindFact, eventKind);
        AddFact(ref builder, EnvironmentOutcomeIds.FacilityFact,
            OptionalFact(source.facilityId));
        AddFact(ref builder, EnvironmentOutcomeIds.RelatedActorFact,
            OptionalFact(source.actorId));
        AddFact(ref builder, EnvironmentOutcomeIds.ReasonFact, reason);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            BuildWildlifeCommandSummary(source, wildlifeName));
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, source.x, source.y));
        return new WildlifeCommandOutcomeReceipt(builder.Build());
    }

    public static WildlifeActionOutcomeReceipt CreateWildlifeAction(
        WildlifeGameplayOutcomeSaveData source)
    {
        ValidateWildlifeOutcomeBase(
            source,
            WildlifeGameplayOutcomeProfile.Action);
        string operationId = RequireStable(source.operationId, nameof(source.operationId));
        string wildlifeId = RequireStable(source.wildlifeId, nameof(source.wildlifeId));
        string actorId = RequireStable(source.actorId, nameof(source.actorId));
        string wildlifeName = RequireDisplay(
            source.wildlifeDisplayName,
            nameof(source.wildlifeDisplayName));
        string actorName = RequireDisplay(
            source.actorDisplayName,
            nameof(source.actorDisplayName));
        string eventKind = RequireStable(source.eventKind, nameof(source.eventKind));
        string reason = RequireStable(source.reasonCode, nameof(source.reasonCode));
        var actor = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            actorId);
        var wildlife = new GameplayEntityId(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.WildlifeActionProducer,
                new GameplayOperationId(operationId),
                source.ownerRevision,
                0),
            EnvironmentOutcomeIds.WildlifeAction,
            source.absoluteDay,
            string.Equals(eventKind, "hunt-attack", StringComparison.Ordinal)
                && !source.hit
                ? GameplayOutcomeStatus.Failed
                : GameplayOutcomeStatus.Succeeded,
            source.ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            actor,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(actorId, actorName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            wildlife,
            EnvironmentOutcomeIds.WildlifeRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(wildlifeId, wildlifeName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            actorId,
            0.62f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId,
            source.killed ? 0.96f : source.hit ? 0.78f : 0.52f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.StateBeforeMetric,
            source.stateBefore, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.StateAfterMetric,
            source.stateAfter, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.HealthBeforeMetric,
            source.healthBefore, EnvironmentOutcomeIds.PointUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.HealthAfterMetric,
            source.healthAfter, EnvironmentOutcomeIds.PointUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.DamageMetric,
            source.damage, EnvironmentOutcomeIds.PointUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.AmmunitionBeforeMetric,
            source.ammunitionBefore, EnvironmentOutcomeIds.CountUnit, actor);
        AddMetric(ref builder, EnvironmentOutcomeIds.AmmunitionConsumedMetric,
            source.ammunitionConsumed, EnvironmentOutcomeIds.CountUnit, actor);
        AddMetric(ref builder, EnvironmentOutcomeIds.AmmunitionAfterMetric,
            source.ammunitionAfter, EnvironmentOutcomeIds.CountUnit, actor);
        AddMetric(ref builder, EnvironmentOutcomeIds.HitMetric,
            source.hit ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.KilledMetric,
            source.killed ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.OutputQuantityMetric,
            source.outputQuantity, EnvironmentOutcomeIds.CountUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.OutputMassMetric,
            source.outputMassGrams, EnvironmentOutcomeIds.GramUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            source.ownerRevision, EnvironmentOutcomeIds.RevisionUnit, wildlife);
        Require(builder.AddTag(EnvironmentOutcomeIds.WildlifeTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.WildlifeActionProducer,
            operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(WildlifeGameplayOutcomeSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.EventKindFact, eventKind);
        AddFact(ref builder, EnvironmentOutcomeIds.WeaponDefinitionFact,
            OptionalFact(source.weaponDefinitionId));
        AddFact(ref builder, EnvironmentOutcomeIds.WeaponInstanceFact,
            OptionalFact(source.weaponInstanceId));
        AddFact(ref builder, EnvironmentOutcomeIds.AmmunitionItemFact,
            OptionalFact(source.ammunitionItemId));
        AddFact(ref builder, EnvironmentOutcomeIds.BodyPartFact,
            OptionalFact(source.bodyPart));
        AddFact(ref builder, EnvironmentOutcomeIds.CoverSourceFact,
            OptionalFact(source.coverSourceId));
        AddFact(ref builder, EnvironmentOutcomeIds.CarcassStackFact,
            OptionalFact(source.carcassStackId));
        AddFact(ref builder, EnvironmentOutcomeIds.FacilityFact,
            OptionalFact(source.facilityId));
        AddFact(ref builder, EnvironmentOutcomeIds.ChildResultKeyFact,
            OptionalFact(source.physicalChildResultKey));
        AddFact(ref builder, EnvironmentOutcomeIds.ReasonFact, reason);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            BuildWildlifeActionSummary(source, actorName, wildlifeName));
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, source.x, source.y));
        return new WildlifeActionOutcomeReceipt(builder.Build());
    }

    private static void ValidateWildlifeOutcomeBase(
        WildlifeGameplayOutcomeSaveData source,
        WildlifeGameplayOutcomeProfile profile)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source.schemaVersion != WildlifeGameplayOutcomeSaveData.CurrentSchemaVersion
            || source.profile != profile
            || source.absoluteDay < 0
            || source.ownerRevision <= 0L
            || source.stateBefore < 0
            || source.stateAfter < 0
            || source.healthBefore < 0
            || source.healthAfter < 0
            || source.damage < 0
            || source.ammunitionBefore < 0
            || source.ammunitionConsumed < 0
            || source.ammunitionAfter < 0
            || source.outputQuantity < 0
            || source.outputMassGrams < 0L)
        {
            throw new InvalidOperationException(
                "A valid current wildlife gameplay outcome is required.");
        }
    }

    private static string OptionalFact(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();

    private static string BuildWildlifeCommandSummary(
        WildlifeGameplayOutcomeSaveData source,
        string wildlifeName) => source.eventKind switch
        {
            "capture-release" => $"{wildlifeName}을 야생으로 방생했다.",
            "companion-assigned" =>
                $"{wildlifeName}을 {RequireDisplay(source.actorDisplayName, nameof(source.actorDisplayName))}의 동료로 배정했다.",
            "companion-cleared" => $"{wildlifeName}의 동료 배정을 해제했다.",
            "haul-assigned" => $"{wildlifeName}에게 운반 역할을 배정했다.",
            "haul-cleared" => $"{wildlifeName}의 운반 역할을 해제했다.",
            "haul-clear-requested" =>
                $"{wildlifeName}의 보유 화물을 배송한 뒤 운반 역할을 해제하도록 명령했다.",
            _ => source.flagAfter
                ? source.priorityAfter
                    ? $"{wildlifeName}을 우선 사냥감으로 지정했다."
                    : $"{wildlifeName}을 사냥감으로 지정했다."
                : $"{wildlifeName}의 사냥 지정을 해제했다."
        };

    private static string BuildWildlifeActionSummary(
        WildlifeGameplayOutcomeSaveData source,
        string actorName,
        string wildlifeName) => source.eventKind switch
        {
            "capture-order" =>
                $"{actorName}이 {wildlifeName}의 생포 운반을 시작했다.",
            "capture-carry-started" =>
                $"{actorName}이 {wildlifeName}을 들어 운반하기 시작했다.",
            "capture-penned" =>
                $"{actorName}이 {wildlifeName}을 우리로 운반했다.",
            "carcass-butchered" =>
                $"{actorName}이 {wildlifeName} 사체를 도축해 산출물을 얻었다.",
            _ => source.killed
                ? $"{actorName}이 {wildlifeName}을 처치했다."
                : source.hit
                    ? $"{actorName}이 {wildlifeName}에게 {source.damage} 피해를 입혔다."
                    : $"{actorName}의 공격이 {wildlifeName}에게 적중하지 않았다."
        };

}
