#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Fail-closed schema-2 proof contract for source-v9 module-selection replay.
/// It deliberately separates response acceptance, candidate storage, selection
/// commit and mechanic execution. Selected evidence is response metadata; only
/// before/after authority anchors may be reported as observed influence.
/// </summary>
internal static class GameplayModuleSelectionReplayProofV2Contract
{
    internal const int SchemaVersion = 2;
    internal const string RequestAcceptedStage = "request-accepted";
    internal const string CandidateStoredStage = "candidate-stored";
    internal const string SelectionCommittedStage = "selection-committed";
    internal const string MechanicExecutedStage = "mechanic-executed";
    internal const string PassedStatus = "passed";
    internal const string FailedStatus = "failed";
    internal const string NotApplicableStatus = "not-applicable";
    internal const string NotExecutedStatus = "not-executed";

    private static readonly string[] OrderedStages =
    {
        RequestAcceptedStage,
        CandidateStoredStage,
        SelectionCommittedStage,
        MechanicExecutedStage
    };

    private static readonly HashSet<string> Statuses = new(
        new[]
        {
            PassedStatus,
            FailedStatus,
            NotApplicableStatus,
            NotExecutedStatus
        },
        StringComparer.Ordinal);

    internal static JObject CreateStageProof(
        string profileId,
        string sourceManifestSha256,
        string responseManifestSha256,
        string requestId,
        string responseSha256,
        JObject replayCodeIdentity,
        string stage,
        string status,
        string reason,
        JObject beforeArtifact,
        JObject appliedArtifact,
        JObject restoredArtifact,
        IEnumerable<string> selectedModuleIds,
        IEnumerable<string> formulaIds,
        IEnumerable<string> selectedEvidenceFactIds,
        IEnumerable<JObject> observedInfluenceDeltas,
        IEnumerable<JObject> stateAssertions,
        IEnumerable<string> effectCoverageRefs,
        IEnumerable<string> failures)
    {
        string canonicalProfile = RequireText(profileId, nameof(profileId));
        string canonicalRequest = RequireText(requestId, nameof(requestId));
        string canonicalStage = RequireStage(stage);
        string canonicalStatus = RequireStatus(status);
        string canonicalReason = RequireText(reason, nameof(reason));
        RequireSha(sourceManifestSha256, nameof(sourceManifestSha256));
        RequireSha(responseManifestSha256, nameof(responseManifestSha256));
        RequireSha(responseSha256, nameof(responseSha256));
        JObject codeIdentity = RequireCodeIdentity(replayCodeIdentity);
        string[] modules = RequireUnique(selectedModuleIds, nameof(selectedModuleIds),
            allowEmpty: canonicalStage == RequestAcceptedStage);
        string[] formulas = RequireUnique(formulaIds, nameof(formulaIds),
            allowEmpty: canonicalStage == RequestAcceptedStage);
        string[] selectedEvidence = RequireUnique(
            selectedEvidenceFactIds,
            nameof(selectedEvidenceFactIds),
            allowEmpty: false);
        JObject[] deltas = (observedInfluenceDeltas
                ?? throw new ArgumentNullException(nameof(observedInfluenceDeltas)))
            .Select(value => RequireInfluenceDelta(value, selectedEvidence))
            .ToArray();
        if (deltas.GroupBy(
                value => ((string)value["sourceKind"],
                    (string)value["evidenceFactId"]),
                EqualityComparer<(string, string)>.Default)
            .Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(
                "Observed influence deltas repeat a source/fact identity.");
        }
        JObject[] assertions = (stateAssertions
                ?? throw new ArgumentNullException(nameof(stateAssertions)))
            .Select(RequireStateAssertion)
            .ToArray();
        string[] coverage = RequireUnique(
            effectCoverageRefs,
            nameof(effectCoverageRefs),
            allowEmpty: true);
        string[] failureList = RequireUnique(
            failures,
            nameof(failures),
            allowEmpty: true);

        bool passed = string.Equals(canonicalStatus, PassedStatus,
            StringComparison.Ordinal);
        bool failed = string.Equals(canonicalStatus, FailedStatus,
            StringComparison.Ordinal);
        bool terminalWithoutExecution = string.Equals(
                canonicalStatus,
                NotApplicableStatus,
                StringComparison.Ordinal)
            || string.Equals(
                canonicalStatus,
                NotExecutedStatus,
                StringComparison.Ordinal);

        JObject before = RequireArtifact(beforeArtifact, "beforeArtifact");
        JObject applied = OptionalArtifact(appliedArtifact, "appliedArtifact");
        JObject restored = OptionalArtifact(restoredArtifact, "restoredArtifact");
        if (passed && (applied == null || restored == null))
        {
            throw new InvalidOperationException(
                "A passed replay stage requires before/applied/restored artifacts.");
        }
        if (terminalWithoutExecution
            && (applied != null || restored != null || deltas.Length != 0))
        {
            throw new InvalidOperationException(
                "A non-executed replay stage cannot claim applied/restored state or influence.");
        }
        if (passed && (assertions.Length == 0
            || assertions.Any(value => (bool?)value["passed"] != true)))
        {
            throw new InvalidOperationException(
                "A passed replay stage requires only passing state assertions.");
        }
        if (failed != (failureList.Length > 0))
        {
            throw new InvalidOperationException(
                "Only a failed replay stage may contain failures, and it must contain one.");
        }
        if ((canonicalStage == RequestAcceptedStage
                || canonicalStage == CandidateStoredStage)
            && deltas.Any(value => (int)value["useCountDelta"] != 0
                || (int)value["revisionDelta"] != 0))
        {
            throw new InvalidOperationException(
                "Request acceptance and candidate storage cannot claim influence consumption.");
        }
        if (canonicalStage != RequestAcceptedStage
            && passed
            && (modules.Length == 0 || formulas.Length == 0))
        {
            throw new InvalidOperationException(
                "A passed candidate/commit/effect stage requires selected modules and formula IDs.");
        }
        if (canonicalStage == MechanicExecutedStage
            && passed
            && coverage.Length == 0)
        {
            throw new InvalidOperationException(
                "A passed mechanic execution requires an effect coverage reference.");
        }

        return new JObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["taskId"] = "gameplay-module-selection",
            ["profileId"] = canonicalProfile,
            ["sourceManifestSha256"] = sourceManifestSha256,
            ["responseManifestSha256"] = responseManifestSha256,
            ["requestId"] = canonicalRequest,
            ["responseSha256"] = responseSha256,
            ["replayCodeIdentity"] = codeIdentity,
            ["stage"] = canonicalStage,
            ["status"] = canonicalStatus,
            ["reason"] = canonicalReason,
            ["artifacts"] = new JObject
            {
                ["before"] = before,
                ["applied"] = applied == null
                    ? JValue.CreateNull()
                    : applied,
                ["restored"] = restored == null
                    ? JValue.CreateNull()
                    : restored
            },
            ["selectedModuleIds"] = new JArray(modules),
            ["formulaIds"] = new JArray(formulas),
            ["selectedEvidenceFactIds"] = new JArray(selectedEvidence),
            ["observedInfluenceDeltas"] = new JArray(deltas),
            ["stateAssertions"] = new JArray(assertions),
            ["effectCoverageRefs"] = new JArray(coverage),
            ["failures"] = new JArray(failureList),
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };
    }

    internal static JArray ValidateProofSet(IEnumerable<JObject> values)
    {
        JObject[] rows = (values ?? throw new ArgumentNullException(nameof(values)))
            .Select(value => (JObject)(value
                ?? throw new InvalidOperationException("Replay proof row is null."))
                .DeepClone())
            .ToArray();
        if (rows.Length != OrderedStages.Length)
            throw new InvalidOperationException(
                "A replay proof set requires exactly four ordered stage rows.");
        for (int index = 0; index < rows.Length; index++)
        {
            JObject row = rows[index];
            JObject artifacts = row["artifacts"] as JObject
                ?? throw new InvalidOperationException(
                    "Replay proof artifacts are missing.");
            JObject rebuilt = CreateStageProof(
                (string)row["profileId"],
                (string)row["sourceManifestSha256"],
                (string)row["responseManifestSha256"],
                (string)row["requestId"],
                (string)row["responseSha256"],
                row["replayCodeIdentity"] as JObject,
                (string)row["stage"],
                (string)row["status"],
                (string)row["reason"],
                artifacts["before"] as JObject,
                artifacts["applied"] as JObject,
                artifacts["restored"] as JObject,
                RequireStringArray(row, "selectedModuleIds"),
                RequireStringArray(row, "formulaIds"),
                RequireStringArray(row, "selectedEvidenceFactIds"),
                RequireObjectArray(row, "observedInfluenceDeltas"),
                RequireObjectArray(row, "stateAssertions"),
                RequireStringArray(row, "effectCoverageRefs"),
                RequireStringArray(row, "failures"));
            if ((int?)row["schemaVersion"] != SchemaVersion
                || !string.Equals((string)row["stage"], OrderedStages[index],
                    StringComparison.Ordinal)
                || row.Property("evidenceInfluenceFactIds") != null)
            {
                throw new InvalidOperationException(
                    "Replay proof schema, stage order or legacy influence field is invalid.");
            }
            if (!JToken.DeepEquals(row, rebuilt))
                throw new InvalidOperationException(
                    "Replay proof contains an unknown, missing or altered field.");
            foreach (string field in new[]
            {
                "profileId", "sourceManifestSha256", "responseManifestSha256",
                "requestId", "responseSha256"
            })
            {
                if (index > 0 && !JToken.DeepEquals(rows[0][field], row[field]))
                    throw new InvalidOperationException(
                        "Replay proof identity changed between stages: " + field);
            }
        }

        bool stopped = false;
        foreach (JObject row in rows)
        {
            string status = RequireStatus((string)row["status"]);
            if (stopped && string.Equals(status, PassedStatus,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A later replay stage cannot pass after a failed or non-executed stage.");
            }
            stopped |= string.Equals(status, FailedStatus, StringComparison.Ordinal)
                || string.Equals(status, NotExecutedStatus, StringComparison.Ordinal)
                || (string.Equals(status, NotApplicableStatus,
                        StringComparison.Ordinal)
                    && !string.Equals((string)row["stage"], CandidateStoredStage,
                        StringComparison.Ordinal));
        }
        return new JArray(rows);
    }

    internal static JObject CaptureCurrentCodeIdentity(
        string projectRoot,
        JArray sourceCodeIdentity,
        string catalogSha256,
        string applicationContractSha256,
        JObject runnerSettings)
    {
        string root = Path.GetFullPath(RequireText(projectRoot, nameof(projectRoot)))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Replay project root is missing: " + root);
        RequireSha(catalogSha256, nameof(catalogSha256));
        RequireSha(applicationContractSha256, nameof(applicationContractSha256));
        JObject canonicalRunnerSettings = (JObject)(runnerSettings
            ?? throw new ArgumentNullException(nameof(runnerSettings))).DeepClone();
        JObject[] expected = (sourceCodeIdentity
                ?? throw new ArgumentNullException(nameof(sourceCodeIdentity)))
            .OfType<JObject>()
            .ToArray();
        if (expected.Length == 0 || expected.Length != sourceCodeIdentity.Count)
            throw new InvalidOperationException("Source code identity is empty or malformed.");

        var current = new List<JObject>(expected.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string rootPrefix = root + Path.DirectorySeparatorChar;
        foreach (JObject descriptor in expected)
        {
            string relative = RequireText((string)descriptor["path"], "codeIdentity.path")
                .Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(relative)
                || relative.Split(Path.DirectorySeparatorChar).Contains(".."))
            {
                throw new InvalidOperationException(
                    "Code identity paths must stay inside the project.");
            }
            string normalized = relative.Replace(Path.DirectorySeparatorChar, '/');
            if (!seen.Add(normalized))
                throw new InvalidOperationException("Duplicate code identity path: " + normalized);
            string full = Path.GetFullPath(Path.Combine(root, relative));
            if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(full)
                || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "Code identity file is missing, outside the project or a reparse point: "
                    + normalized);
            }
            byte[] bytes = File.ReadAllBytes(full);
            var actual = new JObject
            {
                ["path"] = normalized,
                ["bytes"] = bytes.Length,
                ["sha256"] = GameplayModuleSelectionCanonicalJson
                    .Sha256Prefixed(bytes)
            };
            if (!JToken.DeepEquals(descriptor, actual))
                throw new InvalidOperationException(
                    "Current replay code identity differs from source: " + normalized);
            current.Add(actual);
        }

        JArray files = new(current);
        return new JObject
        {
            ["unityVersion"] = Application.unityVersion,
            ["files"] = files,
            ["filesSha256"] = GameplayModuleSelectionCanonicalJson.Hash(files),
            ["catalogSha256"] = catalogSha256,
            ["applicationContractSha256"] = applicationContractSha256,
            ["runnerSettings"] = canonicalRunnerSettings,
            ["runnerSettingsSha256"] = GameplayModuleSelectionCanonicalJson.Hash(
                canonicalRunnerSettings)
        };
    }

    internal static JObject Artifact(
        string relativePath,
        byte[] bytes,
        JToken semanticValue) => new()
    {
        ["path"] = RequireRelativePath(relativePath),
        ["bytes"] = (bytes ?? throw new ArgumentNullException(nameof(bytes))).Length,
        ["byteSha256"] = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes),
        ["canonicalSemanticSha256"] = GameplayModuleSelectionCanonicalJson.Hash(
            semanticValue ?? throw new ArgumentNullException(nameof(semanticValue)))
    };

    internal static JObject InfluenceDelta(
        string sourceKind,
        string evidenceFactId,
        int beforeUseCount,
        int afterUseCount,
        int beforeRevision,
        int afterRevision,
        string beforeAnchorSha256,
        string afterAnchorSha256) => new()
    {
        ["sourceKind"] = RequireSourceKind(sourceKind),
        ["evidenceFactId"] = RequireText(evidenceFactId, nameof(evidenceFactId)),
        ["beforeUseCount"] = beforeUseCount,
        ["afterUseCount"] = afterUseCount,
        ["useCountDelta"] = afterUseCount - beforeUseCount,
        ["beforeRevision"] = beforeRevision,
        ["afterRevision"] = afterRevision,
        ["revisionDelta"] = afterRevision - beforeRevision,
        ["beforeAnchorSha256"] = RequireSha(
            beforeAnchorSha256,
            nameof(beforeAnchorSha256)),
        ["afterAnchorSha256"] = RequireSha(
            afterAnchorSha256,
            nameof(afterAnchorSha256))
    };

    internal static JObject StateAssertion(
        string assertionId,
        bool passed,
        JToken expected,
        JToken actual) => new()
    {
        ["assertionId"] = RequireText(assertionId, nameof(assertionId)),
        ["passed"] = passed,
        ["expected"] = expected?.DeepClone() ?? JValue.CreateNull(),
        ["actual"] = actual?.DeepClone() ?? JValue.CreateNull()
    };

    private static JObject RequireCodeIdentity(JObject value)
    {
        JObject result = (JObject)(value
            ?? throw new ArgumentNullException(nameof(value))).DeepClone();
        RequireText((string)result["unityVersion"], "replayCodeIdentity.unityVersion");
        RequireSha((string)result["filesSha256"], "replayCodeIdentity.filesSha256");
        RequireSha((string)result["catalogSha256"], "replayCodeIdentity.catalogSha256");
        RequireSha((string)result["applicationContractSha256"],
            "replayCodeIdentity.applicationContractSha256");
        RequireSha((string)result["runnerSettingsSha256"],
            "replayCodeIdentity.runnerSettingsSha256");
        if (result["files"] is not JArray files || files.Count == 0
            || result["runnerSettings"] is not JObject)
        {
            throw new InvalidOperationException(
                "Replay code identity requires files and runner settings.");
        }
        foreach (JObject descriptor in files.OfType<JObject>())
        {
            RequireText((string)descriptor["path"], "replayCodeIdentity.files.path");
            int byteCount = (int?)descriptor["bytes"]
                ?? throw new InvalidOperationException(
                    "Replay code identity byte count is missing.");
            if (byteCount < 0)
                throw new InvalidOperationException("Replay code identity has invalid byte count.");
            RequireSha((string)descriptor["sha256"],
                "replayCodeIdentity.files.sha256");
        }
        if (files.OfType<JObject>().Count() != files.Count
            || files.OfType<JObject>().Select(value => (string)value["path"])
                .Distinct(StringComparer.Ordinal).Count() != files.Count
            || !string.Equals(
                GameplayModuleSelectionCanonicalJson.Hash(files),
                (string)result["filesSha256"],
                StringComparison.Ordinal)
            || !string.Equals(
                GameplayModuleSelectionCanonicalJson.Hash(result["runnerSettings"]),
                (string)result["runnerSettingsSha256"],
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Replay code identity file/settings digest is invalid.");
        }
        return result;
    }

    private static JObject RequireInfluenceDelta(
        JObject value,
        IReadOnlyCollection<string> selectedEvidence)
    {
        JObject result = (JObject)(value
            ?? throw new InvalidOperationException("Observed influence delta is null."))
            .DeepClone();
        string sourceKind = RequireSourceKind((string)result["sourceKind"]);
        string factId = RequireText((string)result["evidenceFactId"],
            "observedInfluenceDeltas.evidenceFactId");
        if (!selectedEvidence.Contains(factId))
            throw new InvalidOperationException(
                "Observed influence must refer to selected evidence: " + factId);
        int beforeCount = RequireNonNegative(result, "beforeUseCount");
        int afterCount = RequireNonNegative(result, "afterUseCount");
        int beforeRevision = RequireNonNegative(result, "beforeRevision");
        int afterRevision = RequireNonNegative(result, "afterRevision");
        if (afterCount < beforeCount || afterRevision < beforeRevision
            || (int?)result["useCountDelta"] != afterCount - beforeCount
            || (int?)result["revisionDelta"] != afterRevision - beforeRevision)
        {
            throw new InvalidOperationException(
                "Observed influence delta does not match its authority anchors: "
                + sourceKind + "/" + factId);
        }
        RequireSha((string)result["beforeAnchorSha256"],
            "observedInfluenceDeltas.beforeAnchorSha256");
        RequireSha((string)result["afterAnchorSha256"],
            "observedInfluenceDeltas.afterAnchorSha256");
        return result;
    }

    private static JObject RequireStateAssertion(JObject value)
    {
        JObject result = (JObject)(value
            ?? throw new InvalidOperationException("State assertion is null."))
            .DeepClone();
        RequireText((string)result["assertionId"], "stateAssertions.assertionId");
        if (result["passed"]?.Type != JTokenType.Boolean
            || result.Property("expected") == null
            || result.Property("actual") == null)
        {
            throw new InvalidOperationException("State assertion is malformed.");
        }
        return result;
    }

    private static JObject RequireArtifact(JObject value, string name) =>
        OptionalArtifact(value, name)
        ?? throw new InvalidOperationException(name + " is required.");

    private static JObject OptionalArtifact(JObject value, string name)
    {
        if (value == null)
            return null;
        JObject result = (JObject)value.DeepClone();
        RequireRelativePath((string)result["path"]);
        int byteCount = (int?)result["bytes"]
            ?? throw new InvalidOperationException(name + " byte count is missing.");
        if (byteCount < 0)
            throw new InvalidOperationException(name + " has invalid byte count.");
        RequireSha((string)result["byteSha256"], name + ".byteSha256");
        RequireSha((string)result["canonicalSemanticSha256"],
            name + ".canonicalSemanticSha256");
        return result;
    }

    private static string RequireRelativePath(string value)
    {
        string result = RequireText(value, "artifact.path").Replace('\\', '/');
        if (Path.IsPathRooted(result)
            || result.Split('/').Any(part => part == ".." || part.Length == 0))
            throw new InvalidOperationException("Artifact path must be canonical and relative.");
        return result;
    }

    private static string RequireStage(string value)
    {
        string result = RequireText(value, nameof(value));
        if (!OrderedStages.Contains(result, StringComparer.Ordinal))
            throw new InvalidOperationException("Unknown replay stage: " + result);
        return result;
    }

    private static string RequireStatus(string value)
    {
        string result = RequireText(value, nameof(value));
        if (!Statuses.Contains(result))
            throw new InvalidOperationException("Unknown replay stage status: " + result);
        return result;
    }

    private static string RequireSourceKind(string value)
    {
        string result = RequireText(value, nameof(value));
        if (!string.Equals(result, "exact", StringComparison.Ordinal)
            && !string.Equals(result, "compacted", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Influence source kind must be exact or compacted.");
        }
        return result;
    }

    private static int RequireNonNegative(JObject owner, string key)
    {
        int value = (int?)owner[key]
            ?? throw new InvalidOperationException("Missing integer: " + key);
        if (value < 0)
            throw new InvalidOperationException("Negative integer: " + key);
        return value;
    }

    private static string RequireSha(string value, string name)
    {
        string result = RequireText(value, name);
        if (result.Length != 71
            || !result.StartsWith("sha256:", StringComparison.Ordinal)
            || result.Skip(7).Any(character => !Uri.IsHexDigit(character)
                || char.IsUpper(character)))
        {
            throw new InvalidOperationException(
                name + " must be a canonical lowercase sha256 value.");
        }
        return result;
    }

    private static string RequireText(string value, string name)
    {
        string result = value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(result)
            || !string.Equals(result, result.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException(name + " must be non-empty canonical text.");
        return result;
    }

    private static string[] RequireUnique(
        IEnumerable<string> values,
        string name,
        bool allowEmpty)
    {
        string[] result = (values ?? throw new ArgumentNullException(name)).ToArray();
        if ((!allowEmpty && result.Length == 0)
            || result.Any(string.IsNullOrWhiteSpace)
            || result.Any(value => !string.Equals(value, value.Trim(),
                StringComparison.Ordinal))
            || result.Distinct(StringComparer.Ordinal).Count() != result.Length)
        {
            throw new InvalidOperationException(
                name + " must contain distinct canonical identities.");
        }
        return result;
    }

    private static string[] RequireStringArray(JObject owner, string key)
    {
        if (owner?[key] is not JArray values
            || values.Any(value => value.Type != JTokenType.String))
        {
            throw new InvalidOperationException(
                "Replay proof string array is missing or malformed: " + key);
        }
        return values.Values<string>().ToArray();
    }

    private static JObject[] RequireObjectArray(JObject owner, string key)
    {
        if (owner?[key] is not JArray values
            || values.Any(value => value.Type != JTokenType.Object))
        {
            throw new InvalidOperationException(
                "Replay proof object array is missing or malformed: " + key);
        }
        return values.OfType<JObject>().ToArray();
    }
}
#endif
