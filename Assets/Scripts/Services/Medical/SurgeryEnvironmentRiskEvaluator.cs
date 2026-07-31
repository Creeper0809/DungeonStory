using System;
using UnityEngine;

public sealed class SurgeryEnvironmentRiskEvaluator :
    ISurgeryEnvironmentRiskEvaluator
{
    private readonly IEnvironmentalFieldRuntime field;
    private readonly ICharacterEnvironmentStatusQuery status;
    private readonly ICharacterWorldQuery characters;

    public SurgeryEnvironmentRiskEvaluator(
        IEnvironmentalFieldRuntime field,
        ICharacterEnvironmentStatusQuery status,
        ICharacterWorldQuery characters)
    {
        this.field = field ?? throw new ArgumentNullException(nameof(field));
        this.status = status ?? throw new ArgumentNullException(nameof(status));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
    }

    public SurgeryEnvironmentRiskSnapshot Evaluate(
        Vector2Int facilityPosition,
        CharacterActor doctor,
        SurgicalSubjectRef subject)
    {
        if (!field.TryGetCell(
                facilityPosition,
                out EnvironmentalCellSnapshot environment))
        {
            throw new InvalidOperationException(
                $"Surgery environment cell is unavailable at {facilityPosition}.");
        }

        float successPenalty = 0f;
        float infection = 0f;
        float bleeding = 0f;
        float organDamage = 0f;
        bool extremeTemperature = environment.TemperatureC < 8f
            || environment.TemperatureC > 35f;
        if (extremeTemperature)
        {
            successPenalty += 0.16f;
            bleeding += 0.10f;
        }
        else if (environment.TemperatureC < 16f
                 || environment.TemperatureC > 28f)
        {
            successPenalty += 0.08f;
            bleeding += 0.05f;
        }

        bool extremeAir = environment.AirQuality < 40f;
        if (extremeAir)
        {
            successPenalty += 0.12f;
            infection += 0.25f;
        }
        else if (environment.AirQuality < 70f)
        {
            successPenalty += 0.06f;
            infection += 0.12f;
        }

        bool extremeLight = environment.LightLevel < 40f;
        if (extremeLight)
        {
            successPenalty += 0.20f;
            organDamage += 0.18f;
        }
        else if (environment.LightLevel < 70f)
        {
            successPenalty += 0.10f;
            organDamage += 0.08f;
        }

        EnvironmentalExposureBand doctorBand =
            status.GetPhysiologicalBand(
                doctor?.Identity?.PersistentId);
        successPenalty += doctorBand switch
        {
            EnvironmentalExposureBand.Critical
                or EnvironmentalExposureBand.Collapse => 0.12f,
            EnvironmentalExposureBand.Impaired => 0.05f,
            _ => 0f
        };

        CharacterActor patient = SurgicalSubjectResolver.FindCharacter(
            characters,
            subject?.subjectId);
        EnvironmentalExposureBand patientBand =
            status.GetPhysiologicalBand(
                patient?.Identity?.PersistentId);
        float instability = patientBand switch
        {
            EnvironmentalExposureBand.Critical
                or EnvironmentalExposureBand.Collapse => 0.2f,
            EnvironmentalExposureBand.Impaired => 0.1f,
            _ => 0f
        };
        successPenalty += instability * 0.3f;

        bool normal = IsNormalEnvironment(environment);
        bool extreme = extremeTemperature || extremeAir || extremeLight;
        string summary =
            $"현재 환경: {environment.TemperatureC:0.#}°C, 공기 {environment.AirQuality:0.#}, "
            + $"조명 {environment.LightLevel:0.#}; 성공 -{successPenalty * 100f:0.#}%p, "
            + $"감염 +{infection * 100f:0.#}%p, 출혈 +{bleeding * 100f:0.#}%p, "
            + $"장기 손상 +{organDamage * 100f:0.#}%p";
        return new SurgeryEnvironmentRiskSnapshot(
            environment,
            doctorBand,
            patientBand,
            successPenalty,
            infection,
            bleeding,
            organDamage,
            instability,
            extreme,
            normal,
            summary);
    }

    public SurgeryRiskBreakdown Apply(
        SurgeryRiskBreakdown baseline,
        SurgeryEnvironmentRiskSnapshot snapshot,
        float stageWeight)
    {
        SurgeryRiskBreakdown result =
            baseline?.Clone() ?? new SurgeryRiskBreakdown();
        float weight = Mathf.Clamp01(stageWeight);
        result.environmentStagesEvaluated++;
        result.environmentSuccessPenalty +=
            snapshot.SuccessPenalty * weight;
        result.environmentInfectionPenalty +=
            snapshot.InfectionAdded * weight;
        result.environmentBleedingPenalty +=
            snapshot.BleedingAdded * weight;
        result.environmentOrganDamagePenalty +=
            snapshot.OrganDamageAdded * weight;
        result.environmentInstabilityAdded +=
            snapshot.InstabilityAdded * weight;
        result.successChance = Mathf.Clamp(
            result.successChance
                - snapshot.SuccessPenalty * weight,
            0.05f,
            0.98f);
        result.infectionChance = Mathf.Clamp01(
            result.infectionChance
                + snapshot.InfectionAdded * weight);
        result.bleedingChance = Mathf.Clamp01(
            result.bleedingChance
                + snapshot.BleedingAdded * weight);
        result.organDamageChance = Mathf.Clamp01(
            result.organDamageChance
                + snapshot.OrganDamageAdded * weight);
        result.summary =
            $"{result.summary} | 환경 누적: 성공 -{result.environmentSuccessPenalty * 100f:0.#}%p, "
            + $"감염 +{result.environmentInfectionPenalty * 100f:0.#}%p, "
            + $"출혈 +{result.environmentBleedingPenalty * 100f:0.#}%p, "
            + $"장기 손상 +{result.environmentOrganDamagePenalty * 100f:0.#}%p";
        return result;
    }

    public static bool IsNormalEnvironment(
        EnvironmentalCellSnapshot environment)
    {
        return environment.TemperatureC >= 16f
            && environment.TemperatureC <= 28f
            && environment.AirQuality >= 70f
            && environment.LightLevel >= 70f;
    }
}
