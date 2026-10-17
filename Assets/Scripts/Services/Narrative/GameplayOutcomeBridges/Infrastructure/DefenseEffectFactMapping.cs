using System;
using System.Globalization;
using System.Linq;

public static class DefenseEffectFactMapping
{
    public const string ObserverId = "log:defense.facility-effect";

    public static GameplayResultKey ResultKey(DefenseEffectFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return DefenseFacilityEffectOutcomeIds.ResultKey(
            data.activation.facilityId,
            data.activation.activationCount);
    }

    public static DefenseFacilityEffectOutcomeReceipt Capture(
        DefenseEffectFact fact,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        if (diagnostics == null)
            throw new ArgumentNullException(nameof(diagnostics));

        DefenseEffectFactData data = fact.Snapshot();
        data.Validate();
        GameplayResultKey parentKey = TriggerParentKey(data);
        if (!diagnostics.TryGetResultIdentity(
                parentKey,
                out GameplayOutcomeReplayIdentity parent)
            || parent.ResultKey != parentKey
            || parent.State < GameplayOutcomeReplayState.Committed
            || !parent.OutcomeId.IsValid
            || !parent.HasCanonicalPayloadHash)
        {
            throw new InvalidOperationException(
                "defense-effect-trigger-parent-not-committed");
        }

        return CaptureWithParent(fact, parent.OutcomeId);
    }

    public static DefenseFacilityEffectOutcomeReceipt CaptureWithParent(
        DefenseEffectFact fact,
        GameplayOutcomeId triggerParent)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        if (!triggerParent.IsValid)
            throw new ArgumentException(
                "A committed defense trigger outcome identity is required.",
                nameof(triggerParent));

        DefenseEffectFactData data = fact.Snapshot();
        data.Validate();
        DefenseActivationFactData activation = data.activation;
        GameplayResultKey parentKey = TriggerParentKey(data);
        return new DefenseFacilityEffectOutcomeReceipt(
            ResultKey(data),
            activation.absoluteDay,
            Status(data.result),
            activation.facilityId,
            activation.facilityName,
            activation.targetId,
            activation.targetName,
            activation.timing,
            activation.concept,
            data.damage,
            data.healthBefore,
            data.healthAfter,
            data.statusesBefore.Length,
            data.statusesAfter.Length,
            activation.supplyBefore,
            activation.supplyAfter,
            activation.conditionBefore,
            activation.conditionAfter,
            data.movementDelaySeconds,
            data.ignitionIntensity,
            FormatStatuses(data.statusesBefore),
            FormatStatuses(data.statusesAfter),
            data.effectTags.Length == 0
                ? "none"
                : string.Join("|", data.effectTags),
            data.failure,
            activation.jammed,
            activation.misfired,
            new GameplayOutcomeCausation(
                triggerParent,
                parentKey.OperationId,
                "direct-effect"));
    }

    public static GameplayDomainFactObserverBinding<
        DefenseEffectFact,
        DefenseFacilityEffectOutcomeReceipt> CreateBinding(
            IGameplayOutcomeRegistry registry,
            GameplayOutcomeLedger ledger,
            IGameplayOutcomeRecorder recorder,
            GameplayDomainFactCommitGroup group)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        return new GameplayDomainFactObserverBinding<
            DefenseEffectFact,
            DefenseFacilityEffectOutcomeReceipt>(
                ObserverId,
                fact => Capture(fact, ledger),
                registry,
                ledger,
                recorder,
                group);
    }

    private static GameplayResultKey TriggerParentKey(
        DefenseEffectFactData data) =>
        MigratedProducerOutcomeCatalog.ResultKey(
            MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent,
            data.activation.facilityId,
            data.activation.activationCount);

    private static GameplayOutcomeStatus Status(
        DefenseEffectResultKind result) => result switch
        {
            DefenseEffectResultKind.Succeeded =>
                GameplayOutcomeStatus.Succeeded,
            DefenseEffectResultKind.Partial =>
                GameplayOutcomeStatus.PartiallySucceeded,
            DefenseEffectResultKind.Failed => GameplayOutcomeStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };

    private static string FormatStatuses(
        DefenseEffectStatusFactData[] statuses)
    {
        if (statuses == null || statuses.Length == 0)
            return "none";
        return string.Join(
            "|",
            statuses
                .OrderBy(value => value.kind)
                .Select(value => value.kind
                    + ":" + value.value.ToString(
                        "R",
                        CultureInfo.InvariantCulture)
                    + ":" + value.remainingSeconds.ToString(
                        "R",
                        CultureInfo.InvariantCulture)
                    + ":" + value.stacks));
    }
}
