using System;
using System.Collections.Generic;
using System.Linq;

// Observer-side projection of the existing durable work-completion contract.
// Business work receipts are handled independently by AnimalHusbandryWorkFactMapping.
internal static class AnimalHusbandryCompletionObservation
{
    private static readonly GameplayOutcomeFactId KindFact = new("husbandry-kind");
    private static readonly GameplayOutcomeFactId PenFact = new("pen-id");
    private static readonly GameplayOutcomeFactId AnimalFact = new("animal-id");
    private static readonly GameplayOutcomeFactId WorkerFact = new("worker-id");

    internal static WorkCompletedIdentityEvent CreateCompletionEvent(
        AnimalHusbandryPendingOutcomeState pending) => CreateRequest(new AnimalHusbandryCompletionFact(pending)).ToEvent();

    internal static WorkCompletionIdentityDeliveryRequest CreateRequest(AnimalHusbandryCompletionFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        string[] parts = fact.ParentPublicationKey.Split('|');
        if (parts == null || parts.Length != 4)
            throw new ArgumentException("A canonical husbandry completion child is required.");
        string producer = MigratedProducerOutcomeCatalog.Get(
            MigratedProducerOutcomeKind.AnimalHusbandryWorkResult).ProducerId;
        GameplayResultKey child = WorkCompletionChildReference.Require(
            parts[1], producer, fact.ParentPublicationKey);
        if (!child.Equals(MigratedProducerOutcomeCatalog.ResultKey(
                MigratedProducerOutcomeKind.AnimalHusbandryWorkResult, fact.WorkOperationId, child.CommitRevision)))
            throw new ArgumentException("Completion child is not the exact husbandry work operation.");
        return WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(new WorkCompletedIdentityEvent(
            fact.WorkerId, BuiltInWorkTypeIds.AnimalCare.Value,
            StableKind(fact.Kind) + "|" + fact.AnimalId.Value,
            fact.Origin, fact.AbsoluteDay,
            parts[1], producer, child.ToString(), fact.PenId.Value,
            fact.ContributionWorkUnits, fact.SkillModifierProvenance));
    }

    internal static bool MatchesCompletionChild(
        WorkCompletionIdentityDeliveryRequest request, GameplayOutcomeSnapshot child)
    {
        MigratedProducerOutcomeDefinition definition = MigratedProducerOutcomeCatalog.Get(
            MigratedProducerOutcomeKind.AnimalHusbandryWorkResult);
        return request.WorkId == BuiltInWorkTypeIds.AnimalCare.Value
            && request.TerminalOwnerId == definition.ProducerId
            && child.producerId == definition.ProducerId
            && child.outcomeTypeId == definition.OutcomeTypeId.Value
            && child.status == GameplayOutcomeStatus.Succeeded
            && child.absoluteDay == request.AbsoluteDay
            && child.facts.Any(value => value.factId == WorkerFact.Value
                && value.value == request.Character.Value)
            && child.facts.Any(value => value.factId == PenFact.Value
                && value.value == request.TargetEntityId)
            && child.facts.Any(kind => kind.factId == KindFact.Value
                && child.facts.Any(animal => animal.factId == AnimalFact.Value
                    && request.ProductId == kind.value + "|" + animal.value))
            && child.metrics.Any(value => value.metricId == MigratedProducerOutcomeIds.CompletedWorkMetric.Value
                && request.ContributionWorkUnits > 0f
                && request.ContributionWorkUnits <= value.value / 1000f + 0.001f);
    }

    internal static WorkCompletionIdentityDeliveryRequest CreateSavedRequest(AnimalHusbandryPendingOutcomeSaveData owner) =>
        CreateRequest(new AnimalHusbandryCompletionFact(new AnimalHusbandryPendingOutcomeState
        {
            CompletionIdentityRequired = owner.completionIdentityRequired,
            OperationId = owner.operationId, Kind = owner.kind,
            AnimalId = new WildlifeInstanceId(owner.animalId), PenId = new BuildingInstanceId(owner.penId),
            SpeciesId = new WildlifeSpeciesId(owner.speciesId), WorkerId = owner.workerId,
            AbsoluteDay = owner.absoluteDay, PublicationKey = owner.publicationKey,
            CompletedWorkMilliUnits = owner.completedWorkMilliUnits,
            CompletionOrigin = owner.completionOrigin,
            CompletionContributionWorkUnits = owner.completionContributionWorkUnits,
            CompletionSkillModifierProvenance = owner.completionSkillModifierProvenance
        }));

    internal static void ValidateCompletionRestoreJoin(
        IEnumerable<AnimalHusbandryPendingOutcomeSaveData> owners,
        GameplayOutcomeLedgerSaveData ledger)
    {
        if (owners == null || ledger?.outbox == null || ledger.exactOutcomes == null
            || ledger.tombstones == null)
            throw new InvalidOperationException("Husbandry completion join requires saved owners and ledger.");
        var exact = ledger.outbox.Concat(ledger.exactOutcomes).Where(value => value != null)
            .ToLookup(value => new GameplayResultKey(value.producerId,
                new GameplayOperationId(value.operationId), value.commitRevision, value.localResultIndex).ToString(),
                StringComparer.Ordinal);
        var retired = ledger.tombstones.Where(value => value != null)
            .ToLookup(value => new GameplayResultKey(value.producerId,
                new GameplayOperationId(value.operationId), value.commitRevision, value.localResultIndex).ToString(),
                StringComparer.Ordinal);
        foreach (AnimalHusbandryPendingOutcomeSaveData owner in owners)
        {
            if (owner == null)
                throw new InvalidOperationException("Husbandry completion join contains a null owner.");
            // An effect may legitimately await its business commit. The normal
            // section preflight still validates its complete DTO and authored IDs.
            if (!owner.completionIdentityRequired || !owner.outcomeCommitted) continue;
            // Read-only completion projection: never publish or replace an owner.
            WorkCompletionIdentityDeliveryRequest request = CreateSavedRequest(owner);
            if (!string.IsNullOrEmpty(owner.completionPublicationKey)
                && owner.completionPublicationKey != WorkCompletionIdentityOutcomeIds.ResultKey(request).ToString())
                throw new InvalidOperationException("Husbandry completion recovery key differs from its work: " + owner.operationId);
            GameplayOutcomeSnapshot[] children = exact[request.ChildResultKey].ToArray();
            GameplayOutcomeTombstoneSnapshot[] tombstones = retired[request.ChildResultKey].ToArray();
            if (children.Length + tombstones.Length != 1
                || children.Length == 1 && children[0].absoluteDay != owner.absoluteDay
                || tombstones.Length == 1 && tombstones[0].absoluteDay != owner.absoluteDay)
                throw new InvalidOperationException(
                    "Husbandry completion has no unique committed child/date: " + owner.operationId);
            // Keep the existing compacted/forgotten ledger contract. Tombstones
            // retain the canonical identity/date, not the discarded fact payload.
            if (children.Length == 1 && (!MatchesCompletionChild(request, children[0])
                || !children[0].facts.Any(value => value.factId == MigratedProducerOutcomeIds.OperationFact.Value
                    && value.value == owner.operationId)
                || !children[0].metrics.Any(value => value.metricId == MigratedProducerOutcomeIds.CompletedWorkMetric.Value
                    && value.value == owner.completedWorkMilliUnits)))
                throw new InvalidOperationException(
                    "Husbandry retained completion differs from its frozen child: " + owner.operationId);
        }
    }


    private static string StableKind(AnimalHusbandryOutcomeOperationKind kind) =>
        AnimalHusbandryDomainOperationIdentity.StableKind(kind);
}
