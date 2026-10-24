using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class R20IdentityProtocolDebugScenarios
{
    [MenuItem("DungeonStory/R20/Run Identity Protocol Scenarios")]
    public static void RunAll()
    {
        CharacterTraitSO[] sources = AssetDatabase
            .FindAssets("t:CharacterTraitSO", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterTraitSO>)
            .Where(value => value != null)
            .Distinct()
            .OrderBy(value => AssetDatabase.GetAssetPath(value),
                StringComparer.Ordinal)
            .ToArray();
        SourceSnapshot[] snapshots = sources
            .Select(SourceSnapshot.Capture)
            .ToArray();

        try
        {
            VerifyAnnotations();
            VerifyScopedKeys();
            VerifyLegacyProviderIdsRemainFree();
            VerifyKnownSources(sources);
            Debug.Log(
                "R20_IDENTITY_PROTOCOL_SCENARIOS=PASS; scoped keys; wrong protocol rejection; free provider ids; known sources; source JSON and dirty preserved");
        }
        finally
        {
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
    }

    private static void VerifyAnnotations()
    {
        RequireProtocolField(
            typeof(BehaviorUtilityRule),
            nameof(BehaviorUtilityRule.behaviorTag));
        RequireProtocolField(
            typeof(EventMoodRule),
            nameof(EventMoodRule.eventId));
        RequireProtocolField(
            typeof(MoodImmunityRule),
            nameof(MoodImmunityRule.eventId));
        RequireProtocolField(
            typeof(MoodTransformRule),
            nameof(MoodTransformRule.eventId));
        RequireProtocolField(
            typeof(PostActionConsequenceRule),
            nameof(PostActionConsequenceRule.actionTag));
        RequireProtocolField(
            typeof(RelationshipMemoryRule),
            nameof(RelationshipMemoryRule.eventId));
        RequireProtocolField(
            typeof(AutonomousWorkRestrictionRule),
            nameof(AutonomousWorkRestrictionRule.actionTag));
        RequireProtocolField(
            typeof(AutonomousWorkRestrictionRule),
            nameof(AutonomousWorkRestrictionRule.requiredConditionId));
        RequireProtocolField(
            typeof(IncidentWeightRule),
            nameof(IncidentWeightRule.incidentId));

        RequireNoProtocolField(
            typeof(PersistentNeedRule),
            nameof(PersistentNeedRule.satisfiedEventId));
        RequireNoProtocolField(
            typeof(PersistentNeedRule),
            nameof(PersistentNeedRule.deprivedEventId));
    }

    private static void VerifyScopedKeys()
    {
        IReadOnlyList<string> behaviors = Keys<BehaviorUtilityRule>(
            nameof(BehaviorUtilityRule.behaviorTag));
        foreach (WorkTypeId workTypeId in BuiltInWorkTypeIds.All)
        {
            RequireContains(
                behaviors,
                workTypeId.Value,
                "behavior work type");
        }
        RequireContains(
            behaviors,
            CharacterIdentityRuleProtocols.MinorAlert,
            "minor alert behavior");
        RequireContains(behaviors, "work:emergency-check", "behavior");
        RequireContains(behaviors, "survival:primitive", "behavior");

        IReadOnlyList<string> eventMood = Keys<EventMoodRule>(
            nameof(EventMoodRule.eventId));
        IReadOnlyList<string> immunity = Keys<MoodImmunityRule>(
            nameof(MoodImmunityRule.eventId));
        IReadOnlyList<string> transform = Keys<MoodTransformRule>(
            nameof(MoodTransformRule.eventId));
        Require(eventMood.SequenceEqual(immunity, StringComparer.Ordinal)
                && eventMood.SequenceEqual(transform, StringComparer.Ordinal),
            "Mood rule types did not share the actual mood event protocol.");
        RequireContains(eventMood, "social:sincere-apology", "mood event");
        RequireContains(eventMood, "mood:hostile-kill-guilt", "mood event");
        RequireContains(eventMood, "event:panic", "mood event");

        IReadOnlyList<string> relationship = Keys<RelationshipMemoryRule>(
            nameof(RelationshipMemoryRule.eventId));
        Require(relationship.SequenceEqual(
                new[] { "betrayal-or-assault", "insulted" },
                StringComparer.Ordinal),
            "Relationship memory protocol did not match its two raw conflict ids.");

        RequireContains(
            Keys<PostActionConsequenceRule>(
                nameof(PostActionConsequenceRule.actionTag)),
            "order:defer-cleaning",
            "post action");
        RequireContains(
            Keys<AutonomousWorkRestrictionRule>(
                nameof(AutonomousWorkRestrictionRule.actionTag)),
            "work:dangerous",
            "autonomous action");
        RequireContains(
            Keys<AutonomousWorkRestrictionRule>(
                nameof(AutonomousWorkRestrictionRule.requiredConditionId)),
            "condition:no-safe-alternative",
            "autonomous condition");
        Require(Keys<IncidentWeightRule>(
                nameof(IncidentWeightRule.incidentId)).Count == 0,
            "Incident protocol invented an id without a live producer.");

        RequireRejected<RelationshipMemoryRule>(
            nameof(RelationshipMemoryRule.eventId),
            "social:sincere-apology");
        RequireRejected<RelationshipMemoryRule>(
            nameof(RelationshipMemoryRule.eventId),
            "social:betrayal-or-assault");
        RequireRejected<EventMoodRule>(
            nameof(EventMoodRule.eventId),
            "work:dangerous");
        RequireRejected<BehaviorUtilityRule>(
            nameof(BehaviorUtilityRule.behaviorTag),
            "event:panic");
        Require(CharacterIdentityRuleProtocols.Keys(
                    typeof(EventMoodRule),
                    nameof(PostActionConsequenceRule.actionTag)).Count == 0,
            "A rule accepted a key under the wrong member protocol.");
        Require(CharacterIdentityRuleProtocols.Keys(
                    typeof(CharacterIdentityRule),
                    nameof(EventMoodRule.eventId)).Count == 0,
            "The base rule type received a concrete rule protocol.");
    }

    private static void VerifyLegacyProviderIdsRemainFree()
    {
        PersistentNeedRule provider = new()
        {
            ruleId = "need:fixture",
            needId = "need:fixture",
            satisfiedEventId = "fixture:any-satisfied-id",
            deprivedEventId = "fixture:any-deprived-id"
        };
        Require(!provider.Validate().Any(),
            "Persistent need provider ids gained a consumer membership rule.");
    }

    private static void VerifyKnownSources(
        IEnumerable<CharacterTraitSO> sources)
    {
        int checkedValues = 0;
        foreach (CharacterTraitSO source in sources)
        {
            string path = AssetDatabase.GetAssetPath(source);
            foreach (CharacterIdentityRule rule in source.identityRules
                         ?? new List<CharacterIdentityRule>())
            {
                if (rule == null)
                    continue;
                foreach (FieldInfo field in rule.GetType().GetFields(
                             BindingFlags.Instance | BindingFlags.Public))
                {
                    ContentReferenceAttribute reference = field
                        .GetCustomAttributes(
                            typeof(ContentReferenceAttribute),
                            inherit: true)
                        .OfType<ContentReferenceAttribute>()
                        .FirstOrDefault(value =>
                            value.Kind
                                == ContentReferenceKind.IdentityRuleProtocol);
                    if (reference == null)
                        continue;
                    string key = field.GetValue(rule) as string
                        ?? string.Empty;
                    IReadOnlyList<string> choices =
                        CharacterIdentityRuleProtocols.Keys(
                            rule.GetType(),
                            field.Name);
                    Require(choices.Contains(key, StringComparer.Ordinal),
                        path + " has unsupported " + rule.GetType().Name
                        + "." + field.Name + " key '" + key + "'.");
                    checkedValues++;
                }
            }
        }
        Require(checkedValues > 0,
            "No authored identity protocol consumers were found.");
    }

    private static IReadOnlyList<string> Keys<TRule>(string memberName)
        where TRule : CharacterIdentityRule =>
        CharacterIdentityRuleProtocols.Keys(typeof(TRule), memberName);

    private static void RequireProtocolField(Type type, string memberName)
    {
        ContentReferenceAttribute reference = RequireField(type, memberName)
            .GetCustomAttributes(
                typeof(ContentReferenceAttribute),
                inherit: true)
            .OfType<ContentReferenceAttribute>()
            .SingleOrDefault(value =>
                value.Kind == ContentReferenceKind.IdentityRuleProtocol);
        Require(reference != null,
            type.Name + "." + memberName
            + " is missing IdentityRuleProtocol authoring metadata.");
    }

    private static void RequireNoProtocolField(Type type, string memberName)
    {
        bool found = RequireField(type, memberName)
            .GetCustomAttributes(
                typeof(ContentReferenceAttribute),
                inherit: true)
            .OfType<ContentReferenceAttribute>()
            .Any(value =>
                value.Kind == ContentReferenceKind.IdentityRuleProtocol);
        Require(!found,
            type.Name + "." + memberName
            + " must remain a free provider id.");
    }

    private static FieldInfo RequireField(Type type, string memberName) =>
        type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public)
        ?? throw new InvalidOperationException(
            "Missing identity rule field " + type.Name + "." + memberName + ".");

    private static void RequireContains(
        IReadOnlyList<string> values,
        string expected,
        string context)
    {
        Require(values.Contains(expected, StringComparer.Ordinal),
            "Missing " + context + " key '" + expected + "'.");
    }

    private static void RequireRejected<TRule>(
        string memberName,
        string key)
        where TRule : CharacterIdentityRule
    {
        Require(!Keys<TRule>(memberName).Contains(
                key,
                StringComparer.Ordinal),
            typeof(TRule).Name + "." + memberName
            + " accepted wrong protocol key '" + key + "'.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct SourceSnapshot
    {
        private SourceSnapshot(
            CharacterTraitSO asset,
            string path,
            string json,
            bool dirty)
        {
            this.asset = asset;
            this.path = path;
            this.json = json;
            this.dirty = dirty;
        }

        private readonly CharacterTraitSO asset;
        private readonly string path;
        private readonly string json;
        private readonly bool dirty;

        public static SourceSnapshot Capture(CharacterTraitSO asset) =>
            new(
                asset,
                AssetDatabase.GetAssetPath(asset),
                EditorJsonUtility.ToJson(asset),
                EditorUtility.IsDirty(asset));

        public void RequireUnchanged()
        {
            Require(EditorJsonUtility.ToJson(asset) == json,
                path + " JSON changed during identity protocol validation.");
            Require(EditorUtility.IsDirty(asset) == dirty,
                path + " dirty state changed during identity protocol validation.");
        }
    }
}
