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
        public float Scale
        {
            get => UnityEngine.Time.timeScale;
            set => UnityEngine.Time.timeScale = value;
        }
    }
}
