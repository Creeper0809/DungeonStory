using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Advances persistent needs whose deprivation is defined by the absence of a
/// satisfying event. It never invents success events; actual combat, research,
/// meal and work producers reset the saved need clock through MoodPolicy.
/// </summary>
public sealed class CharacterPersistentNeedClock : IStartable, IDisposable
{
    private static readonly HashSet<string> AbsenceDrivenNeeds = new(
        new[]
        {
            "need:combat-action",
            "need:research-access",
            "need:sweets",
            "need:salt",
            "need:stimulation"
        },
        StringComparer.Ordinal);

    private readonly IGameEventBus events;
    private readonly ICharacterWorldQuery world;
    private readonly CharacterMoodPolicyService moods;
    private readonly IResourceStockPolicyQuery stockPolicies;
    private readonly ICharacterEnvironmentStatusQuery environment;
    private readonly IGridSystemProvider gridProvider;
    private readonly IRoomLayoutCache roomLayouts;
    private IDisposable dayEndedSubscription;

    public CharacterPersistentNeedClock(
        IGameEventBus events,
        ICharacterWorldQuery world,
        CharacterMoodPolicyService moods,
        IResourceStockPolicyQuery stockPolicies = null,
        ICharacterEnvironmentStatusQuery environment = null,
        IGridSystemProvider gridProvider = null,
        IRoomLayoutCache roomLayouts = null)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.moods = moods ?? throw new ArgumentNullException(nameof(moods));
        this.stockPolicies = stockPolicies;
        this.environment = environment;
        this.gridProvider = gridProvider;
        this.roomLayouts = roomLayouts;
    }

    [GameplayInternalOnly(
        "The runtime entry-point container starts the registered daily persistent-need clock.",
        "IStartable|DungeonCharacterRegistration")]
    public void Start() => dayEndedSubscription ??=
        events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);

    [GameplayInternalOnly(
        "The runtime lifetime container disposes the persistent-need day subscription.",
        "IDisposable|DungeonCharacterRegistration")]
    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
    }

    private void OnDayEnded(OperatingDayEndedEvent eventType)
    {
        EmergencyStockReadiness emergencyReadiness =
            stockPolicies?.GetEmergencyReadiness() ?? default;
        ResourceStockPolicyData[] activeStockTargets = stockPolicies?.Policies
            ?.Where(policy => policy != null && policy.enabled)
            .OrderBy(policy => policy.itemId, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<ResourceStockPolicyData>();
        bool stockTargetsMet = activeStockTargets.Length > 0
            && activeStockTargets.All(policy =>
                stockPolicies.CountOwned(policy.itemId) >= policy.targetStock);
        foreach (CharacterActor actor in world.Characters
                     .Where(value => value != null && !value.IsDead)
                     .OrderBy(value => value.Identity?.PersistentId,
                         StringComparer.Ordinal))
        {
            IEnumerable<PersistentNeedRule> rules =
                (actor.Progression?.ResolveSelectedTraits()
                    ?? Array.Empty<CharacterTraitSO>())
                .Where(value => value != null)
                .OrderBy(value => value.id)
                .SelectMany(value => value.identityRules
                    ?? new List<CharacterIdentityRule>())
                .OfType<PersistentNeedRule>()
                .Where(value => AbsenceDrivenNeeds.Contains(value.needId))
                .OrderBy(value => value.priority)
                .ThenBy(value => value.ruleId, StringComparer.Ordinal);
            foreach (PersistentNeedRule rule in rules)
            {
                moods.Apply(
                    actor,
                    rule.deprivedEventId,
                    0f,
                    rule.moodDurationDays,
                    $"미충족 욕구: {rule.needId}");
            }


            PersistentNeedRule emergencyRule =
                (actor.Progression?.ResolveSelectedTraits()
                    ?? Array.Empty<CharacterTraitSO>())
                .Where(value => value != null)
                .OrderBy(value => value.id)
                .SelectMany(value => value.identityRules
                    ?? new List<CharacterIdentityRule>())
                .OfType<PersistentNeedRule>()
                .Where(value => string.Equals(
                    value.needId,
                    "need:emergency-readiness",
                    StringComparison.Ordinal))
                .OrderBy(value => value.priority)
                .ThenBy(value => value.ruleId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (emergencyRule != null)
            {
                moods.Apply(
                    actor,
                    emergencyReadiness.Ready
                        ? emergencyRule.satisfiedEventId
                        : emergencyRule.deprivedEventId,
                    0f,
                    emergencyRule.moodDurationDays,
                    emergencyReadiness.Ready
                        ? "비상 비축 준비 완료"
                        : emergencyReadiness.Configured
                            ? $"비상 비축 {emergencyReadiness.ShortageCount}종 부족"
                            : "비상 비축 미지정");
            }

            if (stockTargetsMet)
            {
                moods.Apply(
                    actor,
                    "stockpile:target-met",
                    0f,
                    1,
                    "비축 목표 달성");
            }

            ApplyDailyEnvironmentEvents(actor);
            ApplyDailyRoomCrowdingEvent(actor);
        }
    }

    private void ApplyDailyRoomCrowdingEvent(CharacterActor actor)
    {
        if (actor == null
            || roomLayouts == null
            || gridProvider == null
            || !gridProvider.TryGetGrid(out Grid grid)
            || !roomLayouts.TryGetRoom(grid, actor.GetNowXY(), out RoomInstance room)
            || room == null
            || !room.IsUsable)
            return;

        int occupants = world.Characters
            .Where(value => value != null && !value.IsDead)
            .Count(value => roomLayouts.TryGetRoom(
                    grid,
                    value.GetNowXY(),
                    out RoomInstance occupiedRoom)
                && occupiedRoom != null
                && occupiedRoom.Id.Equals(room.Id));
        if (occupants < 3 || room.Cells.Count >= occupants * 3)
            return;

        moods.Apply(
            actor,
            "room:cramped-long",
            0f,
            1,
            "장시간 과밀한 방에 머묾");
    }

    private void ApplyDailyEnvironmentEvents(CharacterActor actor)
    {
        if (environment == null
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
            return;
        CharacterEnvironmentExposure exposure = environment.GetExposure(characterId);
        if (exposure == null)
            return;

        const float sustainedExposureThreshold = 15f;
        if (exposure.coldExposure >= sustainedExposureThreshold)
            moods.Apply(actor, "temperature:cold-long", 0f, 1, "장시간 추위 노출");
        if (Math.Max(exposure.coldExposure, exposure.heatExposure)
            >= sustainedExposureThreshold)
        {
            moods.Apply(
                actor,
                "temperature:uncomfortable-long",
                0f,
                1,
                "장시간 불쾌 온도 노출");
        }
        if (exposure.airborneExposure >= sustainedExposureThreshold)
            moods.Apply(actor, "environment:rot-stench", 0f, 1, "부패·오염 악취 노출");
    }
}
