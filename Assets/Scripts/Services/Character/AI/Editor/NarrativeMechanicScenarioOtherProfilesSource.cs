#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal sealed class LedgerRow
{
    public LedgerRow(string id, HistoricalEvidenceKind firstEvidence,
        HistoricalEvidenceKind secondEvidence, string firstEventId,
        string secondEventId, string firstTag, string secondTag)
    {
        Id = id;
        FirstEvidence = firstEvidence;
        SecondEvidence = secondEvidence;
        FirstEventId = firstEventId;
        SecondEventId = secondEventId;
        FirstTag = firstTag;
        SecondTag = secondTag;
    }

    public LedgerRow(
        string id,
        HistoricalEvidenceKind firstEvidence,
        HistoricalEvidenceKind secondEvidence,
        HistoricalEvidenceKind thirdEvidence,
        string firstEventId,
        string secondEventId,
        string thirdEventId,
        string firstTag,
        string secondTag,
        string thirdTag,
        string nearestCalibrationRowId,
        string evaluationReason)
        : this(
            id,
            firstEvidence,
            secondEvidence,
            firstEventId,
            secondEventId,
            firstTag,
            secondTag)
    {
        ThirdEvidence = thirdEvidence;
        ThirdEventId = thirdEventId;
        ThirdTag = thirdTag;
        NearestCalibrationRowId = nearestCalibrationRowId;
        EvaluationReason = evaluationReason;
    }

    public string Id { get; }
    public HistoricalEvidenceKind FirstEvidence { get; }
    public HistoricalEvidenceKind SecondEvidence { get; }
    public string FirstEventId { get; }
    public string SecondEventId { get; }
    public string FirstTag { get; }
    public string SecondTag { get; }
    public HistoricalEvidenceKind ThirdEvidence { get; }
    public string ThirdEventId { get; }
    public string ThirdTag { get; }
    public string NearestCalibrationRowId { get; }
    public string EvaluationReason { get; }
    public bool IsUnseenEvaluation => !string.IsNullOrWhiteSpace(EvaluationReason);
}

internal sealed class NarrativeEquipmentParticipantFixture : IDisposable
{
    private sealed class ControlledCharacterWorld :
        IBuildingWorldQuery,
        ICharacterWorldQuery,
        ICharacterLifetimeQuery
    {
        private readonly CharacterActor[] characters;

        public ControlledCharacterWorld(CharacterActor actor)
        {
            characters = new[] { actor ?? throw new ArgumentNullException(nameof(actor)) };
        }

        public int BuildingVersion => 0;
        public int CharacterVersion => 0;
        public int LifetimeCharacterVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings => Array.Empty<BuildableObject>();
        public IReadOnlyList<CharacterActor> Characters => characters;
        public IReadOnlyList<CharacterActor> AllCharacters => characters;
    }

    private readonly CharacterSO data;
    private readonly ControlledCharacterWorld world;

    private NarrativeEquipmentParticipantFixture(
        GameObject root,
        CharacterSO data,
        CharacterActor actor)
    {
        Root = root;
        this.data = data;
        Actor = actor;
        world = new ControlledCharacterWorld(actor);
    }

    public GameObject Root { get; }
    public CharacterActor Actor { get; }
    public string ActorId => Actor?.Identity?.PersistentId?.Trim() ?? string.Empty;

    public static NarrativeEquipmentParticipantFixture Create(
        LedgerRow row,
        int ordinal)
    {
        if (row == null) throw new ArgumentNullException(nameof(row));
        GameObject root = CharacterAiPlanDebugFixtures.CreateActorObject(
            "NarrativeEquipmentParticipant_" + row.Id);
        CharacterSO data = null;
        try
        {
            data = CharacterAiPlanDebugFixtures.CreateCharacterData(
                CharacterType.NPC,
                "리안",
                "human");
            CharacterActor actor = root.GetComponent<CharacterActor>();
            if (actor == null)
                throw NarrativeMechanicScenarioOtherProfiles.Fail(
                    "equipment-participant-actor-missing",
                    "Controlled equipment narrative participant has no CharacterActor.");
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(
                "character:continuity:equipment:" + row.Id + ":"
                + Math.Abs(ordinal).ToString("D3", CultureInfo.InvariantCulture));
            actor.Initialization(data);
            if (string.IsNullOrWhiteSpace(actor.Identity.PersistentId)
                || string.IsNullOrWhiteSpace(actor.Identity.DisplayName))
            {
                throw NarrativeMechanicScenarioOtherProfiles.Fail(
                    "equipment-participant-identity-missing",
                    "Controlled equipment narrative participant requires a public identity and display name.");
            }
            return new NarrativeEquipmentParticipantFixture(root, data, actor);
        }
        catch
        {
            if (data != null) Object.DestroyImmediate(data);
            Object.DestroyImmediate(root);
            throw;
        }
    }

    public IReadOnlyDictionary<string, string> ResolveDisplayNames(
        IEnumerable<string> participantIds)
    {
        IReadOnlyDictionary<string, string> resolved =
            EvolutionHistoryNarrativeRuntime.ResolveParticipantDisplayNamesFromWorld(
                world,
                participantIds);
        string expectedName = Actor.Identity.DisplayName.Trim();
        if (!resolved.TryGetValue(ActorId, out string resolvedName)
            || !string.Equals(resolvedName, expectedName, StringComparison.Ordinal))
        {
            throw NarrativeMechanicScenarioOtherProfiles.Fail(
                "equipment-participant-public-lookup-failed",
                "Production participant lookup did not resolve the controlled public character identity.");
        }
        return resolved;
    }

    public void Dispose()
    {
        if (Root != null) Object.DestroyImmediate(Root);
        if (data != null) Object.DestroyImmediate(data);
    }
}

internal static class NarrativeMechanicScenarioOtherProfiles
{
    public static readonly LedgerRow[] LedgerRows =
    {
        new LedgerRow("execution-ranged", HistoricalEvidenceKind.BossExecution, HistoricalEvidenceKind.RepeatedLongRangeHit, "combat:hit", "combat:hit", "ranged", "ranged"),
        new LedgerRow("protection-melee", HistoricalEvidenceKind.ProtectedOwner, HistoricalEvidenceKind.ArmorBroken, "combat:block", "combat:hit", "melee", "melee"),
        new LedgerRow("interception-block", HistoricalEvidenceKind.InterceptedFatalHit, HistoricalEvidenceKind.ProtectedOwner, "combat:block", "combat:absorb", "guard", "guard"),
        new LedgerRow("survival-precision", HistoricalEvidenceKind.SurvivedNearDeath, HistoricalEvidenceKind.None, "combat:hit", "combat:hit", "precision", "precision"),
        new LedgerRow("ranged-control", HistoricalEvidenceKind.RepeatedLongRangeHit, HistoricalEvidenceKind.CapturedEnemy, "combat:hit", "combat:block", "ranged", "guard"),
        new LedgerRow("armor-execution", HistoricalEvidenceKind.ArmorBroken, HistoricalEvidenceKind.BossExecution, "combat:absorb", "combat:hit", "melee", "melee"),
        new LedgerRow("capture-melee", HistoricalEvidenceKind.CapturedEnemy, HistoricalEvidenceKind.None, "combat:hit", "combat:hit", "melee", "melee"),
        new LedgerRow("owner-ranged", HistoricalEvidenceKind.ProtectedOwner, HistoricalEvidenceKind.RepeatedLongRangeHit, "combat:block", "combat:hit", "ranged", "ranged"),
        new LedgerRow("survival-block", HistoricalEvidenceKind.SurvivedNearDeath, HistoricalEvidenceKind.ArmorBroken, "combat:hit", "combat:block", "precision", "guard"),
        new LedgerRow("boss-precision", HistoricalEvidenceKind.BossExecution, HistoricalEvidenceKind.None, "combat:hit", "combat:hit", "precision", "precision"),
        new LedgerRow("protection-control", HistoricalEvidenceKind.ProtectedOwner, HistoricalEvidenceKind.CapturedEnemy, "combat:block", "combat:absorb", "guard", "guard"),
        new LedgerRow("longrange-melee", HistoricalEvidenceKind.RepeatedLongRangeHit, HistoricalEvidenceKind.None, "combat:hit", "combat:hit", "ranged", "melee"),
        new LedgerRow("armor-survival", HistoricalEvidenceKind.ArmorBroken, HistoricalEvidenceKind.SurvivedNearDeath, "combat:absorb", "combat:hit", "guard", "precision"),
        new LedgerRow("capture-ranged", HistoricalEvidenceKind.CapturedEnemy, HistoricalEvidenceKind.RepeatedLongRangeHit, "combat:block", "combat:hit", "ranged", "ranged"),
        new LedgerRow("boss-interception", HistoricalEvidenceKind.BossExecution, HistoricalEvidenceKind.InterceptedFatalHit, "combat:hit", "combat:block", "melee", "guard")
    };

    public static readonly LedgerRow[] UnseenEvaluationLedgerRows =
    {
        new LedgerRow("unseen-eval-v1-threefold-execution", HistoricalEvidenceKind.BossExecution, HistoricalEvidenceKind.RepeatedLongRangeHit, HistoricalEvidenceKind.ProtectedOwner, "combat:hit", "combat:hit", "combat:block", "ranged", "ranged", "guard", "execution-ranged", "event sequence: boss execution is followed by ranged pressure and a protection response; outcome: the current ledger changes from a two-event strike record to three distinct combat outcomes."),
        new LedgerRow("unseen-eval-v1-warden-breakthrough", HistoricalEvidenceKind.ProtectedOwner, HistoricalEvidenceKind.ArmorBroken, HistoricalEvidenceKind.RepeatedLongRangeHit, "combat:block", "combat:absorb", "combat:hit", "guard", "melee", "ranged", "protection-melee", "event sequence: owner protection is followed by armor loss and a ranged recovery; domain combination: protection, durability, and ranged evidence replace the calibration's melee-only follow-up."),
        new LedgerRow("unseen-eval-v1-intercept-command", HistoricalEvidenceKind.InterceptedFatalHit, HistoricalEvidenceKind.BossExecution, HistoricalEvidenceKind.CapturedEnemy, "combat:block", "combat:hit", "combat:hit", "guard", "melee", "melee", "interception-block", "event sequence: an interception precedes a boss execution and capture; outcome: the third event changes the record from defensive absorption to a completed enemy capture."),
        new LedgerRow("unseen-eval-v1-survival-rain", HistoricalEvidenceKind.SurvivedNearDeath, HistoricalEvidenceKind.RepeatedLongRangeHit, HistoricalEvidenceKind.BossExecution, "combat:hit", "combat:hit", "combat:hit", "precision", "ranged", "ranged", "survival-precision", "event sequence: near-death survival is followed by ranged pressure and a boss execution; domain combination: survival, ranged, and execution evidence replace a single precision pattern."),
        new LedgerRow("unseen-eval-v1-range-capture", HistoricalEvidenceKind.RepeatedLongRangeHit, HistoricalEvidenceKind.CapturedEnemy, HistoricalEvidenceKind.InterceptedFatalHit, "combat:hit", "combat:block", "combat:block", "ranged", "guard", "guard", "ranged-control", "event sequence: repeated long-range hits lead to a capture and then fatal-hit interception; outcome: the ledger adds a third control outcome rather than ending after two actions."),
        new LedgerRow("unseen-eval-v1-armor-rain", HistoricalEvidenceKind.ArmorBroken, HistoricalEvidenceKind.BossExecution, HistoricalEvidenceKind.RepeatedLongRangeHit, "combat:absorb", "combat:hit", "combat:hit", "melee", "melee", "ranged", "armor-execution", "event sequence: armor break, boss execution, and ranged pressure occur in order; domain combination: durability, execution, and ranged evidence replace the calibration's two-domain history."),
        new LedgerRow("unseen-eval-v1-capture-precision", HistoricalEvidenceKind.CapturedEnemy, HistoricalEvidenceKind.RepeatedLongRangeHit, HistoricalEvidenceKind.SurvivedNearDeath, "combat:hit", "combat:hit", "combat:hit", "melee", "ranged", "precision", "capture-melee", "event sequence: a capture is followed by ranged fire and survival under pressure; outcome: the record changes from a simple melee capture to a three-event control-and-precision sequence."),
        new LedgerRow("unseen-eval-v1-owner-intercept", HistoricalEvidenceKind.ProtectedOwner, HistoricalEvidenceKind.RepeatedLongRangeHit, HistoricalEvidenceKind.InterceptedFatalHit, "combat:block", "combat:hit", "combat:block", "guard", "ranged", "guard", "owner-ranged", "event sequence: owner protection is followed by a ranged exchange and an interception; participant role: the protected actor becomes the target of a distinct final defensive event."),
        new LedgerRow("unseen-eval-v1-survival-arc", HistoricalEvidenceKind.SurvivedNearDeath, HistoricalEvidenceKind.ArmorBroken, HistoricalEvidenceKind.RepeatedLongRangeHit, "combat:hit", "combat:block", "combat:hit", "precision", "guard", "ranged", "survival-block", "event sequence: survival precedes armor damage and then a ranged counterattack; domain combination: the final ranged domain makes this more than the calibration's survival-and-block pair."),
        new LedgerRow("unseen-eval-v1-boss-precision", HistoricalEvidenceKind.BossExecution, HistoricalEvidenceKind.RepeatedLongRangeHit, HistoricalEvidenceKind.SurvivedNearDeath, "combat:hit", "combat:hit", "combat:hit", "melee", "ranged", "precision", "boss-precision", "event sequence: the boss execution is followed by ranged pressure and near-death survival; outcome: a survival outcome is added to the calibration's two-event precision record."),
        new LedgerRow("unseen-eval-v1-protection-command", HistoricalEvidenceKind.ProtectedOwner, HistoricalEvidenceKind.CapturedEnemy, HistoricalEvidenceKind.BossExecution, "combat:block", "combat:absorb", "combat:hit", "guard", "guard", "melee", "protection-control", "event sequence: protection leads to capture and then boss execution; domain combination: a final execution domain changes the control-only calibration history."),
        new LedgerRow("unseen-eval-v1-longrange-command", HistoricalEvidenceKind.RepeatedLongRangeHit, HistoricalEvidenceKind.BossExecution, HistoricalEvidenceKind.InterceptedFatalHit, "combat:hit", "combat:hit", "combat:block", "ranged", "melee", "guard", "longrange-melee", "event sequence: long-range fire is followed by boss execution and interception; outcome: the final event changes the two-event melee vector into a defensive command response."),
        new LedgerRow("unseen-eval-v1-armor-survival", HistoricalEvidenceKind.ArmorBroken, HistoricalEvidenceKind.SurvivedNearDeath, HistoricalEvidenceKind.BossExecution, "combat:absorb", "combat:hit", "combat:hit", "guard", "precision", "melee", "armor-survival", "event sequence: armor break and survival are followed by boss execution; domain combination: the added execution evidence changes both the event sequence and historical direction mix."),
        new LedgerRow("unseen-eval-v1-capture-ward", HistoricalEvidenceKind.CapturedEnemy, HistoricalEvidenceKind.RepeatedLongRangeHit, HistoricalEvidenceKind.ProtectedOwner, "combat:block", "combat:hit", "combat:block", "guard", "ranged", "guard", "capture-ranged", "event sequence: a capture is followed by ranged pressure and owner protection; participant role: the final event explicitly shifts the record to guarding a public target."),
        new LedgerRow("unseen-eval-v1-boss-vigil", HistoricalEvidenceKind.BossExecution, HistoricalEvidenceKind.InterceptedFatalHit, HistoricalEvidenceKind.RepeatedLongRangeHit, "combat:hit", "combat:block", "combat:hit", "melee", "guard", "ranged", "boss-interception", "event sequence: boss execution, interception, and ranged pressure form a three-event ledger; outcome: the added ranged outcome distinguishes it from the calibration's two-event vigil.")
    };

    public static NarrativeMechanicCatalogExportException Fail(string code, string message) =>
        new NarrativeMechanicCatalogUnsupportedSourceException(code, message);
    public static KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue> P(
        string key, NarrativeMechanicCatalogCanonicalJsonValue value) =>
        NarrativeMechanicCatalogCanonicalJson.Property(key, value);
    public static NarrativeMechanicCatalogCanonicalJsonArray StringArray(IEnumerable<string> values) =>
        new NarrativeMechanicCatalogCanonicalJsonArray((values ?? Array.Empty<string>()).Select(
            value => (NarrativeMechanicCatalogCanonicalJsonValue)NarrativeMechanicCatalogCanonicalJson.String(value ?? string.Empty)));
    public static NarrativeMechanicCatalogCanonicalJsonArray Facts(
        params KeyValuePair<string, string>[] values) =>
        new NarrativeMechanicCatalogCanonicalJsonArray((values ?? Array.Empty<KeyValuePair<string, string>>())
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => (NarrativeMechanicCatalogCanonicalJsonValue)NarrativeMechanicCatalogCanonicalJson.Object(
                P("factId", NarrativeMechanicCatalogCanonicalJson.String(value.Key)),
                P("text", NarrativeMechanicCatalogCanonicalJson.String(value.Value)))));
    public static NarrativeMechanicCatalogCanonicalJsonArray EffectCandidates(IEnumerable<string> effectIds) =>
        new NarrativeMechanicCatalogCanonicalJsonArray((effectIds ?? Array.Empty<string>()).Select((effectId, index) =>
        {
            EquipmentHistoricalEffectDefinition definition = EquipmentHistoricalEffectCatalog.Require(effectId);
            return (NarrativeMechanicCatalogCanonicalJsonValue)NarrativeMechanicCatalogCanonicalJson.Object(
                P("candidateIndex", NarrativeMechanicCatalogCanonicalJson.Integer(index)),
                P("description", NarrativeMechanicCatalogCanonicalJson.String(definition.Description)),
                P("displayName", NarrativeMechanicCatalogCanonicalJson.String(definition.DisplayName)),
                P("effectId", NarrativeMechanicCatalogCanonicalJson.String(definition.EffectId)));
        }));
    public static string[] EffectDescriptions(IEnumerable<string> effectIds) =>
        (effectIds ?? Array.Empty<string>()).Select(EquipmentHistoricalEffectCatalog.Require)
            .Select(definition => definition.EffectId + ": " + definition.DisplayName + " — " + definition.Description).ToArray();

    public static NarrativeMechanicScenarioEvaluationMetadata EquipmentHistoryMetadata(
        string profileId,
        LedgerRow row)
    {
        if (row == null || !row.IsUnseenEvaluation)
            throw Fail("unseen-evaluation-ledger-metadata-missing", "Unseen equipment/history rows require authored evaluation metadata.");
        if (string.Equals(profileId, NarrativeMechanicScenarioProfiles.EquipmentChoice, StringComparison.Ordinal))
        {
            return new NarrativeMechanicScenarioEvaluationMetadata(
                "unseen-eval-v1:equipment-history:" + row.Id,
                "unseen-evaluation",
                "equipment-choice-" + row.NearestCalibrationRowId,
                "distinct-context",
                row.EvaluationReason + " generation structure: this selection keeps all three curated events in the current generation.");
        }
        if (string.Equals(profileId, NarrativeMechanicScenarioProfiles.EvolutionHistory, StringComparison.Ordinal))
        {
            return new NarrativeMechanicScenarioEvaluationMetadata(
                "unseen-eval-v1:equipment-history:" + row.Id,
                "unseen-evaluation",
                "evolution-history-" + row.NearestCalibrationRowId,
                "distinct-context",
                row.EvaluationReason + " generation structure: it also includes a compacted generation strictly earlier than the current three-event ledger.");
        }
        throw Fail("unseen-evaluation-ledger-profile-unsupported", "Ledger evaluation metadata only supports EquipmentChoice and EvolutionHistory.");
    }

    public static UsageLedger CreateLedger(
        LedgerRow row,
        int ordinal,
        string actorId,
        string targetId,
        int currentGeneration,
        bool includePriorGeneration)
    {
        if (row == null) throw new ArgumentNullException(nameof(row));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("A public actor id is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("A public target id is required.", nameof(targetId));
        if (currentGeneration < 1)
            throw new ArgumentOutOfRangeException(
                nameof(currentGeneration),
                currentGeneration,
                "A controlled fixture generation must be positive.");

        UsageLedger ledger = new UsageLedger();
        UsageLedgerCompactor compactor = new UsageLedgerCompactor();
        if (includePriorGeneration)
        {
            int priorGeneration = Math.Max(0, currentGeneration - 1);
            compactor.Record(
                ledger,
                row.FirstEventId,
                10 + Math.Abs(ordinal % 7),
                actorId,
                targetId,
                new[] { row.FirstTag, "scenario" },
                "evidence:" + row.Id + ":prior",
                row.FirstEvidence,
                "outcome:" + row.Id + ":prior",
                priorGeneration,
                1 + Math.Abs(ordinal % 2));
            compactor.CloseGeneration(ledger, priorGeneration);
        }

        compactor.Record(
            ledger,
            row.FirstEventId,
            10 + Math.Abs(ordinal % 7),
            actorId,
            targetId,
            new[] { row.FirstTag, "scenario" },
            "evidence:" + row.Id + ":primary",
            row.FirstEvidence,
            "outcome:" + row.Id + ":primary",
            currentGeneration,
            1 + Math.Abs(ordinal % 2));
        compactor.Record(
            ledger,
            row.SecondEventId,
            4 + Math.Abs(ordinal % 5),
            actorId,
            targetId,
            new[] { row.SecondTag, "scenario" },
            "evidence:" + row.Id + ":secondary",
            row.SecondEvidence,
            "outcome:" + row.Id + ":secondary",
            currentGeneration);
        if (row.IsUnseenEvaluation)
        {
            if (row.ThirdEvidence == HistoricalEvidenceKind.None
                || string.IsNullOrWhiteSpace(row.ThirdEventId)
                || string.IsNullOrWhiteSpace(row.ThirdTag)
                || string.IsNullOrWhiteSpace(row.NearestCalibrationRowId))
            {
                throw Fail(
                    "unseen-evaluation-ledger-row-invalid",
                    "Unseen equipment/history rows require a non-empty third authored event and calibration reference.");
            }
            compactor.Record(
                ledger,
                row.ThirdEventId,
                7 + Math.Abs(ordinal % 6),
                actorId,
                targetId,
                new[] { row.ThirdTag, "scenario" },
                "evidence:" + row.Id + ":tertiary",
                row.ThirdEvidence,
                "outcome:" + row.Id + ":tertiary",
                currentGeneration,
                1 + Math.Abs((ordinal + 1) % 3));
        }
        return ledger;
    }
    public static NarrativeMechanicCatalogCanonicalJsonArray LedgerEventsJson(UsageLedger ledger) =>
        new NarrativeMechanicCatalogCanonicalJsonArray((ledger?.currentGenerationEvents ?? new List<UsageLedgerEvent>())
            .Where(entry => entry != null).OrderBy(entry => entry.sequence).Select(entry =>
            (NarrativeMechanicCatalogCanonicalJsonValue)NarrativeMechanicCatalogCanonicalJson.Object(
                P("amountDecimal", NarrativeMechanicCatalogCanonicalJson.String(entry.amount.ToString("0.###", CultureInfo.InvariantCulture))),
                P("evidenceId", NarrativeMechanicCatalogCanonicalJson.String(entry.evidenceId)),
                P("eventId", NarrativeMechanicCatalogCanonicalJson.String(entry.eventId)),
                P("historicalEvidenceKind", NarrativeMechanicCatalogCanonicalJson.String(entry.historicalEvidenceKind.ToString())),
                P("sequence", NarrativeMechanicCatalogCanonicalJson.Integer(entry.sequence)),
                P("sourceTags", StringArray(entry.sourceTags)))));
    public static NarrativeMechanicCatalogCanonicalJsonArray HistoryFacts(UsageLedger ledger) => Facts(
        (ledger?.currentGenerationEvents ?? new List<UsageLedgerEvent>()).Where(entry => entry != null)
            .OrderBy(entry => entry.sequence).Select(entry => new KeyValuePair<string, string>(
                "fact:history:" + entry.evidenceId,
                "Historical evidence: " + entry.historicalEvidenceKind + "; event: " + entry.eventId)).ToArray());
    public static NarrativeMechanicCatalogCanonicalJsonObject EvolutionRequestJson(EvolutionNarrativeRequestSnapshot request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        return NarrativeMechanicCatalogCanonicalJson.Object(
            P("effectBudget", NarrativeMechanicCatalogCanonicalJson.Integer(request.effectBudget)), P("effectId", NarrativeMechanicCatalogCanonicalJson.String(request.effectId)),
            P("evidenceIds", StringArray(request.evidenceIds)), P("generation", NarrativeMechanicCatalogCanonicalJson.Integer(request.generation)),
            P("historyHash", NarrativeMechanicCatalogCanonicalJson.String(request.historyHash)), P("legalCandidateEffectIds", StringArray(request.legalCandidateEffectIds)),
            P("nodeId", NarrativeMechanicCatalogCanonicalJson.String(request.nodeId)), P("parentNodeId", NarrativeMechanicCatalogCanonicalJson.String(request.parentNodeId)),
            P("participantIds", StringArray(request.participantIds)), P("requestKey", NarrativeMechanicCatalogCanonicalJson.String(request.requestKey)),
            P("sourceTags", StringArray(request.sourceTags)), P("targetKind", NarrativeMechanicCatalogCanonicalJson.String(request.targetKind.ToString())),
            P("targetPersistentId", NarrativeMechanicCatalogCanonicalJson.String(request.targetPersistentId)));
    }
    public static string EquipmentChoiceResponse(int selectedIndex) => NarrativeMechanicCatalogCanonicalJson.Object(P("selectedIndex", NarrativeMechanicCatalogCanonicalJson.Integer(selectedIndex))).ToCanonicalString();
    public static string EvolutionHistoryResponse(EvolutionNarrativeRequestSnapshot request, int effectBudget, string historyLabel)
    {
        string displayName = "History " + EquipmentHistoricalEffectCatalog.Require(request.effectId).DisplayName;
        if (displayName.Length > 32) throw Fail("evolution-history-scenario-display-name", "C# authored history display name exceeds the 32-character validator limit: " + displayName);
        return NarrativeMechanicCatalogCanonicalJson.Object(P("description", NarrativeMechanicCatalogCanonicalJson.String("A controlled account of the already locked historical effect.")), P("displayName", NarrativeMechanicCatalogCanonicalJson.String(displayName)), P("effectBudget", NarrativeMechanicCatalogCanonicalJson.Integer(effectBudget)), P("effectId", NarrativeMechanicCatalogCanonicalJson.String(request.effectId)), P("evidenceIds", StringArray(request.evidenceIds)), P("historyReason", NarrativeMechanicCatalogCanonicalJson.String("It reflects the locked evidence packet without changing mechanics.")), P("nodeId", NarrativeMechanicCatalogCanonicalJson.String(request.nodeId)), P("parentNodeId", NarrativeMechanicCatalogCanonicalJson.String(request.parentNodeId)), P("requestKey", NarrativeMechanicCatalogCanonicalJson.String(request.requestKey)), P("targetPersistentId", NarrativeMechanicCatalogCanonicalJson.String(request.targetPersistentId))).ToCanonicalString();
    }
    public static string PersonaResponse(string personaName, string flavorText, bool includeExtraMechanics)
    {
        List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> properties = new List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> { P("flavorText", NarrativeMechanicCatalogCanonicalJson.String(flavorText)), P("personaName", NarrativeMechanicCatalogCanonicalJson.String(personaName)) };
        if (includeExtraMechanics) properties.Add(P("selfCareMultiplier", NarrativeMechanicCatalogCanonicalJson.Integer(1)));
        return new NarrativeMechanicCatalogCanonicalJsonObject(properties).ToCanonicalString();
    }
    public static IDictionary<CharacterCondition, float> CustomerNeeds(string focus, int ordinal)
    {
        Dictionary<CharacterCondition, float> values = new Dictionary<CharacterCondition, float> { {CharacterCondition.HUNGER,74}, {CharacterCondition.SLEEP,76}, {CharacterCondition.FUN,72}, {CharacterCondition.MOOD,70}, {CharacterCondition.EXCRETION,78}, {CharacterCondition.HYGIENE,79} };
        values[focus switch { "hunger"=>CharacterCondition.HUNGER, "sleep"=>CharacterCondition.SLEEP, "fun"=>CharacterCondition.FUN, "mood"=>CharacterCondition.MOOD, "excretion"=>CharacterCondition.EXCRETION, "hygiene"=>CharacterCondition.HYGIENE, _=>throw Fail("persona-scenario-need-unsupported", "Unsupported controlled Customer need '"+focus+"'.") }] = 20 + Math.Abs(ordinal%5)*3;
        return values;
    }
    public static CustomerPersonaData CreateLockedPersonaMechanics(string field, float value, string tag)
    {
        CustomerPersonaData mechanics = new CustomerPersonaData { traitName="CSharpAuthoredMechanics", flavorText="Locked mechanics remain outside LLM output.", preferredFacilityTags=new[] {tag} };
        switch(field) { case "selfCareMultiplier": mechanics.selfCareMultiplier=value; break; case "curiosityMultiplier": mechanics.curiosityMultiplier=value; break; case "shoppingMultiplier": mechanics.shoppingMultiplier=value; break; case "patienceMultiplier": mechanics.patienceMultiplier=value; break; case "hungerCurveMultiplier": mechanics.hungerCurveMultiplier=value; break; case "funCurveMultiplier": mechanics.funCurveMultiplier=value; break; case "moodCurveMultiplier": mechanics.moodCurveMultiplier=value; break; default: throw Fail("persona-scenario-mechanic-unsupported", "Unsupported locked Customer mechanic '"+field+"'."); }
        mechanics.Clamp(); return mechanics;
    }
    public static void CopyPersonaMechanics(CustomerPersonaData source, CustomerPersonaData destination) { destination.selfCareMultiplier=source.selfCareMultiplier; destination.curiosityMultiplier=source.curiosityMultiplier; destination.shoppingMultiplier=source.shoppingMultiplier; destination.patienceMultiplier=source.patienceMultiplier; destination.hungerCurveMultiplier=source.hungerCurveMultiplier; destination.funCurveMultiplier=source.funCurveMultiplier; destination.moodCurveMultiplier=source.moodCurveMultiplier; destination.preferredFacilityTags=source.preferredFacilityTags.ToArray(); }
    public static bool PersonaMechanicsEqual(CustomerPersonaData expected, CustomerPersonaData actual) => actual != null && Mathf.Approximately(expected.selfCareMultiplier,actual.selfCareMultiplier) && Mathf.Approximately(expected.curiosityMultiplier,actual.curiosityMultiplier) && Mathf.Approximately(expected.shoppingMultiplier,actual.shoppingMultiplier) && Mathf.Approximately(expected.patienceMultiplier,actual.patienceMultiplier) && Mathf.Approximately(expected.hungerCurveMultiplier,actual.hungerCurveMultiplier) && Mathf.Approximately(expected.funCurveMultiplier,actual.funCurveMultiplier) && Mathf.Approximately(expected.moodCurveMultiplier,actual.moodCurveMultiplier) && (expected.preferredFacilityTags??Array.Empty<string>()).SequenceEqual(actual.preferredFacilityTags??Array.Empty<string>(),StringComparer.Ordinal);
    public static NarrativeMechanicCatalogCanonicalJsonObject PersonaMechanicsJson(CustomerPersonaData mechanics) => NarrativeMechanicCatalogCanonicalJson.Object(P("curiosityMultiplier", DecimalText(mechanics.curiosityMultiplier)), P("funCurveMultiplier", DecimalText(mechanics.funCurveMultiplier)), P("hungerCurveMultiplier", DecimalText(mechanics.hungerCurveMultiplier)), P("moodCurveMultiplier", DecimalText(mechanics.moodCurveMultiplier)), P("patienceMultiplier", DecimalText(mechanics.patienceMultiplier)), P("preferredFacilityTags", StringArray(mechanics.preferredFacilityTags)), P("selfCareMultiplier", DecimalText(mechanics.selfCareMultiplier)), P("shoppingMultiplier", DecimalText(mechanics.shoppingMultiplier)));
    public static string Hash(string value) => NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(Encoding.UTF8.GetBytes(value ?? string.Empty));
    public static NarrativeMechanicScenarioPublicContext RequirePublicContext(
        string scenarioId,
        NarrativePublicContextMaterial material,
        NarrativePublicPromptEnvelope envelope)
    {
        if (material == null)
            throw Fail("scenario-public-material-missing", "Scenario '" + scenarioId + "' has no production public material.");
        if (envelope == null || string.IsNullOrWhiteSpace(envelope.Prompt)
            || envelope.Material == null)
            throw Fail("scenario-public-envelope-missing", "Scenario '" + scenarioId + "' has no production prompt envelope.");
        if (!string.Equals(material.ProfileId, envelope.Material.ProfileId, StringComparison.Ordinal)
            || !string.Equals(material.SubjectId, envelope.Material.SubjectId, StringComparison.Ordinal)
            || !string.Equals(material.SemanticHash, envelope.PublicContextSemanticHash, StringComparison.Ordinal))
            throw Fail("scenario-public-envelope-mismatch", "Scenario '" + scenarioId + "' prompt envelope is not bound to its public material.");
        return NarrativeMechanicScenarioPublicContextSerializer.Serialize(material);
    }
    private static NarrativeMechanicCatalogCanonicalJsonValue DecimalText(float value) => NarrativeMechanicCatalogCanonicalJson.String(value.ToString("0.###", CultureInfo.InvariantCulture));
}

public sealed class NarrativeMechanicScenarioFacilityEvolutionSource : INarrativeMechanicScenarioSource
{
    private sealed class FacilityRow
    {
        public FacilityRow(string id, string recipeId, string tags, string signal) { Id=id; RecipeId=recipeId; MutationTags=tags.Split('|'); Signal=signal; OperationalHistory=Array.Empty<string>(); }
        public FacilityRow(string id, string recipeId, string tags, string signal, string[] operationalHistory, string nearestCalibrationRowId, string evaluationReason) : this(id,recipeId,tags,signal) { OperationalHistory=operationalHistory??Array.Empty<string>(); NearestCalibrationRowId=nearestCalibrationRowId; EvaluationReason=evaluationReason; }
        public string Id { get; } public string RecipeId { get; } public string[] MutationTags { get; } public string Signal { get; } public string[] OperationalHistory { get; } public string NearestCalibrationRowId { get; } public string EvaluationReason { get; } public bool IsUnseenEvaluation => !string.IsNullOrWhiteSpace(EvaluationReason);
    }
    private sealed class FixtureRecipeQuery : IFacilityEvolutionRecipeQuery, IFacilityEvolutionRecipeProvider
    {
        private readonly IReadOnlyList<FacilityEvolutionRecipeSO> recipes; private readonly IFacilityEvolutionStateComponentFactory states;
        public FixtureRecipeQuery(IReadOnlyList<FacilityEvolutionRecipeSO> recipes, IFacilityEvolutionStateComponentFactory states) { this.recipes=recipes??throw new ArgumentNullException(nameof(recipes)); this.states=states??throw new ArgumentNullException(nameof(states)); }
        public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes() => recipes;
        public bool IsVisible(FacilityEvolutionRecipeSO recipe, BlueprintResearchState state) => FacilityEvolutionService.IsRecipeVisible(recipe,state,null);
        public IReadOnlyList<FacilityEvolutionRecipeSO> GetVisibleRecipes(BlueprintResearchState state) => recipes.Where(recipe=>IsVisible(recipe,state)).ToArray();
        public IReadOnlyList<FacilityEvolutionRecipeSO> GetSourceCandidates(BuildableObject facility, BlueprintResearchState state) => FacilityEvolutionService.GetSourceCandidates(facility,recipes,state,this,states);
    }
    private sealed class FacilityFixture : IDisposable
    {
        private FacilityFixture(GameObject root, BuildableObject facility, FacilityEvolutionStateComponent state, FacilityEvolutionContext context, IReadOnlyList<FacilityEvolutionRecipeSO> legal, IReadOnlyCollection<string> tags, string prompt, string signature) { Root=root; Facility=facility; State=state; Context=context; LegalCandidates=legal; LegalMutationTags=tags; Prompt=prompt; Signature=signature; }
        public GameObject Root { get; } public BuildableObject Facility { get; } public FacilityEvolutionStateComponent State { get; } public FacilityEvolutionContext Context { get; } public IReadOnlyList<FacilityEvolutionRecipeSO> LegalCandidates { get; } public IReadOnlyCollection<string> LegalMutationTags { get; } public string Prompt { get; } public string Signature { get; }
        public static FacilityFixture Create(FacilityRow row, int ordinal, IReadOnlyList<FacilityEvolutionRecipeSO> recipes)
        {
            FacilityEvolutionRecipeSO selected=recipes.FirstOrDefault(recipe=>string.Equals(recipe.EffectiveId,row.RecipeId,StringComparison.Ordinal));
            BuildingSO source=selected?.fromFacilities?.FirstOrDefault(building=>building!=null);
            if(selected==null || source==null) throw NarrativeMechanicScenarioOtherProfiles.Fail("facility-evolution-scenario-source-missing", "Facility scenario row '"+row.Id+"' has no authored source building for '"+row.RecipeId+"'.");
            GameObject root=new GameObject("NarrativeFacilityScenario_"+row.Id);
            try
            {
                BuildableObject facility=root.AddComponent<BuildableObject>();
                facility.RestorePersistentIdentity(ScenarioBuildingId(row, ordinal));
                CharacterAiEditorTestDependencies.Inject(facility);
                facility.Initialization(source,Vector2Int.zero);
                FacilityEvolutionStateComponentFactory states=new FacilityEvolutionStateComponentFactory();
                FixtureRecipeQuery query=new FixtureRecipeQuery(recipes,states);
                IReadOnlyList<FacilityEvolutionRecipeSO> legal=FacilityEvolutionService.GetSourceCandidates(facility,recipes,new BlueprintResearchState(),query,states);
                if(legal.Count==0 || !legal.Any(recipe=>recipe==selected)) throw NarrativeMechanicScenarioOtherProfiles.Fail("facility-evolution-scenario-producer-empty", "FacilityEvolutionService.GetSourceCandidates did not produce authored recipe '"+row.RecipeId+"' for controlled source '"+source.id+"'.");
                RoomProfile profile=new RoomProfile(facility,null); profile.AddTag(row.Signal);
                if(row.IsUnseenEvaluation)
                {
                    if(row.OperationalHistory.Length<2||row.OperationalHistory.Any(string.IsNullOrWhiteSpace)||string.IsNullOrWhiteSpace(row.NearestCalibrationRowId)) throw NarrativeMechanicScenarioOtherProfiles.Fail("facility-evolution-evaluation-history-short","Unseen facility rows require a concrete multi-event operational history and calibration reference.");
                    foreach(string entry in row.OperationalHistory) profile.AddRecentEvent(entry);
                }
                else profile.AddRecentEvent("Controlled scenario signal: "+row.Signal);
                foreach(string tag in row.MutationTags) profile.SetIdentityPressure(tag,0.75f);
                FacilityEvolutionStateComponent state=states.GetOrAdd(facility);
                FacilityEvolutionContext context=new FacilityEvolutionContext(facility,state,profile,legal);
                string prompt=FacilityEvolutionPromptFormatter.BuildPrompt(context); string signature=FacilityEvolutionPromptFormatter.BuildSignature(context);
                if(string.IsNullOrWhiteSpace(prompt)||string.IsNullOrWhiteSpace(signature)) throw NarrativeMechanicScenarioOtherProfiles.Fail("facility-evolution-scenario-request-empty", "FacilityEvolutionPromptFormatter failed to produce a controlled request for '"+row.Id+"'.");
                string[] tags=legal.SelectMany(recipe=>recipe.allowedMutationTags??Array.Empty<string>()).Where(tag=>!string.IsNullOrWhiteSpace(tag)).Select(tag=>tag.Trim()).Distinct(StringComparer.Ordinal).OrderBy(tag=>tag,StringComparer.Ordinal).ToArray();
                if(row.MutationTags.Any(tag=>!tags.Contains(tag,StringComparer.Ordinal))) throw NarrativeMechanicScenarioOtherProfiles.Fail("facility-evolution-scenario-mutation-illegal", "Controlled mutation tag is not in the producer packet for '"+row.Id+"'.");
                return new FacilityFixture(root,facility,state,context,legal.ToArray(),tags,prompt,signature);
            }
            catch { Object.DestroyImmediate(root); throw; }
        }
        public void Dispose() { if(Root!=null) Object.DestroyImmediate(Root); }
    }
    private static readonly FacilityRow[] Rows =
    {
        new FacilityRow("alchemy-research","evolve_research_desk_to_alchemy_bench","Research","research-record"), new FacilityRow("alchemy-ritual","evolve_research_desk_to_alchemy_bench","Ritual","ritual-pressure"), new FacilityRow("alchemy-dual","evolve_research_desk_to_alchemy_bench","Research|Ritual","secure-arcana"),
        new FacilityRow("archery-combat","evolve_training_dummy_to_archery_target","Combat","combat-drill"), new FacilityRow("archery-security","evolve_training_dummy_to_archery_target","Security","guard-watch"), new FacilityRow("archery-dual","evolve_training_dummy_to_archery_target","Combat|Security","fortified-training"),
        new FacilityRow("grill-service","evolve_commercial_hearth_to_grill","Service","meal-rush"), new FacilityRow("grill-logistics","evolve_commercial_hearth_to_grill","Logistics","supply-throughput"), new FacilityRow("ritual-mana","evolve_mana_shelf_to_ritual_focus","Mana","mana-resonance"), new FacilityRow("ritual-focus","evolve_mana_shelf_to_ritual_focus","Ritual","ceremony-cycle"),
        new FacilityRow("display-luxury","evolve_shop_display_to_secure_display","Luxury","premium-stock"), new FacilityRow("display-service","evolve_shop_display_to_secure_display","Service","guest-turnover"), new FacilityRow("display-dual","evolve_shop_display_to_secure_display","Luxury|Service","guarded-commerce"), new FacilityRow("tactical-brutal","evolve_guard_desk_to_tactical_table","Brutal","hard-response"), new FacilityRow("tactical-security","evolve_guard_desk_to_tactical_table","Security","command-watch")
    };
    private static readonly FacilityRow[] UnseenEvaluationRows =
    {
        new FacilityRow("unseen-eval-v1-alchemy-night-ledger","evolve_research_desk_to_alchemy_bench","Research","night-study",new[]{"Apprentices catalogued a volatile mineral sample.","A containment ward held during a midnight spill.","The room reopened after a research review."},"alchemy-research","facility operational history: sample cataloguing, ward containment, and a reopening review replace one generic signal; domain combination: research and ritual pressure now occur in one room history."),
        new FacilityRow("unseen-eval-v1-alchemy-ritual-audit","evolve_research_desk_to_alchemy_bench","Ritual","ritual-audit",new[]{"A ritual circle was inspected before dawn.","An alchemy catalyst was quarantined after a resonance spike.","The keeper recorded a successful cleanup."},"alchemy-ritual","facility operational history: ritual inspection, catalyst quarantine, and cleanup form a multi-event sequence; outcome: the final record is a verified recovery instead of a single pressure tag."),
        new FacilityRow("unseen-eval-v1-alchemy-secure-pair","evolve_research_desk_to_alchemy_bench","Research|Ritual","secure-pair",new[]{"Researchers signed out a sealed formula.","Ritual attendants reinforced the storage ward.","A joint review released the bench for supervised work."},"alchemy-dual","facility operational history: formula checkout, ward reinforcement, and supervised release make the room history concrete; domain combination: research and ritual are coupled across separate events."),
        new FacilityRow("unseen-eval-v1-archery-escort-drill","evolve_training_dummy_to_archery_target","Combat","escort-drill",new[]{"Guards ran an escort drill beside the target lane.","An archer logged a long-distance correction.","The squad closed the lane after a safe weapons count."},"archery-combat","facility operational history: escort drill, ranged correction, and weapons count replace a generic combat token; outcome: the lane ends in a controlled closure."),
        new FacilityRow("unseen-eval-v1-archery-watch-handoff","evolve_training_dummy_to_archery_target","Security","watch-handoff",new[]{"The night watch tested the alarm rope.","A sentry handed the range log to the morning guard.","The target line was cleared after the inspection."},"archery-security","facility operational history: alarm test, guard handoff, and inspection clearance create a three-event watch record; participant role: two shifts share responsibility for the range."),
        new FacilityRow("unseen-eval-v1-archery-fortified-cycle","evolve_training_dummy_to_archery_target","Combat|Security","fortified-cycle",new[]{"A patrol reported a breach near the practice lane.","Combat trainers moved the targets behind the barricade.","Security staff certified the reinforced route."},"archery-dual","facility operational history: breach report, target relocation, and route certification describe an operational cycle; domain combination: combat response and security certification both affect the room."),
        new FacilityRow("unseen-eval-v1-grill-service-recovery","evolve_commercial_hearth_to_grill","Service","service-recovery",new[]{"A guest queue formed before the lunch bell.","The hearth crew replaced a cracked serving stone.","Meals resumed after the queue was redistributed."},"grill-service","facility operational history: guest queue, repair, and meal resumption replace a single service label; outcome: a concrete service recovery closes the sequence."),
        new FacilityRow("unseen-eval-v1-grill-supply-chain","evolve_commercial_hearth_to_grill","Logistics","supply-chain",new[]{"A late spice crate reached the loading shelf.","Cooks rationed the remaining fuel during the delay.","The next delivery restored the prep schedule."},"grill-logistics","facility operational history: delayed crate, fuel rationing, and schedule restoration create a supply chain history; outcome: logistics interruption is resolved rather than merely named."),
        new FacilityRow("unseen-eval-v1-ritual-shelf-resonance","evolve_mana_shelf_to_ritual_focus","Mana","shelf-resonance",new[]{"A mana vial was logged into the shelf ledger.","The focus crystal resonated during a calibration.","A steward sealed the shelf after the reading."},"ritual-mana","facility operational history: vial logging, resonance calibration, and sealing form a concrete mana sequence; domain combination: inventory handling and mana observation both shape the room."),
        new FacilityRow("unseen-eval-v1-ritual-focus-observance","evolve_mana_shelf_to_ritual_focus","Ritual","focus-observance",new[]{"Attendants prepared the focus before the observance.","A chant paused when the ward flickered.","The circle resumed after a keeper confirmed stability."},"ritual-focus","facility operational history: preparation, a ward interruption, and confirmed resumption replace the single ceremony cycle; outcome: stability is explicitly verified after disruption."),
        new FacilityRow("unseen-eval-v1-display-premium-audit","evolve_shop_display_to_secure_display","Luxury","premium-audit",new[]{"A jewelled item was entered in the premium case ledger.","A visitor requested a supervised viewing.","The clerk resealed the display after the appraisal."},"display-luxury","facility operational history: ledger entry, supervised viewing, and resealing make a luxury transaction history; participant role: visitor and clerk act in distinct steps."),
        new FacilityRow("unseen-eval-v1-display-service-turnover","evolve_shop_display_to_secure_display","Service","service-turnover",new[]{"The counter queue changed after an early delivery.","Staff moved a reserved item to the secure case.","The next guest received a confirmed pickup time."},"display-service","facility operational history: queue change, item transfer, and guest confirmation create a service turnover record; outcome: the final event communicates a resolved request."),
        new FacilityRow("unseen-eval-v1-display-guarded-exchange","evolve_shop_display_to_secure_display","Luxury|Service","guarded-exchange",new[]{"A high-value parcel arrived with a courier seal.","The shopkeeper checked the luxury display against the manifest.","Service staff completed a guarded handoff."},"display-dual","facility operational history: parcel arrival, manifest check, and guarded handoff form an operational history; domain combination: luxury control and customer service are both present."),
        new FacilityRow("unseen-eval-v1-tactical-incident-review","evolve_guard_desk_to_tactical_table","Brutal","incident-review",new[]{"A patrol reported a brutal corridor clash.","The guard desk assigned a response pair.","The captain reviewed the cleared route after the clash."},"tactical-brutal","facility operational history: clash report, response assignment, and route review replace a hard-response label; outcome: the third event records a cleared route."),
        new FacilityRow("unseen-eval-v1-tactical-watch-plan","evolve_guard_desk_to_tactical_table","Security","watch-plan",new[]{"The commander compared two watch routes.","A sentry marked an unsecured stairwell.","The revised plan was briefed to the relief team."},"tactical-security","facility operational history: route comparison, stairwell report, and relief briefing create a security planning sequence; participant role: commander, sentry, and relief team occupy separate steps.")
    };
    private readonly bool useUnseenNamingEvaluation;
    public NarrativeMechanicScenarioFacilityEvolutionSource() { }
    public NarrativeMechanicScenarioFacilityEvolutionSource(NarrativeMechanicScenarioSetKind setKind)
    {
        useUnseenNamingEvaluation=setKind==NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation;
        if(!useUnseenNamingEvaluation&&setKind!=NarrativeMechanicScenarioSetKind.Calibration) throw new ArgumentOutOfRangeException(nameof(setKind));
    }
    public string ProfileId => NarrativeMechanicScenarioProfiles.FacilityEvolution;
    public NarrativeMechanicScenarioSourceCapture CaptureScenarios()
    {
        FacilityRow[] activeRows=useUnseenNamingEvaluation?UnseenEvaluationRows:Rows;
        FacilityEvolutionRecipeSO[] recipes=LoadRecipes(activeRows);
        List<NarrativeMechanicScenario> positives=activeRows.Select((row, ordinal)=>BuildPositive(row,ordinal,recipes)).ToList();
        NarrativeMechanicScenario[] negatives=useUnseenNamingEvaluation?Array.Empty<NarrativeMechanicScenario>():new[] {BuildIllegalProposalNegative(Rows[0],recipes)};
        return new NarrativeMechanicScenarioSourceCapture(ProfileId,positives,negatives,new[] {"Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionService.cs","Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionLlmProposalProvider.cs","Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs","Assets/Scripts/Services/Character/AI/LlmJsonResponseParser.cs","Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioOtherProfilesSource.cs"},allowEmptyNegativeScenarios:useUnseenNamingEvaluation);
    }
    private static NarrativeMechanicScenario BuildPositive(FacilityRow row,int ordinal,IReadOnlyList<FacilityEvolutionRecipeSO> recipes) { using FacilityFixture fixture=FacilityFixture.Create(row,ordinal,recipes); string response=FacilityResponse(fixture.LegalCandidates[0].EffectiveId,row.MutationTags,row.Signal); RequireValidation(response,fixture.LegalCandidates,fixture.LegalMutationTags,true,FacilityEvolutionProposalRejectionKind.None,row.Id); return BuildScenario("facility-evolution-"+row.Id,fixture,response,true,string.Empty,row); }
    private static NarrativeMechanicScenario BuildIllegalProposalNegative(FacilityRow row,IReadOnlyList<FacilityEvolutionRecipeSO> recipes) { using FacilityFixture fixture=FacilityFixture.Create(row,99,recipes); string response=FacilityResponse("facility-evolution:scenario-forged",Array.Empty<string>(),"forged-id"); string error=RequireValidation(response,fixture.LegalCandidates,fixture.LegalMutationTags,false,FacilityEvolutionProposalRejectionKind.IllegalProposalId,"illegal-proposal-id"); return BuildScenario("facility-evolution-reject-illegal-proposal-id",fixture,response,false,error,row); }
    private static BuildingInstanceId ScenarioBuildingId(FacilityRow row, int ordinal) =>
        new BuildingInstanceId("building:narrative-scenario:facility-evolution:"
            + ordinal.ToString("D2", CultureInfo.InvariantCulture) + ":" + row.Id);
    private static NarrativeMechanicScenario BuildScenario(string id,FacilityFixture fixture,string response,bool accepted,string failure,FacilityRow row)
    {
        NarrativePublicContextMaterial publicMaterial =
            FacilityEvolutionPromptFormatter.BuildPublicMaterial(fixture.Context);
        NarrativePublicPromptEnvelope envelope =
            FacilityEvolutionPromptFormatter.BuildPromptEnvelope(fixture.Context);
        NarrativeMechanicScenarioPublicContext publicContext =
            NarrativeMechanicScenarioOtherProfiles.RequirePublicContext(
                id,
                publicMaterial,
                envelope);
        NarrativeMechanicCatalogCanonicalJsonArray legal=new NarrativeMechanicCatalogCanonicalJsonArray(fixture.LegalCandidates.Select((recipe,index)=>(NarrativeMechanicCatalogCanonicalJsonValue)RecipeJson(recipe,index)));
        NarrativeMechanicCatalogCanonicalJsonObject request=NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("candidateIds",NarrativeMechanicScenarioOtherProfiles.StringArray(fixture.LegalCandidates.Select(recipe=>recipe.EffectiveId))),NarrativeMechanicScenarioOtherProfiles.P("prompt",NarrativeMechanicCatalogCanonicalJson.String(envelope.Prompt)),NarrativeMechanicScenarioOtherProfiles.P("requestSignature",NarrativeMechanicCatalogCanonicalJson.String(fixture.Signature)),NarrativeMechanicScenarioOtherProfiles.P("responseJson",NarrativeMechanicCatalogCanonicalJson.String(response)));
        NarrativeMechanicCatalogCanonicalJsonObject authority=NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("candidateOrder",NarrativeMechanicScenarioOtherProfiles.StringArray(fixture.LegalCandidates.Select(recipe=>recipe.EffectiveId))),NarrativeMechanicScenarioOtherProfiles.P("facilityPersistentId",NarrativeMechanicCatalogCanonicalJson.String(fixture.Facility.PersistentInstanceId.Value)),NarrativeMechanicScenarioOtherProfiles.P("sourceBuildingId",NarrativeMechanicCatalogCanonicalJson.Integer(fixture.Facility.BuildingData.id)),NarrativeMechanicScenarioOtherProfiles.P("starGrade",NarrativeMechanicCatalogCanonicalJson.Integer(fixture.State.StarGrade)),NarrativeMechanicScenarioOtherProfiles.P("validatorBoundary",NarrativeMechanicCatalogCanonicalJson.String("proposalIds and mutationTags must remain in this packet")));
        NarrativeMechanicCatalogCanonicalJsonObject input=row.IsUnseenEvaluation?NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("fixtureKind",NarrativeMechanicCatalogCanonicalJson.String("controlled-facility-operational-history")),NarrativeMechanicScenarioOtherProfiles.P("operationalHistory",NarrativeMechanicScenarioOtherProfiles.StringArray(row.OperationalHistory)),NarrativeMechanicScenarioOtherProfiles.P("recipeId",NarrativeMechanicCatalogCanonicalJson.String(row.RecipeId)),NarrativeMechanicScenarioOtherProfiles.P("roomSignal",NarrativeMechanicCatalogCanonicalJson.String(row.Signal)),NarrativeMechanicScenarioOtherProfiles.P("selectedMutationTags",NarrativeMechanicScenarioOtherProfiles.StringArray(row.MutationTags))):NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("fixtureKind",NarrativeMechanicCatalogCanonicalJson.String("controlled-facility-source-candidate")),NarrativeMechanicScenarioOtherProfiles.P("recipeId",NarrativeMechanicCatalogCanonicalJson.String(row.RecipeId)),NarrativeMechanicScenarioOtherProfiles.P("roomSignal",NarrativeMechanicCatalogCanonicalJson.String(row.Signal)),NarrativeMechanicScenarioOtherProfiles.P("selectedMutationTags",NarrativeMechanicScenarioOtherProfiles.StringArray(row.MutationTags)));
        return new NarrativeMechanicScenario(id,NarrativeMechanicScenarioProfiles.FacilityEvolution,request,legal,publicContext.TargetPersistentId,publicContext,fixture.LegalCandidates.Select(recipe=>"FacilityEvolutionProposalJsonDto constrains '"+recipe.EffectiveId+"' to the source candidate packet; authored recipe: "+recipe.DisplayName),authority,input,"FacilityEvolutionService.GetSourceCandidates + FacilityEvolutionPromptFormatter.BuildPromptEnvelope","LlmJsonResponseParser.TryParse<FacilityEvolutionProposalJsonDto> + FacilityEvolutionProposalJsonDto.TryCreateRuntimeProposal",accepted,failure,new[] {new KeyValuePair<string,string>("recipe",row.RecipeId),new KeyValuePair<string,string>("roomSignal",row.Signal),new KeyValuePair<string,string>("mutationSelection",string.Join("+",row.MutationTags))},evaluationMetadata:row.IsUnseenEvaluation?new NarrativeMechanicScenarioEvaluationMetadata("unseen-eval-v1:facility:"+row.Id,"unseen-evaluation","facility-evolution-"+row.NearestCalibrationRowId,"distinct-context",row.EvaluationReason):null);
    }
    private static NarrativeMechanicCatalogCanonicalJsonObject RecipeJson(FacilityEvolutionRecipeSO recipe,int index) { if(recipe==null||!recipe.HasValidData) throw NarrativeMechanicScenarioOtherProfiles.Fail("facility-evolution-scenario-recipe-invalid","Facility scenario candidate is missing valid authored data."); return NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("allowedMutationTags",NarrativeMechanicScenarioOtherProfiles.StringArray(recipe.allowedMutationTags??Array.Empty<string>())),NarrativeMechanicScenarioOtherProfiles.P("candidateIndex",NarrativeMechanicCatalogCanonicalJson.Integer(index)),NarrativeMechanicScenarioOtherProfiles.P("displayName",NarrativeMechanicCatalogCanonicalJson.String(recipe.DisplayName)),NarrativeMechanicScenarioOtherProfiles.P("recipeId",NarrativeMechanicCatalogCanonicalJson.String(recipe.EffectiveId)),NarrativeMechanicScenarioOtherProfiles.P("requiredStarGrade",NarrativeMechanicCatalogCanonicalJson.Integer(recipe.requiredStarGrade))); }
    private static string FacilityResponse(string proposalId,IEnumerable<string> tags,string signal) => NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("confidence",NarrativeMechanicCatalogCanonicalJson.Integer(1)),NarrativeMechanicScenarioOtherProfiles.P("flavorText",NarrativeMechanicCatalogCanonicalJson.String("Controlled facility context: "+signal)),NarrativeMechanicScenarioOtherProfiles.P("mutationTags",NarrativeMechanicScenarioOtherProfiles.StringArray(tags)),NarrativeMechanicScenarioOtherProfiles.P("proposalIds",NarrativeMechanicScenarioOtherProfiles.StringArray(new[]{proposalId})),NarrativeMechanicScenarioOtherProfiles.P("reasons",NarrativeMechanicCatalogCanonicalJson.Array(NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("proposalId",NarrativeMechanicCatalogCanonicalJson.String(proposalId)),NarrativeMechanicScenarioOtherProfiles.P("reason",NarrativeMechanicCatalogCanonicalJson.String("The controlled signal selects this already legal facility lineage.")))))).ToCanonicalString();
    private static string RequireValidation(string response,IReadOnlyList<FacilityEvolutionRecipeSO> legal,IReadOnlyCollection<string> tags,bool expected,FacilityEvolutionProposalRejectionKind expectedKind,string label)
    {
        bool accepted=LlmJsonResponseParser.TryParse(LocalLlmRequestProfiles.FacilityEvolutionLegacyV2.Id,response,out FacilityEvolutionProposalJsonDto payload,out string error); FacilityEvolutionProposalRejectionKind kind=accepted?FacilityEvolutionProposalRejectionKind.None:FacilityEvolutionProposalRejectionKind.InvalidPayload;
        if(accepted) accepted=payload.TryCreateRuntimeProposal("controlled-facility-scenario",legal.Select(recipe=>recipe.EffectiveId).ToArray(),tags,new Dictionary<string,string>(StringComparer.Ordinal),new Dictionary<string,string>(StringComparer.Ordinal),null,out _,out kind,out error);
        if(accepted!=expected||kind!=expectedKind) throw NarrativeMechanicScenarioOtherProfiles.Fail("facility-evolution-scenario-validator-mismatch","Facility scenario '"+label+"' produced "+accepted+"/"+kind+"; expected "+expected+"/"+expectedKind+": "+error); return error??string.Empty;
    }
    private static FacilityEvolutionRecipeSO[] LoadRecipes(IEnumerable<FacilityRow> rows)
    {
        FacilityEvolutionRecipeSO[] recipes=AssetDatabase.FindAssets("t:FacilityEvolutionRecipeSO").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>).Where(recipe=>recipe!=null&&recipe.HasValidData).OrderBy(recipe=>recipe.EffectiveId,StringComparer.Ordinal).ToArray();
        if(recipes.Length==0) throw NarrativeMechanicScenarioOtherProfiles.Fail("facility-evolution-scenario-recipe-missing","Facility scenario export requires at least one valid authored evolution recipe.");
        foreach(FacilityRow row in rows??Array.Empty<FacilityRow>()) if(!recipes.Any(recipe=>string.Equals(recipe.EffectiveId,row.RecipeId,StringComparison.Ordinal))) throw NarrativeMechanicScenarioOtherProfiles.Fail("facility-evolution-scenario-row-unsupported","Authored facility recipe is missing for scenario row '"+row.Id+"': "+row.RecipeId); return recipes;
    }
}

public sealed class NarrativeMechanicScenarioEquipmentChoiceSource : INarrativeMechanicScenarioSource
{
    private sealed class EquipmentFixture : IDisposable
    {
        private readonly NarrativeEquipmentParticipantFixture participant;
        private EquipmentFixture(LedgerRow row,UsageLedger ledger,List<string> candidates,EvolutionNarrativeRequestSnapshot request,string grammar,NarrativeEquipmentParticipantFixture participant,IReadOnlyDictionary<string,string> participantDisplayNames) { Row=row; Ledger=ledger; Candidates=candidates; Request=request; Grammar=grammar; this.participant=participant; ParticipantDisplayNames=participantDisplayNames; }
        public LedgerRow Row { get; } public UsageLedger Ledger { get; } public List<string> Candidates { get; } public EvolutionNarrativeRequestSnapshot Request { get; } public string Grammar { get; } public IReadOnlyDictionary<string,string> ParticipantDisplayNames { get; }
        public static EquipmentFixture Create(LedgerRow row,int ordinal)
        {
            NarrativeEquipmentParticipantFixture participant=
                NarrativeEquipmentParticipantFixture.Create(row,ordinal);
            try
            {
                string targetId="scenario-equipment-"+row.Id;
                int currentGeneration=1+Math.Abs(ordinal%3);
                UsageLedger ledger=NarrativeMechanicScenarioOtherProfiles.CreateLedger(row,ordinal,participant.ActorId,targetId,currentGeneration,false); List<string> candidates=EquipmentEvolutionRules.BuildLegalHistoricalEffectCandidates(ledger);
                if(row.IsUnseenEvaluation&&(ledger.currentGenerationEvents==null||ledger.currentGenerationEvents.Count!=3||ledger.currentGenerationEvents.Any(entry=>entry==null||entry.generation!=currentGeneration))) throw NarrativeMechanicScenarioOtherProfiles.Fail("equipment-choice-evaluation-current-ledger-invalid","Unseen equipment rows require exactly three current-generation events for '"+row.Id+"'.");
                if(candidates.Count<2||candidates.Count>3) throw NarrativeMechanicScenarioOtherProfiles.Fail("equipment-choice-scenario-producer-count","EquipmentEvolutionRules produced unsupported candidate count "+candidates.Count+" for '"+row.Id+"'.");
                foreach(string candidate in candidates) EquipmentHistoricalEffectCatalog.Require(candidate);
                EvolutionNode node=new EvolutionNode {nodeId="scenario-choice-node-"+row.Id,parentNodeId="scenario-choice-parent-"+row.Id,effectId=candidates[0],generation=currentGeneration,legalCandidateEffectIds=candidates.ToList(),selectedCandidateIndex=-1};
                EvolutionNarrativeRequestSnapshot request=EvolutionNarrativeRequestFactory.Create(EvolutionNarrativeTargetKind.Equipment,targetId,node,"history-choice-"+row.Id,ledger,1+Math.Abs(ordinal%3));
                IReadOnlyDictionary<string,string> participantDisplayNames=
                    participant.ResolveDisplayNames(request.participantIds);
                return new EquipmentFixture(row,ledger,candidates,request,EquipmentChoiceGrammarCatalog.Require(candidates.Count),participant,participantDisplayNames);
            }
            catch
            {
                participant.Dispose();
                throw;
            }
        }
        public void Dispose() => participant.Dispose();
    }
    private static readonly LedgerRow[] Rows=NarrativeMechanicScenarioOtherProfiles.LedgerRows;
    private readonly bool useUnseenNamingEvaluation;
    public NarrativeMechanicScenarioEquipmentChoiceSource() { }
    public NarrativeMechanicScenarioEquipmentChoiceSource(NarrativeMechanicScenarioSetKind setKind)
    {
        useUnseenNamingEvaluation=setKind==NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation;
        if(!useUnseenNamingEvaluation&&setKind!=NarrativeMechanicScenarioSetKind.Calibration) throw new ArgumentOutOfRangeException(nameof(setKind));
    }
    public string ProfileId => NarrativeMechanicScenarioProfiles.EquipmentChoice;
    public NarrativeMechanicScenarioSourceCapture CaptureScenarios()
    {
        LedgerRow[] activeRows=useUnseenNamingEvaluation?NarrativeMechanicScenarioOtherProfiles.UnseenEvaluationLedgerRows:Rows;
        List<NarrativeMechanicScenario> positives=activeRows.Select(BuildPositive).ToList();
        NarrativeMechanicScenario[] negatives=useUnseenNamingEvaluation?Array.Empty<NarrativeMechanicScenario>():new[]{BuildOutOfRangeNegative(Rows[0])};
        return new NarrativeMechanicScenarioSourceCapture(ProfileId,positives,negatives,new[]{"Assets/Scripts/Services/Combat/EquipmentEvolutionRules.cs","Assets/Scripts/Models/Evolution/Core/EvolutionModuleRegistry.cs","Assets/Scripts/Models/AI/Core/V25NarrativeInferenceContracts.cs","Assets/Scripts/Services/Evolution/EvolutionHistoryNarrativeRuntime.cs","Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioOtherProfilesSource.cs"},allowEmptyNegativeScenarios:useUnseenNamingEvaluation);
    }
    private static NarrativeMechanicScenario BuildPositive(LedgerRow row,int index) { using EquipmentFixture fixture=EquipmentFixture.Create(row,index); int selected=index%fixture.Candidates.Count; string response=NarrativeMechanicScenarioOtherProfiles.EquipmentChoiceResponse(selected); RequireValidation(response,fixture.Candidates.Count,true,row.Id); return BuildScenario("equipment-choice-"+row.Id,fixture,response,selected,true,string.Empty); }
    private static NarrativeMechanicScenario BuildOutOfRangeNegative(LedgerRow row) { using EquipmentFixture fixture=EquipmentFixture.Create(row,99); string response=NarrativeMechanicScenarioOtherProfiles.EquipmentChoiceResponse(fixture.Candidates.Count); string error=RequireValidation(response,fixture.Candidates.Count,false,"out-of-range-index"); return BuildScenario("equipment-choice-reject-out-of-range-index",fixture,response,fixture.Candidates.Count,false,error); }
    private static NarrativeMechanicScenario BuildScenario(string id,EquipmentFixture fixture,string response,int selected,bool accepted,string failure)
    {
        NarrativePublicContextMaterial publicMaterial =
            EvolutionNarrativePromptFormatter.BuildPublicMaterial(
                LocalLlmRequestProfiles.EquipmentChoiceLegacyV2.Id,
                fixture.Request,
                string.Empty,
                fixture.ParticipantDisplayNames);
        NarrativePublicPromptEnvelope envelope =
            EvolutionNarrativePromptFormatter.BuildPromptEnvelope(
                LocalLlmRequestProfiles.EquipmentChoiceLegacyV2.Id,
                fixture.Request,
                string.Empty,
                fixture.ParticipantDisplayNames);
        NarrativeMechanicScenarioPublicContext publicContext =
            NarrativeMechanicScenarioOtherProfiles.RequirePublicContext(
                id,
                publicMaterial,
                envelope);
        NarrativeMechanicCatalogCanonicalJsonObject request=NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("candidateCount",NarrativeMechanicCatalogCanonicalJson.Integer(fixture.Candidates.Count)),NarrativeMechanicScenarioOtherProfiles.P("grammar",NarrativeMechanicCatalogCanonicalJson.String(fixture.Grammar)),NarrativeMechanicScenarioOtherProfiles.P("requestSnapshot",NarrativeMechanicScenarioOtherProfiles.EvolutionRequestJson(fixture.Request)),NarrativeMechanicScenarioOtherProfiles.P("responseJson",NarrativeMechanicCatalogCanonicalJson.String(response)));
        NarrativeMechanicCatalogCanonicalJsonObject authority=NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("candidateOrder",NarrativeMechanicScenarioOtherProfiles.StringArray(fixture.Candidates)),NarrativeMechanicScenarioOtherProfiles.P("historyHash",NarrativeMechanicCatalogCanonicalJson.String(fixture.Request.historyHash)),NarrativeMechanicScenarioOtherProfiles.P("selectedIndex",NarrativeMechanicCatalogCanonicalJson.Integer(selected)),NarrativeMechanicScenarioOtherProfiles.P("targetPersistentId",NarrativeMechanicCatalogCanonicalJson.String(fixture.Request.targetPersistentId)));
        NarrativeMechanicCatalogCanonicalJsonObject input=NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("fixtureKind",NarrativeMechanicCatalogCanonicalJson.String("controlled-equipment-usage-ledger")),NarrativeMechanicScenarioOtherProfiles.P("ledgerEvents",NarrativeMechanicScenarioOtherProfiles.LedgerEventsJson(fixture.Ledger)),NarrativeMechanicScenarioOtherProfiles.P("semanticVector",NarrativeMechanicCatalogCanonicalJson.String(fixture.Row.Id)));
        return new NarrativeMechanicScenario(id,NarrativeMechanicScenarioProfiles.EquipmentChoice,request,NarrativeMechanicScenarioOtherProfiles.EffectCandidates(fixture.Candidates),publicContext.TargetPersistentId,publicContext,NarrativeMechanicScenarioOtherProfiles.EffectDescriptions(fixture.Candidates),authority,input,"EquipmentEvolutionRules.BuildLegalHistoricalEffectCandidates + EvolutionNarrativeRequestFactory.Create + EvolutionNarrativePromptFormatter.BuildPublicMaterial","EquipmentChoiceGrammarCatalog.Require + EquipmentChoiceResultParser.TryParse",accepted,failure,new[]{new KeyValuePair<string,string>("historyVector",fixture.Row.Id),new KeyValuePair<string,string>("candidateCount",fixture.Candidates.Count.ToString(CultureInfo.InvariantCulture)),new KeyValuePair<string,string>("selectedIndex",selected.ToString(CultureInfo.InvariantCulture))},evaluationMetadata:fixture.Row.IsUnseenEvaluation?NarrativeMechanicScenarioOtherProfiles.EquipmentHistoryMetadata(NarrativeMechanicScenarioProfiles.EquipmentChoice,fixture.Row):null);
    }
    private static string RequireValidation(string response,int count,bool expected,string label) { bool accepted=EquipmentChoiceResultParser.TryParse(response,count,out _,out string error); if(accepted!=expected) throw NarrativeMechanicScenarioOtherProfiles.Fail("equipment-choice-scenario-validator-mismatch","Equipment choice scenario '"+label+"' produced "+accepted+"; expected "+expected+": "+error); return error??string.Empty; }
}

public sealed class NarrativeMechanicScenarioEvolutionHistorySource : INarrativeMechanicScenarioSource
{
    private sealed class HistoryFixture : IDisposable
    {
        private readonly NarrativeEquipmentParticipantFixture participant;
        private HistoryFixture(LedgerRow row,UsageLedger ledger,EvolutionNarrativeRequestSnapshot request,int selected,NarrativeEquipmentParticipantFixture participant,IReadOnlyDictionary<string,string> participantDisplayNames) { Row=row; Ledger=ledger; Request=request; SelectedIndex=selected; this.participant=participant; ParticipantDisplayNames=participantDisplayNames; }
        public LedgerRow Row { get; } public UsageLedger Ledger { get; } public EvolutionNarrativeRequestSnapshot Request { get; } public int SelectedIndex { get; } public IReadOnlyDictionary<string,string> ParticipantDisplayNames { get; }
        public static HistoryFixture Create(LedgerRow row,int ordinal)
        {
            NarrativeEquipmentParticipantFixture participant=
                NarrativeEquipmentParticipantFixture.Create(row,ordinal+200);
            try
            {
                string targetId="scenario-history-equipment-"+row.Id;
                int fixtureOrdinal=ordinal+200;
                int currentGeneration=1+Math.Abs(fixtureOrdinal%3);
                UsageLedger ledger=NarrativeMechanicScenarioOtherProfiles.CreateLedger(row,fixtureOrdinal,participant.ActorId,targetId,currentGeneration,true); List<string> candidates=EquipmentEvolutionRules.BuildLegalHistoricalEffectCandidates(ledger);
                if(row.IsUnseenEvaluation&&(ledger.currentGenerationEvents==null||ledger.currentGenerationEvents.Count!=3||ledger.currentGenerationEvents.Any(entry=>entry==null||entry.generation!=currentGeneration))) throw NarrativeMechanicScenarioOtherProfiles.Fail("evolution-history-evaluation-current-ledger-invalid","Unseen history rows require exactly three current-generation events for '"+row.Id+"'.");
                if(candidates.Count==0) throw NarrativeMechanicScenarioOtherProfiles.Fail("evolution-history-scenario-producer-empty","EquipmentEvolutionRules produced no historical effect for '"+row.Id+"'.");
                int selected=Math.Abs(ordinal)%candidates.Count; EvolutionNode node=new EvolutionNode {nodeId="scenario-history-node-"+row.Id,parentNodeId="scenario-history-parent-"+row.Id,effectId=candidates[selected],generation=currentGeneration,legalCandidateEffectIds=candidates.ToList(),selectedCandidateIndex=selected};
                EvolutionNarrativeRequestSnapshot request=EvolutionNarrativeRequestFactory.Create(EvolutionNarrativeTargetKind.Equipment,targetId,node,"history-narrative-"+row.Id,ledger,1+Math.Abs(ordinal%3));
                if(request.evidenceIds.Count==0||request.effectBudget<=0) throw NarrativeMechanicScenarioOtherProfiles.Fail("evolution-history-scenario-request-invalid","EvolutionNarrativeRequestFactory did not lock evidence and effect budget for '"+row.Id+"'.");
                RequireControlledChronology(row,currentGeneration,ledger,request);
                IReadOnlyDictionary<string,string> participantDisplayNames=
                    participant.ResolveDisplayNames(request.participantIds);
                return new HistoryFixture(row,ledger,request,selected,participant,participantDisplayNames);
            }
            catch
            {
                participant.Dispose();
                throw;
            }
        }
        private static void RequireControlledChronology(
            LedgerRow row,
            int currentGeneration,
            UsageLedger ledger,
            EvolutionNarrativeRequestSnapshot request)
        {
            if(request.generation!=currentGeneration)
                throw NarrativeMechanicScenarioOtherProfiles.Fail("evolution-history-scenario-request-generation-mismatch","Evolution history request generation did not match the controlled current generation for '"+row.Id+"'.");
            if(ledger.currentGenerationEvents==null||ledger.currentGenerationEvents.Count==0||ledger.currentGenerationEvents.Any(entry=>entry==null||entry.generation!=currentGeneration))
                throw NarrativeMechanicScenarioOtherProfiles.Fail("evolution-history-scenario-current-generation-mismatch","Evolution history current events did not match the controlled current generation for '"+row.Id+"'.");
            if(ledger.compactedSegments==null||ledger.compactedSegments.Count==0||ledger.compactedSegments.Any(segment=>segment==null||segment.firstGeneration>=currentGeneration||segment.lastGeneration>=currentGeneration||(segment.keyEvents??new List<UsageLedgerEvent>()).Any(entry=>entry==null||entry.generation>=currentGeneration)))
                throw NarrativeMechanicScenarioOtherProfiles.Fail("evolution-history-scenario-prior-generation-invalid","Evolution history prior evidence was not strictly earlier than the controlled current generation for '"+row.Id+"'.");
        }
        public void Dispose() => participant.Dispose();
    }
    private static readonly LedgerRow[] Rows=NarrativeMechanicScenarioOtherProfiles.LedgerRows;
    private readonly bool useUnseenNamingEvaluation;
    public NarrativeMechanicScenarioEvolutionHistorySource() { }
    public NarrativeMechanicScenarioEvolutionHistorySource(NarrativeMechanicScenarioSetKind setKind)
    {
        useUnseenNamingEvaluation=setKind==NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation;
        if(!useUnseenNamingEvaluation&&setKind!=NarrativeMechanicScenarioSetKind.Calibration) throw new ArgumentOutOfRangeException(nameof(setKind));
    }
    public string ProfileId => NarrativeMechanicScenarioProfiles.EvolutionHistory;
    public NarrativeMechanicScenarioSourceCapture CaptureScenarios()
    {
        LedgerRow[] activeRows=useUnseenNamingEvaluation?NarrativeMechanicScenarioOtherProfiles.UnseenEvaluationLedgerRows:Rows;
        List<NarrativeMechanicScenario> positives=activeRows.Select(BuildPositive).ToList();
        NarrativeMechanicScenario[] negatives=useUnseenNamingEvaluation?Array.Empty<NarrativeMechanicScenario>():new[]{BuildBudgetNegative(Rows[0])};
        return new NarrativeMechanicScenarioSourceCapture(ProfileId,positives,negatives,new[]{"Assets/Scripts/Services/Evolution/EvolutionHistoryNarrativeRuntime.cs","Assets/Scripts/Models/Evolution/Core/EvolutionHistoryModels.cs","Assets/Scripts/Services/Combat/EquipmentEvolutionRules.cs","Assets/Scripts/Models/Evolution/Core/EvolutionModuleRegistry.cs","Assets/Scripts/Services/Character/AI/LlmJsonResponseParser.cs","Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioOtherProfilesSource.cs"},allowEmptyNegativeScenarios:useUnseenNamingEvaluation);
    }
    private static NarrativeMechanicScenario BuildPositive(LedgerRow row,int index) { using HistoryFixture fixture=HistoryFixture.Create(row,index); string response=NarrativeMechanicScenarioOtherProfiles.EvolutionHistoryResponse(fixture.Request,fixture.Request.effectBudget,"History "+row.Id); RequireValidation(response,fixture.Request,true,row.Id); return BuildScenario("evolution-history-"+row.Id,fixture,response,true,string.Empty); }
    private static NarrativeMechanicScenario BuildBudgetNegative(LedgerRow row) { using HistoryFixture fixture=HistoryFixture.Create(row,99); string response=NarrativeMechanicScenarioOtherProfiles.EvolutionHistoryResponse(fixture.Request,fixture.Request.effectBudget+1,"Changed budget"); string error=RequireValidation(response,fixture.Request,false,"changed-effect-budget"); return BuildScenario("evolution-history-reject-changed-effect-budget",fixture,response,false,error); }
    private static NarrativeMechanicScenario BuildScenario(string id,HistoryFixture fixture,string response,bool accepted,string failure)
    {
        EvolutionNarrativeRequestSnapshot request=fixture.Request;
        NarrativePublicContextMaterial publicMaterial =
            EvolutionNarrativePromptFormatter.BuildPublicMaterial(
                LocalLlmRequestProfiles.EvolutionHistoryLegacyV2.Id,
                request,
                string.Empty,
                fixture.ParticipantDisplayNames);
        NarrativePublicPromptEnvelope envelope =
            EvolutionNarrativePromptFormatter.BuildPromptEnvelope(
                LocalLlmRequestProfiles.EvolutionHistoryLegacyV2.Id,
                request,
                string.Empty,
                fixture.ParticipantDisplayNames);
        NarrativeMechanicScenarioPublicContext publicContext =
            NarrativeMechanicScenarioOtherProfiles.RequirePublicContext(
                id,
                publicMaterial,
                envelope);
        NarrativeMechanicCatalogCanonicalJsonObject authority=NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("effectBudget",NarrativeMechanicCatalogCanonicalJson.Integer(request.effectBudget)),NarrativeMechanicScenarioOtherProfiles.P("effectId",NarrativeMechanicCatalogCanonicalJson.String(request.effectId)),NarrativeMechanicScenarioOtherProfiles.P("evidenceIds",NarrativeMechanicScenarioOtherProfiles.StringArray(request.evidenceIds)),NarrativeMechanicScenarioOtherProfiles.P("nodeId",NarrativeMechanicCatalogCanonicalJson.String(request.nodeId)),NarrativeMechanicScenarioOtherProfiles.P("parentNodeId",NarrativeMechanicCatalogCanonicalJson.String(request.parentNodeId)),NarrativeMechanicScenarioOtherProfiles.P("requestKey",NarrativeMechanicCatalogCanonicalJson.String(request.requestKey)),NarrativeMechanicScenarioOtherProfiles.P("targetPersistentId",NarrativeMechanicCatalogCanonicalJson.String(request.targetPersistentId)));
        NarrativeMechanicCatalogCanonicalJsonObject input=NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("fixtureKind",NarrativeMechanicCatalogCanonicalJson.String("controlled-history-ledger")),NarrativeMechanicScenarioOtherProfiles.P("ledgerEvents",NarrativeMechanicScenarioOtherProfiles.LedgerEventsJson(fixture.Ledger)),NarrativeMechanicScenarioOtherProfiles.P("selectedCandidateIndex",NarrativeMechanicCatalogCanonicalJson.Integer(fixture.SelectedIndex)));
        return new NarrativeMechanicScenario(id,NarrativeMechanicScenarioProfiles.EvolutionHistory,NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("lockedRequest",NarrativeMechanicScenarioOtherProfiles.EvolutionRequestJson(request)),NarrativeMechanicScenarioOtherProfiles.P("responseJson",NarrativeMechanicCatalogCanonicalJson.String(response))),NarrativeMechanicScenarioOtherProfiles.EffectCandidates(request.legalCandidateEffectIds),publicContext.TargetPersistentId,publicContext,NarrativeMechanicScenarioOtherProfiles.EffectDescriptions(request.legalCandidateEffectIds),authority,input,"EquipmentEvolutionRules.BuildLegalHistoricalEffectCandidates + EvolutionNarrativeRequestFactory.Create + EvolutionNarrativePromptFormatter.BuildPublicMaterial","LlmJsonResponseParser.TryParse<EvolutionHistoryNarrativeResponseDto> + EvolutionNarrativeResponseValidator.Validate",accepted,failure,new[]{new KeyValuePair<string,string>("historyVector",fixture.Row.Id),new KeyValuePair<string,string>("effectBudget",request.effectBudget.ToString(CultureInfo.InvariantCulture)),new KeyValuePair<string,string>("selectedEffect",request.effectId)},evaluationMetadata:fixture.Row.IsUnseenEvaluation?NarrativeMechanicScenarioOtherProfiles.EquipmentHistoryMetadata(NarrativeMechanicScenarioProfiles.EvolutionHistory,fixture.Row):null);
    }
    private static string RequireValidation(string response,EvolutionNarrativeRequestSnapshot request,bool expected,string label) { bool accepted=LlmJsonResponseParser.TryParse(LocalLlmRequestProfiles.EvolutionHistoryLegacyV2.Id,response,out EvolutionHistoryNarrativeResponseDto payload,out string error); if(accepted) accepted=EvolutionNarrativeResponseValidator.Validate(request,payload,out error); if(accepted!=expected) throw NarrativeMechanicScenarioOtherProfiles.Fail("evolution-history-scenario-validator-mismatch","Evolution history scenario '"+label+"' produced "+accepted+"; expected "+expected+": "+error); return error??string.Empty; }
}

public sealed class NarrativeMechanicScenarioPersonaSource : INarrativeMechanicScenarioSource
{
    private sealed class PersonaRow
    {
        public PersonaRow(string id,string name,string species,string need,string field,float value,string tag) { Id=id; Name=name; Species=species; NeedFocus=need; MechanicField=field; MechanicValue=value; PreferredFacilityTag=tag; }
        public PersonaRow(string id,string name,string species,string need,string field,float value,string tag,string nearestCalibrationRowId,string evaluationReason) : this(id,name,species,need,field,value,tag) { NearestCalibrationRowId=nearestCalibrationRowId; EvaluationReason=evaluationReason; }
        public string Id { get; } public string Name { get; } public string Species { get; } public string NeedFocus { get; } public string MechanicField { get; } public float MechanicValue { get; } public string PreferredFacilityTag { get; } public string NearestCalibrationRowId { get; } public string EvaluationReason { get; } public bool IsUnseenEvaluation => !string.IsNullOrWhiteSpace(EvaluationReason);
    }
    private sealed class PersonaFixture : IDisposable
    {
        private sealed class ControlledUnavailablePersonaRuntimeProvider : ILocalLlmRuntimeProvider
        {
            public static readonly ControlledUnavailablePersonaRuntimeProvider Instance=new ControlledUnavailablePersonaRuntimeProvider();
            public bool TryGetRuntime(out ILocalLlmRuntime runtime) { runtime=null; return false; }
            public ILocalLlmRuntime GetRequiredRuntime() => throw new InvalidOperationException("Controlled Persona scenario fixtures never invoke a local LLM runtime.");
        }
        private PersonaFixture(PersonaRow row,GameObject root,CharacterSO data,CharacterActor actor,CustomerPersonaRuntime runtime,CustomerPersonaData mechanics,string prompt,string speciesBackground) { Row=row; ActorRoot=root; Data=data; Actor=actor; Runtime=runtime; Mechanics=mechanics; Prompt=prompt; SpeciesBackground=speciesBackground; }
        public PersonaRow Row { get; } public GameObject ActorRoot { get; } public CharacterSO Data { get; } public CharacterActor Actor { get; } public CustomerPersonaRuntime Runtime { get; } public CustomerPersonaData Mechanics { get; } public string Prompt { get; } public string SpeciesBackground { get; }
        public static PersonaFixture Create(PersonaRow row,int ordinal)
        {
            GameObject root=CharacterAiPlanDebugFixtures.CreateActorObject("NarrativePersonaScenario_"+row.Id); CharacterSO data=null;
            try
            {
                data=CharacterAiPlanDebugFixtures.CreateCharacterData(CharacterType.Customer,row.Name,row.Species);
                CharacterActor actor=root.GetComponent<CharacterActor>(); actor.EnsureRuntimeState(); actor.PersonaRuntime.ConstructCustomerPersonaRuntime(ControlledUnavailablePersonaRuntimeProvider.Instance); actor.Initialize(data);
                string speciesBackground=string.Empty;
                if(row.IsUnseenEvaluation)
                {
                    if(actor.Progression==null) throw NarrativeMechanicScenarioOtherProfiles.Fail("persona-evaluation-progression-missing","Customer evaluation fixture has no progression for species-authored background '"+row.Id+"'.");
                    speciesBackground=ResolveAuthoredSpeciesBackground(row.Species);
                    actor.Progression.GrowthState.origin=speciesBackground;
                }
                actor.stats=NarrativeMechanicScenarioOtherProfiles.CustomerNeeds(row.NeedFocus,ordinal);
                CustomerPersonaRuntime runtime=actor.PersonaRuntime;
                if(runtime==null) throw NarrativeMechanicScenarioOtherProfiles.Fail("persona-scenario-runtime-missing","Controlled customer actor has no CustomerPersonaRuntime.");
                runtime.RequestPersonaIfNeeded(logIfMissingQueue:false); string prompt=runtime.LastPrompt;
                if(actor.characterType!=CharacterType.Customer||string.IsNullOrWhiteSpace(prompt)||!prompt.Contains("name: "+row.Name,StringComparison.Ordinal)||!prompt.Contains("species: "+row.Species,StringComparison.Ordinal)) throw NarrativeMechanicScenarioOtherProfiles.Fail("persona-scenario-producer-unsupported","CustomerPersonaRuntime did not produce the controlled Customer-only prompt for '"+row.Id+"'.");
                CustomerPersonaData mechanics=NarrativeMechanicScenarioOtherProfiles.CreateLockedPersonaMechanics(row.MechanicField,row.MechanicValue,row.PreferredFacilityTag); return new PersonaFixture(row,root,data,actor,runtime,mechanics,prompt,speciesBackground);
            }
            catch { if(data!=null) Object.DestroyImmediate(data); Object.DestroyImmediate(root); throw; }
        }
        public void ApplyParsedNarrative(CustomerPersonaJsonDto payload) { CustomerPersonaData generated=payload.ToRuntimeData(); NarrativeMechanicScenarioOtherProfiles.CopyPersonaMechanics(Mechanics,generated); Runtime.ApplyGeneratedPersona(generated); if(!Runtime.HasGeneratedPersona||!NarrativeMechanicScenarioOtherProfiles.PersonaMechanicsEqual(Mechanics,Runtime.Persona)) throw NarrativeMechanicScenarioOtherProfiles.Fail("persona-scenario-mechanics-changed","CustomerPersonaRuntime did not preserve C#-authored mechanics for '"+Row.Id+"'."); }
        public string[] EffectDescriptions() => new[]{"CustomerPersonaRuntime.GetActionMultiplier locks "+Row.MechanicField+" at "+Row.MechanicValue.ToString("0.##",CultureInfo.InvariantCulture)+" after CustomerPersonaJsonDto parsing.","CustomerPersonaRuntime.GetFacilityTagPreference locks authored preference '"+Row.PreferredFacilityTag+"'."};
        public NarrativeMechanicCatalogCanonicalJsonObject MechanicsJson() => NarrativeMechanicScenarioOtherProfiles.PersonaMechanicsJson(Mechanics);
        private static string ResolveAuthoredSpeciesBackground(string speciesTag)
        {
            CharacterSpeciesDefinitionSO species=AssetDatabase.FindAssets("t:CharacterSpeciesDefinitionSO").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CharacterSpeciesDefinitionSO>).Where(value=>value!=null&&string.Equals(value.speciesTag?.Trim(),speciesTag,StringComparison.Ordinal)).SingleOrDefault();
            string background=species?.shortDescription?.Trim()??string.Empty;
            if(string.IsNullOrWhiteSpace(background)) throw NarrativeMechanicScenarioOtherProfiles.Fail("persona-evaluation-species-background-missing","Customer evaluation row requires a non-empty authored species background for '"+speciesTag+"'.");
            return background;
        }
        public void Dispose() { if(ActorRoot!=null) Object.DestroyImmediate(ActorRoot); if(Data!=null) Object.DestroyImmediate(Data); }
    }
    // CustomerPersonaRuntime.BuildPersonaPrompt exposes only hunger, sleep, fun, mood,
    // excretion and hygiene. Shopping remains a locked C# mechanic, never a prompt need.
    private static readonly PersonaRow[] Rows =
    {
        new PersonaRow("slime-care","Mirel","Slime","hunger","selfCareMultiplier",1.15f,"Meal"), new PersonaRow("orc-patience","Borun","Orc","mood","patienceMultiplier",1.2f,"Rest"), new PersonaRow("vampire-curiosity","Sable","Vampire","fun","curiosityMultiplier",1.18f,"Luxury"), new PersonaRow("human-shopping","Tarin","Human","fun","shoppingMultiplier",1.25f,"Shop"), new PersonaRow("beastkin-hunger","Riva","Beastkin","hunger","hungerCurveMultiplier",0.85f,"Dining"),
        new PersonaRow("demon-mood","Keth","Demon","mood","moodCurveMultiplier",0.82f,"Ritual"), new PersonaRow("golem-rest","Aster","Golem","sleep","selfCareMultiplier",1.1f,"Security"), new PersonaRow("harpy-fun","Lark","Harpy","fun","funCurveMultiplier",0.8f,"Entertainment"), new PersonaRow("kobold-hygiene","Pip","Kobold","hygiene","patienceMultiplier",0.9f,"Service"), new PersonaRow("myconid-excretion","Moss","Myconid","excretion","selfCareMultiplier",1.05f,"Garden"),
        new PersonaRow("slime-shopping","Cella","Slime","mood","shoppingMultiplier",0.88f,"Shop"), new PersonaRow("human-curiosity","Nilo","Human","sleep","curiosityMultiplier",0.92f,"Research"), new PersonaRow("orc-fun","Doran","Orc","fun","funCurveMultiplier",1.14f,"Combat"), new PersonaRow("harpy-mood","Veya","Harpy","mood","moodCurveMultiplier",1.16f,"Luxury"), new PersonaRow("myconid-patience","Lumen","Myconid","hunger","patienceMultiplier",1.22f,"Dining")
    };
    private static readonly PersonaRow[] UnseenEvaluationRows =
    {
        new PersonaRow("unseen-eval-v1-slime-hygiene","Runnel","Slime","hygiene","selfCareMultiplier",1.18f,"Service","slime-care","need/species/background interaction: authored Slime background with current needs hunger=74, sleep=76, fun=72, mood=70, excretion=78, hygiene=20; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-orc-rest","Vark","Orc","sleep","patienceMultiplier",1.17f,"Security","orc-patience","need/species/background interaction: authored Orc background with current needs hunger=74, sleep=23, fun=72, mood=70, excretion=78, hygiene=79; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-vampire-meal","Nerith","Vampire","hunger","curiosityMultiplier",1.13f,"Research","vampire-curiosity","need/species/background interaction: authored Vampire background with current needs hunger=26, sleep=76, fun=72, mood=70, excretion=78, hygiene=79; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-human-hygiene","Maren","Human","hygiene","shoppingMultiplier",1.19f,"Service","human-shopping","need/species/background interaction: authored Human background with current needs hunger=74, sleep=76, fun=72, mood=70, excretion=78, hygiene=29; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-beastkin-fun","Sori","Beastkin","fun","hungerCurveMultiplier",0.87f,"Entertainment","beastkin-hunger","need/species/background interaction: authored Beastkin background with current needs hunger=74, sleep=76, fun=32, mood=70, excretion=78, hygiene=79; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-demon-sleep","Azra","Demon","sleep","moodCurveMultiplier",0.84f,"Ritual","demon-mood","need/species/background interaction: authored Demon background with current needs hunger=74, sleep=20, fun=72, mood=70, excretion=78, hygiene=79; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-golem-fun","Cairn","Golem","fun","selfCareMultiplier",1.12f,"Maintenance","golem-rest","need/species/background interaction: authored Golem background with current needs hunger=74, sleep=76, fun=23, mood=70, excretion=78, hygiene=79; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-harpy-hunger","Skye","Harpy","hunger","funCurveMultiplier",0.83f,"Entertainment","harpy-fun","need/species/background interaction: authored Harpy background with current needs hunger=26, sleep=76, fun=72, mood=70, excretion=78, hygiene=79; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-kobold-sleep","Tink","Kobold","sleep","patienceMultiplier",0.93f,"Workshop","kobold-hygiene","need/species/background interaction: authored Kobold background with current needs hunger=74, sleep=29, fun=72, mood=70, excretion=78, hygiene=79; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-myconid-mood","Spore","Myconid","mood","selfCareMultiplier",1.08f,"Garden","myconid-excretion","need/species/background interaction: authored Myconid background with current needs hunger=74, sleep=76, fun=72, mood=32, excretion=78, hygiene=79; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-slime-fun","Glob","Slime","fun","shoppingMultiplier",0.91f,"Entertainment","slime-shopping","need/species/background interaction: authored Slime background with current needs hunger=74, sleep=76, fun=20, mood=70, excretion=78, hygiene=79; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-human-hunger","Oren","Human","hunger","curiosityMultiplier",0.96f,"Dining","human-curiosity","need/species/background interaction: authored Human background with current needs hunger=23, sleep=76, fun=72, mood=70, excretion=78, hygiene=79; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-orc-hygiene","Brakka","Orc","hygiene","funCurveMultiplier",1.09f,"Combat","orc-fun","need/species/background interaction: authored Orc background with current needs hunger=74, sleep=76, fun=72, mood=70, excretion=78, hygiene=26; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-harpy-sleep","Zephra","Harpy","sleep","moodCurveMultiplier",1.11f,"Rest","harpy-mood","need/species/background interaction: authored Harpy background with current needs hunger=74, sleep=29, fun=72, mood=70, excretion=78, hygiene=79; prior active/erased state: no individual-history evidence is available, so no biography is claimed."),
        new PersonaRow("unseen-eval-v1-myconid-hygiene","Cap","Myconid","hygiene","patienceMultiplier",1.16f,"Garden","myconid-patience","need/species/background interaction: authored Myconid background with current needs hunger=74, sleep=76, fun=72, mood=70, excretion=78, hygiene=32; prior active/erased state: no individual-history evidence is available, so no biography is claimed.")
    };
    private readonly bool useUnseenNamingEvaluation;
    public NarrativeMechanicScenarioPersonaSource() { }
    public NarrativeMechanicScenarioPersonaSource(NarrativeMechanicScenarioSetKind setKind)
    {
        useUnseenNamingEvaluation=setKind==NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation;
        if(!useUnseenNamingEvaluation&&setKind!=NarrativeMechanicScenarioSetKind.Calibration) throw new ArgumentOutOfRangeException(nameof(setKind));
    }
    public string ProfileId => NarrativeMechanicScenarioProfiles.Persona;
    public NarrativeMechanicScenarioSourceCapture CaptureScenarios()
    {
        PersonaRow[] activeRows=useUnseenNamingEvaluation?UnseenEvaluationRows:Rows;
        if(useUnseenNamingEvaluation) RequireEvaluationNeedContrast(activeRows);
        List<NarrativeMechanicScenario> positives=activeRows.Select(BuildPositive).ToList();
        NarrativeMechanicScenario[] negatives=useUnseenNamingEvaluation?Array.Empty<NarrativeMechanicScenario>():new[]{BuildExtraMechanicsNegative(Rows[0],99)};
        return new NarrativeMechanicScenarioSourceCapture(ProfileId,positives,negatives,new[]{"Assets/Scripts/Services/Character/AI/CustomerPersonaRuntime.cs","Assets/Scripts/Services/Character/AI/LlmJsonResponseParser.cs","Assets/Scripts/Models/AI/Core/V25NarrativeInferenceContracts.cs","Assets/Scripts/Services/Character/AI/NarrativeRequestContext.cs","Assets/Scripts/Services/Character/AI/Editor/CharacterAiPlanDebugFixtures.cs","Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioOtherProfilesSource.cs"},allowEmptyNegativeScenarios:useUnseenNamingEvaluation);
    }
    private static void RequireEvaluationNeedContrast(IReadOnlyList<PersonaRow> evaluationRows)
    {
        for(int ordinal=0;ordinal<(evaluationRows?.Count??0);ordinal++)
        {
            PersonaRow row=evaluationRows[ordinal];
            PersonaRow calibration=Rows.SingleOrDefault(candidate=>candidate!=null&&string.Equals(candidate.Id,row?.NearestCalibrationRowId,StringComparison.Ordinal));
            if(row==null||!row.IsUnseenEvaluation||calibration==null||!string.Equals(row.Species,calibration.Species,StringComparison.Ordinal)) throw NarrativeMechanicScenarioOtherProfiles.Fail("persona-evaluation-calibration-reference-invalid","Unseen Persona rows require a same-species calibration reference.");
            int calibrationOrdinal=Array.IndexOf(Rows,calibration);
            IDictionary<CharacterCondition,float> current=NarrativeMechanicScenarioOtherProfiles.CustomerNeeds(row.NeedFocus,ordinal);
            IDictionary<CharacterCondition,float> baseline=NarrativeMechanicScenarioOtherProfiles.CustomerNeeds(calibration.NeedFocus,calibrationOrdinal);
            if(current.All(pair=>baseline.TryGetValue(pair.Key,out float value)&&Mathf.Approximately(value,pair.Value))) throw NarrativeMechanicScenarioOtherProfiles.Fail("persona-evaluation-visible-needs-unchanged","Unseen Persona row '"+row.Id+"' does not differ from its same-species calibration after identity masking.");
            string needVector=CurrentNeedVectorText(current);
            if(row.EvaluationReason.IndexOf("authored "+row.Species+" background",StringComparison.OrdinalIgnoreCase)<0||row.EvaluationReason.IndexOf("current needs "+needVector,StringComparison.Ordinal)<0||row.EvaluationReason.IndexOf("no individual-history evidence",StringComparison.OrdinalIgnoreCase)<0||row.EvaluationReason.IndexOf(row.MechanicField,StringComparison.OrdinalIgnoreCase)>=0||row.EvaluationReason.IndexOf(row.PreferredFacilityTag,StringComparison.OrdinalIgnoreCase)>=0||row.EvaluationReason.IndexOf("multiplier",StringComparison.OrdinalIgnoreCase)>=0||row.EvaluationReason.IndexOf("facility tag",StringComparison.OrdinalIgnoreCase)>=0) throw NarrativeMechanicScenarioOtherProfiles.Fail("persona-evaluation-reason-not-model-visible","Unseen Persona row '"+row.Id+"' must cite only its public current-needs vector, authored species/background, and missing individual-history evidence.");
        }
    }
    private static string CurrentNeedVectorText(IDictionary<CharacterCondition,float> values) => string.Join(", ",new[]{CharacterCondition.HUNGER,CharacterCondition.SLEEP,CharacterCondition.FUN,CharacterCondition.MOOD,CharacterCondition.EXCRETION,CharacterCondition.HYGIENE}.Select(condition=>NeedLabel(condition)+"="+(values.TryGetValue(condition,out float value)?value:float.NaN).ToString("0.###",CultureInfo.InvariantCulture)));
    private static string NeedLabel(CharacterCondition condition) => condition switch { CharacterCondition.HUNGER=>"hunger",CharacterCondition.SLEEP=>"sleep",CharacterCondition.FUN=>"fun",CharacterCondition.MOOD=>"mood",CharacterCondition.EXCRETION=>"excretion",CharacterCondition.HYGIENE=>"hygiene",_=>throw new ArgumentOutOfRangeException(nameof(condition)) };
    private static NarrativeMechanicScenario BuildPositive(PersonaRow row,int index) { using PersonaFixture fixture=PersonaFixture.Create(row,index); string response=NarrativeMechanicScenarioOtherProfiles.PersonaResponse("Persona "+row.Name,"A controlled customer persona focused on "+row.NeedFocus+".",false); if(!LlmJsonResponseParser.TryParse(LocalLlmRequestProfiles.Persona.Id,response,out CustomerPersonaJsonDto payload,out string error)) throw NarrativeMechanicScenarioOtherProfiles.Fail("persona-scenario-validator-rejected","Customer persona parser rejected '"+row.Id+"': "+error); fixture.ApplyParsedNarrative(payload); return BuildScenario("persona-"+row.Id,fixture,response,true,string.Empty); }
    private static NarrativeMechanicScenario BuildExtraMechanicsNegative(PersonaRow row,int ordinal) { using PersonaFixture fixture=PersonaFixture.Create(row,ordinal); string response=NarrativeMechanicScenarioOtherProfiles.PersonaResponse("Invalid mechanics","The exact response contract must reject this extra field.",true); if(LlmJsonResponseParser.TryParse(LocalLlmRequestProfiles.Persona.Id,response,out CustomerPersonaJsonDto _,out string error)||string.IsNullOrWhiteSpace(error)) throw NarrativeMechanicScenarioOtherProfiles.Fail("persona-scenario-extra-key-accepted","Persona exact-key validation accepted an extra mechanical field."); return BuildScenario("persona-reject-extra-mechanical-key",fixture,response,false,error); }
    private static NarrativeMechanicScenario BuildScenario(string id,PersonaFixture fixture,string response,bool accepted,string failure)
    {
        PersonaRow row=fixture.Row;
        NarrativePublicContextMaterial publicMaterial =
            CustomerPersonaPromptBuilder.BuildPublicMaterial(fixture.Actor);
        NarrativePublicPromptEnvelope envelope =
            CustomerPersonaPromptBuilder.BuildEnvelope(fixture.Actor, publicMaterial);
        NarrativeMechanicScenarioPublicContext publicContext =
            NarrativeMechanicScenarioOtherProfiles.RequirePublicContext(
                id,
                publicMaterial,
                envelope);
        NarrativeMechanicCatalogCanonicalJsonObject authority=NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("characterType",NarrativeMechanicCatalogCanonicalJson.String(CharacterType.Customer.ToString())),NarrativeMechanicScenarioOtherProfiles.P("lockedMechanics",fixture.MechanicsJson()),NarrativeMechanicScenarioOtherProfiles.P("requestPromptHash",NarrativeMechanicCatalogCanonicalJson.String(NarrativeMechanicScenarioOtherProfiles.Hash(envelope.Prompt))),NarrativeMechanicScenarioOtherProfiles.P("runtimeAuthority",NarrativeMechanicCatalogCanonicalJson.String("CustomerPersonaRuntime copies C# mechanics after parsing narrative text")));
        NarrativeMechanicCatalogCanonicalJsonObject input=row.IsUnseenEvaluation?NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("fixtureKind",NarrativeMechanicCatalogCanonicalJson.String("controlled-customer-species-background-persona")),NarrativeMechanicScenarioOtherProfiles.P("needFocus",NarrativeMechanicCatalogCanonicalJson.String(row.NeedFocus)),NarrativeMechanicScenarioOtherProfiles.P("preferredFacilityTag",NarrativeMechanicCatalogCanonicalJson.String(row.PreferredFacilityTag)),NarrativeMechanicScenarioOtherProfiles.P("species",NarrativeMechanicCatalogCanonicalJson.String(row.Species)),NarrativeMechanicScenarioOtherProfiles.P("speciesBackground",NarrativeMechanicCatalogCanonicalJson.String(fixture.SpeciesBackground))):NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("fixtureKind",NarrativeMechanicCatalogCanonicalJson.String("controlled-customer-persona")),NarrativeMechanicScenarioOtherProfiles.P("needFocus",NarrativeMechanicCatalogCanonicalJson.String(row.NeedFocus)),NarrativeMechanicScenarioOtherProfiles.P("preferredFacilityTag",NarrativeMechanicCatalogCanonicalJson.String(row.PreferredFacilityTag)),NarrativeMechanicScenarioOtherProfiles.P("species",NarrativeMechanicCatalogCanonicalJson.String(row.Species)));
        return new NarrativeMechanicScenario(id,NarrativeMechanicScenarioProfiles.Persona,NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("prompt",NarrativeMechanicCatalogCanonicalJson.String(envelope.Prompt)),NarrativeMechanicScenarioOtherProfiles.P("responseContract",NarrativeMechanicScenarioOtherProfiles.StringArray(new[]{"personaName","flavorText"})),NarrativeMechanicScenarioOtherProfiles.P("responseJson",NarrativeMechanicCatalogCanonicalJson.String(response))),NarrativeMechanicCatalogCanonicalJson.Array(NarrativeMechanicCatalogCanonicalJson.Object(NarrativeMechanicScenarioOtherProfiles.P("candidateId",NarrativeMechanicCatalogCanonicalJson.String("schema:CustomerPersonaJsonDto")),NarrativeMechanicScenarioOtherProfiles.P("candidateIndex",NarrativeMechanicCatalogCanonicalJson.Integer(0)),NarrativeMechanicScenarioOtherProfiles.P("responseKeys",NarrativeMechanicScenarioOtherProfiles.StringArray(new[]{"personaName","flavorText"})))),publicContext.TargetPersistentId,publicContext,fixture.EffectDescriptions(),authority,input,"CustomerPersonaRuntime.RequestPersonaIfNeeded + CustomerPersonaPromptBuilder.BuildPublicMaterial","LlmJsonResponseParser.TryParse<CustomerPersonaJsonDto> + CustomerPersonaRuntime.ApplyGeneratedPersona",accepted,failure,new[]{new KeyValuePair<string,string>("species",row.Species),new KeyValuePair<string,string>("needFocus",row.NeedFocus),new KeyValuePair<string,string>("lockedMechanic",row.MechanicField)},evaluationMetadata:row.IsUnseenEvaluation?new NarrativeMechanicScenarioEvaluationMetadata("unseen-eval-v1:persona:"+row.Id,"unseen-evaluation","persona-"+row.NearestCalibrationRowId,"distinct-context",row.EvaluationReason):null);
    }
}
#endif
