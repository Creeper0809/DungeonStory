using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AnatomyPairGroupAttribute))]
public sealed class R26AnatomyPairGroupDrawer : PropertyDrawer
{
    private const float LineGap = 2f;

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label) =>
        EditorGUIUtility.singleLineHeight * 2f + LineGap;

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect selector = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight);
        Rect status = new Rect(
            position.x,
            selector.yMax + LineGap,
            position.width,
            EditorGUIUtility.singleLineHeight);

        try
        {
            if (!AnatomyPairGroupSelection.TryInspect(
                    property,
                    out AnatomyPairGroupSelection.Inspection inspection,
                    out string error))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.TextField(selector, label, property.stringValue);
                }

                string current = string.IsNullOrWhiteSpace(property.stringValue)
                    ? "현재 짝 없음"
                    : "현재 group " + property.stringValue;
                EditorGUI.LabelField(
                    status,
                    current + " / 변경 불가: " + error,
                    EditorStyles.miniLabel);
                return;
            }

            List<AnatomyPairGroupSelection.Option> options =
                new List<AnatomyPairGroupSelection.Option>
                {
                    new AnatomyPairGroupSelection.Option(string.Empty, "짝 없음")
                };
            options.AddRange(inspection.Options);
            int selected = options.FindIndex(option => string.Equals(
                option.NodeId,
                inspection.CurrentTargetNodeId,
                StringComparison.Ordinal));
            if (selected < 0)
            {
                selected = 0;
            }

            using (new EditorGUI.DisabledScope(inspection.ReadOnly))
            {
                EditorGUI.BeginChangeCheck();
                int next = EditorGUI.Popup(
                    selector,
                    label.text,
                    selected,
                    options.Select(option => option.Label).ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    bool changed = string.IsNullOrEmpty(options[next].NodeId)
                        ? AnatomyPairGroupSelection.TryClearPair(
                            property,
                            out error)
                        : AnatomyPairGroupSelection.TrySetPair(
                            property,
                            options[next].NodeId,
                            out error);
                    if (!changed)
                    {
                        Debug.LogWarning(
                            "Anatomy pair change was rejected: " + error,
                            property.serializedObject.targetObject);
                    }
                }
            }

            string statusText = inspection.Status;
            if (inspection.ReadOnly)
            {
                statusText += " / 읽기 전용: " + inspection.ReadOnlyReason;
            }
            EditorGUI.LabelField(status, statusText, EditorStyles.miniLabel);
        }
        finally
        {
            EditorGUI.EndProperty();
        }
    }
}

internal static class AnatomyPairGroupSelection
{
    internal readonly struct Option
    {
        public Option(string nodeId, string label)
        {
            NodeId = nodeId;
            Label = label;
        }

        public string NodeId { get; }
        public string Label { get; }
    }

    internal sealed class Inspection
    {
        public string CurrentTargetNodeId { get; set; }
        public string Status { get; set; }
        public bool ReadOnly { get; set; }
        public string ReadOnlyReason { get; set; }
        public IReadOnlyList<Option> Options { get; set; }
    }

    public static bool TryInspect(
        SerializedProperty pairedProperty,
        out Inspection inspection,
        out string error)
    {
        inspection = null;
        if (!TryBuildContext(pairedProperty, out Context context, out error))
        {
            return false;
        }

        string currentTargetId = string.Empty;
        string status = "현재 짝 없음";
        Node partner = null;
        if (!string.IsNullOrEmpty(context.Current.PairGroupId))
        {
            if (!TryGetPairMembers(
                    context,
                    context.Current.PairGroupId,
                    out Node[] members,
                    out error))
            {
                return false;
            }

            partner = members[0] == context.Current ? members[1] : members[0];
            currentTargetId = partner.NodeId;
            status = "현재 짝: " + partner.Label
                + " / group " + context.Current.PairGroupId;
        }

        bool readOnly = TryGetPublishedReason(
            context.Current.PairProperty,
            out string readOnlyReason);
        if (partner != null
            && TryGetPublishedReason(
                partner.PairProperty,
                out string partnerReason))
        {
            readOnly = true;
            readOnlyReason = string.IsNullOrEmpty(readOnlyReason)
                ? partnerReason
                : readOnlyReason + " " + partnerReason;
        }

        inspection = new Inspection
        {
            CurrentTargetNodeId = currentTargetId,
            Status = status,
            ReadOnly = readOnly,
            ReadOnlyReason = readOnlyReason ?? string.Empty,
            Options = context.Nodes
                .Where(node => node != context.Current)
                .Select(node => new Option(
                    node.NodeId,
                    node.Label + " / " + node.NodeId))
                .ToArray()
        };
        error = string.Empty;
        return true;
    }

    public static bool TrySetPair(
        SerializedProperty pairedProperty,
        string targetNodeId,
        out string error)
    {
        if (!TryBuildContext(pairedProperty, out Context context, out error))
        {
            return false;
        }

        string requested = targetNodeId?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(requested))
        {
            error = "A target anatomy node is required.";
            return false;
        }
        if (string.Equals(
                context.Current.NodeId,
                requested,
                StringComparison.Ordinal))
        {
            error = "An anatomy node cannot be paired with itself.";
            return false;
        }

        Node target = context.Nodes.SingleOrDefault(node => string.Equals(
            node.NodeId,
            requested,
            StringComparison.Ordinal));
        if (target == null)
        {
            error = $"Anatomy node '{requested}' does not belong to this profile.";
            return false;
        }

        if (!string.IsNullOrEmpty(context.Current.PairGroupId))
        {
            if (!TryGetPairMembers(
                    context,
                    context.Current.PairGroupId,
                    out Node[] currentMembers,
                    out error))
            {
                return false;
            }

            Node currentPartner = currentMembers[0] == context.Current
                ? currentMembers[1]
                : currentMembers[0];
            if (currentPartner == target)
            {
                error = string.Empty;
                return true;
            }

            error = $"Anatomy node '{context.Current.NodeId}' is already paired "
                + $"with '{currentPartner.NodeId}'. Clear that pair first.";
            return false;
        }

        if (!string.IsNullOrEmpty(target.PairGroupId))
        {
            if (!TryGetPairMembers(
                    context,
                    target.PairGroupId,
                    out _,
                    out string targetGroupError))
            {
                error = "The target node has an ambiguous pair group: "
                    + targetGroupError;
                return false;
            }

            error = $"Anatomy node '{target.NodeId}' is already paired.";
            return false;
        }

        if (TryGetPublishedReason(
                context.Current.PairProperty,
                out string currentPublished))
        {
            error = $"Anatomy node '{context.Current.NodeId}' is published. "
                + currentPublished;
            return false;
        }
        if (TryGetPublishedReason(
                target.PairProperty,
                out string targetPublished))
        {
            error = $"Anatomy node '{target.NodeId}' is published. "
                + targetPublished;
            return false;
        }

        string groupId = "pair:" + Guid.NewGuid().ToString("N");
        Undo.RecordObjects(
            context.SerializedObject.targetObjects,
            "Pair Anatomy Nodes");
        context.Current.PairProperty.stringValue = groupId;
        target.PairProperty.stringValue = groupId;
        context.SerializedObject.ApplyModifiedProperties();
        error = string.Empty;
        return true;
    }

    public static bool TryClearPair(
        SerializedProperty pairedProperty,
        out string error)
    {
        if (!TryBuildContext(pairedProperty, out Context context, out error))
        {
            return false;
        }

        if (string.IsNullOrEmpty(context.Current.PairGroupId))
        {
            error = string.Empty;
            return true;
        }
        if (!TryGetPairMembers(
                context,
                context.Current.PairGroupId,
                out Node[] members,
                out error))
        {
            return false;
        }

        foreach (Node member in members)
        {
            if (TryGetPublishedReason(
                    member.PairProperty,
                    out string publishedReason))
            {
                error = $"Anatomy node '{member.NodeId}' is published. "
                    + publishedReason;
                return false;
            }
        }

        Undo.RecordObjects(
            context.SerializedObject.targetObjects,
            "Clear Anatomy Pair");
        members[0].PairProperty.stringValue = string.Empty;
        members[1].PairProperty.stringValue = string.Empty;
        context.SerializedObject.ApplyModifiedProperties();
        error = string.Empty;
        return true;
    }

    private static bool TryBuildContext(
        SerializedProperty pairedProperty,
        out Context context,
        out string error)
    {
        context = null;
        if (pairedProperty == null
            || pairedProperty.propertyType != SerializedPropertyType.String)
        {
            error = "The anatomy pair field is unavailable.";
            return false;
        }

        SerializedObject serialized = pairedProperty.serializedObject;
        if (serialized == null
            || serialized.targetObjects == null
            || serialized.targetObjects.Length != 1
            || serialized.targetObject is not AnatomyProfileSO)
        {
            error = "Anatomy pairs must be edited on one profile at a time.";
            return false;
        }

        SerializedProperty nodesProperty = serialized.FindProperty("nodes");
        if (nodesProperty == null || !nodesProperty.isArray)
        {
            error = "The owning anatomy profile has no node list.";
            return false;
        }

        var nodes = new List<Node>(nodesProperty.arraySize);
        Node current = null;
        for (int index = 0; index < nodesProperty.arraySize; index++)
        {
            SerializedProperty element =
                nodesProperty.GetArrayElementAtIndex(index);
            SerializedProperty nodeId = element.FindPropertyRelative("nodeId");
            SerializedProperty displayName =
                element.FindPropertyRelative("displayName");
            SerializedProperty pair =
                element.FindPropertyRelative("pairedGroupId");
            if (nodeId == null || displayName == null || pair == null)
            {
                error = $"Anatomy node at index {index} is incomplete.";
                return false;
            }

            var node = new Node(nodeId, displayName, pair);
            nodes.Add(node);
            if (string.Equals(
                    pair.propertyPath,
                    pairedProperty.propertyPath,
                    StringComparison.Ordinal))
            {
                current = node;
            }
        }

        if (current == null)
        {
            error = "The current anatomy node could not be resolved.";
            return false;
        }
        Node missingId = nodes.FirstOrDefault(node =>
            string.IsNullOrEmpty(node.NodeId));
        if (missingId != null)
        {
            error = "Every anatomy node needs a node ID before pairs can be edited.";
            return false;
        }
        IGrouping<string, Node> duplicate = nodes
            .GroupBy(node => node.NodeId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            error = $"Anatomy node ID '{duplicate.Key}' is duplicated.";
            return false;
        }

        context = new Context(serialized, nodes, current);
        error = string.Empty;
        return true;
    }

    private static bool TryGetPairMembers(
        Context context,
        string groupId,
        out Node[] members,
        out string error)
    {
        members = context.Nodes
            .Where(node => string.Equals(
                node.PairGroupId,
                groupId,
                StringComparison.Ordinal))
            .ToArray();
        if (members.Length != 2)
        {
            error = $"Pair group '{groupId}' has {members.Length} members; "
                + "exactly two are required.";
            return false;
        }
        if (string.Equals(
                members[0].NodeId,
                members[1].NodeId,
                StringComparison.Ordinal))
        {
            error = $"Pair group '{groupId}' repeats node ID "
                + $"'{members[0].NodeId}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryGetPublishedReason(
        SerializedProperty property,
        out string reason)
    {
        return ContentIdentityProtection.IsPublished(property, out reason);
    }

    private sealed class Context
    {
        public Context(
            SerializedObject serializedObject,
            IReadOnlyList<Node> nodes,
            Node current)
        {
            SerializedObject = serializedObject;
            Nodes = nodes;
            Current = current;
        }

        public SerializedObject SerializedObject { get; }
        public IReadOnlyList<Node> Nodes { get; }
        public Node Current { get; }
    }

    private sealed class Node
    {
        public Node(
            SerializedProperty nodeId,
            SerializedProperty displayName,
            SerializedProperty pairProperty)
        {
            NodeIdProperty = nodeId.Copy();
            DisplayNameProperty = displayName.Copy();
            PairProperty = pairProperty.Copy();
        }

        public SerializedProperty NodeIdProperty { get; }
        public SerializedProperty DisplayNameProperty { get; }
        public SerializedProperty PairProperty { get; }
        public string NodeId => NodeIdProperty.stringValue?.Trim()
            ?? string.Empty;
        public string Label => string.IsNullOrWhiteSpace(
                DisplayNameProperty.stringValue)
            ? NodeId
            : DisplayNameProperty.stringValue.Trim();
        public string PairGroupId => PairProperty.stringValue?.Trim()
            ?? string.Empty;
    }
}
