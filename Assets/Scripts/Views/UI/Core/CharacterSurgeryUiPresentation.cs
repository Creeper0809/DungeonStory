using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterSurgeryNodeProjection
{
    public string DisplayName { get; set; } = string.Empty;
    public bool Missing { get; set; }
    public SurgicalPartKind InstalledPartKind { get; set; }
    public bool HasInstalledPart { get; set; }
    public float EffectiveEfficiency { get; set; }
    public float CurrentHealth { get; set; }
    public float MaxHealth { get; set; }
    public float BleedingPerSecond { get; set; }
    public float Infection { get; set; }
    public float RejectionBurden { get; set; }
    public float MutationBurden { get; set; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class SurgeryOrderUiProjection
{
    public string OrderId { get; set; } = string.Empty;
    public string ProcedureName { get; set; } = string.Empty;
    public string DoctorId { get; set; } = string.Empty;
    public SurgeryOrderState State { get; set; }
    public SurgeryOrderState EnvironmentResumeStage { get; set; }
    public float EnvironmentStableSeconds { get; set; }
    public float Progress01 { get; set; }
    public SurgeryStatusData Status { get; set; } = new();
    public SurgeryStatusData EnvironmentWait { get; set; } = new();
    public SurgeryStatusData EnvironmentRecovery { get; set; } = new();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterSurgeryHealthProjection
{
    public DomainFailure Failure { get; set; } = DomainFailure.None;
    public string ProfileDisplayName { get; set; } = string.Empty;
    public float Consciousness { get; set; }
    public float Sight { get; set; }
    public float Breathing { get; set; }
    public float Digestion { get; set; }
    public float Filtration { get; set; }
    public float Manipulation { get; set; }
    public float Mobility { get; set; }
    public IReadOnlyList<CharacterSurgeryNodeProjection> Nodes { get; set; } =
        Array.Empty<CharacterSurgeryNodeProjection>();
    public SurgeryOrderUiProjection ActiveOrder { get; set; }

    public bool IsAvailable => !Failure.IsFailure;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CharacterSurgeryUiText
{
    public static string FormatFacilityTags(SurgeryFacilityTag tags)
    {
        List<string> labels = new List<string>();
        Add(SurgeryFacilityTag.Emergency, "응급");
        Add(SurgeryFacilityTag.Anatomy, "해부");
        Add(SurgeryFacilityTag.GeneralSurgery, "외과");
        Add(SurgeryFacilityTag.Sterilization, "세정");
        Add(SurgeryFacilityTag.Anesthesia, "마취");
        Add(SurgeryFacilityTag.Transplant, "순환 이식");
        Add(SurgeryFacilityTag.ImmuneControl, "면역 조절");
        Add(SurgeryFacilityTag.IsolationRecovery, "격리 회복");
        Add(SurgeryFacilityTag.ArcaneSurgery, "비전 개조");
        Add(SurgeryFacilityTag.RuneSuture, "룬 봉합");
        Add(SurgeryFacilityTag.Rehabilitation, "재활");
        Add(SurgeryFacilityTag.OrganStorage, "장기 보관");
        Add(SurgeryFacilityTag.ProstheticAssembly, "보철 조립");
        return string.Join(", ", labels);

        void Add(SurgeryFacilityTag tag, string label)
        {
            if ((tags & tag) != 0)
            {
                labels.Add(label);
            }
        }
    }

    public static string FormatEnvironmentRisk(
        SurgeryEnvironmentRiskSnapshot risk)
    {
        if (!risk.Extreme)
        {
            return string.Empty;
        }

        return LocalizeStatus(new SurgeryStatusData
        {
            code = SurgeryStatusCode.EnvironmentUnsafe,
            scalarValue = risk.Environment.TemperatureC,
            secondaryScalarValue = risk.Environment.AirQuality,
            tertiaryScalarValue = risk.Environment.LightLevel
        });
    }

    public static void AppendHealthSummary(
        StringBuilder builder,
        CharacterSurgeryHealthProjection projection)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (projection == null)
        {
            builder.AppendLine(LocalizeFailure(new DomainFailure(
                FailureCode.SurgerySubjectInvalid)));
            return;
        }

        if (!projection.IsAvailable)
        {
            builder.AppendLine(LocalizeFailure(projection.Failure));
            return;
        }

        builder.AppendLine();
        builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
            "CharacterSummary.Health.Anatomy.Title",
            projection.ProfileDisplayName));
        builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
            "CharacterSummary.Health.Anatomy.PrimaryFunctions",
            Percent(projection.Consciousness),
            Percent(projection.Sight),
            Percent(projection.Breathing)));
        builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
            "CharacterSummary.Health.Anatomy.SecondaryFunctions",
            Percent(projection.Digestion),
            Percent(projection.Filtration),
            Percent(projection.Manipulation),
            Percent(projection.Mobility)));

        foreach (CharacterSurgeryNodeProjection node in
                 projection.Nodes ?? Array.Empty<CharacterSurgeryNodeProjection>())
        {
            if (node == null)
            {
                continue;
            }

            builder.Append("- ");
            builder.Append(node.DisplayName);
            builder.Append(": ");
            if (node.Missing)
            {
                builder.Append(CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Anatomy.Missing"));
            }
            else if (node.HasInstalledPart)
            {
                builder.Append(CharacterSummaryHealthStatusTextFormatter.PartKind(
                    node.InstalledPartKind));
                builder.Append(' ');
                builder.Append(Percent(node.EffectiveEfficiency));
            }
            else
            {
                builder.Append($"{node.CurrentHealth:0.#}/{node.MaxHealth:0.#}");
            }

            if (node.BleedingPerSecond > 0.001f)
            {
                builder.Append(CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Anatomy.BleedingSuffix",
                    node.BleedingPerSecond));
            }
            if (node.Infection > 0.1f)
            {
                builder.Append(CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Anatomy.InfectionSuffix",
                    node.Infection));
            }
            if (node.RejectionBurden > 0.1f)
            {
                builder.Append(CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Anatomy.RejectionSuffix",
                    node.RejectionBurden));
            }
            if (node.MutationBurden > 0.1f)
            {
                builder.Append(CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Anatomy.MutationSuffix",
                    node.MutationBurden));
            }
            builder.AppendLine();
        }

        builder.AppendLine();
        if (projection.ActiveOrder == null)
        {
            builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Health.Treatment.NoQueue"));
            return;
        }

        SurgeryOrderUiProjection order = projection.ActiveOrder;
        builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
            "CharacterSummary.Health.Treatment.Queue",
            order.ProcedureName,
            FormatOrderStatus(order),
            Percent(order.Progress01)));
        if (!string.IsNullOrWhiteSpace(order.DoctorId))
        {
            builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Health.Treatment.Doctor",
                order.DoctorId));
        }
    }

    public static string FormatScheduleResult(
        SurgeryUiCommandResult result)
    {
        return result.Succeeded
            ? "수술을 예약했습니다. 환자 입실과 재료 운반을 시작합니다."
            : LocalizeFailure(result.Failure);
    }

    public static string FormatCancelResult(
        SurgeryUiCommandResult result)
    {
        return result.Succeeded
            ? "수술 예약을 취소했습니다."
            : LocalizeFailure(result.Failure);
    }

    public static string FormatOrderStatus(SurgeryOrderUiProjection order)
    {
        if (order == null)
        {
            return LocalizeFailure(new DomainFailure(
                FailureCode.SurgeryOrderMissing));
        }

        SurgeryStatusData status = order.State == SurgeryOrderState.EnvironmentWaiting
            ? order.EnvironmentWait
            : order.Status;
        if (status == null || status.code == SurgeryStatusCode.None)
        {
            status = new SurgeryStatusData
            {
                code = MapState(order.State),
                scalarValue = order.EnvironmentStableSeconds,
                stage = order.EnvironmentResumeStage
            };
        }

        return LocalizeStatus(status);
    }

    public static string FormatEnvironmentWait(SurgeryOrderUiProjection order)
    {
        return order?.State == SurgeryOrderState.EnvironmentWaiting
            ? LocalizeStatus(order.EnvironmentWait)
            : string.Empty;
    }

    public static string LocalizeFailure(DomainFailure failure)
    {
        if (!failure.IsFailure)
        {
            return string.Empty;
        }

        object[] arguments = failure.Parameters
            .ToArray()
            .Cast<object>()
            .ToArray();
        return LocalizeKey(failure.Code.ToString(), arguments);
    }

    public static string LocalizeStatus(SurgeryStatusData status)
    {
        if (status == null || status.code == SurgeryStatusCode.None)
        {
            return string.Empty;
        }

        object[] arguments =
        {
            status.primaryId ?? string.Empty,
            status.secondaryId ?? string.Empty,
            status.scalarValue,
            status.secondaryScalarValue,
            status.tertiaryScalarValue,
            status.countValue,
            LocalizeKey(MapState(status.stage).ToString(), Array.Empty<object>())
        };
        return LocalizeKey(status.code.ToString(), arguments);
    }

    public static string LocalizeRisk(SurgeryRiskBreakdown risk)
    {
        if (risk == null || risk.summaryCode == SurgeryRiskSummaryCode.None)
        {
            return string.Empty;
        }

        object[] arguments =
        {
            risk.successChance,
            risk.infectionChance,
            risk.bleedingChance,
            risk.organDamageChance,
            risk.deathChance,
            risk.environmentSuccessPenalty,
            risk.environmentInfectionPenalty,
            risk.environmentBleedingPenalty,
            risk.environmentOrganDamagePenalty,
            risk.environmentStagesEvaluated
        };
        return LocalizeKey(risk.summaryCode.ToString(), arguments);
    }

    private static string LocalizeKey(string key, object[] arguments)
    {
        LocalizationSettings.InitializationOperation.WaitForCompletion();
        string template = new LocalizedString(
                DomainFailureLocalizer.TableName,
                key)
            .GetLocalizedString();
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException(
                $"Missing surgery presentation entry '{key}'.");
        }

        return arguments == null || arguments.Length == 0
            ? template
            : string.Format(
                CultureInfo.CurrentCulture,
                template,
                arguments);
    }

    private static SurgeryStatusCode MapState(SurgeryOrderState state)
    {
        return state switch
        {
            SurgeryOrderState.PatientWaiting =>
                SurgeryStatusCode.PatientAdmissionWaiting,
            SurgeryOrderState.MaterialsWaiting =>
                SurgeryStatusCode.MaterialsDeliveryPending,
            SurgeryOrderState.Anesthetizing =>
                SurgeryStatusCode.AnesthesiaInProgress,
            SurgeryOrderState.Incision => SurgeryStatusCode.IncisionInProgress,
            SurgeryOrderState.Procedure => SurgeryStatusCode.ProcedureInProgress,
            SurgeryOrderState.Suturing => SurgeryStatusCode.SuturingInProgress,
            SurgeryOrderState.Recovering => SurgeryStatusCode.RecoveryObservation,
            SurgeryOrderState.Completed => SurgeryStatusCode.Completed,
            SurgeryOrderState.Failed =>
                SurgeryStatusCode.CompletedWithMajorFailure,
            SurgeryOrderState.Cancelled => SurgeryStatusCode.Cancelled,
            SurgeryOrderState.EnvironmentWaiting =>
                SurgeryStatusCode.EnvironmentUnsafe,
            _ => SurgeryStatusCode.None
        };
    }

    private static string Percent(float value) => $"{value * 100f:0}%";
}
