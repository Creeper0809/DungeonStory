using System;
public readonly struct OffenseTruthRevealedEvent
{
    private const string DefaultTruthTitle =
        "\ub358\uc804\uc758 \uc9c4\uc2e4";
    private const string DefaultTruthRevealText =
        "\uc774 \ub358\uc804\uc740 \ubaac\uc2a4\ud130\ub97c \uac00\ub450\uae30 \uc704\ud55c \uac10\uc625\uc774 \uc544\ub2c8\uc5c8\uc2b5\ub2c8\ub2e4. "
        + "\uc9c0\uc0c1\uc758 \uc655\uad6d\uc774 \ub9c8\ub825\uacfc \ub178\ub3d9\uc744 \uc218\ud655\ud558\ub824\uace0 \ub9cc\ub4e0 \uac70\ub300\ud55c \uc7a5\uce58\uc600\uace0, "
        + "\ubc18\ubcf5\ub41c \uce68\uacf5\uc740 \uadf8 \uc99d\uac70\ub97c \uc9c0\uc6b0\uae30 \uc704\ud55c \ubd09\uc1c4\uc600\uc2b5\ub2c8\ub2e4.";

    public OffenseTruthRevealedEvent(
        string targetId,
        string title,
        string truthText)
    {
        this.targetId = targetId ?? string.Empty;
        this.title = string.IsNullOrWhiteSpace(title)
            ? DefaultTruthTitle
            : title;
        this.truthText = string.IsNullOrWhiteSpace(truthText)
            ? DefaultTruthRevealText
            : truthText;
    }

    public string targetId { get; }
    public string title { get; }
    public string truthText { get; }
}
