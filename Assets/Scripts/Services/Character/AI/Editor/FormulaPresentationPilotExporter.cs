#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports module-selection pilot inputs from the same authored catalogs and
/// gameplay ledgers used at runtime. Mechanics and numeric values are deliberately
/// absent: the LLM selects only offered module IDs and evidence IDs, after which
/// C# validates the selection and allocates every number. The fixtures are
/// controlled simulations, never claimed as natural player telemetry or human
/// approval.
/// </summary>
public static partial class FormulaPresentationPilotExporter
{
    private const string ExportParent = "Artifacts/Exports/FormulaModuleSelectionPilot";
    private const int RowsPerProfile = 25;
    private const int MaximumRowsPerProfile = 12500;
    private const int ReservoirRowsPerProfile = 250;
    private const int ReservoirRowStartIndex = RowsPerProfile;
    private const int TrainingRowsPerProfile = 12500;
    private const int TrainingRowStartIndex = ReservoirRowStartIndex + ReservoirRowsPerProfile;

    private static readonly string[] CharacterNames =
    {
        "연화", "무진", "서린", "도윤", "가람", "하진", "세아", "태윤", "라온", "유하",
        "시온", "다온", "예린", "현우", "아린", "준서", "소율", "이안", "채린", "건우",
        "해원", "주안", "나린", "태경", "은호"
    };

    private static readonly string[][] SkillGameplayScenarios =
    {
        new[]
        {
            "character:expedition:battle-victory", "character:expedition:node-battle",
            "character:expedition:node-event", "character:expedition:node-camp",
            "character:expedition:node-cache", "character:expedition:node-boss",
            "character:invasion:started", "character:expedition:battle-survived"
        },
        new[]
        {
            "character:work:repair-completed", "character:work:repair-blocked",
            "character:work:research-completed", "character:work:research-progress",
            "character:work:research-failed", "character:need:sleep-critical",
            "character:need:sleep-satisfied", "character:mood:battle-victory"
        },
        new[]
        {
            "character:survival:hunt", "character:survival:dangerous-hunt",
            "character:need:hunger-critical", "character:need:hunger-satisfied",
            "character:need:sleep-critical", "character:need:sleep-satisfied",
            "character:expedition:node-camp", "character:expedition:node-cache"
        },
        new[]
        {
            "character:relationship:minion-conflict", "character:relationship:answered-insult",
            "character:need:hunger-critical", "character:mood:battle-victory",
            "character:mood:battle-failure", "character:work:repair-completed",
            "character:invasion:started", "character:expedition:battle-survived"
        }
    };

    private static readonly string[][] TraitGameplayScenarios =
    {
        new[]
        {
            "character:relationship:taboo-witness", "character:mood:battle-failure",
            "character:survival:dangerous-hunt", "character:work:research-progress",
            "character:need:hunger-satisfied", "character:expedition:node-event",
            "character:invasion:started"
        },
        new[]
        {
            "character:relationship:answered-insult", "character:mood:battle-victory",
            "character:survival:hunt", "character:work:repair-blocked",
            "character:need:sleep-critical", "character:expedition:node-cache",
            "character:expedition:battle-survived"
        },
        new[]
        {
            "character:relationship:minion-conflict", "character:mood:battle-victory",
            "character:survival:dangerous-hunt", "character:work:research-completed",
            "character:need:hunger-critical", "character:expedition:node-camp",
            "character:expedition:battle-victory"
        },
        new[]
        {
            "character:relationship:answered-insult", "character:mood:battle-failure",
            "character:survival:hunt", "character:work:repair-completed",
            "character:need:sleep-satisfied", "character:expedition:node-boss",
            "character:work:research-failed"
        }
    };

    private static readonly int[] GameplayMilestones = { 1, 3, 8, 20, 50 };

    private sealed class PilotStrengthBand
    {
        public PilotStrengthBand(
            string id,
            string displayName,
            int intendedRank,
            int influenceUseCount)
        {
            Id = id;
            DisplayName = displayName;
            IntendedRank = intendedRank;
            InfluenceUseCount = influenceUseCount;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int IntendedRank { get; }
        public int InfluenceUseCount { get; }

        public JObject ToJson() => new()
        {
            ["id"] = Id,
            ["displayName"] = DisplayName,
            ["intendedRank"] = IntendedRank,
            ["influenceUseCount"] = InfluenceUseCount
        };
    }

    private sealed class PilotCharacterOwner
    {
        public PilotCharacterOwner(string persistentId, string displayName)
        {
            PersistentId = persistentId;
            DisplayName = displayName;
        }

        public string PersistentId { get; }
        public string DisplayName { get; }
    }

    private readonly struct SkillPilotShape
    {
        public SkillPilotShape(
            CharacterSkillKind kind,
            CharacterSkillTrigger trigger,
            CharacterSkillTarget target,
            CharacterSkillTargetingMode targetingMode,
            CharacterSkillEffectArea effectArea,
            int areaSize,
            CharacterUltimateDomain ultimateDomain = CharacterUltimateDomain.None)
        {
            Kind = kind;
            Trigger = trigger;
            Target = target;
            TargetingMode = targetingMode;
            EffectArea = effectArea;
            AreaSize = areaSize;
            UltimateDomain = ultimateDomain;
        }

        public CharacterSkillKind Kind { get; }
        public CharacterSkillTrigger Trigger { get; }
        public CharacterSkillTarget Target { get; }
        public CharacterSkillTargetingMode TargetingMode { get; }
        public CharacterSkillEffectArea EffectArea { get; }
        public int AreaSize { get; }
        public CharacterUltimateDomain UltimateDomain { get; }
    }

    // The cycle deliberately covers the runtime-supported axes instead of
    // repeatedly capturing the easiest ManualCombat/Self/Single path. Broad
    // manual areas are assigned to fresh strength rows, where the same runtime
    // affordability predicate can supply their required distinct capabilities.
    private static readonly SkillPilotShape[] SkillPilotShapes =
    {
        new(CharacterSkillKind.Passive, CharacterSkillTrigger.WorkCompleted,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Passive, CharacterSkillTrigger.MoodChanged,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Passive, CharacterSkillTrigger.RelationshipChanged,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Ultimate, CharacterSkillTrigger.InvasionStarted,
            CharacterSkillTarget.Enemy, CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Single, 1, CharacterUltimateDomain.Defense),
        new(CharacterSkillKind.Active, CharacterSkillTrigger.ManualWork,
            CharacterSkillTarget.Ally, CharacterSkillTargetingMode.AllEligible,
            CharacterSkillEffectArea.Dungeon, 1),
        new(CharacterSkillKind.Active, CharacterSkillTrigger.ManualCombat,
            CharacterSkillTarget.Enemy, CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Active, CharacterSkillTrigger.ManualCombat,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Active, CharacterSkillTrigger.ManualCombat,
            CharacterSkillTarget.Ally, CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Active, CharacterSkillTrigger.ManualWork,
            CharacterSkillTarget.Ally, CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Room, 1),
        new(CharacterSkillKind.Active, CharacterSkillTrigger.ManualWork,
            CharacterSkillTarget.Ally, CharacterSkillTargetingMode.DeterministicRandom,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Passive, CharacterSkillTrigger.InvasionStarted,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Ultimate, CharacterSkillTrigger.ManualCombat,
            CharacterSkillTarget.Enemy, CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Single, 1, CharacterUltimateDomain.Offense),
        new(CharacterSkillKind.Passive, CharacterSkillTrigger.WorkStarted,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Active, CharacterSkillTrigger.ManualWork,
            CharacterSkillTarget.Ally, CharacterSkillTargetingMode.DeterministicRandom,
            CharacterSkillEffectArea.Room, 1),
        new(CharacterSkillKind.Active, CharacterSkillTrigger.ManualWork,
            CharacterSkillTarget.Ally, CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Square, 3),
        new(CharacterSkillKind.Passive, CharacterSkillTrigger.BattleCompleted,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Ultimate, CharacterSkillTrigger.OperatingDayStarted,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1, CharacterUltimateDomain.Management),
        new(CharacterSkillKind.Passive, CharacterSkillTrigger.EnemyDefeated,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Active, CharacterSkillTrigger.ManualWork,
            CharacterSkillTarget.Ally, CharacterSkillTargetingMode.DeterministicRandom,
            CharacterSkillEffectArea.Square, 3),
        new(CharacterSkillKind.Active, CharacterSkillTrigger.ManualWork,
            CharacterSkillTarget.Ally, CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Square, 5),
        new(CharacterSkillKind.Passive, CharacterSkillTrigger.NeedChanged,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Ultimate, CharacterSkillTrigger.InvasionStarted,
            CharacterSkillTarget.Enemy, CharacterSkillTargetingMode.PlayerSelected,
            CharacterSkillEffectArea.Single, 1, CharacterUltimateDomain.Defense),
        new(CharacterSkillKind.Passive, CharacterSkillTrigger.BattleStarted,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Passive, CharacterSkillTrigger.WorkCompleted,
            CharacterSkillTarget.Self, CharacterSkillTargetingMode.Self,
            CharacterSkillEffectArea.Single, 1),
        new(CharacterSkillKind.Active, CharacterSkillTrigger.ManualWork,
            CharacterSkillTarget.Ally, CharacterSkillTargetingMode.DeterministicRandom,
            CharacterSkillEffectArea.Square, 7)
    };

    // Exercise the real influence-reuse rule instead of overriding a budget.
    // Every fifth row has the same kind of gameplay history at a different point
    // in its reuse lifecycle, so the formula itself must produce the spread.
    private static readonly PilotStrengthBand[] PilotStrengthBands =
    {
        new("reuse-decayed", "재사용 감쇠", 0, 9),
        new("emerging", "초기 영향력", 1, 4),
        new("developing", "성장 영향력", 2, 2),
        new("established", "숙련 영향력", 3, 1),
        new("fresh-peak", "신선한 고강도", 4, 0)
    };

    private static PilotStrengthBand StrengthBandFor(int rowIndex) =>
        PilotStrengthBands[rowIndex % PilotStrengthBands.Length];

    private static int MaximumSkillModulesFor(PilotStrengthBand strengthBand) =>
        strengthBand.IntendedRank switch
        {
            <= 1 => 1,
            <= 3 => 2,
            _ => 3
        };

    private static PilotCharacterOwner BuildPilotOwner(string profile, int rowIndex)
    {
        if (string.IsNullOrWhiteSpace(profile))
            throw new ArgumentException("Pilot owner profile is required.", nameof(profile));
        if (rowIndex < 0) throw new ArgumentOutOfRangeException(nameof(rowIndex));
        return new PilotCharacterOwner(
            "character:" + profile + "-pilot:"
            + rowIndex.ToString("D5", CultureInfo.InvariantCulture),
            CharacterNames[rowIndex % CharacterNames.Length]);
    }

    private static SkillPilotShape SkillPilotShapeFor(int rowIndex)
    {
        if (rowIndex < 0) throw new ArgumentOutOfRangeException(nameof(rowIndex));
        return SkillPilotShapes[rowIndex % SkillPilotShapes.Length];
    }

    private static CharacterSkillCandidateRule BuildPilotSkillRule(
        IEnumerable<CharacterSkillModuleRule> modules,
        CharacterSkillSystemSettingsSO settings,
        int rowIndex,
        int formulaBudget)
    {
        if (modules == null) throw new ArgumentNullException(nameof(modules));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (formulaBudget < 1) throw new ArgumentOutOfRangeException(nameof(formulaBudget));

        SkillPilotShape shape = SkillPilotShapeFor(rowIndex);
        CharacterSkillAreaRules.RequireValid(
            shape.TargetingMode, shape.EffectArea, shape.AreaSize);
        CharacterSkillCandidateRule rule = new()
        {
            ruleId = "pilot-rule:" + rowIndex.ToString("D5", CultureInfo.InvariantCulture),
            rarity = shape.Kind == CharacterSkillKind.Ultimate
                ? CharacterSkillRarity.Legendary
                : shape.Kind == CharacterSkillKind.Passive
                    ? CharacterSkillRarity.Advanced
                    : CharacterSkillRarity.Common,
            budget = formulaBudget,
            trigger = shape.Trigger,
            target = shape.Target,
            targetingMode = shape.TargetingMode,
            effectArea = shape.EffectArea,
            areaSize = shape.AreaSize,
            ultimateDomain = shape.UltimateDomain,
            cooldownTurns = 0,
            manualDurationHours = shape.Trigger == CharacterSkillTrigger.ManualWork
                ? GameCalendarRules.HoursPerDay
                : 0,
            manualCooldownDays = shape.Trigger == CharacterSkillTrigger.ManualWork ? 1 : 0,
            mechanicalPolicySource = shape.Kind == CharacterSkillKind.Ultimate
                ? CharacterSkillMechanicalPolicySource.RequestedUltimateDomain
                : CharacterSkillMechanicalPolicySource.AuthoredRule,
            allowedVariantIds = new List<string>()
        };
        CharacterSkillFormationRules.Resolve(rule.target,
            Array.Empty<CharacterSkillModuleSelection>(),
            out rule.usableFrom, out rule.targetPositions);
        if (rule.trigger == CharacterSkillTrigger.ManualWork
            && (rule.manualDurationHours < 1 || rule.manualCooldownDays < 1))
        {
            throw new InvalidOperationException(
                "ManualWork pilot rule must preserve authored positive duration and day cooldown.");
        }

        NarrativeFormulaGenerationCostContext authored = settings.formulaPolicy
            .RequireGenerationCostContext(rule.trigger, rule.target);
        NarrativeFormulaGenerationCostContext costContext = new(
            authored.TriggerFrequencyUnits,
            authored.GuaranteedProc,
            CharacterSkillAreaRules.ResolveCostTargetCount(
                rule.effectArea, rule.areaSize, authored.TargetCount));
        rule.allowedModuleIds = modules.Where(module => module != null
                && module.Allows(shape.Kind, rule.trigger, rule.target)
                && CharacterSkillValidation.IsTargetCompatible(module, rule.target)
                && CharacterSkillFormulaRuntimeContextPolicy.ConsumesAllAppliedAxes(
                    shape.Kind, rule, module)
                && !CharacterSkillValidation.WouldSelfTrigger(module, rule.trigger)
                && (!(module is CharacterManagementSkillModuleRule)
                    || CharacterSkillModuleCapabilityRegistry.Require(module)
                        .IsManagementModuleReachable(shape.Kind, rule.trigger))
                && (shape.Kind != CharacterSkillKind.Ultimate
                    || (rule.ultimateDomain == CharacterUltimateDomain.Management)
                        == (module is CharacterManagementSkillModuleRule)))
            .Where(module => settings.RequireFormulaDescriptor(module)
                .CalculateContextCost(costContext, 1) <= formulaBudget)
            .Select(module => module.id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        int requiredDistinctCapabilities = shape.Kind == CharacterSkillKind.Active ? 3 : 1;
        if (rule.allowedModuleIds.Count < requiredDistinctCapabilities)
        {
            throw new InvalidOperationException(
                "Pilot CharacterSkill shape cannot use the live affordability path: "
                + shape.Kind + "/" + shape.Trigger + "/" + shape.Target + "/"
                + shape.EffectArea + "/" + shape.AreaSize.ToString(CultureInfo.InvariantCulture)
                + "; budget=" + formulaBudget.ToString(CultureInfo.InvariantCulture)
                + "; affordable=" + rule.allowedModuleIds.Count.ToString(CultureInfo.InvariantCulture));
        }
        return rule;
    }

    [MenuItem("DungeonStory/Narrative Formula/Export Module Selection Pilot 100")]
    public static void ExportFromMenu()
    {
        string version = "formula-module-selection-v1-" +
            DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        string output = ExportVerified100(version);
        UnityEngine.Debug.Log("Formula module-selection pilot exported: " + output);
    }

    public static string ExportVerified100(string version)
        => ExportVerifiedRows(version, RowsPerProfile, 0);

    public static string ExportVerifiedReservoir1000(string version)
        => ExportVerifiedRows(version, ReservoirRowsPerProfile, ReservoirRowStartIndex);

    public static string ExportVerifiedTraining50000(string version)
        => ExportVerifiedRows(version, TrainingRowsPerProfile, TrainingRowStartIndex);

    /// <summary>
    /// Creates an immutable controlled-simulation export. Callers must use a
    /// non-overlapping rowStartIndex for reservoir/training material; the human
    /// approval gate lives in the receiving AI workspace and is intentionally not
    /// inferred by this exporter.
    /// </summary>
    public static string ExportVerifiedRows(string version, int rowsPerProfile, int rowStartIndex)
    {
        string canonicalVersion = RequireVersion(version);
        if (rowsPerProfile <= 0 || rowsPerProfile > MaximumRowsPerProfile)
            throw new ArgumentOutOfRangeException(nameof(rowsPerProfile),
                $"rowsPerProfile must be in [1,{MaximumRowsPerProfile}].");
        if (rowStartIndex < 0 || rowStartIndex > int.MaxValue - rowsPerProfile)
            throw new ArgumentOutOfRangeException(nameof(rowStartIndex));
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string gameCommit = RequireGitCommit(projectRoot);
        CatalogContext catalogs = LoadAndValidateCatalogs();
        string combinedCatalogSha256 = BuildCombinedCatalogSha256(catalogs);

        string parent = Path.Combine(projectRoot, ExportParent.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, canonicalVersion);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new InvalidOperationException("Formula pilot export is create-only: " + destination);
        string staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            IReadOnlyList<JObject> first = BuildRows(
                catalogs, combinedCatalogSha256, gameCommit, rowsPerProfile, rowStartIndex);
            ValidateRows(first, rowsPerProfile);
            string sourceFileName = "formula_module_selection_source_"
                + first.Count.ToString(CultureInfo.InvariantCulture) + ".jsonl";
            string sourcePath = Path.Combine(staging, sourceFileName);
            WriteJsonl(sourcePath, first);
            string verificationPath = Path.Combine(staging, ".determinism-verification.jsonl");
            IReadOnlyList<JObject> second = BuildRows(
                catalogs, combinedCatalogSha256, gameCommit, rowsPerProfile, rowStartIndex);
            ValidateRows(second, rowsPerProfile);
            WriteJsonl(verificationPath, second);
            if (!FilesEqual(sourcePath, verificationPath))
                throw new InvalidOperationException(
                    "Independent formula module-selection captures were not byte-identical.");
            File.Delete(verificationPath);
            string rowsSha256 = Sha256File(sourcePath);
            SourceDigest sourceDigest = CaptureSourceDigest(projectRoot, catalogs);
            EquipmentReplayPlanSet equipmentReplayPlans = ResolveEquipmentReplayPlans(
                catalogs.Equipment, catalogs.EquipmentDefinitions);
            FacilityReplayPlanSet facilityReplayPlans = ResolveFacilityReplayPlans(
                catalogs.Facilities);
            JObject manifest = new JObject
            {
                ["schemaVersion"] = 3,
                ["version"] = canonicalVersion,
                ["gameCommit"] = gameCommit,
                ["combinedCatalogSha256"] = combinedCatalogSha256,
                ["sourceDigest"] = sourceDigest.Digest,
                ["workingTreeDirty"] = sourceDigest.WorkingTreeDirty,
                ["controlledSimulation"] = true,
                ["naturalPlayTelemetry"] = false,
                ["gameplayReachableSimulation"] = true,
                ["resultConditionedStorySelection"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false,
                ["rowCount"] = first.Count,
                ["rowsPerProfile"] = rowsPerProfile,
                ["rowStartIndex"] = rowStartIndex,
                ["profileCounts"] = new JObject(first
                    .GroupBy(row => (string)row["profileId"], StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new JProperty(group.Key, group.Count()))),
                ["strengthBandCounts"] = new JObject(first
                    .GroupBy(row => (string)row["strengthBand"]?["id"], StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new JProperty(group.Key, group.Count()))),
                ["equipmentFormulaVersion"] = catalogs.Equipment.RequirePolicy().FormulaVersion,
                ["equipmentFormulaTargetEligibleDefinitionIds"] = new JArray(
                    equipmentReplayPlans.Plans.Select(value => value.Definition.EquipmentId)),
                ["equipmentFormulaTargetExclusions"] = new JArray(
                    equipmentReplayPlans.Exclusions.Select(value => value.ToJson())),
                ["equipmentFormulaTargetEnumeration"] = new JArray(
                    equipmentReplayPlans.Enumeration.Select(value => value.ToJson())),
                ["facilitySourceReplayEligibleRecipeIds"] = new JArray(
                    facilityReplayPlans.Plans.Select(value => value.Recipe.EffectiveId)),
                ["facilitySourceReplayExclusions"] = new JArray(
                    facilityReplayPlans.Exclusions.Select(value => value.ToJson())),
                ["facilityFormulaRecipeEnumeration"] = new JArray(
                    facilityReplayPlans.Enumeration.Select(value => value.ToJson())),
                ["formulaModuleSelectionSourceSha256"] = rowsSha256,
                ["sourceFiles"] = new JArray(sourceDigest.Files.Select(value => new JObject
                {
                    ["path"] = value.Path,
                    ["sha256"] = value.Sha256
                }))
            };
            string manifestPath = Path.Combine(staging, "manifest.json");
            File.WriteAllText(manifestPath, CanonicalJson(manifest) + "\n", new UTF8Encoding(false));
            JObject delivery = new JObject
            {
                ["schemaVersion"] = 1,
                ["files"] = new JArray(new[] { sourceFileName, "manifest.json" }.Select(name =>
                    new JObject
                    {
                        ["path"] = name,
                        ["sha256"] = Sha256File(Path.Combine(staging, name))
                    }))
            };
            File.WriteAllText(Path.Combine(staging, "delivery_manifest.json"),
                CanonicalJson(delivery) + "\n", new UTF8Encoding(false));
            Directory.Move(staging, destination);
            return destination;
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            throw;
        }
    }

    public static void RunAll()
    {
        ValidateEventContextPersistence();
        CatalogContext catalogs = LoadAndValidateCatalogs();
        string catalog = BuildCombinedCatalogSha256(catalogs);
        IReadOnlyList<JObject> rows = BuildRows(
            catalogs, catalog, "0000000000000000000000000000000000000000", RowsPerProfile, 0);
        ValidateRows(rows, RowsPerProfile);
        if (!string.Equals(ToJsonl(rows), ToJsonl(BuildRows(
                catalogs, catalog, "0000000000000000000000000000000000000000", RowsPerProfile, 0)),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Formula pilot row capture is not deterministic.");
        const int expandedRowsPerProfile = 31;
        IReadOnlyList<JObject> expanded = BuildRows(
            catalogs, catalog, "0000000000000000000000000000000000000000",
            expandedRowsPerProfile, ReservoirRowStartIndex);
        ValidateRows(expanded, expandedRowsPerProfile);
        if (!string.Equals(ToJsonl(expanded), ToJsonl(BuildRows(
                catalogs, catalog, "0000000000000000000000000000000000000000",
                expandedRowsPerProfile, ReservoirRowStartIndex)),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Expanded formula rows are not deterministic.");
        IReadOnlyList<JObject> trainingSample = BuildRows(
            catalogs, catalog, "0000000000000000000000000000000000000000",
            expandedRowsPerProfile, TrainingRowStartIndex);
        ValidateRows(trainingSample, expandedRowsPerProfile);
        HashSet<string> reservoirFamilies = expanded.Select(value => CanonicalJson(
            value["moduleSelectionRequest"]?["publicFacts"])).ToHashSet(StringComparer.Ordinal);
        if (trainingSample.Any(value => reservoirFamilies.Contains(CanonicalJson(
                value["moduleSelectionRequest"]?["publicFacts"]))))
            throw new InvalidOperationException("Reservoir and training controlled narrative ranges overlap.");
        if (GameplayNarrativeSourceCatalog.Characters.Count < 6
            || GameplayNarrativeSourceCatalog.Equipment.Count < 2
            || GameplayNarrativeSourceCatalog.Facilities.Count < 6)
            throw new InvalidOperationException(
                "Gameplay narrative registry cannot construct complete evidence histories.");
        FacilityReplayPlanSet facilityReplayPlans = ResolveFacilityReplayPlans(catalogs.Facilities);
        if (facilityReplayPlans.Enumeration.Length != catalogs.Facilities.Length
            || facilityReplayPlans.Enumeration.Any(value =>
                string.IsNullOrWhiteSpace(value.RecipeId)
                || value.FormulaVersion <= 0
                || string.IsNullOrWhiteSpace(value.ResultDefinitionId)
                || value.ReplayEligible != string.IsNullOrWhiteSpace(value.Reason))
            || facilityReplayPlans.Plans.Length != 6
            || facilityReplayPlans.Plans.Any(plan => !plan.DirectReplaySources.Any(source =>
                string.Equals(source.SourceId, "facility:visit", StringComparison.Ordinal)))
            || facilityReplayPlans.Exclusions.Length != 0
            || facilityReplayPlans.Enumeration.Any(value => !value.ReplayEligible))
        {
            throw new InvalidOperationException(
                "Facility pilot must enumerate all six replayable recipes without exclusions.");
        }
        JObject[] facilityRows = rows.Where(value => string.Equals(
                (string)value["profileId"],
                LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
                StringComparison.Ordinal))
            .ToArray();
        JObject[] facilityProvenance = facilityRows
            .SelectMany(value => ((JArray)value["evidenceProvenance"]).OfType<JObject>())
            .ToArray();
        int supportedFacilitySourceCount = facilityReplayPlans.Plans
            .SelectMany(value => value.DirectReplaySources)
            .Select(value => value.SourceId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        int observedFacilitySourceCount = facilityProvenance
            .Select(value => (string)value["sourceDefinitionId"])
            .Distinct(StringComparer.Ordinal)
            .Count();
        int distinctFacilityEventSignatures = facilityRows
            .Select(value => string.Join(",", ((JArray)value["evidenceProvenance"])
                .OfType<JObject>()
                .Select(provenance => (string)provenance["sourceDefinitionId"])))
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (facilityProvenance.Any(value => !ValidateFacilityProvenance(value))
            || (supportedFacilitySourceCount > 2 && observedFacilitySourceCount < 3)
            || distinctFacilityEventSignatures < 2
            || facilityRows.SelectMany(value => ((JArray)value["evidenceProvenance"])
                    .OfType<JObject>())
                .Select(value => (string)value["sourceTargetDefinitionId"])
                .Distinct(StringComparer.Ordinal).Count() != facilityReplayPlans.Plans.Length)
        {
            throw new InvalidOperationException(
                "Facility pilot did not preserve varied source-applicable replay evidence.");
        }
        EquipmentReplayPlanSet equipmentReplayPlans = ResolveEquipmentReplayPlans(
            catalogs.Equipment, catalogs.EquipmentDefinitions);
        int equipmentFormulaVersion = catalogs.Equipment.RequirePolicy().FormulaVersion;
        if (equipmentFormulaVersion <= EquipmentEvolutionRules.DrawbackModuleSelectionFormulaVersion
            || equipmentReplayPlans.Enumeration.Length != catalogs.EquipmentDefinitions.Length
            || equipmentReplayPlans.Enumeration.Any(value =>
                string.IsNullOrWhiteSpace(value.DefinitionId)
                || string.IsNullOrWhiteSpace(value.Kind)
                || value.ReplayEligible != string.IsNullOrWhiteSpace(value.Reason))
            || equipmentReplayPlans.Plans.Length == 0
            || equipmentReplayPlans.Enumeration.Any(value => (value.Kind is "Armor" or "Shield")
                && !value.ReplayEligible)
            || equipmentReplayPlans.Exclusions.Any(value =>
                !string.Equals(value.Kind, CombatEquipmentKind.RecoverableThrowingWeapon.ToString(),
                    StringComparison.Ordinal)
                || !string.Equals(value.Reason,
                    "no-typed-gameplay-replay-for-recoverable-throwing-weapon",
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Equipment pilot must use the current formula version and only exclude truly unreplayable concrete targets.");
        }
        JObject[] equipmentRows = rows.Where(value => string.Equals(
                (string)value["profileId"],
                LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
                StringComparison.Ordinal))
            .ToArray();
        if (equipmentRows.Any(value => !ValidateEquipmentTarget(
                value["authoritativeEquipmentTarget"] as JObject))
            || equipmentRows.Select(value => (string)value["authoritativeEquipmentTarget"]?["definitionId"])
                .Any(definitionId => !equipmentReplayPlans.Plans.Any(plan => string.Equals(
                    plan.Definition.EquipmentId, definitionId, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                "Equipment pilot did not bind every replay to an eligible concrete definition.");
        }
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        HashSet<string> frozenSources = CaptureSourceDigest(projectRoot, catalogs).Files
            .Select(value => value.Path).ToHashSet(StringComparer.Ordinal);
        string[] requiredRuntimeStateSources =
        {
            "Assets/Scripts/Services/Character/Core/CharacterSkillModels.cs",
            "Assets/Scripts/Services/Character/Core/CharacterManualSkillRuntime.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitState.cs",
            "Assets/Scripts/Services/Character/Core/DungeonCharacterSaveData.cs",
            "Assets/Scripts/Models/Evolution/Core/EvolutionHistoryModels.cs",
            "Assets/Scripts/Models/Evolution/Core/UsageLedgerCompactor.cs",
            "Assets/Scripts/Models/Combat/Core/EquipmentItemStateCodec.cs",
            "Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs",
            "Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs",
            "Assets/Scripts/Models/Economy/Content/CombatArmorSO.cs",
            "Assets/Scripts/Models/Economy/Content/CombatShieldSO.cs",
            "Assets/Scripts/Services/Evolution/EvolutionHistoryNarrativeRuntime.cs",
            "Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionModels.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionStateComponent.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityFormulaEvolutionPresentationRuntime.cs",
            "Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaDrawbackPolicyDefinition.cs",
            "Assets/Scripts/Models/Evolution/Core/EvolutionModuleModels.cs",
            "Assets/Scripts/Models/Evolution/Core/EvolutionModuleRegistry.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitEffectSource.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillDrawbackEffectSource.cs",
            "Assets/Scripts/Services/Effects/Runtime/CharacterDerivedStatsSnapshot.cs",
            "Assets/Scripts/Services/Combat/CombatEquipmentStatProjector.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionModifierQuery.cs",
            "Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs",
            "Assets/Scripts/Services/Buildings/Abilities/BuildingAbilityAccessors.cs"
        };
        string[] missingRuntimeStateSources = requiredRuntimeStateSources
            .Where(path => !frozenSources.Contains(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (missingRuntimeStateSources.Length > 0)
            throw new InvalidOperationException(
                "Formula export source digest omitted runtime state or presentation authority: "
                + string.Join(", ", missingRuntimeStateSources));
        UnityEngine.Debug.Log("FormulaPresentationPilotExporter module-selection scenarios passed (100 pilot + disjoint expanded ranges). ");
    }

    private static void ValidateEventContextPersistence()
    {
        CharacterNarrativeLedger ledger = new CharacterNarrativeLedger();
        GameplayNarrativeEventContext context = new GameplayNarrativeEventContext
        {
            eventInstanceId = "event:test:1",
            chainId = "chain:test",
            locationId = "location:test",
            locationDisplayName = "시험 회랑",
            actorId = "character:test",
            actorDisplayName = "연화",
            counterpartyId = "enemy:test",
            counterpartyDisplayName = "동굴늑대",
            usedObjectId = "weapon:test",
            usedObjectDisplayName = "시험창",
            resultDetail = "교전 승리",
            occurredDay = 3
        };
        ledger.Record(
            CharacterNarrativeDomain.Combat,
            "battle-victory",
            "target:test",
            "won",
            1f,
            3,
            CharacterNarrativeEvidenceMetadata.Ordinary(
                CharacterNarrativeDomain.Combat,
                "battle-victory"),
            context);
        CharacterNarrativeLedger restored = JsonUtility.FromJson<CharacterNarrativeLedger>(
            JsonUtility.ToJson(ledger));
        CharacterNarrativeFact fact = restored.Facts.Single();
        if (!string.Equals(fact.lastEventContext?.eventInstanceId, "event:test:1", StringComparison.Ordinal)
            || fact.lastEventContext.sequence <= 0L
            || !fact.lastEventContext.HasNarrativeDetail)
            throw new InvalidOperationException("Character event context did not survive save round-trip.");

        CharacterNarrativeLedger legacy = JsonUtility.FromJson<CharacterNarrativeLedger>(
            "{\"facts\":[{\"factId\":\"legacy\",\"subjectId\":\"subject\",\"count\":1}]}");
        CharacterNarrativeFact legacyFact = legacy?.Facts.Single();
        if (legacyFact == null
            || !string.Equals(legacyFact.factId, "legacy", StringComparison.Ordinal)
            || legacyFact.count != 1
            || (legacyFact.lastEventContext != null
                && !string.IsNullOrWhiteSpace(legacyFact.lastEventContext.eventInstanceId)))
            throw new InvalidOperationException("Legacy character narrative save changed meaning during migration.");

        UsageLedger usage = new UsageLedger();
        UsageLedgerEvent recorded = new UsageLedgerCompactor().Record(
            usage, "combat:hit", 4f, "character:test", "equipment:test",
            new[] { "melee" }, narrativeContext: context);
        UsageLedger restoredUsage = JsonUtility.FromJson<UsageLedger>(JsonUtility.ToJson(usage));
        UsageLedgerEvent restoredEvent = restoredUsage.currentGenerationEvents.Single();
        if (!string.Equals(restoredEvent.narrativeContext?.eventInstanceId,
                recorded.narrativeContext.eventInstanceId, StringComparison.Ordinal)
            || !restoredEvent.narrativeContext.HasNarrativeDetail)
            throw new InvalidOperationException("Usage event context did not survive save round-trip.");

        UsageLedger withoutContext = new UsageLedger();
        UsageLedgerCompactor hashCompactor = new UsageLedgerCompactor();
        hashCompactor.Record(withoutContext, "combat:hit", 4f,
            "character:test", "equipment:test", new[] { "melee" });
        if (!string.Equals(
                hashCompactor.ComputeHistoryHash(usage),
                hashCompactor.ComputeHistoryHash(withoutContext),
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Narrative event context changed authoritative mechanical history identity.");
    }

    private static CatalogContext LoadAndValidateCatalogs()
    {
        CharacterSkillSystemSettingsSO skills = AssetDatabase.LoadAssetAtPath<CharacterSkillSystemSettingsSO>(
            CharacterSkillFormulaCatalogAssetBuilder.SettingsAssetPath)
            ?? throw new InvalidOperationException("CharacterSkill settings asset is missing.");
        CharacterSkillFormulaCatalogAssetBuilder.Validate(skills);

        CharacterAcquiredTraitSettingsSO traitSettings = LoadExactlyOne<CharacterAcquiredTraitSettingsSO>();
        CharacterAcquiredTraitModuleSO[] traits = LoadAll<CharacterAcquiredTraitModuleSO>();
        CharacterAcquiredTraitFormulaCatalogAssetBuilder.Validate(traitSettings, traits);

        EquipmentEvolutionFormulaCatalogSO equipment = AssetDatabase
            .LoadAssetAtPath<EquipmentEvolutionFormulaCatalogSO>(EquipmentEvolutionFormulaCatalogAssetBuilder.AssetPath)
            ?? throw new InvalidOperationException("Equipment formula catalog asset is missing.");
        equipment.RequirePolicy();

        CombatEquipmentDefinitionSO[] equipmentDefinitions = Resources
            .LoadAll<CombatEquipmentDefinitionSO>(ResourceCombatEquipmentCatalog.ResourcePath)
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.EquipmentId))
            .OrderBy(value => value.EquipmentId, StringComparer.Ordinal)
            .ToArray();
        if (equipmentDefinitions.Length == 0
            || equipmentDefinitions.Select(value => value.EquipmentId)
                .Distinct(StringComparer.Ordinal).Count() != equipmentDefinitions.Length)
        {
            throw new InvalidOperationException(
                "Formula pilot requires uniquely identified immutable combat equipment definitions.");
        }

        FacilityEvolutionRecipeSO[] facilities = AssetDatabase.FindAssets(
                "t:FacilityEvolutionRecipeSO", new[] { "Assets/Resources/SO/FacilityEvolution/P1" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>)
            .Where(value => value != null).ToArray();
        if (facilities.Length != 6)
            throw new InvalidOperationException("Formula pilot requires exactly six P1 facility recipes.");
        foreach (FacilityEvolutionRecipeSO recipe in facilities)
        {
            recipe.RequireFormulaPolicy();
            foreach (FacilityEvolutionFormulaCapabilityDefinition capability
                     in recipe.RequireFormulaCapabilities())
                capability.ToRuntime();
        }
        return new CatalogContext(skills, traitSettings, traits, equipment,
            equipmentDefinitions, facilities);
    }

    private static IReadOnlyList<JObject> BuildRows(
        CatalogContext catalogs,
        string combinedCatalogSha256,
        string gameCommit,
        int rowsPerProfile,
        int rowStartIndex)
    {
        List<JObject> rows = new List<JObject>(rowsPerProfile * 4);
        BuildSkillRows(rows, catalogs.Skills, combinedCatalogSha256, gameCommit, rowsPerProfile, rowStartIndex);
        BuildTraitRows(rows, catalogs.TraitSettings, catalogs.Traits, combinedCatalogSha256, gameCommit, rowsPerProfile, rowStartIndex);
        ValidateIndependentCharacterEvidence(rows);
        BuildEquipmentRows(rows, catalogs.Equipment, catalogs.EquipmentDefinitions,
            combinedCatalogSha256, gameCommit, rowsPerProfile, rowStartIndex);
        BuildFacilityRows(rows, catalogs.Facilities, combinedCatalogSha256, gameCommit, rowsPerProfile, rowStartIndex);
        return rows;
    }

    private static void BuildSkillRows(
        ICollection<JObject> rows,
        CharacterSkillSystemSettingsSO settings,
        string combinedCatalogSha256,
        string gameCommit,
        int rowsPerProfile,
        int rowStartIndex)
    {
        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        CharacterSkillModuleRule[] modules = settings.Modules.Where(value => value != null)
            .OrderBy(value => value.id, StringComparer.Ordinal).ToArray();
        for (int localIndex = 0; localIndex < rowsPerProfile; localIndex++)
        {
            int index = rowStartIndex + localIndex;
            PilotStrengthBand strengthBand = StrengthBandFor(index);
            PilotCharacterOwner owner = BuildPilotOwner("skill", index);
            EvidenceBundle evidence = BuildCharacterEvidence(
                "skill", index, policy, strengthBand, owner);
            NarrativeFormulaStrength strength = NarrativeFormulaCore.CalculateStrength(
                policy, evidence.Formula);
            CharacterSkillKind skillKind = SkillPilotShapeFor(index).Kind;
            CharacterSkillCandidateRule rule = BuildPilotSkillRule(
                modules, settings, index, strength.Budget);
            CharacterSkillModuleRule[] legalModules = rule.allowedModuleIds
                .Select(settings.FindModule)
                .Where(value => value != null)
                .OrderBy(value => value.id, StringComparer.Ordinal)
                .ToArray();
            if (legalModules.Length != rule.allowedModuleIds.Count)
                throw new InvalidOperationException(
                    "Pilot CharacterSkill rule lost an authored allowed module.");
            HashSet<string> publicEvidenceIds = evidence.PublicFacts
                .OfType<JObject>()
                .Select(value => (string)value["factId"])
                .ToHashSet(StringComparer.Ordinal);
            CharacterNarrativeFact[] negativeFacts = (evidence.Ledger?.Facts
                    ?? Array.Empty<CharacterNarrativeFact>())
                .Where(value => value != null
                    && NarrativeFormulaNegativeEvidence.Matches(
                        value.outcome, value.factId)
                    && publicEvidenceIds.Contains(
                        CharacterSkillFormulaGeneration.BuildEvidenceId(value)))
                .ToArray();
            NarrativeFormulaDrawbackSelectionKind drawbackSelectionKind =
                skillKind == CharacterSkillKind.Active
                    ? NarrativeFormulaDrawbackSelectionKind.PlayerChoice
                    : NarrativeFormulaDrawbackSelectionKind.Automatic;
            CharacterSkillDrawbackCapabilityDefinition[] drawbacks =
                policy.FormulaVersion >=
                    CharacterSkillFormulaGeneration.DrawbackModuleSelectionFormulaVersion
                    ? settings.RequireDrawbackCatalog().Where(value =>
                        value.Allows(skillKind, rule.trigger, negativeFacts)
                        && settings.formulaPolicy.RequireDrawbackPolicy().Resolve(
                            strength.Budget,
                            new NarrativeFormulaDrawbackOption(
                                value.DrawbackId,
                                value.MaximumCredit,
                                drawbackSelectionKind,
                                reachable: true,
                                mandatory: true,
                                separatelyRemovable: false,
                                cancelsSelectedBenefit: false,
                                hasNegativeNarrativeEvidence: true))
                            .AcceptedCredit > 0)
                    .ToArray()
                    : Array.Empty<CharacterSkillDrawbackCapabilityDefinition>();
            JObject[] offers = legalModules.Select(module => ModuleOffer(
                module.id, "positive",
                module.displayName + ": " + CharacterSkillPresentationSemantics
                        .DescribeCapability(settings.RequireFormulaDescriptor(module), rule, skillKind)))
                .Concat(drawbacks.Select(value => ModuleOffer(
                    value.DrawbackId, "drawback",
                    DescribeSkillDrawback(value))))
                .ToArray();
            string selectionId = "selection:skill:" + Sha256Text(string.Join("\n",
                owner.PersistentId, rule.ruleId,
                policy.FormulaVersion, settings.formulaPolicy.RequireCatalogSha256(),
                string.Join(",", legalModules.Select(value => value.id)),
                string.Join(",", drawbacks.Select(value => value.DrawbackId)),
                string.Join(",", evidence.Formula.Select(value => value.EvidenceId))));
            rows.Add(BuildSelectionRow(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                policy.FormulaVersion, combinedCatalogSha256, gameCommit, owner.PersistentId,
                selectionId, owner.DisplayName,
                offers,
                evidence, Math.Min(MaximumSkillModulesFor(strengthBand), legalModules.Length),
                drawbacks.Length > 0 ? 1 : 0, strengthBand,
                SkillContextJson(rule, skillKind), SkillRuleJson(rule)));
        }
    }

    private static void BuildTraitRows(
        ICollection<JObject> rows,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        string combinedCatalogSha256,
        string gameCommit,
        int rowsPerProfile,
        int rowStartIndex)
    {
        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        CharacterAcquiredTraitModuleSO[] ordered = modules.OrderBy(value => value.ModuleId, StringComparer.Ordinal).ToArray();
        for (int localIndex = 0; localIndex < rowsPerProfile; localIndex++)
        {
            int index = rowStartIndex + localIndex;
            PilotStrengthBand strengthBand = StrengthBandFor(index);
            PilotCharacterOwner owner = BuildPilotOwner("trait", index);
            EvidenceBundle evidence = BuildCharacterEvidence(
                "trait", index, policy, strengthBand, owner);
            NarrativeFormulaStrength strength = NarrativeFormulaCore.CalculateStrength(
                policy, evidence.Formula);
            NarrativeFormulaGenerationCostContext generationContext =
                settings.FormulaPolicy.RequireGenerationContext();
            CharacterNarrativeFact[] facts = (evidence.Ledger?.Facts
                    ?? Array.Empty<CharacterNarrativeFact>())
                .Where(value => value != null).ToArray();
            List<JObject> offers = ordered
                .Where(value => value.DomainAffinities.Any(domain =>
                        facts.Any(fact => fact.domain == domain))
                    && value.RequireFormulaDescriptor().CalculateContextCost(
                        generationContext, 1) <= strength.Budget)
                .Select(value => ModuleOffer(value.ModuleId, "positive",
                    TraitModuleOfferDescription(value))).ToList();
            offers.AddRange(settings.DrawbackCapabilities
                .Where(value => value != null
                    && value.DomainAffinities.Any(domain => facts.Any(fact =>
                        fact.domain == domain && NarrativeFormulaNegativeEvidence.Matches(
                            fact.outcome, fact.factId))))
                .Select(value => ModuleOffer(value.DrawbackId, "drawback",
                    value.DisplayName + ": " + value.Description)));
            if (!offers.Any(value => string.Equals((string)value["polarity"],
                    "positive", StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "Trait module-selection pilot has no evidence-compatible benefit at row "
                    + index.ToString(CultureInfo.InvariantCulture) + ": "
                    + string.Join(",", facts.Select(value => value.domain).Distinct()));
            string selectionId = "selection:trait:" + Sha256Text(string.Join("\n",
                owner.PersistentId, policy.FormulaVersion,
                settings.FormulaPolicy.RequireCatalogSha256(),
                string.Join(",", offers.Select(value => (string)value["moduleId"])),
                string.Join(",", evidence.Formula.Select(value => value.EvidenceId))));
            rows.Add(BuildSelectionRow(
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                policy.FormulaVersion, combinedCatalogSha256, gameCommit, owner.PersistentId,
                selectionId, owner.DisplayName,
                offers, evidence, 1, offers.Any(value =>
                    string.Equals((string)value["polarity"], "drawback",
                        StringComparison.Ordinal)) ? 1 : 0, strengthBand));
        }
    }

    private static void BuildEquipmentRows(
        ICollection<JObject> rows,
        EquipmentEvolutionFormulaCatalogSO catalog,
        IReadOnlyList<CombatEquipmentDefinitionSO> equipmentDefinitions,
        string combinedCatalogSha256,
        string gameCommit,
        int rowsPerProfile,
        int rowStartIndex)
    {
        NarrativeFormulaStrengthPolicy policy = catalog.RequirePolicy();
        EquipmentReplayPlan[] eligiblePlans = ResolveEquipmentReplayPlans(
            catalog, equipmentDefinitions).Plans;
        if (eligiblePlans.Length == 0)
        {
            throw new InvalidOperationException(
                "Equipment pilot has no concrete runtime-applicable replay targets.");
        }
        HashSet<CombatEquipmentKind> sampledKinds = new();
        bool sampledAlternativePositiveOffer = false;
        for (int localIndex = 0; localIndex < rowsPerProfile; localIndex++)
        {
            int index = rowStartIndex + localIndex;
            PilotStrengthBand strengthBand = StrengthBandFor(index);
            string targetId = "equipment:pilot:" + index.ToString("D2", CultureInfo.InvariantCulture);
            EquipmentReplayPlan plan = SelectStratifiedEquipmentReplayPlan(
                eligiblePlans, index);
            EquipmentGameplayFormulaReplayResult replay = EquipmentGameplayFormulaReplay.Capture(
                catalog, plan.Definition, targetId, index, strengthBand.InfluenceUseCount);
            EvidenceBundle evidence = BuildEquipmentEvidence(replay);
            EvolutionNode node = replay.Node;
            sampledKinds.Add(plan.Definition.Kind);
            sampledAlternativePositiveOffer |= node.moduleSelectionOffers.Count(value =>
                value != null && value.polarity == EvolutionModuleOfferPolarity.Positive) > 1;
            rows.Add(BuildSelectionRow(
                LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
                policy.FormulaVersion, combinedCatalogSha256, gameCommit, targetId,
                node.moduleSelectionId, plan.Definition.DisplayName,
                node.moduleSelectionOffers.Select(value => ModuleOffer(
                    value.moduleId, Polarity(value.polarity), value.semanticDescription)),
                evidence, 1, node.moduleSelectionOffers.Any(value =>
                    value.polarity == EvolutionModuleOfferPolarity.Drawback) ? 1 : 0,
                strengthBand,
                authoritativeEquipmentTarget: EquipmentTargetJson(plan.Definition)));
        }
        CombatEquipmentKind[] eligibleKinds = eligiblePlans
            .Select(value => value.Definition.Kind)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        if (rowsPerProfile >= eligibleKinds.Length
            && eligibleKinds.Any(value => !sampledKinds.Contains(value)))
        {
            throw new InvalidOperationException(
                "Equipment pilot failed to stratify all eligible concrete equipment kinds.");
        }
        if (eligiblePlans.Any(value => value.PositiveOfferCount > 1)
            && rowsPerProfile >= eligibleKinds.Length
            && !sampledAlternativePositiveOffer)
        {
            throw new InvalidOperationException(
                "Equipment pilot omitted real alternative positive-module cases.");
        }
    }

    private static EquipmentReplayPlan SelectStratifiedEquipmentReplayPlan(
        IReadOnlyList<EquipmentReplayPlan> plans,
        int rowIndex)
    {
        if (plans == null || plans.Count == 0)
            throw new ArgumentException(
                "Equipment stratified sampling requires at least one replay plan.",
                nameof(plans));
        if (rowIndex < 0) throw new ArgumentOutOfRangeException(nameof(rowIndex));
        EquipmentReplayPlan[][] strata = plans
            .Where(value => value?.Definition != null)
            .GroupBy(value => value.Definition.Kind)
            .OrderBy(value => value.Key)
            .Select(group => group
                .OrderByDescending(value => value.PositiveOfferCount)
                .ThenBy(value => value.Definition.EquipmentId, StringComparer.Ordinal)
                .ToArray())
            .ToArray();
        if (strata.Length == 0)
            throw new InvalidOperationException(
                "Equipment replay plans lost every concrete equipment kind.");
        int stratumIndex = rowIndex % strata.Length;
        int cycle = rowIndex / strata.Length;
        EquipmentReplayPlan[] stratum = strata[stratumIndex];
        return stratum[cycle % stratum.Length];
    }

    private static EquipmentReplayPlanSet ResolveEquipmentReplayPlans(
        EquipmentEvolutionFormulaCatalogSO catalog,
        IEnumerable<CombatEquipmentDefinitionSO> definitions)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        List<EquipmentReplayPlan> plans = new();
        List<EquipmentPilotExclusion> exclusions = new();
        foreach (CombatEquipmentDefinitionSO definition in (definitions
                     ?? Array.Empty<CombatEquipmentDefinitionSO>())
                     .Where(value => value != null)
                     .OrderBy(value => value.EquipmentId, StringComparer.Ordinal))
        {
            if (!EquipmentGameplayFormulaReplay.IsDefinitionReplayEligible(
                    catalog, definition, out string failureReason))
            {
                exclusions.Add(new EquipmentPilotExclusion(definition, failureReason));
                continue;
            }

            int positiveOfferCount = EquipmentEvolutionRules
                .CountRuntimeApplicablePositiveModuleOffers(catalog, definition);
            plans.Add(new EquipmentReplayPlan(definition, positiveOfferCount));
        }

        return new EquipmentReplayPlanSet(plans, exclusions);
    }

    private static void BuildFacilityRows(
        ICollection<JObject> rows,
        IReadOnlyList<FacilityEvolutionRecipeSO> recipes,
        string combinedCatalogSha256,
        string gameCommit,
        int rowsPerProfile,
        int rowStartIndex)
    {
        FacilityReplayPlan[] eligiblePlans = ResolveFacilityReplayPlans(recipes).Plans;
        if (eligiblePlans.Length == 0)
            throw new InvalidOperationException(
                "Facility pilot has no source-applicable controlled replay plans.");
        for (int localIndex = 0; localIndex < rowsPerProfile; localIndex++)
        {
            int index = rowStartIndex + localIndex;
            PilotStrengthBand strengthBand = StrengthBandFor(index);
            FacilityReplayPlan plan = eligiblePlans[index % eligiblePlans.Length];
            FacilityEvolutionRecipeSO recipe = plan.Recipe;
            NarrativeFormulaStrengthPolicy policy = recipe.RequireFormulaPolicy();
            string targetId = "facility:pilot:" + index.ToString("D2", CultureInfo.InvariantCulture);
            FacilityEvolutionState state = new FacilityEvolutionState
            {
                facilityPersistentId = targetId,
                usageLedger = new UsageLedger(),
                formulaEvidence = new List<FacilityFormulaEvidenceRecord>(),
                evolutionNodes = new List<EvolutionNode>(),
                generation = 0
            };
            string facilityDisplayName = plan.SourceDefinition.objectName ?? recipe.DisplayName;
            int replayVariationIndex = index / eligiblePlans.Length;
            IReadOnlyDictionary<string, FacilityGameplayUsageSource> sources =
                BuildFacilityUsageLedger(
                    state, index, replayVariationIndex, facilityDisplayName, plan);
            FacilityEvolutionFormulaPresentationPendingSnapshot pending =
                PrepareFacilityStrengthBand(state, recipe, strengthBand);
            EvidenceBundle evidence = BuildFacilityEvidence(
                state, pending.node, sources, index, plan);
            rows.Add(BuildSelectionRow(
                LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
                policy.FormulaVersion, combinedCatalogSha256, gameCommit, targetId,
                pending.node.moduleSelectionId, facilityDisplayName,
                pending.node.moduleSelectionOffers.Select(value => ModuleOffer(
                    value.moduleId, Polarity(value.polarity), value.semanticDescription)),
                evidence, 1, pending.node.moduleSelectionOffers.Any(value =>
                    value.polarity == EvolutionModuleOfferPolarity.Drawback) ? 1 : 0,
                strengthBand));
        }
    }

    private static FacilityReplayPlanSet ResolveFacilityReplayPlans(
        IEnumerable<FacilityEvolutionRecipeSO> recipes)
    {
        List<FacilityReplayPlan> plans = new();
        List<FacilityPilotExclusion> exclusions = new();
        FacilityEvolutionRecipeSO[] recipeValues = (recipes
                ?? Array.Empty<FacilityEvolutionRecipeSO>())
            .Where(value => value != null)
            .OrderBy(value => value.EffectiveId, StringComparer.Ordinal)
            .ToArray();
        foreach (FacilityEvolutionRecipeSO recipe in recipeValues)
        {
            BuildingSO[] definitions = (recipe.fromFacilities ?? Array.Empty<BuildingSO>())
                .Where(value => value != null)
                .Distinct()
                .ToArray();
            if (definitions.Length != 1)
            {
                exclusions.Add(new FacilityPilotExclusion(
                    recipe.EffectiveId,
                    null,
                    "requires-exactly-one-authored-from-facility-definition",
                    Array.Empty<FacilitySourceDiagnostic>()));
                continue;
            }

            BuildingSO definition = definitions[0];
            List<FacilityGameplayUsageSource> directSources = new();
            List<FacilitySourceDiagnostic> diagnostics = new();
            foreach (FacilityGameplayUsageSource source in GameplayNarrativeSourceCatalog.Facilities
                         .OrderBy(value => value.SourceId, StringComparer.Ordinal))
            {
                if (!source.AppliesTo(definition, out string applicabilityFailure))
                {
                    diagnostics.Add(new FacilitySourceDiagnostic(source, applicabilityFailure));
                    continue;
                }
                if (!source.IsDirectLedgerReplaySupported(out string replayFailure))
                {
                    diagnostics.Add(new FacilitySourceDiagnostic(source, replayFailure));
                    continue;
                }
                directSources.Add(source);
            }

            if (!directSources.Any(value => string.Equals(value.SourceId,
                    "facility:visit", StringComparison.Ordinal)))
            {
                diagnostics.Add(new FacilitySourceDiagnostic(
                    "facility:visit", FacilityGameplayUsageApplicability.VisitorFacility,
                    "requires-replayable-completed-visit-source"));
                exclusions.Add(new FacilityPilotExclusion(
                    recipe.EffectiveId, definition,
                    "no-replayable-completed-visit-source-for-from-facility",
                    diagnostics));
                continue;
            }

            if (!HasRuntimeEligibleFacilityPositiveModule(recipe,
                    out FacilityResultTargetDiagnostic[] resultDiagnostics))
            {
                exclusions.Add(new FacilityPilotExclusion(
                    recipe.EffectiveId, definition,
                    "no-runtime-eligible-positive-module-for-result-building",
                    diagnostics, recipe.resultBuilding, resultDiagnostics));
                continue;
            }

            plans.Add(new FacilityReplayPlan(recipe, definition, directSources));
        }

        return new FacilityReplayPlanSet(plans, exclusions, recipeValues);
    }

    private static bool HasRuntimeEligibleFacilityPositiveModule(
        FacilityEvolutionRecipeSO recipe,
        out FacilityResultTargetDiagnostic[] diagnostics)
    {
        EvolutionModuleRegistry registry = new();
        List<FacilityResultTargetDiagnostic> values = new();
        bool eligible = false;
        IReadOnlyList<FacilityEvolutionFormulaCapabilityDefinition> capabilities = recipe == null
            ? Array.Empty<FacilityEvolutionFormulaCapabilityDefinition>()
            : recipe.RequireFormulaCapabilities();
        foreach (FacilityEvolutionFormulaCapabilityDefinition capability in capabilities)
        {
            if (!registry.TryGet(capability.evolutionModuleId,
                    out EvolutionModuleDefinition module))
            {
                values.Add(new FacilityResultTargetDiagnostic(
                    capability.evolutionModuleId,
                    "unregistered-authored-positive-module"));
                continue;
            }
            if (FacilityEvolutionModifierApplicability
                .IsPositiveModuleEligibleForNewGeneration(
                    recipe.resultBuilding, module, out string failureReason))
            {
                eligible = true;
                continue;
            }
            values.Add(new FacilityResultTargetDiagnostic(module.ModuleId, failureReason));
        }
        diagnostics = values.OrderBy(value => value.ModuleId, StringComparer.Ordinal).ToArray();
        return eligible;
    }

    private static NarrativeFormulaCandidate ChooseWinner(
        NarrativeFormulaCapabilityDescriptor descriptor,
        NarrativeFormulaGenerationCostContext costContext,
        int budget,
        IReadOnlyList<NarrativeFormulaEvidence> evidence,
        string ownerId,
        string position,
        int formulaVersion,
        string catalogSha256,
        NarrativeFormulaDrawbackCreditPolicy drawbackPolicy = null,
        NarrativeFormulaDrawbackOption drawbackOption = null)
    {
        double matches = descriptor.AffinityKeys.Count(key => evidence.Any(value =>
            string.Equals(value.EventGroupKey, key, StringComparison.Ordinal)
            || string.Equals(value.ActionKey, key, StringComparison.Ordinal)
            || string.Equals(value.DomainKey, key, StringComparison.Ordinal)));
        IReadOnlyList<NarrativeFormulaCandidate> candidates = NarrativeFormulaCore.Optimize(
            descriptor, costContext, budget, descriptor.NarrativeAffinity + matches,
            noveltyScore: 1d, evidence.Select(value => value.EvidenceId), maximumResults: 3,
            drawbackPolicy: drawbackPolicy, drawbackOption: drawbackOption);
        if (candidates.Count == 0)
            throw new InvalidOperationException("Pilot capability does not fit its authoritative formula budget: "
                + descriptor.CapabilityId + " budget=" + budget.ToString(CultureInfo.InvariantCulture));
        NarrativeFormulaCandidate winner = NarrativeFormulaCore.ChooseDeterministicWinner(
            candidates, ownerId, position, formulaVersion, catalogSha256);
        int recalculated = NarrativeFormulaCore.CalculateCost(winner.Allocations, costContext);
        if (recalculated != winner.PositiveCost
            || winner.CalculatedCost != winner.PositiveCost - winner.DrawbackCredit
            || winner.CalculatedCost > budget)
            throw new InvalidOperationException("Pilot formula candidate escaped its budget or cost calculation.");
        return winner;
    }

    private static EvidenceBundle BuildCharacterEvidence(
        string profile,
        int rowIndex,
        NarrativeFormulaStrengthPolicy policy,
        PilotStrengthBand strengthBand,
        PilotCharacterOwner owner)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (policy.MilestoneWeights.Count != GameplayMilestones.Length)
            throw new InvalidOperationException(
                "Gameplay narrative milestone registry differs from the formula policy.");
        string diagnosticRowId = "character-replay:" + profile + ":"
            + rowIndex.ToString("D5", CultureInfo.InvariantCulture);
        string[] sourceIds = BuildCharacterReplaySourceIds(profile, rowIndex);
        CharacterNarrativeLedger ledger = new CharacterNarrativeLedger();
        List<CharacterGameplayNarrativeSource> replayedSources = new();
        int nextReplayDay = checked(1 + rowIndex * 400);
        for (int factIndex = 0; factIndex < sourceIds.Length; factIndex++)
        {
            CharacterGameplayNarrativeSource source = GameplayNarrativeSourceCatalog.RequireCharacter(
                sourceIds[factIndex]);
            int repetitions = CharacterReplayRepetitions(profile, rowIndex, factIndex);
            GameplayNarrativeEventContext context = BuildCharacterReplayContext(
                source, rowIndex, factIndex, owner);
            if (!string.Equals(context.actorId, owner.PersistentId, StringComparison.Ordinal)
                || !string.Equals(context.actorDisplayName, owner.DisplayName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    diagnosticRowId + " lost the selected character as event actor provenance.");
            }
            source.Record(
                ledger, BuildCharacterReplaySubjectId(source, context, diagnosticRowId),
                repetitions, nextReplayDay, context);
            nextReplayDay = checked(nextReplayDay + repetitions);
            replayedSources.Add(source);
        }
        foreach (CharacterNarrativeFact fact in ledger.Facts.Where(value => value != null))
            fact.influenceUseCount = strengthBand.InfluenceUseCount;
        List<(CharacterGameplayNarrativeSource Source, CharacterNarrativeFact Fact)> recorded = ledger.Facts
            .Where(value => value != null)
            .Select(fact => (ResolveCharacterReplaySource(
                fact, replayedSources, diagnosticRowId), fact))
            .ToList();
        if (recorded.Count == 0 || recorded.Count > 6)
        {
            throw new InvalidOperationException(
                diagnosticRowId + " produced an invalid number of ledger aggregates: "
                + recorded.Count.ToString(CultureInfo.InvariantCulture) + ".");
        }

        List<NarrativeFormulaEvidence> formula = new();
        JArray facts = new JArray();
        JArray provenance = new JArray();
        var presentationRecords = recorded
            .OrderByDescending(value => value.Fact.importancePoints)
            .ThenByDescending(value => value.Fact.milestoneCount)
            .ThenBy(value => value.Fact.lastEventContext?.sequence ?? 0L)
            .Take(4)
            .OrderBy(value => value.Fact.lastEventContext?.sequence ?? 0L)
            .ThenBy(value => value.Fact.lastEventContext?.occurredDay ?? 0)
            .ToArray();
        foreach ((CharacterGameplayNarrativeSource source, CharacterNarrativeFact fact) in recorded)
        {
            string evidenceId = string.Equals(profile, "trait", StringComparison.Ordinal)
                ? CharacterAcquiredTraitEvidenceProjection.Project(fact)
                : CharacterSkillFormulaGeneration.BuildEvidenceId(fact);
            formula.Add(new NarrativeFormulaEvidence(
                evidenceId, fact.eventGroupKey, fact.actionKey, fact.relationshipKey,
                fact.domain.ToString(), fact.milestoneCount, fact.importancePoints,
                fact.influenceUseCount));
        }
        foreach ((CharacterGameplayNarrativeSource source, CharacterNarrativeFact fact) in
                 presentationRecords)
        {
            string evidenceId = string.Equals(profile, "trait", StringComparison.Ordinal)
                ? CharacterAcquiredTraitEvidenceProjection.Project(fact)
                : CharacterSkillFormulaGeneration.BuildEvidenceId(fact);
            facts.Add(new JObject
            {
                ["factId"] = evidenceId,
                ["text"] = source.FormatPublicFact(fact.lastEventContext)
            });
            provenance.Add(BuildProvenance(source, evidenceId, new JObject
            {
                ["domain"] = fact.domain.ToString(),
                ["factId"] = fact.factId,
                ["subjectId"] = fact.subjectId,
                ["outcome"] = fact.outcome,
                ["count"] = fact.count,
                ["milestoneCount"] = fact.milestoneCount,
                ["importancePoints"] = fact.importancePoints,
                ["influenceUseCount"] = fact.influenceUseCount,
                ["eventContext"] = ContextJson(fact.lastEventContext),
                ["eventGroupKey"] = fact.eventGroupKey,
                ["actionKey"] = fact.actionKey
            }));
        }
        if (formula.Select(value => value.EvidenceId).Distinct(StringComparer.Ordinal).Count()
                != formula.Count
            || facts.OfType<JObject>().Select(value => (string)value["factId"])
                .Distinct(StringComparer.Ordinal).Count() != facts.Count
            || facts.Count != provenance.Count)
        {
            throw new InvalidOperationException(
                diagnosticRowId + " must export exactly one formula/public/provenance entry per ledger aggregate.");
        }
        return new EvidenceBundle(formula, facts, provenance,
            recorded.Any(value => NarrativeFormulaNegativeEvidence.Matches(
                value.Fact.outcome, value.Fact.factId)), ledger);
    }

    private static string[] BuildCharacterReplaySourceIds(
        string profile,
        int rowIndex)
    {
        if (rowIndex < 0) throw new ArgumentOutOfRangeException(nameof(rowIndex));
        if (string.Equals(profile, "skill", StringComparison.Ordinal))
        {
            string[] scenario = SkillGameplayScenarios[
                rowIndex % SkillGameplayScenarios.Length];
            int rotation = (rowIndex / SkillGameplayScenarios.Length) % scenario.Length;
            return Enumerable.Range(0, 6)
                .Select(offset => scenario[(rotation + offset) % scenario.Length])
                .ToArray();
        }
        if (string.Equals(profile, "trait", StringComparison.Ordinal))
        {
            string[] scenario = TraitGameplayScenarios[
                (rowIndex * 3 + 1) % TraitGameplayScenarios.Length];
            int start = (rowIndex * 2 + rowIndex / TraitGameplayScenarios.Length)
                % scenario.Length;
            return Enumerable.Range(0, 6)
                .Select(offset => scenario[(start + offset * 3) % scenario.Length])
                .ToArray();
        }
        throw new ArgumentOutOfRangeException(nameof(profile), profile,
            "Character evidence replay supports only skill and trait profiles.");
    }

    private static int CharacterReplayRepetitions(
        string profile,
        int rowIndex,
        int factIndex)
    {
        if (string.Equals(profile, "trait", StringComparison.Ordinal))
        {
            int milestoneIndex = (rowIndex * 2 + factIndex * 3 + 1)
                % GameplayMilestones.Length;
            return GameplayMilestones[milestoneIndex];
        }
        if (string.Equals(profile, "skill", StringComparison.Ordinal))
            return GameplayMilestones[(rowIndex + factIndex) % GameplayMilestones.Length];
        throw new ArgumentOutOfRangeException(nameof(profile), profile,
            "Character evidence replay supports only skill and trait profiles.");
    }

    private static void ValidateIndependentCharacterEvidence(
        IEnumerable<JObject> rows)
    {
        JObject[] values = (rows ?? Array.Empty<JObject>()).ToArray();
        JObject[] skillRows = values.Where(value => string.Equals(
                (string)value?["profileId"],
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                StringComparison.Ordinal))
            .ToArray();
        JObject[] traitRows = values.Where(value => string.Equals(
                (string)value?["profileId"],
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                StringComparison.Ordinal))
            .ToArray();
        HashSet<string> skillSignatures = skillRows
            .Select(CharacterPublicSourceSignature)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> universalTraitSources = null;
        foreach (JObject traitRow in traitRows)
        {
            HashSet<string> traitSources = ((JArray)traitRow["evidenceProvenance"])
                .OfType<JObject>()
                .Select(value => (string)value["sourceDefinitionId"])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);
            if (universalTraitSources == null)
                universalTraitSources = new HashSet<string>(traitSources, StringComparer.Ordinal);
            else
                universalTraitSources.IntersectWith(traitSources);
            if (skillSignatures.Contains(CharacterPublicSourceSignature(traitRow)))
                throw new InvalidOperationException(
                    "Skill and trait profiles exported an identical public gameplay fact set.");
        }
        if (traitRows.Length >= RowsPerProfile
            && universalTraitSources != null
            && universalTraitSources.Count > 0)
            throw new InvalidOperationException(
                "Trait evidence sampling forced a universal public gameplay source: "
                + string.Join(",", universalTraitSources.OrderBy(
                    value => value, StringComparer.Ordinal)));
    }

    private static string CharacterPublicSourceSignature(JObject row) => string.Join("\n",
        ((JArray)row["evidenceProvenance"]).OfType<JObject>()
            .Select(value => (string)value["sourceDefinitionId"])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal));

    private static CharacterGameplayNarrativeSource ResolveCharacterReplaySource(
        CharacterNarrativeFact fact,
        IEnumerable<CharacterGameplayNarrativeSource> replayedSources,
        string diagnosticRowId)
    {
        CharacterGameplayNarrativeSource[] matches = (replayedSources
                ?? Array.Empty<CharacterGameplayNarrativeSource>())
            .Where(source => source != null && source.Domain == fact.domain
                && string.Equals(source.FactId, fact.factId, StringComparison.Ordinal)
                && string.Equals(source.Outcome, fact.outcome, StringComparison.Ordinal))
            .Distinct()
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                diagnosticRowId + " cannot map ledger aggregate " + fact.domain + "/"
                + fact.factId + "/" + fact.subjectId + "/" + fact.outcome
                + " to exactly one replayed gameplay source.");
        }
        return matches[0];
    }

    private static string BuildCharacterReplaySubjectId(
        CharacterGameplayNarrativeSource source,
        GameplayNarrativeEventContext context,
        string diagnosticRowId)
    {
        if (source == null || context == null)
            throw new ArgumentNullException("Character replay subject requires source and event context.");
        string subjectId = source.Domain switch
        {
            CharacterNarrativeDomain.Combat or CharacterNarrativeDomain.Expedition =>
                context.locationId,
            CharacterNarrativeDomain.Survival or CharacterNarrativeDomain.Relationship =>
                context.counterpartyId,
            CharacterNarrativeDomain.Invasion => context.resultDetail,
            CharacterNarrativeDomain.Work => context.usedObjectId,
            CharacterNarrativeDomain.Need or CharacterNarrativeDomain.Mood => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        if (source.Domain != CharacterNarrativeDomain.Need
            && source.Domain != CharacterNarrativeDomain.Mood
            && string.IsNullOrWhiteSpace(subjectId))
        {
            throw new InvalidOperationException(
                diagnosticRowId + " has no gameplay subject for " + source.SourceId + ".");
        }
        return subjectId;
    }

    private static GameplayNarrativeEventContext BuildCharacterReplayContext(
        CharacterGameplayNarrativeSource source,
        int rowIndex,
        int factIndex,
        PilotCharacterOwner owner)
    {
        IReadOnlyList<OffenseTargetDefinition> targets = OffenseWorldMapService.CreateDefaultTargets();
        // All replayed facts in a row belong to the same typed gameplay context
        // (one expedition target/facility/counterparty). This retains the live
        // ledger's aggregate key when related outcomes repeat that subject.
        OffenseTargetDefinition target = targets[rowIndex % targets.Count];
        string location = !string.IsNullOrWhiteSpace(target.regionDisplayName)
            ? target.regionDisplayName
            : target.title;
        string counterpartyId = source.Domain switch
        {
            CharacterNarrativeDomain.Survival => "wildlife:" + target.id,
            CharacterNarrativeDomain.Relationship => "character:replay-counterparty:"
                + rowIndex.ToString("D5", CultureInfo.InvariantCulture) + ":"
                + factIndex.ToString("D2", CultureInfo.InvariantCulture),
            CharacterNarrativeDomain.Combat => "character:expedition-opponent:"
                + target.id,
            CharacterNarrativeDomain.Invasion => "faction:invasion:" + target.factionId,
            _ => string.Empty
        };
        string counterpartyDisplayName = source.Domain switch
        {
            CharacterNarrativeDomain.Survival => "야생동물 "
                + (1 + (rowIndex + factIndex) % 13).ToString(CultureInfo.InvariantCulture),
            CharacterNarrativeDomain.Relationship => CharacterNames[
                (rowIndex + factIndex + 11) % CharacterNames.Length],
            CharacterNarrativeDomain.Combat => "원정 교전 상대 "
                + (1 + (rowIndex + factIndex) % 17).ToString(CultureInfo.InvariantCulture),
            CharacterNarrativeDomain.Invasion => "침공 세력",
            _ => string.Empty
        };
        return new GameplayNarrativeEventContext
        {
            eventInstanceId = $"character-replay:{rowIndex}:{factIndex}",
            chainId = $"character-replay:{rowIndex}",
            locationId = target.id,
            locationDisplayName = location,
            actorId = owner.PersistentId,
            actorDisplayName = owner.DisplayName,
            counterpartyId = counterpartyId,
            counterpartyDisplayName = counterpartyDisplayName,
            usedObjectId = source.Domain == CharacterNarrativeDomain.Work
                ? "facility:replay:" + target.id
                : source.Domain is CharacterNarrativeDomain.Expedition
                    or CharacterNarrativeDomain.Need
                    or CharacterNarrativeDomain.Mood
                    ? source.FactId
                    : string.Empty,
            usedObjectDisplayName = source.Domain is CharacterNarrativeDomain.Work
                or CharacterNarrativeDomain.Expedition
                or CharacterNarrativeDomain.Need
                or CharacterNarrativeDomain.Mood
                    ? CharacterContextObjectName(source)
                    : string.Empty,
            resultDetail = source.Outcome
        };
    }

    private static string CharacterContextObjectName(CharacterGameplayNarrativeSource source)
    {
        if (source.FactId.StartsWith("node:", StringComparison.Ordinal))
            return source.FactId.Substring("node:".Length) + " 경로 지점";
        if (source.FactId.Contains("research", StringComparison.Ordinal)) return "연구 시설";
        if (source.FactId.Contains("repair", StringComparison.Ordinal)) return "수리 대상 시설";
        if (source.Domain == CharacterNarrativeDomain.Need) return "생존 상태";
        if (source.Domain == CharacterNarrativeDomain.Mood) return "기분 변화 원인";
        return source.Domain.ToString();
    }

    private static JObject ContextJson(GameplayNarrativeEventContext context)
    {
        GameplayNarrativeEventContext value = context?.Clone()
            ?? throw new InvalidOperationException("Presentation evidence lost its event context.");
        if (!value.HasNarrativeDetail
            || string.IsNullOrWhiteSpace(value.eventInstanceId)
            || string.IsNullOrWhiteSpace(value.chainId))
            throw new InvalidOperationException(
                "Presentation evidence event context is not narratively concrete.");
        return new JObject
        {
            ["eventInstanceId"] = value.eventInstanceId,
            ["chainId"] = value.chainId,
            ["sequence"] = value.sequence,
            ["occurredDay"] = value.occurredDay,
            ["locationId"] = value.locationId,
            ["locationDisplayName"] = value.locationDisplayName,
            ["actorId"] = value.actorId,
            ["actorDisplayName"] = value.actorDisplayName,
            ["counterpartyId"] = value.counterpartyId,
            ["counterpartyDisplayName"] = value.counterpartyDisplayName,
            ["usedObjectId"] = value.usedObjectId,
            ["usedObjectDisplayName"] = value.usedObjectDisplayName,
            ["resultDetail"] = value.resultDetail
        };
    }

    private static CharacterAcquiredTraitModuleSO ChooseTraitModuleFromEvidence(
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        IReadOnlyList<NarrativeFormulaEvidence> evidence,
        int rowIndex)
    {
        var scored = modules.Select(module => new
            {
                Module = module,
                Score = CountAffinityMatches(module.RequireFormulaDescriptor(), evidence)
            })
            .OrderByDescending(value => value.Score)
            .ThenBy(value => value.Module.ModuleId, StringComparer.Ordinal)
            .ToArray();
        double best = scored[0].Score;
        var tied = scored.Where(value => Math.Abs(value.Score - best) < 0.0001d).ToArray();
        return tied[Math.Abs(rowIndex) % tied.Length].Module;
    }

    private static double CountAffinityMatches(
        NarrativeFormulaCapabilityDescriptor descriptor,
        IEnumerable<NarrativeFormulaEvidence> evidence)
    {
        HashSet<string> keys = evidence.SelectMany(value => new[]
            {
                value.EventGroupKey, value.ActionKey, value.RelationshipKey, value.DomainKey
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        return descriptor.AffinityKeys.Count(keys.Contains);
    }

    private static EvidenceBundle BuildEquipmentEvidence(
        EquipmentGameplayFormulaReplayResult replay)
    {
        List<NarrativeFormulaEvidence> formula = new();
        JArray facts = new JArray();
        JArray provenance = new JArray();
        EquipmentEvolutionFormulaEvidenceRecord[] ordered = replay.State.formulaEvidence
                     .OrderByDescending(value => Math.Abs(value.originalEvent?.amount ?? 0f))
                     .ThenBy(value => value.originalEvent?.sequence ?? 0L)
                     .ThenBy(value => value.evidenceId, StringComparer.Ordinal)
                     .ToArray();
        foreach (EquipmentEvolutionFormulaEvidenceRecord record in ordered)
        {
            formula.Add(new NarrativeFormulaEvidence(
                record.evidenceId, record.eventGroupKey, record.actionKey,
                record.relationshipKey, record.domainKey, record.attainedMilestoneCount,
                record.importancePoints, record.influenceUseCount));
        }
        foreach (EquipmentEvolutionFormulaEvidenceRecord record in ordered
                     .Take(4)
                     .OrderBy(value => value.originalEvent?.sequence ?? 0L)
                     .ThenBy(value => value.originalEvent?.narrativeContext?.occurredDay ?? 0)
                     .ThenBy(value => value.evidenceId, StringComparer.Ordinal))
        {
            EquipmentGameplayUsageSource source = replay.SourceByEvidenceId[record.evidenceId];
            string text = source.FormatPublicFact(record.originalEvent.narrativeContext);
            facts.Add(new JObject { ["factId"] = record.evidenceId, ["text"] = text });
            provenance.Add(BuildProvenance(source, record.evidenceId, new JObject
            {
                ["eventId"] = record.originalEvent.eventId,
                ["outcomeId"] = record.originalEvent.outcomeId,
                ["actorId"] = record.originalEvent.actorId,
                ["targetId"] = record.originalEvent.targetId,
                ["amount"] = record.originalEvent.amount,
                ["repeatCount"] = record.originalEvent.repeatCount,
                ["attainedMilestoneCount"] = record.attainedMilestoneCount,
                ["importancePoints"] = record.importancePoints,
                ["influenceUseCount"] = record.influenceUseCount,
                ["historicalEvidenceKind"] = record.originalEvent.historicalEvidenceKind.ToString(),
                ["sourceTags"] = new JArray(record.originalEvent.sourceTags)
                , ["eventContext"] = ContextJson(record.originalEvent.narrativeContext)
            }));
        }
        return new EvidenceBundle(formula, facts, provenance,
            replay.State.formulaEvidence.Any(value => value?.originalEvent != null
                && NarrativeFormulaNegativeEvidence.Matches(
                    value.originalEvent.eventId, value.originalEvent.outcomeId)));
    }

    private static string FormatEquipmentPublicFact(
        EquipmentGameplayUsageSource source,
        float amount)
    {
        if (amount <= 0f)
            return source.FormatPublicFact(1);
        string value = amount.ToString("0.##", CultureInfo.InvariantCulture);
        return source.EventId switch
        {
            "combat:hit" => source.FormatPublicFact(1) + " 이 공격으로 "
                + value + "만큼의 피해를 입혔다.",
            "combat:block" => "방패로 공격을 막아 " + value + "만큼의 피해를 차단했다.",
            "combat:absorb" => "방어구가 " + value + "만큼의 피해를 흡수했다.",
            _ => source.FormatPublicFact(1)
        };
    }

    private static IReadOnlyDictionary<string, FacilityGameplayUsageSource> BuildFacilityUsageLedger(
        FacilityEvolutionState state,
        int rowIndex,
        int replayVariationIndex,
        string facilityDisplayName,
        FacilityReplayPlan plan)
    {
        if (state == null || plan == null)
            throw new ArgumentNullException("Facility usage replay requires state and source plan.");
        FacilityGameplayUsageSource visit = plan.DirectReplaySources.SingleOrDefault(value =>
            string.Equals(value.SourceId, "facility:visit", StringComparison.Ordinal));
        if (visit == null)
            throw new InvalidOperationException(
                "Facility source plan must include a replayable completed-visit producer.");
        FacilityGameplayUsageSource[] replaySequence = BuildFacilityReplaySequence(
            plan, visit, replayVariationIndex);
        UsageLedgerCompactor compactor = new UsageLedgerCompactor();
        Dictionary<string, FacilityGameplayUsageSource> sourceByEvidence = new(StringComparer.Ordinal);
        for (int factIndex = 0; factIndex < replaySequence.Length; factIndex++)
        {
            FacilityGameplayUsageSource source = replaySequence[factIndex];
            float amount = FacilityAmount(source.SourceId, rowIndex, factIndex);
            bool visitorUse = source.SourceTags.Contains("visit", StringComparer.Ordinal);
            bool customerEvent = string.Equals(source.SourceId, "facility:revenue",
                    StringComparison.Ordinal)
                || string.Equals(source.SourceId, "facility:stock-consumed",
                    StringComparison.Ordinal)
                || string.Equals(source.SourceId, "facility:crime",
                    StringComparison.Ordinal);
            bool intruderEvent = string.Equals(source.SourceId, "facility:invasion-damage",
                StringComparison.Ordinal);
            bool defenseEvent = string.Equals(source.SourceId, "facility:defense-triggered",
                StringComparison.Ordinal);
            string actorId = visitorUse || customerEvent || intruderEvent
                ? "character:facility-visitor:" + rowIndex.ToString("D5", CultureInfo.InvariantCulture)
                    + ":" + factIndex.ToString("D2", CultureInfo.InvariantCulture)
                : defenseEvent
                    ? state.facilityPersistentId
                    : "character:facility-operator:"
                        + rowIndex.ToString("D5", CultureInfo.InvariantCulture)
                        + ":" + factIndex.ToString("D2", CultureInfo.InvariantCulture);
            string actorDisplayName = visitorUse || customerEvent
                ? CharacterNames[(rowIndex + factIndex) % CharacterNames.Length]
                : intruderEvent
                    ? "침공자 " + (1 + (rowIndex + factIndex) % 11)
                        .ToString(CultureInfo.InvariantCulture)
                    : defenseEvent
                        ? facilityDisplayName
                        : "시설 운영자 "
                    + (1 + (rowIndex + factIndex) % 11).ToString(CultureInfo.InvariantCulture);
            string counterpartyId = defenseEvent
                ? "character:facility-intruder:" + rowIndex.ToString("D5",
                    CultureInfo.InvariantCulture) + ":" + factIndex.ToString("D2",
                    CultureInfo.InvariantCulture)
                : string.Empty;
            string counterpartyDisplayName = defenseEvent
                ? "침입자 " + (1 + (rowIndex + factIndex) % 11)
                    .ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            UsageLedgerEvent recorded = compactor.Record(
                state.usageLedger, source.EventId, amount,
                actorId,
                state.facilityPersistentId,
                source.SourceTags,
                generation: 0,
                repeatCount: 1,
                narrativeContext: new GameplayNarrativeEventContext
                {
                    eventInstanceId = $"facility-replay:{rowIndex}:{factIndex}",
                    chainId = $"facility-replay:{rowIndex}",
                    locationId = state.facilityPersistentId,
                    locationDisplayName = "던전 시설 구역 " + (1 + rowIndex % 7).ToString(CultureInfo.InvariantCulture),
                    actorId = actorId,
                    actorDisplayName = actorDisplayName,
                    counterpartyId = counterpartyId,
                    counterpartyDisplayName = counterpartyDisplayName,
                    usedObjectId = state.facilityPersistentId,
                    usedObjectDisplayName = facilityDisplayName,
                    resultDetail = FacilityReplayResultDetail(source, amount),
                    occurredDay = 1 + checked(rowIndex * replaySequence.Length) + factIndex
                });
            sourceByEvidence.Add(recorded.evidenceId, source);
        }
        return sourceByEvidence;
    }

    private static FacilityGameplayUsageSource[] BuildFacilityReplaySequence(
        FacilityReplayPlan plan,
        FacilityGameplayUsageSource visit,
        int replayVariationIndex)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (visit == null) throw new ArgumentNullException(nameof(visit));
        if (replayVariationIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(replayVariationIndex));
        FacilityGameplayUsageSource[] sequence = Enumerable.Repeat(visit, 6).ToArray();
        FacilityGameplayUsageSource[] alternatives = plan.DirectReplaySources
            .Where(value => value != null
                && !ReferenceEquals(value, visit))
            .OrderBy(value => value.SourceId, StringComparer.Ordinal)
            .ToArray();
        if (alternatives.Length == 0)
            return sequence;

        int selectedAlternativeCount = Math.Min(3, alternatives.Length);
        int sourceRotation = replayVariationIndex % alternatives.Length;
        int slotRotation = replayVariationIndex % 3;
        for (int alternativeIndex = 0;
             alternativeIndex < selectedAlternativeCount;
             alternativeIndex++)
        {
            int slot = 1 + 2 * ((slotRotation + alternativeIndex) % 3);
            sequence[slot] = alternatives[
                (sourceRotation + alternativeIndex) % alternatives.Length];
        }
        return sequence;
    }

    private static FacilityEvolutionFormulaPresentationPendingSnapshot PrepareFacilityStrengthBand(
        FacilityEvolutionState state,
        FacilityEvolutionRecipeSO recipe,
        PilotStrengthBand strengthBand)
    {
        if (!FacilityFormulaEvolutionAuthority.TryPrepare(
                state, recipe, out _, out string seedFailureReason))
            throw new InvalidOperationException(
                "Gameplay-backed facility formula seed preparation failed: " + seedFailureReason);
        foreach (FacilityFormulaEvidenceRecord record in state.formulaEvidence.Where(value => value != null))
            record.influenceUseCount = strengthBand.InfluenceUseCount;
        if (!FacilityFormulaEvolutionAuthority.TryPrepare(
                state, recipe, out FacilityEvolutionFormulaPresentationPendingSnapshot pending,
                out string failureReason))
            throw new InvalidOperationException(
                "Gameplay-backed facility formula preparation failed: " + failureReason);
        return pending;
    }

    private static float FacilityAmount(string sourceId, int rowIndex, int factIndex) => sourceId switch
    {
        "facility:revenue" => 10f + ((rowIndex * 13 + factIndex * 7) % 91),
        "facility:stock-consumed" => 1f + ((rowIndex + factIndex) % 8),
        "facility:defense-triggered" => 2f + ((rowIndex * 5 + factIndex * 3) % 24),
        "facility:clean-service-day" => 3f + ((rowIndex + factIndex) % 12),
        _ => 1f
    };

    private static string FacilityReplayResultDetail(
        FacilityGameplayUsageSource source,
        float amount)
    {
        string value = amount.ToString("0.##", CultureInfo.InvariantCulture);
        return source.SourceId switch
        {
            "facility:visit" => "완료된 시설 이용 1회",
            "facility:revenue" => "수익 " + value,
            "facility:stock-consumed" => "Food 재고 " + value + "개 소비",
            "facility:crime" => "범죄 사건 1건",
            "facility:restock-failed" => "재입고 실패 1건",
            "facility:defense-triggered" => "방어 발동, 피해 " + value,
            "facility:invasion-damage" => "침공 피해 사건 1건",
            "facility:clean-service-day" => "방문 " + value + "회, 사고 0건",
            _ => throw new InvalidOperationException(
                "Unsupported facility replay result source: " + source.SourceId)
        };
    }

    private static EvidenceBundle BuildFacilityEvidence(
        FacilityEvolutionState state,
        EvolutionNode node,
        IReadOnlyDictionary<string, FacilityGameplayUsageSource> sourceByEvidence,
        int rowIndex,
        FacilityReplayPlan plan)
    {
        List<NarrativeFormulaEvidence> formula = new();
        JArray facts = new JArray();
        JArray provenance = new JArray();
        FacilityFormulaEvidenceRecord[] ordered = node.evidenceIds
            .Select(evidenceId => state.formulaEvidence.Single(value => value != null
                && string.Equals(value.evidenceId, evidenceId, StringComparison.Ordinal)))
            .OrderBy(value => value.originalEvent?.sequence ?? 0L)
            .ThenBy(value => value.evidenceId, StringComparer.Ordinal)
            .ToArray();
        int visibleCount = Math.Min(4, ordered.Length);
        int start = ordered.Length == 0
            ? 0
            : (int)((long)Math.Abs(rowIndex) * 3L % ordered.Length);
        HashSet<string> visibleEvidenceIds = Enumerable.Range(0, visibleCount)
            .Select(offset => ordered[(start + offset) % ordered.Length].evidenceId)
            .ToHashSet(StringComparer.Ordinal);
        for (int recordIndex = 0; recordIndex < ordered.Length; recordIndex++)
        {
            FacilityFormulaEvidenceRecord record = ordered[recordIndex];
            UsageLedgerEvent original = record.originalEvent
                ?? throw new InvalidOperationException("Facility formula evidence lost its original gameplay event.");
            FacilityGameplayUsageSource source = sourceByEvidence[original.evidenceId];
            bool sourceApplicable = source.AppliesTo(plan.SourceDefinition,
                out string applicabilityFailure);
            bool replaySupported = source.IsDirectLedgerReplaySupported(out string replayFailure);
            if (!sourceApplicable || !replaySupported)
            {
                throw new InvalidOperationException(
                    "Facility evidence escaped source applicability: " + source.SourceId + "; "
                    + (sourceApplicable ? replayFailure : applicabilityFailure));
            }
            formula.Add(new NarrativeFormulaEvidence(
                record.evidenceId, record.eventGroupKey, record.actionKey,
                record.relationshipKey, record.domainKey, record.attainedMilestoneCount,
                record.importancePoints, record.influenceUseCount));
            if (!visibleEvidenceIds.Contains(record.evidenceId))
                continue;
            string text = source.FormatPublicFact(original.narrativeContext);
            facts.Add(new JObject { ["factId"] = record.evidenceId, ["text"] = text });
            provenance.Add(BuildProvenance(source, record.evidenceId, new JObject
            {
                ["usageEvidenceId"] = original.evidenceId,
                ["eventId"] = original.eventId,
                ["actorId"] = original.actorId,
                ["targetId"] = original.targetId,
                ["amount"] = original.amount,
                ["repeatCount"] = original.repeatCount,
                ["attainedMilestoneCount"] = record.attainedMilestoneCount,
                ["importancePoints"] = record.importancePoints,
                ["influenceUseCount"] = record.influenceUseCount,
                ["sourceTags"] = new JArray(original.sourceTags)
                , ["eventContext"] = ContextJson(original.narrativeContext)
            }, plan.SourceDefinition));
        }
        return new EvidenceBundle(formula, facts, provenance,
            node.drawbackEvidenceQualified);
    }

    private static string FormatFacilityPublicFact(FacilityGameplayUsageSource source, float amount)
    {
        string text = source.FormatPublicFact(1);
        string value = amount.ToString("0.##", CultureInfo.InvariantCulture);
        return source.SourceId switch
        {
            "facility:revenue" => text + " 장부에 기록된 수익은 " + value + "이다.",
            "facility:stock-consumed" => text + " 소비된 수량은 " + value + "개다.",
            "facility:defense-triggered" => text + " 입힌 피해는 " + value + "이다.",
            "facility:clean-service-day" => text + " 이날 방문객은 " + value + "명이다.",
            _ => text
        };
    }

    private static JObject BuildProvenance(
        GameplayNarrativeSourceDefinition source,
        string evidenceId,
        JObject recordedTuple,
        BuildingSO facilitySourceDefinition = null)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string absolute = Path.Combine(projectRoot,
            source.ProducerPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Gameplay narrative producer is missing.", absolute);
        string producerText = File.ReadAllText(absolute);
        if (producerText.IndexOf(source.ProducerProbe, StringComparison.Ordinal) < 0)
            throw new InvalidOperationException(
                "Gameplay narrative producer probe is no longer present: " + source.SourceId);
        const string definitionPath =
            "Assets/Scripts/Services/Character/Core/GameplayNarrativeSourceCatalog.cs";
        JObject value = new JObject
        {
            ["evidenceId"] = evidenceId,
            ["gameplayReachable"] = true,
            ["sourceDefinitionId"] = source.SourceId,
            ["producerSymbol"] = source.ProducerSymbol,
            ["producerPath"] = source.ProducerPath,
            ["producerSha256"] = Sha256File(absolute),
            ["definitionPath"] = definitionPath,
            ["definitionSha256"] = Sha256File(Path.Combine(projectRoot,
                definitionPath.Replace('/', Path.DirectorySeparatorChar))),
            ["recordedTuple"] = recordedTuple
        };
        if (source is FacilityGameplayUsageSource facilitySource)
        {
            if (facilitySourceDefinition == null)
                throw new InvalidOperationException(
                    "Facility evidence provenance requires its actual source definition.");
            bool sourceApplicable = facilitySource.AppliesTo(facilitySourceDefinition,
                out string applicabilityFailure);
            bool replaySupported = facilitySource.IsDirectLedgerReplaySupported(
                out string replayFailure);
            if (!sourceApplicable || !replaySupported)
            {
                throw new InvalidOperationException(
                    "Facility provenance escaped source applicability: "
                    + (sourceApplicable ? replayFailure : applicabilityFailure));
            }
            value["sourceApplicability"] = facilitySource.Applicability.ToString();
            value["sourceTargetDefinitionId"] = BuildingDefinitionIdentity.Resolve(
                facilitySourceDefinition);
            value["sourceTargetRuntimeArchetype"] = facilitySourceDefinition
                .runtimeArchetype.ToString();
        }
        else if (facilitySourceDefinition != null)
        {
            throw new InvalidOperationException(
                "Only facility evidence may include a facility source definition.");
        }
        return value;
    }

    private static JObject BuildRow(
        string profileId,
        int formulaVersion,
        string catalogSha256,
        string gameCommit,
        string targetId,
        string presentationId,
        string targetDisplayName,
        string mechanicalDescription,
        EvidenceBundle evidence) => new JObject
    {
        ["schemaVersion"] = 3,
        ["formulaVersion"] = formulaVersion,
        ["catalogSha256"] = catalogSha256,
        ["gameCommit"] = gameCommit,
        ["profileId"] = profileId,
        ["targetPersistentId"] = targetId,
        ["evidenceProvenance"] = evidence.Provenance.DeepClone(),
        ["presentationRequest"] = new JObject
        {
            ["presentationId"] = presentationId,
            ["targetDisplayName"] = targetDisplayName,
            ["mechanicalDescription"] = mechanicalDescription,
            ["evidenceFactIds"] = new JArray(evidence.PublicFacts
                .OfType<JObject>()
                .Select(value => (string)value["factId"])),
            ["publicFacts"] = evidence.PublicFacts.DeepClone(),
            ["style"] = new JObject
            {
                ["genre"] = "fantasy-wuxia",
                ["nameMaxLength"] = 14,
                ["flavorMaxLength"] = 180
            }
        }
    };

    private static JObject ModuleOffer(
        string moduleId,
        string polarity,
        string semanticDescription) => new JObject
    {
        ["moduleId"] = moduleId,
        ["polarity"] = polarity,
        ["semanticDescription"] = semanticDescription
    };

    private static string DescribeSkillDrawback(
        CharacterSkillDrawbackCapabilityDefinition drawback)
    {
        NarrativeFormulaCapabilityDescriptor descriptor =
            drawback.RequireFormulaDescriptor();
        return descriptor.ForbiddenSynergies.Count == 0
            ? drawback.DisplayName + ": " + drawback.Description
            : drawback.DisplayName + ": " + drawback.Description
                + "; 함께 선택 금지 이로운 기능="
                + string.Join(",", descriptor.ForbiddenSynergies);
    }

    private static string Polarity(EvolutionModuleOfferPolarity value) => value switch
    {
        EvolutionModuleOfferPolarity.Positive => "positive",
        EvolutionModuleOfferPolarity.Drawback => "drawback",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string TraitModuleOfferDescription(
        CharacterAcquiredTraitModuleSO module)
    {
        return CharacterAcquiredTraitFormulaGeneration
            .FormatModuleOfferDescription(module);
    }

    private static JObject BuildSelectionRow(
        string profileId,
        int formulaVersion,
        string catalogSha256,
        string gameCommit,
        string targetId,
        string selectionId,
        string targetDisplayName,
        IEnumerable<JObject> moduleOffers,
        EvidenceBundle evidence,
        int maximumPositiveModules,
        int maximumDrawbackModules,
        PilotStrengthBand strengthBand,
        JObject skillContext = null,
        JObject authoritativeSkillRule = null,
        JObject authoritativeEquipmentTarget = null)
    {
        JObject request = new()
        {
            ["selectionId"] = selectionId,
            ["targetDisplayName"] = targetDisplayName,
            ["moduleOffers"] = new JArray((moduleOffers ?? Array.Empty<JObject>())
                .OrderBy(value => (string)value["moduleId"], StringComparer.Ordinal)
                .Select(value => value.DeepClone())),
            ["maximumPositiveModules"] = maximumPositiveModules,
            ["maximumDrawbackModules"] = maximumDrawbackModules,
            ["evidenceFactIds"] = new JArray(evidence.PublicFacts
                .OfType<JObject>()
                .Select(value => (string)value["factId"])),
            ["publicFacts"] = evidence.PublicFacts.DeepClone(),
            ["style"] = new JObject
            {
                ["genre"] = "fantasy-wuxia",
                ["nameMaxLength"] = 32,
                ["flavorMaxLength"] = 180
            }
        };
        if (skillContext != null)
            request["skillContext"] = skillContext.DeepClone();
        JObject row = new()
        {
        ["schemaVersion"] = 6,
        ["formulaVersion"] = formulaVersion,
        ["catalogSha256"] = catalogSha256,
        ["gameCommit"] = gameCommit,
        ["profileId"] = profileId,
        ["targetPersistentId"] = targetId,
        ["strengthBand"] = strengthBand.ToJson(),
        ["evidenceProvenance"] = evidence.Provenance.DeepClone(),
        ["moduleSelectionRequest"] = request
        };
        if (authoritativeSkillRule != null)
            row["authoritativeSkillRule"] = authoritativeSkillRule.DeepClone();
        if (authoritativeEquipmentTarget != null)
            row["authoritativeEquipmentTarget"] = authoritativeEquipmentTarget.DeepClone();
        return row;
    }

    private static JObject EquipmentTargetJson(CombatEquipmentDefinitionSO definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        string definitionId = definition.EquipmentId;
        if (string.IsNullOrWhiteSpace(definitionId))
            throw new InvalidOperationException(
                "Equipment formula target definition requires a canonical ID.");
        if (!Enum.IsDefined(typeof(CombatEquipmentKind), definition.Kind))
            throw new InvalidOperationException(
                "Equipment formula target definition has an unsupported kind.");
        return new JObject
        {
            ["definitionId"] = definitionId,
            ["kind"] = definition.Kind.ToString()
        };
    }

    private static JObject SkillContextJson(
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind) => new()
    {
        ["skillKind"] = kind.ToString(),
        ["trigger"] = rule.trigger.ToString(),
        ["target"] = rule.target.ToString(),
        ["targetingMode"] = rule.targetingMode.ToString(),
        ["effectArea"] = rule.effectArea.ToString(),
        ["areaSize"] = rule.areaSize,
        ["ultimateDomain"] = rule.ultimateDomain.ToString(),
        ["semanticDescription"] = CharacterSkillPresentationSemantics
            .DescribeContext(rule, kind)
    };

    private static JObject SkillRuleJson(CharacterSkillCandidateRule rule)
    {
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        return new JObject
        {
            ["ruleId"] = rule.ruleId,
            ["rarity"] = rule.rarity.ToString(),
            ["budget"] = rule.budget,
            ["trigger"] = rule.trigger.ToString(),
            ["target"] = rule.target.ToString(),
            ["targetingMode"] = rule.targetingMode.ToString(),
            ["effectArea"] = rule.effectArea.ToString(),
            ["areaSize"] = rule.areaSize,
            ["ultimateDomain"] = rule.ultimateDomain.ToString(),
            ["cooldownTurns"] = rule.cooldownTurns,
            ["manualDurationHours"] = rule.manualDurationHours,
            ["manualCooldownDays"] = rule.manualCooldownDays,
            ["mechanicalPolicySource"] = rule.mechanicalPolicySource.ToString(),
            ["usableFrom"] = rule.usableFrom.ToString(),
            ["targetPositions"] = rule.targetPositions.ToString(),
            ["allowedModuleIds"] = new JArray((rule.allowedModuleIds
                    ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal)),
            ["allowedVariantIds"] = new JArray((rule.allowedVariantIds
                    ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal))
        };
    }

    private static CharacterSkillCandidateRule SkillRuleFromJson(JObject value)
    {
        string[] expectedKeys =
        {
            "allowedModuleIds", "allowedVariantIds", "areaSize", "budget", "cooldownTurns",
            "effectArea", "manualCooldownDays", "manualDurationHours", "mechanicalPolicySource",
            "rarity", "ruleId", "target", "targetPositions", "targetingMode", "trigger",
            "ultimateDomain", "usableFrom"
        };
        if (value.Properties().Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(expectedKeys, StringComparer.Ordinal) == false)
        {
            throw new InvalidOperationException(
                "Authoritative CharacterSkill pilot rule escaped its closed source contract.");
        }
        CharacterSkillCandidateRule rule = new()
        {
            ruleId = RequireString(value, "ruleId"),
            rarity = RequireEnum<CharacterSkillRarity>(value, "rarity"),
            budget = (int?)value["budget"] ?? -1,
            trigger = RequireEnum<CharacterSkillTrigger>(value, "trigger"),
            target = RequireEnum<CharacterSkillTarget>(value, "target"),
            targetingMode = RequireEnum<CharacterSkillTargetingMode>(value, "targetingMode"),
            effectArea = RequireEnum<CharacterSkillEffectArea>(value, "effectArea"),
            areaSize = (int?)value["areaSize"] ?? -1,
            ultimateDomain = RequireEnum<CharacterUltimateDomain>(value, "ultimateDomain"),
            cooldownTurns = (int?)value["cooldownTurns"] ?? -1,
            manualDurationHours = (int?)value["manualDurationHours"] ?? -1,
            manualCooldownDays = (int?)value["manualCooldownDays"] ?? -1,
            mechanicalPolicySource = RequireEnum<CharacterSkillMechanicalPolicySource>(
                value, "mechanicalPolicySource"),
            usableFrom = RequireFormationMask(value, "usableFrom"),
            targetPositions = RequireFormationMask(value, "targetPositions"),
            allowedModuleIds = RequireStringArray(value, "allowedModuleIds").ToList(),
            allowedVariantIds = RequireStringArray(value, "allowedVariantIds").ToList()
        };
        if (rule.budget < 1 || rule.cooldownTurns < 0 || rule.manualDurationHours < 0
            || rule.manualCooldownDays < 0 || rule.allowedModuleIds.Count == 0
            || rule.allowedModuleIds.Distinct(StringComparer.Ordinal).Count()
                != rule.allowedModuleIds.Count
            || rule.allowedVariantIds.Distinct(StringComparer.Ordinal).Count()
                != rule.allowedVariantIds.Count)
        {
            throw new InvalidOperationException(
                "Authoritative CharacterSkill pilot rule has invalid numeric or collection values.");
        }
        CharacterSkillAreaRules.RequireValid(
            rule.targetingMode, rule.effectArea, rule.areaSize);
        if (rule.trigger == CharacterSkillTrigger.ManualWork
            && (rule.manualDurationHours < 1 || rule.manualCooldownDays < 1))
        {
            throw new InvalidOperationException(
                "ManualWork pilot rule must preserve authored positive duration and day cooldown.");
        }
        return rule;
    }

    private static TEnum RequireEnum<TEnum>(JObject value, string key)
        where TEnum : struct, Enum
    {
        string text = RequireString(value, key);
        if (!Enum.TryParse(text, ignoreCase: false, out TEnum parsed)
            || !Enum.IsDefined(typeof(TEnum), parsed))
        {
            throw new InvalidOperationException(
                key + " must be a defined " + typeof(TEnum).Name + " value.");
        }
        return parsed;
    }

    private static OffenseFormationMask RequireFormationMask(JObject value, string key)
    {
        string text = RequireString(value, key);
        if (!Enum.TryParse(text, ignoreCase: false, out OffenseFormationMask parsed)
            || (parsed & ~OffenseFormationMask.Any) != OffenseFormationMask.None)
        {
            throw new InvalidOperationException(
                key + " must be a canonical supported formation mask.");
        }
        return parsed;
    }

    private static void ValidateSkillContext(
        JObject context,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind)
    {
        JObject expected = SkillContextJson(rule, kind);
        if (!JToken.DeepEquals(expected, context))
        {
            throw new InvalidOperationException(
                "CharacterSkill request context diverged from its authoritative rule snapshot.");
        }
    }

    private static void RequireSkillPilotCoverage(IEnumerable<JObject> rows)
    {
        JObject[] contexts = (rows ?? Array.Empty<JObject>())
            .Select(row => RequireObject(
                RequireObject(row["moduleSelectionRequest"], "moduleSelectionRequest")["skillContext"],
                "skillContext"))
            .ToArray();
        bool Has(string key, string value) => contexts.Any(context =>
            string.Equals((string)context[key], value, StringComparison.Ordinal));
        if (!Has("skillKind", CharacterSkillKind.Passive.ToString())
            || !Has("skillKind", CharacterSkillKind.Ultimate.ToString())
            || !Has("trigger", CharacterSkillTrigger.ManualWork.ToString())
            || !Has("target", CharacterSkillTarget.Self.ToString())
            || !Has("target", CharacterSkillTarget.Ally.ToString())
            || !Has("target", CharacterSkillTarget.Enemy.ToString())
            || !Has("targetingMode", CharacterSkillTargetingMode.DeterministicRandom.ToString())
            || !Has("effectArea", CharacterSkillEffectArea.Single.ToString())
            || !Has("effectArea", CharacterSkillEffectArea.Room.ToString())
            || !Has("effectArea", CharacterSkillEffectArea.Dungeon.ToString())
            || !contexts.Any(context => (string)context["effectArea"] == CharacterSkillEffectArea.Square.ToString()
                && (int?)context["areaSize"] == 3)
            || !contexts.Any(context => (string)context["effectArea"] == CharacterSkillEffectArea.Square.ToString()
                && (int?)context["areaSize"] == 5)
            || !contexts.Any(context => (string)context["effectArea"] == CharacterSkillEffectArea.Square.ToString()
                && (int?)context["areaSize"] == 7))
        {
            throw new InvalidOperationException(
                "CharacterSkill pilot does not cover the required legal kind, target, random, and area axes.");
        }
    }

    private static string FormatParameters(NarrativeFormulaCandidate candidate) => string.Join(", ",
        candidate.Parameters.Select(value => value.ParameterId + "="
            + candidate.Descriptor.RequireRange(value.ParameterId).Format(value.Units)));

    private static string FormatTraitMechanics(
        CharacterAcquiredTraitModuleSO module,
        NarrativeFormulaCandidate candidate)
    {
        NarrativeFormulaParameterValue magnitude = candidate.Parameters.Single(value =>
            string.Equals(value.ParameterId, NarrativeFormulaParameterIds.Magnitude, StringComparison.Ordinal));
        float potencyScale = (float)candidate.Descriptor.RequireRange(
            NarrativeFormulaParameterIds.Magnitude).ToDecimal(magnitude.Units);
        string effects = string.Join(", ", module.Effects.Where(value => value != null)
            .OrderBy(value => value.bindingId, StringComparer.Ordinal)
            .Select(value =>
            {
                double scaled = value.definition.Operation == GameplayEffectOperation.Multiply
                    ? 1d + (value.value - 1d) * potencyScale
                    : value.value * potencyScale;
                string condition = value.condition == null
                    ? string.Empty : " if " + value.condition.ConditionId;
                return value.definition.TargetId + " " + value.definition.Operation + "="
                    + scaled.ToString("0.####", CultureInfo.InvariantCulture) + condition;
            }));
        return module.DisplayName + ": " + effects + "; potencyScale="
            + candidate.Descriptor.RequireRange(NarrativeFormulaParameterIds.Magnitude).Format(magnitude.Units)
            + (candidate.DrawbackId.Length == 0 ? string.Empty
                : "; 대가=" + candidate.DrawbackId + "; drawbackCredit="
                    + candidate.DrawbackCredit.ToString(CultureInfo.InvariantCulture))
            + "; positiveCost=" + candidate.PositiveCost.ToString(CultureInfo.InvariantCulture)
            + "; netCost=" + candidate.CalculatedCost.ToString(CultureInfo.InvariantCulture);
    }

    private static string BuildCombinedCatalogSha256(CatalogContext catalogs)
    {
        StringBuilder canonical = new StringBuilder();
        canonical.Append("characterSkill=").Append(catalogs.Skills.formulaPolicy.RequireCatalogSha256()).Append('\n');
        canonical.Append("acquiredTrait=").Append(catalogs.TraitSettings.FormulaPolicy.RequireCatalogSha256()).Append('\n');
        canonical.Append("equipment=").Append(catalogs.Equipment.formulaPolicy.RequireCatalogSha256()).Append('\n');
        foreach (FacilityEvolutionRecipeSO recipe in catalogs.Facilities.OrderBy(value => value.EffectiveId, StringComparer.Ordinal))
            canonical.Append("facility:").Append(recipe.EffectiveId).Append('=')
                .Append(recipe.formulaPolicy.RequireCatalogSha256()).Append('\n');
        return "sha256:" + Sha256Text(canonical.ToString());
    }

    private static string PresentationId(string domain, string targetId, string signature) =>
        "presentation:" + domain + ":" + Sha256Text(targetId + "\n" + signature);

    private static bool ValidateFacilityProvenance(JObject value)
    {
        string sourceId = (string)value?["sourceDefinitionId"] ?? string.Empty;
        string applicabilityText = (string)value?["sourceApplicability"] ?? string.Empty;
        string definitionId = (string)value?["sourceTargetDefinitionId"] ?? string.Empty;
        string archetypeText = (string)value?["sourceTargetRuntimeArchetype"] ?? string.Empty;
        if (sourceId.Length == 0 || definitionId.Length == 0
            || !Enum.TryParse(applicabilityText, ignoreCase: false,
                out FacilityGameplayUsageApplicability applicability)
            || !Enum.IsDefined(typeof(FacilityGameplayUsageApplicability), applicability)
            || !Enum.TryParse(archetypeText, ignoreCase: false,
                out BuildingRuntimeArchetypeKind archetype)
            || !archetype.IsDefined())
        {
            return false;
        }

        FacilityGameplayUsageSource source;
        try
        {
            source = GameplayNarrativeSourceCatalog.RequireFacility(sourceId);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        return source.Applicability == applicability
            && source.IsDirectLedgerReplaySupported(out _);
    }

    private static bool ValidateEquipmentProvenance(
        JObject value,
        CombatEquipmentKind kind,
        string targetPersistentId)
    {
        if (value == null || string.IsNullOrWhiteSpace(targetPersistentId))
            return false;
        EquipmentGameplayUsageSource source;
        try
        {
            source = GameplayNarrativeSourceCatalog.RequireEquipment(
                (string)value["sourceDefinitionId"] ?? string.Empty);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        if (!source.AppliesTo(kind)
            || value["recordedTuple"] is not JObject tuple
            || tuple["eventContext"] is not JObject context
            || !string.Equals((string)tuple["eventId"], source.EventId,
                StringComparison.Ordinal)
            || !string.Equals((string)tuple["outcomeId"], source.OutcomeId,
                StringComparison.Ordinal)
            || !string.Equals((string)tuple["historicalEvidenceKind"],
                source.HistoricalEvidenceKind.ToString(), StringComparison.Ordinal)
            || !string.Equals((string)tuple["targetId"], targetPersistentId,
                StringComparison.Ordinal)
            || !string.Equals((string)context["usedObjectId"], targetPersistentId,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace((string)context["usedObjectDisplayName"])
            || tuple["sourceTags"] is not JArray sourceTags)
        {
            return false;
        }
        string[] recordedTags = sourceTags.Values<string>().ToArray();
        return recordedTags.Length == source.SourceTags.Count
            && recordedTags.OrderBy(tag => tag, StringComparer.Ordinal)
                .SequenceEqual(source.SourceTags.OrderBy(tag => tag, StringComparer.Ordinal),
                    StringComparer.Ordinal);
    }

    private static bool ContainsMechanicalNumber(string value) =>
        (value ?? string.Empty).Any(character => character is >= '0' and <= '9'
            or >= '０' and <= '９' or '%' or '％');

    private static bool ContainsOfferMechanicalNumber(string value)
    {
        const string marker = "; 함께 선택 금지 이로운 기능=";
        string text = value ?? string.Empty;
        int markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return ContainsMechanicalNumber(text);
        if (text.IndexOf(
                marker,
                markerIndex + marker.Length,
                StringComparison.Ordinal) >= 0)
            return true;
        string metadata = text.Substring(markerIndex + marker.Length);
        string[] ids = metadata.Split(',');
        if (ids.Length == 0 || ids.Any(id => string.IsNullOrWhiteSpace(id)
                || !string.Equals(id, id.Trim(), StringComparison.Ordinal)
                || id.Any(character => !char.IsLetterOrDigit(character)
                    && character is not (':' or '-' or '_' or '.'))))
            return true;
        return ContainsMechanicalNumber(text.Substring(0, markerIndex));
    }

    private static void ValidateRows(IReadOnlyList<JObject> rows, int rowsPerProfile)
    {
        if (rows.Count != rowsPerProfile * 4)
            throw new InvalidOperationException("Formula presentation export has an unexpected row count.");
        string[] profiles =
        {
            "CharacterSkillModuleSelection",
            "AcquiredTraitModuleSelection",
            "EquipmentEvolutionModuleSelection",
            "FacilityEvolutionModuleSelection"
        };
        foreach (string profile in profiles)
            if (rows.Count(value => string.Equals((string)value["profileId"], profile, StringComparison.Ordinal))
                != rowsPerProfile)
                throw new InvalidOperationException("Formula presentation profile count is invalid: " + profile);
        JObject[] traitOffers = rows.Where(value => string.Equals(
                (string)value["profileId"], "AcquiredTraitModuleSelection", StringComparison.Ordinal))
            .SelectMany(value => ((JArray)value["moduleSelectionRequest"]?["moduleOffers"])
                .OfType<JObject>())
            .ToArray();
        if (!traitOffers.Any(value => ((string)value["semanticDescription"] ?? string.Empty)
                .Contains("특수 반응:", StringComparison.Ordinal)))
            throw new InvalidOperationException(
                "Acquired-trait pilot does not expose any authored special reaction.");
        foreach (string profile in profiles)
        {
            JObject[] profileRows = rows.Where(value => string.Equals(
                    (string)value["profileId"], profile, StringComparison.Ordinal)).ToArray();
            int minimumBandCount = rowsPerProfile / PilotStrengthBands.Length;
            int maximumBandCount = (rowsPerProfile + PilotStrengthBands.Length - 1)
                / PilotStrengthBands.Length;
            foreach (PilotStrengthBand band in PilotStrengthBands)
            {
                int count = profileRows.Count(value => string.Equals(
                    (string)value["strengthBand"]?["id"], band.Id, StringComparison.Ordinal));
                if (count < minimumBandCount || count > maximumBandCount)
                    throw new InvalidOperationException(
                        $"Formula presentation strength band distribution is invalid: {profile}/{band.Id}={count}.");
            }
        }
        string[] ids = rows.Select(value =>
            (string)value["moduleSelectionRequest"]?["selectionId"]).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != rows.Count)
            throw new InvalidOperationException("Formula pilot selection IDs must be unique and non-empty.");
        if (rows.Select(value => (string)value["catalogSha256"]).Distinct(StringComparer.Ordinal).Count() != 1
            || rows.Select(value => (string)value["gameCommit"]).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new InvalidOperationException("Formula pilot cannot mix catalog or commit identities.");
        int distinctNarrativeFamilies = rows.GroupBy(value => (string)value["profileId"], StringComparer.Ordinal)
            .Min(group => group.Select(value => CanonicalJson(value["moduleSelectionRequest"]?["publicFacts"]))
                .Distinct(StringComparer.Ordinal).Count());
        if (distinctNarrativeFamilies != rowsPerProfile)
            throw new InvalidOperationException("Formula presentation rows contain duplicate narrative families.");
        foreach (JObject row in rows)
        {
            if ((int?)row["schemaVersion"] != 6)
                throw new InvalidOperationException("Formula module-selection rows require schema v6.");
            JObject strengthBand = row["strengthBand"] as JObject;
            PilotStrengthBand expectedBand = PilotStrengthBands.SingleOrDefault(value =>
                string.Equals(value.Id, (string)strengthBand?["id"], StringComparison.Ordinal));
            if (strengthBand == null || expectedBand == null
                || !strengthBand.Properties().Select(value => value.Name)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(new[] { "displayName", "id", "influenceUseCount", "intendedRank" },
                        StringComparer.Ordinal)
                || (string)strengthBand["displayName"] != expectedBand.DisplayName
                || (int?)strengthBand["intendedRank"] != expectedBand.IntendedRank
                || (int?)strengthBand["influenceUseCount"] != expectedBand.InfluenceUseCount)
                throw new InvalidOperationException("Formula pilot strength band metadata is invalid.");
            JObject request = (JObject)row["moduleSelectionRequest"];
            bool characterSkill = string.Equals((string)row["profileId"],
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                StringComparison.Ordinal);
            bool equipment = string.Equals((string)row["profileId"],
                LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
                StringComparison.Ordinal);
            bool acquiredTrait = string.Equals((string)row["profileId"],
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                StringComparison.Ordinal);
            CombatEquipmentKind equipmentKind = default;
            if (characterSkill)
            {
                CharacterSkillCandidateRule rule = SkillRuleFromJson(
                    RequireObject(row["authoritativeSkillRule"], "authoritativeSkillRule"));
                CharacterSkillKind kind = RequireEnum<CharacterSkillKind>(
                    RequireObject(request["skillContext"], "skillContext"), "skillKind");
                ValidateSkillContext(
                    RequireObject(request["skillContext"], "skillContext"), rule, kind);
            }
            else if (request["skillContext"] != null || row["authoritativeSkillRule"] != null)
            {
                throw new InvalidOperationException(
                    "Only CharacterSkill source rows may carry a skill context or rule snapshot.");
            }
            if (equipment)
            {
                JObject equipmentTarget = row["authoritativeEquipmentTarget"] as JObject;
                if (!ValidateEquipmentTarget(equipmentTarget)
                    || !Enum.TryParse((string)equipmentTarget["kind"], ignoreCase: false,
                        out equipmentKind))
                    throw new InvalidOperationException(
                        "Equipment source rows require a closed concrete target snapshot.");
            }
            else if (row["authoritativeEquipmentTarget"] != null)
            {
                throw new InvalidOperationException(
                    "Only equipment source rows may carry a concrete equipment target snapshot.");
            }
            JObject[] offers = ((JArray)request["moduleOffers"]).OfType<JObject>().ToArray();
            int maximumPositive = (int)request["maximumPositiveModules"];
            int maximumDrawback = (int)request["maximumDrawbackModules"];
            if (offers.Length == 0
                || offers.Select(value => (string)value["moduleId"])
                    .Distinct(StringComparer.Ordinal).Count() != offers.Length
                || offers.Any(value => string.IsNullOrWhiteSpace((string)value["moduleId"])
                    || string.IsNullOrWhiteSpace((string)value["semanticDescription"])
                    || (string)value["polarity"] is not ("positive" or "drawback"))
                || offers.Count(value => (string)value["polarity"] == "positive") < 1
                || maximumPositive is < 1 or > 3
                || maximumDrawback is < 0 or > 3
                || maximumDrawback > 0
                    && offers.All(value => (string)value["polarity"] != "drawback"))
                throw new InvalidOperationException(
                    "Formula pilot module offer packet is invalid.");
            if ((equipment || acquiredTrait) && offers.Any(value =>
                    ContainsOfferMechanicalNumber(
                        (string)value["semanticDescription"])))
            {
                throw new InvalidOperationException(
                    "Module-selection semantics must not expose numeric mechanics.");
            }
            string[] evidence = ((JArray)request["evidenceFactIds"]).Values<string>().ToArray();
            string[] facts = ((JArray)request["publicFacts"]).OfType<JObject>()
                .Select(value => (string)value["factId"]).ToArray();
            JObject[] provenance = ((JArray)row["evidenceProvenance"]).OfType<JObject>().ToArray();
            if (evidence.Length < 2 || evidence.Length > 4
                || evidence.Distinct(StringComparer.Ordinal).Count() != evidence.Length
                || evidence.Any(value => !facts.Contains(value, StringComparer.Ordinal)))
                throw new InvalidOperationException("Formula pilot requires two to four distinct public event instances.");
            if (provenance.Length != facts.Length
                || provenance.Any(value => (bool?)value["gameplayReachable"] != true
                    || string.IsNullOrWhiteSpace((string)value["sourceDefinitionId"])
                    || string.IsNullOrWhiteSpace((string)value["producerPath"])
                    || string.IsNullOrWhiteSpace((string)value["producerSymbol"])
                    || !((string)value["producerSha256"] ?? string.Empty).StartsWith("sha256:", StringComparison.Ordinal)
                    || !((string)value["definitionSha256"] ?? string.Empty).StartsWith("sha256:", StringComparison.Ordinal)
                    || value["recordedTuple"] is not JObject)
                || provenance.Select(value => (string)value["evidenceId"])
                    .Distinct(StringComparer.Ordinal).Count() != facts.Length
                || facts.Any(id => !provenance.Any(value => string.Equals(
                    (string)value["evidenceId"], id, StringComparison.Ordinal))))
                throw new InvalidOperationException(
                    "Formula pilot contains an unproven or mismatched gameplay narrative fact.");
            if (string.Equals((string)row["profileId"],
                    LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
                    StringComparison.Ordinal)
                && provenance.Any(value => !ValidateFacilityProvenance(value)))
            {
                throw new InvalidOperationException(
                    "Facility pilot contains evidence outside its authored source applicability.");
            }
            if (equipment && provenance.Any(value => !ValidateEquipmentProvenance(
                    value, equipmentKind, (string)row["targetPersistentId"])))
            {
                throw new InvalidOperationException(
                    "Equipment pilot contains evidence outside its typed source applicability or instance binding.");
            }
            JObject[] contexts = provenance
                .Select(value => value["recordedTuple"]?["eventContext"] as JObject)
                .ToArray();
            if (contexts.Any(value => value == null
                    || string.IsNullOrWhiteSpace((string)value["eventInstanceId"])
                    || string.IsNullOrWhiteSpace((string)value["chainId"])
                    || ((long?)value["sequence"] ?? 0L) <= 0L
                    || ((int?)value["occurredDay"] ?? -1) < 0
                    || string.IsNullOrWhiteSpace((string)value["locationDisplayName"])
                    || string.IsNullOrWhiteSpace((string)value["actorDisplayName"])
                    || (string.IsNullOrWhiteSpace((string)value["counterpartyDisplayName"])
                        && string.IsNullOrWhiteSpace((string)value["usedObjectDisplayName"])
                        && string.IsNullOrWhiteSpace((string)value["resultDetail"])))
                || contexts.Select(value => (string)value["eventInstanceId"])
                    .Distinct(StringComparer.Ordinal).Count() != contexts.Length
                || contexts.Select(value => (string)value["chainId"])
                    .Distinct(StringComparer.Ordinal).Count() != 1)
                throw new InvalidOperationException(
                    "Formula pilot evidence is missing a concrete coherent event chain.");
            JObject[] publicFacts = ((JArray)request["publicFacts"])
                .OfType<JObject>().ToArray();
            if (provenance.Any(value =>
                {
                    string evidenceId = (string)value["evidenceId"] ?? string.Empty;
                    JObject context = (JObject)value["recordedTuple"]?["eventContext"];
                    JObject fact = publicFacts.SingleOrDefault(candidate => string.Equals(
                        (string)candidate["factId"], evidenceId, StringComparison.Ordinal));
                    string expectedPrefix = "시점: "
                        + ((int?)context?["occurredDay"] ?? -1).ToString(
                            CultureInfo.InvariantCulture)
                        + "일, 순서: "
                        + ((long?)context?["sequence"] ?? -1L).ToString(
                            CultureInfo.InvariantCulture)
                        + ". ";
                    return fact == null || !((string)fact["text"] ?? string.Empty)
                        .StartsWith(expectedPrefix, StringComparison.Ordinal);
                }))
            {
                throw new InvalidOperationException(
                    "Formula pilot public facts must expose their exact recorded chronology.");
            }
            if (((JArray)request["publicFacts"]).Values<JObject>().Any(value =>
                    ((string)value["text"] ?? string.Empty).Contains(
                        "같은 유형의 기록", StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "Cumulative mastery labels cannot be presentation evidence.");
            if (((JArray)request["publicFacts"]).OfType<JObject>().Any(value =>
                    ((string)value["factId"] ?? string.Empty).StartsWith("pilot-fact:", StringComparison.Ordinal)))
                throw new InvalidOperationException("Synthetic pilot facts are forbidden.");
            string[] expectedRequestKeys = characterSkill
                ? new[] { "evidenceFactIds", "maximumDrawbackModules", "maximumPositiveModules", "moduleOffers", "publicFacts", "selectionId", "skillContext", "style", "targetDisplayName" }
                : new[] { "evidenceFactIds", "maximumDrawbackModules", "maximumPositiveModules", "moduleOffers", "publicFacts", "selectionId", "style", "targetDisplayName" };
            if (request.Properties().Select(value => value.Name).OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(expectedRequestKeys, StringComparer.Ordinal) == false)
                throw new InvalidOperationException("Formula pilot request keys escaped the closed module-selection contract.");
            if (acquiredTrait)
            {
                string ownerId = (string)row["targetPersistentId"];
                string ownerName = (string)request["targetDisplayName"];
                if (provenance.Any(value =>
                {
                    JObject tuple = (JObject)value["recordedTuple"];
                    JObject context = (JObject)tuple?["eventContext"];
                    return !string.Equals((string)context?["actorId"], ownerId,
                               StringComparison.Ordinal)
                           || !string.Equals((string)context?["actorDisplayName"], ownerName,
                               StringComparison.Ordinal);
                }))
                {
                    throw new InvalidOperationException(
                        "Acquired-trait pilot must preserve its target owner identity as every event actor.");
                }
            }
        }
        JObject[] skillRows = rows.Where(value => string.Equals((string)value["profileId"],
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
            StringComparison.Ordinal)).ToArray();
        RequireSkillPilotCoverage(skillRows);
    }

    private static bool ValidateEquipmentTarget(JObject value)
    {
        if (value == null
            || !value.Properties().Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(new[] { "definitionId", "kind" }, StringComparer.Ordinal)
            || string.IsNullOrWhiteSpace((string)value["definitionId"])
            || !Enum.TryParse((string)value["kind"], ignoreCase: false,
                out CombatEquipmentKind kind)
            || !Enum.IsDefined(typeof(CombatEquipmentKind), kind))
        {
            return false;
        }
        return true;
    }

    private static SourceDigest CaptureSourceDigest(string projectRoot, CatalogContext catalogs)
    {
        HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal)
        {
            "Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaContracts.cs",
            "Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaCore.cs",
            "Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaDrawbackPolicyDefinition.cs",
            "Assets/Scripts/Models/Evolution/Core/EvolutionHistoryModels.cs",
            "Assets/Scripts/Models/Evolution/Core/UsageLedgerCompactor.cs",
            "Assets/Scripts/Models/Evolution/Core/EvolutionModuleModels.cs",
            "Assets/Scripts/Models/Evolution/Core/EvolutionModuleRegistry.cs",
            "Assets/Scripts/Models/Evolution/Equipment/EquipmentEvolutionModels.cs",
            "Assets/Scripts/Models/Combat/Core/EquipmentItemStateCodec.cs",
            "Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs",
            "Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs",
            "Assets/Scripts/Models/Economy/Content/CombatArmorSO.cs",
            "Assets/Scripts/Models/Economy/Content/CombatShieldSO.cs",
            "Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionModels.cs",
            "Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillModuleCapabilityRegistry.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillFormulaGeneration.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillFormulaRuntimeContextPolicy.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillGenerationService.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillFormulaCompatibility.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillPresentationSemantics.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillModels.cs",
            "Assets/Scripts/Services/Character/Core/CharacterProgression.cs",
            "Assets/Scripts/Services/Character/Core/CharacterManualSkillRuntime.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillDrawbackEffectSource.cs",
            "Assets/Scripts/Services/Character/Core/GameplayNarrativeSourceCatalog.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs",
            "Assets/Scripts/Services/Character/Ability/AbilityWork.cs",
            "Assets/Scripts/Controllers/Character/Input/OwnerCommandController.cs",
            "Assets/Scripts/Views/Character/UI/StaffManagementSurfacePanel.cs",
            "Assets/Scripts/Services/Infrastructure/Rooms/RoomLayoutCacheAdapter.cs",
            "Assets/Scripts/Services/Character/Core/CharacterStats.cs",
            "Assets/Scripts/Services/Character/Core/CharacterActorCollaborators.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitInferenceService.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitEffectSource.cs",
            "Assets/Scripts/Services/Effects/Runtime/CharacterDerivedStatsSnapshot.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitState.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitValidation.cs",
            "Assets/Scripts/Services/Character/Core/DungeonCharacterSaveData.cs",
            "Assets/Scripts/Services/Character/AI/Editor/CharacterSkillFormulaCatalogAssetBuilder.cs",
            "Assets/Scripts/Services/Character/AI/Editor/CharacterAcquiredTraitFormulaCatalogAssetBuilder.cs",
            "Assets/Scripts/Services/Character/Editor/V25AcquiredTraitContentAssetBuilder.cs",
            "Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs",
            "Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaCatalogSO.cs",
            "Assets/Scripts/Services/Combat/EquipmentEvolutionRules.cs",
            "Assets/Scripts/Services/Combat/EquipmentEvolutionRuntime.cs",
            "Assets/Scripts/Services/Combat/EquipmentEvolutionContracts.cs",
            "Assets/Scripts/Services/Combat/CombatEquipmentStatProjector.cs",
            "Assets/Scripts/Services/Combat/CombatResolutionService.cs",
            "Assets/Scripts/Services/Combat/Work/RepairWorkExecutionHandler.cs",
            "Assets/Scripts/Services/Combat/Editor/EquipmentEvolutionFormulaCatalogAssetBuilder.cs",
            "Assets/Scripts/Services/Evolution/EvolutionHistoryNarrativeRuntime.cs",
            "Assets/Scripts/Services/Evolution/EvolutionModuleFormulaDescriptor.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityFormulaEvolutionAuthority.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRuntime.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRuntimeContracts.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityInstanceEvolutionRuntime.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionModifierQuery.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordEventRecorder.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionStateComponent.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityFormulaEvolutionPresentationRuntime.cs",
            "Assets/Scripts/Services/FacilityEvolution/Editor/P1FacilityEvolutionAssetBuilder.cs",
            "Assets/Scripts/Services/Buildings/SO/BuildingSO.cs",
            "Assets/Scripts/Services/Buildings/SO/BuildingDefinitionIdentity.cs",
            "Assets/Scripts/Services/Buildings/BuildingRuntimeArchetypeKind.cs",
            "Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs",
            "Assets/Scripts/Services/Buildings/Abilities/BuildingAbilityAccessors.cs",
            "Assets/Scripts/Services/Buildings/BuildingOccupancyAssignment.cs",
            "Assets/Scripts/Services/Buildings/BuildableObject.cs",
            "Assets/Scripts/Services/Buildings/Shop.cs",
            "Assets/Scripts/Services/Defense/DefenseFacilitySystem.cs",
            "Assets/Scripts/Services/Invasion/InvasionFacilityTargetRuntimeProjection.cs",
            "Assets/Scripts/Services/Character/AI/Editor/FormulaPresentationPilotExporter.cs",
            "Assets/Scripts/Services/Character/AI/Editor/FormulaModuleSelectionResolutionExporter.cs",
            "Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionExperienceRules.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionBattleCompletionHandler.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionRuntime.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionPanel.cs",
            "Assets/Scripts/Services/Wildlife/WildlifeHuntRuntime.cs",
            "Assets/Scripts/Services/Captivity/MinionSettlementSocialRuntime.cs",
            "Assets/Scripts/Services/Character/Identity/Runtime/CharacterIdentityDomainAdapters.cs",
            "Assets/Scripts/Services/Survival/CharacterDeprivationConsequences.cs",
            CharacterSkillFormulaCatalogAssetBuilder.SettingsAssetPath,
            EquipmentEvolutionFormulaCatalogAssetBuilder.AssetPath
        };
        foreach (UnityEngine.Object asset in catalogs.Traits.Cast<UnityEngine.Object>()
                     .Append(catalogs.TraitSettings).Concat(catalogs.Facilities))
            paths.Add(AssetDatabase.GetAssetPath(asset).Replace('\\', '/'));
        foreach (BuildingSO sourceDefinition in catalogs.Facilities
                     .Where(recipe => recipe != null)
                     .SelectMany(recipe => recipe.fromFacilities ?? Array.Empty<BuildingSO>())
                     .Where(value => value != null))
        {
            paths.Add(AssetDatabase.GetAssetPath(sourceDefinition).Replace('\\', '/'));
        }
        foreach (BuildingSO resultDefinition in catalogs.Facilities
                     .Where(recipe => recipe != null)
                     .Select(recipe => recipe.resultBuilding)
                     .Where(value => value != null))
        {
            paths.Add(AssetDatabase.GetAssetPath(resultDefinition).Replace('\\', '/'));
        }
        foreach (CombatEquipmentDefinitionSO definition in catalogs.EquipmentDefinitions
                     .Where(value => value != null))
        {
            paths.Add(AssetDatabase.GetAssetPath(definition).Replace('\\', '/'));
        }
        List<SourceFile> files = paths.OrderBy(value => value, StringComparer.Ordinal).Select(path =>
        {
            string absolute = Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute)) throw new FileNotFoundException("Formula export source is missing.", absolute);
            return new SourceFile(path, Sha256File(absolute));
        }).ToList();
        string digest = "sha256:" + Sha256Text(string.Join("\n", files.Select(value => value.Path + "=" + value.Sha256)));
        bool dirty = RunGit(projectRoot, "status --porcelain=v1 -- "
            + string.Join(" ", files.Select(value => QuoteGitArgument(value.Path)))).Trim().Length > 0;
        return new SourceDigest(digest, dirty, files);
    }

    private static string ToJsonl(IEnumerable<JObject> rows) => string.Concat(rows.Select(row =>
        CanonicalJson(row) + "\n"));

    private static void WriteJsonl(string path, IEnumerable<JObject> rows)
    {
        using StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false));
        foreach (JObject row in rows)
        {
            writer.Write(CanonicalJson(row));
            writer.Write('\n');
        }
    }

    private static bool FilesEqual(string leftPath, string rightPath)
    {
        FileInfo leftInfo = new FileInfo(leftPath);
        FileInfo rightInfo = new FileInfo(rightPath);
        if (leftInfo.Length != rightInfo.Length) return false;
        using FileStream left = File.OpenRead(leftPath);
        using FileStream right = File.OpenRead(rightPath);
        byte[] leftBuffer = new byte[81920];
        byte[] rightBuffer = new byte[leftBuffer.Length];
        while (true)
        {
            int leftRead = left.Read(leftBuffer, 0, leftBuffer.Length);
            int rightRead = right.Read(rightBuffer, 0, rightBuffer.Length);
            if (leftRead != rightRead) return false;
            if (leftRead == 0) return true;
            for (int index = 0; index < leftRead; index++)
                if (leftBuffer[index] != rightBuffer[index]) return false;
        }
    }

    private static string CanonicalJson(JToken token)
    {
        if (token is JObject obj)
        {
            JObject sorted = new JObject(obj.Properties().OrderBy(value => value.Name, StringComparer.Ordinal)
                .Select(value => new JProperty(value.Name, JToken.Parse(CanonicalJson(value.Value)))));
            return sorted.ToString(Formatting.None);
        }
        if (token is JArray array)
            return new JArray(array.Select(value => JToken.Parse(CanonicalJson(value)))).ToString(Formatting.None);
        return token.ToString(Formatting.None);
    }

    private static T LoadExactlyOne<T>() where T : UnityEngine.Object
    {
        T[] values = LoadAll<T>();
        if (values.Length != 1) throw new InvalidOperationException("Expected exactly one asset of type " + typeof(T).Name);
        return values[0];
    }

    private static T[] LoadAll<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name)
        .Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value, StringComparer.Ordinal)
        .Select(AssetDatabase.LoadAssetAtPath<T>).Where(value => value != null).ToArray();

    private static string RequireVersion(string value)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || canonical.Any(character => !char.IsLetterOrDigit(character)
            && character is not '-' and not '_' and not '.'))
            throw new ArgumentException("Export version contains an unsupported character.", nameof(value));
        return canonical;
    }

    private static string RequireGitCommit(string projectRoot)
    {
        string value = RunGit(projectRoot, "rev-parse HEAD").Trim().ToLowerInvariant();
        if (value.Length != 40 || value.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidOperationException("Unity export requires a canonical 40-hex git commit.");
        return value;
    }

    private static string RunGit(string projectRoot, string arguments)
    {
        ProcessStartInfo info = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "-C " + QuoteGitArgument(projectRoot) + " " + arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("Could not start git.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("git failed: " + error.Trim());
        return output;
    }

    private static string QuoteGitArgument(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

    private static string Sha256Text(string value)
    {
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))
            .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return "sha256:" + string.Concat(sha.ComputeHash(stream)
            .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private sealed class EvidenceBundle
    {
        public EvidenceBundle(
            IReadOnlyList<NarrativeFormulaEvidence> formula,
            JArray publicFacts,
            JArray provenance,
            bool hasNegativeEvidence,
            CharacterNarrativeLedger ledger = null)
        {
            Formula = formula;
            PublicFacts = publicFacts;
            Provenance = provenance;
            HasNegativeEvidence = hasNegativeEvidence;
            Ledger = ledger;
        }
        public IReadOnlyList<NarrativeFormulaEvidence> Formula { get; }
        public JArray PublicFacts { get; }
        public JArray Provenance { get; }
        public bool HasNegativeEvidence { get; }
        public CharacterNarrativeLedger Ledger { get; }
    }

    private sealed class CatalogContext
    {
        public CatalogContext(CharacterSkillSystemSettingsSO skills,
            CharacterAcquiredTraitSettingsSO traitSettings,
            CharacterAcquiredTraitModuleSO[] traits,
            EquipmentEvolutionFormulaCatalogSO equipment,
            CombatEquipmentDefinitionSO[] equipmentDefinitions,
            FacilityEvolutionRecipeSO[] facilities)
        {
            Skills = skills;
            TraitSettings = traitSettings;
            Traits = traits;
            Equipment = equipment;
            EquipmentDefinitions = equipmentDefinitions;
            Facilities = facilities;
        }
        public CharacterSkillSystemSettingsSO Skills { get; }
        public CharacterAcquiredTraitSettingsSO TraitSettings { get; }
        public CharacterAcquiredTraitModuleSO[] Traits { get; }
        public EquipmentEvolutionFormulaCatalogSO Equipment { get; }
        public CombatEquipmentDefinitionSO[] EquipmentDefinitions { get; }
        public FacilityEvolutionRecipeSO[] Facilities { get; }
    }

    private sealed class SourceFile
    {
        public SourceFile(string path, string sha256) { Path = path; Sha256 = sha256; }
        public string Path { get; }
        public string Sha256 { get; }
    }

    private sealed class SourceDigest
    {
        public SourceDigest(string digest, bool workingTreeDirty, IReadOnlyList<SourceFile> files)
        { Digest = digest; WorkingTreeDirty = workingTreeDirty; Files = files; }
        public string Digest { get; }
        public bool WorkingTreeDirty { get; }
        public IReadOnlyList<SourceFile> Files { get; }
    }

    private sealed class EquipmentReplayPlanSet
    {
        public EquipmentReplayPlanSet(
            IEnumerable<EquipmentReplayPlan> plans,
            IEnumerable<EquipmentPilotExclusion> exclusions)
        {
            Plans = (plans ?? Array.Empty<EquipmentReplayPlan>())
                .OrderBy(value => value.Definition.EquipmentId, StringComparer.Ordinal)
                .ToArray();
            Exclusions = (exclusions ?? Array.Empty<EquipmentPilotExclusion>())
                .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
                .ToArray();
            Enumeration = Plans.Select(value => new EquipmentFormulaTargetEnumeration(
                    value.Definition.EquipmentId,
                    value.Definition.Kind,
                    replayEligible: true,
                    reason: string.Empty))
                .Concat(Exclusions.Select(value => new EquipmentFormulaTargetEnumeration(
                    value.DefinitionId,
                    value.Kind,
                    replayEligible: false,
                    reason: value.Reason)))
                .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
                .ToArray();
            if (Enumeration.Select(value => value.DefinitionId)
                    .Distinct(StringComparer.Ordinal).Count() != Enumeration.Length)
            {
                throw new InvalidOperationException(
                    "Equipment formula target enumeration has duplicate concrete definitions.");
            }
        }

        public EquipmentReplayPlan[] Plans { get; }
        public EquipmentPilotExclusion[] Exclusions { get; }
        public EquipmentFormulaTargetEnumeration[] Enumeration { get; }
    }

    private sealed class EquipmentReplayPlan
    {
        public EquipmentReplayPlan(
            CombatEquipmentDefinitionSO definition,
            int positiveOfferCount)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (positiveOfferCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(positiveOfferCount));
            PositiveOfferCount = positiveOfferCount;
        }

        public CombatEquipmentDefinitionSO Definition { get; }
        public int PositiveOfferCount { get; }
    }

    private sealed class EquipmentPilotExclusion
    {
        public EquipmentPilotExclusion(
            CombatEquipmentDefinitionSO definition,
            string reason)
        {
            DefinitionId = definition?.EquipmentId ?? string.Empty;
            Kind = definition?.Kind.ToString() ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string DefinitionId { get; }
        public string Kind { get; }
        public string Reason { get; }

        public JObject ToJson() => new()
        {
            ["definitionId"] = DefinitionId,
            ["kind"] = Kind,
            ["reason"] = Reason
        };
    }

    private sealed class EquipmentFormulaTargetEnumeration
    {
        public EquipmentFormulaTargetEnumeration(
            string definitionId,
            CombatEquipmentKind kind,
            bool replayEligible,
            string reason)
            : this(definitionId, kind.ToString(), replayEligible, reason)
        {
        }

        public EquipmentFormulaTargetEnumeration(
            string definitionId,
            string kind,
            bool replayEligible,
            string reason)
        {
            DefinitionId = definitionId ?? string.Empty;
            Kind = kind ?? string.Empty;
            ReplayEligible = replayEligible;
            Reason = reason ?? string.Empty;
        }

        public string DefinitionId { get; }
        public string Kind { get; }
        public bool ReplayEligible { get; }
        public string Reason { get; }

        public JObject ToJson() => new()
        {
            ["definitionId"] = DefinitionId,
            ["kind"] = Kind,
            ["replayEligible"] = ReplayEligible,
            ["reason"] = Reason
        };
    }

    private sealed class FacilityReplayPlanSet
    {
        public FacilityReplayPlanSet(
            IEnumerable<FacilityReplayPlan> plans,
            IEnumerable<FacilityPilotExclusion> exclusions,
            IEnumerable<FacilityEvolutionRecipeSO> allRecipes)
        {
            Plans = (plans ?? Array.Empty<FacilityReplayPlan>())
                .OrderBy(value => value.Recipe.EffectiveId, StringComparer.Ordinal)
                .ToArray();
            Exclusions = (exclusions ?? Array.Empty<FacilityPilotExclusion>())
                .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
                .ToArray();
            Dictionary<string, FacilityReplayPlan> plansByRecipe = Plans.ToDictionary(
                value => value.Recipe.EffectiveId, StringComparer.Ordinal);
            Dictionary<string, FacilityPilotExclusion> exclusionsByRecipe = Exclusions.ToDictionary(
                value => value.RecipeId, StringComparer.Ordinal);
            Enumeration = (allRecipes ?? Array.Empty<FacilityEvolutionRecipeSO>())
                .Where(value => value != null)
                .OrderBy(value => value.EffectiveId, StringComparer.Ordinal)
                .Select(recipe =>
                {
                    if (plansByRecipe.TryGetValue(recipe.EffectiveId,
                            out FacilityReplayPlan plan))
                    {
                        return new FacilityFormulaRecipeEnumeration(
                            recipe, plan.SourceDefinition, replayEligible: true,
                            reason: string.Empty);
                    }
                    if (exclusionsByRecipe.TryGetValue(recipe.EffectiveId,
                            out FacilityPilotExclusion exclusion))
                    {
                        return new FacilityFormulaRecipeEnumeration(
                            recipe, exclusion.SourceDefinition, replayEligible: false,
                            reason: exclusion.Reason);
                    }
                    throw new InvalidOperationException(
                        "Facility formula recipe has no source replay decision: "
                        + recipe.EffectiveId);
                })
                .ToArray();
            if (Enumeration.Length != Plans.Length + Exclusions.Length
                || Enumeration.Select(value => value.RecipeId)
                    .Distinct(StringComparer.Ordinal).Count() != Enumeration.Length)
            {
                throw new InvalidOperationException(
                    "Facility formula recipe enumeration is incomplete or duplicate.");
            }
        }

        public FacilityReplayPlan[] Plans { get; }
        public FacilityPilotExclusion[] Exclusions { get; }
        public FacilityFormulaRecipeEnumeration[] Enumeration { get; }
    }

    private sealed class FacilityReplayPlan
    {
        public FacilityReplayPlan(
            FacilityEvolutionRecipeSO recipe,
            BuildingSO sourceDefinition,
            IEnumerable<FacilityGameplayUsageSource> directReplaySources)
        {
            Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            SourceDefinition = sourceDefinition
                ?? throw new ArgumentNullException(nameof(sourceDefinition));
            DirectReplaySources = (directReplaySources
                    ?? Array.Empty<FacilityGameplayUsageSource>())
                .OrderBy(value => value.SourceId, StringComparer.Ordinal)
                .ToArray();
            if (DirectReplaySources.Length == 0)
                throw new ArgumentException(
                    "Facility replay plan requires at least one source.",
                    nameof(directReplaySources));
        }

        public FacilityEvolutionRecipeSO Recipe { get; }
        public BuildingSO SourceDefinition { get; }
        public FacilityGameplayUsageSource[] DirectReplaySources { get; }
    }

    private sealed class FacilityPilotExclusion
    {
        public FacilityPilotExclusion(
            string recipeId,
            BuildingSO sourceDefinition,
            string reason,
            IEnumerable<FacilitySourceDiagnostic> diagnostics)
            : this(recipeId, sourceDefinition, reason, diagnostics,
                null, Array.Empty<FacilityResultTargetDiagnostic>())
        {
        }

        public FacilityPilotExclusion(
            string recipeId,
            BuildingSO sourceDefinition,
            string reason,
            IEnumerable<FacilitySourceDiagnostic> diagnostics,
            BuildingSO resultDefinition,
            IEnumerable<FacilityResultTargetDiagnostic> resultTargetDiagnostics)
        {
            RecipeId = recipeId ?? string.Empty;
            SourceDefinition = sourceDefinition;
            Reason = reason ?? string.Empty;
            Diagnostics = (diagnostics ?? Array.Empty<FacilitySourceDiagnostic>())
                .OrderBy(value => value.SourceId, StringComparer.Ordinal)
                .ToArray();
            ResultDefinition = resultDefinition;
            ResultTargetDiagnostics = (resultTargetDiagnostics
                    ?? Array.Empty<FacilityResultTargetDiagnostic>())
                .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
                .ToArray();
        }

        public string RecipeId { get; }
        public BuildingSO SourceDefinition { get; }
        public string Reason { get; }
        public FacilitySourceDiagnostic[] Diagnostics { get; }
        public BuildingSO ResultDefinition { get; }
        public FacilityResultTargetDiagnostic[] ResultTargetDiagnostics { get; }

        public JObject ToJson() => new()
        {
            ["recipeId"] = RecipeId,
            ["sourceTargetDefinitionId"] = SourceDefinition == null
                ? string.Empty
                : BuildingDefinitionIdentity.Resolve(SourceDefinition),
            ["sourceTargetRuntimeArchetype"] = SourceDefinition == null
                ? string.Empty
                : SourceDefinition.runtimeArchetype.ToString(),
            ["resultTargetDefinitionId"] = ResultDefinition == null
                ? string.Empty
                : BuildingDefinitionIdentity.Resolve(ResultDefinition),
            ["resultTargetRuntimeArchetype"] = ResultDefinition == null
                ? string.Empty
                : ResultDefinition.runtimeArchetype.ToString(),
            ["reason"] = Reason,
            ["sourceDiagnostics"] = new JArray(Diagnostics.Select(value => value.ToJson())),
            ["resultTargetDiagnostics"] = new JArray(ResultTargetDiagnostics
                .Select(value => value.ToJson()))
        };
    }

    private sealed class FacilityFormulaRecipeEnumeration
    {
        public FacilityFormulaRecipeEnumeration(
            FacilityEvolutionRecipeSO recipe,
            BuildingSO sourceDefinition,
            bool replayEligible,
            string reason)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            RecipeId = recipe.EffectiveId;
            FormulaVersion = recipe.RequireFormulaPolicy().FormulaVersion;
            SourceDefinitionId = sourceDefinition == null
                ? string.Empty
                : BuildingDefinitionIdentity.Resolve(sourceDefinition);
            ResultDefinitionId = recipe.resultBuilding == null
                ? string.Empty
                : BuildingDefinitionIdentity.Resolve(recipe.resultBuilding);
            ReplayEligible = replayEligible;
            Reason = reason ?? string.Empty;
        }

        public string RecipeId { get; }
        public int FormulaVersion { get; }
        public string SourceDefinitionId { get; }
        public string ResultDefinitionId { get; }
        public bool ReplayEligible { get; }
        public string Reason { get; }

        public JObject ToJson() => new()
        {
            ["recipeId"] = RecipeId,
            ["formulaVersion"] = FormulaVersion,
            ["sourceDefinitionId"] = SourceDefinitionId,
            ["resultDefinitionId"] = ResultDefinitionId,
            ["replayEligible"] = ReplayEligible,
            ["reason"] = Reason
        };
    }

    private sealed class FacilityResultTargetDiagnostic
    {
        public FacilityResultTargetDiagnostic(string moduleId, string reason)
        {
            ModuleId = moduleId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string ModuleId { get; }
        public string Reason { get; }

        public JObject ToJson() => new()
        {
            ["moduleId"] = ModuleId,
            ["reason"] = Reason
        };
    }

    private sealed class FacilitySourceDiagnostic
    {
        public FacilitySourceDiagnostic(FacilityGameplayUsageSource source, string reason)
            : this(source?.SourceId, source?.Applicability
                ?? throw new ArgumentNullException(nameof(source)), reason)
        {
        }

        public FacilitySourceDiagnostic(
            string sourceId,
            FacilityGameplayUsageApplicability applicability,
            string reason)
        {
            SourceId = sourceId ?? string.Empty;
            Applicability = applicability;
            Reason = reason ?? string.Empty;
        }

        public string SourceId { get; }
        public FacilityGameplayUsageApplicability Applicability { get; }
        public string Reason { get; }

        public JObject ToJson() => new()
        {
            ["sourceDefinitionId"] = SourceId,
            ["sourceApplicability"] = Applicability.ToString(),
            ["reason"] = Reason
        };
    }
}
#endif
