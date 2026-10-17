#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class LiveNarrativeStateObservation
{
    public string actorId = string.Empty;
    public string domain = string.Empty;
    public string factId = string.Empty;
    public string subjectId = string.Empty;
    public string outcome = string.Empty;
    public float value;
    public int day;

    public static LiveNarrativeStateObservation FromProjection(
        CharacterStateNarrativeProjection projection) => new()
    {
        actorId = projection.Character.Value,
        domain = projection.Domain.ToString(),
        factId = projection.FactId,
        subjectId = projection.SubjectId,
        outcome = projection.Outcome,
        value = projection.Value,
        day = projection.Day
    };
}

[Serializable]
public sealed class LiveNarrativeActivityWitness
{
    public string actorId = string.Empty;
    public List<CharacterNarrativeFact> factsBefore = new();
    public List<CharacterNarrativeFact> factsAfter = new();
    public List<CharacterActivityEvent> activities = new();
    public List<LiveNarrativeStateObservation> stateChanges = new();
}

public static class LiveNarrativeActivityEvidence
{
    private const string FixtureActorId = "character:live-activity-actor";
    private const string FixtureOtherActorId = "character:other-activity-actor";
    private const string FixtureActionId = "work:clean-live-room";
    private const string FixtureTargetId = "building:live-room";
    private const string FixtureOtherTargetId = "building:other-room";
    private const string FixtureFactId = "public-fact:live-activity-evidence";
    private const string FixtureStateFactId = "mood:live-state-evidence";
    private const string FixtureStatePublicFactId = "public-fact:live-state-evidence";
    private const string FixtureStateOutcome = "positive";
    private const string PreparedFactIdPrefix = "prepared:";

    public static bool TryMatch(
        LiveNarrativeActivityWitness witness,
        NarrativePublicFactSnapshot selectedFact,
        IReadOnlyCollection<string> selectedEvidenceIds,
        out string detail)
    {
        detail = string.Empty;
        if (witness == null || selectedFact == null || selectedEvidenceIds == null)
            return Reject("Activity evidence requires a witness, selected fact, and selected evidence IDs.", out detail);
        if (string.IsNullOrWhiteSpace(witness.actorId)
            || witness.factsBefore == null
            || witness.factsAfter == null
            || witness.activities == null)
            return Reject("Activity witness identity or collections are unavailable.", out detail);
        if (string.IsNullOrWhiteSpace(selectedFact.factId)
            || !selectedEvidenceIds.Contains(selectedFact.factId, StringComparer.Ordinal))
            return Reject("Selected public fact is not present in the selected evidence IDs.", out detail);
        if ((selectedFact.category & NarrativePublicFactCategory.Memory) == 0
            || !selectedFact.hasCount
            || selectedFact.count <= 0)
            return Reject("Selected public fact is not a counted activity-memory fact.", out detail);

        CharacterNarrativeFact[] beforeRows = MatchingRows(
            witness.factsBefore,
            selectedFact);
        CharacterNarrativeFact[] afterRows = MatchingRows(
            witness.factsAfter,
            selectedFact);
        if (beforeRows.Length > 1 || afterRows.Length != 1)
            return Reject("Legacy narrative rows are missing or ambiguous for the selected fact.", out detail);

        int beforeCount = beforeRows.Length == 0 ? 0 : beforeRows[0].count;
        int afterCount = afterRows[0].count;
        if (beforeCount < 0
            || afterCount <= beforeCount
            || selectedFact.count <= beforeCount
            || selectedFact.count > afterCount)
            return Reject("Selected cumulative count is outside the observed legacy-ledger increase.", out detail);

        int requiredObservations = selectedFact.count - beforeCount;
        int observedActivities = witness.activities.Count(activity =>
            MatchesActivity(witness.actorId, selectedFact, activity));
        if (observedActivities < requiredObservations)
            return Reject("Typed activity observations do not cover the selected cumulative increase.", out detail);

        detail = "activity-ledger-match; evidence=typed-activity+legacy-cumulative-increase"
            + "; exactOutcomeIdentity=none"
            + "; actor=" + witness.actorId
            + "; fact=" + selectedFact.factId
            + "; before=" + beforeCount
            + "; selected=" + selectedFact.count
            + "; after=" + afterCount
            + "; observedActivities=" + observedActivities;
        return true;
    }

    public static bool TryMatchState(
        LiveNarrativeActivityWitness witness,
        NarrativePublicFactSnapshot selectedFact,
        IReadOnlyCollection<string> selectedEvidenceIds,
        out string detail)
    {
        detail = string.Empty;
        if (witness == null || selectedFact == null || selectedEvidenceIds == null)
            return Reject("State evidence requires a witness, selected fact, and selected evidence IDs.", out detail);
        if (string.IsNullOrWhiteSpace(witness.actorId)
            || witness.factsBefore == null
            || witness.factsAfter == null
            || witness.stateChanges == null)
            return Reject("State witness identity or collections are unavailable.", out detail);
        if (string.IsNullOrWhiteSpace(selectedFact.factId)
            || !selectedEvidenceIds.Contains(selectedFact.factId, StringComparer.Ordinal))
            return Reject("Selected state-memory fact is not present in the selected evidence IDs.", out detail);
        if (selectedFact.factId.StartsWith(PreparedFactIdPrefix, StringComparison.Ordinal))
            return Reject("Prepared state-memory identities are not live evidence.", out detail);
        if ((selectedFact.category & NarrativePublicFactCategory.Memory) == 0
            || (selectedFact.category & NarrativePublicFactCategory.Background) != 0
            || !selectedFact.hasCount)
            return Reject("Selected public fact is not a counted state-memory fact.", out detail);
        if (string.IsNullOrWhiteSpace(selectedFact.domain)
            || string.IsNullOrWhiteSpace(selectedFact.originalFactId)
            || string.IsNullOrWhiteSpace(selectedFact.outcome))
            return Reject("Selected state-memory tuple is incomplete.", out detail);
        if (selectedFact.originalFactId.StartsWith("identity:prepared", StringComparison.Ordinal))
            return Reject("Prepared background is not an observed state change.", out detail);
        if (!string.Equals(
                selectedFact.domain,
                CharacterNarrativeDomain.Mood.ToString(),
                StringComparison.Ordinal)
            && !string.Equals(
                selectedFact.domain,
                CharacterNarrativeDomain.Need.ToString(),
                StringComparison.Ordinal))
            return Reject("Selected state-memory domain is not Mood or Need.", out detail);

        CharacterNarrativeFact[] beforeRows = MatchingStateLedgerRows(
            witness.factsBefore,
            selectedFact);
        CharacterNarrativeFact[] afterRows = MatchingStateLedgerRows(
            witness.factsAfter,
            selectedFact);
        if (beforeRows.Length > 1 || afterRows.Length != 1)
            return Reject("State narrative ledger tuple is missing or ambiguous.", out detail);
        CharacterNarrativeFact afterRow = afterRows[0];
        if ((beforeRows.Length == 1
                && !string.Equals(beforeRows[0].outcome, selectedFact.outcome, StringComparison.Ordinal))
            || !string.Equals(afterRow.outcome, selectedFact.outcome, StringComparison.Ordinal))
            return Reject("State narrative ledger outcome does not match the selected fact.", out detail);

        int beforeCount = beforeRows.Length == 0 ? 0 : beforeRows[0].count;
        int afterCount = afterRow.count;
        if (beforeCount < 0
            || afterCount < 0
            || selectedFact.count < 0
            || selectedFact.count <= beforeCount
            || selectedFact.count > afterCount)
            return Reject("Selected cumulative count is outside the observed state-ledger increase.", out detail);

        LiveNarrativeStateObservation[] observations = witness.stateChanges
            .Where(value => MatchesStateObservation(witness.actorId, selectedFact, value))
            .ToArray();
        int requiredObservations = afterCount - beforeCount;
        if (observations.Length != requiredObservations)
            return Reject("Typed state observations do not exactly cover the state-ledger increase.", out detail);

        float beforeTotal = beforeRows.Length == 0 ? 0f : beforeRows[0].totalValue;
        if (!IsFinite(beforeTotal) || !IsFinite(afterRow.totalValue))
            return Reject("State narrative ledger contains a non-finite cumulative value.", out detail);

        int selectedObservationCount = selectedFact.count - beforeCount;
        float cumulative = beforeTotal;
        float selectedCumulative = beforeTotal;
        for (int index = 0; index < observations.Length; index++)
        {
            LiveNarrativeStateObservation observation = observations[index];
            if (!IsFinite(observation.value) || observation.day < 0)
                return Reject("Typed state observation contains an invalid value or day.", out detail);
            cumulative += observation.value;
            if (!IsFinite(cumulative))
                return Reject("Typed state observations overflowed the finite ledger value.", out detail);
            if (index + 1 == selectedObservationCount)
                selectedCumulative = cumulative;
        }
        if (afterRow.totalValue != cumulative)
            return Reject("State ledger total does not equal the typed observation cumulative value.", out detail);

        if (!decimal.TryParse(
                selectedFact.totalValueDecimal,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal selectedTotal))
            return Reject("Selected state-memory total is missing or non-canonical.", out detail);
        decimal expectedSelectedTotal;
        try
        {
            expectedSelectedTotal = (decimal)selectedCumulative;
        }
        catch (OverflowException)
        {
            return Reject("Selected state-memory total exceeds the public decimal range.", out detail);
        }
        if (selectedTotal != expectedSelectedTotal)
            return Reject("Selected state-memory total does not equal the typed observation prefix.", out detail);

        detail = "state-ledger-match; evidence=typed-state+legacy-cumulative-increase"
            + "; actor=" + witness.actorId
            + "; fact=" + selectedFact.factId
            + "; before=" + beforeCount
            + "; selected=" + selectedFact.count
            + "; after=" + afterCount
            + "; observedStateChanges=" + observations.Length;
        return true;
    }

    public static int VerifyContract()
    {
        // Synthetic contract fixtures only. They prove join semantics; they are
        // not evidence that a production activity or model request occurred.
        int checks = 0;
        string[] selectedEvidenceIds = { FixtureFactId };

        NarrativePublicFactSnapshot newFact = CreateSelectedFact(1);
        LiveNarrativeActivityWitness newWitness = CreateWitness(
            newFact,
            beforeCount: null,
            afterCount: 1,
            activities: new[] { CreateActivity(
                FixtureActorId,
                CharacterActivityKinds.Work,
                FixtureActionId,
                FixtureTargetId,
                string.Empty,
                CharacterActivityOutcomes.Completed) });
        ExpectMatch(newWitness, newFact, selectedEvidenceIds, ref checks);

        NarrativePublicFactSnapshot cancelledFact = CreateSelectedFact(1, outcome: CharacterActivityOutcomes.Cancelled);
        LiveNarrativeActivityWitness cancelledWitness = CreateWitness(
            cancelledFact, beforeCount: null, afterCount: 1,
            CreateActivity(FixtureActorId, CharacterActivityKinds.Work, FixtureActionId,
                FixtureTargetId, string.Empty, CharacterActivityOutcomes.Cancelled));
        ExpectMatch(cancelledWitness, cancelledFact, selectedEvidenceIds, ref checks);

        LiveNarrativeActivityWitness unchangedLedger = Clone(cancelledWitness);
        unchangedLedger.factsBefore.Add(unchangedLedger.factsAfter[0].Clone());
        ExpectRejected(unchangedLedger, cancelledFact, selectedEvidenceIds, "activity without ledger increase", ref checks);

        NarrativePublicFactSnapshot existingFact = CreateSelectedFact(
            3,
            originalFactId: CharacterActivityKinds.Work);
        LiveNarrativeActivityWitness existingWitness = CreateWitness(
            existingFact,
            beforeCount: 2,
            afterCount: 4,
            activities: new[] { CreateActivity(
                FixtureActorId,
                CharacterActivityKinds.Work,
                string.Empty,
                string.Empty,
                FixtureTargetId,
                CharacterActivityOutcomes.Completed) });
        ExpectMatch(existingWitness, existingFact, selectedEvidenceIds, ref checks);

        ExpectRejected(null, newFact, selectedEvidenceIds, "null witness", ref checks);
        ExpectRejected(newWitness, null, selectedEvidenceIds, "null selected fact", ref checks);
        ExpectRejected(newWitness, newFact, null, "null selected evidence", ref checks);

        LiveNarrativeActivityWitness blankActor = Clone(newWitness);
        blankActor.actorId = " ";
        ExpectRejected(blankActor, newFact, selectedEvidenceIds, "blank actor", ref checks);

        LiveNarrativeActivityWitness observerAbsent = Clone(newWitness);
        observerAbsent.activities.Clear();
        ExpectRejected(observerAbsent, newFact, selectedEvidenceIds, "observer absent", ref checks);

        NarrativePublicFactSnapshot oldCountFact = CreateSelectedFact(2);
        LiveNarrativeActivityWitness oldCount = CreateWitness(
            oldCountFact,
            beforeCount: 2,
            afterCount: 3,
            activities: new[] { CreateActivity(
                FixtureActorId,
                CharacterActivityKinds.Work,
                FixtureActionId,
                FixtureTargetId,
                string.Empty,
                CharacterActivityOutcomes.Completed) });
        ExpectRejected(oldCount, oldCountFact, selectedEvidenceIds, "old count", ref checks);

        LiveNarrativeActivityWitness wrongActor = Clone(newWitness);
        wrongActor.activities[0] = CreateActivity(
            FixtureOtherActorId,
            CharacterActivityKinds.Work,
            FixtureActionId,
            FixtureTargetId,
            string.Empty,
            CharacterActivityOutcomes.Completed);
        ExpectRejected(wrongActor, newFact, selectedEvidenceIds, "wrong actor", ref checks);

        LiveNarrativeActivityWitness wrongTarget = Clone(newWitness);
        wrongTarget.activities[0] = CreateActivity(
            FixtureActorId,
            CharacterActivityKinds.Work,
            FixtureActionId,
            FixtureOtherTargetId,
            string.Empty,
            CharacterActivityOutcomes.Completed);
        ExpectRejected(wrongTarget, newFact, selectedEvidenceIds, "wrong target", ref checks);

        LiveNarrativeActivityWitness wrongOutcome = Clone(newWitness);
        wrongOutcome.activities[0] = CreateActivity(
            FixtureActorId,
            CharacterActivityKinds.Work,
            FixtureActionId,
            FixtureTargetId,
            string.Empty,
            CharacterActivityOutcomes.Failed);
        ExpectRejected(wrongOutcome, newFact, selectedEvidenceIds, "wrong outcome", ref checks);

        ExpectRejected(
            newWitness,
            newFact,
            new[] { "public-fact:not-selected" },
            "unselected fact",
            ref checks);

        LiveNarrativeActivityWitness domainMismatch = Clone(newWitness);
        domainMismatch.activities[0] = CreateActivity(
            FixtureActorId,
            CharacterActivityKinds.Combat,
            FixtureActionId,
            FixtureTargetId,
            string.Empty,
            CharacterActivityOutcomes.Completed);
        ExpectRejected(domainMismatch, newFact, selectedEvidenceIds, "domain mismatch", ref checks);

        LiveNarrativeActivityWitness duplicateBefore = Clone(existingWitness);
        duplicateBefore.factsBefore.Add(duplicateBefore.factsBefore[0].Clone());
        ExpectRejected(
            duplicateBefore,
            existingFact,
            selectedEvidenceIds,
            "duplicate before rows",
            ref checks);

        LiveNarrativeActivityWitness duplicateAfter = Clone(newWitness);
        duplicateAfter.factsAfter.Add(duplicateAfter.factsAfter[0].Clone());
        ExpectRejected(
            duplicateAfter,
            newFact,
            selectedEvidenceIds,
            "duplicate after rows",
            ref checks);

        NarrativePublicFactSnapshot beyondFact = CreateSelectedFact(3);
        LiveNarrativeActivityWitness beyondObservations = CreateWitness(
            beyondFact,
            beforeCount: 1,
            afterCount: 3,
            activities: new[] { CreateActivity(
                FixtureActorId,
                CharacterActivityKinds.Work,
                FixtureActionId,
                FixtureTargetId,
                string.Empty,
                CharacterActivityOutcomes.Completed) });
        ExpectRejected(
            beyondObservations,
            beyondFact,
            selectedEvidenceIds,
            "selected count beyond observations",
            ref checks);

        VerifyTransientOutcomeRejected(
            CharacterActivityOutcomes.Started,
            selectedEvidenceIds,
            ref checks);
        VerifyTransientOutcomeRejected(
            CharacterActivityOutcomes.Progress,
            selectedEvidenceIds,
            ref checks);

        NarrativePublicFactSnapshot missingCount = newFact.Clone();
        missingCount.hasCount = false;
        ExpectRejected(newWitness, missingCount, selectedEvidenceIds, "missing count", ref checks);

        NarrativePublicFactSnapshot zeroCount = newFact.Clone();
        zeroCount.count = 0;
        ExpectRejected(newWitness, zeroCount, selectedEvidenceIds, "zero count", ref checks);

        NarrativePublicFactSnapshot wrongCategory = newFact.Clone();
        wrongCategory.category = NarrativePublicFactCategory.Background;
        ExpectRejected(newWitness, wrongCategory, selectedEvidenceIds, "non-memory fact", ref checks);

        VerifyStateContract(ref checks);
        return checks;
    }

    private static CharacterNarrativeFact[] MatchingRows(
        IEnumerable<CharacterNarrativeFact> rows,
        NarrativePublicFactSnapshot selectedFact) =>
        (rows ?? Array.Empty<CharacterNarrativeFact>())
        .Where(row => row != null
            && string.Equals(row.domain.ToString(), selectedFact.domain, StringComparison.Ordinal)
            && string.Equals(row.factId, selectedFact.originalFactId, StringComparison.Ordinal)
            && string.Equals(row.subjectId, selectedFact.sourceSubjectId, StringComparison.Ordinal)
            && string.Equals(row.outcome, selectedFact.outcome, StringComparison.Ordinal))
        .ToArray();

    private static CharacterNarrativeFact[] MatchingStateLedgerRows(
        IEnumerable<CharacterNarrativeFact> rows,
        NarrativePublicFactSnapshot selectedFact) =>
        (rows ?? Array.Empty<CharacterNarrativeFact>())
        .Where(row => row != null
            && string.Equals(row.domain.ToString(), selectedFact.domain, StringComparison.Ordinal)
            && string.Equals(row.factId, selectedFact.originalFactId, StringComparison.Ordinal)
            && string.Equals(row.subjectId, selectedFact.sourceSubjectId, StringComparison.Ordinal))
        .ToArray();

    private static bool MatchesStateObservation(
        string actorId,
        NarrativePublicFactSnapshot selectedFact,
        LiveNarrativeStateObservation observation) =>
        observation != null
        && string.Equals(observation.actorId, actorId, StringComparison.Ordinal)
        && string.Equals(observation.domain, selectedFact.domain, StringComparison.Ordinal)
        && string.Equals(observation.factId, selectedFact.originalFactId, StringComparison.Ordinal)
        && string.Equals(observation.subjectId, selectedFact.sourceSubjectId, StringComparison.Ordinal)
        && string.Equals(observation.outcome, selectedFact.outcome, StringComparison.Ordinal);

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool MatchesActivity(
        string actorId,
        NarrativePublicFactSnapshot selectedFact,
        CharacterActivityEvent activity)
    {
        if (activity == null
            || !string.Equals(activity.ActorId, actorId, StringComparison.Ordinal)
            || !activity.NarrativeEligible
            || string.Equals(
                activity.OutcomeId,
                CharacterActivityOutcomes.Started,
                StringComparison.Ordinal)
            || string.Equals(
                activity.OutcomeId,
                CharacterActivityOutcomes.Progress,
                StringComparison.Ordinal))
            return false;

        string activityAction = string.IsNullOrWhiteSpace(activity.ActionId)
            ? activity.KindId
            : activity.ActionId;
        string activityTarget = string.IsNullOrWhiteSpace(activity.TargetId)
            ? activity.PlaceId
            : activity.TargetId;
        return string.Equals(
                   CharacterNarrativeDomainUtility.FromActivity(activity).ToString(),
                   selectedFact.domain,
                   StringComparison.Ordinal)
            && string.Equals(
                activityAction,
                selectedFact.originalFactId,
                StringComparison.Ordinal)
            && string.Equals(
                activityTarget,
                selectedFact.sourceSubjectId,
                StringComparison.Ordinal)
            && string.Equals(
                activity.OutcomeId,
                selectedFact.outcome,
                StringComparison.Ordinal);
    }

    private static void VerifyTransientOutcomeRejected(
        string outcomeId,
        IReadOnlyCollection<string> selectedEvidenceIds,
        ref int checks)
    {
        NarrativePublicFactSnapshot selected = CreateSelectedFact(1, outcome: outcomeId);
        LiveNarrativeActivityWitness witness = CreateWitness(
            selected,
            beforeCount: null,
            afterCount: 1,
            activities: new[] { CreateActivity(
                FixtureActorId,
                CharacterActivityKinds.Work,
                FixtureActionId,
                FixtureTargetId,
                string.Empty,
                outcomeId) });
        ExpectRejected(witness, selected, selectedEvidenceIds, outcomeId, ref checks);
    }

    private static void VerifyStateContract(ref int checks)
    {
        string[] selectedEvidenceIds = { FixtureStatePublicFactId };
        NarrativePublicFactSnapshot firstFact = CreateStateSelectedFact(1, 1.5f);
        LiveNarrativeActivityWitness firstWitness = CreateStateWitness(
            firstFact,
            beforeCount: null,
            beforeTotal: 0f,
            afterCount: 1,
            afterTotal: 1.5f,
            CreateStateObservation(FixtureActorId, FixtureStateFactId,
                FixtureTargetId, FixtureStateOutcome, 1.5f, 2));
        ExpectStateMatch(firstWitness, firstFact, selectedEvidenceIds, ref checks);

        NarrativePublicFactSnapshot needFact = firstFact.Clone();
        needFact.domain = CharacterNarrativeDomain.Need.ToString();
        LiveNarrativeActivityWitness needWitness = Clone(firstWitness);
        needWitness.factsAfter[0].domain = CharacterNarrativeDomain.Need;
        needWitness.stateChanges[0].domain = CharacterNarrativeDomain.Need.ToString();
        ExpectStateMatch(needWitness, needFact, selectedEvidenceIds, ref checks);

        NarrativePublicFactSnapshot prefixFact = CreateStateSelectedFact(2, 1.75f);
        LiveNarrativeActivityWitness prefixWitness = CreateStateWitness(
            prefixFact,
            beforeCount: 1,
            beforeTotal: 1.5f,
            afterCount: 3,
            afterTotal: 3.75f,
            CreateStateObservation(FixtureActorId, FixtureStateFactId,
                FixtureTargetId, FixtureStateOutcome, 0.25f, 3),
            CreateStateObservation(FixtureActorId, FixtureStateFactId,
                FixtureTargetId, FixtureStateOutcome, 2f, 4));
        ExpectStateMatch(prefixWitness, prefixFact, selectedEvidenceIds, ref checks);

        NarrativePublicFactSnapshot background = firstFact.Clone();
        background.category = NarrativePublicFactCategory.Background;
        ExpectStateRejected(firstWitness, background, selectedEvidenceIds,
            "background identity", ref checks);

        NarrativePublicFactSnapshot prepared = firstFact.Clone();
        prepared.factId = PreparedFactIdPrefix + FixtureStatePublicFactId;
        ExpectStateRejected(firstWitness, prepared, new[] { prepared.factId },
            "prepared identity", ref checks);
        NarrativePublicFactSnapshot preparedOrigin = firstFact.Clone();
        preparedOrigin.originalFactId = "identity:prepared-origin:origin:frontier";
        LiveNarrativeActivityWitness preparedWitness = Clone(firstWitness);
        preparedWitness.factsAfter[0].factId = preparedOrigin.originalFactId;
        preparedWitness.stateChanges[0].factId = preparedOrigin.originalFactId;
        ExpectStateRejected(preparedWitness, preparedOrigin, selectedEvidenceIds,
            "prepared original tuple", ref checks);

        NarrativePublicFactSnapshot missingFactId = firstFact.Clone();
        missingFactId.factId = string.Empty;
        ExpectStateRejected(firstWitness, missingFactId, new[] { string.Empty },
            "missing public fact identity", ref checks);

        NarrativePublicFactSnapshot unsupportedDomain = firstFact.Clone();
        unsupportedDomain.domain = CharacterNarrativeDomain.Work.ToString();
        LiveNarrativeActivityWitness unsupportedDomainWitness = Clone(firstWitness);
        unsupportedDomainWitness.factsAfter[0].domain = CharacterNarrativeDomain.Work;
        unsupportedDomainWitness.stateChanges[0].domain = CharacterNarrativeDomain.Work.ToString();
        ExpectStateRejected(unsupportedDomainWitness, unsupportedDomain, selectedEvidenceIds,
            "unsupported state domain", ref checks);

        LiveNarrativeActivityWitness missingObserver = Clone(firstWitness);
        missingObserver.stateChanges.Clear();
        ExpectStateRejected(missingObserver, firstFact, selectedEvidenceIds,
            "missing observer", ref checks);

        LiveNarrativeActivityWitness wrongActor = Clone(firstWitness);
        wrongActor.stateChanges[0].actorId = FixtureOtherActorId;
        ExpectStateRejected(wrongActor, firstFact, selectedEvidenceIds,
            "wrong actor", ref checks);

        LiveNarrativeActivityWitness wrongTuple = Clone(firstWitness);
        wrongTuple.stateChanges[0].factId = "mood:other-state";
        ExpectStateRejected(wrongTuple, firstFact, selectedEvidenceIds,
            "wrong tuple", ref checks);

        LiveNarrativeActivityWitness wrongOutcome = Clone(firstWitness);
        wrongOutcome.stateChanges[0].outcome = "negative";
        ExpectStateRejected(wrongOutcome, firstFact, selectedEvidenceIds,
            "wrong outcome", ref checks);

        LiveNarrativeActivityWitness wrongCount = Clone(prefixWitness);
        wrongCount.factsAfter[0].count = 4;
        ExpectStateRejected(wrongCount, prefixFact, selectedEvidenceIds,
            "wrong count", ref checks);

        LiveNarrativeActivityWitness wrongLedgerValue = Clone(prefixWitness);
        wrongLedgerValue.factsAfter[0].totalValue = 4.75f;
        ExpectStateRejected(wrongLedgerValue, prefixFact, selectedEvidenceIds,
            "wrong ledger value", ref checks);

        NarrativePublicFactSnapshot wrongSelectedValue = prefixFact.Clone();
        wrongSelectedValue.totalValueDecimal = "1.5";
        ExpectStateRejected(prefixWitness, wrongSelectedValue, selectedEvidenceIds,
            "wrong selected value", ref checks);

        LiveNarrativeActivityWitness nonFinite = Clone(firstWitness);
        nonFinite.stateChanges[0].value = float.NaN;
        ExpectStateRejected(nonFinite, firstFact, selectedEvidenceIds,
            "non-finite observation", ref checks);

        LiveNarrativeActivityWitness negativeDay = Clone(firstWitness);
        negativeDay.stateChanges[0].day = -1;
        ExpectStateRejected(negativeDay, firstFact, selectedEvidenceIds,
            "negative observation day", ref checks);

        LiveNarrativeActivityWitness duplicateAfter = Clone(firstWitness);
        duplicateAfter.factsAfter.Add(duplicateAfter.factsAfter[0].Clone());
        ExpectStateRejected(duplicateAfter, firstFact, selectedEvidenceIds,
            "duplicate state ledger tuple", ref checks);
    }

    private static NarrativePublicFactSnapshot CreateSelectedFact(
        int count,
        string originalFactId = FixtureActionId,
        string outcome = CharacterActivityOutcomes.Completed) => new()
    {
        factId = FixtureFactId,
        domain = CharacterNarrativeDomain.Work.ToString(),
        originalFactId = originalFactId,
        sourceSubjectId = FixtureTargetId,
        subjectId = string.Empty,
        category = NarrativePublicFactCategory.Memory,
        outcome = outcome,
        hasCount = true,
        count = count
    };

    private static NarrativePublicFactSnapshot CreateStateSelectedFact(
        int count,
        float totalValue) => new()
    {
        factId = FixtureStatePublicFactId,
        domain = CharacterNarrativeDomain.Mood.ToString(),
        originalFactId = FixtureStateFactId,
        sourceSubjectId = FixtureTargetId,
        subjectId = string.Empty,
        category = NarrativePublicFactCategory.Memory,
        outcome = FixtureStateOutcome,
        hasCount = true,
        count = count,
        totalValueDecimal = ((decimal)totalValue).ToString(CultureInfo.InvariantCulture)
    };

    private static LiveNarrativeActivityWitness CreateWitness(
        NarrativePublicFactSnapshot selectedFact,
        int? beforeCount,
        int afterCount,
        params CharacterActivityEvent[] activities)
    {
        LiveNarrativeActivityWitness witness = new()
        {
            actorId = FixtureActorId,
            factsAfter = new List<CharacterNarrativeFact>
            {
                CreateLedgerFact(selectedFact, afterCount)
            },
            activities = (activities ?? Array.Empty<CharacterActivityEvent>())
                .Where(value => value != null)
                .ToList()
        };
        if (beforeCount.HasValue)
            witness.factsBefore.Add(CreateLedgerFact(selectedFact, beforeCount.Value));
        return witness;
    }

    private static CharacterNarrativeFact CreateLedgerFact(
        NarrativePublicFactSnapshot selectedFact,
        int count) => new()
    {
        domain = Enum.Parse<CharacterNarrativeDomain>(selectedFact.domain),
        factId = selectedFact.originalFactId,
        subjectId = selectedFact.sourceSubjectId,
        outcome = selectedFact.outcome,
        count = count
    };

    private static LiveNarrativeActivityWitness CreateStateWitness(
        NarrativePublicFactSnapshot selectedFact,
        int? beforeCount,
        float beforeTotal,
        int afterCount,
        float afterTotal,
        params LiveNarrativeStateObservation[] stateChanges)
    {
        LiveNarrativeActivityWitness witness = new()
        {
            actorId = FixtureActorId,
            factsAfter = new List<CharacterNarrativeFact>
            {
                CreateStateLedgerFact(selectedFact, afterCount, afterTotal)
            },
            stateChanges = (stateChanges ?? Array.Empty<LiveNarrativeStateObservation>())
                .Where(value => value != null)
                .ToList()
        };
        if (beforeCount.HasValue)
        {
            witness.factsBefore.Add(CreateStateLedgerFact(
                selectedFact,
                beforeCount.Value,
                beforeTotal));
        }
        return witness;
    }

    private static CharacterNarrativeFact CreateStateLedgerFact(
        NarrativePublicFactSnapshot selectedFact,
        int count,
        float totalValue) => new()
    {
        domain = Enum.Parse<CharacterNarrativeDomain>(selectedFact.domain),
        factId = selectedFact.originalFactId,
        subjectId = selectedFact.sourceSubjectId,
        outcome = selectedFact.outcome,
        count = count,
        totalValue = totalValue
    };

    private static LiveNarrativeStateObservation CreateStateObservation(
        string actorId,
        string factId,
        string subjectId,
        string outcome,
        float value,
        int day) => LiveNarrativeStateObservation.FromProjection(
        new CharacterStateNarrativeProjection(
            (CharacterId)actorId,
            CharacterNarrativeDomain.Mood,
            factId,
            subjectId,
            outcome,
            value,
            day,
            triggerPassives: false,
            context: null));

    private static CharacterActivityEvent CreateActivity(
        string actorId,
        string kindId,
        string actionId,
        string targetId,
        string placeId,
        string outcomeId)
    {
        ActivityJsonFixture fixture = new()
        {
            kindId = kindId,
            actorId = actorId,
            actorName = "합성 계약 배우",
            actionId = actionId,
            targetId = targetId,
            placeId = placeId,
            outcomeId = outcomeId,
            factText = "합성 활동 계약 근거",
            narrativeEligible = true,
            visibleToPlayer = true
        };
        return JsonUtility.FromJson<CharacterActivityEvent>(JsonUtility.ToJson(fixture));
    }

    private static LiveNarrativeActivityWitness Clone(
        LiveNarrativeActivityWitness source) =>
        JsonUtility.FromJson<LiveNarrativeActivityWitness>(JsonUtility.ToJson(source));

    private static void ExpectMatch(
        LiveNarrativeActivityWitness witness,
        NarrativePublicFactSnapshot selectedFact,
        IReadOnlyCollection<string> selectedEvidenceIds,
        ref int checks)
    {
        if (!TryMatch(witness, selectedFact, selectedEvidenceIds, out string detail)
            || !detail.Contains("evidence=typed-activity+legacy-cumulative-increase", StringComparison.Ordinal)
            || !detail.Contains("exactOutcomeIdentity=none", StringComparison.Ordinal))
            throw new InvalidOperationException("Expected synthetic activity evidence match: " + detail);
        checks++;
    }

    private static void ExpectRejected(
        LiveNarrativeActivityWitness witness,
        NarrativePublicFactSnapshot selectedFact,
        IReadOnlyCollection<string> selectedEvidenceIds,
        string label,
        ref int checks)
    {
        if (TryMatch(witness, selectedFact, selectedEvidenceIds, out string detail)
            || string.IsNullOrWhiteSpace(detail))
            throw new InvalidOperationException("Expected synthetic activity evidence rejection: " + label);
        checks++;
    }

    private static void ExpectStateMatch(
        LiveNarrativeActivityWitness witness,
        NarrativePublicFactSnapshot selectedFact,
        IReadOnlyCollection<string> selectedEvidenceIds,
        ref int checks)
    {
        if (!TryMatchState(witness, selectedFact, selectedEvidenceIds, out string detail)
            || !detail.Contains("evidence=typed-state+legacy-cumulative-increase", StringComparison.Ordinal))
            throw new InvalidOperationException("Expected synthetic state evidence match: " + detail);
        checks++;
    }

    private static void ExpectStateRejected(
        LiveNarrativeActivityWitness witness,
        NarrativePublicFactSnapshot selectedFact,
        IReadOnlyCollection<string> selectedEvidenceIds,
        string label,
        ref int checks)
    {
        if (TryMatchState(witness, selectedFact, selectedEvidenceIds, out string detail)
            || string.IsNullOrWhiteSpace(detail))
            throw new InvalidOperationException("Expected synthetic state evidence rejection: " + label);
        checks++;
    }

    private static bool Reject(string reason, out string detail)
    {
        detail = reason;
        return false;
    }

    [Serializable]
    private sealed class ActivityJsonFixture
    {
        public string kindId = string.Empty;
        public string actorId = string.Empty;
        public string actorName = string.Empty;
        public string actionId = string.Empty;
        public string targetId = string.Empty;
        public string targetName = string.Empty;
        public string placeId = string.Empty;
        public string placeName = string.Empty;
        public string outcomeId = string.Empty;
        public string reasonCode = string.Empty;
        public string factText = string.Empty;
        public float value;
        public int quantity;
        public int facilityId = -1;
        public float sentiment;
        public bool narrativeEligible;
        public bool bubbleEligible;
        public bool visibleToPlayer = true;
    }
}
#endif
