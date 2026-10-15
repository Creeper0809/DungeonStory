using System;
using DungeonStory.Foundation.Facts;

/// <summary>Immutable observation of an already applied disease burden, not a body mutation command.</summary>
public sealed class DiseaseAnatomyBurdenAppliedFact
{
    public DiseaseAnatomyBurdenAppliedFact(DiseaseAnatomyBurdenProjectionSaveData value)
    {
        if (value == null || value.phase != (int)DiseaseAnatomyBurdenProjectionPhase.AnatomyApplied
            || !DomainFactValueSyntax.IsIdentity(value.operationId)
            || !DomainFactValueSyntax.IsIdentity(value.characterId)
            || !DomainFactValueSyntax.IsIdentity(value.diseaseId)
            || !DomainFactValueSyntax.IsIdentity(value.nodeId)
            || !DomainFactValueSyntax.IsText(value.characterDisplayName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || !Enum.IsDefined(typeof(DiseaseTargetSystem), value.targetSystem)
            || value.absoluteDay <= 0 || !Nonnegative(value.severity)
            || !Nonnegative(value.requestedInfectionBurden) || value.requestedInfectionBurden == 0f
            || !Nonnegative(value.infectionBefore) || !Nonnegative(value.infectionAfter)
            || value.infectionAfter < value.infectionBefore)
            throw new ArgumentException("disease-body-burden-fact-invalid");
        OperationId = value.operationId; CharacterId = value.characterId; CharacterDisplayName = value.characterDisplayName;
        DiseaseId = value.diseaseId; TargetSystem = (DiseaseTargetSystem)value.targetSystem; NodeId = value.nodeId;
        Severity = value.severity; RequestedBurden = value.requestedInfectionBurden;
        InfectionBefore = value.infectionBefore; InfectionAfter = value.infectionAfter; AbsoluteDay = value.absoluteDay;
    }
    public string OperationId { get; }
    public string CharacterId { get; }
    public string CharacterDisplayName { get; }
    public string DiseaseId { get; }
    public DiseaseTargetSystem TargetSystem { get; }
    public string NodeId { get; }
    public float Severity { get; }
    public float RequestedBurden { get; }
    public float InfectionBefore { get; }
    public float InfectionAfter { get; }
    public int AbsoluteDay { get; }
    private static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}
