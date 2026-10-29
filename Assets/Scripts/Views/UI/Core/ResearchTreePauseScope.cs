using System;

public sealed class ResearchTreePauseScope
{
    private readonly IDungeonUserSettingsService settingsService;
    private readonly IGameSpeedController gameSpeedController;
    private IDisposable pauseRequest;

    public ResearchTreePauseScope(
        IDungeonUserSettingsService settingsService,
        IGameSpeedController gameSpeedController)
    {
        this.settingsService = settingsService
            ?? throw new ArgumentNullException(nameof(settingsService));
        this.gameSpeedController = gameSpeedController
            ?? throw new ArgumentNullException(nameof(gameSpeedController));
    }

    public void Capture()
    {
        if (pauseRequest != null || !settingsService.Current.pauseOnResearchTree)
        {
            return;
        }

        pauseRequest = gameSpeedController.AcquirePause();
    }

    public void Restore()
    {
        IDisposable request = pauseRequest;
        pauseRequest = null;
        request?.Dispose();
    }
}
