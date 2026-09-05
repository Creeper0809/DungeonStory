#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class V26FounderTraitAuditScenario
{
    private sealed class EffectSource : IGameplayEffectSource
    {
        public EffectSource(
            GameplayEffectSourceKind kind,
            string id,
            params GameplayEffectBinding[] effects)
        {
            SourceRef = new GameplayEffectSourceRef(kind, id);
            Effects = effects;
        }
        public GameplayEffectSourceRef SourceRef { get; }
        public IReadOnlyList<GameplayEffectBinding> Effects { get; }
    }

    private sealed class AuditGameClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time { get; set; }
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class EmptyEquipmentEffects :
        ICharacterEquipmentGameplayEffectSourceQuery
    {
        public IReadOnlyList<IGameplayEffectSource> GetEquipmentSources(
            CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
    }

    private sealed class EmptyTransientEffects :
        ICharacterTransientGameplayEffectSourceQuery
    {
        public IReadOnlyList<IGameplayEffectSource> GetStatusSources(
            CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
        public IReadOnlyList<IGameplayEffectSource> GetCompletedResearchSources(
            CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
    }

    private sealed class CompletionAuditWorld :
        ICharacterWorldQuery,
        ICharacterLifetimeQuery
    {
        public CharacterActor[] Active = Array.Empty<CharacterActor>();
        public CharacterActor[] Lifetime = Array.Empty<CharacterActor>();
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => Active;
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> AllCharacters => Lifetime;
    }

    private sealed class CompletionAuditEnvironment :
        ICharacterEnvironmentStatusQuery
    {
        public bool ThrowOnExposure;
        public CharacterEnvironmentExposure GetExposure(CharacterId characterId)
        {
            if (ThrowOnExposure)
                throw new InvalidOperationException(
                    "Injected completion environment failure.");
            return new CharacterEnvironmentExposure
            {
                characterId = characterId.Value
            };
        }
        public EnvironmentalExposureBand GetPhysiologicalBand(
            CharacterId characterId) => EnvironmentalExposureBand.Stable;
        public EnvironmentalExposureBand GetVisualBand(
            CharacterId characterId) => EnvironmentalExposureBand.Stable;
        public float GetWorkSpeedMultiplier(CharacterId characterId) => 1f;
        public float GetPrecisionWorkSpeedMultiplier(CharacterId characterId) => 1f;
        public float GetMoveSpeedMultiplier(CharacterId characterId) => 1f;
        public float GetAccuracyPenaltyPoints(CharacterId characterId) => 0f;
    }

    private const string CatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string ReportPath =
        "Artifacts/QA/v26-founder-trait-mythic-audit.txt";

    [MenuItem("DungeonStory/V26/Audit Founder Traits and Mythic")]
    public static void Run()
    {
        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        HashSet<int> retained = new(new[]
        {
            101,102,103,104,105,106,107,108,109,
            200,201,202,203,204,205,206,207,208,209,210,211,212,213,214,
            215,216,217,218,219,220,221,222,223,224,225,226,227,228,229,230,
            235,239,245
        });
        CharacterTraitSO[] founder = catalog.Definitions.OfType<CharacterTraitSO>()
            .Where(trait => trait != null && (retained.Contains(trait.id)
                || trait.id is >= 247 and <= 259
                || trait.id is >= 300 and <= 306
                || trait.id is >= 400 and <= 417
                || trait.id is >= 500 and <= 518))
            .OrderBy(trait => trait.id)
            .ToArray();
        int[] retired = { 231,232,233,234,236,237,238,240,241,242,243,244,246 };
        Require(founder.Length == 100, $"Founder count={founder.Length}.");
        CharacterTraitSO diligent = founder.Single(value => value.id == 409);
        CombatEquipmentDefinitionSO poweredHarness = AssetDatabase
            .LoadAssetAtPath<CombatEquipmentDefinitionSO>(
                "Assets/Resources/SO/Combat/Equipment/A15_PoweredHarness.asset")
            ?? throw new InvalidOperationException(
                "Powered harness shared-effect slice is missing.");
        Require(diligent.Effects.Count == 1
            && poweredHarness.Effects.Count == 1
            && ReferenceEquals(
                diligent.Effects[0].definition,
                poweredHarness.Effects[0].definition)
            && diligent.Effects[0].definition.TargetId
                == GameplayEffectTargetIds.WorkSpeed,
            "Trait and equipment do not share the work-speed effect definition.");
        Require(founder.GroupBy(value => value.id).All(group => group.Count() == 1),
            "Duplicate founder trait id.");
        Require(founder.All(value => !retired.Contains(value.id)),
            "Retired founder trait remains selectable.");
        CharacterTraitSO quickHealer = founder.Single(value => value.id == 207);
        string[] diseaseTargets =
        {
            GameplayEffectTargetIds.DiseaseResistance,
            GameplayEffectTargetIds.DiseaseRecoverySpeed,
            GameplayEffectTargetIds.ImmunityGain,
            GameplayEffectTargetIds.ImmunityRetention
        };
        Require(diseaseTargets.All(target => quickHealer.Effects.Any(binding =>
                binding?.definition != null
                && string.Equals(binding.definition.TargetId, target, StringComparison.Ordinal)
                && Mathf.Approximately(binding.value, 1.10f))),
            "Quick healer does not author all four independent disease detailed stats.");
        CharacterTraitSO[] authoredIdentityTraits = founder
            .Where(value => retained.Contains(value.id) || value.id is >= 300 and <= 306)
            .ToArray();
        Require(authoredIdentityTraits.All(value => value.identityRules != null
                && value.identityRules.Count > 0),
            "A retained or extreme founder trait has no identity rule.");
        Require(founder.All(value => (value.identityRules ?? new List<CharacterIdentityRule>())
                .Where(rule => rule != null)
                .GroupBy(rule => rule.ruleId, StringComparer.Ordinal)
                .All(group => group.Count() == 1)),
            "A founder trait contains duplicate identity rule ids.");
        string[] identityErrors = founder
            .SelectMany(value => (value.identityRules ?? new List<CharacterIdentityRule>())
                .Where(rule => rule != null)
                .SelectMany(rule => rule.Validate())
                .Select(error => $"trait:{value.id}:{error}"))
            .ToArray();
        Require(identityErrors.Length == 0,
            $"Identity validation failed: {string.Join(" | ", identityErrors)}");
        ExtremeCraftInspirationRule inspirationRule = founder
            .Single(value => value.id == MythicCraftInspirationRules.SourceTraitId)
            .identityRules
            .OfType<ExtremeCraftInspirationRule>()
            .Single();
        Require(Mathf.Approximately(inspirationRule.mythicChance, .03f)
                && Mathf.Approximately(
                    inspirationRule.minimumContributionShare,
                    .60f)
                && !MythicCraftInspirationRules.IsMythic(0UL, 0f)
                && MythicCraftInspirationRules.IsMythic(
                    MythicCraftInspirationRules.RollScale - 1UL,
                    1f),
            "Mythic chance or maker contribution authority is not authored by the identity rule.");

        GameplayEffectDefinitionSO sharedWork = ScriptableObject
            .CreateInstance<GameplayEffectDefinitionSO>();
        sharedWork.Configure(
            1470001,
            "effect:audit:work-speed",
            GameplayEffectTargetIds.WorkSpeed,
            GameplayEffectOperation.Multiply,
            GameplayEffectProjectionPhase.Multiplicative,
            GameplayEffectSourceKind.Trait | GameplayEffectSourceKind.Equipment,
            GameplayEffectStackingPolicy.StackAll,
            .1f,
            10f);
        GameplayEffectBinding traitBinding = new()
        {
            bindingId = "audit:trait:work",
            definition = sharedWork,
            value = 1.05f
        };
        GameplayEffectBinding equipmentBinding = new()
        {
            bindingId = "audit:equipment:work",
            definition = sharedWork,
            value = 1.10f
        };
        GameplayEffectProjectionResult sharedProjection =
            CharacterGameplayEffectProjector.Resolve(
                GameplayEffectTargetIds.WorkSpeed,
                1f,
                new IGameplayEffectSource[]
                {
                    new EffectSource(
                        GameplayEffectSourceKind.Trait,
                        "trait:audit",
                        traitBinding,
                        traitBinding),
                    new EffectSource(
                        GameplayEffectSourceKind.Equipment,
                        "equipment:audit",
                        equipmentBinding)
                });
        Require(Mathf.Abs(sharedProjection.Value - 1.155f) < .0001f,
            $"Shared effect stacking produced {sharedProjection.Value:F6}.");
        Require(sharedProjection.Contributions.Count(value => value.Suppressed) == 1
            && sharedProjection.Contributions.Any(value =>
                value.SuppressionReason == "duplicate source binding"),
            "Duplicate source binding was not explicitly suppressed.");
        UnityEngine.Object.DestroyImmediate(sharedWork);

        CharacterTraitSO inspirationTrait = founder.Single(value => value.id == 300);
        CharacterIdentityStateStore identityStore = new();
        identityStore.Set(
            "character:audit",
            inspirationTrait.DefinitionId.Value,
            ExtremeCraftInspirationRuntime.RuleId,
            1,
            JsonUtility.ToJson(new ExtremeCraftInspirationRuntimeState
            {
                lastProductDefinitionId = "equipment:audit",
                consecutiveEligibleCompletions = 4,
                lastCompletionElapsedSeconds = 120f
            }));
        identityStore.Set(
            "character:audit+segment",
            inspirationTrait.DefinitionId.Value,
            ExtremeCraftInspirationRuntime.RuleId,
            1,
            JsonUtility.ToJson(new ExtremeCraftInspirationRuntimeState
            {
                lastProductDefinitionId = "equipment:plus-key",
                consecutiveEligibleCompletions = 2,
                lastCompletionElapsedSeconds = 240f
            }));
        Require(
            !string.Equals(
                CharacterIdentityStateStore.BuildKey("a+b", "c", "d"),
                CharacterIdentityStateStore.BuildKey("a", "b+c", "d"),
                StringComparison.Ordinal),
            "Identity state key escaping permits '+' collisions.");
        IReadOnlyList<CharacterIdentityRuntimeStateSaveData> identitySaved =
            identityStore.Capture();
        CharacterIdentityStateStore restoredIdentity = new();
        restoredIdentity.Restore(identitySaved, founder);
        Require(restoredIdentity.Capture().Count == 2
            && restoredIdentity.TryGet(
                "character:audit+segment",
                inspirationTrait.DefinitionId.Value,
                ExtremeCraftInspirationRuntime.RuleId,
                out CharacterIdentityRuleStateSaveData plusState)
            && plusState.statePayload.Contains("equipment:plus-key"),
            "Identity state round-trip failed.");
        CharacterIdentityRuntimeStateSaveData duplicateIdentity = identitySaved
            .First(value => value.characterId == "character:audit")
            .Clone();
        duplicateIdentity.rules.Add(duplicateIdentity.rules[0].Clone());
        bool duplicateRejected = false;
        try
        {
            restoredIdentity.Restore(new[] { duplicateIdentity }, founder);
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        Require(duplicateRejected, "Duplicate identity rule state was partially restored.");
        CharacterIdentityRuntimeStateSaveData wrongRevision = identitySaved
            .First(value => value.characterId == "character:audit")
            .Clone();
        wrongRevision.rules[0].revision = 2;
        bool revisionRejected = false;
        try
        {
            restoredIdentity.Restore(new[] { wrongRevision }, founder);
        }
        catch (InvalidOperationException)
        {
            revisionRejected = true;
        }
        Require(revisionRejected, "Unknown identity rule revision was partially restored.");
        VerifyExtremeTraitRuntime();
        VerifyCharacterNarrativeExternalRestoreTransaction();

        const int million = 1_000_000;
        ulong stableA = MythicCraftInspirationRules.ResolveFixedRollHash(
            991UL, "pipeline:stable", "equipment:stable", 7, "character:stable");
        ulong stableB = MythicCraftInspirationRules.ResolveFixedRollHash(
            991UL, "pipeline:stable", "equipment:stable", 7, "character:stable");
        Require(stableA == stableB, "Mythic fixed roll is not stable.");
        int mythicCount = 0;
        for (int index = 0; index < million; index++)
        {
            ulong hash = MythicCraftInspirationRules.ResolveFixedRollHash(
                24701UL,
                "pipeline:audit",
                "equipment:audit",
                index,
                "character:audit");
            if (MythicCraftInspirationRules.IsMythic(
                    hash,
                    inspirationRule.mythicChance))
                mythicCount++;
        }
        float mythicRate = mythicCount / (float)million;
        Require(mythicRate is >= .029f and <= .031f,
            $"Mythic rate={mythicRate:P4}.");

        DeterministicCraftQualityResolver quality = new();
        int normalMythic = 0;
        for (int index = 0; index < million; index++)
        {
            CraftQualityRollSaveData roll = quality.Roll(
                861UL, "quality:audit", "item:audit", index);
            if (quality.Resolve(roll, 100f, 50f, 50f, 0f).Tier
                == CraftsmanshipQualityTier.Mythic)
            {
                normalMythic++;
            }
        }
        Require(normalMythic == 0,
            $"Normal quality resolver emitted {normalMythic} Mythic results.");

        ApparelInstanceState mythicState = new()
        {
            apparelDefinitionId = "apparel:audit",
            primaryMaterialId = "material:audit",
            craftsmanshipQuality = CraftsmanshipQualityTier.Mythic,
            sourceKind = TextileSourceKind.Crop,
            sourceDefinitionId = "material:audit",
            size = ApparelSizeClass.Medium,
            durability = 100f,
            craftedAbsoluteDay = 12,
            deterministicBatchHash = 17UL,
            mythicProvenance = new MythicProvenanceSaveData
            {
                makerCharacterId = "character:audit",
                sourceTraitId = 300,
                originalQuality = CraftsmanshipQualityTier.Legendary,
                fixedRollHash = stableA,
                createdDay = 12,
                createdFacilityId = "facility:audit"
            }
        };
        ItemInstanceComponentSaveData encoded = ApparelItemStateCodec.Create(mythicState);
        Require(
            ApparelItemStateCodec.TryRead(
                new[] { encoded }, out ApparelInstanceState decoded)
            && decoded.craftsmanshipQuality == CraftsmanshipQualityTier.Mythic
            && decoded.mythicProvenance?.fixedRollHash == stableA,
            "Mythic apparel provenance round-trip failed.");
        ApparelInstanceState invalid = new()
        {
            apparelDefinitionId = "apparel:invalid",
            primaryMaterialId = "material:audit",
            craftsmanshipQuality = CraftsmanshipQualityTier.Mythic,
            sourceKind = TextileSourceKind.Crop,
            size = ApparelSizeClass.Medium
        };
        Require(!ApparelItemStateCodec.TryRead(
                new[] { ApparelItemStateCodec.Create(invalid) }, out _),
            "Mythic apparel without provenance was accepted.");

        const int rerolls = 100_000;
        DeterministicRandomSequence random = new(147026);
        string[] speciesTags = founder
            .SelectMany(value => value.eligibleSpeciesTags ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Prepend((string)null)
            .ToArray();
        HashSet<int> reached = new();
        long slots = 0;
        long negativeSlots = 0;
        long extremeSlots = 0;
        int multiExtreme = 0;
        for (int index = 0; index < rerolls; index++)
        {
            string species = speciesTags[index % speciesTags.Length];
            IReadOnlyList<int> ids = CharacterTraitSelectionRules.Select(
                founder,
                Array.Empty<CharacterTraitConflictRule>(),
                random,
                species,
                maximumCount: 4);
            CharacterTraitSO[] selected = ids
                .Select(id => founder.First(value => value.id == id))
                .ToArray();
            slots += selected.Length;
            negativeSlots += selected.Count(value =>
                value.polarity == CharacterTraitPolarity.Negative);
            int extremes = selected.Count(value =>
                value.polarity == CharacterTraitPolarity.Extreme);
            extremeSlots += extremes;
            if (extremes >= 2) multiExtreme++;
            Require(selected
                    .Where(value => !string.IsNullOrWhiteSpace(value.selectionFamilyId))
                    .GroupBy(value => value.selectionFamilyId)
                    .All(group => group.Count() == 1),
                "Selection-family collision.");
            foreach (CharacterTraitSO trait in selected) reached.Add(trait.id);
        }
        float average = slots / (float)rerolls;
        float negativeRate = negativeSlots / (float)slots;
        float extremeRate = extremeSlots / (float)slots;
        float multiExtremeRate = multiExtreme / (float)rerolls;
        Require(average is >= 2.38f and <= 2.42f, $"Average count={average:F4}.");
        Require(negativeRate is >= .25f and <= .31f,
            $"Negative rate={negativeRate:P4}.");
        Require(extremeRate is >= .008f and <= .012f,
            $"Extreme rate={extremeRate:P4}.");
        Require(multiExtremeRate <= .001f,
            $"Multi-extreme rate={multiExtremeRate:P4}.");
        int[] unreachable = founder.Where(value => !reached.Contains(value.id))
            .Select(value => value.id).ToArray();
        Require(unreachable.Length == 0,
            $"Unreachable founder ids={string.Join(",", unreachable)}.");

        string report =
            "V26 founder trait and Mythic deterministic audit\n"
            + $"founder_traits={founder.Length}\n"
            + $"rerolls={rerolls}\n"
            + $"average_trait_count={average:F6}\n"
            + $"negative_slot_rate={negativeRate:F6}\n"
            + $"extreme_slot_rate={extremeRate:F6}\n"
            + $"multi_extreme_character_rate={multiExtremeRate:F6}\n"
            + $"mythic_trials={million}\n"
            + $"mythic_rate={mythicRate:F6}\n"
            + $"normal_quality_mythic={normalMythic}\n"
            + $"shared_effect_value={sharedProjection.Value:F6}\n"
            + "shared_effect_duplicate_suppression=pass\n"
            + "identity_state_round_trip=pass\n"
            + "identity_duplicate_rejection=pass\n"
            + "extreme_trait_runtime=pass\n"
            + "direct_order_cost_preview_and_apply=pass\n"
            + "apparel_mythic_provenance_round_trip=pass\n"
            + "status=PASS\n";
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Artifacts/QA");
        File.WriteAllText(ReportPath, report);
        AssetDatabase.Refresh();
        Debug.Log(
            $"PHASE147_AUDIT=PASS founder={founder.Length} rerolls={rerolls} "
            + $"avg={average:F4} negative={negativeRate:P3} extreme={extremeRate:P3} "
            + $"multiExtreme={multiExtremeRate:P4} mythic={mythicRate:P3} "
            + $"normalMythic={normalMythic}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void VerifyExtremeTraitRuntime()
    {
        const ulong naturalGoldenHash = 2920600955397026131UL;
        ulong capturedNaturalGoldenHash =
            GoldenHarvestDeterministicOutcomeAuthority.CaptureRollHash(
                21UL,
                "building:qa:golden-witness",
                0,
                "character:qa:golden-witness");
        Require(capturedNaturalGoldenHash == naturalGoldenHash
                && Mathf.Abs(
                    GoldenHarvestDeterministicOutcomeAuthority.CaptureRoll01(
                        capturedNaturalGoldenHash)
                    - 0.026131f) < 0.000001f,
            "Golden Harvest natural deterministic witness drifted from the "
            + "production roll authority.");

        GameObject host = new("V26 Extreme Trait Runtime Audit");
        try
        {
            CharacterActor actor = host.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(host);
            actor.EnsureRuntimeState();
            actor.Initialize(AssetDatabase.FindAssets("t:CharacterSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterSO>)
                .First(value => value != null
                    && value.id > 0
                    && value.species != null));
            actor.Identity.SetPersistentId("character:v26:extreme:audit");
            CharacterIdentityStateStore store = new();
            ExtremeTraitRuntime runtime = new(store);
            AuditGameClock clock = new();

            actor.Progression.ApplyPreparedIdentity(
                "V26 Effect Audit",
                "audit",
                Array.Empty<int>(),
                CharacterPotentialGrade.Ordinary,
                147,
                autoChooseDrafts: false);
            ProficiencyWorkProfile learningProfile = new(
                BuiltInCharacterProficiencyIds.Crafting);
            float baselineLearning = CharacterProficiencyLearningRules.Resolve(
                actor,
                learningProfile);
            ApplyTrait(actor, 230);
            float projectedLearning = actor.ProjectDetailedStat(
                GameplayEffectTargetIds.EarnedWorkExperience,
                1f).Value;
            float acceleratedLearning = CharacterProficiencyLearningRules.Resolve(
                actor,
                learningProfile);
            Require(Mathf.Abs(projectedLearning - 1.30f) < .0001f
                    && Mathf.Abs(
                        acceleratedLearning / baselineLearning - 1.30f) < .0001f,
                $"Earned work XP did not consume the shared derived-stat projection exactly once. "
                + $"baseline={baselineLearning:F4} projected={projectedLearning:F4} "
                + $"accelerated={acceleratedLearning:F4} ratio={acceleratedLearning / baselineLearning:F4}");
            CharacterDerivedStatsSnapshotProjector snapshotProjector = new(
                CharacterAiEditorTestDependencies.ContentDefinitions,
                new EmptyEquipmentEffects(),
                new EmptyTransientEffects(),
                runtime,
                clock);
            Dictionary<string, float> learningBase = new(StringComparer.Ordinal)
            {
                [GameplayEffectTargetIds.EarnedWorkExperience] = 1f
            };
            CharacterDerivedStatsSnapshot cachedA = snapshotProjector.Project(
                actor,
                learningBase);
            CharacterDerivedStatsSnapshot cachedB = snapshotProjector.Project(
                actor,
                learningBase);
            Require(ReferenceEquals(cachedA, cachedB)
                    && Mathf.Abs(cachedA.Get(
                        GameplayEffectTargetIds.EarnedWorkExperience) - 1.30f) < .0001f,
                "Derived-stat cache did not reuse an identical authority revision.");
            ApplyTrait(actor, 416);
            CharacterDerivedStatsSnapshot invalidated = snapshotProjector.Project(
                actor,
                learningBase);
            Require(!ReferenceEquals(cachedA, invalidated)
                    && !string.Equals(
                        cachedA.RevisionKey,
                        invalidated.RevisionKey,
                        StringComparison.Ordinal)
                    && Mathf.Abs(invalidated.Get(
                        GameplayEffectTargetIds.EarnedWorkExperience) - 1.10f) < .0001f,
                "Derived-stat cache was not invalidated by trait authority revision input.");

            ApplyTrait(actor, 301);
            Require(!runtime.TryActivateLastStand(
                    actor, "encounter:audit", .50f, false, 10f, out _),
                "Last stand activated above its threshold.");
            Require(runtime.TryActivateLastStand(
                    actor, "encounter:audit", .19f, false, 10f,
                    out LastStandRule lastStand)
                && lastStand != null,
                "Last stand did not activate at its threshold.");
            Require(!runtime.TryActivateLastStand(
                    actor, "encounter:audit", .10f, true, 11f, out _),
                "Last stand activated twice in one encounter.");
            runtime.EndLastStand(actor, "encounter:audit", 20f);
            Require(runtime.GetActiveConditionIds(actor, 21f)
                    .Contains("state:last-stand-aftermath"),
                "Last stand aftermath was not projected.");

            ApplyTrait(actor, 302);
            Require(runtime.TryResolveForbiddenResearchLeap(
                    actor, "research:audit", 147UL, 30f, out _)
                && !runtime.TryResolveForbiddenResearchLeap(
                    actor, "research:audit", 147UL, 31f, out _),
                "Forbidden research leap was not once-per-project.");

            ApplyTrait(actor, 303);
            Require(!runtime.TryResolveMiracleSurgery(
                    actor, "surgery:safe", false, 147UL, 40f, out _)
                && runtime.TryResolveMiracleSurgery(
                    actor, "surgery:critical", true, 147UL, 40f, out _)
                && !runtime.TryResolveMiracleSurgery(
                    actor, "surgery:critical", true, 147UL, 41f, out _),
                "Miracle surgery eligibility or once-per-order guard failed.");

            ApplyTrait(actor, 304);
            const string goldenFieldId = "field:audit";
            const string goldenOperationId =
                "crop-harvest:field:audit:000003";
            float goldenResolveAt = 50f + GameCalendarRules.SecondsPerDay;
            GoldenHarvestPreparedResolution preparedGolden = default;
            Require(runtime.TryScheduleGoldenHarvest(
                    actor,
                    goldenFieldId,
                    3,
                    50f)
                && !runtime.TryPrepareGoldenHarvest(
                    actor,
                    goldenFieldId,
                    goldenOperationId,
                    147UL,
                    50f,
                    out _)
                && runtime.TryPrepareGoldenHarvest(
                    actor,
                    goldenFieldId,
                    goldenOperationId,
                    147UL,
                    goldenResolveAt,
                    out preparedGolden)
                && preparedGolden.Resolution.PrimaryMultiplier > 0f
                && !preparedGolden.Committed,
                "Golden harvest delay or preparation failed.");
            Require(runtime.TryPrepareGoldenHarvest(
                    actor,
                    goldenFieldId,
                    goldenOperationId,
                    147UL,
                    goldenResolveAt,
                    out GoldenHarvestPreparedResolution prepareReplay),
                "Golden harvest preparation was not idempotent.");
            RequireGoldenHarvestPreparedExact(
                preparedGolden,
                prepareReplay,
                expectedCommitted: false,
                context: "Golden harvest prepare replay");

            IReadOnlyList<CharacterIdentityRuntimeStateSaveData>
                goldenCapturedState = store.Capture();
            store.Restore(
                goldenCapturedState,
                CharacterAiEditorTestDependencies.ContentDefinitions
                    .GetAll<CharacterTraitSO>());
            IReadOnlyList<GoldenHarvestPreparedResolution> restoredCensus =
                runtime.CapturePreparedGoldenHarvests();
            Require(restoredCensus.Count == 1,
                $"Golden harvest restored census count={restoredCensus.Count}.");
            RequireGoldenHarvestPreparedExact(
                preparedGolden,
                restoredCensus[0],
                expectedCommitted: false,
                context: "Golden harvest restored census");
            int removedOnDeath = store.RemoveCharacter(
                preparedGolden.CharacterId,
                new ICharacterIdentityDeathStateRetentionPolicy[]
                {
                    runtime
                });
            IReadOnlyList<GoldenHarvestPreparedResolution> deathRetainedCensus =
                runtime.CapturePreparedGoldenHarvests();
            Require(removedOnDeath >= 0 && deathRetainedCensus.Count == 1,
                "Character death removed an unresolved Golden Harvest owner.");
            RequireGoldenHarvestPreparedExact(
                preparedGolden,
                deathRetainedCensus[0],
                expectedCommitted: false,
                context: "Golden harvest death retention");

            Require(runtime.TryCommitPreparedGoldenHarvest(
                    preparedGolden.CharacterId,
                    preparedGolden.TraitDefinitionId,
                    goldenOperationId,
                    out GoldenHarvestPreparedResolution committedGolden),
                "Golden harvest prepared result did not commit.");
            Require(runtime.TryCommitPreparedGoldenHarvest(
                    preparedGolden.CharacterId,
                    preparedGolden.TraitDefinitionId,
                    goldenOperationId,
                    out GoldenHarvestPreparedResolution commitReplay),
                "Golden harvest commit replay was not idempotent.");
            RequireGoldenHarvestPreparedExact(
                committedGolden,
                commitReplay,
                expectedCommitted: true,
                context: "Golden harvest commit replay");
            IReadOnlyList<GoldenHarvestPreparedResolution> committedCensus =
                runtime.CapturePreparedGoldenHarvests();
            Require(committedCensus.Count == 1,
                $"Golden harvest committed census count={committedCensus.Count}.");
            RequireGoldenHarvestPreparedExact(
                committedGolden,
                committedCensus[0],
                expectedCommitted: true,
                context: "Golden harvest committed census");
            Require(runtime.TryAcknowledgePreparedGoldenHarvest(
                    preparedGolden.CharacterId,
                    preparedGolden.TraitDefinitionId,
                    goldenOperationId)
                && runtime.CapturePreparedGoldenHarvests().Count == 0
                && !store.TryGet(
                    preparedGolden.CharacterId,
                    preparedGolden.TraitDefinitionId,
                    ExtremeTraitRuntime.GoldenHarvestRuleId,
                    out _),
                "Golden harvest acknowledgement did not clear the census.");

            ApplyTrait(actor, 305);
            Require(runtime.TryBeginProductionLimitBreak(
                    actor, "batch:audit", 50f, out ProductionLimitBreakRule limitBreak)
                && limitBreak != null
                && !runtime.TryBeginProductionLimitBreak(
                    actor, "batch:other", 50f, out _),
                "Production limit break activation guard failed.");
            runtime.EndProductionLimitBreak(actor, "batch:audit", 60f);
            Require(runtime.GetActiveConditionIds(actor, 61f)
                    .Contains("state:production-limit-break-aftermath"),
                "Production limit break aftermath was not projected.");

            ApplyTrait(actor, 306);
            Require(!runtime.TryActivateArcaneOvercharge(
                    actor, "arcane:high-mana", .50f, 70f, out _)
                && runtime.TryActivateArcaneOvercharge(
                    actor, "arcane:low-mana", .20f, 70f,
                    out ArcaneOverchargeActivation overcharge)
                && Mathf.Approximately(overcharge.SelfDamageFraction, .15f)
                && !runtime.TryActivateArcaneOvercharge(
                    actor, "arcane:repeat", .10f, 71f, out _),
                "Arcane overcharge threshold or cooldown guard failed.");

            ApplyTrait(actor, 101);
            CharacterPersistentNeedRuntime needs = new(store, clock);
            Require(Mathf.Approximately(
                    needs.ResolveMoodDelta(actor, "food:meal-missed"),
                    -5f)
                && Mathf.Approximately(
                    needs.ResolveMoodDelta(actor, "food:meal-missed"),
                    0f)
                && Mathf.Approximately(
                    needs.ResolveMoodDelta(actor, "food:sated"),
                    3f),
                "Persistent need state did not gate deprivation or satisfaction mood.");

            ApplyTrait(actor, 245);
            CharacterIdentityRuleRouter router = new();
            CharacterMoodPolicyService moodPolicy = new(router, needs);
            CharacterDirectOrderCostPreviewService directOrders = new(
                router,
                moodPolicy);
            CharacterDirectOrderCostPreview preview = directOrders.Preview(
                actor,
                "order:defer-cleaning");
            Require(preview.HasCost
                    && Mathf.Approximately(preview.MoodDelta, -2f)
                    && Mathf.Approximately(preview.StressDelta, 3f)
                    && StaffWorkPriorityIdentityOrderPolicy.IsDeferredCleaning(
                        BuiltInWorkTypeIds.Clean,
                        WorkPriorityLevel.Priority1,
                        WorkPriorityLevel.Priority2)
                    && !StaffWorkPriorityIdentityOrderPolicy.IsDeferredCleaning(
                        BuiltInWorkTypeIds.Clean,
                        WorkPriorityLevel.Off,
                        WorkPriorityLevel.Priority1)
                    && !StaffWorkPriorityIdentityOrderPolicy.IsDeferredCleaning(
                        BuiltInWorkTypeIds.Research,
                        WorkPriorityLevel.Priority1,
                        WorkPriorityLevel.Priority2),
                "Cleaning compulsion direct-order cost preview or UI trigger policy failed.");
            CharacterDirectOrderCostPreview applied = directOrders.Apply(
                actor,
                "order:defer-cleaning");
            Require(Mathf.Approximately(applied.MoodDelta, -2f)
                    && Mathf.Approximately(actor.Lifecycle.ExpeditionRecovery.stress, 3f)
                    && actor.Mood.Factors.Any(factor =>
                        string.Equals(
                            factor.Id,
                            "identity:post-action:order:defer-cleaning",
                            StringComparison.Ordinal)
                        && Mathf.Approximately(factor.Value, -2f)),
                "Cleaning compulsion direct order did not apply mood and stress costs.");

            VerifyWorkCompletionDeliveryTransaction(
                actor,
                store,
                moodPolicy,
                directOrders,
                needs);

            IReadOnlyList<CharacterIdentityRuntimeStateSaveData> captured = store.Capture();
            Require(captured.Count == 1
                    && store.TryGet(
                        actor.Identity.PersistentId,
                        "character-trait:217",
                        "mood:small-success",
                        out CharacterIdentityRuleStateSaveData smallSuccess)
                    && !string.IsNullOrWhiteSpace(smallSuccess.statePayload)
                    && store.TryGet(
                        actor.Identity.PersistentId,
                        "character-trait:230",
                        "mood:first-process-success",
                        out CharacterIdentityRuleStateSaveData firstProcess)
                    && !string.IsNullOrWhiteSpace(firstProcess.statePayload),
                "Extreme runtime state was not captured under one character authority. "
                + $"characters={captured.Count}, rules="
                + string.Join(",", captured.Select(value =>
                    $"{value.characterId}:{value.rules.Count}")));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void ApplyTrait(CharacterActor actor, int traitId) =>
        actor.Progression.ApplyPreparedIdentity(
            "V26 Extreme Audit",
            "audit",
            new[] { traitId },
            CharacterPotentialGrade.Ordinary,
            traitId,
            autoChooseDrafts: false);

    private static void VerifyWorkCompletionDeliveryTransaction(
        CharacterActor actor,
        CharacterIdentityStateStore store,
        CharacterMoodPolicyService moodPolicy,
        CharacterDirectOrderCostPreviewService directOrders,
        CharacterPersistentNeedRuntime needs)
    {
        actor.Progression.ApplyPreparedIdentity(
            "V27 Completion Delivery Audit",
            "audit",
            new[] { 217, 230 },
            CharacterPotentialGrade.Ordinary,
            2701,
            autoChooseDrafts: false);
        CompletionAuditWorld world = new()
        {
            Active = new[] { actor },
            Lifetime = new[] { actor }
        };
        CompletionAuditEnvironment environment = new()
        {
            ThrowOnExposure = true
        };
        WorkCompletionIdentityDeliveryLedger ledger = new();
        WorkIdentityEventAdapter adapter = new(
            new GameEventBus(),
            world,
            moodPolicy,
            directOrders,
            store,
            environment,
            needs,
            ledger,
            world);
        WorkCompletionIdentityDeliveryRequest request = new(
            "identity-event:crop-harvest:audit:000000",
            "crop-harvest:building:crop-plot:audit",
            0,
            actor.Identity.TypedPersistentId,
            BuiltInWorkTypeIds.Harvest.Value,
            "building:crop-plot:audit:normal",
            CharacterCommandOrigin.Autonomous,
            3);
        string before = CaptureCompletionState(actor, store);
        bool faulted = false;
        try
        {
            adapter.EnsureApplied(request);
        }
        catch (InvalidOperationException error) when (
            error.Message.Contains(
                "Injected completion environment failure",
                StringComparison.Ordinal))
        {
            faulted = true;
        }
        Require(faulted
                && ledger.Capture().Count == 0
                && string.Equals(
                    before,
                    CaptureCompletionState(actor, store),
                    StringComparison.Ordinal),
            "Faulted work-completion delivery did not roll back exactly.");

        environment.ThrowOnExposure = false;
        WorkCompletionIdentityDeliveryResult applied =
            adapter.EnsureApplied(request);
        string afterApplied = CaptureCompletionState(actor, store);
        WorkCompletionIdentityDeliveryResult replay =
            adapter.EnsureApplied(request);
        Require(applied.Status == WorkCompletionIdentityDeliveryStatus.Applied
                && replay.Status ==
                    WorkCompletionIdentityDeliveryStatus.AlreadyApplied
                && ledger.Capture().Count == 1
                && string.Equals(
                    afterApplied,
                    CaptureCompletionState(actor, store),
                    StringComparison.Ordinal),
            "Work-completion delivery replay changed applied identity state.");

        WorkCompletionIdentityDeliveryLedger transientLedger = new();
        world.Active = Array.Empty<CharacterActor>();
        WorkIdentityEventAdapter transientAdapter = new(
            new GameEventBus(),
            world,
            moodPolicy,
            directOrders,
            store,
            environment,
            needs,
            transientLedger,
            world);
        WorkCompletionIdentityDeliveryRequest transientRequest = new(
            "identity-event:crop-harvest:transient:000000",
            "crop-harvest:building:crop-plot:transient",
            0,
            actor.Identity.TypedPersistentId,
            BuiltInWorkTypeIds.Harvest.Value,
            "building:crop-plot:transient:normal",
            CharacterCommandOrigin.Autonomous,
            3);
        Require(transientAdapter.EnsureApplied(transientRequest).Status ==
                WorkCompletionIdentityDeliveryStatus.Deferred
            && transientLedger.Capture().Count == 0,
            "Transient completion recipient absence was not deferred.");
        world.Active = new[] { actor };
        Require(transientAdapter.EnsureApplied(transientRequest).IsApplied,
            "Returned completion recipient did not resume exactly once.");

        WorkCompletionIdentityDeliveryLedger terminalLedger = new();
        world.Active = Array.Empty<CharacterActor>();
        world.Lifetime = Array.Empty<CharacterActor>();
        WorkIdentityEventAdapter terminalAdapter = new(
            new GameEventBus(),
            world,
            moodPolicy,
            directOrders,
            store,
            environment,
            needs,
            terminalLedger,
            world);
        WorkCompletionIdentityDeliveryRequest terminalRequest = new(
            "identity-event:crop-harvest:terminal:000000",
            "crop-harvest:building:crop-plot:terminal",
            0,
            actor.Identity.TypedPersistentId,
            BuiltInWorkTypeIds.Harvest.Value,
            "building:crop-plot:terminal:normal",
            CharacterCommandOrigin.Autonomous,
            3);
        Require(terminalAdapter.EnsureApplied(terminalRequest).IsApplied
                && terminalLedger.Capture().Single().disposition ==
                    WorkCompletionIdentityDeliveryDisposition
                        .TerminalRecipientUnavailable,
            "Terminal completion recipient absence did not retire durably.");

        WorkCompletionIdentityDeliveryRequest unsupported = new(
            "identity-event:unsupported:000000",
            "unsupported:completion:audit",
            0,
            actor.Identity.TypedPersistentId,
            BuiltInWorkTypeIds.Harvest.Value,
            "unsupported:product",
            CharacterCommandOrigin.DirectPlayerOrder,
            3);
        Require(terminalAdapter.EnsureApplied(unsupported).Status ==
                WorkCompletionIdentityDeliveryStatus.Conflict,
            "Unsupported durable work-completion scope was accepted.");
    }

    private static void VerifyCharacterNarrativeExternalRestoreTransaction()
    {
        IGameContentDefinitionSource content =
            CharacterAiEditorTestDependencies.ContentDefinitions;
        CharacterIdentityStateStore identityStore = new();
        identityStore.Set(
            "character:v27:narrative-restore:audit",
            "character-trait:217",
            "mood:small-success",
            1,
            "{\"lastSmallSuccessDay\":2}");
        WorkCompletionIdentityDeliveryLedger ledger = new();
        ledger.Restore(new[]
        {
            new WorkCompletionIdentityDeliveryCursorSaveData
            {
                producerStreamId =
                    "crop-harvest:building:crop-plot:narrative-previous",
                operationSequence = 0,
                deliveryId =
                    "identity-event:crop-harvest:narrative-previous:000000",
                payloadFingerprint = new string('b', 64),
                disposition = WorkCompletionIdentityDeliveryDisposition
                    .EffectsApplied
            }
        });
        CharacterNarrativeRuntime runtime = new(
            new DungeonRuntimeAggregateRootStore(),
            new CharacterNarrativeCatalog(content),
            identityStore,
            content,
            ledger);
        CharacterNarrativeAggregateState candidate = runtime.PrepareRestore(
            new CharacterNarrativeWorldSaveData
            {
                characters = new List<CharacterNarrativeSaveData>(),
                identityStates =
                    new List<CharacterIdentityRuntimeStateSaveData>(),
                workCompletionDeliveries = new List<
                    WorkCompletionIdentityDeliveryCursorSaveData>
                {
                    new()
                    {
                        producerStreamId =
                            "crop-harvest:building:crop-plot:narrative-candidate",
                        operationSequence = 0,
                        deliveryId =
                            "identity-event:crop-harvest:narrative-candidate:000000",
                        payloadFingerprint = new string('a', 64),
                        disposition = WorkCompletionIdentityDeliveryDisposition
                            .EffectsApplied
                    }
                }
            });

        runtime.BeginRestoreCandidate();
        runtime.PublishRestore(candidate);
        Require(identityStore.Capture().Count == 1
                && ledger.Capture().Single().producerStreamId.EndsWith(
                    "narrative-previous",
                    StringComparison.Ordinal),
            "Narrative section staging mutated external live state.");
        runtime.PublishRestoreCandidate();
        Require(identityStore.Capture().Count == 0
                && runtime.Version == 2
                && ledger.Capture().Single().producerStreamId.EndsWith(
                    "narrative-candidate",
                    StringComparison.Ordinal),
            "Narrative external candidate was not published atomically.");
        runtime.RollbackPublishedRestoreCandidate();
        Require(identityStore.Capture().Count == 1
                && runtime.Version == 1
                && ledger.Capture().Single().producerStreamId.EndsWith(
                    "narrative-previous",
                    StringComparison.Ordinal),
            "Narrative external publication did not roll back exactly.");

        runtime.BeginRestoreCandidate();
        runtime.PublishRestore(candidate);
        runtime.PublishRestoreCandidate();
        runtime.CompleteRestoreCandidate();
        Require(identityStore.Capture().Count == 0
                && runtime.Version == 2
                && ledger.Capture().Single().producerStreamId.EndsWith(
                    "narrative-candidate",
                    StringComparison.Ordinal),
            "Narrative external publication did not survive completion.");
    }

    private static string CaptureCompletionState(
        CharacterActor actor,
        CharacterIdentityStateStore store)
    {
        CharacterMoodSnapshot mood = actor.Mood;
        string moodToken = string.Join(";", mood.Factors
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .Select(value => string.Join(
                ",",
                value.Id,
                value.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                value.RemainingSeconds.ToString(
                    "R",
                    System.Globalization.CultureInfo.InvariantCulture))));
        string identityToken = string.Join(";", store.Capture()
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .SelectMany(value => value.rules
                .OrderBy(rule => rule.traitDefinitionId, StringComparer.Ordinal)
                .ThenBy(rule => rule.ruleId, StringComparer.Ordinal)
                .Select(rule => string.Join(
                    ",",
                    value.characterId,
                    rule.traitDefinitionId,
                    rule.ruleId,
                    rule.revision,
                    rule.statePayload))));
        return mood.Value.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture)
            + "|" + mood.BaseValue.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture)
            + "|" + moodToken
            + "|" + JsonUtility.ToJson(actor.Progression.NarrativeLedger)
            + "|" + identityToken;
    }

    private static void RequireGoldenHarvestPreparedExact(
        GoldenHarvestPreparedResolution expected,
        GoldenHarvestPreparedResolution actual,
        bool expectedCommitted,
        string context)
    {
        Require(!string.IsNullOrWhiteSpace(actual.Fingerprint)
            && string.Equals(
                actual.OperationId,
                expected.OperationId,
                StringComparison.Ordinal)
            && string.Equals(
                actual.CharacterId,
                expected.CharacterId,
                StringComparison.Ordinal)
            && string.Equals(
                actual.TraitDefinitionId,
                expected.TraitDefinitionId,
                StringComparison.Ordinal)
            && string.Equals(
                actual.FieldId,
                expected.FieldId,
                StringComparison.Ordinal)
            && string.Equals(
                actual.Fingerprint,
                expected.Fingerprint,
                StringComparison.Ordinal)
            && expected.Committed == expectedCommitted
            && actual.Committed == expectedCommitted
            && actual.Resolution.Outcome == expected.Resolution.Outcome
            && actual.Resolution.PrimaryMultiplier.Equals(
                expected.Resolution.PrimaryMultiplier)
            && actual.Resolution.SecondaryMultiplier.Equals(
                expected.Resolution.SecondaryMultiplier)
            && actual.Resolution.ProgressDelta.Equals(
                expected.Resolution.ProgressDelta)
            && actual.Resolution.FixedRollHash
                == expected.Resolution.FixedRollHash,
            $"{context} changed the frozen prepared result.");
    }
}
#endif
