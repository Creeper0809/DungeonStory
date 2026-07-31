using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ResourceAnatomyProfileCatalog : IAnatomyProfileCatalog
{
    private readonly IReadOnlyList<AnatomyProfileDefinition> profiles;
    private readonly IReadOnlyDictionary<string, AnatomyProfileDefinition> byId;
    private readonly IReadOnlyDictionary<string, AnatomyProfileDefinition> bySpecies;

    public ResourceAnatomyProfileCatalog(IResourcesAssetLoader resources)
        : this(resources?.LoadAllOptional<AnatomyProfileSO>(AnatomyProfileSO.ResourcePath))
    {
    }

    public ResourceAnatomyProfileCatalog(IEnumerable<AnatomyProfileSO> assets)
    {
        List<AnatomyProfileDefinition> loaded = (assets ?? Array.Empty<AnatomyProfileSO>())
            .Where(asset => asset != null)
            .Select(asset => new AnatomyProfileDefinition(asset))
            .OrderBy(profile => profile.ProfileId, StringComparer.Ordinal)
            .ToList();
        if (loaded.Count == 0)
        {
            loaded.Add(AnatomyProfileDefaults.CreateHumanoid());
            loaded.Add(AnatomyProfileDefaults.CreateQuadruped());
            loaded.Add(AnatomyProfileDefaults.CreateSlime());
            loaded.Add(AnatomyProfileDefaults.CreateFungal());
            loaded.Add(AnatomyProfileDefaults.CreateAvian());
            loaded.Add(AnatomyProfileDefaults.CreateConstruct());
        }

        profiles = loaded;
        byId = loaded.ToDictionary(profile => profile.ProfileId, StringComparer.Ordinal);
        Dictionary<string, AnatomyProfileDefinition> species =
            new Dictionary<string, AnatomyProfileDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (AnatomyProfileDefinition profile in loaded)
        {
            foreach (string speciesId in profile.SpeciesIds)
            {
                species.TryAdd(speciesId, profile);
            }
        }

        bySpecies = species;
    }

    public IReadOnlyList<AnatomyProfileDefinition> Profiles => profiles;

    public AnatomyProfileDefinition GetDefaultHumanoid()
    {
        return profiles.FirstOrDefault(profile =>
                string.Equals(
                    profile.AnatomyFamily,
                    "humanoid",
                    StringComparison.OrdinalIgnoreCase))
            ?? profiles[0];
    }

    public AnatomyProfileDefinition GetForSpecies(string speciesId)
    {
        if (!string.IsNullOrWhiteSpace(speciesId)
            && bySpecies.TryGetValue(speciesId.Trim(), out AnatomyProfileDefinition profile))
        {
            return profile;
        }

        return GetDefaultHumanoid();
    }

    public bool TryGet(string profileId, out AnatomyProfileDefinition profile)
    {
        return byId.TryGetValue(profileId?.Trim() ?? string.Empty, out profile);
    }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = new List<string>();
        foreach (AnatomyProfileDefinition profile in profiles)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (AnatomyNodeDefinition node in profile.Nodes)
            {
                if (!ids.Add(node.NodeId))
                {
                    errors.Add(
                        $"{profile.ProfileId}: 해부 노드 ID '{node.NodeId}'가 중복됩니다.");
                }

                if (!string.IsNullOrWhiteSpace(node.ParentNodeId)
                    && !profile.Nodes.Any(candidate =>
                        string.Equals(
                            candidate.NodeId,
                            node.ParentNodeId,
                            StringComparison.Ordinal)))
                {
                    errors.Add(
                        $"{profile.ProfileId}: '{node.NodeId}'의 부모 '{node.ParentNodeId}'가 없습니다.");
                }
            }

            if (!profile.Nodes.Any(node => node.Vital))
            {
                errors.Add($"{profile.ProfileId}: 필수 생명 기관이 없습니다.");
            }
        }

        return errors;
    }
}

public static class AnatomyProfileDefaults
{
    public static AnatomyProfileDefinition CreateHumanoid()
    {
        return new AnatomyProfileDefinition(
            "anatomy:humanoid",
            "인간형",
            "humanoid",
            new[]
            {
                "Human",
                "Orc",
                "Vampire",
                "Goblin",
                "Beastkin",
                "Demon",
                "Kobold"
            },
            new[]
            {
                Node("head", "머리", "", AnatomyNodeKind.BodyPart,
                    AnatomyFunction.Consciousness | AnatomyFunction.Sight,
                    18f, 0.35f, true, false, legacy: CombatBodyPart.Head),
                Node("brain", "뇌", "head", AnatomyNodeKind.Organ,
                    AnatomyFunction.Consciousness, 12f, 0.65f, true, false),
                Node("eye:left", "왼쪽 눈", "head", AnatomyNodeKind.SensoryOrgan,
                    AnatomyFunction.Sight, 6f, 0.5f, false, true, "eyes"),
                Node("eye:right", "오른쪽 눈", "head", AnatomyNodeKind.SensoryOrgan,
                    AnatomyFunction.Sight, 6f, 0.5f, false, true, "eyes"),
                Node("torso", "몸통", "", AnatomyNodeKind.Core,
                    AnatomyFunction.Core, 45f, 1f, true, false,
                    legacy: CombatBodyPart.Torso),
                Node("heart", "심장", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Core, 14f, 1f, true, true),
                Node("lung:left", "왼쪽 폐", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Breathing, 12f, 0.5f, false, true, "lungs"),
                Node("lung:right", "오른쪽 폐", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Breathing, 12f, 0.5f, false, true, "lungs"),
                Node("liver", "간", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Filtration | AnatomyFunction.Digestion,
                    16f, 0.6f, false, true),
                Node("kidney:left", "왼쪽 신장", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Filtration, 10f, 0.5f, false, true, "kidneys"),
                Node("kidney:right", "오른쪽 신장", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Filtration, 10f, 0.5f, false, true, "kidneys"),
                Node("stomach", "위", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Digestion, 14f, 0.4f, false, true),
                Node("arm:left", "왼팔", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Manipulation, 22f, 0.5f, false, true, "arms",
                    CombatBodyPart.LeftArm),
                Node("arm:right", "오른팔", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Manipulation, 22f, 0.5f, false, true, "arms",
                    CombatBodyPart.RightArm),
                Node("leg:left", "왼다리", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility, 26f, 0.5f, false, true, "legs",
                    CombatBodyPart.LeftLeg),
                Node("leg:right", "오른다리", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility, 26f, 0.5f, false, true, "legs",
                    CombatBodyPart.RightLeg)
            });
    }

    public static AnatomyProfileDefinition CreateQuadruped()
    {
        return new AnatomyProfileDefinition(
            "anatomy:quadruped",
            "사족 동물",
            "quadruped",
            new[]
            {
                "cave_rat",
                "shadow_hare",
                "moss_boar",
                "rune_deer",
                "shadow_wolf",
                "ash_goat"
            },
            new[]
            {
                Node("head", "머리", "", AnatomyNodeKind.BodyPart,
                    AnatomyFunction.Consciousness | AnatomyFunction.Sight,
                    15f, 0.35f, true, false, legacy: CombatBodyPart.Head),
                Node("brain", "뇌", "head", AnatomyNodeKind.Organ,
                    AnatomyFunction.Consciousness, 9f, 0.65f, true, false),
                Node("eye:left", "왼쪽 눈", "head", AnatomyNodeKind.SensoryOrgan,
                    AnatomyFunction.Sight, 5f, 0.5f, false, true, "eyes"),
                Node("eye:right", "오른쪽 눈", "head", AnatomyNodeKind.SensoryOrgan,
                    AnatomyFunction.Sight, 5f, 0.5f, false, true, "eyes"),
                Node("torso", "몸통", "", AnatomyNodeKind.Core,
                    AnatomyFunction.Core, 38f, 1f, true, false,
                    legacy: CombatBodyPart.Torso),
                Node("heart", "심장", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Core, 12f, 1f, true, true),
                Node("lung:left", "왼쪽 폐", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Breathing, 10f, 0.5f, false, true, "lungs"),
                Node("lung:right", "오른쪽 폐", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Breathing, 10f, 0.5f, false, true, "lungs"),
                Node("liver", "간", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Filtration | AnatomyFunction.Digestion,
                    13f, 0.6f, false, true),
                Node("kidney:left", "왼쪽 신장", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Filtration, 8f, 0.5f, false, true, "kidneys"),
                Node("kidney:right", "오른쪽 신장", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Filtration, 8f, 0.5f, false, true, "kidneys"),
                Node("stomach", "위", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Digestion, 12f, 0.4f, false, true),
                Node("forelegs", "앞다리", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility, 20f, 0.5f, false, true, "legs",
                    CombatBodyPart.LeftLeg),
                Node("hindlegs", "뒷다리", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility, 22f, 0.5f, false, true, "legs",
                    CombatBodyPart.RightLeg)
            });
    }

    public static AnatomyProfileDefinition CreateSlime()
    {
        return new AnatomyProfileDefinition(
            "anatomy:slime",
            "슬라임 핵 구조",
            "aberrant",
            new[] { "Slime" },
            new[]
            {
                Node("membrane", "외막", "", AnatomyNodeKind.BodyPart,
                    AnatomyFunction.Core | AnatomyFunction.Mobility,
                    55f, 0.7f, true, false, legacy: CombatBodyPart.Torso),
                Node("core", "핵", "membrane", AnatomyNodeKind.Core,
                    AnatomyFunction.Core | AnatomyFunction.Consciousness,
                    20f, 1f, true, false),
                Node("sensory-gel", "감각 젤", "membrane", AnatomyNodeKind.SensoryOrgan,
                    AnatomyFunction.Sight, 16f, 1f, false, true),
                Node("pseudopods", "위족", "membrane", AnatomyNodeKind.Limb,
                    AnatomyFunction.Manipulation | AnatomyFunction.Mobility,
                    30f, 1f, false, true)
            });
    }

    public static AnatomyProfileDefinition CreateFungal()
    {
        return new AnatomyProfileDefinition(
            "anatomy:fungal",
            "균사 군체 구조",
            "fungal",
            new[] { "Myconid" },
            new[]
            {
                Node("head", "균모", "", AnatomyNodeKind.BodyPart,
                    AnatomyFunction.Sight | AnatomyFunction.Filtration,
                    24f, 0.5f, false, true, legacy: CombatBodyPart.Head),
                Node("spore-sac", "포자낭", "head", AnatomyNodeKind.Organ,
                    AnatomyFunction.Breathing | AnatomyFunction.Filtration,
                    16f, 0.8f, false, true),
                Node("torso", "균사 몸통", "", AnatomyNodeKind.Core,
                    AnatomyFunction.Core | AnatomyFunction.Consciousness
                    | AnatomyFunction.Digestion,
                    48f, 1f, true, false, legacy: CombatBodyPart.Torso),
                Node("hypha-core", "중심 균핵", "torso", AnatomyNodeKind.Core,
                    AnatomyFunction.Core | AnatomyFunction.Consciousness,
                    18f, 1f, true, false),
                Node("arm:left", "왼쪽 균사팔", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Manipulation, 20f, 0.5f, false, true, "arms",
                    CombatBodyPart.LeftArm),
                Node("arm:right", "오른쪽 균사팔", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Manipulation, 20f, 0.5f, false, true, "arms",
                    CombatBodyPart.RightArm),
                Node("leg:left", "왼쪽 균사다리", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility, 22f, 0.5f, false, true, "legs",
                    CombatBodyPart.LeftLeg),
                Node("leg:right", "오른쪽 균사다리", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility, 22f, 0.5f, false, true, "legs",
                    CombatBodyPart.RightLeg)
            });
    }

    public static AnatomyProfileDefinition CreateAvian()
    {
        return new AnatomyProfileDefinition(
            "anatomy:avian",
            "조류형",
            "avian",
            new[] { "Harpy" },
            new[]
            {
                Node("head", "머리", "", AnatomyNodeKind.BodyPart,
                    AnatomyFunction.Consciousness | AnatomyFunction.Sight,
                    15f, 0.35f, true, false, legacy: CombatBodyPart.Head),
                Node("brain", "뇌", "head", AnatomyNodeKind.Organ,
                    AnatomyFunction.Consciousness, 10f, 0.7f, true, false),
                Node("eye:left", "왼쪽 눈", "head", AnatomyNodeKind.SensoryOrgan,
                    AnatomyFunction.Sight, 6f, 0.5f, false, true, "eyes"),
                Node("eye:right", "오른쪽 눈", "head", AnatomyNodeKind.SensoryOrgan,
                    AnatomyFunction.Sight, 6f, 0.5f, false, true, "eyes"),
                Node("torso", "몸통", "", AnatomyNodeKind.Core,
                    AnatomyFunction.Core, 38f, 1f, true, false,
                    legacy: CombatBodyPart.Torso),
                Node("heart", "심장", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Core, 12f, 1f, true, true),
                Node("air-sacs", "기낭", "torso", AnatomyNodeKind.Organ,
                    AnatomyFunction.Breathing, 13f, 1f, false, true),
                Node("wing:left", "왼날개", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility | AnatomyFunction.Manipulation,
                    24f, 0.5f, false, true, "wings", CombatBodyPart.LeftArm),
                Node("wing:right", "오른날개", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility | AnatomyFunction.Manipulation,
                    24f, 0.5f, false, true, "wings", CombatBodyPart.RightArm),
                Node("leg:left", "왼다리", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility, 20f, 0.5f, false, true, "legs",
                    CombatBodyPart.LeftLeg),
                Node("leg:right", "오른다리", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility, 20f, 0.5f, false, true, "legs",
                    CombatBodyPart.RightLeg)
            });
    }

    public static AnatomyProfileDefinition CreateConstruct()
    {
        return new AnatomyProfileDefinition(
            "anatomy:construct",
            "골렘 기계 구조",
            "construct",
            new[] { "Golem" },
            new[]
            {
                Node("head", "감응석 머리", "", AnatomyNodeKind.BodyPart,
                    AnatomyFunction.Sight, 28f, 1f, false, true,
                    legacy: CombatBodyPart.Head),
                Node("sensor-core", "감응 핵", "head", AnatomyNodeKind.SensoryOrgan,
                    AnatomyFunction.Sight, 18f, 1f, false, true),
                Node("torso", "주조 몸체", "", AnatomyNodeKind.Core,
                    AnatomyFunction.Core | AnatomyFunction.Consciousness,
                    70f, 1f, true, false, legacy: CombatBodyPart.Torso),
                Node("power-core", "동력핵", "torso", AnatomyNodeKind.Core,
                    AnatomyFunction.Core | AnatomyFunction.Consciousness,
                    24f, 1f, true, false),
                Node("arm:left", "왼쪽 작업팔", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Manipulation, 38f, 0.5f, false, true, "arms",
                    CombatBodyPart.LeftArm),
                Node("arm:right", "오른쪽 작업팔", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Manipulation, 38f, 0.5f, false, true, "arms",
                    CombatBodyPart.RightArm),
                Node("leg:left", "왼쪽 지지각", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility, 44f, 0.5f, false, true, "legs",
                    CombatBodyPart.LeftLeg),
                Node("leg:right", "오른쪽 지지각", "torso", AnatomyNodeKind.Limb,
                    AnatomyFunction.Mobility, 44f, 0.5f, false, true, "legs",
                    CombatBodyPart.RightLeg)
            });
    }

    private static AnatomyNodeDefinition Node(
        string id,
        string label,
        string parent,
        AnatomyNodeKind kind,
        AnatomyFunction functions,
        float maxHealth,
        float capacityWeight,
        bool vital,
        bool removable,
        string pair = "",
        CombatBodyPart legacy = default)
    {
        bool mapsToLegacy = id is "head"
            or "torso"
            or "membrane"
            or "arm:left"
            or "arm:right"
            or "leg:left"
            or "leg:right"
            or "forelegs"
            or "hindlegs";
        return new AnatomyNodeDefinition(
            id,
            label,
            parent,
            kind,
            functions,
            maxHealth,
            capacityWeight,
            vital,
            removable,
            pair,
            legacy,
            mapsToLegacy);
    }
}
