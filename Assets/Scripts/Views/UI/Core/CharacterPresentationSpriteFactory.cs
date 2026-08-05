using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterPresentationSpriteKind
{
    Tray = 0,
    Crate = 1,
    Sack = 2,
    Backpack = 3,
    Hammer = 4,
    Broom = 5,
    Ladle = 6,
    Cup = 7,
    Coin = 8,
    Medical = 9,
    Dust = 10,
    Spark = 11,
    Steam = 12,
    Bubble = 13
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CharacterPresentationSpriteFactory
{
    private const int PixelsPerUnit = WorldInteractionPresentationCatalogSO.PixelsPerUnit;
    private static readonly Dictionary<CharacterPresentationSpriteKind, Sprite> Cache =
        new Dictionary<CharacterPresentationSpriteKind, Sprite>();

    public static Sprite Get(CharacterPresentationSpriteKind kind)
    {
        if (Cache.TryGetValue(kind, out Sprite sprite) && sprite != null)
        {
            return sprite;
        }

        PixelCanvas canvas = new PixelCanvas(12, 12);
        Draw(kind, canvas);
        Texture2D texture = new Texture2D(
            canvas.Width,
            canvas.Height,
            TextureFormat.RGBA32,
            mipChain: false)
        {
            name = $"Procedural_{kind}",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(canvas.Pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, canvas.Width, canvas.Height),
            new Vector2(0.5f, 0.15f),
            PixelsPerUnit,
            extrude: 0,
            SpriteMeshType.FullRect);
        sprite.name = $"Procedural_{kind}";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        Cache[kind] = sprite;
        return sprite;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        Cache.Clear();
    }

    private static void Draw(CharacterPresentationSpriteKind kind, PixelCanvas canvas)
    {
        Color32 outline = new Color32(38, 30, 35, 255);
        Color32 wood = new Color32(133, 83, 48, 255);
        Color32 woodLight = new Color32(190, 129, 70, 255);
        Color32 cloth = new Color32(122, 87, 65, 255);
        Color32 metal = new Color32(175, 180, 174, 255);
        Color32 water = new Color32(94, 177, 199, 255);
        Color32 gold = new Color32(242, 190, 72, 255);
        Color32 effect = new Color32(231, 222, 180, 230);

        switch (kind)
        {
            case CharacterPresentationSpriteKind.Tray:
                canvas.Rect(2, 4, 9, 2, outline);
                canvas.Rect(3, 5, 7, 1, woodLight);
                canvas.Rect(5, 2, 2, 2, outline);
                break;
            case CharacterPresentationSpriteKind.Crate:
                canvas.Rect(2, 2, 8, 8, outline);
                canvas.Rect(3, 3, 6, 6, wood);
                canvas.Line(3, 3, 8, 8, woodLight);
                canvas.Line(8, 3, 3, 8, woodLight);
                break;
            case CharacterPresentationSpriteKind.Sack:
                canvas.Rect(4, 2, 5, 2, outline);
                canvas.Rect(3, 4, 7, 6, outline);
                canvas.Rect(4, 5, 5, 4, cloth);
                canvas.Rect(5, 3, 3, 1, woodLight);
                break;
            case CharacterPresentationSpriteKind.Backpack:
                canvas.Rect(3, 2, 7, 8, outline);
                canvas.Rect(4, 3, 5, 6, cloth);
                canvas.Rect(5, 1, 3, 2, outline);
                canvas.Rect(5, 6, 3, 2, wood);
                break;
            case CharacterPresentationSpriteKind.Hammer:
                canvas.Line(3, 2, 8, 7, woodLight);
                canvas.Rect(6, 7, 5, 3, outline);
                canvas.Rect(7, 8, 3, 1, metal);
                break;
            case CharacterPresentationSpriteKind.Broom:
                canvas.Line(3, 2, 9, 9, woodLight);
                canvas.Rect(2, 1, 5, 3, outline);
                canvas.Rect(3, 2, 3, 1, cloth);
                break;
            case CharacterPresentationSpriteKind.Ladle:
                canvas.Line(3, 2, 8, 8, metal);
                canvas.Rect(7, 8, 4, 3, outline);
                canvas.Rect(8, 9, 2, 1, metal);
                break;
            case CharacterPresentationSpriteKind.Cup:
                canvas.Rect(3, 3, 6, 7, outline);
                canvas.Rect(4, 4, 4, 5, water);
                canvas.Rect(9, 5, 2, 3, outline);
                break;
            case CharacterPresentationSpriteKind.Coin:
                canvas.Rect(3, 3, 7, 7, outline);
                canvas.Rect(4, 4, 5, 5, gold);
                canvas.Rect(6, 5, 1, 3, effect);
                break;
            case CharacterPresentationSpriteKind.Medical:
                canvas.Rect(4, 1, 4, 10, outline);
                canvas.Rect(1, 4, 10, 4, outline);
                canvas.Rect(5, 2, 2, 8, new Color32(198, 70, 74, 255));
                canvas.Rect(2, 5, 8, 2, new Color32(198, 70, 74, 255));
                break;
            case CharacterPresentationSpriteKind.Spark:
                canvas.Line(2, 6, 9, 6, gold);
                canvas.Line(6, 2, 6, 9, gold);
                canvas.Set(4, 4, effect);
                canvas.Set(8, 8, effect);
                break;
            case CharacterPresentationSpriteKind.Steam:
                canvas.Line(3, 2, 5, 5, effect);
                canvas.Line(7, 3, 9, 6, effect);
                canvas.Line(5, 7, 7, 10, effect);
                break;
            case CharacterPresentationSpriteKind.Bubble:
                canvas.Rect(3, 3, 6, 6, water);
                canvas.Rect(4, 4, 4, 4, new Color32(156, 220, 226, 115));
                canvas.Set(5, 7, effect);
                break;
            default:
                canvas.Rect(3, 2, 2, 2, effect);
                canvas.Rect(7, 4, 3, 2, effect);
                canvas.Rect(2, 7, 3, 2, effect);
                break;
        }
    }

    private sealed class PixelCanvas
    {
        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color32[width * height];
        }

        public int Width { get; }
        public int Height { get; }
        public Color32[] Pixels { get; }

        public void Set(int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return;
            }

            Pixels[(y * Width) + x] = color;
        }

        public void Rect(int x, int y, int width, int height, Color32 color)
        {
            for (int offsetY = 0; offsetY < height; offsetY++)
            {
                for (int offsetX = 0; offsetX < width; offsetX++)
                {
                    Set(x + offsetX, y + offsetY, color);
                }
            }
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 color)
        {
            int deltaX = Math.Abs(x1 - x0);
            int stepX = x0 < x1 ? 1 : -1;
            int deltaY = -Math.Abs(y1 - y0);
            int stepY = y0 < y1 ? 1 : -1;
            int error = deltaX + deltaY;
            while (true)
            {
                Set(x0, y0, color);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int twiceError = 2 * error;
                if (twiceError >= deltaY)
                {
                    error += deltaY;
                    x0 += stepX;
                }

                if (twiceError <= deltaX)
                {
                    error += deltaX;
                    y0 += stepY;
                }
            }
        }
    }
}
