using System;

// Immutable committed interrogation knowledge. No UI request, owner DTO or mutable child escapes.
public sealed class CaptivityInterrogationInformationResolvedFact
{
    public CaptivityInterrogationInformationResolvedFact(string captiveId,
        CaptivityInterrogationTerminalState terminal)
    {
        if (string.IsNullOrWhiteSpace(captiveId)) throw new ArgumentException("A captive identity is required.", nameof(captiveId));
        if (terminal == null || !terminal.HasOutcome || !terminal.codexPublicationCompleted)
            throw new ArgumentException("A frozen outcome with completed knowledge publication is required.", nameof(terminal));
        CaptiveId = captiveId; AttemptId = terminal.attemptId;
        SubjectDisplayName = terminal.subjectDisplayName; HasInformation = terminal.hasInformation;
        CodexEntryId = terminal.codexEntryId; CodexEntryTitle = terminal.codexEntryTitle;
        OriginEnemyArchetypeId = terminal.originEnemyArchetypeId; OriginFactionId = terminal.originFactionId;
        EnemyDisplayName = terminal.enemyDisplayName; FormationTag = terminal.formationTag;
        InformationText = terminal.informationText; HighFearCaution = terminal.highFearCaution;
    }
    public string CaptiveId { get; }
    public int AttemptId { get; }
    public string SubjectDisplayName { get; }
    public bool HasInformation { get; }
    public string CodexEntryId { get; }
    public string CodexEntryTitle { get; }
    public string OriginEnemyArchetypeId { get; }
    public string OriginFactionId { get; }
    public string EnemyDisplayName { get; }
    public string FormationTag { get; }
    public string InformationText { get; }
    public bool HighFearCaution { get; }
}
