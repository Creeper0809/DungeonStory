using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R18MedicalPolicyReferenceDebugScenarios
{
    private const string MissingFamily = "humanoidd";
    private const string HumanoidFamily = "humanoid";
    private const string AdventurerSpecies = "Adventurer";
    private static readonly string[] LegacyFactionPaths =
    {
        "Assets/Resources/SO/Factions/faction_dungeon_beastkin.asset",
        "Assets/Resources/SO/Factions/faction_dungeon_demon.asset",
        "Assets/Resources/SO/Factions/faction_dungeon_golem.asset",
        "Assets/Resources/SO/Factions/faction_dungeon_harpy.asset",
        "Assets/Resources/SO/Factions/faction_dungeon_kobold.asset",
        "Assets/Resources/SO/Factions/faction_dungeon_myconid.asset"
    };

    [MenuItem("DungeonStory/V27/Content/Validate R18 Medical Policy References")]
    public static void RunAll()
    {
        IGameContentCatalog content =
            new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        VerifyProcedureFamilyReference(content);
        VerifyConditionLexiconFallback(content);
        VerifyConditionLexiconRequiredSpecies(content);
        VerifyFactionEconomicPolicies(content);
        Debug.Log(
            "R18_MEDICAL_POLICY_REFERENCES=PASS; procedure family rejection; condition family fallback; policy registry rejection; repair retry; unchanged source");
    }

    // Invoke only through the isolated batch-mode Inspector contract runner.
    public static void VerifyInspectorGUI()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException(
                "R18 medical policy Inspector verification requires isolated batch mode.");

        IGameContentCatalog content =
            new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        GameplayEffectDefinitionSO effectSource =
            content.GetAll<GameplayEffectDefinitionSO>()
                .First(value => !string.IsNullOrWhiteSpace(value.TargetId));
        SurgicalProcedureSO procedureSource =
            content.GetAll<SurgicalProcedureSO>()
                .Single(value => string.Equals(
                    value.ProcedureId,
                    "procedure:amputation",
                    StringComparison.Ordinal));
        CharacterTraitSO traitSource = content.GetAll<CharacterTraitSO>()
            .Single(value => value.id == 301);
        DungeonFactionDefinitionSO factionSource =
            content.GetAll<DungeonFactionDefinitionSO>()
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .First();
        ScriptableObject[] sources =
        {
            effectSource, procedureSource, traitSource, factionSource
        };
        SourceSnapshot[] snapshots = sources.Select(SourceSnapshot.Capture)
            .ToArray();
        ScriptableObject[] clones = sources.Select(value =>
        {
            ScriptableObject clone = Object.Instantiate(value);
            clone.hideFlags = HideFlags.HideAndDontSave;
            return clone;
        }).ToArray();
        string[] cloneJson = clones.Select(EditorJsonUtility.ToJson).ToArray();
        ContentReferenceInspectorProbeWindow window =
            ScriptableObject.CreateInstance<ContentReferenceInspectorProbeWindow>();
        try
        {
            GameplayEffectDefinitionSO effect =
                (GameplayEffectDefinitionSO)clones[0];
            SurgicalProcedureSO procedure =
                (SurgicalProcedureSO)clones[1];
            CharacterTraitSO trait = (CharacterTraitSO)clones[2];
            DungeonFactionDefinitionSO faction =
                (DungeonFactionDefinitionSO)clones[3];

            ContentReferenceSelection.Invalidate();
            window.position = new Rect(0, 0, 720, 300);
            window.ShowUtility();

            VerifyGameplayEffectTargetInspector(window, effect);
            VerifyProcedureFamilyInspector(window, procedure, content);
            VerifyFixedTraitRuleInspector(window, trait);
            VerifyFactionPolicyInspector(window, faction);

            Require(window.DrawCount >= 6,
                "R18 medical policy PropertyDrawer GUI events did not execute.");
            for (int index = 0; index < clones.Length; index++)
                Require(EditorJsonUtility.ToJson(clones[index])
                        == cloneJson[index],
                    $"Inspector verification changed clone '{clones[index].name}' after Undo.");
            Debug.Log(
                "R18_MEDICAL_POLICY_REFERENCE_INSPECTOR=PASS; gameplay target; anatomy family; fixed trait rule; faction policy pair; none; invalid version; undo; unchanged source");
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

    private static void VerifyGameplayEffectTargetInspector(
        ContentReferenceInspectorProbeWindow window,
        GameplayEffectDefinitionSO effect)
    {
        using SerializedObject serialized = new(effect);
        SerializedProperty target = RequireProperty(serialized, "targetId");
        ContentReferenceAttribute reference = new(
            ContentReferenceKind.GameplayEffectTarget);
        ContentReferenceAttribute resolved =
            ContentReferenceSelection.ResolveReference(reference, target);
        Require(resolved?.Kind == ContentReferenceKind.GameplayEffectTarget,
            "Gameplay effect target did not resolve its reference kind.");
        string[] candidates = ContentReferenceSelection
            .Entries(resolved, target)
            .Select(value => value.Id)
            .ToArray();
        string codeCandidate = GameplayEffectTargetIds.DeclaredTargets
            .First(value => !string.Equals(
                value,
                effect.TargetId,
                StringComparison.Ordinal));
        Require(candidates.Contains(codeCandidate, StringComparer.Ordinal)
                && candidates.Contains(effect.TargetId, StringComparer.Ordinal),
            "Gameplay effect target candidates omitted a code or authored target.");
        DrawProbe(window, target, "gameplay effect target");
        Require(string.Equals(
                target.stringValue,
                effect.TargetId,
                StringComparison.Ordinal),
            "Drawing the gameplay effect target changed its value.");
    }

    private static void VerifyProcedureFamilyInspector(
        ContentReferenceInspectorProbeWindow window,
        SurgicalProcedureSO procedure,
        IGameContentCatalog content)
    {
        using SerializedObject serialized = new(procedure);
        SerializedProperty families = RequireProperty(
            serialized,
            "allowedAnatomyFamilies");
        int appliedCount = procedure.AllowedAnatomyFamilies.Count;
        families.arraySize++;
        SerializedProperty pending = families.GetArrayElementAtIndex(
            families.arraySize - 1);
        pending.stringValue = HumanoidFamily;
        ContentReferenceAttribute reference = new(
            ContentReferenceKind.AnatomyFamily);
        RequireSameIds(
            ContentReferenceSelection.Entries(reference, pending)
                .Select(value => value.Id),
            content.GetAll<AnatomyProfileSO>()
                .Select(value => value.AnatomyFamily),
            "Inspector anatomy family candidates");
        DrawProbe(window, pending, "pending surgical anatomy family");
        Require(procedure.AllowedAnatomyFamilies.Count == appliedCount,
            "Drawing a pending anatomy family applied it to the procedure.");
        serialized.Update();
    }

    private static void VerifyFixedTraitRuleInspector(
        ContentReferenceInspectorProbeWindow window,
        CharacterTraitSO trait)
    {
        using SerializedObject serialized = new(trait);
        SerializedProperty rule = RequireProperty(
            serialized,
            "identityRules.Array.data[0]");
        SerializedProperty ruleId = rule.FindPropertyRelative("ruleId")
            ?? throw new InvalidOperationException(
                "Trait 301 ruleId property was not found.");
        Require(rule.managedReferenceValue != null,
            "Trait 301 has no identity rule instance.");
        Require(CharacterTraitIdentityRuleContract.TryGetExpectedKey(
                trait.id,
                rule.managedReferenceValue.GetType(),
                ruleId.name,
                out string expected),
            "Trait 301 did not expose its fixed rule contract.");
        ContentReferenceAttribute requested = new(
            ContentReferenceKind.FixedTraitRule);
        ContentReferenceAttribute resolved =
            ContentReferenceSelection.ResolveReference(requested, ruleId);
        string[] aliases = (resolved?.Aliases ?? string.Empty)
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        Require(resolved?.Kind == ContentReferenceKind.FixedTraitRule
                && aliases.Length == 1
                && string.Equals(
                    aliases[0],
                    expected,
                    StringComparison.Ordinal)
                && ContentReferenceSelection.Entries(resolved, ruleId).Count == 0,
            "Trait 301 did not expose exactly one fixed rule candidate.");
        DrawProbe(window, ruleId, "trait 301 fixed identity rule");
        Require(string.Equals(
                ruleId.stringValue,
                expected,
                StringComparison.Ordinal),
            "Drawing the fixed trait rule changed its contract key.");
    }

    private static void VerifyFactionPolicyInspector(
        ContentReferenceInspectorProbeWindow window,
        DungeonFactionDefinitionSO faction)
    {
        Undo.ClearUndo(faction);
        using SerializedObject serialized = new(faction);
        SerializedProperty capability = RequireProperty(
            serialized,
            "tradeEconomicPolicy.capabilityId");
        SerializedProperty version = RequireProperty(
            serialized,
            "tradeEconomicPolicy.capabilityVersion");
        ContentReferenceAttribute reference = new(
            ContentReferenceKind.FactionEconomicPolicy);
        string[] candidates = ContentReferenceSelection
            .Entries(reference, capability)
            .Select(value => value.Id)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(candidates.Length == 2,
            "Faction policy Inspector did not expose both built-in policies.");

        string originalId = faction.tradeEconomicPolicy?.capabilityId
            ?? string.Empty;
        int originalVersion = faction.tradeEconomicPolicy?.capabilityVersion
            ?? 0;
        string originalKey = ContentReferenceSelection.ReadId(
            capability,
            ContentReferenceKind.FactionEconomicPolicy);
        string selectedKey = candidates.First(value => !string.Equals(
            value,
            originalKey,
            StringComparison.Ordinal));

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("R18 faction economic policy pair");
        ContentReferenceSelection.WriteId(
            capability,
            ContentReferenceKind.FactionEconomicPolicy,
            selectedKey);
        Require(string.Equals(
                    ContentReferenceSelection.ReadId(
                        capability,
                        ContentReferenceKind.FactionEconomicPolicy),
                    selectedKey,
                    StringComparison.Ordinal)
                && version.intValue > 0,
            "Faction policy selection did not update ID and version together.");
        DrawProbe(window, capability, "faction economic policy pair");
        Require(string.Equals(
                    faction.tradeEconomicPolicy?.capabilityId,
                    originalId,
                    StringComparison.Ordinal)
                && (faction.tradeEconomicPolicy?.capabilityVersion ?? 0)
                    == originalVersion,
            "Drawing a pending faction policy applied it before serialization.");
        Require(serialized.ApplyModifiedProperties(),
            "Faction policy pair did not apply for the Undo probe.");
        Undo.CollapseUndoOperations(undoGroup);
        Require(string.Equals(
                    faction.tradeEconomicPolicy.capabilityId,
                    selectedKey.Substring(0, selectedKey.LastIndexOf('@')),
                    StringComparison.Ordinal)
                && faction.tradeEconomicPolicy.capabilityVersion
                    == version.intValue,
            "Applied faction policy lost its ID or version.");
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        serialized.Update();
        capability = RequireProperty(
            serialized,
            "tradeEconomicPolicy.capabilityId");
        version = RequireProperty(
            serialized,
            "tradeEconomicPolicy.capabilityVersion");
        Require(string.Equals(
                    capability.stringValue,
                    originalId,
                    StringComparison.Ordinal)
                && version.intValue == originalVersion,
            "Undo did not restore the faction policy ID and version pair.");

        ContentReferenceSelection.WriteId(
            capability,
            ContentReferenceKind.FactionEconomicPolicy,
            string.Empty);
        Require(capability.stringValue.Length == 0
                && version.intValue == 0
                && ContentReferenceSelection.ReadId(
                    capability,
                    ContentReferenceKind.FactionEconomicPolicy).Length == 0,
            "Faction policy None did not serialize as an empty ID and version 0.");
        DrawProbe(window, capability, "faction economic policy none");
        serialized.Update();

        capability = RequireProperty(
            serialized,
            "tradeEconomicPolicy.capabilityId");
        version = RequireProperty(
            serialized,
            "tradeEconomicPolicy.capabilityVersion");
        int split = candidates[0].LastIndexOf('@');
        string validId = candidates[0].Substring(0, split);
        int invalidVersion = int.Parse(candidates[0].Substring(split + 1)) + 1;
        capability.stringValue = validId;
        version.intValue = invalidVersion;
        string invalidKey = ContentReferenceSelection.ReadId(
            capability,
            ContentReferenceKind.FactionEconomicPolicy);
        Require(!candidates.Contains(invalidKey, StringComparer.Ordinal),
            "Faction policy wrong-version fixture unexpectedly matched a candidate.");
        DrawProbe(window, capability, "faction economic policy wrong version");
        Require(string.Equals(
                    ContentReferenceSelection.ReadId(
                        capability,
                        ContentReferenceKind.FactionEconomicPolicy),
                    invalidKey,
                    StringComparison.Ordinal)
                && capability.stringValue == validId
                && version.intValue == invalidVersion,
            "Drawing an invalid faction policy version repaired it silently.");
        serialized.Update();
    }

    private static void VerifyProcedureFamilyReference(
        IGameContentCatalog content)
    {
        SurgicalProcedureSO source = content.GetAll<SurgicalProcedureSO>()
            .Single(value => string.Equals(
                value.ProcedureId,
                "procedure:amputation",
                StringComparison.Ordinal));
        SourceSnapshot snapshot = SourceSnapshot.Capture(source);
        SurgicalProcedureSO clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            ContentReferenceCatalog catalog =
                ContentReferenceCatalog.FromRoot(content.Root);
            RequireSameIds(
                catalog.Entries(new ContentReferenceAttribute(
                        ContentReferenceKind.AnatomyFamily))
                    .Select(value => value.Id),
                content.GetAll<AnatomyProfileSO>()
                    .Select(value => value.AnatomyFamily),
                "Anatomy family candidates");
            RequireClean(
                catalog.Validate(new ScriptableObject[] { clone }),
                "Amputation source definition");

            SetStringArray(clone, "allowedAnatomyFamilies", MissingFamily);
            SetStringArray(clone, "allowedSpeciesIds");
            SetString(clone, "targetNodeId", string.Empty);
            IReadOnlyList<string> errors = catalog.Validate(
                new ScriptableObject[] { clone });
            Require(errors.Any(value => value.Contains(
                        "select no eligible profile",
                        StringComparison.OrdinalIgnoreCase)),
                "A family-only procedure typo with no species or target was accepted: "
                + string.Join(" | ", errors));
            Require(errors.Any(value => value.Contains(
                        MissingFamily,
                        StringComparison.OrdinalIgnoreCase)),
                "The rejected procedure family did not identify the bad key: "
                + string.Join(" | ", errors));

            SetStringArray(clone, "allowedAnatomyFamilies", HumanoidFamily);
            RequireClean(
                catalog.Validate(new ScriptableObject[] { clone }),
                "Amputation family repair");
            RequireClean(
                catalog.Validate(new ScriptableObject[] { clone }),
                "Amputation family repair retry");
        }
        finally
        {
            Object.DestroyImmediate(clone);
            snapshot.RequireUnchanged();
        }
    }

    private static void VerifyConditionLexiconRequiredSpecies(
        IGameContentCatalog content)
    {
        AnatomyProfileSO source = content.GetAll<AnatomyProfileSO>()
            .Single(value => value.SpeciesIds.Contains(
                AdventurerSpecies,
                StringComparer.OrdinalIgnoreCase));
        SourceSnapshot snapshot = SourceSnapshot.Capture(source);
        AnatomyProfileSO clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            SetString(clone, "anatomyFamily", MissingFamily);
            IGameContentCatalog altered =
                new ReplacementContentCatalog(content, source, clone);
            ResourceAnatomyProfileCatalog alteredProfiles =
                new ResourceAnatomyProfileCatalog(altered);
            IReadOnlyList<string> errors =
                new ResourceAnatomyConditionLexicon(altered)
                    .Validate(alteredProfiles);
            Require(errors.Any(value => value.Contains(
                        AdventurerSpecies + ": missing",
                        StringComparison.OrdinalIgnoreCase)),
                "The lexicon validator did not inspect the authored Adventurer species: "
                + string.Join(" | ", errors));

            SetString(clone, "anatomyFamily", HumanoidFamily);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                ResourceAnatomyProfileCatalog repairedProfiles =
                    new ResourceAnatomyProfileCatalog(altered);
                ResourceAnatomyConditionLexicon repairedLexicon =
                    new ResourceAnatomyConditionLexicon(altered);
                RequireClean(
                    repairedLexicon.Validate(repairedProfiles),
                    attempt == 0
                        ? "Required species family repair"
                        : "Required species family repair retry");
            }
        }
        finally
        {
            Object.DestroyImmediate(clone);
            snapshot.RequireUnchanged();
        }
    }

    private static void VerifyConditionLexiconFallback(
        IGameContentCatalog content)
    {
        AnatomyConditionLexiconSO source =
            content.GetAll<AnatomyConditionLexiconSO>()
                .Single(value => string.Equals(
                    value.LexiconId,
                    "condition:biological",
                    StringComparison.Ordinal));
        CharacterSpeciesDefinitionSO adventurer =
            content.GetAll<CharacterSpeciesDefinitionSO>()
                .Single(value => string.Equals(
                    value.speciesTag,
                    AdventurerSpecies,
                    StringComparison.Ordinal));
        SourceSnapshot snapshot = SourceSnapshot.Capture(source);
        AnatomyConditionLexiconSO clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            ResourceAnatomyProfileCatalog profiles =
                new ResourceAnatomyProfileCatalog(content);
            ResourceAnatomyConditionLexicon lexicon =
                new ResourceAnatomyConditionLexicon(content);
            RequireClean(
                lexicon.Validate(profiles),
                "Authored anatomy condition lexicons");
            Require(!source.SpeciesIds.Contains(
                    adventurer.speciesTag,
                    StringComparer.OrdinalIgnoreCase),
                "The biological lexicon directly lists Adventurer, so the family fallback was not exercised.");
            RequireFamilyFallback(source, profiles, lexicon);

            SetString(clone, "anatomyFamily", MissingFamily);
            IGameContentCatalog altered =
                new ReplacementContentCatalog(content, source, clone);
            ResourceAnatomyProfileCatalog alteredProfiles =
                new ResourceAnatomyProfileCatalog(altered);
            IReadOnlyList<string> errors =
                new ResourceAnatomyConditionLexicon(altered)
                    .Validate(alteredProfiles);
            Require(errors.Any(value => value.Contains(
                        "unknown anatomy condition lexicon family",
                        StringComparison.OrdinalIgnoreCase)
                    && value.Contains(
                        MissingFamily,
                        StringComparison.OrdinalIgnoreCase)),
                "A lexicon with an unknown anatomy family was accepted: "
                + string.Join(" | ", errors));

            SetString(clone, "anatomyFamily", HumanoidFamily);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                ResourceAnatomyProfileCatalog repairedProfiles =
                    new ResourceAnatomyProfileCatalog(altered);
                ResourceAnatomyConditionLexicon repairedLexicon =
                    new ResourceAnatomyConditionLexicon(altered);
                RequireClean(
                    repairedLexicon.Validate(repairedProfiles),
                    attempt == 0
                        ? "Anatomy lexicon family repair"
                        : "Anatomy lexicon family repair retry");
                RequireFamilyFallback(clone, repairedProfiles, repairedLexicon);
            }
        }
        finally
        {
            Object.DestroyImmediate(clone);
            snapshot.RequireUnchanged();
        }
    }

    private static void RequireFamilyFallback(
        AnatomyConditionLexiconSO biological,
        IAnatomyProfileCatalog profiles,
        IAnatomyConditionLexicon lexicon)
    {
        AnatomyProfileDefinition profile =
            profiles.GetForSpecies(AdventurerSpecies);
        Require(string.Equals(
                profile.AnatomyFamily,
                HumanoidFamily,
                StringComparison.OrdinalIgnoreCase),
            "Adventurer is not mapped to the authored humanoid anatomy family.");
        AnatomyConditionLexiconEntry expected = biological.Entries
            .Single(value => value.condition == AnatomyConditionKind.FluidLoss);
        Require(lexicon.TryResolve(
                AdventurerSpecies,
                profile.AnatomyFamily,
                AnatomyConditionKind.FluidLoss,
                out AnatomyConditionPresentation actual),
            "Adventurer did not resolve the biological condition through its anatomy family.");
        Require(string.Equals(actual.Label, expected.label, StringComparison.Ordinal)
                && string.Equals(
                    actual.TreatmentVerb,
                    expected.treatmentVerb,
                    StringComparison.Ordinal)
                && string.Equals(actual.IconId, expected.iconId, StringComparison.Ordinal)
                && string.Equals(actual.VfxId, expected.vfxId, StringComparison.Ordinal),
            "Adventurer family fallback did not use condition:biological.");
    }

    private static void VerifyFactionEconomicPolicies(
        IGameContentCatalog content)
    {
        IDungeonItemCatalogProvider items =
            new ResourceDungeonItemCatalogProvider(
                new ResourceItemDefinitionCatalog(content));
        FactionRouteEconomicPolicyRegistry registry =
            FactionRouteEconomicPolicyRegistry.CreateBuiltIns(items);
        FactionRouteEconomicPolicyDescriptor[] policies =
            registry.Definitions.ToArray();
        Require(policies.Length == 2
                && policies.All(registry.Contains),
            "The built-in faction economic policy registry is incomplete.");
        ContentReferenceCatalog references =
            ContentReferenceCatalog.FromRoot(content.Root);
        RequireSameIds(
            references.Entries(new ContentReferenceAttribute(
                    ContentReferenceKind.FactionEconomicPolicy))
                .Select(value => value.Id),
            policies.Select(value => value.capabilityId
                + "@" + value.capabilityVersion),
            "Faction economic policy candidates");

        DungeonFactionDefinitionSO[] factions =
            content.GetAll<DungeonFactionDefinitionSO>()
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .ToArray();
        Require(factions.Length == 6,
            $"Expected six authored faction definitions, found {factions.Length}.");
        foreach (DungeonFactionDefinitionSO faction in factions)
        {
            Require(registry.Contains(faction.tradeEconomicPolicy)
                    && registry.Contains(faction.supplyEconomicPolicy),
                "Faction policy registration is missing for "
                + faction.StableId + ".");
            FactionDefinitionSnapshot definition = faction.ToSnapshot();
            Require(registry.TryCreateQuote(
                    definition,
                    FactionRouteKind.TradeCaravan,
                    out FactionRouteQuoteSnapshot trade,
                    out string tradeFailure),
                faction.StableId + " trade policy lookup failed: "
                + tradeFailure);
            Require(registry.TryCreateQuote(
                    definition,
                    FactionRouteKind.SupplyCaravan,
                    out FactionRouteQuoteSnapshot supply,
                    out string supplyFailure),
                faction.StableId + " supply policy lookup failed: "
                + supplyFailure);
            Require(trade != null
                    && supply != null
                    && string.Equals(
                        trade.CapabilityId,
                        faction.tradeEconomicPolicy.capabilityId,
                        StringComparison.Ordinal)
                    && trade.CapabilityVersion
                        == faction.tradeEconomicPolicy.capabilityVersion
                    && string.Equals(
                        supply.CapabilityId,
                        faction.supplyEconomicPolicy.capabilityId,
                        StringComparison.Ordinal)
                    && supply.CapabilityVersion
                        == faction.supplyEconomicPolicy.capabilityVersion,
                "Faction quote policy identity drifted for "
                + faction.StableId + ".");
        }

        ResourceDungeonFactionCatalogApplicationAdapter explicitAdapter =
            new ResourceDungeonFactionCatalogApplicationAdapter(
                content,
                registry);
        ResourceDungeonFactionCatalogApplicationAdapter defaultAdapter =
            new ResourceDungeonFactionCatalogApplicationAdapter(content);
        Require(explicitAdapter.Definitions.Count == 6
                && defaultAdapter.Definitions.Count == 6,
            "The public faction catalog adapter did not expose all six definitions.");
        VerifyLegacyFactionEconomicPolicies(registry, references);

        DungeonFactionDefinitionSO source = factions.First(value =>
            value.tradeCargo != null && value.tradeCargo.Count > 0);
        SourceSnapshot snapshot = SourceSnapshot.Capture(source);
        string baseline = EditorJsonUtility.ToJson(source);
        DungeonFactionDefinitionSO clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Restore(clone, baseline);
            clone.tradeCargo = new List<FactionCargoLine>();
            clone.tradeEconomicPolicy =
                FactionRouteEconomicPolicyDescriptor.Create(string.Empty, 0);
            Require(!clone.tradeEconomicPolicy.HasSelection,
                "An empty faction policy descriptor was treated as selected.");
            IGameContentCatalog altered =
                new ReplacementContentCatalog(content, source, clone);
            RequireClean(
                references.Validate(new ScriptableObject[] { clone }),
                "No-cargo faction policy None");
            Require(new ResourceDungeonFactionCatalogApplicationAdapter(
                        altered,
                        registry).Definitions.Count == 6,
                "The faction adapter rejected None for a route without cargo.");

            Restore(clone, baseline);
            clone.tradeEconomicPolicy =
                FactionRouteEconomicPolicyDescriptor.Create(string.Empty, 0);
            IReadOnlyList<string> noPolicyErrors = references.Validate(
                new ScriptableObject[] { clone });
            Require(noPolicyErrors.Any(value => value.Contains(
                        "trade cargo requires a selected economic policy",
                        StringComparison.OrdinalIgnoreCase)),
                "Content validation accepted cargo with policy None: "
                + string.Join(" | ", noPolicyErrors));
            ExpectInvalidOperation(
                () => new ResourceDungeonFactionCatalogApplicationAdapter(
                    altered,
                    registry),
                "unregistered economic policy/version",
                "The faction adapter accepted None for a route with cargo.");

            FactionRouteEconomicPolicyDescriptor wrongId =
                FactionRouteEconomicPolicyDescriptor.Create(
                    "faction-economy:fixture-missing",
                    1);
            Require(!registry.Contains(wrongId),
                "The registry accepted an unknown policy ID.");
            clone.tradeEconomicPolicy = wrongId;
            ExpectInvalidOperation(
                () => new ResourceDungeonFactionCatalogApplicationAdapter(
                    altered,
                    registry),
                "unregistered economic policy/version",
                "The faction adapter accepted an unknown policy ID with cargo.");

            Restore(clone, baseline);
            FactionRouteEconomicPolicyDescriptor wrongVersion =
                FactionRouteEconomicPolicyDescriptor.Create(
                    clone.supplyEconomicPolicy.capabilityId,
                    clone.supplyEconomicPolicy.capabilityVersion + 1);
            Require(!registry.Contains(wrongVersion),
                "The registry accepted an unknown policy version.");
            clone.supplyCargo = new List<FactionCargoLine>();
            clone.supplyEconomicPolicy = wrongVersion;
            ExpectInvalidOperation(
                () => new ResourceDungeonFactionCatalogApplicationAdapter(
                    altered,
                    registry),
                "unregistered economic policy/version",
                "The faction adapter accepted an unknown descriptor without cargo.");

            Restore(clone, baseline);
            Require(new ResourceDungeonFactionCatalogApplicationAdapter(
                        altered,
                        registry).Definitions.Count == 6,
                "The repaired faction definition was rejected.");
            Require(new ResourceDungeonFactionCatalogApplicationAdapter(
                        altered,
                        registry).Definitions.Count == 6,
                "The repaired faction definition failed on retry.");
        }
        finally
        {
            Object.DestroyImmediate(clone);
            snapshot.RequireUnchanged();
        }
    }

    private static void VerifyLegacyFactionEconomicPolicies(
        FactionRouteEconomicPolicyRegistry registry,
        ContentReferenceCatalog references)
    {
        DungeonFactionDefinitionSO[] factions = LegacyFactionPaths
            .Select(path => AssetDatabase
                .LoadAssetAtPath<DungeonFactionDefinitionSO>(path)
                ?? throw new InvalidOperationException(
                    $"Required legacy faction asset '{path}' was not found."))
            .ToArray();
        Require(factions.Length == 6
                && factions.Select(value => value.StableId)
                    .Distinct(StringComparer.Ordinal).Count() == 6,
            "The bounded legacy faction set is incomplete or duplicated.");
        SourceSnapshot[] snapshots = factions.Select(SourceSnapshot.Capture)
            .ToArray();
        try
        {
            int quoteCount = 0;
            foreach (DungeonFactionDefinitionSO faction in factions)
            {
                Require(faction.tradeCargo?.Count > 0
                        && faction.supplyCargo?.Count > 0,
                    "Legacy faction cargo is missing for "
                    + faction.StableId + ".");
                Require(registry.Contains(faction.tradeEconomicPolicy)
                        && registry.Contains(faction.supplyEconomicPolicy),
                    "Legacy faction policy registration is missing for "
                    + faction.StableId + ".");
                RequireClean(
                    references.Validate(new ScriptableObject[] { faction }),
                    "Legacy faction references " + faction.StableId);

                FactionDefinitionSnapshot definition = faction.ToSnapshot();
                Require(registry.TryCreateQuote(
                        definition,
                        FactionRouteKind.TradeCaravan,
                        out FactionRouteQuoteSnapshot trade,
                        out string tradeFailure),
                    faction.StableId + " legacy trade policy lookup failed: "
                    + tradeFailure);
                quoteCount++;
                Require(registry.TryCreateQuote(
                        definition,
                        FactionRouteKind.SupplyCaravan,
                        out FactionRouteQuoteSnapshot supply,
                        out string supplyFailure),
                    faction.StableId + " legacy supply policy lookup failed: "
                    + supplyFailure);
                quoteCount++;
                Require(trade != null
                        && supply != null
                        && string.Equals(
                            trade.CapabilityId,
                            faction.tradeEconomicPolicy.capabilityId,
                            StringComparison.Ordinal)
                        && trade.CapabilityVersion
                            == faction.tradeEconomicPolicy.capabilityVersion
                        && string.Equals(
                            supply.CapabilityId,
                            faction.supplyEconomicPolicy.capabilityId,
                            StringComparison.Ordinal)
                        && supply.CapabilityVersion
                            == faction.supplyEconomicPolicy.capabilityVersion,
                    "Legacy faction quote policy identity drifted for "
                    + faction.StableId + ".");
            }

            Require(quoteCount == 12,
                $"Expected 12 legacy faction policy quotes, found {quoteCount}.");
        }
        finally
        {
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
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

    private static void SetString(
        ScriptableObject target,
        string path,
        string value)
    {
        using SerializedObject serialized = new(target);
        RequireProperty(serialized, path).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStringArray(
        ScriptableObject target,
        string path,
        params string[] values)
    {
        using SerializedObject serialized = new(target);
        SerializedProperty array = RequireProperty(serialized, path);
        array.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
            array.GetArrayElementAtIndex(index).stringValue = values[index];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string path) => serialized.FindProperty(path)
        ?? throw new InvalidOperationException(
            $"Serialized property '{path}' was not found on '{serialized.targetObject.name}'.");

    private static void Restore(ScriptableObject clone, string json)
    {
        EditorJsonUtility.FromJsonOverwrite(json, clone);
    }

    private static void ExpectInvalidOperation(
        Action action,
        string expectedText,
        string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message.Contains(
                    expectedText,
                    StringComparison.OrdinalIgnoreCase),
                message + " Unexpected error: " + exception.Message);
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void RequireClean(
        IEnumerable<string> errors,
        string context)
    {
        string[] values = errors?.ToArray() ?? Array.Empty<string>();
        Require(values.Length == 0,
            context + " failed: " + string.Join(" | ", values));
    }

    private static void RequireSameIds(
        IEnumerable<string> actual,
        IEnumerable<string> expected,
        string context)
    {
        string[] actualIds = Normalize(actual);
        string[] expectedIds = Normalize(expected);
        Require(expectedIds.Length > 0,
            context + " expected set is empty.");
        Require(actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal),
            context + " mismatch. expected="
            + string.Join(",", expectedIds)
            + "; actual=" + string.Join(",", actualIds));
    }

    private static string[] Normalize(IEnumerable<string> values) =>
        (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ReplacementContentCatalog : IGameContentCatalog
    {
        private readonly IGameContentCatalog source;
        private readonly ScriptableObject original;
        private readonly ScriptableObject replacement;

        public ReplacementContentCatalog(
            IGameContentCatalog source,
            ScriptableObject original,
            ScriptableObject replacement)
        {
            this.source = source
                ?? throw new ArgumentNullException(nameof(source));
            this.original = original
                ?? throw new ArgumentNullException(nameof(original));
            this.replacement = replacement
                ?? throw new ArgumentNullException(nameof(replacement));
        }

        public GameContentCatalogSO Root => source.Root;
        public ItemDefinitionCatalogSO Items => source.Items;
        public WorldInteractionPresentationCatalogSO WorldPresentation =>
            source.WorldPresentation;
        public CharacterSkillSystemSettingsSO CharacterSkillSettings =>
            source.CharacterSkillSettings;
        public GameMediaCatalogSO Media => source.Media;
        public GameDomainContentCatalogSO Domain => source.Domain;
        public IReadOnlyList<ServiceProcessSO> ServiceProcesses =>
            source.ServiceProcesses;
        public RoomEnvironmentSettingsSO RoomEnvironmentSettings =>
            source.RoomEnvironmentSettings;
        public IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes =>
            source.SiteArchetypes;
        public IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites =>
            source.UrgentSites;
        public IReadOnlyList<OffenseDecisionCardSO> DecisionCards =>
            source.DecisionCards;
        public IReadOnlyList<OffenseEncounterSO> Encounters =>
            source.Encounters;

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject =>
            source.GetAll<T>()
                .Select(value => ReferenceEquals(value, original)
                    ? (T)replacement
                    : value)
                .ToArray();

        public T RequireSingle<T>() where T : ScriptableObject =>
            GetAll<T>().Single();
    }

    private sealed class SourceSnapshot
    {
        private readonly ScriptableObject source;
        private readonly string json;
        private readonly bool dirty;

        private SourceSnapshot(ScriptableObject source)
        {
            this.source = source;
            json = EditorJsonUtility.ToJson(source);
            dirty = EditorUtility.IsDirty(source);
        }

        public static SourceSnapshot Capture(ScriptableObject source) =>
            new(source ?? throw new ArgumentNullException(nameof(source)));

        public void RequireUnchanged()
        {
            Require(EditorJsonUtility.ToJson(source) == json,
                $"R18 fixture changed source asset '{source.name}'.");
            Require(EditorUtility.IsDirty(source) == dirty,
                $"R18 fixture changed dirty state for '{source.name}'.");
        }
    }
}
