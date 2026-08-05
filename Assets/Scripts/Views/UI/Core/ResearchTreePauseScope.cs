using System;

public sealed class ResearchTreePauseScope
{
    private readonly IDungeonUserSettingsService settingsService;
    private readonly IGameSpeedController gameSpeedController;
    private bool captured;
    private bool wasPaused;

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
        if (captured || !settingsService.Current.pauseOnResearchTree)
        {
            return;
        }

        wasPaused = gameSpeedController.IsPaused;
        captured = true;
        gameSpeedController.SetPaused(true);
    }

    public void Restore()
    {
        if (!captured)
        {
            return;
        }

        gameSpeedController.SetPaused(wasPaused);
        captured = false;
    }
}
