using UnityEngine;

namespace DungeonStory.Foundation
{
    public interface IGameClock
    {
        float DeltaTime { get; }
        float Time { get; }
        int FrameCount { get; }
        bool IsPaused { get; }
    }

    public interface IUiClock
    {
        float DeltaTime { get; }
        float Time { get; }
    }

    public interface IGameTimeScaleController
    {
        float Scale { get; set; }
    }

    public sealed class UnityGameClock : IGameClock
    {
        public float DeltaTime => UnityEngine.Time.deltaTime;
        public float Time => UnityEngine.Time.time;
        public int FrameCount => UnityEngine.Time.frameCount;
        public bool IsPaused => UnityEngine.Time.timeScale <= 0f;
    }

    public sealed class UnityUiClock : IUiClock
    {
        public float DeltaTime => UnityEngine.Time.unscaledDeltaTime;
        public float Time => UnityEngine.Time.unscaledTime;
    }

    public sealed class UnityGameTimeScaleController : IGameTimeScaleController
    {
        private static readonly float BaseFixedDeltaTime =
            ResolveBaseFixedDeltaTime();

        public float Scale
        {
            get => UnityEngine.Time.timeScale;
            set
            {
                float scale = Mathf.Max(0f, value);
                UnityEngine.Time.timeScale = scale;
                UnityEngine.Time.fixedDeltaTime = scale > 0f
                    ? BaseFixedDeltaTime * scale
                    : BaseFixedDeltaTime;
            }
        }

        private static float ResolveBaseFixedDeltaTime()
        {
            float fixedDeltaTime = Mathf.Max(
                0.001f,
                UnityEngine.Time.fixedDeltaTime);
            float timeScale = UnityEngine.Time.timeScale;
            if (timeScale > 1f && fixedDeltaTime > 0.05f)
            {
                return Mathf.Max(0.001f, fixedDeltaTime / timeScale);
            }

            return fixedDeltaTime;
        }
    }
}
