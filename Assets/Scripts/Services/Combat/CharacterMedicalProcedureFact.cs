using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

/// <summary>Clinical facts only. Presentation, record identity and importance belong to observers.</summary>
public sealed class CharacterMedicalProcedureFact
{
    private readonly CharacterMedicalProcedureFactData data;

    public CharacterMedicalProcedureFact(CharacterMedicalProcedureFactData value, bool proposalOnly = false)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        data = value.Clone();
        IsProposal = proposalOnly;
        if (!CharacterMedicalSaveValidation.TryParseOrderId(data.orderId, out _)
            || !CharacterMedicalSaveValidation.IsCharacterId(data.patientId)
            || !CharacterMedicalSaveValidation.IsCharacterId(data.clinicianId)
            || !DomainFactValueSyntax.IsText(data.patientName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || !DomainFactValueSyntax.IsText(data.clinicianName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || data.facilityId == null || data.facilityId.Length > 0 && !DomainFactValueSyntax.IsIdentity(data.facilityId)
            || data.sequence <= 0 || data.absoluteDay <= 0
            || !Enum.IsDefined(typeof(CharacterMedicalProcedureKind), data.procedure)
            || !FiniteNonnegative(data.completedWork) || !FiniteNonnegative(data.requiredWork)
            || data.completedWork > data.requiredWork || data.completedWork + 0.001f < data.requiredWork)
            throw new ArgumentException("medical-procedure-fact-header-invalid", nameof(value));
        if (proposalOnly) return;
        if (!Enum.IsDefined(typeof(CharacterMedicalSupplyKind), data.materialKind)
            || data.materialItemId == null
            || data.materialItemId.Length > 0 && !DomainFactValueSyntax.IsIdentity(data.materialItemId)
            || data.materialQuantity < 0 || data.materialQuantity > 1
            || data.materialKind == CharacterMedicalSupplyKind.None
                && (data.materialQuantity != 0 || data.materialItemId.Length != 0)
            || data.materialQuantity > 0 && data.materialItemId.Length == 0
            || data.procedure == CharacterMedicalProcedureKind.Stabilization
                && (data.materialKind != CharacterMedicalSupplyKind.None || data.materialItemId.Length != 0 || data.materialQuantity != 0))
            throw new ArgumentException("medical-procedure-fact-material-invalid", nameof(value));
        ValidateVitals(data.before);
        ValidateVitals(data.after);
    }

    public bool IsProposal { get; }
    public CharacterMedicalProcedureFactData Data => data.Clone();
    public CharacterMedicalProcedureFactData Snapshot() => !IsProposal ? data.Clone()
        : throw new InvalidOperationException("A medical proposal is not a completed procedure.");

    public static CharacterMedicalVitalsFactData CaptureVitals(CharacterBodyHealthState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        var value = new CharacterMedicalVitalsFactData
        { health = state.currentHealth, bloodLoss = state.bloodLoss, downed = state.downed };
        if (state.parts != null)
            foreach (var part in state.parts)
                if (part != null) value.parts.Add(new CharacterMedicalPartFactData
                { currentHealth = part.currentHealth, maxHealth = part.maxHealth, bleedingPerSecond = part.bleedingPerSecond });
        if (state.anatomyNodes != null)
            foreach (var node in state.anatomyNodes)
                if (node != null) value.anatomyNodes.Add(new CharacterMedicalAnatomyFactData
                {
                    nodeId = node.nodeId, currentHealth = node.currentHealth, maxHealth = node.maxHealth,
                    bleedingPerSecond = node.bleedingPerSecond, missing = node.missing
                });
        ValidateVitals(value);
        return value;
    }

    private static void ValidateVitals(CharacterMedicalVitalsFactData value)
    {
        if (value == null || !FiniteNonnegative(value.health) || !FiniteNonnegative(value.bloodLoss)
            || value.parts == null || value.anatomyNodes == null)
            throw new ArgumentException("medical-procedure-vitals-invalid");
        foreach (var part in value.parts)
            if (part == null || !FiniteNonnegative(part.currentHealth) || !FiniteNonnegative(part.maxHealth)
                || !FiniteNonnegative(part.bleedingPerSecond))
                throw new ArgumentException("medical-procedure-part-invalid");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in value.anatomyNodes)
            if (node == null || !DomainFactValueSyntax.IsIdentity(node.nodeId) || !ids.Add(node.nodeId)
                || !FiniteNonnegative(node.currentHealth) || !FiniteNonnegative(node.maxHealth)
                || !FiniteNonnegative(node.bleedingPerSecond))
                throw new ArgumentException("medical-procedure-anatomy-invalid");
    }

    private static bool FiniteNonnegative(float value) => float.IsFinite(value) && value >= 0f;
}
