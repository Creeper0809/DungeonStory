using System;

/// <summary>Exact numeric reader for section payloads and nested physical item JSON.</summary>
public static class DungeonSaveJson
{
    public static TPayload Deserialize<TPayload>(string json) where TPayload : class
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Save payload JSON is empty.");
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Culture = System.Globalization.CultureInfo.InvariantCulture,
            DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
            FloatParseHandling = Newtonsoft.Json.FloatParseHandling.Double,
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None,
            MetadataPropertyHandling = Newtonsoft.Json.MetadataPropertyHandling.Ignore,
            ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace,
            CheckAdditionalContent = true
        };
        using var text = new System.IO.StringReader(json);
        using var reader = new Newtonsoft.Json.JsonTextReader(text);
        return Newtonsoft.Json.JsonSerializer.Create(settings).Deserialize<TPayload>(reader)
            ?? throw new InvalidOperationException("Save payload JSON is null.");
    }
}
