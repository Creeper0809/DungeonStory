using System;
using UnityEngine;

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

        return JsonUtility.FromJson<ModularFacilityWorldSaveData>(json)
            ?? throw new InvalidOperationException(
                "Modular facility save payload could not be deserialized.");
    }
}
