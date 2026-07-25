#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CombatCoverAssetBuilder
{
    private const string BuildingRoot = "Assets/Resources/SO/Building/Combat";
    private const string SpriteRoot = "Assets/Images/Generated/CombatCover";

    [MenuItem("DungeonStory/Debug/Combat/Build Initial Cover")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(BuildingRoot);
        Directory.CreateDirectory(SpriteRoot);

        BuildCover(
            "C01_WoodBarricade",
            9601,
            "목재 바리케이드",
            35,
            60f,
            45,
            24f,
            3,
            CreateWoodBarricade);
        BuildCover(
            "C02_SackBulwark",
            9602,
            "자루 방책",
            55,
            90f,
            70,
            34f,
            4,
            CreateSackBulwark);
        BuildCover(
            "C03_ArrowScreen",
            9603,
            "화살막이",
            70,
            110f,
            95,
            42f,
            5,
            CreateArrowScreen);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Initial combat cover assets built.");
    }

    private static void BuildCover(
        string fileName,
        int id,
        string displayName,
        int blockPercent,
        float hitPoints,
        int constructionCost,
        float constructionWork,
        int materialAmount,
        Action<Texture2D> draw)
    {
        Sprite sprite = BuildSprite(fileName, draw);
        string path = $"{BuildingRoot}/{fileName}.asset";
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingSO>();
            AssetDatabase.CreateAsset(building, path);
        }

        building.id = id;
        building.objectName = displayName;
        building.sprite = sprite;
        building.icon = sprite;
        building.width = 1;
        building.height = 1;
        building.layer = GridLayer.Building;
        building.category = BuildingCategory.Special;
        building.horizontalDraggable = false;
        building.verticalDraggable = false;
        building.type = typeof(BuildableObject);
        building.tiles = null;
        building.movementAnchorOffset = Vector2.zero;
        building.unlocked = true;
        building.Facility = new FacilityData
        {
            roles = FacilityRole.Security,
            capacity = 0,
            useDuration = 0f,
            requiredWorkers = 0,
            disabledWhenDamaged = false
        };
        building.Facility.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Repair });

        BuildingAbilityCollection abilities = new BuildingAbilityCollection();
        abilities.Add(new BuildingEconomyAbility
        {
            constructionCost = constructionCost,
            maintenance = 0,
            unlockPhase = 1,
            demolitionRefundRate = 0.5f
        });
        abilities.Add(new BuildingFacilityAbility
        {
            settings = building.Facility
        });
        abilities.Add(new BuildingCoverAbility
        {
            height = CombatCoverHeight.Low,
            blockChance = blockPercent / 100f,
            facingDirection = Vector2Int.left,
            coverHitPoints = hitPoints
        });
        abilities.Add(new BuildingWorkAmountAbility
        {
            constructionWorkRequired = constructionWork,
            repairWorkRequired = Mathf.Max(8f, constructionWork * 0.35f),
            cleanWorkRequired = 4f,
            researchWorkRequired = 6f,
            operateWorkRequired = 10f,
            constructionMaterialCategory = StockCategory.General,
            constructionMaterialAmount = materialAmount,
            materialUnitsPerConstructionCost = 0f
        });
        building.ReplaceAbilities(abilities);
        building.ValidateAbilitiesOrThrow();
        EditorUtility.SetDirty(building);
    }

    private static Sprite BuildSprite(string fileName, Action<Texture2D> draw)
    {
        string path = $"{SpriteRoot}/{fileName}.png";
        Texture2D texture = new Texture2D(32, 24, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[texture.width * texture.height];
        Array.Fill(pixels, clear);
        texture.SetPixels(pixels);
        draw(texture);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void CreateWoodBarricade(Texture2D texture)
    {
        Color dark = Hex("33251f");
        Color wood = Hex("6d4930");
        Color light = Hex("a87545");
        DrawRect(texture, 3, 4, 26, 4, dark);
        DrawRect(texture, 4, 8, 24, 4, wood);
        DrawRect(texture, 5, 12, 22, 4, light);
        DrawRect(texture, 7, 2, 4, 16, dark);
        DrawRect(texture, 21, 2, 4, 16, dark);
        DrawLine(texture, 5, 13, 25, 9, dark);
    }

    private static void CreateSackBulwark(Texture2D texture)
    {
        Color dark = Hex("42382b");
        Color sack = Hex("8f805e");
        Color light = Hex("baaa78");
        for (int row = 0; row < 3; row++)
        {
            int y = 3 + row * 6;
            int offset = row % 2 == 0 ? 2 : 6;
            for (int x = offset; x < 30; x += 9)
            {
                DrawRect(texture, x, y, 8, 5, dark);
                DrawRect(texture, x + 1, y + 1, 6, 3, row == 2 ? light : sack);
            }
        }
    }

    private static void CreateArrowScreen(Texture2D texture)
    {
        Color dark = Hex("261e1a");
        Color wood = Hex("775039");
        Color cloth = Hex("4f433f");
        Color rim = Hex("9c6d46");
        DrawRect(texture, 3, 2, 3, 20, dark);
        DrawRect(texture, 26, 2, 3, 20, dark);
        DrawRect(texture, 5, 5, 22, 14, rim);
        DrawRect(texture, 7, 7, 18, 10, cloth);
        for (int x = 8; x <= 24; x += 4)
        {
            DrawLine(texture, x, 7, x - 3, 17, wood);
        }
    }

    private static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (int px = Mathf.Max(0, x); px < Mathf.Min(texture.width, x + width); px++)
        {
            for (int py = Mathf.Max(0, y); py < Mathf.Min(texture.height, y + height); py++)
            {
                texture.SetPixel(px, py, color);
            }
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            if (x0 >= 0 && x0 < texture.width && y0 >= 0 && y0 < texture.height)
            {
                texture.SetPixel(x0, y0, color);
            }

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int doubled = error * 2;
            if (doubled >= dy)
            {
                error += dy;
                x0 += sx;
            }
            if (doubled <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static Color Hex(string rgb)
    {
        return ColorUtility.TryParseHtmlString($"#{rgb}", out Color value)
            ? value
            : Color.magenta;
    }
}
#endif
