using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct InvasionCombatReportReadyEvent
{
    public InvasionCombatReportSnapshot report { get; }

    public InvasionCombatReportReadyEvent(InvasionCombatReportSnapshot report)
    {
        this.report = report;
    }
}

internal sealed class InvasionCombatReport
{
    private readonly List<DefenseContribution> defenseContributions = new List<DefenseContribution>();
    private readonly List<BuildableObject> damagedFacilities = new List<BuildableObject>();
    private readonly List<string> observations = new List<string>();
    private readonly List<string> synergyLogs = new List<string>();
    private readonly List<string> activationLogs = new List<string>();
    private readonly IReadOnlyList<DefenseContribution> defenseContributionsView;
    private readonly IReadOnlyList<BuildableObject> damagedFacilitiesView;
    private readonly IReadOnlyList<string> observationsView;
    private readonly IReadOnlyList<string> synergyLogsView;
    private readonly IReadOnlyList<string> activationLogsView;

    public InvasionCombatReport(InvasionThreatSnapshot snapshot, float startedAt)
    {
        defenseContributionsView = defenseContributions.AsReadOnly();
        damagedFacilitiesView = damagedFacilities.AsReadOnly();
        observationsView = observations.AsReadOnly();
        synergyLogsView = synergyLogs.AsReadOnly();
        activationLogsView = activationLogs.AsReadOnly();
        ThreatSnapshot = snapshot;
        StartedAt = startedAt;
    }

    public InvasionThreatSnapshot ThreatSnapshot { get; }
    public float StartedAt { get; }
    public CharacterActor Intruder { get; private set; }
    public CharacterActor FinalCombatOwner { get; private set; }
    public bool FinalCombatStarted { get; private set; }
    public bool Defended { get; private set; }
    public float ResidualRisk { get; private set; }
    public bool IsResolved { get; private set; }

    public IReadOnlyList<DefenseContribution> DefenseContributions => defenseContributionsView;
    public IReadOnlyList<BuildableObject> DamagedFacilities => damagedFacilitiesView;
    public IReadOnlyList<string> Observations => observationsView;
    public IReadOnlyList<string> SynergyLogs => synergyLogsView;
    public IReadOnlyList<string> ActivationLogs => activationLogsView;

    public DefenseContribution TopDamageContribution => defenseContributions
        .Where((item) => item.TotalDamage > 0f)
        .OrderByDescending((item) => item.TotalDamage)
        .FirstOrDefault();

    public DefenseContribution TopDelayContribution => defenseContributions
        .Where((item) => item.MaxDelaySeconds > 0f)
        .OrderByDescending((item) => item.MaxDelaySeconds)
        .FirstOrDefault();

    public IReadOnlyList<BuildableObject> BrokenFacilities => Array.AsReadOnly(damagedFacilities
        .Where((facility) => facility != null && facility.IsDamaged)
        .ToArray());

    public InvasionCombatReportSnapshot CreateSnapshot()
    {
        DefenseContributionSnapshot[] contributions = defenseContributions
            .Select((item) => new DefenseContributionSnapshot(item))
            .ToArray();
        InvasionFacilitySnapshot[] facilities = damagedFacilities
            .Where((facility) => facility != null)
            .Select((facility) => new InvasionFacilitySnapshot(facility))
            .ToArray();

        return new InvasionCombatReportSnapshot(
            ThreatSnapshot,
            StartedAt,
            Intruder != null ? Intruder.name : string.Empty,
            FinalCombatOwner != null ? FinalCombatOwner.name : string.Empty,
            FinalCombatStarted,
            Defended,
            ResidualRisk,
            IsResolved,
            contributions,
            facilities,
            observations,
            synergyLogs,
            activationLogs,
            ToDetailText());
    }

    public void SetIntruder(CharacterActor intruder)
    {
        Intruder = intruder;
    }

    public void RecordDefenseActivation(DefenseActivationSnapshot report)
    {
        if (report == null || report.Facility == null)
        {
            return;
        }

        DefenseContribution contribution = FindOrCreateContribution(report);
        contribution.Add(report);

        string activation = InvasionCombatReportFormatter.FormatActivation(report);
        if (!string.IsNullOrWhiteSpace(activation))
        {
            activationLogs.Add(activation);
        }

        foreach (string tag in report.EffectTags)
        {
            AddObservation(InvasionCombatReportFormatter.FormatObservation(tag));
        }

        string synergy = InvasionCombatReportFormatter.FormatSynergy(report);
        if (!string.IsNullOrWhiteSpace(synergy) && !synergyLogs.Contains(synergy))
        {
            synergyLogs.Add(synergy);
        }
    }

    public void RecordFacilityDamage(BuildableObject facility)
    {
        if (facility == null || damagedFacilities.Contains(facility))
        {
            return;
        }

        damagedFacilities.Add(facility);
    }

    public void RecordFinalCombat(CharacterActor owner)
    {
        FinalCombatStarted = true;
        FinalCombatOwner = owner;
    }

    public void Resolve(bool defended, float residualRisk)
    {
        Defended = defended;
        ResidualRisk = Mathf.Max(0f, residualRisk);
        IsResolved = true;
    }

    public string ToDetailText()
    {
        List<string> lines = new List<string>
        {
            Defended ? "방어 결과: 방어 성공" : "방어 결과: 방어 실패",
            $"잔여 위험도: {ResidualRisk:0.#}"
        };

        AddContributionLine(lines, "가장 많은 피해를 준 시설", TopDamageContribution, true);
        AddContributionLine(lines, "가장 오래 지연시킨 시설", TopDelayContribution, false);

        lines.Add($"결정적 방어: {GetDecisiveDefenseText()}");
        lines.Add($"피해를 입은 시설: {FormatFacilities(damagedFacilities)}");
        lines.Add($"파손 시설: {FormatFacilities(BrokenFacilities)}");
        lines.Add($"획득한 관찰 정보: {FormatObservations()}");

        if (synergyLogs.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("작동한 연계:");
            foreach (string synergy in synergyLogs.Take(5))
            {
                lines.Add($"- {synergy}");
            }
        }

        if (activationLogs.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("전투 중 발동:");
            foreach (string activation in activationLogs.Skip(Mathf.Max(0, activationLogs.Count - 8)))
            {
                lines.Add($"- {activation}");
            }
        }

        return string.Join("\n", lines);
    }

    private DefenseContribution FindOrCreateContribution(DefenseActivationSnapshot report)
    {
        DefenseContribution contribution = defenseContributions.FirstOrDefault((item) =>
            item.FacilityRuntimeId == report.FacilityRuntimeId);
        if (contribution != null)
        {
            return contribution;
        }

        contribution = new DefenseContribution(report);
        defenseContributions.Add(contribution);
        return contribution;
    }

    private void AddObservation(string observation)
    {
        if (!string.IsNullOrWhiteSpace(observation) && !observations.Contains(observation))
        {
            observations.Add(observation);
        }
    }

    private string GetDecisiveDefenseText()
    {
        if (FinalCombatStarted)
        {
            string ownerName = FinalCombatOwner != null ? FinalCombatOwner.name : "사장";
            return Defended ? $"{ownerName} 최종 교전 생존" : $"{ownerName} 최종 교전 패배";
        }

        DefenseContribution topDamage = TopDamageContribution;
        if (Defended && topDamage != null)
        {
            return $"{topDamage.FacilityName} 피해 {topDamage.TotalDamage:0.#}";
        }

        DefenseContribution topDelay = TopDelayContribution;
        if (topDelay != null)
        {
            return $"{topDelay.FacilityName} 지연 {topDelay.MaxDelaySeconds:0.#}초";
        }

        return Defended ? "침입자 제압" : "기록된 결정적 방어 없음";
    }

    private string FormatFacilities(IEnumerable<BuildableObject> facilities)
    {
        List<string> names = facilities?
            .Where((facility) => facility != null)
            .Select(InvasionCombatReportFormatter.GetBuildingName)
            .Distinct()
            .ToList()
            ?? new List<string>();

        return names.Count > 0 ? string.Join(", ", names) : "없음";
    }

    private string FormatObservations()
    {
        return observations.Count > 0 ? string.Join(", ", observations) : "없음";
    }

    private static void AddContributionLine(
        List<string> lines,
        string label,
        DefenseContribution contribution,
        bool damage)
    {
        if (contribution == null)
        {
            lines.Add($"{label}: 없음");
            return;
        }

        string value = damage
            ? $"{contribution.TotalDamage:0.#} 피해"
            : $"{contribution.MaxDelaySeconds:0.#}초 지연";
        lines.Add($"{label}: {contribution.FacilityName} ({value})");
    }
}

public sealed class InvasionFacilitySnapshot
{
    internal InvasionFacilitySnapshot(BuildableObject facility)
    {
        Building = facility != null ? facility.BuildingData : null;
        Name = InvasionCombatReportFormatter.GetBuildingName(facility);
        WasDamaged = facility != null && facility.IsDamaged;
    }

    public BuildingSO Building { get; }
    public string Name { get; }
    public bool WasDamaged { get; }
}

public sealed class DefenseContributionSnapshot
{
    internal DefenseContributionSnapshot(DefenseContribution source)
    {
        Facility = source?.Facility;
        FacilityName = source?.FacilityName ?? string.Empty;
        TotalDamage = source?.TotalDamage ?? 0f;
        MaxDelaySeconds = source?.MaxDelaySeconds ?? 0f;
        TriggerCount = source?.TriggerCount ?? 0;
        EffectTags = EventPayloadSnapshot.Copy(source?.EffectTags);
    }

    public BuildingSO Facility { get; }
    public string FacilityName { get; }
    public float TotalDamage { get; }
    public float MaxDelaySeconds { get; }
    public int TriggerCount { get; }
    public IReadOnlyList<string> EffectTags { get; }
}

public sealed class InvasionCombatReportSnapshot
{
    internal InvasionCombatReportSnapshot(
        InvasionThreatSnapshot threatSnapshot,
        float startedAt,
        string intruderName,
        string finalCombatOwnerName,
        bool finalCombatStarted,
        bool defended,
        float residualRisk,
        bool isResolved,
        IReadOnlyList<DefenseContributionSnapshot> defenseContributions,
        IReadOnlyList<InvasionFacilitySnapshot> damagedFacilities,
        IReadOnlyList<string> observations,
        IReadOnlyList<string> synergyLogs,
        IReadOnlyList<string> activationLogs,
        string detailText)
    {
        ThreatSnapshot = threatSnapshot;
        StartedAt = startedAt;
        IntruderName = intruderName ?? string.Empty;
        FinalCombatOwnerName = finalCombatOwnerName ?? string.Empty;
        FinalCombatStarted = finalCombatStarted;
        Defended = defended;
        ResidualRisk = Mathf.Max(0f, residualRisk);
        IsResolved = isResolved;
        DefenseContributions = EventPayloadSnapshot.Copy(defenseContributions);
        DamagedFacilities = EventPayloadSnapshot.Copy(damagedFacilities);
        BrokenFacilities = EventPayloadSnapshot.Copy(
            damagedFacilities?
                .Where((facility) => facility != null && facility.WasDamaged)
                .ToArray());
        Observations = EventPayloadSnapshot.Copy(observations);
        SynergyLogs = EventPayloadSnapshot.Copy(synergyLogs);
        ActivationLogs = EventPayloadSnapshot.Copy(activationLogs);
        this.detailText = detailText ?? string.Empty;

        TopDamageContribution = DefenseContributions
            .Where((item) => item != null && item.TotalDamage > 0f)
            .OrderByDescending((item) => item.TotalDamage)
            .FirstOrDefault();
        TopDelayContribution = DefenseContributions
            .Where((item) => item != null && item.MaxDelaySeconds > 0f)
            .OrderByDescending((item) => item.MaxDelaySeconds)
            .FirstOrDefault();
    }

    private readonly string detailText;

    public InvasionThreatSnapshot ThreatSnapshot { get; }
    public float StartedAt { get; }
    public string IntruderName { get; }
    public string FinalCombatOwnerName { get; }
    public bool FinalCombatStarted { get; }
    public bool Defended { get; }
    public float ResidualRisk { get; }
    public bool IsResolved { get; }
    public IReadOnlyList<DefenseContributionSnapshot> DefenseContributions { get; }
    public IReadOnlyList<InvasionFacilitySnapshot> DamagedFacilities { get; }
    public IReadOnlyList<InvasionFacilitySnapshot> BrokenFacilities { get; }
    public IReadOnlyList<string> Observations { get; }
    public IReadOnlyList<string> SynergyLogs { get; }
    public IReadOnlyList<string> ActivationLogs { get; }
    public DefenseContributionSnapshot TopDamageContribution { get; }
    public DefenseContributionSnapshot TopDelayContribution { get; }

    public string ToDetailText()
    {
        return detailText;
    }
}

internal sealed class DefenseContribution
{
    private readonly List<string> effectTags = new List<string>();
    private readonly IReadOnlyList<string> effectTagsView;

    public DefenseContribution(DefenseActivationSnapshot report)
    {
        effectTagsView = effectTags.AsReadOnly();
        Facility = report?.Facility;
        FacilityRuntimeId = report?.FacilityRuntimeId ?? 0;
        FacilityName = report?.FacilityName ?? string.Empty;
    }

    public BuildingSO Facility { get; }
    public int FacilityRuntimeId { get; }
    public float TotalDamage { get; private set; }
    public float MaxDelaySeconds { get; private set; }
    public int TriggerCount { get; private set; }
    public IReadOnlyList<string> EffectTags => effectTagsView;
    public string FacilityName { get; }

    public void Add(DefenseActivationSnapshot report)
    {
        if (report == null)
        {
            return;
        }

        TriggerCount++;
        TotalDamage += Mathf.Max(0f, report.TotalDamage);
        MaxDelaySeconds = Mathf.Max(MaxDelaySeconds, report.MovementDelaySeconds);

        foreach (string tag in report.EffectTags)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !effectTags.Contains(tag))
            {
                effectTags.Add(tag);
            }
        }
    }
}

public static class InvasionCombatReportFormatter
{
    public static string FormatActivation(DefenseActivationSnapshot report)
    {
        if (report == null || report.Facility == null)
        {
            return string.Empty;
        }

        List<string> parts = new List<string>
        {
            $"{report.FacilityName} 발동"
        };

        if (report.TotalDamage > 0f)
        {
            parts.Add($"피해 {report.TotalDamage:0.#}");
        }

        if (report.MovementDelaySeconds > 0f)
        {
            parts.Add($"지연 {report.MovementDelaySeconds:0.#}초");
        }

        if (report.EffectTags.Count > 0)
        {
            parts.Add(string.Join(", ", report.EffectTags));
        }

        return string.Join(" / ", parts);
    }

    public static string FormatObservation(string effectTag)
    {
        if (string.IsNullOrWhiteSpace(effectTag))
        {
            return string.Empty;
        }

        string normalized = effectTag.Trim();
        if (normalized.Contains("축전 방전"))
        {
            return "관찰: 축전 방전";
        }

        if (normalized.Contains("축전"))
        {
            return "관찰: 축전 누적";
        }

        if (normalized.Contains("감속") || normalized.Contains("속박"))
        {
            return "관찰: 감속 적용";
        }

        if (normalized.Contains("부식"))
        {
            return "관찰: 부식 적용";
        }

        if (normalized.Contains("연소"))
        {
            return "관찰: 연소 적용";
        }

        if (normalized.Contains("경비"))
        {
            return "관찰: 경비 교전";
        }

        if (normalized.Contains("피해") || normalized.Contains("가시"))
        {
            return "관찰: 직접 피해";
        }

        return $"관찰: {normalized}";
    }

    public static string FormatSynergy(DefenseActivationSnapshot report)
    {
        if (report == null)
        {
            return string.Empty;
        }

        string facilityName = report.FacilityName;
        IReadOnlyList<string> tags = report.EffectTags;
        if (report.Concept == DefenseAttackConcept.Guard || tags.Any((tag) => tag.Contains("경비")))
        {
            return $"{facilityName}: 직원/경비 교전";
        }

        if (tags.Any((tag) => tag.Contains("축전 방전")))
        {
            return $"{facilityName}: 번개 축전 방전";
        }

        if (tags.Any((tag) => tag.Contains("부식")) && report.TotalDamage > 0f)
        {
            return $"{facilityName}: 독 피해와 부식";
        }

        if (tags.Any((tag) => tag.Contains("연소")) && report.TotalDamage > 0f)
        {
            return $"{facilityName}: 화염 피해와 연소";
        }

        if (report.MovementDelaySeconds > 0f)
        {
            return $"{facilityName}: 냉기 피해와 이동 지연";
        }

        if (report.TotalDamage > 0f)
        {
            return $"{facilityName}: 직접 피해";
        }

        return string.Empty;
    }

    public static string GetBuildingName(BuildableObject building)
    {
        if (building == null)
        {
            return "시설";
        }

        if (building.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName))
        {
            return building.BuildingData.objectName;
        }

        return string.IsNullOrWhiteSpace(building.name) ? "시설" : building.name;
    }
}
