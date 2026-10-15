using System;
using Newtonsoft.Json.Linq;

internal static class CombatCellShotSaveJson
{
    internal static void Validate(string json)
    {
        var root = JObject.Parse(json);
        if (root["pendingCellShots"] is not JArray pending)
            throw new InvalidOperationException("Combat-command V3 pending cell shots are required.");
        foreach (var token in pending)
        {
            if (token is not JObject entry || entry["publicationKey"]?.Type != JTokenType.String
                || entry["fact"] is not JObject fact)
                throw new InvalidOperationException("Combat cell-shot entry is incomplete.");
            Require(JTokenType.String, "commandId", "actorId", "actorName", "weaponDefinitionId", "weaponInstanceId", "ammunitionItemId");
            Require(JTokenType.Integer, "attackRevision", "absoluteDay", "targetX", "targetY", "damageType", "fireMode",
                "ammunitionBefore", "ammunitionConsumed", "ammunitionAfter");
            Require(JTokenType.Boolean, "requiresAmmunition");
            foreach (string field in new[] { "manaBefore", "manaCost", "manaSpent", "manaAfter" })
                if (fact[field]?.Type is not (JTokenType.Float or JTokenType.Integer))
                    throw new InvalidOperationException("Combat cell-shot scalar missing: " + field);
            void Require(JTokenType type, params string[] fields)
            {
                foreach (string field in fields)
                    if (fact[field]?.Type != type) throw new InvalidOperationException("Combat cell-shot field missing: " + field);
            }
        }
    }
}
