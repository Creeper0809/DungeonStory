using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R20PublishedProviderDebugScenarios
{
    private enum MutationKind
    {
        Rename,
        Remove
    }

    private sealed class ProviderLocation
    {
        public ScriptableObject source;
        public string guid = string.Empty;
        public string value = string.Empty;
    }

    public static void RunAll()
    {
        ContentIdentityProtection.Snapshot baseline =
            ContentIdentityProtection.CaptureCurrent();
        RequireClean(
            ContentIdentityProtection.Validate(baseline, CloneSnapshot(baseline)),
            "Initial provider snapshot");

        int executed = 0;
        try
        {
            VerifyCharacterNeedRemoval(baseline);
            executed++;
            VerifyRequiredProvider(
                baseline,
                nameof(BuildingProductionWorkstationAbility),
                "workstationTag",
                arrayPath: null,
                valueMember: null,
                MutationKind.Rename,
                "production workstation tag");
            executed++;
            VerifyRequiredProvider(
                baseline,
                nameof(BuildingProductionSupportAbility),
                "featureTags",
                "featureTags",
                valueMember: null,
                MutationKind.Remove,
                "production feature tag");
            executed++;
            VerifyRequiredProvider(
                baseline,
                nameof(BuildingServiceHubAbility),
                "serviceHubTag",
                arrayPath: null,
                valueMember: null,
                MutationKind.Rename,
                "service hub tag");
            executed++;
            VerifyRequiredProvider(
                baseline,
                nameof(BuildingServiceSupportAbility),
                "featureTags",
                "featureTags",
                valueMember: null,
                MutationKind.Remove,
                "service feature tag");
            executed++;
            VerifyRequiredProvider(
                baseline,
                nameof(BuildingSemanticTagsAbility),
                "tags",
                "tags",
                valueMember: null,
                MutationKind.Remove,
                "generic semantic tag");
            executed++;

            if (VerifyProviderWhenPresent(
                    baseline,
                    nameof(BuildingEvolutionAbility),
                    "settings.tags",
                    "settings.tags",
                    valueMember: null,
                    MutationKind.Remove,
                    "evolution tag"))
                executed++;
            if (VerifyProviderWhenPresent(
                    baseline,
                    nameof(BuildingEvolutionAbility),
                    "settings.scores.key",
                    "settings.scores",
                    "key",
                    MutationKind.Rename,
                    "evolution score key"))
                executed++;
            if (VerifyProviderWhenPresent(
                    baseline,
                    nameof(BuildingEvolutionAbility),
                    "settings.metrics.key",
                    "settings.metrics",
                    "key",
                    MutationKind.Remove,
                    "evolution metric key"))
                executed++;
        }
        finally
        {
            ContentIdentityProtection.Snapshot restored =
                ContentIdentityProtection.CaptureCurrent();
            RequireClean(
                ContentIdentityProtection.Validate(baseline, restored),
                "Provider source assets after clone-only mutations");
        }

        Require(executed >= 6, "Required provider mutation scenarios did not execute.");
        Debug.Log($"R20_PUBLISHED_PROVIDER=PASS; scenarios={executed}; clone-only; source-json-preserved");
    }

    private static void VerifyCharacterNeedRemoval(
        ContentIdentityProtection.Snapshot baseline)
    {
        const string needId = "need:hunger";
        ContentIdentityProtection.AssetRecord owner = null;
        ScriptableObject source = null;
        foreach (ContentIdentityProtection.AssetRecord candidate in baseline.assets
                     .Where(value => TypeMatches(
                         value?.typeName,
                         nameof(GameDomainContentCatalogSO))))
        {
            ScriptableObject current = LoadSource(candidate);
            using var serialized = new SerializedObject(current);
            SerializedProperty needs = serialized.FindProperty("characterNeeds");
            if (FindArrayValue(needs, "id", needId) < 0)
                continue;
            owner = candidate;
            source = current;
            break;
        }

        Require(owner != null && source != null,
            "Missing actual need:hunger provider in a game domain catalog.");
        string path = NestedPath("characterNeeds", "id", needId, providerType: null);
        ContentIdentityProtection.FieldRecord published = RequireField(owner, path);
        VerifyCloneMutation(
            baseline,
            owner,
            published,
            source,
            serialized =>
            {
                SerializedProperty needs = serialized.FindProperty("characterNeeds");
                int index = FindArrayValue(needs, "id", needId);
                Require(index >= 0, "Clone lost need:hunger before mutation.");
                needs.DeleteArrayElementAtIndex(index);
            },
            "need:hunger removal");
    }

    private static void VerifyRequiredProvider(
        ContentIdentityProtection.Snapshot baseline,
        string providerType,
        string routeKey,
        string arrayPath,
        string valueMember,
        MutationKind mutation,
        string label)
    {
        bool executed = VerifyProviderWhenPresent(
            baseline,
            providerType,
            routeKey,
            arrayPath,
            valueMember,
            mutation,
            label);
        Require(executed, "Missing actual provider value for " + label + ".");
    }

    private static bool VerifyProviderWhenPresent(
        ContentIdentityProtection.Snapshot baseline,
        string providerType,
        string routeKey,
        string arrayPath,
        string valueMember,
        MutationKind mutation,
        string label)
    {
        ProviderLocation location = FindProvider(
            baseline,
            providerType,
            arrayPath ?? routeKey,
            valueMember,
            arrayPath != null);
        if (location == null)
            return false;

        ContentIdentityProtection.AssetRecord owner = baseline.assets.Single(
            value => string.Equals(
                value.guid,
                location.guid,
                StringComparison.OrdinalIgnoreCase));
        string path = NestedPath(
            "abilityModules.items",
            routeKey,
            location.value,
            providerType);
        ContentIdentityProtection.FieldRecord published = RequireField(owner, path);
        VerifyCloneMutation(
            baseline,
            owner,
            published,
            location.source,
            serialized => MutateProvider(
                serialized,
                providerType,
                arrayPath ?? routeKey,
                valueMember,
                location.value,
                arrayPath != null,
                mutation),
            label + " " + mutation.ToString().ToLowerInvariant());
        return true;
    }

    private static ProviderLocation FindProvider(
        ContentIdentityProtection.Snapshot snapshot,
        string providerType,
        string propertyPath,
        string valueMember,
        bool isArray)
    {
        foreach (ContentIdentityProtection.AssetRecord record in snapshot.assets
                     .Where(value => TypeMatches(value?.typeName, nameof(BuildingSO)))
                     .OrderBy(value => value.guid, StringComparer.Ordinal))
        {
            ScriptableObject source = LoadSource(record);
            using var serialized = new SerializedObject(source);
            SerializedProperty items = serialized.FindProperty("abilityModules.items");
            if (items == null || !items.isArray)
                continue;
            for (int itemIndex = 0; itemIndex < items.arraySize; itemIndex++)
            {
                SerializedProperty item = items.GetArrayElementAtIndex(itemIndex);
                if (!ManagedTypeMatches(item, providerType))
                    continue;
                SerializedProperty property = item.FindPropertyRelative(propertyPath);
                if (property == null)
                    continue;
                if (!isArray)
                {
                    if (!string.IsNullOrWhiteSpace(property.stringValue))
                    {
                        return new ProviderLocation
                        {
                            source = source,
                            guid = record.guid,
                            value = property.stringValue
                        };
                    }
                    continue;
                }

                if (!property.isArray)
                    continue;
                for (int valueIndex = 0; valueIndex < property.arraySize; valueIndex++)
                {
                    SerializedProperty element = property.GetArrayElementAtIndex(valueIndex);
                    SerializedProperty valueProperty = valueMember == null
                        ? element
                        : element.FindPropertyRelative(valueMember);
                    string value = valueProperty?.stringValue;
                    if (string.IsNullOrWhiteSpace(value))
                        continue;
                    return new ProviderLocation
                    {
                        source = source,
                        guid = record.guid,
                        value = value
                    };
                }
            }
        }

        return null;
    }

    private static void MutateProvider(
        SerializedObject serialized,
        string providerType,
        string propertyPath,
        string valueMember,
        string originalValue,
        bool isArray,
        MutationKind mutation)
    {
        SerializedProperty items = serialized.FindProperty("abilityModules.items");
        Require(items != null && items.isArray,
            "Clone is missing abilityModules.items.");
        for (int itemIndex = 0; itemIndex < items.arraySize; itemIndex++)
        {
            SerializedProperty item = items.GetArrayElementAtIndex(itemIndex);
            if (!ManagedTypeMatches(item, providerType))
                continue;
            SerializedProperty property = item.FindPropertyRelative(propertyPath);
            Require(property != null,
                $"Clone is missing {providerType}.{propertyPath}.");
            if (!isArray)
            {
                Require(mutation == MutationKind.Rename
                    && string.Equals(
                        property.stringValue,
                        originalValue,
                        StringComparison.Ordinal),
                    "Scalar provider mutation target changed before execution.");
                property.stringValue = originalValue + ":r20-renamed";
                return;
            }

            Require(property.isArray,
                $"Clone provider {providerType}.{propertyPath} is not an array.");
            for (int valueIndex = 0; valueIndex < property.arraySize; valueIndex++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(valueIndex);
                SerializedProperty valueProperty = valueMember == null
                    ? element
                    : element.FindPropertyRelative(valueMember);
                if (!string.Equals(
                        valueProperty?.stringValue,
                        originalValue,
                        StringComparison.Ordinal))
                    continue;
                if (mutation == MutationKind.Remove)
                    property.DeleteArrayElementAtIndex(valueIndex);
                else
                    valueProperty.stringValue = originalValue + ":r20-renamed";
                return;
            }
        }

        throw new InvalidOperationException(
            $"Could not find clone provider {providerType}.{propertyPath}='{originalValue}'.");
    }

    private static void VerifyCloneMutation(
        ContentIdentityProtection.Snapshot baseline,
        ContentIdentityProtection.AssetRecord publishedOwner,
        ContentIdentityProtection.FieldRecord publishedField,
        ScriptableObject source,
        Action<SerializedObject> mutate,
        string label)
    {
        string sourceJson = EditorJsonUtility.ToJson(source);
        bool sourceWasDirty = EditorUtility.IsDirty(source);
        ScriptableObject clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        string cloneJson = EditorJsonUtility.ToJson(clone);
        bool cloneWasDirty = EditorUtility.IsDirty(clone);
        bool cloneRestored = false;
        bool sourcePreserved = false;
        try
        {
            using (var serialized = new SerializedObject(clone))
            {
                mutate(serialized);
                Require(serialized.ApplyModifiedPropertiesWithoutUndo(),
                    "Serialized clone mutation was not applied: " + label);
            }

            ContentIdentityProtection.AssetRecord captured =
                ContentIdentityProtection.CaptureAsset(publishedOwner.guid, clone);
            Require(
                string.Equals(captured.guid, publishedOwner.guid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(captured.scriptGuid, publishedOwner.scriptGuid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(captured.typeName, publishedOwner.typeName, StringComparison.Ordinal),
                "Clone capture changed published asset metadata: " + label);
            Require(captured.fields.All(field => !string.Equals(
                    field.path,
                    publishedField.path,
                    StringComparison.Ordinal)),
                "Provider mutation retained the published route: " + publishedField.path);

            ContentIdentityProtection.Snapshot current = CloneSnapshot(baseline);
            ContentIdentityProtection.AssetRecord replacement = current.assets.Single(
                value => string.Equals(
                    value.guid,
                    publishedOwner.guid,
                    StringComparison.OrdinalIgnoreCase));
            Require(
                string.Equals(replacement.guid, publishedOwner.guid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(replacement.scriptGuid, publishedOwner.scriptGuid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(replacement.typeName, publishedOwner.typeName, StringComparison.Ordinal),
                "Snapshot copy changed published asset metadata: " + label);
            replacement.fields = captured.fields.Select(CloneField).ToList();

            IReadOnlyList<string> errors =
                ContentIdentityProtection.Validate(baseline, current);
            Require(errors.Any(error => error.IndexOf(
                        publishedField.path,
                        StringComparison.Ordinal) >= 0),
                "Published provider mutation was accepted: " + label);
        }
        finally
        {
            if (clone != null)
            {
                EditorJsonUtility.FromJsonOverwrite(cloneJson, clone);
                if (cloneWasDirty)
                    EditorUtility.SetDirty(clone);
                else
                    EditorUtility.ClearDirty(clone);
                cloneRestored = string.Equals(
                        EditorJsonUtility.ToJson(clone),
                        cloneJson,
                        StringComparison.Ordinal)
                    && EditorUtility.IsDirty(clone) == cloneWasDirty;
                Object.DestroyImmediate(clone);
            }
            sourcePreserved = string.Equals(
                    EditorJsonUtility.ToJson(source),
                    sourceJson,
                    StringComparison.Ordinal)
                && EditorUtility.IsDirty(source) == sourceWasDirty;
        }

        Require(cloneRestored, "Clone JSON or dirty state was not restored: " + label);
        Require(sourcePreserved,
            "Source asset JSON or dirty state changed during clone-only mutation: " + label);
    }

    private static ContentIdentityProtection.Snapshot CloneSnapshot(
        ContentIdentityProtection.Snapshot source)
    {
        ContentIdentityProtection.Snapshot clone =
            JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(
                JsonUtility.ToJson(source));
        Require(clone != null && clone.assets != null,
            "Could not clone provider identity snapshot in memory.");
        return clone;
    }

    private static ContentIdentityProtection.FieldRecord CloneField(
        ContentIdentityProtection.FieldRecord source) => new()
    {
        path = source.path,
        value = source.value
    };

    private static ContentIdentityProtection.FieldRecord RequireField(
        ContentIdentityProtection.AssetRecord owner,
        string path)
    {
        ContentIdentityProtection.FieldRecord field = owner.fields.SingleOrDefault(
            value => string.Equals(value.path, path, StringComparison.Ordinal));
        Require(field != null, "Missing captured provider route: " + path);
        return field;
    }

    private static ScriptableObject LoadSource(
        ContentIdentityProtection.AssetRecord record)
    {
        string path = AssetDatabase.GUIDToAssetPath(record.guid);
        ScriptableObject source =
            AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        Require(source != null, "Could not load provider source asset: " + path);
        return source;
    }

    private static int FindArrayValue(
        SerializedProperty array,
        string member,
        string value)
    {
        if (array == null || !array.isArray)
            return -1;
        for (int index = 0; index < array.arraySize; index++)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(index);
            SerializedProperty property = member == null
                ? element
                : element.FindPropertyRelative(member);
            if (string.Equals(property?.stringValue, value, StringComparison.Ordinal))
                return index;
        }
        return -1;
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
        string value,
        string providerType) =>
        "@nested:" + collection + "[" + key + "="
        + Uri.EscapeDataString(value)
        + (providerType == null ? string.Empty : ";type=" + providerType)
        + "]." + key;

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
