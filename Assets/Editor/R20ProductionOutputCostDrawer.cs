using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ProductionOutputCostAllocationAuthoring))]
public sealed class R20ProductionOutputCostDrawer : PropertyDrawer
{
    private const float Spacing = 2f;

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return height;

        SerializedProperty capabilityId =
            property.FindPropertyRelative("capabilityId");
        height += Spacing + EditorGUI.GetPropertyHeight(
            capabilityId,
            includeChildren: true);

        R20ProductionOutputCostInspection inspection = Inspect(property);
        height += Spacing + StatusHeight(inspection);
        if (inspection.IsValid && !inspection.IsEmpty)
        {
            height += inspection.Weights.Count
                * (Spacing + EditorGUIUtility.singleLineHeight);
        }
        height += Spacing + EditorGUIUtility.singleLineHeight;
        return height;
    }

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        try
        {
            Rect line = new(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(
                line,
                property.isExpanded,
                label,
                toggleOnLabelClick: true);
            if (!property.isExpanded)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                SerializedProperty capabilityId =
                    property.FindPropertyRelative("capabilityId");
                line.y += line.height + Spacing;
                line.height = EditorGUI.GetPropertyHeight(
                    capabilityId,
                    includeChildren: true);
                EditorGUI.PropertyField(
                    line,
                    capabilityId,
                    new GUIContent("Allocation policy"),
                    includeChildren: true);

                R20ProductionOutputCostInspection inspection = Inspect(property);
                line.y += line.height + Spacing;
                line.height = StatusHeight(inspection);
                DrawStatus(line, property, inspection);

                if (inspection.IsValid && !inspection.IsEmpty)
                {
                    foreach (ProductionOutputCostAllocationWeight weight in
                             inspection.Weights)
                    {
                        line.y += line.height + Spacing;
                        line.height = EditorGUIUtility.singleLineHeight;
                        EditorGUI.BeginChangeCheck();
                        long next = EditorGUI.LongField(
                            line,
                            new GUIContent(
                                weight.OutputLineId,
                                weight.OutputLineId),
                            weight.Weight);
                        if (EditorGUI.EndChangeCheck()
                            && !TryReplaceWeight(
                                property,
                                inspection,
                                weight.OutputLineId,
                                next,
                                out string error))
                        {
                            Debug.LogWarning(
                                error,
                                property.serializedObject.targetObject);
                        }
                    }
                }

                line.y += line.height + Spacing;
                line.height = EditorGUIUtility.singleLineHeight;
                using (new EditorGUI.DisabledScope(
                           property.serializedObject.isEditingMultipleObjects))
                {
                    string buttonLabel = inspection.IsEmpty
                        ? "Initialize weights from current outputs"
                        : inspection.IsValid
                            ? "Reset weights from current outputs"
                            : "Correct using current outputs";
                    if (GUI.Button(line, buttonLabel)
                        && !TryApplyCorrection(property, out string error))
                    {
                        Debug.LogWarning(
                            error,
                            property.serializedObject.targetObject);
                    }
                }
            }
        }
        finally
        {
            EditorGUI.EndProperty();
        }
    }

    public static R20ProductionOutputCostInspection Inspect(
        SerializedProperty allocationProperty)
    {
        if (!TryCapturePendingOutputs(
                allocationProperty,
                out ProductionOutputDefinition[] outputs,
                out string outputError))
        {
            return R20ProductionOutputCostInspection.Invalid(
                outputError,
                Array.Empty<ProductionOutputDefinition>());
        }

        SerializedProperty capabilityId =
            allocationProperty?.FindPropertyRelative("capabilityId");
        SerializedProperty contractVersion =
            allocationProperty?.FindPropertyRelative("contractVersion");
        SerializedProperty canonicalPayload =
            allocationProperty?.FindPropertyRelative("canonicalPayload");
        if (capabilityId == null || contractVersion == null
            || canonicalPayload == null)
        {
            return R20ProductionOutputCostInspection.Invalid(
                "Output-cost allocation serialization is incomplete.",
                outputs);
        }
        if (allocationProperty.serializedObject.isEditingMultipleObjects
            || capabilityId.hasMultipleDifferentValues
            || contractVersion.hasMultipleDifferentValues
            || canonicalPayload.hasMultipleDifferentValues)
        {
            return R20ProductionOutputCostInspection.Invalid(
                "Multiple recipes have different output-cost allocation values. Edit each recipe separately.",
                outputs);
        }

        string id = capabilityId.stringValue ?? string.Empty;
        int version = contractVersion.intValue;
        string payload = canonicalPayload.stringValue ?? string.Empty;
        if (id.Length == 0 && version == 0 && payload.Length == 0)
        {
            return R20ProductionOutputCostInspection.Empty(outputs);
        }

        try
        {
            var authoring = new ProductionOutputCostAllocationAuthoringSnapshot(
                id,
                version,
                payload);
            IReadOnlyList<ProductionOutputCostAllocationWeight> weights =
                ProductionOutputCostAllocationCapabilityRegistry.CreateDefault()
                    .ResolveWeights(authoring, outputs);
            return R20ProductionOutputCostInspection.Valid(outputs, weights);
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                           or ArgumentException
                                           or OverflowException)
        {
            return R20ProductionOutputCostInspection.Invalid(
                exception.Message,
                outputs);
        }
    }

    public static bool TryBuildCorrectionPayload(
        SerializedProperty allocationProperty,
        out string payload,
        out string error)
    {
        payload = string.Empty;
        if (!TryCapturePendingOutputs(
                allocationProperty,
                out ProductionOutputDefinition[] outputs,
                out error))
        {
            return false;
        }

        try
        {
            payload = WeightedOutputShareProductionOutputCostAllocationCapability
                .BuildPayload(outputs);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                           or ArgumentException
                                           or OverflowException)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryApplyCorrection(
        SerializedProperty allocationProperty,
        out string error)
    {
        if (!TryBuildCorrectionPayload(allocationProperty, out string payload,
                out error))
        {
            return false;
        }

        SerializedProperty capabilityId =
            allocationProperty.FindPropertyRelative("capabilityId");
        SerializedProperty contractVersion =
            allocationProperty.FindPropertyRelative("contractVersion");
        SerializedProperty canonicalPayload =
            allocationProperty.FindPropertyRelative("canonicalPayload");
        capabilityId.stringValue =
            WeightedOutputShareProductionOutputCostAllocationCapability.Id;
        contractVersion.intValue =
            WeightedOutputShareProductionOutputCostAllocationCapability.Version;
        canonicalPayload.stringValue = payload;
        error = string.Empty;
        return true;
    }

    private static bool TryReplaceWeight(
        SerializedProperty allocationProperty,
        R20ProductionOutputCostInspection inspection,
        string outputLineId,
        long nextWeight,
        out string error)
    {
        try
        {
            ProductionOutputCostAllocationWeight[] replacement = inspection.Weights
                .Select(weight => new ProductionOutputCostAllocationWeight(
                    weight.OutputLineId,
                    string.Equals(weight.OutputLineId, outputLineId,
                        StringComparison.Ordinal)
                        ? nextWeight
                        : weight.Weight))
                .ToArray();
            string payload =
                WeightedOutputShareProductionOutputCostAllocationCapability
                    .EncodePayload(replacement);
            var authoring = new ProductionOutputCostAllocationAuthoringSnapshot(
                WeightedOutputShareProductionOutputCostAllocationCapability.Id,
                WeightedOutputShareProductionOutputCostAllocationCapability.Version,
                payload);
            ProductionOutputCostAllocationCapabilityRegistry.CreateDefault()
                .ValidateForOutputs(authoring, inspection.Outputs);
            allocationProperty.FindPropertyRelative("canonicalPayload").stringValue =
                payload;
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                           or ArgumentException
                                           or OverflowException)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryCapturePendingOutputs(
        SerializedProperty allocationProperty,
        out ProductionOutputDefinition[] outputs,
        out string error)
    {
        outputs = Array.Empty<ProductionOutputDefinition>();
        if (allocationProperty?.serializedObject == null)
        {
            error = "Output-cost allocation property is unavailable.";
            return false;
        }

        SerializedProperty serializedOutputs =
            allocationProperty.serializedObject.FindProperty("outputs");
        if (serializedOutputs == null || !serializedOutputs.isArray)
        {
            error = "Owning recipe outputs are unavailable.";
            return false;
        }

        var captured = new ProductionOutputDefinition[serializedOutputs.arraySize];
        var lineIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < serializedOutputs.arraySize; index++)
        {
            SerializedProperty row = serializedOutputs.GetArrayElementAtIndex(index);
            SerializedProperty lineIdProperty =
                row.FindPropertyRelative("outputLineId");
            SerializedProperty roleProperty = row.FindPropertyRelative("role");
            SerializedProperty itemIdProperty = row.FindPropertyRelative("itemId");
            SerializedProperty amountProperty = row.FindPropertyRelative("amount");
            SerializedProperty probabilityProperty =
                row.FindPropertyRelative("probability");
            if (lineIdProperty == null || roleProperty == null
                || itemIdProperty == null || amountProperty == null
                || probabilityProperty == null)
            {
                error = $"Recipe output {index} serialization is incomplete.";
                return false;
            }

            string lineId = lineIdProperty.stringValue ?? string.Empty;
            string itemId = itemIdProperty.stringValue ?? string.Empty;
            int roleValue = roleProperty.intValue;
            int amount = amountProperty.intValue;
            float probability = probabilityProperty.floatValue;
            if (!ProductionOutputDefinition.IsCanonicalOutputLineId(lineId))
            {
                error = $"Recipe output {index} has noncanonical output line ID '{lineId}'.";
                return false;
            }
            if (!lineIds.Add(lineId))
            {
                error = $"Recipe outputs contain duplicate output line ID '{lineId}'.";
                return false;
            }
            if (!Enum.IsDefined(typeof(ProductionOutputRole), roleValue))
            {
                error = $"Recipe output '{lineId}' has invalid role {roleValue}.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(itemId)
                || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
            {
                error = $"Recipe output '{lineId}' has a noncanonical item ID.";
                return false;
            }
            if (amount <= 0 || float.IsNaN(probability)
                || float.IsInfinity(probability)
                || probability < 0f || probability > 1f)
            {
                error = $"Recipe output '{lineId}' has invalid amount or probability.";
                return false;
            }

            captured[index] = new ProductionOutputDefinition(
                lineId,
                (ProductionOutputRole)roleValue,
                itemId,
                amount,
                probability);
        }

        outputs = captured;
        error = string.Empty;
        return true;
    }

    private static void DrawStatus(
        Rect position,
        SerializedProperty property,
        R20ProductionOutputCostInspection inspection)
    {
        if (property.serializedObject.isEditingMultipleObjects)
        {
            EditorGUI.HelpBox(position, inspection.Error, MessageType.Info);
            return;
        }
        if (!inspection.IsValid)
        {
            EditorGUI.HelpBox(
                position,
                inspection.Error + " Existing serialized bytes are preserved until an explicit edit or correction.",
                MessageType.Error);
            return;
        }
        if (inspection.IsEmpty)
        {
            EditorGUI.HelpBox(
                position,
                "No output-cost allocation policy is authored.",
                MessageType.Info);
            return;
        }
        EditorGUI.HelpBox(
            position,
            $"Payload resolves against {inspection.Weights.Count} current physical outputs.",
            MessageType.None);
    }

    private static float StatusHeight(
        R20ProductionOutputCostInspection inspection) =>
        EditorGUIUtility.singleLineHeight * (inspection.IsValid ? 2f : 4f);
}

public readonly struct R20ProductionOutputCostInspection
{
    private R20ProductionOutputCostInspection(
        bool isValid,
        bool isEmpty,
        string error,
        IReadOnlyList<ProductionOutputDefinition> outputs,
        IReadOnlyList<ProductionOutputCostAllocationWeight> weights)
    {
        IsValid = isValid;
        IsEmpty = isEmpty;
        Error = error ?? string.Empty;
        Outputs = outputs ?? Array.Empty<ProductionOutputDefinition>();
        Weights = weights ?? Array.Empty<ProductionOutputCostAllocationWeight>();
    }

    public bool IsValid { get; }
    public bool IsEmpty { get; }
    public string Error { get; }
    public IReadOnlyList<ProductionOutputDefinition> Outputs { get; }
    public IReadOnlyList<ProductionOutputCostAllocationWeight> Weights { get; }
    public IReadOnlyList<string> OutputLineIds => Outputs
        .Where(output => output != null
                         && ProductionOutputRoleRules.IsPhysical(output.Role)
                         && output.Probability > 0f)
        .Select(output => output.OutputLineId)
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();

    internal static R20ProductionOutputCostInspection Valid(
        IReadOnlyList<ProductionOutputDefinition> outputs,
        IReadOnlyList<ProductionOutputCostAllocationWeight> weights) =>
        new(true, false, string.Empty, outputs, weights);

    internal static R20ProductionOutputCostInspection Empty(
        IReadOnlyList<ProductionOutputDefinition> outputs) =>
        new(true, true, string.Empty, outputs,
            Array.Empty<ProductionOutputCostAllocationWeight>());

    internal static R20ProductionOutputCostInspection Invalid(
        string error,
        IReadOnlyList<ProductionOutputDefinition> outputs) =>
        new(false, false, error, outputs,
            Array.Empty<ProductionOutputCostAllocationWeight>());
}
