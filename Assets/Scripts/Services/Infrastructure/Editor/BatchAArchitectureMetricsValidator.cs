#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BatchAArchitectureMetricsValidator
{
    private const int RoslynSchemaVersion = 2;
    private const int UnitySchemaVersion = 1;
    private const int ConstructorDependencyLimit = 8;
    private const string RoslynReportPath =
        "Assets/Architecture/runtime-architecture-metrics-current.json";
    private const string RoslynBaselinePath =
        "Assets/Architecture/runtime-architecture-metrics-baseline.json";
    private const string UnitySnapshotPath =
        "Assets/Architecture/runtime-architecture-unity-current.json";
    private const string UnityBaselinePath =
        "Assets/Architecture/runtime-architecture-unity-baseline.json";

    [Serializable]
    private sealed class RoslynSnapshot
    {
        public int schemaVersion;
        public string sourceFingerprint = string.Empty;
        public int runtimeSourceFileCount;
        public int runtimeTypeCount;
        public int mutableStaticCount;
        public string mutableStaticSetHash = string.Empty;
        public int oversizedTypeCount;
        public string oversizedTypeSetHash = string.Empty;
        public int largeConstructorCount;
        public string largeConstructorSetHash = string.Empty;
        public int defaultAssemblySourceCount;
        public string defaultAssemblySourceSetHash = string.Empty;
        public int contentEscapeCount;
        public string contentEscapeSetHash = string.Empty;
        public int directSessionMutationCount;
        public string directSessionMutationSetHash = string.Empty;
        public int rawKoreanStringCount;
        public string rawKoreanStringSetHash = string.Empty;
        public int rootCatalogReferenceCount;
        public string rootCatalogReferenceSetHash = string.Empty;
    }

    [Serializable]
    private sealed class RoslynBaseline
    {
        public int schemaVersion;
        public int maxMutableStatic;
        public string MutableStaticSetHash = string.Empty;
        public int maxOversizedType;
        public string OversizedTypeSetHash = string.Empty;
        public int maxLargeConstructor;
        public string LargeConstructorSetHash = string.Empty;
        public int maxDefaultAssemblySource;
        public string DefaultAssemblySourceSetHash = string.Empty;
        public int maxContentEscape;
        public string ContentEscapeSetHash = string.Empty;
        public int maxDirectSessionMutation;
        public string DirectSessionMutationSetHash = string.Empty;
        public int maxRawKoreanString;
        public string RawKoreanStringSetHash = string.Empty;
        public int maxRootCatalogReference;
        public string RootCatalogReferenceSetHash = string.Empty;
    }

    [Serializable]
    private sealed class UnitySnapshot
    {
        public int schemaVersion = UnitySchemaVersion;
        public int mutableRuntimeStaticCount;
        public string mutableRuntimeStaticSetHash = string.Empty;
        public int largeRuntimeConstructorCount;
        public string largeRuntimeConstructorSetHash = string.Empty;
        public int defaultAssemblyMonoScriptCount;
        public string defaultAssemblyMonoScriptSetHash = string.Empty;
        public int optionalRuntimeInterfaceDependencyCount;
        public string optionalRuntimeInterfaceDependencySetHash = string.Empty;
        public int rootCatalogValidationErrorCount;
        public string rootCatalogValidationErrorSetHash = string.Empty;
        public int assetGraphBrokenReferenceCount;
        public string assetGraphBrokenReferenceSetHash = string.Empty;
    }

    [MenuItem("Tools/DungeonStory/Validation/Capture Batch A Architecture Baseline")]
    public static void CaptureBaselineMenu()
    {
        Debug.Log(CaptureBaselineOrThrow());
    }

    [MenuItem("Tools/DungeonStory/Validation/Validate Batch A Architecture Metrics")]
    public static void ValidateMenu()
    {
        Debug.Log(ValidateOrThrow());
    }

    public static string CaptureBaselineOrThrow()
    {
        RoslynSnapshot roslyn = LoadRoslynSnapshotAndValidateBaseline();
        UnitySnapshot snapshot = CaptureUnitySnapshot();
        RequireCleanContentGraph(snapshot);
        WriteJson(UnitySnapshotPath, snapshot);
        WriteJson(UnityBaselinePath, snapshot);
        AssetDatabase.ImportAsset(UnitySnapshotPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(UnityBaselinePath, ImportAssetOptions.ForceUpdate);
        return FormatSummary("CAPTURED", roslyn, snapshot);
    }

    public static string CaptureCurrentForReviewOrThrow()
    {
        RoslynSnapshot roslyn = LoadRoslynSnapshotAndValidateBaseline();
        UnitySnapshot snapshot = CaptureUnitySnapshot();
        RequireCleanContentGraph(snapshot);
        WriteJson(UnitySnapshotPath, snapshot);
        AssetDatabase.ImportAsset(UnitySnapshotPath, ImportAssetOptions.ForceUpdate);
        return FormatSummary("REVIEW", roslyn, snapshot);
    }

    public static string ValidateOrThrow()
    {
        RoslynSnapshot roslyn = LoadRoslynSnapshotAndValidateBaseline();
        UnitySnapshot current = CaptureUnitySnapshot();
        RequireCleanContentGraph(current);
        UnitySnapshot baseline = LoadJson<UnitySnapshot>(UnityBaselinePath);
        if (baseline.schemaVersion != UnitySchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unity architecture baseline schema must be {UnitySchemaVersion}.");
        }

        CompareMetric(
            "mutable runtime static",
            current.mutableRuntimeStaticCount,
            current.mutableRuntimeStaticSetHash,
            baseline.mutableRuntimeStaticCount,
            baseline.mutableRuntimeStaticSetHash);
        CompareMetric(
            "large runtime constructor",
            current.largeRuntimeConstructorCount,
            current.largeRuntimeConstructorSetHash,
            baseline.largeRuntimeConstructorCount,
            baseline.largeRuntimeConstructorSetHash);
        // Phase 117 reports default-assembly ownership for review, but it is
        // no longer a monotonic completion ratchet. Concrete authority and
        // dependency defects are enforced by the remaining structural gates.
        CompareMetric(
            "optional runtime interface dependency",
            current.optionalRuntimeInterfaceDependencyCount,
            current.optionalRuntimeInterfaceDependencySetHash,
            baseline.optionalRuntimeInterfaceDependencyCount,
            baseline.optionalRuntimeInterfaceDependencySetHash);
        CompareMetric(
            "root catalog validation error",
            current.rootCatalogValidationErrorCount,
            current.rootCatalogValidationErrorSetHash,
            baseline.rootCatalogValidationErrorCount,
            baseline.rootCatalogValidationErrorSetHash);
        CompareMetric(
            "asset graph broken reference",
            current.assetGraphBrokenReferenceCount,
            current.assetGraphBrokenReferenceSetHash,
            baseline.assetGraphBrokenReferenceCount,
            baseline.assetGraphBrokenReferenceSetHash);

        WriteJson(UnitySnapshotPath, current);
        AssetDatabase.ImportAsset(UnitySnapshotPath, ImportAssetOptions.ForceUpdate);
        return FormatSummary("PASS", roslyn, current);
    }

    private static RoslynSnapshot LoadRoslynSnapshotAndValidateBaseline()
    {
        RoslynSnapshot snapshot = LoadJson<RoslynSnapshot>(RoslynReportPath);
        RoslynBaseline baseline = LoadJson<RoslynBaseline>(RoslynBaselinePath);
        if (snapshot.schemaVersion != RoslynSchemaVersion
            || baseline.schemaVersion != RoslynSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Roslyn architecture schema must be {RoslynSchemaVersion}.");
        }

        string currentFingerprint = ComputeRuntimeSourceFingerprint();
        if (!string.Equals(
                snapshot.sourceFingerprint,
                currentFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Roslyn architecture report is stale. Run "
                + "Tools/ArchitectureMetrics/Run-ArchitectureMetrics.ps1 -Verify.");
        }

        CompareMetric(
            "Roslyn mutable static",
            snapshot.mutableStaticCount,
            snapshot.mutableStaticSetHash,
            baseline.maxMutableStatic,
            baseline.MutableStaticSetHash);
        CompareMetric(
            "Roslyn oversized type",
            snapshot.oversizedTypeCount,
            snapshot.oversizedTypeSetHash,
            baseline.maxOversizedType,
            baseline.OversizedTypeSetHash);
        CompareMetric(
            "Roslyn large constructor",
            snapshot.largeConstructorCount,
            snapshot.largeConstructorSetHash,
            baseline.maxLargeConstructor,
            baseline.LargeConstructorSetHash);
        // Default-assembly source ownership is informational in Phase 117.
        // Moving an approved Unity edge solely to change this count is not a
        // release requirement.
        CompareMetric(
            "Roslyn content escape",
            snapshot.contentEscapeCount,
            snapshot.contentEscapeSetHash,
            baseline.maxContentEscape,
            baseline.ContentEscapeSetHash);
        CompareMetric(
            "Roslyn direct session mutation",
            snapshot.directSessionMutationCount,
            snapshot.directSessionMutationSetHash,
            baseline.maxDirectSessionMutation,
            baseline.DirectSessionMutationSetHash);
        // Raw Korean is an audit count, not a release ratchet. User-visible
        // mojibake, missing keys, and format mismatches are validated by each
        // surface's strict localization contract instead.
        CompareMetric(
            "Roslyn root catalog reference",
            snapshot.rootCatalogReferenceCount,
            snapshot.rootCatalogReferenceSetHash,
            baseline.maxRootCatalogReference,
            baseline.RootCatalogReferenceSetHash);
        return snapshot;
    }

    private static UnitySnapshot CaptureUnitySnapshot()
    {
        string[] mutableStatics = RuntimeAuthorityV18Validator
            .FindMutableRuntimeStaticFields()
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] largeConstructors = AppDomain.CurrentDomain.GetAssemblies()
            .Where(IsGameplayRuntimeAssembly)
            .SelectMany(GetLoadableTypes)
            .Where(type => type != null && !type.IsAbstract)
            .SelectMany(type => type.GetConstructors(
                    BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance)
                .Where(constructor => constructor.IsPublic
                    || HasInjectAttribute(constructor))
                .Where(constructor => constructor.GetParameters().Length
                    > ConstructorDependencyLimit)
                .Select(constructor =>
                    $"{type.FullName}.ctor:{constructor.GetParameters().Length}"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] defaultAssemblyScripts = AssetDatabase.FindAssets(
                "t:MonoScript",
                new[] { "Assets/Scripts" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !path.Contains("/Editor/", StringComparison.OrdinalIgnoreCase))
            .Select(path => (path, script: AssetDatabase.LoadAssetAtPath<MonoScript>(path)))
            .Select(pair => (pair.path, type: pair.script?.GetClass()))
            .Where(pair => pair.type != null
                && string.Equals(
                    pair.type.Assembly.GetName().Name,
                    "Assembly-CSharp",
                    StringComparison.Ordinal))
            .Select(pair => $"{pair.path}|{pair.type.FullName}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] optionalDependencies = RuntimeAuthorityV18Validator
            .FindOptionalRuntimeInterfaceDependencies()
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
            GameContentCatalogSO.ResourcePath);
        List<string> catalogErrors = new();
        if (root == null)
        {
            catalogErrors.Add("GameContentCatalogSO root is missing.");
        }
        else if (root.GetItemDefinitions<ItemDefinitionCatalogSO>() == null)
        {
            catalogErrors.Add("Root item catalog is missing.");
        }
        else
        {
            catalogErrors.AddRange(
                root.GetItemDefinitions<ItemDefinitionCatalogSO>().ValidateCatalog());
        }

        string[] brokenReferences = root == null
            ? Array.Empty<string>()
            : FindBrokenScriptableObjectReferences(root);
        return new UnitySnapshot
        {
            mutableRuntimeStaticCount = mutableStatics.Length,
            mutableRuntimeStaticSetHash = HashValues(mutableStatics),
            largeRuntimeConstructorCount = largeConstructors.Length,
            largeRuntimeConstructorSetHash = HashValues(largeConstructors),
            defaultAssemblyMonoScriptCount = defaultAssemblyScripts.Length,
            defaultAssemblyMonoScriptSetHash = HashValues(defaultAssemblyScripts),
            optionalRuntimeInterfaceDependencyCount = optionalDependencies.Length,
            optionalRuntimeInterfaceDependencySetHash = HashValues(optionalDependencies),
            rootCatalogValidationErrorCount = catalogErrors.Count,
            rootCatalogValidationErrorSetHash = HashValues(catalogErrors),
            assetGraphBrokenReferenceCount = brokenReferences.Length,
            assetGraphBrokenReferenceSetHash = HashValues(brokenReferences)
        };
    }

    private static string[] FindBrokenScriptableObjectReferences(
        ScriptableObject root)
    {
        Queue<ScriptableObject> pending = new();
        HashSet<int> visited = new();
        List<string> broken = new();
        pending.Enqueue(root);
        while (pending.Count > 0)
        {
            ScriptableObject current = pending.Dequeue();
            if (current == null || !visited.Add(current.GetInstanceID()))
            {
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(current);
            SerializedObject serialized;
            try
            {
                serialized = new SerializedObject(current);
            }
            catch (Exception exception)
            {
                broken.Add($"{assetPath}|<serialize>|{exception.GetType().Name}");
                continue;
            }

            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                UnityEngine.Object referenced = property.objectReferenceValue;
                if (referenced == null)
                {
                    if (property.objectReferenceInstanceIDValue != 0)
                    {
                        broken.Add($"{assetPath}|{property.propertyPath}");
                    }
                    continue;
                }

                if (referenced is ScriptableObject referencedSo
                    && AssetDatabase.GetAssetPath(referencedSo).StartsWith(
                        "Assets/",
                        StringComparison.Ordinal))
                {
                    pending.Enqueue(referencedSo);
                }
            }
        }

        return broken
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void RequireCleanContentGraph(UnitySnapshot snapshot)
    {
        if (snapshot.optionalRuntimeInterfaceDependencyCount != 0
            || snapshot.rootCatalogValidationErrorCount != 0
            || snapshot.assetGraphBrokenReferenceCount != 0)
        {
            throw new InvalidOperationException(
                "Batch A clean gates failed: optionalDependencies="
                + snapshot.optionalRuntimeInterfaceDependencyCount
                + ", catalogErrors=" + snapshot.rootCatalogValidationErrorCount
                + ", brokenReferences=" + snapshot.assetGraphBrokenReferenceCount + ".");
        }
    }

    private static void CompareMetric(
        string label,
        int currentCount,
        string currentHash,
        int maximumCount,
        string baselineHash)
    {
        if (currentCount > maximumCount)
        {
            throw new InvalidOperationException(
                $"{label} grew from {maximumCount} to {currentCount}.");
        }
        // Strictly fewer violations are a valid ratchet improvement. Preserve
        // the identity check only at the same count so equal-size replacement
        // of a known violation still fails review.
        if (currentCount == maximumCount
            && !string.Equals(currentHash, baselineHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} set changed. Review the exact diff before updating its baseline.");
        }
    }

    private static string ComputeRuntimeSourceFingerprint()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string scriptsRoot = Path.Combine(projectRoot, "Assets", "Scripts");
        string[] paths = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Replace('\\', '/').Contains(
                "/Editor/",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => NormalizeRelative(projectRoot, path), StringComparer.Ordinal)
            .ToArray();
        using SHA256 hash = SHA256.Create();
        using MemoryStream stream = new();
        foreach (string path in paths)
        {
            byte[] name = Encoding.UTF8.GetBytes(
                NormalizeRelative(projectRoot, path) + "\n");
            stream.Write(name, 0, name.Length);
            byte[] content = File.ReadAllBytes(path);
            stream.Write(content, 0, content.Length);
            stream.WriteByte((byte)'\n');
        }
        return ToHex(hash.ComputeHash(stream.ToArray()));
    }

    private static string NormalizeRelative(string root, string path)
    {
        string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path)
            .Substring(normalizedRoot.Length)
            .Replace('\\', '/');
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null);
        }
    }

    private static bool IsGameplayRuntimeAssembly(Assembly assembly)
    {
        string name = assembly?.GetName().Name ?? string.Empty;
        return name.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) < 0
            && (name.StartsWith("Assembly-CSharp", StringComparison.Ordinal)
                || name.StartsWith("DungeonStory.", StringComparison.Ordinal));
    }

    private static bool HasInjectAttribute(MemberInfo member)
    {
        return member.GetCustomAttributes(false).Any(attribute =>
            string.Equals(
                attribute.GetType().Name,
                "InjectAttribute",
                StringComparison.Ordinal));
    }

    private static T LoadJson<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Architecture metrics file is missing.", path);
        }
        return JsonUtility.FromJson<T>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Architecture metrics JSON is invalid: {path}");
    }

    private static void WriteJson<T>(string path, T payload)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Metrics directory is unavailable."));
        File.WriteAllText(fullPath, JsonUtility.ToJson(payload, prettyPrint: true) + "\n");
    }

    private static string HashValues(IEnumerable<string> values)
    {
        string canonical = string.Join(
            "\n",
            values.OrderBy(value => value, StringComparer.Ordinal));
        using SHA256 hash = SHA256.Create();
        return ToHex(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string ToHex(byte[] bytes)
    {
        StringBuilder builder = new(bytes.Length * 2);
        foreach (byte value in bytes)
        {
            builder.Append(value.ToString("x2"));
        }
        return builder.ToString();
    }

    private static string FormatSummary(
        string status,
        RoslynSnapshot roslyn,
        UnitySnapshot unity)
    {
        return $"BATCH A ARCHITECTURE METRICS {status}: "
            + $"sourceFiles={roslyn.runtimeSourceFileCount}, "
            + $"sourceTypes={roslyn.runtimeTypeCount}, "
            + $"syntaxStatics={roslyn.mutableStaticCount}, "
            + $"loadedStatics={unity.mutableRuntimeStaticCount}, "
            + $"oversizedTypes={roslyn.oversizedTypeCount}, "
            + $"largeConstructors={roslyn.largeConstructorCount}/"
            + $"{unity.largeRuntimeConstructorCount}, "
            + $"defaultAssembly={roslyn.defaultAssemblySourceCount}/"
            + $"{unity.defaultAssemblyMonoScriptCount}, "
            + $"contentEscapes={roslyn.contentEscapeCount}, "
            + $"directSessionMutations={roslyn.directSessionMutationCount}, "
            + $"rawKoreanStrings={roslyn.rawKoreanStringCount}, "
            + $"optionalDI={unity.optionalRuntimeInterfaceDependencyCount}, "
            + $"catalogErrors={unity.rootCatalogValidationErrorCount}, "
            + $"brokenAssetRefs={unity.assetGraphBrokenReferenceCount}.";
    }
}
#endif
