using System;
using System.Collections.Generic;
using System.Linq;

public enum CharacterFunctionalCapacityId
{
    MentalMaintenance = 0,
    VisualDiscernment = 1,
    AuditorySensing = 2,
    RespiratoryExchange = 3,
    PowerCirculation = 4,
    IntakeProcessing = 5,
    PurificationProcessing = 6,
    VitalityResponse = 7,
    PhysicalPower = 8,
    PrecisionManipulation = 9,
    PhysicalMobility = 10,
    Communication = 11,
    ArcaneConduction = 12,
    ImmuneDefense = 13
}

public enum CharacterCompositePerformanceId
{
    SituationalAwareness = 0,
    PrecisionExecution = 1,
    MobilityExecution = 2,
    SustainedExecution = 3,
    RecoveryFoundation = 4
}

public enum CharacterPerformanceFormulaDomain
{
    Composite = 0,
    Work = 1,
    Combat = 2,
    Medical = 3,
    SurvivalSocial = 4
}

public enum CharacterPerformanceResultChannel
{
    Factor = 0,
    Speed = 1,
    AccidentRisk = 2,
    Quality = 3,
    Yield = 4,
    SuccessChance = 5,
    Recovery = 6,
    Consumption = 7,
    Exposure = 8,
    Detection = 9,
    MoodDuration = 10,
    RelationshipRecovery = 11
}

[Flags]
public enum CharacterPerformanceInputRole
{
    None = 0,
    Contribution = 1 << 0,
    Bottleneck = 1 << 1,
    Required = 1 << 2
}

public static class CharacterFunctionalCapacityIds
{
    public const string MentalMaintenance = "capacity:mental-maintenance";
    public const string VisualDiscernment = "capacity:visual-discernment";
    public const string AuditorySensing = "capacity:auditory-sensing";
    public const string RespiratoryExchange = "capacity:respiratory-exchange";
    public const string PowerCirculation = "capacity:power-circulation";
    public const string IntakeProcessing = "capacity:intake-processing";
    public const string PurificationProcessing = "capacity:purification-processing";
    public const string VitalityResponse = "capacity:vitality-response";
    public const string PhysicalPower = "capacity:physical-power";
    public const string PrecisionManipulation = "capacity:precision-manipulation";
    public const string PhysicalMobility = "capacity:physical-mobility";
    public const string Communication = "capacity:communication";
    public const string ArcaneConduction = "capacity:arcane-conduction";
    public const string ImmuneDefense = "capacity:immune-defense";

    public static string GetStableId(CharacterFunctionalCapacityId id) => id switch
    {
        CharacterFunctionalCapacityId.MentalMaintenance => MentalMaintenance,
        CharacterFunctionalCapacityId.VisualDiscernment => VisualDiscernment,
        CharacterFunctionalCapacityId.AuditorySensing => AuditorySensing,
        CharacterFunctionalCapacityId.RespiratoryExchange => RespiratoryExchange,
        CharacterFunctionalCapacityId.PowerCirculation => PowerCirculation,
        CharacterFunctionalCapacityId.IntakeProcessing => IntakeProcessing,
        CharacterFunctionalCapacityId.PurificationProcessing => PurificationProcessing,
        CharacterFunctionalCapacityId.VitalityResponse => VitalityResponse,
        CharacterFunctionalCapacityId.PhysicalPower => PhysicalPower,
        CharacterFunctionalCapacityId.PrecisionManipulation => PrecisionManipulation,
        CharacterFunctionalCapacityId.PhysicalMobility => PhysicalMobility,
        CharacterFunctionalCapacityId.Communication => Communication,
        CharacterFunctionalCapacityId.ArcaneConduction => ArcaneConduction,
        CharacterFunctionalCapacityId.ImmuneDefense => ImmuneDefense,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };

    public static string GetDisplayName(CharacterFunctionalCapacityId id) => id switch
    {
        CharacterFunctionalCapacityId.MentalMaintenance => "정신 유지",
        CharacterFunctionalCapacityId.VisualDiscernment => "시야 판별",
        CharacterFunctionalCapacityId.AuditorySensing => "음향 감지",
        CharacterFunctionalCapacityId.RespiratoryExchange => "호흡 교환",
        CharacterFunctionalCapacityId.PowerCirculation => "동력 순환",
        CharacterFunctionalCapacityId.IntakeProcessing => "섭취 처리",
        CharacterFunctionalCapacityId.PurificationProcessing => "정화 처리",
        CharacterFunctionalCapacityId.VitalityResponse => "활력 반응",
        CharacterFunctionalCapacityId.PhysicalPower => "근력 출력",
        CharacterFunctionalCapacityId.PrecisionManipulation => "정밀 조작",
        CharacterFunctionalCapacityId.PhysicalMobility => "신체 기동",
        CharacterFunctionalCapacityId.Communication => "의사 전달",
        CharacterFunctionalCapacityId.ArcaneConduction => "마력 전도",
        CharacterFunctionalCapacityId.ImmuneDefense => "면역 방어",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };
}

public static class CharacterCompositePerformanceIds
{
    public const string SituationalAwareness = "performance:composite:situational-awareness";
    public const string PrecisionExecution = "performance:composite:precision-execution";
    public const string MobilityExecution = "performance:composite:mobility-execution";
    public const string SustainedExecution = "performance:composite:sustained-execution";
    public const string RecoveryFoundation = "performance:composite:recovery-foundation";

    public static string GetStableId(CharacterCompositePerformanceId id) => id switch
    {
        CharacterCompositePerformanceId.SituationalAwareness => SituationalAwareness,
        CharacterCompositePerformanceId.PrecisionExecution => PrecisionExecution,
        CharacterCompositePerformanceId.MobilityExecution => MobilityExecution,
        CharacterCompositePerformanceId.SustainedExecution => SustainedExecution,
        CharacterCompositePerformanceId.RecoveryFoundation => RecoveryFoundation,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };

    public static string GetDisplayName(CharacterCompositePerformanceId id) => id switch
    {
        CharacterCompositePerformanceId.SituationalAwareness => "상황 파악",
        CharacterCompositePerformanceId.PrecisionExecution => "정밀 수행",
        CharacterCompositePerformanceId.MobilityExecution => "기동 수행",
        CharacterCompositePerformanceId.SustainedExecution => "지속 수행",
        CharacterCompositePerformanceId.RecoveryFoundation => "회복 기반",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };
}

public static class CharacterPerformanceFormulaIds
{
    public const string HaulCapacity = "performance:survival:haul-capacity";
    public const string MeleePower = "performance:combat:melee-power";
    public const string ArcanePower = "performance:combat:arcane-power";
    public const string ManaRecovery = "performance:combat:mana-recovery";
    public const string TreatmentSpeed = "performance:medical:treatment-speed";
    public const string TreatmentEfficiency = "performance:medical:treatment-efficiency";
    public const string SurgerySpeed = "performance:medical:surgery-speed";
    public const string SurgerySuccess = "performance:medical:surgery-success";
    public const string ComplicationRisk = "performance:medical:complication-risk";
    public const string DiseaseResistance = "performance:medical:disease-resistance";
    public const string ImmunityGain = "performance:medical:immunity-gain";
    public const string NutritionEfficiency = "performance:survival:nutrition-efficiency";
    public const string AlarmResponse = "performance:survival:alarm-response";
    public const string RiskDetection = "performance:survival:risk-detection";
    public const string NegativeMoodDuration = "performance:social:negative-mood-duration";
    public const string RelationshipRecovery = "performance:social:relationship-recovery";
}

public readonly struct CharacterFunctionalCapacityValue
{
    public CharacterFunctionalCapacityValue(
        CharacterFunctionalCapacityId capacityId,
        bool isApplicable,
        float value,
        string nonApplicableReason,
        IReadOnlyList<CharacterPerformanceContributionTrace> contributions)
    {
        if (isApplicable && (float.IsNaN(value) || float.IsInfinity(value) || value < 0f))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"{CharacterFunctionalCapacityIds.GetStableId(capacityId)} must be finite and non-negative.");
        }
        if (!isApplicable && string.IsNullOrWhiteSpace(nonApplicableReason))
        {
            throw new ArgumentException(
                $"N/A capacity {CharacterFunctionalCapacityIds.GetStableId(capacityId)} requires a reason.",
                nameof(nonApplicableReason));
        }

        CapacityId = capacityId;
        IsApplicable = isApplicable;
        Value = isApplicable ? value : 0f;
        NonApplicableReason = nonApplicableReason?.Trim() ?? string.Empty;
        Contributions = contributions ?? Array.Empty<CharacterPerformanceContributionTrace>();
    }

    public CharacterFunctionalCapacityId CapacityId { get; }
    public string StableId => CharacterFunctionalCapacityIds.GetStableId(CapacityId);
    public bool IsApplicable { get; }
    public float Value { get; }
    public string NonApplicableReason { get; }
    public IReadOnlyList<CharacterPerformanceContributionTrace> Contributions { get; }
}

public sealed class CharacterFunctionalCapacitySnapshot
{
    private readonly IReadOnlyDictionary<CharacterFunctionalCapacityId, CharacterFunctionalCapacityValue> values;

    public CharacterFunctionalCapacitySnapshot(
        IEnumerable<CharacterFunctionalCapacityValue> values)
    {
        CharacterFunctionalCapacityValue[] authored = (values
            ?? throw new ArgumentNullException(nameof(values))).ToArray();
        this.values = authored.ToDictionary(value => value.CapacityId);
        CharacterFunctionalCapacityId[] missing = Enum
            .GetValues(typeof(CharacterFunctionalCapacityId))
            .Cast<CharacterFunctionalCapacityId>()
            .Where(id => !this.values.ContainsKey(id))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Functional capacity snapshot is incomplete: "
                + string.Join(", ", missing.Select(CharacterFunctionalCapacityIds.GetStableId)));
        }
    }

    public IReadOnlyCollection<CharacterFunctionalCapacityValue> Values =>
        values.Values.ToArray();

    public CharacterFunctionalCapacityValue Get(CharacterFunctionalCapacityId id) =>
        values.TryGetValue(id, out CharacterFunctionalCapacityValue value)
            ? value
            : throw new KeyNotFoundException(CharacterFunctionalCapacityIds.GetStableId(id));
}

public sealed class CharacterPerformanceContributionTrace
{
    public string SourceKind { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public float AuthoredValue { get; set; }
    public float AppliedValue { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public sealed class CharacterPerformanceFailure
{
    public string Code { get; set; } = string.Empty;
    public string CapacityId { get; set; } = string.Empty;
    public float CurrentValue { get; set; }
    public float RequiredValue { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CharacterPerformanceSnapshot
{
    public string FormulaId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public CharacterPerformanceResultChannel ResultChannel { get; set; }
    public float BaseValue { get; set; } = 1f;
    public float FunctionalCapacityFactor { get; set; } = 1f;
    public float ProficiencyFactor { get; set; } = 1f;
    public float GameplayEffectFactor { get; set; } = 1f;
    public float ContextFactor { get; set; } = 1f;
    public float Value { get; set; } = 1f;
    public float WeightedCapacityValue { get; set; } = 1f;
    public float BottleneckCap { get; set; } = float.PositiveInfinity;
    public bool IsApplicable { get; set; } = true;
    public CharacterPerformanceFailure Failure { get; set; }
    public IReadOnlyList<CharacterPerformanceContributionTrace> Contributions { get; set; } =
        Array.Empty<CharacterPerformanceContributionTrace>();
}
