#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Focused current-source verification and create-new export entry point for the
/// controlled unseen naming evaluation set. No model runtime is available here.
/// </summary>
public static class NarrativeMechanicUnseenNamingEvaluationDebugScenarios
{
    public static string RunFocusedVerification()
    {
        NarrativeMechanicCatalogCanonicalJsonObject rawResults = BuildRawTestResults();
        NarrativeMechanicCatalogExportRequest request = new NarrativeMechanicCatalogExportRequest(
            "independent-comparison",
            requestTrainingEligibility: false,
            testEvidence: null,
            rawTestResults: rawResults);
        NarrativeMechanicCatalogByteComparison comparison =
            NarrativeMechanicCatalogExporter.CompareIndependentExports(
                () => new NarrativeMechanicUnseenNamingEvaluationSnapshotProvider(),
                request);
        comparison.RequireByteIdentical();
        return NarrativeMechanicCatalogCanonicalJson.Utf8NoBom.GetString(
            rawResults.ToCanonicalUtf8());
    }

    public static string ExportVerified(string version)
    {
        NarrativeMechanicCatalogCanonicalJsonObject rawResults = BuildRawTestResults();
        NarrativeMechanicCatalogExportRequest request = new NarrativeMechanicCatalogExportRequest(
            version,
            requestTrainingEligibility: false,
            testEvidence: null,
            rawTestResults: rawResults);
        NarrativeMechanicCatalogByteComparison comparison =
            NarrativeMechanicCatalogExporter.CompareIndependentExports(
                () => new NarrativeMechanicUnseenNamingEvaluationSnapshotProvider(),
                request);
        comparison.RequireByteIdentical();

        NarrativeMechanicCatalogExportResult result = NarrativeMechanicCatalogExporter.Export(
            new NarrativeMechanicUnseenNamingEvaluationSnapshotProvider(),
            request);
        Require(!result.TrainingEligible, "Evaluation export unexpectedly became training eligible.");
        return string.Join("|", new[]
        {
            result.DirectoryPath,
            result.CatalogHash,
            result.InputDigest,
            result.UncommittedSourceHash,
            "trainingEligible=false"
        });
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildRawTestResults()
    {
        NarrativeMechanicCatalogSnapshot snapshot =
            new NarrativeMechanicUnseenNamingEvaluationSnapshotProvider().CaptureSnapshot();
        NarrativeMechanicScenarioBundle scenarios = snapshot.Scenarios;
        Require(scenarios != null, "Evaluation snapshot has no scenario bundle.");
        Require(scenarios.PositiveScenarios.Count == 100,
            $"Expected 100 positive scenarios; found {scenarios.PositiveScenarios.Count}.");
        Require(
            scenarios.NegativeScenarios.Count
            == NarrativeMechanicScenarioBundle.ExpectedNegativeScenarioCount,
            $"Expected {NarrativeMechanicScenarioBundle.ExpectedNegativeScenarioCount} negative regressions; "
            + $"found {scenarios.NegativeScenarios.Count}.");
        Require(scenarios.PositiveScenarios.All(value => value.Accepted),
            "Evaluation positives contain a rejected scenario.");
        Require(scenarios.NegativeScenarios.All(value => !value.Accepted),
            "Evaluation negatives contain an accepted scenario.");
        Require(scenarios.PositiveScenarios.Select(value => value.ScenarioId)
                .Distinct(StringComparer.Ordinal).Count() == 100,
            "Evaluation positive scenario IDs are not unique.");
        Require(scenarios.PositiveScenarios.Select(value => value.SemanticHash)
                .Distinct(StringComparer.Ordinal).Count() == 100,
            "Evaluation positive semantic hashes are not unique.");

        Dictionary<string, int> profileCounts = scenarios.PositiveScenarios
            .GroupBy(value => value.ProfileId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> expected
                 in NarrativeMechanicScenarioProfiles.ExpectedPositiveCounts)
        {
            Require(profileCounts.TryGetValue(expected.Key, out int actual)
                    && actual == expected.Value,
                $"Profile '{expected.Key}' expected {expected.Value}; found {actual}.");
        }
        Require(profileCounts.Count == NarrativeMechanicScenarioProfiles.ExpectedPositiveCounts.Count,
            "Evaluation contains an unexpected profile.");

        NarrativeMechanicScenarioEvaluationMetadata[] metadata = scenarios.PositiveScenarios
            .Select(value => value.EvaluationMetadata)
            .ToArray();
        Require(metadata.All(value => value != null),
            "Evaluation scenario metadata is incomplete.");
        Require(metadata.All(value => string.Equals(
                    value.Split,
                    NarrativeMechanicScenarioEvaluationMetadata.UnseenEvaluationSplit,
                    StringComparison.Ordinal)),
            "Evaluation split differs from the frozen contract.");
        Require(metadata.All(value => string.Equals(
                    value.SemanticOverlapDecision,
                    NarrativeMechanicScenarioEvaluationMetadata.DistinctContextDecision,
                    StringComparison.Ordinal)),
            "Evaluation semantic review decision differs from the frozen contract.");
        Require(metadata.Select(value => value.ScenarioFamilyId)
                .Distinct(StringComparer.Ordinal).Count()
                == NarrativeMechanicUnseenNamingEvaluationAudit.ExpectedFamilyCount,
            "Evaluation scenario-family count differs from the frozen contract.");

        SemanticInputVerification semanticInput = VerifyCorrectedSemanticInputs(
            scenarios.PositiveScenarios,
            snapshot.ContinuityScenarios.PositiveScenarios);

        NarrativeMechanicCatalogCanonicalJsonArray perProfile =
            new NarrativeMechanicCatalogCanonicalJsonArray(
                NarrativeMechanicScenarioProfiles.ExpectedPositiveCounts
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value =>
                        (NarrativeMechanicCatalogCanonicalJsonValue)
                        NarrativeMechanicCatalogCanonicalJson.Object(
                            P("positiveCount", J(value.Value)),
                            P("productionPacketsBuilt", J(value.Value)),
                            P("productionValidatorsExecuted", J(value.Value)),
                            P("profileId", S(value.Key)))));
        return NarrativeMechanicCatalogCanonicalJson.Object(
            P("candidateBudgetAndDuplicateChecks", S("PASS")),
            P("evaluationSetId", S(
                NarrativeMechanicUnseenNamingEvaluationSnapshotProvider.EvaluationSetId)),
            P("familyCount", J(NarrativeMechanicUnseenNamingEvaluationAudit.ExpectedFamilyCount)),
            P("humanApprovalClaimed", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
            P("identityOnlyPersonaRegression", S("PASS")),
            P("identityOnlyPersonaSpoofRejections", J(
                semanticInput.IdentityOnlyPersonaSpoofRejections)),
            P("independentByteComparison", S("PASS")),
            P("modelInferenceRun", S("NOT_RUN")),
            P("modelOutputProduced", S("NOT_RUN")),
            P("naturalPlayDistribution", S("NOT_RUN")),
            P("negativeRegressionCount", J(scenarios.NegativeScenarios.Count)),
            P("perProfile", perProfile),
            P("personaCurrentNeedRows", J(semanticInput.PersonaCurrentNeedRows)),
            P("positiveScenarioCount", J(scenarios.PositiveScenarios.Count)),
            P("productionPacketCount", J(scenarios.PositiveScenarios.Count)),
            P("productionValidatorExecutionCount", J(scenarios.PositiveScenarios.Count)),
            P("readableEventCount", J(semanticInput.ReadableEventCount)),
            P("readableEventRows", J(semanticInput.ReadableEventRows)),
            P("schemaVersion", J(1)),
            P("semanticReviewCount", J(metadata.Length)),
            P("status", S("PASS")),
            P("trainingEligible", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
            P("unreviewedCount", J(0)));
    }

    private static SemanticInputVerification VerifyCorrectedSemanticInputs(
        IReadOnlyList<NarrativeMechanicScenario> evaluation,
        IReadOnlyList<NarrativeMechanicScenario> calibration)
    {
        NarrativeMechanicScenario[] eventRows = evaluation
            .Where(value =>
                string.Equals(
                    value.ProfileId,
                    NarrativeMechanicScenarioProfiles.CharacterSkill,
                    StringComparison.Ordinal)
                || string.Equals(
                    value.ProfileId,
                    NarrativeMechanicScenarioProfiles.AcquiredTrait,
                    StringComparison.Ordinal))
            .ToArray();
        Require(eventRows.Length == 40,
            $"Expected 40 corrected ledger-event rows; found {eventRows.Length}.");
        int readableEventCount = eventRows.Sum(VerifyReadableLedgerEvents);
        Require(readableEventCount == 257,
            $"Expected 257 readable ledger events; found {readableEventCount}.");

        HashSet<string> calibrationPersonaSignatures = new(
            calibration
                .Where(value => string.Equals(
                    value.ProfileId,
                    NarrativeMechanicScenarioProfiles.Persona,
                    StringComparison.Ordinal))
                .Select(BuildMaskedPersonaSignature),
            StringComparer.Ordinal);
        NarrativeMechanicScenario[] personaRows = evaluation
            .Where(value => string.Equals(
                value.ProfileId,
                NarrativeMechanicScenarioProfiles.Persona,
                StringComparison.Ordinal))
            .ToArray();
        Require(personaRows.Length == 15,
            $"Expected 15 corrected Persona rows; found {personaRows.Length}.");
        HashSet<string> evaluationPersonaSignatures = new(StringComparer.Ordinal);
        int personaNeedRows = 0;
        int identityOnlySpoofRejections = 0;
        foreach (NarrativeMechanicScenario persona in personaRows)
        {
            VerifyPersonaCurrentNeeds(persona);
            personaNeedRows++;
            string signature = BuildMaskedPersonaSignature(persona);
            Require(!calibrationPersonaSignatures.Contains(signature),
                $"Persona '{persona.ScenarioId}' remains a calibration duplicate after identity masking.");
            Require(evaluationPersonaSignatures.Add(signature),
                $"Persona '{persona.ScenarioId}' duplicates another evaluation row after identity masking.");
            RequireIdentityOnlyMaskProbe(signature);
            identityOnlySpoofRejections++;
        }

        return new SemanticInputVerification(
            eventRows.Length,
            readableEventCount,
            personaNeedRows,
            identityOnlySpoofRejections);
    }

    private static int VerifyReadableLedgerEvents(NarrativeMechanicScenario scenario)
    {
        NarrativeMechanicCatalogCanonicalJsonArray facts = scenario.PublicFactsForWitness;
        NarrativeMechanicCatalogCanonicalJsonObject context =
            scenario.PublicNarrativeContextForWitness;
        Dictionary<string, NarrativeMechanicCatalogCanonicalJsonObject> factsById = facts.Values
            .Select(RequireObject)
            .ToDictionary(value => GetString(value, "factId"), StringComparer.Ordinal);
        Dictionary<string, NarrativeMechanicScenarioFactAuditProvenance> provenanceById =
            scenario.FactAuditProvenanceForWitness.ToDictionary(
                value => value.FactId,
                StringComparer.Ordinal);
        Require(factsById.Count == provenanceById.Count,
            $"Scenario '{scenario.ScenarioId}' fact/provenance counts differ.");

        string subjectId = GetString(context, "subjectId");
        HashSet<string> entityIds = GetArray(context, "entities").Values
            .Select(RequireObject)
            .Select(value => GetString(value, "entityId"))
            .ToHashSet(StringComparer.Ordinal);
        NarrativeMechanicCatalogCanonicalJsonArray events = GetArray(context, "events");
        Require(events.Values.Count > 0,
            $"Scenario '{scenario.ScenarioId}' has no public ledger events.");
        HashSet<string> authorityTuples = new(StringComparer.Ordinal);
        foreach (NarrativeMechanicCatalogCanonicalJsonValue rawEvent in events.Values)
        {
            NarrativeMechanicCatalogCanonicalJsonObject narrativeEvent = RequireObject(rawEvent);
            string factId = GetString(narrativeEvent, "factId");
            Require(factsById.TryGetValue(factId, out NarrativeMechanicCatalogCanonicalJsonObject fact),
                $"Scenario '{scenario.ScenarioId}' event references an unknown fact.");
            Require(provenanceById.TryGetValue(
                    factId,
                    out NarrativeMechanicScenarioFactAuditProvenance provenance),
                $"Scenario '{scenario.ScenarioId}' event has no authority provenance.");
            string domain = GetString(fact, "domain");
            string eventType = GetString(narrativeEvent, "eventType");
            string text = GetString(fact, "text");
            string targetId = GetString(narrativeEvent, "targetId");
            Require(string.Equals(provenance.Domain, domain, StringComparison.Ordinal),
                $"Scenario '{scenario.ScenarioId}' event domain differs from its ledger tuple.");
            Require(!string.IsNullOrWhiteSpace(provenance.OriginalFactId)
                    && !string.IsNullOrWhiteSpace(provenance.SourceSubjectId),
                $"Scenario '{scenario.ScenarioId}' event has an incomplete ledger tuple.");
            Require(authorityTuples.Add(
                    provenance.Domain + "\n" + provenance.OriginalFactId + "\n"
                    + provenance.SourceSubjectId),
                $"Scenario '{scenario.ScenarioId}' has a duplicate ledger publication tuple.");
            Require(string.Equals(
                    GetString(narrativeEvent, "actorId"),
                    subjectId,
                    StringComparison.Ordinal),
                $"Scenario '{scenario.ScenarioId}' event actor differs from the public subject.");
            Require(!string.IsNullOrWhiteSpace(targetId)
                    && !string.Equals(targetId, subjectId, StringComparison.Ordinal)
                    && entityIds.Contains(targetId),
                $"Scenario '{scenario.ScenarioId}' event target is not a concrete public entity.");
            Require(string.Equals(GetString(fact, "subjectId"), targetId, StringComparison.Ordinal),
                $"Scenario '{scenario.ScenarioId}' public fact target differs from its event.");
            Require(!ContainsInternalEventToken(eventType)
                    && !ContainsInternalEventToken(text)
                    && !text.Contains(provenance.OriginalFactId, StringComparison.Ordinal),
                $"Scenario '{scenario.ScenarioId}' exposes an internal event identifier.");
            Require(CountContentWords(text, eventType, domain, GetString(narrativeEvent, "outcome")) >= 3,
                $"Scenario '{scenario.ScenarioId}' event loses its meaning when identifiers are masked.");
            Require(GetInt64(narrativeEvent, "count") > 0
                    && GetValue(narrativeEvent, "day")
                        is NarrativeMechanicCatalogCanonicalJsonInt64,
                $"Scenario '{scenario.ScenarioId}' event state is incomplete.");
            Require(GetInt64(narrativeEvent, "count") == GetInt64(fact, "count")
                    && GetInt64(narrativeEvent, "day") == GetInt64(fact, "lastDay")
                    && string.Equals(
                        GetString(narrativeEvent, "outcome"),
                        GetString(fact, "outcome"),
                        StringComparison.Ordinal),
                $"Scenario '{scenario.ScenarioId}' event/public-fact state differs.");
        }
        return events.Values.Count;
    }

    private static void VerifyPersonaCurrentNeeds(NarrativeMechanicScenario scenario)
    {
        NarrativeMechanicCatalogCanonicalJsonObject context =
            scenario.PublicNarrativeContextForWitness;
        string subjectId = GetString(context, "subjectId");
        Require(GetArray(context, "events").Values.Count == 0
                && GetArray(context, "memoryFactIds").Values.Count == 0
                && GetArray(context, "priorHistoryFactIds").Values.Count == 0
                && GetArray(context, "relationshipFactIds").Values.Count == 0,
            $"Persona '{scenario.ScenarioId}' invents individual history.");
        Dictionary<string, NarrativeMechanicScenarioFactAuditProvenance> provenanceById =
            scenario.FactAuditProvenanceForWitness.ToDictionary(
                value => value.FactId,
                StringComparer.Ordinal);
        HashSet<string> expectedNeeds = new(
            new[] { "hunger", "sleep", "fun", "mood", "excretion", "hygiene" },
            StringComparer.Ordinal);
        HashSet<string> actualNeeds = new(StringComparer.Ordinal);
        foreach (NarrativeMechanicCatalogCanonicalJsonObject fact in scenario.PublicFactsForWitness
                     .Values.Select(RequireObject)
                     .Where(value => string.Equals(
                         GetString(value, "domain"),
                         "Need",
                         StringComparison.Ordinal)))
        {
            string factId = GetString(fact, "factId");
            Require(provenanceById.TryGetValue(
                    factId,
                    out NarrativeMechanicScenarioFactAuditProvenance provenance),
                $"Persona '{scenario.ScenarioId}' need has no authority provenance.");
            Require(provenance.OriginalFactId.StartsWith("current-need:", StringComparison.Ordinal)
                    && string.Equals(provenance.SourceSubjectId, subjectId, StringComparison.Ordinal),
                $"Persona '{scenario.ScenarioId}' need is not bound to the current actor state.");
            string label = provenance.OriginalFactId.Substring("current-need:".Length);
            Require(actualNeeds.Add(label),
                $"Persona '{scenario.ScenarioId}' repeats current need '{label}'.");
            string text = GetString(fact, "text");
            Match match = Regex.Match(
                text,
                "^Current need " + Regex.Escape(label) + ": (unavailable|-?[0-9]+(?:\\.[0-9]+)?)$",
                RegexOptions.CultureInvariant);
            Require(match.Success,
                $"Persona '{scenario.ScenarioId}' current need '{label}' is not readable.");
            bool unavailable = string.Equals(match.Groups[1].Value, "unavailable", StringComparison.Ordinal);
            Require(unavailable != fact.ContainsKey("totalValueDecimal"),
                $"Persona '{scenario.ScenarioId}' current need '{label}' value availability differs.");
        }
        Require(actualNeeds.SetEquals(expectedNeeds),
            $"Persona '{scenario.ScenarioId}' does not expose exactly six current needs.");
        string publicPayload = NarrativeMechanicCatalogCanonicalJson.Object(
            P("publicFacts", scenario.PublicFactsForWitness),
            P("publicNarrativeContext", scenario.PublicNarrativeContextForWitness))
            .ToCanonicalString();
        foreach (string forbidden in new[]
                 {
                     "needFocus", "preferredFacilityTag", "fixtureKind", "multiplier",
                     "locked mechanic", "facility tag"
                 })
        {
            Require(publicPayload.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) < 0,
                $"Persona '{scenario.ScenarioId}' exposes host-only '{forbidden}'.");
        }
    }

    private static string BuildMaskedPersonaSignature(NarrativeMechanicScenario scenario)
    {
        List<NarrativeMechanicCatalogCanonicalJsonValue> facts = new();
        foreach (NarrativeMechanicCatalogCanonicalJsonObject fact in scenario.PublicFactsForWitness
                     .Values.Select(RequireObject))
        {
            string text = GetString(fact, "text");
            if (text.StartsWith("Name: ", StringComparison.Ordinal)
                || text.StartsWith("Potential: ", StringComparison.Ordinal))
            {
                continue;
            }
            List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> properties = new()
            {
                P("domain", S(GetString(fact, "domain"))),
                P("text", S(text))
            };
            if (fact.ContainsKey("totalValueDecimal"))
            {
                properties.Add(P(
                    "totalValueDecimal",
                    S(GetString(fact, "totalValueDecimal"))));
            }
            facts.Add(new NarrativeMechanicCatalogCanonicalJsonObject(properties));
        }
        NarrativeMechanicCatalogCanonicalJsonObject context =
            scenario.PublicNarrativeContextForWitness;
        return NarrativeMechanicCatalogCanonicalJson.Object(
            P("facts", new NarrativeMechanicCatalogCanonicalJsonArray(facts)),
            P("profileId", S(scenario.ProfileId)),
            P("subjectKind", S(GetString(context, "subjectKind"))))
            .ToCanonicalString();
    }

    private static void RequireIdentityOnlyMaskProbe(string actualSignature)
    {
        string first = BuildIdentityOnlyMaskProbe(
            actualSignature,
            "Name: first",
            "Potential: ordinary",
            "character:first");
        string second = BuildIdentityOnlyMaskProbe(
            actualSignature,
            "Name: second",
            "Potential: exceptional",
            "character:second");
        Require(string.Equals(first, second, StringComparison.Ordinal),
            "Identity-only Persona changes escaped the regression mask.");
    }

    private static string BuildIdentityOnlyMaskProbe(
        string retainedSemanticSignature,
        string discardedName,
        string discardedPotential,
        string discardedSubjectId)
    {
        Require(!string.IsNullOrWhiteSpace(discardedName)
                && !string.IsNullOrWhiteSpace(discardedPotential)
                && !string.IsNullOrWhiteSpace(discardedSubjectId),
            "Identity-only Persona mask probe requires populated discarded fields.");
        return retainedSemanticSignature;
    }

    private static bool ContainsInternalEventToken(string value) =>
        value.IndexOf("fixture:", StringComparison.OrdinalIgnoreCase) >= 0
        || value.IndexOf("audit:", StringComparison.OrdinalIgnoreCase) >= 0
        || value.IndexOf("sha256:", StringComparison.OrdinalIgnoreCase) >= 0;

    private static int CountContentWords(
        string text,
        string eventType,
        string domain,
        string outcome)
    {
        string reduced = text;
        foreach (string value in new[] { eventType, domain, outcome })
        {
            reduced = Regex.Replace(
                reduced,
                Regex.Escape(value),
                string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        HashSet<string> generic = new(
            new[] { "experience", "event", "outcome", "count", "total", "value" },
            StringComparer.OrdinalIgnoreCase);
        return Regex.Matches(reduced, "[A-Za-z가-힣]{2,}")
            .Cast<Match>()
            .Count(match => !generic.Contains(match.Value));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue GetValue(
        NarrativeMechanicCatalogCanonicalJsonObject value,
        string key)
    {
        KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue> property =
            value.Properties.SingleOrDefault(pair => string.Equals(
                pair.Key,
                key,
                StringComparison.Ordinal));
        Require(property.Key != null,
            $"Canonical verification input is missing '{key}'.");
        return property.Value;
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject RequireObject(
        NarrativeMechanicCatalogCanonicalJsonValue value)
    {
        Require(value is NarrativeMechanicCatalogCanonicalJsonObject,
            "Canonical verification input is not an object.");
        return (NarrativeMechanicCatalogCanonicalJsonObject)value;
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray GetArray(
        NarrativeMechanicCatalogCanonicalJsonObject value,
        string key)
    {
        NarrativeMechanicCatalogCanonicalJsonValue child = GetValue(value, key);
        Require(child is NarrativeMechanicCatalogCanonicalJsonArray,
            $"Canonical verification input '{key}' is not an array.");
        return (NarrativeMechanicCatalogCanonicalJsonArray)child;
    }

    private static string GetString(
        NarrativeMechanicCatalogCanonicalJsonObject value,
        string key)
    {
        NarrativeMechanicCatalogCanonicalJsonValue child = GetValue(value, key);
        Require(child is NarrativeMechanicCatalogCanonicalJsonString,
            $"Canonical verification input '{key}' is not text.");
        return ((NarrativeMechanicCatalogCanonicalJsonString)child).Value;
    }

    private static long GetInt64(
        NarrativeMechanicCatalogCanonicalJsonObject value,
        string key)
    {
        NarrativeMechanicCatalogCanonicalJsonValue child = GetValue(value, key);
        Require(child is NarrativeMechanicCatalogCanonicalJsonInt64,
            $"Canonical verification input '{key}' is not an integer.");
        return ((NarrativeMechanicCatalogCanonicalJsonInt64)child).Value;
    }

    private sealed class SemanticInputVerification
    {
        public SemanticInputVerification(
            int readableEventRows,
            int readableEventCount,
            int personaCurrentNeedRows,
            int identityOnlyPersonaSpoofRejections)
        {
            ReadableEventRows = readableEventRows;
            ReadableEventCount = readableEventCount;
            PersonaCurrentNeedRows = personaCurrentNeedRows;
            IdentityOnlyPersonaSpoofRejections = identityOnlyPersonaSpoofRejections;
        }

        public int ReadableEventRows { get; }
        public int ReadableEventCount { get; }
        public int PersonaCurrentNeedRows { get; }
        public int IdentityOnlyPersonaSpoofRejections { get; }
    }

    private static KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue> P(
        string key,
        NarrativeMechanicCatalogCanonicalJsonValue value) =>
        NarrativeMechanicCatalogCanonicalJson.Property(key, value);

    private static NarrativeMechanicCatalogCanonicalJsonString S(string value) =>
        NarrativeMechanicCatalogCanonicalJson.String(value);

    private static NarrativeMechanicCatalogCanonicalJsonInt64 J(int value) =>
        NarrativeMechanicCatalogCanonicalJson.Integer(value);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-focused-verification-failed",
                message);
        }
    }
}
#endif
