using System;
using DungeonStory.Foundation;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

public sealed class DungeonTitleLifetimeScope : LifetimeScope
{
    public sealed class TitleGameSpeedController : IGameSpeedController
    {
        private readonly IGameTimeScaleController timeScaleController;
        private int speed = 1;
        private bool manualPause;
        private int pauseRequests;

        public TitleGameSpeedController(
            IGameTimeScaleController timeScaleController)
        {
            this.timeScaleController = timeScaleController
                ?? throw new ArgumentNullException(
                    nameof(timeScaleController));
            manualPause = timeScaleController.Scale <= 0f;
        }

        public int Speed => speed;
        public bool IsPaused => manualPause || pauseRequests > 0;

        public void CycleSpeed()
        {
            SetSpeed(speed % 5 + 1);
        }

        public void SetSpeed(int nextSpeed)
        {
            speed = Math.Clamp(nextSpeed, 1, 5);
            if (!IsPaused)
            {
                timeScaleController.Scale = speed;
            }
        }

        public void TogglePause()
        {
            SetPaused(!manualPause);
        }

        public void SetPaused(bool paused)
        {
            manualPause = paused;
            ApplyTimeScale();
        }

        public IDisposable AcquirePause()
        {
            pauseRequests++;
            ApplyTimeScale();
            return new GamePauseRequest(() =>
            {
                pauseRequests--;
                ApplyTimeScale();
            });
        }

        private void ApplyTimeScale() =>
            timeScaleController.Scale = IsPaused ? 0f : speed;
    }

    protected override void Configure(IContainerBuilder builder)
    {
        Scene titleScene = gameObject.scene;
        DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery(titleScene);
        builder.RegisterInstance(new SceneUiBootstrapReferences(
            sceneQuery.First<UnityEngine.EventSystems.EventSystem>(
                includeInactive: true)));
        builder.RegisterInstance(new DungeonUserSettingsRuntimeTargets(
            sceneQuery.First<CameraManager>(includeInactive: true),
            sceneQuery.All<DungeonUiThemeRuntime>(includeInactive: true)));
        builder.RegisterDungeonFoundation();
        builder.Register<UnityGameContentRootLoader>(Lifetime.Singleton)
            .As<IGameContentRootLoader>();
        builder.RegisterDungeonGameContentCatalog();
        builder.Register<ResourceTmpKoreanFontProvider>(Lifetime.Singleton)
            .As<ITmpKoreanFontProvider>();
        builder.Register<TmpKoreanFontService>(Lifetime.Singleton)
            .As<ITmpKoreanFontService>();
        builder.Register<DungeonTitleCanvasProvider>(Lifetime.Singleton)
            .As<IDungeonUiCanvasProvider>();
        builder.Register<DungeonSaveSlotCatalog>(Lifetime.Singleton)
            .As<IDungeonSaveSlotCatalog>();
        builder.Register<DungeonSceneNavigator>(Lifetime.Singleton)
            .AsSelf()
            .As<IDungeonSceneNavigator>();
        builder.Register<TitleGameSpeedController>(Lifetime.Singleton)
            .As<IGameSpeedController>();
        builder.RegisterEntryPoint<DungeonUserSettingsService>(Lifetime.Singleton)
            .As<IDungeonUserSettingsService>();
        builder.RegisterEntryPoint<DungeonAudioController>(Lifetime.Singleton)
            .As<IDungeonAudioService>();
        builder.RegisterEntryPoint<DungeonSettingsUiController>(Lifetime.Singleton)
            .As<IDungeonSettingsUi>();
        builder.Register<DungeonTitleUiEnvironment>(Lifetime.Singleton)
            .As<IDungeonTitleUiEnvironment>();
        builder.RegisterEntryPoint<DungeonTitleUiController>(Lifetime.Singleton);
    }
}
