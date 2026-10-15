// Independent review regressions. This file is outside Assets and owns no
// runtime data, narrative wording, module choices, formula or save authority.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public static class Phase79EvidenceQualityEntry
{
    public static object VerifyExport(string sourcePath)
    {
        if (UnityEditor.EditorUtility.scriptCompilationFailed)
            throw new InvalidOperationException("Current Unity compilation failed.");
        // Resolve exactly the signed JSON library used by the export. The CLI
        // references a second embedded copy and Unity's native serializer does
        // not reliably populate these ephemeral nested DTOs.
        Type jsonConvert = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json, "
            + "Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed", throwOnError: true);
        var deserialize = jsonConvert.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) });
        Require(deserialize != null, "Known JSON deserializer is unavailable.");
        SourceRow[] rows = File.ReadLines(sourcePath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => (SourceRow)deserialize.Invoke(null, new object[] { line, typeof(SourceRow) })).ToArray();
        Require(rows.Length == 100, "Expected all100 source rows.");
        var profileCounts = rows.GroupBy(row => row.profileId)
            .ToDictionary(group => group.Key, group => group.Count());
        Require(profileCounts.Count == 4 && profileCounts.Values.All(count => count == 25),
            "Expected25 rows in each of four profiles.");

        var skillFacts = new HashSet<string>(StringComparer.Ordinal);
        var traitFacts = new HashSet<string>(StringComparer.Ordinal);
        var skillSourceSets = new HashSet<string>(StringComparer.Ordinal);
        var traitSourceSets = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> allTraitSources = null;
        var expeditionPlaceNames = new HashSet<string>(
            OffenseWorldMapService.CreateDefaultTargets().Select(target => target.title), StringComparer.Ordinal);
        int relationshipFacts = 0;
        int protectiveEquipmentFacts = 0;
        for (int index = 0; index < rows.Length; index++)
        {
            SourceRow row = rows[index];
            string profile = row.profileId;
            Provenance[] provenance = row.evidenceProvenance;
            var facts = row.moduleSelectionRequest.publicFacts.ToDictionary(
                fact => fact.factId, fact => fact.text, StringComparer.Ordinal);
            Require(facts.Count == provenance.Length, $"row{index + 1}: evidence count drift.");
            var contexts = provenance.Select(entry => entry.recordedTuple.eventContext)
                .OrderBy(context => context.sequence).ToArray();
            Require(contexts.Select(context => context.sequence).Distinct().Count() == contexts.Length,
                $"row{index + 1}: repeated event sequence.");
            for (int i = 1; i < contexts.Length; i++)
                Require(contexts[i].occurredDay >= contexts[i - 1].occurredDay,
                    $"row{index + 1}: event sequence runs backward through days.");

            foreach (Provenance entry in provenance)
            {
                RecordedTuple tuple = entry.recordedTuple;
                GameplayNarrativeEventContext context = tuple.eventContext;
                string fact = facts[entry.evidenceId];
                string prefix = $"시점: {context.occurredDay}일, 순서: {context.sequence}. "
                    + $"장소: {context.locationDisplayName}. 인물: {context.actorDisplayName}. ";
                foreach (var label in new[]
                {
                    ("상대", context.counterpartyDisplayName),
                    ("사용 대상", context.usedObjectDisplayName),
                    ("결과", context.resultDetail)
                })
                    if (!string.IsNullOrWhiteSpace(label.Item2))
                        prefix += label.Item1 + ": " + label.Item2 + ". ";
                prefix += "사건: ";
                Require(fact.StartsWith(prefix, StringComparison.Ordinal),
                    $"row{index + 1}: public context is not the recorded context.");
                if (tuple.domain == "Relationship")
                {
                    relationshipFacts++;
                    string counterpart = context.counterpartyId;
                    Require(!string.IsNullOrWhiteSpace(counterpart)
                            && counterpart.StartsWith("character:", StringComparison.Ordinal),
                        $"row{index + 1}: relationship counterpart is not a typed person.");
                    Require(context.counterpartyDisplayName != context.locationDisplayName
                            && !expeditionPlaceNames.Contains(context.counterpartyDisplayName),
                        $"row{index + 1}: relationship uses expedition place as counterpart.");
                }
                string sourceId = entry.sourceDefinitionId;
                if (sourceId == "equipment:shield-block" || sourceId == "equipment:armor-absorb")
                {
                    protectiveEquipmentFacts++;
                    string expectedResult = (sourceId == "equipment:shield-block"
                        ? "방어 성공, 차단량 " : "피해 흡수량 ")
                        + tuple.amount.ToString("0.##", CultureInfo.InvariantCulture);
                    Require(context.resultDetail == expectedResult,
                        $"row{index + 1}: protective result does not match recorded amount.");
                }
            }
            string fingerprint = string.Join("\n", facts.Values.OrderBy(text => text, StringComparer.Ordinal)
                .Select(text => Convert.ToBase64String(Encoding.UTF8.GetBytes(text))));
            var sourceIds = provenance.Select(value => value.sourceDefinitionId)
                .ToHashSet(StringComparer.Ordinal);
            string sourceSignature = string.Join("\n", sourceIds.OrderBy(value => value, StringComparer.Ordinal));
            if (profile == "CharacterSkillModuleSelection")
            {
                skillFacts.Add(fingerprint);
                skillSourceSets.Add(sourceSignature);
            }
            if (profile == "AcquiredTraitModuleSelection")
            {
                traitFacts.Add(fingerprint);
                traitSourceSets.Add(sourceSignature);
                if (allTraitSources == null) allTraitSources = new HashSet<string>(sourceIds);
                else allTraitSources.IntersectWith(sourceIds);
            }
        }
        Require(relationshipFacts > 0, "No Relationship event exercised the counterpart regression.");
        Require(protectiveEquipmentFacts > 0, "Protection equipment amount regression was not exercised.");
        Require(!skillFacts.Overlaps(traitFacts), "Skill and trait profiles reuse identical public fact sets.");
        Require(!skillSourceSets.Overlaps(traitSourceSets), "Renamed context conceals duplicate skill/trait source sets.");
        Require(allTraitSources != null && allTraitSources.Count == 0,
            "Every trait shares a mandatory event anchor; independent history sampling is not achieved.");
        return new
        {
            rowCount = rows.Length,
            profileCounts,
            relationshipFacts,
            protectiveEquipmentFacts,
            chronologyAndPublicContext = "PASS",
            characterTraitFactSetOverlap = 0,
            editorialApproval = false,
            humanApprovalClaimed = false,
            limitation = "Source/export assertions, not natural-play coverage or a final prose quality approval."
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // Typed read-only inspection DTOs. The exact signed serializer is resolved
    // above, without an AppDomain-wide type scan or ambiguous compile reference.
    [Serializable] private sealed class SourceRow
    {
        public string profileId;
        public SelectionRequest moduleSelectionRequest;
        public Provenance[] evidenceProvenance;
    }
    [Serializable] private sealed class SelectionRequest
    {
        public PublicFact[] publicFacts;
    }
    [Serializable] private sealed class PublicFact
    {
        public string factId;
        public string text;
    }
    [Serializable] private sealed class Provenance
    {
        public string evidenceId;
        public string sourceDefinitionId;
        public RecordedTuple recordedTuple;
    }
    [Serializable] private sealed class RecordedTuple
    {
        public string domain;
        public float amount;
        public GameplayNarrativeEventContext eventContext;
    }
}
