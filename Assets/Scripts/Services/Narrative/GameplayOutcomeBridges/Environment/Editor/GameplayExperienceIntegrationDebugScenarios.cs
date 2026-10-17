using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Controlled ledger contracts; actual owner execution is supplied by E09.</summary>
public static class GameplayExperienceIntegrationDebugScenarios
{
    private const string ActorId = "character:experience-integration";

    public static int RunLedgerContracts()
    {
        var josa = new KoreanJosaFormatter();
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[] { new EnvironmentalExposureThresholdOutcomeDescriptor(josa) },
            new IGameplayOutcomeAdapterRegistration[] { new EnvironmentalExposureThresholdOutcomeAdapter() });
        var ledger = NewLedger(registry, "run:experience-integration");
        var bridge = new EnvironmentGameplayOutcomeBridge(
            new GameplayOutcomeRecorder(ledger, registry, new GameEventBus()), ledger, ledger);
        // The oldest event must survive selection even beyond the old top-100 and
        // the new 200-row page boundary. These are explicit receipt fixtures.
        for (int index = 0; index < 205; index++)
        {
            float before = index == 0 ? 100f : 87f;
            float after = index == 0 ? 40f : 86f;
            var data = new EnvironmentalExposureOutcomeSaveData
            {
                operationId = "environment-exposure:experience-test:" + index,
                eventKind = "damage-tick", characterId = ActorId,
                characterDisplayName = "경험 검증 주민", x = 3, y = 1,
                absoluteDay = 11, ownerRevision = index + 1,
                temperatureC = -12f, airQuality = 82f, lightLevel = 44f,
                coldBefore = 76f, coldAfter = 78f,
                bandBefore = EnvironmentalExposureBand.Critical,
                bandAfter = EnvironmentalExposureBand.Critical,
                damage = before - after, healthBefore = before, healthAfter = after,
                reasonCode = "experience-controlled-fixture"
            };
            var receipt = EnvironmentConditionOutcomeReceiptFactory.CreateEnvironmentalExposureThreshold(data);
            Require(bridge.TryPrepare(receipt, out var prepared, out string failure), failure);
            Require(bridge.Commit(prepared, data.ownerRevision).CanClearOwnerPending,
                "Controlled receipt was not committed.");
        }
        var subject = new GameplayEntityId(EnvironmentOutcomeIds.CharacterKind, ActorId);
        var query = new GameplayOutcomeNarrativeEvidenceQuery(ledger);
        string ledgerBefore = JsonUtility.ToJson(ledger.CaptureGameplayOutcomes());
        var episodes = query.GetExperiencesForEntity(subject, false);
        Require(episodes.Count == 205, "Discontinuous health was incorrectly joined or a page was omitted.");
        var selected = query.GetForEntity(subject, 4, 0f, false);
        var strongest = GameplayOutcomeEvidenceFormulaProjection.Capture(new[] { selected[0] })[0];
        Require(strongest.outcomeSequence == 1 && strongest.experience.importance == .80f,
            "Older intense experience was lost behind routine/raw-salience rows.");
        Require(query.GetForEntity(subject, 100, .8f, false).Count == 1,
            "Selection threshold still uses raw salience instead of experience importance.");
        Require(ledgerBefore == JsonUtility.ToJson(ledger.CaptureGameplayOutcomes()),
            "Read-side experience projection mutated the ledger.");
        Require(selected.Select(value => value.SourceId).SequenceEqual(
            query.GetForEntity(subject, 4, 0f, false).Select(value => value.SourceId)),
            "Repeated selection was not deterministic.");
        Require(GameplayOutcomeEvidenceBindingAuthority.TryValidate(strongest, out string invalid), invalid);
        string frozenHash = GameplayOutcomeEvidenceBindingAuthority.ComputeCanonicalHash(strongest);
        var restoredBinding = JsonUtility.FromJson<GameplayOutcomeEvidenceBindingSnapshot>(JsonUtility.ToJson(strongest));
        Require(GameplayOutcomeEvidenceBindingAuthority.ComputeCanonicalHash(restoredBinding) == frozenHash,
            "Frozen assessment hash did not survive serialization.");
        restoredBinding.experience.importance = .95f;
        Require(!GameplayOutcomeEvidenceBindingAuthority.TryValidate(restoredBinding, out _),
            "Tampered experience importance was admitted.");
        restoredBinding = strongest.Clone();
        restoredBinding.experience.policyVersion++;
        Require(!GameplayOutcomeEvidenceBindingAuthority.TryValidate(restoredBinding, out _),
            "Unsupported policy version was silently reinterpreted.");
        restoredBinding = strongest.Clone();
        restoredBinding.experience.explanationKo = "위조된 판단 이유";
        Require(!GameplayOutcomeEvidenceBindingAuthority.TryValidate(restoredBinding, out _),
            "Tampered judgment explanation was admitted.");
        restoredBinding = strongest.Clone();
        restoredBinding.experience.evidenceFactIds.Add("unobserved-fact");
        Require(!GameplayOutcomeEvidenceBindingAuthority.TryValidate(restoredBinding, out _),
            "Tampered judgment evidence was admitted.");
        // Reuse a fully validated typed binding envelope, not the minimal rule-only
        // fixture which deliberately omits public projection/name metadata.
        var technical = strongest.Clone();
        technical.outcomeTypeId = "work.emergency-suspension-recorded";
        technical.status = (int)GameplayOutcomeStatus.Failed;
        technical.canonicalFactText = "통제 검사: 작업 중단 신원 처리 실패";
        technical.experience = GameplayExperienceObserverRegistration.Default.Observe(technical);
        var nonzeroFloor = new NarrativeFormulaStrengthPolicy(1, 0, 1, 1, 1, 4, new[] { 1d });
        Require(GameplayOutcomeEvidenceFormulaProjection.ToFormulaEvidence(technical, nonzeroFloor).ImportancePoints == 0,
            "Technical context gained importance from a formula floor.");
        Require(!GameplayOutcomeEvidenceFormulaProjection.IsNegative(technical),
            "Technical failure became a negative personal experience.");
        Require(Math.Abs(GameplayOutcomeEvidenceFormulaProjection.ToFormulaEvidence(strongest, null)
                .ImportancePoints - 3.2d) < .00001d,
            "New importance contribution saturated or retained the legacy multiplier.");
        var mild = GameplayOutcomeEvidenceFormulaProjection.Capture(new[] { selected[1] })[0];
        Require(Math.Abs(GameplayOutcomeEvidenceFormulaProjection.ToFormulaEvidence(mild, null)
                .ImportancePoints - 1.2d) < .00001d, "Mild formula importance differs.");
        var material = GameplayOutcomeEvidencePublicMaterialComposer.BuildExactEvidenceOnly(
            "CharacterSkill", ActorId, NarrativePublicSubjectKind.Character, "경험 검증 주민",
            "human", false, false, new[] { strongest, mild }, "experience-policy-v2");
        Require(material.PublicFacts.Any(value => value.Text.Contains("강렬한 경험"))
            && material.PublicFacts.Any(value => value.Text.Contains("가벼운 경험")),
            "Model public facts did not carry experience classification.");

        var restored = NewLedger(registry, "run:experience-restore");
        var candidate = restored.PrepareGameplayOutcomeRestore(
            JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(ledgerBefore));
        restored.BeginRestoreCandidate();
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        var restoredQuery = new GameplayOutcomeNarrativeEvidenceQuery(restored);
        var restoredSelection = restoredQuery.GetForEntity(subject, 4, 0f, false);
        Require(selected.Select(value => value.SourceId).SequenceEqual(
            restoredSelection.Select(value => value.SourceId)), "Ledger restore changed selected fact identities.");
        Require(restoredQuery.TryResolveExactBinding(strongest, out _),
            "Frozen experience no longer resolves to the restored ledger.");
        Require(GameplayOutcomeEvidenceBindingAuthority.ComputeCanonicalHash(
                GameplayOutcomeEvidenceFormulaProjection.Capture(new[] { restoredSelection[0] })[0]) == frozenHash,
            "Ledger restore changed the frozen experience assessment.");
        return 19;
    }

    public static string ObserveLive(GameplayOutcomeLedger ledger, string actorId,
        JArray observations, string stage)
    {
        var query = new GameplayOutcomeNarrativeEvidenceQuery(ledger);
        var subject = new GameplayEntityId(EnvironmentOutcomeIds.CharacterKind, actorId);
        string before = JsonUtility.ToJson(ledger.CaptureGameplayOutcomes());
        var selected = query.GetForEntity(subject, 8, 0f, false);
        var bindings = GameplayOutcomeEvidenceFormulaProjection.Capture(selected);
        var episodes = query.GetExperiencesForEntity(subject, false);
        Require(episodes.Count == 1 && episodes[0].Members.Count == 2,
            "Actual continuous band/damage observations did not form one two-fact experience.");
        Require(bindings.Count == 1, "Same experience was counted more than once in generation input.");
        var damage = bindings.Single(value => value.metrics.Any(metric =>
            metric.metricId == EnvironmentOutcomeIds.DamageMetric.Value && metric.value > 0));
        Require(damage.subjectSalience == .88f && damage.experience.importance == .30f
            && damage.experience.category == (int)GameplayExperienceCategory.Mild,
            "Actual minor environmental damage was inflated or raw receipt salience was overwritten.");
        Require(bindings.All(value => GameplayOutcomeEvidenceBindingAuthority.TryValidate(value, out _)
            && query.TryResolveExactBinding(value, out _)), "Actual experience lost its exact ledger identity.");
        Require(before == JsonUtility.ToJson(ledger.CaptureGameplayOutcomes()),
            "Actual ledger changed during experience observation.");
        var payload = new JArray(bindings.Select(value => JObject.Parse(JsonUtility.ToJson(value))));
        observations?.Add(new JObject
        {
            ["stage"] = stage, ["kind"] = "actual-owner-experience-projection",
            ["testPreparationExcludedFromTraining"] = true, ["naturalGameplayClaimed"] = false,
            ["bindings"] = payload, ["experienceId"] = episodes[0].ExperienceId,
            ["memberFactIds"] = new JArray(episodes[0].Members.Select(value => value.SourceId))
        });
        return string.Join("|", bindings.Select(GameplayOutcomeEvidenceBindingAuthority.ComputeCanonicalHash));
    }

    private static GameplayOutcomeLedger NewLedger(GameplayOutcomeRegistry registry, string runId) =>
        new(registry, GameplayOutcomeBufferLimits.Default, new GameplayOutcomeRunId(runId), 1L);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Experience integration: " + message);
    }
}
