using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R20ReverseMembershipDebugScenarios
{
    [MenuItem("DungeonStory/QA/R20 Validate Reverse Membership")]
    public static void RunAll()
    {
        Require(!Application.isPlaying,
            "R20 reverse-membership verification must run before Play mode.");

        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        ScriptableObject[] sources = content
            .GetAll<SpeciesCultureDefinitionSO>().Cast<ScriptableObject>()
            .Concat(content.GetAll<CulturalPracticeDefinitionSO>())
            .Concat(content.GetAll<FactionArcDefinitionSO>())
            .Concat(content.GetAll<FactionContractDefinitionSO>())
            .Distinct()
            .ToArray();
        SourceSnapshot[] snapshots = sources
            .Select(SourceSnapshot.Capture)
            .ToArray();

        try
        {
            V20StoryContentCatalog stories = new(content);
            VerifyCurrentSets(content, stories);
            VerifyMissingCultureChildRejectsAndRepairs(content);
            VerifyMissingOrdinaryContractRejectsAndRepairs(content, stories);
            Debug.Log(
                "R20_REVERSE_MEMBERSHIP=PASS; current exact sets; "
                + "missing culture child rejected; missing ordinary arc relation rejected; "
                + "seasonal contract excluded; repair retry; source JSON and dirty state unchanged");
        }
        finally
        {
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
    }

    private static void VerifyCurrentSets(
        ResourceGameContentCatalog content,
        V20StoryContentCatalog stories)
    {
        CulturalPracticeDefinitionSO[] practices = content
            .GetAll<CulturalPracticeDefinitionSO>()
            .ToArray();
        foreach (SpeciesCultureDefinitionSO culture in
                 content.GetAll<SpeciesCultureDefinitionSO>())
        {
            string[] expected = practices
                .Where(value => string.Equals(
                    value.cultureId,
                    culture.StableId,
                    StringComparison.Ordinal))
                .Select(value => value.StableId)
                .ToArray();
            Require(culture.ceremonyIds != null
                    && culture.ceremonyIds.Count == expected.Length
                    && culture.ceremonyIds.Distinct(StringComparer.Ordinal).Count()
                        == culture.ceremonyIds.Count
                    && culture.ceremonyIds.ToHashSet(StringComparer.Ordinal)
                        .SetEquals(expected),
                $"Culture '{culture.StableId}' does not list its exact practice set.");
        }

        FactionContractDefinitionSO[] ordinary = stories.Contracts
            .Where(value => string.IsNullOrEmpty(value.seasonalEventId))
            .ToArray();
        FactionContractDefinitionSO[] seasonal = stories.Contracts
            .Where(value => !string.IsNullOrEmpty(value.seasonalEventId))
            .ToArray();
        Require(ordinary.Length == 18 && seasonal.Length == 6,
            $"Expected 18 ordinary and 6 seasonal contracts, found "
            + $"{ordinary.Length} and {seasonal.Length}.");

        HashSet<string> seasonalIds = seasonal
            .Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (FactionArcDefinitionSO arc in stories.Arcs)
        {
            string[] expected = ordinary
                .Where(value => string.Equals(
                    value.factionId,
                    arc.factionId,
                    StringComparison.Ordinal))
                .Select(value => value.StableId)
                .ToArray();
            Require(arc.contractIds != null
                    && arc.contractIds.Count == 3
                    && arc.contractIds.Distinct(StringComparer.Ordinal).Count() == 3
                    && arc.contractIds.ToHashSet(StringComparer.Ordinal)
                        .SetEquals(expected)
                    && !arc.contractIds.Any(seasonalIds.Contains),
                $"Faction arc '{arc.StableId}' does not list its exact ordinary contract set.");
        }
    }

    private static void VerifyMissingCultureChildRejectsAndRepairs(
        ResourceGameContentCatalog content)
    {
        SpeciesCultureDefinitionSO culture = content
            .GetAll<SpeciesCultureDefinitionSO>()
            .First(value => value.ceremonyIds?.Count > 0);
        using CultureReplacementFixture fixture = new(content, culture);
        int removedIndex = 0;
        string removedId = fixture.Culture.ceremonyIds[removedIndex];
        fixture.Culture.ceremonyIds.RemoveAt(removedIndex);

        RequireRejected(
            () => _ = fixture.Build(),
            "ceremony list does not match",
            "Startup accepted a culture that omitted an owned practice.");

        fixture.Culture.ceremonyIds.Insert(removedIndex, removedId);
        ResourceGameContentCatalog repaired = fixture.Build();
        Require(repaired.GetAll<SpeciesCultureDefinitionSO>()
                .Single(value => string.Equals(
                    value.StableId,
                    culture.StableId,
                    StringComparison.Ordinal))
                .ceremonyIds.Contains(removedId, StringComparer.Ordinal),
            "Repaired culture membership did not pass startup retry.");
    }

    private static void VerifyMissingOrdinaryContractRejectsAndRepairs(
        ResourceGameContentCatalog content,
        V20StoryContentCatalog stories)
    {
        FactionArcDefinitionSO arc = stories.Arcs
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .First();
        FactionContractDefinitionSO seasonal = stories.Contracts.Single(value =>
            string.Equals(value.factionId, arc.factionId, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(value.seasonalEventId));
        FactionArcDefinitionSO clone = Object.Instantiate(arc);
        clone.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            clone.contractIds = arc.contractIds.ToList();
            clone.contractIds[0] = seasonal.StableId;
            Require(clone.contractIds.Count == 3
                    && clone.contractIds.Distinct(StringComparer.Ordinal).Count() == 3,
                "Arc fixture did not preserve the three-contract balance shape.");

            RequireRejected(
                () => _ = new V20StoryContentCatalog(
                    new ReplacedDefinitionSource(content, arc, clone)),
                "nonseasonal contracts consumed",
                "Startup accepted an arc that replaced an ordinary contract with a seasonal contract.");

            clone.contractIds = arc.contractIds.ToList();
            V20StoryContentCatalog repaired = new(
                new ReplacedDefinitionSource(content, arc, clone));
            Require(repaired.Arcs.Single(value => string.Equals(
                        value.StableId,
                        arc.StableId,
                        StringComparison.Ordinal))
                    .contractIds.SequenceEqual(
                        arc.contractIds,
                        StringComparer.Ordinal),
                "Repaired faction arc membership did not pass startup retry.");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private sealed class CultureReplacementFixture : IDisposable
    {
        private readonly ResourceGameContentCatalog baseline;
        private readonly SpeciesCultureDefinitionSO source;
        private readonly GameContentCatalogSO root;
        private readonly GameDomainContentCatalogSO domain;

        internal CultureReplacementFixture(
            ResourceGameContentCatalog baseline,
            SpeciesCultureDefinitionSO source)
        {
            this.baseline = baseline
                ?? throw new ArgumentNullException(nameof(baseline));
            this.source = source
                ?? throw new ArgumentNullException(nameof(source));
            root = Object.Instantiate(baseline.Root);
            domain = Object.Instantiate(baseline.Domain);
            Culture = Object.Instantiate(source);
            root.hideFlags = HideFlags.HideAndDontSave;
            domain.hideFlags = HideFlags.HideAndDontSave;
            Culture.hideFlags = HideFlags.HideAndDontSave;
            Culture.ceremonyIds = source.ceremonyIds.ToList();
        }

        internal SpeciesCultureDefinitionSO Culture { get; }

        internal ResourceGameContentCatalog Build()
        {
            domain.SetDefinitions(baseline.Domain.Definitions.Select(value =>
                ReferenceEquals(value, source) ? Culture : value));
            root.Configure(
                baseline.Items,
                baseline.WorldPresentation,
                baseline.CharacterSkillSettings,
                baseline.Media,
                new ScriptableObject[] { domain });
            return new ResourceGameContentCatalog(new RootLoader(root));
        }

        public void Dispose()
        {
            Object.DestroyImmediate(Culture);
            Object.DestroyImmediate(domain);
            Object.DestroyImmediate(root);
        }
    }

    private sealed class ReplacedDefinitionSource : IGameContentDefinitionSource
    {
        private readonly IGameContentDefinitionSource inner;
        private readonly ScriptableObject original;
        private readonly ScriptableObject replacement;

        internal ReplacedDefinitionSource(
            IGameContentDefinitionSource inner,
            ScriptableObject original,
            ScriptableObject replacement)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.original = original
                ?? throw new ArgumentNullException(nameof(original));
            this.replacement = replacement
                ?? throw new ArgumentNullException(nameof(replacement));
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject =>
            inner.GetAll<T>()
                .Select(value => ReferenceEquals(value, original)
                    ? (T)(ScriptableObject)replacement
                    : value)
                .ToArray();

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = GetAll<T>();
            return values.Count == 1
                ? values[0]
                : throw new InvalidOperationException(
                    $"Expected one {typeof(T).Name}, found {values.Count}.");
        }
    }

    private sealed class RootLoader : IGameContentRootLoader
    {
        private readonly ScriptableObject root;

        internal RootLoader(ScriptableObject root)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public ScriptableObject LoadRequiredRoot() => root;
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

        internal static SourceSnapshot Capture(ScriptableObject source) =>
            new(source ?? throw new ArgumentNullException(nameof(source)));

        internal void RequireUnchanged()
        {
            Require(EditorJsonUtility.ToJson(source) == json,
                $"R20 fixture changed source asset '{source.name}'.");
            Require(EditorUtility.IsDirty(source) == dirty,
                $"R20 fixture changed dirty state for '{source.name}'.");
        }
    }

    private static void RequireRejected(
        Action action,
        string expectedMessage,
        string failureMessage)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException error)
        {
            Require(error.Message.Contains(
                    expectedMessage,
                    StringComparison.OrdinalIgnoreCase),
                failureMessage + " Unexpected error: " + error.Message);
            return;
        }

        throw new InvalidOperationException(failureMessage);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
