using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Read-only reinterpretation of historical exact receipts, not fresh gameplay/training data.</summary>
public static class GameplayExperienceHistoricalReplay
{
    public const string Source = "Artifacts/QA/AuditRemediation-20260926-r1/u0-matrix-r597/b1-review-all91-export-a-r1/samples.jsonl";

    public static JObject Run()
    {
        byte[] bytes = File.ReadAllBytes(Source);
        string hash;
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        var results = new JArray();
        int snapshots = 0;
        foreach (string line in File.ReadLines(Source))
        {
            var sample = JObject.Parse(line);
            foreach (var snapshot in Objects(sample["evidence"]).Where(value => value["immutablePayloadHash"] != null
                && value["storageTier"]?.Value<int>() == 1 && value["subjects"] is JArray && value["initialSubjects"] is JArray))
            {
                snapshots++;
                string original = snapshot.ToString(Formatting.None);
                foreach (JObject subject in snapshot["subjects"].OfType<JObject>())
                {
                    var binding = new GameplayOutcomeEvidenceBindingSnapshot
                    {
                        schemaVersion = GameplayOutcomeEvidenceFormulaProjection.CurrentBindingSchemaVersion,
                        sourceKind = "exact", publicFactId = "historical-policy-replay:" + snapshots,
                        outcomeRunId = (string)snapshot["runId"], outcomeSequence = (long)snapshot["sequence"],
                        outcomeTypeId = (string)snapshot["outcomeTypeId"], status = (int)snapshot["status"],
                        subjectKindId = (string)subject["entityKindId"], subjectId = (string)subject["entityId"],
                        subjectSalience = (float)subject["salience"],
                        participants = snapshot["participants"].ToObject<List<GameplayOutcomeEvidenceParticipantBindingSnapshot>>(),
                        metrics = snapshot["metrics"].ToObject<List<GameplayOutcomeEvidenceMetricBindingSnapshot>>(),
                        facts = snapshot["facts"].ToObject<List<GameplayOutcomeEvidenceFactBindingSnapshot>>()
                    };
                    var assessment = GameplayExperienceObserverRegistration.Default.Observe(binding);
                    results.Add(new JObject
                    {
                        ["sampleId"] = sample["sampleId"], ["snapshotPath"] = snapshot.Path,
                        ["outcomeType"] = binding.outcomeTypeId, ["runId"] = binding.outcomeRunId,
                        ["sequence"] = binding.outcomeSequence, ["subjectKind"] = binding.subjectKindId,
                        ["subjectId"] = binding.subjectId, ["assessment"] = JObject.FromObject(assessment)
                    });
                }
                if (snapshot.ToString(Formatting.None) != original) throw new InvalidOperationException("Historical source mutated.");
            }
        }
        int missing = results.Count(value => (int)value["assessment"]["category"] == 0);
        return new JObject
        {
            ["kind"] = "historical-exact-receipt-policy-replay", ["source"] = Source, ["sha256"] = hash,
            ["freshGameplay"] = false, ["trainingEligible"] = false, ["snapshots"] = snapshots,
            ["types"] = results.Select(value => (string)value["outcomeType"]).Distinct().Count(),
            ["subjectAssessments"] = results.Count, ["unassessed"] = missing, ["results"] = results
        };
    }

    private static IEnumerable<JObject> Objects(JToken node)
    {
        if (node is JObject value) yield return value;
        if (node is JContainer container)
            foreach (JToken child in container.Children())
                foreach (JObject descendant in Objects(child)) yield return descendant;
    }
}
