using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R19InspectorDebugScenarios
{
    private const string SkillSettingsPath =
        "Assets/Resources/SO/Character/CharacterSkillSystemSettings.asset";
    private const string GameplayCatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string EquipmentFormulaPath =
        "Assets/Resources/SO/EquipmentEvolutionFormulaCatalogV3.asset";
    private const string MassRecipePath =
        "Assets/Resources/SO/Economy/Recipes/recipe_antidote.asset";
    private const string OutputCostRecipePath =
        "Assets/Resources/SO/Economy/Recipes/source_logging.asset";
    private const string CoreSessionPath =
        "Assets/Resources/SO/Content/CoreSessionRules.asset";

    // Invoke only through the isolated batch-mode Inspector contract runner.
    public static void VerifyInspectorGUI()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException(
                "R19 Inspector verification requires isolated batch mode.");

        ScriptableObject[] sources =
        {
            RequireAsset<CharacterSkillSystemSettingsSO>(SkillSettingsPath),
            RequireAsset<GameDomainContentCatalogSO>(GameplayCatalogPath),
            RequireAsset<EquipmentEvolutionFormulaCatalogSO>(EquipmentFormulaPath),
            RequireAsset<ProductionRecipeSO>(MassRecipePath),
            RequireAsset<ProductionRecipeSO>(OutputCostRecipePath),
            RequireAsset<CoreSessionRulesSO>(CoreSessionPath)
        };
        SourceSnapshot[] snapshots = sources.Select(SourceSnapshot.Capture)
            .ToArray();
        ScriptableObject[] clones = sources.Select(Clone).ToArray();
        ContentReferenceInspectorProbeWindow window =
            ScriptableObject.CreateInstance<ContentReferenceInspectorProbeWindow>();
        try
        {
            ContentReferenceSelection.Invalidate();
            window.position = new Rect(0, 0, 760, 360);
            window.ShowUtility();

            VerifySkillBehaviorAndFormulaMirror(
                window,
                (CharacterSkillSystemSettingsSO)clones[0]);
            VerifyMetaEffectTarget(
                window,
                (GameDomainContentCatalogSO)clones[1]);
            VerifyEquipmentApplicator(
                window,
                (EquipmentEvolutionFormulaCatalogSO)clones[2]);
            VerifyProductionPolicies(
                window,
                (ProductionRecipeSO)clones[3],
                (ProductionRecipeSO)clones[4]);
            VerifyCoreSessionNumericSelectors(
                window,
                (CoreSessionRulesSO)clones[5]);

            Require(window.DrawCount >= 12,
                "R19 PropertyDrawer GUI events did not execute.");
            Debug.Log(
                "R19_INSPECTOR_GUI=PASS; skill behavior; formula mirror; meta targets; equipment applicator; production policies; core numeric labels; undo; unchanged source");
        }
        finally
        {
            window.Property = null;
            window.Close();
            if (window != null) Object.DestroyImmediate(window);
            foreach (ScriptableObject clone in clones)
            {
                Undo.ClearUndo(clone);
                Object.DestroyImmediate(clone);
            }
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
    }

    private static void VerifySkillBehaviorAndFormulaMirror(
        ContentReferenceInspectorProbeWindow window,
        CharacterSkillSystemSettingsSO target)
    {
        string baseline = EditorJsonUtility.ToJson(target);
        Undo.ClearUndo(target);
        using SerializedObject serialized = new(target);
        SerializedProperty modules = RequireProperty(serialized, "modules");
        int selectedIndex = -1;
        string replacement = string.Empty;
        for (int index = 0; index < modules.arraySize; index++)
        {
            SerializedProperty capability = modules.GetArrayElementAtIndex(index)
                .FindPropertyRelative("capabilityId");
            if (capability == null) continue;
            string candidate = ContentReferenceSelection.Entries(
                    new ContentReferenceAttribute(ContentReferenceKind.SkillBehavior),
                    capability)
                .Select(value => value.Id)
                .FirstOrDefault(value => !string.Equals(
                    value,
                    capability.stringValue,
                    StringComparison.Ordinal));
            SerializedProperty formula = modules.GetArrayElementAtIndex(index)
                .FindPropertyRelative("formula");
            if (!string.IsNullOrEmpty(candidate)
                && formula != null
                && target.Modules[index]?.formula != null)
            {
                selectedIndex = index;
                replacement = candidate;
                break;
            }
        }
        Require(selectedIndex >= 0,
            "No skill module exposes an alternate registered behavior.");

        CharacterSkillModuleRule applied = target.Modules[selectedIndex];
        string originalCapability = applied.capabilityId;
        string originalFormatter = applied.formula.formatterId;
        string originalApplicator = applied.formula.applicatorId;
        int undoGroup = BeginUndo("R19 skill behavior and formula mirror");
        SerializedProperty module = modules.GetArrayElementAtIndex(selectedIndex);
        SerializedProperty capabilityId = RequireRelative(module, "capabilityId");
        SerializedProperty formulaProperty = RequireRelative(module, "formula");
        SerializedProperty formatterId = RequireRelative(
            formulaProperty,
            "formatterId");
        SerializedProperty applicatorId = RequireRelative(
            formulaProperty,
            "applicatorId");

        ContentReferenceSelection.WriteId(
            capabilityId,
            ContentReferenceKind.SkillBehavior,
            replacement);
        Require(capabilityId.stringValue == replacement
                && formatterId.stringValue == replacement
                && applicatorId.stringValue == replacement,
            "Skill behavior selection did not synchronize its formula mirrors.");
        Require(applied.capabilityId == originalCapability
                && applied.formula.formatterId == originalFormatter
                && applied.formula.applicatorId == originalApplicator,
            "Pending skill selection was applied before ApplyModifiedProperties.");
        RequireOnlyCandidate(
            formatterId,
            ContentReferenceKind.FormulaMirror,
            replacement,
            "Pending formatter mirror");
        RequireOnlyCandidate(
            applicatorId,
            ContentReferenceKind.FormulaMirror,
            replacement,
            "Pending applicator mirror");
        DrawProbe(window, capabilityId, "skill behavior selection");
        DrawProbe(window, formatterId, "pending formatter mirror");
        DrawProbe(window, applicatorId, "pending applicator mirror");

        Require(serialized.ApplyModifiedProperties(),
            "Skill behavior selection did not apply.");
        applied = target.Modules[selectedIndex];
        Require(applied.capabilityId == replacement
                && applied.formula.formatterId == replacement
                && applied.formula.applicatorId == replacement,
            "Applied skill behavior and formula mirrors disagree.");
        EndUndo(target, baseline, undoGroup,
            "Skill behavior and formula mirrors");
    }

    private static void VerifyMetaEffectTarget(
        ContentReferenceInspectorProbeWindow window,
        GameDomainContentCatalogSO target)
    {
        string baseline = EditorJsonUtility.ToJson(target);
        Undo.ClearUndo(target);
        using SerializedObject serialized = new(target);
        SerializedProperty upgrades = RequireProperty(serialized, "metaUpgrades");
        Require(upgrades.arraySize > 0,
            "The gameplay catalog has no meta upgrade for the Inspector probe.");
        SerializedProperty effects = RequireRelative(
            upgrades.GetArrayElementAtIndex(0),
            "effects");
        int appliedCount = target.MetaUpgrades[0].effects.Count;
        int undoGroup = BeginUndo("R19 meta effect target");
        effects.arraySize++;
        SerializedProperty effect = effects.GetArrayElementAtIndex(
            effects.arraySize - 1);
        SerializedProperty kind = RequireRelative(effect, "kind");
        SerializedProperty targetId = RequireRelative(effect, "id");

        kind.intValue = (int)AuthoredGameplayEffectKind.MetaIntegerBonus;
        ContentReferenceSelection.Entry[] integers =
            ContentReferenceSelection.Entries(
                    new ContentReferenceAttribute(
                        ContentReferenceKind.MetaEffectTarget),
                    targetId)
                .ToArray();
        RequireSameIds(
            integers,
            MetaUpgradeEffectIds.IntegerTargets,
            "Integer meta effect targets");
        ContentReferenceSelection.WriteId(
            targetId,
            ContentReferenceKind.MetaEffectTarget,
            integers[0].Id);
        DrawProbe(window, targetId, "pending integer meta effect target");
        Require(target.MetaUpgrades[0].effects.Count == appliedCount,
            "Pending integer meta effect row was applied while drawing.");

        kind.intValue = (int)AuthoredGameplayEffectKind.MetaMultiplierDelta;
        ContentReferenceSelection.Entry[] multipliers =
            ContentReferenceSelection.Entries(
                    new ContentReferenceAttribute(
                        ContentReferenceKind.MetaEffectTarget),
                    targetId)
                .ToArray();
        RequireSameIds(
            multipliers,
            MetaUpgradeEffectIds.MultiplierTargets,
            "Multiplier meta effect targets");
        ContentReferenceSelection.WriteId(
            targetId,
            ContentReferenceKind.MetaEffectTarget,
            multipliers[0].Id);
        DrawProbe(window, targetId, "pending multiplier meta effect target");
        Require(target.MetaUpgrades[0].effects.Count == appliedCount,
            "Pending multiplier meta effect row was applied while drawing.");

        Require(serialized.ApplyModifiedProperties(),
            "Meta effect target selection did not apply.");
        AuthoredGameplayEffectRecord applied = target.MetaUpgrades[0].effects[^1];
        Require(applied.kind == AuthoredGameplayEffectKind.MetaMultiplierDelta
                && applied.id == multipliers[0].Id,
            "Applied meta effect did not retain the pending sibling kind context.");
        EndUndo(target, baseline, undoGroup, "Meta effect target");
    }

    private static void VerifyEquipmentApplicator(
        ContentReferenceInspectorProbeWindow window,
        EquipmentEvolutionFormulaCatalogSO target)
    {
        string baseline = EditorJsonUtility.ToJson(target);
        Undo.ClearUndo(target);
        using SerializedObject serialized = new(target);
        SerializedProperty capabilities = RequireProperty(
            serialized,
            "capabilities");
        Require(capabilities.arraySize > 0,
            "The equipment formula catalog has no capability.");
        SerializedProperty capability = capabilities.GetArrayElementAtIndex(0);
        SerializedProperty applicatorId = RequireRelative(
            capability,
            "applicatorId");
        SerializedProperty statId = RequireRelative(capability, "statId");
        SerializedProperty modifierKind = RequireRelative(
            capability,
            "modifierKind");
        ContentReferenceSelection.Entry replacement =
            ContentReferenceSelection.Entries(
                    new ContentReferenceAttribute(
                        ContentReferenceKind.EquipmentApplicator),
                    applicatorId)
                .First(value => !string.Equals(
                    value.Id,
                    applicatorId.stringValue,
                    StringComparison.Ordinal));
        Require(EquipmentEvolutionFormulaCapabilityDefinition.TryResolveApplicator(
                replacement.Id,
                out string expectedStat,
                out EquipmentEvolutionFormulaModifierKind expectedModifier),
            "The selected equipment applicator is not registered.");

        EquipmentEvolutionFormulaCapabilityDefinition applied =
            target.capabilities[0];
        string originalApplicator = applied.applicatorId;
        string originalStat = applied.statId;
        EquipmentEvolutionFormulaModifierKind originalModifier =
            applied.modifierKind;
        int undoGroup = BeginUndo("R19 equipment applicator");
        ContentReferenceSelection.WriteId(
            applicatorId,
            ContentReferenceKind.EquipmentApplicator,
            replacement.Id);
        Require(applicatorId.stringValue == replacement.Id
                && statId.stringValue == expectedStat
                && modifierKind.intValue == (int)expectedModifier,
            "Equipment applicator selection did not update stat and modifier atomically.");
        Require(applied.applicatorId == originalApplicator
                && applied.statId == originalStat
                && applied.modifierKind == originalModifier,
            "Pending equipment applicator was applied before ApplyModifiedProperties.");
        RequireOnlyCandidate(
            statId,
            ContentReferenceKind.EquipmentStat,
            expectedStat,
            "Pending equipment stat");
        DrawProbe(window, applicatorId, "equipment applicator selection");
        DrawProbe(window, statId, "pending equipment stat");

        Require(serialized.ApplyModifiedProperties(),
            "Equipment applicator selection did not apply.");
        applied = target.capabilities[0];
        Require(applied.applicatorId == replacement.Id
                && applied.statId == expectedStat
                && applied.modifierKind == expectedModifier,
            "Applied equipment applicator tuple is inconsistent.");
        EndUndo(target, baseline, undoGroup, "Equipment applicator");
    }

    private static void VerifyProductionPolicies(
        ContentReferenceInspectorProbeWindow window,
        ProductionRecipeSO massTarget,
        ProductionRecipeSO outputTarget)
    {
        VerifyProductionPolicy(
            window,
            massTarget,
            "massExplanation",
            ContentReferenceKind.ProductionMassPolicy,
            requireAlternate: true,
            "production mass policy");
        VerifyProductionPolicy(
            window,
            outputTarget,
            "outputCostAllocation",
            ContentReferenceKind.ProductionOutputCostPolicy,
            requireAlternate: false,
            "production output cost policy");
    }

    private static void VerifyProductionPolicy(
        ContentReferenceInspectorProbeWindow window,
        ProductionRecipeSO target,
        string ownerPath,
        ContentReferenceKind referenceKind,
        bool requireAlternate,
        string context)
    {
        string baseline = EditorJsonUtility.ToJson(target);
        Undo.ClearUndo(target);
        using SerializedObject serialized = new(target);
        SerializedProperty owner = RequireProperty(serialized, ownerPath);
        SerializedProperty capabilityId = RequireRelative(owner, "capabilityId");
        SerializedProperty contractVersion = RequireRelative(
            owner,
            "contractVersion");
        SerializedProperty canonicalPayload = RequireRelative(
            owner,
            "canonicalPayload");
        ContentReferenceSelection.Entry[] candidates =
            ContentReferenceSelection.Entries(
                    new ContentReferenceAttribute(referenceKind),
                    capabilityId)
                .ToArray();
        Require(candidates.Length > 0,
            context + " has no registered ID and version candidate.");
        string current = ContentReferenceSelection.ReadId(
            capabilityId,
            referenceKind);
        ContentReferenceSelection.Entry selection = requireAlternate
            ? candidates.First(value => !string.Equals(
                value.Id,
                current,
                StringComparison.Ordinal))
            : candidates[0];
        string payloadBefore = canonicalPayload.stringValue;
        Require(payloadBefore.Length > 0,
            context + " source has no payload for the preservation probe.");
        int undoGroup = BeginUndo("R19 " + context);

        contractVersion.intValue = 99;
        ContentReferenceSelection.WriteId(
            capabilityId,
            referenceKind,
            selection.Id);
        Require(ContentReferenceSelection.ReadId(capabilityId, referenceKind)
                    == selection.Id
                && contractVersion.intValue != 99,
            context + " selection did not synchronize its contract version.");
        Require(canonicalPayload.stringValue == payloadBefore,
            context + " selection cleared its canonical payload.");
        DrawProbe(window, capabilityId, context + " selection");
        Require(serialized.ApplyModifiedProperties(),
            context + " selection did not apply.");

        serialized.Update();
        owner = RequireProperty(serialized, ownerPath);
        capabilityId = RequireRelative(owner, "capabilityId");
        contractVersion = RequireRelative(owner, "contractVersion");
        canonicalPayload = RequireRelative(owner, "canonicalPayload");
        Require(canonicalPayload.stringValue == payloadBefore,
            context + " applied selection lost its payload.");
        ContentReferenceSelection.WriteId(
            capabilityId,
            referenceKind,
            string.Empty);
        Require(capabilityId.stringValue.Length == 0
                && contractVersion.intValue == 0
                && canonicalPayload.stringValue.Length == 0,
            context + " None did not clear ID, version, and payload together.");
        DrawProbe(window, capabilityId, context + " None");
        Require(serialized.ApplyModifiedProperties(),
            context + " None did not apply.");
        EndUndo(target, baseline, undoGroup, context);
    }

    private static void VerifyCoreSessionNumericSelectors(
        ContentReferenceInspectorProbeWindow window,
        CoreSessionRulesSO target)
    {
        string baseline = EditorJsonUtility.ToJson(target);
        Undo.ClearUndo(target);
        using SerializedObject serialized = new(target);
        SerializedProperty incident = RequireProperty(
            serialized,
            "externalProblemBands.Array.data[1].allowedIncidentKinds.Array.data[0]");
        SerializedProperty category = RequireProperty(
            serialized,
            "serviceResearch.Array.data[0].serviceCategory");
        SerializedProperty mode = RequireProperty(
            serialized,
            "serviceResearch.Array.data[0].operationMode");
        var incidentReference = new ContentReferenceAttribute(
            ContentReferenceKind.CoreIncident,
            emptyNumericId: 0);
        var categoryReference = new ContentReferenceAttribute(
            ContentReferenceKind.CoreServiceCategory);
        var modeReference = new ContentReferenceAttribute(
            ContentReferenceKind.CoreServiceMode,
            emptyNumericId: 0);
        ContentReferenceSelection.Entry[] incidents =
            ContentReferenceSelection.Entries(incidentReference, incident)
                .ToArray();
        ContentReferenceSelection.Entry[] categories =
            ContentReferenceSelection.Entries(categoryReference, category)
                .ToArray();
        ContentReferenceSelection.Entry[] modes =
            ContentReferenceSelection.Entries(modeReference, mode).ToArray();
        Require(incidents.Length == 6,
            "Core incident selector did not expose IDs 1 through 6.");
        Require(categories.Length == 5,
            "Core service category selector did not expose IDs 0 through 4.");
        Require(modes.Length == 2,
            "Core service mode selector did not expose IDs 1 and 2.");
        RequireHumanLabels(incidents, "Core incidents");
        RequireHumanLabels(categories, "Core service categories");
        RequireHumanLabels(modes, "Core service modes");

        ContentReferenceSelection.Entry incidentSelection = incidents.First(
            value => value.Id == "3");
        ContentReferenceSelection.Entry categorySelection = categories.First(
            value => value.Id == "4");
        ContentReferenceSelection.Entry modeSelection = modes.First(
            value => value.Id == "2");
        int undoGroup = BeginUndo("R19 core numeric selectors");
        ContentReferenceSelection.WriteId(
            incident,
            ContentReferenceKind.CoreIncident,
            incidentSelection.Id,
            0);
        ContentReferenceSelection.WriteId(
            category,
            ContentReferenceKind.CoreServiceCategory,
            categorySelection.Id);
        ContentReferenceSelection.WriteId(
            mode,
            ContentReferenceKind.CoreServiceMode,
            modeSelection.Id,
            0);
        Require(incident.intValue == 3
                && category.intValue == 4
                && mode.intValue == 2,
            "Core selectors did not serialize their numeric IDs.");
        DrawProbe(window, incident, "core incident numeric selector");
        DrawProbe(window, category, "core service category numeric selector");
        DrawProbe(window, mode, "core service mode numeric selector");
        Require(serialized.ApplyModifiedProperties(),
            "Core numeric selections did not apply.");
        EndUndo(target, baseline, undoGroup, "Core numeric selectors");
    }

    private static void RequireOnlyCandidate(
        SerializedProperty property,
        ContentReferenceKind kind,
        string expected,
        string context)
    {
        string[] ids = ContentReferenceSelection.Entries(
                new ContentReferenceAttribute(kind),
                property)
            .Select(value => value.Id)
            .ToArray();
        Require(ids.Length == 1 && ids[0] == expected,
            context + " did not resolve from the pending sibling value.");
    }

    private static void RequireSameIds(
        IEnumerable<ContentReferenceSelection.Entry> entries,
        IEnumerable<string> expected,
        string context)
    {
        string[] actual = entries.Select(value => value.Id)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] required = expected.OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(actual.SequenceEqual(required, StringComparer.Ordinal),
            context + " do not match the runtime target authority.");
    }

    private static void RequireHumanLabels(
        IEnumerable<ContentReferenceSelection.Entry> entries,
        string context)
    {
        Require(entries.All(value => !string.IsNullOrWhiteSpace(value.Label)
                && !string.Equals(value.Label, value.Id, StringComparison.Ordinal)),
            context + " do not expose human-readable labels.");
    }

    private static int BeginUndo(string name)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(name);
        return group;
    }

    private static void EndUndo(
        ScriptableObject target,
        string baseline,
        int undoGroup,
        string context)
    {
        Undo.CollapseUndoOperations(undoGroup);
        Require(EditorJsonUtility.ToJson(target) != baseline,
            context + " did not change the clone before Undo.");
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Require(EditorJsonUtility.ToJson(target) == baseline,
            context + " did not restore the original clone JSON with one Undo.");
    }

    private static void DrawProbe(
        ContentReferenceInspectorProbeWindow window,
        SerializedProperty property,
        string context)
    {
        window.Property = property;
        int previous = window.DrawCount;
        window.SendEvent(new Event { type = EventType.Layout });
        window.SendEvent(new Event { type = EventType.Repaint });
        if (window.Failure != null)
            throw new InvalidOperationException(context, window.Failure);
        Require(window.DrawCount > previous,
            "Inspector GUI did not execute: " + context);
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string path)
    {
        SerializedProperty property = serialized.FindProperty(path);
        Require(property != null,
            "Serialized property is unavailable: " + path);
        return property;
    }

    private static SerializedProperty RequireRelative(
        SerializedProperty parent,
        string name)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        Require(property != null,
            "Serialized child is unavailable: " + parent.propertyPath + "." + name);
        return property;
    }

    private static T RequireAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException(
                $"Required asset is missing at '{path}'.");
        return asset;
    }

    private static ScriptableObject Clone(ScriptableObject source)
    {
        ScriptableObject clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        return clone;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private readonly struct SourceSnapshot
    {
        private readonly ScriptableObject asset;
        private readonly string json;
        private readonly bool dirty;

        private SourceSnapshot(
            ScriptableObject asset,
            string json,
            bool dirty)
        {
            this.asset = asset;
            this.json = json;
            this.dirty = dirty;
        }

        public static SourceSnapshot Capture(ScriptableObject asset) =>
            new(asset, EditorJsonUtility.ToJson(asset), EditorUtility.IsDirty(asset));

        public void RequireUnchanged()
        {
            Require(EditorJsonUtility.ToJson(asset) == json,
                $"Source asset '{asset.name}' JSON changed.");
            Require(EditorUtility.IsDirty(asset) == dirty,
                $"Source asset '{asset.name}' dirty state changed.");
        }
    }
}
