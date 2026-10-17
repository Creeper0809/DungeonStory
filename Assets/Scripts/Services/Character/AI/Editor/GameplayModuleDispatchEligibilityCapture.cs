#if UNITY_EDITOR
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Observation of an accepted production dispatch, not a second eligibility
/// policy. A failed owner query produces no allowed witness. Pre-request gates,
/// model quality and training approval are outside this observation's scope.
/// </summary>
[Serializable]
internal sealed class GameplayModuleDispatchEligibilityWitness
{
    public string schema = "dungeonstory-module-dispatch-eligibility@1";
    public string scope = "current-owned-in-flight-request";
    public string decision = "allowed";
    public string reason = "PRODUCTION_DISPATCH_ACCEPTED";
    public string authority, profileId, correlationId, ownerId, targetId, requestId;
}

internal static class GameplayModuleDispatchEligibilityCapture
{
    internal static bool TryCaptureSkill(CharacterSkillGenerationService runtime, string correlationId,
        out CharacterProgression progression, out CharacterSkillDraft pending,
        out string prompt, out bool batch, out GameplayModuleDispatchEligibilityWitness eligibility)
    {
        eligibility = null;
        if (!runtime.TryCapturePendingModuleDispatch(correlationId,
                out progression, out pending, out prompt, out batch)) return false;
        string ownerId = progression.Actor.Identity.PersistentId;
        eligibility = Create(LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
            correlationId, ownerId, ownerId, pending.requestKey);
        return true;
    }

    internal static bool TryCaptureTrait(CharacterAcquiredTraitManifestationRuntime runtime, string correlationId,
        out CharacterProgression progression, out CharacterAcquiredTraitPendingRequestState pending,
        out string prompt, out NarrativePublicContextMaterialSnapshot publicContext,
        out GameplayModuleDispatchEligibilityWitness eligibility)
    {
        eligibility = null;
        if (!runtime.TryCapturePendingModuleDispatch(correlationId,
                out progression, out pending, out prompt, out publicContext)) return false;
        string ownerId = progression.Actor.Identity.PersistentId;
        eligibility = Create(LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
            correlationId, ownerId, ownerId, pending.requestId);
        return true;
    }

    internal static bool TryCaptureEquipment(EvolutionHistoryNarrativeRuntime runtime, string correlationId,
        out CombatEquipmentInstance instance, out EvolutionNode node,
        out EquipmentEvolutionPresentationRequest pending, out string prompt,
        out GameplayModuleDispatchEligibilityWitness eligibility)
    {
        eligibility = null;
        if (!runtime.TryCapturePendingModuleDispatch(correlationId,
                out instance, out node, out pending, out prompt)) return false;
        eligibility = Create(LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
            correlationId, pending.attunementOwnerPersistentId, instance.instanceId, pending.presentationId);
        return true;
    }

    internal static bool TryCaptureFacility(FacilityFormulaEvolutionPresentationRuntime runtime, string correlationId,
        out BuildableObject facility, out FacilityEvolutionFormulaPresentationPendingSnapshot pending,
        out string prompt, out GameplayModuleDispatchEligibilityWitness eligibility)
    {
        eligibility = null;
        if (!runtime.TryCapturePendingModuleDispatch(correlationId,
                out facility, out pending, out prompt)) return false;
        string ownerId = facility.PersistentInstanceId.Value;
        eligibility = Create(LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
            correlationId, ownerId, ownerId, pending.presentationId);
        return true;
    }

    // Only used after the exporter has verified the saved pending and exact
    // wire. Reconstruct an identity tuple, never a gameplay eligibility rule.
    internal static JObject RequireForExport(JObject witness, string ownerId, string targetId, string requestId)
    {
        var expected = Create(Read(witness, "profileId"), Read(witness, "correlationId"),
            ownerId, targetId, requestId);
        JObject expectedJson = JObject.Parse(JsonUtility.ToJson(expected));
        if (witness["dispatchEligibility"] is not JObject actual || !JToken.DeepEquals(actual, expectedJson))
            throw new InvalidOperationException("Missing or mismatched authoritative dispatch eligibility.");
        return (JObject)actual.DeepClone();
    }

    internal static void RequireSame(GameplayModuleDispatchEligibilityWitness before,
        GameplayModuleDispatchEligibilityWitness restored)
    {
        if (before == null || restored == null || JsonUtility.ToJson(before) != JsonUtility.ToJson(restored))
            throw new InvalidOperationException("Restored dispatch eligibility identity differs.");
    }

    private static GameplayModuleDispatchEligibilityWitness Create(string profile, string correlationId,
        string ownerId, string targetId, string requestId)
    {
        foreach (string value in new[] { profile, correlationId, ownerId, targetId, requestId })
            if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("Dispatch eligibility identity is missing or non-canonical.");
        string authority = profile == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id
            ? "CharacterSkillGenerationService.TryCapturePendingModuleDispatch"
            : profile == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id
                ? "CharacterAcquiredTraitManifestationRuntime.TryCapturePendingModuleDispatch"
            : profile == LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id
                ? "EvolutionHistoryNarrativeRuntime.TryCapturePendingModuleDispatch"
            : profile == LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id
                ? "FacilityFormulaEvolutionPresentationRuntime.TryCapturePendingModuleDispatch"
            : throw new InvalidOperationException("Unsupported dispatch eligibility profile: " + profile);
        return new GameplayModuleDispatchEligibilityWitness
        {
            authority = authority, profileId = profile, correlationId = correlationId,
            ownerId = ownerId, targetId = targetId, requestId = requestId
        };
    }

    private static string Read(JObject value, string key) => value[key]?.Type == JTokenType.String
        ? (string)value[key] : throw new InvalidOperationException("Missing dispatch identity: " + key);
}
#endif
