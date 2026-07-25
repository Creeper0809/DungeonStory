using System;
public readonly struct OffenseTruthRevealedEvent
{
    public OffenseTruthRevealedEvent(
        string targetId,
        string title,
        string truthText)
    {
        this.targetId = targetId ?? string.Empty;
        this.title = string.IsNullOrWhiteSpace(title)
            ? OffenseWorldMapService.TruthTitle
            : title;
        this.truthText = string.IsNullOrWhiteSpace(truthText)
            ? OffenseWorldMapService.TruthRevealText
            : truthText;
    }

    public string targetId { get; }
    public string title { get; }
    public string truthText { get; }
}
