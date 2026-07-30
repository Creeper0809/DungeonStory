using System;
using UnityEngine;

public sealed class SurgeryRiskEvaluator : ISurgeryRiskEvaluator
{
    public SurgeryRiskBreakdown Evaluate(
        CharacterActor doctor,
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        SurgicalFacilitySnapshot facility,
        float patientInstability,
        float compatibilityPenalty)
    {
        if (procedure == null)
        {
            return new SurgeryRiskBreakdown
            {
                successChance = 0.05f,
                deathChance = 0.1f,
                summary = "수술 절차가 없습니다."
            };
        }

        float medical = GetStat(doctor, CharacterStatType.Medical);
        float dexterity = GetStat(doctor, CharacterStatType.Dexterity);
        float research = GetStat(doctor, CharacterStatType.Research);
        const float maximumSkillContribution = 0.45f;
        float medicalContribution =
            Mathf.Clamp01(medical / 30f) * 0.65f * maximumSkillContribution;
        float dexterityContribution =
            Mathf.Clamp01(dexterity / 30f) * 0.25f * maximumSkillContribution;
        float researchContribution =
            Mathf.Clamp01(research / 30f) * 0.10f * maximumSkillContribution;
        float facilityContribution = facility.SuccessBonus;
        float cleanlinessContribution = facility.Sterility * 0.08f;
        float medicineContribution = procedure.Materials.Count > 0 ? 0.04f : 0f;
        float anesthesiaContribution = procedure.RequiresAnesthesia
            ? facility.AnesthesiaBonus * 0.03f
            : 0.03f;
        float instability = Mathf.Clamp01(patientInstability) * 0.3f;
        float compatibility = Mathf.Clamp01(compatibilityPenalty);
        float success = Mathf.Clamp(
            0.3f
            + medicalContribution
            + dexterityContribution
            + researchContribution
            + facilityContribution
            + cleanlinessContribution
            + medicineContribution
            + anesthesiaContribution
            - procedure.DifficultyPenalty
            - instability
            - compatibility,
            0.05f,
            0.98f);
        float failure = 1f - success;
        float infection = Mathf.Clamp01(
            procedure.BaseInfectionRisk
            + failure * 0.3f
            + compatibility * 0.25f
            - facility.Sterility * 0.45f);
        float bleeding = Mathf.Clamp01(
            procedure.BaseBleedingRisk
            + failure * 0.25f
            + instability * 0.4f
            - facility.AnesthesiaBonus * 0.1f);
        float organDamage = failure * 0.3f;
        float death = failure * 0.1f;
        return new SurgeryRiskBreakdown
        {
            successChance = success,
            infectionChance = infection,
            bleedingChance = bleeding,
            organDamageChance = organDamage,
            deathChance = death,
            medicalContribution = medicalContribution,
            dexterityContribution = dexterityContribution,
            researchContribution = researchContribution,
            facilityContribution = facilityContribution,
            cleanlinessContribution = cleanlinessContribution,
            medicineContribution = medicineContribution,
            anesthesiaContribution = anesthesiaContribution,
            difficultyPenalty = procedure.DifficultyPenalty,
            instabilityPenalty = instability,
            compatibilityPenalty = compatibility,
            summary =
                $"성공 {success * 100f:0.#}% · 감염 {infection * 100f:0.#}% · "
                + $"출혈 {bleeding * 100f:0.#}% · 장기 손상 {organDamage * 100f:0.#}% · "
                + $"사망 {death * 100f:0.#}%"
        };
    }

    private static int GetStat(CharacterActor actor, CharacterStatType stat)
    {
        return actor?.Progression != null
            ? actor.Progression.GetFinalStat(stat)
            : actor?.Identity?.Profile?.GetStat(stat) ?? 5;
    }
}
