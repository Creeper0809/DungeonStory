using System;
using UnityEngine;

[Serializable]
public sealed class NarrativeFormulaDrawbackCreditPolicyDefinition
{
    [Range(0f, 1f)] public float playerChoiceMaximumBudgetFraction = 0.25f;
    [Range(0f, 1f)] public float automaticMaximumBudgetFraction = 0.10f;
    [Min(0)] public int absoluteMaximumCredit = 3;
    public bool requireNegativeEvidenceForAutomatic = true;

    public NarrativeFormulaDrawbackCreditPolicy ToRuntime() => new(
        playerChoiceMaximumBudgetFraction,
        automaticMaximumBudgetFraction,
        absoluteMaximumCredit,
        requireNegativeEvidenceForAutomatic);
}
