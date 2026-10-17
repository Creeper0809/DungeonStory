// Controlled regression of the real progression snapshot/prompt boundary.
// Does not mutate authored assets, user saves, or submit an LLM response.
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Phase79TraitPendingEntry
{
    public static object VerifyLegacyPending()
    {
        Require(!EditorUtility.scriptCompilationFailed, "Current compilation failed.");
        var settings = AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitSettingsSO>(
            "Assets/Resources/SO/V25/AcquiredTraits/acquired-trait-settings.asset");
        var modules = AssetDatabase.FindAssets("t:CharacterAcquiredTraitModuleSO",
                new[] { "Assets/Resources/SO/V25/AcquiredTraits/Modules" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitModuleSO>)
            .Where(value => value != null).OrderBy(value => value.ModuleId, StringComparer.Ordinal).ToArray();
        Require(settings != null && modules.Length > 0, "Authored content is missing.");
        string assetsBefore = AssetFingerprint(settings, modules);
        const string targetId = "character:qa-phase79-trait-pending";
        using var sourceFixture = new ExistingActorFixture(87901, targetId);
        using var restoredFixture = new ExistingActorFixture(87902, targetId);
        CharacterProgression source = sourceFixture.Actor.Progression;
        CharacterProgression restored = restoredFixture.Actor.Progression;
        // Distinct fixture facts exercise the existing milestone service. They are
        // not exported or described as organically collected gameplay evidence.
        string[] factIds = { "work:qa-phase79-a", "work:qa-phase79-b", "work:qa-phase79-c" };
        foreach (string factId in factIds)
            source.RecordNarrative(CharacterNarrativeDomain.Work, factId,
                "target:qa:phase79", "completed", 1f, 1);
        Require(CharacterAcquiredTraitExperienceScore.Require(source.NarrativeLedger) == 3,
            "Fixture must reach exactly the first manifestation milestone.");
        NarrativePublicContextMaterial material =
            CharacterAcquiredTraitPromptBuilder.BuildPublicMaterialForLedgerEvidence(source, factIds);
        var projected = CharacterAcquiredTraitPromptBuilder.BuildProjectedEvidenceFactIds(material, factIds);
        var command = new CharacterAcquiredTraitSubmissionCommand(targetId,
            "request:qa:phase79-trait", "request-key:qa:phase79-trait", 3, projected,
            source.CaptureAcquiredTraitState().revision, NarrativeInferenceTimestamp.FromGameTick(3));
        var service = new CharacterAcquiredTraitInferenceService(settings, modules);
        var submitted = service.SubmitMilestone(source, command);
        Require(submitted.Succeeded && submitted.Packet != null, "Milestone submission failed.");
        var packet = submitted.Packet;
        var saved = source.CapturePersistentState();
        var pending = saved.AcquiredTraitState.pendingRequests.Single();
        Require(pending.presentationState == CharacterAcquiredTraitPresentationState.ModuleSelectionPending,
            "Expected an unresolved module selection, not committed mechanics.");
        Require(saved.AcquiredTraitState.ActiveCount == 0
                && saved.AcquiredTraitState.processedMilestones.Count == 0,
            "Submitting a pending request must not acquire a trait or consume its milestone.");

        string sourceBefore = Fingerprint(source);
        var baselineMaterial = CharacterAcquiredTraitPromptBuilder.BuildPublicMaterial(source, packet);
        string baselinePrompt = CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
            source, packet, baselineMaterial, settings, modules).Prompt;
        const string selectedId = "acquired-trait:module:work-rhythm";
        Require(pending.offeredBenefitModuleIds.Contains(selectedId), "Expected authored work-rhythm offer.");
        var choice = new NarrativeFormulaModuleSelectionChoice(pending.moduleSelectionId,
            new[] { selectedId }, Array.Empty<string>(), pending.evidenceFactIds);
        string baselineAllocation = JsonUtility.ToJson(CharacterAcquiredTraitFormulaGeneration.FreezeSelected(
            source, pending.requestId, pending.requestKey, pending.manifestationMilestone,
            settings, modules, pending.evidenceFactIds, choice));
        Require(Fingerprint(source) == sourceBefore, "Prompt/allocation preview changed source progression.");

        var legacy = saved.AcquiredTraitState.Clone();
        legacy.pendingRequests.Single().moduleOffers.First().semanticDescription =
            "레거시 검증용 문구: 경험치를 2 얻고 작업 속도가 12% 증가한다.";
        // Actual serializable aggregate boundary, followed by the public restore.
        // This is deliberately not an assertion about the entire game save file.
        legacy = JsonUtility.FromJson<CharacterAcquiredTraitAggregateState>(JsonUtility.ToJson(legacy));
        restored.RestorePersistentState(new CharacterProgressionSnapshot(saved.Level,
            saved.CurrentExperience, saved.GrowthState, saved.NarrativeLedger, legacy));
        string restoredBefore = Fingerprint(restored);
        var restoredMaterial = CharacterAcquiredTraitPromptBuilder.BuildPublicMaterial(restored, packet);
        ExpectRejected(() => CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
                restored, packet, restoredMaterial),
            "Numeric persisted prose was accepted without authority.");
        ExpectRejected(() => CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
                restored, packet, restoredMaterial, settings, null),
            "Partial authority was accepted.");
        string rebuilt = CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
            restored, packet, restoredMaterial, settings, modules).Prompt;
        Require(rebuilt == baselinePrompt && !rebuilt.Contains("레거시 검증용 문구"),
            "Restored prompt did not reconstruct exactly the current authored semantics.");
        string restoredAllocation = JsonUtility.ToJson(CharacterAcquiredTraitFormulaGeneration.FreezeSelected(
            restored, pending.requestId, pending.requestKey, pending.manifestationMilestone,
            settings, modules, pending.evidenceFactIds, choice));
        Require(restoredAllocation == baselineAllocation,
            "Legacy prose reconstruction changed the final C# allocation.");
        Require(Fingerprint(restored) == restoredBefore,
            "Prompt success/failure changed saved effects, evidence influence, cost, or revision.");

        var badPacket = JsonUtility.FromJson<CharacterAcquiredTraitRequestPacketDto>(JsonUtility.ToJson(packet));
        badPacket.candidatePacketHash = NarrativeInferenceHash.ComputeSha256Utf8("phase79-packet-tamper");
        ExpectRejected(() => CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
                restored, badPacket, restoredMaterial, settings, modules),
            "Tampered packet binding was accepted.");
        Require(Fingerprint(restored) == restoredBefore, "Rejected packet changed progression.");

        var badState = restored.CapturePersistentState();
        badState.AcquiredTraitState.pendingRequests.Single().moduleSelectionId = "selection:trait:tampered";
        bool rejectedAtRestore = false;
        try { restored.RestorePersistentState(badState); }
        catch (InvalidOperationException) { rejectedAtRestore = true; }
        if (rejectedAtRestore)
            Require(Fingerprint(restored) == restoredBefore, "Failed restore partially changed progression.");
        else
        {
            string tamperedBefore = Fingerprint(restored);
            ExpectRejected(() => CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
                    restored, packet, restoredMaterial, settings, modules),
                "Tampered selection identity was accepted.");
            Require(Fingerprint(restored) == tamperedBefore, "Failed revalidation changed progression.");
        }
        Require(AssetFingerprint(settings, modules) == assetsBefore, "Verification mutated authored assets.");
        return new
        {
            status = "PASS",
            formulaVersion = pending.formulaVersion,
            numericLegacyProseRestoredQualitatively = true,
            originalSelectionAndCsharpAllocationPreserved = true,
            pendingStateUnchangedByPromptAndPreview = true,
            absentOrPartialAuthorityRejected = true,
            alteredPacketAndSelectionRejected = true,
            limitation = "Controlled aggregate JSON/progression snapshot regression, not whole-game save or natural play."
        };
    }

    private static string AssetFingerprint(CharacterAcquiredTraitSettingsSO settings,
        CharacterAcquiredTraitModuleSO[] modules) => EditorJsonUtility.ToJson(settings)
        + string.Join("\n", modules.Select(value => EditorJsonUtility.ToJson(value)));

    private static string Fingerprint(CharacterProgression progression)
    {
        var value = progression.CapturePersistentState();
        return value.Level + "|" + value.CurrentExperience + "|"
            + JsonUtility.ToJson(value.GrowthState) + "|" + JsonUtility.ToJson(value.NarrativeLedger)
            + "|" + JsonUtility.ToJson(value.AcquiredTraitState);
    }

    private static void ExpectRejected(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ExistingActorFixture : IDisposable
    {
        private readonly IDisposable fixture;
        public CharacterActor Actor { get; }

        public ExistingActorFixture(int id, string persistentId)
        {
            // Reuse only the known Editor fixture's composition setup. Assertions
            // and oracle values above are independent of its existing tests.
            Type fixtureType = typeof(CharacterProgressionDebugScenarios)
                .GetNestedType("ActorFixture", BindingFlags.NonPublic);
            Require(fixtureType != null, "Known ActorFixture is missing.");
            object instance = Activator.CreateInstance(fixtureType,
                new object[] { id, "후천 특성 검증자", "human", false, persistentId });
            fixture = (IDisposable)instance;
            Actor = (CharacterActor)fixtureType.GetProperty("Actor").GetValue(instance);
        }

        public void Dispose() => fixture.Dispose();
    }
}
