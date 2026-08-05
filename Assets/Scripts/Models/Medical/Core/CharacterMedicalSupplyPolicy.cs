using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(
    true,
    sourceNamespace: "",
    sourceAssembly: "Assembly-CSharp",
    sourceClassName: "CharacterMedicalSupplyKind")]
public enum CharacterMedicalSupplyKind
{
    None = 0,
    Medicine = 1,
    ExtractedBlood = 2
}

public readonly struct CharacterMedicalMedicineCandidate
{
    public CharacterMedicalMedicineCandidate(
        string itemId,
        int unitPrice,
        float treatmentPotency,
        float infectionReduction,
        float painReduction)
    {
        ItemId = itemId ?? string.Empty;
        UnitPrice = unitPrice;
        TreatmentPotency = treatmentPotency;
        InfectionReduction = infectionReduction;
        PainReduction = painReduction;
    }

    public string ItemId { get; }
    public int UnitPrice { get; }
    public float TreatmentPotency { get; }
    public float InfectionReduction { get; }
    public float PainReduction { get; }
}

public readonly struct CharacterMedicalSupplyState
{
    public CharacterMedicalSupplyState(
        CharacterMedicalSupplyKind kind,
        bool consumed,
        bool deliveryRequested,
        string itemId,
        float potency,
        float infectionReduction,
        float painReduction)
    {
        Kind = kind;
        Consumed = consumed;
        DeliveryRequested = deliveryRequested;
        ItemId = itemId ?? string.Empty;
        Potency = potency;
        InfectionReduction = infectionReduction;
        PainReduction = painReduction;
    }

    public CharacterMedicalSupplyKind Kind { get; }
    public bool Consumed { get; }
    public bool DeliveryRequested { get; }
    public string ItemId { get; }
    public float Potency { get; }
    public float InfectionReduction { get; }
    public float PainReduction { get; }
}

public static class CharacterMedicalSupplyPolicy
{
    public static IReadOnlyList<CharacterMedicalMedicineCandidate> RankMedicines(
        IReadOnlyList<CharacterMedicalMedicineCandidate> candidates,
        float requiredTreatmentWork)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return Array.Empty<CharacterMedicalMedicineCandidate>();
        }

        float desiredPotency = Mathf.Lerp(
            0.7f,
            1.35f,
            Mathf.InverseLerp(20f, 75f, requiredTreatmentWork));
        List<CharacterMedicalMedicineCandidate> ranked =
            new List<CharacterMedicalMedicineCandidate>(candidates);
        ranked.Sort((left, right) => Compare(left, right, desiredPotency));
        return ranked;
    }

    public static CharacterMedicalSupplyState CreateMedicine(
        CharacterMedicalMedicineCandidate medicine,
        bool consumed) =>
        new CharacterMedicalSupplyState(
            CharacterMedicalSupplyKind.Medicine,
            consumed,
            !consumed,
            medicine.ItemId,
            medicine.TreatmentPotency,
            medicine.InfectionReduction,
            medicine.PainReduction);

    public static CharacterMedicalSupplyState CreateExtractedBlood() =>
        new CharacterMedicalSupplyState(
            CharacterMedicalSupplyKind.ExtractedBlood,
            consumed: false,
            deliveryRequested: true,
            itemId: string.Empty,
            potency: 0.55f,
            infectionReduction: 0f,
            painReduction: 0f);

    public static CharacterMedicalSupplyState MarkConsumed(
        CharacterMedicalSupplyState state) =>
        new CharacterMedicalSupplyState(
            state.Kind,
            consumed: true,
            deliveryRequested: false,
            state.ItemId,
            state.Potency,
            state.InfectionReduction,
            state.PainReduction);

    private static int Compare(
        CharacterMedicalMedicineCandidate left,
        CharacterMedicalMedicineCandidate right,
        float desiredPotency)
    {
        int result = Mathf.Abs(left.TreatmentPotency - desiredPotency)
            .CompareTo(Mathf.Abs(right.TreatmentPotency - desiredPotency));
        if (result != 0)
        {
            return result;
        }

        result = left.UnitPrice.CompareTo(right.UnitPrice);
        return result != 0
            ? result
            : string.CompareOrdinal(left.ItemId, right.ItemId);
    }
}
