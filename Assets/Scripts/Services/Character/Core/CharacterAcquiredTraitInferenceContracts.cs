using System;
using System.Collections.Generic;
using System.Linq;

public enum CharacterAcquiredTraitInferenceIssueCode
{
    None,
    InvalidCommand,
    InvalidTimestamp,
    StaleRevision,
    InvalidSettings,
    InvalidModuleDefinition,
    UnknownModule,
    InvalidMilestone,
    MilestoneNotReached,
    MilestoneAlreadyProcessed,
    ActiveCapacityExceeded,
    ActiveModuleAlreadyPresent,
    ActiveConflict,
    DuplicateRequest,
    PendingRequestInFlight,
    OutOfOrderMilestone,
    DuplicateCallback,
    MissingPendingRequest,
    StaleCallback,
    InvalidEvidence,
    EvidenceForgery,
    DuplicateEvidence,
    NoEligibleDomain,
    DomainMismatch,
    InvalidCombination,
    UnknownCombination,
    DuplicateCombination,
    DuplicateModule,
    BudgetExceeded,
    Conflict,
    PacketMismatch,
    ResponseSchemaInvalid,
    MissingResponseKey,
    ExtraResponseKey,
    InvalidNarrative,
    StateValidationFailed,
    CommitRejected
}

public enum CharacterAcquiredTraitInferenceCommandKind
{
    Submission,
    Completion
}

[Serializable]
public sealed class CharacterAcquiredTraitModulePacketDto
{
    public string moduleId = string.Empty;
    public string displayName = string.Empty;
    public string description = string.Empty;
    public int cost;
    public List<string> domainAffinities = new();
    public List<string> conflictGroups = new();

    public CharacterAcquiredTraitModulePacketDto Clone() => new()
    {
        moduleId = moduleId,
        displayName = displayName,
        description = description,
        cost = cost,
        domainAffinities = (domainAffinities ?? new List<string>()).ToList(),
        conflictGroups = (conflictGroups ?? new List<string>()).ToList()
    };
}

[Serializable]
public sealed class CharacterAcquiredTraitCombinationPacketDto
{
    public string combinationId = string.Empty;
    public List<string> moduleIds = new();
    public int totalCost;
    public List<string> domainAffinities = new();
    public List<string> conflictGroups = new();

    public CharacterAcquiredTraitCombinationPacketDto Clone() => new()
    {
        combinationId = combinationId,
        moduleIds = (moduleIds ?? new List<string>()).ToList(),
        totalCost = totalCost,
        domainAffinities = (domainAffinities ?? new List<string>()).ToList(),
        conflictGroups = (conflictGroups ?? new List<string>()).ToList()
    };
}

[Serializable]
public sealed class CharacterAcquiredTraitRequestPacketDto
{
    public string settingsId = string.Empty;
    public string targetPersistentId = string.Empty;
    public string requestId = string.Empty;
    public string requestKey = string.Empty;
    public int manifestationMilestone;
    public string rarity = string.Empty;
    public int budget;
    public int maximumActiveTraits;
    public List<string> eligibleDomains = new();
    public List<CharacterAcquiredTraitModulePacketDto> modules = new();
    public List<CharacterAcquiredTraitCombinationPacketDto> combinationOptions = new();
    public List<string> evidenceFactIds = new();
    public string candidatePacketHash = string.Empty;

    public CharacterAcquiredTraitRequestPacketDto Clone() => new()
    {
        settingsId = settingsId,
        targetPersistentId = targetPersistentId,
        requestId = requestId,
        requestKey = requestKey,
        manifestationMilestone = manifestationMilestone,
        rarity = rarity,
        budget = budget,
        maximumActiveTraits = maximumActiveTraits,
        eligibleDomains = (eligibleDomains ?? new List<string>()).ToList(),
        modules = (modules ?? new List<CharacterAcquiredTraitModulePacketDto>())
            .Select(value => value?.Clone())
            .ToList(),
        combinationOptions = (combinationOptions
                ?? new List<CharacterAcquiredTraitCombinationPacketDto>())
            .Select(value => value?.Clone())
            .ToList(),
        evidenceFactIds = (evidenceFactIds ?? new List<string>()).ToList(),
        candidatePacketHash = candidatePacketHash
    };
}

[Serializable]
public sealed class CharacterAcquiredTraitResponseDto : ILlmJsonPayload
{
    public string combinationId = string.Empty;
    public string displayName = string.Empty;
    public string description = string.Empty;
    public string narrativeReason = string.Empty;
    public List<string> evidenceFactIds = new();

    public bool Validate(out string error)
    {
        if (!IsCanonicalLength(displayName, 40)
            || !IsCanonicalLength(description, 180)
            || !IsCanonicalLength(narrativeReason, 180))
        {
            error = "Acquired-trait narrative fields are missing, padded, or too long.";
            return false;
        }
        if (!IsCanonicalRequired(combinationId))
        {
            error = "Acquired-trait combinationId is missing or non-canonical.";
            return false;
        }
        if (evidenceFactIds == null || evidenceFactIds.Count == 0)
        {
            error = "Acquired-trait evidenceFactIds must contain at least one fact.";
            return false;
        }
        if (evidenceFactIds.Any(value => !IsCanonicalRequired(value)))
        {
            error = "Acquired-trait evidenceFactIds contain an empty or padded fact ID.";
            return false;
        }
        if (evidenceFactIds.Distinct(StringComparer.Ordinal).Count()
            != evidenceFactIds.Count)
        {
            error = "Acquired-trait evidenceFactIds contain a duplicate fact ID.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsCanonicalLength(string value, int maximumLength) =>
        IsCanonicalRequired(value) && value.Length <= maximumLength;

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class CharacterAcquiredTraitSubmissionCommand
{
    public CharacterAcquiredTraitSubmissionCommand(
        string targetPersistentId,
        string requestId,
        string requestKey,
        int manifestationMilestone,
        IEnumerable<string> evidenceFactIds,
        int expectedRevision,
        NarrativeInferenceTimestamp timestamp)
        : this(
            targetPersistentId,
            requestId,
            requestKey,
            manifestationMilestone,
            evidenceFactIds,
            Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>(),
            expectedRevision,
            timestamp)
    {
    }

    public CharacterAcquiredTraitSubmissionCommand(
        string targetPersistentId,
        string requestId,
        string requestKey,
        int manifestationMilestone,
        IEnumerable<string> evidenceFactIds,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> evidenceBindings,
        int expectedRevision,
        NarrativeInferenceTimestamp timestamp)
    {
        TargetPersistentId = targetPersistentId?.Trim() ?? string.Empty;
        RequestId = requestId?.Trim() ?? string.Empty;
        RequestKey = requestKey?.Trim() ?? string.Empty;
        ManifestationMilestone = manifestationMilestone;
        EvidenceFactIds = (evidenceFactIds ?? Array.Empty<string>()).ToArray();
        EvidenceBindings = (evidenceBindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null).Select(value => value.Clone()).ToArray();
        ExpectedRevision = expectedRevision;
        Timestamp = timestamp;
    }

    public string TargetPersistentId { get; }
    public string RequestId { get; }
    public string RequestKey { get; }
    public int ManifestationMilestone { get; }
    public IReadOnlyList<string> EvidenceFactIds { get; }
    public IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> EvidenceBindings { get; }
    public int ExpectedRevision { get; }
    public NarrativeInferenceTimestamp Timestamp { get; }
}

public sealed class CharacterAcquiredTraitCompletionCommand
{
    public CharacterAcquiredTraitCompletionCommand(
        string targetPersistentId,
        string requestId,
        string requestKey,
        string candidatePacketHash,
        int registeredRevision,
        int expectedRevision,
        string responseJson,
        NarrativeInferenceTimestamp timestamp)
    {
        TargetPersistentId = targetPersistentId?.Trim() ?? string.Empty;
        RequestId = requestId?.Trim() ?? string.Empty;
        RequestKey = requestKey?.Trim() ?? string.Empty;
        CandidatePacketHash = candidatePacketHash?.Trim() ?? string.Empty;
        RegisteredRevision = registeredRevision;
        ExpectedRevision = expectedRevision;
        ResponseJson = responseJson ?? string.Empty;
        Timestamp = timestamp;
    }

    public string TargetPersistentId { get; }
    public string RequestId { get; }
    public string RequestKey { get; }
    public string CandidatePacketHash { get; }
    public int RegisteredRevision { get; }
    public int ExpectedRevision { get; }
    public string ResponseJson { get; }
    public NarrativeInferenceTimestamp Timestamp { get; }
}

public readonly struct CharacterAcquiredTraitInferenceAuditRecord
{
    public CharacterAcquiredTraitInferenceAuditRecord(
        string auditId,
        CharacterAcquiredTraitInferenceCommandKind commandKind,
        CharacterAcquiredTraitInferenceIssueCode issueCode,
        string requestKey,
        string candidatePacketHash,
        string selectedCombinationId,
        string targetPersistentId,
        int expectedRevision,
        int registeredRevision,
        int resultingRevision,
        NarrativeInferenceTimestamp timestamp,
        string validationError)
    {
        AuditId = auditId?.Trim() ?? string.Empty;
        CommandKind = commandKind;
        IssueCode = issueCode;
        ProfileId = LocalLlmRequestProfiles.AcquiredTrait.Id;
        RequestKey = requestKey?.Trim() ?? string.Empty;
        CandidatePacketHash = candidatePacketHash?.Trim() ?? string.Empty;
        SelectedCombinationId = selectedCombinationId?.Trim() ?? string.Empty;
        TargetPersistentId = targetPersistentId?.Trim() ?? string.Empty;
        ExpectedRevision = expectedRevision;
        RegisteredRevision = registeredRevision;
        ResultingRevision = resultingRevision;
        Timestamp = timestamp;
        ValidationError = validationError?.Trim() ?? string.Empty;
    }

    public string AuditId { get; }
    public CharacterAcquiredTraitInferenceCommandKind CommandKind { get; }
    public CharacterAcquiredTraitInferenceIssueCode IssueCode { get; }
    public string ProfileId { get; }
    public string RequestKey { get; }
    public string CandidatePacketHash { get; }
    public bool Succeeded => IssueCode == CharacterAcquiredTraitInferenceIssueCode.None;
    public string ValidationError { get; }
    public bool FallbackUsed => false;
    public string FallbackReason => string.Empty;
    public string SelectedCombinationId { get; }
    public string TargetPersistentId { get; }
    public int ExpectedRevision { get; }
    public int RegisteredRevision { get; }
    public int ResultingRevision { get; }
    public NarrativeInferenceTimestamp Timestamp { get; }
}

public readonly struct CharacterAcquiredTraitInferenceCommandResult
{
    public CharacterAcquiredTraitInferenceCommandResult(
        CharacterAcquiredTraitInferenceAuditRecord audit,
        CharacterAcquiredTraitRequestPacketDto packet)
    {
        Audit = audit;
        Packet = packet?.Clone();
    }

    public CharacterAcquiredTraitInferenceAuditRecord Audit { get; }
    public CharacterAcquiredTraitRequestPacketDto Packet { get; }
    public bool Succeeded => Audit.Succeeded;
}
