using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Closed registry of narrative events that are emitted by current gameplay
/// producers. Editor exports may replay these typed tuples, but may not invent
/// event IDs, outcomes, tags, historical evidence, or causal prose.
/// </summary>
public static class GameplayNarrativeSourceCatalog
{
    private static readonly CharacterGameplayNarrativeSource[] CharacterValues =
    {
        Character("character:expedition:battle-victory", CharacterNarrativeDomain.Combat,
            "battle-victory", "won", "원정 교전에서 승리했다.",
            "Assets/Scripts/Services/Offense/OffenseExpeditionBattleCompletionHandler.cs",
            "OffenseExpeditionBattleCompletionHandler", "battle-victory"),
        Character("character:expedition:battle-survived", CharacterNarrativeDomain.Combat,
            "battle-survived", "survived", "원정 교전에서 살아남았다.",
            "Assets/Scripts/Services/Offense/OffenseExpeditionBattleCompletionHandler.cs",
            "OffenseExpeditionBattleCompletionHandler", "battle-survived"),
        Character("character:expedition:node-battle", CharacterNarrativeDomain.Expedition,
            "node:Battle", "resolved", "원정 경로의 교전 지점을 해결했다.",
            "Assets/Scripts/Services/Offense/OffenseExpeditionExperienceRules.cs",
            "OffenseExpeditionExperienceRules.AwardNodeExperience", "$\"node:{node.Kind}\""),
        Character("character:expedition:node-event", CharacterNarrativeDomain.Expedition,
            "node:Event", "resolved", "원정 경로의 사건 지점을 해결했다.",
            "Assets/Scripts/Services/Offense/OffenseExpeditionExperienceRules.cs",
            "OffenseExpeditionExperienceRules.AwardNodeExperience", "$\"node:{node.Kind}\""),
        Character("character:expedition:node-camp", CharacterNarrativeDomain.Expedition,
            "node:Camp", "resolved", "원정 경로의 야영 지점을 해결했다.",
            "Assets/Scripts/Services/Offense/OffenseExpeditionExperienceRules.cs",
            "OffenseExpeditionExperienceRules.AwardNodeExperience", "$\"node:{node.Kind}\""),
        Character("character:expedition:node-cache", CharacterNarrativeDomain.Expedition,
            "node:Cache", "resolved", "원정 경로의 보급 지점을 해결했다.",
            "Assets/Scripts/Services/Offense/OffenseExpeditionExperienceRules.cs",
            "OffenseExpeditionExperienceRules.AwardNodeExperience", "$\"node:{node.Kind}\""),
        Character("character:expedition:node-boss", CharacterNarrativeDomain.Expedition,
            "node:Boss", "resolved", "원정 경로의 우두머리 지점을 해결했다.",
            "Assets/Scripts/Services/Offense/OffenseExpeditionExperienceRules.cs",
            "OffenseExpeditionExperienceRules.AwardNodeExperience", "$\"node:{node.Kind}\""),
        Character("character:survival:hunt", CharacterNarrativeDomain.Survival,
            "survival/hunt", "hunt", "야생동물을 사냥했다.",
            "Assets/Scripts/Services/Wildlife/WildlifeHuntRuntime.cs",
            "WildlifeHuntRuntime.RecordHuntNarrative", "survival/hunt"),
        Character("character:survival:dangerous-hunt", CharacterNarrativeDomain.Survival,
            "survival/hunt", "dangerous-hunt", "위험한 야생동물을 사냥했다.",
            "Assets/Scripts/Services/Wildlife/WildlifeHuntRuntime.cs",
            "WildlifeHuntRuntime.RecordHuntNarrative", "dangerous-hunt"),
        Character("character:invasion:started", CharacterNarrativeDomain.Invasion,
            "invasion-started", "faced", "침공이 시작될 때 위협에 맞섰다.",
            "Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs",
            "CharacterSkillRuntimeEffects", "invasion-started"),
        Character("character:need:hunger-critical", CharacterNarrativeDomain.Need,
            "need:hunger", "critical", "허기가 위험 수준으로 내려갔다.",
            "Assets/Scripts/Services/Character/Core/CharacterStats.cs",
            "CharacterStats.ApplyStatDeltaWithoutPublishing", "nextValue < 20f ? \"critical\" : \"satisfied\""),
        Character("character:need:hunger-satisfied", CharacterNarrativeDomain.Need,
            "need:hunger", "satisfied", "허기가 충분히 회복됐다.",
            "Assets/Scripts/Services/Character/Core/CharacterStats.cs",
            "CharacterStats.ApplyStatDeltaWithoutPublishing", "nextValue < 20f ? \"critical\" : \"satisfied\""),
        Character("character:need:sleep-critical", CharacterNarrativeDomain.Need,
            "need:sleep", "critical", "수면 욕구가 위험 수준으로 내려갔다.",
            "Assets/Scripts/Services/Character/Core/CharacterStats.cs",
            "CharacterStats.ApplyStatDeltaWithoutPublishing", "need:{condition.ToString().ToLowerInvariant()}"),
        Character("character:need:sleep-satisfied", CharacterNarrativeDomain.Need,
            "need:sleep", "satisfied", "수면 욕구가 충분히 회복됐다.",
            "Assets/Scripts/Services/Character/Core/CharacterStats.cs",
            "CharacterStats.ApplyStatDeltaWithoutPublishing", "need:{condition.ToString().ToLowerInvariant()}"),
        Character("character:work:repair-completed", CharacterNarrativeDomain.Work,
            "work:repair", CharacterActivityOutcomes.Completed, "시설 수리를 완료했다.",
            "Assets/Scripts/Services/Combat/Work/RepairWorkExecutionHandler.cs",
            "RepairWorkExecutionHandler", "FacilityWorkType.Repair"),
        Character("character:work:repair-blocked", CharacterNarrativeDomain.Work,
            "work:repair", CharacterActivityOutcomes.Blocked, "시설 수리가 중단됐다.",
            "Assets/Scripts/Services/Combat/Work/RepairWorkExecutionHandler.cs",
            "RepairWorkExecutionHandler", "CharacterActivityOutcomes.Blocked"),
        Character("character:work:research-completed", CharacterNarrativeDomain.Work,
            "work:research", CharacterActivityOutcomes.Completed, "시설 연구를 완료했다.",
            "Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs",
            "ResearchWorkExecutionAdapter", "CharacterActivityOutcomes.Completed"),
        Character("character:work:research-progress", CharacterNarrativeDomain.Work,
            "work:research", CharacterActivityOutcomes.Progress, "시설 연구를 진행했다.",
            "Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs",
            "ResearchWorkExecutionAdapter", "CharacterActivityOutcomes.Progress"),
        Character("character:work:research-failed", CharacterNarrativeDomain.Work,
            "work:research", CharacterActivityOutcomes.Failed, "시설 연구에 실패했다.",
            "Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs",
            "ResearchWorkExecutionAdapter", "CharacterActivityOutcomes.Failed"),
        Character("character:relationship:minion-conflict", CharacterNarrativeDomain.Relationship,
            "minion-social-conflict", CharacterActivityOutcomes.Changed, "거주자와 사회적 충돌을 겪었다.",
            "Assets/Scripts/Services/Captivity/MinionSettlementSocialRuntime.cs",
            "MinionSettlementSocialRuntime", "minion-social-conflict"),
        Character("character:relationship:answered-insult", CharacterNarrativeDomain.Relationship,
            "social:answer-insult", CharacterActivityOutcomes.Responded, "상대의 모욕에 맞섰다.",
            "Assets/Scripts/Services/Character/Identity/Runtime/CharacterIdentityDomainAdapters.cs",
            "CharacterIdentityDomainAdapters", "social:answer-insult"),
        Character("character:mood:battle-victory", CharacterNarrativeDomain.Mood,
            "offense:battle-victory", "positive", "원정 교전 승리로 기분이 좋아졌다.",
            "Assets/Scripts/Services/Offense/OffenseExpeditionBattleCompletionHandler.cs",
            "OffenseExpeditionBattleCompletionHandler", "offense:battle-victory"),
        Character("character:mood:battle-failure", CharacterNarrativeDomain.Mood,
            "offense:battle-failure", "negative", "원정 전투의 패배로 기분이 나빠졌다.",
            "Assets/Scripts/Services/Offense/OffenseExpeditionBattleCompletionHandler.cs",
            "OffenseExpeditionBattleCompletionHandler", "offense:battle-failure"),
        Character("character:relationship:taboo-witness", CharacterNarrativeDomain.Relationship,
            "survival/taboo-witness", "witnessed", "다른 인물의 금기 행동을 목격했다.",
            "Assets/Scripts/Services/Survival/CharacterDeprivationConsequences.cs",
            "CharacterDeprivationConsequences.ApplyWitnessMood", "survival/taboo-witness")
    };

    private static readonly EquipmentGameplayUsageSource[] EquipmentValues =
    {
        EquipmentDefinition("equipment:melee-hit", "combat:hit", "hit",
            HistoricalEvidenceKind.None, new[] { "melee", "Contact" },
            "근접 무기의 공격이 적중했다.",
            EquipmentGameplayUsageApplicability.MeleeWeapon),
        EquipmentDefinition("equipment:ranged-hit-close", "combat:hit", "hit",
            HistoricalEvidenceKind.RepeatedLongRangeHit, new[] { "ranged", "Near" },
            "원거리 무기의 근거리 공격이 적중했다.",
            EquipmentGameplayUsageApplicability.RangedWeapon),
        EquipmentDefinition("equipment:ranged-hit-medium", "combat:hit", "hit",
            HistoricalEvidenceKind.RepeatedLongRangeHit, new[] { "ranged", "Medium" },
            "원거리 무기의 중거리 공격이 적중했다.",
            EquipmentGameplayUsageApplicability.RangedWeapon),
        EquipmentDefinition("equipment:ranged-hit-long", "combat:hit", "hit",
            HistoricalEvidenceKind.RepeatedLongRangeHit, new[] { "ranged", "Long" },
            "원거리 무기의 장거리 공격이 적중했다.",
            EquipmentGameplayUsageApplicability.RangedWeapon),
        EquipmentDefinition("equipment:attack-missed", "combat:attack", "miss",
            HistoricalEvidenceKind.None, new[] { "melee", "Contact" },
            "무기로 공격했지만 적중하지 않았다.",
            EquipmentGameplayUsageApplicability.MeleeWeapon),
        EquipmentDefinition("equipment:ranged-attack-missed-medium", "combat:attack", "miss",
            HistoricalEvidenceKind.None, new[] { "ranged", "Medium" },
            "원거리 무기로 공격했지만 적중하지 않았다.",
            EquipmentGameplayUsageApplicability.RangedWeapon),
        EquipmentDefinition("equipment:attack-failed", "combat:failed-attack", "miss",
            HistoricalEvidenceKind.None, new[] { "melee", "Contact" },
            "무기 공격을 실행하지 못했다.",
            EquipmentGameplayUsageApplicability.MeleeWeapon),
        EquipmentDefinition("equipment:ranged-attack-failed-medium", "combat:failed-attack", "miss",
            HistoricalEvidenceKind.None, new[] { "ranged", "Medium" },
            "원거리 무기 공격을 실행하지 못했다.",
            EquipmentGameplayUsageApplicability.RangedWeapon),
        EquipmentDefinition("equipment:shield-block", "combat:block", "blocked",
            HistoricalEvidenceKind.ProtectedOwner, new[] { "shield", "defense" },
            "방패가 공격을 막았다.",
            EquipmentGameplayUsageApplicability.Shield),
        EquipmentDefinition("equipment:armor-absorb", "combat:absorb", "absorbed",
            HistoricalEvidenceKind.ProtectedOwner, new[] { "armor", "defense" },
            "방어구가 피해를 흡수했다.",
            EquipmentGameplayUsageApplicability.Armor)
    };

    private static readonly FacilityGameplayUsageSource[] FacilityValues =
    {
        FacilityDefinition("facility:visit", "facility.visit", new[] { "service", "visit" },
            "방문객이 시설을 이용했다.", FacilityGameplayUsageApplicability.VisitorFacility),
        FacilityDefinition("facility:revenue", "facility.revenue", new[] { "service", "revenue" },
            "시설 운영에서 수익이 발생했다.", FacilityGameplayUsageApplicability.ShopRuntime),
        FacilityDefinition("facility:stock-consumed", "facility.stock-consumed", new[] { "production", "Food" },
            "시설 운영 중 재고가 소비됐다.", FacilityGameplayUsageApplicability.ShopRuntime),
        FacilityDefinition("facility:crime", "facility.crime", new[] { "accident", "service" },
            "시설에서 범죄 사건이 발생했다.", FacilityGameplayUsageApplicability.ShopRuntime),
        FacilityDefinition("facility:restock-failed", "facility.restock-failed", new[] { "accident", "logistics" },
            "시설 재입고가 실패했다.", FacilityGameplayUsageApplicability.ShopRuntime),
        FacilityDefinition("facility:defense-triggered", "facility.defense-triggered", new[] { "defense", "combat" },
            "방어 시설이 침입자 대응에 작동했다.", FacilityGameplayUsageApplicability.DefenseRuntime),
        FacilityDefinition("facility:invasion-damage", "facility.invasion-damage", new[] { "defense", "accident" },
            "시설이 침공 중 피해를 입었다.", FacilityGameplayUsageApplicability.DamageableFacility),
        FacilityDefinition("facility:clean-service-day", "facility.clean-service-day", new[] { "service", "clean" },
            "시설이 사고 없이 운영일을 마쳤다.", FacilityGameplayUsageApplicability.CleanServiceDay)
    };

    public static IReadOnlyList<CharacterGameplayNarrativeSource> Characters => CharacterValues;
    public static IReadOnlyList<EquipmentGameplayUsageSource> Equipment => EquipmentValues;
    public static IReadOnlyList<FacilityGameplayUsageSource> Facilities => FacilityValues;

    public static CharacterGameplayNarrativeSource RequireCharacter(string sourceId) =>
        CharacterValues.Single(value => string.Equals(value.SourceId, sourceId, StringComparison.Ordinal));

    public static EquipmentGameplayUsageSource RequireEquipment(string sourceId) =>
        EquipmentValues.Single(value => string.Equals(value.SourceId, sourceId, StringComparison.Ordinal));

    public static FacilityGameplayUsageSource RequireFacility(string sourceId) =>
        FacilityValues.Single(value => string.Equals(value.SourceId, sourceId, StringComparison.Ordinal));

    private static CharacterGameplayNarrativeSource Character(
        string sourceId, CharacterNarrativeDomain domain, string factId, string outcome,
        string publicText, string producerPath, string producerSymbol, string producerProbe) =>
        new(sourceId, domain, factId, outcome, publicText,
            producerPath, producerSymbol, producerProbe);

    private static EquipmentGameplayUsageSource EquipmentDefinition(
        string sourceId, string eventId, string outcomeId,
        HistoricalEvidenceKind historicalEvidenceKind, string[] sourceTags, string publicText,
        EquipmentGameplayUsageApplicability applicability) =>
        new(sourceId, eventId, outcomeId, historicalEvidenceKind, sourceTags, publicText,
            applicability,
            "Assets/Scripts/Services/Combat/CombatResolutionService.cs",
            "CombatResolutionService", eventId);

    private static FacilityGameplayUsageSource FacilityDefinition(
        string sourceId, string eventId, string[] sourceTags, string publicText,
        FacilityGameplayUsageApplicability applicability) =>
        new(sourceId, eventId, sourceTags, publicText, applicability,
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordEventRecorder.cs",
            "FacilityEvolutionRecordEventRecorder", eventId);
}

/// <summary>
/// Closed target-definition requirements for replaying a registered facility
/// event. These are deliberately derived from the same authored runtime and
/// ability metadata that gates its live producer; they are not recipe labels.
/// </summary>
public enum FacilityGameplayUsageApplicability
{
    VisitorFacility,
    ShopRuntime,
    DefenseRuntime,
    DamageableFacility,
    CleanServiceDay
}

public abstract class GameplayNarrativeSourceDefinition
{
    protected GameplayNarrativeSourceDefinition(
        string sourceId, string publicText, string producerPath,
        string producerSymbol, string producerProbe)
    {
        SourceId = Require(sourceId, nameof(sourceId));
        PublicText = Require(publicText, nameof(publicText));
        ProducerPath = Require(producerPath, nameof(producerPath));
        ProducerSymbol = Require(producerSymbol, nameof(producerSymbol));
        ProducerProbe = Require(producerProbe, nameof(producerProbe));
    }

    public string SourceId { get; }
    public string PublicText { get; }
    public string ProducerPath { get; }
    public string ProducerSymbol { get; }
    public string ProducerProbe { get; }

    public string FormatPublicFact(int recordedCount)
    {
        int count = Math.Max(1, recordedCount);
        return count == 1
            ? PublicText
            : PublicText.TrimEnd('.') + ". 같은 유형의 기록이 총 " + count + "회 누적됐다.";
    }

    public string FormatPublicFact(GameplayNarrativeEventContext context)
    {
        if (context == null || !context.HasNarrativeDetail)
            throw new InvalidOperationException(
                "Narrative presentation evidence requires a concrete event instance context.");
        string text = "시점: " + context.occurredDay.ToString(CultureInfo.InvariantCulture)
            + "일, 순서: " + context.sequence.ToString(CultureInfo.InvariantCulture)
            + ". 장소: " + context.locationDisplayName
            + ". 인물: " + context.actorDisplayName;
        if (!string.IsNullOrWhiteSpace(context.counterpartyDisplayName))
            text += ". 상대: " + context.counterpartyDisplayName;
        if (!string.IsNullOrWhiteSpace(context.usedObjectDisplayName))
            text += ". 사용 대상: " + context.usedObjectDisplayName;
        if (!string.IsNullOrWhiteSpace(context.resultDetail))
            text += ". 결과: " + context.resultDetail;
        return text + ". 사건: " + PublicText;
    }

    protected static string Require(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new ArgumentException("Gameplay narrative source values must be canonical.", name);
        return canonical;
    }
}

[Flags]
public enum EquipmentGameplayUsageApplicability
{
    None = 0,
    MeleeWeapon = 1 << 0,
    RangedWeapon = 1 << 1,
    Armor = 1 << 2,
    Shield = 1 << 3
}

public sealed class CharacterGameplayNarrativeSource : GameplayNarrativeSourceDefinition
{
    public CharacterGameplayNarrativeSource(
        string sourceId, CharacterNarrativeDomain domain, string factId, string outcome,
        string publicText, string producerPath, string producerSymbol, string producerProbe)
        : base(sourceId, publicText, producerPath, producerSymbol, producerProbe)
    {
        Domain = domain;
        FactId = Require(factId, nameof(factId));
        Outcome = Require(outcome, nameof(outcome));
    }

    public CharacterNarrativeDomain Domain { get; }
    public string FactId { get; }
    public string Outcome { get; }

    public CharacterNarrativeFact Record(
        CharacterNarrativeLedger ledger, string subjectId, int repetitions, int day,
        GameplayNarrativeEventContext eventContext = null)
    {
        if (ledger == null) throw new ArgumentNullException(nameof(ledger));
        string subject = subjectId?.Trim() ?? string.Empty;
        int count = Math.Max(1, repetitions);
        for (int index = 0; index < count; index++)
        {
            GameplayNarrativeEventContext occurrence = eventContext?.Clone();
            if (occurrence != null)
            {
                occurrence.occurredDay = Math.Max(0, day + index);
                occurrence.eventInstanceId = occurrence.eventInstanceId + ":" + index;
            }
            ledger.Record(Domain, FactId, subject, Outcome, 1f, day + index,
                CharacterNarrativeEvidenceMetadata.Ordinary(Domain, FactId), occurrence);
        }
        return ledger.Facts.Single(value => value != null
            && value.domain == Domain
            && string.Equals(value.factId, FactId, StringComparison.Ordinal)
            && string.Equals(value.subjectId, subject, StringComparison.Ordinal));
    }
}

public sealed class EquipmentGameplayUsageSource : GameplayNarrativeSourceDefinition
{
    public EquipmentGameplayUsageSource(
        string sourceId, string eventId, string outcomeId,
        HistoricalEvidenceKind historicalEvidenceKind, IEnumerable<string> sourceTags,
        string publicText, EquipmentGameplayUsageApplicability applicability,
        string producerPath, string producerSymbol, string producerProbe)
        : base(sourceId, publicText, producerPath, producerSymbol, producerProbe)
    {
        EventId = Require(eventId, nameof(eventId));
        OutcomeId = Require(outcomeId, nameof(outcomeId));
        HistoricalEvidenceKind = historicalEvidenceKind;
        const EquipmentGameplayUsageApplicability supported =
            EquipmentGameplayUsageApplicability.MeleeWeapon
            | EquipmentGameplayUsageApplicability.RangedWeapon
            | EquipmentGameplayUsageApplicability.Armor
            | EquipmentGameplayUsageApplicability.Shield;
        if (applicability == EquipmentGameplayUsageApplicability.None
            || (applicability & ~supported) != EquipmentGameplayUsageApplicability.None)
            throw new ArgumentOutOfRangeException(nameof(applicability));
        Applicability = applicability;
        SourceTags = (sourceTags ?? Array.Empty<string>()).Select(value => Require(value, nameof(sourceTags)))
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (SourceTags.Count == 0) throw new ArgumentException("Equipment gameplay usage requires source tags.", nameof(sourceTags));
    }

    public string EventId { get; }
    public string OutcomeId { get; }
    public HistoricalEvidenceKind HistoricalEvidenceKind { get; }
    public EquipmentGameplayUsageApplicability Applicability { get; }
    public IReadOnlyList<string> SourceTags { get; }

    public bool AppliesTo(CombatEquipmentKind kind) => kind switch
    {
        CombatEquipmentKind.MeleeWeapon =>
            (Applicability & EquipmentGameplayUsageApplicability.MeleeWeapon) != 0,
        CombatEquipmentKind.RangedWeapon =>
            (Applicability & EquipmentGameplayUsageApplicability.RangedWeapon) != 0,
        CombatEquipmentKind.Armor =>
            (Applicability & EquipmentGameplayUsageApplicability.Armor) != 0,
        CombatEquipmentKind.Shield =>
            (Applicability & EquipmentGameplayUsageApplicability.Shield) != 0,
        _ => false
    };
}

public sealed class FacilityGameplayUsageSource : GameplayNarrativeSourceDefinition
{
    public FacilityGameplayUsageSource(
        string sourceId, string eventId, IEnumerable<string> sourceTags, string publicText,
        FacilityGameplayUsageApplicability applicability,
        string producerPath, string producerSymbol, string producerProbe)
        : base(sourceId, publicText, producerPath, producerSymbol, producerProbe)
    {
        EventId = Require(eventId, nameof(eventId));
        if (!Enum.IsDefined(typeof(FacilityGameplayUsageApplicability), applicability))
            throw new ArgumentOutOfRangeException(nameof(applicability));
        Applicability = applicability;
        SourceTags = (sourceTags ?? Array.Empty<string>()).Select(value => Require(value, nameof(sourceTags)))
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (SourceTags.Count == 0) throw new ArgumentException("Facility gameplay usage requires source tags.", nameof(sourceTags));
    }

    public string EventId { get; }
    public FacilityGameplayUsageApplicability Applicability { get; }
    public IReadOnlyList<string> SourceTags { get; }

    /// <summary>
    /// Tests only immutable authoring requirements for this source against the
    /// actual recipe source definition. Live dynamic prerequisites remain live:
    /// a shop still needs a completed transaction and a defense facility still
    /// needs an eligible activation.
    /// </summary>
    public bool AppliesTo(BuildingSO definition, out string failureReason)
    {
        if (definition == null)
        {
            failureReason = "source-building-definition-missing";
            return false;
        }

        bool visitorFacility = definition.Facility?.IsVisitorFacility == true
            && (definition.runtimeArchetype == BuildingRuntimeArchetypeKind.Facility
                || definition.runtimeArchetype == BuildingRuntimeArchetypeKind.Shop);
        bool applicable = Applicability switch
        {
            FacilityGameplayUsageApplicability.VisitorFacility => visitorFacility,
            FacilityGameplayUsageApplicability.CleanServiceDay => visitorFacility,
            FacilityGameplayUsageApplicability.ShopRuntime =>
                definition.runtimeArchetype == BuildingRuntimeArchetypeKind.Shop,
            FacilityGameplayUsageApplicability.DefenseRuntime =>
                definition.runtimeArchetype == BuildingRuntimeArchetypeKind.DefenseFacility
                && definition.Defense?.IsDefenseFacility == true,
            FacilityGameplayUsageApplicability.DamageableFacility =>
                definition.Facility != null && !definition.IsGridMovement,
            _ => false
        };
        failureReason = applicable ? string.Empty : Applicability switch
        {
            FacilityGameplayUsageApplicability.VisitorFacility =>
                "requires-visitor-facility-runtime-and-authored-visitor-role",
            FacilityGameplayUsageApplicability.CleanServiceDay =>
                "requires-visitor-facility-runtime-and-authored-visitor-role",
            FacilityGameplayUsageApplicability.ShopRuntime => "requires-shop-runtime",
            FacilityGameplayUsageApplicability.DefenseRuntime =>
                "requires-enabled-defense-facility-runtime",
            FacilityGameplayUsageApplicability.DamageableFacility =>
                "requires-nonmovement-facility-definition",
            _ => "unsupported-facility-source-applicability"
        };
        return applicable;
    }

    /// <summary>
    /// The controlled export currently records direct ledger tuples. A clean
    /// service day is not legal without first replaying its same-day completed
    /// visits and incident-free day-end, so it is fail-closed until that typed
    /// sequence is represented rather than being asserted as a standalone fact.
    /// </summary>
    public bool IsDirectLedgerReplaySupported(out string failureReason)
    {
        if (Applicability != FacilityGameplayUsageApplicability.CleanServiceDay)
        {
            failureReason = string.Empty;
            return true;
        }

        failureReason = "requires-completed-visits-and-incident-free-operating-day-sequence";
        return false;
    }
}

#if UNITY_EDITOR
public sealed class EquipmentGameplayFormulaReplayResult
{
    public EquipmentGameplayFormulaReplayResult(
        EquipmentEvolutionState state,
        EvolutionNode node,
        IReadOnlyDictionary<string, EquipmentGameplayUsageSource> sourceByEvidenceId)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Node = node ?? throw new ArgumentNullException(nameof(node));
        SourceByEvidenceId = sourceByEvidenceId
            ?? throw new ArgumentNullException(nameof(sourceByEvidenceId));
    }

    public EquipmentEvolutionState State { get; }
    public EvolutionNode Node { get; }
    public IReadOnlyDictionary<string, EquipmentGameplayUsageSource> SourceByEvidenceId { get; }
}

/// <summary>Narrow runtime-owned replay used by the immutable data exporter.
/// It deliberately calls the same usage ledger and formula projection helpers
/// used by live equipment evolution.</summary>
public static class EquipmentGameplayFormulaReplay
{
    /// <summary>
    /// Checks both the concrete equipment kind's gameplay ledger path and the
    /// formula's concrete runtime consumers. This is deliberately defined
    /// beside the replay, so editor pilots cannot infer a kind from a fixture
    /// ID or treat an unsupported catalog definition as a legal source.
    /// </summary>
    public static bool IsDefinitionReplayEligible(
        EquipmentEvolutionFormulaCatalogSO catalog,
        CombatEquipmentDefinitionSO definition,
        out string failureReason)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (definition == null)
        {
            failureReason = "missing-equipment-definition";
            return false;
        }

        EquipmentFormulaTargetContext targetContext;
        try
        {
            targetContext = new EquipmentFormulaTargetContext(definition);
        }
        catch (ArgumentException)
        {
            failureReason = "invalid-concrete-equipment-definition";
            return false;
        }

        if (!TryResolveReplayDirection(targetContext.Kind, out _, out failureReason))
            return false;
        if (!EquipmentEvolutionRules.HasRuntimeApplicablePositiveOffer(catalog, targetContext))
        {
            failureReason = "no-runtime-applicable-positive-offer";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public static EquipmentGameplayFormulaReplayResult Capture(
        EquipmentEvolutionFormulaCatalogSO catalog,
        CombatEquipmentDefinitionSO definition,
        string equipmentInstanceId,
        int rowIndex,
        int influenceUseCount = 0)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        string targetId = equipmentInstanceId?.Trim() ?? string.Empty;
        if (targetId.Length == 0) throw new ArgumentException(
            "Equipment gameplay replay requires a persistent target.", nameof(equipmentInstanceId));
        if (influenceUseCount < 0) throw new ArgumentOutOfRangeException(nameof(influenceUseCount));
        if (!IsDefinitionReplayEligible(catalog, definition, out string failureReason))
            throw new InvalidOperationException(
                "Equipment gameplay replay is not eligible for definition '"
                + definition.EquipmentId + "': " + failureReason + ".");
        EquipmentFormulaTargetContext targetContext = new EquipmentFormulaTargetContext(definition);
        if (!TryResolveReplayDirection(targetContext.Kind, out EquipmentEvolutionDirection expectedDirection,
                out _))
        {
            throw new InvalidOperationException(
                "Equipment gameplay replay lost its validated concrete direction.");
        }
        string[] sourceIds = targetContext.Kind switch
        {
            CombatEquipmentKind.MeleeWeapon => new[]
            {
                "equipment:melee-hit", "equipment:attack-missed", "equipment:melee-hit",
                "equipment:attack-failed", "equipment:melee-hit", "equipment:attack-missed"
            },
            CombatEquipmentKind.RangedWeapon => new[]
            {
                "equipment:ranged-hit-close", "equipment:ranged-hit-medium",
                "equipment:ranged-hit-long", "equipment:ranged-attack-missed-medium",
                "equipment:ranged-hit-medium", "equipment:ranged-attack-failed-medium"
            },
            CombatEquipmentKind.Armor => new[]
            {
                "equipment:armor-absorb", "equipment:armor-absorb", "equipment:armor-absorb",
                "equipment:armor-absorb", "equipment:armor-absorb", "equipment:armor-absorb"
            },
            CombatEquipmentKind.Shield => new[]
            {
                "equipment:shield-block", "equipment:shield-block", "equipment:shield-block",
                "equipment:shield-block", "equipment:shield-block", "equipment:shield-block"
            },
            _ => throw new InvalidOperationException(
                "Unsupported gameplay equipment replay kind: " + targetContext.Kind)
        };
        EquipmentEvolutionState state = new EquipmentEvolutionState
        {
            generation = 0,
            usageLedger = new UsageLedger(),
            formulaEvidence = new List<EquipmentEvolutionFormulaEvidenceRecord>(),
            evolutionNodes = new List<EvolutionNode>()
        };
        UsageLedgerCompactor compactor = new UsageLedgerCompactor();
        Dictionary<string, EquipmentGameplayUsageSource> sourceByEvidence =
            new(StringComparer.Ordinal);
        for (int factIndex = 0; factIndex < sourceIds.Length; factIndex++)
        {
            EquipmentGameplayUsageSource source = GameplayNarrativeSourceCatalog.RequireEquipment(
                sourceIds[(factIndex + rowIndex) % sourceIds.Length]);
            if (!source.AppliesTo(targetContext.Kind))
                throw new InvalidOperationException(
                    "Equipment gameplay source '" + source.SourceId
                    + "' does not apply to " + targetContext.Kind + ".");
            float amount = source.OutcomeId == "miss"
                ? 0f
                : 4f + ((rowIndex * 7 + factIndex * 3) % 29);
            UsageLedgerEvent recorded = compactor.Record(
                state.usageLedger, source.EventId, amount,
                "character:equipment-user:" + rowIndex.ToString("D5", CultureInfo.InvariantCulture),
                targetId, source.SourceTags,
                historicalEvidenceKind: source.HistoricalEvidenceKind,
                outcomeId: source.OutcomeId,
                generation: 0,
                repeatCount: 1,
                narrativeContext: BuildReplayContext(
                    source,
                    targetContext,
                    targetId,
                    rowIndex,
                    factIndex,
                    amount));
            EquipmentEvolutionRules.RecordFormulaEvidence(state, recorded);
            state.formulaEvidence.Single(value => value != null
                && string.Equals(value.evidenceId, recorded.evidenceId,
                    StringComparison.Ordinal)).influenceUseCount = influenceUseCount;
            sourceByEvidence.Add(recorded.evidenceId, source);
        }
        CompactedHistorySegment segment = compactor.CloseGeneration(state.usageLedger, 0);
        EquipmentEvolutionDirection inferred = EquipmentEvolutionRules.InferDirection(segment);
        if (inferred != expectedDirection)
            throw new InvalidOperationException(
                $"Gameplay equipment evidence inferred {inferred}, expected {expectedDirection}.");
        EvolutionNode node = EquipmentEvolutionRules.BuildFormulaNode(
            state, catalog, targetContext, targetId,
            "equipment-formula-node:pilot-" + rowIndex.ToString("D5", CultureInfo.InvariantCulture),
            string.Empty,
            "history:pilot-" + rowIndex.ToString("D5", CultureInfo.InvariantCulture),
            inferred,
            string.Empty,
            generation: 1,
            historical: false,
            manifestationPosition: "reforge:pilot-" + rowIndex.ToString("D5", CultureInfo.InvariantCulture));
        return new EquipmentGameplayFormulaReplayResult(state, node, sourceByEvidence);
    }

    private static GameplayNarrativeEventContext BuildReplayContext(
        EquipmentGameplayUsageSource source,
        EquipmentFormulaTargetContext targetContext,
        string equipmentInstanceId,
        int rowIndex,
        int factIndex,
        float amount)
    {
        IReadOnlyList<OffenseTargetDefinition> targets = OffenseWorldMapService.CreateDefaultTargets();
        OffenseTargetDefinition target = targets[(rowIndex + factIndex) % targets.Count];
        string location = !string.IsNullOrWhiteSpace(target.regionDisplayName)
            ? target.regionDisplayName
            : target.title;
        return new GameplayNarrativeEventContext
        {
            eventInstanceId = $"equipment-replay:{rowIndex}:{factIndex}",
            chainId = $"equipment-replay:{rowIndex}",
            locationId = target.id,
            locationDisplayName = location,
            actorId = $"character:equipment-user:{rowIndex:D5}",
            actorDisplayName = "장비 사용자 " + (rowIndex + 1).ToString(CultureInfo.InvariantCulture),
            counterpartyId = $"character:equipment-opponent:{rowIndex:D5}:{factIndex:D2}",
            counterpartyDisplayName = "교전 상대 "
                + (1 + (rowIndex + factIndex) % 17).ToString(CultureInfo.InvariantCulture),
            usedObjectId = equipmentInstanceId,
            usedObjectDisplayName = targetContext.Definition.DisplayName,
            resultDetail = FormatReplayResult(source, amount),
            occurredDay = 1 + checked(rowIndex * 6) + factIndex
        };
    }

    private static string FormatReplayResult(
        EquipmentGameplayUsageSource source,
        float amount)
    {
        string value = amount.ToString("0.##", CultureInfo.InvariantCulture);
        return source.EventId switch
        {
            "combat:hit" => "적중, 피해 " + value,
            "combat:block" => "방어 성공, 차단량 " + value,
            "combat:absorb" => "피해 흡수량 " + value,
            "combat:attack" => "공격 빗나감, 피해 0",
            "combat:failed-attack" => "공격 실행 실패, 피해 0",
            _ => source.OutcomeId + ", 수치 " + value
        };
    }

    private static bool TryResolveReplayDirection(
        CombatEquipmentKind kind,
        out EquipmentEvolutionDirection direction,
        out string failureReason)
    {
        switch (kind)
        {
            case CombatEquipmentKind.MeleeWeapon:
                direction = EquipmentEvolutionDirection.Melee;
                failureReason = string.Empty;
                return true;
            case CombatEquipmentKind.RangedWeapon:
                direction = EquipmentEvolutionDirection.Ranged;
                failureReason = string.Empty;
                return true;
            case CombatEquipmentKind.RecoverableThrowingWeapon:
                direction = default;
                failureReason = "no-typed-gameplay-replay-for-recoverable-throwing-weapon";
                return false;
            case CombatEquipmentKind.Armor:
            case CombatEquipmentKind.Shield:
                direction = EquipmentEvolutionDirection.Protection;
                failureReason = string.Empty;
                return true;
            default:
                direction = default;
                failureReason = "unsupported-equipment-kind";
                return false;
        }
    }
}
#endif
