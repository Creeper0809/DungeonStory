using System;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects applied regular-customer observations into the legacy alert feed.
/// Recruitment state remains owned by the producer.
/// </summary>
public sealed class RegularCustomerObservationObserver :
    IInitializable,
    IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public RegularCustomerObservationObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize() => subscription ??=
        events.Subscribe<RegularCustomerObservation>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void Observe(RegularCustomerObservation value)
    {
        RegularCustomerSnapshot customer = value.Customer
            ?? throw new InvalidOperationException(
                "A regular customer observation requires a customer snapshot.");
        switch (value.Kind)
        {
            case RegularCustomerObservationKind.BecameRegular:
                events.RaiseAlert(
                    "단골 등장",
                    $"{customer.displayName}이 단골이 되었습니다.\n{customer.ToSummaryText()}",
                    EventAlertImportance.Low,
                    "단골");
                break;
            case RegularCustomerObservationKind.BecameRecruitCandidate:
                events.RaiseAlert(
                    "영입 후보",
                    $"{customer.displayName}을 영입할 수 있습니다.\n가능 역할: {RegularCustomerService.FormatCapabilities(customer.recruitCapabilities)}",
                    EventAlertImportance.Medium,
                    "영입");
                break;
            case RegularCustomerObservationKind.Recruited:
                ObserveRecruitment(value, customer);
                break;
            case RegularCustomerObservationKind.ExpeditionCandidate:
                events.RaiseAlert(
                    "원정 영입 후보",
                    $"{customer.displayName}이 원정 보상으로 영입 후보가 되었습니다.\n가능 역할: {RegularCustomerService.FormatCapabilities(customer.recruitCapabilities)}",
                    EventAlertImportance.Medium,
                    "영입");
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value.Kind,
                    "Unknown regular customer observation.");
        }
    }

    private void ObserveRecruitment(
        in RegularCustomerObservation value,
        RegularCustomerSnapshot customer)
    {
        bool mercenary = value.DeliveryKind
            == RegularCustomerRecruitDeliveryKind.Mercenary;
        string body = value.DeliveredImmediately
            ? $"{customer.displayName} 영입 완료\n가능 역할: "
                + RegularCustomerService.FormatCapabilities(
                    customer.recruitCapabilities)
            : $"{customer.displayName} 영입 확정; 캐릭터 배치 재시도 대기";
        events.RaiseAlert(
            mercenary ? "용병 계약" : "손님 영입",
            body,
            EventAlertImportance.Medium,
            mercenary ? "고용" : "영입");
    }
}
