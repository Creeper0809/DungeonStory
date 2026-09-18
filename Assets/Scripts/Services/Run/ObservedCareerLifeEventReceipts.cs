using System;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using VContainer.Unity;

public enum CharacterProficiencyAwardKind
{
    ApprovedWork = 1,
    DirectExperience = 2
}

public readonly struct CharacterProficiencyAwardCommitReceipt
{
    public CharacterProficiencyAwardCommitReceipt(
        CharacterProficiencyAwardKind awardKind,
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        long beforeCurrentMilliExperience,
        long afterCurrentMilliExperience,
        long beforeLifetimeMilliExperience,
        long afterLifetimeMilliExperience,
        long absoluteHour)
    {
        AwardKind = awardKind;
        CharacterId = characterId;
        ProficiencyId = proficiencyId;
        BeforeCurrentMilliExperience = beforeCurrentMilliExperience;
        AfterCurrentMilliExperience = afterCurrentMilliExperience;
        BeforeLifetimeMilliExperience = beforeLifetimeMilliExperience;
        AfterLifetimeMilliExperience = afterLifetimeMilliExperience;
        AbsoluteHour = absoluteHour;
        BeforeRank = ResolveCareerRank(beforeCurrentMilliExperience);
        AfterRank = ResolveCareerRank(afterCurrentMilliExperience);
        if (!Enum.IsDefined(typeof(CharacterProficiencyAwardKind), AwardKind)
            || !CharacterId.IsValid
            || !ProficiencyId.IsValid
            || BeforeCurrentMilliExperience < 0L
            || AfterCurrentMilliExperience <= BeforeCurrentMilliExperience
            || BeforeLifetimeMilliExperience < BeforeCurrentMilliExperience
            || AfterLifetimeMilliExperience < AfterCurrentMilliExperience
            || AfterLifetimeMilliExperience <= BeforeLifetimeMilliExperience
            || AbsoluteHour < 0L
            || (int)AfterRank <= (int)BeforeRank)
        {
            throw new ArgumentException(
                "Proficiency-promotion source receipt is invalid.");
        }

        SourceOperationId = string.Join(":", new[]
        {
            "proficiency-promotion",
            ((int)AwardKind).ToString(CultureInfo.InvariantCulture),
            Encode(CharacterId.Value),
            Encode(ProficiencyId.Value),
            BeforeCurrentMilliExperience.ToString(CultureInfo.InvariantCulture),
            AfterCurrentMilliExperience.ToString(CultureInfo.InvariantCulture),
            BeforeLifetimeMilliExperience.ToString(CultureInfo.InvariantCulture),
            AfterLifetimeMilliExperience.ToString(CultureInfo.InvariantCulture),
            AbsoluteHour.ToString(CultureInfo.InvariantCulture)
        });
    }

    public CharacterProficiencyAwardKind AwardKind { get; }
    public CharacterId CharacterId { get; }
    public CharacterProficiencyId ProficiencyId { get; }
    public long BeforeCurrentMilliExperience { get; }
    public long AfterCurrentMilliExperience { get; }
    public long BeforeLifetimeMilliExperience { get; }
    public long AfterLifetimeMilliExperience { get; }
    public CareerRank BeforeRank { get; }
    public CareerRank AfterRank { get; }
    public long AbsoluteHour { get; }
    public string SourceOperationId { get; }

    private static CareerRank ResolveCareerRank(long milliExperience) =>
        CareerRules.ResolveRank(checked((int)Math.Min(
            int.MaxValue,
            Math.Max(0L, milliExperience)
                / ProficiencyProgressionRules.MilliPerExperience)));

    private static string Encode(string value) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes(value ?? string.Empty));
}

public interface IObservedCareerLifeEventCommand
{
    bool TryCaptureProductionDeclaredLoss(
        ProductionDeclaredLossCycleReceipt source,
        out bool stateChanged,
        out string failureReason);

    bool TryCaptureLastLesson(
        CareerMentorshipSnapshot mentorship,
        CharacterCareerSnapshot retirement,
        CombatEquipmentInstance protectiveEquipment,
        CareerMentorshipAwardCommitReceipt award,
        int absoluteDay,
        out bool stateChanged,
        out string failureReason);

    bool TryCaptureQuietPromotion(
        CharacterProficiencyAwardCommitReceipt source,
        out bool stateChanged,
        out string failureReason);
}

internal readonly struct ObservedQuietPromotionLifeEventReceipt
{
    private const string PayloadVersion = "quiet-promotion@1";

    internal ObservedQuietPromotionLifeEventReceipt(
        CharacterProficiencyAwardCommitReceipt source,
        int absoluteDay,
        int generation)
    {
        int sourceAbsoluteDay = checked((int)Math.Min(
            int.MaxValue,
            source.AbsoluteHour / GameCalendarRules.HoursPerDay + 1L));
        if (absoluteDay != sourceAbsoluteDay || generation < 0)
            throw new ArgumentException(
                "Observed quiet-promotion day or generation is invalid.");

        SourceOperationId = source.SourceOperationId;
        AwardKind = source.AwardKind;
        CharacterId = source.CharacterId;
        ProficiencyId = source.ProficiencyId;
        BeforeCurrentMilliExperience = source.BeforeCurrentMilliExperience;
        AfterCurrentMilliExperience = source.AfterCurrentMilliExperience;
        BeforeLifetimeMilliExperience = source.BeforeLifetimeMilliExperience;
        AfterLifetimeMilliExperience = source.AfterLifetimeMilliExperience;
        BeforeRank = source.BeforeRank;
        AfterRank = source.AfterRank;
        AbsoluteHour = source.AbsoluteHour;
        AbsoluteDay = absoluteDay;
        Generation = generation;
    }

    internal string SourceOperationId { get; }
    internal CharacterProficiencyAwardKind AwardKind { get; }
    internal CharacterId CharacterId { get; }
    internal CharacterProficiencyId ProficiencyId { get; }
    internal long BeforeCurrentMilliExperience { get; }
    internal long AfterCurrentMilliExperience { get; }
    internal long BeforeLifetimeMilliExperience { get; }
    internal long AfterLifetimeMilliExperience { get; }
    internal CareerRank BeforeRank { get; }
    internal CareerRank AfterRank { get; }
    internal long AbsoluteHour { get; }
    internal int AbsoluteDay { get; }
    internal int Generation { get; }
    internal string CanonicalPayload => string.Join("|", new[]
    {
        PayloadVersion,
        Encode(SourceOperationId),
        ((int)AwardKind).ToString(CultureInfo.InvariantCulture),
        Encode(CharacterId.Value),
        Encode(ProficiencyId.Value),
        BeforeCurrentMilliExperience.ToString(CultureInfo.InvariantCulture),
        AfterCurrentMilliExperience.ToString(CultureInfo.InvariantCulture),
        BeforeLifetimeMilliExperience.ToString(CultureInfo.InvariantCulture),
        AfterLifetimeMilliExperience.ToString(CultureInfo.InvariantCulture),
        ((int)BeforeRank).ToString(CultureInfo.InvariantCulture),
        ((int)AfterRank).ToString(CultureInfo.InvariantCulture),
        AbsoluteHour.ToString(CultureInfo.InvariantCulture),
        AbsoluteDay.ToString(CultureInfo.InvariantCulture),
        Generation.ToString(CultureInfo.InvariantCulture)
    });

    internal static bool TryParse(
        string payload,
        out ObservedQuietPromotionLifeEventReceipt receipt)
    {
        receipt = default;
        string[] fields = (payload ?? string.Empty).Split('|');
        if (fields.Length != 14
            || !string.Equals(fields[0], PayloadVersion, StringComparison.Ordinal)
            || !TryDecode(fields[1], out string sourceOperationId)
            || !int.TryParse(fields[2], NumberStyles.None,
                CultureInfo.InvariantCulture, out int awardKind)
            || !TryDecode(fields[3], out string characterId)
            || !TryDecode(fields[4], out string proficiencyId)
            || !long.TryParse(fields[5], NumberStyles.None,
                CultureInfo.InvariantCulture, out long beforeCurrent)
            || !long.TryParse(fields[6], NumberStyles.None,
                CultureInfo.InvariantCulture, out long afterCurrent)
            || !long.TryParse(fields[7], NumberStyles.None,
                CultureInfo.InvariantCulture, out long beforeLifetime)
            || !long.TryParse(fields[8], NumberStyles.None,
                CultureInfo.InvariantCulture, out long afterLifetime)
            || !int.TryParse(fields[9], NumberStyles.None,
                CultureInfo.InvariantCulture, out int beforeRank)
            || !int.TryParse(fields[10], NumberStyles.None,
                CultureInfo.InvariantCulture, out int afterRank)
            || !long.TryParse(fields[11], NumberStyles.None,
                CultureInfo.InvariantCulture, out long absoluteHour)
            || !int.TryParse(fields[12], NumberStyles.None,
                CultureInfo.InvariantCulture, out int absoluteDay)
            || !int.TryParse(fields[13], NumberStyles.None,
                CultureInfo.InvariantCulture, out int generation))
        {
            return false;
        }

        try
        {
            CharacterProficiencyAwardCommitReceipt source = new(
                (CharacterProficiencyAwardKind)awardKind,
                new CharacterId(characterId),
                new CharacterProficiencyId(proficiencyId),
                beforeCurrent,
                afterCurrent,
                beforeLifetime,
                afterLifetime,
                absoluteHour);
            receipt = new ObservedQuietPromotionLifeEventReceipt(
                source,
                absoluteDay,
                generation);
            return source.BeforeRank == (CareerRank)beforeRank
                && source.AfterRank == (CareerRank)afterRank
                && string.Equals(
                    source.SourceOperationId,
                    sourceOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    receipt.CanonicalPayload,
                    payload,
                    StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or OverflowException
            or FormatException)
        {
            return false;
        }
    }

    private static string Encode(string value) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes(value ?? string.Empty));

    private static bool TryDecode(string value, out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            return string.Equals(Encode(decoded), value, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }
}

public sealed class ObservedProficiencyPromotionApplicationAdapter :
    IInitializable,
    IDisposable
{
    private readonly IGameEventBus events;
    private readonly IObservedCareerLifeEventCommand commands;
    private IDisposable subscription;

    public ObservedProficiencyPromotionApplicationAdapter(
        IGameEventBus events,
        IObservedCareerLifeEventCommand commands)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.commands = commands
            ?? throw new ArgumentNullException(nameof(commands));
    }

    public void Initialize()
    {
        if (subscription != null)
            throw new InvalidOperationException(
                "Proficiency-promotion observer is already initialized.");
        subscription = events.Subscribe<CharacterProficiencyAwardCommitReceipt>(
            OnPromotionCommitted);
    }

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void OnPromotionCommitted(
        CharacterProficiencyAwardCommitReceipt source)
    {
        if (!commands.TryCaptureQuietPromotion(
                source,
                out _,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Quiet-promotion observation failed after proficiency commit: "
                + failureReason);
        }
    }
}

internal readonly struct ObservedProductionLossLifeEventReceipt
{
    private const string PayloadVersion = "production-loss@1";

    internal ObservedProductionLossLifeEventReceipt(
        ProductionDeclaredLossCycleReceipt source,
        CareerMentorshipSnapshot mentorship,
        int absoluteDay,
        int generation)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (!mentorship.StudentCharacterId.IsValid
            || !mentorship.MentorCharacterId.IsValid
            || !mentorship.AcademyBuildingId.IsValid
            || !mentorship.ProficiencyId.IsValid
            || !string.Equals(
                mentorship.StudentCharacterId.Value,
                source.WorkerPersistentId,
                StringComparison.Ordinal)
            || source.DeclaredLossMassGrams <= 0L
            || absoluteDay < 1
            || generation < 0)
        {
            throw new ArgumentException(
                "Observed production-loss receipt is inconsistent with its active mentorship.");
        }

        SourceOperationId = RequireCanonical(
            source.SourceOperationId,
            "production-loss source operation");
        BillId = source.BillId;
        CycleSequence = source.CycleSequence;
        RecipeId = RequireCanonical(source.RecipeId, "production-loss recipe");
        FacilityId = source.FacilityId;
        StudentCharacterId = mentorship.StudentCharacterId;
        MentorCharacterId = mentorship.MentorCharacterId;
        AcademyBuildingId = mentorship.AcademyBuildingId;
        ProficiencyId = mentorship.ProficiencyId;
        BatchCommitId = RequireCanonical(
            source.BatchCommitId,
            "production-loss batch commit");
        OutcomeFingerprint = RequireDigest(source.OutcomeFingerprint);
        DeclaredLossMassGrams = source.DeclaredLossMassGrams;
        AbsoluteDay = absoluteDay;
        Generation = generation;
    }

    private ObservedProductionLossLifeEventReceipt(
        string sourceOperationId,
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        BuildingInstanceId facilityId,
        CharacterId studentCharacterId,
        CharacterId mentorCharacterId,
        BuildingInstanceId academyBuildingId,
        CharacterProficiencyId proficiencyId,
        string batchCommitId,
        string outcomeFingerprint,
        long declaredLossMassGrams,
        int absoluteDay,
        int generation)
    {
        SourceOperationId = RequireCanonical(
            sourceOperationId,
            "production-loss source operation");
        BillId = billId;
        CycleSequence = cycleSequence;
        RecipeId = RequireCanonical(recipeId, "production-loss recipe");
        FacilityId = facilityId;
        StudentCharacterId = studentCharacterId;
        MentorCharacterId = mentorCharacterId;
        AcademyBuildingId = academyBuildingId;
        ProficiencyId = proficiencyId;
        BatchCommitId = RequireCanonical(
            batchCommitId,
            "production-loss batch commit");
        OutcomeFingerprint = RequireDigest(outcomeFingerprint);
        DeclaredLossMassGrams = declaredLossMassGrams;
        AbsoluteDay = absoluteDay;
        Generation = generation;
        if (!BillId.IsValid
            || CycleSequence <= 0
            || !FacilityId.IsValid
            || !StudentCharacterId.IsValid
            || !MentorCharacterId.IsValid
            || !AcademyBuildingId.IsValid
            || !ProficiencyId.IsValid
            || DeclaredLossMassGrams <= 0L
            || AbsoluteDay < 1
            || Generation < 0
            || !string.Equals(
                BatchCommitId,
                ProductionPreparedOutputIdentity.BuildBatchCommitId(
                    BillId,
                    CycleSequence,
                    OutcomeFingerprint),
                StringComparison.Ordinal)
            || !string.Equals(
                SourceOperationId,
                "production-declared-loss:" + BatchCommitId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Observed production-loss receipt fields are invalid.");
        }
    }

    internal string SourceOperationId { get; }
    internal ProductionBillId BillId { get; }
    internal int CycleSequence { get; }
    internal string RecipeId { get; }
    internal BuildingInstanceId FacilityId { get; }
    internal CharacterId StudentCharacterId { get; }
    internal CharacterId MentorCharacterId { get; }
    internal BuildingInstanceId AcademyBuildingId { get; }
    internal CharacterProficiencyId ProficiencyId { get; }
    internal string BatchCommitId { get; }
    internal string OutcomeFingerprint { get; }
    internal long DeclaredLossMassGrams { get; }
    internal int AbsoluteDay { get; }
    internal int Generation { get; }
    internal string CanonicalPayload => string.Join("|", new[]
    {
        PayloadVersion,
        Encode(SourceOperationId),
        Encode(BillId.Value),
        CycleSequence.ToString(CultureInfo.InvariantCulture),
        Encode(RecipeId),
        Encode(FacilityId.Value),
        Encode(StudentCharacterId.Value),
        Encode(MentorCharacterId.Value),
        Encode(AcademyBuildingId.Value),
        Encode(ProficiencyId.Value),
        Encode(BatchCommitId),
        OutcomeFingerprint,
        DeclaredLossMassGrams.ToString(CultureInfo.InvariantCulture),
        AbsoluteDay.ToString(CultureInfo.InvariantCulture),
        Generation.ToString(CultureInfo.InvariantCulture)
    });

    internal static bool TryParse(
        string payload,
        out ObservedProductionLossLifeEventReceipt receipt)
    {
        receipt = default;
        string[] fields = (payload ?? string.Empty).Split('|');
        if (fields.Length != 15
            || !string.Equals(fields[0], PayloadVersion, StringComparison.Ordinal)
            || !TryDecode(fields[1], out string operationId)
            || !TryDecode(fields[2], out string billId)
            || !int.TryParse(fields[3], NumberStyles.None,
                CultureInfo.InvariantCulture, out int cycleSequence)
            || !TryDecode(fields[4], out string recipeId)
            || !TryDecode(fields[5], out string facilityId)
            || !TryDecode(fields[6], out string studentId)
            || !TryDecode(fields[7], out string mentorId)
            || !TryDecode(fields[8], out string academyId)
            || !TryDecode(fields[9], out string proficiencyId)
            || !TryDecode(fields[10], out string batchCommitId)
            || !long.TryParse(fields[12], NumberStyles.None,
                CultureInfo.InvariantCulture, out long declaredLoss)
            || !int.TryParse(fields[13], NumberStyles.None,
                CultureInfo.InvariantCulture, out int day)
            || !int.TryParse(fields[14], NumberStyles.None,
                CultureInfo.InvariantCulture, out int generation))
        {
            return false;
        }

        try
        {
            receipt = new ObservedProductionLossLifeEventReceipt(
                operationId,
                new ProductionBillId(billId),
                cycleSequence,
                recipeId,
                new BuildingInstanceId(facilityId),
                new CharacterId(studentId),
                new CharacterId(mentorId),
                new BuildingInstanceId(academyId),
                new CharacterProficiencyId(proficiencyId),
                batchCommitId,
                fields[11],
                declaredLoss,
                day,
                generation);
            return string.Equals(
                receipt.CanonicalPayload,
                payload,
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static string RequireCanonical(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException($"{label} ID must be canonical.");
        return value;
    }

    private static string RequireDigest(string value)
    {
        if (value == null
            || value.Length != 64
            || value.Any(character => !((character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f'))))
            throw new ArgumentException("Production-loss outcome digest is invalid.");
        return value;
    }

    private static string Encode(string value) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes(value ?? string.Empty));

    private static bool TryDecode(string value, out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            return string.Equals(Encode(decoded), value, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }
}

internal readonly struct ObservedLastLessonLifeEventReceipt
{
    private const string PayloadVersion = "last-lesson@1";

    internal ObservedLastLessonLifeEventReceipt(
        CareerMentorshipSnapshot mentorship,
        CharacterCareerSnapshot retirement,
        CombatEquipmentInstance protectiveEquipment,
        CareerMentorshipAwardCommitReceipt award,
        int absoluteDay,
        int generation)
    {
        if (!mentorship.MentorCharacterId.IsValid
            || !mentorship.StudentCharacterId.IsValid
            || !mentorship.AcademyBuildingId.IsValid
            || !mentorship.ProficiencyId.IsValid
            || !retirement.CharacterId.Equals(mentorship.MentorCharacterId)
            || retirement.Retired
            || retirement.RetirementScheduleStatus
                != RetirementScheduleStatus.Pending
            || string.IsNullOrWhiteSpace(retirement.RetirementEventId)
            || string.IsNullOrWhiteSpace(retirement.RetirementChoiceId)
            || retirement.RetirementDecisionAbsoluteDay < 1
            || retirement.RetirementDueAbsoluteDay
                < retirement.RetirementDecisionAbsoluteDay
            || retirement.RetirementTerminalAbsoluteDay != 0
            || protectiveEquipment == null
            || !string.Equals(
                protectiveEquipment.ownerCharacterId,
                mentorship.MentorCharacterId.Value,
                StringComparison.Ordinal)
            || protectiveEquipment.worldState
                != CombatEquipmentWorldState.Equipped
            || (CombatEquipmentRoleRules.For(protectiveEquipment.definitionId)
                & CombatEquipmentRoleFlags.BlastAndSmokeProtection) == 0
            || !award.IsValid
            || !award.AcademyBuildingId.Equals(
                mentorship.AcademyBuildingId)
            || absoluteDay < 1
            || generation < 0)
        {
            throw new ArgumentException(
                "Observed last-lesson receipt is inconsistent with its retirement, mentorship, PPE, or durable award.");
        }

        SourceOperationId = award.SourceOperationId;
        MentorCharacterId = mentorship.MentorCharacterId;
        StudentCharacterId = mentorship.StudentCharacterId;
        AcademyBuildingId = mentorship.AcademyBuildingId;
        ProficiencyId = mentorship.ProficiencyId;
        RetirementEventId = retirement.RetirementEventId;
        RetirementChoiceId = retirement.RetirementChoiceId;
        RetirementDecisionAbsoluteDay = retirement.RetirementDecisionAbsoluteDay;
        RetirementDueAbsoluteDay = retirement.RetirementDueAbsoluteDay;
        ProtectiveEquipmentInstanceId = RequireCanonical(
            protectiveEquipment.instanceId,
            "last-lesson protective-equipment instance");
        ProtectiveEquipmentDefinitionId = RequireCanonical(
            protectiveEquipment.definitionId,
            "last-lesson protective-equipment definition");
        AwardLedgerStackId = award.LedgerStackId;
        AwardBeforeContentRevision = award.BeforeContentRevision;
        AwardAfterContentRevision = award.AfterContentRevision;
        AbsoluteDay = absoluteDay;
        Generation = generation;
    }

    internal string SourceOperationId { get; }
    internal CharacterId MentorCharacterId { get; }
    internal CharacterId StudentCharacterId { get; }
    internal BuildingInstanceId AcademyBuildingId { get; }
    internal CharacterProficiencyId ProficiencyId { get; }
    internal string RetirementEventId { get; }
    internal string RetirementChoiceId { get; }
    internal int RetirementDecisionAbsoluteDay { get; }
    internal int RetirementDueAbsoluteDay { get; }
    internal string ProtectiveEquipmentInstanceId { get; }
    internal string ProtectiveEquipmentDefinitionId { get; }
    internal string AwardLedgerStackId { get; }
    internal long AwardBeforeContentRevision { get; }
    internal long AwardAfterContentRevision { get; }
    internal int AbsoluteDay { get; }
    internal int Generation { get; }
    internal string CanonicalPayload => string.Join("|", new[]
    {
        PayloadVersion,
        Encode(SourceOperationId),
        Encode(MentorCharacterId.Value),
        Encode(StudentCharacterId.Value),
        Encode(AcademyBuildingId.Value),
        Encode(ProficiencyId.Value),
        Encode(RetirementEventId),
        Encode(RetirementChoiceId),
        RetirementDecisionAbsoluteDay.ToString(CultureInfo.InvariantCulture),
        RetirementDueAbsoluteDay.ToString(CultureInfo.InvariantCulture),
        Encode(ProtectiveEquipmentInstanceId),
        Encode(ProtectiveEquipmentDefinitionId),
        Encode(AwardLedgerStackId),
        AwardBeforeContentRevision.ToString(CultureInfo.InvariantCulture),
        AwardAfterContentRevision.ToString(CultureInfo.InvariantCulture),
        AbsoluteDay.ToString(CultureInfo.InvariantCulture),
        Generation.ToString(CultureInfo.InvariantCulture)
    });

    internal static bool TryParse(
        string payload,
        out ObservedLastLessonLifeEventReceipt receipt)
    {
        receipt = default;
        string[] fields = (payload ?? string.Empty).Split('|');
        if (fields.Length != 17
            || !string.Equals(fields[0], PayloadVersion, StringComparison.Ordinal)
            || !TryDecode(fields[1], out string operationId)
            || !TryDecode(fields[2], out string mentorId)
            || !TryDecode(fields[3], out string studentId)
            || !TryDecode(fields[4], out string academyId)
            || !TryDecode(fields[5], out string proficiencyId)
            || !TryDecode(fields[6], out string retirementEventId)
            || !TryDecode(fields[7], out string retirementChoiceId)
            || !int.TryParse(fields[8], NumberStyles.None,
                CultureInfo.InvariantCulture, out int decisionDay)
            || !int.TryParse(fields[9], NumberStyles.None,
                CultureInfo.InvariantCulture, out int dueDay)
            || !TryDecode(fields[10], out string ppeInstanceId)
            || !TryDecode(fields[11], out string ppeDefinitionId)
            || !TryDecode(fields[12], out string ledgerStackId)
            || !long.TryParse(fields[13], NumberStyles.None,
                CultureInfo.InvariantCulture, out long beforeRevision)
            || !long.TryParse(fields[14], NumberStyles.None,
                CultureInfo.InvariantCulture, out long afterRevision)
            || !int.TryParse(fields[15], NumberStyles.None,
                CultureInfo.InvariantCulture, out int day)
            || !int.TryParse(fields[16], NumberStyles.None,
                CultureInfo.InvariantCulture, out int generation))
        {
            return false;
        }

        try
        {
            receipt = new ObservedLastLessonLifeEventReceipt(
                new CareerMentorshipSnapshot(
                    new CharacterId(mentorId),
                    new CharacterId(studentId),
                    new BuildingInstanceId(academyId),
                    new CharacterProficiencyId(proficiencyId),
                    0,
                    0,
                    0f,
                    0f),
                new CharacterCareerSnapshot(
                    new CharacterId(mentorId),
                    false,
                    default,
                    string.Empty,
                    0,
                    0f,
                    RetirementScheduleStatus.Pending,
                    retirementEventId,
                    retirementChoiceId,
                    decisionDay,
                    dueDay,
                    0),
                new CombatEquipmentInstance
                {
                    instanceId = ppeInstanceId,
                    definitionId = ppeDefinitionId,
                    ownerCharacterId = mentorId,
                    worldState = CombatEquipmentWorldState.Equipped
                },
                CareerMentorshipAwardCommitReceipt.Restore(
                    new BuildingInstanceId(academyId),
                    ledgerStackId,
                    beforeRevision,
                    afterRevision,
                    operationId),
                day,
                generation);
            return string.Equals(
                receipt.CanonicalPayload,
                payload,
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static string RequireCanonical(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException($"{label} ID must be canonical.");
        return value;
    }

    private static string Encode(string value) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes(value ?? string.Empty));

    private static bool TryDecode(string value, out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            return string.Equals(Encode(decoded), value, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }
}
