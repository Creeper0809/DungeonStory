#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

[Serializable]
public sealed class FacilityUsageEvidenceBinding
{
    public string commandFile, publicFactId, evidenceKind, outcomeRunId;
    public long outcomeSequence;
}

/// <summary>
/// Private capture provenance, not a gameplay mutation or a character-memory
/// importance policy. Proves an observed visit origin, never its whole history.
/// </summary>
public static class LiveFacilityUsageEvidence
{
    public const string Kind = "facility-usage-operation@1";

    public static List<FacilityUsageEvidenceBinding> Create(string facilityId, long hour,
        JObject pending, JObject world, IReadOnlyDictionary<string, JObject> commands)
    {
        JObject state = ReadState(world, facilityId);
        var result = new List<FacilityUsageEvidenceBinding>();
        foreach (string factId in Array(pending["node"], "evidenceIds").Values<string>())
        {
            JObject record = Array(state, "formulaEvidence").OfType<JObject>()
                .SingleOrDefault(row => Text(row, "evidenceId") == factId);
            if (record == null || (string)record["originalEvent"]?["eventId"] != "facility.visit") continue;
            string operation = Text(record["originalEvent"]["narrativeContext"], "eventInstanceId");
            var origins = commands.SelectMany(command => Array(command.Value, "publishedOutcomes")
                .OfType<JObject>().Where(row => Text(row, "outcomeTypeId") == "service.facility-visited"
                    && Fact(row, "operation-id") == operation)
                .Select(row => new FacilityUsageEvidenceBinding
                {
                    commandFile = command.Key, publicFactId = factId, evidenceKind = Kind,
                    outcomeRunId = Text(row, "runId"), outcomeSequence = Number(row, "sequence")
                })).ToArray();
            Require(origins.Length <= 1, "Facility usage has ambiguous observed completion origins.");
            if (origins.Length == 1) result.Add(origins[0]);
        }
        Validate(facilityId, hour, pending, world, commands, JArray.FromObject(result));
        return result;
    }

    public static void Validate(string facilityId, long hour, JObject pending, JObject world,
        IReadOnlyDictionary<string, JObject> commands, JArray bindings)
    {
        Require(hour >= 0 && Text(pending, "sourceFacilityPersistentId") == facilityId,
            "Facility usage owner/clock mismatch.");
        JObject state = ReadState(world, facilityId);
        Require(Text(state, "facilityPersistentId") == facilityId, "Facility usage saved owner mismatch.");
        var selected = Array(pending["node"], "evidenceIds").Values<string>().ToArray();
        Require(selected.Length == selected.Distinct(StringComparer.Ordinal).Count(), "Duplicate facility evidence selection.");
        JObject ledger = Section(world, "world.gameplay-outcome-ledger");
        var seenFacts = new HashSet<string>(StringComparer.Ordinal);
        var seenOutcomes = new HashSet<string>(StringComparer.Ordinal);
        string[] fields = { "commandFile", "publicFactId", "evidenceKind", "outcomeRunId", "outcomeSequence" };
        foreach (JToken value in bindings)
        {
            Require(value is JObject, "Malformed facility usage binding.");
            var binding = (JObject)value;
            Require(binding.Properties().Select(p => p.Name).OrderBy(s => s, StringComparer.Ordinal)
                    .SequenceEqual(fields.OrderBy(s => s, StringComparer.Ordinal)), "Unexpected facility usage binding fields.");
            string name = Text(binding, "commandFile"), factId = Text(binding, "publicFactId");
            string run = Text(binding, "outcomeRunId");
            long sequence = Number(binding, "outcomeSequence");
            Require(Regex.IsMatch(name, @"\Acommand-[0-9]{3}\.json\z")
                && commands.ContainsKey(name) && Text(binding, "evidenceKind") == Kind
                && selected.Contains(factId) && seenFacts.Add(factId)
                && sequence > 0 && seenOutcomes.Add(run + "/" + sequence.ToString(CultureInfo.InvariantCulture)),
                "Invalid/duplicated/unselected facility usage binding.");
            JObject command = commands[name];
            string actor = Text(command, "actorId");
            long before = Number(command, "usesBefore"), after = Number(command, "usesAfter");
            long start = Number(command, "hourBefore"), end = Number(command, "hourAfter");
            Require(Text(command, "command") == "facility.interact" && Text(command, "facilityId") == facilityId
                && before >= 0 && after > before && start >= 0 && end >= start && end <= hour,
                "Facility usage command owner/count/clock mismatch.");
            JObject record = One(Array(state, "formulaEvidence"), row => Text(row, "evidenceId") == factId);
            JObject usage = record["originalEvent"] as JObject
                ?? throw new InvalidOperationException("Facility usage origin is missing.");
            long usageSequence = Number(usage, "sequence"), repeats = Number(usage, "repeatCount");
            Require(usageSequence > 0 && repeats > 0 && Number(usage, "historicalEvidenceKind") == 0
                && Number(usage, "generation") == Number(state, "generation"), "Unsupported facility usage origin.");
            string canonicalId;
            using (var hash = SHA256.Create())
                canonicalId = "facility-fact:" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                    facilityId + "|" + Text(usage, "evidenceId") + "|" + usageSequence.ToString(CultureInfo.InvariantCulture))))
                    .Replace("-", "").ToLowerInvariant();
            Require(factId == canonicalId && Text(record, "domainKey") == "facility"
                && Text(record, "eventGroupKey") == "facility.visit" && Text(record, "actionKey") == "facility.visit"
                && Text(record, "relationshipKey") == actor + ">" + facilityId
                && Text(usage, "eventId") == "facility.visit" && Text(usage, "actorId") == actor
                && Text(usage, "targetId") == facilityId, "Facility usage fact/hash/actor mismatch.");
            JObject saved = One(Array(state["usageLedger"], "currentGenerationEvents"), row =>
                Text(row, "evidenceId") == Text(usage, "evidenceId") && Number(row, "sequence") == usageSequence);
            Require(JToken.DeepEquals(saved, usage), "Selected facility usage differs from the official ledger.");
            JToken context = usage["narrativeContext"];
            string operation = Text(context, "eventInstanceId");
            Require(Text(context, "actorId") == actor && Text(context, "usedObjectId") == facilityId
                && Text(context, "locationId") == facilityId && Text(context, "chainId") == facilityId
                && Number(context, "sequence") == usageSequence
                && operation == "facility-visit:facility=" + facilityId + ":use=" + after.ToString(CultureInfo.InvariantCulture),
                "Facility usage operation/context mismatch.");
            JObject outcome = One(Array(command, "publishedOutcomes"), row => Text(row, "runId") == run
                && Number(row, "sequence") == sequence);
            JObject canonical = One(Array(ledger, "exactOutcomes"), row => Text(row, "runId") == run
                && Number(row, "sequence") == sequence);
            Require(JToken.DeepEquals(outcome, canonical), "Facility command outcome differs from official saved outcome.");
            long recorded = Number(outcome, "recordedAbsoluteHour"), day = Number(outcome, "absoluteDay");
            Require(Text(outcome, "outcomeTypeId") == "service.facility-visited"
                && Fact(outcome, "outcome-status") == "Completed" && Number(outcome, "status") == 1
                && Number(outcome, "deliveryMetadataVersion") == 1 && Number(outcome, "publishedRevision") > 0
                && recorded >= start && recorded <= end && day >= 0 && day <= hour / 24 + 1
                && Fact(outcome, "operation-id") == operation
                && Number(context, "occurredDay") >= 0 && Number(context, "occurredDay") <= day,
                "Facility completion status/clock/operation mismatch.");
            Require(Array(outcome, "participants").OfType<JObject>().Count(row =>
                    Text(row, "entityKindId") == "character" && Text(row, "entityId") == actor
                    && Text(row, "roleId") == "actor" && Number(row, "participationKind") == 1) == 1
                && Array(outcome, "participants").OfType<JObject>().Count(row =>
                    Text(row, "entityKindId") == "facility" && Text(row, "entityId") == facilityId
                    && Number(row, "participationKind") == 1) == 1
                && Array(outcome, "subjects").OfType<JObject>().Count(row =>
                    Text(row, "entityKindId") == "facility" && Text(row, "entityId") == facilityId) == 1,
                "Facility completion participant/subject mismatch.");
        }
    }

    private static JObject ReadState(JObject world, string id)
    {
        JObject building = One(Array(Section(world, "world.facilities"), "buildings"),
            row => Text(row, "persistentInstanceId") == id);
        JObject module = One(Array(building, "stateModules"), row => Text(row, "moduleId") == "facility.evolution");
        return JObject.Parse(Text(module, "payload"))["instanceEvolution"] as JObject
            ?? throw new InvalidOperationException("Facility evolution state is missing.");
    }

    // Exercise rejection using copies of the observed witness/command only.
    // The real game, saved world and pending request remain untouched.
    public static int VerifyCommandBoundaries(string id, long hour, JObject pending, JObject world,
        IReadOnlyDictionary<string, JObject> commands, JArray bindings)
    {
        Validate(id, hour, pending, world, commands, bindings);
        Require(bindings.Count > 0, "Facility boundary test needs an actual observed origin.");
        int checks = 1;
        void Reject(Action<JArray, Dictionary<string, JObject>> mutate)
        {
            var copy = (JArray)bindings.DeepClone();
            var copiedCommands = commands.ToDictionary(pair => pair.Key, pair => (JObject)pair.Value.DeepClone(), StringComparer.Ordinal);
            mutate(copy, copiedCommands);
            try { Validate(id, hour, pending, world, copiedCommands, copy); }
            catch (InvalidOperationException) { checks++; return; }
            throw new InvalidOperationException("Facility provenance accepted a tampered witness/command.");
        }
        Reject((rows, _) => rows.Add(rows[0].DeepClone()));
        Reject((rows, _) => rows[0]["evidenceKind"] = "exact");
        Reject((rows, _) => rows[0]["publicFactId"] = "facility-fact:unselected");
        Reject((rows, _) => rows[0]["commandFile"] = "../command-001.json");
        Reject((rows, _) => rows[0]["outcomeSequence"] = 0);
        Reject((rows, _) => rows[0]["outcomeRunId"] = "run:wrong");
        Reject((rows, _) => rows[0]["extra"] = true);
        string name = Text(bindings[0], "commandFile");
        Reject((_, copies) => copies[name]["actorId"] = "character:wrong");
        Reject((_, copies) => copies[name]["facilityId"] = "building:wrong");
        Reject((_, copies) => copies[name]["usesBefore"] = copies[name]["usesAfter"].DeepClone());
        Reject((_, copies) => copies[name]["hourAfter"] = -1);
        Reject((_, copies) => copies[name]["command"] = "fake.interact");
        Reject((_, copies) => copies[name]["publishedOutcomes"] = new JArray());
        return checks;
    }

    private static JObject Section(JObject world, string id) => JObject.Parse(Text(
        One(Array(world, "sections"), row => Text(row, "sectionId") == id), "payloadJson"));
    private static JObject One(JArray rows, Func<JObject, bool> predicate)
    {
        Require(rows.All(row => row is JObject), "Malformed facility provenance array.");
        JObject[] found = rows.OfType<JObject>().Where(predicate).ToArray();
        Require(found.Length == 1, "Missing/duplicated facility provenance identity.");
        return found[0];
    }
    private static string Fact(JToken row, string key) => Text(
        One(Array(row, "facts"), fact => Text(fact, "factId") == key), "value");
    private static string Text(JToken row, string key)
    {
        JToken value = row?[key];
        Require(value?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)value), "Missing facility provenance text: " + key);
        return (string)value;
    }
    private static long Number(JToken row, string key)
    {
        Require(row?[key]?.Type == JTokenType.Integer, "Missing facility provenance integer: " + key);
        return (long)row[key];
    }
    private static JArray Array(JToken row, string key) => row?[key] as JArray
        ?? throw new InvalidOperationException("Missing facility provenance array: " + key);
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
#endif
