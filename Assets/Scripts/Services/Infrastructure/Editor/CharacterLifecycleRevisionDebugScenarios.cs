#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Focused lifecycle owner and raw save-boundary checks; not a full-world restore.</summary>
public static class CharacterLifecycleRevisionDebugScenarios
{
    public static void RunBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new lifecycle output directory is required.");
        Directory.CreateDirectory(output);
        var checks = new JArray();
        try
        {
            VerifyRawSaveBoundary();
            checks.Add("raw-revision-required-integer-unique-nonnegative-int64");
            VerifyLifecycleRoundTrip();
            checks.Add("detached-owner-restores-exact-state-and-revision");
            checks.Add("invalid-state-revision-and-overflow-preserve-owner");
            checks.Add("published-owner-rejects-restore");
            Write(output, new JObject { ["passed"] = true, ["checks"] = checks,
                ["testId"] = "audit-u1-lifecycle-revision", ["fullWorldRestore"] = false,
                ["fullU1Closure"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false });
        }
        catch (Exception error)
        {
            Write(output, new JObject { ["passed"] = false, ["checks"] = checks, ["failure"] = error.ToString() });
            throw;
        }
    }

    private static void VerifyRawSaveBoundary()
    {
        void Validate(string json) => DungeonStrictJsonShape.RequireObjectArrayNonNegativeInt64Fields(
            CharacterWorldSaveSection.Id, json, "actors", "lifecycleRevision");
        Validate("{\"actors\":[{\"lifecycleRevision\":0},{\"lifecycleRevision\":9223372036854775807}],\"extra\":{\"nested\":[1,null,true,\"text\"]}}");
        Validate("{\"actors\":[]}");
        Validate("{\"actors\":[{\"lifecycle\\u0052evision\":42}]}");
        foreach (string json in new[]
        {
            "{}", "{\"actors\":null}", "{\"actors\":[null]}", "{\"actors\":[{}]}",
            "{\"actors\":[{\"lifecycleRevision\":-1}]}", "{\"actors\":[{\"lifecycleRevision\":-0}]}",
            "{\"actors\":[{\"lifecycleRevision\":1.0}]}", "{\"actors\":[{\"lifecycleRevision\":1e0}]}",
            "{\"actors\":[{\"lifecycleRevision\":\"1\"}]}", "{\"actors\":[{\"lifecycleRevision\":null}]}",
            "{\"actors\":[{\"lifecycleRevision\":true}]}", "{\"actors\":[{\"lifecycleRevision\":9223372036854775808}]}",
            "{\"actors\":[{\"lifecycleRevision\":0,\"lifecycleRevision\":1}]}",
            "{\"actors\":[{\"lifecycleRevision\":0,\"lifecycle\\u0052evision\":1}]}",
            "{\"actors\":[],\"actors\":[]}", "{\"actors\":[]} true",
            "{\"actors\":[{\"lifecycleRevision\":00}]}"
        })
            ExpectRejected(() => Validate(json), "Invalid raw save accepted: " + json);

        var dto = JsonUtility.FromJson<DungeonCharacterSaveData>(
            "{\"lifecycleState\":0,\"lifecycleRevision\":73}");
        Require(dto.lifecycleRevision == 73 &&
            JsonUtility.FromJson<DungeonCharacterSaveData>(JsonUtility.ToJson(dto)).lifecycleRevision == 73,
            "Lifecycle revision did not survive the production JSON serializer.");
        dto.lifecycleRevision = -1;
        var report = new DungeonGameRestoreReport();
        CharacterWorldSaveValidation.ValidateActor(dto, "character:lifecycle-test", report);
        Require(System.Linq.Enumerable.Any(report.Errors, error => error.Contains("lifecycle revision")),
            "Typed save validation accepted a missing/negative revision.");
    }

    private static void VerifyLifecycleRoundTrip()
    {
        var go = new GameObject("Lifecycle revision controlled owner");
        CharacterSO data = null;
        try
        {
            var actor = go.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(go);
            actor.EnsureRuntimeState();
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(CharacterType.Customer, "복원 검사", "human");
            actor.data = data;
            actor.Identity.SetPersistentId("character:lifecycle-revision-test");
            CharacterLifecycle lifecycle = actor.Lifecycle;
            lifecycle.PrepareForDetachedRestore(actor);
            lifecycle.RestorePersistentState(CharacterLifecycleState.Active, 73);
            Require(lifecycle.CurrentState == CharacterLifecycleState.Active && lifecycle.LifecycleRevision == 73,
                "Restore counted an extra transition or lost the saved revision.");
            lifecycle.RestorePersistentState(CharacterLifecycleState.Downed, 81);
            Require(lifecycle.CurrentState == CharacterLifecycleState.Downed && lifecycle.LifecycleRevision == 81
                && !lifecycle.LifecycleTransitionInProgressForDiagnostics,
                "Downed restore did not preserve state/revision or finish cleanup.");
            ExpectRejected(() => lifecycle.RestorePersistentState(CharacterLifecycleState.Active, -1), "Negative revision accepted.");
            ExpectRejected(() => lifecycle.RestorePersistentState((CharacterLifecycleState)int.MaxValue, 82), "Unknown lifecycle accepted.");
            Require(lifecycle.CurrentState == CharacterLifecycleState.Downed && lifecycle.LifecycleRevision == 81,
                "Invalid restore partially changed lifecycle authority.");
            lifecycle.SetLifecycleState(CharacterLifecycleState.Active);
            Require(lifecycle.LifecycleRevision == 82, "Real transition did not advance the restored revision exactly once.");
            lifecycle.RestorePersistentState(CharacterLifecycleState.Active, long.MaxValue);
            ExpectRejected(() => lifecycle.SetLifecycleState(CharacterLifecycleState.Downed), "Revision overflow accepted.");
            Require(lifecycle.CurrentState == CharacterLifecycleState.Active && lifecycle.LifecycleRevision == long.MaxValue,
                "Overflow partially changed the live owner.");
            lifecycle.RestorePersistentState(CharacterLifecycleState.Active, 82);
            lifecycle.PublishDetachedRestore();
            ExpectRejected(() => lifecycle.RestorePersistentState(CharacterLifecycleState.Downed, 1),
                "Published owner allowed revision rewind through restore.");
            Require(lifecycle.CurrentState == CharacterLifecycleState.Active && lifecycle.LifecycleRevision == 82,
                "Published restore rejection changed authority.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static void ExpectRejected(Action action, string message)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidOperationException or ArgumentOutOfRangeException or OverflowException) { return; }
        throw new InvalidOperationException(message);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Write(string output, JObject report)
    {
        using var file = new FileStream(Path.Combine(output, "report.json"), FileMode.CreateNew);
        using var writer = new StreamWriter(file);
        writer.Write(report.ToString(Formatting.Indented));
    }
}
#endif
