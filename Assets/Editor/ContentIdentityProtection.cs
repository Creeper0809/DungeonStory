using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Protects published ScriptableObject identities without changing their serialized contract.
/// The checked-in baseline is an append-only inventory keyed by Unity asset GUID.
/// </summary>
public static class ContentIdentityProtection
{
    public const int CurrentSchemaVersion = 1;
    public const string BaselinePath = "ProjectSettings/PublishedContentIdentities.json";
    public const string RootAssetPath = "Assets/Resources/SO/GameContentCatalog.asset";

    [Serializable]
    public sealed class Snapshot
    {
        public int schemaVersion = CurrentSchemaVersion;
        public List<AssetRecord> assets = new();
    }

    [Serializable]
    public sealed class AssetRecord
    {
        public string guid = string.Empty;
        public string scriptGuid = string.Empty;
        public string typeName = string.Empty;
        public List<FieldRecord> fields = new();
    }

    [Serializable]
    public sealed class FieldRecord
    {
        public string path = string.Empty;
        public string value = string.Empty;
    }

    private sealed class BaselineCache
    {
        public bool exists;
        public long writeTicks;
        public Snapshot snapshot;
        public Dictionary<string, AssetRecord> assets;
        public string error = string.Empty;
    }

    private static BaselineCache cachedBaseline;

    /// <summary>
    /// Captures every persistent game ScriptableObject under Assets whose script is owned by
    /// Assets/Scripts outside Editor/vendor folders. This method never writes files.
    /// </summary>
    public static Snapshot CaptureCurrent()
    {
        var snapshot = new Snapshot();
        string[] guids = AssetDatabase.FindAssets(
            "t:ScriptableObject",
            new[] { "Assets" });

        foreach (string guid in guids
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject[] candidates = AssetDatabase
                .LoadAllAssetsAtPath(assetPath)
                .OfType<ScriptableObject>()
                .Where(asset => IsEligibleGameAsset(asset, assetPath))
                .ToArray();
            if (candidates.Length == 0)
                continue;
            if (candidates.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Content identity inventory cannot key {candidates.Length} eligible "
                    + $"ScriptableObjects in '{assetPath}' by one GUID '{guid}'.");
            }

            snapshot.assets.Add(CaptureAsset(guid, candidates[0]));
        }

        SortSnapshot(snapshot);
        return snapshot;
    }

    /// <summary>Returns a deterministic JSON representation without writing the baseline.</summary>
    public static string CaptureCurrentJson() => ToJson(CaptureCurrent());

    public static string CapturePublicationCandidateJson()
    {
        if (!TryLoadBaseline(out Snapshot baseline, out string error))
            throw new InvalidOperationException(error);
        Snapshot current = CaptureCurrent();
        var errors = ValidatePreservedBaseline(baseline, current);
        errors.AddRange(ValidateNewPublication(baseline, current));
        if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
        return ToJson(current);
    }

    public static string ToJson(Snapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        SortSnapshot(snapshot);
        return JsonUtility.ToJson(snapshot, true) + Environment.NewLine;
    }

    /// <summary>
    /// Pure comparison. Existing records and identity values are immutable, while every new
    /// asset or identity field must be explicitly appended before validation can pass.
    /// </summary>
    public static IReadOnlyList<string> Validate(
        Snapshot baseline,
        Snapshot current)
    {
        List<string> errors = ValidatePreservedBaseline(baseline, current);
        if (baseline == null || current == null)
            return errors;

        Dictionary<string, AssetRecord> baselineAssets = UniqueAssets(baseline);
        Dictionary<string, AssetRecord> currentAssets = UniqueAssets(current);
        if (baselineAssets == null || currentAssets == null)
            return errors;

        foreach (AssetRecord currentAsset in current.assets ?? new List<AssetRecord>())
        {
            if (currentAsset == null || string.IsNullOrWhiteSpace(currentAsset.guid))
                continue;
            if (!baselineAssets.TryGetValue(currentAsset.guid, out AssetRecord published))
            {
                errors.Add(
                    $"Unpublished ScriptableObject '{currentAsset.guid}' ({currentAsset.typeName}) "
                    + "must be appended to the content identity baseline.");
                continue;
            }

            Dictionary<string, FieldRecord> publishedFields = UniqueFields(published);
            Dictionary<string, FieldRecord> currentFields = UniqueFields(currentAsset);
            if (publishedFields == null || currentFields == null)
                continue;
            foreach (FieldRecord field in currentAsset.fields ?? new List<FieldRecord>())
            {
                if (field == null || string.IsNullOrWhiteSpace(field.path))
                    continue;
                if (!publishedFields.ContainsKey(field.path))
                {
                    errors.Add(
                        $"Unpublished identity field '{field.path}' on asset "
                        + $"'{currentAsset.guid}' must be explicitly appended.");
                }
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateAll()
    {
        var errors = new List<string>();
        if (!TryLoadBaseline(out Snapshot baseline, out string loadError))
        {
            errors.Add(loadError);
            return errors;
        }

        Snapshot current;
        try
        {
            current = CaptureCurrent();
        }
        catch (Exception exception)
        {
            errors.Add("Content identity inventory capture failed: " + exception.Message);
            return errors;
        }

        errors.AddRange(Validate(baseline, current));
        return errors;
    }

    internal static bool IsPublished(
        SerializedProperty property,
        out string reason)
    {
        reason = string.Empty;
        BaselineCache state = GetCachedBaseline();
        if (!state.exists)
            return false;
        if (state.snapshot == null)
        {
            reason = state.error;
            return true;
        }

        if (state.assets == null)
        {
            reason = "The published content identity baseline has duplicate asset GUIDs.";
            return true;
        }

        foreach (UnityEngine.Object target in property.serializedObject.targetObjects)
        {
            string assetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(assetPath))
                continue;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!state.assets.TryGetValue(guid, out AssetRecord asset))
                continue;
            if ((asset.fields ?? new List<FieldRecord>()).Any(
                    field => field != null
                        && MatchesPublishedPath(field.path, property)))
            {
                reason = "Published content identities are immutable. "
                    + "Create a new unpublished definition when a new ID is required.";
                return true;
            }
        }

        return false;
    }

    // Baseline paths use stable element keys instead of array indices. Resolve
    // against pending Inspector values so reordering cannot unlock an old ID.
    private static bool MatchesPublishedPath(string publishedPath, SerializedProperty property)
    {
        if (!publishedPath.StartsWith("@nested:", StringComparison.Ordinal))
            return property.propertyPath == publishedPath
                || property.propertyPath.StartsWith(publishedPath + ".", StringComparison.Ordinal);
        string remaining = publishedPath.Substring(8);
        string actual = string.Empty;
        while (remaining.IndexOf('[') is int open && open >= 0)
        {
            int close = remaining.IndexOf(']', open);
            if (close < 0) return false;
            actual += remaining.Substring(0, open);
            string prefix = actual + ".Array.data[";
            if (!property.propertyPath.StartsWith(prefix, StringComparison.Ordinal)) return false;
            int indexEnd = property.propertyPath.IndexOf(']', prefix.Length);
            if (indexEnd < 0 || !int.TryParse(property.propertyPath.Substring(prefix.Length,
                    indexEnd - prefix.Length), out int index)) return false;
            SerializedProperty list = property.serializedObject.FindProperty(actual);
            if (list == null || !list.isArray || index >= list.arraySize) return false;
            SerializedProperty element = list.GetArrayElementAtIndex(index);
            string selector = remaining.Substring(open + 1, close - open - 1);
            int typeAt = selector.IndexOf(";type=", StringComparison.Ordinal);
            string type = typeAt < 0 ? null : selector.Substring(typeAt + 6);
            if (typeAt >= 0) selector = selector.Substring(0, typeAt);
            int equals = selector.IndexOf('=');
            if (equals < 0) return false;
            SerializedProperty key = element.FindPropertyRelative(selector.Substring(0, equals));
            if (key == null || key.propertyType != SerializedPropertyType.String
                || key.stringValue != Uri.UnescapeDataString(selector.Substring(equals + 1))) return false;
            if (type != null && (element.propertyType != SerializedPropertyType.ManagedReference
                || element.managedReferenceFullTypename.Split(' ').Last().Split('.').Last() != type)) return false;
            actual = element.propertyPath;
            remaining = remaining.Substring(close + 1);
        }
        return property.propertyPath == actual + remaining;
    }

    internal static bool TryLoadBaseline(
        out Snapshot snapshot,
        out string error)
    {
        string absolutePath = AbsoluteBaselinePath;
        if (!File.Exists(absolutePath))
        {
            snapshot = null;
            error = $"Content identity baseline is missing at '{BaselinePath}'.";
            return false;
        }

        try
        {
            snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(absolutePath));
        }
        catch (Exception exception)
        {
            snapshot = null;
            error = $"Content identity baseline cannot be read: {exception.Message}";
            return false;
        }

        if (snapshot == null)
        {
            error = "Content identity baseline JSON is empty or invalid.";
            return false;
        }

        List<string> shapeErrors = ValidateSnapshotShape(snapshot, "Baseline");
        if (shapeErrors.Count > 0)
        {
            snapshot = null;
            error = string.Join("\n", shapeErrors);
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static IReadOnlyList<string> ValidateAssetForSave(
        string assetPath,
        Snapshot baseline)
    {
        var errors = new List<string>();
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        AssetRecord published = (baseline?.assets ?? new List<AssetRecord>())
            .FirstOrDefault(record => record != null
                && string.Equals(record.guid, guid, StringComparison.OrdinalIgnoreCase));
        if (published == null)
            return errors;

        if (!TryCaptureAssetPath(assetPath, guid, out AssetRecord current, out string captureError))
        {
            errors.Add(captureError);
            return errors;
        }

        errors.AddRange(ValidatePreservedRecord(published, current));
        errors.AddRange(ValidateNewPublication(
            new Snapshot { assets = new List<AssetRecord> { published } },
            new Snapshot { assets = new List<AssetRecord> { current } }));
        errors.AddRange(ValidatePublishedFormulaContracts(baseline));
        return errors;
    }

    // Formula hashes include dependent module/effect authoring. A save of a
    // dependency must not silently invalidate already-saved pending choices.
    internal static IReadOnlyList<string> ValidatePublishedFormulaContracts(Snapshot baseline)
    {
        var errors = new List<string>();
        foreach (AssetRecord published in baseline.assets.Where(asset => asset.fields.Any(
                     field => field.path == "@formula:content")))
        {
            try
            {
                string path = AssetDatabase.GUIDToAssetPath(published.guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                string expected = published.fields.Single(field => field.path == "@formula:content").value;
                if (asset == null || FormulaContentHash(asset) != expected)
                    errors.Add($"Published formula '{path}' changed. Preserve the published definition and author an explicit version migration before replacing its saved contract.");
            }
            catch (Exception exception) { errors.Add("Published formula validation failed: " + exception.Message); }
        }
        return errors;
    }

    private static string FormulaContentHash(ScriptableObject asset)
    {
        if (asset is EquipmentEvolutionFormulaCatalogSO equipment)
            return equipment.ComputeCanonicalSha256();
        if (asset is FacilityEvolutionRecipeSO facility)
            return P1FacilityEvolutionAssetBuilder.ComputeFormulaCatalogSha256(
                facility, facility.formulaCapabilities, facility.formulaPolicy);
        if (asset is CharacterAcquiredTraitSettingsSO acquired)
        {
            GameContentCatalogSO root = AssetDatabase.LoadAssetAtPath<GameContentCatalogSO>(RootAssetPath)
                ?? throw new InvalidOperationException("Game content root is missing.");
            return CharacterAcquiredTraitFormulaCatalogAssetBuilder.ComputeCanonicalCatalogSha256(acquired,
                root.DomainCatalogs.OfType<GameDomainContentCatalogSO>()
                    .SelectMany(domain => domain.Definitions).OfType<CharacterAcquiredTraitModuleSO>());
        }
        return null;
    }

    internal static bool IsEligibleAssetPath(string assetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<ScriptableObject>()
            .Any(asset => IsEligibleGameAsset(asset, assetPath));
    }

    internal static void InvalidateBaselineCache() => cachedBaseline = null;

    [MenuItem("DungeonStory/Content/Append New Published Identities")]
    private static void AppendNewPublishedIdentities()
    {
        if (!TryLoadBaseline(out Snapshot baseline, out string loadError))
        {
            Debug.LogError(
                loadError
                + " Initial publication must use CaptureCurrent or CaptureCurrentJson and "
                + "review the returned snapshot before writing the baseline.");
            return;
        }

        Snapshot current;
        try
        {
            current = CaptureCurrent();
        }
        catch (Exception exception)
        {
            Debug.LogError("Content identity inventory capture failed: " + exception.Message);
            return;
        }

        List<string> immutableErrors = ValidatePreservedBaseline(baseline, current);
        immutableErrors.AddRange(ValidateNewPublication(baseline, current));
        if (immutableErrors.Count > 0)
        {
            Debug.LogError(
                "Cannot append published identities because existing entries changed:\n"
                + string.Join("\n", immutableErrors));
            return;
        }

        Dictionary<string, AssetRecord> published = UniqueAssets(baseline);
        int addedAssets = 0;
        int addedFields = 0;
        foreach (AssetRecord currentAsset in current.assets)
        {
            if (!published.TryGetValue(currentAsset.guid, out AssetRecord publishedAsset))
            {
                baseline.assets.Add(Clone(currentAsset));
                published[currentAsset.guid] = baseline.assets[baseline.assets.Count - 1];
                addedAssets++;
                addedFields += currentAsset.fields.Count;
                continue;
            }

            HashSet<string> paths = new(
                (publishedAsset.fields ?? new List<FieldRecord>())
                    .Where(field => field != null)
                    .Select(field => field.path),
                StringComparer.Ordinal);
            foreach (FieldRecord field in currentAsset.fields)
            {
                if (paths.Add(field.path))
                {
                    publishedAsset.fields.Add(Clone(field));
                    addedFields++;
                }
            }
        }

        if (addedAssets == 0 && addedFields == 0)
        {
            Debug.Log("Published content identity baseline already contains the full inventory.");
            return;
        }

        File.WriteAllText(AbsoluteBaselinePath, ToJson(baseline));
        InvalidateBaselineCache();
        Debug.Log(
            $"Appended {addedAssets} ScriptableObject assets and {addedFields} identity fields "
            + $"to '{BaselinePath}'. Existing entries were preserved.");
    }

    private static List<string> ValidatePreservedBaseline(
        Snapshot baseline,
        Snapshot current)
    {
        var errors = new List<string>();
        errors.AddRange(ValidateSnapshotShape(baseline, "Baseline"));
        errors.AddRange(ValidateSnapshotShape(current, "Current snapshot"));
        if (errors.Count > 0 || baseline == null || current == null)
            return errors;

        Dictionary<string, AssetRecord> baselineAssets = UniqueAssets(baseline);
        Dictionary<string, AssetRecord> currentAssets = UniqueAssets(current);
        if (baselineAssets == null || currentAssets == null)
            return errors;

        foreach (AssetRecord published in baseline.assets)
        {
            if (!currentAssets.TryGetValue(published.guid, out AssetRecord currentAsset))
            {
                errors.Add(
                    $"Published ScriptableObject '{published.guid}' ({published.typeName}) "
                    + "is missing from the current inventory.");
                continue;
            }
            errors.AddRange(ValidatePreservedRecord(published, currentAsset));
        }

        return errors;
    }

    internal static List<string> ValidateNewPublication(Snapshot baseline, Snapshot current)
    {
        var errors = new List<string>();
        var published = UniqueAssets(baseline);
        var registered = current.assets.SelectMany(asset => asset.fields)
            .Where(field => field.path.StartsWith("@registration:", StringComparison.Ordinal))
            .Select(field => field.value).ToHashSet(StringComparer.Ordinal);
        foreach (AssetRecord record in current.assets)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(record.guid);
            if (record.typeName == nameof(GameContentCatalogSO) && assetPath != RootAssetPath)
                errors.Add("The startup content catalog must remain at " + RootAssetPath);
            if (published.TryGetValue(record.guid, out AssetRecord previous))
            {
                var previousPaths = previous.fields.Select(field => field.path).ToHashSet(StringComparer.Ordinal);
                foreach (FieldRecord field in record.fields)
                    if (!previousPaths.Contains(field.path) && string.IsNullOrWhiteSpace(field.value))
                        errors.Add($"New identity '{assetPath}.{field.path}' cannot be empty.");
                continue;
            }
            if (!registered.Contains(record.guid))
                errors.Add($"Unregistered draft '{assetPath}' cannot be published.");
            foreach (FieldRecord field in record.fields)
                if (string.IsNullOrWhiteSpace(field.value))
                    errors.Add($"New identity '{assetPath}.{field.path}' cannot be empty.");
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            MethodInfo validate = asset.GetType().GetMethod("ValidateDefinition",
                BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (validate?.Invoke(asset, null) is IEnumerable<string> definitionErrors)
                errors.AddRange(definitionErrors.Select(value => assetPath + ": " + value));
        }
        return errors;
    }

    private static IReadOnlyList<string> ValidatePreservedRecord(
        AssetRecord published,
        AssetRecord current)
    {
        var errors = new List<string>();
        if (!string.Equals(published.typeName, current.typeName, StringComparison.Ordinal))
        {
            errors.Add(
                $"Published asset '{published.guid}' changed type from "
                + $"'{published.typeName}' to '{current.typeName}'.");
        }
        if (!string.Equals(
                published.scriptGuid,
                current.scriptGuid,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"Published asset '{published.guid}' changed script GUID from "
                + $"'{published.scriptGuid}' to '{current.scriptGuid}'.");
        }

        Dictionary<string, FieldRecord> currentFields = UniqueFields(current);
        if (currentFields == null)
        {
            errors.Add($"Current asset '{current.guid}' has duplicate identity field paths.");
            return errors;
        }

        foreach (FieldRecord field in published.fields ?? new List<FieldRecord>())
        {
            if (field == null || string.IsNullOrWhiteSpace(field.path))
                continue;
            if (!currentFields.TryGetValue(field.path, out FieldRecord currentField))
            {
                errors.Add(
                    $"Published identity field '{field.path}' was removed from asset "
                    + $"'{published.guid}'.");
            }
            else if (!string.Equals(field.value, currentField.value, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Published identity '{field.path}' on asset '{published.guid}' changed "
                    + $"from '{field.value}' to '{currentField.value}'.");
            }
        }

        return errors;
    }

    private static List<string> ValidateSnapshotShape(
        Snapshot snapshot,
        string label)
    {
        var errors = new List<string>();
        if (snapshot == null)
        {
            errors.Add(label + " is null.");
            return errors;
        }
        if (snapshot.schemaVersion != CurrentSchemaVersion)
        {
            errors.Add(
                $"{label} schema version is {snapshot.schemaVersion}; expected "
                + $"{CurrentSchemaVersion}.");
        }
        if (snapshot.assets == null)
        {
            errors.Add(label + " has no assets array.");
            return errors;
        }

        var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (AssetRecord asset in snapshot.assets)
        {
            if (asset == null)
            {
                errors.Add(label + " contains a null asset record.");
                continue;
            }
            if (!IsGuid(asset.guid))
                errors.Add($"{label} contains invalid asset GUID '{asset.guid}'.");
            else if (!guids.Add(asset.guid))
                errors.Add($"{label} contains duplicate asset GUID '{asset.guid}'.");
            if (string.IsNullOrWhiteSpace(asset.typeName))
                errors.Add($"{label} asset '{asset.guid}' has no full type name.");
            if (!IsGuid(asset.scriptGuid))
                errors.Add(
                    $"{label} asset '{asset.guid}' has invalid script GUID "
                    + $"'{asset.scriptGuid}'.");
            if (asset.fields == null)
            {
                errors.Add($"{label} asset '{asset.guid}' has no fields array.");
                continue;
            }

            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (FieldRecord field in asset.fields)
            {
                if (field == null)
                {
                    errors.Add($"{label} asset '{asset.guid}' has a null field record.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(field.path))
                    errors.Add($"{label} asset '{asset.guid}' has an empty field path.");
                else if (!paths.Add(field.path))
                    errors.Add(
                        $"{label} asset '{asset.guid}' contains duplicate field "
                        + $"'{field.path}'.");
                if (field.value == null)
                    errors.Add(
                        $"{label} asset '{asset.guid}' field '{field.path}' has a null value.");
            }
        }

        return errors;
    }

    private static Dictionary<string, AssetRecord> UniqueAssets(Snapshot snapshot)
    {
        if (snapshot?.assets == null)
            return null;
        var result = new Dictionary<string, AssetRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (AssetRecord asset in snapshot.assets)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.guid)
                || !result.TryAdd(asset.guid, asset))
                return null;
        }
        return result;
    }

    private static Dictionary<string, FieldRecord> UniqueFields(AssetRecord asset)
    {
        if (asset?.fields == null)
            return null;
        var result = new Dictionary<string, FieldRecord>(StringComparer.Ordinal);
        foreach (FieldRecord field in asset.fields)
        {
            if (field == null || string.IsNullOrWhiteSpace(field.path)
                || !result.TryAdd(field.path, field))
                return null;
        }
        return result;
    }

    internal static AssetRecord CaptureAsset(string guid, ScriptableObject asset)
    {
        Type type = asset.GetType();
        MonoScript script = MonoScript.FromScriptableObject(asset);
        if (script == null)
        {
            throw new InvalidOperationException(
                $"Game ScriptableObject '{type.FullName}' has no MonoScript.");
        }

        string scriptPath = NormalizePath(AssetDatabase.GetAssetPath(script));
        string scriptGuid = AssetDatabase.AssetPathToGUID(scriptPath);
        if (!IsGuid(scriptGuid))
        {
            throw new InvalidOperationException(
                $"Game ScriptableObject '{type.FullName}' has no stable MonoScript GUID.");
        }
        var record = new AssetRecord
        {
            guid = guid,
            scriptGuid = scriptGuid,
            typeName = type.FullName ?? type.Name
        };
        var serialized = new SerializedObject(asset);
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (FieldInfo field in IdentityFields(type))
        {
            string path = field.Name;
            if (!seenPaths.Add(path))
            {
                throw new InvalidOperationException(
                    $"'{type.FullName}' has multiple ContentIdentity fields serialized as "
                    + $"'{path}'.");
            }
            SerializedProperty property = serialized.FindProperty(path);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"ContentIdentity field '{type.FullName}.{field.Name}' is not serialized "
                    + "by Unity.");
            }
            record.fields.Add(new FieldRecord
            {
                path = path,
                value = ReadIdentityValue(field, property)
            });
        }
        if (asset is GameContentCatalogSO)
        {
            record.fields.Add(new FieldRecord { path = "@resource-path",
                value = NormalizePath(AssetDatabase.GetAssetPath(asset)) });
            foreach (string member in new[] { "itemDefinitions", "worldPresentation",
                         "characterSkillSettings", "media", "domainCatalogs" })
                CaptureRegistrations(serialized.FindProperty(member), member, record);
        }
        else if (asset is GameDomainContentCatalogSO || asset is ItemDefinitionCatalogSO)
            CaptureRegistrations(serialized.FindProperty("definitions"), "definitions", record);
        CaptureNestedIdentities(serialized, record);
        record.fields.Sort((left, right) =>
            string.Compare(left.path, right.path, StringComparison.Ordinal));
        return record;
    }

    private static void CaptureRegistrations(SerializedProperty property, string member,
        AssetRecord record)
    {
        if (property == null) throw new InvalidOperationException(
            $"Missing content registry field '{record.typeName}.{member}'.");
        if (property.isArray)
        {
            for (int i = 0; i < property.arraySize; i++)
                CaptureRegistrations(property.GetArrayElementAtIndex(i), member, record);
            return;
        }
        if (property.propertyType != SerializedPropertyType.ObjectReference
            || property.objectReferenceValue == null)
            throw new InvalidOperationException($"Missing registered definition '{member}'.");
        string guid = AssetDatabase.AssetPathToGUID(
            AssetDatabase.GetAssetPath(property.objectReferenceValue));
        if (!IsGuid(guid)) throw new InvalidOperationException(
            $"Registered definition '{member}' is not a persistent asset.");
        string path = "@registration:" + member + ":" + guid;
        if (record.fields.Any(field => field.path == path))
            throw new InvalidOperationException($"Duplicate content registration '{path}'.");
        record.fields.Add(new FieldRecord { path = path, value = guid });
    }

    private static void CaptureNestedIdentities(SerializedObject source, AssetRecord record)
    {
        string formulaHash = FormulaContentHash(source.targetObject as ScriptableObject);
        if (formulaHash != null)
            record.fields.Add(new FieldRecord { path = "@formula:content", value = formulaHash });
        if (source.targetObject is CharacterTraitSO trait)
            foreach (string alias in trait.LegacyPolicyNames)
                record.fields.Add(new FieldRecord { path = "@legacy-trait-name:" + Uri.EscapeDataString(alias), value = alias });
        void List(string path, string key, string[] leaves = null,
            string child = null, string childKey = null, string[] types = null) =>
            CaptureNestedList(source.FindProperty(path), path, key,
                leaves ?? new[] { key }, record, child, childKey, types);
        if (source.targetObject is ItemDefinitionSO)
        {
            List("features", "ammunitionKindId", types: new[] { nameof(AmmunitionItemFeature) });
            List("features", "substanceId", types: new[] { nameof(SubstanceItemFeature) });
            List("features", "equipmentDefinitionId", types: new[] { nameof(EquipmentItemFeature) });
        }
        switch (record.typeName)
        {
            case nameof(CharacterTraitSO):
                List("identityRules", "ruleId");
                List("combatAbilities.abilities", "id");
                break;
            case nameof(CharacterSpeciesSO): List("combatAbilities.abilities", "id"); break;
            case nameof(AnatomyProfileSO): List("nodes", "nodeId", new[] { "nodeId", "pairedGroupId" }); break;
            case nameof(OffenseDecisionCardSO):
            case nameof(FactionChapterDefinitionSO):
            case nameof(LifeEventDefinitionSO): List("choices", "choiceId"); break;
            case nameof(ServiceIncidentDefinitionSO): List("responses", "choiceId"); break;
            case nameof(OffenseCampaignCatalogSO):
                List("targets", "id");
                List("regions", "id", new[] { "id", "factionId" });
                break;
            case nameof(ProductionRecipeSO): List("outputs", "outputLineId"); break;
            case nameof(CharacterSkillSystemSettingsSO):
                List("modules", "id", new[] { "id", "capabilityId" }, "variants", "id");
                List("drawbackCapabilities", "drawbackId", new[] { "drawbackId", "capabilityId" });
                break;
            case nameof(CharacterAcquiredTraitSettingsSO):
                List("drawbackCapabilities", "drawbackId", new[] { "drawbackId", "capabilityId" },
                    "effects", "bindingId"); break;
            case nameof(CharacterAcquiredTraitModuleSO):
                record.fields.Add(new FieldRecord { path = "capabilityId",
                    value = source.FindProperty("capabilityId").stringValue });
                List("effects", "bindingId"); List("specialReactions", "reactionId"); break;
            case nameof(BuildingSO):
                List("abilityModules.items", "abilityId", types:
                    ((BuildingSO)source.targetObject).AbilityModules.Items
                        .Where(ability => ability is IBuildingPersistentAbilityDefinition)
                        .Select(ability => ability.GetType().Name).Distinct(StringComparer.Ordinal).ToArray());
                List("abilityModules.items", "workstationTag", types: new[] { "BuildingProductionWorkstationAbility" });
                List("abilityModules.items", "serviceHubTag", types: new[] { "BuildingServiceHubAbility" });
                CaptureProviderArray(source, record, "BuildingSemanticTagsAbility", "tags");
                CaptureProviderArray(source, record, "BuildingProductionSupportAbility", "featureTags");
                CaptureProviderArray(source, record, "BuildingServiceSupportAbility", "featureTags");
                CaptureProviderArray(source, record, "BuildingEvolutionAbility", "settings.tags");
                CaptureProviderArray(source, record, "BuildingEvolutionAbility", "settings.scores", "key");
                CaptureProviderArray(source, record, "BuildingEvolutionAbility", "settings.metrics", "key");
                break;
            case nameof(GameDomainContentCatalogSO):
                foreach (string path in new[] { "metaUpgrades", "runVariables", "ownerDoctrines", "invasionPatterns", "characterNeeds" })
                    List(path, "id"); break;
            case nameof(EquipmentEvolutionFormulaCatalogSO): List("capabilities", "capabilityId"); break;
            case nameof(FacilityEvolutionRecipeSO): List("formulaCapabilities", "capabilityId"); break;
        }
    }

    private static void CaptureProviderArray(SerializedObject source, AssetRecord record,
        string providerType, string arrayPath, string valueMember = null)
    {
        const string path = "abilityModules.items";
        SerializedProperty list = source.FindProperty(path);
        if (list == null || !list.isArray) throw new InvalidOperationException("Missing provider collection: " + path);
        string key = arrayPath + (valueMember == null ? "" : "." + valueMember);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            if (element.propertyType != SerializedPropertyType.ManagedReference
                || element.managedReferenceFullTypename.Split(' ').Last().Split('.').Last() != providerType) continue;
            SerializedProperty values = element.FindPropertyRelative(arrayPath);
            if (values == null || !values.isArray)
                throw new InvalidOperationException($"Missing provider values '{providerType}.{arrayPath}'.");
            for (int j = 0; j < values.arraySize; j++)
            {
                SerializedProperty value = values.GetArrayElementAtIndex(j);
                string id = (valueMember == null ? value : value.FindPropertyRelative(valueMember))?.stringValue;
                if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || !seen.Add(id))
                    throw new InvalidOperationException($"Blank, noncanonical or duplicate provider '{providerType}.{key}': '{id}'.");
                string scope = path + "[" + key + "=" + Uri.EscapeDataString(id) + ";type=" + providerType + "]";
                record.fields.Add(new FieldRecord { path = "@nested:" + scope + "." + key, value = id });
            }
        }
    }

    private static void CaptureNestedList(SerializedProperty list, string path, string key,
        string[] leaves, AssetRecord record, string child = null, string childKey = null,
        string[] types = null)
    {
        if (list == null || !list.isArray)
            throw new InvalidOperationException($"Missing nested identity collection '{record.typeName}.{path}'.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            string type = element.propertyType == SerializedPropertyType.ManagedReference
                ? element.managedReferenceFullTypename.Split(' ').Last().Split('.').Last() : string.Empty;
            if (types != null && !types.Contains(type, StringComparer.Ordinal)) continue;
            string id = element.FindPropertyRelative(key)?.stringValue;
            if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || !seen.Add(id))
                throw new InvalidOperationException($"Blank, noncanonical or duplicate nested identity '{path}.{key}': '{id}'.");
            string scope = path + "[" + key + "=" + Uri.EscapeDataString(id)
                + (types == null ? "" : ";type=" + type) + "]";
            foreach (string leaf in leaves)
            {
                string value = element.FindPropertyRelative(leaf)?.stringValue;
                if (leaf == "pairedGroupId")
                {
                    value ??= string.Empty;
                    if (value != value.Trim()) throw new InvalidOperationException($"Noncanonical pair group '{scope}'.");
                    record.fields.Add(new FieldRecord { path = "@nested:" + scope + "." + leaf,
                        value = value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value });
                    continue;
                }
                if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
                    throw new InvalidOperationException($"Invalid nested saved identity '{scope}.{leaf}'.");
                record.fields.Add(new FieldRecord { path = "@nested:" + scope + "." + leaf, value = value });
            }
            if (child != null)
                CaptureNestedList(element.FindPropertyRelative(child), scope + "." + child,
                    childKey, new[] { childKey }, record);
        }
    }

    internal static bool IsRootMove(string source, string destination)
    {
        source = NormalizePath(source).TrimEnd('/');
        destination = NormalizePath(destination).TrimEnd('/');
        return source != destination && (source == RootAssetPath
            || RootAssetPath.StartsWith(source + "/", StringComparison.Ordinal));
    }

    private static IEnumerable<FieldInfo> IdentityFields(Type type)
    {
        var hierarchy = new Stack<Type>();
        for (Type current = type;
             current != null && typeof(ScriptableObject).IsAssignableFrom(current);
             current = current.BaseType)
        {
            hierarchy.Push(current);
        }

        while (hierarchy.Count > 0)
        {
            foreach (FieldInfo field in hierarchy.Pop()
                         .GetFields(
                             BindingFlags.Instance
                             | BindingFlags.Public
                             | BindingFlags.NonPublic
                             | BindingFlags.DeclaredOnly)
                         .Where(field => !field.IsStatic
                             && field.GetCustomAttribute<ContentIdentityAttribute>(false) != null)
                         .OrderBy(field => field.Name, StringComparer.Ordinal))
            {
                yield return field;
            }
        }
    }

    private static string ReadIdentityValue(
        FieldInfo field,
        SerializedProperty property)
    {
        if (field.FieldType == typeof(string)
            && property.propertyType == SerializedPropertyType.String)
        {
            return property.stringValue ?? string.Empty;
        }
        if (field.FieldType == typeof(int)
            && property.propertyType == SerializedPropertyType.Integer)
        {
            return property.intValue.ToString(CultureInfo.InvariantCulture);
        }
        if (field.FieldType.IsEnum
            && property.propertyType == SerializedPropertyType.Enum)
        {
            return property.intValue.ToString(CultureInfo.InvariantCulture);
        }

        if (property.propertyType == SerializedPropertyType.Generic)
        {
            var text = new StringBuilder();
            SerializedProperty cursor = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            bool enter = true;
            do
            {
                string value = cursor.propertyType switch
                {
                    SerializedPropertyType.Generic => "container",
                    SerializedPropertyType.String => cursor.stringValue ?? string.Empty,
                    SerializedPropertyType.Integer or SerializedPropertyType.Enum
                        or SerializedPropertyType.ArraySize => cursor.longValue.ToString(CultureInfo.InvariantCulture),
                    SerializedPropertyType.Float => cursor.doubleValue.ToString("R", CultureInfo.InvariantCulture),
                    SerializedPropertyType.Boolean => cursor.boolValue ? "true" : "false",
                    _ => throw new InvalidOperationException("Unsupported published formula field: " + cursor.propertyPath)
                };
                string key = cursor.propertyPath.Substring(property.propertyPath.Length);
                text.Append(key.Length).Append(':').Append(key).Append(':').Append((int)cursor.propertyType)
                    .Append(':').Append(value.Length).Append(':').Append(value).Append('\n');
                enter = cursor.propertyType == SerializedPropertyType.Generic;
            } while (cursor.Next(enter) && !SerializedProperty.EqualContents(cursor, end));
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        throw new InvalidOperationException(
            $"ContentIdentity field '{field.DeclaringType?.FullName}.{field.Name}' uses "
            + $"unsupported type '{field.FieldType.FullName}' or serialized property type "
            + $"'{property.propertyType}'. Only string, int, and enum are supported.");
    }

    private static bool TryCaptureAssetPath(
        string assetPath,
        string guid,
        out AssetRecord record,
        out string error)
    {
        ScriptableObject[] candidates = AssetDatabase
            .LoadAllAssetsAtPath(assetPath)
            .OfType<ScriptableObject>()
            .Where(asset => IsEligibleGameAsset(asset, assetPath))
            .ToArray();
        if (candidates.Length != 1)
        {
            record = null;
            error = candidates.Length == 0
                ? $"Published asset '{assetPath}' is no longer an eligible game ScriptableObject."
                : $"Published asset '{assetPath}' now contains {candidates.Length} eligible "
                    + "ScriptableObjects under one GUID.";
            return false;
        }

        try
        {
            record = CaptureAsset(guid, candidates[0]);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            record = null;
            error = $"Cannot capture published asset '{assetPath}': {exception.Message}";
            return false;
        }
    }

    private static bool IsEligibleGameAsset(
        ScriptableObject asset,
        string assetPath)
    {
        if (asset == null
            || string.IsNullOrEmpty(assetPath)
            || !NormalizePath(assetPath).StartsWith("Assets/", StringComparison.Ordinal))
        {
            return false;
        }

        MonoScript script = MonoScript.FromScriptableObject(asset);
        if (script == null)
            return false;
        string scriptPath = NormalizePath(AssetDatabase.GetAssetPath(script));
        return scriptPath.StartsWith("Assets/Scripts/", StringComparison.Ordinal)
            && !IsExcludedPath(scriptPath);
    }

    private static bool IsExcludedPath(string path) =>
        path.Contains("/Editor/", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/Vendor/", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/ThirdParty/", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/Plugins/", StringComparison.OrdinalIgnoreCase);

    private static void SortSnapshot(Snapshot snapshot)
    {
        if (snapshot.assets == null)
            snapshot.assets = new List<AssetRecord>();
        foreach (AssetRecord asset in snapshot.assets.Where(value => value != null))
        {
            if (asset.fields == null)
                asset.fields = new List<FieldRecord>();
            asset.fields.Sort((left, right) => string.Compare(
                left?.path,
                right?.path,
                StringComparison.Ordinal));
        }
        snapshot.assets.Sort((left, right) => string.Compare(
            left?.guid,
            right?.guid,
            StringComparison.Ordinal));
    }

    private static AssetRecord Clone(AssetRecord source) => new()
    {
        guid = source.guid,
        scriptGuid = source.scriptGuid,
        typeName = source.typeName,
        fields = source.fields.Select(Clone).ToList()
    };

    private static FieldRecord Clone(FieldRecord source) => new()
    {
        path = source.path,
        value = source.value
    };

    private static bool IsGuid(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length == 32
        && value.All(Uri.IsHexDigit);

    private static string NormalizePath(string value) =>
        (value ?? string.Empty).Replace('\\', '/');

    private static string AbsoluteBaselinePath => Path.Combine(
        Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable."),
        BaselinePath.Replace('/', Path.DirectorySeparatorChar));

    private static BaselineCache GetCachedBaseline()
    {
        string path = AbsoluteBaselinePath;
        bool exists = File.Exists(path);
        long writeTicks = exists ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
        if (cachedBaseline != null
            && cachedBaseline.exists == exists
            && cachedBaseline.writeTicks == writeTicks)
        {
            return cachedBaseline;
        }

        var next = new BaselineCache { exists = exists, writeTicks = writeTicks };
        if (exists)
        {
            if (!TryLoadBaseline(out next.snapshot, out next.error))
                next.snapshot = null;
            else
                next.assets = UniqueAssets(next.snapshot);
        }
        cachedBaseline = next;
        return cachedBaseline;
    }
}

[CustomPropertyDrawer(typeof(ContentIdentityAttribute))]
public sealed class ContentIdentityDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var reference = fieldInfo.GetCustomAttribute<ContentReferenceAttribute>();
        if (reference != null) return ContentReferenceDrawer.ReferenceHeight(reference, property);
        float height = EditorGUIUtility.singleLineHeight;
        if (property.propertyType != SerializedPropertyType.Generic || !property.isExpanded) return height;
        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        if (child.NextVisible(true))
            do
            {
                if (SerializedProperty.EqualContents(child, end)) break;
                height += 2 + EditorGUI.GetPropertyHeight(child, true);
            } while (child.NextVisible(false));
        return height;
    }

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        var reference = fieldInfo.GetCustomAttribute<ContentReferenceAttribute>();
        if (reference != null)
        {
            ContentReferenceDrawer.DrawReference(position, property, label, reference);
            return;
        }
        bool published = ContentIdentityProtection.IsPublished(property, out string reason);
        GUIContent displayed = published && !string.IsNullOrEmpty(reason)
            ? new GUIContent(label.text, reason)
            : label;
        if (property.propertyType != SerializedPropertyType.Generic)
        {
            using (new EditorGUI.DisabledScope(published))
                EditorGUI.PropertyField(position, property, displayed, true);
            return;
        }
        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, displayed, true);
        if (!property.isExpanded) return;
        using var disabled = new EditorGUI.DisabledScope(published);
        using var indent = new EditorGUI.IndentLevelScope();
        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        if (child.NextVisible(true))
            do
            {
                if (SerializedProperty.EqualContents(child, end)) break;
                line.y += line.height + 2;
                line.height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(line, child, true);
            } while (child.NextVisible(false));
    }
}

public sealed class ContentIdentityBuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        IReadOnlyList<string> errors = ContentIdentityProtection.ValidateAll();
        if (errors.Count > 0)
        {
            throw new BuildFailedException(
                "Published content identity validation failed:\n"
                + string.Join("\n", errors));
        }
    }
}

public sealed class ContentIdentityAssetModificationProcessor : AssetModificationProcessor
{
    private static AssetMoveResult OnWillMoveAsset(string source, string destination)
    {
        if (!ContentIdentityProtection.IsRootMove(source, destination))
            return AssetMoveResult.DidNotMove;
        Debug.LogError("Moving the startup content catalog or its parent folder is blocked: "
            + ContentIdentityProtection.RootAssetPath);
        return AssetMoveResult.FailedMove;
    }

    private static string[] OnWillSaveAssets(string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return Array.Empty<string>();

        bool baselineExists = File.Exists(Path.Combine(
            Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
            ContentIdentityProtection.BaselinePath.Replace(
                '/',
                Path.DirectorySeparatorChar)));
        bool loaded = ContentIdentityProtection.TryLoadBaseline(
            out ContentIdentityProtection.Snapshot baseline,
            out string loadError);
        if (!baselineExists)
            return paths;

        var accepted = new List<string>(paths.Length);
        foreach (string path in paths)
        {
            if (!loaded)
            {
                if (CouldContainGameContent(path))
                {
                    Debug.LogError(
                        $"Saving '{path}' was blocked because {loadError} The in-memory value "
                        + "was left unchanged and the previous disk asset was preserved.");
                }
                else
                {
                    accepted.Add(path);
                }
                continue;
            }

            bool eligible = ContentIdentityProtection.IsEligibleAssetPath(path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            bool published = baseline?.assets?.Any(record => record != null
                && string.Equals(record.guid, guid, StringComparison.OrdinalIgnoreCase)) == true;
            if (!eligible && !published)
            {
                accepted.Add(path);
                continue;
            }
            IReadOnlyList<string> errors =
                ContentIdentityProtection.ValidateAssetForSave(path, baseline);
            if (errors.Count == 0)
            {
                accepted.Add(path);
                continue;
            }
            Debug.LogError(
                $"Saving '{path}' was blocked by published content identity protection:\n"
                + string.Join("\n", errors)
                + "\nThe in-memory value was left unchanged and the previous disk asset "
                + "was preserved.");
        }

        return accepted.ToArray();
    }

    private static AssetDeleteResult OnWillDeleteAsset(
        string assetPath,
        RemoveAssetOptions options)
    {
        bool baselineExists = File.Exists(Path.Combine(
            Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
            ContentIdentityProtection.BaselinePath.Replace(
                '/',
                Path.DirectorySeparatorChar)));
        if (!baselineExists)
            return AssetDeleteResult.DidNotDelete;
        if (!ContentIdentityProtection.TryLoadBaseline(
                out ContentIdentityProtection.Snapshot baseline,
                out string loadError))
        {
            if (CouldContainGameContent(assetPath))
            {
                Debug.LogError(
                    $"Deleting '{assetPath}' was blocked because {loadError}");
                return AssetDeleteResult.FailedDelete;
            }
            return AssetDeleteResult.DidNotDelete;
        }

        string normalized = Normalize(assetPath).TrimEnd('/');
        foreach (ContentIdentityProtection.AssetRecord record in baseline.assets)
        {
            string publishedPath = Normalize(AssetDatabase.GUIDToAssetPath(record.guid));
            if (string.IsNullOrEmpty(publishedPath))
                continue;
            if (string.Equals(normalized, publishedPath, StringComparison.Ordinal)
                || publishedPath.StartsWith(normalized + "/", StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"Deleting '{assetPath}' was blocked because it contains published "
                    + $"ScriptableObject '{publishedPath}' ({record.guid}).");
                return AssetDeleteResult.FailedDelete;
            }
        }

        return AssetDeleteResult.DidNotDelete;
    }

    private static bool CouldContainGameContent(string assetPath)
    {
        string normalized = Normalize(assetPath).TrimEnd('/');
        bool folder = AssetDatabase.IsValidFolder(assetPath);
        bool relatedFolder = folder
            && (string.Equals(normalized, "Assets", StringComparison.Ordinal)
                || normalized.StartsWith("Assets/", StringComparison.Ordinal));
        bool possibleAsset = normalized.StartsWith("Assets/", StringComparison.Ordinal)
            && string.Equals(
                Path.GetExtension(normalized),
                ".asset",
                StringComparison.OrdinalIgnoreCase);
        return relatedFolder
            || possibleAsset
            || ContentIdentityProtection.IsEligibleAssetPath(assetPath);
    }

    private static string Normalize(string path) =>
        (path ?? string.Empty).Replace('\\', '/');
}
