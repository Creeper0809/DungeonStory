#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class GameplayModuleSelectionReplayProofV2DebugScenarios
{
    private const string ZeroSha =
        "sha256:0000000000000000000000000000000000000000000000000000000000000000";
    private const string OneSha =
        "sha256:1111111111111111111111111111111111111111111111111111111111111111";

    [MenuItem("Tools/DungeonStory/QA/Run Module Replay Proof V2 Contracts")]
    public static void RunFromCommandLine()
    {
        JObject report = RunAll();
        Debug.Log("Gameplay module replay proof v2 contracts PASS "
            + report.ToString(Formatting.None));
    }

    internal static JObject RunAll()
    {
        int checks = 0;
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        const string contractPath =
            "Assets/Scripts/Services/Character/AI/Editor/"
            + "GameplayModuleSelectionReplayProofV2Contract.cs";
        byte[] contractBytes = File.ReadAllBytes(Path.Combine(
            projectRoot,
            contractPath.Replace('/', Path.DirectorySeparatorChar)));
        var expectedFiles = new JArray
        {
            new JObject
            {
                ["path"] = contractPath,
                ["bytes"] = contractBytes.Length,
                ["sha256"] = GameplayModuleSelectionCanonicalJson
                    .Sha256Prefixed(contractBytes)
            }
        };
        var runnerSettings = new JObject
        {
            ["mode"] = "isolated-edit",
            ["sourceSchema"] = 9,
            ["proofSchema"] = 2
        };
        JObject codeIdentity = GameplayModuleSelectionReplayProofV2Contract
            .CaptureCurrentCodeIdentity(
                projectRoot,
                expectedFiles,
                ZeroSha,
                OneSha,
                runnerSettings);
        Require(codeIdentity["files"] is JArray files && files.Count == 1,
            "Current code identity was not recomputed.");
        checks++;

        JObject before = Artifact("proof/before.json", 0);
        JObject applied = Artifact("proof/applied.json", 1);
        JObject restored = Artifact("proof/restored.json", 1);
        JObject assertion = GameplayModuleSelectionReplayProofV2Contract
            .StateAssertion("state-round-trip", true, 1, 1);
        JObject zeroDelta = GameplayModuleSelectionReplayProofV2Contract
            .InfluenceDelta("exact", "evidence:1", 0, 0, 0, 0,
                ZeroSha, ZeroSha);

        JObject request = Create(
            GameplayModuleSelectionReplayProofV2Contract.RequestAcceptedStage,
            GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
            codeIdentity,
            before,
            applied,
            restored,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { zeroDelta },
            new[] { assertion },
            Array.Empty<string>(),
            Array.Empty<string>());
        JObject candidate = Create(
            GameplayModuleSelectionReplayProofV2Contract.CandidateStoredStage,
            GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
            codeIdentity,
            before,
            applied,
            restored,
            new[] { "module:one" },
            new[] { "formula:one" },
            new[] { zeroDelta },
            new[] { assertion },
            Array.Empty<string>(),
            Array.Empty<string>());
        JObject commit = Create(
            GameplayModuleSelectionReplayProofV2Contract.SelectionCommittedStage,
            GameplayModuleSelectionReplayProofV2Contract.NotApplicableStatus,
            codeIdentity,
            before,
            null,
            null,
            new[] { "module:one" },
            new[] { "formula:one" },
            Array.Empty<JObject>(),
            Array.Empty<JObject>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        JObject effect = Create(
            GameplayModuleSelectionReplayProofV2Contract.MechanicExecutedStage,
            GameplayModuleSelectionReplayProofV2Contract.NotExecutedStatus,
            codeIdentity,
            before,
            null,
            null,
            new[] { "module:one" },
            new[] { "formula:one" },
            Array.Empty<JObject>(),
            Array.Empty<JObject>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        JArray proof = GameplayModuleSelectionReplayProofV2Contract
            .ValidateProofSet(new[] { request, candidate, commit, effect });
        Require(proof.Count == 4, "The four-stage proof was not preserved.");
        checks++;

        Require(candidate.Property("selectedEvidenceFactIds") != null
            && candidate.Property("evidenceInfluenceFactIds") == null,
            "Selected evidence was mislabeled as observed influence.");
        checks++;
        Require(candidate["observedInfluenceDeltas"] is JArray candidateDeltas
            && candidateDeltas.Count == 1
            && (int)candidateDeltas[0]["useCountDelta"] == 0,
            "Candidate storage did not preserve the zero-influence policy.");
        checks++;

        JObject positiveDelta = GameplayModuleSelectionReplayProofV2Contract
            .InfluenceDelta("exact", "evidence:1", 0, 1, 0, 1,
                ZeroSha, OneSha);
        ExpectReject(() => Create(
            GameplayModuleSelectionReplayProofV2Contract.CandidateStoredStage,
            GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
            codeIdentity,
            before,
            applied,
            restored,
            new[] { "module:one" },
            new[] { "formula:one" },
            new[] { positiveDelta },
            new[] { assertion },
            Array.Empty<string>(),
            Array.Empty<string>()));
        checks++;

        ExpectReject(() => Create(
            GameplayModuleSelectionReplayProofV2Contract.MechanicExecutedStage,
            GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
            codeIdentity,
            before,
            applied,
            restored,
            new[] { "module:one" },
            new[] { "formula:one" },
            new[] { positiveDelta },
            new[] { assertion },
            Array.Empty<string>(),
            Array.Empty<string>()));
        checks++;

        JObject legacyCandidate = (JObject)candidate.DeepClone();
        legacyCandidate["evidenceInfluenceFactIds"] = new JArray("evidence:1");
        ExpectReject(() => GameplayModuleSelectionReplayProofV2Contract
            .ValidateProofSet(new[] { request, legacyCandidate, commit, effect }));
        checks++;

        JObject passedEffect = Create(
            GameplayModuleSelectionReplayProofV2Contract.MechanicExecutedStage,
            GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
            codeIdentity,
            before,
            applied,
            restored,
            new[] { "module:one" },
            new[] { "formula:one" },
            new[] { positiveDelta },
            new[] { assertion },
            new[] { "effect-fingerprint:one" },
            Array.Empty<string>());
        ExpectReject(() => GameplayModuleSelectionReplayProofV2Contract
            .ValidateProofSet(new[] { request, candidate, commit, passedEffect }));
        checks++;

        JObject candidateNotApplicable = Create(
            GameplayModuleSelectionReplayProofV2Contract.CandidateStoredStage,
            GameplayModuleSelectionReplayProofV2Contract.NotApplicableStatus,
            codeIdentity,
            before,
            null,
            null,
            new[] { "module:one" },
            new[] { "formula:one" },
            Array.Empty<JObject>(),
            Array.Empty<JObject>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        JObject passedCommit = Create(
            GameplayModuleSelectionReplayProofV2Contract.SelectionCommittedStage,
            GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
            codeIdentity,
            before,
            applied,
            restored,
            new[] { "module:one" },
            new[] { "formula:one" },
            new[] { positiveDelta },
            new[] { assertion },
            Array.Empty<string>(),
            Array.Empty<string>());
        GameplayModuleSelectionReplayProofV2Contract.ValidateProofSet(
            new[] { request, candidateNotApplicable, passedCommit, effect });
        checks++;

        JObject stoppedCommit = Create(
            GameplayModuleSelectionReplayProofV2Contract.SelectionCommittedStage,
            GameplayModuleSelectionReplayProofV2Contract.NotExecutedStatus,
            codeIdentity,
            before,
            null,
            null,
            new[] { "module:one" },
            new[] { "formula:one" },
            Array.Empty<JObject>(),
            Array.Empty<JObject>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        ExpectReject(() => GameplayModuleSelectionReplayProofV2Contract
            .ValidateProofSet(new[] { request, candidate, stoppedCommit, passedEffect }));
        checks++;

        JObject badExpected = (JObject)((JObject)expectedFiles[0]).DeepClone();
        badExpected["sha256"] = ZeroSha;
        ExpectReject(() => GameplayModuleSelectionReplayProofV2Contract
            .CaptureCurrentCodeIdentity(
                projectRoot,
                new JArray(badExpected),
                ZeroSha,
                OneSha,
                runnerSettings));
        checks++;

        ExpectReject(() => GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                "EquipmentEvolutionModuleSelection",
                ZeroSha,
                OneSha,
                "request:one",
                ZeroSha,
                codeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.SelectionCommittedStage,
                GameplayModuleSelectionReplayProofV2Contract.FailedStatus,
                "rejected",
                before,
                null,
                null,
                new[] { "module:one" },
                new[] { "formula:one" },
                new[] { "evidence:1" },
                Array.Empty<JObject>(),
                Array.Empty<JObject>(),
                Array.Empty<string>(),
                Array.Empty<string>()));
        checks++;

        return new JObject
        {
            ["schemaVersion"] = 2,
            ["checks"] = checks,
            ["stageCount"] = proof.Count,
            ["currentCodeIdentityVerified"] = true,
            ["legacyInfluenceFieldRejected"] = true,
            ["candidateInfluenceMustRemainZero"] = true,
            ["mechanicCoverageRequired"] = true,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };
    }

    private static JObject Create(
        string stage,
        string status,
        JObject codeIdentity,
        JObject before,
        JObject applied,
        JObject restored,
        string[] modules,
        string[] formulas,
        JObject[] deltas,
        JObject[] assertions,
        string[] coverage,
        string[] failures) =>
        GameplayModuleSelectionReplayProofV2Contract.CreateStageProof(
            "EquipmentEvolutionModuleSelection",
            ZeroSha,
            OneSha,
            "request:one",
            ZeroSha,
            codeIdentity,
            stage,
            status,
            "bounded contract scenario",
            before,
            applied,
            restored,
            modules,
            formulas,
            new[] { "evidence:1" },
            deltas,
            assertions,
            coverage,
            failures);

    private static JObject Artifact(string path, int state)
    {
        var value = new JObject { ["state"] = state };
        return GameplayModuleSelectionReplayProofV2Contract.Artifact(
            path,
            GameplayModuleSelectionCanonicalJson.ToUtf8(value),
            value);
    }

    private static void ExpectReject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("Expected fail-closed rejection did not occur.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
