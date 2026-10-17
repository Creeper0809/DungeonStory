using System;
using Newtonsoft.Json.Linq;

internal static class FactionCargoSaveValidation
{
    internal static void ValidateJson(string json)
    {
        var root = JObject.Parse(json);
        if (root["version"]?.Type != JTokenType.Integer
            || root["version"].Value<int>() != DungeonStory.Factions.DungeonFactionSaveData.CurrentVersion
            || root["relationshipObservationSequence"]?.Type != JTokenType.Integer
            || root["relationshipObservations"] is not JArray relationships)
            throw new InvalidOperationException("faction-relationship-observation-json-missing");
        foreach (var row in relationships)
        {
            if (row is not JObject observation || observation["transitions"] is not JArray transitions)
                throw new InvalidOperationException("faction-relationship-frozen-json-invalid");
            foreach (var field in typeof(DungeonStory.Factions.FactionRelationshipObservationData).GetFields())
            {
                if (field.Name == "transitions") continue;
                JToken token = observation[field.Name];
                bool valid = field.FieldType == typeof(string) ? token?.Type == JTokenType.String
                    : token?.Type == JTokenType.Integer;
                if (!valid) throw new InvalidOperationException("faction-relationship-frozen-field-missing:" + field.Name);
            }
            foreach (var entry in transitions)
            {
                if (entry is not JObject item) throw new InvalidOperationException("faction-relationship-transition-json-invalid");
                foreach (var field in typeof(DungeonStory.Factions.FactionRelationshipTrustData).GetFields())
                    if (field.FieldType == typeof(string) ? item[field.Name]?.Type != JTokenType.String
                        : item[field.Name]?.Type != JTokenType.Integer)
                        throw new InvalidOperationException("faction-relationship-transition-field-missing:" + field.Name);
            }
        }
        if (root["factions"] is not JArray factions) throw new InvalidOperationException("faction-observation-factions-missing");
        foreach (var value in factions)
        {
            if (value is not JObject faction || faction["goodwillObserved"]?.Type != JTokenType.Boolean
                || faction["goodwillObservationDay"]?.Type != JTokenType.Integer || faction["goodwillTransferRevision"]?.Type != JTokenType.Integer)
                throw new InvalidOperationException("faction-goodwill-observation-fields-missing");
            foreach (string field in new[] { "goodwillPublicationKey", "goodwillPublicationId", "goodwillPublicationFingerprint", "goodwillFactionDisplayName" })
                if (faction[field]?.Type != JTokenType.String) throw new InvalidOperationException("faction-goodwill-field-missing:" + field);
        }
        if (root["routes"] is not JArray routes) throw new InvalidOperationException("faction-cargo-routes-missing");
        foreach (var value in routes)
        {
            if (value is not JObject route || route["cargoObservationDay"]?.Type != JTokenType.Integer)
                throw new InvalidOperationException("faction-cargo-observation-day-missing");
            foreach (string field in new[] { "cargoPublicationKey", "cargoPublicationId", "cargoPublicationFingerprint", "cargoFactionDisplayName",
                "settlementPublicationKey", "settlementPublicationId", "settlementPublicationFingerprint", "arrivalPublicationKey", "arrivalPublicationId", "arrivalPublicationFingerprint" })
                if (route[field]?.Type != JTokenType.String) throw new InvalidOperationException("faction-cargo-field-missing:" + field);
            foreach (string phase in new[] { "settlement", "arrival" })
                if (route[phase + "Observation"] is not JObject observation
                    || observation["absoluteDay"]?.Type != JTokenType.Integer || observation["operationId"]?.Type != JTokenType.String
                    || observation["before"] is not JObject || observation["after"] is not JObject || observation["settlement"] is not JObject)
                    throw new InvalidOperationException("faction-route-frozen-observation-missing:" + phase);
        }
    }
}
