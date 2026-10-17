#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Editor-only bridge from a strictly authored equipment story to the existing
/// equipment-history authorities. This capture does not mutate a runtime
/// equipment instance and does not claim that the authored story occurred in
/// natural play.
///
/// choiceInputHash is SHA-256 over sorted-key compact UTF-8 JSON with this exact
/// projection:
/// {
///   schemaVersion: 1,
///   subject: { persistentId, displayName, background },
///   events: [ every strict authored event, in input order ],
///   generation,
///   ownerId,
///   ownerName
/// }
/// caseId, scenarioFamilyId, profileId, selectedIndex and choiceInputHash are
/// deliberately excluded so an EquipmentChoice and EvolutionHistory case can
/// bind the same authored choice input without sharing their case identity.
/// </summary>
public static class NarrativeControlledEquipmentCapture
{
    private const string EquipmentChoiceProfile = "EquipmentChoice";
    private const string EvolutionHistoryProfile = "EvolutionHistory";
    private const string ChoiceHashAlgorithm = "sha256";
    private const string ChoiceHashCanonicalization = "sorted-key-compact-utf8";
    private const string CurrentHistorySlice = "current-generation-only-no-prior-history";
    private const string PublicationPolicy = "authored-controlled-equipment-current-generation-v1";
    private const int MaximumLockedCurrentEvents = 8;

    private static readonly string[] CaseFields =
    {
        "caseId",
        "scenarioFamilyId",
        "profileId",
        "subject",
        "events",
        "state"
    };

    private static readonly string[] SubjectFields =
    {
        "persistentId",
        "displayName",
        "background"
    };

    private static readonly string[] EventFields =
    {
        "eventKey",
        "eventId",
        "text",
        "domain",
        "outcome",
        "day",
        "count",
        "valueDecimal",
        "historicalEvidenceKind",
        "sourceTags",
        "actorId",
        "actorName"
    };

    private static readonly string[] ChoiceStateFields =
    {
        "generation",
        "ownerId",
        "ownerName"
    };

    private static readonly string[] HistoryStateFields =
    {
        "generation",
        "ownerId",
        "ownerName",
        "selectedIndex",
        "choiceInputHash"
    };

    public static JObject Capture(JObject input)
    {
        ControlledCase value = ParseCase(input);
        JObject choiceProjection = BuildChoiceInputProjection(value);
        string choiceInputHash = ComputeCanonicalSha256(choiceProjection);
        if (string.Equals(value.ProfileId, EvolutionHistoryProfile, StringComparison.Ordinal)
            && !string.Equals(
                value.ChoiceInputHash,
                choiceInputHash,
                StringComparison.Ordinal))
        {
            throw Fail(
                "controlled-equipment-choice-input-hash-mismatch",
                $"EvolutionHistory case '{value.CaseId}' supplied choiceInputHash "
                + $"'{value.ChoiceInputHash}', but its exact same-input EquipmentChoice "
                + $"projection hashes to '{choiceInputHash}'.");
        }

        UsageLedger ledger = new UsageLedger();
        UsageLedgerCompactor compactor = new UsageLedgerCompactor();
        RecordEvents(value, choiceInputHash, ledger, compactor);
        if (ledger.currentGenerationEvents == null
            || ledger.currentGenerationEvents.Count != value.Events.Count)
        {
            throw Fail(
                "controlled-equipment-ledger-event-loss",
                $"Case '{value.CaseId}' recorded {ledger.currentGenerationEvents?.Count ?? 0} "
                + $"of {value.Events.Count} authored events. Truncated equipment history "
                + "cannot be exported.");
        }
        if (ledger.compactedSegments == null || ledger.compactedSegments.Count != 0)
        {
            throw Fail(
                "controlled-equipment-prior-history-unexpected",
                $"Case '{value.CaseId}' only supports the explicit no-prior-history slice.");
        }

        List<string> candidateIds = EquipmentEvolutionRules
            .BuildLegalHistoricalEffectCandidates(ledger);
        RequireLegalCandidatePacket(value.CaseId, candidateIds);
        JArray fullLegalCandidates = BuildCandidateDefinitions(candidateIds);
        string historyHash = compactor.ComputeHistoryHash(ledger);
        if (string.IsNullOrWhiteSpace(historyHash))
        {
            throw Fail(
                "controlled-equipment-history-hash-empty",
                $"UsageLedgerCompactor returned an empty history hash for '{value.CaseId}'.");
        }

        EquipmentEvolutionState equipmentState = new EquipmentEvolutionState
        {
            generation = value.Generation,
            usageLedger = ledger
        };
        int effectBudget = equipmentState.ResonanceBudget;
        if (effectBudget <= 0)
        {
            throw Fail(
                "controlled-equipment-resonance-budget-invalid",
                $"EquipmentEvolutionState.ResonanceBudget returned {effectBudget} for "
                + $"generation {value.Generation} in '{value.CaseId}'.");
        }

        EvolutionNarrativeRequestSnapshot lockedRequest =
            CreateProductionRequest(
                value,
                candidateIds,
                historyHash,
                choiceInputHash,
                ledger,
                effectBudget);
        NarrativeMechanicScenarioPublicContext publicContext =
            BuildAuthoredPublicContext(value, choiceInputHash);
        JArray publicFacts = ParseArray(
            publicContext.PublicFacts.ToCanonicalString(),
            "controlled-equipment-public-facts-invalid");
        JObject publicNarrativeContext = ParseObject(
            publicContext.PublicNarrativeContext.ToCanonicalString(),
            "controlled-equipment-public-context-invalid");
        RequireAuthoredPublicationPreserved(
            value,
            choiceInputHash,
            publicFacts,
            publicNarrativeContext);

        JObject request = string.Equals(
                value.ProfileId,
                EquipmentChoiceProfile,
                StringComparison.Ordinal)
            ? new JObject
            {
                ["candidateCount"] = candidateIds.Count
            }
            : new JObject
            {
                ["lockedRequest"] = BuildLockedRequest(lockedRequest)
            };

        JObject authorityContext = BuildAuthorityContext(
            value,
            choiceProjection,
            choiceInputHash,
            ledger,
            candidateIds,
            lockedRequest,
            effectBudget,
            historyHash);

        return new JObject
        {
            ["scenarioId"] = BuildScenarioId(value),
            ["profileId"] = value.ProfileId,
            ["targetPersistentId"] = value.SubjectPersistentId,
            ["accepted"] = true,
            ["failureReason"] = string.Empty,
            ["publicFacts"] = publicFacts,
            ["publicNarrativeContext"] = publicNarrativeContext,
            ["request"] = request,
            ["fullLegalCandidates"] = fullLegalCandidates,
            ["authorityContext"] = authorityContext
        };
    }

    private static ControlledCase ParseCase(JObject input)
    {
        if (input == null)
        {
            throw Fail(
                "controlled-equipment-input-null",
                "NarrativeControlledEquipmentCapture requires a non-null JObject case.");
        }
        RequireExactFields(
            input,
            CaseFields,
            "case",
            "controlled-equipment-case-schema");

        string caseId = RequireCanonicalString(input, "caseId", "case");
        string scenarioFamilyId = RequireCanonicalString(
            input,
            "scenarioFamilyId",
            "case");
        string profileId = RequireCanonicalString(input, "profileId", "case");
        if (!string.Equals(profileId, EquipmentChoiceProfile, StringComparison.Ordinal)
            && !string.Equals(profileId, EvolutionHistoryProfile, StringComparison.Ordinal))
        {
            throw Fail(
                "controlled-equipment-profile-unsupported",
                $"Case '{caseId}' profileId must be exactly '{EquipmentChoiceProfile}' "
                + $"or '{EvolutionHistoryProfile}', not '{profileId}'.");
        }

        JObject subject = RequireObject(input, "subject", "case");
        RequireExactFields(
            subject,
            SubjectFields,
            "subject",
            "controlled-equipment-subject-schema");
        string subjectPersistentId = RequireCanonicalString(
            subject,
            "persistentId",
            "subject");
        string subjectDisplayName = RequireCanonicalString(
            subject,
            "displayName",
            "subject");
        string subjectBackground = RequireCanonicalString(
            subject,
            "background",
            "subject");

        JArray eventArray = RequireArray(input, "events", "case");
        if (eventArray.Count == 0)
        {
            throw Fail(
                "controlled-equipment-events-empty",
                $"Case '{caseId}' requires at least one authored equipment event.");
        }
        if (eventArray.Count > MaximumLockedCurrentEvents)
        {
            throw Fail(
                "controlled-equipment-events-exceed-locked-request-capacity",
                $"Case '{caseId}' has {eventArray.Count} events, but the production locked "
                + $"request can preserve at most {MaximumLockedCurrentEvents} current events. "
                + "This explicit no-truncation slice rejects the case instead of silently "
                + "omitting ledger evidence.");
        }
        if (eventArray.Count > UsageLedger.RawEventCapacity)
        {
            throw Fail(
                "controlled-equipment-events-exceed-ledger-capacity",
                $"Case '{caseId}' exceeds UsageLedger.RawEventCapacity. No event was truncated.");
        }

        List<ControlledEvent> events = new List<ControlledEvent>(eventArray.Count);
        HashSet<string> eventKeys = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, string> participantNames =
            new Dictionary<string, string>(StringComparer.Ordinal);
        int previousDay = -1;
        for (int index = 0; index < eventArray.Count; index++)
        {
            if (!(eventArray[index] is JObject eventObject))
            {
                throw Fail(
                    "controlled-equipment-event-not-object",
                    $"Case '{caseId}' events[{index}] must be an object.");
            }
            RequireExactFields(
                eventObject,
                EventFields,
                $"events[{index}]",
                "controlled-equipment-event-schema");
            ControlledEvent authoredEvent = ParseEvent(eventObject, caseId, index);
            if (index > 0 && authoredEvent.Day < previousDay)
            {
                throw Fail(
                    "controlled-equipment-event-day-order",
                    $"Case '{caseId}' events[{index}] day {authoredEvent.Day} precedes "
                    + $"events[{index - 1}] day {previousDay}. Authored event days must be "
                    + "nondecreasing so input order is a valid public chronology.");
            }
            previousDay = authoredEvent.Day;
            if (!eventKeys.Add(authoredEvent.EventKey))
            {
                throw Fail(
                    "controlled-equipment-event-key-duplicate",
                    $"Case '{caseId}' repeats eventKey '{authoredEvent.EventKey}'.");
            }
            if (string.Equals(
                authoredEvent.ActorId,
                subjectPersistentId,
                StringComparison.Ordinal))
            {
                throw Fail(
                    "controlled-equipment-actor-is-subject",
                    $"Case '{caseId}' event '{authoredEvent.EventKey}' uses the equipment "
                    + "subject ID as a character actor ID.");
            }
            if (participantNames.TryGetValue(
                    authoredEvent.ActorId,
                    out string existingActorName)
                && !string.Equals(
                    existingActorName,
                    authoredEvent.ActorName,
                    StringComparison.Ordinal))
            {
                throw Fail(
                    "controlled-equipment-participant-name-conflict",
                    $"Case '{caseId}' assigns both '{existingActorName}' and "
                    + $"'{authoredEvent.ActorName}' to actorId '{authoredEvent.ActorId}'.");
            }
            participantNames[authoredEvent.ActorId] = authoredEvent.ActorName;
            events.Add(authoredEvent);
        }

        JObject state = RequireObject(input, "state", "case");
        RequireExactFields(
            state,
            string.Equals(profileId, EquipmentChoiceProfile, StringComparison.Ordinal)
                ? ChoiceStateFields
                : HistoryStateFields,
            "state",
            "controlled-equipment-state-schema",
            string.Equals(profileId, EvolutionHistoryProfile, StringComparison.Ordinal)
                ? " This bridge supports only the explicit current-generation/no-prior-history slice."
                : string.Empty);
        int generation = RequireInt32(state, "generation", "state", 0);
        string ownerId = RequireCanonicalString(state, "ownerId", "state");
        string ownerName = RequireCanonicalString(state, "ownerName", "state");
        if (!participantNames.TryGetValue(ownerId, out string publicOwnerName))
        {
            throw Fail(
                "controlled-equipment-owner-not-participant",
                $"Case '{caseId}' ownerId '{ownerId}' must appear as an authored event actor.");
        }
        if (!string.Equals(publicOwnerName, ownerName, StringComparison.Ordinal))
        {
            throw Fail(
                "controlled-equipment-owner-name-mismatch",
                $"Case '{caseId}' ownerName '{ownerName}' does not match the authored "
                + $"actor name '{publicOwnerName}' for '{ownerId}'.");
        }

        int selectedIndex = -1;
        string choiceInputHash = string.Empty;
        if (string.Equals(profileId, EvolutionHistoryProfile, StringComparison.Ordinal))
        {
            selectedIndex = RequireInt32(state, "selectedIndex", "state", 0);
            choiceInputHash = RequireCanonicalString(
                state,
                "choiceInputHash",
                "state");
            if (!IsSha256Identifier(choiceInputHash))
            {
                throw Fail(
                    "controlled-equipment-choice-input-hash-format",
                    $"Case '{caseId}' choiceInputHash must be 'sha256:' followed by 64 "
                    + "lowercase hexadecimal characters.");
            }
        }

        return new ControlledCase(
            caseId,
            scenarioFamilyId,
            profileId,
            subjectPersistentId,
            subjectDisplayName,
            subjectBackground,
            events,
            generation,
            ownerId,
            ownerName,
            selectedIndex,
            choiceInputHash);
    }

    private static ControlledEvent ParseEvent(
        JObject input,
        string caseId,
        int index)
    {
        string location = $"events[{index}]";
        string eventKey = RequireCanonicalString(input, "eventKey", location);
        string eventId = RequireCanonicalString(input, "eventId", location);
        string text = RequireCanonicalString(input, "text", location);
        string domain = RequireCanonicalString(input, "domain", location);
        if (!Enum.TryParse(
                domain,
                ignoreCase: false,
                out CharacterNarrativeDomain parsedDomain)
            || !Enum.IsDefined(typeof(CharacterNarrativeDomain), parsedDomain)
            || !string.Equals(parsedDomain.ToString(), domain, StringComparison.Ordinal))
        {
            throw Fail(
                "controlled-equipment-domain-unknown",
                $"Case '{caseId}' event '{eventKey}' domain must be an exact "
                + $"CharacterNarrativeDomain enum name, not '{domain}'.");
        }
        string outcome = RequireCanonicalString(input, "outcome", location);
        int day = RequireInt32(input, "day", location, 0);
        int count = RequireInt32(input, "count", location, 1);
        string valueText = RequireCanonicalString(input, "valueDecimal", location);
        decimal decimalValue = ParseDecimal(valueText, caseId, eventKey);
        if (!float.TryParse(
                valueText,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out float ledgerValue)
            || float.IsNaN(ledgerValue)
            || float.IsInfinity(ledgerValue))
        {
            throw Fail(
                "controlled-equipment-value-not-binary32",
                $"Case '{caseId}' event '{eventKey}' valueDecimal '{valueText}' cannot "
                + "be represented by the production binary32 usage ledger.");
        }

        string evidenceKindText = RequireCanonicalString(
            input,
            "historicalEvidenceKind",
            location);
        if (!Enum.TryParse(
                evidenceKindText,
                ignoreCase: false,
                out HistoricalEvidenceKind evidenceKind)
            || !Enum.IsDefined(typeof(HistoricalEvidenceKind), evidenceKind)
            || !string.Equals(
                evidenceKind.ToString(),
                evidenceKindText,
                StringComparison.Ordinal))
        {
            throw Fail(
                "controlled-equipment-evidence-kind-unknown",
                $"Case '{caseId}' event '{eventKey}' has unknown HistoricalEvidenceKind "
                + $"'{evidenceKindText}'.");
        }

        JArray sourceTagArray = RequireArray(input, "sourceTags", location);
        List<string> sourceTags = new List<string>(sourceTagArray.Count);
        HashSet<string> uniqueTags = new HashSet<string>(StringComparer.Ordinal);
        for (int tagIndex = 0; tagIndex < sourceTagArray.Count; tagIndex++)
        {
            if (sourceTagArray[tagIndex]?.Type != JTokenType.String)
            {
                throw Fail(
                    "controlled-equipment-source-tag-type",
                    $"Case '{caseId}' event '{eventKey}' sourceTags[{tagIndex}] must be a string.");
            }
            string tag = RequireCanonicalText(
                sourceTagArray[tagIndex].Value<string>(),
                $"{location}.sourceTags[{tagIndex}]");
            if (!uniqueTags.Add(tag))
            {
                throw Fail(
                    "controlled-equipment-source-tag-duplicate",
                    $"Case '{caseId}' event '{eventKey}' repeats source tag '{tag}'.");
            }
            sourceTags.Add(tag);
        }

        string actorId = RequireCanonicalString(input, "actorId", location);
        string actorName = RequireCanonicalString(input, "actorName", location);
        return new ControlledEvent(
            eventKey,
            eventId,
            text,
            domain,
            outcome,
            day,
            count,
            valueText,
            decimalValue,
            ledgerValue,
            evidenceKind,
            evidenceKindText,
            sourceTags,
            actorId,
            actorName);
    }

    private static void RecordEvents(
        ControlledCase value,
        string choiceInputHash,
        UsageLedger ledger,
        UsageLedgerCompactor compactor)
    {
        foreach (ControlledEvent authoredEvent in value.Events)
        {
            string evidenceId = BuildEvidenceId(
                choiceInputHash,
                authoredEvent.EventKey);
            UsageLedgerEvent recorded = compactor.Record(
                ledger,
                authoredEvent.EventId,
                authoredEvent.LedgerValue,
                authoredEvent.ActorId,
                value.SubjectPersistentId,
                authoredEvent.SourceTags,
                evidenceId,
                authoredEvent.EvidenceKind,
                authoredEvent.Outcome,
                value.Generation,
                authoredEvent.Count);
            if (!string.Equals(recorded.evidenceId, evidenceId, StringComparison.Ordinal)
                || !string.Equals(
                    recorded.eventId,
                    authoredEvent.EventId,
                    StringComparison.Ordinal)
                || !string.Equals(recorded.actorId, authoredEvent.ActorId, StringComparison.Ordinal)
                || !string.Equals(
                    recorded.targetId,
                    value.SubjectPersistentId,
                    StringComparison.Ordinal)
                || recorded.generation != value.Generation
                || recorded.repeatCount != authoredEvent.Count)
            {
                throw Fail(
                    "controlled-equipment-ledger-record-mismatch",
                    $"UsageLedgerCompactor changed identity, participant, count or generation "
                    + $"for case '{value.CaseId}' event '{authoredEvent.EventKey}'.");
            }
        }
    }

    private static EvolutionNarrativeRequestSnapshot CreateProductionRequest(
        ControlledCase value,
        IReadOnlyList<string> candidateIds,
        string historyHash,
        string choiceInputHash,
        UsageLedger ledger,
        int effectBudget)
    {
        bool history = string.Equals(
            value.ProfileId,
            EvolutionHistoryProfile,
            StringComparison.Ordinal);
        if (history
            && (value.SelectedIndex < 0 || value.SelectedIndex >= candidateIds.Count))
        {
            throw Fail(
                "controlled-equipment-selected-index-out-of-range",
                $"EvolutionHistory case '{value.CaseId}' selectedIndex "
                + $"{value.SelectedIndex} is outside the exact same-input legal candidate "
                + $"range 0..{candidateIds.Count - 1}.");
        }

        string selectedEffectId = history
            ? candidateIds[value.SelectedIndex]
            : string.Empty;
        string nodeSeed = string.Join(
            "|",
            "controlled-equipment-node-v1",
            value.ProfileId,
            value.SubjectPersistentId,
            choiceInputHash,
            value.Generation.ToString(CultureInfo.InvariantCulture),
            history ? value.SelectedIndex.ToString(CultureInfo.InvariantCulture) : "unselected",
            selectedEffectId);
        EvolutionNode node = new EvolutionNode
        {
            nodeId = (history
                    ? "controlled-equipment-history-node:"
                    : "controlled-equipment-choice-node:")
                + StableEvolutionHash.Compute(nodeSeed),
            parentNodeId = string.Empty,
            effectId = selectedEffectId,
            generation = value.Generation,
            historical = history,
            active = history,
            mechanicallyUnlocked = true,
            narrativeReady = false,
            uiVisible = true,
            playerVisible = true,
            legalCandidateEffectIds = candidateIds.ToList(),
            selectedCandidateIndex = history ? value.SelectedIndex : -1
        };
        EvolutionNarrativeRequestSnapshot request =
            EvolutionNarrativeRequestFactory.Create(
                EvolutionNarrativeTargetKind.Equipment,
                value.SubjectPersistentId,
                node,
                historyHash,
                ledger,
                effectBudget);
        if (!string.Equals(
                request.targetPersistentId,
                value.SubjectPersistentId,
                StringComparison.Ordinal)
            || !string.Equals(request.nodeId, node.nodeId, StringComparison.Ordinal)
            || !string.Equals(request.parentNodeId, string.Empty, StringComparison.Ordinal)
            || !string.Equals(request.historyHash, historyHash, StringComparison.Ordinal)
            || request.generation != value.Generation
            || request.effectBudget != effectBudget
            || !request.legalCandidateEffectIds.SequenceEqual(
                candidateIds,
                StringComparer.Ordinal)
            || !request.frozenHistoryCaptured
            || request.frozenPriorGenerationSegments == null
            || request.frozenPriorGenerationSegments.Count != 0)
        {
            throw Fail(
                "controlled-equipment-production-request-mismatch",
                $"EvolutionNarrativeRequestFactory did not preserve the locked current-generation "
                + $"request for case '{value.CaseId}'.");
        }
        RequireLockedLedgerCoverage(value, ledger, request);
        if (history
            && (!string.Equals(request.effectId, selectedEffectId, StringComparison.Ordinal)
                || request.selectedCandidateIndex != value.SelectedIndex))
        {
            throw Fail(
                "controlled-equipment-selected-effect-mismatch",
                $"EvolutionHistory case '{value.CaseId}' did not lock the selected legal effect.");
        }
        if (!history && !string.IsNullOrEmpty(request.effectId))
        {
            throw Fail(
                "controlled-equipment-choice-answer-leak",
                $"EquipmentChoice case '{value.CaseId}' unexpectedly locked an effect before selection.");
        }
        return request;
    }

    private static void RequireLockedLedgerCoverage(
        ControlledCase value,
        UsageLedger ledger,
        EvolutionNarrativeRequestSnapshot request)
    {
        List<UsageLedgerEvent> suppliedEvents = ledger.currentGenerationEvents?
            .Where(entry => entry != null)
            .ToList() ?? new List<UsageLedgerEvent>();
        List<UsageLedgerEvent> frozenEvents = request.frozenCurrentEvents?
            .Where(entry => entry != null)
            .ToList() ?? new List<UsageLedgerEvent>();
        if (suppliedEvents.Count != value.Events.Count
            || frozenEvents.Count != suppliedEvents.Count)
        {
            throw Fail(
                "controlled-equipment-locked-event-count-mismatch",
                $"EvolutionNarrativeRequestFactory froze {frozenEvents.Count} of "
                + $"{suppliedEvents.Count} supplied ledger events for case '{value.CaseId}'. "
                + "No current event may be truncated from this controlled slice.");
        }

        List<string> expectedEvidenceIds = suppliedEvents
            .Select(entry => entry.evidenceId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if (request.evidenceIds == null
            || request.evidenceIds.Count != suppliedEvents.Count
            || !request.evidenceIds.SequenceEqual(
                expectedEvidenceIds,
                StringComparer.Ordinal))
        {
            throw Fail(
                "controlled-equipment-locked-evidence-id-mismatch",
                $"EvolutionNarrativeRequestFactory did not bind every supplied ledger "
                + $"evidence ID exactly once for case '{value.CaseId}'.");
        }

        Dictionary<string, UsageLedgerEvent> frozenByEvidenceId =
            new Dictionary<string, UsageLedgerEvent>(StringComparer.Ordinal);
        foreach (UsageLedgerEvent frozenEvent in frozenEvents)
        {
            if (string.IsNullOrEmpty(frozenEvent.evidenceId)
                || !frozenByEvidenceId.TryAdd(frozenEvent.evidenceId, frozenEvent))
            {
                throw Fail(
                    "controlled-equipment-locked-evidence-duplicate",
                    $"EvolutionNarrativeRequestFactory produced a blank or duplicate frozen "
                    + $"evidence ID for case '{value.CaseId}'.");
            }
        }

        foreach (UsageLedgerEvent suppliedEvent in suppliedEvents)
        {
            if (!frozenByEvidenceId.TryGetValue(
                    suppliedEvent.evidenceId,
                    out UsageLedgerEvent frozenEvent)
                || !UsageLedgerEventsEqual(suppliedEvent, frozenEvent))
            {
                throw Fail(
                    "controlled-equipment-locked-event-mismatch",
                    $"EvolutionNarrativeRequestFactory did not preserve the complete ledger "
                    + $"event '{suppliedEvent.evidenceId}' for case '{value.CaseId}'.");
            }
        }
    }

    private static bool UsageLedgerEventsEqual(
        UsageLedgerEvent expected,
        UsageLedgerEvent actual) =>
        string.Equals(expected.evidenceId, actual.evidenceId, StringComparison.Ordinal)
        && string.Equals(expected.eventId, actual.eventId, StringComparison.Ordinal)
        && string.Equals(expected.actorId, actual.actorId, StringComparison.Ordinal)
        && string.Equals(expected.targetId, actual.targetId, StringComparison.Ordinal)
        && expected.amount.Equals(actual.amount)
        && expected.historicalEvidenceKind == actual.historicalEvidenceKind
        && string.Equals(expected.outcomeId, actual.outcomeId, StringComparison.Ordinal)
        && expected.generation == actual.generation
        && expected.repeatCount == actual.repeatCount
        && expected.sequence == actual.sequence
        && (expected.sourceTags ?? new List<string>()).SequenceEqual(
            actual.sourceTags ?? new List<string>(),
            StringComparer.Ordinal);

    private static NarrativeMechanicScenarioPublicContext BuildAuthoredPublicContext(
        ControlledCase value,
        string choiceInputHash)
    {
        List<NarrativePublicEntityInput> entities =
            new List<NarrativePublicEntityInput>
            {
                new NarrativePublicEntityInput(
                    value.SubjectPersistentId,
                    NarrativePublicEntityKind.Equipment,
                    value.SubjectDisplayName)
            };
        foreach (KeyValuePair<string, string> actor in value.Events
                     .Select(entry => new KeyValuePair<string, string>(
                         entry.ActorId,
                         entry.ActorName))
                     .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            entities.Add(new NarrativePublicEntityInput(
                actor.Key,
                NarrativePublicEntityKind.Character,
                actor.Value));
        }

        List<NarrativePublicFactInput> facts =
            new List<NarrativePublicFactInput>
            {
                new NarrativePublicFactInput(
                    "Background",
                    "controlled-equipment-background:" + choiceInputHash,
                    value.SubjectPersistentId,
                    value.SubjectBackground,
                    2000,
                    NarrativePublicFactCategory.Background,
                    value.SubjectPersistentId)
            };
        for (int index = 0; index < value.Events.Count; index++)
        {
            ControlledEvent authoredEvent = value.Events[index];
            string evidenceId = BuildEvidenceId(
                choiceInputHash,
                authoredEvent.EventKey);
            NarrativePublicEventInput eventData = new NarrativePublicEventInput(
                evidenceId,
                authoredEvent.ActorId,
                value.SubjectPersistentId,
                authoredEvent.EventId,
                authoredEvent.Outcome,
                authoredEvent.Day,
                authoredEvent.Count,
                value.Generation);
            facts.Add(new NarrativePublicFactInput(
                authoredEvent.Domain,
                authoredEvent.EventKey,
                value.SubjectPersistentId,
                authoredEvent.Text,
                1500 - index,
                NarrativePublicFactCategory.Memory,
                value.SubjectPersistentId,
                authoredEvent.Outcome,
                authoredEvent.Day,
                authoredEvent.Count,
                null,
                authoredEvent.DecimalValue,
                eventData));
        }

        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            value.ProfileId,
            value.SubjectPersistentId,
            NarrativePublicSubjectKind.Equipment,
            string.Empty,
            requireCharacterFact: true,
            requireMotif: true,
            entities,
            facts,
            PublicationPolicy,
            maximumFacts: facts.Count);
        return NarrativeMechanicScenarioPublicContextSerializer.Serialize(material);
    }

    private static void RequireAuthoredPublicationPreserved(
        ControlledCase value,
        string choiceInputHash,
        JArray publicFacts,
        JObject publicNarrativeContext)
    {
        HashSet<string> publishedTexts = publicFacts
            .OfType<JObject>()
            .Select(fact => fact["text"]?.Value<string>() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        if (!publishedTexts.Contains(value.SubjectBackground))
        {
            throw Fail(
                "controlled-equipment-background-not-published",
                $"Production public projection omitted the authored background for '{value.CaseId}'.");
        }
        foreach (ControlledEvent authoredEvent in value.Events)
        {
            if (!publishedTexts.Contains(authoredEvent.Text))
            {
                throw Fail(
                    "controlled-equipment-event-text-not-published",
                    $"Production public projection omitted authored text for case "
                    + $"'{value.CaseId}' event '{authoredEvent.EventKey}'.");
            }
        }

        JArray entities = publicNarrativeContext["entities"] as JArray;
        JArray publicEvents = publicNarrativeContext["events"] as JArray;
        JArray priorHistoryFactIds =
            publicNarrativeContext["priorHistoryFactIds"] as JArray;
        JObject selection = publicNarrativeContext["selection"] as JObject;
        if (entities == null
            || publicEvents == null
            || priorHistoryFactIds == null
            || priorHistoryFactIds.Count != 0
            || selection == null
            || selection["availableFactCount"]?.Value<int>() != value.Events.Count + 1
            || selection["omittedFactCount"]?.Value<int>() != 0
            || publicFacts.Count != value.Events.Count + 1
            || publicEvents.Count != value.Events.Count)
        {
            throw Fail(
                "controlled-equipment-public-event-count-mismatch",
                $"Production public projection did not preserve the complete {value.Events.Count}-event "
                + $"current-generation/no-prior-history slice for '{value.CaseId}'.");
        }
        bool subjectFound = entities.OfType<JObject>().Any(entity =>
            string.Equals(
                entity["entityId"]?.Value<string>(),
                value.SubjectPersistentId,
                StringComparison.Ordinal)
            && string.Equals(
                entity["displayName"]?.Value<string>(),
                value.SubjectDisplayName,
                StringComparison.Ordinal)
            && string.Equals(
                entity["kind"]?.Value<string>(),
                "equipment",
                StringComparison.Ordinal));
        if (!subjectFound)
        {
            throw Fail(
                "controlled-equipment-public-subject-missing",
                $"Production public projection omitted the authored equipment subject "
                + $"from case '{value.CaseId}'.");
        }
        foreach (ControlledEvent authoredEvent in value.Events)
        {
            string evidenceId = BuildEvidenceId(choiceInputHash, authoredEvent.EventKey);
            JObject publishedEvent = publicEvents
                .OfType<JObject>()
                .SingleOrDefault(entry => string.Equals(
                    entry["evidenceId"]?.Value<string>(),
                    evidenceId,
                    StringComparison.Ordinal));
            if (publishedEvent == null
                || !string.Equals(
                    publishedEvent["actorId"]?.Value<string>(),
                    authoredEvent.ActorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    publishedEvent["targetId"]?.Value<string>(),
                    value.SubjectPersistentId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    publishedEvent["eventType"]?.Value<string>(),
                    authoredEvent.EventId,
                    StringComparison.Ordinal)
                || publishedEvent["day"]?.Value<int>() != authoredEvent.Day
                || publishedEvent["count"]?.Value<int>() != authoredEvent.Count
                || publishedEvent["generation"]?.Value<int>() != value.Generation)
            {
                throw Fail(
                    "controlled-equipment-public-event-mismatch",
                    $"Production public projection changed actor, target, day, count, generation "
                    + $"or evidence ID for '{value.CaseId}' event '{authoredEvent.EventKey}'.");
            }
            string factId = publishedEvent["factId"]?.Value<string>() ?? string.Empty;
            JObject publishedFact = publicFacts.OfType<JObject>().SingleOrDefault(fact =>
                string.Equals(
                    fact["factId"]?.Value<string>(),
                    factId,
                    StringComparison.Ordinal));
            string expectedValue = authoredEvent.DecimalValue.ToString(
                CultureInfo.InvariantCulture);
            if (publishedFact == null
                || !string.Equals(
                    publishedFact["domain"]?.Value<string>(),
                    authoredEvent.Domain,
                    StringComparison.Ordinal)
                || !string.Equals(
                    publishedFact["text"]?.Value<string>(),
                    authoredEvent.Text,
                    StringComparison.Ordinal)
                || !string.Equals(
                    publishedFact["outcome"]?.Value<string>(),
                    authoredEvent.Outcome,
                    StringComparison.Ordinal)
                || !string.Equals(
                    publishedFact["subjectId"]?.Value<string>(),
                    value.SubjectPersistentId,
                    StringComparison.Ordinal)
                || publishedFact["lastDay"]?.Value<int>() != authoredEvent.Day
                || publishedFact["count"]?.Value<int>() != authoredEvent.Count
                || !string.Equals(
                    publishedFact["totalValueDecimal"]?.Value<string>(),
                    expectedValue,
                    StringComparison.Ordinal))
            {
                throw Fail(
                    "controlled-equipment-public-fact-mismatch",
                    $"Production public projection changed authored fact fields for "
                    + $"'{value.CaseId}' event '{authoredEvent.EventKey}'.");
            }
        }

        foreach (KeyValuePair<string, string> participant in value.Events
                     .Select(entry => new KeyValuePair<string, string>(
                         entry.ActorId,
                         entry.ActorName))
                     .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            bool found = entities.OfType<JObject>().Any(entity =>
                string.Equals(
                    entity["entityId"]?.Value<string>(),
                    participant.Key,
                    StringComparison.Ordinal)
                && string.Equals(
                    entity["displayName"]?.Value<string>(),
                    participant.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    entity["kind"]?.Value<string>(),
                    "character",
                    StringComparison.Ordinal));
            if (!found)
            {
                throw Fail(
                    "controlled-equipment-public-participant-missing",
                    $"Production public projection omitted participant '{participant.Key}' "
                    + $"from case '{value.CaseId}'.");
            }
        }
    }

    private static JObject BuildAuthorityContext(
        ControlledCase value,
        JObject choiceProjection,
        string choiceInputHash,
        UsageLedger ledger,
        IReadOnlyList<string> candidateIds,
        EvolutionNarrativeRequestSnapshot lockedRequest,
        int effectBudget,
        string historyHash)
    {
        JObject authority = new JObject
        {
            ["captureKind"] = "authored-controlled-equipment",
            ["choiceInputHash"] = choiceInputHash,
            ["choiceInputHashProjection"] = new JObject
            {
                ["algorithm"] = ChoiceHashAlgorithm,
                ["canonicalization"] = ChoiceHashCanonicalization,
                ["projectionSchemaVersion"] = 1,
                ["value"] = choiceProjection.DeepClone()
            },
            ["effectBudget"] = effectBudget,
            ["effectBudgetSource"] = "EquipmentEvolutionState.ResonanceBudget",
            ["historyHash"] = historyHash,
            ["historyIdSource"] = "UsageLedgerCompactor.ComputeHistoryHash",
            ["historySlice"] = new JObject
            {
                ["mode"] = CurrentHistorySlice,
                ["priorHistoryRequested"] = false,
                ["priorHistorySegmentCount"] = ledger.compactedSegments?.Count ?? 0,
                ["supportsPriorHistoryInput"] = false
            },
            ["ledgerEvents"] = BuildLedgerAudit(value, ledger, choiceInputHash),
            ["legalCandidateEffectIds"] = new JArray(candidateIds),
            ["lockedRequest"] = BuildLockedRequest(lockedRequest),
            ["owner"] = new JObject
            {
                ["ownerId"] = value.OwnerId,
                ["ownerName"] = value.OwnerName
            },
            ["productionAuthorities"] = new JArray
            {
                "UsageLedgerCompactor.Record",
                "UsageLedgerCompactor.ComputeHistoryHash",
                "EquipmentEvolutionRules.BuildLegalHistoricalEffectCandidates",
                "EquipmentHistoricalEffectCatalog.Require",
                "EvolutionNarrativeRequestFactory.Create",
                "NarrativePublicContextFactory.Build",
                "NarrativeMechanicScenarioPublicContextSerializer.Serialize"
            },
            ["publicationOrigin"] = "authored-publication-not-natural-play"
        };
        if (string.Equals(
            value.ProfileId,
            EvolutionHistoryProfile,
            StringComparison.Ordinal))
        {
            authority["choiceBindingVerified"] = true;
            authority["selectedIndex"] = value.SelectedIndex;
            authority["selectedEffectId"] = candidateIds[value.SelectedIndex];
        }
        return authority;
    }

    private static JArray BuildLedgerAudit(
        ControlledCase value,
        UsageLedger ledger,
        string choiceInputHash)
    {
        Dictionary<string, ControlledEvent> authoredByEvidence = value.Events
            .ToDictionary(
                entry => BuildEvidenceId(choiceInputHash, entry.EventKey),
                entry => entry,
                StringComparer.Ordinal);
        JArray result = new JArray();
        foreach (UsageLedgerEvent entry in (ledger.currentGenerationEvents
                     ?? new List<UsageLedgerEvent>())
                 .Where(item => item != null)
                 .OrderBy(item => item.sequence))
        {
            if (!authoredByEvidence.TryGetValue(entry.evidenceId, out ControlledEvent authored))
            {
                throw Fail(
                    "controlled-equipment-ledger-audit-orphan",
                    $"Case '{value.CaseId}' ledger contains an unauthored evidence ID "
                    + $"'{entry.evidenceId}'.");
            }
            result.Add(new JObject
            {
                ["actorId"] = entry.actorId,
                ["actorName"] = authored.ActorName,
                ["authoredValueDecimal"] = authored.ValueDecimal,
                ["count"] = entry.repeatCount,
                ["day"] = authored.Day,
                ["domain"] = authored.Domain,
                ["eventKey"] = authored.EventKey,
                ["eventId"] = entry.eventId,
                ["evidenceId"] = entry.evidenceId,
                ["historicalEvidenceKind"] = entry.historicalEvidenceKind.ToString(),
                ["ledgerValueBinary32"] = entry.amount.ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                ["outcome"] = entry.outcomeId,
                ["sequence"] = entry.sequence,
                ["sourceTags"] = new JArray(entry.sourceTags ?? new List<string>()),
                ["targetId"] = entry.targetId,
                ["text"] = authored.Text,
                ["generation"] = entry.generation
            });
        }
        return result;
    }

    private static JObject BuildLockedRequest(
        EvolutionNarrativeRequestSnapshot request)
    {
        if (request == null)
        {
            throw Fail(
                "controlled-equipment-locked-request-null",
                "Production evolution narrative request cannot be null.");
        }
        return new JObject
        {
            ["effectBudget"] = request.effectBudget,
            ["effectId"] = request.effectId ?? string.Empty,
            ["evidenceIds"] = new JArray(request.evidenceIds ?? new List<string>()),
            ["generation"] = request.generation,
            ["historyHash"] = request.historyHash ?? string.Empty,
            ["legalCandidateEffectIds"] = new JArray(
                request.legalCandidateEffectIds ?? new List<string>()),
            ["nodeId"] = request.nodeId ?? string.Empty,
            ["parentNodeId"] = request.parentNodeId ?? string.Empty,
            ["participantIds"] = new JArray(
                request.participantIds ?? new List<string>()),
            ["requestKey"] = request.requestKey ?? string.Empty,
            ["sourceTags"] = new JArray(request.sourceTags ?? new List<string>()),
            ["targetKind"] = request.targetKind.ToString(),
            ["targetPersistentId"] = request.targetPersistentId ?? string.Empty
        };
    }

    private static JArray BuildCandidateDefinitions(
        IEnumerable<string> candidateIds)
    {
        JArray result = new JArray();
        int index = 0;
        foreach (string candidateId in candidateIds ?? Array.Empty<string>())
        {
            EquipmentHistoricalEffectDefinition definition =
                EquipmentHistoricalEffectCatalog.Require(candidateId);
            result.Add(new JObject
            {
                ["candidateIndex"] = index,
                ["description"] = definition.Description,
                ["displayName"] = definition.DisplayName,
                ["effectId"] = definition.EffectId
            });
            index++;
        }
        return result;
    }

    private static void RequireLegalCandidatePacket(
        string caseId,
        IReadOnlyList<string> candidateIds)
    {
        int count = candidateIds?.Count ?? 0;
        if (count <= 1)
        {
            throw Fail(
                "controlled-equipment-choice-not-model-selectable",
                $"Case '{caseId}' produced {count} legal equipment history candidate(s). "
                + "Zero/one-candidate cases are visible non-model rejections.");
        }
        if (count > 3)
        {
            throw Fail(
                "controlled-equipment-candidate-count-unsupported",
                $"Case '{caseId}' produced {count} candidates; the production request "
                + "supports at most three without truncation.");
        }
        if (candidateIds.Any(string.IsNullOrWhiteSpace)
            || candidateIds.Distinct(StringComparer.Ordinal).Count() != count)
        {
            throw Fail(
                "controlled-equipment-candidate-packet-invalid",
                $"Case '{caseId}' produced blank or duplicate legal candidate IDs.");
        }
        foreach (string candidateId in candidateIds)
        {
            EquipmentHistoricalEffectDefinition definition =
                EquipmentHistoricalEffectCatalog.Require(candidateId);
            if (!string.Equals(
                    definition.EffectId,
                    candidateId,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(definition.DisplayName)
                || string.IsNullOrWhiteSpace(definition.Description))
            {
                throw Fail(
                    "controlled-equipment-candidate-definition-invalid",
                    $"Historical effect catalog definition '{candidateId}' is incomplete.");
            }
        }
        _ = EquipmentChoiceGrammarCatalog.Require(count);
    }

    private static JObject BuildChoiceInputProjection(ControlledCase value)
    {
        JArray events = new JArray();
        foreach (ControlledEvent authoredEvent in value.Events)
        {
            events.Add(new JObject
            {
                ["actorId"] = authoredEvent.ActorId,
                ["actorName"] = authoredEvent.ActorName,
                ["count"] = authoredEvent.Count,
                ["day"] = authoredEvent.Day,
                ["domain"] = authoredEvent.Domain,
                ["eventKey"] = authoredEvent.EventKey,
                ["eventId"] = authoredEvent.EventId,
                ["historicalEvidenceKind"] = authoredEvent.EvidenceKindText,
                ["outcome"] = authoredEvent.Outcome,
                ["sourceTags"] = new JArray(authoredEvent.SourceTags),
                ["text"] = authoredEvent.Text,
                ["valueDecimal"] = authoredEvent.ValueDecimal
            });
        }
        return new JObject
        {
            ["events"] = events,
            ["generation"] = value.Generation,
            ["ownerId"] = value.OwnerId,
            ["ownerName"] = value.OwnerName,
            ["schemaVersion"] = 1,
            ["subject"] = new JObject
            {
                ["background"] = value.SubjectBackground,
                ["displayName"] = value.SubjectDisplayName,
                ["persistentId"] = value.SubjectPersistentId
            }
        };
    }

    private static string BuildScenarioId(ControlledCase value) =>
        string.Equals(value.ProfileId, EquipmentChoiceProfile, StringComparison.Ordinal)
            ? "controlled-equipment-choice:" + value.CaseId
            : "controlled-equipment-history:" + value.CaseId;

    private static string BuildEvidenceId(
        string choiceInputHash,
        string eventKey) =>
        "controlled-equipment-evidence:"
        + ComputeSha256Hex(choiceInputHash + "|" + eventKey);

    private static string ComputeCanonicalSha256(JToken value)
    {
        JToken canonical = SortObjectKeys(value);
        string json = canonical.ToString(Formatting.None);
        return ChoiceHashAlgorithm + ":" + ComputeSha256Hex(json);
    }

    private static string ComputeSha256Hex(string text)
    {
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(text ?? string.Empty);
        using SHA256 sha256 = SHA256.Create();
        byte[] digest = sha256.ComputeHash(bytes);
        StringBuilder result = new StringBuilder(digest.Length * 2);
        foreach (byte value in digest)
        {
            result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }
        return result.ToString();
    }

    private static JToken SortObjectKeys(JToken value)
    {
        if (value == null)
        {
            throw Fail(
                "controlled-equipment-canonical-null",
                "Canonical choice projection cannot contain null tokens.");
        }
        if (value is JObject objectValue)
        {
            JObject result = new JObject();
            foreach (JProperty property in objectValue.Properties()
                         .OrderBy(entry => entry.Name, StringComparer.Ordinal))
            {
                result.Add(property.Name, SortObjectKeys(property.Value));
            }
            return result;
        }
        if (value is JArray arrayValue)
        {
            return new JArray(arrayValue.Select(SortObjectKeys));
        }
        if (value.Type != JTokenType.String
            && value.Type != JTokenType.Integer
            && value.Type != JTokenType.Boolean)
        {
            throw Fail(
                "controlled-equipment-canonical-token-unsupported",
                $"Choice projection contains unsupported JSON token '{value.Type}'.");
        }
        return value.DeepClone();
    }

    private static decimal ParseDecimal(
        string value,
        string caseId,
        string eventKey)
    {
        const NumberStyles style = NumberStyles.AllowLeadingSign
            | NumberStyles.AllowDecimalPoint;
        if (!decimal.TryParse(value, style, CultureInfo.InvariantCulture, out decimal result))
        {
            throw Fail(
                "controlled-equipment-value-decimal-invalid",
                $"Case '{caseId}' event '{eventKey}' valueDecimal '{value}' is not a "
                + "plain invariant decimal string.");
        }
        return result;
    }

    private static JObject RequireObject(
        JObject source,
        string propertyName,
        string location)
    {
        JToken token = source[propertyName];
        if (!(token is JObject result))
        {
            throw Fail(
                "controlled-equipment-object-required",
                $"{location}.{propertyName} must be an object.");
        }
        return result;
    }

    private static JArray RequireArray(
        JObject source,
        string propertyName,
        string location)
    {
        JToken token = source[propertyName];
        if (!(token is JArray result))
        {
            throw Fail(
                "controlled-equipment-array-required",
                $"{location}.{propertyName} must be an array.");
        }
        return result;
    }

    private static string RequireCanonicalString(
        JObject source,
        string propertyName,
        string location)
    {
        JToken token = source[propertyName];
        if (token?.Type != JTokenType.String)
        {
            throw Fail(
                "controlled-equipment-string-required",
                $"{location}.{propertyName} must be a string.");
        }
        return RequireCanonicalText(
            token.Value<string>(),
            $"{location}.{propertyName}");
    }

    private static string RequireCanonicalText(string value, string location)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw Fail(
                "controlled-equipment-text-empty",
                $"{location} must be a non-empty string.");
        }
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            throw Fail(
                "controlled-equipment-text-not-canonical",
                $"{location} must not contain leading or trailing whitespace.");
        }
        return normalized;
    }

    private static int RequireInt32(
        JObject source,
        string propertyName,
        string location,
        int minimum)
    {
        JToken token = source[propertyName];
        if (token?.Type != JTokenType.Integer)
        {
            throw Fail(
                "controlled-equipment-integer-required",
                $"{location}.{propertyName} must be an integer.");
        }
        long raw;
        try
        {
            raw = token.Value<long>();
        }
        catch (Exception error) when (
            error is OverflowException
            || error is InvalidCastException
            || error is FormatException)
        {
            throw Fail(
                "controlled-equipment-integer-out-of-range",
                $"{location}.{propertyName} is outside the Int64 range.");
        }
        if (raw < minimum || raw > int.MaxValue)
        {
            throw Fail(
                "controlled-equipment-integer-out-of-range",
                $"{location}.{propertyName} must be within {minimum}..{int.MaxValue}.");
        }
        return (int)raw;
    }

    private static void RequireExactFields(
        JObject value,
        IEnumerable<string> expectedFields,
        string location,
        string code,
        string additionalMessage = "")
    {
        HashSet<string> expected = new HashSet<string>(
            expectedFields ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .ToArray();
        string duplicate = actual
            .GroupBy(name => name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (!string.IsNullOrEmpty(duplicate))
        {
            throw Fail(
                code,
                $"{location} contains duplicate field '{duplicate}'.");
        }
        string[] unknown = actual
            .Where(name => !expected.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] missing = expected
            .Where(name => !value.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (unknown.Length > 0 || missing.Length > 0)
        {
            string exactFields = string.Join(
                ",",
                expected.OrderBy(name => name, StringComparer.Ordinal));
            throw Fail(
                code,
                $"{location} must contain exactly [{exactFields}]. "
                + $"Unknown=[{string.Join(",", unknown)}]; missing=[{string.Join(",", missing)}]."
                + additionalMessage);
        }
    }

    private static bool IsSha256Identifier(string value)
    {
        if (value == null
            || value.Length != 71
            || !value.StartsWith("sha256:", StringComparison.Ordinal))
        {
            return false;
        }
        for (int index = 7; index < value.Length; index++)
        {
            char current = value[index];
            if (!((current >= '0' && current <= '9')
                || (current >= 'a' && current <= 'f')))
            {
                return false;
            }
        }
        return true;
    }

    private static JObject ParseObject(string json, string code)
    {
        try
        {
            return JObject.Parse(json);
        }
        catch (JsonException error)
        {
            throw Fail(code, "Existing canonical public object could not be parsed: " + error.Message);
        }
    }

    private static JArray ParseArray(string json, string code)
    {
        try
        {
            return JArray.Parse(json);
        }
        catch (JsonException error)
        {
            throw Fail(code, "Existing canonical public array could not be parsed: " + error.Message);
        }
    }

    private static NarrativeMechanicCatalogUnsupportedSourceException Fail(
        string code,
        string message) =>
        new NarrativeMechanicCatalogUnsupportedSourceException(code, message);

    private sealed class ControlledCase
    {
        public ControlledCase(
            string caseId,
            string scenarioFamilyId,
            string profileId,
            string subjectPersistentId,
            string subjectDisplayName,
            string subjectBackground,
            IReadOnlyList<ControlledEvent> events,
            int generation,
            string ownerId,
            string ownerName,
            int selectedIndex,
            string choiceInputHash)
        {
            CaseId = caseId;
            ScenarioFamilyId = scenarioFamilyId;
            ProfileId = profileId;
            SubjectPersistentId = subjectPersistentId;
            SubjectDisplayName = subjectDisplayName;
            SubjectBackground = subjectBackground;
            Events = events;
            Generation = generation;
            OwnerId = ownerId;
            OwnerName = ownerName;
            SelectedIndex = selectedIndex;
            ChoiceInputHash = choiceInputHash;
        }

        public string CaseId { get; }
        public string ScenarioFamilyId { get; }
        public string ProfileId { get; }
        public string SubjectPersistentId { get; }
        public string SubjectDisplayName { get; }
        public string SubjectBackground { get; }
        public IReadOnlyList<ControlledEvent> Events { get; }
        public int Generation { get; }
        public string OwnerId { get; }
        public string OwnerName { get; }
        public int SelectedIndex { get; }
        public string ChoiceInputHash { get; }
    }

    private sealed class ControlledEvent
    {
        public ControlledEvent(
            string eventKey,
            string eventId,
            string text,
            string domain,
            string outcome,
            int day,
            int count,
            string valueDecimal,
            decimal decimalValue,
            float ledgerValue,
            HistoricalEvidenceKind evidenceKind,
            string evidenceKindText,
            IReadOnlyList<string> sourceTags,
            string actorId,
            string actorName)
        {
            EventKey = eventKey;
            EventId = eventId;
            Text = text;
            Domain = domain;
            Outcome = outcome;
            Day = day;
            Count = count;
            ValueDecimal = valueDecimal;
            DecimalValue = decimalValue;
            LedgerValue = ledgerValue;
            EvidenceKind = evidenceKind;
            EvidenceKindText = evidenceKindText;
            SourceTags = sourceTags;
            ActorId = actorId;
            ActorName = actorName;
        }

        public string EventKey { get; }
        public string EventId { get; }
        public string Text { get; }
        public string Domain { get; }
        public string Outcome { get; }
        public int Day { get; }
        public int Count { get; }
        public string ValueDecimal { get; }
        public decimal DecimalValue { get; }
        public float LedgerValue { get; }
        public HistoricalEvidenceKind EvidenceKind { get; }
        public string EvidenceKindText { get; }
        public IReadOnlyList<string> SourceTags { get; }
        public string ActorId { get; }
        public string ActorName { get; }
    }
}
#endif
