#if UNITY_EDITOR
using System;
using System.Collections.Generic;

/// <summary>
/// Exercises only the registered-profile quality-gate text fields. Whether a
/// supplied module or evidence ID is semantically valid belongs to the domain
/// parser; these checks are not full replay/pass evidence.
/// </summary>
public static class NarrativeQualityContractFieldDebugScenarios
{
    private const string StableFactId = "public-fact:sha256:known";

    public static bool RunAll(bool log = true)
    {
        List<string> errors = new();
        Run("skill batch permits stable evidence ID", VerifySkillBatchEvidence, errors);
        Run("stable ID prose rejection", VerifyStableIdProseRejection, errors);
        Run("escaped stable ID prose rejection", VerifyEscapedStableIdRejection, errors);
        Run("reference prose validation", VerifyReferenceProseValidation, errors);
        Run("module-selection evidence IDs", VerifyModuleSelectionProfiles, errors);
        Run("persona flavor stable ID rejection", VerifyPersonaStableIdRejection, errors);
        Run("exact-key and text-type rejection", VerifyExactKeyAndTextTypeRejection, errors);

        if (errors.Count == 0)
        {
            if (log)
            {
                UnityEngine.Debug.Log(
                    "Narrative quality contract field scenarios PASS");
            }
            return true;
        }

        if (log)
        {
            UnityEngine.Debug.LogError(
                "Narrative quality contract field scenarios FAIL\n"
                + string.Join("\n", errors));
        }
        return false;
    }

    private static void VerifySkillBatchEvidence()
    {
        string response = Batch(SelectionItem());
        RequireExactAndAccepted(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection,
            response,
            "The exact six-key CharacterSkill batch rejected a stable evidence ID.");
    }

    private static void VerifyStableIdProseRejection()
    {
        LocalLlmRequestProfile profile =
            LocalLlmRequestProfiles.FacilityEvolutionModuleSelection;
        RequireRejected(
            profile,
            SelectionItem(displayName: "리아 " + StableFactId),
            "A stable ID in displayName was accepted.");
        RequireRejected(
            profile,
            SelectionItem(narrativeFlavor: "리아는 " + StableFactId + "을 말한다."),
            "A stable ID in narrativeFlavor was accepted.");
    }

    private static void VerifyEscapedStableIdRejection()
    {
        string escapedStableId = StableFactId
            .Replace("-", "\\u002d")
            .Replace(":", "\\u003a");
        RequireRejected(
            LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection,
            SelectionItem(narrativeFlavor: "리아는 " + escapedStableId + "을 말한다."),
            "An escaped stable ID in narrativeFlavor was accepted.");
    }

    private static void VerifyReferenceProseValidation()
    {
        LocalLlmRequestProfile profile =
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection;
        RequireRejected(
            profile,
            SelectionItem(narrativeFlavor: "F99의 사건을 되새긴다."),
            "Unknown F99 prose reference was accepted.");
        RequireExactAndAccepted(
            profile,
            SelectionItem(narrativeFlavor: "F01의 기록을 되새긴다."),
            "Known F01 prose reference was rejected.");
    }

    private static void VerifyModuleSelectionProfiles()
    {
        RequireExactAndAccepted(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection,
            Batch(SelectionItem()),
            "CharacterSkill batch did not permit stable evidenceFactIds.");
        RequireExactAndAccepted(
            LocalLlmRequestProfiles.FacilityEvolutionModuleSelection,
            SelectionItem(),
            "Facility module selection did not permit stable evidenceFactIds.");
        RequireExactAndAccepted(
            LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection,
            SelectionItem(),
            "Equipment module selection did not permit stable evidenceFactIds.");
        RequireExactAndAccepted(
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection,
            SelectionItem(),
            "Acquired-trait module selection did not permit stable evidenceFactIds.");
    }

    private static void VerifyPersonaStableIdRejection()
    {
        RequireRejected(
            LocalLlmRequestProfiles.Persona,
            "{\"personaName\":\"리아\",\"flavorText\":\""
            + StableFactId + "을 품은 인물이다.\"}",
            "A stable ID in Persona flavorText was accepted.");
    }

    private static void VerifyExactKeyAndTextTypeRejection()
    {
        RequireRejected(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection,
            "{\"candidates\":[" + SelectionItem() + "],\"unknown\":\"value\"}",
            "An unknown exact-key field was accepted.");
        RequireRejected(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection,
            "{\"candidates\":["
            + "{\"selectionId\":\"selection:quality\","
            + "\"positiveModuleIds\":[\"output\"],"
            + "\"drawbackModuleIds\":[],"
            + "\"evidenceFactIds\":[\"" + StableFactId + "\"],"
            + "\"displayName\":\"리아 감각\","
            + "\"displayName\":\"중복 감각\","
            + "\"narrativeFlavor\":\"리아의 반복된 경험에서 익힌 감각이다.\"}]}",
            "A duplicate JSON key was accepted.");
        RequireRejected(
            LocalLlmRequestProfiles.FacilityEvolutionModuleSelection,
            "{\"selectionId\":\"selection:quality\","
            + "\"positiveModuleIds\":[\"output\"],"
            + "\"drawbackModuleIds\":[],"
            + "\"evidenceFactIds\":[\"" + StableFactId + "\"],"
            + "\"displayName\":{\"text\":\"리아 감각\"},"
            + "\"narrativeFlavor\":\"리아의 반복된 경험에서 익힌 감각이다.\"}",
            "A nested object in a player-facing text field was accepted.");
    }

    private static void RequireExactAndAccepted(
        LocalLlmRequestProfile profile,
        string response,
        string message)
    {
        Require(NarrativeExactKeyContract.TryValidateProfileResponse(
                profile.Id,
                response,
                out _,
                out string exactError),
            message + " Exact-key error: " + exactError);
        NarrativeQualityResult result = Evaluate(profile, response);
        Require(result.IsAccepted, message + " Gate error: " + result.Error);
    }

    private static void RequireRejected(
        LocalLlmRequestProfile profile,
        string response,
        string message)
    {
        NarrativeQualityResult result = Evaluate(profile, response);
        Require(result.Verdict == NarrativeQualityVerdict.HardReject,
            message + " Verdict: " + result.Verdict + "; error: " + result.Error);
    }

    private static NarrativeQualityResult Evaluate(
        LocalLlmRequestProfile profile,
        string response)
    {
        NarrativeRequestContext context = new NarrativeRequestContext(
            profile.Id,
            "quality-fixture",
            requireCharacterFact: false,
            requireMotif: false);
        context.AddFact(StableFactId, "known event", priority: 100);
        return new NarrativeTextQualityGate().Evaluate(
            profile,
            context.AppendToPrompt("quality fixture"),
            response);
    }

    private static string Batch(string item) =>
        "{\"candidates\":[" + item + "]}";

    private static string SelectionItem(
        string displayName = "리아 감각",
        string narrativeFlavor = "리아의 반복된 경험에서 익힌 감각이다.") =>
        "{\"selectionId\":\"selection:quality\","
        + "\"positiveModuleIds\":[\"output\"],"
        + "\"drawbackModuleIds\":[],"
        + "\"evidenceFactIds\":[\"" + StableFactId + "\"],"
        + "\"displayName\":\"" + displayName + "\","
        + "\"narrativeFlavor\":\"" + narrativeFlavor + "\"}";

    private static void Run(
        string name,
        Action scenario,
        ICollection<string> errors)
    {
        try
        {
            scenario();
        }
        catch (Exception exception)
        {
            errors.Add(name + ": " + exception.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
