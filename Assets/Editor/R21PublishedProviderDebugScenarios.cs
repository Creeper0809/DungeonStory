using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R21PublishedProviderDebugScenarios
{
    private sealed class NestedLocation
    {
        public ContentIdentityProtection.AssetRecord owner;
        public ContentIdentityProtection.FieldRecord publishedField;
        public ScriptableObject source;
        public string collectionPath = string.Empty;
        public string key = string.Empty;
        public string leaf = string.Empty;
        public string providerType = string.Empty;
        public string keyValue = string.Empty;
        public string value = string.Empty;
    }

    private sealed class ScalarLocation
    {
        public ContentIdentityProtection.AssetRecord owner;
        public ContentIdentityProtection.FieldRecord publishedField;
        public ScriptableObject source;
        public string propertyPath = string.Empty;
        public string value = string.Empty;
    }

    private sealed class SourceState
    {
        public ScriptableObject source;
        public string guid = string.Empty;
        public string assetPath = string.Empty;
        public string absolutePath = string.Empty;
        public string json = string.Empty;
        public bool dirty;
        public byte[] bytes = Array.Empty<byte>();
    }

    public static void RunAll()
    {
        Require(
            ContentIdentityProtection.TryLoadBaseline(
                out ContentIdentityProtection.Snapshot baseline,
                out string loadError),
            loadError);
        Require(
            baseline.schemaVersion == ContentIdentityProtection.CurrentSchemaVersion,
            $"Published identity schema changed from "
            + $"{ContentIdentityProtection.CurrentSchemaVersion} to {baseline.schemaVersion}.");

        NestedLocation speciesAbility = FindNestedLocation(
            baseline,
            nameof(CharacterSpeciesSO),
            "combatAbilities.abilities",
            "id",
            "id",
            null,
            "character species combat ability");
        NestedLocation ammunition = FindNestedLocation(
            baseline,
            nameof(ResourceItemDefinitionSO),
            "features",
            "ammunitionKindId",
            "ammunitionKindId",
            nameof(AmmunitionItemFeature),
            "resource ammunition kind");
        NestedLocation substance = FindNestedLocation(
            baseline,
            nameof(ResourceItemDefinitionSO),
            "features",
            "substanceId",
            "substanceId",
            nameof(SubstanceItemFeature),
            "resource substance");
        ScalarLocation anatomyFamily = FindScalarLocation(
            baseline,
            nameof(AnatomyProfileSO),
            "anatomyFamily",
            "anatomy family");
        ScalarLocation enemyFaction = FindScalarLocation(
            baseline,
            nameof(EnemyArchetypeDefinitionSO),
            "factionId",
            "enemy faction");
        NestedLocation regionId = FindNestedLocation(
            baseline,
            nameof(OffenseCampaignCatalogSO),
            "regions",
            "id",
            "id",
            null,
            "campaign region id");
        NestedLocation regionFaction = FindNestedLocation(
            baseline,
            nameof(OffenseCampaignCatalogSO),
            "regions",
            "id",
            "factionId",
            null,
            "campaign region faction");
        Require(
            string.Equals(
                regionId.keyValue,
                regionFaction.keyValue,
                StringComparison.Ordinal),
            "Campaign region ID and faction fixtures selected different rows.");

        SourceState[] sources = CaptureSourceStates(
            speciesAbility,
            ammunition,
            substance,
            anatomyFamily,
            enemyFaction,
            regionId);
        int scenarios = 0;
        try
        {
            VerifyRejectedMutation(
                baseline,
                speciesAbility.owner,
                speciesAbility.publishedField,
                speciesAbility.source,
                serialized => RenameNested(serialized, speciesAbility),
                "character species combat ability rename");
            scenarios++;
            VerifyRejectedMutation(
                baseline,
                ammunition.owner,
                ammunition.publishedField,
                ammunition.source,
                serialized => RenameNested(serialized, ammunition),
                "resource ammunition kind rename");
            scenarios++;
            VerifyRejectedMutation(
                baseline,
                substance.owner,
                substance.publishedField,
                substance.source,
                serialized => RenameNested(serialized, substance),
                "resource substance rename");
            scenarios++;
            VerifyRejectedMutation(
                baseline,
                anatomyFamily.owner,
                anatomyFamily.publishedField,
                anatomyFamily.source,
                serialized => RenameScalar(serialized, anatomyFamily),
                "anatomy family rename");
            scenarios++;
            VerifyRejectedMutation(
                baseline,
                enemyFaction.owner,
                enemyFaction.publishedField,
                enemyFaction.source,
                serialized => RenameScalar(serialized, enemyFaction),
                "enemy faction rename");
            scenarios++;
            VerifyRejectedMutation(
                baseline,
                regionId.owner,
                regionId.publishedField,
                regionId.source,
                serialized => RenameNested(serialized, regionId),
                "campaign region id rename");
            scenarios++;
            VerifyRejectedMutation(
                baseline,
                regionFaction.owner,
                regionFaction.publishedField,
                regionFaction.source,
                serialized => RenameNested(serialized, regionFaction),
                "campaign region faction rename");
            scenarios++;

            VerifyRejectedMutation(
                baseline,
                speciesAbility.owner,
                speciesAbility.publishedField,
                speciesAbility.source,
                serialized => RemoveNested(serialized, speciesAbility),
                "character species combat ability removal");
            scenarios++;
            VerifyRejectedMutation(
                baseline,
                ammunition.owner,
                ammunition.publishedField,
                ammunition.source,
                serialized => RemoveNested(serialized, ammunition),
                "resource ammunition feature removal");
            scenarios++;
            VerifyRejectedMutation(
                baseline,
                substance.owner,
                substance.publishedField,
                substance.source,
                serialized => RemoveNested(serialized, substance),
                "resource substance feature removal");
            scenarios++;
            VerifyRejectedMutation(
                baseline,
                regionId.owner,
                regionId.publishedField,
                regionId.source,
                serialized => RemoveNested(serialized, regionId),
                "campaign region removal");
            scenarios++;

            VerifyRegionAppend(baseline, regionFaction);
            scenarios++;
        }
        finally
        {
            VerifySourceStates(sources);
        }

        Require(scenarios == 12, "Not all R21 published provider scenarios executed.");
        Debug.Log(
            "R21_PUBLISHED_PROVIDER=PASS; scenarios=12; baseline-only; "
            + "record-capture-only; source-json-dirty-bytes-preserved");
    }

    private static NestedLocation FindNestedLocation(
        ContentIdentityProtection.Snapshot baseline,
        string ownerType,
        string collectionPath,
        string key,
        string leaf,
        string providerType,
        string label)
    {
        foreach (ContentIdentityProtection.AssetRecord owner in baseline.assets
                     .Where(value => TypeMatches(value?.typeName, ownerType))
                     .OrderBy(value => value.guid, StringComparer.Ordinal))
        {
            ScriptableObject source = LoadSource(owner);
            using var serialized = new SerializedObject(source);
            SerializedProperty list = serialized.FindProperty(collectionPath);
            if (list == null || !list.isArray)
                continue;
            for (int index = 0; index < list.arraySize; index++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(index);
                if (providerType != null && !ManagedTypeMatches(element, providerType))
                    continue;
                SerializedProperty keyProperty = element.FindPropertyRelative(key);
                SerializedProperty leafProperty = element.FindPropertyRelative(leaf);
                string keyValue = keyProperty?.stringValue;
                string value = leafProperty?.stringValue;
                if (string.IsNullOrWhiteSpace(keyValue)
                    || string.IsNullOrWhiteSpace(value))
                    continue;

                string path = NestedPath(
                    collectionPath,
                    key,
                    keyValue,
                    leaf,
                    providerType);
                ContentIdentityProtection.FieldRecord published = owner.fields
                    .SingleOrDefault(field => string.Equals(
                        field.path,
                        path,
                        StringComparison.Ordinal));
                if (published == null
                    || !string.Equals(published.value, value, StringComparison.Ordinal))
                    continue;
                return new NestedLocation
                {
                    owner = owner,
                    publishedField = published,
                    source = source,
                    collectionPath = collectionPath,
                    key = key,
                    leaf = leaf,
                    providerType = providerType ?? string.Empty,
                    keyValue = keyValue,
                    value = value
                };
            }
        }

        throw new InvalidOperationException(
            "Missing nonempty published provider for " + label + ".");
    }

    private static ScalarLocation FindScalarLocation(
        ContentIdentityProtection.Snapshot baseline,
        string ownerType,
        string propertyPath,
        string label)
    {
        foreach (ContentIdentityProtection.AssetRecord owner in baseline.assets
                     .Where(value => TypeMatches(value?.typeName, ownerType))
                     .OrderBy(value => value.guid, StringComparer.Ordinal))
        {
            ScriptableObject source = LoadSource(owner);
            using var serialized = new SerializedObject(source);
            string value = serialized.FindProperty(propertyPath)?.stringValue;
            if (string.IsNullOrWhiteSpace(value))
                continue;
            ContentIdentityProtection.FieldRecord published = owner.fields
                .SingleOrDefault(field => string.Equals(
                    field.path,
                    propertyPath,
                    StringComparison.Ordinal));
            if (published == null
                || !string.Equals(published.value, value, StringComparison.Ordinal))
                continue;
            return new ScalarLocation
            {
                owner = owner,
                publishedField = published,
                source = source,
                propertyPath = propertyPath,
                value = value
            };
        }

        throw new InvalidOperationException(
            "Missing nonempty published provider for " + label + ".");
    }

    private static void VerifyRejectedMutation(
        ContentIdentityProtection.Snapshot baseline,
        ContentIdentityProtection.AssetRecord publishedOwner,
        ContentIdentityProtection.FieldRecord publishedField,
        ScriptableObject source,
        Action<SerializedObject> mutate,
        string label)
    {
        ScriptableObject clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            using (var serialized = new SerializedObject(clone))
            {
                serialized.Update();
                mutate(serialized);
                Require(
                    serialized.ApplyModifiedPropertiesWithoutUndo(),
                    "Serialized clone mutation was not applied: " + label);
            }

            ContentIdentityProtection.AssetRecord captured =
                ContentIdentityProtection.CaptureAsset(publishedOwner.guid, clone);
            RequireMatchingMetadata(publishedOwner, captured, label);
            Require(
                !captured.fields.Any(field => string.Equals(
                        field.path,
                        publishedField.path,
                        StringComparison.Ordinal)
                    && string.Equals(
                        field.value,
                        publishedField.value,
                        StringComparison.Ordinal)),
                "Clone retained the published identity without change: " + label);

            ContentIdentityProtection.Snapshot current = CloneSnapshot(baseline);
            ReplaceRecord(current, publishedOwner.guid, captured);
            Require(
                current.schemaVersion == baseline.schemaVersion,
                "Record replacement changed the identity schema: " + label);
            IReadOnlyList<string> errors =
                ContentIdentityProtection.Validate(baseline, current);
            Require(
                errors.Any(error => error.IndexOf(
                    publishedField.path,
                    StringComparison.Ordinal) >= 0),
                "Published identity mutation was accepted: " + label);
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyRegionAppend(
        ContentIdentityProtection.Snapshot baseline,
        NestedLocation region)
    {
        ScriptableObject clone = Object.Instantiate(region.source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            string appendedId;
            using (var serialized = new SerializedObject(clone))
            {
                serialized.Update();
                SerializedProperty regions = serialized.FindProperty(region.collectionPath);
                Require(regions != null && regions.isArray,
                    "Campaign clone is missing its region list.");
                appendedId = UniqueAppendId(regions, region.keyValue);
                int index = regions.arraySize;
                regions.arraySize++;
                SerializedProperty appended = regions.GetArrayElementAtIndex(index);
                SerializedProperty id = appended.FindPropertyRelative("id");
                SerializedProperty displayName = appended.FindPropertyRelative("displayName");
                SerializedProperty factionId = appended.FindPropertyRelative("factionId");
                Require(id != null && displayName != null && factionId != null,
                    "Campaign region serialization contract is incomplete.");
                id.stringValue = appendedId;
                displayName.stringValue = "R21 fixture region";
                factionId.stringValue = region.value;
                Require(
                    serialized.ApplyModifiedPropertiesWithoutUndo(),
                    "Campaign region append was not applied to the clone.");
            }
            RequireClean(
                ((OffenseCampaignCatalogSO)clone).ValidateDefinition(),
                "Campaign region append definition");

            ContentIdentityProtection.AssetRecord captured =
                ContentIdentityProtection.CaptureAsset(region.owner.guid, clone);
            RequireMatchingMetadata(region.owner, captured, "campaign region append");
            AssertPreservedPublishedFields(region.owner, captured);

            string idPath = NestedPath(
                region.collectionPath,
                region.key,
                appendedId,
                "id",
                providerType: null);
            string factionPath = NestedPath(
                region.collectionPath,
                region.key,
                appendedId,
                "factionId",
                providerType: null);
            ContentIdentityProtection.FieldRecord[] appendedFields = captured.fields
                .Where(field => string.Equals(field.path, idPath, StringComparison.Ordinal)
                    || string.Equals(field.path, factionPath, StringComparison.Ordinal))
                .ToArray();
            Require(
                appendedFields.Length == 2
                && appendedFields.Any(field => string.Equals(
                    field.path,
                    idPath,
                    StringComparison.Ordinal)
                    && string.Equals(field.value, appendedId, StringComparison.Ordinal))
                && appendedFields.Any(field => string.Equals(
                    field.path,
                    factionPath,
                    StringComparison.Ordinal)
                    && string.Equals(field.value, region.value, StringComparison.Ordinal)),
                "Campaign region append did not capture both new identities.");

            ContentIdentityProtection.Snapshot current = CloneSnapshot(baseline);
            ReplaceRecord(current, region.owner.guid, captured);
            Require(
                current.schemaVersion == baseline.schemaVersion,
                "Campaign append changed the identity schema.");
            string[] validationErrors = ContentIdentityProtection
                .Validate(baseline, current)
                .ToArray();
            Require(
                validationErrors.Length == appendedFields.Length
                && appendedFields.All(field => validationErrors.Any(error =>
                    error.StartsWith(
                        "Unpublished identity field '",
                        StringComparison.Ordinal)
                    && error.IndexOf(field.path, StringComparison.Ordinal) >= 0)),
                "Campaign append changed a published field or produced an unexpected error: "
                + string.Join(" | ", validationErrors));
            RequireClean(
                ContentIdentityProtection.ValidateNewPublication(baseline, current),
                "Campaign append publication candidate");

            ContentIdentityProtection.Snapshot publishedCandidate =
                CloneSnapshot(baseline);
            ReplaceRecord(publishedCandidate, region.owner.guid, captured);
            RequireClean(
                ContentIdentityProtection.Validate(publishedCandidate, current),
                "Explicitly appended campaign region publication");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void AssertPreservedPublishedFields(
        ContentIdentityProtection.AssetRecord published,
        ContentIdentityProtection.AssetRecord captured)
    {
        Dictionary<string, string> current = captured.fields.ToDictionary(
            field => field.path,
            field => field.value,
            StringComparer.Ordinal);
        int deleted = 0;
        int changed = 0;
        foreach (ContentIdentityProtection.FieldRecord field in published.fields)
        {
            if (!current.TryGetValue(field.path, out string value))
                deleted++;
            else if (!string.Equals(value, field.value, StringComparison.Ordinal))
                changed++;
        }

        Require(
            deleted == 0 && changed == 0,
            $"Campaign append failed published field preservation: "
            + $"deleted={deleted}, changed={changed}.");
    }

    private static void RenameNested(
        SerializedObject serialized,
        NestedLocation location)
    {
        SerializedProperty list = serialized.FindProperty(location.collectionPath);
        int index = FindNestedIndex(list, location);
        Require(index >= 0, "Clone lost nested provider: " + location.publishedField.path);
        SerializedProperty leaf = list.GetArrayElementAtIndex(index)
            .FindPropertyRelative(location.leaf);
        Require(
            leaf != null
            && string.Equals(leaf.stringValue, location.value, StringComparison.Ordinal),
            "Nested rename target changed before execution: "
            + location.publishedField.path);
        leaf.stringValue = location.value + ":r21-renamed";
    }

    private static void RemoveNested(
        SerializedObject serialized,
        NestedLocation location)
    {
        SerializedProperty list = serialized.FindProperty(location.collectionPath);
        int index = FindNestedIndex(list, location);
        Require(index >= 0, "Clone lost nested provider: " + location.publishedField.path);
        int previousSize = list.arraySize;
        list.DeleteArrayElementAtIndex(index);
        if (list.arraySize == previousSize)
            list.DeleteArrayElementAtIndex(index);
        Require(
            list.arraySize == previousSize - 1,
            "Nested provider removal did not shrink the list: "
            + location.publishedField.path);
    }

    private static void RenameScalar(
        SerializedObject serialized,
        ScalarLocation location)
    {
        SerializedProperty property = serialized.FindProperty(location.propertyPath);
        Require(
            property != null
            && string.Equals(property.stringValue, location.value, StringComparison.Ordinal),
            "Scalar rename target changed before execution: "
            + location.propertyPath);
        property.stringValue = location.value + ":r21-renamed";
    }

    private static int FindNestedIndex(
        SerializedProperty list,
        NestedLocation location)
    {
        if (list == null || !list.isArray)
            return -1;
        for (int index = 0; index < list.arraySize; index++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(location.providerType)
                && !ManagedTypeMatches(element, location.providerType))
                continue;
            string value = element.FindPropertyRelative(location.key)?.stringValue;
            if (string.Equals(value, location.keyValue, StringComparison.Ordinal))
                return index;
        }
        return -1;
    }

    private static SourceState[] CaptureSourceStates(params object[] locations)
    {
        var states = new Dictionary<string, SourceState>(StringComparer.OrdinalIgnoreCase);
        foreach (object location in locations)
        {
            ContentIdentityProtection.AssetRecord owner;
            ScriptableObject source;
            if (location is NestedLocation nested)
            {
                owner = nested.owner;
                source = nested.source;
            }
            else if (location is ScalarLocation scalar)
            {
                owner = scalar.owner;
                source = scalar.source;
            }
            else
            {
                throw new InvalidOperationException("Unknown fixture source location.");
            }

            if (states.ContainsKey(owner.guid))
                continue;
            string assetPath = AssetDatabase.GUIDToAssetPath(owner.guid);
            string absolutePath = AbsoluteAssetPath(assetPath);
            Require(File.Exists(absolutePath),
                "Fixture source asset file is missing: " + assetPath);
            states.Add(owner.guid, new SourceState
            {
                source = source,
                guid = owner.guid,
                assetPath = assetPath,
                absolutePath = absolutePath,
                json = EditorJsonUtility.ToJson(source),
                dirty = EditorUtility.IsDirty(source),
                bytes = File.ReadAllBytes(absolutePath)
            });
        }

        Require(
            states.Count == 6,
            "R21 fixture must preserve six independent representative source assets.");
        return states.Values.ToArray();
    }

    private static void VerifySourceStates(IEnumerable<SourceState> states)
    {
        foreach (SourceState state in states)
        {
            Require(
                string.Equals(
                    EditorJsonUtility.ToJson(state.source),
                    state.json,
                    StringComparison.Ordinal),
                "Source asset JSON changed: " + state.assetPath);
            Require(
                EditorUtility.IsDirty(state.source) == state.dirty,
                "Source asset dirty state changed: " + state.assetPath);
            Require(
                File.Exists(state.absolutePath)
                && File.ReadAllBytes(state.absolutePath).SequenceEqual(state.bytes),
                "Source asset bytes changed: " + state.assetPath);
        }
    }

    private static string UniqueAppendId(
        SerializedProperty list,
        string seed)
    {
        string candidate = seed + ":r21-fixture-append";
        int suffix = 2;
        while (ArrayContainsKey(list, "id", candidate))
            candidate = seed + ":r21-fixture-append-" + suffix++;
        return candidate;
    }

    private static bool ArrayContainsKey(
        SerializedProperty list,
        string key,
        string value)
    {
        for (int index = 0; index < list.arraySize; index++)
        {
            string current = list.GetArrayElementAtIndex(index)
                .FindPropertyRelative(key)?.stringValue;
            if (string.Equals(current, value, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static void RequireMatchingMetadata(
        ContentIdentityProtection.AssetRecord published,
        ContentIdentityProtection.AssetRecord captured,
        string label)
    {
        Require(
            string.Equals(published.guid, captured.guid, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                published.scriptGuid,
                captured.scriptGuid,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(published.typeName, captured.typeName, StringComparison.Ordinal),
            "Clone capture changed asset metadata: " + label);
    }

    private static ContentIdentityProtection.Snapshot CloneSnapshot(
        ContentIdentityProtection.Snapshot source)
    {
        ContentIdentityProtection.Snapshot clone =
            JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(
                JsonUtility.ToJson(source));
        Require(clone != null && clone.assets != null,
            "Could not clone the published identity baseline in memory.");
        Require(clone.schemaVersion == source.schemaVersion,
            "Snapshot clone changed the identity schema.");
        return clone;
    }

    private static void ReplaceRecord(
        ContentIdentityProtection.Snapshot snapshot,
        string guid,
        ContentIdentityProtection.AssetRecord replacement)
    {
        int index = snapshot.assets.FindIndex(value => string.Equals(
            value.guid,
            guid,
            StringComparison.OrdinalIgnoreCase));
        Require(index >= 0, "Published asset record is missing: " + guid);
        snapshot.assets[index] = CloneRecord(replacement);
    }

    private static ContentIdentityProtection.AssetRecord CloneRecord(
        ContentIdentityProtection.AssetRecord source) => new()
    {
        guid = source.guid,
        scriptGuid = source.scriptGuid,
        typeName = source.typeName,
        fields = source.fields.Select(field => new ContentIdentityProtection.FieldRecord
        {
            path = field.path,
            value = field.value
        }).ToList()
    };

    private static ScriptableObject LoadSource(
        ContentIdentityProtection.AssetRecord record)
    {
        string path = AssetDatabase.GUIDToAssetPath(record.guid);
        ScriptableObject source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        Require(source != null, "Could not load published source asset: " + path);
        return source;
    }

    private static bool ManagedTypeMatches(
        SerializedProperty property,
        string expectedType)
    {
        if (property == null
            || property.propertyType != SerializedPropertyType.ManagedReference)
            return false;
        string fullName = property.managedReferenceFullTypename ?? string.Empty;
        int space = fullName.IndexOf(' ');
        string typeName = space >= 0 ? fullName.Substring(space + 1) : fullName;
        int dot = typeName.LastIndexOf('.');
        return string.Equals(
            dot >= 0 ? typeName.Substring(dot + 1) : typeName,
            expectedType,
            StringComparison.Ordinal);
    }

    private static bool TypeMatches(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.Ordinal)
        || actual?.EndsWith("." + expected, StringComparison.Ordinal) == true;

    private static string NestedPath(
        string collection,
        string key,
        string keyValue,
        string leaf,
        string providerType) =>
        "@nested:" + collection + "[" + key + "="
        + Uri.EscapeDataString(keyValue)
        + (providerType == null ? string.Empty : ";type=" + providerType)
        + "]." + leaf;

    private static string AbsoluteAssetPath(string assetPath)
    {
        string projectRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(
            projectRoot,
            assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void RequireClean(
        IEnumerable<string> errors,
        string label)
    {
        string[] values = errors?.ToArray() ?? Array.Empty<string>();
        Require(values.Length == 0,
            label + " failed: " + string.Join(" | ", values));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
