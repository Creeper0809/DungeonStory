#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R21ReferenceAuthoringDebugScenarios
{
    private const string RootPath =
        "Assets/Resources/SO/GameContentCatalog.asset";
    private const string MissingSpeciesIncident =
        "species-incident:fixture-missing";
    private const string MissingTacticalTarget =
        "fixture-missing-tactical-target";
    private const string MissingTacticalOverride =
        "fixture-missing-tactical-override";
    private const string MissingFaction =
        "fixture-missing-enemy-faction";
    private const string MissingRegion =
        "fixture-missing-campaign-region";

    public static void RunAll()
    {
        GameContentCatalogSO root = RequireAsset<GameContentCatalogSO>(RootPath);
        ContentReferenceCatalog catalog = ContentReferenceCatalog.FromRoot(root);
        ScriptableObject[] published = PublishedDefinitions(root);

        CharacterSO characterSource = RequirePublished<CharacterSO>(
            published,
            value => value.species != null
                && !string.IsNullOrWhiteSpace(value.speciesTag),
            "character with an authored phenotype species");
        CharacterSpeciesDefinitionSO speciesSource =
            RequirePublished<CharacterSpeciesDefinitionSO>(
                published,
                value => value.incident != null
                    && !string.IsNullOrWhiteSpace(value.incident.incidentId),
                "species with an authored incident");
        EnemyArchetypeDefinitionSO tacticalSource =
            RequirePublished<EnemyArchetypeDefinitionSO>(
                published,
                HasAuthoredTacticalTargets,
                "enemy with preferred and avoided tactical targets");
        EnemyArchetypeDefinitionSO bossSource =
            RequirePublished<EnemyArchetypeDefinitionSO>(
                published,
                HasAuthoredBossOverride,
                "boss enemy with a tactical override");
        CharacterBackgroundDefinitionSO backgroundSource =
            RequirePublished<CharacterBackgroundDefinitionSO>(
                published,
                HasLegacyFactionReaction,
                "background with a legacy faction alias");
        OffenseCampaignCatalogSO campaignSource =
            RequirePublished<OffenseCampaignCatalogSO>(
                published,
                value => value.Regions.Count >= 2
                    && value.Targets.Count > 0,
                "campaign with authored regions and targets");

        ScriptableObject[] sources =
        {
            root,
            characterSource,
            speciesSource,
            tacticalSource,
            bossSource,
            backgroundSource,
            campaignSource
        };
        SourceSnapshot[] snapshots = sources
            .Where(value => value != null)
            .Distinct()
            .Select(SourceSnapshot.Capture)
            .ToArray();

        CharacterSO character = Clone(characterSource);
        CharacterSpeciesDefinitionSO species = Clone(speciesSource);
        EnemyArchetypeDefinitionSO tactical = Clone(tacticalSource);
        EnemyArchetypeDefinitionSO boss = Clone(bossSource);
        CharacterBackgroundDefinitionSO background = Clone(backgroundSource);
        OffenseCampaignCatalogSO campaign = Clone(campaignSource);
        ScriptableObject[] clones =
        {
            character,
            species,
            tactical,
            boss,
            background,
            campaign
        };

        try
        {
            VerifyCharacterSpecies(catalog, characterSource, character);
            VerifySpeciesIncident(catalog, speciesSource, species);
            VerifyEnemyTactics(
                catalog,
                tacticalSource,
                tactical,
                bossSource,
                boss);
            VerifyBackgroundFaction(catalog, backgroundSource, background);
            VerifyCampaign(catalog, campaignSource, campaign);

            Debug.Log(
                "R21_REFERENCE_AUTHORING=PASS; character species; species incident; "
                + "enemy tactics; background faction alias; campaign region and faction; "
                + "pending owner selection; source JSON dirty state and asset bytes unchanged");
        }
        finally
        {
            foreach (ScriptableObject clone in clones)
                Object.DestroyImmediate(clone);
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
    }

    public static void VerifyInspectorGUI()
    {
        if (!Application.isBatchMode)
        {
            throw new InvalidOperationException(
                "R21 Inspector verification requires isolated batch mode.");
        }

        GameContentCatalogSO root = RequireAsset<GameContentCatalogSO>(RootPath);
        ScriptableObject[] published = PublishedDefinitions(root);
        CharacterBackgroundDefinitionSO backgroundSource =
            RequirePublished<CharacterBackgroundDefinitionSO>(
                published,
                HasLegacyFactionReaction,
                "background with a weighted faction reaction");
        OffenseCampaignCatalogSO campaignSource =
            RequirePublished<OffenseCampaignCatalogSO>(
                published,
                value => value.Regions.Count > 0
                    && value.Targets.Count > 0,
                "campaign with region and faction references");
        FacilityEvolutionRecordTokenDefinitionSO tokenSource =
            RequirePublished<FacilityEvolutionRecordTokenDefinitionSO>(
                published,
                value => !string.IsNullOrWhiteSpace(value.TokenId),
                "facility record token");
        EnemyArchetypeDefinitionSO enemySource =
            RequirePublished<EnemyArchetypeDefinitionSO>(
                published,
                value => HasAuthoredTacticalTargets(value)
                    && HasAuthoredBossOverride(value),
                "boss enemy with tactical target and override references");

        ScriptableObject[] sources =
        {
            root,
            backgroundSource,
            campaignSource,
            tokenSource,
            enemySource
        };
        SourceSnapshot[] snapshots = sources
            .Distinct()
            .Select(SourceSnapshot.Capture)
            .ToArray();

        CharacterBackgroundDefinitionSO background = Clone(backgroundSource);
        OffenseCampaignCatalogSO campaign = Clone(campaignSource);
        FacilityEvolutionRecordTokenDefinitionSO token = Clone(tokenSource);
        EnemyArchetypeDefinitionSO enemy = Clone(enemySource);
        ScriptableObject[] clones = { background, campaign, token, enemy };

        token.generationTrigger =
            FacilityEvolutionRecordTokenGenerationTrigger.MetricThresholdCrossing;
        token.sourceMetric = FacilityEvolutionTerms.ProducedRecordMetricKeys.First();
        token.threshold = 1f;
        token.decayPolicy = "long-term";

        ContentReferenceInspectorProbeWindow window =
            ScriptableObject.CreateInstance<
                ContentReferenceInspectorProbeWindow>();
        var drawn = new List<string>();
        try
        {
            ContentReferenceSelection.Invalidate();
            window.position = new Rect(0, 0, 820, 460);
            window.ShowUtility();

            VerifyReference(
                window,
                background,
                "factionReactions.Array.data[0].id",
                new ContentReferenceAttribute(
                    ContentReferenceKind.WeightedTarget),
                ContentReferenceKind.EnemyFaction,
                "BackgroundWeighted faction reaction",
                drawn);
            VerifyReference(
                window,
                campaign,
                "targets.Array.data[0].regionId",
                new ContentReferenceAttribute(
                    ContentReferenceKind.CampaignRegion),
                ContentReferenceKind.CampaignRegion,
                "CampaignRegion target region",
                drawn);
            VerifyReference(
                window,
                campaign,
                "targets.Array.data[0].factionId",
                new ContentReferenceAttribute(
                    ContentReferenceKind.CampaignFaction),
                ContentReferenceKind.CampaignFaction,
                "CampaignFaction target faction",
                drawn);
            VerifyReference(
                window,
                token,
                "sourceMetric",
                new ContentReferenceAttribute(
                    ContentReferenceKind.FacilityRecordMetric),
                ContentReferenceKind.FacilityRecordMetric,
                "threshold sourceMetric",
                drawn);
            VerifyReference(
                window,
                enemy,
                "tacticalProfile.preferredTargetTags.Array.data[0]",
                new ContentReferenceAttribute(
                    ContentReferenceKind.EnemyTacticalTarget)
                    { IgnoreCase = true },
                ContentReferenceKind.EnemyTacticalTarget,
                "EnemyTactic preferred target",
                drawn);
            VerifyReference(
                window,
                enemy,
                "bossPhases.Array.data[0].tacticalProfileOverrideTag",
                new ContentReferenceAttribute(
                    ContentReferenceKind.EnemyTacticalOverride)
                    { IgnoreCase = true },
                ContentReferenceKind.EnemyTacticalOverride,
                "EnemyTactic boss override",
                drawn);

            Require(window.DrawCount >= drawn.Count,
                "R21 Inspector draw count is below the verified surface count.");
            Debug.Log(
                "R21_REFERENCE_AUTHORING_INSPECTOR=PASS; surfaces="
                + string.Join(", ", drawn)
                + "; source JSON dirty state and asset bytes unchanged");
        }
        finally
        {
            window.Property = null;
            window.Close();
            if (window != null)
                Object.DestroyImmediate(window);
            foreach (ScriptableObject clone in clones)
            {
                Undo.ClearUndo(clone);
                Object.DestroyImmediate(clone);
            }
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
    }

    private static void VerifyCharacterSpecies(
        ContentReferenceCatalog catalog,
        CharacterSO source,
        CharacterSO clone)
    {
        string json = EditorJsonUtility.ToJson(source);
        RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
            "Character species baseline");

        clone.species = null;
        RequireRejected(
            catalog.Validate(new ScriptableObject[] { clone }),
            "registered phenotype species",
            "A character with a null species object was accepted.");

        Restore(source, clone, json);
        clone.speciesTag = string.Empty;
        RequireRejected(
            catalog.Validate(new ScriptableObject[] { clone }),
            "disagrees with its legacy species ID",
            "A character with an empty legacy species ID was accepted.");

        RestoreAndRequireClean(
            catalog,
            source,
            clone,
            json,
            "Character species repair");
    }

    private static void VerifySpeciesIncident(
        ContentReferenceCatalog catalog,
        CharacterSpeciesDefinitionSO source,
        CharacterSpeciesDefinitionSO clone)
    {
        string json = EditorJsonUtility.ToJson(source);
        RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
            "Species incident baseline");

        clone.incident.incidentId = MissingSpeciesIncident;
        RequireRejected(
            catalog.Validate(new ScriptableObject[] { clone }),
            MissingSpeciesIncident,
            "An unregistered species incident was accepted.");

        RestoreAndRequireClean(
            catalog,
            source,
            clone,
            json,
            "Species incident repair");
    }

    private static void VerifyEnemyTactics(
        ContentReferenceCatalog catalog,
        EnemyArchetypeDefinitionSO tacticalSource,
        EnemyArchetypeDefinitionSO tactical,
        EnemyArchetypeDefinitionSO bossSource,
        EnemyArchetypeDefinitionSO boss)
    {
        string tacticalJson = EditorJsonUtility.ToJson(tacticalSource);
        string bossJson = EditorJsonUtility.ToJson(bossSource);
        RequireClean(catalog.Validate(new ScriptableObject[] { tactical }),
            "Enemy tactic baseline");
        RequireClean(catalog.Validate(new ScriptableObject[] { boss }),
            "Enemy boss override baseline");

        tactical.tacticalProfile.preferredTargetTags[0] =
            MissingTacticalTarget;
        RequireRejected(
            catalog.Validate(new ScriptableObject[] { tactical }),
            MissingTacticalTarget,
            "An unknown preferred tactical target was accepted.");

        Restore(tacticalSource, tactical, tacticalJson);
        tactical.tacticalProfile.avoidedTargetTags[0] =
            MissingTacticalTarget;
        RequireRejected(
            catalog.Validate(new ScriptableObject[] { tactical }),
            MissingTacticalTarget,
            "An unknown avoided tactical target was accepted.");

        Restore(tacticalSource, tactical, tacticalJson);
        tactical.tacticalProfile.preferredTargetTags.Clear();
        tactical.tacticalProfile.avoidedTargetTags.Clear();
        RequireClean(catalog.Validate(new ScriptableObject[] { tactical }),
            "Enemy empty tactical target lists");

        tactical.tacticalProfile.preferredTargetTags.Add(
            EnemyTacticalTargetProtocols.None);
        RequireClean(catalog.Validate(new ScriptableObject[] { tactical }),
            "Enemy none tactical target");

        RestoreAndRequireClean(
            catalog,
            tacticalSource,
            tactical,
            tacticalJson,
            "Enemy tactic repair");

        boss.bossPhases[0].tacticalProfileOverrideTag =
            MissingTacticalOverride;
        RequireRejected(
            catalog.Validate(new ScriptableObject[] { boss }),
            MissingTacticalOverride,
            "An unknown boss tactical override was accepted.");

        Restore(bossSource, boss, bossJson);
        boss.bossPhases[0].tacticalProfileOverrideTag = string.Empty;
        RequireClean(catalog.Validate(new ScriptableObject[] { boss }),
            "Enemy empty tactical override");

        RestoreAndRequireClean(
            catalog,
            bossSource,
            boss,
            bossJson,
            "Enemy boss override repair");
    }

    private static void VerifyBackgroundFaction(
        ContentReferenceCatalog catalog,
        CharacterBackgroundDefinitionSO source,
        CharacterBackgroundDefinitionSO clone)
    {
        string json = EditorJsonUtility.ToJson(source);
        string alias = clone.factionReactions[0].id;
        Require(BackgroundFactionReactionRules.LegacyAliases.Contains(
                alias,
                StringComparer.Ordinal),
            "The background fixture is not using a real legacy faction alias.");
        RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
            "Background faction alias baseline");

        clone.factionReactions[0].id = MissingFaction;
        RequireRejected(
            catalog.Validate(new ScriptableObject[] { clone }),
            MissingFaction,
            "An unregistered background faction key was accepted.");

        RestoreAndRequireClean(
            catalog,
            source,
            clone,
            json,
            "Background faction alias repair");
        Require(clone.factionReactions[0].id == alias,
            "Background faction repair did not restore the original alias.");
    }

    private static void VerifyCampaign(
        ContentReferenceCatalog catalog,
        OffenseCampaignCatalogSO source,
        OffenseCampaignCatalogSO clone)
    {
        string json = EditorJsonUtility.ToJson(source);
        Require(clone.Regions.Count == 3,
            "The published campaign does not expose its three region providers.");
        RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
            "Campaign baseline");

        OffenseTargetDefinition target = clone.Targets[0];
        target.regionId = MissingRegion;
        RequireRejected(
            catalog.Validate(new ScriptableObject[] { clone }),
            MissingRegion,
            "An unknown campaign region was accepted.");

        Restore(source, clone, json);
        target = clone.Targets[0];
        OffenseCampaignRegionDefinition alternate = clone.Regions.First(value =>
            value.id != target.regionId
            && value.factionId != target.factionId);
        target.factionId = alternate.factionId;
        RequireRejected(
            catalog.Validate(new ScriptableObject[] { clone }),
            target.regionId + "/" + alternate.factionId,
            "A mismatched campaign region and faction pair was accepted.");

        RestoreAndRequireClean(
            catalog,
            source,
            clone,
            json,
            "Campaign repair");

        target = clone.Targets[0];
        ContentReferenceAttribute regionReference = new(
            ContentReferenceKind.CampaignRegion);
        ContentReferenceAttribute factionReference = new(
            ContentReferenceKind.CampaignFaction);
        ContentReferenceCatalog.Entry[] regionEntries = catalog
            .Entries(regionReference, clone, target)
            .ToArray();
        RequireSameIds(
            regionEntries.Select(value => value.Id),
            clone.Regions.Select(value => value.id),
            "Campaign owner region entries");
        Require(regionEntries.All(value => value.Asset == clone),
            "Campaign owner region entries include a non-owner provider.");

        ContentReferenceCatalog.Entry[] factionEntries = catalog
            .Entries(factionReference, clone, target)
            .ToArray();
        RequireSameIds(
            factionEntries.Select(value => value.Id),
            clone.Regions
                .Where(value => value.id == target.regionId)
                .Select(value => value.factionId),
            "Campaign owner faction entries");
        Require(factionEntries.All(value => value.Asset == clone),
            "Campaign owner faction entries include a non-owner provider.");

        VerifyPendingCampaignSelection(clone);
    }

    private static void VerifyPendingCampaignSelection(
        OffenseCampaignCatalogSO campaign)
    {
        string baseline = EditorJsonUtility.ToJson(campaign);
        OffenseTargetDefinition target = campaign.Targets[0];
        OffenseCampaignRegionDefinition alternate = campaign.Regions.First(
            value => value.id != target.regionId);
        string appliedRegion = target.regionId;
        string appliedFaction = target.factionId;

        ContentReferenceSelection.Invalidate();
        using SerializedObject serialized = new(campaign);
        SerializedProperty row = RequireProperty(
            serialized,
            "targets.Array.data[0]");
        SerializedProperty region = row.FindPropertyRelative("regionId");
        SerializedProperty faction = row.FindPropertyRelative("factionId");
        region.stringValue = alternate.id;

        ContentReferenceAttribute regionReference = new(
            ContentReferenceKind.CampaignRegion);
        ContentReferenceAttribute factionReference = new(
            ContentReferenceKind.CampaignFaction);
        RequireSameIds(
            ContentReferenceSelection.Entries(regionReference, region)
                .Select(value => value.Id),
            campaign.Regions.Select(value => value.id),
            "Pending campaign region entries");
        RequireSameIds(
            ContentReferenceSelection.Entries(factionReference, faction)
                .Select(value => value.Id),
            new[] { alternate.factionId },
            "Pending campaign faction entries");
        Require(campaign.Targets[0].regionId == appliedRegion
                && campaign.Targets[0].factionId == appliedFaction,
            "Reading pending campaign entries applied values to the clone.");
        Require(EditorJsonUtility.ToJson(campaign) == baseline,
            "Reading pending campaign entries changed clone JSON.");
        serialized.Update();
    }

    private static void VerifyReference(
        ContentReferenceInspectorProbeWindow window,
        ScriptableObject target,
        string propertyPath,
        ContentReferenceAttribute declared,
        ContentReferenceKind expectedKind,
        string context,
        ICollection<string> drawn)
    {
        string baseline = EditorJsonUtility.ToJson(target);
        using SerializedObject serialized = new(target);
        SerializedProperty property = RequireProperty(serialized, propertyPath);
        ContentReferenceAttribute resolved =
            ContentReferenceSelection.ResolveReference(declared, property);
        Require(resolved != null && resolved.Kind == expectedKind,
            context + " did not resolve the expected ContentReference kind.");

        ContentReferenceSelection.Entry[] candidates =
            ContentReferenceSelection.Entries(resolved, property).ToArray();
        string current = ContentReferenceSelection.ReadId(
            property,
            resolved.Kind,
            resolved.EmptyNumericId);
        Require(current.Length > 0
                && candidates.Any(value => ContentReferenceCatalog.Matches(
                    resolved,
                    value.Id,
                    current)),
            context + " candidates omit the authored value '" + current + "'.");

        DrawProbe(window, property, context);
        Require(EditorJsonUtility.ToJson(target) == baseline,
            context + " Inspector drawing changed clone JSON.");
        drawn.Add(context);
    }

    private static void DrawProbe(
        ContentReferenceInspectorProbeWindow window,
        SerializedProperty property,
        string context)
    {
        int previous = window.DrawCount;
        window.Property = property;
        window.SendEvent(new Event { type = EventType.Layout });
        window.SendEvent(new Event { type = EventType.Repaint });
        window.Property = null;
        if (window.Failure != null)
            throw new InvalidOperationException(context, window.Failure);
        Require(window.DrawCount > previous,
            "Inspector GUI did not execute: " + context);
    }

    private static bool HasAuthoredTacticalTargets(
        EnemyArchetypeDefinitionSO value) =>
        value?.tacticalProfile != null
        && value.tacticalProfile.preferredTargetTags?.Count > 0
        && value.tacticalProfile.avoidedTargetTags?.Count > 0;

    private static bool HasAuthoredBossOverride(
        EnemyArchetypeDefinitionSO value) =>
        value?.bossPhases?.Count > 0
        && value.bossPhases[0] != null
        && !string.IsNullOrWhiteSpace(
            value.bossPhases[0].tacticalProfileOverrideTag);

    private static bool HasLegacyFactionReaction(
        CharacterBackgroundDefinitionSO value) =>
        value?.factionReactions?.Count > 0
        && value.factionReactions[0] != null
        && BackgroundFactionReactionRules.LegacyAliases.Contains(
            value.factionReactions[0].id,
            StringComparer.Ordinal);

    private static ScriptableObject[] PublishedDefinitions(
        GameContentCatalogSO root) =>
        root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .SelectMany(value => value.Definitions
                .Concat(new ScriptableObject[] { value }))
            .Concat(new[]
            {
                root,
                root.ItemDefinitions,
                root.CharacterSkillSettings
            })
            .Where(value => value != null)
            .Distinct()
            .ToArray();

    private static T RequirePublished<T>(
        IEnumerable<ScriptableObject> published,
        Func<T, bool> predicate,
        string label)
        where T : ScriptableObject
    {
        T value = published.OfType<T>().FirstOrDefault(predicate);
        return value != null
            ? value
            : throw new InvalidOperationException(
                "The published root has no " + label + ".");
    }

    private static T RequireAsset<T>(string path) where T : Object
    {
        T value = AssetDatabase.LoadAssetAtPath<T>(path);
        return value != null
            ? value
            : throw new InvalidOperationException(
                "Required fixture asset is missing: " + path);
    }

    private static T Clone<T>(T source) where T : ScriptableObject
    {
        T clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        return clone;
    }

    private static void Restore<T>(T source, T clone, string json)
        where T : ScriptableObject
    {
        EditorJsonUtility.FromJsonOverwrite(json, clone);
        Require(EditorJsonUtility.ToJson(clone) ==
                EditorJsonUtility.ToJson(source),
            typeof(T).Name + " clone did not restore its source JSON.");
    }

    private static void RestoreAndRequireClean<T>(
        ContentReferenceCatalog catalog,
        T source,
        T clone,
        string json,
        string context)
        where T : ScriptableObject
    {
        Restore(source, clone, json);
        RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
            context);
        RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
            context + " retry");
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

    private static void RequireSameIds(
        IEnumerable<string> actual,
        IEnumerable<string> expected,
        string context)
    {
        string[] actualIds = actual
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expectedIds = expected
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal),
            context + " mismatch. expected=" + string.Join(",", expectedIds)
            + "; actual=" + string.Join(",", actualIds));
    }

    private static void RequireRejected(
        IReadOnlyList<string> errors,
        string expectedText,
        string message)
    {
        Require(errors.Any(value => value.Contains(
                expectedText,
                StringComparison.OrdinalIgnoreCase)),
            message + " Errors: " + string.Join(" | ", errors));
    }

    private static void RequireClean(
        IReadOnlyList<string> errors,
        string context)
    {
        Require(errors.Count == 0,
            context + ": " + string.Join(" | ", errors));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct SourceSnapshot
    {
        private readonly ScriptableObject source;
        private readonly string json;
        private readonly bool dirty;
        private readonly string assetPath;
        private readonly byte[] bytes;

        private SourceSnapshot(
            ScriptableObject source,
            string json,
            bool dirty,
            string assetPath,
            byte[] bytes)
        {
            this.source = source;
            this.json = json;
            this.dirty = dirty;
            this.assetPath = assetPath;
            this.bytes = bytes;
        }

        internal static SourceSnapshot Capture(ScriptableObject source)
        {
            string path = AssetDatabase.GetAssetPath(source);
            string fullPath = string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "..",
                    path));
            Require(fullPath.Length > 0 && File.Exists(fullPath),
                "Fixture source has no readable asset path: " + source.name);
            return new SourceSnapshot(
                source,
                EditorJsonUtility.ToJson(source),
                EditorUtility.IsDirty(source),
                fullPath,
                File.ReadAllBytes(fullPath));
        }

        internal void RequireUnchanged()
        {
            Require(EditorJsonUtility.ToJson(source) == json,
                "R21 fixture changed source JSON for '" + source.name + "'.");
            Require(EditorUtility.IsDirty(source) == dirty,
                "R21 fixture changed dirty state for '" + source.name + "'.");
            Require(File.ReadAllBytes(assetPath).SequenceEqual(bytes),
                "R21 fixture changed source asset bytes for '"
                + source.name + "'.");
        }
    }
}
#endif
