#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Review-only reinterpretation of captured facts. No gameplay, save restore,
// model response, approval or raw-salience fallback is performed here.
public static class PilotInputExperienceProfileExporter
{
    public static void RunForBatch()
    {
        // This edit-only evaluator never opens a gameplay save or creates a world.
        // OS station/desktop isolation is certified by the mandatory launcher probe.
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Isolated batch execution required.");
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-logFile");
        if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
            throw new InvalidOperationException("An absolute isolated log path is required.");
        string output = Path.GetDirectoryName(Path.GetFullPath(args[index + 1]));
        string qa = Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar;
        if (!output.StartsWith(qa, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Output must remain in this project's QA tree.");
        string input = Path.Combine(Directory.GetParent(output).FullName, "policy-inputs-r1.json");
        byte[] bytes = File.ReadAllBytes(input);
        var document = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        if ((string)document["schema"] != "dungeonstory-pilot-experience-evaluation-input@1"
            || document["rows"] is not JArray rows || rows.Count != 100
            || rows.Select(value => (string)value["requestKey"]).Distinct().Count() != 100)
            throw new InvalidOperationException("Expected100 unique, source-bound captured requests.");
        var registry = GameplayExperienceObserverRegistration.Default;
        if (registry.SupportedOutcomeTypes.Count != 149)
            throw new InvalidOperationException("Current149 experience policies are missing.");
        var results = new JArray();
        int assessed = 0, unknown = 0;
        foreach (JObject row in rows)
        {
            var facts = new JArray();
            foreach (JObject fact in (JArray)row["facts"])
            {
                GameplayExperienceAssessmentSnapshot assessment = null;
                var token = fact["binding"] as JObject;
                if (token != null)
                {
                    var binding = token.ToObject<GameplayOutcomeEvidenceBindingSnapshot>();
                    if (binding.publicFactId != (string)fact["sourceFactId"])
                        throw new InvalidOperationException("Captured binding identity differs.");
                    if (binding.sourceKind == "exact")
                    {
                        if (string.IsNullOrWhiteSpace(binding.outcomeRunId)
                            || string.IsNullOrWhiteSpace(binding.outcomeTypeId))
                            throw new InvalidOperationException("Exact binding has no original outcome identity.");
                        assessment = registry.Observe(binding);
                    }
                }
                if (assessment == null || assessment.category == 0) unknown++;
                else assessed++;
                facts.Add(new JObject
                {
                    ["id"] = fact["id"], ["sourceFactId"] = fact["sourceFactId"],
                    ["assessment"] = assessment == null ? JValue.CreateNull() : JObject.FromObject(assessment),
                    ["unassessedReason"] = assessment == null
                        ? "no-exact-typed-binding-in-captured-input" : string.Empty
                });
            }
            results.Add(new JObject
            {
                ["requestKey"] = row["requestKey"], ["sourceWireSha256"] = row["sourceWireSha256"],
                ["profileId"] = row["profileId"], ["facts"] = facts
            });
        }
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        var report = new JObject
        {
            ["schema"] = "dungeonstory-pilot-current-experience-review@1",
            ["policyVersion"] = GameplayExperienceImportancePolicy.Version, ["policies"] = 149,
            ["inputSha256"] = "sha256:" + hash, ["requests"] = results.Count,
            ["assessedFacts"] = assessed, ["unassessedFacts"] = unknown,
            ["freshGameplay"] = false, ["originalWireModified"] = false,
            ["humanApprovalClaimed"] = false, ["trainingEligible"] = false, ["rows"] = results
        };
        // Never overwrite a prior success or failure artifact.
        using (var stream = new FileStream(Path.Combine(output, "current-experience-review.json"), FileMode.CreateNew))
        using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            writer.Write(report.ToString(Formatting.Indented));
        Debug.Log("B132_PROFILE100_CURRENT_POLICY=PASS; requests=" + results.Count
            + "; assessed=" + assessed + "; unassessed=" + unknown);
    }
}
#endif
