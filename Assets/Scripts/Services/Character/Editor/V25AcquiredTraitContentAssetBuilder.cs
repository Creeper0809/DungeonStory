#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sole authoring source for the V25 acquired-trait content slice. This builder
/// deliberately creates only immutable definitions and the physical erasure
/// seal; manifestation, erasure, reward delivery, UI, and save authority stay
/// in their respective runtime owners.
/// </summary>
public static class V25AcquiredTraitContentAssetBuilder
{
    public const string SettingsId = "character:acquired-trait-settings";
    public const string SealItemId = "item:memory-erasure-seal";

    public const string SettingsPath =
        "Assets/Resources/SO/V25/AcquiredTraits/acquired-trait-settings.asset";
    public const string ModuleRoot = "Assets/Resources/SO/V25/AcquiredTraits/Modules";
    public const string SealItemPath =
        "Assets/Resources/SO/Items/Definitions/item_memory_erasure_seal.asset";

    private const string DomainCatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string ItemCatalogPath =
        "Assets/Resources/SO/Content/ItemDefinitionCatalog.asset";
    private const string RootCatalogPath =
        "Assets/Resources/SO/GameContentCatalog.asset";
    private const int SealUnitMassGrams = 100;
    private const int SealStackLimit = 75;

    private sealed class GateSpec
    {
        public int Milestone;
        public CharacterSkillRarity Rarity;
        public int Budget;
    }

    private sealed class EffectSpec
    {
        public string TargetId;
        public GameplayEffectOperation Operation;
        public float Value;
        public string ConditionId;
        public string EffectPath;
        public string ConditionPath;
        public bool IsDrawback;
    }

    private sealed class ModuleSpec
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public CharacterNarrativeDomain[] Domains;
        public string[] ConflictGroups;
        public EffectSpec[] Effects;
        public CharacterAcquiredTraitSpecialReactionDefinition[] Reactions =
            Array.Empty<CharacterAcquiredTraitSpecialReactionDefinition>();

        public string AssetPath => ModuleRoot + "/" + Id.Replace(':', '_') + ".asset";
    }

    private readonly struct EffectSemanticSnapshot
    {
        public EffectSemanticSnapshot(GameplayEffectDefinitionSO effect)
        {
            AssetName = effect.name;
            NumericId = effect.NumericId;
            EffectId = effect.EffectId;
            TargetId = effect.TargetId;
            Operation = effect.Operation;
            ProjectionPhase = effect.ProjectionPhase;
            AllowedSources = effect.AllowedSources;
            StackingPolicy = effect.StackingPolicy;
            MinimumResult = effect.MinimumResult;
            MaximumResult = effect.MaximumResult;
        }

        public string AssetName { get; }
        public int NumericId { get; }
        public string EffectId { get; }
        public string TargetId { get; }
        public GameplayEffectOperation Operation { get; }
        public GameplayEffectProjectionPhase ProjectionPhase { get; }
        public GameplayEffectSourceKind AllowedSources { get; }
        public GameplayEffectStackingPolicy StackingPolicy { get; }
        public float MinimumResult { get; }
        public float MaximumResult { get; }
    }

    private static readonly GateSpec[] GateSpecs =
    {
        new() { Milestone = 3, Rarity = CharacterSkillRarity.Common, Budget = 4 },
        new() { Milestone = 8, Rarity = CharacterSkillRarity.Advanced, Budget = 6 },
        new() { Milestone = 20, Rarity = CharacterSkillRarity.Rare, Budget = 8 }
    };

    private static readonly ModuleSpec[] ModuleSpecs =
    {
        Module(
            "acquired-trait:module:battle-temper",
            "전장의 담력",
            "전투 경험을 견디며 다져진 담력으로 전투력이 조금 높아집니다.",
            Domains(
                CharacterNarrativeDomain.Combat,
                CharacterNarrativeDomain.Invasion,
                CharacterNarrativeDomain.Expedition),
            Conflicts("risk-posture"),
            Multiply(
                GameplayEffectTargetIds.CombatPower,
                1.04f,
                "Assets/Resources/SO/V26/Effects/Definitions/effect_character_combat-power_multiply.asset")),
        Module(
            "acquired-trait:module:steady-hands",
            "흔들림 없는 손",
            "반복된 제작 경험이 손끝의 정밀함으로 남아 제작 품질 점수가 높아집니다.",
            Domains(CharacterNarrativeDomain.Work),
            Conflicts(),
            AddFlat(
                GameplayEffectTargetIds.CraftQualityScore,
                2f,
                "Assets/Resources/SO/V26/Effects/Definitions/effect_craft_quality-score_addflat.asset")),
        Module(
            "acquired-trait:module:scar-memory",
            "상흔의 기억",
            "힘든 기억을 견딘 경험이 부정적인 기분의 지속 시간을 조금 줄입니다.",
            Domains(CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Mood),
            Conflicts(),
            Multiply(
                GameplayEffectTargetIds.NegativeMoodDuration,
                0.96f,
                "Assets/Resources/SO/V26/Effects/Definitions/effect_character_negative-mood-duration_multiply.asset")),
        Module(
            "acquired-trait:module:night-vigil",
            "야간 경계",
            "밤의 긴 경계를 익힌 몸은 야간에는 빨라지고 낮에는 조금 느려집니다.",
            Domains(CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Survival),
            Conflicts("work-cycle"),
            Multiply(
                GameplayEffectTargetIds.WorkSpeed,
                1.04f,
                "Assets/Resources/SO/V26/Effects/Definitions/effect_character_work-speed_multiply.asset",
                "shift:night",
                "Assets/Resources/SO/V26/Effects/Conditions/shift_night.asset"),
            Drawback(Multiply(
                GameplayEffectTargetIds.WorkSpeed,
                0.99f,
                "Assets/Resources/SO/V26/Effects/Definitions/effect_character_work-speed_multiply.asset",
                "shift:day",
                "Assets/Resources/SO/V26/Effects/Conditions/shift_day.asset"))),
        Module(
            "acquired-trait:module:work-rhythm",
            "일의 리듬",
            "일을 마치는 자신만의 리듬이 생겨 작업 속도가 조금 높아집니다.",
            Domains(CharacterNarrativeDomain.Work),
            Conflicts("work-cycle"),
            Multiply(
                GameplayEffectTargetIds.WorkSpeed,
                1.03f,
                "Assets/Resources/SO/V26/Effects/Definitions/effect_character_work-speed_multiply.asset")),
        Module(
            "acquired-trait:module:social-bridge",
            "관계의 다리",
            "갈등과 화해를 거친 경험으로 관계 회복이 조금 빨라집니다.",
            Domains(CharacterNarrativeDomain.Relationship),
            Conflicts(),
            Multiply(
                GameplayEffectTargetIds.RelationshipRecovery,
                1.08f,
                "Assets/Resources/SO/V26/Effects/Definitions/effect_character_relationship-recovery_multiply.asset")),
        Module(
            "acquired-trait:module:survival-instinct",
            "생존 본능",
            "위험한 작업을 넘긴 경험이 위험 작업 중 사고 확률을 조금 낮춥니다.",
            Domains(
                CharacterNarrativeDomain.Survival,
                CharacterNarrativeDomain.Work,
                CharacterNarrativeDomain.Invasion,
                CharacterNarrativeDomain.Expedition),
            Conflicts("risk-posture"),
            Multiply(
                GameplayEffectTargetIds.AccidentChance,
                0.94f,
                "Assets/Resources/SO/V26/Effects/Definitions/effect_character_accident-chance_multiply.asset",
                "work:dangerous",
                "Assets/Resources/SO/V26/Effects/Conditions/work_dangerous.asset")),
        Module(
            "acquired-trait:module:ritual-attunement",
            "의례적 공명",
            "반복한 관찰과 의례가 연구의 집중을 높여 연구 속도가 조금 빨라집니다.",
            Domains(CharacterNarrativeDomain.FacilityUse, CharacterNarrativeDomain.Work),
            Conflicts(),
            Multiply(
                GameplayEffectTargetIds.ResearchSpeed,
                1.04f,
                "Assets/Resources/SO/V26/Effects/Definitions/effect_character_research-speed_multiply.asset"))
    };

    private static readonly IReadOnlyDictionary<string,
        CharacterAcquiredTraitSpecialReactionDefinition[]> ReactionsByModule =
        new Dictionary<string, CharacterAcquiredTraitSpecialReactionDefinition[]>(
            StringComparer.Ordinal)
        {
            ["acquired-trait:module:battle-temper"] = new[]
            {
                Reaction("expedition-composure",
                    CharacterAcquiredTraitReactionTrigger.ExpeditionOutcome,
                    CharacterAcquiredTraitReactionOwnerRole.Participant,
                    CharacterAcquiredTraitReactionCondition.ExpeditionSucceeded,
                    CharacterAcquiredTraitReactionAction.ApplyMoodImpulse,
                    2f, 2, 2, 1, 0, "원정에서 얻은 자신감")
            },
            ["acquired-trait:module:steady-hands"] = new[]
            {
                Reaction("finished-work-insight",
                    CharacterAcquiredTraitReactionTrigger.WorkCompleted,
                    CharacterAcquiredTraitReactionOwnerRole.Subject,
                    CharacterAcquiredTraitReactionCondition.ProductCreated,
                    CharacterAcquiredTraitReactionAction.GrantExperience,
                    3f, 0, 1, 1, 0, "완성품에서 얻은 깨달음")
            },
            ["acquired-trait:module:scar-memory"] = new[]
            {
                Reaction("accident-composure",
                    CharacterAcquiredTraitReactionTrigger.CharacterInjured,
                    CharacterAcquiredTraitReactionOwnerRole.Subject,
                    CharacterAcquiredTraitReactionCondition.WorkAccident,
                    CharacterAcquiredTraitReactionAction.ApplyMoodImpulse,
                    1.5f, 1, 2, 1, 0, "사고 뒤의 침착")
            },
            ["acquired-trait:module:social-bridge"] = new[]
            {
                Reaction("accepted-restitution",
                    CharacterAcquiredTraitReactionTrigger.ApologyCompleted,
                    CharacterAcquiredTraitReactionOwnerRole.Recipient,
                    CharacterAcquiredTraitReactionCondition.RestitutionProvided,
                    CharacterAcquiredTraitReactionAction.ApplyMoodImpulse,
                    2f, 2, 2, 1, 0, "성의 있는 화해")
            },
            ["acquired-trait:module:survival-instinct"] = new[]
            {
                Reaction("learn-from-accident",
                    CharacterAcquiredTraitReactionTrigger.CharacterInjured,
                    CharacterAcquiredTraitReactionOwnerRole.Subject,
                    CharacterAcquiredTraitReactionCondition.WorkAccident,
                    CharacterAcquiredTraitReactionAction.GrantExperience,
                    2f, 0, 2, 1, 0, "사고에서 얻은 교훈")
            }
        };

    [MenuItem("DungeonStory/Content/V25/Build Acquired Trait Content")]
    public static void Build()
    {
        GameDomainContentCatalogSO domainCatalog = RequireAsset<GameDomainContentCatalogSO>(
            DomainCatalogPath);
        ItemDefinitionCatalogSO itemCatalog = RequireAsset<ItemDefinitionCatalogSO>(
            ItemCatalogPath);
        RequireAsset<GameContentCatalogSO>(RootCatalogPath);
        RequireNoDuplicateManagedCatalogDefinitions(domainCatalog.Definitions);

        EnsureFolders();
        CharacterAcquiredTraitSettingsSO settings = GetOrCreate<
            CharacterAcquiredTraitSettingsSO>(SettingsPath);
        ConfigureSettings(settings);

        CharacterAcquiredTraitModuleSO[] modules = ModuleSpecs
            .Select(spec =>
            {
                CharacterAcquiredTraitModuleSO module = GetOrCreate<
                    CharacterAcquiredTraitModuleSO>(spec.AssetPath);
                ConfigureModule(module, spec);
                return module;
            })
            .ToArray();

        foreach (EffectSpec effect in ModuleSpecs.SelectMany(spec => spec.Effects)
                     .GroupBy(spec => spec.EffectPath, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            EnsureAcquiredTraitSource(effect);
        }

        GenericItemDefinitionSO seal = GetOrCreate<GenericItemDefinitionSO>(SealItemPath);
        ConfigureSeal(seal);

        ReplaceAcquiredTraitDefinitionSlice(domainCatalog, settings, modules);
        ReplaceSealItemCatalogEntry(itemCatalog, seal);
        ValidateAuthoring();

        EditorUtility.SetDirty(domainCatalog);
        EditorUtility.SetDirty(itemCatalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"V25_ACQUIRED_TRAIT_CONTENT=PASS; gates={GateSpecs.Length}; "
            + $"modules={ModuleSpecs.Length}; item={SealItemId}; unitMassGrams={SealUnitMassGrams}");
    }

    [MenuItem("DungeonStory/Content/V25/Validate Acquired Trait Content")]
    public static void ValidateAuthoring()
    {
        List<string> errors = new();
        ValidateDefinitions(errors);
        ValidateCatalogMembership(errors);
        ValidateSealTradeExposure(errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "V25 acquired-trait content validation failed:\n"
                + string.Join("\n", errors.Distinct(StringComparer.Ordinal)));
        }

        Debug.Log(
            $"V25_ACQUIRED_TRAIT_CONTENT=VALID; gates={GateSpecs.Length}; "
            + $"modules={ModuleSpecs.Length}; item={SealItemId}; duplicateIds=0; "
            + "tradeExposure=0");
    }

    /// <summary>
    /// Shared, side-effect-free authority for the approved V25 authored policy.
    /// The builder and catalog exporter must reject the same policy drift before
    /// either can treat the slice as usable.
    /// </summary>
    internal static IReadOnlyList<string> ValidateApprovedAuthoringPolicy(
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        ItemDefinitionSO seal)
    {
        List<string> errors = new();
        ValidateApprovedSettings(settings, errors);
        ValidateApprovedModules(moduleDefinitions, errors);
        ValidateApprovedSeal(seal, errors);
        return errors;
    }

    /// <summary>
    /// Scans persisted authored assets for every direct way that a reward-only
    /// seal could be exposed to recipes, shops, procurement, or production.
    /// Item-catalog membership is the one legitimate authored reference.
    /// </summary>
    internal static IReadOnlyList<string> ValidateRewardOnlySealTradeExposure(
        GenericItemDefinitionSO seal,
        ItemDefinitionCatalogSO itemCatalog)
    {
        List<string> errors = new();
        if (seal == null)
        {
            errors.Add("Memory-erasure seal asset is missing for trade-exposure validation.");
            return errors;
        }
        if (itemCatalog == null)
        {
            errors.Add("Memory-erasure seal trade-exposure validation requires the item catalog.");
            return errors;
        }

        string sealPath = AssetDatabase.GetAssetPath(seal);
        string itemCatalogPath = AssetDatabase.GetAssetPath(itemCatalog);
        if (string.IsNullOrWhiteSpace(sealPath)
            || string.IsNullOrWhiteSpace(itemCatalogPath))
        {
            errors.Add("Memory-erasure seal and item catalog must both be persisted assets.");
            return errors;
        }

        HashSet<string> reported = new(StringComparer.Ordinal);
        foreach (string path in AssetDatabase.FindAssets(
                     "t:ScriptableObject",
                     new[] { "Assets" })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            if (string.Equals(path, sealPath, StringComparison.Ordinal)
                || string.Equals(path, itemCatalogPath, StringComparison.Ordinal))
            {
                continue;
            }

            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null)
            {
                continue;
            }

            if (AssetDatabase.GetDependencies(path, false)
                .Any(value => string.Equals(value, sealPath, StringComparison.Ordinal)))
            {
                ReportTradeExposure(
                    errors,
                    reported,
                    path,
                    "direct asset dependency");
            }

            SerializedObject serialized = new(asset);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType == SerializedPropertyType.ObjectReference
                    && property.objectReferenceValue == seal)
                {
                    ReportTradeExposure(
                        errors,
                        reported,
                        path,
                        "serialized object reference at '" + property.propertyPath + "'");
                }
                else if (property.propertyType == SerializedPropertyType.String
                    && string.Equals(
                        property.stringValue,
                        SealItemId,
                        StringComparison.Ordinal))
                {
                    ReportTradeExposure(
                        errors,
                        reported,
                        path,
                        "serialized item ID at '" + property.propertyPath + "'");
                }
            }
        }

        return errors;
    }

    private static void ConfigureSettings(CharacterAcquiredTraitSettingsSO settings)
    {
        SerializedObject serialized = new(settings);
        RequireProperty(serialized, "settingsId").stringValue = SettingsId;
        RequireProperty(serialized, "maximumActiveTraits").intValue = 3;
        SerializedProperty gates = RequireProperty(serialized, "manifestationGates");
        gates.arraySize = GateSpecs.Length;
        for (int index = 0; index < GateSpecs.Length; index++)
        {
            GateSpec spec = GateSpecs[index];
            SerializedProperty gate = gates.GetArrayElementAtIndex(index);
            RequireRelativeProperty(gate, "meaningfulRecordMilestone").intValue =
                spec.Milestone;
            RequireRelativeProperty(gate, "rarity").enumValueIndex = (int)spec.Rarity;
            RequireRelativeProperty(gate, "budget").intValue = spec.Budget;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    private static void ConfigureModule(
        CharacterAcquiredTraitModuleSO module,
        ModuleSpec spec)
    {
        SerializedObject serialized = new(module);
        RequireProperty(serialized, "moduleId").stringValue = spec.Id;
        RequireProperty(serialized, "displayName").stringValue = spec.DisplayName;
        RequireProperty(serialized, "description").stringValue = spec.Description;
        RequireProperty(serialized, "cost").intValue = 2;
        SetStringArray(RequireProperty(serialized, "conflictGroups"), spec.ConflictGroups);
        SetEnumArray(RequireProperty(serialized, "domainAffinities"), spec.Domains);

        SerializedProperty effects = RequireProperty(serialized, "effects");
        effects.arraySize = spec.Effects.Length;
        for (int index = 0; index < spec.Effects.Length; index++)
        {
            EffectSpec effectSpec = spec.Effects[index];
            SerializedProperty binding = effects.GetArrayElementAtIndex(index);
            RequireRelativeProperty(binding, "bindingId").stringValue =
                spec.Id + ":effect:" + index;
            RequireRelativeProperty(binding, "definition").objectReferenceValue =
                ResolveEffect(effectSpec);
            RequireRelativeProperty(binding, "value").floatValue = effectSpec.Value;
            RequireRelativeProperty(binding, "condition").objectReferenceValue =
                ResolveCondition(effectSpec);
        }
        SetStringArray(RequireProperty(serialized, "drawbackBindingIds"),
            spec.Effects.Select((value, index) => (value, index))
                .Where(value => value.value.IsDrawback)
                .Select(value => spec.Id + ":effect:" + value.index).ToArray());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        module.ApplySpecialReactionAuthoring(
            ReactionsByModule.TryGetValue(spec.Id, out var reactions)
                ? reactions
                : Array.Empty<CharacterAcquiredTraitSpecialReactionDefinition>());
        EditorUtility.SetDirty(module);
    }

    private static void ConfigureSeal(GenericItemDefinitionSO seal)
    {
        seal.ConfigureCore(
            SealItemId,
            "망각의 인장",
            "후천 특성 하나를 지우는 지역 보스 최초 처치 보상입니다.",
            StockCategory.General,
            price: 0,
            weight: SealUnitMassGrams / 1000f,
            stackLimit: SealStackLimit);

        SerializedObject serialized = new(seal);
        RequireProperty(serialized, "features").arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(seal);
    }

    private static void ReplaceAcquiredTraitDefinitionSlice(
        GameDomainContentCatalogSO domainCatalog,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> modules)
    {
        // GameDomainContentCatalogSO.SetDefinitions sorts every entry. Keep the
        // current author-owned order and replace only this V25 ID slice in place.
        RequireNoDuplicateManagedCatalogDefinitions(domainCatalog.Definitions);
        ScriptableObject[] beforeUnrelated = domainCatalog.Definitions
            .Where(value => !IsManagedV25Definition(value))
            .ToArray();
        ScriptableObject[] replacement = new ScriptableObject[] { settings }
            .Concat(modules.Cast<ScriptableObject>())
            .ToArray();
        ScriptableObject[] next = ReplaceSlice(
            domainCatalog.Definitions,
            IsManagedV25Definition,
            replacement);
        RequireUnrelatedEntriesPreserved(
            beforeUnrelated,
            next.Where(value => !IsManagedV25Definition(value)),
            DomainCatalogPath);
        SetObjectReferenceArray(domainCatalog, "definitions", next);
        RequireUnrelatedEntriesPreserved(
            beforeUnrelated,
            domainCatalog.Definitions.Where(value => !IsManagedV25Definition(value)),
            DomainCatalogPath);
        EditorUtility.SetDirty(domainCatalog);
    }

    private static void ReplaceSealItemCatalogEntry(
        ItemDefinitionCatalogSO itemCatalog,
        GenericItemDefinitionSO seal)
    {
        // ItemDefinitionCatalogSO.SetDefinitions and its broad reindex sort and
        // rebuild all definitions. This builder owns one entry only.
        ItemDefinitionSO[] beforeUnrelated = itemCatalog.Definitions
            .Where(value => value == null
                || !string.Equals(value.ItemId, SealItemId, StringComparison.Ordinal))
            .ToArray();
        ItemDefinitionSO[] next = ReplaceSlice(
            itemCatalog.Definitions,
            value => value != null
                && string.Equals(value.ItemId, SealItemId, StringComparison.Ordinal),
            new ItemDefinitionSO[] { seal });
        RequireUnrelatedEntriesPreserved(
            beforeUnrelated,
            next.Where(value => value == null
                || !string.Equals(value.ItemId, SealItemId, StringComparison.Ordinal)),
            ItemCatalogPath);
        SetObjectReferenceArray(itemCatalog, "definitions", next);
        RequireUnrelatedEntriesPreserved(
            beforeUnrelated,
            itemCatalog.Definitions.Where(value => value == null
                || !string.Equals(value.ItemId, SealItemId, StringComparison.Ordinal)),
            ItemCatalogPath);
        EditorUtility.SetDirty(itemCatalog);
    }

    private static bool IsExpectedV25ModuleId(string moduleId) =>
        ModuleSpecs.Any(spec => string.Equals(
            spec.Id,
            moduleId,
            StringComparison.Ordinal));

    private static bool IsManagedV25Definition(ScriptableObject value) => value switch
    {
        CharacterAcquiredTraitSettingsSO settings => string.Equals(
            settings.SettingsId,
            SettingsId,
            StringComparison.Ordinal),
        CharacterAcquiredTraitModuleSO module => IsExpectedV25ModuleId(module.ModuleId),
        _ => false
    };

    private static void RequireNoDuplicateManagedCatalogDefinitions(
        IEnumerable<ScriptableObject> definitions)
    {
        string[] duplicates = (definitions ?? Array.Empty<ScriptableObject>())
            .Where(IsManagedV25Definition)
            .Select(value => value is CharacterAcquiredTraitSettingsSO
                ? SettingsId
                : ((CharacterAcquiredTraitModuleSO)value).ModuleId)
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                "V25 acquired-trait catalog slice contains duplicate managed IDs: "
                + string.Join(", ", duplicates));
        }
    }

    private static void ReportTradeExposure(
        ICollection<string> errors,
        ISet<string> reported,
        string path,
        string detail)
    {
        string key = path + "|" + detail;
        if (!reported.Add(key))
        {
            return;
        }

        errors.Add(
            $"Memory-erasure seal is exposed by '{path}' through {detail}; reward-only "
            + "items cannot be referenced by recipes, shops, procurement, or production content.");
    }

    private static void EnsureAcquiredTraitSource(EffectSpec spec)
    {
        GameplayEffectDefinitionSO effect = ResolveEffect(spec);
        if (!string.Equals(effect.TargetId, spec.TargetId, StringComparison.Ordinal)
            || effect.Operation != spec.Operation)
        {
            throw new InvalidOperationException(
                $"Reused effect '{effect.EffectId}' does not match acquired-trait target "
                + $"'{spec.TargetId}' and operation '{spec.Operation}'.");
        }

        EffectSemanticSnapshot before = new(effect);
        GameplayEffectSourceKind expectedSources = before.AllowedSources
            | GameplayEffectSourceKind.AcquiredTrait;
        if (before.AllowedSources == expectedSources)
        {
            return;
        }

        effect.Configure(
            before.NumericId,
            before.EffectId,
            before.TargetId,
            before.Operation,
            before.ProjectionPhase,
            expectedSources,
            before.StackingPolicy,
            before.MinimumResult,
            before.MaximumResult);
        RequireOnlyAcquiredTraitSourceMaskChanged(
            before,
            new EffectSemanticSnapshot(effect),
            spec.EffectPath);
        EditorUtility.SetDirty(effect);
    }

    private static T[] ReplaceSlice<T>(
        IEnumerable<T> source,
        Func<T, bool> isManaged,
        IEnumerable<T> replacement)
    {
        List<T> result = new();
        bool inserted = false;
        foreach (T value in source ?? Array.Empty<T>())
        {
            if (isManaged(value))
            {
                if (!inserted)
                {
                    result.AddRange(replacement ?? Array.Empty<T>());
                    inserted = true;
                }
                continue;
            }
            result.Add(value);
        }

        if (!inserted)
        {
            result.AddRange(replacement ?? Array.Empty<T>());
        }
        return result.ToArray();
    }

    private static void SetObjectReferenceArray(
        UnityEngine.Object owner,
        string propertyPath,
        IEnumerable<UnityEngine.Object> values)
    {
        UnityEngine.Object[] references = (values ?? Array.Empty<UnityEngine.Object>())
            .ToArray();
        SerializedObject serialized = new(owner);
        SerializedProperty entries = RequireProperty(serialized, propertyPath);
        entries.arraySize = references.Length;
        for (int index = 0; index < references.Length; index++)
        {
            entries.GetArrayElementAtIndex(index).objectReferenceValue = references[index];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RequireUnrelatedEntriesPreserved<T>(
        IEnumerable<T> before,
        IEnumerable<T> after,
        string catalogPath)
        where T : UnityEngine.Object
    {
        T[] beforeEntries = (before ?? Array.Empty<T>()).ToArray();
        T[] afterEntries = (after ?? Array.Empty<T>()).ToArray();
        if (beforeEntries.Length != afterEntries.Length
            || !beforeEntries.SequenceEqual(afterEntries))
        {
            throw new InvalidOperationException(
                $"Surgical V25 catalog update changed unrelated references or their order "
                + $"in '{catalogPath}'.");
        }
    }

    private static void RequireOnlyAcquiredTraitSourceMaskChanged(
        EffectSemanticSnapshot before,
        EffectSemanticSnapshot after,
        string effectPath)
    {
        GameplayEffectSourceKind expectedSources = before.AllowedSources
            | GameplayEffectSourceKind.AcquiredTrait;
        if (!string.Equals(before.AssetName, after.AssetName, StringComparison.Ordinal)
            || before.NumericId != after.NumericId
            || !string.Equals(before.EffectId, after.EffectId, StringComparison.Ordinal)
            || !string.Equals(before.TargetId, after.TargetId, StringComparison.Ordinal)
            || before.Operation != after.Operation
            || before.ProjectionPhase != after.ProjectionPhase
            || before.StackingPolicy != after.StackingPolicy
            || before.MinimumResult != after.MinimumResult
            || before.MaximumResult != after.MaximumResult
            || after.AllowedSources != expectedSources)
        {
            throw new InvalidOperationException(
                $"V25 acquired-trait source-mask update changed non-mask fields of "
                + $"reused effect '{effectPath}'.");
        }
    }

    private static GameplayEffectDefinitionSO ResolveEffect(EffectSpec spec)
    {
        GameplayEffectDefinitionSO effect = RequireAsset<GameplayEffectDefinitionSO>(
            spec.EffectPath);
        string expectedId = "effect:" + spec.TargetId + ":"
            + spec.Operation.ToString().ToLowerInvariant();
        if (!string.Equals(effect.EffectId, expectedId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Effect asset '{spec.EffectPath}' has id '{effect.EffectId}', expected "
                + $"'{expectedId}'.");
        }
        return effect;
    }

    private static GameplayEffectConditionDefinitionSO ResolveCondition(EffectSpec spec)
    {
        if (string.IsNullOrEmpty(spec.ConditionId))
        {
            return null;
        }

        GameplayEffectConditionDefinitionSO condition =
            RequireAsset<GameplayEffectConditionDefinitionSO>(spec.ConditionPath);
        if (!string.Equals(condition.ConditionId, spec.ConditionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Condition asset '{spec.ConditionPath}' has id '{condition.ConditionId}', "
                + $"expected '{spec.ConditionId}'.");
        }
        return condition;
    }

    private static void ValidateApprovedSettings(
        CharacterAcquiredTraitSettingsSO settings,
        ICollection<string> errors)
    {
        if (settings == null)
        {
            errors.Add($"Missing acquired-trait settings '{SettingsId}'.");
            return;
        }

        AddErrors(errors, settings.ValidateDefinition());
        if (!string.Equals(settings.SettingsId, SettingsId, StringComparison.Ordinal)
            || settings.MaximumActiveTraits != 3)
        {
            errors.Add(
                "Acquired-trait settings must use the approved stable ID and maximumActiveTraits=3.");
        }
        if (settings.ManifestationGates.Count != GateSpecs.Length)
        {
            errors.Add(
                $"Acquired-trait gate count is {settings.ManifestationGates.Count}, "
                + $"expected {GateSpecs.Length}.");
            return;
        }

        for (int index = 0; index < GateSpecs.Length; index++)
        {
            CharacterAcquiredTraitManifestationGateDefinition gate =
                settings.ManifestationGates[index];
            GateSpec spec = GateSpecs[index];
            if (gate == null
                || gate.MeaningfulRecordMilestone != spec.Milestone
                || gate.Rarity != spec.Rarity
                || gate.Budget != spec.Budget)
            {
                errors.Add(
                    $"Acquired-trait gate {index} must be milestone={spec.Milestone}, "
                    + $"rarity={spec.Rarity}, budget={spec.Budget}.");
            }
        }
    }

    private static void ValidateApprovedModules(
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        ICollection<string> errors)
    {
        CharacterAcquiredTraitModuleSO[] modules = (moduleDefinitions
                ?? Array.Empty<CharacterAcquiredTraitModuleSO>())
            .ToArray();
        if (modules.Length != ModuleSpecs.Length)
        {
            errors.Add(
                $"Acquired-trait module count is {modules.Length}, expected {ModuleSpecs.Length}.");
        }
        if (modules.Any(value => value == null))
        {
            errors.Add("Acquired-trait module definitions contain a null entry.");
        }

        CharacterAcquiredTraitModuleSO[] concrete = modules
            .Where(value => value != null)
            .ToArray();
        foreach (CharacterAcquiredTraitModuleSO unexpected in concrete.Where(value =>
                     !IsExpectedV25ModuleId(value.ModuleId)))
        {
            errors.Add(
                $"Acquired-trait module '{unexpected.ModuleId}' is outside the approved V25 policy.");
        }

        foreach (ModuleSpec spec in ModuleSpecs)
        {
            CharacterAcquiredTraitModuleSO[] matches = concrete.Where(value =>
                string.Equals(value.ModuleId, spec.Id, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
            {
                errors.Add(
                    $"Acquired-trait module '{spec.Id}' expected once, found {matches.Length}.");
                continue;
            }
            ValidateApprovedModule(matches[0], spec, errors);
        }
    }

    private static void ValidateApprovedModule(
        CharacterAcquiredTraitModuleSO module,
        ModuleSpec spec,
        ICollection<string> errors)
    {
        AddErrors(errors, module.ValidateDefinition());
        if (!string.Equals(module.DisplayName, spec.DisplayName, StringComparison.Ordinal)
            || !string.Equals(module.Description, spec.Description, StringComparison.Ordinal))
        {
            errors.Add($"Acquired-trait module '{spec.Id}' has wrong presentation text.");
        }
        if (module.Cost != 2)
            errors.Add($"Acquired-trait module '{spec.Id}' must cost 2.");
        if (!module.DomainAffinities.SequenceEqual(spec.Domains))
            errors.Add($"Acquired-trait module '{spec.Id}' has wrong domain affinities.");
        if (!module.ConflictGroups.SequenceEqual(spec.ConflictGroups))
            errors.Add($"Acquired-trait module '{spec.Id}' has wrong conflict groups.");
        CharacterAcquiredTraitSpecialReactionDefinition[] expectedReactions =
            ReactionsByModule.TryGetValue(spec.Id, out var reactions)
                ? reactions
                : Array.Empty<CharacterAcquiredTraitSpecialReactionDefinition>();
        if (!ReactionSignatures(module.SpecialReactions).SequenceEqual(
                ReactionSignatures(expectedReactions), StringComparer.Ordinal))
            errors.Add($"Acquired-trait module '{spec.Id}' has wrong special reactions.");
        if (module.Effects.Count != spec.Effects.Length)
        {
            errors.Add($"Acquired-trait module '{spec.Id}' has wrong effect count.");
            return;
        }

        GameplayEffectSourceRef source = new(
            GameplayEffectSourceKind.AcquiredTrait,
            spec.Id);
        for (int index = 0; index < spec.Effects.Length; index++)
        {
            GameplayEffectBinding binding = module.Effects[index];
            EffectSpec effectSpec = spec.Effects[index];
            string expectedBindingId = spec.Id + ":effect:" + index;
            string expectedEffectId = "effect:" + effectSpec.TargetId + ":"
                + effectSpec.Operation.ToString().ToLowerInvariant();
            if (binding == null
                || !string.Equals(binding.bindingId, expectedBindingId, StringComparison.Ordinal)
                || binding.definition == null
                || !binding.IsValidFor(source, out _)
                || !string.Equals(
                    binding.definition.EffectId,
                    expectedEffectId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    binding.definition.TargetId,
                    effectSpec.TargetId,
                    StringComparison.Ordinal)
                || binding.definition.Operation != effectSpec.Operation
                || (binding.definition.AllowedSources
                    & GameplayEffectSourceKind.AcquiredTrait) == 0
                || !Mathf.Approximately(binding.value, effectSpec.Value)
                || (string.IsNullOrEmpty(effectSpec.ConditionId)
                    ? binding.condition != null
                    : binding.condition == null
                        || !string.Equals(
                            binding.condition.ConditionId,
                            effectSpec.ConditionId,
                            StringComparison.Ordinal)))
            {
                errors.Add(
                    $"Acquired-trait module '{spec.Id}' effect {index} has wrong values.");
            }
        }
    }

    private static CharacterAcquiredTraitSpecialReactionDefinition Reaction(
        string suffix,
        CharacterAcquiredTraitReactionTrigger trigger,
        CharacterAcquiredTraitReactionOwnerRole ownerRole,
        CharacterAcquiredTraitReactionCondition condition,
        CharacterAcquiredTraitReactionAction action,
        float baseValue,
        int durationDays,
        int cooldownDays,
        int maximumTriggersPerDay,
        int maximumLifetimeTriggers,
        string displayLabel) => new(
            "acquired-trait:reaction:" + suffix,
            trigger,
            ownerRole,
            condition,
            action,
            baseValue,
            durationDays,
            cooldownDays,
            maximumTriggersPerDay,
            maximumLifetimeTriggers,
            displayLabel);

    private static IEnumerable<string> ReactionSignatures(
        IEnumerable<CharacterAcquiredTraitSpecialReactionDefinition> source) =>
        (source ?? Array.Empty<CharacterAcquiredTraitSpecialReactionDefinition>())
        .Where(value => value != null)
        .OrderBy(value => value.ReactionId, StringComparer.Ordinal)
        .Select(value => string.Join("|",
            value.ReactionId,
            value.Trigger,
            value.OwnerRole,
            value.Condition,
            value.Action,
            value.BaseValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            value.DurationDays,
            value.CooldownDays,
            value.MaximumTriggersPerDay,
            value.MaximumLifetimeTriggers,
            value.DisplayLabel));

    private static void ValidateApprovedSeal(
        ItemDefinitionSO seal,
        ICollection<string> errors)
    {
        if (seal is not GenericItemDefinitionSO genericSeal)
        {
            errors.Add(
                $"Memory-erasure seal '{SealItemPath}' must be a {nameof(GenericItemDefinitionSO)}.");
            return;
        }

        AddErrors(errors, genericSeal.ValidateDefinition());
        if (!string.Equals(genericSeal.ItemId, SealItemId, StringComparison.Ordinal)
            || !string.Equals(genericSeal.DisplayName, "망각의 인장", StringComparison.Ordinal)
            || genericSeal.StockCategory != StockCategory.General
            || genericSeal.UnitPrice != 0
            || !Mathf.Approximately(
                genericSeal.UnitWeight,
                SealUnitMassGrams / 1000f)
            || genericSeal.MaxStack != SealStackLimit)
        {
            errors.Add(
                "Memory-erasure seal must be the authored General 100g stackable zero-price item.");
        }
        if (genericSeal.Features.Count != 0
            || genericSeal.TryGetFeature(out ProductionItemFeature _)
            || genericSeal.TryGetFeature(out MarketItemFeature _))
        {
            errors.Add(
                "Memory-erasure seal must have no production or market capability.");
        }
    }

    private static void ValidateDefinitions(ICollection<string> errors)
    {
        CharacterAcquiredTraitSettingsSO[] settings = FindAssets<
            CharacterAcquiredTraitSettingsSO>("Assets/Resources/SO")
            .Where(value => string.Equals(
                value.SettingsId,
                SettingsId,
                StringComparison.Ordinal))
            .ToArray();
        if (settings.Length != 1)
        {
            errors.Add(
                $"Acquired-trait settings '{SettingsId}' expected once, found {settings.Length}.");
        }

        CharacterAcquiredTraitModuleSO[] modules = FindAssets<
            CharacterAcquiredTraitModuleSO>("Assets/Resources/SO")
            .Where(value => IsExpectedV25ModuleId(value.ModuleId))
            .ToArray();
        ItemDefinitionSO seal = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(SealItemPath);
        AddErrors(errors, ValidateApprovedAuthoringPolicy(
            settings.Length == 1 ? settings[0] : null,
            modules,
            seal));
    }

    private static void ValidateCatalogMembership(ICollection<string> errors)
    {
        GameDomainContentCatalogSO domainCatalog = AssetDatabase.LoadAssetAtPath<
            GameDomainContentCatalogSO>(DomainCatalogPath);
        ItemDefinitionCatalogSO itemCatalog = AssetDatabase.LoadAssetAtPath<
            ItemDefinitionCatalogSO>(ItemCatalogPath);
        GameContentCatalogSO rootCatalog = AssetDatabase.LoadAssetAtPath<
            GameContentCatalogSO>(RootCatalogPath);
        if (domainCatalog == null || itemCatalog == null || rootCatalog == null)
        {
            errors.Add("V25 acquired-trait content requires all current root catalogs.");
            return;
        }

        if (domainCatalog.Definitions.OfType<CharacterAcquiredTraitSettingsSO>()
            .Count(value => string.Equals(
                value.SettingsId,
                SettingsId,
                StringComparison.Ordinal)) != 1)
        {
            errors.Add("Domain catalog must contain exactly one V25 acquired-trait settings asset.");
        }
        if (domainCatalog.Definitions.OfType<CharacterAcquiredTraitModuleSO>()
            .Count(value => IsExpectedV25ModuleId(value.ModuleId)) != ModuleSpecs.Length)
        {
            errors.Add(
                $"Domain catalog must contain exactly {ModuleSpecs.Length} V25 acquired-trait modules.");
        }
        foreach (ModuleSpec spec in ModuleSpecs)
        {
            int count = domainCatalog.Definitions.OfType<CharacterAcquiredTraitModuleSO>()
                .Count(value => string.Equals(value.ModuleId, spec.Id, StringComparison.Ordinal));
            if (count != 1)
            {
                errors.Add(
                    $"Domain catalog module '{spec.Id}' expected once, found {count}.");
            }
        }

        int itemCount = itemCatalog.Definitions.Count(value => value != null
            && string.Equals(value.ItemId, SealItemId, StringComparison.Ordinal));
        if (itemCount != 1)
        {
            errors.Add(
                $"Item catalog seal '{SealItemId}' expected once, found {itemCount}.");
        }
        if (rootCatalog.GetItemDefinitions<ItemDefinitionCatalogSO>() != itemCatalog)
        {
            errors.Add("Root game content catalog does not reference the current item catalog.");
        }
    }

    private static void ValidateSealTradeExposure(ICollection<string> errors)
    {
        GenericItemDefinitionSO seal = AssetDatabase.LoadAssetAtPath<
            GenericItemDefinitionSO>(SealItemPath);
        ItemDefinitionCatalogSO itemCatalog = AssetDatabase.LoadAssetAtPath<
            ItemDefinitionCatalogSO>(ItemCatalogPath);
        if (seal != null && itemCatalog != null)
        {
            AddErrors(errors, ValidateRewardOnlySealTradeExposure(seal, itemCatalog));
        }
    }

    private static void AddErrors(
        ICollection<string> destination,
        IEnumerable<string> source)
    {
        foreach (string value in source ?? Array.Empty<string>())
        {
            destination.Add(value);
        }
    }

    private static T[] FindAssets<T>(string root) where T : ScriptableObject =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .Distinct()
            .OrderBy(value => AssetDatabase.GetAssetPath(value), StringComparer.Ordinal)
            .ToArray();

    private static T RequireAsset<T>(string path) where T : ScriptableObject =>
        AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException(
            $"Required {typeof(T).Name} asset is missing at '{path}'.");

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing == null)
        {
            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
        if (existing is T typed)
        {
            return typed;
        }
        throw new InvalidOperationException(
            $"Asset '{path}' has type '{existing.GetType().Name}', expected '{typeof(T).Name}'.");
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyPath) => serialized.FindProperty(propertyPath)
        ?? throw new InvalidOperationException(
            $"Serialized property '{propertyPath}' is missing on "
            + $"'{serialized.targetObject.GetType().Name}'.");

    private static SerializedProperty RequireRelativeProperty(
        SerializedProperty serialized,
        string propertyPath) => serialized.FindPropertyRelative(propertyPath)
        ?? throw new InvalidOperationException(
            $"Serialized property '{propertyPath}' is missing below "
            + $"'{serialized.propertyPath}'.");

    private static void SetStringArray(SerializedProperty property, string[] values)
    {
        property.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            property.GetArrayElementAtIndex(index).stringValue = values[index];
        }
    }

    private static void SetEnumArray(
        SerializedProperty property,
        CharacterNarrativeDomain[] values)
    {
        property.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            property.GetArrayElementAtIndex(index).enumValueIndex = (int)values[index];
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Resources/SO", "V25");
        EnsureFolder("Assets/Resources/SO/V25", "AcquiredTraits");
        EnsureFolder("Assets/Resources/SO/V25/AcquiredTraits", "Modules");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static ModuleSpec Module(
        string id,
        string displayName,
        string description,
        CharacterNarrativeDomain[] domains,
        string[] conflicts,
        params EffectSpec[] effects) => new()
    {
        Id = id,
        DisplayName = displayName,
        Description = description,
        Domains = domains,
        ConflictGroups = conflicts,
        Effects = effects
    };

    private static CharacterNarrativeDomain[] Domains(
        params CharacterNarrativeDomain[] values) => values;

    private static string[] Conflicts(params string[] values) => values;

    private static EffectSpec Multiply(
        string targetId,
        float value,
        string effectPath,
        string conditionId = null,
        string conditionPath = null) => new()
    {
        TargetId = targetId,
        Operation = GameplayEffectOperation.Multiply,
        Value = value,
        EffectPath = effectPath,
        ConditionId = conditionId,
        ConditionPath = conditionPath
    };

    private static EffectSpec Drawback(EffectSpec value)
    {
        value.IsDrawback = true;
        return value;
    }

    private static EffectSpec AddFlat(
        string targetId,
        float value,
        string effectPath) => new()
    {
        TargetId = targetId,
        Operation = GameplayEffectOperation.AddFlat,
        Value = value,
        EffectPath = effectPath
    };
}
#endif
