#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Factions;
using UnityEditor;
using UnityEngine;

public static class DungeonFactionAssetBuilder
{
    private const string Root =
        "Assets/Resources/SO/Factions/Dungeons";

    [MenuItem("DungeonStory/Content/Build Dungeon Factions")]
    public static void BuildAll()
    {
        EnsureFolder(Root);
        foreach (FactionSpec spec in Specs())
        {
            string path = $"{Root}/{spec.FileName}.asset";
            DungeonFactionDefinitionSO asset =
                AssetDatabase.LoadAssetAtPath<DungeonFactionDefinitionSO>(path);
            if (asset == null)
            {
                asset =
                    ScriptableObject.CreateInstance<DungeonFactionDefinitionSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.factionId = spec.Id;
            asset.displayName = spec.Name;
            asset.speciesTag = spec.Species;
            asset.description = spec.Description;
            asset.relationTags = spec.RelationTags;
            asset.tradeTags = spec.TradeTags;
            asset.reinforcementRole = spec.ReinforcementRole;
            asset.tradeCargo = Cargo(spec.TradeCargo);
            asset.supplyCargo = Cargo(spec.SupplyCargo);
            asset.tradeCooldownDays = TradeCooldownDays(spec.Id);
            asset.supplyCooldownDays = SupplyCooldownDays(spec.Id);
            asset.reinforcementCooldownDays = 10;
            asset.crest ??= CreateCrest(asset, spec);
            EditorUtility.SetDirty(asset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Dungeon faction content built: 6 factions with physical cargo and crests.");
    }

    private static Sprite CreateCrest(
        DungeonFactionDefinitionSO owner,
        FactionSpec spec)
    {
        const int size = 32;
        Texture2D texture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false)
        {
            name = $"{spec.FileName}_CrestTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color transparent = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool shield = y >= 3
                    && y <= 27
                    && x >= 4 + Mathf.Abs(y - 14) / 4
                    && x <= 27 - Mathf.Abs(y - 14) / 4;
                bool border = shield && (x < 7 || x > 24 || y < 6 || y > 24);
                bool emblem = shield
                    && ((x + spec.Pattern) % 7 == 0
                        || (y + spec.Pattern * 2) % 9 == 0);
                texture.SetPixel(
                    x,
                    y,
                    !shield
                        ? transparent
                        : border
                            ? spec.Accent
                            : emblem
                                ? Color.Lerp(spec.Accent, Color.white, 0.45f)
                                : spec.Primary);
            }
        }
        texture.Apply(false, true);
        AssetDatabase.AddObjectToAsset(texture, owner);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        sprite.name = $"{spec.FileName}_Crest";
        AssetDatabase.AddObjectToAsset(sprite, owner);
        return sprite;
    }

    private static List<FactionCargoLine> Cargo(
        IReadOnlyList<CargoSpec> specs)
    {
        List<FactionCargoLine> result = new List<FactionCargoLine>();
        foreach (CargoSpec spec in specs)
        {
            result.Add(new FactionCargoLine
            {
                itemId = spec.ItemId,
                amount = spec.Amount
            });
        }
        return result;
    }

    private static FactionSpec[] Specs()
    {
        return new[]
        {
            new FactionSpec(
                DungeonFactionIds.Beastkin,
                "Faction_Beastkin_RedPawPost",
                "붉은발 역참",
                "Beastkin",
                "빠른 교역, 정찰과 긴급 운반을 맡는 수인 역참 연합.",
                new[] { "야외", "무리", "소음" },
                new[] { "고기", "가죽", "사료", "동물" },
                "정찰병과 긴급 운반대",
                new[]
                {
                    C("resource:meat", 8),
                    C("resource:hide", 5),
                    C("feed:dog-food", 6)
                },
                new[]
                {
                    C("food:preserved-ration", 12),
                    C("material:leather", 8),
                    C("feed:hay", 10)
                },
                new Color(0.55f, 0.12f, 0.08f),
                new Color(0.95f, 0.62f, 0.24f),
                1),
            new FactionSpec(
                DungeonFactionIds.Demon,
                "Faction_Demon_AshContractCourt",
                "잿불 계약정",
                "Demon",
                "마력 촉매, 사치품과 화염·저주 전력을 계약하는 데몬 궁정.",
                new[] { "화염", "마력", "고급" },
                new[] { "마력", "사치품", "촉매", "계약 장비" },
                "화염술사와 계약 저주단",
                new[]
                {
                    C("resource:mana-crystal", 6),
                    C("craft:gold-ornament", 3),
                    C("craft:ritual-reagent", 4)
                },
                new[]
                {
                    C("drug:mana-awakener", 6),
                    C("material:blacksteel-ingot", 5),
                    C("craft:ritual-reagent", 8)
                },
                new Color(0.32f, 0.04f, 0.08f),
                new Color(1f, 0.28f, 0.05f),
                2),
            new FactionSpec(
                DungeonFactionIds.Kobold,
                "Faction_Kobold_DeepGearWarren",
                "심층 톱니굴",
                "Kobold",
                "광석, 탄약, 함정 부품과 정비 인력을 공급하는 코볼트 공방.",
                new[] { "질서", "협소", "기계" },
                new[] { "광석", "볼트", "함정", "방어 설계도" },
                "함정 재장전과 현장 수리반",
                new[]
                {
                    C("resource:iron-ore", 10),
                    C("ammo:bolt-iron", 12),
                    C("material:iron-ingot", 5)
                },
                new[]
                {
                    C("ammo:bolt-steel", 16),
                    C("material:steel-ingot", 8),
                    C("material:lumber", 10)
                },
                new Color(0.25f, 0.20f, 0.08f),
                new Color(0.92f, 0.72f, 0.18f),
                3),
            new FactionSpec(
                DungeonFactionIds.Myconid,
                "Faction_Myconid_MycelialGrove",
                "균사 심림",
                "Myconid",
                "약품, 퇴비와 발효식을 순환시키는 균사 공동체.",
                new[] { "습기", "오염", "야외" },
                new[] { "약품", "발효식", "퇴비", "균사 재료" },
                "치료사와 포자 제독반",
                new[]
                {
                    C("resource:cave-mushroom", 10),
                    C("material:compost", 8),
                    C("medicine:herbal-poultice", 5)
                },
                new[]
                {
                    C("medicine:antidote", 8),
                    C("medicine:standard", 8),
                    C("food:mushroom-soup", 10)
                },
                new Color(0.16f, 0.31f, 0.14f),
                new Color(0.62f, 0.84f, 0.32f),
                4),
            new FactionSpec(
                DungeonFactionIds.Harpy,
                "Faction_Harpy_StormNest",
                "폭풍 둥지",
                "Harpy",
                "정보와 원거리 탄약을 빠르게 나르는 하피 고지 동맹.",
                new[] { "야외", "청정", "개방" },
                new[] { "정보", "깃털", "원거리 탄약", "정찰" },
                "외부 고지 정찰과 원거리 엄호대",
                new[]
                {
                    C("resource:feather", 10),
                    C("ammo:arrow-iron", 12),
                    C("ammo:bolt-bone", 8)
                },
                new[]
                {
                    C("ammo:arrow-steel", 16),
                    C("ammo:bolt-steel", 12),
                    C("resource:feather", 14)
                },
                new Color(0.12f, 0.28f, 0.42f),
                new Color(0.55f, 0.86f, 1f),
                5),
            new FactionSpec(
                DungeonFactionIds.Golem,
                "Faction_Golem_StoneveinFoundry",
                "석맥 주조소",
                "Golem",
                "장갑판, 동력핵과 자동화 부품을 주조하는 골렘 산업 도시.",
                new[] { "질서", "마력", "기계" },
                new[] { "장갑판", "동력핵", "자동화", "중화기" },
                "방패벽과 시설 긴급 복구반",
                new[]
                {
                    C("material:iron-ingot", 8),
                    C("material:stone-block", 10),
                    C("resource:mana-crystal", 4)
                },
                new[]
                {
                    C("material:steel-ingot", 10),
                    C("material:blacksteel-ingot", 5),
                    C("resource:mana-crystal", 8)
                },
                new Color(0.18f, 0.20f, 0.24f),
                new Color(0.64f, 0.78f, 0.92f),
                6)
        };
    }

    private static CargoSpec C(string itemId, int amount) =>
        new CargoSpec(itemId, amount);

    private static int TradeCooldownDays(string factionId) => factionId switch
    {
        DungeonFactionIds.Beastkin => 7,
        DungeonFactionIds.Harpy => 16,
        DungeonFactionIds.Myconid => 22,
        DungeonFactionIds.Demon => 23,
        DungeonFactionIds.Kobold => 25,
        DungeonFactionIds.Golem => 27,
        _ => throw new ArgumentOutOfRangeException(nameof(factionId), factionId, null)
    };

    private static int SupplyCooldownDays(string factionId) => factionId switch
    {
        DungeonFactionIds.Beastkin => 20,
        DungeonFactionIds.Harpy => 22,
        DungeonFactionIds.Myconid => 38,
        DungeonFactionIds.Kobold => 49,
        DungeonFactionIds.Demon => 84,
        DungeonFactionIds.Golem => 99,
        _ => throw new ArgumentOutOfRangeException(nameof(factionId), factionId, null)
    };

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = $"{current}/{parts[index]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }
            current = next;
        }
    }

    private readonly struct CargoSpec
    {
        public CargoSpec(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }

        public string ItemId { get; }
        public int Amount { get; }
    }

    private sealed class FactionSpec
    {
        public FactionSpec(
            string id,
            string fileName,
            string name,
            string species,
            string description,
            string[] relationTags,
            string[] tradeTags,
            string reinforcementRole,
            CargoSpec[] tradeCargo,
            CargoSpec[] supplyCargo,
            Color primary,
            Color accent,
            int pattern)
        {
            Id = id;
            FileName = fileName;
            Name = name;
            Species = species;
            Description = description;
            RelationTags = relationTags;
            TradeTags = tradeTags;
            ReinforcementRole = reinforcementRole;
            TradeCargo = tradeCargo;
            SupplyCargo = supplyCargo;
            Primary = primary;
            Accent = accent;
            Pattern = pattern;
        }

        public string Id { get; }
        public string FileName { get; }
        public string Name { get; }
        public string Species { get; }
        public string Description { get; }
        public string[] RelationTags { get; }
        public string[] TradeTags { get; }
        public string ReinforcementRole { get; }
        public CargoSpec[] TradeCargo { get; }
        public CargoSpec[] SupplyCargo { get; }
        public Color Primary { get; }
        public Color Accent { get; }
        public int Pattern { get; }
    }
}
#endif
