using System;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class CareerMentorshipAwardFactData
{
    public string mentorId, studentId, academyId, proficiencyId, ledgerStackId, operationId;
    public long beforeContentRevision, afterContentRevision;
    public int absoluteDay;
    public CareerMentorshipAwardFactData Clone() => (CareerMentorshipAwardFactData)MemberwiseClone();
}

// A frozen receipt for an already-applied award, not a command to repeat its effects.
public sealed class CareerMentorshipAwardFact
{
    private readonly CareerMentorshipAwardFactData data;
    public CareerMentorshipAwardFact(CareerMentorshipAwardFactData source)
    {
        data = source?.Clone() ?? throw new ArgumentNullException(nameof(source));
        foreach (string value in new[] { data.mentorId, data.studentId, data.academyId,
                     data.proficiencyId, data.ledgerStackId, data.operationId })
            if (!DomainFactValueSyntax.IsText(value, 4096, false) || value != value.Trim())
                throw new InvalidOperationException("career-mentorship-fact-text-invalid");
        if (!new CharacterId(data.mentorId).IsValid || !new CharacterId(data.studentId).IsValid
            || data.mentorId == data.studentId || !new CharacterProficiencyId(data.proficiencyId).IsValid
            || data.absoluteDay < 1 || data.beforeContentRevision < 0
            || data.beforeContentRevision == long.MaxValue
            || data.afterContentRevision != data.beforeContentRevision + 1)
            throw new InvalidOperationException("career-mentorship-fact-invalid");
        CareerMentorshipAwardCommitReceipt.Restore(new BuildingInstanceId(data.academyId),
            data.ledgerStackId, data.beforeContentRevision, data.afterContentRevision, data.operationId);
    }
    internal CareerMentorshipAwardFact(in CareerMentorshipSnapshot mentorship,
        in CareerMentorshipAwardCommitReceipt receipt, int day) : this(new CareerMentorshipAwardFactData
    {
        mentorId = mentorship.MentorCharacterId.Value, studentId = mentorship.StudentCharacterId.Value,
        academyId = receipt.AcademyBuildingId.Value, proficiencyId = mentorship.ProficiencyId.Value,
        ledgerStackId = receipt.LedgerStackId, operationId = receipt.SourceOperationId,
        beforeContentRevision = receipt.BeforeContentRevision, afterContentRevision = receipt.AfterContentRevision,
        absoluteDay = day
    }) { }
    public CareerMentorshipAwardFactData Snapshot() => data.Clone();
}
