using System;
using UnityEngine;

public sealed class SurgeryRiskEvaluator : ISurgeryRiskEvaluator
{
    private readonly ICharacterPerformanceQuery performance;

    public SurgeryRiskEvaluator(ICharacterPerformanceQuery performance)
    {
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
    }

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
                summaryCode = SurgeryRiskSummaryCode.SurgeryRiskProcedureMissing
            };
        }

        const float maximumSkillContribution = 0.45f;
        float surgeryPerformance = 1f;
        float complicationRiskMultiplier = 1f;
        if (doctor != null)
        {
            CharacterPerformanceSnapshot snapshot = performance.Evaluate(
                doctor,
                CharacterPerformanceFormulaIds.SurgerySuccess);
            if (!snapshot.IsApplicable)
            {
                return new SurgeryRiskBreakdown
                {
                    successChance = 0f,
                    deathChance = 0.1f,
                    summaryCode = SurgeryRiskSummaryCode.SurgeryRiskEvaluated
                };
            }
            surgeryPerformance = snapshot.Value;
            CharacterPerformanceSnapshot complication = performance.Evaluate(
                doctor,
                CharacterPerformanceFormulaIds.ComplicationRisk);
            if (!complication.IsApplicable)
                return new SurgeryRiskBreakdown
                {
                    successChance = 0f,
                    deathChance = 0.1f,
                    summaryCode = SurgeryRiskSummaryCode.SurgeryRiskEvaluated
                };
            complicationRiskMultiplier = complication.Value;
        }
        float medicalContribution = Mathf.Clamp01(
            (surgeryPerformance - .70f) / .60f) * maximumSkillContribution;
        float dexterityContribution = 0f;
        float researchContribution = 0f;
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
        float infection = Mathf.Clamp01(complicationRiskMultiplier * (
            procedure.BaseInfectionRisk
            + failure * 0.3f
            + compatibility * 0.25f
            - facility.Sterility * 0.45f));
        float bleeding = Mathf.Clamp01(complicationRiskMultiplier * (
            procedure.BaseBleedingRisk
            + failure * 0.25f
            + instability * 0.4f
            - facility.AnesthesiaBonus * 0.1f));
        float organDamage = Mathf.Clamp01(
            failure * 0.3f * complicationRiskMultiplier);
        float death = Mathf.Clamp01(
            failure * 0.1f * complicationRiskMultiplier);
        CharacterPerformanceExecutionTrace.Record(
            CharacterPerformanceFormulaIds.ComplicationRisk,
            "SurgeryRiskEvaluator.Evaluate",
            failure,
            infection + bleeding + organDamage + death,
            doctor?.Identity?.PersistentId);
        return new SurgeryRiskBreakdown
        {
            successChance = success,
            infectionChance = infection,
            bleedingChance = bleeding,
            organDamageChance = organDamage,
            deathChance = death,
            complicationRiskMultiplier = complicationRiskMultiplier,
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
            summaryCode = SurgeryRiskSummaryCode.SurgeryRiskEvaluated
        };
    }

}
