using UnityEngine;
using UnityEngine.Rendering.Universal;

public class BackgroundManager : MonoBehaviour
{
    private const float SecondsPerDay = 180f;

    private static readonly SkyKey[] SkyKeys =
    {
        new SkyKey(0f, new Color32(17, 19, 31, 255)),
        new SkyKey(5f, new Color32(29, 38, 51, 255)),
        new SkyKey(7f, new Color32(110, 101, 112, 255)),
        new SkyKey(9f, new Color32(91, 119, 126, 255)),
        new SkyKey(12f, new Color32(120, 147, 154, 255)),
        new SkyKey(16f, new Color32(113, 133, 139, 255)),
        new SkyKey(18f, new Color32(143, 102, 88, 255)),
        new SkyKey(20f, new Color32(70, 59, 76, 255)),
        new SkyKey(24f, new Color32(17, 19, 31, 255))
    };

    private static readonly LightKey[] LightKeys =
    {
        new LightKey(0f, 0.56f),
        new LightKey(5f, 0.58f),
        new LightKey(7f, 0.78f),
        new LightKey(9f, 0.94f),
        new LightKey(12f, 1f),
        new LightKey(16f, 0.92f),
        new LightKey(18f, 0.76f),
        new LightKey(20f, 0.62f),
        new LightKey(24f, 0.56f)
    };

    public SpriteRenderer background;
    public Light2D light2D;
    public GameData gameData;

    private void OnEnable()
    {
        if (gameData == null || gameData.curTime == null)
        {
            Debug.LogError($"{nameof(BackgroundManager)} requires initialized {nameof(GameData.curTime)} data.", this);
            return;
        }

        ApplyCurrentTime();
    }

    private void LateUpdate()
    {
        ApplyCurrentTime();
    }

    public static Color EvaluateSkyColor(float hour)
    {
        float wrappedHour = WrapHour(hour);
        int index = FindKeyIndex(SkyKeys, wrappedHour);
        SkyKey from = SkyKeys[index];
        SkyKey to = SkyKeys[index + 1];
        float t = Mathf.InverseLerp(from.Hour, to.Hour, wrappedHour);
        return Color.Lerp(from.Color, to.Color, Smooth(t));
    }

    public static float EvaluateGlobalLightIntensity(float hour)
    {
        float wrappedHour = WrapHour(hour);
        int index = FindKeyIndex(LightKeys, wrappedHour);
        LightKey from = LightKeys[index];
        LightKey to = LightKeys[index + 1];
        float t = Mathf.InverseLerp(from.Hour, to.Hour, wrappedHour);
        return Mathf.Lerp(from.Intensity, to.Intensity, Smooth(t));
    }

    private void ApplyCurrentTime()
    {
        if (gameData == null || gameData.curTime == null)
        {
            return;
        }

        float hour = Mathf.Repeat(gameData.curTime.Value, SecondsPerDay) / SecondsPerDay * 24f;
        if (background != null)
        {
            background.color = EvaluateSkyColor(hour);
        }

        if (light2D != null)
        {
            light2D.color = Color.white;
            light2D.intensity = EvaluateGlobalLightIntensity(hour);
        }
    }

    private static int FindKeyIndex(SkyKey[] keys, float hour)
    {
        for (int index = 0; index < keys.Length - 1; index++)
        {
            if (hour <= keys[index + 1].Hour)
            {
                return index;
            }
        }

        return keys.Length - 2;
    }

    private static int FindKeyIndex(LightKey[] keys, float hour)
    {
        for (int index = 0; index < keys.Length - 1; index++)
        {
            if (hour <= keys[index + 1].Hour)
            {
                return index;
            }
        }

        return keys.Length - 2;
    }

    private static float WrapHour(float hour)
    {
        return Mathf.Repeat(hour, 24f);
    }

    private static float Smooth(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private readonly struct SkyKey
    {
        public SkyKey(float hour, Color color)
        {
            Hour = hour;
            Color = color;
        }

        public float Hour { get; }
        public Color Color { get; }
    }

    private readonly struct LightKey
    {
        public LightKey(float hour, float intensity)
        {
            Hour = hour;
            Intensity = intensity;
        }

        public float Hour { get; }
        public float Intensity { get; }
    }
}
