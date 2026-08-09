using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public sealed class SocialRumorPromptComposer
{
    private readonly IBuildingWorldQuery buildingWorldQuery;

    public SocialRumorPromptComposer(IBuildingWorldQuery buildingWorldQuery)
    {
        this.buildingWorldQuery = buildingWorldQuery
            ?? throw new ArgumentNullException(nameof(buildingWorldQuery));
    }

    public string BuildEventPrompt(
        CharacterActor speaker,
        CharacterLogEntry entry,
        BuildableObject explicitFacility,
        bool hasExplicitSentiment,
        float explicitSentiment,
        int maxCharacters)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Interpret a DungeonStory NPC event as social rumor/reputation data.");
        builder.AppendLine("Return exactly one JSON object with rumorType, targetType, targetFacilityId, targetFacilityTag, targetCharacterId, targetCharacterName, sentiment, summary, spreadChance, trustImpact, validSeconds.");
        builder.AppendLine("Allowed rumorType values: None, Complaint, Recommendation, Warning, Praise.");
        builder.AppendLine("Allowed targetType values: None, Facility, Character.");
        builder.AppendLine("All numeric fields must be raw JSON numbers, never strings, words, or null.");
        builder.AppendLine("targetFacilityId must be an integer. targetCharacterId must be a persistent-ID string. Use -1 and an empty string when not used. Never output null.");
        builder.AppendLine("sentiment and trustImpact must be numbers between -1 and 1. spreadChance must be a number between 0 and 1.");
        builder.AppendLine("validSeconds must be a number between 0 and 1800. Use 600 for normal rumors. Never output 3600 or higher.");
        builder.AppendLine("Actionable Complaint, Recommendation, Warning, and Praise rumors must use spreadChance between 0.35 and 1.0.");
        builder.AppendLine("For blocked path, no destination, or occupied destination warnings, use spreadChance 1.0.");
        builder.AppendLine("Use rumorType None only when no NPC should share this event.");
        builder.AppendLine("Use rumorType None and targetType None when this event should not become a rumor. Do not invent targets outside candidateFacilities.");
        if (explicitFacility != null)
        {
            builder.AppendLine("This request has exactly one allowed facility target.");
            builder.AppendLine($"The only valid facility target is id={explicitFacility.id}, tag={SocialRumorUtility.GetFacilityTag(explicitFacility)}.");
            builder.AppendLine("Because this event is a facility experience, output targetType Facility unless rumorType is None.");
            builder.AppendLine("Do not output targetType Character for facility experiences.");
            builder.AppendLine("If you output targetType Facility, targetFacilityId must equal that id. Any other facility target is invalid.");
            builder.AppendLine($"Required target fields for this event: \"targetType\":\"Facility\", \"targetFacilityId\":{explicitFacility.id}, \"targetCharacterId\":\"\", \"targetCharacterName\":\"\".");
            if (hasExplicitSentiment)
            {
                builder.AppendLine($"Reported experience sentiment is {explicitSentiment:0.00}; output sentiment must keep the same sign.");
                builder.AppendLine(explicitSentiment >= 0f
                    ? "For positive facility experiences, use Recommendation or Praise and a positive sentiment."
                    : "For negative facility experiences, use Complaint or Warning and a negative sentiment.");
            }
        }

        builder.AppendLine("Speaker:");
        builder.AppendLine($"name: {SocialRumorUtility.GetActorLabel(speaker)}");
        builder.AppendLine($"species: {(speaker != null ? speaker.SpeciesTag : string.Empty)}");
        builder.AppendLine($"role: {(speaker != null ? speaker.Role.ToString() : string.Empty)}");
        builder.AppendLine("Event:");
        builder.AppendLine($"tag: {entry.Tag}");
        builder.AppendLine($"count: {entry.Count}");
        builder.AppendLine($"message: {entry.OriginalMessage}");
        builder.AppendLine("candidateFacilities:");
        AppendCandidateFacilities(builder, speaker, explicitFacility);
        builder.AppendLine("Example: {\"rumorType\":\"Recommendation\",\"targetType\":\"Facility\",\"targetFacilityId\":12,\"targetFacilityTag\":\"Rest\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":0.6,\"summary\":\"rest facility visit was good\",\"spreadChance\":0.55,\"trustImpact\":0.05,\"validSeconds\":600}");
        return NarrativeRequestContextBuilder.ForActor(
                LocalLlmRequestProfiles.SocialRumor.Id,
                speaker,
                requireCharacterFact: false,
                requireMotif: false)
            .AppendToPrompt(Truncate(builder, maxCharacters));
    }

    public string BuildFacilityExperiencePrompt(
        CharacterActor speaker,
        BuildableObject facility,
        string eventName,
        float sentiment,
        string summary,
        int maxCharacters)
    {
        string rumorTypeHint = sentiment >= 0f ? "Recommendation" : "Complaint";
        string facilityTag = SocialRumorUtility.GetFacilityTag(facility);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Create one shareable NPC facility rumor from this facility experience.");
        builder.AppendLine("Return exactly one compact JSON object and no markdown.");
        builder.AppendLine("Do not invent another target. Do not output targetType Character.");
        builder.AppendLine("All numeric fields must be raw JSON numbers, never strings, words, or null.");
        builder.AppendLine($"The rumorType should be {rumorTypeHint} unless the event clearly fits Praise or Warning.");
        builder.AppendLine("targetType must be Facility.");
        builder.AppendLine($"targetFacilityId must be {facility.id}.");
        builder.AppendLine($"targetFacilityTag must be \"{facilityTag}\".");
        builder.AppendLine("targetCharacterId and targetCharacterName must both be empty strings.");
        builder.AppendLine(sentiment >= 0f
            ? "sentiment must be a positive number between 0.35 and 1."
            : "sentiment must be a negative number between -1 and -0.35.");
        builder.AppendLine("spreadChance must be 1.0 so nearby listeners can deterministically hear this facility experience.");
        builder.AppendLine("trustImpact must be a number between -1 and 1.");
        builder.AppendLine("validSeconds must be 600.");
        builder.AppendLine("Required JSON shape:");
        builder.AppendLine(
            $"{{\"rumorType\":\"{rumorTypeHint}\",\"targetType\":\"Facility\",\"targetFacilityId\":{facility.id},\"targetFacilityTag\":\"{facilityTag}\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":{(sentiment >= 0f ? "0.75" : "-0.75")},\"summary\":\"short text\",\"spreadChance\":1.0,\"trustImpact\":0.1,\"validSeconds\":600}}");
        builder.AppendLine("Speaker:");
        builder.AppendLine($"name: {SocialRumorUtility.GetActorLabel(speaker)}");
        builder.AppendLine($"species: {(speaker != null ? speaker.SpeciesTag : string.Empty)}");
        builder.AppendLine("Facility:");
        builder.AppendLine($"id: {facility.id}");
        builder.AppendLine($"name: {SocialRumorUtility.GetFacilityLabel(facility)}");
        builder.AppendLine($"tag: {facilityTag}");
        builder.AppendLine("Experience:");
        builder.AppendLine($"eventName: {eventName}");
        builder.AppendLine($"reportedSentiment: {sentiment:0.00}");
        builder.AppendLine($"summary: {summary}");
        return NarrativeRequestContextBuilder.ForActor(
                LocalLlmRequestProfiles.SocialRumor.Id,
                speaker,
                requireCharacterFact: false,
                requireMotif: false)
            .AppendToPrompt(Truncate(builder, maxCharacters));
    }

    public BuildableObject ResolveFacility(CharacterLogEntry entry)
    {
        int facilityId = entry.Activity != null ? entry.Activity.FacilityId : -1;
        return facilityId < 0
            ? null
            : buildingWorldQuery.Buildings.FirstOrDefault(
                building => building != null && building.id == facilityId);
    }

    private void AppendCandidateFacilities(
        StringBuilder builder,
        CharacterActor speaker,
        BuildableObject explicitFacility)
    {
        if (explicitFacility != null)
        {
            AppendFacilityLine(builder, explicitFacility);
            return;
        }

        foreach (BuildableObject building in FindNearbyFacilities(speaker)
                     .Where(building => building != null)
                     .Take(8))
        {
            AppendFacilityLine(builder, building);
        }
    }

    private IEnumerable<BuildableObject> FindNearbyFacilities(CharacterActor speaker)
    {
        IReadOnlyList<BuildableObject> buildings = buildingWorldQuery.Buildings;
        if (speaker == null)
        {
            return buildings.Where(building => building != null);
        }

        Vector3 speakerPosition = speaker.transform.position;
        return buildings
            .Where(building => building != null)
            .OrderBy(building => (building.transform.position - speakerPosition).sqrMagnitude);
    }

    private static void AppendFacilityLine(StringBuilder builder, BuildableObject building)
    {
        string label = SocialRumorUtility.GetFacilityLabel(building);
        string tag = SocialRumorUtility.GetFacilityTag(building);
        builder.AppendLine($"- id={building.id}; name={label}; tag={tag}");
    }

    private static string Truncate(StringBuilder builder, int maxCharacters)
    {
        string prompt = builder.ToString();
        return prompt.Length > maxCharacters
            ? prompt.Substring(0, maxCharacters)
            : prompt;
    }
}
