#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Deterministic freshness authority for long-running PlayMode evidence.
/// The digest covers every C# source file so a report cannot silently survive
/// a gameplay, save, verifier, or test-contract edit.
/// </summary>
public static class V27CurrentSourceEvidenceDigest
{
    public const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    public const string OfficialGameplaySceneSha256 =
        "6c35a17693d3cedca2c85b89b22a8bff9b5bae6de88c01b255481c058d2aee40";

    public static string ComputeAllScriptsDigest()
        => Capture().Digest;

    public static V27CurrentSourceEvidenceSnapshot Capture()
    {
        string root = ProjectRoot();
        SourceInput[] inputs = EnumerateInputs(root);
        if (inputs.Length == 0)
            throw new InvalidOperationException(
                "Current-source evidence input set is empty.");

        using SHA256 sha = SHA256.Create();
        using SHA256 pathSha = SHA256.Create();
        foreach (SourceInput input in inputs)
        {
            byte[] raw = File.ReadAllBytes(input.Absolute);
            int offset = HasUtf8Bom(raw) ? 3 : 0;
            string source = StrictUtf8.GetString(raw, offset, raw.Length - offset)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            byte[] bytes = StrictUtf8.GetBytes(
                input.Relative + "\n" + source + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);

            byte[] pathBytes = StrictUtf8.GetBytes(input.Relative + "\n");
            pathSha.TransformBlock(
                pathBytes, 0, pathBytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        pathSha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return new V27CurrentSourceEvidenceSnapshot(
            Hex(sha.Hash),
            inputs.Length,
            Hex(pathSha.Hash));
    }

    public static string ComputeGameplaySceneDigest()
    {
        string absolute = Path.Combine(
            ProjectRoot(),
            GameplayScenePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute))
            throw new InvalidOperationException(
                "Gameplay scene evidence authority is missing: "
                + GameplayScenePath);
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(absolute);
        return Hex(sha.ComputeHash(stream));
    }

    private static string ProjectRoot() =>
        Directory.GetParent(Application.dataPath)?.FullName
        ?? throw new InvalidOperationException("Project root is unavailable.");

    private static readonly UTF8Encoding StrictUtf8 =
        new(false, true);

    private static SourceInput[] EnumerateInputs(string root)
    {
        var inputs = new List<SourceInput>();
        var canonical = new HashSet<string>(StringComparer.Ordinal);
        foreach (string relativeRoot in new[] { "Assets", "Packages" })
        {
            string absoluteRoot = Path.Combine(root, relativeRoot);
            if (!Directory.Exists(absoluteRoot))
                throw new InvalidOperationException(
                    "Current-source evidence root is missing: " + relativeRoot);

            foreach (string absolute in EnumerateFilesWithoutReparsePoints(
                         absoluteRoot, root))
            {
                string relative = CanonicalPath(
                    Path.GetRelativePath(root, absolute));
                if (!IsIncluded(relative))
                    continue;
                if (!canonical.Add(relative))
                    throw new InvalidOperationException(
                        "Duplicate canonical current-source path: " + relative);
                inputs.Add(new SourceInput(absolute, relative));
            }
        }

        inputs.Sort(SourceInput.CompareByUtf8Path);
        return inputs.ToArray();
    }

    private static IEnumerable<string> EnumerateFilesWithoutReparsePoints(
        string absoluteRoot,
        string root)
    {
        var pending = new Stack<string>();
        pending.Push(absoluteRoot);
        while (pending.Count != 0)
        {
            string current = pending.Pop();
            foreach (string directory in Directory.EnumerateDirectories(current))
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException(
                        "Reparse directory is forbidden in current-source evidence: "
                        + CanonicalPath(Path.GetRelativePath(root, directory)));
                pending.Push(directory);
            }

            foreach (string file in Directory.EnumerateFiles(current))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException(
                        "Reparse file is forbidden in current-source evidence: "
                        + CanonicalPath(Path.GetRelativePath(root, file)));
                yield return file;
            }
        }
    }

    private static bool IsIncluded(string relative)
    {
        string extension = Path.GetExtension(relative);
        bool supportedIgnoringCase =
            string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".asmdef", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".asmref", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".rsp", StringComparison.OrdinalIgnoreCase);
        bool supportedCanonical = extension == ".cs"
            || extension == ".asmdef"
            || extension == ".asmref"
            || extension == ".rsp";
        if (supportedIgnoringCase && !supportedCanonical)
            throw new InvalidOperationException(
                "Non-canonical current-source extension: " + relative);

        return supportedCanonical
            || relative == "Packages/manifest.json"
            || relative == "Packages/packages-lock.json";
    }

    private static bool HasUtf8Bom(byte[] bytes) =>
        bytes.Length >= 3
        && bytes[0] == 0xef
        && bytes[1] == 0xbb
        && bytes[2] == 0xbf;

    private static string CanonicalPath(string value) =>
        (value ?? string.Empty)
        .Replace('\\', '/')
        .Normalize(NormalizationForm.FormC);

    private static string Hex(byte[] bytes)
    {
        const string alphabet = "0123456789abcdef";
        char[] result = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = alphabet[bytes[index] >> 4];
            result[index * 2 + 1] = alphabet[bytes[index] & 0x0f];
        }
        return new string(result);
    }

    private sealed class SourceInput
    {
        public SourceInput(string absolute, string relative)
        {
            Absolute = absolute;
            Relative = relative;
            Utf8Path = StrictUtf8.GetBytes(relative);
        }

        public string Absolute { get; }
        public string Relative { get; }
        private byte[] Utf8Path { get; }

        public static int CompareByUtf8Path(SourceInput left, SourceInput right)
        {
            int shared = Math.Min(left.Utf8Path.Length, right.Utf8Path.Length);
            for (int index = 0; index < shared; index++)
            {
                int comparison = left.Utf8Path[index].CompareTo(right.Utf8Path[index]);
                if (comparison != 0)
                    return comparison;
            }

            return left.Utf8Path.Length.CompareTo(right.Utf8Path.Length);
        }
    }
}

public readonly struct V27CurrentSourceEvidenceSnapshot
{
    public V27CurrentSourceEvidenceSnapshot(
        string digest,
        int inputCount,
        string pathListDigest)
    {
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
        InputCount = inputCount > 0
            ? inputCount
            : throw new ArgumentOutOfRangeException(nameof(inputCount));
        PathListDigest = pathListDigest
            ?? throw new ArgumentNullException(nameof(pathListDigest));
    }

    public string Digest { get; }
    public int InputCount { get; }
    public string PathListDigest { get; }
}
#endif
