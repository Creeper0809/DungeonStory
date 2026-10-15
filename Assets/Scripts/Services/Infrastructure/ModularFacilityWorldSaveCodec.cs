using System;
using UnityEngine;
using Newtonsoft.Json.Linq;

public static class ModularFacilityWorldSaveCodec
{
    public static string Serialize(
        ModularFacilityWorldSaveData snapshot,
        bool prettyPrint = false)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        return JsonUtility.ToJson(snapshot, prettyPrint);
    }

    public static ModularFacilityWorldSaveData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                "Modular facility save payload is empty.");
        }

        ModularFacilitySaveVersionHeader header =
            JsonUtility.FromJson<ModularFacilitySaveVersionHeader>(json);
        if (header == null
            || header.version != ModularFacilityWorldSaveService.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported modular facility save version {header?.version ?? 0}; "
                + $"V{ModularFacilityWorldSaveService.CurrentVersion} is required by the V18 save generation.");
        }

        ValidateSpatialPublicationJson(json);
        return JsonUtility.FromJson<ModularFacilityWorldSaveData>(json)
            ?? throw new InvalidOperationException(
                "Modular facility save payload could not be deserialized.");
    }

    internal static void ValidateSpatialPublicationJson(string json)
    {
        JObject root = JObject.Parse(json);
        if (root["spaceExpansionPublication"] is not JObject pending
            || pending["active"]?.Type != JTokenType.Boolean
            || pending["publicationKey"]?.Type != JTokenType.String)
            throw new InvalidOperationException("Spatial publication fields are missing or malformed.");
        if (!(bool)pending["active"]) return;
        foreach (var field in typeof(DungeonSpaceExpansionPublicationData).GetFields())
        {
            JTokenType expected = field.FieldType == typeof(string) ? JTokenType.String
                : field.FieldType == typeof(bool) ? JTokenType.Boolean : JTokenType.Integer;
            if (pending[field.Name]?.Type != expected)
                throw new InvalidOperationException("Spatial publication field is missing or malformed: " + field.Name);
        }
    }
}
