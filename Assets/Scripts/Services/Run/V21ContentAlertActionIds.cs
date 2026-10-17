using System;
using System.Collections.Generic;
using System.Linq;

public static class V21ContentAlertActionIds
{
    private const string Prefix = "v21-content";

    public static string Society(string instanceId, string choiceId) =>
        Join("society", instanceId, choiceId);

    public static string FactionChapter(string factionId, string choiceId) =>
        Join("faction-chapter", factionId, choiceId);

    public static string FactionContractAccept(
        string factionId,
        string contractId) =>
        Join("faction-contract-accept", factionId, contractId);

    public static string SeasonalFactionContractAccept(
        string occurrenceId,
        string contractId) =>
        Join("seasonal-faction-contract-accept", occurrenceId, contractId);

    public static string FactionContractOutcome(
        string factionId,
        bool succeeded) =>
        Join("faction-contract-outcome", factionId, succeeded ? "success" : "failure");

    public static string ReproductionStart(
        string processId,
        bool useFertilityTreatment = false) =>
        Join(
            "reproduction-start",
            processId,
            useFertilityTreatment ? "treatment" : "standard");

    public static string Festival(string festivalId) =>
        Join("festival", festivalId, "resolve");

    public static string Festival(string festivalId, int occurrenceYear) =>
        Join("festival", festivalId, $"resolve,{Math.Max(1, occurrenceYear)}");

    public static string FestivalSkip(string festivalId, int occurrenceYear) =>
        Join("festival", festivalId, $"skip,{Math.Max(1, occurrenceYear)}");

    public static string AgeTreatment(
        CharacterId patientId,
        AgeTreatmentKind treatment,
        string facilityInstanceId) =>
        Join(
            "age-treatment",
            patientId.Value,
            $"{(int)treatment},{facilityInstanceId?.Trim() ?? string.Empty}");

    public static string Funeral(CharacterId deceasedId) =>
        Join("social-care", deceasedId.Value, "funeral");

    public static string Counseling(CharacterId patientId) =>
        Join("social-care", patientId.Value, "counsel");

    public static string DiseaseResponse(
        CharacterId patientId,
        string diseaseId,
        string responseId,
        string facilityInstanceId) =>
        Join(
            "disease-response",
            patientId.Value,
            string.Join(",", diseaseId, responseId, facilityInstanceId));

    public static string CertifiedSeed(
        string cropId,
        string facilityInstanceId) =>
        Join("certified-seed", cropId, facilityInstanceId);

    public static string TraitAnalysis(CharacterId characterId) =>
        Join("trait-analysis", characterId.Value, "analyze");

    public static string CulturalPractice(
        string practiceId,
        IEnumerable<CharacterId> participantIds) =>
        Join(
            "cultural-practice",
            practiceId,
            string.Join(",", (participantIds ?? Array.Empty<CharacterId>())
                .Where(value => value.IsValid)
                .Select(value => value.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)));

    public static string CulturalPracticeNeglect(
        string practiceId,
        IEnumerable<CharacterId> participantIds) =>
        Join(
            "cultural-practice-neglect",
            practiceId,
            string.Join(",", (participantIds ?? Array.Empty<CharacterId>())
                .Where(value => value.IsValid)
                .Select(value => value.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)));

    public static bool TryParse(
        string actionId,
        out string kind,
        out string first,
        out string second)
    {
        kind = string.Empty;
        first = string.Empty;
        second = string.Empty;
        string[] segments = (actionId ?? string.Empty).Split('|');
        if (segments.Length != 4
            || !string.Equals(segments[0], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            kind = Uri.UnescapeDataString(segments[1]);
            first = Uri.UnescapeDataString(segments[2]);
            second = Uri.UnescapeDataString(segments[3]);
            return kind.Length > 0 && first.Length > 0 && second.Length > 0;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static string Join(string kind, string first, string second) =>
        string.Join(
            "|",
            Prefix,
            Uri.EscapeDataString(kind?.Trim() ?? string.Empty),
            Uri.EscapeDataString(first?.Trim() ?? string.Empty),
            Uri.EscapeDataString(second?.Trim() ?? string.Empty));
}
