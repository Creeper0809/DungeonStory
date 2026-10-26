using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class R26LocalKeyGuardDebugScenarios
{
    private const string DomainCatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";

    [MenuItem("DungeonStory/R26/Validate Local Key Guards")]
    public static void Run()
    {
        Debug.Log(ValidateOnly());
    }

    public static string ValidateOnly()
    {
        GameDomainContentCatalogSO domain =
            AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(
                DomainCatalogPath);
        Require(domain != null, "Game-domain content catalog is missing.");

        VerifyCurrentAnatomyLexicons(domain);
        VerifyAnatomyPairGroupGuards(domain);
        VerifyAnatomyLexiconDuplicateGuards();
        VerifySurvivalDuplicateGuards(domain);
        VerifyWeaponRangeDuplicateGuards(domain);

        return "R26 local-key guards passed: current authored values are valid; "
            + "anatomy pair and species-condition, survival condition-pressure, "
            + "and weapon range-band duplicates are rejected.";
    }

    private static void VerifyCurrentAnatomyLexicons(
        GameDomainContentCatalogSO domain)
    {
        var profiles = new ResourceAnatomyProfileCatalog(
            domain.GetAll<AnatomyProfileSO>());
        var lexicons = new ResourceAnatomyConditionLexicon(
            domain.GetAll<AnatomyConditionLexiconSO>());
        RequireClean(lexicons.Validate(profiles), "Current anatomy lexicons");
    }

    private static void VerifyAnatomyLexiconDuplicateGuards()
    {
        AnatomyConditionLexiconSO localDuplicate = CreateLexicon(
            "fixture:local-duplicate",
            "humanoid",
            new[] { "fixture:local" },
            Entry(AnatomyConditionKind.FluidLoss),
            Entry(AnatomyConditionKind.FluidLoss));
        AnatomyConditionLexiconSO ownerA = CreateLexicon(
            "fixture:owner-a",
            "humanoid",
            new[] { "fixture:shared" },
            Entry(AnatomyConditionKind.FluidLoss));
        AnatomyConditionLexiconSO ownerB = CreateLexicon(
            "fixture:owner-b",
            "humanoid",
            new[] { "fixture:shared" },
            Entry(AnatomyConditionKind.FluidLoss));
        AnatomyConditionLexiconSO familyA = CreateLexicon(
            "fixture:family-a",
            "humanoid",
            new[] { "fixture:family-a" },
            Entry(AnatomyConditionKind.FluidLoss));
        AnatomyConditionLexiconSO familyB = CreateLexicon(
            "fixture:family-b",
            "humanoid",
            new[] { "fixture:family-b" },
            Entry(AnatomyConditionKind.FluidLoss));
        AnatomyConditionLexiconSO splitA = CreateLexicon(
            "fixture:split-a",
            "humanoid",
            new[] { "fixture:split" },
            Entry(AnatomyConditionKind.FluidLoss));
        AnatomyConditionLexiconSO splitB = CreateLexicon(
            "fixture:split-b",
            "humanoid",
            new[] { "fixture:split" },
            Entry(AnatomyConditionKind.Contamination));

        try
        {
            RequireRejected(
                localDuplicate.ValidateDefinition(),
                "duplicate condition",
                "Local lexicon condition");
            ExpectInvalid(
                () => new ResourceAnatomyConditionLexicon(
                    new[] { localDuplicate }),
                "duplicate condition");
            ExpectInvalid(
                () => new ResourceAnatomyConditionLexicon(
                    new[] { ownerA, ownerB }),
                "owned by both");

            _ = new ResourceAnatomyConditionLexicon(
                new[] { familyA, familyB });

            var split = new ResourceAnatomyConditionLexicon(
                new[] { splitA, splitB });
            Require(
                split.TryResolve(
                    "fixture:split",
                    "humanoid",
                    AnatomyConditionKind.FluidLoss,
                    out _)
                && split.TryResolve(
                    "fixture:split",
                    "humanoid",
                    AnatomyConditionKind.Contamination,
                    out _),
                "Disjoint condition rows for one species owner were not resolved.");
        }
        finally
        {
            Destroy(
                localDuplicate,
                ownerA,
                ownerB,
                familyA,
                familyB,
                splitA,
                splitB);
        }
    }

    private static void VerifySurvivalDuplicateGuards(
        GameDomainContentCatalogSO domain)
    {
        SurvivalBalanceSettingsSO source =
            domain.GetAll<SurvivalBalanceSettingsSO>().Single();
        RequireClean(source.ValidateDefinition(), "Current survival settings");

        SurvivalBalanceSettingsSO duplicateNeed =
            UnityEngine.Object.Instantiate(source);
        SurvivalBalanceSettingsSO duplicatePressure =
            UnityEngine.Object.Instantiate(source);
        try
        {
            List<CharacterNeedBalanceEntry> needs = source.Needs.ToList();
            needs.Add(needs[0]);
            SetPrivateField(duplicateNeed, "needs", needs);
            RequireRejected(
                duplicateNeed.ValidateDefinition(),
                "condition",
                "Survival need key");
            ExpectInvalid(
                () => duplicateNeed.TryGetNeed(
                    needs[0].condition,
                    out CharacterNeedBalanceEntry _),
                "ambiguous keys");

            List<SurvivalPressureBalanceProfile> pressures =
                source.Pressures.ToList();
            pressures.Add(pressures[0]);
            SetPrivateField(duplicatePressure, "pressures", pressures);
            RequireRejected(
                duplicatePressure.ValidateDefinition(),
                "pressure",
                "Survival pressure key");
            ExpectInvalid(
                () => duplicatePressure.GetPressure(pressures[0].pressure),
                "ambiguous keys");
        }
        finally
        {
            Destroy(duplicateNeed, duplicatePressure);
        }
    }

    private static void VerifyWeaponRangeDuplicateGuards(
        GameDomainContentCatalogSO domain)
    {
        CombatWeaponSO[] weapons = domain.GetAll<CombatWeaponSO>().ToArray();
        Require(weapons.Length > 0, "Current combat weapon definitions are missing.");
        foreach (CombatWeaponSO weapon in weapons)
        {
            RequireClean(
                weapon.ValidateDefinition(),
                $"Combat weapon '{weapon.EquipmentId}'");
            _ = weapon.CreateSnapshot(new CombatEquipmentInstance
            {
                instanceId = "fixture:" + weapon.EquipmentId,
                durabilityRatio = 1f
            });
        }

        CombatWeaponSO source = weapons.First(weapon =>
            weapon.RangeProfiles.Any(profile => profile != null));
        CombatRangeProfile profile = source.RangeProfiles.First(value =>
            value != null);
        CombatWeaponSO duplicate =
            UnityEngine.Object.Instantiate(source);
        try
        {
            SetPrivateField(
                duplicate,
                "rangeProfiles",
                new List<CombatRangeProfile> { profile, profile });
            RequireRejected(
                duplicate.ValidateDefinition(),
                "range band",
                "Combat weapon range key");
            ExpectInvalid(
                () => duplicate.CreateSnapshot(new CombatEquipmentInstance
                {
                    instanceId = "fixture:duplicate-range",
                    durabilityRatio = 1f
                }),
                "ambiguous range profiles");
        }
        finally
        {
            Destroy(duplicate);
        }
    }

    private static void VerifyAnatomyPairGroupGuards(
        GameDomainContentCatalogSO domain)
    {
        AnatomyProfileSO[] currentProfiles =
            domain.GetAll<AnatomyProfileSO>().ToArray();
        Require(currentProfiles.Length > 0, "Current anatomy profiles are missing.");
        foreach (AnatomyProfileSO profile in currentProfiles)
        {
            RequireClean(
                profile.ValidateDefinition(),
                $"Anatomy profile '{profile.ProfileId}'");
        }

        var currentGroups = currentProfiles
            .SelectMany(profile => profile.Nodes
                .Where(node => node != null
                    && !string.IsNullOrWhiteSpace(node.PairedGroupId))
                .Select(node => new
                {
                    ProfileId = profile.ProfileId,
                    GroupId = node.PairedGroupId,
                    NodeId = node.NodeId
                }))
            .GroupBy(value => (value.ProfileId, value.GroupId))
            .ToArray();
        Require(
            currentGroups.Length == 57
            && currentGroups.Sum(group => group.Count()) == 114
            && currentGroups.All(group => group.Count() == 2
                && group.Select(value => value.NodeId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == 2),
            "Current anatomy pair inventory is not 57 unambiguous groups with 114 members.");

        AnatomyProfileSO owner = CreateAnatomyProfile(
            "fixture:pair-owner",
            AnatomyNode("left"),
            AnatomyNode("right"));
        AnatomyProfileSO otherOwner = CreateAnatomyProfile(
            "fixture:other-owner",
            AnatomyNode("left"),
            AnatomyNode("right"));
        AnatomyProfileSO duplicateIds = CreateAnatomyProfile(
            "fixture:duplicate-node-ids",
            AnatomyNode("duplicate", "pair:duplicate"),
            AnatomyNode("duplicate", "pair:duplicate"),
            AnatomyNode("free"));
        AnatomyProfileSO occupiedTarget = CreateAnatomyProfile(
            "fixture:occupied-target",
            AnatomyNode("paired-a", "pair:existing"),
            AnatomyNode("paired-b", "pair:existing"),
            AnatomyNode("free"));
        AnatomyProfileSO ambiguous = CreateAnatomyProfile(
            "fixture:ambiguous-pair",
            AnatomyNode("orphan", "pair:orphan"),
            AnatomyNode("free"));

        try
        {
            SerializedProperty leftPair = PairProperty(owner, 0);
            Require(
                AnatomyPairGroupSelection.TrySetPair(
                    leftPair,
                    "right",
                    out string error),
                "Local anatomy pair selection failed: " + error);
            string generatedGroup = owner.Nodes[0].PairedGroupId;
            Require(
                generatedGroup.StartsWith("pair:", StringComparison.Ordinal)
                && generatedGroup == owner.Nodes[1].PairedGroupId,
                "Local anatomy pair selection did not update both nodes.");

            Require(
                AnatomyPairGroupSelection.TrySetPair(
                    PairProperty(owner, 0),
                    "right",
                    out error)
                && owner.Nodes[0].PairedGroupId == generatedGroup
                && owner.Nodes[1].PairedGroupId == generatedGroup,
                "Reselecting a valid pair changed its group ID.");

            Require(
                AnatomyPairGroupSelection.TryClearPair(
                    PairProperty(owner, 0),
                    out error)
                && string.IsNullOrEmpty(owner.Nodes[0].PairedGroupId)
                && string.IsNullOrEmpty(owner.Nodes[1].PairedGroupId),
                "Clearing an anatomy pair did not clear both nodes: " + error);

            Require(
                !AnatomyPairGroupSelection.TrySetPair(
                    PairProperty(owner, 0),
                    "left",
                    out error)
                && owner.Nodes.All(node =>
                    string.IsNullOrEmpty(node.PairedGroupId)),
                "Self-pairing changed the anatomy profile.");

            Require(
                !AnatomyPairGroupSelection.TrySetPair(
                    PairProperty(owner, 0),
                    "foreign",
                    out error)
                && owner.Nodes.All(node =>
                    string.IsNullOrEmpty(node.PairedGroupId)),
                "A node outside the owning profile changed the pair.");

            var multiple = new SerializedObject(
                new UnityEngine.Object[] { owner, otherOwner });
            SerializedProperty multiplePair = multiple.FindProperty("nodes")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("pairedGroupId");
            Require(
                !AnatomyPairGroupSelection.TrySetPair(
                    multiplePair,
                    "right",
                    out error)
                && owner.Nodes.All(node =>
                    string.IsNullOrEmpty(node.PairedGroupId))
                && otherOwner.Nodes.All(node =>
                    string.IsNullOrEmpty(node.PairedGroupId)),
                "Editing multiple anatomy profile owners changed a pair.");

            string[] duplicateBefore = duplicateIds.Nodes
                .Select(node => node.PairedGroupId)
                .ToArray();
            Require(
                !AnatomyPairGroupSelection.TrySetPair(
                    PairProperty(duplicateIds, 2),
                    "duplicate",
                    out error)
                && duplicateIds.Nodes.Select(node => node.PairedGroupId)
                    .SequenceEqual(duplicateBefore),
                "Duplicate anatomy node IDs caused a partial pair change.");
            RequireRejected(
                duplicateIds.ValidateDefinition(),
                "distinct",
                "Anatomy pair member identity");

            string[] occupiedBefore = occupiedTarget.Nodes
                .Select(node => node.PairedGroupId)
                .ToArray();
            Require(
                !AnatomyPairGroupSelection.TrySetPair(
                    PairProperty(occupiedTarget, 2),
                    "paired-a",
                    out error)
                && occupiedTarget.Nodes.Select(node => node.PairedGroupId)
                    .SequenceEqual(occupiedBefore),
                "Selecting an already paired target changed the profile.");

            string[] ambiguousBefore = ambiguous.Nodes
                .Select(node => node.PairedGroupId)
                .ToArray();
            Require(
                !AnatomyPairGroupSelection.TryClearPair(
                    PairProperty(ambiguous, 0),
                    out error)
                && ambiguous.Nodes.Select(node => node.PairedGroupId)
                    .SequenceEqual(ambiguousBefore),
                "Clearing an ambiguous anatomy pair changed the profile.");
            RequireRejected(
                ambiguous.ValidateDefinition(),
                "exactly two",
                "Anatomy pair group cardinality");
        }
        finally
        {
            Destroy(
                owner,
                otherOwner,
                duplicateIds,
                occupiedTarget,
                ambiguous);
        }
    }

    private static AnatomyProfileSO CreateAnatomyProfile(
        string id,
        params AnatomyNodeDefinition[] nodes)
    {
        AnatomyProfileSO profile =
            ScriptableObject.CreateInstance<AnatomyProfileSO>();
        profile.name = id;
        profile.Configure(
            id,
            id,
            "fixture",
            new[] { id },
            nodes);
        return profile;
    }

    private static AnatomyNodeDefinition AnatomyNode(
        string nodeId,
        string pairGroupId = "")
    {
        return new AnatomyNodeDefinition(
            nodeId,
            nodeId,
            string.Empty,
            AnatomyNodeKind.BodyPart,
            AnatomyFunction.PhysicalMobility,
            20f,
            1f,
            vital: false,
            removable: true,
            pairedGroupId: pairGroupId);
    }

    private static SerializedProperty PairProperty(
        AnatomyProfileSO profile,
        int nodeIndex)
    {
        var serialized = new SerializedObject(profile);
        SerializedProperty nodes = serialized.FindProperty("nodes");
        Require(
            nodes != null
            && nodeIndex >= 0
            && nodeIndex < nodes.arraySize,
            "Anatomy pair fixture node is missing.");
        return nodes.GetArrayElementAtIndex(nodeIndex)
            .FindPropertyRelative("pairedGroupId");
    }

    private static AnatomyConditionLexiconSO CreateLexicon(
        string id,
        string family,
        IEnumerable<string> speciesIds,
        params AnatomyConditionLexiconEntry[] entries)
    {
        AnatomyConditionLexiconSO lexicon =
            ScriptableObject.CreateInstance<AnatomyConditionLexiconSO>();
        lexicon.name = id;
        lexicon.Configure(id, family, speciesIds, entries);
        return lexicon;
    }

    private static AnatomyConditionLexiconEntry Entry(
        AnatomyConditionKind condition)
    {
        return new AnatomyConditionLexiconEntry
        {
            condition = condition,
            label = condition.ToString(),
            treatmentVerb = "treat"
        };
    }

    private static void SetPrivateField<T>(
        object target,
        string fieldName,
        T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, $"Private field '{fieldName}' is missing.");
        field.SetValue(target, value);
    }

    private static void RequireClean(
        IReadOnlyList<string> errors,
        string label)
    {
        Require(
            errors != null && errors.Count == 0,
            $"{label} is invalid: {string.Join(" | ", errors ?? Array.Empty<string>())}");
    }

    private static void RequireRejected(
        IReadOnlyList<string> errors,
        string token,
        string label)
    {
        Require(
            errors != null && errors.Any(error =>
                error.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0),
            $"{label} duplicate was accepted.");
    }

    private static void ExpectInvalid(Action action, string token)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(
                exception.Message.IndexOf(
                    token,
                    StringComparison.OrdinalIgnoreCase) >= 0,
                $"Unexpected rejection: {exception.Message}");
            return;
        }

        throw new InvalidOperationException(
            $"Expected rejection containing '{token}'.");
    }

    private static void Destroy(params UnityEngine.Object[] values)
    {
        foreach (UnityEngine.Object value in values)
        {
            if (value != null)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
