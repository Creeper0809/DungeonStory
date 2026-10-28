#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class CharacterProgressionDebugScenarios
{
    public static void VerifyOwnerFixedSkillReferenceRestore()
    {
        using ActorFixture fixture = new ActorFixture(81207, "OwnerReference", "Slime");
        using var outcomes = new CharacterSkillActivationEditorFixture(
            "run:n07-owner-reference", calendar: EditorFixedGameCalendar.Instance);
        CharacterActor actor = fixture.Actor;
        actor.Progression.ConstructSkillActivationRuntime(outcomes.Transaction);
        CharacterSO definition = actor.Identity.Data;
        definition.role = CharacterRole.Owner;
        definition.species = null;
        var field = typeof(CharacterSO).GetField("ownerFixedSkills",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Require(field != null, "Owner skill authoring field is missing.");
        string originalJson = EditorJsonUtility.ToJson(definition);
        CharacterSkillInstance skill = actor.Progression.OwnerFixedSkills[0].Clone();
        skill.id = "qa:owner-reference";
        skill.displayName = "Owner reference execution";
        skill.modules.Add(Module("mood", "small"));
        field.SetValue(definition, new[] { skill });
        CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(actor.Progression).passiveSkills.Clear();
        string authoredJson = EditorJsonUtility.ToJson(definition);
        float moodBefore = actor.Stats.Mood;
        const string eventId = "qa:owner-reference:work-started";
        CharacterSkillRuntimeEffects.BeginWork(actor, null, BuiltInWorkTypeIds.Operate, eventId);
        Require(actor.Stats.Mood > moodBefore
                && Mathf.Approximately(CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(actor), 1.1f),
            "Owner fixed skill did not reach actual mood and work-speed consumers.");
        CharacterProgressionSnapshot snapshot = actor.Progression.CapturePersistentState();
        var saved = new PreparedStartPartyMemberSnapshot
        {
            isOwner = true, persistentId = actor.Identity.PersistentId, level = snapshot.Level,
            currentExperience = snapshot.CurrentExperience,
            growth = snapshot.GrowthState, narrative = snapshot.NarrativeLedger
        };
        string json = JsonUtility.ToJson(saved);
        var restored = JsonUtility.FromJson<PreparedStartPartyMemberSnapshot>(json);
        Require(!json.Contains("ownerFixedSkills", StringComparison.Ordinal),
            "Immutable owner definitions were duplicated into the save DTO.");
        string stateBefore = JsonUtility.ToJson(actor.Progression.GrowthState);
        void RejectRestore(string label)
        {
            bool rejected = false;
            try { actor.Progression.RestorePersistentState(restored.ToProgressionSnapshot()); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Owner restore accepted " + label);
            Require(JsonUtility.ToJson(actor.Progression.GrowthState) == stateBefore,
                "Rejected owner restore partially mutated growth: " + label);
        }
        skill.modules[0].moduleId = "missing-owner-module";
        RejectRestore("an unknown module");
        skill.modules[0].moduleId = "work_speed";
        skill.modules[0].variantId = "missing-owner-variant";
        RejectRestore("an unknown variant");
        skill.modules[0].variantId = "small";
        skill.formulaVersion = 1;
        RejectRestore("an incomplete frozen formula envelope");
        skill.formulaVersion = 0;
        actor.Progression.RestorePersistentState(restored.ToProgressionSnapshot());
        float moodAfter = actor.Stats.Mood;
        long revision = actor.Progression.GrowthState.useLimits.activationSequence;
        int receipts = actor.Progression.GrowthState.useLimits.automaticSkillActivations.Count;
        Require(receipts > 0, "The actual automatic owner activation produced no durable receipt.");
        CharacterSkillRuntimeEffects.BeginWork(actor, null, BuiltInWorkTypeIds.Operate, eventId);
        Require(Mathf.Approximately(actor.Stats.Mood, moodAfter)
                && actor.Progression.GrowthState.useLimits.activationSequence == revision
                && actor.Progression.GrowthState.useLimits.automaticSkillActivations.Count == receipts,
            "Retry after owner save/restore repeated the automatic effect or receipt.");
        Require(actor.Progression.OwnerFixedSkills.Count == 4
                && actor.Progression.OwnerFixedSkills[0].id == skill.id
                && Mathf.Approximately(CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(actor), 1.1f),
            "Owner identity, fallback slots or work-speed changed after restore/retry.");
        Require(EditorJsonUtility.ToJson(definition) == authoredJson,
            "Owner execution/restore mutated the authored SO.");
        CharacterSkillRuntimeEffects.EndWork(actor);
        EditorJsonUtility.FromJsonOverwrite(originalJson, definition);
        Debug.Log("N07_OWNER_FIXED_REFERENCE=PASS; actual-work-mood; legacy-json; reject-unchanged; repair-restore; retry-no-duplicate; source-unchanged");
    }

    // Controlled projection regression using immutable historical inputs. This
    // is not a repair of that saved request or a fresh gameplay capture.
    public static string VerifySkillPublicNarrativeEvidence()
    {
        const string source = "Artifacts/QA/AuditRemediation-20260926-r1/u6-source-v9-skill-trait-r598-r1/unity-evidence/source-v9/selection/skill/";
        string ReadPinned(string name, string hash)
        {
            byte[] bytes = System.IO.File.ReadAllBytes(source + name);
            using var sha = System.Security.Cryptography.SHA256.Create();
            Require(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() == hash,
                "Historical skill projection input changed: " + name);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        string pendingJson = ReadPinned("pending.json", "a3393360a16ba1e87a53553557e2f85e93d43a0ca60c644d0e3ad0699650dd63");
        var historical = JsonUtility.FromJson<CharacterSkillDraft>(pendingJson);
        var world = Newtonsoft.Json.Linq.JObject.Parse(ReadPinned("world.json",
            "ad3e4eda4a82d9ea6c554383725d29eda6146e7781ef160b9bc6c33b2a0d5d71"));
        var section = world["sections"].Single(value => (string)value["sectionId"] == "characters.world");
        var owner = Newtonsoft.Json.Linq.JObject.Parse((string)section["payloadJson"])["actors"]
            .Single(value => (string)value["persistentId"] == historical.ownerBinding.persistentId);
        using ActorFixture fixture = new(913900, "Roma", "slime",
            persistentId: historical.ownerBinding.persistentId);
        CharacterProgression progression = fixture.Actor.Progression;
        JsonUtility.FromJsonOverwrite(owner["narrative"].ToString(),
            CharacterAiEditorTestDependencies.GetOwnedNarrativeForFixture(progression));
        CharacterSkillDraft draft = historical.Clone();
        draft.requestContext = null;
        draft.requestContextHash = string.Empty;
        draft.requestContextVersion = 0;
        NarrativePublicContextMaterial material = CharacterSkillPromptBuilder.BuildModuleSelectionPublicMaterial(
            NarrativeRequestContextTestFixture.CreateEmpty(), progression, draft);
        string[] required = draft.moduleSelectionOffers.SelectMany(offer => offer.evidenceFactIds)
            .Where(id => id.StartsWith("skill-evidence:", StringComparison.Ordinal)).Distinct().ToArray();
        Require(required.Length == 7 && draft.outcomeEvidenceBindings.Count == 14,
            "Historical regression does not reproduce seven narrative plus fourteen exact offers.");
        Require(required.All(id => material.PublicFacts.Any(fact =>
                CharacterSkillFormulaGeneration.BuildEvidenceId(new CharacterNarrativeFact
                {
                    domain = Enum.TryParse(fact.Domain, out CharacterNarrativeDomain domain) ? domain : (CharacterNarrativeDomain)(-1),
                    factId = fact.OriginalFactId, subjectId = fact.SourceSubjectId
                }) == id)), "Required narrative tuples were dropped by public selection.");
        void Reject(Action action, string reason)
        {
            bool rejected = false;
            try { action(); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, reason);
        }
        Reject(() => CharacterSkillPromptBuilder.BuildEnvelope(progression, historical,
            progression.SkillSettings, historical.RestoreRequestPublicContext(), batchModuleSelection: true),
            "Historical incomplete frozen context was silently repaired.");
        draft.requestContext = CharacterSkillRequestContextSnapshot.Capture(progression, draft, material);
        draft.requestContextHash = draft.requestContext.ComputeHash(draft.requestKey);
        draft.requestContextVersion = 1;
        string prompt = CharacterSkillPromptBuilder.BuildEnvelope(progression, draft,
            progression.SkillSettings, draft.RestoreRequestPublicContext(), batchModuleSelection: true).Prompt;
        var restored = JsonUtility.FromJson<CharacterSkillDraft>(JsonUtility.ToJson(draft));
        // Fresh snapshot projection, independent of mutable live narrative text.
        CharacterAiEditorTestDependencies.GetOwnedNarrativeForFixture(progression).facts.Clear();
        string roundTrip = CharacterSkillPromptBuilder.BuildEnvelope(progression, restored,
            progression.SkillSettings, restored.RestoreRequestPublicContext(), batchModuleSelection: true).Prompt;
        Require(prompt == roundTrip && JsonUtility.ToJson(historical) == JsonUtility.ToJson(
                JsonUtility.FromJson<CharacterSkillDraft>(pendingJson)),
            "Frozen narrative evidence changed after JSON restoration or mutated historical input.");
        foreach (int index in Enumerable.Range(0, 25))
            CharacterAiEditorTestDependencies.GetOwnedNarrativeForFixture(progression).facts.Add(new CharacterNarrativeFact
            {
                domain = CharacterNarrativeDomain.Work, factId = "work:cap-test",
                subjectId = "facility:cap-" + index, outcome = "completed", count = 1, milestoneCount = 1
            });
        var overCap = new CharacterSkillDraft
        {
            moduleSelectionOffers = new List<CharacterSkillModuleOfferState>
            {
                new() { evidenceFactIds = progression.NarrativeLedger.Facts
                    .Select(CharacterSkillFormulaGeneration.BuildEvidenceId).ToList() }
            }
        };
        Reject(() => CharacterSkillPromptBuilder.BuildModuleSelectionPublicMaterial(NarrativeRequestContextTestFixture.CreateEmpty(), progression, overCap),
            "Public evidence cap silently dropped exact target tuples.");
        Debug.Log("B139_SKILL_PUBLIC_SELECTION=PASS; narrative=7; exact=14; frozen-json=1; old-incomplete-rejected=1; cap-rejected=1; model-calls=0");
        return prompt;
    }

    // The exact binding was captured from three real core receipt publications.
    // This tests the production budget path, not natural gameplay or LLM output.
    public static void VerifyMixedLegacyAggregateBudget(
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings,
        IReadOnlyList<string> operationIds,
        GameplayOutcomeEvidenceBindingSnapshot compacted)
    {
        Require(bindings?.Count == 3 && operationIds?.Count == 3,
            "Mixed-origin inputs require three actual event bindings.");
        GameplayOutcomeEvidenceBindingSnapshot latest = bindings.Single(
            value => value.operationId == operationIds[2]);
        Require(latest != null && operationIds?.Count == 3
            && latest.operationId == operationIds[2], "Mixed-origin inputs must bind the third exact event.");
        using ActorFixture fixture = new(812590, "합산 검증자", "human",
            persistentId: latest.subjectId);
        CharacterProgression progression = fixture.Actor.Progression;
        const string factId = "work:mixed-origin-repeat";
        void Record(int index) => progression.RecordNarrative(
            CharacterNarrativeDomain.Work, factId, "facility:mixed-origin", "completed",
            value: 1f, day: index + 1, triggerPassives: false,
            eventContext: new GameplayNarrativeEventContext
            {
                eventInstanceId = operationIds[index], chainId = "work:mixed-origin-chain",
                actorId = latest.subjectId, occurredDay = index + 1
            });
        Record(0);
        Record(1);
        int beforeRedundantLegacy = CharacterSkillFormulaGeneration.CalculateNarrativeBudget(
            progression, progression.SkillSettings, new[] { latest });
        LoadAuthoredAcquiredTraitContent(out var traitSettings, out var traitModules);
        string legacyPublicId = CharacterAcquiredTraitEvidenceProjection.Project(
            progression.NarrativeLedger.Facts.Single(value => value.factId == factId));
        CharacterAcquiredTraitPendingRequestState PrepareTrait() =>
            CharacterAcquiredTraitFormulaGeneration.PreparePendingModuleSelection(
                progression, "request:mixed-origin", "trait:mixed-origin", 1,
                traitSettings, traitModules, new[] { legacyPublicId, latest.publicFactId },
                new[] { latest });
        CharacterAcquiredTraitPendingRequestState traitBefore = PrepareTrait();
        string before = JsonUtility.ToJson(progression.NarrativeLedger);
        Record(2);
        int afterRedundantLegacy = CharacterSkillFormulaGeneration.CalculateNarrativeBudget(
            progression, progression.SkillSettings, new[] { latest });
        CharacterNarrativeFact aggregate = progression.NarrativeLedger.Facts.Single(
            value => value.factId == factId);
        Require(aggregate.count == 3 && aggregate.milestoneCount == 2,
            "Three distinct actions must remain recorded in the authoritative legacy aggregate.");
        var observation = new Newtonsoft.Json.Linq.JObject
        {
            ["test"] = "mixed-legacy-aggregate-budget",
            ["operationIds"] = new Newtonsoft.Json.Linq.JArray(operationIds),
            ["exactPublicFactId"] = latest.publicFactId,
            ["exactOperationId"] = latest.operationId,
            ["budgetBeforeRedundantLegacy"] = beforeRedundantLegacy,
            ["budgetAfterRedundantLegacy"] = afterRedundantLegacy,
            ["beforeLedgerJson"] = before,
            ["afterLedgerJson"] = JsonUtility.ToJson(progression.NarrativeLedger),
            ["expectation"] = "A+B legacy and C exact must have the same budget after adding only C's redundant legacy representation.",
            ["passed"] = beforeRedundantLegacy == afterRedundantLegacy,
            ["trainingEligible"] = false
        };
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            string path = System.IO.Path.Combine(output, "mixed-legacy-aggregate-budget.json");
            using var stream = new System.IO.FileStream(path, System.IO.FileMode.CreateNew,
                System.IO.FileAccess.Write, System.IO.FileShare.Read);
            using var writer = new System.IO.StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(observation.ToString());
        }
        Require(beforeRedundantLegacy == afterRedundantLegacy,
            "MIXED_LEGACY_AGGREGATE_BUDGET_CHANGED: adding only the redundant third event changed budget "
            + beforeRedundantLegacy + " -> " + afterRedundantLegacy
            + "; distinct first and second events must not disappear or be double-counted.");
        Debug.Log("PASS MIXED_LEGACY_AGGREGATE_BUDGET_PRESERVES_DISTINCT_SOURCES");

        Require(aggregate.sourceEventIds.SequenceEqual(operationIds),
            "The authority must preserve each occurrence identity in recording order.");
        string authoritativeJson = JsonUtility.ToJson(progression.NarrativeLedger);
        int Remaining(IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> exact) =>
            GameplayOutcomeEvidenceFormulaProjection.GetRemainingLegacyMilestoneCounts(
                new[] { aggregate }, exact).TryGetValue(aggregate, out int value) ? value : 0;
        GameplayOutcomeEvidenceBindingSnapshot first = bindings.Single(
            value => value.operationId == operationIds[0]);
        Require(Remaining(Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>()) == 2
            && Remaining(new[] { first }) == 1 && Remaining(new[] { latest }) == 1
            && Remaining(new[] { first, first }) == 1
            && Remaining(bindings) == 0 && Remaining(new[] { compacted }) == 0,
            "Partial/full/no overlap, non-last origins and compacted receipt aliases must use residual milestones.");
        Require(CharacterAcquiredTraitEvidenceWindow.SelectLegacyFactIds(
                progression.NarrativeLedger, new[] { latest }).Contains(factId)
            && CharacterAcquiredTraitEvidenceWindow.SelectLegacyFactIds(
                progression.NarrativeLedger, bindings).Count == 0,
            "Trait evidence window must keep a partial aggregate but exclude a fully represented one.");

        CharacterAcquiredTraitPendingRequestState traitAfter = PrepareTrait();
        Require(traitBefore.narrativeBudget == traitAfter.narrativeBudget,
            "Redundant legacy representation changed the actual trait module-selection budget.");
        var frozen = JsonUtility.FromJson<CharacterAcquiredTraitPendingRequestState>(
            JsonUtility.ToJson(traitAfter));
        Require(CharacterAcquiredTraitFormulaGeneration.HasValidModuleSelectionLocalFacts(frozen)
            && CharacterAcquiredTraitFormulaGeneration.RebuildFrozenModuleSelection(
                progression, frozen, traitSettings, traitModules).SelectionId == traitAfter.moduleSelectionId,
            "Frozen trait evidence failed its ordinary JSON/packet rebuild.");
        frozen.moduleSelectionLocalFacts.Single().sourceEventIds[0] = "operation:tampered";
        Require(!CharacterAcquiredTraitFormulaGeneration.HasValidModuleSelectionLocalFacts(frozen),
            "Frozen trait packet accepted a changed historical origin with unchanged last context.");

        CharacterNarrativeLedger clone = progression.NarrativeLedger.Clone();
        clone.facts.Single().sourceEventIds[0] = "operation:clone-only";
        Require(JsonUtility.ToJson(progression.NarrativeLedger) == authoritativeJson,
            "A cloned origin list mutated the authoritative narrative ledger.");
        CharacterNarrativeLedger roundTrip = JsonUtility.FromJson<CharacterNarrativeLedger>(authoritativeJson);
        Require(JsonUtility.ToJson(roundTrip) == authoritativeJson
            && CharacterAcquiredTraitMeaningfulLedgerValidator.TryValidate(roundTrip, out _),
            "Narrative occurrence identities failed JSON round-trip or normal validation.");
        CharacterProgressionSnapshot snapshot = progression.CapturePersistentState();
        using ActorFixture restored = new(812591, "합산 복원 검증자", "human",
            persistentId: latest.subjectId, detachedRestore: true);
        restored.Actor.Progression.RestorePersistentState(new CharacterProgressionSnapshot(
            snapshot.Level, snapshot.CurrentExperience, snapshot.GrowthState, roundTrip,
            snapshot.AcquiredTraitState));
        Require(JsonUtility.ToJson(restored.Actor.Progression.NarrativeLedger) == authoritativeJson
            && CharacterSkillFormulaGeneration.CalculateNarrativeBudget(restored.Actor.Progression,
                restored.Actor.Progression.SkillSettings, new[] { latest }) == afterRedundantLegacy,
            "Ordinary progression restore changed origins or the actual skill budget.");

        void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, message);
        }
        foreach (int corruption in Enumerable.Range(0, 3))
        {
            CharacterNarrativeLedger invalid = progression.NarrativeLedger.Clone();
            if (corruption == 0) invalid.facts.Single().sourceEventIds = null;
            else if (corruption == 1) invalid.facts.Single().sourceEventIds.RemoveAt(0);
            else invalid.facts.Single().sourceEventIds[0] = null;
            Require(!CharacterAcquiredTraitMeaningfulLedgerValidator.TryValidate(invalid, out _),
                "Normal save validation accepted incomplete occurrence identities.");
            Reject(() => GameplayOutcomeEvidenceFormulaProjection.GetRemainingLegacyMilestoneCounts(
                invalid.Facts, new[] { latest }), "Budget query accepted incomplete occurrence identities.");
            string restoredBefore = JsonConvert.SerializeObject(restored.Actor.Progression.CapturePersistentState());
            Reject(() => restored.Actor.Progression.RestorePersistentState(new CharacterProgressionSnapshot(
                snapshot.Level, snapshot.CurrentExperience, snapshot.GrowthState, invalid,
                snapshot.AcquiredTraitState)), "Ordinary restore accepted incomplete occurrence identities.");
            Require(JsonConvert.SerializeObject(restored.Actor.Progression.CapturePersistentState()) == restoredBefore,
                "Rejected origin restore partially changed the target progression.");
            if (corruption < 2)
            {
                string invalidBefore = JsonUtility.ToJson(invalid);
                Reject(() => invalid.Record(CharacterNarrativeDomain.Work, factId,
                    "facility:mixed-origin", "completed", 1f, 4),
                    "Record silently repaired incomplete historical occurrence identities.");
                Require(JsonUtility.ToJson(invalid) == invalidBefore,
                    "Rejected origin append partially changed the aggregate.");
            }
        }
        CharacterNarrativeLedger unkeyed = new();
        unkeyed.Record(CharacterNarrativeDomain.Work, "work:unkeyed", "subject:unkeyed", "completed");
        Require(unkeyed.Facts.Single().sourceEventIds.SequenceEqual(new[] { string.Empty })
            && GameplayOutcomeEvidenceFormulaProjection.GetRemainingLegacyMilestoneCounts(
                unkeyed.Facts, bindings).Values.Single() == 1,
            "Explicitly unkeyed occurrences must remain, without inventing historical event IDs.");
        Require(JsonUtility.ToJson(progression.NarrativeLedger) == authoritativeJson,
            "Read-only origin projections changed the source ledger.");
        Debug.Log("PASS MIXED_LEGACY_ORIGIN_BOUNDARIES_SAVE_RESTORE_AND_TRAIT_BUDGET"
            + " skill=" + beforeRedundantLegacy + "->" + afterRedundantLegacy
            + " trait=" + traitBefore.narrativeBudget + "->" + traitAfter.narrativeBudget);
    }

    // Replays only the immutable bad native response through RequestDraft/Tick
    // and the production callback. The actor is an explicit controlled fixture,
    // not a restored whole world, natural play, approval, or a new model answer.
    public static Newtonsoft.Json.Linq.JObject ReplayOriginalNativeSkillOwner(
        CharacterSkillDraft original, CharacterProgressionSnapshot originalOwner,
        CharacterLifecycleState savedLifecycleState, long savedLifecycleRevision,
        string originalPrompt, string actualResponse, bool captureCanonicalRetry = false)
    {
        Require(original?.ownerBinding != null && original.requestContext?.version == 1
                && original.requestContext.characterName == "Miru"
                && original.requestKey == "skill:character:staff:1:02:Active:5:r1:g1:l1"
                && !original.isReady && !original.permanentlyChosen
                && original.nextPresentationIndex == 0 && original.candidates.Count == 0,
            "Original native replay request is not the unchanged pending Miru draft.");
        string originalDraftJson = JsonUtility.ToJson(original);
        string originalContextHash = original.requestContextHash;
        string snapshotDraftJson = JsonUtility.ToJson(originalOwner.GrowthState.drafts.Single(
            value => value.requestKey == original.requestKey));
        Require(savedLifecycleRevision == original.ownerBinding.lifecycleRevision,
            "Original checkpoint and pending owner lifecycle revisions differ.");
        CharacterSkillSystemSettingsSO settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        using ActorFixture fixture = new ActorFixture(
            90515, original.requestContext.characterName, "slime",
            persistentId: original.ownerBinding.persistentId, detachedRestore: true);
        CharacterSkillGenerationService service = null;
        try
        {
            CharacterProgression progression = fixture.Actor.Progression;
            var provider = new TestSettingsProvider(settings);
            var runtime = new OriginalNativeReplayRuntime(actualResponse, captureCanonicalRetry);
            var uiClock = new MutableUiClock();
            service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), provider, new TestLlmRuntimeProvider(runtime), uiClock);
            progression.ConstructCharacterProgression(
                service, provider, gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(new ResourceGameContentCatalog(new UnityGameContentRootLoader()))));
            long fixtureLifecycleBeforeRestore = fixture.Actor.Lifecycle.LifecycleRevision;
            fixture.Actor.Lifecycle.RestorePersistentState(savedLifecycleState, savedLifecycleRevision);
            progression.RestorePersistentState(originalOwner);
            fixture.Actor.PublishDetachedRestore();
            CharacterSkillDraft draft = CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.Single(
                value => value.requestKey == original.requestKey);
            string restoredDraftJson = JsonUtility.ToJson(draft);
            bool restoredDraftExact = restoredDraftJson == originalDraftJson;
            bool restoredOwnerExact = original.ownerBinding.Matches(progression, out _);
            if (!restoredDraftExact || !restoredOwnerExact)
            {
                string diagnostic = "Selected owner progression restore changed the original pending request or binding."
                    + " draftExact=" + restoredDraftExact + "; ownerExact=" + restoredOwnerExact
                    + "; originalLifecycle=" + savedLifecycleRevision
                    + "; fixtureLifecycleBeforeRestore=" + fixtureLifecycleBeforeRestore
                    + "; currentLifecycle=" + fixture.Actor.Lifecycle.LifecycleRevision
                    + "; currentGeneration=" + progression.GrowthState.skillGenerationRevision
                    + "; snapshotDraftExact=" + (snapshotDraftJson == originalDraftJson)
                    + "; snapshotDifferences=" + DescribeOriginalNativeDraftDifference(originalDraftJson, snapshotDraftJson)
                    + "; restoreDifferences=" + DescribeOriginalNativeDraftDifference(snapshotDraftJson, restoredDraftJson);
                return new Newtonsoft.Json.Linq.JObject
                {
                    ["expectedOwnerNameReject"] = false, ["preparationFailed"] = true,
                    ["lastDiagnostic"] = diagnostic, ["ownerBindingMatches"] = restoredOwnerExact,
                    ["originalPendingJson"] = originalDraftJson,
                    ["checkpointSnapshotDraftJson"] = snapshotDraftJson,
                    ["restoredPendingJson"] = restoredDraftJson,
                    ["dispatchCount"] = 0, ["callbackCount"] = 0,
                    ["savedLifecycleRevision"] = savedLifecycleRevision,
                    ["fixtureLifecycleBeforeRestore"] = fixtureLifecycleBeforeRestore,
                    ["restoredLifecycleRevision"] = fixture.Actor.Lifecycle.LifecycleRevision,
                    ["freshOfficialWorldRestore"] = false, ["trainingEligible"] = false
                };
            }
            string beforeState = JsonConvert.SerializeObject(progression.CapturePersistentState());
            int activeBefore = progression.GrowthState.activeSkills.Count;
            int passiveBefore = progression.GrowthState.passiveSkills.Count;
            service.RequestDraft(progression, draft);
            service.Tick();
            Require(runtime.CallCount == 1 && runtime.CallbackCount == 0,
                "The production owner did not submit exactly one deferred request.");
            if (runtime.Prompt != originalPrompt)
            {
                return new Newtonsoft.Json.Linq.JObject
                {
                    ["expectedOwnerNameReject"] = false, ["preparationFailed"] = true,
                    ["lastDiagnostic"] = "Restored original request prompt differs; do not substitute a rewritten prompt.",
                    ["originalPendingExactBeforeDispatch"] = true, ["ownerBindingMatches"] = restoredOwnerExact,
                    ["originalPrompt"] = originalPrompt, ["generatedPrompt"] = runtime.Prompt,
                    ["dispatchCount"] = runtime.CallCount, ["callbackCount"] = runtime.CallbackCount,
                    ["freshOfficialWorldRestore"] = false, ["trainingEligible"] = false
                };
            }
            runtime.Complete();
            var observation = new Newtonsoft.Json.Linq.JObject
            {
                ["ownerId"] = original.ownerBinding.persistentId,
                ["requestKey"] = draft.requestKey,
                ["ownerBindingMatches"] = original.ownerBinding.Matches(progression, out _),
                ["originalPendingExactBeforeDispatch"] = restoredDraftExact,
                ["savedLifecycleRevision"] = savedLifecycleRevision,
                ["fixtureLifecycleBeforeRestore"] = fixtureLifecycleBeforeRestore,
                ["restoredLifecycleRevision"] = fixture.Actor.Lifecycle.LifecycleRevision,
                ["requestContextHashBefore"] = originalContextHash,
                ["requestContextHashAfter"] = draft.requestContextHash,
                ["originalPromptExact"] = runtime.Prompt == originalPrompt,
                ["responseBytesDecodedUnchanged"] = runtime.Response == actualResponse,
                ["dispatchCount"] = runtime.CallCount, ["callbackCount"] = runtime.CallbackCount,
                ["candidateCount"] = draft.candidates.Count,
                ["frozenMechanicsCount"] = draft.frozenMechanics.Count,
                ["nextPresentationIndex"] = draft.nextPresentationIndex,
                ["isReady"] = draft.isReady, ["permanentlyChosen"] = draft.permanentlyChosen,
                ["presentationFailureCount"] = draft.presentationFailureCount,
                ["presentationCorrection"] = draft.presentationCorrection,
                ["lastDiagnostic"] = service.LastDiagnostic,
                ["pendingRequestCount"] = service.PendingRequestCount,
                ["activeSkillCountBefore"] = activeBefore,
                ["activeSkillCountAfter"] = progression.GrowthState.activeSkills.Count,
                ["passiveSkillCountBefore"] = passiveBefore,
                ["passiveSkillCountAfter"] = progression.GrowthState.passiveSkills.Count,
                ["stateBefore"] = beforeState,
                ["stateAfter"] = JsonConvert.SerializeObject(progression.CapturePersistentState()),
                ["selectedOwnerProgressionRestoredFromOriginalCheckpoint"] = true,
                ["freshOfficialWorldRestore"] = false, ["trainingEligible"] = false
            };
            observation["expectedOwnerNameReject"] = runtime.CallbackCount == 1 && !draft.isReady && !draft.permanentlyChosen
                    && draft.candidates.Count == 0 && draft.frozenMechanics.Count == 0
                    && draft.nextPresentationIndex == 0 && draft.presentationFailureCount == 1
                    && draft.presentationCorrection.Contains("Every skill must mention the character name 'Miru'", StringComparison.Ordinal)
                    && service.PendingRequestCount == 1
                    && activeBefore == progression.GrowthState.activeSkills.Count
                    && passiveBefore == progression.GrowthState.passiveSkills.Count
                    && draft.requestContextHash == originalContextHash
                    && JsonUtility.ToJson(original) == originalDraftJson;
            if (captureCanonicalRetry)
            {
                Require((bool?)observation["expectedOwnerNameReject"] == true,
                    "A canonical retry requires the original actual response rejection first.");
                string correction = draft.presentationCorrection.Replace('\r', ' ').Replace('\n', ' ').Trim();
                if (correction.Length > 180) correction = correction.Substring(0, 180);
                string correctionLine = "이전 응답 거부 이유=" + correction
                    + ". 이번 응답에서 반드시 고친다." + Environment.NewLine;
                float retryDelay = Mathf.Min(settings.maximumRetrySeconds, settings.initialRetrySeconds);
                Require(!float.IsNaN(retryDelay) && !float.IsInfinity(retryDelay) && retryDelay >= 0f,
                    "The configured first retry UI delay is invalid.");
                uiClock.Advance(retryDelay);
                service.Tick();
                Require(runtime.CallCount == 2 && runtime.CallbackCount == 1,
                    "The production owner did not dispatch exactly one canonical retry.");
                int correctionOffset = runtime.Prompt.IndexOf(correctionLine, StringComparison.Ordinal);
                Require(correctionOffset >= 0
                        && runtime.Prompt.IndexOf(correctionLine, correctionOffset + correctionLine.Length,
                            StringComparison.Ordinal) < 0
                        && runtime.Prompt.Remove(correctionOffset, correctionLine.Length) == originalPrompt,
                    "Canonical retry changed more than the existing actual failure-feedback line.");
                Require(!draft.isReady && !draft.permanentlyChosen && draft.candidates.Count == 0
                        && draft.frozenMechanics.Count == 0 && draft.nextPresentationIndex == 0
                        && draft.presentationFailureCount == 1 && service.PendingRequestCount == 1
                        && draft.requestContextHash == originalContextHash
                        && activeBefore == progression.GrowthState.activeSkills.Count
                        && passiveBefore == progression.GrowthState.passiveSkills.Count,
                    "Capturing the canonical retry mutated candidates, effects or frozen context.");
                observation["canonicalRetryCaptured"] = true;
                observation["canonicalRetryPrompt"] = runtime.Prompt;
                observation["canonicalCorrectionLine"] = correctionLine;
                observation["retryDispatchCount"] = runtime.CallCount;
                observation["callbackCountAfterRetryCapture"] = runtime.CallbackCount;
                observation["configuredFirstRetryUiDelay"] = retryDelay;
                observation["retryPromptOriginalWithoutOnlyCorrectionLine"] = true;
                observation["readyCandidatesOrEffectsAfterRetryCapture"] = false;
            }
            return observation;
        }
        finally
        {
            service?.CancelRequests(fixture.Actor.Progression);
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static string DescribeOriginalNativeDraftDifference(string beforeJson, string afterJson)
    {
        var before = Newtonsoft.Json.Linq.JObject.Parse(beforeJson);
        var after = Newtonsoft.Json.Linq.JObject.Parse(afterJson);
        return string.Join("; ", before.Descendants().Concat(after.Descendants())
            .Where(value => !value.HasValues && value.Type != Newtonsoft.Json.Linq.JTokenType.Property)
            .Select(value => value.Path).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)
            .Where(path => !string.Equals(before.SelectToken(path)?.ToString(Formatting.None),
                after.SelectToken(path)?.ToString(Formatting.None), StringComparison.Ordinal))
            .Select(path => path + ": " + before.SelectToken(path)?.ToString(Formatting.None)
                + " -> " + after.SelectToken(path)?.ToString(Formatting.None)));
    }

    private sealed class OriginalNativeReplayRuntime : ILocalLlmRuntime, ICharacterSkillModuleSelectionBatchLlmRuntime
    {
        private Action<LocalLlmResult> callback;
        private readonly bool captureCanonicalRetry;
        internal OriginalNativeReplayRuntime(string response, bool captureCanonicalRetry = false)
        { Response = response; this.captureCanonicalRetry = captureCanonicalRetry; }
        internal string Response { get; }
        internal string Prompt { get; private set; }
        internal int CallCount { get; private set; }
        internal int CallbackCount { get; private set; }
        public bool GenerateCharacterSkillModuleSelectionBatchAsync(
            string requestKey, string prompt, Action<LocalLlmResult> completion)
        {
            if (completion == null || CallCount >= (captureCanonicalRetry ? 2 : 1))
                throw new InvalidOperationException("Replay dispatch repeated or absent.");
            CallCount++; Prompt = prompt; callback = completion; return true;
        }
        internal void Complete()
        {
            if (callback == null || CallbackCount != 0) throw new InvalidOperationException("Replay callback repeated or absent.");
            CallbackCount++;
            Action<LocalLlmResult> completion = callback; callback = null;
            completion(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, Response, string.Empty, string.Empty));
        }
        public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> completion) => false;
        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> completion) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> completion) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> completion) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> completion) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> completion) => false;
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> completion) => false;
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> completion) => false;
    }

    [MenuItem("DungeonStory/Debug/Character/Run Progression Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Character progression scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        CleanupLeakedActorFixtures();
        CharacterSkillRuntimeEffects.ResetTransientExecutionStateForDebug();
        List<string> errors = new List<string>();
        Run("level 50 experience curve", VerifyExperienceCurve, errors);
        Run("potential distribution and rarity pity", VerifyPotentialAndRarityRules, errors);
        Run("initial stats and level-50 growth", VerifyStatsAndLevelGrowth, errors);
        Run("active, passive, and ultimate slots", VerifySkillMilestones, errors);
        Run("permanent choice and save round trip", VerifyPermanentChoiceAndPersistence, errors);
        Run("formula presentation response validation",
            VerifyFormulaPresentationContract,
            errors);
        Run("skill acquisition outcome atomicity",
            VerifySkillAcquiredOutcomeAtomicity,
            errors);
        Run("module-selection exact-count batch atomicity",
            VerifyModuleSelectionBatchContract,
            errors);
        Run("management passive reachable-effect legality",
            VerifyManagementPassiveReachability,
            errors);
        Run("empty-ledger skill generation fails closed",
            VerifyEmptyLedgerSkillPublicContextFallback,
            errors);
        Run("unpublished composition defers empty-ledger generation",
            VerifyUnpublishedCompositionDefersSkillGeneration,
            errors);
        Run("world restore resumes saved drafts without inventing new ones",
            () => VerifyWorldRestoreGenerationContract(out _),
            errors);
        Run("acquired-trait experience score", VerifyAcquiredTraitExperienceScore, errors);
        Run("acquired-trait bounded evidence window",
            VerifyAcquiredTraitBoundedEvidenceWindow,
            errors);
        Run("acquired-trait milestone lifecycle", VerifyAcquiredTraitMilestoneLifecycle, errors);
        Run("acquired-trait outcome failure rolls back and retries",
            VerifyAcquiredTraitOutcomeFailureRollback,
            errors);
        Run("acquired-trait formula v2 compositional drawbacks",
            VerifyAcquiredTraitFormulaV2Composition,
            errors);
        Run("authored acquired-trait effect projection",
            VerifyAuthoredAcquiredTraitEffectProjection,
            errors);
        Run("acquired-trait automatic producer resume",
            VerifyAcquiredTraitAutomaticProducerResume,
            errors);
        Run("acquired-trait stale owner automatic reissue",
            VerifyAcquiredTraitAutomaticOwnerInvalidation, errors);
        Run("acquired-trait special reaction publishes canonical outcome",
            VerifyAcquiredTraitSpecialReactionOutcome,
            errors);
        Run("acquired-trait whole-root save round trip",
            VerifyAcquiredTraitWholeRootSaveRoundTrip,
            errors);
        Run("memory-erasure seal atomicity", VerifyMemoryErasureSealAtomicity, errors);
        Run("provider startup wait does not consume accepted timeout",
            VerifyProviderStartupWaitDoesNotConsumeAcceptedTimeout,
            errors);
        Run("completion-owning provider controls accepted lifetime",
            VerifyCompletionOwningProviderControlsAcceptedLifetime,
            errors);
        Run("in-flight generation timeout awaits narrative retry", VerifyInFlightGenerationTimeout, errors);
        Run("provider timeout circuit preserves queued requests",
            VerifyProviderCircuitDrainsQueuedRequests,
            errors);
        Run("LLM retry and request-key resume", VerifyRetryAndRequestKeyResume, errors);
        Run("frozen skill context survives later events and restore", VerifyFrozenSkillRequestContext, errors);
        Run("ultimate domain use limits", VerifyUltimateUseLimits, errors);
        Run("independent scoped transient roots", VerifyIndependentTransientRoots, errors);
        Run("management and defense runtime effects", VerifySkillRuntimeEffects, errors);
        Run("manual work skill targeting, areas, and cooldown",
            VerifyManualWorkSkillTargetingAreasAndCooldown,
            errors);
        Run("manual work expedition formation projection",
            VerifyManualWorkExpeditionFormationProjection,
            errors);
        Run("training experience", VerifyTrainingExperience, errors);

        foreach (string error in errors)
        {
            Debug.LogError(error);
        }

        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log("Character progression scenarios passed.");
        }

        CleanupLeakedActorFixtures();
        return errors.Count == 0;
    }

    public static bool RunGenerationTimeoutScenario() =>
        VerifyInFlightGenerationTimeout();

    public static bool RunProviderStartupReadinessScenario() =>
        VerifyProviderStartupWaitDoesNotConsumeAcceptedTimeout();

    public static bool RunCompletionOwnerScenario() =>
        VerifyCompletionOwningProviderControlsAcceptedLifetime();

    public static void RunPromptSizeDiagnostics()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        using ActorFixture actor = new ActorFixture(
            994,
            "프롬프트 크기 검증자",
            "human");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new MissingLlmRuntimeProvider(),
                new MutableUiClock());
            CharacterProgression progression = actor.Actor.Progression;
            progression.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            progression.RecordNarrative(
                CharacterNarrativeDomain.Survival,
                "origin:prompt-size",
                "character:prompt-size",
                CharacterActivityOutcomes.Completed,
                day: 0);
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "history:prompt-size",
                "facility:prompt-size",
                CharacterActivityOutcomes.Completed,
                day: 0);

            CharacterSkillDraft active = service.CreateDraft(
                progression,
                CharacterSkillKind.Active,
                1);
            CharacterSkillDraft passive = service.CreateDraft(
                progression,
                CharacterSkillKind.Passive,
                1);
            NarrativePublicContextMaterial publicMaterial =
                CharacterSkillPromptBuilder.BuildPublicMaterial(NarrativeRequestContextTestFixture.CreateEmpty(), progression);
            string activePrompt = CharacterSkillPromptBuilder.BuildEnvelope(
                progression,
                active,
                settings,
                publicMaterial,
                batchModuleSelection: true).Prompt;
            string passivePrompt = CharacterSkillPromptBuilder.BuildEnvelope(
                progression,
                passive,
                settings,
                publicMaterial,
                batchModuleSelection: true).Prompt;
            string activeTransportPrompt =
                BuildCharacterSkillTransportPromptForDiagnostics(activePrompt);
            string passiveTransportPrompt =
                BuildCharacterSkillTransportPromptForDiagnostics(passivePrompt);
            Debug.Log(
                "CHARACTER_SKILL_PROMPT_DIAGNOSTIC "
                + $"activeChars={activePrompt.Length}; "
                + $"activeTransportChars={activeTransportPrompt.Length}; "
                + $"activePacketChars={CharacterSkillPromptBuilder.BuildModuleSelectionBatchPacket(active, settings).Length}; "
                + $"activeOffers={active.moduleSelectionOffers[0].positiveModuleIds.Count}; "
                + $"passiveChars={passivePrompt.Length}; "
                + $"passiveTransportChars={passiveTransportPrompt.Length}; "
                + $"passivePacketChars={CharacterSkillPromptBuilder.BuildModuleSelectionBatchPacket(passive, settings).Length}; "
                + $"passiveOffers={passive.moduleSelectionOffers[0].positiveModuleIds.Count}");
            Debug.Log(
                "CHARACTER_SKILL_ACTIVE_TRANSPORT_PROMPT_BASE64="
                + Convert.ToBase64String(Encoding.UTF8.GetBytes(activeTransportPrompt)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static string BuildCharacterSkillTransportPromptForDiagnostics(string prompt)
    {
        System.Reflection.MethodInfo method = typeof(NarrativeRequestContext).GetMethod(
            "ToModelPromptWithoutCanonicalPublicPayload",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Require(method != null,
            "The CharacterSkill transport prompt projection is unavailable.");
        return (string)method.Invoke(null, new object[] { prompt });
    }

    public static bool RunProviderCircuitScenario() =>
        VerifyProviderCircuitDrainsQueuedRequests();

    public static bool RunTransientRootLifetimeScenario() =>
        VerifyIndependentTransientRoots();

    public static bool RunEmptyLedgerSkillPublicContextFallbackScenario() =>
        VerifyEmptyLedgerSkillPublicContextFallback();

    public static bool RunFormulaPresentationContractScenario() =>
        VerifyFormulaPresentationContract();

    public static bool RunSkillAcquiredOutcomeAtomicityScenario() =>
        VerifySkillAcquiredOutcomeAtomicity();

    public static bool RunPreparationSkillAcquisitionScenario()
    {
        var settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.formulaPolicy.formulaVersion = CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion;
        CharacterSkillFormulaCatalogAssetBuilder.Populate(settings);
        using ActorFixture actor = new(7792, "준비 기술 검증자", "human", unpublishedComposition: true);
        try
        {
            var settingsProvider = new TestSettingsProvider(settings);
            var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
            var projector = new CharacterProgressionProfileProjector(content, new CharacterRuntimeProfileFactory(content));
            var service = new CharacterSkillGenerationService(NarrativeRequestContextTestFixture.CreateEmpty(),
                settingsProvider, new MissingLlmRuntimeProvider(), CharacterAiEditorTestDependencies.UiClock);
            var progression = actor.Actor.Progression;
            progression.ConfigurePreview(service, settingsProvider, projector);
            progression.SetPublicSkillNotificationsSuppressed(true);
            // ActorFixture starts with legacy diagnostic drafts. Start this
            // formula-only case through the same reroll owner command as the UI.
            progression.ApplyPreparedIdentity("준비 기술 검증자", "prepared skill boundary",
                Array.Empty<int>(), progression.PotentialGrade, 7792,
                autoChooseDrafts: false, ensureInitialDrafts: false);
            Require(progression.ActiveSkills.Count == 0 && progression.PassiveSkills.Count == 0
                    && progression.GrowthState.skillAcquisitionRevision == 0L,
                "Preparation fixture did not start with empty skill selections.");
            progression.RecordNarrative(CharacterNarrativeDomain.Work, "work:prepared-skill",
                "facility:prepared-skill", CharacterActivityOutcomes.Completed, day: 1);
            CharacterSkillDraft active = CreateReadyFormulaDraftForOutcome(service, progression,
                CharacterSkillKind.Active, 1, "준비 기술 검증자");
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.RemoveAll(value => value?.kind == CharacterSkillKind.Active && value.unlockLevel == 1);
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.Add(active);
            string before = JsonUtility.ToJson(progression.GrowthState);
            string ledgerBefore = JsonUtility.ToJson(progression.NarrativeLedger);
            Require(!progression.TryChooseActiveSkill(1, 0, true, out string unprepared)
                    && unprepared == "skill-acquisition-preview-contract-invalid"
                    && before == JsonUtility.ToJson(progression.GrowthState)
                    && ledgerBefore == JsonUtility.ToJson(progression.NarrativeLedger),
                "A preview without a prepared profile changed state: " + unprepared);

            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).startingProfile.prepared = true;
            var events = new GameEventBus();
            progression.ConstructCharacterProgression(service, settingsProvider,
                new CharacterProgressionNotificationApplicationAdapter(events), projector, events,
                new UnexpectedEvidenceUseTransaction(), facts: null, skillAcquisitions: null);
            before = JsonUtility.ToJson(progression.GrowthState);
            Require(!progression.TryChooseActiveSkill(1, 0, true, out string missingPublisher)
                    && missingPublisher == "skill-acquisition-observation-not-configured"
                    && before == JsonUtility.ToJson(progression.GrowthState)
                    && ledgerBefore == JsonUtility.ToJson(progression.NarrativeLedger),
                "World configuration retained preview permission or bypassed a missing publisher: " + missingPublisher);

            progression.ConfigurePreview(service, settingsProvider, projector);
            int usesBefore = progression.NarrativeLedger.Facts.Sum(value => value?.influenceUseCount ?? 0);
            Require(progression.TryChooseActiveSkill(1, 0, true, out string activeFailure),
                "Prepared active selection failed: " + activeFailure);
            CharacterSkillDraft passive = CreateReadyFormulaDraftForOutcome(service, progression,
                CharacterSkillKind.Passive, 1, "준비 기술 검증자");
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.RemoveAll(value => value?.kind == CharacterSkillKind.Passive && value.unlockLevel == 1);
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.Add(passive);
            progression.OnDraftReady(passive);
            Require(progression.ActiveSkills.Count == 1 && progression.PassiveSkills.Count == 1
                    && progression.GrowthState.skillAcquisitionRevision == 0L
                    && progression.NarrativeLedger.Facts.Sum(value => value?.influenceUseCount ?? 0) > usesBefore,
                $"Prepared choices mismatch: active={progression.ActiveSkills.Count}; passive={progression.PassiveSkills.Count}; "
                    + $"revision={progression.GrowthState.skillAcquisitionRevision}; usesBefore={usesBefore}; "
                    + $"usesAfter={progression.NarrativeLedger.Facts.Sum(value => value?.influenceUseCount ?? 0)}.");
            before = JsonUtility.ToJson(progression.GrowthState);
            ledgerBefore = JsonUtility.ToJson(progression.NarrativeLedger);
            progression.OnDraftReady(passive);
            Require(progression.TryChooseActiveSkill(1, 0, true, out _)
                    && !progression.TryChooseActiveSkill(1, 1, true, out _)
                    && before == JsonUtility.ToJson(progression.GrowthState)
                    && ledgerBefore == JsonUtility.ToJson(progression.NarrativeLedger),
                "Redelivered preparation choices changed state or consumed evidence twice.");
            return true;
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }

    public static void RunObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            Application.dataPath, "..", "Artifacts", "QA"))
            + System.IO.Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output)
            || !System.IO.Path.IsPathRooted(output)
            || !System.IO.Path.GetFullPath(output).StartsWith(
                qaRoot,
                StringComparison.OrdinalIgnoreCase)
            || System.IO.Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "Skill-acquisition observer checks require new project-local batch evidence.");
        }
        string probe = System.IO.File.ReadAllText(System.IO.Path.Combine(
            System.IO.Directory.GetParent(
                System.IO.Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
            && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true")
            && probe.Contains("inJob=true"),
            "Skill-acquisition observer checks require the noninteractive desktop probe.");
        Require(VerifySkillAcquiredOutcomeAtomicity(),
            "Skill-acquisition focused observer checks failed.");
        Debug.Log("B82_SKILL_ACQUISITION_OBSERVER_FOCUSED=PASS");
    }

    public static bool RunManagementPassiveReachabilityScenario() =>
        VerifyManagementPassiveReachability();

    public static bool RunAcquiredTraitFormulaV2CompositionScenario() =>
        VerifyAcquiredTraitFormulaV2Composition();

    public static bool RunAcquiredTraitMilestoneLifecycleScenario() =>
        VerifyAcquiredTraitMilestoneLifecycle();

    public static bool RunAcquiredTraitEffectProjectionScenario() =>
        VerifyAuthoredAcquiredTraitEffectProjection();

    public static bool RunAcquiredTraitAutomaticProducerResumeScenario() =>
        VerifyAcquiredTraitAutomaticProducerResume();

    public static void RunTraitInferenceObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            Application.dataPath, "..", "Artifacts", "QA"))
            + System.IO.Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output)
            || !System.IO.Path.IsPathRooted(output)
            || !System.IO.Path.GetFullPath(output).StartsWith(
                qaRoot,
                StringComparison.OrdinalIgnoreCase)
            || System.IO.Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "Trait-inference observer checks require new project-local batch evidence.");
        }
        string probe = System.IO.File.ReadAllText(System.IO.Path.Combine(
            System.IO.Directory.GetParent(
                System.IO.Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
            && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true")
            && probe.Contains("inJob=true"),
            "Trait-inference observer checks require the noninteractive desktop probe.");
        Require(VerifyAcquiredTraitOutcomeFailureRollback(),
            "Trait-inference publication rollback and recovery checks failed.");
        Require(VerifyAcquiredTraitAutomaticProducerResume(),
            "Trait-inference automatic producer resume checks failed.");
        Require(VerifyAcquiredTraitAutomaticOwnerInvalidation(),
            "Trait-inference automatic owner invalidation checks failed.");
        Debug.Log("B83_TRAIT_INFERENCE_OBSERVER_FOCUSED=PASS");
    }

    public static void RunTraitReactionObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            Application.dataPath, "..", "Artifacts", "QA"))
            + System.IO.Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output)
            || !System.IO.Path.IsPathRooted(output)
            || !System.IO.Path.GetFullPath(output).StartsWith(
                qaRoot,
                StringComparison.OrdinalIgnoreCase)
            || System.IO.Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "Trait-reaction observer checks require new project-local batch evidence.");
        }
        string probe = System.IO.File.ReadAllText(System.IO.Path.Combine(
            System.IO.Directory.GetParent(
                System.IO.Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
            && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true")
            && probe.Contains("inJob=true"),
            "Trait-reaction observer checks require the noninteractive desktop probe.");
        Require(VerifyAcquiredTraitSpecialReactionOutcome(),
            "Trait-reaction publication, rollback, and recovery checks failed.");
        Debug.Log("B84_TRAIT_REACTION_OBSERVER_FOCUSED=PASS");
    }

    public static void RunLevelObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            Application.dataPath, "..", "Artifacts", "QA"))
            + System.IO.Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output)
            || !System.IO.Path.IsPathRooted(output)
            || !System.IO.Path.GetFullPath(output).StartsWith(
                qaRoot,
                StringComparison.OrdinalIgnoreCase)
            || System.IO.Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "Level observer checks require new project-local batch evidence.");
        }
        string probe = System.IO.File.ReadAllText(System.IO.Path.Combine(
            System.IO.Directory.GetParent(
                System.IO.Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
            && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true")
            && probe.Contains("inJob=true"),
            "Level observer checks require the noninteractive desktop probe.");
        Require(VerifyLevelObserver(),
            "Level event projection and observer lifecycle checks failed.");
        Require(VerifyAcquiredTraitSpecialReactionOutcome(),
            "Trait-reaction XP notification regression checks failed.");
        Debug.Log("B85_LEVEL_OBSERVER_FOCUSED=PASS");
    }

    public static void RunMemoryErasureObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            Application.dataPath, "..", "Artifacts", "QA"))
            + System.IO.Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output)
            || !System.IO.Path.IsPathRooted(output)
            || !System.IO.Path.GetFullPath(output).StartsWith(
                qaRoot,
                StringComparison.OrdinalIgnoreCase)
            || System.IO.Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "Memory-erasure observer checks require new project-local batch evidence.");
        }
        string probe = System.IO.File.ReadAllText(System.IO.Path.Combine(
            System.IO.Directory.GetParent(
                System.IO.Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
            && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true")
            && probe.Contains("inJob=true"),
            "Memory-erasure observer checks require the noninteractive desktop probe.");
        Require(VerifyMemoryErasureSealAtomicity(),
            "Memory-erasure publication, rollback, and recovery checks failed.");
        Debug.Log("B86_MEMORY_ERASURE_OBSERVER_FOCUSED=PASS");
    }

    public static void RunFrozenTraitModuleEvidenceBatch()
    {
        Require(VerifyAcquiredTraitAutomaticProducerResume(),
            "Frozen trait module evidence did not survive an authoritative outcome change and JSON restore.");
        Require(VerifyAcquiredTraitAutomaticOwnerInvalidation(),
            "Frozen trait evidence weakened stale-owner cancellation or callback rejection.");
        Debug.Log("FROZEN_TRAIT_MODULE_EVIDENCE=PASS; same-tuple-completed-to-blocked=1; "
            + "live-offer-divergence-observed=1; frozen-prompt-identical=1; fresh-json-owner=1; "
            + "invalid-pending-atomic-rejections=10; completed-outcome=1; duplicate-callback-effects=0; "
            + "stale-life-and-generation-owner-rejections=2; legacy-v3-migration=0");
    }

    public static bool RunAcquiredTraitAutomaticOwnerInvalidationScenario() =>
        VerifyAcquiredTraitAutomaticOwnerInvalidation();

    public static bool RunAcquiredTraitBoundedEvidenceWindowScenario() =>
        VerifyAcquiredTraitBoundedEvidenceWindow();

    public static bool RunManualWorkSkillTargetingScenario() =>
        VerifyManualWorkSkillTargetingAreasAndCooldown();

    public static bool RunManualWorkExpeditionScenario() =>
        VerifyManualWorkExpeditionFormationProjection();

    private static bool VerifyLevelObserver()
    {
        var events = new GameEventBus();
        using var actor = new ActorFixture(
            812585,
            "레벨 관찰자",
            "human",
            progressionEvents: events);
        var world = new FixedCharacterWorld(actor.Actor);
        var observed = new List<CharacterLevelChangedEvent>();
        using IDisposable rawSubscription = events
            .Subscribe<CharacterLevelChangedEvent>(value => observed.Add(value));
        using var observer = new CharacterLevelActivityObserver(events, world);
        observer.Initialize();
        observer.Initialize();

        CharacterProgression progression = actor.Actor.Progression;
        CharacterLog log = actor.Actor.LogComponent;
        var addedEntries = new List<CharacterLogEntry>();
        void CaptureLogEntry(CharacterLogEntry entry) => addedEntries.Add(entry);
        log.OnLogAdded += CaptureLogEntry;
        string Diagnostics() =>
            " events=[" + string.Join(", ", observed.Select(value =>
                $"{value.PreviousLevel}->{value.Level}/{value.Cause}"))
            + "]; entries=[" + string.Join(" | ", addedEntries.Select(value =>
                $"{value.OriginalMessage}/{value.Activity?.FactText ?? "<null>"}"))
            + "]; visible=[" + string.Join(" | ", log.Entries) + "]";
        int initialLogCount = log.Entries.Count;
        CharacterId characterId = actor.Actor.BuildingCharacterId;
        Require(characterId.IsValid && progression.Level == 1,
            "Level observer fixture must start from a valid level-one actor.");

        Require(progression.AddExperience(GetExperienceToReach(2)) == 1
            && progression.Level == 2
            && observed.Count == 1
            && observed[0].Character.Equals(characterId)
            && observed[0].PreviousLevel == 1
            && observed[0].Level == 2
            && observed[0].Cause == CharacterLevelChangeCause.Experience,
            "Experience level-up did not publish the exact applied transition."
            + Diagnostics());
        Require(log.Entries.Count == initialLogCount + 1
            && log.Entries[^1] == "레벨 2에 도달했다."
            && addedEntries.Count == 1
            && addedEntries[0].OriginalMessage == "레벨 2에 도달했다."
            && addedEntries[0].Activity?.FactText == "레벨 2에 도달했다."
            && CharacterLevelActivityObserver.Project(observed[0])
                == "레벨 2에 도달했다.",
            "Experience level-up was not projected exactly once."
            + Diagnostics());

        int afterExperienceLogCount = log.Entries.Count;
        Require(progression.AddExperience(1) == 0
            && observed.Count == 1
            && addedEntries.Count == 1
            && log.Entries.Count == afterExperienceLogCount,
            "Experience without a level transition emitted an observation."
            + Diagnostics());
        Require(!progression.EnsureMinimumLevel(
                2,
                CharacterLevelChangeCause.CampaignRecruitmentTraining)
            && observed.Count == 1
            && addedEntries.Count == 1
            && log.Entries.Count == afterExperienceLogCount,
            "A satisfied minimum-level request emitted an observation."
            + Diagnostics());

        actor.Actor.gameObject.SetActive(false);
        Require(progression.EnsureMinimumLevel(
                3,
                CharacterLevelChangeCause.CampaignRecruitmentTraining)
            && observed.Count == 2
            && observed[^1].PreviousLevel == 2
            && observed[^1].Level == 3
            && observed[^1].Cause
                == CharacterLevelChangeCause.CampaignRecruitmentTraining
            && addedEntries.Count == 2
            && addedEntries[^1].OriginalMessage == "원정 합류 훈련을 마쳤다."
            && addedEntries[^1].Activity?.FactText == "원정 합류 훈련을 마쳤다."
            && CharacterLevelActivityObserver.Project(observed[^1])
                == "원정 합류 훈련을 마쳤다.",
            "Lifetime observer did not project an inactive actor's training level-up."
            + Diagnostics());
        actor.Actor.gameObject.SetActive(true);

        Require(progression.EnsureMinimumLevel(
                4,
                CharacterLevelChangeCause.DebugAdjustment)
            && observed.Count == 3
            && observed[^1].Cause == CharacterLevelChangeCause.DebugAdjustment
            && addedEntries.Count == 3
            && addedEntries[^1].OriginalMessage == "디버그 레벨 조정"
            && addedEntries[^1].Activity?.FactText == "디버그 레벨 조정"
            && CharacterLevelActivityObserver.Project(observed[^1])
                == "디버그 레벨 조정",
            "Debug level-up did not retain its existing activity text."
            + Diagnostics());

        int beforeSilentMinimum = addedEntries.Count;
        Require(progression.EnsureMinimumLevel(5)
            && observed.Count == 4
            && observed[^1].Cause
                == CharacterLevelChangeCause.MinimumLevelRequirement
            && CharacterLevelActivityObserver.Project(observed[^1]).Length == 0
            && addedEntries.Count == beforeSilentMinimum,
            "Default minimum-level transition should publish without a UI log."
            + Diagnostics());

        observer.Dispose();
        Require(progression.EnsureMinimumLevel(
                6,
                CharacterLevelChangeCause.CampaignRecruitmentTraining)
            && observed.Count == 5
            && addedEntries.Count == beforeSilentMinimum,
            "Disposed level observer still recorded activity."
            + Diagnostics());
        observer.Initialize();
        observer.Initialize();
        Require(progression.EnsureMinimumLevel(
                7,
                CharacterLevelChangeCause.CampaignRecruitmentTraining)
            && observed.Count == 6
            && addedEntries.Count == beforeSilentMinimum + 1
            && addedEntries[^1].OriginalMessage == "원정 합류 훈련을 마쳤다."
            && addedEntries[^1].Activity?.FactText == "원정 합류 훈련을 마쳤다.",
            "Reinitialized level observer did not bind exactly once."
            + Diagnostics());

        bool invalidIdentityRejected = false;
        bool invalidRangeRejected = false;
        bool invalidCauseRejected = false;
        try
        {
            _ = new CharacterLevelChangedEvent(
                default,
                1,
                2,
                CharacterLevelChangeCause.Experience);
        }
        catch (ArgumentException)
        {
            invalidIdentityRejected = true;
        }
        try
        {
            _ = new CharacterLevelChangedEvent(
                characterId,
                2,
                2,
                CharacterLevelChangeCause.Experience);
        }
        catch (ArgumentOutOfRangeException)
        {
            invalidRangeRejected = true;
        }
        try
        {
            _ = new CharacterLevelChangedEvent(
                characterId,
                1,
                2,
                (CharacterLevelChangeCause)int.MaxValue);
        }
        catch (ArgumentOutOfRangeException)
        {
            invalidCauseRejected = true;
        }

        log.OnLogAdded -= CaptureLogEntry;
        return invalidIdentityRejected
            && invalidRangeRejected
            && invalidCauseRejected;
    }

    private static bool VerifyManualWorkSkillTargetingAreasAndCooldown()
    {
        using var activation = new CharacterSkillActivationEditorFixture();
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        world.Clear();
        GameObject gridObject = new GameObject(
            "Manual Skill Scenario GridSystemManager");
        gridObject.SetActive(false);
        GridSystemManager manager = gridObject.AddComponent<GridSystemManager>();
        manager.defaultGridWidth = 20;
        manager.defaultGridHeight = 20;
        gridObject.SetActive(true);
        manager.EnsureGridInitialized();
        Grid grid = manager.grid;
        world.SetGrid(grid);
        try
        {
            using ActorFixture source = new ActorFixture(8501, "시전자", "human");
            using ActorFixture nearA = new ActorFixture(8502, "근접 대상 A", "human");
            using ActorFixture nearB = new ActorFixture(8503, "근접 대상 B", "human");
            using ActorFixture far = new ActorFixture(8504, "원거리 대상", "human");
            CharacterActor[] actors =
            {
                source.Actor,
                nearA.Actor,
                nearB.Actor,
                far.Actor
            };
            foreach (CharacterActor actor in actors)
                world.RegisterCharacter(actor);

            source.Actor.transform.position = grid.GetWorldPos(new Vector2Int(10, 10));
            nearA.Actor.transform.position = grid.GetWorldPos(new Vector2Int(11, 10));
            nearB.Actor.transform.position = grid.GetWorldPos(new Vector2Int(12, 10));
            far.Actor.transform.position = grid.GetWorldPos(new Vector2Int(17, 17));

            CharacterSkillInstance self = CreateManualWorkTestSkill(
                "skill:qa:manual-self",
                CharacterSkillTargetingMode.Self,
                CharacterSkillEffectArea.Single,
                1);
            CharacterSkillInstance square = CreateManualWorkTestSkill(
                "skill:qa:manual-square",
                CharacterSkillTargetingMode.PlayerSelected,
                CharacterSkillEffectArea.Square,
                3);
            CharacterSkillInstance random = CreateManualWorkTestSkill(
                "skill:qa:manual-random",
                CharacterSkillTargetingMode.DeterministicRandom,
                CharacterSkillEffectArea.Single,
                1);
            CharacterSkillInstance all = CreateManualWorkTestSkill(
                "skill:qa:manual-all",
                CharacterSkillTargetingMode.AllEligible,
                CharacterSkillEffectArea.Dungeon,
                1);
            CharacterSkillInstance room = CreateManualWorkTestSkill(
                "skill:qa:manual-room",
                CharacterSkillTargetingMode.PlayerSelected,
                CharacterSkillEffectArea.Room,
                1);
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(source.Actor.Progression).activeSkills.AddRange(
                new[] { self, square, random, all, room });

            FakeRoomLayoutCache rooms = new FakeRoomLayoutCache(new RoomInstance(
                1,
                new[]
                {
                    nearA.Actor.GetNowXY(),
                    nearB.Actor.GetNowXY()
                },
                Array.Empty<BuildableObject>(),
                Array.Empty<BuildableObject>(),
                Array.Empty<BuildableObject>(),
                solidBoundaryCount: 1,
                openBoundaryCount: 0,
                selfContained: true));

            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, self.id, null, null, world, rooms, 100L,
                    activation.Transaction,
                    out _),
                "Self manual work skills must activate immediately without a target click.");
            Require(HasManualBuff(source.Actor, self.id, 100L)
                    && !actors.Skip(1).Any(actor => HasManualBuff(actor, self.id, 100L)),
                "Self manual work skills must affect only the source actor.");

            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, square.id, nearA.Actor, null, world, rooms,
                    100L, activation.Transaction, out _),
                "Player-selected square manual work skills must accept a legal actor anchor.");
            Require(HasManualBuff(nearA.Actor, square.id, 100L)
                    && HasManualBuff(nearB.Actor, square.id, 100L)
                    && !HasManualBuff(far.Actor, square.id, 100L),
                "A 3x3 manual work area must include legal actors within one cell of its anchor only.");
            Require(!CharacterManualSkillRuntime.TryActivate(
                    source.Actor, square.id, nearA.Actor, null, world, rooms,
                    101L, activation.Transaction, out string cooldownMessage)
                    && cooldownMessage.Contains("47시간", StringComparison.Ordinal),
                "Manual work skills must enforce their two-day in-game cooldown.");
            CharacterSkillUseLimitState roundTrip =
                source.Actor.Progression.GrowthState.useLimits.Clone();
            Require(roundTrip.manualSkillCooldowns.Any(value => value != null
                        && value.skillId == square.id
                        && value.readyAbsoluteHour == 148L)
                    && nearA.Actor.Progression.GrowthState.useLimits.Clone()
                        .manualSkillBuffs.Any(value => value != null
                            && value.skillId == square.id
                            && value.expiresAbsoluteHour == 124L),
                "Manual cooldown and temporary buff state must survive a save-state clone.");
            Require(HasManualBuff(nearA.Actor, square.id, 123L)
                    && !HasManualBuff(nearA.Actor, square.id, 124L),
                "Manual work buffs must expire at the authored in-game hour boundary.");

            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, room.id, nearA.Actor, null, world, rooms,
                    100L, activation.Transaction, out _),
                "Room-area manual work skills must accept an actor inside a usable room.");
            Require(HasManualBuff(nearA.Actor, room.id, 100L)
                    && HasManualBuff(nearB.Actor, room.id, 100L)
                    && !HasManualBuff(far.Actor, room.id, 100L),
                "Room-area manual work skills must affect only legal actors in the anchor room.");

            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, all.id, null, null, world, rooms, 100L,
                    activation.Transaction,
                    out _),
                "Dungeon-area manual work skills must activate without a target click.");
            Require(actors.Skip(1).All(actor => HasManualBuff(actor, all.id, 100L))
                    && !HasManualBuff(source.Actor, all.id, 100L),
                "Dungeon-area ally skills must affect every legal ally and not the source.");

            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, random.id, null, null, world, rooms, 100L,
                    activation.Transaction,
                    out _),
                "Deterministic-random manual work skills must choose one current legal target.");
            string firstRandomTarget = actors.Skip(1)
                .Single(actor => HasManualBuff(actor, random.id, 100L))
                .Identity.PersistentId;
            ClearManualSkillState(source.Actor, actors, random.id);
            Require(CharacterManualSkillRuntime.TryActivate(
                    source.Actor, random.id, null, null, world, rooms, 777L,
                    activation.Transaction,
                    out _),
                "Deterministic-random manual work skills must remain usable after test-state reset.");
            string secondRandomTarget = actors.Skip(1)
                .Single(actor => HasManualBuff(actor, random.id, 777L))
                .Identity.PersistentId;
            Require(string.Equals(firstRandomTarget, secondRandomTarget,
                    StringComparison.Ordinal),
                "Deterministic-random targeting must not change merely because the clock hour changed.");

            bool rejectedEvenSquare = false;
            bool rejectedSelfRoom = false;
            try
            {
                CharacterSkillAreaRules.RequireValid(
                    CharacterSkillTargetingMode.PlayerSelected,
                    CharacterSkillEffectArea.Square,
                    4);
            }
            catch (InvalidOperationException)
            {
                rejectedEvenSquare = true;
            }
            try
            {
                CharacterSkillAreaRules.RequireValid(
                    CharacterSkillTargetingMode.Self,
                    CharacterSkillEffectArea.Room,
                    1);
            }
            catch (InvalidOperationException)
            {
                rejectedSelfRoom = true;
            }
            Require(rejectedEvenSquare && rejectedSelfRoom,
                "Invalid even-sized square and self-room combinations must fail closed.");
            return true;
        }
        finally
        {
            world.Clear();
            if (gridObject != null)
                UnityEngine.Object.DestroyImmediate(gridObject);
        }
    }

    private static CharacterSkillInstance CreateManualWorkTestSkill(
        string id,
        CharacterSkillTargetingMode targetingMode,
        CharacterSkillEffectArea effectArea,
        int areaSize)
    {
        return new CharacterSkillInstance
        {
            id = id,
            displayName = id,
            kind = CharacterSkillKind.Active,
            trigger = CharacterSkillTrigger.ManualWork,
            target = targetingMode == CharacterSkillTargetingMode.Self
                ? CharacterSkillTarget.Self
                : CharacterSkillTarget.Ally,
            targetingMode = targetingMode,
            effectArea = effectArea,
            areaSize = areaSize,
            manualDurationHours = GameCalendarRules.HoursPerDay,
            manualCooldownDays = 2,
            modules = new List<CharacterSkillModuleSelection>
            {
                new CharacterSkillModuleSelection
                {
                    moduleId = "work_speed",
                    variantId = "small"
                }
            }
        };
    }

    private static bool VerifyManualWorkExpeditionFormationProjection()
    {
        using var activation = new CharacterSkillActivationEditorFixture();
        using ActorFixture source = new ActorFixture(8511, "원정 시전자", "human");
        using ActorFixture frontA = new ActorFixture(8512, "전열 대상 A", "human");
        using ActorFixture frontB = new ActorFixture(8513, "전열 대상 B", "human");
        using ActorFixture middle = new ActorFixture(8514, "중열 대상", "human");
        using ActorFixture rear = new ActorFixture(8515, "후열 대상", "human");
        CharacterActor[] party =
        {
            source.Actor,
            frontA.Actor,
            frontB.Actor,
            middle.Actor,
            rear.Actor
        };
        Dictionary<CharacterActor, int> formation = new()
        {
            [source.Actor] = 0,
            [frontA.Actor] = 0,
            [frontB.Actor] = 0,
            [middle.Actor] = 1,
            [rear.Actor] = 2
        };

        CharacterSkillInstance room = CreateManualWorkTestSkill(
            "skill:qa:expedition-room",
            CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Room,
            1);
        CharacterSkillInstance square5 = CreateManualWorkTestSkill(
            "skill:qa:expedition-square5",
            CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Square,
            5);
        CharacterSkillInstance square7 = CreateManualWorkTestSkill(
            "skill:qa:expedition-square7",
            CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Square,
            7);
        CharacterSkillInstance random = CreateManualWorkTestSkill(
            "skill:qa:expedition-random",
            CharacterSkillTargetingMode.DeterministicRandom,
            CharacterSkillEffectArea.Single,
            1);
        CharacterSkillInstance all = CreateManualWorkTestSkill(
            "skill:qa:expedition-all",
            CharacterSkillTargetingMode.AllEligible,
            CharacterSkillEffectArea.Dungeon,
            1);
        CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(source.Actor.Progression).activeSkills.AddRange(
            new[] { room, square5, square7, random, all });

        Require(!CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                room.id,
                null,
                party,
                actor => formation[actor],
                200L,
                activation.Transaction,
                out _),
            "Canceling expedition target selection must fail without applying a skill.");
        Require(CharacterManualSkillRuntime.GetCommands(source.Actor, 200L)
                .Single(value => value.SkillId == room.id)
                .IsReady,
            "Canceled expedition targeting must not consume cooldown.");
        Require(!CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                room.id,
                source.Actor,
                party,
                actor => formation[actor],
                200L,
                activation.Transaction,
                out _)
                && CharacterManualSkillRuntime.GetCommands(source.Actor, 200L)
                    .Single(value => value.SkillId == room.id)
                    .IsReady,
            "An illegal expedition target must not consume cooldown.");

        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                room.id,
                frontA.Actor,
                party,
                actor => formation[actor],
                200L,
                activation.Transaction,
                out _),
            "Room scope must accept a living expedition ally anchor.");
        Require(HasManualBuff(frontA.Actor, room.id, 200L)
                && HasManualBuff(frontB.Actor, room.id, 200L)
                && !HasManualBuff(middle.Actor, room.id, 200L)
                && !HasManualBuff(rear.Actor, room.id, 200L),
            "Room scope must project to the anchor's expedition formation only.");

        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                square5.id,
                frontA.Actor,
                party,
                actor => formation[actor],
                200L,
                activation.Transaction,
                out _),
            "5x5 scope must accept a living expedition ally anchor.");
        Require(HasManualBuff(frontA.Actor, square5.id, 200L)
                && HasManualBuff(frontB.Actor, square5.id, 200L)
                && HasManualBuff(middle.Actor, square5.id, 200L)
                && !HasManualBuff(rear.Actor, square5.id, 200L),
            "5x5 scope must project to the anchor and adjacent expedition formations.");

        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                square7.id,
                frontA.Actor,
                party,
                actor => formation[actor],
                200L,
                activation.Transaction,
                out _),
            "7x7 scope must accept a living expedition ally anchor.");
        Require(party.Skip(1).All(actor => HasManualBuff(
                    actor,
                    square7.id,
                    200L))
                && !HasManualBuff(source.Actor, square7.id, 200L),
            "7x7 scope must project to every living legal expedition ally.");

        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                random.id,
                null,
                party,
                actor => formation[actor],
                200L,
                activation.Transaction,
                out _),
            "Deterministic random expedition targeting must choose one legal ally.");
        string firstRandomTarget = party.Skip(1)
            .Single(actor => HasManualBuff(actor, random.id, 200L))
            .Identity.PersistentId;
        ClearManualSkillState(source.Actor, party, random.id);
        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                random.id,
                null,
                party,
                actor => formation[actor],
                999L,
                activation.Transaction,
                out _),
            "Reset deterministic random expedition targeting must remain usable.");
        string secondRandomTarget = party.Skip(1)
            .Single(actor => HasManualBuff(actor, random.id, 999L))
            .Identity.PersistentId;
        Require(string.Equals(firstRandomTarget, secondRandomTarget,
                StringComparison.Ordinal),
            "Expedition random targeting must not reroll when only time changes.");

        Require(CharacterManualSkillRuntime.TryActivateForExpedition(
                source.Actor,
                all.id,
                null,
                party,
                actor => formation[actor],
                200L,
                activation.Transaction,
                out _),
            "All-eligible expedition targeting must activate without a click.");
        Require(party.Skip(1).All(actor => HasManualBuff(actor, all.id, 200L))
                && !HasManualBuff(source.Actor, all.id, 200L),
            "All-eligible expedition targeting must affect every living legal ally.");
        return true;
    }

    private static bool HasManualBuff(
        CharacterActor actor,
        string skillId,
        long absoluteHour) => CharacterManualSkillRuntime
        .GetActiveBuffSkills(actor, absoluteHour)
        .Any(value => value != null
            && string.Equals(value.id, skillId, StringComparison.Ordinal));

    private static void ClearManualSkillState(
        CharacterActor source,
        IEnumerable<CharacterActor> actors,
        string skillId)
    {
        CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(source.Progression).useLimits.manualSkillCooldowns
            .RemoveAll(value => value != null
                && string.Equals(value.skillId, skillId, StringComparison.Ordinal));
        foreach (CharacterActor actor in actors)
        {
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(actor.Progression).useLimits.manualSkillBuffs
                .RemoveAll(value => value != null
                    && string.Equals(value.skillId, skillId, StringComparison.Ordinal));
        }
    }

    private static bool VerifyAcquiredTraitExperienceScore()
    {
        CharacterNarrativeLedger ledger = new CharacterNarrativeLedger();
        ledger.Record(
            CharacterNarrativeDomain.Work,
            "work:craft-furniture",
            "building:chair-a",
            "completed");
        ledger.Record(
            CharacterNarrativeDomain.Work,
            "work:craft-furniture",
            "building:chair-b",
            "completed");
        ledger.Record(
            CharacterNarrativeDomain.Work,
            "work:craft-furniture",
            "building:chair-c",
            "completed");

        Require(
            ledger.MeaningfulRecordCount == 3,
            "The fixture no longer proves that subject-specific rows are distinct in the source ledger.");
        Require(
            CharacterAcquiredTraitExperienceScore.Require(ledger) == 2,
            "Three copies of one experience type must cross only the 1 and 3 aggregate thresholds.");

        for (int index = 3; index < 8; index++)
        {
            ledger.Record(
                CharacterNarrativeDomain.Work,
                "work:craft-furniture",
                $"building:chair-{index}",
                "completed");
        }
        Require(
            CharacterAcquiredTraitExperienceScore.Require(ledger) == 3,
            "Changing only the target identity must not farm more than the aggregate 1, 3, and 8 thresholds.");

        for (int index = 0; index < 3; index++)
        {
            ledger.Record(
                CharacterNarrativeDomain.Survival,
                "survival:hunt",
                $"wildlife:wolf-{index}",
                "completed");
        }
        Require(
            CharacterAcquiredTraitExperienceScore.Require(ledger) == 5,
            "A distinct experience type must contribute its own aggregate 1 and 3 thresholds.");

        CharacterNarrativeFact corrupted = ledger.facts[0];
        corrupted.milestoneCount++;
        Require(
            !CharacterAcquiredTraitExperienceScore.TryCalculate(
                ledger,
                out _,
                out string failure)
            && failure.Contains("expected exactly", StringComparison.Ordinal),
            "Acquired-trait scoring accepted a forged source-ledger milestone count.");
        return true;
    }

    private static bool VerifyAcquiredTraitBoundedEvidenceWindow()
    {
        List<(CharacterNarrativeDomain Domain, string FactId, float Importance)>
            source = new();
        for (int index = 0; index < 30; index++)
        {
            source.Add((
                CharacterNarrativeDomain.Work,
                $"work:window-{index:00}",
                100f - index));
        }
        source.Add((CharacterNarrativeDomain.Combat,
            "combat:window-representative", 1f));
        source.Add((CharacterNarrativeDomain.Relationship,
            "relationship:window-representative", 1f));
        source.Add((CharacterNarrativeDomain.Injury,
            "injury:window-representative", 1f));

        CharacterNarrativeLedger forward = BuildEvidenceWindowLedger(source);
        CharacterNarrativeLedger reverse = BuildEvidenceWindowLedger(
            source.AsEnumerable().Reverse());
        string[] selected = CharacterAcquiredTraitEvidenceWindow
            .SelectLegacyFactIds(
                forward,
                Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .ToArray();
        string[] repeated = CharacterAcquiredTraitEvidenceWindow
            .SelectLegacyFactIds(
                reverse,
                Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .ToArray();
        Require(
            selected.Length == NarrativeRequestContext.MaximumFacts
            && selected.SequenceEqual(repeated, StringComparer.Ordinal),
            "Acquired-trait evidence window is not capped and deterministic across ledger insertion order.");
        Require(
            selected.Contains("combat:window-representative", StringComparer.Ordinal)
            && selected.Contains(
                "relationship:window-representative", StringComparer.Ordinal)
            && selected.Contains("injury:window-representative", StringComparer.Ordinal),
            "Acquired-trait evidence window lost domain diversity to high-importance work facts.");

        GameplayOutcomeEvidenceBindingSnapshot[] reservedExact =
            Enumerable.Range(0, 5)
                .Select(index => new GameplayOutcomeEvidenceBindingSnapshot
                {
                    publicFactId = $"public-fact:sha256:reserved-{index:00}"
                })
                .ToArray();
        Require(
            CharacterAcquiredTraitEvidenceWindow.SelectLegacyFactIds(
                    forward,
                    reservedExact)
                .Count
            == NarrativeRequestContext.MaximumFacts - reservedExact.Length,
            "Acquired-trait legacy evidence did not reserve room for exact gameplay outcomes.");

        bool rejectedOverflow = false;
        try
        {
            CharacterAcquiredTraitEvidenceWindow.SelectLegacyFactIds(
                forward,
                Enumerable.Range(
                        0,
                        CharacterAcquiredTraitEvidenceWindow
                            .MaximumExactEvidenceFacts + 1)
                    .Select(index => new GameplayOutcomeEvidenceBindingSnapshot
                    {
                        publicFactId = $"public-fact:sha256:overflow-{index:00}"
                    }));
        }
        catch (InvalidOperationException exception)
        {
            rejectedOverflow = exception.Message.Contains(
                "exceeds its reserved public window",
                StringComparison.Ordinal);
        }
        Require(rejectedOverflow,
            "Acquired-trait evidence accepted an exact-evidence overflow instead of failing closed.");
        return true;
    }

    private static CharacterNarrativeLedger BuildEvidenceWindowLedger(
        IEnumerable<(CharacterNarrativeDomain Domain, string FactId,
            float Importance)> source)
    {
        CharacterNarrativeLedger ledger = new();
        int day = 0;
        foreach (var item in source)
        {
            ledger.Record(
                item.Domain,
                item.FactId,
                "character:qa-evidence-window",
                "completed",
                0f,
                day++,
                new CharacterNarrativeEvidenceMetadata(
                    "event:" + item.FactId,
                    "action:" + item.FactId,
                    string.Empty,
                    item.Importance));
        }
        return ledger;
    }

    private static bool VerifyAcquiredTraitFormulaV2Composition()
    {
        LoadAuthoredAcquiredTraitContent(
            out CharacterAcquiredTraitSettingsSO settings,
            out CharacterAcquiredTraitModuleSO[] modules);
        Require(settings.FormulaPolicy.formulaVersion >= 2
                && settings.DrawbackCapabilities.Count >= 3,
            "The authored acquired-trait catalog does not expose formula v2 compositional drawbacks.");

        using ActorFixture negative = new(
            812570,
            "TraitV2Negative",
            "Slime",
            persistentId: "character:qa-trait-v2-negative");
        for (int index = 0; index < 3; index++)
            negative.Actor.Progression.RecordNarrative(
                CharacterNarrativeDomain.Combat,
                "combat:injury:qa-" + index,
                "target:qa-combat",
                "injury",
                day: index + 1,
                triggerPassives: false);
        string[] negativeEvidence = negative.Actor.Progression.NarrativeLedger.Facts
            .Select(CharacterAcquiredTraitEvidenceProjection.Project)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CharacterAcquiredTraitPendingRequestState first =
            CharacterAcquiredTraitFormulaGeneration.Freeze(
                negative.Actor.Progression,
                "request:qa-trait-v2-negative",
                "request-key:qa-trait-v2-negative",
                3,
                settings,
                modules,
                negativeEvidence);
        CharacterAcquiredTraitPendingRequestState repeated =
            CharacterAcquiredTraitFormulaGeneration.Freeze(
                negative.Actor.Progression,
                "request:qa-trait-v2-negative",
                "request-key:qa-trait-v2-negative",
                3,
                settings,
                modules,
                negativeEvidence);
        Require(first.formulaVersion >= 2
                && first.benefitModuleIds.Count == 1
                && first.drawbackCapabilityIds.Count == 1
                && first.drawbackCredit > 0
                && first.formulaCapabilities.Count == 2
                && string.Equals(JsonUtility.ToJson(first),
                    JsonUtility.ToJson(repeated), StringComparison.Ordinal),
            "Formula v2 did not deterministically freeze one benefit and one credited drawback.");
        CharacterAcquiredTraitDrawbackCapabilityDefinition selectedDrawback =
            settings.DrawbackCapabilities.Single(value => string.Equals(
                value.DrawbackId,
                first.drawbackCapabilityIds[0],
                StringComparison.Ordinal));
        Require(selectedDrawback.DomainAffinities.Contains(
                CharacterNarrativeDomain.Combat)
                && string.Equals(first.drawbackId,
                    selectedDrawback.DrawbackId, StringComparison.Ordinal),
            "Formula v2 selected a drawback outside the negative evidence domain.");
        CharacterAcquiredTraitModuleSO selectedBenefit = modules.Single(value =>
            string.Equals(value.ModuleId, first.benefitModuleIds[0],
                StringComparison.Ordinal));
        Require(!first.effectOverrides.Any(value =>
                selectedBenefit.DrawbackBindingIds.Contains(
                    value.bindingId, StringComparer.Ordinal))
                && selectedDrawback.Effects.All(effect => first.effectOverrides.Any(
                    value => string.Equals(value.bindingId, effect.bindingId,
                        StringComparison.Ordinal))),
            "Formula v2 leaked a legacy fixed drawback or omitted the selected independent drawback.");

        using ActorFixture positive = new(
            812571,
            "TraitV2Positive",
            "Slime",
            persistentId: "character:qa-trait-v2-positive");
        for (int index = 0; index < 3; index++)
            positive.Actor.Progression.RecordNarrative(
                CharacterNarrativeDomain.Combat,
                "combat:victory:qa-" + index,
                "target:qa-combat",
                "completed",
                day: index + 1,
                triggerPassives: false);
        string[] positiveEvidence = positive.Actor.Progression.NarrativeLedger.Facts
            .Select(CharacterAcquiredTraitEvidenceProjection.Project)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CharacterAcquiredTraitPendingRequestState noDrawback =
            CharacterAcquiredTraitFormulaGeneration.Freeze(
                positive.Actor.Progression,
                "request:qa-trait-v2-positive",
                "request-key:qa-trait-v2-positive",
                3,
                settings,
                modules,
                positiveEvidence);
        Require(noDrawback.drawbackCapabilityIds.Count == 0
                && noDrawback.drawbackCredit == 0
                && string.IsNullOrEmpty(noDrawback.drawbackId)
                && noDrawback.formulaCapabilities.Count == 1,
            "Formula v2 invented a drawback without compatible negative evidence.");
        return true;
    }

    private static bool VerifyAcquiredTraitMilestoneLifecycle()
    {
        using ActorFixture actor = new ActorFixture(
            812503,
            "Acquired Trait Fixture",
            "human");
        using AcquiredTraitContentFixture content =
            new AcquiredTraitContentFixture();
        CharacterProgression progression = actor.Actor.Progression;
        string targetId = actor.Actor.Identity.PersistentId;
        string[] evidence =
        {
            "work:craft-furniture",
            "work:operate-kitchen",
            "work:repair-equipment",
            "work:research-ritual"
        };
        foreach (string factId in evidence)
        {
            for (int count = 0; count < 50; count++)
            {
                progression.RecordNarrative(
                    CharacterNarrativeDomain.Work,
                    factId,
                    $"target:{factId}:{count}",
                    "completed",
                    1f,
                    count / 10);
            }
        }
        Require(
            CharacterAcquiredTraitExperienceScore.Require(
                progression.NarrativeLedger) == 20,
            "The milestone fixture did not reach the exact 20-point acquired-trait score.");

        CharacterAcquiredTraitInferenceService service = new(
            content.Settings,
            content.Modules);
        CharacterAcquiredTraitInferenceCommandResult outOfOrder =
            service.SubmitMilestone(
                progression,
                BuildAcquiredSubmission(progression, targetId, 8, 0, evidence));
        Require(
            !outOfOrder.Succeeded
            && outOfOrder.Audit.IssueCode
                == CharacterAcquiredTraitInferenceIssueCode.OutOfOrderMilestone,
            "A reached later milestone bypassed the lowest unprocessed milestone.");

        CharacterAcquiredTraitInferenceCommandResult firstSubmission =
            service.SubmitMilestone(
                progression,
                BuildAcquiredSubmission(progression, targetId, 3, 0, evidence));
        Require(firstSubmission.Succeeded && firstSubmission.Packet != null,
            "The first acquired-trait milestone was not submitted.");
        CharacterAcquiredTraitInferenceCommandResult concurrent =
            service.SubmitMilestone(
                progression,
                BuildAcquiredSubmission(
                    progression,
                    targetId,
                    3,
                    1,
                    evidence,
                    "parallel"));
        Require(
            !concurrent.Succeeded
            && concurrent.Audit.IssueCode
                == CharacterAcquiredTraitInferenceIssueCode.PendingRequestInFlight,
            "A second acquired-trait request was accepted while one was pending.");

        CharacterAcquiredTraitInferenceCommandResult firstCompletion =
            CompleteAcquiredMilestone(
                service,
                progression,
                targetId,
                firstSubmission,
                1);
        Require(firstCompletion.Succeeded,
            "The first acquired-trait milestone did not complete.");
        CharacterAcquiredTraitAggregateState afterFirst =
            progression.CaptureAcquiredTraitState();
        Require(
            afterFirst.revision == 2
            && afterFirst.ActiveCount == 1
            && afterFirst.processedMilestones.SequenceEqual(new[] { 3 }),
            "The first milestone did not commit exactly one active acquired trait.");

        CharacterAcquiredTraitInferenceCommandResult duplicateCompletion =
            CompleteAcquiredMilestone(
                service,
                progression,
                targetId,
                firstSubmission,
                1);
        Require(
            !duplicateCompletion.Succeeded
            && duplicateCompletion.Audit.IssueCode
                == CharacterAcquiredTraitInferenceIssueCode.DuplicateCallback
            && progression.CaptureAcquiredTraitState().revision == 2,
            "A duplicate completion callback changed acquired-trait state.");

        CompleteNextAcquiredMilestone(
            service,
            progression,
            targetId,
            8,
            evidence);
        CompleteNextAcquiredMilestone(
            service,
            progression,
            targetId,
            20,
            evidence);

        CharacterAcquiredTraitAggregateState completed =
            progression.CaptureAcquiredTraitState();
        Require(
            completed.revision == 6
            && completed.ActiveCount == 3
            && completed.processedMilestones.SequenceEqual(new[] { 3, 8, 20 }),
            "The 3/8/20 milestones did not complete once each in order.");
        Require(
            completed.CaptureActiveInstances()
                .SelectMany(value => value.moduleIds)
                .Distinct(StringComparer.Ordinal)
                .Count()
            == completed.CaptureActiveInstances()
                .Sum(value => value.moduleIds.Count),
            "An active acquired-trait module was selected more than once.");

        IReadOnlyList<IGameplayEffectSource> sources =
            CharacterAcquiredTraitEffectSourceProjection.Project(
                completed,
                progression.NarrativeLedger,
                content.Settings,
                content.Modules);
        Require(
            sources.Count == 3
            && sources.Select(value => value.SourceRef.SourceId)
                .Distinct(StringComparer.Ordinal)
                .Count() == 3,
            "Acquired traits did not project one independently removable effect source per instance.");

        CharacterProgressionSnapshot snapshot = progression.CapturePersistentState();
        using ActorFixture restoredActor = new ActorFixture(
            812504,
            "Acquired Trait Restore Fixture",
            "human");
        restoredActor.Actor.Progression.RestorePersistentState(snapshot);
        CharacterAcquiredTraitAggregateState restored = restoredActor.Actor
            .Progression.CaptureAcquiredTraitState();
        Require(
            restored.revision == completed.revision
            && restored.processedMilestones.SequenceEqual(
                completed.processedMilestones)
            && restored.instances.Select(value => value.instanceId)
                .SequenceEqual(
                    completed.instances.Select(value => value.instanceId),
                    StringComparer.Ordinal)
            && restored.instances.Select(value => value.combinationId)
                .SequenceEqual(
                    completed.instances.Select(value => value.combinationId),
                    StringComparer.Ordinal)
            && restored.instances.SelectMany(value => value.evidenceFactIds)
                .SequenceEqual(
                    completed.instances.SelectMany(value => value.evidenceFactIds),
                    StringComparer.Ordinal),
            "Acquired-trait milestones, identities, combinations, or evidence changed across save round trip.");
        return true;
    }

    private static CharacterAcquiredTraitSubmissionCommand BuildAcquiredSubmission(
        CharacterProgression progression,
        string targetId,
        int milestone,
        int expectedRevision,
        IEnumerable<string> evidence,
        string suffix = null)
    {
        string token = string.IsNullOrEmpty(suffix)
            ? milestone.ToString()
            : $"{milestone}:{suffix}";
        NarrativePublicContextMaterial publicMaterial =
            CharacterAcquiredTraitPromptBuilder.BuildPublicMaterialForLedgerEvidence(
                NarrativeRequestContextTestFixture.CreateEmpty(), progression,
                evidence);
        IReadOnlyList<string> projectedEvidence =
            CharacterAcquiredTraitPromptBuilder.BuildProjectedEvidenceFactIds(
                publicMaterial,
                evidence);
        return new CharacterAcquiredTraitSubmissionCommand(
            targetId,
            $"request:acquired:{token}",
            $"request-key:acquired:{token}",
            milestone,
            projectedEvidence,
            expectedRevision,
            NarrativeInferenceTimestamp.FromGameTick(milestone));
    }

    private static bool VerifyAcquiredTraitOutcomeFailureRollback()
    {
        using ActorFixture actor = new ActorFixture(
            812505,
            "Acquired Trait Outcome Rollback Fixture",
            "human");
        using AcquiredTraitContentFixture content =
            new AcquiredTraitContentFixture();
        CharacterProgression progression = actor.Actor.Progression;
        string targetId = actor.Actor.Identity.PersistentId;
        string[] evidence =
        {
            "work:outcome-rollback-a",
            "work:outcome-rollback-b",
            "work:outcome-rollback-c",
            "work:outcome-rollback-d"
        };
        foreach (string factId in evidence)
        {
            for (int count = 0; count < 50; count++)
            {
                progression.RecordNarrative(
                    CharacterNarrativeDomain.Work,
                    factId,
                    $"target:{factId}:{count}",
                    "completed",
                    1f,
                    count / 10);
            }
        }

        CharacterAcquiredTraitInferenceService service = new(
            content.Settings,
            content.Modules);
        CharacterAcquiredTraitInferenceCommandResult submission =
            service.SubmitMilestone(
                progression,
                BuildAcquiredSubmission(
                    progression,
                    targetId,
                    3,
                    0,
                    evidence,
                    "outcome-rollback"));
        Require(submission.Succeeded && submission.Packet != null,
            "The outcome rollback fixture could not submit its milestone.");

        CharacterAcquiredTraitAggregateState before = progression
            .CaptureAcquiredTraitState();
        int[] influenceBefore = progression.NarrativeLedger.facts
            .Select(fact => fact.influenceUseCount)
            .ToArray();
        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:trait-outcome-fault-editor");
        using DomainFactDispatcher traitFacts = new();
        GameplayDomainFactObserverBinding<CharacterTraitInferredFact,
            AcquiredTraitInferenceOutcomeReceipt> CreateBinding() =>
            CharacterTraitInferredFactMapping.CreateBinding(
                outcomes.Registry,
                outcomes.Ledger,
                outcomes.Recorder,
                new GameplayDomainFactCommitGroup(
                    outcomes.Recorder,
                    outcomes.Ledger));
        IDisposable binding = CreateBinding().Connect(traitFacts);
        try
        {
            var publisher = new FaultingTraitInferencePublisher(traitFacts);
            var publications = new CharacterTraitInferencePublicationRuntime(
                publisher,
                publisher);

            bool duplicateObserverRejected = false;
            try
            {
                using IDisposable duplicate = CreateBinding().Connect(traitFacts);
            }
            catch (InvalidOperationException exception)
            {
                duplicateObserverRejected = exception.Message.Contains(
                    "Duplicate durable domain observer",
                    StringComparison.Ordinal);
            }
            Require(duplicateObserverRejected,
                "The dispatcher accepted a duplicate trait-inference observer.");

            binding.Dispose();
            binding = null;
            CharacterAcquiredTraitInferenceCommandResult missingObserver =
                CompleteAcquiredMilestone(
                    service,
                    progression,
                    targetId,
                    submission,
                    1,
                    publisher,
                    publications);
            RequireCommitRejectedWithoutTraitMutation(
                missingObserver,
                progression,
                before,
                influenceBefore,
                "missing trait-inference observer");
            binding = CreateBinding().Connect(traitFacts);

            publisher.ThrowNextPublishBeforeCommit = true;
            CharacterAcquiredTraitInferenceCommandResult precommit =
                CompleteAcquiredMilestone(
                    service,
                    progression,
                    targetId,
                    submission,
                    1,
                    publisher,
                    publications);
            RequireCommitRejectedWithoutTraitMutation(
                precommit,
                progression,
                before,
                influenceBefore,
                "escaped precommit trait-inference failure");
            Require(precommit.Audit.ValidationError.Contains(
                    "editor-injected-trait-precommit-exception",
                    StringComparison.Ordinal),
                "The precommit trait-inference rejection hid its original failure.");

            publisher.RejectNextPublish = true;
            CharacterAcquiredTraitInferenceCommandResult rejected =
                CompleteAcquiredMilestone(
                    service,
                    progression,
                    targetId,
                    submission,
                    1,
                    publisher,
                    publications);
            RequireCommitRejectedWithoutTraitMutation(
                rejected,
                progression,
                before,
                influenceBefore,
                "known trait-inference publication rejection");
            GameplayOutcomeLedgerDiagnostics failedDiagnostics = outcomes.Ledger
                .GetDiagnostics();
            Require(publications.Capture().observations.Count == 0
                    && failedDiagnostics.ReservedCount == 0
                    && failedDiagnostics.ActiveAnchorReservationCount == 0
                    && failedDiagnostics.ActiveInfluenceReservationCount == 0,
                "Rejected trait inference retained a journal row or reservation.");

            publisher.HideNextCommittedAcknowledgement = true;
            CharacterAcquiredTraitInferenceCommandResult accepted =
                CompleteAcquiredMilestone(
                    service,
                    progression,
                    targetId,
                    submission,
                    1,
                    publisher,
                    publications);
            CharacterAcquiredTraitAggregateState completed = progression
                .CaptureAcquiredTraitState();
            GameplayResultKey expectedKey = new(
                EvolutionOutcomeIds.TraitInferenceProducerId,
                new GameplayOperationId(
                    "trait-inference:" + targetId + ":revision:00000002"),
                2L,
                0);
            CharacterTraitInferencePublicationData pending = publications
                .PendingForCharacter(targetId);
            Require(
                accepted.Succeeded
                && completed.revision == 2
                && completed.ActiveCount == 1
                && completed.pendingRequests.Count == 0
                && pending != null
                && pending.pending
                && pending.fact.characterId == targetId
                && pending.fact.beforeRevision == 1
                && pending.fact.afterRevision == 2
                && string.IsNullOrEmpty(pending.publicationId)
                && string.IsNullOrEmpty(pending.fingerprint)
                && outcomes.Ledger.TryGetResultIdentity(
                    expectedKey,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && outcomes.Ledger.GetDiagnostics().KnownResultKeyCount == 1,
                "Lost trait-inference acknowledgement did not preserve one owner effect and exact pending fact.");

            CharacterProgressionSnapshot acceptedOwner = progression
                .CapturePersistentState();
            CharacterAcquiredTraitAggregateState savedTraits =
                DungeonSaveJson.Deserialize<CharacterAcquiredTraitAggregateState>(
                    DungeonCharacterWorldSaveCodec.SerializeFields(
                        acceptedOwner.AcquiredTraitState));
            CharacterNarrativeLedger savedNarrative =
                DungeonSaveJson.Deserialize<CharacterNarrativeLedger>(
                    DungeonCharacterWorldSaveCodec.SerializeFields(
                        acceptedOwner.NarrativeLedger));
            string journalJson = new CharacterTraitInferencePublicationSaveSection(
                publications).Capture();
            CharacterTraitInferencePublicationSaveSection.ValidateJson(journalJson);
            CharacterTraitInferencePublicationSaveData savedPublications =
                DungeonSaveJson.Deserialize<
                    CharacterTraitInferencePublicationSaveData>(journalJson);
            Require(savedPublications.observations.Count == 1
                    && savedPublications.observations[0].pending,
                "Pending trait-inference publication did not survive its strict save boundary.");
            CharacterTraitInferredFactPreflight.ValidateObservation(
                savedPublications.observations[0],
                outcomes.Ledger.CaptureGameplayOutcomes());

            CharacterTraitInferencePublicationSaveData tampered =
                DungeonSaveJson.Deserialize<
                    CharacterTraitInferencePublicationSaveData>(journalJson);
            tampered.observations[0].publicationKey += ":tampered";
            bool tamperedKeyRejected = false;
            try
            {
                CharacterTraitInferredFactPreflight.ValidateObservation(
                    tampered.observations[0],
                    outcomes.Ledger.CaptureGameplayOutcomes());
            }
            catch (InvalidOperationException exception)
            {
                tamperedKeyRejected = exception.Message.Contains(
                    "publication-key-conflict",
                    StringComparison.Ordinal);
            }
            Require(tamperedKeyRejected,
                "Tampered trait-inference publication key joined canonical evidence.");

            using ActorFixture restored = new(
                812506,
                "Acquired Trait Outcome Rollback Fixture",
                "human",
                persistentId: targetId,
                detachedRestore: true);
            restored.Actor.Progression.RestorePersistentState(
                new CharacterProgressionSnapshot(
                    acceptedOwner.Level,
                    acceptedOwner.CurrentExperience,
                    acceptedOwner.GrowthState,
                    savedNarrative,
                    savedTraits));
            restored.Actor.PublishDetachedRestore();
            var restoredPublications =
                new CharacterTraitInferencePublicationRuntime(
                    publisher,
                    publisher);
            restoredPublications.Restore(savedPublications);
            int restoredInfluence = restored.Actor.Progression.NarrativeLedger
                .Facts.Sum(value => value?.influenceUseCount ?? 0);
            Require(restoredPublications.RecoverPending(out string recoveryFailure),
                "Saved trait-inference observation did not replay: "
                    + recoveryFailure);
            CharacterTraitInferencePublicationData recovered =
                restoredPublications.Capture().observations.Single();
            Require(restoredPublications.PendingForCharacter(targetId) == null
                    && !recovered.pending
                    && !string.IsNullOrWhiteSpace(recovered.publicationId)
                    && recovered.fingerprint.Length == 64
                    && restored.Actor.Progression.AcquiredTraitRevision == 2
                    && restored.Actor.Progression.CaptureAcquiredTraitState()
                        .ActiveCount == 1
                    && restored.Actor.Progression.NarrativeLedger.Facts.Sum(
                        value => value?.influenceUseCount ?? 0)
                        == restoredInfluence
                    && publisher.CommittedPublishCalls == 1
                    && publisher.ReplayBatchCalls == 1,
                "Exact trait-inference recovery repeated effects or skipped canonical replay.");
            string recoveredOwner = JsonUtility.ToJson(
                restored.Actor.Progression.CaptureAcquiredTraitState());
            Require(restoredPublications.RecoverPending(out string repeatedFailure)
                    && repeatedFailure == string.Empty
                    && publisher.ReplayBatchCalls == 1
                    && recoveredOwner == JsonUtility.ToJson(
                        restored.Actor.Progression.CaptureAcquiredTraitState()),
                "Repeated trait-inference recovery changed its owner or replayed the fact twice.");
            return true;
        }
        finally
        {
            binding?.Dispose();
        }
    }

    private static void RequireCommitRejectedWithoutTraitMutation(
        CharacterAcquiredTraitInferenceCommandResult result,
        CharacterProgression progression,
        CharacterAcquiredTraitAggregateState before,
        IReadOnlyList<int> influenceBefore,
        string scenario)
    {
        CharacterAcquiredTraitAggregateState after = progression
            .CaptureAcquiredTraitState();
        Require(!result.Succeeded
                && result.Audit.IssueCode
                    == CharacterAcquiredTraitInferenceIssueCode.CommitRejected
                && after.revision == before.revision
                && after.ActiveCount == before.ActiveCount
                && after.pendingRequests.Count == before.pendingRequests.Count
                && progression.NarrativeLedger.facts
                    .Select(fact => fact.influenceUseCount)
                    .SequenceEqual(influenceBefore),
            "The " + scenario
                + " did not roll back trait and evidence state atomically: "
                + result.Audit.ValidationError);
    }

    private static CharacterAcquiredTraitInferenceCommandResult
        CompleteAcquiredMilestone(
            CharacterAcquiredTraitInferenceService service,
            CharacterProgression progression,
            string targetId,
            CharacterAcquiredTraitInferenceCommandResult submission,
            int expectedRevision,
            IDomainFactPublisher facts = null,
            CharacterTraitInferencePublicationRuntime publications = null)
    {
        CharacterAcquiredTraitRequestPacketDto packet = submission.Packet;
        CharacterAcquiredTraitAggregateState state = progression
            .CaptureAcquiredTraitState();
        state.TryGetPendingRequest(
            packet.requestId,
            out CharacterAcquiredTraitPendingRequestState pending);
        CharacterAcquiredTraitInstanceState completed = state.instances
            .FirstOrDefault(value => value != null
                && string.Equals(
                    value.originatingRequestId,
                    packet.requestId,
                    StringComparison.Ordinal));
        Require(pending != null || completed != null,
            "The acquired-trait completion fixture cannot resolve its pending or completed request.");
        string response;
        if (pending?.presentationState
            == CharacterAcquiredTraitPresentationState.ModuleSelectionPending)
        {
            response = JsonUtility.ToJson(
                new CharacterAcquiredTraitModuleSelectionResponseDto
                {
                    selectionId = pending.moduleSelectionId,
                    positiveModuleIds = new List<string>
                    {
                        pending.offeredBenefitModuleIds[0]
                    },
                    drawbackModuleIds = new List<string>(),
                    evidenceFactIds = new List<string> { pending.evidenceFactIds[0] },
                    displayName = "후천 특성 발현",
                    narrativeFlavor = "반복된 경험이 새로운 성향으로 발현되었다."
                });
        }
        else if ((pending?.formulaVersion ?? completed.formulaVersion) > 0)
        {
            response = JsonUtility.ToJson(
                new CharacterAcquiredTraitFormulaPresentationDto
                {
                    presentationId = pending?.presentationId
                        ?? completed.presentationId,
                    displayName = "후천 특성 발현",
                    narrativeFlavor = "반복된 경험이 새로운 성향으로 발현되었다."
                });
        }
        else
        {
            CharacterAcquiredTraitCombinationPacketDto selected =
                packet.combinationOptions[0];
            response = JsonUtility.ToJson(new CharacterAcquiredTraitResponseDto
            {
                combinationId = selected.combinationId,
                displayName = $"후천 특성 {packet.manifestationMilestone}",
                description = "반복된 경험이 새로운 성향으로 발현되었다.",
                narrativeReason = "검증된 경험 장부를 근거로 선택되었다.",
                evidenceFactIds = new List<string> { packet.evidenceFactIds[0] }
            });
        }
        CharacterAcquiredTraitCompletionCommand command = new(
            targetId,
            packet.requestId,
            packet.requestKey,
            packet.candidatePacketHash,
            expectedRevision,
            expectedRevision,
            response,
            NarrativeInferenceTimestamp.FromGameTick(
                packet.manifestationMilestone + 1L));
        if (facts != null || publications != null)
        {
            Require(facts != null && publications != null,
                "Trait-inference completion requires matching fact and publication ports.");
            return service.CompleteMilestone(
                progression,
                command,
                packet,
                facts,
                publications);
        }
        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:trait-progression-editor:" + targetId + ":" + expectedRevision);
        return service.CompleteMilestone(
            progression,
            command,
            packet,
            outcomes.TraitInferenceFacts,
            outcomes.TraitInferences);
    }

    private static void CompleteNextAcquiredMilestone(
        CharacterAcquiredTraitInferenceService service,
        CharacterProgression progression,
        string targetId,
        int milestone,
        IEnumerable<string> evidence)
    {
        int submissionRevision = progression.AcquiredTraitRevision;
        CharacterAcquiredTraitInferenceCommandResult submission =
            service.SubmitMilestone(
                progression,
                BuildAcquiredSubmission(
                    progression,
                    targetId,
                    milestone,
                    submissionRevision,
                    evidence));
        Require(submission.Succeeded,
            $"Acquired-trait milestone {milestone} submission failed: "
            + submission.Audit.ValidationError);
        CharacterAcquiredTraitInferenceCommandResult completion =
            CompleteAcquiredMilestone(
                service,
                progression,
                targetId,
                submission,
                submissionRevision + 1);
        Require(completion.Succeeded,
            $"Acquired-trait milestone {milestone} completion failed: "
            + completion.Audit.ValidationError);
    }

    private static bool VerifyMemoryErasureSealAtomicity()
    {
        VerifyMemoryErasureSealSuccessAndIdempotence();
        VerifyMemoryErasureSealPhysicalFailureLeavesEverythingUnchanged();
        VerifyMemoryErasureSealObserverFailureDoesNotUndoCommittedErasure();
        VerifyMemoryErasureSealFactStageFailureRollsBackEverything();
        VerifyMemoryErasureSealLostAcknowledgementRecovery();
        return true;
    }

    private static bool VerifyAuthoredAcquiredTraitEffectProjection()
    {
        LoadAuthoredAcquiredTraitContent(
            out CharacterAcquiredTraitSettingsSO settings,
            out CharacterAcquiredTraitModuleSO[] modules);
        Require(
            settings.ValidateDefinition().Count == 0
            && modules.Length == 8
            && modules.All(module => module.ValidateDefinition().Count == 0),
            "The authored acquired-trait catalog is missing or invalid.");

        int projectedBindingCount = 0;
        foreach (CharacterAcquiredTraitModuleSO module in modules)
        {
            CharacterNarrativeDomain domain = module.DomainAffinities[0];
            string token = module.ModuleId.Substring(
                module.ModuleId.LastIndexOf(':') + 1);
            string[] evidence =
            {
                $"qa:acquired:{token}:a",
                $"qa:acquired:{token}:b",
                $"qa:acquired:{token}:c"
            };
            CharacterNarrativeLedger ledger = new();
            foreach (string factId in evidence)
            {
                ledger.Record(
                    domain,
                    factId,
                    "target:qa:authored-acquired",
                    "completed");
            }

            string combinationId = CharacterAcquiredTraitCombinationIdentity
                .Build(new[] { module.ModuleId });
            CharacterAcquiredTraitAggregateState state = new()
            {
                revision = 1,
                processedMilestones = new List<int> { 3 },
                instances = new List<CharacterAcquiredTraitInstanceState>
                {
                    new()
                    {
                        instanceId = $"acquired-trait-instance:qa:{token}",
                        combinationId = combinationId,
                        moduleIds = new List<string> { module.ModuleId },
                        displayName = module.DisplayName,
                        description = module.Description,
                        narrativeReason = "작성된 경험이 후천 특성으로 발현되었다.",
                        evidenceFactIds = evidence.OrderBy(
                            value => value,
                            StringComparer.Ordinal).ToList(),
                        manifestationMilestone = 3,
                        originatingRequestId = $"request:qa:{token}",
                        originatingRequestKey = $"request-key:qa:{token}",
                        candidatePacketHash = NarrativeInferenceHash
                            .ComputeSha256Utf8($"packet:qa:{token}"),
                        selectionAuditId = $"audit:qa:{token}",
                        acceptedRevision = 1,
                        erasedAt = CharacterAcquiredTraitInstanceState
                            .NotErasedAtAbsoluteHour
                    }
                },
                pendingRequests = new List<CharacterAcquiredTraitPendingRequestState>()
            };
            int moduleEnumerations = 0;
            IEnumerable<CharacterAcquiredTraitModuleSO> ReadModulesOnce()
            {
                Require(++moduleEnumerations == 1,
                    "Effect projection enumerated its authored input more than once.");
                foreach (var definition in modules) yield return definition;
            }
            IReadOnlyList<IGameplayEffectSource> sources =
                CharacterAcquiredTraitEffectSourceProjection.Project(
                    state,
                    ledger,
                    settings,
                    ReadModulesOnce());
            Require(moduleEnumerations == 1, "Effect projection did not validate authored definitions.");
            Require(
                sources.Count == 1
                && sources[0].SourceRef.Kind
                    == GameplayEffectSourceKind.AcquiredTrait
                && string.Equals(
                    sources[0].SourceRef.SourceId,
                    state.instances[0].instanceId,
                    StringComparison.Ordinal)
                && sources[0].Effects.Count == module.Effects.Count,
                $"Authored module '{module.ModuleId}' did not project through the acquired-trait source boundary.");

            foreach (GameplayEffectBinding binding in module.Effects)
            {
                string conditionId = binding.condition?.ConditionId;
                GameplayEffectContext activeContext = new(
                    conditionId == null
                        ? Array.Empty<string>()
                        : new[] { conditionId });
                GameplayEffectProjectionResult projection =
                    CharacterGameplayEffectProjector.Resolve(
                        binding.definition.TargetId,
                        100f,
                        sources,
                        activeContext);
                GameplayEffectContribution contribution = projection
                    .Contributions.Single(value => string.Equals(
                        value.BindingId,
                        binding.bindingId,
                        StringComparison.Ordinal));
                Require(
                    !contribution.Suppressed
                    && Mathf.Approximately(
                        projection.Value,
                        ProjectSingleAuthoredBinding(100f, binding)),
                    $"Authored acquired-trait binding '{binding.bindingId}' did not reach its gameplay target with the authored value.");
                if (binding.condition != null)
                {
                    GameplayEffectProjectionResult inactive =
                        CharacterGameplayEffectProjector.Resolve(
                            binding.definition.TargetId,
                            100f,
                            sources,
                            new GameplayEffectContext());
                    Require(
                        inactive.Contributions.Single(value => string.Equals(
                            value.BindingId,
                            binding.bindingId,
                            StringComparison.Ordinal)).Suppressed,
                        $"Conditional acquired-trait binding '{binding.bindingId}' applied without its authored condition.");
                }
                projectedBindingCount++;
            }

            void RequireProjectionRejection(
                CharacterAcquiredTraitAggregateState candidate,
                CharacterNarrativeLedger candidateLedger,
                CharacterAcquiredTraitSettingsSO candidateSettings,
                IReadOnlyList<CharacterAcquiredTraitValidationIssue> expected,
                bool structuralFailure)
            {
                Require(expected.Count > 0, "Invalid projection fixture had no validation failure.");
                string before = JsonUtility.ToJson(candidate);
                string ledgerBefore = JsonUtility.ToJson(candidateLedger);
                moduleEnumerations = 0;
                string failure = null;
                try { CharacterAcquiredTraitEffectSourceProjection.Project(
                    candidate, candidateLedger, candidateSettings, ReadModulesOnce()); }
                catch (InvalidOperationException exception) { failure = exception.Message; }
                Require(failure == "Acquired-trait effect projection rejected invalid state: "
                        + string.Join(" | ", expected.Select(value => value.ToString()))
                    && moduleEnumerations == (structuralFailure ? 0 : 1)
                    && JsonUtility.ToJson(candidate) == before
                    && JsonUtility.ToJson(candidateLedger) == ledgerBefore,
                    "Effect projection changed its rejection priority, skipped validation, or mutated rejected input.");
            }

            // Run after a valid query: a warm query must not conceal later
            // malformed state, changed evidence, or absent definition authority.
            var malformed = state.Clone();
            malformed.formatVersion = -1;
            RequireProjectionRejection(malformed, ledger, null,
                CharacterAcquiredTraitStateValidator.ValidatePersistentState(malformed, ledger), true);
            var changedLedger = ledger.Clone();
            changedLedger.facts[0].sourceEventIds.Clear();
            RequireProjectionRejection(state, changedLedger, settings,
                CharacterAcquiredTraitStateValidator.ValidatePersistentState(state, changedLedger), true);
            RequireProjectionRejection(state, ledger, null,
                CharacterAcquiredTraitStateValidator.ValidateWithDefinitions(state, ledger, null, modules), false);
        }

        Require(
            projectedBindingCount == 9,
            $"Expected nine authored acquired-trait bindings, projected {projectedBindingCount}.");
        return true;
    }

    private static float ProjectSingleAuthoredBinding(
        float baseValue,
        GameplayEffectBinding binding)
    {
        float value = binding.definition.Operation switch
        {
            GameplayEffectOperation.AddFlat => baseValue + binding.value,
            GameplayEffectOperation.AddPercent => baseValue * (1f + binding.value),
            GameplayEffectOperation.Multiply => baseValue * binding.value,
            GameplayEffectOperation.Override => binding.value,
            GameplayEffectOperation.ClampMinimum => Mathf.Max(
                baseValue,
                binding.value),
            GameplayEffectOperation.ClampMaximum => Mathf.Min(
                baseValue,
                binding.value),
            _ => throw new InvalidOperationException(
                $"Unsupported authored acquired-trait operation '{binding.definition.Operation}'.")
        };
        return Mathf.Clamp(
            value,
            binding.definition.MinimumResult,
            binding.definition.MaximumResult);
    }

    private static bool VerifyAcquiredTraitAutomaticProducerResume()
    {
        LoadAuthoredAcquiredTraitContent(
            out CharacterAcquiredTraitSettingsSO settings,
            out CharacterAcquiredTraitModuleSO[] modules);
        CharacterProgressionSnapshot pendingSnapshot;
        string originalRequestId;
        string originalPrompt;
        const string PersistentId = "character:qa-acquired-producer-resume";
        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:trait-producer-resume-editor");
        using (ActorFixture source = new(
                   812508,
                   "Acquired Producer Source",
                   "human",
                   publishComposition: true,
                   persistentId: PersistentId))
        {
            foreach (string factId in Enumerable.Range(0, 33)
                         .Select(index => $"work:qa-producer-{index:00}"))
            {
                source.Actor.Progression.RecordNarrative(
                    CharacterNarrativeDomain.Work,
                    factId,
                    "target:qa:producer",
                    "completed");
            }

            DeferredAcquiredTraitLlmRuntime transport = new();
            using CharacterAcquiredTraitManifestationRuntime runtime = new(
                NarrativeRequestContextTestFixture.CreateEmpty(), new AcquiredTraitDefinitionSource(settings, modules),
                new FixedCharacterWorld(source.Actor),
                new TestLlmRuntimeProvider(transport),
                new FixedGameCalendar(day: 17, hour: 4),
                 new MutableUiClock(),
                 new GameEventBus(),
                 EmptyGameplayOutcomeNarrativeEvidenceQuery.Instance,
                 outcomes.TraitInferenceFacts,
                 outcomes.TraitInferences);
            runtime.Start();
            runtime.Tick();
            CharacterAcquiredTraitAggregateState pending = source.Actor
                .Progression.CaptureAcquiredTraitState();
            runtime.TryGetLatestDiagnostic(PersistentId, out var submissionDiagnostic);
            Require(
                runtime.PendingRequestCount == 1
                && transport.PendingCallbackCount == 1
                && pending.pendingRequests.Count == 1
                && pending.pendingRequests[0].evidenceFactIds.Count
                    == NarrativeRequestContext.MaximumFacts
                && pending.ActiveCount == 0,
                "The automatic acquired-trait producer did not cap, persist, and dispatch the reached milestone: "
                    + submissionDiagnostic.Status + ": " + submissionDiagnostic.Detail);
            originalPrompt = transport.PeekPromptForDebug();
            CharacterAcquiredTraitPendingRequestState frozenPending = pending.pendingRequests.Single();
            Require(frozenPending.publicContextVersion == 1 && frozenPending.RestorePublicContext() != null,
                "Live trait registration did not persist its request-time public context.");
            var jsonPending = JsonUtility.FromJson<CharacterAcquiredTraitPendingRequestState>(JsonUtility.ToJson(frozenPending));
            Require(jsonPending.RestorePublicContext().SemanticHash == frozenPending.publicContext.semanticHash
                && CharacterAcquiredTraitFormulaGeneration.HasValidModuleSelectionLocalFacts(jsonPending)
                && jsonPending.moduleSelectionLocalFactsHash == frozenPending.moduleSelectionLocalFactsHash,
                "Trait public context or frozen formula evidence did not survive the persisted JSON representation.");
            string stateBeforeTamper = JsonUtility.ToJson(source.Actor.Progression.CaptureAcquiredTraitState());
            for (int tamper = 0; tamper < 10; tamper++)
            {
                var invalid = pending.Clone();
                invalid.revision++;
                var invalidPending = invalid.pendingRequests.Single();
                if (tamper == 0) invalidPending.publicContext.publicFacts[0].text += " altered";
                else if (tamper == 1) invalidPending.publicContext = null;
                else if (tamper == 2) invalidPending.publicContextVersion = 2;
                else if (tamper == 3) invalidPending.publicContextVersion = 0;
                else if (tamper == 4) invalidPending.requestId += ":altered";
                else if (tamper == 5) invalidPending.moduleSelectionLocalFacts = null;
                else if (tamper == 6) invalidPending.moduleSelectionLocalFactsHash += ":altered";
                else if (tamper == 7) invalidPending.moduleSelectionLocalFacts[0].outcome = "blocked";
                else if (tamper == 8) invalidPending.moduleSelectionLocalFacts.RemoveAt(0);
                else invalid.formatVersion = 3;
                Require(!source.Actor.Progression.TryCommitAcquiredTraitState(invalid, pending.revision,
                    settings, modules, out _)
                    && JsonUtility.ToJson(source.Actor.Progression.CaptureAcquiredTraitState()) == stateBeforeTamper,
                    "Tampered frozen trait context/evidence/identity or obsolete schema changed authoritative pending state.");
            }
            CharacterNarrativeFact selectedLocal = frozenPending.moduleSelectionLocalFacts
                .First(value => value.domain == CharacterNarrativeDomain.Work);
            source.Actor.Progression.RecordNarrative(selectedLocal.domain,
                selectedLocal.factId, selectedLocal.subjectId, "blocked");
            NarrativeFormulaModuleSelectionRequest liveSelection =
                CharacterAcquiredTraitFormulaGeneration.PrepareModuleSelection(source.Actor.Progression,
                    frozenPending.requestId, frozenPending.requestKey, frozenPending.manifestationMilestone,
                    settings, modules, frozenPending.evidenceFactIds, frozenPending.evidenceBindings);
            NarrativeFormulaModuleSelectionRequest frozenSelection =
                CharacterAcquiredTraitFormulaGeneration.RebuildFrozenModuleSelection(
                    source.Actor.Progression, jsonPending, settings, modules);
            Require(liveSelection.SelectionId != frozenPending.moduleSelectionId
                && liveSelection.Offers.Count(value => value.Polarity == NarrativeFormulaModulePolarity.Drawback)
                    > frozenPending.offeredDrawbackModuleIds.Count
                && frozenSelection.SelectionId == frozenPending.moduleSelectionId
                && frozenSelection.Offers.Count(value => value.Polarity == NarrativeFormulaModulePolarity.Drawback)
                    == frozenPending.offeredDrawbackModuleIds.Count
                && selectedLocal.outcome == "completed",
                "The changed authoritative outcome was not distinguished from the immutable request-time evidence.");
            // This occurred after submission and must remain in the game ledger,
            // but must not be spliced into the already-issued inference request.
            source.Actor.Progression.RecordNarrative(CharacterNarrativeDomain.Mood,
                "mood:qa-after-trait-dispatch", "target:qa:later", "positive");
            pendingSnapshot = source.Actor.Progression.CapturePersistentState();
            // Recreate the saved fields from their real JSON representation, not only a DTO clone.
            pendingSnapshot = new CharacterProgressionSnapshot(pendingSnapshot.Level,
                pendingSnapshot.CurrentExperience, pendingSnapshot.GrowthState,
                JsonUtility.FromJson<CharacterNarrativeLedger>(JsonUtility.ToJson(pendingSnapshot.NarrativeLedger)),
                JsonUtility.FromJson<CharacterAcquiredTraitAggregateState>(JsonUtility.ToJson(pendingSnapshot.AcquiredTraitState)));
            originalRequestId = pending.pendingRequests[0].requestId;
        }

        using ActorFixture restored = new(
            812509,
            "Acquired Producer Restored",
            "human",
            publishComposition: true,
            persistentId: PersistentId);
        restored.Actor.Progression.RestorePersistentState(pendingSnapshot);
        DeferredAcquiredTraitLlmRuntime resumedTransport = new();
        using CharacterAcquiredTraitManifestationRuntime resumedRuntime = new(
            NarrativeRequestContextTestFixture.CreateEmpty(), new AcquiredTraitDefinitionSource(settings, modules),
            new FixedCharacterWorld(restored.Actor),
            new TestLlmRuntimeProvider(resumedTransport),
            new FixedGameCalendar(day: 17, hour: 5),
             new MutableUiClock(),
             new GameEventBus(),
             EmptyGameplayOutcomeNarrativeEvidenceQuery.Instance,
             outcomes.TraitInferenceFacts,
             outcomes.TraitInferences);
        resumedRuntime.Start();
        resumedRuntime.Tick();
        Require(
            resumedTransport.PendingCallbackCount == 1
            && resumedRuntime.PendingRequestCount == 1
            && restored.Actor.Progression.CaptureAcquiredTraitState().pendingRequests.Single().requestId == originalRequestId
            && resumedTransport.PeekPromptForDebug() == originalPrompt
            && restored.Actor.Progression.NarrativeLedger.Facts.Any(value => value.factId == "mood:qa-after-trait-dispatch"),
            "The automatic acquired-trait producer did not resume the exact persisted request.");
        Action<LocalLlmResult> duplicateCallback = resumedTransport.PeekCallbackForDebug();
        resumedTransport.CompleteNextSuccess();
        CharacterAcquiredTraitAggregateState completed = restored.Actor
            .Progression.CaptureAcquiredTraitState();
        bool hasDiagnostic = resumedRuntime.TryGetLatestDiagnostic(
            PersistentId,
            out CharacterAcquiredTraitManifestationDiagnostic diagnostic);
        Require(
            completed.ActiveCount == 1
            && completed.pendingRequests.Count == 0
            && completed.processedMilestones.SequenceEqual(new[] { 3 })
            && hasDiagnostic
            && diagnostic.Status
                == CharacterAcquiredTraitManifestationStatus.CompletionSucceeded,
            "The resumed automatic acquired-trait callback did not commit exactly one manifestation. "
            + $"status={diagnostic.Status}; issue={diagnostic.IssueCode}; detail={diagnostic.Detail}");
        string beforeDuplicate = JsonUtility.ToJson(completed);
        int knownResults = outcomes.Ledger.GetDiagnostics().KnownResultKeyCount;
        duplicateCallback(new LocalLlmResult(LocalLlmRequestStatus.Succeeded,
            "not-json", string.Empty, string.Empty));
        Require(knownResults == 1
            && outcomes.Ledger.GetDiagnostics().KnownResultKeyCount == knownResults
            && beforeDuplicate == JsonUtility.ToJson(restored.Actor.Progression.CaptureAcquiredTraitState()),
            "A repeated completed trait callback applied another state change or outcome.");
        return true;
    }

    private static bool VerifyAcquiredTraitAutomaticOwnerInvalidation()
    {
        LoadAuthoredAcquiredTraitContent(out var settings, out var modules);
        foreach (bool changeGeneration in new[] { false, true })
        {
            using ActorFixture source = new(812512, "Trait stale owner", "human", publishComposition: true,
                persistentId: "character:qa-trait-stale-" + (changeGeneration ? "generation" : "life"));
            foreach (int index in Enumerable.Range(0, 33))
                source.Actor.Progression.RecordNarrative(CharacterNarrativeDomain.Work,
                    $"work:qa-stale-{index:00}", "target:qa:stale", "completed");
            using var outcomes = new EvolutionGameplayOutcomeEditorFixture(
                "run:qa-trait-stale-" + changeGeneration);
            var transport = new DeferredAcquiredTraitLlmRuntime();
            using var runtime = new CharacterAcquiredTraitManifestationRuntime(
                NarrativeRequestContextTestFixture.CreateEmpty(), new AcquiredTraitDefinitionSource(settings, modules), new FixedCharacterWorld(source.Actor),
                new TestLlmRuntimeProvider(transport), new FixedGameCalendar(day: 17, hour: 4),
                new MutableUiClock(), new GameEventBus(),
                EmptyGameplayOutcomeNarrativeEvidenceQuery.Instance,
                outcomes.TraitInferenceFacts,
                outcomes.TraitInferences);
            runtime.Start();
            runtime.Tick();
            var original = source.Actor.Progression.CaptureAcquiredTraitState().pendingRequests.Single();
            Action<LocalLlmResult> late = transport.PeekCallbackForDebug();
            string narrativeBefore = JsonUtility.ToJson(source.Actor.Progression.NarrativeLedger);
            if (changeGeneration) CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(source.Actor.Progression).skillGenerationRevision++;
            else source.Actor.Lifecycle.SetLifecycleState(source.Actor.CurrentLifecycleState);
            runtime.Tick();
            var replaced = source.Actor.Progression.CaptureAcquiredTraitState();
            Require(replaced.pendingRequests.Count == 1 && replaced.pendingRequests[0].requestId != original.requestId
                && replaced.pendingRequests[0].ownerBinding.Matches(source.Actor.Progression, out _)
                && replaced.lastRequestInvalidation?.requestId == original.requestId
                && replaced.processedMilestones.Count == 0 && replaced.ActiveCount == 0
                && transport.PendingCallbackCount == 1
                && narrativeBefore == JsonUtility.ToJson(source.Actor.Progression.NarrativeLedger),
                "Live trait producer failed to cancel/reissue without consuming the milestone.");
            string beforeLate = JsonUtility.ToJson(replaced);
            late(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, "not-json", string.Empty, string.Empty));
            Require(beforeLate == JsonUtility.ToJson(source.Actor.Progression.CaptureAcquiredTraitState()),
                "Old trait transport callback changed its replacement request.");
            transport.CompleteNextSuccess();
            var completed = source.Actor.Progression.CaptureAcquiredTraitState();
            Require(completed.ActiveCount == 1 && completed.pendingRequests.Count == 0
                && completed.processedMilestones.SequenceEqual(new[] { 3 }),
                "Replacement trait request did not complete the original milestone exactly once.");
        }
        return true;
    }

    private static bool VerifyAcquiredTraitWholeRootSaveRoundTrip()
    {
        using ActorFixture actor = new(
            812510,
            "Acquired Whole Root Source",
            "human");
        using AcquiredTraitContentFixture content = new();
        string[] evidence =
        {
            "work:qa-root-a",
            "work:qa-root-b",
            "work:qa-root-c",
            "work:qa-root-d"
        };
        foreach (string factId in evidence)
        {
            for (int count = 0; count < 50; count++)
            {
                actor.Actor.Progression.RecordNarrative(
                    CharacterNarrativeDomain.Work,
                    factId,
                    $"target:qa:root:{factId}:{count}",
                    "completed");
            }
        }

        CharacterAcquiredTraitInferenceService inference = new(
            content.Settings,
            content.Modules);
        CompleteNextAcquiredMilestone(
            inference,
            actor.Actor.Progression,
            actor.Actor.Identity.PersistentId,
            3,
            evidence);
        CharacterAcquiredTraitInstanceState erasedCandidate = actor.Actor
            .Progression.CaptureAcquiredTraitState()
            .CaptureActiveInstances().Single();
        CompleteNextAcquiredMilestone(
            inference,
            actor.Actor.Progression,
            actor.Actor.Identity.PersistentId,
            8,
            evidence);

        const string EraseOperation = "operation:qa:root-erasure";
        using var erasureOutcomes = new EvolutionGameplayOutcomeEditorFixture(
            "run:qa-root-memory-erasure");
        CharacterCarryInventory carry = SeedCarriedMemoryErasureSeal(
            actor.Actor,
            EraseOperation);
        MemoryErasureSealUseResult erase = new MemoryErasureSealTransactionService(
            new AcquiredTraitDefinitionSource(content.Settings, content.Modules),
            new FixedGameCalendar(day: 18, hour: 6),
            new FakeReversibleCarriedDisposition(),
            erasureOutcomes.MemoryErasureFacts,
            erasureOutcomes.MemoryErasures).TryErase(
                actor.Actor,
                erasedCandidate.instanceId,
                carry,
                "carried:qa:memory-erasure",
                EraseOperation);
        Require(
            erase.Status == MemoryErasureSealUseStatus.Succeeded,
            "The whole-root fixture could not establish erased history.");

        CharacterAcquiredTraitAggregateState beforePending = actor.Actor
            .Progression.CaptureAcquiredTraitState();
        CharacterAcquiredTraitInferenceCommandResult pending =
            inference.SubmitMilestone(
                actor.Actor.Progression,
                BuildAcquiredSubmission(
                    actor.Actor.Progression,
                    actor.Actor.Identity.PersistentId,
                    20,
                    beforePending.revision,
                    evidence));
        Require(pending.Succeeded, "The whole-root fixture could not persist a pending request.");

        CharacterProgressionSnapshot captured = actor.Actor.Progression
            .CapturePersistentState();
        DungeonCharacterWorldSaveData root = new()
        {
            actors = new List<DungeonCharacterSaveData>
            {
                new()
                {
                    persistentId = actor.Actor.Identity.PersistentId,
                    dataId = actor.Actor.Identity.Data.id,
                    displayName = actor.Actor.Identity.DisplayName,
                    characterType = actor.Actor.Identity.CharacterType,
                    lifecycleState = actor.Actor.CurrentLifecycleState,
                    level = captured.Level,
                    currentExperience = captured.CurrentExperience,
                    growth = captured.GrowthState.Clone(),
                    narrative = captured.NarrativeLedger.Clone(),
                    acquiredTraits = captured.AcquiredTraitState.Clone(),
                    carryInventory = carry.Capture()
                }
            }
        };
        DungeonCharacterWorldSaveData decoded = JsonUtility.FromJson<
            DungeonCharacterWorldSaveData>(JsonUtility.ToJson(root));
        DungeonCharacterSaveData saved = decoded.actors.Single();

        using ActorFixture restored = new(
            812511,
            "Acquired Whole Root Restored",
            "human");
        restored.Actor.Progression.RestorePersistentState(
            new CharacterProgressionSnapshot(
                saved.level,
                saved.currentExperience,
                saved.growth,
                saved.narrative,
                saved.acquiredTraits));
        CharacterAcquiredTraitAggregateState after = restored.Actor.Progression
            .CaptureAcquiredTraitState();
        Require(
            after.ActiveCount == 1
            && after.instances.Count(value => value.erased) == 1
            && after.pendingRequests.Count == 1
            && after.pendingRequests[0].manifestationMilestone == 20
            && after.processedMilestones.SequenceEqual(new[] { 3, 8 })
            && string.Equals(
                JsonUtility.ToJson(captured.AcquiredTraitState),
                JsonUtility.ToJson(after),
                StringComparison.Ordinal)
            && CharacterAcquiredTraitEffectSourceProjection.Project(
                after,
                restored.Actor.Progression.NarrativeLedger,
                content.Settings,
                content.Modules).Count == 1,
            "Active, erased, pending, processed, evidence, or projected acquired-trait state changed across the whole-root JSON boundary.");
        return true;
    }

    private static bool VerifyAcquiredTraitSpecialReactionOutcome()
    {
        using ActorFixture actor = new(
            812512,
            "Acquired Reaction Outcome Fixture",
            "human");
        const string ModuleId = "acquired-trait:module:steady-hands";
        LoadAuthoredAcquiredTraitContent(
            out CharacterAcquiredTraitSettingsSO settings,
            out CharacterAcquiredTraitModuleSO[] modules);
        Require(modules.Any(value => value != null
                && string.Equals(
                    value.ModuleId,
                    ModuleId,
                    StringComparison.Ordinal)),
            "The authored steady-hands acquired-trait module is missing.");
        string[] evidence =
        {
            "work:qa-reaction-a",
            "work:qa-reaction-b",
            "work:qa-reaction-c"
        };
        foreach (string factId in evidence)
        {
            actor.Actor.Progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                factId,
                "target:" + factId,
                "completed",
                1f,
                0);
        }

        CharacterAcquiredTraitInferenceService inference = new(
            settings,
            modules);
        CharacterAcquiredTraitInferenceCommandResult submission =
            inference.SubmitMilestone(
                actor.Actor.Progression,
                BuildAcquiredSubmission(
                    actor.Actor.Progression,
                    actor.Actor.Identity.PersistentId,
                    3,
                    0,
                    evidence));
        Require(submission.Succeeded && submission.Packet != null,
            "The special-reaction fixture could not submit its acquired trait.");
        CharacterAcquiredTraitAggregateState submitted = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        Require(submitted.TryGetPendingRequest(
                submission.Packet.requestId,
                out CharacterAcquiredTraitPendingRequestState pending)
            && pending != null
            && pending.presentationState
                == CharacterAcquiredTraitPresentationState.ModuleSelectionPending
            && pending.offeredBenefitModuleIds.Contains(ModuleId),
            "The work-grounded request did not offer the authored steady-hands module.");

        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:trait-reaction-outcome-editor");
        string response = JsonUtility.ToJson(
            new CharacterAcquiredTraitModuleSelectionResponseDto
            {
                selectionId = pending.moduleSelectionId,
                positiveModuleIds = new List<string> { ModuleId },
                drawbackModuleIds = new List<string>(),
                evidenceFactIds = new List<string> { pending.evidenceFactIds[0] },
                displayName = "숙련의 손길",
                narrativeFlavor = "반복된 작업 경험이 마무리 순간의 통찰로 굳어졌다."
            });
        CharacterAcquiredTraitInferenceCommandResult completion =
            inference.CompleteMilestone(
                actor.Actor.Progression,
                new CharacterAcquiredTraitCompletionCommand(
                    actor.Actor.Identity.PersistentId,
                    submission.Packet.requestId,
                    submission.Packet.requestKey,
                    submission.Packet.candidatePacketHash,
                    1,
                    1,
                    response,
                    NarrativeInferenceTimestamp.FromGameTick(4L)),
                submission.Packet,
                outcomes.TraitInferenceFacts,
                outcomes.TraitInferences);
        Require(completion.Succeeded,
            "The special-reaction fixture could not manifest steady hands: "
            + completion.Audit.ValidationError);

        const string MoodModuleId = "acquired-trait:module:battle-temper";
        Require(modules.Any(value => value != null
                && string.Equals(
                    value.ModuleId,
                    MoodModuleId,
                    StringComparison.Ordinal)),
            "The authored battle-temper acquired-trait module is missing.");
        (CharacterNarrativeDomain Domain, string FactId)[] moodEvidence =
        {
            (CharacterNarrativeDomain.Combat, "combat:qa-reaction-mood"),
            (CharacterNarrativeDomain.Expedition, "expedition:qa-reaction-mood"),
            (CharacterNarrativeDomain.Invasion, "invasion:qa-reaction-mood")
        };
        foreach ((CharacterNarrativeDomain domain, string factId) in moodEvidence)
        {
            int repetitions = domain == CharacterNarrativeDomain.Invasion
                ? 1
                : 3;
            for (int index = 0; index < repetitions; index++)
            {
                actor.Actor.Progression.RecordNarrative(
                    domain,
                    factId,
                    "target:" + factId + ":" + index,
                    "completed",
                    1f,
                    1);
            }
        }
        Require(CharacterAcquiredTraitExperienceScore.Require(
                    actor.Actor.Progression.NarrativeLedger) == 8,
            "The authored mood-reaction fixture did not reach the exact second milestone score.");
        int moodSubmissionRevision = actor.Actor.Progression.AcquiredTraitRevision;
        CharacterAcquiredTraitInferenceCommandResult moodSubmission =
            inference.SubmitMilestone(
                actor.Actor.Progression,
                BuildAcquiredSubmission(
                    actor.Actor.Progression,
                    actor.Actor.Identity.PersistentId,
                    8,
                    moodSubmissionRevision,
                    moodEvidence.Select(value => value.FactId),
                    "mood-reaction"));
        Require(moodSubmission.Succeeded && moodSubmission.Packet != null,
            "The mood-reaction fixture could not submit battle temper: "
                + moodSubmission.Audit.ValidationError);
        CharacterAcquiredTraitAggregateState moodSubmitted = actor.Actor
            .Progression.CaptureAcquiredTraitState();
        Require(moodSubmitted.TryGetPendingRequest(
                moodSubmission.Packet.requestId,
                out CharacterAcquiredTraitPendingRequestState moodPending)
            && moodPending != null
            && moodPending.offeredBenefitModuleIds.Contains(MoodModuleId),
            "Expedition evidence did not offer the authored battle-temper module.");
        string moodResponse = JsonUtility.ToJson(
            new CharacterAcquiredTraitModuleSelectionResponseDto
            {
                selectionId = moodPending.moduleSelectionId,
                positiveModuleIds = new List<string> { MoodModuleId },
                drawbackModuleIds = new List<string>(),
                evidenceFactIds = new List<string>
                {
                    moodPending.evidenceFactIds[0]
                },
                displayName = "전장의 담력",
                narrativeFlavor = "원정 경험이 흔들리지 않는 담력으로 굳어졌다."
            });
        int moodCompletionRevision = moodSubmission.Audit.ResultingRevision;
        CharacterAcquiredTraitInferenceCommandResult moodCompletion =
            inference.CompleteMilestone(
                actor.Actor.Progression,
                new CharacterAcquiredTraitCompletionCommand(
                    actor.Actor.Identity.PersistentId,
                    moodSubmission.Packet.requestId,
                    moodSubmission.Packet.requestKey,
                    moodSubmission.Packet.candidatePacketHash,
                    moodCompletionRevision,
                    moodCompletionRevision,
                    moodResponse,
                    NarrativeInferenceTimestamp.FromGameTick(9L)),
                moodSubmission.Packet,
                outcomes.TraitInferenceFacts,
                outcomes.TraitInferences);
        Require(moodCompletion.Succeeded
                && actor.Actor.Progression.CaptureAcquiredTraitState()
                    .CaptureActiveInstances().Any(value =>
                        value.moduleIds.Contains(MoodModuleId)),
            "The mood-reaction fixture could not manifest battle temper: "
                + moodCompletion.Audit.ValidationError);

        DungeonStory.Foundation.GameEventBus events = new();
        int executedNotificationCount = 0;
        using IDisposable executedSubscription = events.Subscribe<
            CharacterAcquiredTraitReactionExecutedEvent>(
            _ => executedNotificationCount++);
        CharacterMoodPolicyService moodPolicy = new(
            new CharacterIdentityRuleRouter(),
            new CharacterPersistentNeedRuntime(
                new CharacterIdentityStateStore(),
                outcomes.Calendar));
        using DomainFactDispatcher reactionFacts = new();
        GameplayDomainFactObserverBinding<CharacterTraitReactionFact,
            AcquiredTraitReactionOutcomeReceipt> CreateReactionBinding() =>
            CharacterTraitReactionFactMapping.CreateBinding(
                outcomes.Registry,
                outcomes.Ledger,
                outcomes.Recorder,
                new GameplayDomainFactCommitGroup(
                    outcomes.Recorder,
                    outcomes.Ledger));
        IDisposable reactionBinding = CreateReactionBinding().Connect(
            reactionFacts);
        try
        {
            var publisher = new FaultingTraitReactionPublisher(reactionFacts);
            var publications = new CharacterTraitReactionPublicationRuntime(
                publisher,
                publisher);
            using CharacterAcquiredTraitSpecialReactionRuntime runtime = new(
                new AcquiredTraitDefinitionSource(settings, modules),
                new FixedCharacterWorld(actor.Actor),
                events,
                moodPolicy,
                publisher,
                publications);

            bool duplicateObserverRejected = false;
            try
            {
                using IDisposable duplicate = CreateReactionBinding().Connect(
                    reactionFacts);
            }
            catch (InvalidOperationException exception)
            {
                duplicateObserverRejected = exception.Message.Contains(
                    "Duplicate durable domain observer",
                    StringComparison.Ordinal);
            }
            Require(duplicateObserverRejected,
                "The dispatcher accepted a duplicate trait-reaction observer.");

            string CaptureOwner() => JsonUtility.ToJson(
                    actor.Actor.Progression.CaptureAcquiredTraitState())
                + "|level=" + actor.Actor.Progression.Level
                + "|experience=" + actor.Actor.Progression.CurrentExperience
                + "|mood=" + actor.Actor.Stats.GetConditionValue(
                    CharacterCondition.MOOD).ToString("R",
                    System.Globalization.CultureInfo.InvariantCulture);
            string CaptureMoodDetails()
            {
                CharacterMoodDeliveryTransactionSnapshot snapshot = actor.Actor
                    .Stats.CaptureMoodDeliveryTransactionState();
                return snapshot.BaseMood.ToString(
                        "R",
                        System.Globalization.CultureInfo.InvariantCulture)
                    + "|last=" + snapshot.LastCalculatedMood.ToString(
                        "R",
                        System.Globalization.CultureInfo.InvariantCulture)
                    + "|factors=" + string.Join(";", snapshot.Factors
                        .OrderBy(value => value.Id, StringComparer.Ordinal)
                        .Select(value => value.Id
                            + ":" + value.TotalValue.ToString(
                                "R",
                                System.Globalization.CultureInfo.InvariantCulture)
                            + ":" + value.ExpiresAt.ToString(
                                "R",
                                System.Globalization.CultureInfo.InvariantCulture)));
            }
            void PublishWork(int day, string suffix) => events.Publish(
                new WorkCompletedIdentityEvent(
                    actor.Actor.BuildingCharacterId,
                    "work:qa-reaction-finish:" + suffix,
                    "item:qa-reaction-product:" + suffix,
                    CharacterCommandOrigin.DirectPlayerOrder,
                    day));

            runtime.Start();
            runtime.Start();
            string beforeFailures = CaptureOwner();
            reactionBinding.Dispose();
            reactionBinding = null;
            PublishWork(24, "missing-observer");
            Require(beforeFailures == CaptureOwner()
                    && runtime.LastFailure.Contains(
                        "domain-fact-observer-missing",
                        StringComparison.Ordinal),
                "A missing trait-reaction observer changed owner effects or hid its rejection: "
                    + runtime.LastFailure);
            reactionBinding = CreateReactionBinding().Connect(reactionFacts);

            publisher.ThrowNextPublishBeforeCommit = true;
            PublishWork(24, "precommit-exception");
            Require(beforeFailures == CaptureOwner()
                    && runtime.LastFailure.Contains(
                        "editor-injected-trait-reaction-precommit-exception",
                        StringComparison.Ordinal),
                "A trait-reaction precommit exception did not roll back state, experience, and mood: "
                    + runtime.LastFailure);

            publisher.RejectNextPublish = true;
            PublishWork(24, "known-rejection");
            Require(beforeFailures == CaptureOwner()
                    && runtime.LastFailure.Contains(
                        "editor-injected-trait-reaction-publication-rejection",
                        StringComparison.Ordinal)
                    && publications.Capture().observations.Count == 0
                    && executedNotificationCount == 0,
                "A known trait-reaction publication rejection retained effects or a journal row: "
                    + runtime.LastFailure);

            runtime.Dispose();
            PublishWork(24, "disposed-runtime");
            Require(beforeFailures == CaptureOwner(),
                "Disposed trait-reaction runtime remained subscribed.");
            runtime.Start();
            runtime.Start();

            CharacterAcquiredTraitAggregateState before = actor.Actor.Progression
                .CaptureAcquiredTraitState();
            int levelBefore = actor.Actor.Progression.Level;
            int experienceBefore = actor.Actor.Progression.CurrentExperience;
            publisher.HideNextCommittedAcknowledgement = true;
            PublishWork(24, "lost-acknowledgement");
            CharacterAcquiredTraitAggregateState after = actor.Actor.Progression
                .CaptureAcquiredTraitState();
            CharacterTraitReactionPublicationData pendingReaction = publications
                .PendingForCharacter(actor.Actor.BuildingCharacterId.Value);
            Require(after.revision == before.revision + 1
                && after.CaptureActiveInstances().Single(value =>
                    value.moduleIds.Contains(ModuleId)).reactionStates.Any(value =>
                    value != null
                    && string.Equals(
                        value.reactionId,
                        "acquired-trait:reaction:finished-work-insight",
                        StringComparison.Ordinal)
                    && value.totalTriggerCount == 1)
                && pendingReaction != null
                && pendingReaction.pending
                && pendingReaction.fact.action
                    == CharacterAcquiredTraitReactionAction.GrantExperience
                && pendingReaction.fact.beforeRevision == before.revision
                && pendingReaction.fact.afterRevision == after.revision
                && pendingReaction.fact.beforeLevel == levelBefore
                && pendingReaction.fact.beforeExperience == experienceBefore
                && pendingReaction.fact.afterLevel
                    == actor.Actor.Progression.Level
                && pendingReaction.fact.afterExperience
                    == actor.Actor.Progression.CurrentExperience
                && pendingReaction.fact.appliedValue > 0f
                && executedNotificationCount == 1
                && runtime.LastFailure.Contains(
                    "editor-injected-trait-reaction-ack-loss-after-real-commit",
                    StringComparison.Ordinal),
                "Lost trait-reaction acknowledgement did not retain exactly one XP effect and frozen fact: "
                    + runtime.LastFailure);
            GameplayResultKey expectedKey =
                CharacterTraitReactionFactMapping.ResultKey(
                    pendingReaction.fact);
            Require(outcomes.Ledger.TryGetResultIdentity(
                    expectedKey,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && publisher.CommittedPublishCalls == 1,
                "The real trait-reaction fact was not committed exactly once.");

            string journalJson = new CharacterTraitReactionPublicationSaveSection(
                publications).Capture();
            CharacterTraitReactionPublicationSaveSection.ValidateJson(journalJson);
            CharacterTraitReactionPublicationSaveData savedPublications =
                DungeonSaveJson.Deserialize<
                    CharacterTraitReactionPublicationSaveData>(journalJson);
            Require(savedPublications.observations.Count == 1
                    && savedPublications.observations[0].pending,
                "Pending trait-reaction fact did not survive strict JSON save.");
            CharacterTraitReactionFactPreflight.ValidateObservation(
                savedPublications.observations[0],
                outcomes.Ledger.CaptureGameplayOutcomes());

            string ownerBeforeRecovery = CaptureOwner();
            var restoredPublications =
                new CharacterTraitReactionPublicationRuntime(
                    publisher,
                    publisher);
            restoredPublications.Restore(savedPublications);
            Require(restoredPublications.RecoverPending(
                    out string recoveryFailure),
                "Saved trait-reaction fact did not replay: " + recoveryFailure);
            CharacterTraitReactionPublicationData recovered =
                restoredPublications.Capture().observations.Single();
            Require(restoredPublications.PendingForCharacter(
                        actor.Actor.BuildingCharacterId.Value) == null
                    && !recovered.pending
                    && !string.IsNullOrWhiteSpace(recovered.publicationId)
                    && recovered.fingerprint.Length == 64
                    && ownerBeforeRecovery == CaptureOwner()
                    && publisher.ReplayBatchCalls == 1
                    && executedNotificationCount == 1,
                "Trait-reaction replay repeated XP, mood, or reaction state.");
            Require(restoredPublications.RecoverPending(
                    out string repeatedRecoveryFailure)
                    && repeatedRecoveryFailure == string.Empty
                    && publisher.ReplayBatchCalls == 1
                    && executedNotificationCount == 1
                    && ownerBeforeRecovery == CaptureOwner(),
                "Repeated trait-reaction recovery replayed its effect or fact.");

            runtime.Dispose();
            var moodPublications = new CharacterTraitReactionPublicationRuntime(
                publisher,
                publisher);
            using CharacterAcquiredTraitSpecialReactionRuntime moodRuntime = new(
                new AcquiredTraitDefinitionSource(settings, modules),
                new FixedCharacterWorld(actor.Actor),
                events,
                moodPolicy,
                publisher,
                moodPublications);
            moodRuntime.Start();
            string moodOwnerBefore = CaptureOwner();
            string moodDetailsBefore = CaptureMoodDetails();
            int notificationsBeforeMood = executedNotificationCount;
            void PublishSuccessfulExpedition(string suffix) => events.Publish(
                new ExpeditionOutcomeEvent(
                    "expedition:qa-trait-reaction:" + suffix,
                    new[] { actor.Actor.BuildingCharacterId },
                    "success",
                    30));

            publisher.RejectNextPublish = true;
            PublishSuccessfulExpedition("known-rejection");
            Require(moodOwnerBefore == CaptureOwner()
                    && moodDetailsBefore == CaptureMoodDetails()
                    && moodPublications.Capture().observations.Count == 0
                    && executedNotificationCount == notificationsBeforeMood
                    && moodRuntime.LastFailure.Contains(
                        "editor-injected-trait-reaction-publication-rejection",
                        StringComparison.Ordinal),
                "Rejected authored mood reaction changed its factor, deadline, counter, or notification: "
                    + moodRuntime.LastFailure);

            int moodCommittedBefore = publisher.CommittedPublishCalls;
            int moodReplayBefore = publisher.ReplayBatchCalls;
            publisher.HideNextCommittedAcknowledgement = true;
            PublishSuccessfulExpedition("lost-acknowledgement");
            string moodOwnerAfter = CaptureOwner();
            string moodDetailsAfter = CaptureMoodDetails();
            CharacterTraitReactionPublicationData pendingMood = moodPublications
                .PendingForCharacter(actor.Actor.BuildingCharacterId.Value);
            CharacterMoodDeliveryTransactionSnapshot appliedMood = actor.Actor
                .Stats.CaptureMoodDeliveryTransactionState();
            Require(moodOwnerAfter != moodOwnerBefore
                    && moodDetailsAfter != moodDetailsBefore
                    && pendingMood != null
                    && pendingMood.pending
                    && pendingMood.fact.action
                        == CharacterAcquiredTraitReactionAction.ApplyMoodImpulse
                    && pendingMood.fact.durationDays == 2
                    && pendingMood.fact.beforeLevel == pendingMood.fact.afterLevel
                    && pendingMood.fact.beforeExperience
                        == pendingMood.fact.afterExperience
                    && pendingMood.fact.moodBefore != pendingMood.fact.moodAfter
                    && appliedMood.Factors.Any(value =>
                        value.Id.Contains(
                            "acquired-trait:reaction:expedition-composure",
                            StringComparison.Ordinal)
                        && value.ExpiresAt > 0f)
                    && executedNotificationCount == notificationsBeforeMood + 1
                    && publisher.CommittedPublishCalls == moodCommittedBefore + 1,
                "Authored mood reaction did not retain one frozen factor and reaction state after ACK loss: "
                    + moodRuntime.LastFailure);

            string moodJournalJson =
                new CharacterTraitReactionPublicationSaveSection(
                    moodPublications).Capture();
            CharacterTraitReactionPublicationSaveSection.ValidateJson(
                moodJournalJson);
            CharacterTraitReactionPublicationSaveData savedMoodPublications =
                DungeonSaveJson.Deserialize<
                    CharacterTraitReactionPublicationSaveData>(
                        moodJournalJson);
            CharacterTraitReactionFactPreflight.ValidateObservation(
                savedMoodPublications.observations.Single(),
                outcomes.Ledger.CaptureGameplayOutcomes());
            var restoredMoodPublications =
                new CharacterTraitReactionPublicationRuntime(
                    publisher,
                    publisher);
            restoredMoodPublications.Restore(savedMoodPublications);
            Require(restoredMoodPublications.RecoverPending(
                    out string moodRecoveryFailure),
                "Saved authored mood reaction did not replay: "
                    + moodRecoveryFailure);
            CharacterTraitReactionPublicationData recoveredMood =
                restoredMoodPublications.Capture().observations.Single();
            Require(!recoveredMood.pending
                    && restoredMoodPublications.PendingForCharacter(
                        actor.Actor.BuildingCharacterId.Value) == null
                    && moodOwnerAfter == CaptureOwner()
                    && moodDetailsAfter == CaptureMoodDetails()
                    && executedNotificationCount == notificationsBeforeMood + 1
                    && publisher.ReplayBatchCalls == moodReplayBefore + 1,
                "Authored mood reaction replay changed its factor value, expiry, counter, or notification.");
            Require(restoredMoodPublications.RecoverPending(
                    out string repeatedMoodRecoveryFailure)
                    && repeatedMoodRecoveryFailure == string.Empty
                    && moodOwnerAfter == CaptureOwner()
                    && moodDetailsAfter == CaptureMoodDetails()
                    && publisher.ReplayBatchCalls == moodReplayBefore + 1,
                "Repeated authored mood recovery reapplied its factor or durable fact.");
            return true;
        }
        finally
        {
            reactionBinding?.Dispose();
        }
    }

    private static void LoadAuthoredAcquiredTraitContent(
        out CharacterAcquiredTraitSettingsSO settings,
        out CharacterAcquiredTraitModuleSO[] modules)
    {
        settings = AssetDatabase.LoadAssetAtPath<
            CharacterAcquiredTraitSettingsSO>(
            V25AcquiredTraitContentAssetBuilder.SettingsPath);
        modules = AssetDatabase.FindAssets(
                "t:CharacterAcquiredTraitModuleSO",
                new[] { V25AcquiredTraitContentAssetBuilder.ModuleRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<
                CharacterAcquiredTraitModuleSO>)
            .Where(value => value != null)
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        Require(settings != null, "The authored acquired-trait settings asset is missing.");
    }

    private static void VerifyMemoryErasureSealSuccessAndIdempotence()
    {
        using ActorFixture actor = new ActorFixture(
            812505,
            "Memory Erasure Success Fixture",
            "human");
        using AcquiredTraitContentFixture content =
            new AcquiredTraitContentFixture();
        CharacterAcquiredTraitInstanceState selected = ManifestFirstAcquiredTrait(
            actor.Actor,
            content);
        const string OperationId = "operation:qa:memory-erasure:success";
        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:memory-erasure-success-editor");
        RealMemoryErasurePhysicalFixture physical = new(
            actor.Actor.BuildingCharacterId.Value,
            outcomes.Clock);
        CharacterCarryInventory carry = SeedCarriedMemoryErasureSeal(
            actor.Actor,
            OperationId,
            physical.StackId,
            physical.StackId);
        CharacterAcquiredTraitAggregateState before = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        int ledgerFactCount = actor.Actor.Progression.NarrativeLedger.facts.Count;
        string auditId = "memory-erasure-seal-audit:"
            + NarrativeInferenceHash.ComputeSha256Utf8(
                OperationId + "|" + actor.Actor.BuildingCharacterId.Value
                + "|" + selected.instanceId);
        var cancelled = new MemoryErasureSealUseResult(
            MemoryErasureSealUseStatus.InterruptedBeforePickup,
            OperationId,
            auditId,
            actor.Actor.BuildingCharacterId.Value,
            selected.instanceId,
            string.Empty,
            before.revision,
            before.revision,
            GameCalendarRules.Project(14, 7).AbsoluteHour,
            "cancelled before the exact carried seal was applied");
        DomainFactDelivery cancelledDelivery = outcomes.MemoryErasures
            .RecordTerminal(
                cancelled,
                selected,
                actor.Actor.Identity.DisplayName,
                14);
        MemoryErasurePublicationData cancelledObservation = outcomes
            .MemoryErasures.Capture().observations.Single();
        GameplayResultKey cancelledKey = MemoryErasureTerminalFactMapping
            .ResultKey(cancelledObservation.fact);
        Require(cancelledDelivery.Completed
                && !cancelledObservation.pending
                && cancelledObservation.fact.terminalRevision == 1L,
            "Cancelled memory-erasure terminal did not commit its first history revision.");
        MemoryErasureSealTransactionService service = new(
            new AcquiredTraitDefinitionSource(content.Settings, content.Modules),
            new FixedGameCalendar(day: 14, hour: 7),
            physical.Disposition,
            outcomes.MemoryErasureFacts,
            outcomes.MemoryErasures);

        MemoryErasureSealUseResult result = service.TryErase(
            actor.Actor,
            selected.instanceId,
            carry,
            physical.StackId,
            OperationId);
        CharacterAcquiredTraitAggregateState after = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        MemoryErasurePublicationData[] observations = outcomes.MemoryErasures
            .Capture().observations.ToArray();
        MemoryErasurePublicationData successObservation = observations.Single(
            value => value.fact.status == MemoryErasureSealUseStatus.Succeeded);
        GameplayResultKey successKey = MemoryErasureTerminalFactMapping
            .ResultKey(successObservation.fact);
        Require(
            result.Status == MemoryErasureSealUseStatus.Succeeded
            && physical.Repository.GetEditorTestQuantity(physical.StackId) == 0
            && outcomes.Ledger.TryGetResultIdentity(
                cancelledKey,
                out GameplayOutcomeReplayIdentity cancelledIdentity)
            && cancelledIdentity.State
                == GameplayOutcomeReplayState.PublishedAcknowledged
            && outcomes.Ledger.TryGetResultIdentity(
                successKey,
                out GameplayOutcomeReplayIdentity outcomeIdentity)
            && outcomeIdentity.State
                == GameplayOutcomeReplayState.PublishedAcknowledged
            && observations.Length == 2
            && successObservation.fact.terminalRevision == 2L
            && !cancelledKey.Equals(successKey)
            && outcomes.Ledger.GetDiagnostics().KnownResultKeyCount == 2,
            "A valid memory-erasure seal transaction did not commit exactly once.");
        Require(
            carry.CountItem(MemoryErasureSealItemRules.ItemId) == 0,
            "A successful memory-erasure transaction retained its physical seal.");
        Require(
            after.revision == before.revision + 1
            && after.processedMilestones.SequenceEqual(before.processedMilestones)
            && after.TryGetInstance(selected.instanceId, out var erased)
            && erased.erased
            && erased.erasedAt == GameCalendarRules.Project(14, 7).AbsoluteHour
            && string.Equals(erased.erasureAuditId, result.AuditId, StringComparison.Ordinal),
            "A successful memory-erasure transaction changed the wrong acquired-trait state.");
        Require(
            actor.Actor.Progression.NarrativeLedger.facts.Count == ledgerFactCount,
            "Memory erasure changed the source narrative ledger.");
        Require(
            CharacterAcquiredTraitEffectSourceProjection.Project(
                after,
                actor.Actor.Progression.NarrativeLedger,
                content.Settings,
                content.Modules).Count == 0,
            "Memory erasure left the selected acquired-trait effect source active.");

        MemoryErasureSealUseResult duplicate = service.TryErase(
            actor.Actor,
            selected.instanceId,
            carry,
            physical.StackId,
            OperationId);
        Require(
            duplicate.Status == MemoryErasureSealUseStatus.AlreadyCompleted
            && physical.Repository.GetEditorTestQuantity(physical.StackId) == 0
            && actor.Actor.Progression.CaptureAcquiredTraitState().revision
                == after.revision
            && outcomes.MemoryErasures.Capture().observations.Count == 2
            && outcomes.Ledger.GetDiagnostics().KnownResultKeyCount == 2,
            "Retrying the same committed erasure consumed or mutated state twice.");
    }

    private static void VerifyMemoryErasureSealPhysicalFailureLeavesEverythingUnchanged()
    {
        using ActorFixture actor = new ActorFixture(
            812506,
            "Memory Erasure Physical Failure Fixture",
            "human");
        using AcquiredTraitContentFixture content =
            new AcquiredTraitContentFixture();
        CharacterAcquiredTraitInstanceState selected = ManifestFirstAcquiredTrait(
            actor.Actor,
            content);
        const string OperationId = "operation:qa:memory-erasure:physical-failure";
        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:memory-erasure-physical-failure-editor");
        CharacterCarryInventory carry = SeedCarriedMemoryErasureSeal(
            actor.Actor,
            OperationId);
        CharacterAcquiredTraitAggregateState before = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        CharacterCarryInventorySaveData carryBefore = carry.Capture();
        FakeReversibleCarriedDisposition disposition = new()
        {
            AllowCommit = false
        };
        MemoryErasureSealTransactionService service = new(
            new AcquiredTraitDefinitionSource(content.Settings, content.Modules),
            new FixedGameCalendar(day: 15, hour: 3),
            disposition,
            outcomes.MemoryErasureFacts,
            outcomes.MemoryErasures);

        MemoryErasureSealUseResult result = service.TryErase(
            actor.Actor,
            selected.instanceId,
            carry,
            "carried:qa:memory-erasure",
            OperationId);
        CharacterAcquiredTraitAggregateState after = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        Require(
            result.Status == MemoryErasureSealUseStatus.PhysicalCommitFailed
            && disposition.BeginCount == 1
            && disposition.AcknowledgeCount == 0
            && disposition.RollbackCount == 0,
            "A rejected physical seal debit reported the wrong terminal result.");
        Require(
            after.revision == before.revision
            && after.TryGetInstance(selected.instanceId, out var retained)
            && !retained.erased
            && CarrySnapshotsEqual(carryBefore, carry.Capture()),
            "A rejected physical seal debit changed the trait or carried item.");
    }

    private static void VerifyMemoryErasureSealObserverFailureDoesNotUndoCommittedErasure()
    {
        using ActorFixture actor = new ActorFixture(
            812507,
            "Memory Erasure Observer Failure Fixture",
            "human");
        using AcquiredTraitContentFixture content =
            new AcquiredTraitContentFixture();
        CharacterAcquiredTraitInstanceState selected = ManifestFirstAcquiredTrait(
            actor.Actor,
            content);
        const string OperationId = "operation:qa:memory-erasure:observer-failure";
        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:memory-erasure-observer-failure-editor");
        CharacterCarryInventory carry = SeedCarriedMemoryErasureSeal(
            actor.Actor,
            OperationId);
        CharacterAcquiredTraitAggregateState before = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        int ledgerFactCount = actor.Actor.Progression.NarrativeLedger.facts.Count;
        FakeReversibleCarriedDisposition disposition = new();
        MemoryErasureSealTransactionService service = new(
            new AcquiredTraitDefinitionSource(content.Settings, content.Modules),
            new FixedGameCalendar(day: 16, hour: 11),
            disposition,
            outcomes.MemoryErasureFacts,
            outcomes.MemoryErasures);
        int observerFailureDiagnostics = 0;
        void CaptureObserverFailure(
            string condition,
            string stackTrace,
            LogType type)
        {
            if (condition?.Contains(
                    "qa-erasure-observer-failure",
                    StringComparison.Ordinal) == true)
            {
                observerFailureDiagnostics++;
            }
        }
        Application.logMessageReceived += CaptureObserverFailure;
        actor.Actor.Progression.Changed += ThrowErasureObserverFailure;
        try
        {
            MemoryErasureSealUseResult result = service.TryErase(
                actor.Actor,
                selected.instanceId,
                carry,
                "carried:qa:memory-erasure",
                OperationId);
            CharacterAcquiredTraitAggregateState after = actor.Actor.Progression
                .CaptureAcquiredTraitState();
            MemoryErasurePublicationData observation = outcomes.MemoryErasures
                .Capture().observations.Single();
            GameplayResultKey key = MemoryErasureTerminalFactMapping.ResultKey(
                observation.fact);
            Require(
                result.Status == MemoryErasureSealUseStatus.Succeeded
                && disposition.BeginCount == 1
                && disposition.AcknowledgeCount == 1
                && disposition.RollbackCount == 0,
                "An isolated Changed observer failure altered the committed transaction result.");
            Require(
                after.revision == before.revision + 1
                && after.TryGetInstance(selected.instanceId, out var erased)
                && erased.erased
                && string.Equals(
                    erased.erasureAuditId,
                    result.AuditId,
                    StringComparison.Ordinal)
                && carry.CountItem(MemoryErasureSealItemRules.ItemId) == 0
                && actor.Actor.Progression.NarrativeLedger.facts.Count == ledgerFactCount
                && observerFailureDiagnostics == 1
                && !observation.pending
                && outcomes.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged,
                "Changed observer failure did not preserve one committed erasure and canonical observation.");
        }
        finally
        {
            actor.Actor.Progression.Changed -= ThrowErasureObserverFailure;
            Application.logMessageReceived -= CaptureObserverFailure;
        }
    }

    private static void VerifyMemoryErasureSealFactStageFailureRollsBackEverything()
    {
        using ActorFixture actor = new ActorFixture(
            812508,
            "Memory Erasure Stage Failure Fixture",
            "human");
        using AcquiredTraitContentFixture content =
            new AcquiredTraitContentFixture();
        CharacterAcquiredTraitInstanceState selected = ManifestFirstAcquiredTrait(
            actor.Actor,
            content);
        const string OperationId = "operation:qa:memory-erasure:stage-failure";
        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:memory-erasure-stage-failure-editor");
        var facts = new FaultingMemoryErasureFacts(
            outcomes.MemoryErasureFacts,
            outcomes.MemoryErasureFacts)
        {
            RejectNextStage = true
        };
        var publications = new MemoryErasurePublicationRuntime(
            facts,
            outcomes.MemoryErasureFacts);
        RealMemoryErasurePhysicalFixture physical = new(
            actor.Actor.BuildingCharacterId.Value,
            outcomes.Clock);
        CharacterCarryInventory carry = SeedCarriedMemoryErasureSeal(
            actor.Actor,
            OperationId,
            physical.StackId,
            physical.StackId);
        CharacterAcquiredTraitAggregateState before = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        CharacterCarryInventorySaveData carryBefore = carry.Capture();
        MemoryErasureSealTransactionService service = new(
            new AcquiredTraitDefinitionSource(content.Settings, content.Modules),
            new FixedGameCalendar(day: 17, hour: 2),
            physical.Disposition,
            facts,
            publications);

        MemoryErasureSealUseResult result = service.TryErase(
            actor.Actor,
            selected.instanceId,
            carry,
            physical.StackId,
            OperationId);
        CharacterAcquiredTraitAggregateState after = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        Require(result.Status == MemoryErasureSealUseStatus.TraitCommitFailed
                && result.Detail.Contains(
                    "memory-erasure observation stage failed",
                    StringComparison.Ordinal)
                && physical.Repository.GetEditorTestQuantity(physical.StackId)
                    == MemoryErasureSealItemRules.UseQuantity
                && CarrySnapshotsEqual(carryBefore, carry.Capture())
                && after.revision == before.revision
                && after.TryGetInstance(selected.instanceId, out var retained)
                && !retained.erased
                && publications.Capture().observations.Count == 0
                && outcomes.Ledger.GetDiagnostics().KnownResultKeyCount == 0,
            "A rejected memory-erasure fact stage did not roll back item and trait authority.");
    }

    private static void VerifyMemoryErasureSealLostAcknowledgementRecovery()
    {
        using ActorFixture actor = new ActorFixture(
            812509,
            "Memory Erasure Recovery Fixture",
            "human");
        using AcquiredTraitContentFixture content =
            new AcquiredTraitContentFixture();
        CharacterAcquiredTraitInstanceState selected = ManifestFirstAcquiredTrait(
            actor.Actor,
            content);
        const string OperationId = "operation:qa:memory-erasure:ack-loss";
        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:memory-erasure-ack-loss-editor");
        var facts = new FaultingMemoryErasureFacts(
            outcomes.MemoryErasureFacts,
            outcomes.MemoryErasureFacts)
        {
            HideNextCommitAcknowledgement = true
        };
        var publications = new MemoryErasurePublicationRuntime(
            facts,
            outcomes.MemoryErasureFacts);
        RealMemoryErasurePhysicalFixture physical = new(
            actor.Actor.BuildingCharacterId.Value,
            outcomes.Clock);
        CharacterCarryInventory carry = SeedCarriedMemoryErasureSeal(
            actor.Actor,
            OperationId,
            physical.StackId,
            physical.StackId);
        CharacterAcquiredTraitAggregateState before = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        MemoryErasureSealTransactionService service = new(
            new AcquiredTraitDefinitionSource(content.Settings, content.Modules),
            new FixedGameCalendar(day: 18, hour: 5),
            physical.Disposition,
            facts,
            publications);

        MemoryErasureSealUseResult result = service.TryErase(
            actor.Actor,
            selected.instanceId,
            carry,
            physical.StackId,
            OperationId);
        CharacterAcquiredTraitAggregateState applied = actor.Actor.Progression
            .CaptureAcquiredTraitState();
        MemoryErasurePublicationData pending = publications.PendingForOperation(
            OperationId);
        Require(result.Status == MemoryErasureSealUseStatus.Succeeded
                && result.Detail.Contains("observation pending", StringComparison.Ordinal)
                && applied.revision == before.revision + 1
                && applied.TryGetInstance(selected.instanceId, out var erased)
                && erased.erased
                && carry.CountItem(MemoryErasureSealItemRules.ItemId) == 0
                && physical.Repository.GetEditorTestQuantity(physical.StackId) == 0
                && pending != null
                && pending.pending
                && !string.IsNullOrWhiteSpace(pending.publicationKey)
                && string.IsNullOrEmpty(pending.publicationId)
                && string.IsNullOrEmpty(pending.fingerprint)
                && outcomes.Ledger.GetDiagnostics().KnownResultKeyCount == 1,
            "A committed memory erasure with a lost ACK did not retain one frozen observation.");

        string publicationJson = new MemoryErasurePublicationSaveSection(
                publications)
            .Capture();
        MemoryErasurePublicationSaveSection.ValidateJson(publicationJson);
        MemoryErasurePublicationSaveData saved = DungeonSaveJson.Deserialize<
            MemoryErasurePublicationSaveData>(publicationJson);
        MemoryErasureTerminalFactPreflight.ValidateObservation(
            saved.observations.Single(),
            outcomes.Ledger.CaptureGameplayOutcomes());
        var restored = new MemoryErasurePublicationRuntime(
            facts,
            outcomes.MemoryErasureFacts);
        restored.Restore(saved);
        int knownBeforeReplay = outcomes.Ledger.GetDiagnostics()
            .KnownResultKeyCount;
        int revisionBeforeReplay = actor.Actor.Progression
            .CaptureAcquiredTraitState().revision;

        Require(restored.RecoverPending(out string recoveryFailure),
            "Saved memory-erasure observation did not replay: " + recoveryFailure);
        MemoryErasurePublicationData completed = restored.Capture()
            .observations.Single();
        CharacterAcquiredTraitAggregateState afterReplay = actor.Actor
            .Progression.CaptureAcquiredTraitState();
        Require(!completed.pending
                && !string.IsNullOrWhiteSpace(completed.publicationId)
                && !string.IsNullOrWhiteSpace(completed.fingerprint)
                && outcomes.Ledger.GetDiagnostics().KnownResultKeyCount
                    == knownBeforeReplay
                && afterReplay.revision == revisionBeforeReplay
                && afterReplay.TryGetInstance(selected.instanceId, out var replayed)
                && replayed.erased
                && carry.CountItem(MemoryErasureSealItemRules.ItemId) == 0
                && physical.Repository.GetEditorTestQuantity(physical.StackId) == 0,
            "Memory-erasure observation replay repeated effects or changed its canonical result.");
        Require(restored.RecoverPending(out recoveryFailure)
                && outcomes.Ledger.GetDiagnostics().KnownResultKeyCount
                    == knownBeforeReplay
                && actor.Actor.Progression.CaptureAcquiredTraitState().revision
                    == revisionBeforeReplay,
            "Repeated memory-erasure recovery was not an exact no-op: "
            + recoveryFailure);
    }

    private static void ThrowErasureObserverFailure() =>
        throw new InvalidOperationException("qa-erasure-observer-failure");

    private static CharacterAcquiredTraitInstanceState ManifestFirstAcquiredTrait(
        CharacterActor actor,
        AcquiredTraitContentFixture content)
    {
        string[] evidence =
        {
            "work:qa-memory-seal-a",
            "work:qa-memory-seal-b",
            "work:qa-memory-seal-c"
        };
        foreach (string factId in evidence)
        {
            actor.Progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                factId,
                "target:" + factId,
                "completed",
                1f,
                0);
        }
        Require(
            CharacterAcquiredTraitExperienceScore.Require(
                actor.Progression.NarrativeLedger) == 3,
            "Memory-erasure fixture did not reach the first manifestation gate.");
        CharacterAcquiredTraitInferenceService inference = new(
            content.Settings,
            content.Modules);
        CharacterAcquiredTraitInferenceCommandResult submission =
            inference.SubmitMilestone(
                actor.Progression,
                BuildAcquiredSubmission(
                    actor.Progression,
                    actor.Identity.PersistentId,
                    3,
                    0,
                    evidence));
        Require(submission.Succeeded,
            "Memory-erasure fixture could not submit its acquired trait.");
        CharacterAcquiredTraitInferenceCommandResult completion =
            CompleteAcquiredMilestone(
                inference,
                actor.Progression,
                actor.Identity.PersistentId,
                submission,
                1);
        Require(completion.Succeeded,
            "Memory-erasure fixture could not manifest its acquired trait.");
        return actor.Progression.CaptureAcquiredTraitState()
            .CaptureActiveInstances()
            .Single();
    }

    private static CharacterCarryInventory SeedCarriedMemoryErasureSeal(
        CharacterActor actor,
        string operationId,
        string carriedStackId = "carried:qa:memory-erasure",
        string sourceStackId = "stack:qa:memory-erasure")
    {
        CharacterCarryInventory carry = actor.gameObject
            .GetComponent<CharacterCarryInventory>()
            ?? actor.gameObject.AddComponent<CharacterCarryInventory>();
        carry.Restore(new CharacterCarryInventorySaveData
        {
            items = new List<CharacterCarriedItemSaveData>
            {
                new()
                {
                    carriedStackId = carriedStackId,
                    sourceStackId = sourceStackId,
                    ownerOperationId = operationId,
                    itemId = MemoryErasureSealItemRules.ItemId,
                    quantity = MemoryErasureSealItemRules.UseQuantity,
                    components = new List<ItemInstanceComponentSaveData>()
                }
            }
        });
        return carry;
    }

    private static bool CarrySnapshotsEqual(
        CharacterCarryInventorySaveData left,
        CharacterCarryInventorySaveData right)
    {
        CharacterCarriedItemSaveData[] leftItems = (left?.items
                ?? new List<CharacterCarriedItemSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToArray();
        CharacterCarriedItemSaveData[] rightItems = (right?.items
                ?? new List<CharacterCarriedItemSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToArray();
        return leftItems.Length == rightItems.Length
            && leftItems.Zip(rightItems, (a, b) =>
                    string.Equals(a.carriedStackId, b.carriedStackId, StringComparison.Ordinal)
                    && string.Equals(a.sourceStackId, b.sourceStackId, StringComparison.Ordinal)
                    && string.Equals(a.ownerOperationId, b.ownerOperationId, StringComparison.Ordinal)
                    && string.Equals(a.itemId, b.itemId, StringComparison.Ordinal)
                    && a.quantity == b.quantity)
                .All(value => value);
    }

    private static void CleanupLeakedActorFixtures()
    {
        foreach (CharacterActor actor in UnityEngine.Object
            .FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(value => value != null
                && value.name.StartsWith(
                    "ProgressionActor_",
                    StringComparison.Ordinal)))
        {
            UnityEngine.Object.DestroyImmediate(actor.gameObject);
        }
    }

    private static bool VerifyPotentialAndRarityRules()
    {
        CharacterSkillSystemSettingsSO settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        try
        {
            const int Samples = 100000;
            int[] potentialCounts = new int[5];
            System.Random potentialRandom = new System.Random(104729);
            for (int i = 0; i < Samples; i++)
            {
                potentialCounts[(int)CharacterGrowthRules.RollPotential(settings, potentialRandom)]++;
            }

            float[] expected = { 0.45f, 0.30f, 0.15f, 0.08f, 0.02f };
            for (int i = 0; i < expected.Length; i++)
            {
                float actual = potentialCounts[i] / (float)Samples;
                Require(Mathf.Abs(actual - expected[i]) < 0.012f,
                    $"Potential grade {i} distribution was {actual:P2}.");
            }

            HashSet<CharacterSkillRarity> ordinaryRarities = new HashSet<CharacterSkillRarity>();
            int baselineUpper = 0;
            int pityUpper = 0;
            System.Random baselineRandom = new System.Random(13007);
            System.Random pityRandom = new System.Random(13007);
            for (int i = 0; i < Samples; i++)
            {
                CharacterSkillRarity baseline = CharacterGrowthRules.RollRarity(
                    settings,
                    CharacterPotentialGrade.Ordinary,
                    applyPity: false,
                    baselineRandom);
                CharacterSkillRarity pity = CharacterGrowthRules.RollRarity(
                    settings,
                    CharacterPotentialGrade.Ordinary,
                    applyPity: true,
                    pityRandom);
                ordinaryRarities.Add(baseline);
                if (baseline >= CharacterSkillRarity.Rare) baselineUpper++;
                if (pity >= CharacterSkillRarity.Rare) pityUpper++;
            }

            Require(ordinaryRarities.Count == Enum.GetValues(typeof(CharacterSkillRarity)).Length,
                "Ordinary potential could not roll every rarity.");
            Require(pityUpper > baselineUpper,
                "The missed-upper-rarity correction did not increase Rare-or-higher outcomes.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyIndependentTransientRoots()
    {
        const string CharacterKey = "character:transient-root-proof";
        CharacterId characterId = (CharacterId)CharacterKey;
        GameObject actorObject = new GameObject("TransientRootProofActor");
        CharacterCarryInventory inventory = null;
        ICharacterCarryInventoryRegistry firstCarry = null;
        ICharacterSkillTransientStateRegistry firstSkills = null;
        try
        {
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(CharacterKey);
            inventory = actorObject.AddComponent<CharacterCarryInventory>();

            using (IObjectResolver firstRoot = BuildTransientStateRoot())
            {
                firstCarry = firstRoot.Resolve<ICharacterCarryInventoryRegistry>();
                firstSkills = firstRoot.Resolve<ICharacterSkillTransientStateRegistry>();
                Require(
                    ReferenceEquals(firstCarry, firstSkills),
                    "Carry and skill transient state did not share one scoped owner.");
                firstCarry.Register(inventory);
                Require(
                    ReferenceEquals(firstCarry.Find(characterId), inventory),
                    "First root did not index its active carry inventory.");
                Require(
                    firstSkills.TryEnter(characterId, "event:base"),
                    "First root rejected a fresh execution key.");
                firstSkills.Exit(characterId, "event:base");
                Require(
                    !firstSkills.TryEnter(characterId, "event:base"),
                    "First root executed the same event key twice.");
                firstSkills.BeginWork(characterId, BuiltInWorkTypeIds.Operate, 1.4f);
                Require(
                    Mathf.Approximately(
                        firstSkills.GetWorkSpeedMultiplier(characterId),
                        1.4f),
                    "First root did not retain its work-speed snapshot.");

                for (int index = 0; index < 512; index++)
                {
                    string key = $"event:fifo:{index}";
                    Require(
                        firstSkills.TryEnter(characterId, key),
                        $"FIFO key {index} was unexpectedly rejected.");
                    firstSkills.Exit(characterId, key);
                }

                Require(
                    !firstSkills.TryEnter(characterId, "event:fifo:510"),
                    "The run-scoped owner forgot a recent executed-event key.");
                Require(
                    !firstSkills.TryEnter(characterId, "event:base"),
                    "The run-scoped owner evicted an earlier exactly-once key.");
            }

            Require(
                firstCarry.Find(characterId) == null,
                "Disposed first root retained an active carry inventory.");
            bool firstSkillOwnerDisposed = false;
            try
            {
                firstSkills.GetWorkSpeedMultiplier(characterId);
            }
            catch (ObjectDisposedException)
            {
                firstSkillOwnerDisposed = true;
            }
            Require(
                firstSkillOwnerDisposed,
                "Disposed first root still accepted skill-state queries.");

            using IObjectResolver secondRoot = BuildTransientStateRoot();
            ICharacterCarryInventoryRegistry secondCarry =
                secondRoot.Resolve<ICharacterCarryInventoryRegistry>();
            ICharacterSkillTransientStateRegistry secondSkills =
                secondRoot.Resolve<ICharacterSkillTransientStateRegistry>();
            Require(
                ReferenceEquals(secondCarry, secondSkills),
                "Second root did not resolve one composite transient owner.");
            Require(
                secondCarry.Find(characterId) == null,
                "Second root inherited the first root's carry inventory.");
            Require(
                Mathf.Approximately(
                    secondSkills.GetWorkSpeedMultiplier(characterId),
                    1f),
                "Second root inherited the first root's work-speed snapshot.");
            Require(
                secondSkills.TryEnter(characterId, "event:fifo:510"),
                "Second root inherited the first root's executed-event keys.");
            secondSkills.Exit(characterId, "event:fifo:510");
            return true;
        }
        finally
        {
            if (actorObject != null)
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
            }
        }
    }

    private static IObjectResolver BuildTransientStateRoot()
    {
        ContainerBuilder builder = new ContainerBuilder();
        builder.Register<CharacterCarryInventoryRegistry>(Lifetime.Singleton)
            .As<ICharacterCarryInventoryRegistry>()
            .As<ICharacterSkillTransientStateRegistry>()
            .As<ICharacterRuntimeTransientStateRegistry>();
        return builder.Build();
    }

    private static bool VerifyStatsAndLevelGrowth()
    {
        using ActorFixture fixture = new ActorFixture(81001, "Growth", "Orc");
        CharacterProgression progression = fixture.Actor.Progression;
        IReadOnlyList<CharacterStartingProficiencyExperience> starts =
            progression.GrowthState.startingProficiencies;
        CharacterStartingProficiencyRules.Validate(starts);
        string initialProjection = string.Join(
            "|",
            starts.OrderBy(value => value.proficiencyId, StringComparer.Ordinal)
                .Select(value => $"{value.proficiencyId}:{value.experience}"));

        progression.AddExperience(GetExperienceToReach(50));
        Require(progression.Level == 50, $"Expected level 50, got {progression.Level}.");
        Require(progression.CurrentExperience == 0 && progression.ExperienceToNextLevel == 0,
            "Level 50 retained experience or a next-level requirement.");
        Require(string.Equals(
                initialProjection,
                string.Join(
                    "|",
                    progression.GrowthState.startingProficiencies
                        .OrderBy(value => value.proficiencyId, StringComparer.Ordinal)
                        .Select(value => $"{value.proficiencyId}:{value.experience}")),
                StringComparison.Ordinal),
            "Generic character levels changed proficiency-derived performance.");
        return true;
    }

    private static bool VerifyExperienceCurve()
    {
        Require(CharacterProgression.GetExperienceRequired(1) == 20,
            "Level 1 XP requirement changed.");
        Require(CharacterProgression.GetExperienceRequired(10) == 20,
            "Level 10 XP requirement changed.");
        Require(CharacterProgression.GetExperienceRequired(11) == 25,
            "Level 11 XP requirement changed.");
        Require(CharacterProgression.GetExperienceRequired(21) == 30,
            "Level 21 XP requirement changed.");
        Require(CharacterProgression.GetExperienceRequired(41) == 40,
            "Level 41 XP requirement changed.");
        Require(GetExperienceToReach(50) == 1460,
            $"Level 1->50 cumulative XP was {GetExperienceToReach(50)}, not 1460.");
        return true;
    }

    private static bool VerifySkillMilestones()
    {
        using ActorFixture fixture = new ActorFixture(81002, "Milestone", "Slime");
        CharacterProgression progression = fixture.Actor.Progression;
        Require(progression.PassiveSkills.Count == 1,
            "The identity passive was not granted at level 1. "
            + DescribeDraftState(progression));
        Require(ChooseActive(progression, 1, 0), "The level-1 active could not be chosen.");

        progression.AddExperience(GetExperienceToReach(5));
        Require(ChooseActive(progression, 5, 1), "The level-5 active could not be chosen.");

        progression.AddExperience(GetExperienceBetween(5, 25));
        CharacterNarrativeDomain[] domains =
        {
            CharacterNarrativeDomain.Work,
            CharacterNarrativeDomain.Relationship,
            CharacterNarrativeDomain.Combat
        };
        for (int i = 0; i < 8; i++)
        {
            progression.RecordNarrative(domains[i % domains.Length], $"fact-{i}", "qa", "completed", 1f, i + 1);
        }

        Require(progression.NarrativeLedger.MeaningfulRecordCount >= 8,
            "Eight structured narrative records were not retained.");
        Require(progression.NarrativeLedger.MeaningfulDomainCount >= 3,
            "Narrative breadth did not reach three domains.");
        Require(progression.PassiveSkills.Count == 2,
            "The level-25 narrative passive was not granted.");

        progression.AddExperience(GetExperienceBetween(25, 30));
        Require(ChooseActive(progression, 30, 2), "The level-30 active could not be chosen.");
        Require(progression.ActiveSkills.Count == CharacterProgression.NormalActiveSlots,
            "The three fixed normal-active slots were not filled.");

        progression.AddExperience(GetExperienceBetween(30, 50));
        Require(progression.Ultimate != null
            && progression.Ultimate.kind == CharacterSkillKind.Ultimate
            && progression.Ultimate.ultimateDomain != CharacterUltimateDomain.None,
            "The level-50 narrative ultimate was not committed.");
        return true;
    }

    private static bool VerifyUltimateUseLimits()
    {
        using ActorFixture fixture = new ActorFixture(81008, "Ultimate", "Orc");
        // This ultimate-use fixture has no authored date contract.
        CharacterProgression progression = fixture.Actor.Progression;
        progression.AddExperience(GetExperienceToReach(50));
        CharacterSkillInstance ultimate = progression.Ultimate;
        Require(ultimate != null,
            "The level-50 ultimate was not available for use-limit checks. "
            + DescribeDraftState(progression));

        ultimate.ultimateDomain = CharacterUltimateDomain.Offense;
        CharacterCombatAbilityDefinition combatAbility = CharacterSkillRuntimeEffects.ToCombatAbility(
            ultimate,
            progression.SkillSettings);
        Require(combatAbility != null && combatAbility.CooldownTurns >= 999,
            "The offense ultimate was not restricted to one use per battle.");
        Require(progression.TryMarkUltimateUsed(CharacterUltimateDomain.Offense, 101),
            "The first offense ultimate use was rejected.");
        Require(!progression.TryMarkUltimateUsed(CharacterUltimateDomain.Offense, 101)
            && progression.CanUseUltimate(CharacterUltimateDomain.Offense, 102),
            "The offense ultimate did not reset on a new battle serial.");

        ultimate.ultimateDomain = CharacterUltimateDomain.Defense;
        Require(progression.TryMarkUltimateUsed(CharacterUltimateDomain.Defense, 202)
            && !progression.TryMarkUltimateUsed(CharacterUltimateDomain.Defense, 202)
            && progression.CanUseUltimate(CharacterUltimateDomain.Defense, 203),
            "The defense ultimate was not limited per invasion.");

        ultimate.ultimateDomain = CharacterUltimateDomain.Management;
        Require(progression.TryMarkUltimateUsed(CharacterUltimateDomain.Management, 303)
            && !progression.TryMarkUltimateUsed(CharacterUltimateDomain.Management, 303)
            && progression.CanUseUltimate(CharacterUltimateDomain.Management, 304),
            "The management ultimate was not limited per operating day.");

        CharacterProgressionSnapshot snapshot = progression.CapturePersistentState();
        using ActorFixture restored = new ActorFixture(81009, "UltimateRestored", "Orc");
        restored.Actor.Progression.RestorePersistentState(snapshot);
        CharacterSkillUseLimitState restoredLimits = restored.Actor.Progression.GrowthState.useLimits;
        Require(!restoredLimits.CanUse(CharacterUltimateDomain.Offense, 101)
            && !restoredLimits.CanUse(CharacterUltimateDomain.Defense, 202)
            && !restoredLimits.CanUse(CharacterUltimateDomain.Management, 303),
            "Ultimate use limits changed during save/restore.");
        return true;
    }

    private static bool VerifySkillRuntimeEffects()
    {
        using ActorFixture workerFixture = new ActorFixture(81010, "RuntimeWorker", "Slime");
        CharacterActor worker = workerFixture.Actor;
        CharacterGrowthState growth = CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(worker.Progression);
        growth.passiveSkills.Clear();
        growth.passiveSkills.Add(new CharacterSkillInstance
        {
            id = "qa-management-passive",
            displayName = "살림 감각",
            description = "실제 경영 수치 검증",
            kind = CharacterSkillKind.Passive,
            trigger = CharacterSkillTrigger.WorkCompleted,
            target = CharacterSkillTarget.Facility,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("work_speed", "small"),
                Module("output", "small"),
                Module("stock", "small"),
                Module("cleaning", "small"),
                Module("repair", "small"),
                Module("research", "small"),
                Module("revenue", "small")
            }
        });
        growth.passiveSkills.Add(new CharacterSkillInstance
        {
            id = "qa-relationship-passive",
            displayName = "부드러운 말씨",
            description = "긍정적 관계 반응 검증",
            kind = CharacterSkillKind.Passive,
            trigger = CharacterSkillTrigger.RelationshipChanged,
            target = CharacterSkillTarget.Ally,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("relationship", "small")
            }
        });

        Require(
            worker.Progression.OwnerFixedSkills.Count == 0,
            "A regular employee received owner-only fixed skills.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(worker), 1f),
            "Work-speed module leaked as an always-on bonus before work started.");
        CharacterSkillRuntimeEffects.BeginWork(
            worker,
            null,
            BuiltInWorkTypeIds.Operate,
            "qa-management-work-started");
        float workSpeedModuleTotal = CharacterSkillRuntimeEffects.GetManagementModuleTotal(
            worker,
            "work_speed",
            CharacterSkillTrigger.WorkCompleted);
        float workSpeedSnapshot =
            CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(worker);
        Require(
            Mathf.Approximately(workSpeedSnapshot, 1.1f),
            $"Work-speed module did not snapshot at work start. "
            + $"snapshot={workSpeedSnapshot:0.###}; "
            + $"moduleTotal={workSpeedModuleTotal:0.###}; "
            + $"passives={growth.passiveSkills.Count}");
        CharacterSkillRuntimeEffects.EndWork(worker);
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(worker), 1f),
            "Work-speed snapshot was not cleared after work ended.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(worker), 1.1f),
            "Output module did not affect the real production multiplier.");
        Require(CharacterSkillRuntimeEffects.GetStockProductionBonus(worker) == 1,
            "Stock module did not add a real produced item.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetCleaningSpeedMultiplier(worker), 1.05f),
            "Cleaning module did not affect cleaning duration.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetRepairSpeedMultiplier(worker), 1.08f),
            "Repair module did not affect repair duration.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetResearchWorkBonus(worker, 1f), 5f),
            "Research module did not add real research work.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetRevenueMultiplier(worker), 1.05f),
            "Revenue module did not affect real shop revenue.");
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.ApplyPositiveRelationshipBonus(worker, 0.5f), 0.52f),
            "Relationship module did not strengthen a positive social result.");

        growth.passiveSkills.Clear();
        growth.ultimate = new CharacterSkillInstance
        {
            id = "qa-management-ultimate",
            displayName = "오늘의 운영법",
            description = "하루 생산량 증가",
            kind = CharacterSkillKind.Ultimate,
            trigger = CharacterSkillTrigger.OperatingDayStarted,
            target = CharacterSkillTarget.Dungeon,
            ultimateDomain = CharacterUltimateDomain.Management,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("output", "large")
            }
        };
        Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(worker), 1f),
            "Management ultimate became active before the operating-day trigger.");
        Require(worker.Progression.TryMarkUltimateUsed(CharacterUltimateDomain.Management, 5)
            && Mathf.Approximately(CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(worker), 1.3f)
            && !worker.Progression.TryMarkUltimateUsed(CharacterUltimateDomain.Management, 5)
            && worker.Progression.TryMarkUltimateUsed(CharacterUltimateDomain.Management, 6),
            "Management ultimate activation or once-per-day reset was incorrect.");

        using ActorFixture intruderFixture = new ActorFixture(81011, "RuntimeIntruder", "Orc");
        CharacterActor intruder = intruderFixture.Actor;
        float healthBefore = intruder.Stats.CurrentHealth;
        CharacterSkillInstance defenseUltimate = new CharacterSkillInstance
        {
            id = "qa-defense-ultimate",
            displayName = "수호자의 일격",
            description = "침입자에게 실제 피해",
            kind = CharacterSkillKind.Ultimate,
            trigger = CharacterSkillTrigger.InvasionStarted,
            target = CharacterSkillTarget.Enemy,
            ultimateDomain = CharacterUltimateDomain.Defense,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("damage", "light")
            }
        };
        CharacterSkillRuntimeEffects.ApplyDefenseUltimate(worker, defenseUltimate, intruder);
        Require(intruder.Stats.CurrentHealth < healthBefore,
            "Defense ultimate did not change the spawned intruder's health.");

        growth.passiveSkills.Clear();
        CharacterSkillRuntimeEffects.ResetTransientExecutionStateForDebug();
        growth.passiveSkills.Add(new CharacterSkillInstance
        {
            id = "qa-battle-start-passive",
            displayName = "Battle opener",
            description = "Battle-start passive damage probe.",
            kind = CharacterSkillKind.Passive,
            trigger = CharacterSkillTrigger.BattleStarted,
            target = CharacterSkillTarget.Enemy,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("damage", "light")
            }
        });
        OffenseBattleCombatant ally = new OffenseBattleCombatant(
            worker.Identity.PersistentId,
            worker.Identity.DisplayName,
            worker.Identity.SpeciesTag,
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(100f, 8f, 4f, 1f, 4f, 4f),
            100f);
        OffenseBattleCombatant enemy = new OffenseBattleCombatant(
            "qa-battle-passive-enemy",
            "Passive Target",
            "Training",
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(100f, 4f, 2f, 1f, 2f, 2f),
            100f);
        CombatEquipmentRuntime battleEquipment = CombatEquipmentEditorTestFactory.Create(
            new ResourceCombatEquipmentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader())),
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
            materialCatalog: EmptyResourceEconomyContentCatalog.Instance,
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            moduleCatalog: EmptyEquipmentModuleCatalog.Instance,
            itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance,
            calendar: CharacterAiEditorTestDependencies.GameCalendar);
        CombatResolutionService battleResolution = new CombatResolutionService(
            new UnityCombatRandomSource(new DungeonStory.Foundation.RandomStreamProvider(40405)),
            evolution: null,
            overclock: null,
            environmentStatus: null,
            environmentalField: NoEnvironmentalFieldQuery.Instance,
            characters: null,
            environmentExposure:
                NoOpCharacterEnvironmentExposureCommand.Instance);
        OffenseBattleSession passiveSession = new OffenseBattleSession(
            "qa-battle-passive-session",
            "qa-expedition",
            "qa-target",
            "QA Passive Battle",
            DungeonDifficulty.Normal,
            new[] { ally, enemy },
            battleResolution,
            battleEquipment);
        CharacterCombatAbilityDefinition convertedPassive =
            CharacterSkillRuntimeEffects.ToCombatAbility(
                growth.passiveSkills[0],
                worker.Progression.SkillSettings);
        float enemyHealthBefore = enemy.CurrentHealth;
        CharacterSkillRuntimeEffects.ApplyTriggeredPassives(new CharacterSkillExecutionContext(
            worker,
            CharacterSkillTrigger.BattleStarted,
            "qa-battle-passive-started",
            passiveSession,
            ally,
            enemy));
        if (!(enemy.CurrentHealth < enemyHealthBefore))
        {
            Debug.LogError(
                "BATTLE_PASSIVE_DIAGNOSTIC "
                + $"passives={growth.passiveSkills.Count}; converted={convertedPassive != null}; "
                + $"effects={convertedPassive?.Effects.Count ?? -1}; basic={passiveSession.CalculateBasicDamage(ally, enemy):0.##}; "
                + $"enemy={enemyHealthBefore:0.##}->{enemy.CurrentHealth:0.##}; "
                + $"actorId={worker.Identity.PersistentId}; source={ally.PersistentId}/{ally.Formation}; "
                + $"target={enemy.PersistentId}/{enemy.Formation}; logs={string.Join(" / ", passiveSession.Log)}");
        }
        Require(enemy.CurrentHealth < enemyHealthBefore,
            "Battle-start passive module did not affect the real battle session. "
            + $"passives={growth.passiveSkills.Count}; converted={convertedPassive != null}; "
            + $"effects={convertedPassive?.Effects.Count ?? -1}; basic={passiveSession.CalculateBasicDamage(ally, enemy):0.##}; "
            + $"enemy={enemyHealthBefore:0.##}->{enemy.CurrentHealth:0.##}; "
            + $"actorId={worker.Identity.PersistentId}; source={ally.PersistentId}/{ally.Formation}; "
            + $"target={enemy.PersistentId}/{enemy.Formation}; logs={string.Join(" / ", passiveSession.Log)}");
        RequireCombatFormation(
            CharacterSkillTarget.Enemy,
            Module("damage", "light"),
            OffenseFormationMask.Front | OffenseFormationMask.Middle,
            OffenseFormationMask.Front | OffenseFormationMask.Middle,
            "Direct attack generated the wrong formation constraints.");
        RequireCombatFormation(
            CharacterSkillTarget.Enemy,
            Module("delay", "short"),
            OffenseFormationMask.Middle | OffenseFormationMask.Rear,
            OffenseFormationMask.Any,
            "Ranged control generated the wrong formation constraints.");
        RequireCombatFormation(
            CharacterSkillTarget.Ally,
            Module("heal", "minor"),
            OffenseFormationMask.Middle | OffenseFormationMask.Rear,
            OffenseFormationMask.Any,
            "Support generated the wrong formation constraints.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 50.01f,
            threshold: 0.5f,
            extraDamageMultiplier: 0.35f,
            expectedToApply: false,
            "Wounded amplification triggered above the selected target's threshold.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 50f,
            threshold: 0.5f,
            extraDamageMultiplier: 0.35f,
            expectedToApply: true,
            "Wounded amplification did not include the exact selected-target threshold.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 49.99f,
            threshold: 0.5f,
            extraDamageMultiplier: 0.35f,
            expectedToApply: true,
            "Wounded amplification did not trigger below the selected target's threshold.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 24f,
            targetHealth: 50.01f,
            threshold: 0.5f,
            extraDamageMultiplier: 0.35f,
            expectedToApply: false,
            "Wounded amplification inspected the caster instead of the selected target.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 25.01f,
            threshold: 0.25f,
            extraDamageMultiplier: 0.7f,
            expectedToApply: false,
            "Critical amplification triggered above the selected target's threshold.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 25f,
            threshold: 0.25f,
            extraDamageMultiplier: 0.7f,
            expectedToApply: true,
            "Critical amplification did not include the exact selected-target threshold.");
        RequireConditionalAmplifyBoundary(
            worker,
            battleEquipment,
            battleResolution,
            sourceHealth: 100f,
            targetHealth: 24.99f,
            threshold: 0.25f,
            extraDamageMultiplier: 0.7f,
            expectedToApply: true,
            "Critical amplification did not trigger below the selected target's threshold.");

        CharacterSkillInstance defenseConditional = new CharacterSkillInstance
        {
            id = "qa-defense-conditional",
            displayName = "조건부 방어 기여",
            description = "방어 궁극기 경로의 현재 무조건 피해 기여 검증",
            kind = CharacterSkillKind.Ultimate,
            trigger = CharacterSkillTrigger.InvasionStarted,
            target = CharacterSkillTarget.Enemy,
            ultimateDomain = CharacterUltimateDomain.Defense,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("conditional_amplify", "wounded")
            }
        };
        using ActorFixture healthyDefenseTarget =
            new ActorFixture(81012, "HealthyDefenseTarget", "Orc");
        using ActorFixture woundedDefenseTarget =
            new ActorFixture(81013, "WoundedDefenseTarget", "Orc");
        woundedDefenseTarget.Actor.ApplyDamage(
            woundedDefenseTarget.Actor.MaxHealth * 0.6f,
            "qa-defense-threshold-setup");
        float healthyDefenseBefore = healthyDefenseTarget.Actor.CurrentHealth;
        float woundedDefenseBefore = woundedDefenseTarget.Actor.CurrentHealth;
        CharacterSkillRuntimeEffects.ApplyDefenseUltimate(
            worker,
            defenseConditional,
            healthyDefenseTarget.Actor);
        CharacterSkillRuntimeEffects.ApplyDefenseUltimate(
            worker,
            defenseConditional,
            woundedDefenseTarget.Actor);
        float healthyDefenseDamage = healthyDefenseBefore
            - healthyDefenseTarget.Actor.CurrentHealth;
        float woundedDefenseDamage = woundedDefenseBefore
            - woundedDefenseTarget.Actor.CurrentHealth;
        Require(healthyDefenseDamage > 0f
                && Mathf.Approximately(healthyDefenseDamage, woundedDefenseDamage),
            "Defense-ultimate conditional amplification no longer matches its current "
            + "unconditional fixed-contribution runtime semantics.");
        return true;
    }

    private static void RequireConditionalAmplifyBoundary(
        CharacterActor actor,
        CombatEquipmentRuntime equipment,
        CombatResolutionService resolution,
        float sourceHealth,
        float targetHealth,
        float threshold,
        float extraDamageMultiplier,
        bool expectedToApply,
        string message)
    {
        OffenseBattleCombatant source = new OffenseBattleCombatant(
            actor.Identity.PersistentId,
            "Conditional Source",
            "Training",
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(100f, 8f, 4f, 1f, 4f, 4f),
            sourceHealth);
        OffenseBattleCombatant target = new OffenseBattleCombatant(
            $"qa-conditional-target-{sourceHealth:R}-{targetHealth:R}-{threshold:R}",
            "Conditional Target",
            "Training",
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(100f, 4f, 2f, 1f, 2f, 2f),
            targetHealth);
        OffenseBattleSession session = new OffenseBattleSession(
            $"qa-conditional-session-{sourceHealth:R}-{targetHealth:R}-{threshold:R}",
            "qa-expedition",
            "qa-target",
            "QA Conditional Amplification",
            DungeonDifficulty.Normal,
            new[] { source, target },
            resolution,
            equipment);
        float before = target.CurrentHealth;
        float expectedDamage = session.CalculateBasicDamage(source, target)
            * extraDamageMultiplier;
        string variantId = threshold <= 0.25f ? "critical" : "wounded";
        CharacterSkillInstance skill = new CharacterSkillInstance
        {
            id = $"qa-conditional-{variantId}-{sourceHealth:R}-{targetHealth:R}",
            displayName = "Conditional Amplification QA",
            description = "Production conditional amplification boundary verification.",
            narrativeReason = "QA",
            kind = CharacterSkillKind.Active,
            rarity = CharacterSkillRarity.Legendary,
            trigger = CharacterSkillTrigger.ManualCombat,
            target = CharacterSkillTarget.Enemy,
            cooldownTurns = 1,
            usableFrom = OffenseFormationMask.Any,
            targetPositions = OffenseFormationMask.Any,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("conditional_amplify", variantId)
            }
        };
        CharacterSkillRuntimeEffects.ExecuteSkill(
            new CharacterSkillExecutionContext(
                actor,
                CharacterSkillTrigger.ManualCombat,
                $"qa-conditional-event-{variantId}-{sourceHealth:R}-{targetHealth:R}",
                session,
                source,
                target),
            skill);
        float actualDamage = before - target.CurrentHealth;
        Require(expectedToApply
                ? Mathf.Approximately(actualDamage, expectedDamage)
                : Mathf.Approximately(actualDamage, 0f),
            message + $" source={sourceHealth:R}; target={targetHealth:R}; "
            + $"threshold={threshold:R}; multiplier={extraDamageMultiplier:R}; "
            + $"damage={actualDamage:R}; expected={expectedDamage:R}");
    }

    private static void RequireCombatFormation(
        CharacterSkillTarget target,
        CharacterSkillModuleSelection module,
        OffenseFormationMask expectedUsableFrom,
        OffenseFormationMask expectedTargets,
        string message)
    {
        CharacterSkillFormationRules.Resolve(
            target,
            new[] { module },
            out OffenseFormationMask usableFrom,
            out OffenseFormationMask targetPositions);
        CharacterSkillInstance skill = new CharacterSkillInstance
        {
            id = $"qa-formation-{module.moduleId}",
            displayName = "Formation QA",
            description = "Formation verification.",
            narrativeReason = "Formation verification.",
            kind = CharacterSkillKind.Active,
            rarity = CharacterSkillRarity.Common,
            trigger = CharacterSkillTrigger.ManualCombat,
            target = target,
            usableFrom = usableFrom,
            targetPositions = targetPositions,
            modules = new List<CharacterSkillModuleSelection> { module }
        };
        CharacterCombatAbilityDefinition ability = CharacterSkillRuntimeEffects.ToCombatAbility(
            skill,
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults());
        Require(ability != null
                && ability.UsableFrom == expectedUsableFrom
                && ability.TargetPositions == expectedTargets,
            message);
    }

    private static CharacterSkillModuleSelection Module(string moduleId, string variantId)
    {
        return new CharacterSkillModuleSelection
        {
            moduleId = moduleId,
            variantId = variantId
        };
    }

    private static bool VerifyPermanentChoiceAndPersistence()
    {
        using ActorFixture source = new ActorFixture(81003, "Source", "Vampire");
        CharacterProgression progression = source.Actor.Progression;
        Require(ChooseActive(progression, 1, 2),
            "Initial active selection failed. " + DescribeDraftState(progression));
        string selectedId = progression.ActiveSkills[0].id;
        Require(!progression.TryChooseActiveSkill(1, 0, confirmed: true, out _),
            "A permanently selected active could be replaced.");

        CharacterProgressionSnapshot snapshot = progression.CapturePersistentState();
        DungeonCharacterSaveData serialized = new DungeonCharacterSaveData
        {
            level = snapshot.Level,
            currentExperience = snapshot.CurrentExperience,
            growth = snapshot.GrowthState.Clone(),
            narrative = snapshot.NarrativeLedger.Clone()
        };
        string json = JsonUtility.ToJson(serialized);
        DungeonCharacterSaveData restoredData = JsonUtility.FromJson<DungeonCharacterSaveData>(json);

        using ActorFixture restored = new ActorFixture(81004, "Restored", "Vampire");
        restored.Actor.Progression.RestorePersistentState(new CharacterProgressionSnapshot(
            restoredData.level,
            restoredData.currentExperience,
            restoredData.growth,
            restoredData.narrative));
        Require(restored.Actor.Progression.ActiveSkills.Count == 1
            && restored.Actor.Progression.ActiveSkills[0].id == selectedId,
            "The permanent active changed during JSON save/restore.");
        CharacterSkillDraft restoredDraft = restored.Actor.Progression.Drafts
            .First(draft => draft.kind == CharacterSkillKind.Active && draft.unlockLevel == 1);
        CharacterSkillDraft sourceDraft = progression.Drafts
            .First(draft => draft.kind == CharacterSkillKind.Active && draft.unlockLevel == 1);
        Require(restoredDraft.rules.Select(rule => rule.rarity)
                .SequenceEqual(sourceDraft.rules.Select(rule => rule.rarity)),
            "Reloading rerolled a prepared candidate rarity.");
        Require(restoredDraft.rules.Select(rule => rule.ruleId)
                .SequenceEqual(sourceDraft.rules.Select(rule => rule.ruleId)),
            "Reloading changed stable skill rule identities.");
        Require(restoredDraft.candidates.Select(skill => skill.id)
                .SequenceEqual(sourceDraft.candidates.Select(skill => skill.id)),
            "Reloading regenerated prepared candidates.");
        Require(restoredDraft.candidates.Select(skill => (skill.ruleId, skill.combinationId))
                .SequenceEqual(sourceDraft.candidates.Select(skill => (skill.ruleId, skill.combinationId))),
            "Reloading changed the selected rule/combination mapping.");
        Require(restored.Actor.Progression.GrowthState.traitIds.Count == 0
            && restored.Actor.Progression.PassiveSkills.Count == 1,
            "Traits and learned passives were not persisted as separate concepts.");
        return true;
    }

    private static bool VerifyFormulaPresentationContract()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.formulaPolicy.formulaVersion =
            CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion;
        CharacterSkillFormulaCatalogAssetBuilder.Populate(settings);
        using ActorFixture actor = new ActorFixture(7717, "검증자", "human");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new MissingLlmRuntimeProvider(),
                CharacterAiEditorTestDependencies.UiClock);
            CharacterProgression progression = actor.Actor.Progression;
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:formula-presentation",
                "facility:formula-presentation",
                CharacterActivityOutcomes.Completed,
                day: 1);
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:formula-presentation-failed",
                "facility:formula-presentation",
                CharacterActivityOutcomes.Failed,
                day: 2);

            CharacterSkillDraft active = service.CreateDraft(
                progression,
                CharacterSkillKind.Active,
                1);
            Require(active.formulaVersion >= CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion
                    && active.presentationState
                        == CharacterSkillPresentationState.PresentationPending
                    && active.moduleSelectionOffers.Count == 3
                    && active.frozenMechanics.Count == 0
                    && active.candidates.Count == 0
                    && active.nextPresentationIndex == 0,
                "A new active draft did not expose three unresolved module-selection offers.");
            Require(active.moduleSelectionOffers.All(value => value != null
                    && value.positiveModuleIds.Count > 0
                    && value.evidenceFactIds.Count > 0
                    && value.drawbackModuleIds.Count >= 3
                    && value.qualifiedNegativeEvidenceFactIds.Count > 0
                    && value.maximumDrawbackModules == 1),
                "A negative work history did not expose both cooldown and runtime-stat drawbacks per active offer.");

            int originalPresentationIndex = active.nextPresentationIndex;
            int selfCancellingOfferIndex = active.moduleSelectionOffers.FindIndex(value =>
                value.positiveModuleIds.Contains("research", StringComparer.Ordinal)
                && value.drawbackModuleIds.Contains(
                    "character-skill:drawback:research-speed",
                    StringComparer.Ordinal));
            Require(selfCancellingOfferIndex >= 0,
                "The fixture did not expose the research same-axis conflict witness.");
            active.nextPresentationIndex = selfCancellingOfferIndex;
            NarrativeFormulaModuleSelectionRequest conflictRequest =
                CharacterSkillFormulaGeneration.BuildModuleSelectionRequest(active, settings);
            NarrativeFormulaModuleOffer researchSlowdown = conflictRequest.Offers.Single(value =>
                string.Equals(value.ModuleId,
                    "character-skill:drawback:research-speed",
                    StringComparison.Ordinal));
            Require(researchSlowdown.SemanticDescription.Contains(
                    "함께 선택 금지 이로운 기능=research",
                    StringComparison.Ordinal),
                "The prompt does not explain the research same-axis exclusion.");
            Require(!NarrativeFormulaModuleSelectionValidator.TryValidate(
                    conflictRequest,
                    new NarrativeFormulaModuleSelectionChoice(
                        conflictRequest.SelectionId,
                        new[] { "research" },
                        new[] { "character-skill:drawback:research-speed" },
                        new[] { conflictRequest.EvidenceFactIds[0] }),
                    out _,
                    out string sameAxisError)
                && sameAxisError.Contains("conflict", StringComparison.OrdinalIgnoreCase),
                "A CharacterSkill benefit and drawback on the same research axis were accepted.");
            active.nextPresentationIndex = originalPresentationIndex;

            string validJson = BuildValidModuleSelectionJson(active, "검증자");
            Require(service.TryValidateResponse(
                    active,
                    validJson,
                    out List<CharacterSkillInstance> skills,
                    out string validError)
                    && skills.Count == 1
                    && skills[0].formulaCapabilities.Count == 1
                    && skills[0].calculatedCost <= active.formulaBudget
                    && string.Equals(skills[0].modules[0].moduleId,
                        active.moduleSelectionOffers[0].positiveModuleIds[0], StringComparison.Ordinal),
                $"A valid post-selection numeric allocation was rejected: {validError}");

            CharacterSkillModuleSelectionResponseDto drawbackDto =
                BuildValidModuleSelectionDto(active, "검증자");
            CharacterSkillModuleOfferState drawbackOffer =
                active.moduleSelectionOffers[active.nextPresentationIndex];
            drawbackDto.drawbackModuleIds.Add(
                drawbackOffer.drawbackModuleIds.Single(value => string.Equals(
                    value,
                    "character-skill:drawback:work-cooldown",
                    StringComparison.Ordinal)));
            drawbackDto.evidenceFactIds = new List<string>
            {
                drawbackOffer.qualifiedNegativeEvidenceFactIds[0]
            };
            Require(service.TryValidateResponse(
                    active,
                    JsonUtility.ToJson(drawbackDto),
                    out List<CharacterSkillInstance> drawbackSkills,
                    out string drawbackError)
                    && drawbackSkills.Count == 1
                    && drawbackSkills[0].drawbackCredit is >= 1 and <= 2
                    && drawbackSkills[0].drawbackEvidenceQualified
                    && drawbackSkills[0].manualCooldownDays is >= 1 and <= 2
                    && drawbackSkills[0].drawbackId.StartsWith(
                        "character-skill:work-cooldown:+",
                        StringComparison.Ordinal),
                $"An authored work cooldown drawback was not allocated by C#: {drawbackError}");

            CharacterSkillModuleSelectionResponseDto unsupportedDrawback =
                BuildValidModuleSelectionDto(active, "검증자");
            unsupportedDrawback.drawbackModuleIds.Add(
                drawbackOffer.drawbackModuleIds[0]);
            unsupportedDrawback.evidenceFactIds = new List<string>
            {
                drawbackOffer.evidenceFactIds.First(value =>
                    !drawbackOffer.qualifiedNegativeEvidenceFactIds.Contains(value,
                        StringComparer.Ordinal))
            };
            Require(!service.TryValidateResponse(
                    active,
                    JsonUtility.ToJson(unsupportedDrawback),
                    out _,
                    out string unsupportedError)
                    && unsupportedError.Contains(
                        "qualified negative evidence", StringComparison.Ordinal),
                "A selected drawback without cited negative evidence was accepted.");

            CharacterSkillModuleSelectionResponseDto statDrawback =
                BuildValidModuleSelectionDto(active, "검증자");
            CharacterSkillModuleOfferState statOffer =
                active.moduleSelectionOffers[active.nextPresentationIndex];
            string statDrawbackId = statOffer.drawbackModuleIds.First(value =>
                !string.Equals(value,
                    "character-skill:drawback:work-cooldown",
                    StringComparison.Ordinal));
            statDrawback.drawbackModuleIds.Add(statDrawbackId);
            statDrawback.evidenceFactIds = new List<string>
            {
                statOffer.qualifiedNegativeEvidenceFactIds[0]
            };
            Require(service.TryValidateResponse(
                    active,
                    JsonUtility.ToJson(statDrawback),
                    out List<CharacterSkillInstance> statSkills,
                    out string statError)
                    && statSkills.Count == 1
                    && statSkills[0].drawbackEffect != null
                    && statSkills[0].drawbackEffect.severityUnits is >= 1 and <= 2
                    && statSkills[0].mechanicalDescription.Contains(
                        "장착 부담", StringComparison.Ordinal)
                    && AcquiredTraitBurdenTargetCatalog.IsHarmful(
                        settings.FindDrawback(statDrawbackId).effectDefinition,
                        statSkills[0].drawbackEffect.value,
                        out _),
                $"An authored runtime-stat drawback was not frozen by C#: {statError}");
            CharacterSkillDrawbackCapabilityDefinition statDefinition =
                settings.FindDrawback(statDrawbackId);
            GameplayEffectBinding statBinding = new GameplayEffectBinding
            {
                bindingId = "qa:skill-burden",
                definition = statDefinition.effectDefinition,
                value = statSkills[0].drawbackEffect.value
            };
            GameplayEffectProjectionResult statProjection =
                CharacterGameplayEffectProjector.Resolve(
                    statSkills[0].drawbackEffect.targetId,
                    1f,
                    new IGameplayEffectSource[]
                    {
                        new DebugGameplayEffectSource(
                            new GameplayEffectSourceRef(
                                GameplayEffectSourceKind.Status,
                                "qa:skill-burden"),
                            statBinding)
                    },
                    new GameplayEffectContext());
            Require(Math.Abs(statProjection.Value
                    - statSkills[0].drawbackEffect.value) < 0.000001f,
                "The frozen CharacterSkill burden value did not reach the shared gameplay-effect projector.");
            CharacterSkillInstance restoredStatSkill =
                JsonUtility.FromJson<CharacterSkillInstance>(
                    JsonUtility.ToJson(statSkills[0]));
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(
                restoredStatSkill,
                settings);
            Require(restoredStatSkill.drawbackEffect != null
                    && string.Equals(
                        restoredStatSkill.drawbackEffect.effectId,
                        statSkills[0].drawbackEffect.effectId,
                        StringComparison.Ordinal)
                    && Math.Abs(restoredStatSkill.drawbackEffect.value
                        - statSkills[0].drawbackEffect.value) < 0.000001f,
                "The frozen CharacterSkill burden did not survive a JSON save round trip.");

            string extraMechanicalField = validJson.Insert(
                validJson.Length - 1,
                ",\"combinationId\":\"forbidden\"");
            Require(!service.TryValidateResponse(
                    active,
                    extraMechanicalField,
                    out _,
                    out _),
                "A CharacterSkill response containing an extra mechanical field was accepted.");
            CharacterSkillModuleSelectionResponseDto staleDto = BuildValidModuleSelectionDto(active, "검증자");
            staleDto.selectionId = "selection:skill:" + new string('0', 64);
            string stalePresentation = JsonUtility.ToJson(staleDto);
            Require(!service.TryValidateResponse(
                    active,
                    stalePresentation,
                    out _,
                    out string staleError)
                    && staleError.Contains("selectionId", StringComparison.Ordinal),
                "A stale formula selectionId was accepted.");
            CharacterSkillModuleSelectionResponseDto numericDto = BuildValidModuleSelectionDto(active, "검증자");
            numericDto.narrativeFlavor = "검증자의 작업 경험에서 피해 5를 익혔다.";
            string numericPresentation = JsonUtility.ToJson(numericDto);
            Require(!service.TryValidateResponse(
                    active,
                    numericPresentation,
                    out _,
                    out _),
                "A presentation that restates a mechanical number was accepted.");
            string missingField = "{\"selectionId\":\""
                + active.moduleSelectionOffers[0].selectionId
                + "\",\"positiveModuleIds\":[\""
                + active.moduleSelectionOffers[0].positiveModuleIds[0]
                + "\"],\"drawbackModuleIds\":[],\"evidenceFactIds\":[\""
                + active.moduleSelectionOffers[0].evidenceFactIds[0]
                + "\"],\"displayName\":\"검증자 감각\"}";
            Require(!service.TryValidateResponse(active, missingField, out _, out _),
                "A CharacterSkill presentation missing narrativeFlavor was accepted.");

            CharacterSkillModuleSelectionResponseDto negativeOnly = BuildValidModuleSelectionDto(active, "검증자");
            negativeOnly.positiveModuleIds.Clear();
            Require(!service.TryValidateResponse(active, JsonUtility.ToJson(negativeOnly), out _, out _),
                "A negative-only/empty-positive module selection was accepted.");
            CharacterSkillModuleSelectionResponseDto invented = BuildValidModuleSelectionDto(active, "검증자");
            invented.positiveModuleIds[0] = "invented-module";
            Require(!service.TryValidateResponse(active, JsonUtility.ToJson(invented), out _, out _),
                "An unoffered module ID was accepted.");

            CharacterSkillDraft passive = service.CreateDraft(
                progression,
                CharacterSkillKind.Passive,
                1);
            Require(passive.formulaVersion >= CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion
                    && passive.moduleSelectionOffers.Count == 1
                    && passive.frozenMechanics.Count == 0
                    && passive.candidates.Count == 0,
                "A new passive draft did not expose exactly one unresolved selection offer.");
            string passivePrompt = CharacterSkillPromptBuilder.Build(
                NarrativeRequestContextTestFixture.CreateEmpty(), progression,
                passive,
                settings);
            Require(passivePrompt.Contains("selectionId=", StringComparison.Ordinal)
                    && passivePrompt.Contains("positiveModules:", StringComparison.Ordinal)
                    && passivePrompt.Contains(
                        "positiveModuleIds에는 positiveModules의 ID만",
                        StringComparison.Ordinal)
                    && passivePrompt.Contains(
                        "drawbackModuleIds에는 drawbackModules의 ID만",
                        StringComparison.Ordinal)
                    && passivePrompt.Contains(
                        "evidenceFactIds에는 evidenceFactIds의 ID만 1~2개",
                        StringComparison.Ordinal)
                    && passivePrompt.Contains(
                        "48자 이하 간결한 한국어 한 문장",
                        StringComparison.Ordinal)
                    && passivePrompt.Contains("수치와 비용은 응답 후 C#이 계산", StringComparison.Ordinal)
                    && !passivePrompt.Contains("selectedIndex", StringComparison.Ordinal),
                "The formula prompt does not expose the post-selection allocation contract.");
            Require(service.TryValidateResponse(
                    passive,
                    BuildValidModuleSelectionJson(passive, "검증자"),
                    out List<CharacterSkillInstance> passiveSkills,
                    out string passiveError)
                    && passiveSkills.Count == 1,
                $"A valid passive module selection was rejected: {passiveError}");

            string legacySelection =
                "{\"candidates\":[{\"ruleId\":\"legacy\",\"combinationId\":\"legacy\"}]}";
            Require(!service.TryValidateResponse(
                    passive,
                    legacySelection,
                    out _,
                    out _),
                "A legacy candidate-selection response was accepted for new formula generation.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifySkillAcquiredOutcomeAtomicity()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.formulaPolicy.formulaVersion =
            CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion;
        CharacterSkillFormulaCatalogAssetBuilder.Populate(settings);
        using ActorFixture actor = new ActorFixture(
            7791,
            "원장 기술 검증자",
            "human");
        DomainFactDispatcher skillFacts = null;
        IDisposable skillBinding = null;
        try
        {
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new CharacterSkillAcquiredOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CharacterSkillAcquiredOutcomeAdapter()
                });
            GameplayOutcomeLedger ledger = new(
                registry,
                new GameplayOutcomeBufferLimits(16, 0, 64),
                new GameplayOutcomeRunId("skill-acquired-runtime-entry"),
                1L);
            GameplayOutcomeRecorder recorder = new(
                ledger,
                registry,
                new GameEventBus());
            var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
            skillFacts = new DomainFactDispatcher();
            GameplayDomainFactObserverBinding<CharacterSkillAcquiredFact,
                CharacterSkillAcquiredOutcomeReceipt> CreateSkillBinding() =>
                CharacterSkillAcquiredFactMapping.CreateBinding(
                    registry,
                    ledger,
                    recorder,
                    commits);
            skillBinding = CreateSkillBinding().Connect(skillFacts);
            FaultingSkillAcquisitionPublisher outcomes = new(skillFacts);
            CharacterSkillAcquisitionPublicationRuntime skillAcquisitions =
                new(outcomes, outcomes);
            TestSettingsProvider settingsProvider = new(settings);
            CharacterSkillGenerationService service = new(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new MissingLlmRuntimeProvider(),
                CharacterAiEditorTestDependencies.UiClock);
            CharacterProgression progression = actor.Actor.Progression;
            var progressionEvents = new GameEventBus();
            progression.ConstructCharacterProgression(
                service,
                settingsProvider,
                new CharacterProgressionNotificationApplicationAdapter(
                    progressionEvents),
                new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))),
                progressionEvents,
                new UnexpectedEvidenceUseTransaction(),
                outcomes,
                skillAcquisitions,
                new FixedGameSessionStateProvider(7));
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:skill-ledger-success",
                "facility:skill-ledger",
                CharacterActivityOutcomes.Completed,
                day: 5);
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:skill-ledger-failure",
                "facility:skill-ledger",
                CharacterActivityOutcomes.Failed,
                day: 6);

            CharacterSkillDraft draft = service.CreateDraft(
                progression,
                CharacterSkillKind.Active,
                1);
            draft.candidates.Clear();
            for (int index = 0; index < draft.moduleSelectionOffers.Count; index++)
            {
                draft.nextPresentationIndex = index;
                Require(service.TryValidateResponse(
                        draft,
                        BuildValidModuleSelectionJson(draft, "원장 기술 검증자"),
                        out List<CharacterSkillInstance> resolved,
                        out string validationFailure)
                    && resolved.Count == 1,
                    "Formula skill fixture could not resolve candidate "
                    + index + ": " + validationFailure);
                draft.candidates.Add(resolved[0]);
            }
            draft.nextPresentationIndex = draft.moduleSelectionOffers.Count;
            draft.isReady = true;
            draft.presentationState = CharacterSkillPresentationState.Ready;
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.RemoveAll(value => value != null
                && value.kind == CharacterSkillKind.Active
                && value.unlockLevel == 1);
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.Add(draft);
            Require(draft.candidates[0].formulaVersion > 0,
                "Skill acquisition fixture resolved a legacy non-formula skill.");

            bool duplicateObserverRejected = false;
            try
            {
                using IDisposable duplicate =
                    CreateSkillBinding().Connect(skillFacts);
            }
            catch (InvalidOperationException exception)
            {
                duplicateObserverRejected = exception.Message.Contains(
                    "Duplicate durable domain observer",
                    StringComparison.Ordinal);
            }
            Require(duplicateObserverRejected,
                "The dispatcher accepted a duplicate skill-acquisition observer.");

            int influenceBefore = progression.NarrativeLedger.Facts.Sum(
                value => value?.influenceUseCount ?? 0);
            skillBinding.Dispose();
            skillBinding = null;
            Require(!progression.TryChooseActiveSkill(
                    1,
                    0,
                    confirmed: true,
                    out string missingObserver)
                && missingObserver.Contains(
                    "domain-fact-observer-missing",
                    StringComparison.Ordinal),
                "Missing skill-acquisition observer did not reject before owner mutation: "
                + missingObserver);
            Require(progression.ActiveSkills.Count == 0
                    && progression.GrowthState.skillAcquisitionRevision == 0L
                    && progression.NarrativeLedger.Facts.Sum(
                        value => value?.influenceUseCount ?? 0) == influenceBefore,
                "Missing skill-acquisition observer changed owner or evidence state.");
            skillBinding = CreateSkillBinding().Connect(skillFacts);

            outcomes.ThrowNextPublishBeforeCommit = true;
            Require(!progression.TryChooseActiveSkill(
                    1,
                    0,
                    confirmed: true,
                    out string precommitFailure)
                && precommitFailure.Contains(
                    "editor-injected-skill-precommit-exception",
                    StringComparison.Ordinal),
                "Escaped precommit publication exception did not reject: "
                + precommitFailure);
            Require(progression.ActiveSkills.Count == 0
                    && progression.GrowthState.skillAcquisitionRevision == 0L
                    && progression.NarrativeLedger.Facts.Sum(
                        value => value?.influenceUseCount ?? 0) == influenceBefore,
                "Escaped precommit publication exception changed owner or evidence state.");

            outcomes.RejectNextPublish = true;
            bool rejectedCall = progression.TryChooseActiveSkill(
                1,
                0,
                confirmed: true,
                out string rejected);
            Require(!rejectedCall,
                "Injected skill outcome rejection unexpectedly returned success.");
            Require(rejected == "editor-injected-skill-publication-rejection",
                "Injected skill outcome returned the wrong failure: " + rejected);
            CharacterSkillDraft rejectedDraft = progression.Drafts.Single(
                value => value != null
                    && value.kind == CharacterSkillKind.Active
                    && value.unlockLevel == 1);
            Require(!rejectedDraft.permanentlyChosen,
                "Rejected skill outcome left the restored draft chosen.");
            Require(progression.ActiveSkills.Count == 0,
                "Rejected skill outcome left an active skill in its owner slot.");
            Require(progression.GrowthState.skillAcquisitionRevision == 0L,
                "Rejected skill outcome advanced its owner revision.");
            int influenceAfterRejection = progression.NarrativeLedger.Facts.Sum(
                value => value?.influenceUseCount ?? 0);
            Require(influenceAfterRejection == influenceBefore,
                "Rejected skill outcome changed narrative influence from "
                + influenceBefore + " to " + influenceAfterRejection + ".");
            GameplayResultKey resultKey = new(
                CharacterSkillAcquiredOutcomeIds.ProducerId,
                new GameplayOperationId(
                    "character-skill:character:qa-7791:active:1:1"),
                1L,
                0);
            Require(!ledger.TryGetResultIdentity(resultKey, out _),
                "Rejected skill acquisition left a replay identity.");

            outcomes.HideNextCommittedAcknowledgement = true;
            Require(progression.TryChooseActiveSkill(
                    1,
                    0,
                    confirmed: true,
                    out string accepted)
                && progression.Drafts.Single(value => value != null
                    && value.kind == CharacterSkillKind.Active
                    && value.unlockLevel == 1).permanentlyChosen
                && progression.ActiveSkills.Count == 1
                && progression.GrowthState.skillAcquisitionRevision == 1L
                && progression.NarrativeLedger.Facts.Sum(
                    value => value?.influenceUseCount ?? 0) == influenceBefore + 1,
                "Accepted skill acquisition did not atomically publish owner state: "
                + accepted);
            CharacterSkillAcquisitionPublicationData pendingAcquisition =
                skillAcquisitions.PendingForCharacter(
                    actor.Actor.BuildingCharacterId.Value);
            Require(pendingAcquisition != null
                    && pendingAcquisition.pending
                    && pendingAcquisition.fact.characterDisplayName
                        == "원장 기술 검증자"
                    && pendingAcquisition.fact.beforeRevision == 0L
                    && pendingAcquisition.fact.afterRevision == 1L
                    && pendingAcquisition.fact.unlockLevel == 1
                    && pendingAcquisition.fact.skill.id
                        == progression.ActiveSkills[0].id
                    && string.IsNullOrEmpty(
                        pendingAcquisition.publicationId)
                    && string.IsNullOrEmpty(
                        pendingAcquisition.fingerprint),
                "Unknown acknowledgement did not retain the exact frozen skill acquisition.");
            Require(ledger.TryGetResultIdentity(
                    resultKey,
                    out GameplayOutcomeReplayIdentity identity)
                && ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot exact)
                && string.Equals(
                    exact.outcomeTypeId,
                    CharacterSkillAcquiredOutcomeIds.Acquired.Value,
                    StringComparison.Ordinal)
                && exact.facts.Count == 8
                && exact.facts[3].value == progression.ActiveSkills[0].displayName,
                "Accepted skill acquisition did not record its exact selected display name.");

            CharacterProgressionSnapshot acceptedOwner =
                progression.CapturePersistentState();
            string growthJson = DungeonCharacterWorldSaveCodec.SerializeFields(
                acceptedOwner.GrowthState);
            CharacterGrowthState roundTrippedGrowth =
                DungeonSaveJson.Deserialize<CharacterGrowthState>(growthJson);
            string publicationJson =
                new CharacterSkillAcquisitionPublicationSaveSection(
                    skillAcquisitions).Capture();
            CharacterSkillAcquisitionPublicationSaveSection.ValidateJson(
                publicationJson);
            CharacterSkillAcquisitionPublicationSaveData savedPublications =
                DungeonSaveJson.Deserialize<
                    CharacterSkillAcquisitionPublicationSaveData>(
                        publicationJson);
            Require(roundTrippedGrowth.skillAcquisitionRevision == 1L
                    && roundTrippedGrowth.activeSkills.Count == 1
                    && savedPublications.observations.Single().pending,
                "Skill-acquisition owner or publication journal failed JSON round trip.");
            CharacterSkillAcquiredFactPreflight.ValidateObservation(
                savedPublications.observations.Single(),
                ledger.CaptureGameplayOutcomes());

            CharacterSkillAcquisitionPublicationSaveData proofTamper =
                DungeonSaveJson.Deserialize<
                    CharacterSkillAcquisitionPublicationSaveData>(
                        publicationJson);
            proofTamper.observations.Single().publicationId = "tampered-proof";
            bool proofTamperRejected = false;
            try
            {
                new CharacterSkillAcquisitionPublicationRuntime(outcomes, outcomes)
                    .Restore(proofTamper);
            }
            catch (ArgumentException)
            {
                proofTamperRejected = true;
            }
            Require(proofTamperRejected,
                "Pending skill-acquisition journal accepted a partial proof.");

            CharacterSkillAcquisitionPublicationSaveData keyTamper =
                DungeonSaveJson.Deserialize<
                    CharacterSkillAcquisitionPublicationSaveData>(
                        publicationJson);
            keyTamper.observations.Single().publicationKey += ":tampered";
            bool keyTamperRejected = false;
            try
            {
                CharacterSkillAcquiredFactPreflight.ValidateObservation(
                    keyTamper.observations.Single(),
                    ledger.CaptureGameplayOutcomes());
            }
            catch (InvalidOperationException exception)
            {
                keyTamperRejected = exception.Message.Contains(
                    "publication-key-conflict",
                    StringComparison.Ordinal);
            }
            Require(keyTamperRejected,
                "Changed skill-acquisition publication key joined canonical evidence.");

            using ActorFixture restoredActor = new(
                7792,
                "원장 기술 검증자",
                "human",
                persistentId: actor.Actor.BuildingCharacterId.Value,
                detachedRestore: true);
            CharacterProgression restoredProgression =
                restoredActor.Actor.Progression;
            var restoredSkillAcquisitions =
                new CharacterSkillAcquisitionPublicationRuntime(
                    outcomes,
                    outcomes);
            restoredSkillAcquisitions.Restore(savedPublications);
            var restoredProgressionEvents = new GameEventBus();
            restoredProgression.ConstructCharacterProgression(
                service,
                settingsProvider,
                new CharacterProgressionNotificationApplicationAdapter(
                    restoredProgressionEvents),
                new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))),
                restoredProgressionEvents,
                new UnexpectedEvidenceUseTransaction(),
                outcomes,
                restoredSkillAcquisitions,
                new FixedGameSessionStateProvider(7));
            restoredProgression.RestorePersistentState(
                new CharacterProgressionSnapshot(
                    acceptedOwner.Level,
                    acceptedOwner.CurrentExperience,
                    roundTrippedGrowth,
                    acceptedOwner.NarrativeLedger,
                    acceptedOwner.AcquiredTraitState));
            restoredActor.Actor.PublishDetachedRestore();
            int restoredInfluence = restoredProgression.NarrativeLedger.Facts.Sum(
                value => value?.influenceUseCount ?? 0);
            Require(restoredProgression.TryRecoverSkillAcquisition(
                    out string recoveryFailure),
                "Saved skill-acquisition observation did not replay: "
                + recoveryFailure);
            CharacterSkillAcquisitionPublicationData recoveredObservation =
                restoredSkillAcquisitions.Capture().observations.Single();
            Require(restoredSkillAcquisitions.PendingForCharacter(
                        actor.Actor.BuildingCharacterId.Value) == null
                    && !recoveredObservation.pending
                    && !string.IsNullOrWhiteSpace(
                        recoveredObservation.publicationId)
                    && recoveredObservation.fingerprint.Length == 64
                    && restoredProgression.ActiveSkills.Count == 1
                    && restoredProgression.ActiveSkills[0].id
                        == progression.ActiveSkills[0].id
                    && restoredProgression.GrowthState
                        .skillAcquisitionRevision == 1L
                    && restoredProgression.NarrativeLedger.Facts.Sum(
                        value => value?.influenceUseCount ?? 0)
                        == restoredInfluence
                    && outcomes.CommittedPublishCalls == 1
                    && outcomes.ReplayBatchCalls == 1,
                "Exact skill-acquisition recovery repeated effects or skipped canonical replay.");
            progression = restoredProgression;

            progression.AddExperience(GetExperienceToReach(50));
            Require(progression.Level == 50
                    && progression.GrowthState.skillAcquisitionRevision == 1L,
                "Reaching automatic-skill milestones changed the acquisition ledger before a draft was ready.");
            CharacterNarrativeDomain[] automaticDomains =
            {
                CharacterNarrativeDomain.Work,
                CharacterNarrativeDomain.Relationship,
                CharacterNarrativeDomain.Combat
            };
            for (int index = 0; index < 8; index++)
            {
                progression.RecordNarrative(
                    automaticDomains[index % automaticDomains.Length],
                    "skill-ledger-automatic-" + index,
                    "character:qa-7791",
                    CharacterActivityOutcomes.Completed,
                    day: 8 + index);
            }

            int passiveBefore = progression.PassiveSkills.Count;
            var automaticDrafts = new List<CharacterSkillDraft>();
            foreach ((CharacterSkillKind Kind, int UnlockLevel) automatic in new[]
                     {
                         (CharacterSkillKind.Passive, 25),
                         (CharacterSkillKind.Ultimate, 50)
                     })
            {
                CharacterSkillDraft automaticDraft = CreateReadyFormulaDraftForOutcome(
                    service,
                    progression,
                    automatic.Kind,
                    automatic.UnlockLevel,
                    "원장 기술 검증자");
                CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.RemoveAll(value => value != null
                    && value.kind == automatic.Kind
                    && value.unlockLevel == automatic.UnlockLevel);
                CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.Add(automaticDraft);
                long beforeRevision = progression.GrowthState.skillAcquisitionRevision;
                CharacterSkillInstance selected = automaticDraft.candidates[0].Clone();

                progression.OnDraftReady(automaticDraft);

                Require(automaticDraft.permanentlyChosen
                        && automaticDraft.chosenIndex == 0
                        && progression.GrowthState.skillAcquisitionRevision
                            == beforeRevision + 1L,
                    $"Automatic {automatic.Kind} acquisition did not commit exactly once.");
                if (automatic.Kind == CharacterSkillKind.Passive)
                {
                    Require(progression.PassiveSkills.Any(value => value != null
                            && string.Equals(value.id, selected.id, StringComparison.Ordinal)),
                        "The automatic passive result was not installed in its owner slot.");
                }
                else
                {
                    Require(progression.Ultimate != null
                            && string.Equals(
                                progression.Ultimate.id,
                                selected.id,
                                StringComparison.Ordinal),
                        "The automatic ultimate result was not installed in its owner slot.");
                }
                RequireExactSkillAcquiredOutcome(
                    ledger,
                    progression.Actor.BuildingCharacterId.Value,
                    automatic.UnlockLevel,
                    beforeRevision,
                    beforeRevision + 1L,
                    selected);
                automaticDrafts.Add(automaticDraft);
            }
            Require(progression.PassiveSkills.Count == passiveBefore + 1
                    && progression.Ultimate != null
                    && progression.GrowthState.skillAcquisitionRevision == 3L
                    && restoredSkillAcquisitions.Capture().observations.Count == 3
                    && restoredSkillAcquisitions.Capture().observations.All(
                        value => !value.pending),
                "Automatic passive/ultimate acquisition did not leave the expected owner state.");
            Require(ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
                        string.Equals(
                            value.outcomeTypeId,
                            CharacterSkillAcquiredOutcomeIds.Acquired.Value,
                            StringComparison.Ordinal)) == 3,
                "The active, passive, and ultimate acquisitions did not produce exactly three skill-acquired outcomes.");

            string committedGrowth = JsonUtility.ToJson(progression.GrowthState);
            string committedLedger = JsonConvert.SerializeObject(
                ledger.CaptureGameplayOutcomes());
            foreach (CharacterSkillDraft automaticDraft in automaticDrafts)
                progression.OnDraftReady(automaticDraft);
            Require(committedGrowth == JsonUtility.ToJson(progression.GrowthState)
                    && committedLedger == JsonConvert.SerializeObject(
                        ledger.CaptureGameplayOutcomes()),
                "Redelivering an automatic terminal draft duplicated its owner state or skill-acquired outcome.");

            CharacterGrowthState restored =
                DungeonSaveJson.Deserialize<CharacterGrowthState>(
                    DungeonCharacterWorldSaveCodec.SerializeFields(
                        progression.GrowthState));
            Require(restored.skillAcquisitionRevision == 3L
                    && restored.passiveSkills.Any(value => value != null
                        && automaticDrafts.Any(draft => draft.kind == CharacterSkillKind.Passive
                            && string.Equals(
                                draft.candidates[0].id,
                                value.id,
                                StringComparison.Ordinal)))
                    && restored.ultimate != null
                    && automaticDrafts.Any(draft => draft.kind == CharacterSkillKind.Ultimate
                        && string.Equals(
                            draft.candidates[0].id,
                            restored.ultimate.id,
                            StringComparison.Ordinal)),
                "Active/passive/ultimate acquisition state did not survive its runtime save shape.");
            var journalWithGap = restoredSkillAcquisitions.Capture();
            journalWithGap.observations.RemoveAt(1);
            bool gapRejected = false;
            try { CharacterSkillAcquisitionPublicationRuntime.ValidateAndClone(journalWithGap); }
            catch (ArgumentException) { gapRejected = true; }
            Require(gapRejected, "Skill-acquisition publication history accepted a revision gap.");
            return true;
        }
        finally
        {
            skillBinding?.Dispose();
            skillFacts?.Dispose();
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static CharacterSkillDraft CreateReadyFormulaDraftForOutcome(
        CharacterSkillGenerationService service,
        CharacterProgression progression,
        CharacterSkillKind kind,
        int unlockLevel,
        string characterName)
    {
        CharacterSkillDraft draft = service.CreateDraft(
            progression,
            kind,
            unlockLevel);
        draft.candidates.Clear();
        for (int index = 0; index < draft.moduleSelectionOffers.Count; index++)
        {
            draft.nextPresentationIndex = index;
            Require(service.TryValidateResponse(
                    draft,
                    BuildValidModuleSelectionJson(draft, characterName),
                    out List<CharacterSkillInstance> resolved,
                    out string validationFailure)
                && resolved.Count == 1,
                $"Formula {kind} fixture could not resolve candidate {index}: "
                + validationFailure);
            draft.candidates.Add(resolved[0]);
        }
        draft.nextPresentationIndex = draft.moduleSelectionOffers.Count;
        draft.isReady = true;
        draft.presentationState = CharacterSkillPresentationState.Ready;
        Require(draft.candidates.Count > 0
                && draft.candidates.All(value => value != null
                    && value.kind == kind
                    && value.formulaVersion > 0),
            $"Formula {kind} fixture did not resolve a valid selected skill.");
        return draft;
    }

    private static void RequireExactSkillAcquiredOutcome(
        GameplayOutcomeLedger ledger,
        string characterId,
        int unlockLevel,
        long beforeRevision,
        long afterRevision,
        CharacterSkillInstance selected)
    {
        string operationId = "character-skill:" + characterId + ":"
            + selected.kind.ToString().ToLowerInvariant() + ":"
            + unlockLevel + ":" + afterRevision;
        GameplayResultKey resultKey = new(
            CharacterSkillAcquiredOutcomeIds.ProducerId,
            new GameplayOperationId(operationId),
            afterRevision,
            0);
        Require(ledger.TryGetResultIdentity(
                resultKey,
                out GameplayOutcomeReplayIdentity identity)
            && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
            && ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot exact)
            && string.Equals(
                exact.outcomeTypeId,
                CharacterSkillAcquiredOutcomeIds.Acquired.Value,
                StringComparison.Ordinal)
            && exact.participants.Count == 1
            && string.Equals(
                exact.participants[0].entityId,
                characterId,
                StringComparison.Ordinal)
            && string.Equals(
                exact.participants[0].roleId,
                CharacterSkillAcquiredOutcomeIds.OwnerRole.Value,
                StringComparison.Ordinal)
            && exact.metrics.Any(metric => string.Equals(
                    metric.metricId,
                    CharacterSkillAcquiredOutcomeIds.BeforeRevision.Value,
                    StringComparison.Ordinal)
                && metric.value == beforeRevision)
            && exact.metrics.Any(metric => string.Equals(
                    metric.metricId,
                    CharacterSkillAcquiredOutcomeIds.AfterRevision.Value,
                    StringComparison.Ordinal)
                && metric.value == afterRevision)
            && exact.facts.Any(fact => string.Equals(
                    fact.factId,
                    CharacterSkillAcquiredOutcomeIds.SkillId.Value,
                    StringComparison.Ordinal)
                && string.Equals(fact.value, selected.id, StringComparison.Ordinal))
            && exact.facts.Any(fact => string.Equals(
                    fact.factId,
                    CharacterSkillAcquiredOutcomeIds.SkillKind.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    fact.value,
                    selected.kind.ToString(),
                    StringComparison.Ordinal))
            && exact.facts.Any(fact => string.Equals(
                    fact.factId,
                    CharacterSkillAcquiredOutcomeIds.UnlockLevel.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    fact.value,
                    unlockLevel.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            && exact.facts.Any(fact => string.Equals(
                    fact.factId,
                    CharacterSkillAcquiredOutcomeIds.DisplayName.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    fact.value,
                    selected.displayName,
                    StringComparison.Ordinal)),
            $"The selected {selected.kind} did not publish its exact skill-acquired identity and snapshot.");
    }

    private static bool VerifyModuleSelectionBatchContract()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.formulaPolicy.formulaVersion =
            CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion;
        CharacterSkillFormulaCatalogAssetBuilder.Populate(settings);
        using ActorFixture actor = new ActorFixture(7718, "묶음 검증자", "human");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            MutableUiClock clock = new MutableUiClock();
            BatchSkillLlmRuntime runtime = new BatchSkillLlmRuntime();
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new TestLlmRuntimeProvider(runtime),
                clock);
            CharacterProgression progression = actor.Actor.Progression;
            progression.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:module-selection-batch",
                "facility:module-selection-batch",
                CharacterActivityOutcomes.Completed,
                day: 1);

            CharacterSkillDraft successDraft = service.CreateDraft(
                progression,
                CharacterSkillKind.Active,
                1);
            string successJson = BuildValidModuleSelectionBatchJson(
                service,
                successDraft,
                "묶음 검증자");
            CharacterSkillModuleSelectionBatchResponseDto qualityPayload =
                JsonUtility.FromJson<CharacterSkillModuleSelectionBatchResponseDto>(
                    successJson);
            CharacterSkillModuleSelectionBatchResponseDto duplicateNames =
                JsonUtility.FromJson<CharacterSkillModuleSelectionBatchResponseDto>(
                    successJson);
            duplicateNames.candidates[1].displayName =
                duplicateNames.candidates[0].displayName;
            Require(!duplicateNames.Validate(out string duplicateNameError)
                    && duplicateNameError.Contains("repeats another candidate's skill name",
                        StringComparison.Ordinal),
                "A batch with duplicate skill names passed validation.");
            qualityPayload.candidates[0].selectionId =
                "selection:skill:deadf12beef";
            string qualityPrompt = CharacterSkillPromptBuilder.BuildEnvelope(
                progression,
                successDraft,
                settings,
                CharacterSkillPromptBuilder.BuildPublicMaterial(NarrativeRequestContextTestFixture.CreateEmpty(), progression),
                batchModuleSelection: true).Prompt;
            LlmStaticSchemaDefinition moduleSelectionSchema =
                LlmStaticSchemaCatalog.Require(
                    LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id);
            Require(moduleSelectionSchema.Version == 7
                    && moduleSelectionSchema.Json.Contains(
                        "^[a-z][a-z0-9]*(_[a-z0-9]+)*$",
                        StringComparison.Ordinal)
                    && moduleSelectionSchema.Json.Contains(
                        "^character-skill:drawback:[a-z][a-z0-9]*(-[a-z0-9]+)*$",
                        StringComparison.Ordinal)
                    && moduleSelectionSchema.Json.Contains(
                        "\"evidenceFactIds\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":2",
                        StringComparison.Ordinal)
                    && moduleSelectionSchema.Json.Contains(
                        "\"narrativeFlavor\":{\"type\":\"string\"",
                        StringComparison.Ordinal)
                    && moduleSelectionSchema.Json.Contains(
                        "\"maxLength\":48",
                        StringComparison.Ordinal),
                "The v7 module-selection schema does not constrain field namespaces and response size.");
            CharacterSkillModuleSelectionResponseDto invalidCardinality =
                BuildValidModuleSelectionDto(successDraft, "묶음 검증자");
            invalidCardinality.evidenceFactIds = new List<string>
            {
                successDraft.moduleSelectionOffers[0].evidenceFactIds[0],
                successDraft.moduleSelectionOffers[0].evidenceFactIds[0],
                successDraft.moduleSelectionOffers[0].evidenceFactIds[0]
            };
            Require(!invalidCardinality.Validate(out _),
                "A module-selection response with more than two evidence IDs passed DTO validation.");
            CharacterSkillModuleSelectionResponseDto multilinePresentation =
                BuildValidModuleSelectionDto(successDraft, "묶음 검증자");
            multilinePresentation.narrativeFlavor = "첫 문장\n둘째 문장";
            Require(!multilinePresentation.Validate(out _),
                "A multiline module-selection narrative passed DTO validation.");
            CharacterSkillModuleSelectionResponseDto fieldConfusion =
                BuildValidModuleSelectionDto(successDraft, "묶음 검증자");
            fieldConfusion.positiveModuleIds[0] =
                successDraft.moduleSelectionOffers[0].evidenceFactIds[0];
            Require(!service.TryValidateResponse(
                    successDraft,
                    JsonUtility.ToJson(fieldConfusion),
                    out _,
                    out _),
                "An evidence ID placed in positiveModuleIds passed C# offered-ID validation.");
            NarrativeTextQualityGate qualityGate = new NarrativeTextQualityGate();
            NarrativeQualityResult hashCollision = qualityGate.Evaluate(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection,
                qualityPrompt,
                JsonUtility.ToJson(qualityPayload));
            Require(hashCollision.Verdict != NarrativeQualityVerdict.HardReject,
                "An Fxx-like substring inside an opaque selection id was treated as an inline reference.");
            qualityPayload.candidates[0].narrativeFlavor += " F99";
            NarrativeQualityResult unknownStandalone = qualityGate.Evaluate(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection,
                qualityPrompt,
                JsonUtility.ToJson(qualityPayload));
            Require(unknownStandalone.Verdict == NarrativeQualityVerdict.HardReject,
                "An unknown standalone F99 reference was not rejected.");
            using (UnityEngine.Networking.UnityWebRequest transportRequest =
                   new DungeonStoryHostStructuredChatBackend().BuildRequest(
                       "http://127.0.0.1:1",
                       "test-model",
                       LocalLlmRequestProfiles.CharacterSkillModuleSelection,
                       LlmStaticSchemaCatalog.Require(
                           LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id),
                       qualityPrompt))
            {
                string transportBody = Encoding.UTF8.GetString(
                    transportRequest.uploadHandler.data);
                Require(!qualityPrompt.Contains("/no_think", StringComparison.Ordinal)
                        && transportBody.Contains("/no_think", StringComparison.Ordinal),
                    "CharacterSkill transport did not add non-thinking mode without mutating the audit prompt.");
            }
            service.RequestDraft(progression, successDraft);
            service.Tick();
            Require(runtime.BatchCallCount == 1
                    && runtime.HasPendingCallback
                    && runtime.LastPrompt.Contains("candidateCount=3", StringComparison.Ordinal)
                    && runtime.LastPrompt.Contains(
                        "positiveModuleIds 조합을 중복하지 않는다",
                        StringComparison.Ordinal),
                "An active draft was not submitted as one exact-count batch request.");
            runtime.Complete(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                successJson,
                string.Empty,
                string.Empty));
            Require(successDraft.isReady
                    && successDraft.candidates.Count == 3
                    && successDraft.frozenMechanics.Count == 3
                    && successDraft.nextPresentationIndex == 3
                    && service.PendingRequestCount == 0,
                "One valid active batch did not atomically commit all three candidates: "
                    + service.LastDiagnostic + "; correction=" + successDraft.presentationCorrection
                    + "; candidates=" + successDraft.candidates.Count
                    + "; frozen=" + successDraft.frozenMechanics.Count
                    + "; next=" + successDraft.nextPresentationIndex);

            CharacterSkillDraft countDraft = service.CreateDraft(
                progression,
                CharacterSkillKind.Active,
                10);
            CharacterSkillModuleSelectionBatchResponseDto countPayload =
                JsonUtility.FromJson<CharacterSkillModuleSelectionBatchResponseDto>(
                    BuildValidModuleSelectionBatchJson(
                        service,
                        countDraft,
                        "묶음 검증자"));
            countPayload.candidates.RemoveAt(countPayload.candidates.Count - 1);
            service.RequestDraft(progression, countDraft);
            service.Tick();
            Require(runtime.BatchCallCount == 2 && runtime.HasPendingCallback,
                "The exact-count rejection fixture was not submitted once.");
            runtime.Complete(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                JsonUtility.ToJson(countPayload),
                string.Empty,
                string.Empty));
            Require(!countDraft.isReady
                    && countDraft.candidates.Count == 0
                    && countDraft.frozenMechanics.Count == 0
                    && countDraft.nextPresentationIndex == 0
                    && countDraft.presentationFailureCount == 1
                    && service.LastDiagnostic.Contains("required count", StringComparison.Ordinal),
                "A short batch was accepted or partially mutated the active draft.");
            service.CancelRequests(progression);

            CharacterSkillDraft invalidDraft = service.CreateDraft(
                progression,
                CharacterSkillKind.Active,
                20);
            CharacterSkillModuleSelectionBatchResponseDto invalidPayload =
                JsonUtility.FromJson<CharacterSkillModuleSelectionBatchResponseDto>(
                    BuildValidModuleSelectionBatchJson(
                        service,
                        invalidDraft,
                        "묶음 검증자"));
            invalidPayload.candidates[1].selectionId =
                "selection:skill:" + new string('0', 64);
            service.RequestDraft(progression, invalidDraft);
            service.Tick();
            Require(runtime.BatchCallCount == 3 && runtime.HasPendingCallback,
                "The atomic rejection fixture was not submitted once.");
            runtime.Complete(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                JsonUtility.ToJson(invalidPayload),
                string.Empty,
                string.Empty));
            Require(!invalidDraft.isReady
                    && invalidDraft.candidates.Count == 0
                    && invalidDraft.frozenMechanics.Count == 0
                    && invalidDraft.nextPresentationIndex == 0
                    && invalidDraft.presentationFailureCount == 1,
                "One illegal batch item partially committed earlier legal candidates.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyModuleValidation()
    {
        CharacterSkillSystemSettingsSO settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        using ActorFixture actor = new ActorFixture(
            7717,
            "검증자",
            "human");
        try
        {
            CharacterSkillCombinationSemanticsFactory.ValidateSettingsCoverage(settings);
            CharacterSkillCandidateRule semanticsRule = new CharacterSkillCandidateRule
            {
                rarity = CharacterSkillRarity.Legendary,
                budget = 99,
                trigger = CharacterSkillTrigger.ManualCombat,
                target = CharacterSkillTarget.Enemy,
                ultimateDomain = CharacterUltimateDomain.None,
                cooldownTurns = 1,
                mechanicalPolicySource = CharacterSkillMechanicalPolicySource.AuthoredRule,
                usableFrom = OffenseFormationMask.Front | OffenseFormationMask.Middle,
                targetPositions = OffenseFormationMask.Front | OffenseFormationMask.Middle,
                allowedModuleIds = new List<string> { "conditional_amplify" },
                allowedVariantIds = new List<string> { "critical", "wounded" }
            };
            CharacterSkillDraft semanticsDraft = new CharacterSkillDraft
            {
                kind = CharacterSkillKind.Active,
                requestKey = "qa:character-skill-public-semantics",
                rules = new List<CharacterSkillCandidateRule> { semanticsRule }
            };
            CharacterSkillRuleIdentity.Ensure(semanticsDraft);
            CharacterSkillAllowedCombination woundedCombination =
                CharacterSkillCombinationCatalog
                    .Build(semanticsRule, settings, semanticsDraft.kind)
                    .Single(candidate => candidate.Modules.Count == 1
                        && candidate.Modules[0].moduleId == "conditional_amplify"
                        && candidate.Modules[0].variantId == "wounded");
            CharacterSkillCombinationSemanticsDto woundedSemantics =
                CharacterSkillCombinationSemanticsFactory.Create(
                    woundedCombination,
                    semanticsRule,
                    semanticsDraft.kind,
                    settings);
            CharacterSkillModuleSemanticsDto woundedModule = woundedSemantics.modules.Single();
            string canonicalWounded = CharacterSkillCombinationSemanticsFactory
                .SerializeCanonical(woundedSemantics);
            Require(woundedModule.executionScope == "offense_battle"
                    && woundedModule.effectKind == "conditional_basic_damage"
                    && woundedModule.terms.Contains("health_ratio_subject=selected_target")
                    && woundedModule.terms.Contains("health_ratio_formula=current_health/max(1,max_health)")
                    && woundedModule.terms.Contains("comparison=less_than_or_equal")
                    && woundedModule.terms.Contains("health_ratio_threshold=0.5")
                    && woundedModule.terms.Contains("amplified_effect=separate_basic_damage")
                     && woundedModule.terms.Contains("separate_basic_damage_multiplier=0.35")
                     && woundedModule.descriptionKo.Contains("선택 대상", StringComparison.Ordinal)
                     && woundedModule.descriptionKo.Contains("50%", StringComparison.Ordinal)
                     && woundedModule.descriptionKo.Contains("비율 0.5", StringComparison.Ordinal)
                     && woundedModule.descriptionKo.Contains("이하", StringComparison.Ordinal),
                "The public wounded-amplification contract omitted or changed its actual runtime semantics: "
                + canonicalWounded);
            Require(CharacterSkillCombinationSemanticsFactory.TryValidateCanonicalJson(
                    canonicalWounded,
                    woundedCombination,
                    semanticsRule,
                    semanticsDraft.kind,
                    settings,
                    out string canonicalError),
                $"Exact canonical CharacterSkill semantics were rejected: {canonicalError}");

            CharacterSkillCombinationSemanticsDto missingCondition = woundedSemantics.Clone();
            missingCondition.modules[0].terms.Remove("comparison=less_than_or_equal");
            Require(!CharacterSkillCombinationSemanticsFactory.TryValidateExact(
                    missingCondition,
                    woundedCombination,
                    semanticsRule,
                    semanticsDraft.kind,
                    settings,
                    out _),
                "CharacterSkill semantics accepted a missing comparison condition.");

            CharacterSkillCombinationSemanticsDto reversedComparison = woundedSemantics.Clone();
            int comparisonIndex = reversedComparison.modules[0].terms.IndexOf(
                "comparison=less_than_or_equal");
            reversedComparison.modules[0].terms[comparisonIndex] = "comparison=greater_than";
            Require(!CharacterSkillCombinationSemanticsFactory.TryValidateExact(
                    reversedComparison,
                    woundedCombination,
                    semanticsRule,
                    semanticsDraft.kind,
                    settings,
                    out _),
                "CharacterSkill semantics accepted the opposite health comparison.");

            CharacterSkillCombinationSemanticsDto wrongThreshold = woundedSemantics.Clone();
            int thresholdIndex = wrongThreshold.modules[0].terms.IndexOf(
                "health_ratio_threshold=0.5");
            wrongThreshold.modules[0].terms[thresholdIndex] = "health_ratio_threshold=0.5001";
            Require(!CharacterSkillCombinationSemanticsFactory.TryValidateExact(
                    wrongThreshold,
                    woundedCombination,
                    semanticsRule,
                    semanticsDraft.kind,
                    settings,
                    out _),
                "CharacterSkill semantics accepted a runtime-mismatched health threshold.");

            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new MissingLlmRuntimeProvider(),
                CharacterAiEditorTestDependencies.UiClock);
            CharacterProgression progression = actor.Actor.Progression;
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:skill-validation",
                "facility:skill-validation",
                CharacterActivityOutcomes.Completed,
                day: 1);
            CharacterSkillDraft draft = service.CreateDraft(progression, CharacterSkillKind.Active, 1);
            CharacterSkillGenerationResponseDto valid = BuildValidResponse(draft, settings);
            string validJson = JsonUtility.ToJson(valid);
            Require(service.TryValidateResponse(draft, validJson, out List<CharacterSkillInstance> skills, out string validError)
                && skills.Count == 3,
                $"A valid constrained response was rejected: {validError}");
            Require(skills.Select(skill => skill.combinationId)
                    .SequenceEqual(valid.candidates.Select(candidate => candidate.combinationId))
                && skills.Select(skill => skill.ruleId)
                    .SequenceEqual(draft.rules.Select(rule => rule.ruleId)),
                "The validated response replaced the model-selected rule/combination identity.");

            CharacterSkillGenerationResponseDto reordered = BuildValidResponse(draft, settings);
            reordered.candidates.Reverse();
            Require(service.TryValidateResponse(
                    draft,
                    JsonUtility.ToJson(reordered),
                    out List<CharacterSkillInstance> reorderedSkills,
                    out string reorderedError)
                && reorderedSkills.Select(skill => skill.ruleId)
                    .SequenceEqual(draft.rules.Select(rule => rule.ruleId)),
                $"Rule identity did not preserve authored rule order independently of JSON array order: {reorderedError}");

            CharacterSkillGenerationResponseDto wrongCount = BuildValidResponse(draft, settings);
            wrongCount.candidates.RemoveAt(wrongCount.candidates.Count - 1);
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(wrongCount), out _, out _),
                "A response with the wrong candidate count was accepted.");

            string extraRootField = validJson.Insert(validJson.Length - 1, ",\"mechanics\":1");
            Require(!service.TryValidateResponse(draft, extraRootField, out _, out _),
                "A CharacterSkill response with an extra root mechanics field was accepted.");

            string candidateMarker = "\"narrativeReason\":\"";
            int candidateMarkerIndex = validJson.IndexOf(candidateMarker, StringComparison.Ordinal);
            int candidateObjectEnd = validJson.IndexOf('}', candidateMarkerIndex);
            string extraCandidateField = validJson.Insert(candidateObjectEnd, ",\"cooldownTurns\":0");
            Require(!service.TryValidateResponse(draft, extraCandidateField, out _, out _),
                "A CharacterSkill candidate with an extra mechanical field was accepted.");

            CharacterSkillDraft passiveDraft = service.CreateDraft(
                progression,
                CharacterSkillKind.Passive,
                1);
            Require(passiveDraft.rules.All(rule => rule.allowedModuleIds.All(moduleId =>
                    !CharacterSkillValidation.WouldSelfTrigger(moduleId, rule.trigger))),
                "A self-triggering module was offered in a passive draft.");
            string passivePrompt = CharacterSkillPromptBuilder.Build(
                NarrativeRequestContextTestFixture.CreateEmpty(), progression,
                passiveDraft,
                settings);
            Require(passivePrompt.Contains("candidateCount=1")
                && passivePrompt.Contains("제공된 ruleId의 후보 하나만")
                && !passivePrompt.Contains("세 후보의"),
                "The single-passive prompt contains conflicting candidate-count instructions.");
            CharacterSkillGenerationResponseDto passive = BuildValidResponse(passiveDraft, settings);
            Require(service.TryValidateResponse(
                    passiveDraft,
                    JsonUtility.ToJson(passive),
                    out List<CharacterSkillInstance> passiveSkills,
                    out string passiveError)
                && passiveSkills.Count == 1,
                $"A valid non-ultimate management passive was rejected: {passiveError}");

            CharacterSkillAllowedCombination allowedCombination = CharacterSkillCombinationCatalog
                .Build(passiveDraft.rules[0], settings, CharacterSkillKind.Passive)
                .First();
            CharacterSkillGenerationResponseDto combinationResponse = BuildValidResponse(
                passiveDraft,
                settings);
            combinationResponse.candidates[0].combinationId = allowedCombination.Id;
            Require(service.TryValidateResponse(
                    passiveDraft,
                    JsonUtility.ToJson(combinationResponse),
                    out List<CharacterSkillInstance> combinationSkills,
                    out string combinationError)
                && combinationSkills.Count == 1
                && combinationSkills[0].modules.Count == allowedCombination.Modules.Count,
                $"A valid authored combination ID was rejected: {combinationError}");

            combinationResponse.candidates[0].combinationId = "unknown-combination";
            Require(!service.TryValidateResponse(
                    passiveDraft,
                    JsonUtility.ToJson(combinationResponse),
                    out _,
                    out _),
                "An unknown authored combination ID was accepted.");

            CharacterSkillGenerationResponseDto unknownRule = BuildValidResponse(draft, settings);
            unknownRule.candidates[0].ruleId = "skill-rule:unknown";
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(unknownRule), out _, out _),
                "An unknown rule ID was accepted.");

            CharacterSkillGenerationResponseDto repairedRule = BuildValidResponse(draft, settings);
            repairedRule.candidates[0].ruleId = " " + repairedRule.candidates[0].ruleId + " ";
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(repairedRule), out _, out _),
                "A whitespace-repaired rule ID was accepted instead of failing exact validation.");

            CharacterSkillGenerationResponseDto repairedCombination = BuildValidResponse(draft, settings);
            repairedCombination.candidates[0].combinationId =
                " " + repairedCombination.candidates[0].combinationId + " ";
            Require(!service.TryValidateResponse(
                    draft,
                    JsonUtility.ToJson(repairedCombination),
                    out _,
                    out _),
                "A whitespace-repaired combination ID was accepted instead of failing exact validation.");

            CharacterSkillGenerationResponseDto duplicateRule = BuildValidResponse(draft, settings);
            duplicateRule.candidates[1].ruleId = duplicateRule.candidates[0].ruleId;
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(duplicateRule), out _, out _),
                "A duplicated response rule ID was accepted.");

            CharacterSkillGenerationResponseDto overBudget = BuildValidResponse(draft, settings);
            draft.rules[0].budget = 0;
            Require(!service.TryValidateResponse(draft, JsonUtility.ToJson(overBudget), out _, out _),
                "A response above its authored budget was accepted.");

            CharacterSkillDraft duplicateMechanicalDraft = new CharacterSkillDraft
            {
                kind = passiveDraft.kind,
                requestKey = passiveDraft.requestKey + ":mechanical-duplicate",
                rules = new List<CharacterSkillCandidateRule>
                {
                    passiveDraft.rules[0].Clone(),
                    passiveDraft.rules[0].Clone()
                }
            };
            duplicateMechanicalDraft.rules[0].ruleId = "skill-rule:duplicate-a";
            duplicateMechanicalDraft.rules[1].ruleId = "skill-rule:duplicate-b";
            CharacterSkillGenerationResponseDto duplicateMechanical = BuildValidResponse(
                duplicateMechanicalDraft,
                settings);
            CharacterSkillAllowedCombination firstRuleCombination = CharacterSkillCombinationCatalog
                .Build(duplicateMechanicalDraft.rules[0], settings, duplicateMechanicalDraft.kind)
                .First();
            CharacterSkillAllowedCombination secondRuleSameMechanical = CharacterSkillCombinationCatalog
                .Build(duplicateMechanicalDraft.rules[1], settings, duplicateMechanicalDraft.kind)
                .First(candidate => string.Equals(
                    candidate.MechanicalIdentity,
                    firstRuleCombination.MechanicalIdentity,
                    StringComparison.Ordinal));
            duplicateMechanical.candidates[0].combinationId = firstRuleCombination.Id;
            duplicateMechanical.candidates[1].combinationId = secondRuleSameMechanical.Id;
            Require(!service.TryValidateResponse(
                    duplicateMechanicalDraft,
                    JsonUtility.ToJson(duplicateMechanical),
                    out _,
                    out _),
                "Two rule-local IDs that resolve to the same mechanical combination were accepted.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyTrainingExperience()
    {
        using ActorFixture fixture = new ActorFixture(81005, "Trainee", "Slime");
        GameObject buildingObject = new GameObject("ProgressionTrainingFacility");
        try
        {
            Facility facility = buildingObject.AddComponent<Facility>();
            BuildingTrainingAbility training = new BuildingTrainingAbility
            {
                experienceAmount = 24,
                moodAmount = 0f
            };
            training.ApplyUseCompleted(fixture.Actor, facility);
            Require(fixture.Actor.Progression.Level == 2
                    && fixture.Actor.Progression.CurrentExperience == 4,
                "Training did not award its configured experience.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(buildingObject);
        }

        return true;
    }

    private static bool VerifyRetryAndRequestKeyResume()
    {
        CharacterSkillSystemSettingsSO settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.initialRetrySeconds = 0f;
        settings.maximumRetrySeconds = 0f;
        const string PersistentId = "character:qa-skill-retry";
        using ActorFixture sourceActor = new ActorFixture(
            991,
            "재시도 원본",
            "human",
            persistentId: PersistentId);
        using ActorFixture restoredActor = new ActorFixture(
            993,
            "재시도 복원",
            "human",
            persistentId: PersistentId);
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            SequencedLlmRuntime runtime = new SequencedLlmRuntime();
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new TestLlmRuntimeProvider(runtime),
                CharacterAiEditorTestDependencies.UiClock);
            CharacterProgression source = sourceActor.Actor.Progression;
            source.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:skill-retry",
                "facility:skill-retry",
                CharacterActivityOutcomes.Completed,
                day: 1);
            CharacterSkillDraft draft = service.CreateDraft(source, CharacterSkillKind.Active, 1, revision: 4);
            string requestKey = draft.requestKey;
            runtime.Enqueue(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                "{}",
                string.Empty,
                string.Empty));
            service.RequestDraft(source, draft);
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 1 && !draft.isReady,
                "A schema-rejected generation request did not remain pending.");

            CharacterGrowthState restoredGrowth = source.GrowthState.Clone();
            restoredGrowth.drafts.Clear();
            restoredGrowth.drafts.Add(draft.Clone());
            restoredGrowth.passiveSkills.Add(new CharacterSkillInstance
            {
                id = "qa:restored-passive",
                displayName = "복원 패시브",
                description = "재개 요청 격리용 패시브",
                narrativeReason = "이미 검증됨",
                kind = CharacterSkillKind.Passive,
                rarity = CharacterSkillRarity.Advanced,
                trigger = CharacterSkillTrigger.WorkCompleted,
                target = CharacterSkillTarget.Self,
                modules = new List<CharacterSkillModuleSelection>
                {
                    new CharacterSkillModuleSelection
                    {
                        moduleId = "work_speed",
                        variantId = "small"
                    }
                }
            });
            restoredGrowth.pendingRequestKeys.Clear();
            restoredGrowth.pendingRequestKeys.Add(requestKey);
            CharacterNarrativeLedger restoredLedger =
                source.NarrativeLedger.Clone();
            service.CancelRequests(source);

            CharacterProgression restored = restoredActor.Actor.Progression;
            restored.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            restored.RestorePersistentState(new CharacterProgressionSnapshot(
                1,
                0,
                restoredGrowth,
                restoredLedger));
            CharacterSkillDraft restoredDraft = restored.Drafts.First(item => item.requestKey == requestKey);
            int expectedCalls = runtime.CharacterSkillCallCount;
            while (!restoredDraft.isReady)
            {
                runtime.Enqueue(new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    BuildValidModuleSelectionJson(
                        restoredDraft,
                        restoredDraft.requestContext.characterName),
                    string.Empty,
                    string.Empty));
                service.Tick();
                restoredDraft = restored.Drafts.First(item => item.requestKey == requestKey);
                expectedCalls++;
                Require(runtime.CharacterSkillCallCount == expectedCalls,
                    "A restored formula presentation slot was not submitted exactly once. "
                    + $"actualCalls={runtime.CharacterSkillCallCount}, expectedCalls={expectedCalls}, "
                    + $"pending={service.PendingRequestCount}, index={restoredDraft.nextPresentationIndex}, "
                    + $"state={restoredDraft.presentationState}, diagnostic={service.LastDiagnostic}");
            }
            Require(restoredDraft.isReady && restoredDraft.requestKey == requestKey,
                "The restored request changed key or failed to commit its prepared result.");
            Require(restoredDraft.candidates.Count == 3
                    && restoredDraft.nextPresentationIndex == 3,
                "The restored active draft did not complete all three frozen presentations.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyFrozenSkillRequestContext()
    {
        CharacterSkillSystemSettingsSO settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        try
        {
            foreach (CharacterSkillKind kind in new[] { CharacterSkillKind.Active, CharacterSkillKind.Passive, CharacterSkillKind.Ultimate })
            {
                string actorId = "character:qa-frozen-skill-" + kind;
                using ActorFixture sourceFixture = new(813001, "문맥 원본", "human", persistentId: actorId);
                using ActorFixture restoredFixture = new(813002, "바뀐 표기", "human", persistentId: actorId);
                CharacterProgression source = sourceFixture.Actor.Progression;
                CharacterProgression restored = restoredFixture.Actor.Progression;
                var provider = new TestSettingsProvider(settings);
                var transport = new DelayedSkillLlmRuntime();
                var service = new CharacterSkillGenerationService(NarrativeRequestContextTestFixture.CreateEmpty(), provider, new TestLlmRuntimeProvider(transport), new MutableUiClock());
                source.ConstructCharacterProgression(service, provider, gameEventBus: new GameEventBus(),
                    profileProjector: new CharacterProgressionProfileProjector(new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                        new CharacterRuntimeProfileFactory(new ResourceGameContentCatalog(new UnityGameContentRootLoader()))));
                source.RecordNarrative(CharacterNarrativeDomain.Work, "work:frozen-skill", "facility:frozen-skill", "completed", day: 1);
                CharacterSkillDraft draft = service.CreateDraft(source, kind, 1, revision: 7);
                CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(source).drafts.RemoveAll(value => value.kind == kind && value.unlockLevel == 1);
                CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(source).drafts.Add(draft);
                service.RequestDraft(source, draft);
                service.Tick();
                string originalPrompt = transport.LastPrompt;
                string originalKey = transport.LastRequestKey;
                Require(draft.requestContextVersion == 1 && draft.RestoreRequestPublicContext() != null
                    && !string.IsNullOrEmpty(originalPrompt), "Skill request did not freeze its context: " + kind);
                var jsonDraft = DungeonSaveSectionPayload.Deserialize<CharacterSkillDraft>(JsonUtility.ToJson(draft));
                Require(jsonDraft.RestoreRequestPublicContext().SemanticHash == draft.requestContext.publicContext.semanticHash,
                    "Skill context did not survive the exact persisted JSON reader.");

                source.RecordNarrative(CharacterNarrativeDomain.Mood, "mood:after-skill-dispatch", "target:later", "positive", day: 2);
                Require(!draft.isReady && service.PendingRequestCount > 0,
                    "Later-event fixture must leave the real asynchronous request pending.");
                CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(source).origin += " 이후 변경";
                CharacterProgressionSnapshot saved = source.CapturePersistentState();
                service.CancelRequests(source);
                var resumedTransport = new DelayedSkillLlmRuntime();
                var resumedService = new CharacterSkillGenerationService(NarrativeRequestContextTestFixture.CreateEmpty(), provider, new TestLlmRuntimeProvider(resumedTransport), new MutableUiClock());
                restored.ConstructCharacterProgression(resumedService, provider, gameEventBus: new GameEventBus(),
                    profileProjector: new CharacterProgressionProfileProjector(new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                        new CharacterRuntimeProfileFactory(new ResourceGameContentCatalog(new UnityGameContentRootLoader()))));
                string beforeInvalid = JsonUtility.ToJson(restored.GrowthState);
                for (int tamper = 0; tamper < 9; tamper++)
                {
                    var invalidGrowth = saved.GrowthState.Clone();
                    CharacterSkillDraft invalid = invalidGrowth.drafts.Single(value => value.requestKey == draft.requestKey);
                    if (tamper == 0) invalid.requestContext.publicContext.publicFacts[0].text += " changed";
                    else if (tamper == 1) invalid.requestContext = null;
                    else if (tamper == 2) invalid.requestContextVersion = 2;
                    else if (tamper == 3) invalid.requestContextVersion = 0;
                    else if (tamper == 4) invalid.requestKey += ":changed";
                    else if (tamper == 5) invalid.requestContext.characterName += " 변경";
                    else if (tamper == 6) invalid.ownerBinding.persistentId += ":changed";
                    else if (tamper == 7) { invalid.requestContextVersion = 0; invalid.requestContext = null; }
                    else
                    {
                        invalid.requestContext.characterName = string.Empty;
                        invalid.requestContextHash = invalid.requestContext.ComputeHash(invalid.requestKey);
                    }
                    bool rejected = false;
                    try { restored.RestorePersistentState(new CharacterProgressionSnapshot(saved.Level, saved.CurrentExperience,
                        invalidGrowth, saved.NarrativeLedger, saved.AcquiredTraitState)); }
                    catch (InvalidOperationException) { rejected = true; }
                    Require(rejected && JsonUtility.ToJson(restored.GrowthState) == beforeInvalid,
                        "Invalid skill context changed progression during restore: " + kind + "/" + tamper);
                }
                restored.RestorePersistentState(saved);
                resumedService.CancelRequests(restored);
                CharacterSkillDraft resumed = CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(restored).drafts.Single(value => value.requestKey == draft.requestKey);
                Require(resumed.ownerBinding.Matches(restored, out string ownerFailure),
                    "Frozen-context fixture did not restore its original lifecycle: " + ownerFailure);
                resumedService.RequestDraft(restored, resumed);
                resumedService.Tick();
                Require(resumedTransport.LastPrompt == originalPrompt && resumedTransport.LastRequestKey == originalKey
                    && restored.NarrativeLedger.Facts.Any(value => value.factId == "mood:after-skill-dispatch"),
                    "Later facts/header changes altered the restored skill request: " + kind
                    + "; promptEqual=" + (resumedTransport.LastPrompt == originalPrompt)
                    + "; keyEqual=" + (resumedTransport.LastRequestKey == originalKey)
                    + "; calls=" + resumedTransport.CharacterSkillCallCount + "; " + resumedService.LastDiagnostic
                    + "; ledger=" + string.Join(",", restored.NarrativeLedger.Facts.Select(value => value.factId)));
                resumedService.CancelRequests(restored);
            }
            var legacy = DungeonSaveSectionPayload.Deserialize<CharacterSkillDraft>(JsonUtility.ToJson(new CharacterSkillDraft()));
            Require(legacy.RestoreRequestPublicContext() == null, "Empty legacy context was not recognized explicitly.");
            return true;
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }

    private static bool VerifyProviderStartupWaitDoesNotConsumeAcceptedTimeout()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.initialRetrySeconds = 1f;
        settings.maximumRetrySeconds = 2f;
        using ActorFixture actor = new ActorFixture(
            991,
            "기동 대기 검증자",
            "human");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            MutableUiClock clock = new MutableUiClock();
            ReadinessControlledLlmRuntime runtime =
                new ReadinessControlledLlmRuntime();
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new TestLlmRuntimeProvider(runtime),
                clock);
            CharacterProgression progression = actor.Actor.Progression;
            progression.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:provider-startup-wait",
                "facility:provider-startup-wait",
                CharacterActivityOutcomes.Completed,
                day: 1);
            CharacterSkillDraft draft = service.CreateDraft(
                progression,
                CharacterSkillKind.Passive,
                1);
            service.RequestDraft(progression, draft);

            service.Tick();
            Require(runtime.CharacterSkillCallCount == 0
                    && draft.presentationFailureCount == 0
                    && service.PendingRequestCount == 1
                    && service.ProviderCircuitTripCount == 0
                    && service.LastDiagnostic.Contains(
                        "provider-starting",
                        StringComparison.Ordinal),
                "Bundled-host startup was treated as an accepted or failed request.");

            clock.Advance(service.RequestTimeoutSeconds * 3f);
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 0
                    && draft.presentationFailureCount == 0
                    && service.PendingRequestCount == 1
                    && service.ProviderCircuitTripCount == 0,
                "Waiting longer than the accepted-request timeout consumed a retry during host startup.");

            runtime.SetReady();
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 1
                    && draft.presentationFailureCount == 0
                    && service.PendingRequestCount == 1,
                "A ready provider did not dispatch the preserved request exactly once.");

            clock.Advance(service.RequestTimeoutSeconds + 0.1f);
            service.Tick();
            Require(draft.presentationFailureCount == 1
                    && service.ProviderCircuitTripCount == 1
                    && service.LastDiagnostic.Contains(
                        "accepted-request-timeout",
                        StringComparison.Ordinal),
                "The accepted-request timeout did not begin after real dispatch.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    public static bool VerifyInFlightGenerationTimeout()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.initialRetrySeconds = 1f;
        settings.maximumRetrySeconds = 2f;
        using ActorFixture actor = new ActorFixture(
            992,
            "시간 검증자",
            "human");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            MutableUiClock clock = new MutableUiClock();
            NeverCompletingLlmRuntime runtime = new NeverCompletingLlmRuntime();
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new TestLlmRuntimeProvider(runtime),
                clock);
            CharacterProgression progression = actor.Actor.Progression;
            progression.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:skill-timeout",
                "facility:skill-timeout",
                CharacterActivityOutcomes.Completed,
                day: 1);
            CharacterSkillDraft draft = service.CreateDraft(
                progression,
                CharacterSkillKind.Passive,
                1);
            service.RequestDraft(progression, draft);
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                service.Tick();
                Require(runtime.CharacterSkillCallCount == attempt,
                    "A timed-out formula presentation attempt was not submitted exactly once.");
                clock.Advance(service.RequestTimeoutSeconds + 0.1f);
                service.Tick();
                Require(draft.presentationFailureCount == attempt,
                    "A timed-out formula presentation did not increment its visible failure count.");
                if (attempt < 5)
                {
                    Require(service.PendingRequestCount == 1
                            && !draft.isReady
                            && draft.candidates.Count == 0,
                        "A timeout applied mechanics or discarded the retryable presentation early.");
                    clock.Advance(
                        service.ProviderCircuitCooldownRemainingSeconds + 0.1f);
                }
            }
            Require(service.PendingRequestCount == 0
                    && !draft.isReady
                    && draft.candidates.Count == 0
                    && draft.presentationState
                        == CharacterSkillPresentationState.AwaitingNarrativeRetry
                    && !draft.requestSubmitted,
                "Five timeouts did not persist an unapplied AwaitingNarrativeRetry draft.");
            Require(service.ProviderCircuitTripCount == 5
                    && service.LastDiagnostic.Contains(
                        "accepted-request-timeout",
                        StringComparison.Ordinal),
                "Timed-out generation did not preserve its explicit failure diagnostic.");

            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.Add(draft);
            CharacterProgressionSnapshot captured = progression.CapturePersistentState();
            CharacterProgressionSnapshot saved = new(
                captured.Level, captured.CurrentExperience,
                DungeonSaveSectionPayload.Deserialize<CharacterGrowthState>(
                    JsonUtility.ToJson(captured.GrowthState)),
                DungeonSaveSectionPayload.Deserialize<CharacterNarrativeLedger>(
                    JsonUtility.ToJson(captured.NarrativeLedger)),
                captured.AcquiredTraitState);
            string savedJson = JsonConvert.SerializeObject(saved);
            using ActorFixture restoredActor = new(
                993, "시간 검증자", "human",
                persistentId: actor.Actor.Identity.PersistentId, detachedRestore: true);
            CharacterProgression restored = restoredActor.Actor.Progression;
            NeverCompletingLlmRuntime restoredRuntime = new();
            CharacterSkillGenerationService restoredService = new(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new TestLlmRuntimeProvider(restoredRuntime), new MutableUiClock());
            try
            {
                restored.ConstructCharacterProgression(
                    restoredService, settingsProvider, gameEventBus: new GameEventBus(),
                    profileProjector: new CharacterProgressionProfileProjector(
                        new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                        new CharacterRuntimeProfileFactory(
                            new ResourceGameContentCatalog(new UnityGameContentRootLoader()))));
                restoredActor.Actor.Lifecycle.RestorePersistentState(
                    actor.Actor.CurrentLifecycleState, actor.Actor.Lifecycle.LifecycleRevision);
                restored.RestorePersistentState(saved);
                restoredActor.Actor.PublishDetachedRestore();
                restored.ResumePendingGenerationAfterWorldRestore();
                restored.ResumePendingGenerationAfterWorldRestore();
                restoredService.Tick();
                Require(savedJson == JsonConvert.SerializeObject(restored.CapturePersistentState())
                        && restoredService.PendingRequestCount == 0
                        && restoredRuntime.CharacterSkillCallCount == 0,
                    "World restore retried an exhausted skill request or changed its saved failure state.");

                CharacterSkillDraft retried = CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(restored).drafts.Single(
                    value => value.requestKey == draft.requestKey);
                restoredService.RequestDraft(restored, retried);
                Require(restoredService.PendingRequestCount == 1 && retried.requestSubmitted
                        && retried.presentationFailureCount == 0
                        && retried.presentationState == CharacterSkillPresentationState.PresentationPending
                        && retried.requestContextHash == draft.requestContextHash,
                    "An explicit retry did not retain the saved request identity/context and renew its failure budget.");
                restoredService.Tick();
                restoredService.RequestDraft(restored, retried);
                restoredService.Tick();
                Require(restoredRuntime.CharacterSkillCallCount == 1 && restoredService.PendingRequestCount == 1,
                    "Repeated retry duplicated the pending transport request.");
            }
            finally { restoredService.CancelRequests(restored); }
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyProviderCircuitDrainsQueuedRequests()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.initialRetrySeconds = 1f;
        settings.maximumRetrySeconds = 2f;
        List<ActorFixture> actors = new List<ActorFixture>();
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            MutableUiClock clock = new MutableUiClock();
            DelayedSkillLlmRuntime runtime = new DelayedSkillLlmRuntime();
            CharacterSkillGenerationService service =
                new CharacterSkillGenerationService(
                    NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                    new TestLlmRuntimeProvider(runtime),
                    clock);
            List<CharacterProgression> progressions =
                new List<CharacterProgression>();
            for (int index = 0; index < 3; index++)
            {
                ActorFixture actor = new ActorFixture(
                    12000 + index,
                    "Circuit verifier " + index,
                    "human");
                actors.Add(actor);
                CharacterProgression progression = actor.Actor.Progression;
                progression.ConstructCharacterProgression(
                    service,
                    settingsProvider,
                    gameEventBus: new GameEventBus(),
                    profileProjector: new CharacterProgressionProfileProjector(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()),
                        new CharacterRuntimeProfileFactory(
                            new ResourceGameContentCatalog(
                                new UnityGameContentRootLoader()))));
                progression.RecordNarrative(
                    CharacterNarrativeDomain.Work,
                    "work:provider-circuit-" + index,
                    "facility:provider-circuit",
                    CharacterActivityOutcomes.Completed,
                    day: 1);
                progression.ApplyPreparedIdentity(
                    "Circuit verifier " + index,
                    "deterministic provider-health test",
                    Array.Empty<int>(),
                    CharacterPotentialGrade.Ordinary,
                    12000 + index,
                    autoChooseDrafts: true);
                progressions.Add(progression);
            }

            int queuedCount = service.PendingRequestCount;
            Require(queuedCount > 2,
                "The provider circuit fixture did not exceed concurrent capacity.");
            service.Tick();
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 2
                    && runtime.PendingCallbackCount == 2
                    && service.PendingRequestCount == queuedCount,
                "The fixture did not retain queued work behind two accepted requests.");

            clock.Advance(service.RequestTimeoutSeconds + 0.1f);
            service.Tick();
            Require(service.PendingRequestCount == queuedCount,
                "A provider timeout discarded queued formula presentation requests.");
            Require(progressions.SelectMany(value => value.Drafts)
                    .All(draft => draft != null
                        && !draft.isReady
                        && draft.candidates.Count == 0),
                "The provider circuit applied hidden fallback mechanics.");
            Require(service.IsProviderCircuitOpen
                    && service.ProviderCircuitTripCount == 1
                    && service.ProviderCircuitCooldownRemainingSeconds > 0f,
                "The provider timeout did not open a visible bounded circuit.");
            Require(service.LastDiagnostic.Contains(
                    "accepted-request-timeout",
                    StringComparison.Ordinal),
                "The provider circuit diagnostic did not retain the timeout cause.");

            ActorFixture cooldownActor = new ActorFixture(
                12003,
                "Circuit cooldown verifier",
                "human");
            actors.Add(cooldownActor);
            CharacterProgression cooldownProgression =
                cooldownActor.Actor.Progression;
            cooldownProgression.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            cooldownProgression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:provider-circuit-cooldown",
                "facility:provider-circuit",
                CharacterActivityOutcomes.Completed,
                day: 1);
            cooldownProgression.ApplyPreparedIdentity(
                "Circuit cooldown verifier",
                "deterministic provider-health test",
                Array.Empty<int>(),
                CharacterPotentialGrade.Ordinary,
                12003,
                autoChooseDrafts: true);
            int cooldownQueued = service.PendingRequestCount;
            Require(cooldownQueued > queuedCount,
                "The cooldown fixture did not enqueue generation work.");
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 2
                    && service.PendingRequestCount == cooldownQueued
                    && cooldownProgression.Drafts.All(
                        draft => draft != null
                            && !draft.isReady
                            && draft.candidates.Count == 0),
                "An open provider circuit submitted work or applied a hidden fallback.");
            progressions.Add(cooldownProgression);

            runtime.CompleteAcceptedCallbacks(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                "{}",
                string.Empty,
                string.Empty));
            service.Tick();
            Require(runtime.DeliveredCallbackCount == 2
                    && service.PendingRequestCount == cooldownQueued
                    && progressions.SelectMany(value => value.Drafts)
                        .All(draft => draft != null
                            && !draft.isReady
                            && draft.candidates.Count == 0),
                "A late provider callback committed mechanics or discarded queued work.");
            return true;
        }
        finally
        {
            foreach (ActorFixture actor in actors)
            {
                actor.Dispose();
            }
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static CharacterSkillCandidateRule CreateManagementReachabilityRule(
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind,
        CharacterSkillTrigger trigger,
        CharacterSkillTarget target,
        CharacterUltimateDomain ultimateDomain,
        string requestKey,
        IEnumerable<string> restrictedModuleIds = null)
    {
        HashSet<string> restriction = restrictedModuleIds == null
            ? null
            : new HashSet<string>(restrictedModuleIds, StringComparer.Ordinal);
        List<CharacterSkillModuleRule> modules = settings.Modules
            .OfType<CharacterManagementSkillModuleRule>()
            .Where(value => restriction == null || restriction.Contains(value.id))
            .Where(value => !CharacterSkillValidation.WouldSelfTrigger(value.id, trigger))
            .Cast<CharacterSkillModuleRule>()
            .ToList();
        CharacterSkillCandidateRule rule = new CharacterSkillCandidateRule
        {
            rarity = CharacterSkillRarity.Legendary,
            budget = 99,
            trigger = trigger,
            target = target,
            ultimateDomain = ultimateDomain,
            cooldownTurns = 0,
            mechanicalPolicySource = CharacterSkillMechanicalPolicySource.AuthoredRule,
            allowedModuleIds = modules
                .Select(value => value.id)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            allowedVariantIds = modules
                .SelectMany(value => value.variants ?? new List<CharacterSkillNumericVariant>())
                .Where(value => value != null)
                .Select(value => value.id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList()
        };
        CharacterSkillDraft identityDraft = new CharacterSkillDraft
        {
            kind = kind,
            requestKey = requestKey,
            requestedUltimateDomain = ultimateDomain,
            rules = new List<CharacterSkillCandidateRule> { rule }
        };
        CharacterSkillRuleIdentity.Ensure(identityDraft);
        return rule;
    }

    private static bool VerifyCompletionOwningProviderControlsAcceptedLifetime()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        settings.initialRetrySeconds = 1f;
        settings.maximumRetrySeconds = 2f;
        using ActorFixture actor = new ActorFixture(
            993,
            "완료 소유권 검증자",
            "human");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            MutableUiClock clock = new MutableUiClock();
            CompletionOwningLlmRuntime runtime = new CompletionOwningLlmRuntime();
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new TestLlmRuntimeProvider(runtime),
                clock);
            CharacterProgression progression = actor.Actor.Progression;
            progression.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "work:completion-owner",
                "facility:completion-owner",
                CharacterActivityOutcomes.Completed,
                day: 1);
            CharacterSkillDraft draft = service.CreateDraft(
                progression,
                CharacterSkillKind.Passive,
                1);
            service.RequestDraft(progression, draft);
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 1
                    && runtime.HasPendingCallback
                    && draft.presentationFailureCount == 0,
                "The completion-owning runtime did not accept exactly one request.");

            clock.Advance(service.RequestTimeoutSeconds * 3f);
            service.Tick();
            Require(runtime.CharacterSkillCallCount == 1
                    && runtime.HasPendingCallback
                    && draft.presentationFailureCount == 0
                    && service.ProviderCircuitTripCount == 0
                    && service.PendingRequestCount == 1,
                "The service watchdog raced a completion-owning runtime.");

            runtime.Complete(new LocalLlmResult(
                LocalLlmRequestStatus.TimedOut,
                string.Empty,
                "owned transport timeout",
                string.Empty));
            Require(draft.presentationFailureCount == 1
                    && service.ProviderCircuitTripCount == 1
                    && !runtime.HasPendingCallback
                    && service.LastDiagnostic.Contains(
                        "accepted-request-timeout",
                        StringComparison.Ordinal),
                "A completion-owner timeout callback did not enter the visible fail-loud path.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static void VerifyManagementReachabilityRuntimePaths()
    {
        using ActorFixture fixture = new ActorFixture(
            81018,
            "ReachabilityRuntime",
            "Slime");
        CharacterActor actor = fixture.Actor;
        CharacterGrowthState growth = CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(actor.Progression);
        growth.passiveSkills.Clear();
        actor.Stats.Stats[CharacterCondition.HUNGER] = 0f;
        actor.Stats.Stats[CharacterCondition.SLEEP] = 80f;
        actor.Stats.Stats[CharacterCondition.FUN] = 80f;
        actor.Stats.Stats[CharacterCondition.EXCRETION] = 80f;
        actor.Stats.Stats[CharacterCondition.HYGIENE] = 50f;
        growth.passiveSkills.Add(new CharacterSkillInstance
        {
            id = "qa-reachability-work-started",
            combinationId = "qa-reachability-work-started-combination",
            displayName = "작업 시작 경로",
            description = "작업 시작 이벤트와 조회 경로 검증",
            kind = CharacterSkillKind.Passive,
            trigger = CharacterSkillTrigger.WorkStarted,
            target = CharacterSkillTarget.Self,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("cleaning", "small"),
                Module("needs", "small"),
                Module("mood", "small"),
                Module("work_speed", "small")
            }
        });
        float moodBefore = actor.Stats.Mood;
        CharacterSkillRuntimeEffects.BeginWork(
            actor,
            null,
            BuiltInWorkTypeIds.Operate,
            "qa-reachability-work-started-event");
        Require(Mathf.Approximately(
                actor.Stats.GetConditionValue(CharacterCondition.HYGIENE),
                55f)
            && Mathf.Approximately(
                actor.Stats.GetConditionValue(CharacterCondition.HUNGER),
                5f)
            && actor.Stats.Mood > moodBefore
            && Mathf.Approximately(
                CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(actor),
                1.1f),
            "WorkStarted did not reach cleaning/needs/mood event effects and work-speed query effect.");
        CharacterSkillRuntimeEffects.EndWork(actor);

        CharacterSkillTrigger[] immediateTriggers =
        {
            CharacterSkillTrigger.NeedChanged,
            CharacterSkillTrigger.MoodChanged,
            CharacterSkillTrigger.RelationshipChanged,
            CharacterSkillTrigger.OperatingDayStarted
        };
        foreach (CharacterSkillTrigger trigger in immediateTriggers)
        {
            growth.passiveSkills.Clear();
            growth.passiveSkills.Add(new CharacterSkillInstance
            {
                id = "qa-reachability-event-" + trigger,
                combinationId = "qa-reachability-event-combination-" + trigger,
                displayName = "관리 이벤트 경로",
                description = "관리 이벤트 즉시 효과 검증",
                kind = CharacterSkillKind.Passive,
                trigger = trigger,
                target = CharacterSkillTarget.Self,
                modules = new List<CharacterSkillModuleSelection>
                {
                    Module("cleaning", "small")
                }
            });
            actor.Stats.Stats[CharacterCondition.HYGIENE] = 10f;
            CharacterSkillRuntimeEffects.ApplyTriggeredPassives(actor, trigger);
            Require(Mathf.Approximately(
                    actor.Stats.GetConditionValue(CharacterCondition.HYGIENE),
                    15f),
                $"{trigger} did not reach the retained cleaning event effect.");
        }

        growth.passiveSkills.Clear();
        growth.passiveSkills.Add(new CharacterSkillInstance
        {
            id = "qa-unreachable-cross-trigger",
            combinationId = "qa-unreachable-cross-trigger-combination",
            displayName = "교차 발동 차단",
            description = "다른 발동의 조회 효과 차단 검증",
            kind = CharacterSkillKind.Passive,
            trigger = CharacterSkillTrigger.WorkStarted,
            target = CharacterSkillTarget.Self,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("output", "small"),
                Module("stock", "small")
            }
        });
        CharacterSkillRuntimeEffects.BeginWork(
            actor,
            null,
            BuiltInWorkTypeIds.Operate,
            "qa-unreachable-cross-trigger-event");
        Require(Mathf.Approximately(
                CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(actor),
                1f)
            && CharacterSkillRuntimeEffects.GetStockProductionBonus(actor) == 0,
            "WorkStarted leaked WorkCompleted-only output or stock query effects.");
        CharacterSkillRuntimeEffects.EndWork(actor);
    }

    private static void VerifyInstalledLegacyManagementPassivePreservation()
    {
        const string LegacyCombinationId =
            "skill-combination:qa-legacy-work-started-output";
        using ActorFixture source = new ActorFixture(
            81019,
            "LegacySkillSource",
            "Slime");
        CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(source.Actor.Progression).passiveSkills.Clear();
        CharacterSkillInstance original = new CharacterSkillInstance
        {
            id = "qa-legacy-work-started-output",
            ruleId = "skill-rule:qa-legacy-work-started-output",
            combinationId = LegacyCombinationId,
            displayName = "이전 작업 시작 기술",
            description = "이전 저장에서 유지되는 기술",
            narrativeReason = "이전 승인 결과를 그대로 보존한다.",
            kind = CharacterSkillKind.Passive,
            rarity = CharacterSkillRarity.Advanced,
            trigger = CharacterSkillTrigger.WorkStarted,
            target = CharacterSkillTarget.Self,
            ultimateDomain = CharacterUltimateDomain.None,
            cooldownTurns = 3,
            usableFrom = OffenseFormationMask.Front | OffenseFormationMask.Middle,
            targetPositions = OffenseFormationMask.Rear,
            modules = new List<CharacterSkillModuleSelection>
            {
                Module("output", "small")
            },
            requestKey = "qa:legacy:work-started-output",
            narrativeTrace = new NarrativeGenerationTrace
            {
                schemaId = "qa:legacy:skill",
                schemaVersion = 17,
                schemaHash = "sha256:qa-legacy-skill",
                cultureStyleId = "qa:legacy:culture",
                usedMotifIds = new[] { "motif:legacy:a", "motif:legacy:b" },
                usedCharacterFactIds = new[] { "fact:legacy:a" },
                verdict = NarrativeQualityVerdict.SoftPass,
                retryCount = 2,
                usedFallback = false
            }
        };
        CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(source.Actor.Progression).passiveSkills.Add(original);
        string exactBefore = JsonUtility.ToJson(original);
        CharacterProgressionSnapshot snapshot =
            source.Actor.Progression.CapturePersistentState();

        using ActorFixture restored = new ActorFixture(
            81020,
            "LegacySkillRestored",
            "Slime");
        restored.Actor.Progression.RestorePersistentState(snapshot);
        CharacterSkillInstance legacy = restored.Actor.Progression.PassiveSkills.Single();
        string exactAfterRestore = JsonUtility.ToJson(legacy);
        CharacterProgressionSnapshot recaptured =
            restored.Actor.Progression.CapturePersistentState();
        string exactAfterRecapture = JsonUtility.ToJson(
            recaptured.GrowthState.passiveSkills.Single());
        Require(string.Equals(exactAfterRestore, exactBefore, StringComparison.Ordinal)
                && string.Equals(exactAfterRecapture, exactBefore, StringComparison.Ordinal),
            "An installed legacy management passive was deleted, replaced, or rewritten on restore.");
        Require(Mathf.Approximately(
                CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(restored.Actor),
                1f),
            "An installed legacy WorkStarted/output skill gained a new invented effect.");
    }

    [Serializable]
    private sealed class SkillPresentationFixtureDto
    {
        public string presentationId = string.Empty;
        public string displayName = string.Empty;
        public string narrativeFlavor = string.Empty;
    }

    private static string BuildValidFormulaPresentationJson(
        CharacterSkillDraft draft,
        string characterName)
    {
        Require(draft != null
                && draft.frozenMechanics != null
                && draft.nextPresentationIndex >= 0
                && draft.nextPresentationIndex < draft.frozenMechanics.Count,
            "The formula presentation fixture has no pending frozen mechanic.");
        CharacterSkillInstance frozen =
            draft.frozenMechanics[draft.nextPresentationIndex];
        return JsonUtility.ToJson(new SkillPresentationFixtureDto
        {
            presentationId = frozen.presentationId,
            displayName = characterName + " 감각",
            narrativeFlavor = characterName + "의 반복된 경험에서 익힌 감각이다."
        });
    }

    internal static string BuildValidModuleSelectionJson(
        CharacterSkillDraft draft,
        string characterName) =>
        JsonUtility.ToJson(BuildValidModuleSelectionDto(draft, characterName));

    private static string BuildValidModuleSelectionBatchJson(
        CharacterSkillGenerationService service,
        CharacterSkillDraft draft,
        string characterName)
    {
        Require(service != null && draft != null,
            "The module-selection batch fixture is incomplete.");
        CharacterSkillDraft scratch = draft.Clone();
        CharacterSkillModuleSelectionBatchResponseDto payload = new();
        while (scratch.nextPresentationIndex < scratch.moduleSelectionOffers.Count)
        {
            CharacterSkillModuleSelectionResponseDto candidate =
                BuildValidModuleSelectionDto(scratch, characterName);
            candidate.displayName = characterName + (scratch.nextPresentationIndex switch
            {
                0 => " 감각",
                1 => " 집중",
                2 => " 통찰",
                _ => throw new InvalidOperationException("The batch fixture exceeds three candidates.")
            });
            Require(service.TryValidateResponse(
                    scratch,
                    JsonUtility.ToJson(candidate),
                    out List<CharacterSkillInstance> resolved,
                    out string error)
                    && resolved.Count == 1,
                "The fixture could not resolve a legal batch candidate: " + error);
            payload.candidates.Add(candidate);
            scratch.candidates.Add(resolved[0].Clone());
            scratch.frozenMechanics.Add(resolved[0].Clone());
            scratch.nextPresentationIndex++;
        }
        return JsonUtility.ToJson(payload);
    }

    private static CharacterSkillModuleSelectionResponseDto BuildValidModuleSelectionDto(
        CharacterSkillDraft draft,
        string characterName)
    {
        Require(draft != null
                && draft.moduleSelectionOffers != null
                && draft.nextPresentationIndex >= 0
                && draft.nextPresentationIndex < draft.moduleSelectionOffers.Count,
            "The formula fixture has no pending module-selection offer.");
        CharacterSkillModuleOfferState offer =
            draft.moduleSelectionOffers[draft.nextPresentationIndex];
        HashSet<string> usedPrimaryModules = (draft.candidates
                ?? new List<CharacterSkillInstance>())
            .Where(candidate => candidate != null)
            .Select(candidate => candidate.combinationId)
            .Where(moduleId => !string.IsNullOrWhiteSpace(moduleId))
            .ToHashSet(StringComparer.Ordinal);
        string positiveModuleId = offer.positiveModuleIds
            .FirstOrDefault(moduleId => !usedPrimaryModules.Contains(moduleId));
        Require(!string.IsNullOrWhiteSpace(positiveModuleId),
            "The formula fixture has no unused positive module for the active draft.");
        return new CharacterSkillModuleSelectionResponseDto
        {
            selectionId = offer.selectionId,
            positiveModuleIds = new List<string> { positiveModuleId },
            drawbackModuleIds = new List<string>(),
            evidenceFactIds = new List<string> { offer.evidenceFactIds[0] },
            displayName = characterName + " 감각",
            narrativeFlavor = characterName + "의 반복된 경험에서 익힌 감각이다."
        };
    }

    private static CharacterSkillGenerationResponseDto BuildValidResponse(
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        string characterName = "검증자")
    {
        CharacterSkillRuleIdentity.Ensure(draft);
        CharacterSkillGenerationResponseDto response = new CharacterSkillGenerationResponseDto();
        HashSet<string> usedMechanicalIdentities = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < draft.rules.Count; i++)
        {
            CharacterSkillCandidateRule rule = draft.rules[i];
            List<CharacterSkillAllowedCombination> combinations = CharacterSkillCombinationCatalog
                .Build(rule, settings, draft.kind);
            CharacterSkillAllowedCombination selected = combinations
                .FirstOrDefault(candidate => usedMechanicalIdentities.Add(candidate.MechanicalIdentity))
                ?? combinations.First();
            response.candidates.Add(new CharacterSkillCandidateResponseDto
            {
                ruleId = rule.ruleId,
                combinationId = selected.Id,
                displayName = $"{characterName} 기술 {i + 1}",
                description = "검증된 효과를 적용한다.",
                narrativeReason = "반복된 경험에서 익혔다."
            });
        }

        return response;
    }

    private static bool ChooseActive(CharacterProgression progression, int level, int index)
    {
        CharacterSkillDraft draft = progression.Drafts.FirstOrDefault(candidate => candidate != null
            && candidate.kind == CharacterSkillKind.Active
            && candidate.unlockLevel == level
            && candidate.isReady);
        return draft != null
            && progression.TryChooseActiveSkill(level, Mathf.Clamp(index, 0, draft.candidates.Count - 1), true, out _);
    }

    private static int GetExperienceToReach(int targetLevel)
    {
        return GetExperienceBetween(1, targetLevel);
    }

    private static int GetExperienceBetween(int currentLevel, int targetLevel)
    {
        int total = 0;
        for (int level = Mathf.Max(1, currentLevel); level < Mathf.Min(CharacterProgression.MaxLevel, targetLevel); level++)
        {
            total += CharacterProgression.GetExperienceRequired(level);
        }

        return total;
    }

    private static void Run(string name, Func<bool> scenario, ICollection<string> errors)
    {
        try
        {
            if (!scenario())
            {
                errors.Add($"{name}: returned false");
            }
        }
        catch (Exception exception)
        {
            errors.Add($"{name}: {exception.Message}\n{exception.StackTrace}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string DescribeDraftState(CharacterProgression progression)
    {
        return progression == null
            ? "progression=null"
            : $"initialized={progression.GrowthState.initialized}; "
                + $"level={progression.Level}; active={progression.ActiveSkills.Count}; "
                + $"passive={progression.PassiveSkills.Count}; drafts="
                + string.Join(",", progression.Drafts.Select(draft => draft == null
                    ? "null"
                    : $"{draft.kind}@{draft.unlockLevel}:ready={draft.isReady}:candidates={draft.candidates?.Count ?? 0}:chosen={draft.permanentlyChosen}"));
    }

    private sealed class AcquiredTraitDefinitionSource :
        IGameContentDefinitionSource
    {
        private readonly CharacterAcquiredTraitSettingsSO settings;
        private readonly CharacterAcquiredTraitModuleSO[] modules;

        public AcquiredTraitDefinitionSource(
            CharacterAcquiredTraitSettingsSO settings,
            IEnumerable<CharacterAcquiredTraitModuleSO> modules)
        {
            this.settings = settings
                ?? throw new ArgumentNullException(nameof(settings));
            this.modules = (modules
                    ?? throw new ArgumentNullException(nameof(modules)))
                .Where(value => value != null)
                .ToArray();
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
        {
            if (typeof(T) == typeof(CharacterAcquiredTraitSettingsSO))
            {
                return new[] { settings }.Cast<T>().ToArray();
            }
            if (typeof(T) == typeof(CharacterAcquiredTraitModuleSO))
            {
                return modules.Cast<T>().ToArray();
            }
            return Array.Empty<T>();
        }

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = GetAll<T>();
            if (values.Count != 1)
            {
                throw new InvalidOperationException(
                    $"QA content expected one {typeof(T).Name}, found {values.Count}.");
            }
            return values[0];
        }
    }

    private sealed class FaultingTraitInferencePublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;

        internal FaultingTraitInferencePublisher(
            DomainFactDispatcher inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal bool RejectNextPublish { get; set; }
        internal bool ThrowNextPublishBeforeCommit { get; set; }
        internal bool HideNextCommittedAcknowledgement { get; set; }
        internal int CommittedPublishCalls { get; private set; }
        internal int ReplayBatchCalls { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) => inner.TryReserve(
                proposed,
                out lease,
                out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (ThrowNextPublishBeforeCommit)
            {
                ThrowNextPublishBeforeCommit = false;
                throw new InvalidOperationException(
                    "editor-injected-trait-precommit-exception");
            }
            if (RejectNextPublish)
            {
                RejectNextPublish = false;
                return DomainFactDelivery.Reject(
                    "editor-injected-trait-publication-rejection");
            }
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            CommittedPublishCalls++;
            if (HideNextCommittedAcknowledgement)
            {
                HideNextCommittedAcknowledgement = false;
                return DomainFactDelivery.Uncertain(
                    "editor-injected-trait-ack-loss-after-real-commit");
            }
            return delivered;
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(
                facts,
                results,
                out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            ReplayBatchCalls++;
            return inner.ReplayBatch(facts, results, out failure);
        }
    }

    private sealed class FaultingTraitReactionPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;

        internal FaultingTraitReactionPublisher(
            DomainFactDispatcher inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal bool RejectNextPublish { get; set; }
        internal bool ThrowNextPublishBeforeCommit { get; set; }
        internal bool HideNextCommittedAcknowledgement { get; set; }
        internal int CommittedPublishCalls { get; private set; }
        internal int ReplayBatchCalls { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) => inner.TryReserve(
                proposed,
                out lease,
                out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (ThrowNextPublishBeforeCommit)
            {
                ThrowNextPublishBeforeCommit = false;
                throw new InvalidOperationException(
                    "editor-injected-trait-reaction-precommit-exception");
            }
            if (RejectNextPublish)
            {
                RejectNextPublish = false;
                return DomainFactDelivery.Reject(
                    "editor-injected-trait-reaction-publication-rejection");
            }
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            CommittedPublishCalls++;
            if (HideNextCommittedAcknowledgement)
            {
                HideNextCommittedAcknowledgement = false;
                return DomainFactDelivery.Uncertain(
                    "editor-injected-trait-reaction-ack-loss-after-real-commit");
            }
            return delivered;
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(
                facts,
                results,
                out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            ReplayBatchCalls++;
            return inner.ReplayBatch(facts, results, out failure);
        }
    }

    private sealed class FaultingSkillAcquisitionPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;

        internal FaultingSkillAcquisitionPublisher(
            DomainFactDispatcher inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal bool RejectNextPublish { get; set; }
        internal bool ThrowNextPublishBeforeCommit { get; set; }
        internal bool HideNextCommittedAcknowledgement { get; set; }
        internal int CommittedPublishCalls { get; private set; }
        internal int ReplayBatchCalls { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) => inner.TryReserve(
                proposed,
                out lease,
                out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (ThrowNextPublishBeforeCommit)
            {
                ThrowNextPublishBeforeCommit = false;
                throw new InvalidOperationException(
                    "editor-injected-skill-precommit-exception");
            }
            if (RejectNextPublish)
            {
                RejectNextPublish = false;
                return DomainFactDelivery.Reject(
                    "editor-injected-skill-publication-rejection");
            }
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            CommittedPublishCalls++;
            if (HideNextCommittedAcknowledgement)
            {
                HideNextCommittedAcknowledgement = false;
                return DomainFactDelivery.Uncertain(
                    "editor-injected-skill-ack-loss-after-real-commit");
            }
            return delivered;
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(
                facts,
                results,
                out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            ReplayBatchCalls++;
            return inner.ReplayBatch(facts, results, out failure);
        }
    }

    private sealed class UnexpectedEvidenceUseTransaction :
        IGameplayOutcomeEvidenceUseTransaction
    {
        public bool TryPrepare(
            string anchorTypeId,
            string anchorId,
            IReadOnlyList<GameplayOutcomeNarrativeEvidenceSelection> selections,
            out PreparedGameplayOutcomeEvidenceUse prepared,
            out string failureCode)
        {
            prepared = null;
            failureCode = "unexpected-legacy-evidence-transaction";
            return false;
        }

        public bool TryPrepareBindings(
            string anchorTypeId,
            string anchorId,
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings,
            out PreparedGameplayOutcomeEvidenceUse prepared,
            out string failureCode)
        {
            prepared = null;
            failureCode = "unexpected-exact-evidence-transaction";
            return false;
        }
    }

    private static bool VerifyManagementPassiveReachability()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        try
        {
            (CharacterSkillTrigger Trigger, string[] ExpectedModules)[] cases =
            {
                (CharacterSkillTrigger.WorkStarted,
                    new[] { "cleaning", "mood", "needs", "work_speed" }),
                (CharacterSkillTrigger.WorkCompleted,
                    new[]
                    {
                        "cleaning", "mood", "needs", "output", "repair", "research",
                        "revenue", "stock", "work_speed"
                    }),
                (CharacterSkillTrigger.NeedChanged,
                    new[] { "cleaning", "mood" }),
                (CharacterSkillTrigger.MoodChanged,
                    new[] { "cleaning", "needs" }),
                (CharacterSkillTrigger.RelationshipChanged,
                    new[] { "cleaning", "mood", "needs" }),
                (CharacterSkillTrigger.OperatingDayStarted,
                    new[] { "cleaning", "mood", "needs" })
            };

            foreach ((CharacterSkillTrigger trigger, string[] expectedModules) in cases)
            {
                CharacterSkillCandidateRule rule = CreateManagementReachabilityRule(
                    settings,
                    CharacterSkillKind.Passive,
                    trigger,
                    CharacterSkillTarget.Self,
                    CharacterUltimateDomain.None,
                    $"qa:management-reachability:{trigger}");
                List<CharacterSkillAllowedCombination> combinations =
                    CharacterSkillCombinationCatalog.Build(
                        rule,
                        settings,
                        CharacterSkillKind.Passive,
                        maximumCount: 4096);
                string[] actualModules = combinations
                    .SelectMany(value => value.Modules)
                    .Select(value => value.moduleId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                string[] expected = expectedModules
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                Require(actualModules.SequenceEqual(expected, StringComparer.Ordinal),
                    $"Management passive reachability mismatch for {trigger}: "
                    + $"expected={string.Join(",", expected)}; "
                    + $"actual={string.Join(",", actualModules)}");
                Require(combinations.Count > 0,
                    $"Management passive reachability removed every {trigger} combination.");

                foreach (string expectedModule in expected)
                {
                    CharacterSkillCandidateRule moduleRule =
                        CreateManagementReachabilityRule(
                            settings,
                            CharacterSkillKind.Passive,
                            trigger,
                            CharacterSkillTarget.Self,
                            CharacterUltimateDomain.None,
                            $"qa:management-reachability:{trigger}:{expectedModule}",
                            new[] { expectedModule });
                    CharacterSkillAllowedCombination combination =
                        CharacterSkillCombinationCatalog.Build(
                                moduleRule,
                                settings,
                                CharacterSkillKind.Passive)
                            .FirstOrDefault();
                    Require(combination != null,
                        $"Reachable {trigger}/{expectedModule} produced no legal combination.");
                    CharacterSkillCombinationSemanticsDto semantics =
                        CharacterSkillCombinationSemanticsFactory.Create(
                            combination,
                            moduleRule,
                            CharacterSkillKind.Passive,
                            settings);
                    foreach (IGrouping<string, CharacterSkillModuleSemanticsDto> module in
                             semantics.modules.GroupBy(
                                 value => value.moduleId + "|" + value.variantId,
                                 StringComparer.Ordinal))
                    {
                        Require(module.Any(value => !string.Equals(
                                value.effectKind,
                                "no_op",
                                StringComparison.Ordinal)),
                            $"Legal {trigger} combination '{combination.Id}' contains "
                            + $"permanently inactive module '{module.Key}'.");
                    }
                }
            }

            CharacterSkillCandidateRule ultimateRule = CreateManagementReachabilityRule(
                settings,
                CharacterSkillKind.Ultimate,
                CharacterSkillTrigger.OperatingDayStarted,
                CharacterSkillTarget.Dungeon,
                CharacterUltimateDomain.Management,
                "qa:management-reachability:ultimate");
            string[] ultimateModules = CharacterSkillCombinationCatalog.Build(
                    ultimateRule,
                    settings,
                    CharacterSkillKind.Ultimate,
                    maximumCount: 4096)
                .SelectMany(value => value.Modules)
                .Select(value => value.moduleId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] allManagementModules = settings.Modules
                .OfType<CharacterManagementSkillModuleRule>()
                .Select(value => value.id)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Require(ultimateModules.SequenceEqual(allManagementModules, StringComparer.Ordinal),
                "The reachability correction changed management-ultimate module coverage.");

            CharacterSkillCandidateRule validRule = CreateManagementReachabilityRule(
                settings,
                CharacterSkillKind.Passive,
                CharacterSkillTrigger.WorkCompleted,
                CharacterSkillTarget.Self,
                CharacterUltimateDomain.None,
                "qa:management-reachability:valid-id");
            CharacterSkillDraft validDraft = new CharacterSkillDraft
            {
                kind = CharacterSkillKind.Passive,
                requestKey = "qa:management-reachability:valid-id",
                rules = new List<CharacterSkillCandidateRule> { validRule }
            };
            CharacterSkillRuleIdentity.Ensure(validDraft);
            CharacterSkillAllowedCombination selected = CharacterSkillCombinationCatalog.Build(
                    validRule,
                    settings,
                    validDraft.kind)
                .First();
            CharacterSkillGenerationResponseDto validResponse = new CharacterSkillGenerationResponseDto
            {
                candidates = new List<CharacterSkillCandidateResponseDto>
                {
                    new CharacterSkillCandidateResponseDto
                    {
                        ruleId = validRule.ruleId,
                        combinationId = selected.Id,
                        displayName = "검증자의 운영 감각",
                        description = "검증된 관리 효과를 적용한다.",
                        narrativeReason = "검증자의 반복된 작업 기록에서 익혔다."
                    }
                }
            };
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), new TestSettingsProvider(settings),
                new MissingLlmRuntimeProvider(),
                CharacterAiEditorTestDependencies.UiClock);
            Require(!service.TryValidateResponse(
                    validDraft,
                    JsonUtility.ToJson(validResponse),
                    out _,
                    out string validError)
                && validError.Contains("legacy candidate selection", StringComparison.Ordinal),
                "A legacy LLM-selected combination was accepted as a new formula skill: "
                    + validError);

            CharacterSkillCandidateRule starvedRule = CreateManagementReachabilityRule(
                settings,
                CharacterSkillKind.Passive,
                CharacterSkillTrigger.NeedChanged,
                CharacterSkillTarget.Self,
                CharacterUltimateDomain.None,
                "qa:management-reachability:starved",
                new[] { "stock" });
            CharacterSkillDraft starvedDraft = new CharacterSkillDraft
            {
                kind = CharacterSkillKind.Passive,
                requestKey = "qa:management-reachability:starved",
                rules = new List<CharacterSkillCandidateRule> { starvedRule }
            };
            CharacterSkillRuleIdentity.Ensure(starvedDraft);
            Require(CharacterSkillCombinationCatalog.Build(
                    starvedRule,
                    settings,
                    starvedDraft.kind).Count == 0,
                "A permanently inactive NeedChanged/stock combination remained legal.");

            SequencedLlmRuntime starvationRuntime = new SequencedLlmRuntime();
            CharacterSkillGenerationService starvationService =
                new CharacterSkillGenerationService(
                    NarrativeRequestContextTestFixture.CreateEmpty(), new TestSettingsProvider(settings),
                    new TestLlmRuntimeProvider(starvationRuntime),
                    CharacterAiEditorTestDependencies.UiClock);
            using (ActorFixture starvationActor = new ActorFixture(
                       81017,
                       "StarvedRequest",
                       "Slime"))
            {
                InvalidOperationException starvation = null;
                try
                {
                    starvationService.RequestDraft(
                        starvationActor.Actor.Progression,
                        starvedDraft);
                }
                catch (InvalidOperationException exception)
                {
                    starvation = exception;
                }
                starvationService.Tick();
                Require(starvation != null
                        && starvation.Message.Contains(
                            "legacy variants are load-only",
                            StringComparison.Ordinal)
                        && starvationRuntime.CharacterSkillCallCount == 0
                        && starvationService.PendingRequestCount == 0,
                    "A legacy zero-combination draft was submitted instead of failing closed.");
            }

            string staleMechanicalIdentity = string.Join("|",
                starvedDraft.kind,
                starvedRule.rarity,
                starvedRule.trigger,
                starvedRule.target,
                starvedRule.ultimateDomain,
                starvedRule.cooldownTurns,
                "stock|small");
            string staleCombinationId = "skill-combination:"
                + NarrativeInferenceHash.ComputeSha256Utf8(
                    starvedRule.ruleId + "|" + staleMechanicalIdentity);
            CharacterSkillGenerationResponseDto staleResponse =
                new CharacterSkillGenerationResponseDto
                {
                    candidates = new List<CharacterSkillCandidateResponseDto>
                    {
                        new CharacterSkillCandidateResponseDto
                        {
                            ruleId = starvedRule.ruleId,
                            combinationId = staleCombinationId,
                            displayName = "검증자의 낡은 선택",
                            description = "이전 후보 식별자를 그대로 반환한다.",
                            narrativeReason = "검증자의 오래된 요청 결과다."
                        }
                    }
                };
            Require(!service.TryValidateResponse(
                    starvedDraft,
                    JsonUtility.ToJson(staleResponse),
                    out _,
                    out string staleError)
                && staleError.Contains("legacy candidate selection", StringComparison.Ordinal),
                "A stale formerly-enumerable combination ID bypassed the formula-only gate.");

            VerifyManagementReachabilityRuntimePaths();
            VerifyInstalledLegacyManagementPassivePreservation();
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    public static bool VerifyWorldRestoreGenerationContract(out string detail)
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        using ActorFixture fixture = new ActorFixture(
            91773, "저장 요청 재개 검증자", "human",
            unpublishedComposition: true);
        CharacterSkillGenerationService service = null;
        try
        {
            CharacterActor actor = fixture.Actor;
            CharacterProgression progression = actor.Progression;
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.Clear();
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).activeSkills.Clear();
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).passiveSkills.Clear();
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).pendingRequestKeys.Clear();
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).ultimate = null;
            CharacterAiEditorTestDependencies.GetOwnedNarrativeForFixture(progression).facts.Clear();
            TestSettingsProvider provider = new TestSettingsProvider(settings);
            service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), provider, new MissingLlmRuntimeProvider(),
                CharacterAiEditorTestDependencies.UiClock);
            progression.ConstructCharacterProgression(
                service, provider, gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(new UnityGameContentRootLoader()))));
            bool unpublishedRejected = false;
            try { progression.ResumePendingGenerationAfterWorldRestore(); }
            catch (InvalidOperationException) { unpublishedRejected = true; }
            Require(unpublishedRejected,
                "World generation resume accepted an unpublished actor.");
            actor.PublishComposition();
            string emptyState = JsonConvert.SerializeObject(
                progression.CapturePersistentState());
            progression.ResumePendingGenerationAfterWorldRestore();
            progression.ResumePendingGenerationAfterWorldRestore();
            Require(emptyState == JsonConvert.SerializeObject(
                    progression.CapturePersistentState())
                    && service.PendingRequestCount == 0,
                "World resume changed empty progression or fabricated a request.");

            bool emptyGenerationRejected = false;
            try { service.CreateDraft(progression, CharacterSkillKind.Active, 1); }
            catch (InvalidOperationException exception)
            {
                emptyGenerationRejected = exception.Message.Contains(
                    "at least one meaningful narrative evidence fact",
                    StringComparison.Ordinal);
            }
            Require(emptyGenerationRejected && service.PendingRequestCount == 0,
                "Explicit empty-evidence generation no longer fails closed.");

            progression.RecordNarrative(
                CharacterNarrativeDomain.Work, "qa:restore:completed-work",
                actor.Identity.PersistentId, "success", 1f, 1,
                triggerPassives: false);
            Require(progression.Drafts.Count > 0 && service.PendingRequestCount > 0,
                "A real narrative transition did not create skill requests.");
            string[] savedKeys = progression.Drafts.Select(draft => draft.requestKey).ToArray();
            service.CancelRequests(progression);
            Require(service.PendingRequestCount == 0,
                "The saved-request fixture did not clear transient requests.");
            progression.ResumePendingGenerationAfterWorldRestore();
            int resumedCount = service.PendingRequestCount;
            progression.ResumePendingGenerationAfterWorldRestore();
            Require(savedKeys.SequenceEqual(progression.Drafts.Select(draft => draft.requestKey))
                    && resumedCount > 0 && service.PendingRequestCount == resumedCount,
                "World resume lost saved requests, changed their identities or duplicated them.");
            detail = "emptyStateExact=true;emptyEvidenceRejected=true;narrativeUnlock=true;savedRequestsResumed="
                + resumedCount + ";duplicateRequests=0;unpublishedRejected=true";
            return true;
        }
        catch (Exception exception)
        {
            detail = exception.ToString();
            return false;
        }
        finally
        {
            service?.CancelRequests(fixture.Actor.Progression);
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyUnpublishedCompositionDefersSkillGeneration()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        using ActorFixture fixture = new ActorFixture(
            91772,
            "준비 중인 원장 검증자",
            "human",
            unpublishedComposition: true);
        try
        {
            CharacterActor actor = fixture.Actor;
            CharacterProgression progression = actor.Progression;
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).drafts.Clear();
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).activeSkills.Clear();
            CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(progression).passiveSkills.Clear();
            CharacterAiEditorTestDependencies.GetOwnedNarrativeForFixture(progression).facts.Clear();
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new MissingLlmRuntimeProvider(),
                CharacterAiEditorTestDependencies.UiClock);
            progression.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(new UnityGameContentRootLoader()))));

            actor.Initialize(actor.Identity.Data);
            Require(progression.Drafts.Count == 0
                    && service.PendingRequestCount == 0
                    && progression.NarrativeLedger.facts.Count == 0,
                "Unpublished character composition generated skills or fabricated evidence before restore.");

            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                "qa:composition:completed-work",
                actor.Identity.PersistentId,
                "success",
                1f,
                1,
                triggerPassives: false);
            Require(progression.Drafts.Count > 0
                    && progression.NarrativeLedger.facts.Count == 1,
                "Authoritative narrative installation did not resume skill draft generation.");
            service.CancelRequests(progression);
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static bool VerifyEmptyLedgerSkillPublicContextFallback()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        using ActorFixture actor = new ActorFixture(
            91771,
            "빈 원장 검증자",
            "human");
        try
        {
            TestSettingsProvider settingsProvider = new TestSettingsProvider(settings);
            SequencedLlmRuntime runtime = new SequencedLlmRuntime();
            CharacterSkillGenerationService service = new CharacterSkillGenerationService(
                NarrativeRequestContextTestFixture.CreateEmpty(), settingsProvider,
                new TestLlmRuntimeProvider(runtime),
                CharacterAiEditorTestDependencies.UiClock);
            CharacterProgression progression = actor.Actor.Progression;
            progression.ConstructCharacterProgression(
                service,
                settingsProvider,
                gameEventBus: new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(new UnityGameContentRootLoader()))));
            Require(progression.NarrativeLedger.facts.Count == 0,
                "Empty-ledger fail-closed fixture unexpectedly contains narrative facts.");

            InvalidOperationException failure = null;
            try
            {
                CharacterSkillDraft draft = service.CreateDraft(
                    progression,
                    CharacterSkillKind.Active,
                    1);
                service.RequestDraft(progression, draft);
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }

            Require(failure != null
                    && failure.Message.Contains(
                        "at least one meaningful narrative evidence fact",
                        StringComparison.Ordinal)
                    && runtime.CharacterSkillCallCount == 0
                    && service.PendingRequestCount == 0
                    && progression.ActiveSkills.Count == 0,
                "CharacterSkill did not fail clearly before inventing or submitting mechanics without narrative evidence.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private sealed class FixedGameCalendar : IGameCalendar
    {
        private int day;
        private int hour;

        public FixedGameCalendar(int day, int hour)
        {
            SetDateTime(day, hour);
        }

        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => hour / 24f * GameCalendarRules.SecondsPerDay;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int nextDay, int nextHour)
        {
            day = Math.Max(1, nextDay);
            hour = Math.Clamp(nextHour, 0, 23);
        }
    }

    private sealed class RealMemoryErasurePhysicalFixture
    {
        internal RealMemoryErasurePhysicalFixture(
            string characterId,
            IGameClock clock)
        {
            IDungeonItemCatalogProvider catalog =
                EditorItemCatalogFactory.Create();
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(catalog);
            ItemQuantityReservationService reservations = new(
                Repository,
                EditorNullItemMarkerPresenter.Instance,
                clock);
            Disposition = new PhysicalItemBatchDispositionService(
                Repository,
                mass,
                EditorNullItemMarkerPresenter.Instance,
                reservations, clock: new DungeonStory.Foundation.UnityUiClock());
            StackId = WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                MemoryErasureSealItemRules.ItemId,
                MemoryErasureSealItemRules.UseQuantity,
                WorldItemStackState.Carried,
                destinationId: "character-carry:" + characterId);
        }

        internal WorldItemRepository Repository { get; }
        internal PhysicalItemBatchDispositionService Disposition { get; }
        internal string StackId { get; }
    }

    private sealed class FaultingMemoryErasureFacts :
        IDomainFactStagingPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly IDomainFactStagingPublisher staging;
        private readonly IDomainFactRecoveryPublisher recovery;

        internal FaultingMemoryErasureFacts(
            IDomainFactStagingPublisher staging,
            IDomainFactRecoveryPublisher recovery)
        {
            this.staging = staging
                ?? throw new ArgumentNullException(nameof(staging));
            this.recovery = recovery
                ?? throw new ArgumentNullException(nameof(recovery));
        }

        internal bool RejectNextStage { get; set; }
        internal bool HideNextCommitAcknowledgement { get; set; }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failureReason)
        {
            if (fact is MemoryErasureTerminalFact && RejectNextStage)
            {
                RejectNextStage = false;
                stage = null;
                failureReason = "qa-memory-erasure-stage-rejected";
                return false;
            }
            if (!staging.TryStage(fact, out stage, out failureReason))
                return false;
            if (fact is MemoryErasureTerminalFact)
                stage = new FaultingStage(this, stage);
            return true;
        }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure) => recovery.ReplayBatch(
                facts,
                results,
                out failure);

        private sealed class FaultingStage : IDomainFactStage
        {
            private readonly FaultingMemoryErasureFacts owner;
            private readonly IDomainFactStage inner;

            internal FaultingStage(
                FaultingMemoryErasureFacts owner,
                IDomainFactStage inner)
            {
                this.owner = owner;
                this.inner = inner
                    ?? throw new ArgumentNullException(nameof(inner));
            }

            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;

            public DomainFactDelivery Commit()
            {
                DomainFactDelivery delivery = inner.Commit();
                if (!owner.HideNextCommitAcknowledgement
                    || !delivery.MayBeCommitted)
                {
                    return delivery;
                }
                owner.HideNextCommitAcknowledgement = false;
                return new DomainFactDelivery(
                    DomainFactDeliveryPhase.InDoubt,
                    delivery.OperationKey,
                    delivery.PublicationId,
                    delivery.Fingerprint,
                    "qa-memory-erasure-commit-acknowledgement-lost");
            }

            public void Cancel() => inner.Cancel();
        }
    }

    private sealed class FakeReversibleCarriedDisposition :
        IReversibleCarriedPhysicalItemBatchDispositionService
    {
        public bool AllowCommit { get; set; } = true;
        public bool AllowAcknowledge { get; set; } = true;
        public bool AllowRollback { get; set; } = true;
        public int BeginCount { get; private set; }
        public int AcknowledgeCount { get; private set; }
        public int RollbackCount { get; private set; }

        public bool TryCommitCarriedSinkReversible(
            string stackId,
            int quantity,
            string operationId,
            string reasonCode,
            out IReversiblePhysicalItemDisposition transaction,
            out string failureReason)
        {
            BeginCount++;
            bool exact = string.Equals(
                    stackId,
                    "carried:qa:memory-erasure",
                    StringComparison.Ordinal)
                && quantity == MemoryErasureSealItemRules.UseQuantity
                && !string.IsNullOrWhiteSpace(operationId)
                && string.Equals(
                    reasonCode,
                    "memory-erasure-seal-consume",
                    StringComparison.Ordinal);
            if (!AllowCommit || !exact)
            {
                transaction = null;
                failureReason = exact
                    ? "qa-physical-commit-rejected"
                    : "qa-physical-request-mismatch";
                return false;
            }

            transaction = new FakeReversibleDisposition(
                this,
                new PhysicalItemBatchDispositionReceipt(
                    PhysicalItemDispositionKind.Sink,
                    operationId,
                    reasonCode,
                    "qa-memory-erasure-disposition:" + stackId,
                    new[] { stackId },
                    quantity,
                    1L));
            failureReason = string.Empty;
            return true;
        }

        private sealed class FakeReversibleDisposition :
            IReversiblePhysicalItemDisposition
        {
            private readonly FakeReversibleCarriedDisposition owner;
            private readonly PhysicalItemBatchDispositionReceipt receipt;
            private bool terminal;

            public FakeReversibleDisposition(
                FakeReversibleCarriedDisposition owner,
                PhysicalItemBatchDispositionReceipt receipt)
            {
                this.owner = owner;
                this.receipt = receipt;
            }

            public PhysicalItemBatchDispositionReceipt Receipt => receipt;

            public bool TryRollback(out string failureReason)
            {
                owner.RollbackCount++;
                if (terminal || !owner.AllowRollback)
                {
                    failureReason = terminal
                        ? "qa-physical-already-terminal"
                        : "qa-physical-rollback-rejected";
                    return false;
                }
                terminal = true;
                failureReason = string.Empty;
                return true;
            }

            public bool TryAcknowledge(out string failureReason)
            {
                owner.AcknowledgeCount++;
                if (terminal || !owner.AllowAcknowledge)
                {
                    failureReason = terminal
                        ? "qa-physical-already-terminal"
                        : "qa-physical-acknowledge-rejected";
                    return false;
                }
                terminal = true;
                failureReason = string.Empty;
                return true;
            }
        }
    }

    private sealed class FakeRoomLayoutCache : IRoomLayoutCache
    {
        private readonly RoomLayout layout;

        public FakeRoomLayoutCache(params RoomInstance[] rooms)
        {
            layout = new RoomLayout(rooms ?? Array.Empty<RoomInstance>());
        }

        public RoomLayout GetLayout(Grid grid) => layout;

        public bool TryGetRoom(
            Grid grid,
            Vector2Int cell,
            out RoomInstance room) => layout.TryGetRoom(cell, out room);

        public bool TryGetRoom(
            BuildableObject part,
            out RoomInstance room) => layout.TryGetRoom(part, out room);

        public void Clear()
        {
        }
    }

    private sealed class ActorFixture : IDisposable
    {
        private readonly CharacterSO data;
        private readonly CharacterSkillSystemSettingsSO settings;

        public ActorFixture(
            int id,
            string displayName,
            string speciesTag,
            bool publishComposition = false,
            string persistentId = null,
            bool unpublishedComposition = false,
            bool detachedRestore = false,
            IGameEventBus progressionEvents = null)
        {
            settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                displayName,
                speciesTag);
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            GameObject actorObject = new GameObject($"ProgressionActor_{id}");
            actorObject.AddComponent<SpriteRenderer>();
            Actor = actorObject.AddComponent<CharacterActor>();
            actorObject.AddComponent<AbilityMove>();
            actorObject.AddComponent<AbilityWork>();
            if (detachedRestore)
            {
                if (publishComposition || unpublishedComposition)
                    throw new InvalidOperationException("Detached fixture cannot also select composition mode.");
                Actor.EnsureRuntimeState();
                Actor.PrepareForDetachedRestore();
            }
            else if (publishComposition || unpublishedComposition)
                Actor.PrepareForComposition();
            Actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            Actor.Progression.ConstructCharacterProgression(
                new ImmediateSkillGenerationService(),
                new TestSettingsProvider(settings),
                gameEventBus: progressionEvents ?? new GameEventBus(),
                profileProjector: new CharacterProgressionProfileProjector(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader()),
                    new CharacterRuntimeProfileFactory(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))));
            Actor.RefreshAbilityCache();
            Actor.Identity.SetPersistentId(
                persistentId ?? $"character:qa-{id}");
            Actor.Initialization(data);
            System.Random random = new System.Random(id);
            Actor.Progression.ApplyPreparedIdentity(
                displayName,
                $"{speciesTag} test origin",
                Array.Empty<int>(),
                CharacterGrowthRules.RollPotential(settings, random),
                id,
                autoChooseDrafts: false);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
            if (publishComposition)
                Actor.PublishComposition();
        }

        public CharacterActor Actor { get; }

        public void Dispose()
        {
            if (Application.isPlaying
                && Actor != null
                && Actor.Identity != null)
            {
                CharacterId characterId = new(Actor.Identity.PersistentId);
                if (characterId.IsValid)
                {
                    DungeonRuntimeLifetimeScope scope = UnityEngine.Object
                        .FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
                    scope?.Container?.Resolve<IPopulationHealthService>()
                        ?.RemovePendingExposures(characterId);
                }
            }
            if (Actor != null) UnityEngine.Object.DestroyImmediate(Actor.gameObject);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private sealed class DebugGameplayEffectSource : IGameplayEffectSource
    {
        public DebugGameplayEffectSource(
            GameplayEffectSourceRef sourceRef,
            params GameplayEffectBinding[] effects)
        {
            SourceRef = sourceRef;
            Effects = effects ?? Array.Empty<GameplayEffectBinding>();
        }

        public GameplayEffectSourceRef SourceRef { get; }
        public IReadOnlyList<GameplayEffectBinding> Effects { get; }
    }

    private sealed class AcquiredTraitContentFixture : IDisposable
    {
        private readonly GameplayEffectDefinitionSO effect;

        public AcquiredTraitContentFixture()
        {
            Settings = ScriptableObject
                .CreateInstance<CharacterAcquiredTraitSettingsSO>();
            ConfigureSettings(Settings);

            effect = ScriptableObject
                .CreateInstance<GameplayEffectDefinitionSO>();
            effect.Configure(
                812503,
                "effect:qa:acquired-work-speed",
                GameplayEffectTargetIds.WorkSpeed,
                GameplayEffectOperation.Multiply,
                GameplayEffectProjectionPhase.Multiplicative,
                GameplayEffectSourceKind.AcquiredTrait,
                GameplayEffectStackingPolicy.StackAll,
                0.1f,
                10f);

            Modules = new[]
            {
                CreateModule("acquired:qa:focus", "집중", "집중력을 익혔다.",
                    "binding:qa:acquired:focus", 1.01f),
                CreateModule("acquired:qa:craft", "숙련", "손놀림이 안정되었다.",
                    "binding:qa:acquired:craft", 1.02f),
                CreateModule("acquired:qa:rhythm", "리듬", "작업 리듬을 익혔다.",
                    "binding:qa:acquired:rhythm", 1.03f),
                CreateModule("acquired:qa:insight", "통찰", "반복 속에서 통찰을 얻었다.",
                    "binding:qa:acquired:insight", 1.04f)
            };
            CharacterAcquiredTraitFormulaCatalogAssetBuilder.PopulateForEditorTest(
                Settings,
                Modules);
        }

        public CharacterAcquiredTraitSettingsSO Settings { get; }
        public CharacterAcquiredTraitModuleSO[] Modules { get; }

        public void Dispose()
        {
            foreach (CharacterAcquiredTraitModuleSO module in Modules)
            {
                if (module != null)
                    UnityEngine.Object.DestroyImmediate(module);
            }
            if (effect != null)
                UnityEngine.Object.DestroyImmediate(effect);
            if (Settings != null)
                UnityEngine.Object.DestroyImmediate(Settings);
        }

        private static void ConfigureSettings(
            CharacterAcquiredTraitSettingsSO settings)
        {
            SerializedObject serialized = new SerializedObject(settings);
            serialized.FindProperty("settingsId").stringValue =
                "acquired-trait-settings:qa";
            serialized.FindProperty("maximumActiveTraits").intValue = 3;
            SerializedProperty gates = serialized.FindProperty(
                "manifestationGates");
            gates.arraySize = 3;
            ConfigureGate(gates.GetArrayElementAtIndex(0), 3, 3,
                CharacterSkillRarity.Common);
            ConfigureGate(gates.GetArrayElementAtIndex(1), 8, 5,
                CharacterSkillRarity.Advanced);
            ConfigureGate(gates.GetArrayElementAtIndex(2), 20, 7,
                CharacterSkillRarity.Rare);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureGate(
            SerializedProperty gate,
            int milestone,
            int budget,
            CharacterSkillRarity rarity)
        {
            gate.FindPropertyRelative("meaningfulRecordMilestone").intValue =
                milestone;
            gate.FindPropertyRelative("budget").intValue = budget;
            gate.FindPropertyRelative("rarity").enumValueIndex = (int)rarity;
        }

        private CharacterAcquiredTraitModuleSO CreateModule(
            string moduleId,
            string displayName,
            string description,
            string bindingId,
            float value)
        {
            CharacterAcquiredTraitModuleSO module = ScriptableObject
                .CreateInstance<CharacterAcquiredTraitModuleSO>();
            SerializedObject serialized = new SerializedObject(module);
            serialized.FindProperty("moduleId").stringValue = moduleId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("cost").intValue = 2;

            SerializedProperty domains = serialized.FindProperty(
                "domainAffinities");
            domains.arraySize = 1;
            domains.GetArrayElementAtIndex(0).enumValueIndex =
                (int)CharacterNarrativeDomain.Work;

            SerializedProperty effects = serialized.FindProperty("effects");
            effects.arraySize = 1;
            SerializedProperty binding = effects.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("bindingId").stringValue = bindingId;
            binding.FindPropertyRelative("definition").objectReferenceValue =
                effect;
            binding.FindPropertyRelative("value").floatValue = value;
            binding.FindPropertyRelative("condition").objectReferenceValue =
                null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return module;
        }
    }

    private sealed class TestSettingsProvider : ICharacterSkillSystemSettingsProvider
    {
        public TestSettingsProvider(CharacterSkillSystemSettingsSO settings)
        {
            Settings = settings;
        }

        public CharacterSkillSystemSettingsSO Settings { get; }
    }

    private sealed class FixedCharacterWorld :
        ICharacterWorldQuery,
        ICharacterLifetimeQuery
    {
        private readonly CharacterActor[] actors;

        public FixedCharacterWorld(params CharacterActor[] actors)
        {
            this.actors = (actors ?? Array.Empty<CharacterActor>())
                .Where(value => value != null)
                .ToArray();
        }

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => actors;
        public int LifetimeCharacterVersion => CharacterVersion;
        public IReadOnlyList<CharacterActor> AllCharacters => actors;
    }

    private sealed class DeferredAcquiredTraitLlmRuntime :
        ILocalLlmRuntime,
        ICorrelatedAcquiredTraitLlmRuntime,
        ICorrelatedAcquiredTraitModuleSelectionLlmRuntime
    {
        private sealed class Pending
        {
            public string RequestKey;
            public string Prompt;
            public Action<LocalLlmResult> Callback;
        }

        private readonly Queue<Pending> pending = new();

        public int PendingCallbackCount => pending.Count;
        public Action<LocalLlmResult> PeekCallbackForDebug() => pending.Peek().Callback;
        public string PeekPromptForDebug() => pending.Peek().Prompt;

        public bool GenerateAcquiredTraitAsync(
            string requestKey,
            string prompt,
            Action<LocalLlmResult> callback)
        {
            pending.Enqueue(new Pending
            {
                RequestKey = requestKey,
                Prompt = prompt,
                Callback = callback
            });
            return true;
        }

        public bool GenerateAcquiredTraitModuleSelectionAsync(
            string requestKey,
            string prompt,
            Action<LocalLlmResult> callback) =>
            GenerateAcquiredTraitAsync(requestKey, prompt, callback);

        public void CancelAcquiredTraitRequest(string requestKey)
        {
            Pending[] retained = pending
                .Where(value => !string.Equals(
                    value.RequestKey,
                    requestKey,
                    StringComparison.Ordinal))
                .ToArray();
            pending.Clear();
            foreach (Pending value in retained)
                pending.Enqueue(value);
        }

        public void CompleteNextSuccess()
        {
            Require(pending.Count > 0, "No acquired-trait callback is pending.");
            Pending value = pending.Dequeue();
            if (value.Prompt.Contains("selectionId=", StringComparison.Ordinal))
            {
                string selectionId = ReadPromptLine(value.Prompt, "selectionId=");
                string positiveModuleId = ReadPromptToken(
                    value.Prompt, "- moduleId=", ';');
                string selectionEvidence = ReadPromptLine(value.Prompt, "evidenceFactIds=")
                    .Split(',').First(item => !string.IsNullOrWhiteSpace(item)).Trim();
                string selectionResponse = JsonUtility.ToJson(
                    new CharacterAcquiredTraitModuleSelectionResponseDto
                    {
                        selectionId = selectionId,
                        positiveModuleIds = new List<string> { positiveModuleId },
                        drawbackModuleIds = new List<string>(),
                        evidenceFactIds = new List<string> { selectionEvidence },
                        displayName = "후천 특성 발현",
                        narrativeFlavor = "반복된 경험이 새로운 성향으로 발현되었다."
                    });
                value.Callback?.Invoke(new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    selectionResponse,
                    string.Empty,
                    string.Empty));
                return;
            }
            if (value.Prompt.Contains("presentationId=", StringComparison.Ordinal))
            {
                string presentationId = ReadPromptLine(
                    value.Prompt,
                    "presentationId=");
                string formulaResponse = JsonUtility.ToJson(
                    new CharacterAcquiredTraitFormulaPresentationDto
                    {
                        presentationId = presentationId,
                        displayName = "후천 특성 발현",
                        narrativeFlavor = "반복된 경험이 새로운 성향으로 발현되었다."
                    });
                value.Callback?.Invoke(new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    formulaResponse,
                    string.Empty,
                    string.Empty));
                return;
            }
            string combinationId = ReadPromptToken(
                value.Prompt,
                "- combinationId=",
                ';');
            string evidenceList = ReadPromptLine(
                value.Prompt,
                "evidenceFactIds=");
            string evidence = evidenceList.Split(',')
                .First(item => !string.IsNullOrWhiteSpace(item))
                .Trim();
            string response = JsonUtility.ToJson(
                new CharacterAcquiredTraitResponseDto
                {
                    combinationId = combinationId,
                    displayName = "후천 특성 발현",
                    description = "반복된 경험이 새로운 성향으로 발현되었다.",
                    narrativeReason = "저장된 경험 근거에 따라 선택되었다.",
                    evidenceFactIds = new List<string> { evidence }
                });
            value.Callback?.Invoke(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                response,
                string.Empty,
                string.Empty));
        }

        private static string ReadPromptToken(
            string prompt,
            string prefix,
            char terminator)
        {
            int start = prompt.IndexOf(prefix, StringComparison.Ordinal);
            Require(start >= 0, $"Prompt token '{prefix}' is missing.");
            start += prefix.Length;
            int end = prompt.IndexOf(terminator, start);
            Require(end > start, $"Prompt token '{prefix}' is malformed.");
            return prompt.Substring(start, end - start).Trim();
        }

        private static string ReadPromptLine(string prompt, string prefix)
        {
            int start = prompt.IndexOf(prefix, StringComparison.Ordinal);
            Require(start >= 0, $"Prompt line '{prefix}' is missing.");
            start += prefix.Length;
            int end = prompt.IndexOf('\n', start);
            if (end < 0)
                end = prompt.Length;
            return prompt.Substring(start, end - start).Trim();
        }

        public bool GenerateCharacterSkillAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GeneratePersonaAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(
            string prompt,
            string originalText,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(
            string prompt,
            string originalText,
            Action<LocalLlmResult> callback) => false;
    }

    private sealed class ImmediateSkillGenerationService : ICharacterSkillGenerationService
    {
        public CharacterSkillDraft CreateDraft(
            CharacterProgression progression,
            CharacterSkillKind kind,
            int unlockLevel,
            int revision = 0)
        {
            CharacterSkillDraft draft = new CharacterSkillDraft
            {
                kind = kind,
                unlockLevel = unlockLevel,
                requestKey = $"qa:{progression.GetInstanceID()}:{kind}:{unlockLevel}:r{revision}"
            };
            int count = kind == CharacterSkillKind.Active ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                draft.rules.Add(new CharacterSkillCandidateRule
                {
                    rarity = kind == CharacterSkillKind.Ultimate
                        ? CharacterSkillRarity.Legendary
                        : CharacterSkillRarity.Advanced,
                    budget = 10,
                    trigger = kind == CharacterSkillKind.Active
                        ? CharacterSkillTrigger.ManualCombat
                        : CharacterSkillTrigger.WorkCompleted,
                    target = kind == CharacterSkillKind.Active
                        ? CharacterSkillTarget.Enemy
                        : CharacterSkillTarget.Self
                });
            }

            return draft;
        }

        public void RequestDraft(CharacterProgression progression, CharacterSkillDraft draft)
        {
            if (draft == null || draft.isReady || draft.permanentlyChosen)
            {
                return;
            }

            for (int i = 0; i < draft.rules.Count; i++)
            {
                draft.candidates.Add(new CharacterSkillInstance
                {
                    id = $"{draft.requestKey}:{i}",
                    displayName = $"QA {draft.kind} {draft.unlockLevel}-{i}",
                    description = "검증용 기술",
                    narrativeReason = "검증용 경험",
                    kind = draft.kind,
                    rarity = draft.rules[i].rarity,
                    trigger = draft.kind == CharacterSkillKind.Ultimate
                        ? CharacterSkillTrigger.ManualCombat
                        : draft.rules[i].trigger,
                    target = draft.rules[i].target,
                    ultimateDomain = draft.kind == CharacterSkillKind.Ultimate
                        ? CharacterUltimateDomain.Offense
                        : CharacterUltimateDomain.None,
                    modules = new List<CharacterSkillModuleSelection>
                    {
                        new CharacterSkillModuleSelection
                        {
                            moduleId = draft.kind == CharacterSkillKind.Passive ? "work_speed" : "damage",
                            variantId = draft.kind == CharacterSkillKind.Passive ? "small" : "light"
                        }
                    },
                    requestKey = draft.requestKey
                });
            }

            draft.isReady = true;
            progression.OnDraftReady(draft);
        }

        public void CancelRequests(CharacterProgression progression)
        {
        }

        public bool TryValidateResponse(
            CharacterSkillDraft draft,
            string response,
            out List<CharacterSkillInstance> skills,
            out string error)
        {
            skills = new List<CharacterSkillInstance>();
            error = string.Empty;
            return false;
        }
    }

    private sealed class MissingLlmRuntimeProvider : ILocalLlmRuntimeProvider
    {
        public bool TryGetRuntime(out ILocalLlmRuntime runtime)
        {
            runtime = null;
            return false;
        }

        public ILocalLlmRuntime GetRequiredRuntime()
        {
            throw new InvalidOperationException("No runtime is expected in validation scenarios.");
        }
    }

    private sealed class TestLlmRuntimeProvider : ILocalLlmRuntimeProvider
    {
        private readonly ILocalLlmRuntime runtime;

        public TestLlmRuntimeProvider(ILocalLlmRuntime runtime)
        {
            this.runtime = runtime;
        }

        public bool TryGetRuntime(out ILocalLlmRuntime resolvedRuntime)
        {
            resolvedRuntime = runtime;
            return runtime != null;
        }

        public ILocalLlmRuntime GetRequiredRuntime()
        {
            return runtime ?? throw new InvalidOperationException("Missing test LLM runtime.");
        }
    }

    private sealed class SequencedLlmRuntime :
        ILocalLlmRuntime,
        ICharacterSkillModuleSelectionLlmRuntime
    {
        private readonly Queue<LocalLlmResult> results = new Queue<LocalLlmResult>();

        public int CharacterSkillCallCount { get; private set; }

        public void Enqueue(LocalLlmResult result)
        {
            results.Enqueue(result);
        }

        public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback)
        {
            CharacterSkillCallCount++;
            callback?.Invoke(results.Count > 0
                ? results.Dequeue()
                : new LocalLlmResult(LocalLlmRequestStatus.Failed, string.Empty, "missing result", string.Empty));
            return true;
        }

        public bool GenerateCharacterSkillModuleSelectionAsync(
            string requestKey,
            string prompt,
            Action<LocalLlmResult> callback) =>
            GenerateCharacterSkillAsync(prompt, callback);

        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
    }

    private sealed class NeverCompletingLlmRuntime :
        ILocalLlmRuntime,
        ICharacterSkillModuleSelectionLlmRuntime
    {
        public int CharacterSkillCallCount { get; private set; }

        public bool GenerateCharacterSkillAsync(
            string prompt,
            Action<LocalLlmResult> callback)
        {
            CharacterSkillCallCount++;
            return true;
        }

        public bool GenerateCharacterSkillModuleSelectionAsync(
            string requestKey,
            string prompt,
            Action<LocalLlmResult> callback) =>
            GenerateCharacterSkillAsync(prompt, callback);

        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
    }

    private sealed class BatchSkillLlmRuntime :
        ILocalLlmRuntime,
        ICharacterSkillModuleSelectionBatchLlmRuntime
    {
        private Action<LocalLlmResult> callback;

        public int BatchCallCount { get; private set; }
        public bool HasPendingCallback => callback != null;
        public string LastPrompt { get; private set; } = string.Empty;

        public bool GenerateCharacterSkillModuleSelectionBatchAsync(
            string requestKey,
            string prompt,
            Action<LocalLlmResult> resultCallback)
        {
            BatchCallCount++;
            LastPrompt = prompt ?? string.Empty;
            callback = resultCallback;
            return true;
        }

        public void Complete(LocalLlmResult result)
        {
            Action<LocalLlmResult> pending = callback;
            callback = null;
            pending?.Invoke(result);
        }

        public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> resultCallback) => false;
        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> resultCallback) => false;
    }

    private sealed class ReadinessControlledLlmRuntime :
        ILocalLlmRuntime,
        ILocalLlmRuntimeReadiness,
        ICharacterSkillModuleSelectionLlmRuntime
    {
        private LocalLlmRuntimeReadinessState state =
            LocalLlmRuntimeReadinessState.Starting;

        public int CharacterSkillCallCount { get; private set; }

        public LocalLlmRuntimeReadinessSnapshot CaptureReadiness() =>
            new LocalLlmRuntimeReadinessSnapshot(state);

        public void SetReady()
        {
            state = LocalLlmRuntimeReadinessState.Ready;
        }

        public bool GenerateCharacterSkillAsync(
            string prompt,
            Action<LocalLlmResult> callback)
        {
            CharacterSkillCallCount++;
            return true;
        }

        public bool GenerateCharacterSkillModuleSelectionAsync(
            string requestKey,
            string prompt,
            Action<LocalLlmResult> callback) =>
            GenerateCharacterSkillAsync(prompt, callback);

        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
    }

    private sealed class CompletionOwningLlmRuntime :
        ILocalLlmRuntime,
        ILocalLlmRuntimeReadiness,
        ILocalLlmAcceptedRequestCompletionOwner,
        ICharacterSkillModuleSelectionLlmRuntime
    {
        private Action<LocalLlmResult> callback;

        public int CharacterSkillCallCount { get; private set; }
        public bool HasPendingCallback => callback != null;
        public bool OwnsAcceptedRequestCompletion => true;

        public LocalLlmRuntimeReadinessSnapshot CaptureReadiness() =>
            new LocalLlmRuntimeReadinessSnapshot(
                LocalLlmRuntimeReadinessState.Ready);

        public bool GenerateCharacterSkillAsync(
            string prompt,
            Action<LocalLlmResult> resultCallback)
        {
            CharacterSkillCallCount++;
            callback = resultCallback;
            return true;
        }

        public bool GenerateCharacterSkillModuleSelectionAsync(
            string requestKey,
            string prompt,
            Action<LocalLlmResult> resultCallback) =>
            GenerateCharacterSkillAsync(prompt, resultCallback);

        public void Complete(LocalLlmResult result)
        {
            Action<LocalLlmResult> pending = callback;
            callback = null;
            pending?.Invoke(result);
        }

        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> resultCallback) => false;
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> resultCallback) => false;
    }

    private sealed class DelayedSkillLlmRuntime :
        ILocalLlmRuntime,
        ICharacterSkillModuleSelectionLlmRuntime
    {
        private readonly List<Action<LocalLlmResult>> acceptedCallbacks =
            new List<Action<LocalLlmResult>>();

        public int CharacterSkillCallCount { get; private set; }
        public int PendingCallbackCount => acceptedCallbacks.Count;
        public int DeliveredCallbackCount { get; private set; }
        public string LastPrompt { get; private set; }
        public string LastRequestKey { get; private set; }

        public bool GenerateCharacterSkillAsync(
            string prompt,
            Action<LocalLlmResult> callback)
        {
            CharacterSkillCallCount++;
            LastPrompt = prompt;
            acceptedCallbacks.Add(callback);
            return true;
        }

        public bool GenerateCharacterSkillModuleSelectionAsync(
            string requestKey,
            string prompt,
            Action<LocalLlmResult> callback)
        {
            LastRequestKey = requestKey;
            return GenerateCharacterSkillAsync(prompt, callback);
        }

        public void CompleteAcceptedCallbacks(LocalLlmResult result)
        {
            Action<LocalLlmResult>[] callbacks = acceptedCallbacks.ToArray();
            acceptedCallbacks.Clear();
            foreach (Action<LocalLlmResult> callback in callbacks)
            {
                DeliveredCallbackCount++;
                callback?.Invoke(result);
            }
        }

        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
    }

    private sealed class MutableUiClock : IUiClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }

        public void Advance(float seconds)
        {
            DeltaTime = Mathf.Max(0f, seconds);
            Time += DeltaTime;
        }
    }
}

public static class CharacterPopulationDebugScenarios
{
    public static void RunDeadVisitorCaptureBatch()
    {
        // This edit-mode fixture uses transient actors and in-memory profile
        // DTOs only; it never opens a player save. OS isolation belongs to the
        // mandatory launcher, not a PlayMode persistence-root setting.
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Dead-visitor capture coverage requires batch QA.");
        Require(VerifyDeadVisitorCapture(), "Dead visitor capture failed.");
        Require(VisitorPersistenceBindingDebugScenarios.RunAll(true),
            "Existing strict visitor save binding regressions failed.");
        Debug.Log("DEAD_VISITOR_CAPTURE=PASS; actual-public-death=1; visit-count-preserved=1; "
            + "dead-profile-restored=1; duplicate-admission=0; strict-missing-owner-rejected=1; "
            + "controlled-actor-not-natural-play=1");
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        Run("ready pool and persistent identity", VerifyReadyPool, errors);
        Run("profile save restore", VerifyProfileRestore, errors);
        Run("dead visitor capture retires admission", VerifyDeadVisitorCapture, errors);
        Run("failed preparation preserves profile", VerifyFailedPreparationPreservesProfile, errors);
        Run("duplicate profile IDs rejected", VerifyDuplicateProfileRestoreRejected, errors);
        Run("recruited visitor promotion", VerifyStaffPromotion, errors);
        Run("long-running population remains bounded", VerifyPopulationBound, errors);
        foreach (string error in errors)
        {
            Debug.LogError(error);
        }

        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log("Character population scenarios passed.");
        }

        return errors.Count == 0;
    }

    private static bool VerifyDeadVisitorCapture()
    {
        using PopulationFixture fixture = new PopulationFixture();
        WorldCharacterProfile profile = fixture.Service.AcquireVisitor(fixture.Customers[0]);
        GameObject actorObject = new GameObject("PopulationDeadVisitorCaptureActor");
        try
        {
            actorObject.AddComponent<SpriteRenderer>();
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            actorObject.AddComponent<AbilityMove>();
            actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Identity.SetPersistentId(profile.persistentId);
            actor.Initialize(fixture.Customers[0]);
            fixture.Service.BindActor(profile, actor);
            int visitCount = profile.visitCount;
            Require(profile.isAlive && profile.isVisiting && !actor.IsDead,
                "Controlled live visitor admission was not bound.");

            // A controlled fixture calls the real public death producer. This
            // is not a natural-play death or a generated learning example.
            actor.Die("controlled visitor capture boundary");
            Require(actor.Stats.IsDead && actor.CurrentLifecycleState == CharacterLifecycleState.Despawned,
                "Public death did not terminate the controlled visitor's real lifetime.");
            fixture.Service.RefreshProfile(actor);
            List<WorldCharacterProfile> saved = fixture.Service.CaptureProfiles();
            WorldCharacterProfile terminal = saved.Single(value => value.persistentId == profile.persistentId);
            Require(!terminal.isAlive && !terminal.isVisiting && !terminal.isStaff
                    && terminal.settlementStanding == CharacterSettlementStanding.PreparedCandidate
                    && terminal.visitCount == visitCount && !CharacterWorldPersistenceRules.IsPersistentActor(actor),
                "A dead visitor retained admission, changed visit count or remained a live save owner.");
            string terminalJson = JsonUtility.ToJson(terminal);
            fixture.Service.RefreshProfile(actor);
            Require(terminalJson == JsonUtility.ToJson(fixture.Service.CaptureProfiles()
                    .Single(value => value.persistentId == profile.persistentId)),
                "Repeated dead visitor synchronization changed its terminal profile.");
            DungeonGameRestoreReport join = new();
            CharacterWorldSaveValidation.ValidateVisitorBindings(Array.Empty<DungeonCharacterSaveData>(),
                new[] { terminal }, join);
            Require(join.Success, "A lawful dead non-visiting profile requires no live actor owner.");

            string savedJson = JsonConvert.SerializeObject(saved);
            using PopulationFixture restored = new PopulationFixture();
            restored.Service.RestoreProfiles(saved);
            WorldCharacterProfile restoredTerminal = restored.Service.Profiles
                .Single(value => value.persistentId == terminal.persistentId);
            Require(terminalJson == JsonUtility.ToJson(restoredTerminal)
                    && savedJson == JsonConvert.SerializeObject(saved),
                "Dead profile restore changed its history or serialized source input.");
            WorldCharacterProfile next = restored.Service.AcquireVisitor(restored.Customers[0]);
            Require(next != null && next.persistentId != terminal.persistentId,
                "A dead visitor was recycled into a live duplicate admission.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifyReadyPool()
    {
        using PopulationFixture fixture = new PopulationFixture();
        List<WorldCharacterProfile> acquired = new List<WorldCharacterProfile>();
        WorldCharacterProfile first = fixture.Service.AcquireVisitor(fixture.Customers[0]);
        Require(first != null && first.IsReady,
            "The first visitor entered before its active and passive were prepared.");
        acquired.Add(first);
        Require(fixture.Service.Profiles.Count == fixture.Settings.guestReadyTarget
            && fixture.Service.Profiles.All(profile => profile.IsReady),
            "The initial ready pool was not prepared to the configured target.");
        Require(fixture.LifePublication.CharacterIds.SetEquals(
                fixture.Service.Profiles.Select(profile => profile.persistentId)),
            "Every persisted ready-pool profile must publish exactly one V19 life record.");

        while (acquired.Count < fixture.Settings.maximumAliveNonStaffGuests)
        {
            WorldCharacterProfile profile = fixture.Customers
                .Select(candidate => fixture.Service.AcquireVisitor(candidate))
                .FirstOrDefault(candidate => candidate != null);
            if (profile == null)
            {
                break;
            }

            Require(profile.IsReady, "An unprepared world character was admitted.");
            acquired.Add(profile);
        }

        Require(acquired.Count == fixture.Settings.maximumAliveNonStaffGuests,
            $"Expected the living guest cap {fixture.Settings.maximumAliveNonStaffGuests}, got {acquired.Count}.");
        Require(fixture.Customers.All(candidate => fixture.Service.AcquireVisitor(candidate) == null),
            "The living non-staff cap allowed an extra visitor profile.");
        Require(acquired.Select(profile => profile.persistentId).Distinct(StringComparer.Ordinal).Count()
                == fixture.Settings.maximumAliveNonStaffGuests,
            "World-character persistent IDs were duplicated.");
        string startingPacketDiagnostics = string.Join(" | ", acquired
            .Where(profile => !HasValidGeneratedGrowthAuthority(profile))
            .Select(DescribeStartingPacket));
        Require(acquired.All(HasValidGeneratedGrowthAuthority),
            "A visitor profile did not retain its authoritative nine-entry starting packet. "
                + startingPacketDiagnostics);
        Require(acquired.All(HasStrictProfileDraftBindings),
            "A visitor draft did not retain its exact population-profile owner binding.");

        WorldCharacterProfile returning = first;
        returning.isVisiting = false;
        returning.visitCount = 4;
        CharacterSO returningTemplate = fixture.Customers.First(candidate => candidate.id == returning.characterDataId);
        WorldCharacterProfile reacquired = fixture.Service.AcquireVisitor(returningTemplate);
        Require(reacquired != null && reacquired.persistentId == returning.persistentId,
            "A returning guest was replaced by a new identity.");
        Require(reacquired.visitCount == 4,
            "The returning guest lost visit history.");
        return true;
    }

    private static bool VerifyProfileRestore()
    {
        using PopulationFixture source = new PopulationFixture();
        WorldCharacterProfile first = source.Service.AcquireVisitor(source.Customers[0]);
        WorldCharacterProfile second = source.Service.AcquireVisitor(source.Customers[0]);
        first.visitCount = 7;
        first.socialMemory ??= new CharacterSocialMemorySnapshot();
        first.socialMemory.characterSentiments.Add(new SocialMemoryFloat("world:test:friend", 0.45f));
        first.socialMemory.sourceTrust.Add(new SocialMemoryFloat("world:test:source", 0.8f));
        first.socialMemory.recentRumors.Add(new SocialRumorSnapshot
        {
            type = SocialRumorType.Praise,
            targetType = SocialRumorTargetType.Character,
            sourceActorId = "world:test:source",
            targetCharacterId = "world:test:friend",
            sentiment = 0.5f,
            spreadChance = 0.75f,
            trustImpact = 0.1f,
            remainingSeconds = 300f,
            summary = "helped at the counter",
            source = "test"
        });
        first.growth.nextActiveDraftHasPity = true;
        first.narrative.Record(
            CharacterNarrativeDomain.Relationship,
            "returning-guest",
            "dungeon",
            "trusted",
            3f,
            9);
        string sourceNarrativeJson = JsonUtility.ToJson(first.narrative);
        string sourceStartingPacketJson = CaptureStartingPacketJson(first);
        List<WorldCharacterProfile> snapshot = source.Service.CaptureProfiles();
        WorldCharacterProfile capturedFirst = snapshot
            .First(profile => profile.persistentId == first.persistentId);
        Require(first.isVisiting && capturedFirst.isVisiting,
            "The acquired visitor was not captured as visiting.");

        using PopulationFixture restored = new PopulationFixture();
        restored.Service.RestoreProfiles(snapshot);
        Require(restored.Service.Profiles.Count == snapshot.Count,
            "The restored population count changed.");
        WorldCharacterProfile restoredFirst = restored.Service.Profiles
            .First(profile => profile.persistentId == first.persistentId);
        bool socialRestored = restoredFirst.socialMemory != null
            && restoredFirst.socialMemory.characterSentiments.Any(item =>
                item != null
                && item.key == "world:test:friend"
                && Mathf.Approximately(item.value, 0.45f))
            && restoredFirst.socialMemory.sourceTrust.Any(item =>
                item != null
                && item.key == "world:test:source"
                && Mathf.Approximately(item.value, 0.8f))
            && restoredFirst.socialMemory.recentRumors.Any(item =>
                item != null
                && item.targetCharacterId == "world:test:friend"
                && item.remainingSeconds > 0f);
        Require(restoredFirst.isVisiting == first.isVisiting
            && restoredFirst.visitCount == 7
            && socialRestored,
            "Visit state or social memory changed during restore.");
        string sourceFacts = string.Join(",", first.narrative.Facts.Select(value =>
            value == null
                ? "<null>"
                : $"{value.factId}:{value.count}/{value.milestoneCount}"));
        string restoredFacts = string.Join(",", restoredFirst.narrative.Facts.Select(value =>
            value == null
                ? "<null>"
                : $"{value.factId}:{value.count}/{value.milestoneCount}"));
        string restoredNarrativeJson = JsonUtility.ToJson(restoredFirst.narrative);
        string restoredStartingPacketJson = CaptureStartingPacketJson(restoredFirst);
        bool returningGuestPreserved = restoredFirst.narrative.Facts.Any(value =>
            value != null
            && string.Equals(
                value.factId,
                "returning-guest",
                StringComparison.Ordinal));
        bool narrativeEqual = string.Equals(
            sourceNarrativeJson,
            restoredNarrativeJson,
            StringComparison.Ordinal);
        bool startingPacketEqual = string.Equals(
            sourceStartingPacketJson,
            restoredStartingPacketJson,
            StringComparison.Ordinal);
        Require(first.growth.nextActiveDraftHasPity
            && restoredFirst.growth.nextActiveDraftHasPity
            && narrativeEqual
            && returningGuestPreserved
            && startingPacketEqual,
            "Growth correction, narrative ledger, or starting packet changed during restore. "
                + $"sourcePity={first.growth.nextActiveDraftHasPity}, "
                + $"restoredPity={restoredFirst.growth.nextActiveDraftHasPity}, "
                + $"narrativeEqual={narrativeEqual}, "
                + $"returningGuestPreserved={returningGuestPreserved}, "
                + $"startingPacketEqual={startingPacketEqual}, "
                + $"sourceMeaningful={first.narrative.MeaningfulRecordCount}, "
                + $"restoredMeaningful={restoredFirst.narrative.MeaningfulRecordCount}, "
                + $"sourceFacts={first.narrative.Facts.Count}[{sourceFacts}], "
                + $"restoredFacts={restoredFirst.narrative.Facts.Count}[{restoredFacts}], "
                + $"sourcePacket={sourceStartingPacketJson}, "
                + $"restoredPacket={restoredStartingPacketJson}.");
        Require(restored.Service.Profiles.Any(profile => profile.persistentId == second.persistentId),
            "A second profile disappeared during restore.");
        Require(restored.Service.Profiles.All(HasStrictProfileDraftBindings),
            "Profile restore changed a population draft owner binding.");
        return true;
    }

    private static bool VerifyFailedPreparationPreservesProfile()
    {
        ThrowOnceSkillGenerationService generation = new(
            new ImmediateSkillGenerationService());
        using PopulationFixture fixture = new(generation);
        string failedProfileId = null;
        string originalProfileJson = null;
        generation.BeforeFailure = progression =>
        {
            CharacterNarrativeRequestOwnerBinding binding =
                CharacterNarrativeRequestOwnerBinding.Capture(progression);
            WorldCharacterProfile original = fixture.Service.Profiles.Single(profile =>
                string.Equals(
                    profile.persistentId,
                    binding.persistentId,
                    StringComparison.Ordinal));
            failedProfileId = original.persistentId;
            originalProfileJson = JsonUtility.ToJson(original);
        };

        bool failedAsExpected = false;
        try
        {
            fixture.Service.AcquireVisitor(fixture.Customers[0]);
        }
        catch (InvalidOperationException exception)
        {
            failedAsExpected = exception.Message.Contains(
                ThrowOnceSkillGenerationService.FailureMessage,
                StringComparison.Ordinal);
        }

        Require(failedAsExpected
            && generation.FailureCount == 1
            && generation.CancelCount
                == generation.CancelCountAtFailure + 1
            && !string.IsNullOrWhiteSpace(failedProfileId)
            && !string.IsNullOrWhiteSpace(originalProfileJson),
            "The injected population preparation failure did not reach atomic cleanup.");
        Require(generation.FailedPreviewObject == null,
            "The failed population preview was not destroyed.");

        WorldCharacterProfile preserved = fixture.Service.CaptureProfiles()
            .Single(profile => string.Equals(
                profile.persistentId,
                failedProfileId,
                StringComparison.Ordinal));
        Require(string.Equals(
                JsonUtility.ToJson(preserved),
                originalProfileJson,
                StringComparison.Ordinal),
            "Failed preparation changed the original population profile during capture.");
        Require(HasValidGeneratedGrowthAuthority(preserved),
            "Failed preparation corrupted population growth authority or its nine-entry starting packet.");

        generation.ReleaseFault();
        WorldCharacterProfile prepared = fixture.Service.AcquireVisitor(fixture.Customers[0]);
        Require(prepared != null
            && prepared.IsReady
            && string.Equals(
                prepared.persistentId,
                failedProfileId,
                StringComparison.Ordinal)
            && generation.FailureCount == 1,
            "Population preparation did not resume the original profile after the one-shot fault.");
        Require(fixture.Service.CaptureProfiles().All(profile => profile.IsReady),
            "Population preparation retained an orphan pending profile after retry.");
        return true;
    }

    private static bool HasValidGeneratedGrowthAuthority(
        WorldCharacterProfile profile)
    {
        CharacterGrowthState growth = profile?.growth;
        CharacterStartingProfileState starting = growth?.startingProfile;
        List<CharacterStartingProficiencyExperience> values =
            growth?.startingProficiencies;
        if (growth == null
            || !growth.initialized
            || !Enum.IsDefined(typeof(CharacterPotentialGrade), growth.potentialGrade)
            || growth.traitSelectionAuthorityOrigin
                != CharacterTraitSelectionAuthorityOrigin.PopulationGeneration
            || growth.traitSelectionAuthorityVersion
                != CharacterGrowthState.CurrentTraitSelectionAuthorityVersion
            || growth.skillGenerationRevision < 0
            || growth.skillAcquisitionRevision < 0L
            || growth.traitIds == null
            || growth.traitIds.Count is < 1 or > 4
            || growth.traitIds.Count != growth.traitIds.Distinct().Count()
            || starting?.prepared != true
            || !Enum.IsDefined(typeof(CharacterStartingAgeBand), starting.ageBand)
            || starting.proficiencyCap
                != CharacterStartingProfileRules.ResolveAgeCap(starting.ageBand)
            || values == null
            || values.Count != BuiltInCharacterProficiencyIds.All.Count)
        {
            return false;
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (CharacterStartingProficiencyExperience value in values)
        {
            if (value == null)
            {
                return false;
            }
            CharacterProficiencyId id = new(value.proficiencyId);
            if (!id.IsValid
                || !BuiltInCharacterProficiencyIds.All.Contains(id)
                || !seen.Add(id.Value)
                || value.experience < 0
                || value.experience > starting.proficiencyCap
                || float.IsNaN(value.learningMultiplier)
                || float.IsInfinity(value.learningMultiplier)
                || !CharacterProficiencySpecializationRules.IsCanonical(
                    value.learningMultiplier))
            {
                return false;
            }
        }
        return seen.Count == BuiltInCharacterProficiencyIds.All.Count;
    }

    private static string DescribeStartingPacket(WorldCharacterProfile profile)
    {
        CharacterGrowthState growth = profile?.growth;
        CharacterStartingProfileState starting = growth?.startingProfile;
        List<CharacterStartingProficiencyExperience> values =
            growth?.startingProficiencies;
        string expectedCap = starting != null
            && Enum.IsDefined(typeof(CharacterStartingAgeBand), starting.ageBand)
                ? CharacterStartingProfileRules.ResolveAgeCap(starting.ageBand)
                    .ToString()
                : "<invalid>";
        string entries = values == null
            ? "<null>"
            : string.Join(",", values.Select(value => value == null
                ? "<null>"
                : $"{value.proficiencyId}={value.experience}@{value.learningMultiplier:R}"));
        return $"{profile?.persistentId ?? "<null>"}:initialized={growth?.initialized}"
            + $",authority={growth?.traitSelectionAuthorityOrigin}/"
            + $"{growth?.traitSelectionAuthorityVersion}"
            + $",revisions={growth?.skillGenerationRevision}/"
            + $"{growth?.skillAcquisitionRevision}"
            + $",traits={growth?.traitIds?.Count ?? -1}"
            + $",prepared={starting?.prepared},ageBand={starting?.ageBand}"
            + $",ageCap={starting?.proficiencyCap ?? -1},expectedCap={expectedCap}"
            + $",count={(values == null ? -1 : values.Count)},entries=[{entries}]";
    }

    private static string CaptureStartingPacketJson(WorldCharacterProfile profile) =>
        JsonUtility.ToJson(new StartingPacketJson
        {
            entries = profile?.growth?.startingProficiencies
        });

    [Serializable]
    private sealed class StartingPacketJson
    {
        public List<CharacterStartingProficiencyExperience> entries;
    }

    private static bool HasStrictProfileDraftBindings(
        WorldCharacterProfile profile)
    {
        return profile?.growth?.drafts != null
            && profile.growth.drafts.All(draft => draft != null
                && draft.ownerBinding?.IsWellFormed == true
                && string.Equals(
                    draft.ownerBinding.persistentId,
                    profile.persistentId,
                    StringComparison.Ordinal)
                && draft.requestKey?.StartsWith(
                    $"population-test:{profile.persistentId}:",
                    StringComparison.Ordinal) == true);
    }

    private static bool VerifyStaffPromotion()
    {
        using PopulationFixture fixture = new PopulationFixture();
        WorldCharacterProfile profile = fixture.Service.AcquireVisitor(fixture.Customers[0]);
        GameObject actorObject = new GameObject("PopulationPromotionActor");
        try
        {
            actorObject.AddComponent<SpriteRenderer>();
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            actorObject.AddComponent<AbilityMove>();
            actorObject.AddComponent<AbilityWork>();
            actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Identity.SetPersistentId(profile.persistentId);
            actor.Initialize(fixture.Customers[0]);
            fixture.Service.BindActor(profile, actor);
            fixture.Service.PromoteToStaff(actor);
            Require(profile.isStaff && !profile.isVisiting,
                "A recruited visitor profile was not promoted to staff.");
            actor.characterType = CharacterType.Customer;
            fixture.Service.ReleaseVisitor(actor);
            Require(profile.isStaff
                && !profile.isVisiting
                && actor.Identity.CharacterType == CharacterType.NPC
                && fixture.Service.TryGetProfile(actor, out WorldCharacterProfile retained)
                && retained.persistentId == profile.persistentId,
                "A hired profile was treated as a visitor after its actor type was reset.");
            WorldCharacterProfile next = fixture.Service.AcquireVisitor(fixture.Customers[0]);
            Require(next != null && next.persistentId != profile.persistentId,
                "A hired profile returned through the guest pool.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifyDuplicateProfileRestoreRejected()
    {
        using PopulationFixture fixture = new PopulationFixture();
        WorldCharacterProfile first = fixture.Service.AcquireVisitor(fixture.Customers[0]);
        Require(first != null, "No profile was available for duplicate restore verification.");
        WorldCharacterProfile duplicate = first.Clone();
        duplicate.visitCount++;

        try
        {
            fixture.Service.RestoreProfiles(new[] { first, duplicate });
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message.Contains(first.persistentId, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool VerifyPopulationBound()
    {
        using PopulationFixture fixture = new PopulationFixture();
        for (int cycle = 0; cycle < 200; cycle++)
        {
            CharacterSO template = fixture.Customers[cycle % fixture.Customers.Length];
            WorldCharacterProfile visitor = fixture.Service.AcquireVisitor(template);
            if (visitor != null)
            {
                visitor.visitCount++;
                visitor.isVisiting = false;
            }
        }

        Require(fixture.Service.Profiles.Count <= fixture.Settings.maximumAliveNonStaffGuests,
            "Repeated visits grew the world-character pool beyond its configured cap.");
        Require(fixture.Service.Profiles.Select(profile => profile.persistentId)
                .Distinct(StringComparer.Ordinal).Count() == fixture.Service.Profiles.Count,
            "Repeated visits duplicated a persistent identity.");
        Require(fixture.Service.Profiles.All(profile => profile.IsReady),
            "The long-running pool retained an unfinished profile.");
        return true;
    }

    private static void Run(string name, Func<bool> scenario, ICollection<string> errors)
    {
        try
        {
            if (!scenario()) errors.Add($"{name}: returned false");
        }
        catch (Exception exception)
        {
            errors.Add($"{name}: {exception.Message}\n{exception.StackTrace}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class MissingFactionContractQuery : IFactionContractQuery
    {
        public bool IsContractUnlocked(
            string factionId,
            FactionContractKind contract) => false;
    }

    private sealed class PopulationFixture : IDisposable
    {
        private readonly DomainFactDispatcher skillFacts;
        private readonly IDisposable skillBinding;

        public PopulationFixture(
            ICharacterSkillGenerationService skillGenerationService = null)
        {
            Settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
            Settings.guestReadyTarget = 8;
            Settings.guestReadyLowWatermark = 4;
            Settings.maximumAliveNonStaffGuests = 24;
            UnityGameContentRootLoader loader = new UnityGameContentRootLoader();
            ResourceGameContentCatalog content = new ResourceGameContentCatalog(loader);
            LifePublication = new RecordingCharacterLifePublicationService();
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new CharacterSkillAcquiredOutcomeDescriptor() },
                new IGameplayOutcomeAdapterRegistration[] { new CharacterSkillAcquiredOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry,
                new GameplayOutcomeBufferLimits(128, 8, 512),
                new GameplayOutcomeRunId("run:population-fixture-skill-acquisition"), 1L);
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            skillFacts = new DomainFactDispatcher();
            skillBinding = CharacterSkillAcquiredFactMapping.CreateBinding(
                registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger))
                .Connect(skillFacts);
            CharacterPopulationApplicationAdapter adapter = new(
                new PopulationSettingsProvider(Settings),
                content,
                skillGenerationService ?? new ImmediateSkillGenerationService(),
                new FixedPopulationRunSeedProvider(1),
                new MissingFactionContractQuery(),
                new ResourceRunCharacterCatalog(content),
                new CharacterRuntimeProfileFactory(content),
                LifePublication,
                new GameplayOutcomeEvidenceUseTransaction(ledger,
                    new GameplayOutcomeNarrativeEvidenceQuery(ledger)),
                skillFacts,
                new CharacterSkillAcquisitionPublicationRuntime(skillFacts, skillFacts),
                new FixedGameSessionStateProvider());
            Service = new CharacterPopulationService(adapter);
            Customers = content.GetAll<CharacterSO>()
                .Where(candidate => candidate != null && candidate.characterType == CharacterType.Customer)
                .OrderBy(candidate => candidate.id)
                .ToArray();
            Require(Customers.Length > 0, "No customer templates were available for population tests.");
        }

        public CharacterSkillSystemSettingsSO Settings { get; }
        public CharacterPopulationService Service { get; }
        public CharacterSO[] Customers { get; }
        public RecordingCharacterLifePublicationService LifePublication { get; }

        public void Dispose()
        {
            Service.Dispose();
            skillBinding.Dispose();
            skillFacts.Dispose();
            UnityEngine.Object.DestroyImmediate(Settings);
        }

        public sealed class RecordingCharacterLifePublicationService :
            ICharacterLifePublicationService
        {
            public HashSet<string> CharacterIds { get; } =
                new(StringComparer.Ordinal);

            public void EnsureRegistered(CharacterActor actor)
            {
                if (actor == null)
                {
                    throw new ArgumentNullException(nameof(actor));
                }

                CharacterIds.Add(CharacterPersistentIdentity.Require(actor).Value);
            }

            public void EnsureRegistered(
                CharacterId characterId,
                CharacterSpeciesId phenotypeSpeciesId)
            {
                if (!characterId.IsValid || !phenotypeSpeciesId.IsValid)
                {
                    throw new ArgumentException(
                        "Population fixture requires valid life IDs.");
                }

                CharacterIds.Add(characterId.Value);
            }
        }

        private sealed class FixedPopulationRunSeedProvider : IRunSeedProvider
        {
            public FixedPopulationRunSeedProvider(int runSeed)
            {
                RunSeed = runSeed;
            }

            public int RunSeed { get; }
        }
    }

    private sealed class ImmediateSkillGenerationService : ICharacterSkillGenerationService
    {
        public CharacterSkillDraft CreateDraft(
            CharacterProgression progression,
            CharacterSkillKind kind,
            int unlockLevel,
            int revision = 0)
        {
            int count = kind == CharacterSkillKind.Active ? 3 : 1;
            CharacterNarrativeRequestOwnerBinding ownerBinding =
                CharacterNarrativeRequestOwnerBinding.Capture(progression);
            Require(ownerBinding.Matches(progression, out string bindingFailure),
                "Population draft did not capture its live owner binding: "
                + bindingFailure);
            string requestKey =
                $"population-test:{ownerBinding.persistentId}:{kind}:{unlockLevel}:r{revision}:{ownerBinding.IdentitySuffix}";
            Require(requestKey.StartsWith(
                    $"population-test:{ownerBinding.persistentId}:",
                    StringComparison.Ordinal),
                "Population draft request key did not use the profile identity.");
            return new CharacterSkillDraft
            {
                kind = kind,
                unlockLevel = unlockLevel,
                requestKey = requestKey,
                ownerBinding = ownerBinding,
                rules = Enumerable.Range(0, count)
                    .Select(_ => new CharacterSkillCandidateRule { rarity = CharacterSkillRarity.Common, budget = 2 })
                    .ToList()
            };
        }

        public void RequestDraft(CharacterProgression progression, CharacterSkillDraft draft)
        {
            if (progression == null || draft == null || draft.isReady)
            {
                return;
            }

            int count = draft.kind == CharacterSkillKind.Active ? 3 : 1;
            draft.requestSubmitted = true;
            draft.candidates = Enumerable.Range(0, count)
                .Select(index => CreateSkill(draft, index))
                .ToList();
            draft.isReady = true;
            progression.OnDraftReady(draft);
        }

        public void CancelRequests(CharacterProgression progression)
        {
        }

        public bool TryValidateResponse(
            CharacterSkillDraft draft,
            string response,
            out List<CharacterSkillInstance> skills,
            out string error)
        {
            skills = null;
            error = "Not used by the immediate population test generator.";
            return false;
        }

        private static CharacterSkillInstance CreateSkill(CharacterSkillDraft draft, int index)
        {
            bool passive = draft.kind == CharacterSkillKind.Passive;
            return new CharacterSkillInstance
            {
                id = $"population-{draft.kind}-{draft.unlockLevel}-{index}",
                displayName = passive ? "생활 감각" : $"기본기 {index + 1}",
                description = "준비된 인물 풀 검증 기술",
                narrativeReason = "정체성과 경험을 바탕으로 익혔다.",
                kind = draft.kind,
                rarity = CharacterSkillRarity.Common,
                trigger = passive ? CharacterSkillTrigger.WorkCompleted : CharacterSkillTrigger.ManualCombat,
                target = passive ? CharacterSkillTarget.Self : CharacterSkillTarget.Enemy,
                modules = new List<CharacterSkillModuleSelection>
                {
                    new CharacterSkillModuleSelection
                    {
                        moduleId = passive ? "work_speed" : "damage",
                        variantId = passive ? "small" : "light"
                    }
                },
                requestKey = draft.requestKey
            };
        }
    }

    private sealed class ThrowOnceSkillGenerationService :
        ICharacterSkillGenerationService
    {
        public const string FailureMessage =
            "Injected population preparation draft failure.";

        private readonly ICharacterSkillGenerationService inner;
        private bool faultArmed = true;

        public ThrowOnceSkillGenerationService(
            ICharacterSkillGenerationService inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public Action<CharacterProgression> BeforeFailure { get; set; }
        public GameObject FailedPreviewObject { get; private set; }
        public int FailureCount { get; private set; }
        public int CancelCount { get; private set; }
        public int CancelCountAtFailure { get; private set; }

        public CharacterSkillDraft CreateDraft(
            CharacterProgression progression,
            CharacterSkillKind kind,
            int unlockLevel,
            int revision = 0)
        {
            if (faultArmed)
            {
                faultArmed = false;
                FailureCount++;
                CancelCountAtFailure = CancelCount;
                FailedPreviewObject = progression != null
                    ? progression.gameObject
                    : null;
                BeforeFailure?.Invoke(progression);
                throw new InvalidOperationException(FailureMessage);
            }

            return inner.CreateDraft(progression, kind, unlockLevel, revision);
        }

        public void RequestDraft(
            CharacterProgression progression,
            CharacterSkillDraft draft) => inner.RequestDraft(progression, draft);

        public void CancelRequests(CharacterProgression progression)
        {
            CancelCount++;
            inner.CancelRequests(progression);
        }

        public bool TryValidateResponse(
            CharacterSkillDraft draft,
            string response,
            out List<CharacterSkillInstance> skills,
            out string error) => inner.TryValidateResponse(
            draft,
            response,
            out skills,
            out error);

        public void ReleaseFault() => faultArmed = false;
    }

    private sealed class PopulationSettingsProvider : ICharacterSkillSystemSettingsProvider
    {
        public PopulationSettingsProvider(CharacterSkillSystemSettingsSO settings)
        {
            Settings = settings;
        }

        public CharacterSkillSystemSettingsSO Settings { get; }
    }
}
#endif
