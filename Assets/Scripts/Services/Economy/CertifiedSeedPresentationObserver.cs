using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

// Query-only projection of a completed daily certified-seed owner pass.
public sealed class CertifiedSeedPresentationObserver : IInitializable, IDisposable
{
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IResourceEconomyContentCatalog crops;
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public CertifiedSeedPresentationObserver(
        IFacilityCapabilityQuery facilities,
        IResourceEconomyContentCatalog crops,
        IGameEventBus events)
    {
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.crops = crops ?? throw new ArgumentNullException(nameof(crops));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Initialize() => subscription ??=
        events.Subscribe<CertifiedSeedPlansAdvancedFact>(OnPlansAdvanced);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void OnPlansAdvanced(CertifiedSeedPlansAdvancedFact _)
    {
        try { PublishChoices(); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError("certified-seed-presentation-observer:"
                + exception.GetType().Name);
        }
    }

    private void PublishChoices()
    {
        BuildableObject facility = CertifiedSeedFacilityEligibility
            .FindOperational(facilities)
            .FirstOrDefault();
        if (facility == null) return;

        CropDefinitionSO[] authored = crops.Crops
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.SeedItemId))
            .OrderBy(value => value.CropId, StringComparer.Ordinal)
            .ToArray();
        for (int offset = 0; offset < authored.Length; offset += 4)
        {
            EventAlertChoice[] choices = authored.Skip(offset).Take(4)
                .Select(crop => new EventAlertChoice(
                    crop.DisplayName,
                    "기존 종자 로트와 인증 꾸러미를 운반해 품질을 높이고 병원체 부하를 낮춥니다.",
                    V21ContentAlertActionIds.CertifiedSeed(
                        crop.CropId,
                        facility.PersistentInstanceId.Value)))
                .ToArray();
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "인증 품종 종자 생산",
                "육종 온실에서 인증할 작물을 선택하십시오. 재료가 실제로 도착한 뒤 종자 로트가 배출됩니다.",
                EventAlertImportance.Medium,
                "V21 농업",
                choices,
                $"certified-seed:{facility.PersistentInstanceId.Value}:{offset / 4}")));
        }
    }
}
