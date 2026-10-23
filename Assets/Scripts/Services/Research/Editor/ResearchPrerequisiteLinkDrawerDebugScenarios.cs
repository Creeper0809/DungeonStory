#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ResearchPrerequisiteLinkDrawerDebugScenarios
{
    [MenuItem("Tools/DungeonStory/Research/Validate Prerequisite Link Authoring")]
    public static void RunFromMenu()
    {
        RunOrThrow();
        Debug.Log("Research prerequisite link authoring scenarios passed.");
    }

    public static string RunOrThrow()
    {
        ResearchProjectSO[] projects = ContentReferenceSelection
            .Entries(ContentReferenceKind.Research)
            .Select(entry => entry.Asset as ResearchProjectSO)
            .Where(project => project != null && project.ProjectId.IsValid)
            .Distinct()
            .OrderBy(project => project.ProjectId.Value, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        Require(projects.Length == 2, "Two registered research projects are required.");

        ResearchProjectSO firstOwner = CreateOwner("debug:research-prerequisite-owner:first", projects[0]);
        ResearchProjectSO secondOwner = CreateOwner("debug:research-prerequisite-owner:second", projects[1]);
        try
        {
            VerifyAuthoringContract();
            VerifyMismatchIsVisible(firstOwner, projects[0], projects[1]);
            VerifySelectionAndUndo(firstOwner, projects[0], projects[1]);
            VerifyClear(firstOwner, projects[0]);
            VerifyMultiObjectSelection(firstOwner, secondOwner, projects[0], projects[1]);
            return "research-prerequisite-link/select-clear-mismatch-undo-multi-edit=PASS";
        }
        finally
        {
            Undo.ClearUndo(firstOwner);
            Undo.ClearUndo(secondOwner);
            Object.DestroyImmediate(firstOwner);
            Object.DestroyImmediate(secondOwner);
        }
    }

    private static void VerifyAuthoringContract()
    {
        FieldInfo compatibilityId = typeof(ResearchPrerequisiteLink).GetField(
            "prerequisiteId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(compatibilityId != null
                && compatibilityId.GetCustomAttribute<HideInInspector>() != null,
            "The compatibility ID is exposed as an independent authoring field.");
        Require(compatibilityId.GetCustomAttribute<ContentReferenceAttribute>()?.Kind
                == ContentReferenceKind.Research,
            "The compatibility ID lost its research reference validation contract.");
    }

    private static void VerifyMismatchIsVisible(
        ResearchProjectSO owner,
        ResearchProjectSO objectProject,
        ResearchProjectSO idProject)
    {
        ResetLink(owner, objectProject);
        SerializedObject serialized = new SerializedObject(owner);
        SerializedProperty link = Link(serialized);
        link.FindPropertyRelative("prerequisiteId").stringValue = idProject.ProjectId.Value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        serialized.Update();

        Require(!ResearchPrerequisiteLinkDrawer.HasSynchronizedIdentity(Link(serialized)),
            "ID/object mismatch was not exposed by the dedicated drawer contract.");
        Require(!owner.PrerequisiteLinks[0].IsValid,
            "Research prerequisite definition validation accepted a mismatched ID/object pair.");
        Require(owner.ValidateDefinition().Count > 0,
            "Research project validation accepted a mismatched prerequisite link.");
    }

    private static void VerifySelectionAndUndo(
        ResearchProjectSO owner,
        ResearchProjectSO original,
        ResearchProjectSO selected)
    {
        ResetLink(owner, original);
        SerializedObject serialized = new SerializedObject(owner);
        SerializedProperty link = Link(serialized);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Research Prerequisite Link Scenario");
        Require(ResearchPrerequisiteLinkDrawer.TryAssignSelection(link, selected, out string error), error);
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(undoGroup);
        serialized.Update();
        RequireLink(serialized, owner, selected, "selection");

        Undo.PerformUndo();
        serialized.Update();
        RequireLink(serialized, owner, original, "undo");
    }

    private static void VerifyClear(ResearchProjectSO owner, ResearchProjectSO original)
    {
        ResetLink(owner, original);
        SerializedObject serialized = new SerializedObject(owner);
        Require(ResearchPrerequisiteLinkDrawer.TryAssignSelection(Link(serialized), null, out string error), error);
        serialized.Update();

        SerializedProperty link = Link(serialized);
        Require(link.FindPropertyRelative("prerequisite").objectReferenceValue == null,
            "Clearing the prerequisite left the object reference populated.");
        Require(string.IsNullOrEmpty(link.FindPropertyRelative("prerequisiteId").stringValue),
            "Clearing the prerequisite left the compatibility ID populated.");
        Require(ResearchPrerequisiteLinkDrawer.HasSynchronizedIdentity(link),
            "Clearing the prerequisite did not synchronize both serialized fields.");
    }

    private static void VerifyMultiObjectSelection(
        ResearchProjectSO firstOwner,
        ResearchProjectSO secondOwner,
        ResearchProjectSO firstSelection,
        ResearchProjectSO selected)
    {
        ResetLink(firstOwner, firstSelection);
        ResetLink(secondOwner, selected);
        SerializedObject serialized = new SerializedObject(new Object[] { firstOwner, secondOwner });
        SerializedProperty link = Link(serialized);
        Require(ResearchPrerequisiteLinkDrawer.TryAssignSelection(link, selected, out string error), error);
        serialized.Update();
        RequireLink(new SerializedObject(firstOwner), firstOwner, selected, "multi-edit first target");
        RequireLink(new SerializedObject(secondOwner), secondOwner, selected, "multi-edit second target");
    }

    private static ResearchProjectSO CreateOwner(string id, ResearchProjectSO prerequisite)
    {
        ResearchProjectSO owner = ScriptableObject.CreateInstance<ResearchProjectSO>();
        owner.Configure(
            id,
            id,
            "Research prerequisite link authoring fixture.",
            ResearchField.IndustryAndAutomation,
            1f,
            ResearchBlueprintRule.None,
            null,
            new[] { prerequisite },
            causalPrerequisites: new[]
            {
                new ResearchPrerequisiteLink(
                    prerequisite,
                    ResearchPrerequisiteKind.Engineering,
                    "Authoring fixture requires this prerequisite.")
            });
        return owner;
    }

    private static void ResetLink(ResearchProjectSO owner, ResearchProjectSO prerequisite)
    {
        SerializedObject serialized = new SerializedObject(owner);
        SerializedProperty link = Link(serialized);
        link.FindPropertyRelative("prerequisite").objectReferenceValue = prerequisite;
        link.FindPropertyRelative("prerequisiteId").stringValue = prerequisite.ProjectId.Value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty Link(SerializedObject serialized)
    {
        SerializedProperty links = serialized.FindProperty("prerequisiteLinks");
        Require(links != null && links.isArray && links.arraySize == 1,
            "Research prerequisite link fixture is unavailable.");
        return links.GetArrayElementAtIndex(0);
    }

    private static void RequireLink(
        SerializedObject serialized,
        ResearchProjectSO owner,
        ResearchProjectSO expected,
        string phase)
    {
        serialized.Update();
        SerializedProperty link = Link(serialized);
        Require(link.FindPropertyRelative("prerequisite").objectReferenceValue == expected,
            $"{phase} did not update the prerequisite object.");
        Require(string.Equals(
                link.FindPropertyRelative("prerequisiteId").stringValue,
                expected.ProjectId.Value,
                StringComparison.Ordinal),
            $"{phase} did not update the compatibility ID.");
        Require(ResearchPrerequisiteLinkDrawer.HasSynchronizedIdentity(link),
            $"{phase} left the prerequisite ID/object pair unsynchronized.");
        Require(owner.PrerequisiteLinks[0].IsValid,
            $"{phase} produced an invalid prerequisite link.");
        Require(owner.ValidateDefinition().Count == 0,
            $"{phase} failed research project definition validation.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
