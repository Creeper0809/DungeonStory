using System;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects owner-run observations into the legacy notice feed.
/// </summary>
public sealed class OwnerRunObservationObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public OwnerRunObservationObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize() => subscription ??=
        events.Subscribe<OwnerRunObservation>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void Observe(OwnerRunObservation value)
    {
        switch (value.Kind)
        {
            case OwnerRunObservationKind.InvalidCandidateIndex:
                events.ShowNotice(
                    "선택할 사장 후보가 없습니다.",
                    NoticeFeedEvent.Grade.WARNING);
                break;
            case OwnerRunObservationKind.MissingOwnerData:
                events.ShowNotice(
                    "사장 데이터가 없습니다.",
                    NoticeFeedEvent.Grade.DANGER);
                break;
            case OwnerRunObservationKind.NotOwnerCandidate:
                events.ShowNotice(
                    $"{value.DisplayName}은 사장 후보가 아닙니다.",
                    NoticeFeedEvent.Grade.WARNING);
                break;
            case OwnerRunObservationKind.OwnerSelected:
                events.ShowNotice(
                    value.DisplayName.EndsWith(
                        "사장",
                        StringComparison.Ordinal)
                        ? $"{value.DisplayName}으로 시작"
                        : $"{value.DisplayName} 사장으로 시작",
                    NoticeFeedEvent.Grade.NONE);
                break;
            case OwnerRunObservationKind.RunCompleted:
                bool victory = value.Outcome == DungeonRunOutcome.Victory;
                events.ShowNotice(
                    victory
                        ? $"런 승리: {value.Reason}"
                        : $"런 패배: {value.Reason}",
                    victory
                        ? NoticeFeedEvent.Grade.NONE
                        : NoticeFeedEvent.Grade.DANGER);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value.Kind,
                    "Unknown owner-run observation.");
        }
    }
}
