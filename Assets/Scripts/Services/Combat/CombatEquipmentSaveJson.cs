using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Writes craft optionals from the owner's actual presence, not JsonUtility's default objects.</summary>
public static class CombatEquipmentSaveJson
{
    public static void ValidateCompletionFacts(string payloadJson)
    {
        var root = JObject.Parse(payloadJson);
        if (root["craftOrders"] is not JArray orders)
            throw new InvalidOperationException("Combat V13 craft orders are missing.");
        foreach (var order in orders)
        {
            RequireFields(order?["completionFact"], typeof(CombatEquipmentCraftCompletionFactData));
            if (order is not JObject value
                || value["qualityOutcomeSchemaVersion"]?.Type != JTokenType.Integer
                || value["qualityOutcomeCommitted"]?.Type != JTokenType.Boolean
                || value["qualityOutcomeResultKey"]?.Type != JTokenType.String
                || value["attemptOutcomeResolved"]?.Type != JTokenType.Boolean
                || value["outputOperationId"]?.Type != JTokenType.String
                || value["qualityAttemptIndex"]?.Type != JTokenType.Integer
                || value["resolvedMakerCharacterId"]?.Type != JTokenType.String
                || value["resolvedMakerDisplayName"]?.Type != JTokenType.String
                || value["definitionId"]?.Type != JTokenType.String
                || value["resolvedQuality"]?.Type != JTokenType.Integer
                || value["minimumQuality"]?.Type != JTokenType.Integer
                || value["resolvedAbsoluteDay"]?.Type != JTokenType.Integer)
                throw new InvalidOperationException("Combat quality observation fields are missing or malformed.");
        }
    }

    private static void RequireFields(JToken token, Type type)
    {
        if (token is not JObject value) throw new InvalidOperationException("Combat completion fact object is missing.");
        foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            var item = value[field.Name]; var fieldType = field.FieldType;
            bool valid = item != null && item.Type != JTokenType.Null;
            if (fieldType == typeof(string)) valid &= item?.Type == JTokenType.String;
            else if (fieldType == typeof(float)) valid &= item?.Type is JTokenType.Integer or JTokenType.Float;
            else if (fieldType.IsPrimitive || fieldType.IsEnum) valid &= item?.Type == JTokenType.Integer;
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                valid &= item is JArray;
                if (valid) foreach (var child in (JArray)item) RequireFields(child, fieldType.GetGenericArguments()[0]);
            }
            else if (valid) RequireFields(item, fieldType);
            if (!valid) throw new InvalidOperationException("Combat completion fact field is missing or malformed: " + field.Name);
        }
    }

    public static string Serialize(DungeonCombatEquipmentSaveData payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        JObject json = JObject.Parse(JsonUtility.ToJson(payload));
        if (payload.craftOrders != null)
        {
            JArray orders = json[nameof(payload.craftOrders)] as JArray
                ?? throw new InvalidOperationException("Combat craft JSON array is missing.");
            if (orders.Count != payload.craftOrders.Count)
                throw new InvalidOperationException("Combat craft JSON count changed.");
            for (int index = 0; index < payload.craftOrders.Count; index++)
                WriteAbsentOptionals((JObject)orders[index], payload.craftOrders[index]);
        }
        return json.ToString(Formatting.None);
    }

    public static string SerializeOrder(CombatEquipmentCraftOrderSaveData order)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));
        JObject json = JObject.Parse(JsonUtility.ToJson(order));
        WriteAbsentOptionals(json, order);
        return json.ToString(Formatting.None);
    }

    private static void WriteAbsentOptionals(
        JObject json,
        CombatEquipmentCraftOrderSaveData order)
    {
        if (order == null || json == null)
            throw new InvalidOperationException("Combat craft JSON order is null.");
        // Only fresh owner captures are written here. Non-null invalid objects
        // remain intact for strict restore rejection; old payloads are not repaired.
        if (order.outputPreparedComponent == null)
            json[nameof(order.outputPreparedComponent)] = JValue.CreateNull();
        if (order.resolvedMythicProvenance == null)
            json[nameof(order.resolvedMythicProvenance)] = JValue.CreateNull();
    }
}
