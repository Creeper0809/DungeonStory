#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class V27RandomStreamManifestDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-random-stream-manifest.txt";

    private static readonly string[] RuntimeRoots =
    {
        "Assets/Scripts/Models",
        "Assets/Scripts/Services"
    };

    [MenuItem("DungeonStory/V27/Verify Random Stream Manifest")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        WriteReportIfDifferent(report);
        Debug.Log(report);
    }

    public static string RunAll()
    {
        string[] files = RuntimeRoots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.GetFiles(
                root,
                "*.cs",
                SearchOption.AllDirectories))
            .Where(path => path.IndexOf(
                    $"{Path.DirectorySeparatorChar}Editor{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase) < 0)
            .Select(NormalizePath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        List<Consumer> consumers = new();
        List<string> failures = new();
        using IncrementalHash sourceDigest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string path in files)
        {
            string source = File.ReadAllText(path);
            if (source.IndexOf("IRandomStreamProvider", StringComparison.Ordinal) < 0
                && source.IndexOf(".RandomStreams.Get(", StringComparison.Ordinal) < 0
                && source.IndexOf(".RandomStreamProvider.Get(", StringComparison.Ordinal) < 0)
            {
                continue;
            }

            byte[] digestBytes = Encoding.UTF8.GetBytes(path + "\n" + NormalizeNewlines(source));
            sourceDigest.AppendData(digestBytes);
            consumers.Add(new Consumer(
                path,
                ClassifyScope(source),
                source.IndexOf("IRandomStreamProvider", StringComparison.Ordinal) >= 0));

            if (source.IndexOf("Get(\"character-ai\")", StringComparison.Ordinal) >= 0
                || source.IndexOf("Get(\"character-movement\")", StringComparison.Ordinal) >= 0)
            {
                failures.Add("LEGACY_GLOBAL_CHARACTER_STREAM:" + path);
            }
            if (source.IndexOf("UnityEngine.Random.", StringComparison.Ordinal) >= 0
                || source.IndexOf("Random.Range(", StringComparison.Ordinal) >= 0
                || source.IndexOf("Random.value", StringComparison.Ordinal) >= 0)
            {
                failures.Add("UNITY_RANDOM_RUNTIME_USE:" + path);
            }
        }

        RequireSourceToken(
            "Assets/Scripts/Services/Character/AI/AIBrain.cs",
            "CharacterRandomStreamScopeIds.Decision(characterId)",
            failures,
            "CHARACTER_DECISION_SCOPE_MISSING");
        RequireSourceToken(
            "Assets/Scripts/Services/Character/Ability/AbilityMove.cs",
            "CharacterRandomStreamScopeIds.Movement(characterId)",
            failures,
            "CHARACTER_MOVEMENT_SCOPE_MISSING");
        RequireSourceToken(
            "Assets/Scripts/Services/Wildlife/WildlifeActor.cs",
            "RandomStreamScopeIds.WildlifeActor(",
            failures,
            "WILDLIFE_ACTOR_SCOPE_MISSING");
        string isolation = RandomStreamIsolationDebugScenarios.RunAll();
        string[] requiredIsolationMarkers =
        {
            "RESULT=PASS; suite=RandomStreamIsolationDebugScenarios",
            "PASS RNG_ACTOR_EXTRA_DRAWS_CROSS_TALK_ZERO draws=100",
            "PASS RNG_DECISION_MOVEMENT_CROSS_TALK_ZERO draws=100",
            "PASS RNG_ACTOR_SPAWN_DESPAWN_EXISTING_STREAMS_UNCHANGED",
            "PASS RNG_KEYED_EVENT_ORDER_INDEPENDENT",
            "PASS RNG_DUPLICATE_EVENT_KEY_REJECTED",
            "PASS RNG_SAVE_RESTORE_NEXT_DRAW_EXACT",
            "PASS RNG_DIAGNOSTIC_DRAW_COUNT_RESTORED_TO_ZERO",
            "PASS RNG_DUPLICATE_CHARACTER_ID_REJECTED",
            "PASS RNG_CAUSAL_CONE_OUTSIDE_STREAMS_UNCHANGED",
            "PASS RNG_LEGACY_GLOBAL_CHARACTER_STREAMS_REJECTED"
        };
        foreach (string marker in requiredIsolationMarkers)
        {
            if (isolation.IndexOf(marker, StringComparison.Ordinal) < 0)
                failures.Add("RNG_ISOLATION_MARKER_MISSING:" + marker);
        }

        StringBuilder builder = new();
        builder.Append("RESULT=")
            .Append(failures.Count == 0 ? "PASS" : "FAIL")
            .Append("; runtimeFiles=").Append(files.Length)
            .Append("; consumers=").Append(consumers.Count)
            .Append("; failures=").Append(failures.Count)
            .Append("; legacyCharacterStreams=0; unityRandomRuntimeUses=0;")
            .Append(" namedIsolationTests=").Append(requiredIsolationMarkers.Length)
            .Append("\n")
            .Append("sourceDigest=")
            .Append(ToLowerHex(sourceDigest.GetHashAndReset()))
            .Append('\n');

        foreach (Consumer consumer in consumers
                     .OrderBy(value => value.Path, StringComparer.Ordinal))
        {
            builder.Append("CONSUMER\t")
                .Append(consumer.Path)
                .Append("\tscope=")
                .Append(consumer.Scope)
                .Append("\tproviderReference=")
                .Append(consumer.HasProviderReference ? "true" : "false")
                .Append('\n');
        }
        builder.Append(isolation);
        foreach (string failure in failures.OrderBy(value => value, StringComparer.Ordinal))
            builder.Append("FAIL\t").Append(failure).Append('\n');

        if (failures.Count > 0)
            throw new InvalidOperationException(builder.ToString());
        return builder.ToString();
    }

    private static string ClassifyScope(string source)
    {
        if (source.IndexOf("CharacterRandomStreamScopeIds.Decision", StringComparison.Ordinal) >= 0)
            return "character-decision:persistent-id";
        if (source.IndexOf("CharacterRandomStreamScopeIds.Movement", StringComparison.Ordinal) >= 0)
            return "character-movement:persistent-id";
        if (source.IndexOf("RandomStreamScopeIds.WildlifeActor", StringComparison.Ordinal) >= 0)
            return "wildlife-actor:persistent-id";
        return "domain-stable";
    }

    private static void RequireSourceToken(
        string path,
        string token,
        ICollection<string> failures,
        string failureCode)
    {
        if (!File.Exists(path)
            || File.ReadAllText(path).IndexOf(token, StringComparison.Ordinal) < 0)
        {
            failures.Add(failureCode + ":" + path);
        }
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');

    private static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n").Replace('\r', '\n');

    private static void WriteReportIfDifferent(string report)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string target = Path.Combine(
            projectRoot,
            ReportPath.Replace('/', Path.DirectorySeparatorChar));
        string directory = Path.GetDirectoryName(target)
            ?? throw new InvalidOperationException("Report directory is unavailable.");
        Directory.CreateDirectory(directory);

        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report);
        if (File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(bytes))
            return;

        string temporary = target + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(target))
                File.Replace(temporary, target, null);
            else
                File.Move(temporary, target);
        }
        catch
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
            throw;
        }
    }

    private static string ToLowerHex(byte[] bytes)
    {
        const string Hex = "0123456789abcdef";
        char[] result = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = Hex[bytes[index] >> 4];
            result[index * 2 + 1] = Hex[bytes[index] & 0x0f];
        }
        return new string(result);
    }

    private readonly struct Consumer
    {
        internal Consumer(string path, string scope, bool hasProviderReference)
        {
            Path = path;
            Scope = scope;
            HasProviderReference = hasProviderReference;
        }

        internal string Path { get; }
        internal string Scope { get; }
        internal bool HasProviderReference { get; }
    }
}
#endif
