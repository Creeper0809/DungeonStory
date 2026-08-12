#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class V26FounderTraitConnectivityManifestScenario
{
    private const string CatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string ReportPath =
        "Artifacts/QA/v26-founder-trait-source-consumer-manifest.md";

    private static readonly HashSet<int> RetainedFounderIds = new(new[]
    {
        101,102,103,104,105,106,107,108,109,
        200,201,202,203,204,205,206,207,208,209,210,211,212,213,214,
        215,216,217,218,219,220,221,222,223,224,225,226,227,228,229,230,
        235,239,245
    });

    private static readonly HashSet<string> ExpectedConditions = new(
        new[]
        {
            "accident:fall-slip", "event:hostile-execution",
            "relationship:first-apology", "relationship:negative", "room:noise",
            "shift:day", "shift:night", "state:arcane-overcharge",
            "state:arcane-overcharge-aftermath", "state:emergency-stocked",
            "state:forbidden-leap-aftermath", "state:formal-status",
            "state:golden-harvest-jackpot", "state:insulted", "state:last-stand",
            "state:last-stand-aftermath", "state:miracle-surgery-aftermath",
            "state:pain", "state:production-limit-break",
            "state:production-limit-break-aftermath", "state:ritual-fast-ended",
            "state:ritual-fasting", "state:sated", "state:sweet-fed",
            "temperature:cold", "temperature:comfortable", "temperature:hot",
            "temperature:uncomfortable", "terrain:rough", "work:clean",
            "work:clean-maintenance", "work:contaminated-food",
            "work:contamination", "work:craft-finished", "work:dangerous",
            "work:emergency", "work:long-shift", "work:mentoring",
            "work:not-clean", "work:not-research", "work:on-schedule",
            "work:precision", "work:research", "work:retry-after-failure",
            "work:substitute-material"
        },
        StringComparer.Ordinal);

    private static readonly HashSet<string> ExpectedIdentityEventActions = new(
        new[]
        {
            "combat:inactive-five-days", "combat:victory", "danger:directly-assigned",
            "danger:safe-return", "danger:success", "environment:rot-stench", "event:panic",
            "food:bland-streak", "food:meal-missed", "food:new-meal", "food:no-sweets",
            "food:salted", "food:sated", "food:sweet", "injury:blunt",
            "living:basic-only", "living:luxury-satisfied", "medical:entered-clinic",
            "medical:severity-reduced", "mentee:rank-up", "mood:butchery-guilt",
            "mood:hostile-execution-guilt", "mood:hostile-kill-guilt", "order:defer-cleaning",
            "product:defect-caught-before-release", "product:defect-found",
            "product:quality-low", "product:quality-masterwork", "research:completed",
            "research:no-access", "resource:salvageable-discarded", "resource:wasted",
            "rest:private", "rest:sufficient", "ritual:fast-broken", "ritual:fast-completed",
            "room:cleaned", "room:cramped-long", "room:dirty",
            "schedule:sudden-reassignment", "shift:forced-day", "sleep:noisy",
            "social:betrayal-or-assault", "social:insult-answered", "social:insulted",
            "social:public-question", "social:sincere-apology", "status:publicly-ignored",
            "status:recognized", "stockpile:emergency-ready", "stockpile:emergency-shortage",
            "stockpile:target-met", "temperature:cold-long",
            "temperature:safe-cold-work-complete", "temperature:uncomfortable-long",
            "terrain:rough-crossed-safely", "wait:exceeded", "work:failed",
            "work:first-process-success", "work:repetitive-three-days", "work:small-success",
            "work:strict-procedure", "work:substitute-success"
        },
        StringComparer.Ordinal);

    private static readonly HashSet<string> ExpectedBehaviorActionTags = new(
        new[]
        {
            "alert:minor", "consume:luxury", "food:salted", "food:sweet",
            "food:unfamiliar", "medical:rest-treatment", "rest:private", "ritual:fast",
            "room:temperature-controlled", "shift:night", "social:answer-insult",
            "social:encourage", "social:formal-etiquette", "social:reconcile", "work:clean",
            "work:cold-zone", "work:combat-training", "work:crisis-rescue", "work:dangerous",
            "work:eat", "work:emergency-check", "work:heavy-haul", "work:immediate",
            "work:inspect", "work:inspect-scout", "work:long-shift", "work:mentoring",
            "work:new-process", "work:on-schedule", "work:precision",
            "work:prevent-repeat-failure", "work:prototype", "work:quality-first",
            "work:research", "work:rough-terrain-rescue", "work:salvage", "work:subdue",
            "work:while-in-pain"
        },
        StringComparer.Ordinal);

    private static readonly HashSet<string> ExpectedPersistentNeeds = new(
        new[]
        {
            "need:combat-action", "need:emergency-readiness", "need:large-meal",
            "need:luxury-standard", "need:research-access", "need:ritual-fast",
            "need:salt", "need:stimulation", "need:sweets"
        },
        StringComparer.Ordinal);

    private sealed class Row
    {
        public string Kind;
        public string Id;
        public string Definition;
        public string Producer;
        public string Authority;
        public string Consumer;
        public string Save;
        public string Observation;
        public string Test;
        public string Status;
        public string Evidence;
    }

    [MenuItem("DungeonStory/V26/Audit Founder Trait Source Consumer Manifest")]
    public static void Run()
    {
        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        CharacterTraitSO[] founders = catalog.Definitions
            .OfType<CharacterTraitSO>()
            .Where(IsFounder)
            .OrderBy(value => value.id)
            .ToArray();
        Require(founders.Length == 100, $"Founder trait count={founders.Length}.");
        Require(founders.All(value => value.moodReactions == null
                || value.moodReactions.Count == 0),
            "Founder traits must not author the retired moodReactions path.");
        Require(catalog.Definitions.OfType<HeritableTraitDefinitionSO>().Count() == 24,
            "Heritable trait catalog must remain a separate 24-definition catalog.");

        Dictionary<string, string> liveSources = LoadLiveSources();
        List<Row> rows = new();
        BuildTargetRows(catalog, liveSources, rows);
        BuildConditionRows(catalog, liveSources, rows);
        BuildIdentityRows(founders, liveSources, rows);
        BuildExtremeRows(founders, liveSources, rows);
        BuildGameplayCommandRows(liveSources, rows);
        BuildPublicRuntimeApiRows(liveSources, rows);
        BuildNonPublicRuntimeHelperRows(liveSources, rows);
        BuildIdentitySerializedFieldRows(liveSources, rows);
        AuditLegacyBoundaries(liveSources);

        string[] duplicateKeys = rows
            .GroupBy(value => value.Kind + "|" + value.Id, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .ToArray();
        Require(duplicateKeys.Length == 0,
            "Duplicate manifest keys: " + string.Join(", ", duplicateKeys));

        Row[] orphaned = rows.Where(value => value.Status != "connected").ToArray();
        WriteReport(rows, orphaned);
        Require(orphaned.Length == 0,
            "Connectivity orphans: " + string.Join(", ", orphaned.Select(value =>
                value.Kind + ":" + value.Id)));
        Debug.Log(
            $"V26_TRAIT_CONNECTIVITY_MANIFEST=PASS; rows={rows.Count}; "
            + $"targets={rows.Count(value => value.Kind == "effect-target")}; "
            + $"conditions={rows.Count(value => value.Kind == "effect-condition")}; "
            + $"identity={rows.Count(value => value.Kind == "identity-event-action")}; "
            + $"behaviors={rows.Count(value => value.Kind == "behavior-tag")}; "
            + $"needs={rows.Count(value => value.Kind == "persistent-need")}; "
            + $"extremes={rows.Count(value => value.Kind == "extreme")}; "
            + $"publicApis={rows.Count(value => value.Kind == "public-runtime-api")}; "
            + $"helperMethods={rows.Count(value => value.Kind == "nonpublic-runtime-helper")}; "
            + $"serializedFields={rows.Count(value => value.Kind is "identity-serialized-field" or "runtime-serialized-field")}; "
            + "orphans=0");
    }

    private static void BuildTargetRows(
        GameDomainContentCatalogSO catalog,
        IReadOnlyDictionary<string, string> liveSources,
        ICollection<Row> rows)
    {
        GameplayEffectDefinitionSO[] definitions = catalog.Definitions
            .OfType<GameplayEffectDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.TargetId, StringComparer.Ordinal)
            .ToArray();
        Require(definitions.Length == 49,
            $"Gameplay effect definition count={definitions.Length}.");
        Require(definitions.Select(value => value.TargetId)
                .Distinct(StringComparer.Ordinal).Count() == definitions.Length,
            "Gameplay effect targets are not one-definition-per-target.");
        foreach (GameplayEffectDefinitionSO definition in definitions)
        {
            string consumer = TargetConsumer(definition.TargetId);
            bool connected = !string.IsNullOrWhiteSpace(consumer)
                && LiveFileContainsSymbol(liveSources, consumer);
            rows.Add(new Row
            {
                Kind = "effect-target",
                Id = definition.TargetId,
                Definition = AssetDatabase.GetAssetPath(definition),
                Producer = "IGameplayEffectSource.Effects / CharacterDerivedStatsSnapshotProjector.CollectSources",
                Authority = "CharacterDerivedStatsSnapshotProjector.Project",
                Consumer = consumer,
                Save = "N/A: derived snapshot and cache are recomputed",
                Observation = "CharacterDetailedStatsRuntime.BuildGameplayEffectTrace",
                Test = "V26FounderTraitConnectivityManifestScenario.Run + domain focused gate",
                Status = connected ? "connected" : "orphan",
                Evidence = connected
                    ? "explicit target-to-domain-consumer table and contribution trace"
                    : "missing explicit live domain consumer"
            });
        }
    }

    private static void BuildConditionRows(
        GameDomainContentCatalogSO catalog,
        IReadOnlyDictionary<string, string> liveSources,
        ICollection<Row> rows)
    {
        GameplayEffectConditionDefinitionSO[] definitions = catalog.Definitions
            .OfType<GameplayEffectConditionDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.ConditionId, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> actual = definitions
            .Select(value => value.ConditionId)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(ExpectedConditions),
            "Effect condition catalog differs from the explicit producer manifest. "
            + "Missing=" + string.Join(",", ExpectedConditions.Except(actual))
            + " Extra=" + string.Join(",", actual.Except(ExpectedConditions)));
        foreach (GameplayEffectConditionDefinitionSO definition in definitions)
        {
            string[] producers = FindLiveReferences(liveSources, definition.ConditionId);
            rows.Add(new Row
            {
                Kind = "effect-condition",
                Id = definition.ConditionId,
                Definition = AssetDatabase.GetAssetPath(definition),
                Producer = string.Join("<br>", producers),
                Authority = "GameplayEffectContext.ActiveConditionIds",
                Consumer = "CharacterGameplayEffectProjector.Resolve / IsActive",
                Save = definition.ConditionId.StartsWith("state:", StringComparison.Ordinal)
                    ? "identity/body/domain authority save or deterministic recompute"
                    : "N/A: current work/environment context recompute",
                Observation = "contribution trace reports active or condition-is-inactive",
                Test = "V26FounderTraitConnectivityManifestScenario.Run + condition producer gate",
                Status = producers.Length > 0 ? "connected" : "orphan",
                Evidence = producers.Length > 0
                    ? "non-Editor producer literal found"
                    : "no non-Editor producer literal"
            });
        }
    }

    private static void BuildIdentityRows(
        IEnumerable<CharacterTraitSO> founders,
        IReadOnlyDictionary<string, string> liveSources,
        ICollection<Row> rows)
    {
        Dictionary<string, List<string>> identity = new(StringComparer.Ordinal);
        Dictionary<string, List<string>> behaviors = new(StringComparer.Ordinal);
        Dictionary<string, List<string>> needs = new(StringComparer.Ordinal);
        foreach (CharacterTraitSO trait in founders)
        foreach (CharacterIdentityRule rule in trait.identityRules
                     ?? new List<CharacterIdentityRule>())
        {
            string definition = $"trait:{trait.id}/{rule.ruleId}";
            switch (rule)
            {
                case EventMoodRule value:
                    Add(identity, value.eventId, definition);
                    break;
                case MoodImmunityRule value:
                    Add(identity, value.eventId, definition);
                    break;
                case MoodTransformRule value:
                    Add(identity, value.eventId, definition);
                    break;
                case PostActionConsequenceRule value:
                    Add(identity, value.actionTag, definition);
                    break;
                case RelationshipMemoryRule value:
                    Add(identity, value.eventId, definition);
                    break;
                case PersistentNeedRule value:
                    Add(identity, value.satisfiedEventId, definition);
                    Add(identity, value.deprivedEventId, definition);
                    Add(needs, value.needId, definition);
                    break;
                case BehaviorUtilityRule value:
                    Add(behaviors, value.behaviorTag, definition);
                    break;
                case AutonomousWorkRestrictionRule value:
                    Add(behaviors, value.actionTag, definition);
                    break;
                case IncidentWeightRule value:
                    Add(identity, value.incidentId, definition);
                    break;
            }
        }
        Require(identity.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(ExpectedIdentityEventActions),
            $"Identity event/action manifest mismatch. Missing={string.Join(",", ExpectedIdentityEventActions.Except(identity.Keys))} "
            + $"Extra={string.Join(",", identity.Keys.Except(ExpectedIdentityEventActions))}");
        Require(behaviors.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(ExpectedBehaviorActionTags),
            $"Behavior/action tag manifest mismatch. Missing={string.Join(",", ExpectedBehaviorActionTags.Except(behaviors.Keys))} "
            + $"Extra={string.Join(",", behaviors.Keys.Except(ExpectedBehaviorActionTags))}");
        Require(needs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(ExpectedPersistentNeeds),
            $"Persistent need manifest mismatch. Missing={string.Join(",", ExpectedPersistentNeeds.Except(needs.Keys))} "
            + $"Extra={string.Join(",", needs.Keys.Except(ExpectedPersistentNeeds))}");

        foreach ((string id, List<string> definitions) in identity
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
            AddLiveReferenceRow(
                rows, liveSources, "identity-event-action", id, definitions,
                "typed domain event or direct command result",
                "CharacterMoodPolicyService / identity adapters",
                "CharacterIdentityStateStore when stateful",
                "mood, relationship, AI and activity UI");
        foreach ((string id, List<string> definitions) in behaviors
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
            AddLiveReferenceRow(
                rows, liveSources, "behavior-tag", id, definitions,
                "actual AI candidate semantic tag",
                "CharacterIdentityBehaviorUtility.Resolve",
                "N/A: AI utility recompute",
                "AI chosen action and exact failure reason");
        foreach ((string id, List<string> definitions) in needs
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
            AddLiveReferenceRow(
                rows, liveSources, "persistent-need", id, definitions,
                "domain satisfaction/deprivation event",
                "CharacterPersistentNeedRuntime / Clock",
                "CharacterIdentityStateStore revision 1",
                "mood factor and staff status UI");
    }

    private static void BuildExtremeRows(
        IEnumerable<CharacterTraitSO> founders,
        IReadOnlyDictionary<string, string> liveSources,
        ICollection<Row> rows)
    {
        Dictionary<int, (string Runtime, string Entry, string Save, string Test)> evidence =
            new()
            {
                [300] = ("ExtremeCraftInspirationRuntime", "CombatEquipmentCraftingRuntime|ApparelWorkOrderRuntime", "item provenance + CharacterIdentityStateStore", "V26 mythic audit"),
                [301] = ("ExtremeTraitRuntime", "CharacterBodyHealthRuntime|CharacterCombatCommandRuntime", "CharacterIdentityStateStore", "critical-health combat gate"),
                [302] = ("ExtremeTraitRuntime", "ResearchTreeWindow", "CharacterIdentityStateStore", "research leap UI gate"),
                [303] = ("ExtremeTraitRuntime", "SurgeryRuntime", "CharacterIdentityStateStore", "fatal surgery focused gate"),
                [304] = ("ExtremeTraitRuntime", "CropPlotBuildingPanelPresenter|CropPlotRuntime", "CropPlotSaveData v3 + CharacterIdentityStateStore", "golden harvest save/worker/hash gate"),
                [305] = ("ExtremeTraitRuntime", "ProductionBuildingPanelPresenter|ProductionBillSceneFacade", "Production bill v7 + CharacterIdentityStateStore", "production lease/atomic-start gate"),
                [306] = ("ArcaneOverchargeCommandRuntime", "StaffManagementSurfacePanel|CharacterCombatCommandRuntime", "body-health v5 + equipment + CharacterIdentityStateStore", "mana spend/refund/overcharge gate")
            };
        foreach (int id in Enumerable.Range(300, 7))
        {
            CharacterTraitSO trait = founders.Single(value => value.id == id);
            (string runtime, string entry, string save, string test) = evidence[id];
            bool connected = entry.Split('|').All(symbol =>
                    LiveFileContainsSymbol(liveSources, symbol))
                && LiveFileContainsSymbol(liveSources, runtime);
            rows.Add(new Row
            {
                Kind = "extreme",
                Id = id.ToString(),
                Definition = AssetDatabase.GetAssetPath(trait),
                Producer = entry,
                Authority = runtime,
                Consumer = entry,
                Save = save,
                Observation = entry,
                Test = test,
                Status = connected ? "connected" : "orphan",
                Evidence = connected
                    ? "explicit runtime + non-Editor player/domain entrypoint"
                    : "missing runtime or player/domain entrypoint symbol"
            });
        }
    }

    private static void BuildGameplayCommandRows(
        IReadOnlyDictionary<string, string> liveSources,
        ICollection<Row> rows)
    {
        (Type Type, string Method)[] commands =
        {
            (typeof(ExtremeCraftInspirationRuntime), "RecordEligibleCompletion"),
            (typeof(ExtremeTraitRuntime), "TryActivateLastStand"),
            (typeof(ExtremeTraitRuntime), "EndLastStand"),
            (typeof(ExtremeTraitRuntime), "TryResolveForbiddenResearchLeap"),
            (typeof(ExtremeTraitRuntime), "TryResolveMiracleSurgery"),
            (typeof(ExtremeTraitRuntime), "TryScheduleGoldenHarvest"),
            (typeof(ExtremeTraitRuntime), "TryResolveGoldenHarvest"),
            (typeof(ExtremeTraitRuntime), "TryBeginProductionLimitBreak"),
            (typeof(ExtremeTraitRuntime), "EndProductionLimitBreak"),
            (typeof(ExtremeTraitRuntime), "TryActivateArcaneOvercharge"),
            (typeof(CropPlotRuntime), "TryScheduleGoldenHarvest"),
            (typeof(ProductionBillSceneFacade), "TrySetEmergencyProduction"),
            (typeof(ArcaneOverchargeCommandRuntime), "TryActivate"),
            (typeof(CharacterRitualFastingRuntime), "TryBegin"),
            (typeof(CharacterRitualFastingRuntime), "TryComplete"),
            (typeof(CharacterRitualFastingRuntime), "TryBreak"),
            (typeof(CharacterBodyHealthRuntime), "TrySpendMana"),
            (typeof(CharacterBodyHealthRuntime), "RefundFailedManaSpend"),
            (typeof(CharacterIdentityEventPublisher), "Publish"),
            (typeof(CharacterActor), "ApplyMoodFactor"),
            (typeof(CharacterMoodPolicyService), "Apply"),
            (typeof(CharacterMoodPolicyService), "ApplySeconds"),
            (typeof(CharacterPersistentNeedRuntime), "MarkSatisfied"),
            (typeof(CharacterDirectOrderCostPreviewService), "Apply"),
            (typeof(CharacterRelationshipMemoryService), "Remember"),
            (typeof(CharacterRelationshipMemoryService), "TryForgive"),
            (typeof(CharacterApologyCommandRuntime), "TryApologize")
        };
        foreach ((Type type, string methodName) in commands)
        {
            MethodInfo method = type.GetMethods(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly)
                .SingleOrDefault(value => string.Equals(
                    value.Name,
                    methodName,
                    StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"Gameplay command '{type.Name}.{methodName}' is missing or ambiguous.");
            Attribute[] intent = ResolveIntentAttributes(method);
            Require(intent.Length == 1,
                $"Gameplay command '{type.Name}.{methodName}' requires exactly one intent attribute.");

            string authority;
            string observation;
            string evidence;
            bool connected;
            switch (intent[0])
            {
                case GameplayEntryPointAttribute entry:
                    authority = "GameplayEntryPoint";
                    observation = entry.ExecutionEvidence;
                    connected = FindLiveReferences(liveSources, methodName).Length >= 2;
                    evidence = entry.ExecutionEvidence;
                    break;
                case GameplayInternalOnlyAttribute internalOnly:
                    authority = "GameplayInternalOnly";
                    observation = "N/A: internal orchestration";
                    connected = LiveFileContainsSymbol(
                        liveSources,
                        internalOnly.AllowedCallerScope);
                    evidence = internalOnly.Reason + " callers="
                        + internalOnly.AllowedCallerScope;
                    break;
                case GameplayMigrationOnlyAttribute migration:
                    authority = "GameplayMigrationOnly";
                    observation = "N/A: migration-only";
                    connected = true;
                    evidence = migration.Reason + " removal="
                        + migration.RemovalCondition;
                    break;
                default:
                    throw new InvalidOperationException("Unknown gameplay command intent.");
            }

            rows.Add(new Row
            {
                Kind = "gameplay-command",
                Id = type.Name + "." + methodName,
                Definition = type.FullName,
                Producer = authority,
                Authority = type.Name,
                Consumer = evidence,
                Save = "domain authority owned by command target",
                Observation = observation,
                Test = "V26FounderTraitConnectivityManifestScenario.Run + focused path audit",
                Status = connected ? "connected" : "orphan",
                Evidence = connected
                    ? "intent and allowed live caller evidence found"
                    : "intent exists but allowed live caller evidence is missing"
            });
        }
    }

    private static void BuildPublicRuntimeApiRows(
        IReadOnlyDictionary<string, string> liveSources,
        ICollection<Row> rows)
    {
        foreach ((Type type, string sourcePath) in LoadAuditedTypes(liveSources))
        foreach (MethodInfo method in type.GetMethods(
                     BindingFlags.Instance
                     | BindingFlags.Static
                     | BindingFlags.Public
                     | BindingFlags.DeclaredOnly)
                 .Where(value => !value.IsSpecialName)
                 .OrderBy(value => value.Name, StringComparer.Ordinal)
                 .ThenBy(value => value.GetParameters().Length))
        {
            string signature = MethodSignature(method);
            Attribute[] intent = method.GetCustomAttributes(false)
                .OfType<Attribute>()
                .Where(value => value is GameplayEntryPointAttribute
                    or GameplayInternalOnlyAttribute
                    or GameplayMigrationOnlyAttribute)
                .ToArray();
            bool mutates = IsRuntimeMutationSurface(sourcePath, method);
            string[] references = FindMethodCallReferences(
                liveSources,
                method.Name);
            bool connected = mutates
                ? intent.Length == 1 && HasIntentEvidence(intent[0], liveSources)
                : references.Length > 0;
            rows.Add(new Row
            {
                Kind = "public-runtime-api",
                Id = signature,
                Definition = sourcePath,
                Producer = mutates
                    ? intent.Length == 1 ? intent[0].GetType().Name : string.Empty
                    : "read/validation/data API",
                Authority = mutates ? type.Name : "N/A: query or pure contract",
                Consumer = string.Join("<br>", references),
                Save = mutates
                    ? "domain authority or explicitly classified lifecycle"
                    : "N/A: no mutation",
                Observation = IntentObservation(intent),
                Test = "automatic public API enumeration + focused/full runtime gates",
                Status = connected ? "connected" : "orphan",
                Evidence = connected
                    ? mutates
                        ? "exactly one intent attribute and live evidence"
                        : "at least one non-Editor call/reference beyond the declaration"
                    : mutates
                        ? $"mutation surface requires one intent attribute; found {intent.Length}"
                        : "public query/data method has no non-Editor call/reference"
            });
        }
    }

    private static void BuildIdentitySerializedFieldRows(
        IReadOnlyDictionary<string, string> liveSources,
        ICollection<Row> rows)
    {
        Type[] ruleTypes = typeof(CharacterIdentityRule).Assembly.GetTypes()
            .Where(value => value != typeof(CharacterIdentityRule)
                && typeof(CharacterIdentityRule).IsAssignableFrom(value)
                && !value.IsAbstract)
            .OrderBy(value => value.Name, StringComparer.Ordinal)
            .ToArray();
        foreach (Type type in ruleTypes)
        foreach (FieldInfo field in type.GetFields(
                     BindingFlags.Instance
                     | BindingFlags.Public
                     | BindingFlags.DeclaredOnly)
                 .OrderBy(value => value.Name, StringComparer.Ordinal))
        {
            string[] consumers = liveSources
                .Where(value => !value.Key.EndsWith(
                    "Services/Foundation/CharacterIdentityRules.cs",
                    StringComparison.Ordinal)
                    && Regex.IsMatch(
                        value.Value,
                        @"\b" + Regex.Escape(field.Name) + @"\b"))
                .Select(value => value.Key)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            rows.Add(new Row
            {
                Kind = "identity-serialized-field",
                Id = type.Name + "." + field.Name,
                Definition = "Services/Foundation/CharacterIdentityRules.cs",
                Producer = "CharacterTraitSO.identityRules SerializeReference authoring",
                Authority = type.Name,
                Consumer = string.Join("<br>", consumers),
                Save = "definition SO; mutable state is stored separately by traitDefinitionId+ruleId",
                Observation = "trait tooltip or owning domain outcome",
                Test = "automatic serialized-field enumeration + definition/runtime audit",
                Status = consumers.Length > 0 ? "connected" : "orphan",
                Evidence = consumers.Length > 0
                    ? "field name is consumed outside its definition/validator file"
                    : "serialized rule field is never consumed by live runtime code"
            });
        }

        foreach ((Type type, string sourcePath) in LoadAuditedTypes(liveSources))
        {
            if (typeof(CharacterIdentityRule).IsAssignableFrom(type))
                continue;
            foreach (FieldInfo field in type.GetFields(
                         BindingFlags.Instance
                         | BindingFlags.Public
                         | BindingFlags.DeclaredOnly)
                     .Where(value => !value.IsInitOnly && !value.IsLiteral)
                     .OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                GameplayMigrationOnlyAttribute migration = field
                    .GetCustomAttribute<GameplayMigrationOnlyAttribute>(false)
                    ?? type.GetCustomAttribute<GameplayMigrationOnlyAttribute>(false);
                string[] consumers = FindSerializedFieldConsumers(
                    liveSources,
                    sourcePath,
                    field.Name);
                bool connected = migration != null
                    ? !string.IsNullOrWhiteSpace(migration.Reason)
                        && !string.IsNullOrWhiteSpace(migration.RemovalCondition)
                    : consumers.Length > 0;
                rows.Add(new Row
                {
                    Kind = "runtime-serialized-field",
                    Id = type.Name + "." + field.Name,
                    Definition = sourcePath,
                    Producer = migration != null
                        ? "GameplayMigrationOnly"
                        : "serialized/runtime authoring",
                    Authority = type.Name,
                    Consumer = migration != null
                        ? migration.Reason
                        : string.Join("<br>", consumers),
                    Save = migration != null
                        ? migration.RemovalCondition
                        : "definition or owning runtime state contract",
                    Observation = migration != null
                        ? "N/A: migration-only payload"
                        : "owning domain outcome or definition UI",
                    Test = "automatic serialized-field enumeration + focused/full runtime gates",
                    Status = connected ? "connected" : "orphan",
                    Evidence = connected
                        ? migration != null
                            ? "explicit migration reason and removal condition"
                            : "field has an owning live runtime read/write path"
                        : "public serialized/runtime field has no live owning consumer"
                });
            }
        }
    }

    private static void BuildNonPublicRuntimeHelperRows(
        IReadOnlyDictionary<string, string> liveSources,
        ICollection<Row> rows)
    {
        foreach ((Type type, string sourcePath) in LoadAuditedTypes(liveSources))
        foreach (MethodInfo method in type.GetMethods(
                     BindingFlags.Instance
                     | BindingFlags.Static
                     | BindingFlags.NonPublic
                     | BindingFlags.DeclaredOnly)
                 .Where(value => !value.IsSpecialName
                     && !value.IsAbstract
                     && value.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() == null
                     && !value.Name.Contains('<'))
                 .OrderBy(value => value.Name, StringComparer.Ordinal)
                 .ThenBy(value => value.GetParameters().Length))
        {
            string[] references = FindNonPublicMethodReferences(
                liveSources,
                sourcePath,
                method.Name);
            rows.Add(new Row
            {
                Kind = "nonpublic-runtime-helper",
                Id = MethodSignature(method),
                Definition = sourcePath,
                Producer = method.IsPrivate
                    ? "private helper"
                    : method.IsFamily
                        ? "protected helper"
                        : "internal helper",
                Authority = type.Name,
                Consumer = string.Join("<br>", references),
                Save = "N/A: helper follows owning public/domain authority",
                Observation = "N/A: observed through owning entry point",
                Test = "automatic non-public method enumeration + focused/full runtime gates",
                Status = references.Length > 0 ? "connected" : "orphan",
                Evidence = references.Length > 0
                    ? "method has a call, delegate subscription, or internal cross-file reference beyond its declaration"
                    : "non-public helper is declaration-only dead code"
            });
        }
    }

    private static string[] FindSerializedFieldConsumers(
        IReadOnlyDictionary<string, string> sources,
        string definitionPath,
        string fieldName)
    {
        Regex qualified = new(@"\.\s*" + Regex.Escape(fieldName) + @"\b");
        Regex word = new(@"\b" + Regex.Escape(fieldName) + @"\b");
        return sources
            .Where(value => qualified.IsMatch(value.Value)
                || string.Equals(value.Key, definitionPath, StringComparison.Ordinal)
                && word.Matches(value.Value).Count > 1)
            .Select(value => value.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] FindNonPublicMethodReferences(
        IReadOnlyDictionary<string, string> sources,
        string definitionPath,
        string methodName)
    {
        Regex word = new(@"\b" + Regex.Escape(methodName) + @"\b");
        Regex declaration = new(
            @"\b(?:private|protected|internal)\s+(?:(?:static|virtual|override|sealed|async)\s+)*[^;{}=]+\b"
            + Regex.Escape(methodName)
            + @"\s*\(");
        return sources
            .Where(value =>
            {
                int mentions = word.Matches(value.Value).Count;
                if (mentions == 0)
                    return false;
                int declarations = declaration.Matches(value.Value).Count;
                return !string.Equals(value.Key, definitionPath, StringComparison.Ordinal)
                    || mentions > declarations;
            })
            .Select(value => value.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<(Type Type, string SourcePath)> LoadAuditedTypes(
        IReadOnlyDictionary<string, string> liveSources)
    {
        string[] roots =
        {
            "Assets/Scripts/Services/Effects/Runtime/",
            "Assets/Scripts/Services/Character/Identity/Runtime/"
        };
        string[] exact =
        {
            "Assets/Scripts/Content/CharacterTraitSO.cs",
            "Assets/Scripts/Services/Foundation/GameplayEffectContracts.cs",
            "Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs",
            "Assets/Scripts/Services/Foundation/GameplayEffectConditionDefinitionSO.cs",
            "Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs"
        };
        Regex declaration = new(
            @"\bpublic\s+(?:(?:sealed|abstract|readonly)\s+)*(?:class|struct)\s+([A-Za-z_]\w*)",
            RegexOptions.CultureInvariant);
        Dictionary<string, Type[]> typesByName = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(value => value.GetName().Name.StartsWith(
                    "DungeonStory",
                    StringComparison.Ordinal)
                || value.GetName().Name.StartsWith(
                    "Assembly-CSharp",
                    StringComparison.Ordinal))
            .SelectMany(SafeGetTypes)
            .Where(value => !value.IsGenericTypeDefinition)
            .GroupBy(value => value.Name, StringComparer.Ordinal)
            .ToDictionary(
                value => value.Key,
                value => value.ToArray(),
                StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> source in liveSources
                     .Where(value => roots.Any(root => value.Key.StartsWith(
                             root,
                             StringComparison.Ordinal))
                         || exact.Contains(value.Key, StringComparer.Ordinal))
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        foreach (Match match in declaration.Matches(source.Value).Cast<Match>())
        {
            string name = match.Groups[1].Value;
            if (!typesByName.TryGetValue(name, out Type[] matches)
                || matches.Length != 1)
                throw new InvalidOperationException(
                    $"Audited public type '{name}' from '{source.Key}' is missing or ambiguous.");
            yield return (matches[0], source.Key);
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(value => value != null);
        }
    }

    private static bool IsRuntimeMutationSurface(string sourcePath, MethodInfo method)
    {
        if (!sourcePath.Contains("/Runtime/", StringComparison.Ordinal))
            return false;
        string name = method.Name;
        if (name.StartsWith("Get", StringComparison.Ordinal)
            || name.StartsWith("Can", StringComparison.Ordinal)
            || name.StartsWith("Is", StringComparison.Ordinal)
            || name.StartsWith("Resolve", StringComparison.Ordinal)
            || name.StartsWith("Project", StringComparison.Ordinal)
            || name.StartsWith("Collect", StringComparison.Ordinal)
            || name.StartsWith("Preview", StringComparison.Ordinal)
            || name.StartsWith("Capture", StringComparison.Ordinal)
            || name.StartsWith("Clone", StringComparison.Ordinal)
            || name.StartsWith("Build", StringComparison.Ordinal)
            || name.StartsWith("TryGet", StringComparison.Ordinal)
            || name is "Equals" or "GetHashCode" or "ToString")
            return false;
        return method.ReturnType == typeof(void)
            || name.StartsWith("Try", StringComparison.Ordinal)
            || name.StartsWith("Apply", StringComparison.Ordinal)
            || name.StartsWith("Set", StringComparison.Ordinal)
            || name.StartsWith("Remove", StringComparison.Ordinal)
            || name.StartsWith("Record", StringComparison.Ordinal)
            || name.StartsWith("Remember", StringComparison.Ordinal)
            || name.StartsWith("Refund", StringComparison.Ordinal)
            || name.StartsWith("Refresh", StringComparison.Ordinal)
            || name.StartsWith("Expire", StringComparison.Ordinal);
    }

    private static bool HasIntentEvidence(
        Attribute intent,
        IReadOnlyDictionary<string, string> liveSources) => intent switch
    {
        GameplayEntryPointAttribute entry =>
            !string.IsNullOrWhiteSpace(entry.ExecutionEvidence)
            && FindLiveReferences(liveSources, entry.ExecutionEvidence.Split('|')[0]).Length > 0,
        GameplayInternalOnlyAttribute internalOnly =>
            !string.IsNullOrWhiteSpace(internalOnly.Reason)
            && LiveFileContainsSymbol(liveSources, internalOnly.AllowedCallerScope),
        GameplayMigrationOnlyAttribute migration =>
            !string.IsNullOrWhiteSpace(migration.Reason)
            && !string.IsNullOrWhiteSpace(migration.RemovalCondition),
        _ => false
    };

    private static Attribute[] ResolveIntentAttributes(MethodInfo method)
    {
        Attribute[] direct = method.GetCustomAttributes(false)
            .OfType<Attribute>()
            .Where(IsIntentAttribute)
            .ToArray();
        if (direct.Length > 0)
            return direct;
        Type[] parameters = method.GetParameters()
            .Select(value => value.ParameterType)
            .ToArray();
        for (Type current = method.DeclaringType?.BaseType;
             current != null;
             current = current.BaseType)
        {
            MethodInfo inherited = current.GetMethod(
                method.Name,
                BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly,
                null,
                parameters,
                null);
            Attribute[] inheritedIntent = inherited?.GetCustomAttributes(false)
                .OfType<Attribute>()
                .Where(IsIntentAttribute)
                .ToArray() ?? Array.Empty<Attribute>();
            if (inheritedIntent.Length > 0)
                return inheritedIntent;
        }
        return direct;
    }

    private static bool IsIntentAttribute(Attribute value) =>
        value is GameplayEntryPointAttribute
            or GameplayInternalOnlyAttribute
            or GameplayMigrationOnlyAttribute;

    private static string IntentObservation(IEnumerable<Attribute> intent)
    {
        Attribute value = intent.SingleOrDefault();
        return value switch
        {
            GameplayEntryPointAttribute entry => entry.ExecutionEvidence,
            GameplayInternalOnlyAttribute internalOnly => internalOnly.AllowedCallerScope,
            GameplayMigrationOnlyAttribute migration => migration.RemovalCondition,
            _ => string.Empty
        };
    }

    private static string[] FindMethodCallReferences(
        IReadOnlyDictionary<string, string> sources,
        string methodName)
    {
        Regex call = new(@"\b" + Regex.Escape(methodName) + @"\s*\(");
        return sources
            .Where(value => call.Matches(value.Value).Count > 1
                || call.IsMatch(value.Value)
                && !Regex.IsMatch(
                    value.Value,
                    @"\bpublic\s+[^;{}=]+\b" + Regex.Escape(methodName) + @"\s*\("))
            .Select(value => value.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string MethodSignature(MethodInfo method) =>
        method.DeclaringType?.Name + "." + method.Name + "("
        + string.Join(",", method.GetParameters().Select(value =>
            value.ParameterType.Name)) + ")";

    private static void AddLiveReferenceRow(
        ICollection<Row> rows,
        IReadOnlyDictionary<string, string> liveSources,
        string kind,
        string id,
        IEnumerable<string> definitions,
        string producerRole,
        string consumer,
        string save,
        string observation)
    {
        string semanticEvidence = SemanticProducerSymbols(kind, id);
        string[] requiredSymbols = string.IsNullOrWhiteSpace(semanticEvidence)
            ? new[] { id }
            : semanticEvidence.Split('|');
        bool connected = requiredSymbols.All(symbol =>
            LiveFileContainsSymbol(liveSources, symbol));
        string[] references = requiredSymbols
            .SelectMany(symbol => FindLiveReferences(liveSources, symbol))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        rows.Add(new Row
        {
            Kind = kind,
            Id = id,
            Definition = string.Join("<br>", definitions.Distinct()),
            Producer = references.Length > 0
                ? producerRole + "<br>" + string.Join("<br>", references)
                : string.Empty,
            Authority = consumer,
            Consumer = consumer,
            Save = save,
            Observation = observation,
            Test = "V26FounderTraitConnectivityManifestScenario.Run + focused runtime gate",
            Status = connected ? "connected" : "orphan",
            Evidence = connected
                ? "explicit semantic producer chain found"
                : "missing semantic producer symbol: "
                    + string.Join(",", requiredSymbols.Where(symbol =>
                        !LiveFileContainsSymbol(liveSources, symbol)))
        });
    }

    private static string SemanticProducerSymbols(string kind, string id) =>
        (kind, id) switch
        {
            ("identity-event-action", "combat:inactive-five-days") or
            ("identity-event-action", "food:bland-streak") or
            ("identity-event-action", "food:no-sweets") or
            ("identity-event-action", "research:no-access") or
            ("identity-event-action", "work:repetitive-three-days") =>
                "CharacterPersistentNeedClock|rule.deprivedEventId",
            ("identity-event-action", "stockpile:emergency-ready") or
            ("identity-event-action", "stockpile:emergency-shortage") =>
                "CharacterPersistentNeedClock|emergencyRule.satisfiedEventId|emergencyRule.deprivedEventId",
            ("identity-event-action", "research:completed") =>
                "BlueprintResearchRuntime|ResearchIdentityEventAdapter|research:{e.OutcomeId}",
            ("identity-event-action", "ritual:fast-completed") =>
                "CharacterRitualFastingRuntime|rule.satisfiedEventId",
            ("identity-event-action", "ritual:fast-broken") =>
                "CharacterRitualFastingRuntime|rule.deprivedEventId",
            ("identity-event-action", "social:betrayal-or-assault") =>
                "SocialConflictEvent|betrayal-or-assault|SocialIdentityEventAdapter",
            ("identity-event-action", "social:insulted") =>
                "SocialConflictEvent|\"insulted\"|SocialIdentityEventAdapter",
            ("identity-event-action", "social:public-question") =>
                "SocialConflictEvent|public-question|SocialIdentityEventAdapter",
            ("persistent-need", "need:large-meal") =>
                "CharacterPersistentNeedRuntime|MealIdentityEventAdapter|food:sated|food:meal-missed",
            ("persistent-need", "need:luxury-standard") =>
                "CharacterPersistentNeedRuntime|MealIdentityEventAdapter|living:luxury-satisfied|living:basic-only",
            _ => string.Empty
        };

    private static string TargetConsumer(string id) => id switch
    {
        "arcane:mana-recovery" => "CharacterBodyHealthRuntime",
        "arcane:power" => "CombatResolutionService",
        "character:accident-chance" => "CharacterStatsProjectionService",
        "character:alarm-response-delay" => "CharacterAlarmResponseRuntime",
        "character:cold-exposure" => "CharacterThermalGameplayEffectProjection",
        "character:combat-power" => "CharacterStatsProjectionService",
        "character:combat-stress" => "CharacterIdentityRuntime",
        "character:consumption" => "CharacterStatsProjectionService",
        "character:crowd-sensitivity" => "CharacterStatsProjectionService",
        "character:danger-detection" => "CharacterAiNaturalness",
        "character:disease-recovery-speed" => "PopulationHealthAggregateState|ResolveContagiousDurationDays",
        "character:disease-resistance" => "CharacterPopulationDiseaseModifierQuery|GameplayEffectTargetIds.DiseaseResistance",
        "character:earned-work-xp" => "CharacterProficiencyLearningRules|GameplayEffectTargetIds.EarnedWorkExperience",
        "character:fatigue-rate" => "CharacterStats",
        "character:food-poisoning-chance" => "CharacterConsumablesRuntime",
        "character:heat-exposure" => "CharacterThermalGameplayEffectProjection",
        "character:immunity-gain" => "PopulationHealthAggregateState|ResolveImmunityAward",
        "character:immunity-retention" => "PopulationHealthAggregateState|ResolveDailyImmunityDecay",
        "character:mentee-xp" => "CareerApplicationAdapter",
        "character:move-speed" => "CharacterStatsProjectionService",
        "character:negative-mood-duration" => "CharacterIdentityRuntime",
        "character:pain-work-penalty" => "CharacterStatsProjectionService",
        "character:recovery-speed" => "CharacterStats",
        "character:relationship-recovery" => "CharacterRelationshipMemoryService|GameplayEffectTargetIds.RelationshipRecovery",
        "character:research-speed" => "CharacterStatsProjectionService",
        "character:sleep-recovery" => "CharacterStats",
        "character:spending" => "CharacterStatsProjectionService",
        "character:wait-patience" => "CharacterStatsProjectionService",
        "character:work-speed" => "CharacterStatsProjectionService",
        "craft:quality-score" => "CombatEquipmentCraftingRuntime|ApparelWorkOrderRuntime",
        "damage:blunt-taken" => "CombatCommandResultApplier|DefenseCombatExecutor",
        "environment:comfort-minimum-offset" => "CharacterThermalGameplayEffectProjection",
        "environment:safe-minimum-offset" => "CharacterThermalGameplayEffectProjection",
        "food:spoilage-detection" => "CharacterConsumablesRuntime",
        "harvest:seed-yield" => "CropPlotRuntime",
        "harvest:yield" => "CropPlotRuntime",
        "medical:aftermath-duration" => "SurgeryRuntime",
        "proficiency:construction-engineering:starting-xp" or
        "proficiency:crafting:starting-xp" or
        "proficiency:fieldwork:starting-xp" or
        "proficiency:food-production:starting-xp" or
        "proficiency:medicine:starting-xp" or
        "proficiency:melee-combat:starting-xp" or
        "proficiency:ranged-combat:starting-xp" or
        "proficiency:scholarship:starting-xp" or
        "proficiency:social:starting-xp" => "StartPartyPreparationService|CharacterTraitStartingProficiencyRules",
        "social:negotiation" => "StaffDiscontentRuntime",
        "work:haul-capacity" => "CharacterCarryInventory",
        "work:salvage-yield" => "CombatEquipmentCraftingRuntime|CombatEquipmentRuntime|ApparelWorkOrderRuntime",
        _ => string.Empty
    };

    private static void AuditLegacyBoundaries(
        IReadOnlyDictionary<string, string> liveSources)
    {
        Require(!LiveFileContainsSymbol(liveSources, "CharacterTraitReactionRuntime")
                && !LiveFileContainsSymbol(liveSources, "ICharacterTraitReactionService")
                && !LiveFileContainsSymbol(liveSources, "CharacterTraitReactionEvent"),
            "Retired moodReactions runtime path remains in live code.");
        Require(LiveFileContainsSymbol(liveSources, "traitSelectionAuthorityVersion")
                && LiveFileContainsSymbol(liveSources, "traitSelectionAuthorityOrigin"),
            "Trait selection authority version/origin is not enforced in live code.");
    }

    private static Dictionary<string, string> LoadLiveSources()
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(
                     "Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            string normalized = file.Replace('\\', '/');
            if (normalized.Contains("/Editor/", StringComparison.Ordinal)
                || normalized.Contains("/Tests/", StringComparison.Ordinal)
                || normalized.EndsWith("V26FounderTraitConnectivityManifestScenario.cs",
                    StringComparison.Ordinal))
                continue;
            result[normalized] = File.ReadAllText(file);
        }
        return result;
    }

    private static string[] FindLiveReferences(
        IReadOnlyDictionary<string, string> sources,
        string literal) => sources
        .Where(value => value.Value.Contains(literal, StringComparison.Ordinal))
        .Select(value => value.Key)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static bool LiveFileContainsSymbol(
        IReadOnlyDictionary<string, string> sources,
        string symbols) => symbols.Split('|')
        .All(symbol => sources.Any(value =>
            value.Value.Contains(symbol, StringComparison.Ordinal)));

    private static void Add(
        IDictionary<string, List<string>> target,
        string id,
        string definition)
    {
        string normalized = id?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            return;
        if (!target.TryGetValue(normalized, out List<string> definitions))
        {
            definitions = new List<string>();
            target.Add(normalized, definitions);
        }
        definitions.Add(definition);
    }

    private static bool IsFounder(CharacterTraitSO trait) => trait != null
        && (RetainedFounderIds.Contains(trait.id)
            || trait.id is >= 247 and <= 259
            || trait.id is >= 300 and <= 306
            || trait.id is >= 400 and <= 417
            || trait.id is >= 500 and <= 518);

    private static void WriteReport(IReadOnlyList<Row> rows, IReadOnlyList<Row> orphans)
    {
        StringBuilder report = new();
        report.AppendLine("# V26 창립자 특성 source-to-consumer manifest");
        report.AppendLine();
        report.AppendLine("이 파일은 Unity Editor 감사가 현재 카탈로그와 운영 코드에서 생성한다. Editor/테스트/빌더만 있는 참조는 live evidence에서 제외한다.");
        report.AppendLine();
        report.AppendLine($"- 전체 행: {rows.Count}");
        report.AppendLine($"- 연결: {rows.Count - orphans.Count}");
        report.AppendLine($"- 고아: {orphans.Count}");
        report.AppendLine();
        report.AppendLine("| kind | symbol-or-id | definition | live-producer | authority | live-consumer | save-or-recompute | player/ai-observation | deterministic-test | status | evidence |");
        report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (Row row in rows.OrderBy(value => value.Kind, StringComparer.Ordinal)
                     .ThenBy(value => value.Id, StringComparer.Ordinal))
        {
            report.Append('|').Append(Escape(row.Kind))
                .Append('|').Append(Escape(row.Id))
                .Append('|').Append(Escape(row.Definition))
                .Append('|').Append(Escape(row.Producer))
                .Append('|').Append(Escape(row.Authority))
                .Append('|').Append(Escape(row.Consumer))
                .Append('|').Append(Escape(row.Save))
                .Append('|').Append(Escape(row.Observation))
                .Append('|').Append(Escape(row.Test))
                .Append('|').Append(Escape(row.Status))
                .Append('|').Append(Escape(row.Evidence)).AppendLine("|");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Artifacts/QA");
        File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
    }

    private static string Escape(string value) => (value ?? string.Empty)
        .Replace("|", "\\|")
        .Replace("\r", string.Empty)
        .Replace("\n", "<br>");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
