using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// Current schema only. Missing fields must not silently become valid defaults.
internal static class FacilityUseSaveValidation
{
    internal static void ValidateJson(string json)
    {
        var root = JObject.Parse(json);
        if (root["nextUseOperationSequence"]?.Type != JTokenType.Integer
            || root["pendingUses"] is not JArray pending
            || root["acknowledgedUseRanges"] is not JArray acknowledged)
            throw new InvalidOperationException("facility-use-save-fields-missing");
        foreach (var item in pending)
        {
            if (item is not JObject entry) throw new InvalidOperationException("facility-use-pending-null");
            foreach (var field in typeof(FacilityUseCommitState).GetFields())
            {
                JToken value = entry[field.Name];
                Type type = field.FieldType;
                bool valid = type == typeof(string) ? value?.Type == JTokenType.String
                    : type == typeof(bool) ? value?.Type == JTokenType.Boolean
                    : type == typeof(float) ? value?.Type is JTokenType.Integer or JTokenType.Float
                    : type == typeof(int) || type == typeof(long) ? value?.Type == JTokenType.Integer
                    : type == typeof(List<string>) && value is JArray;
                if (!valid) throw new InvalidOperationException("facility-use-pending-field-invalid:" + field.Name);
                if (value is JArray list)
                    foreach (var element in list)
                        if (element.Type != JTokenType.String) throw new InvalidOperationException("facility-use-pending-list-invalid:" + field.Name);
            }
            foreach (string obsolete in new[] { "parentProducerId", "parentOperationId", "parentCommitRevision", "parentLocalResultIndex" })
                if (entry[obsolete] != null) throw new InvalidOperationException("facility-use-obsolete-native-metadata");
        }
        foreach (var item in acknowledged)
            if (item is not JObject range || range["first"]?.Type != JTokenType.Integer || range["last"]?.Type != JTokenType.Integer)
                throw new InvalidOperationException("facility-use-acknowledgement-invalid");
    }
}
