using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Characters;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Coordinates inferred-trait evidence, state publication and retained rollback.
/// Serialized state and its guards remain behind the progression authority port.
/// </summary>
internal sealed class CharacterTraitInferenceTransaction
{
    private readonly ICharacterTraitInferenceAuthority authority;
    private bool traitInferenceRollbackPending;

    internal CharacterTraitInferenceTransaction(ICharacterTraitInferenceAuthority authority)
    {
        this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
    }

    internal bool IsRollbackPending => traitInferenceRollbackPending;

    internal bool TryCommitInferredTrait(
        CharacterAcquiredTraitAggregateState candidate,
        int expectedRevision,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        IEnumerable<string> evidenceFactIds,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> evidenceBindings,
        CharacterAcquiredTraitInferenceAuditRecord audit,
        IDomainFactPublisher publisher,
        CharacterTraitInferencePublicationRuntime publications,
        out IReadOnlyList<CharacterAcquiredTraitValidationIssue> validationIssues)
    {
        var issues = new List<CharacterAcquiredTraitValidationIssue>();
        validationIssues = issues;
        var current = authority.CaptureAcquiredTraitState();
        string characterId;
        CharacterTraitInferredFact fact;
        try
        {
            characterId = authority.RequireCharacterId();
            if (publisher == null || publications == null || candidate == null || !audit.Succeeded
                || audit.CommandKind != CharacterAcquiredTraitInferenceCommandKind.Completion
                || audit.TargetPersistentId != characterId || audit.ExpectedRevision != expectedRevision
                || audit.ResultingRevision != candidate.revision
                || audit.Timestamp.Authority != NarrativeInferenceTimeAuthority.GameTick)
                throw new ArgumentException("trait-inference-command-contract-invalid");
            var published = candidate.instances.Single(value => value != null
                && value.acceptedRevision == candidate.revision && value.selectionAuditId == audit.AuditId);
            if (current.instances.Any(value => value?.instanceId == published.instanceId))
                throw new ArgumentException("trait-inference-instance-already-present");
            string displayName = authority.IdentityDisplayName;
            if (string.IsNullOrWhiteSpace(displayName)) displayName = authority.GrowthDisplayName;
            fact = new CharacterTraitInferredFact(new CharacterTraitInferredFactData
            {
                characterId = characterId, characterDisplayName = displayName?.Trim(),
                auditId = audit.AuditId, requestKey = audit.RequestKey,
                candidatePacketHash = audit.CandidatePacketHash, selectedCombinationId = audit.SelectedCombinationId,
                beforeRevision = expectedRevision, registeredRevision = audit.RegisteredRevision,
                afterRevision = candidate.revision, gameTick = audit.Timestamp.GameTick, trait = published
            });
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            issues.Add(new(CharacterAcquiredTraitValidationIssueCode.InvalidInstance, error.Message));
            return false;
        }
        if (!publications.TryBegin(characterId, out string failure))
        {
            issues.Add(new(CharacterAcquiredTraitValidationIssueCode.Conflict, failure));
            return false;
        }
        DomainFactLease lease = default;
        try
        {
            if (!publisher.TryReserve(fact, out lease, out failure))
            {
                issues.Add(new(CharacterAcquiredTraitValidationIssueCode.Conflict, failure));
                return false;
            }
            if (fact.Snapshot().trait.formulaVersion > 0)
                return TryCommitInferredFormulaTrait(candidate, expectedRevision, settings, moduleDefinitions,
                    evidenceFactIds, evidenceBindings, fact, lease, publications, out validationIssues);
            issues.AddRange(authority.ValidateAcquiredTraitCandidate(current, candidate, expectedRevision, settings, moduleDefinitions));
            if (issues.Count != 0) return false;
            return ApplyInferredTrait(candidate, current, new List<CharacterNarrativeFact>(), null,
                Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>(), fact, lease, publications, issues);
        }
        catch (Exception error) when (error is not OutOfMemoryException
            && error is not StackOverflowException && error is not AccessViolationException)
        {
            issues.Add(new(CharacterAcquiredTraitValidationIssueCode.Conflict, error.Message));
            validationIssues = issues;
            return false;
        }
        finally
        {
            try { if (lease.IsValid) publisher.Cancel(lease); }
            finally { publications.End(characterId); }
        }
    }

    private bool TryCommitInferredFormulaTrait(
        CharacterAcquiredTraitAggregateState candidate,
        int expectedRevision,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        IEnumerable<string> evidenceFactIds,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> evidenceBindings,
        CharacterTraitInferredFact fact,
        DomainFactLease lease,
        CharacterTraitInferencePublicationRuntime publications,
        out IReadOnlyList<CharacterAcquiredTraitValidationIssue> validationIssues)
    {
        var current = authority.CaptureAcquiredTraitState();
        CharacterAcquiredTraitModuleSO[] definitions = (moduleDefinitions
            ?? Array.Empty<CharacterAcquiredTraitModuleSO>()).ToArray();
        List<CharacterAcquiredTraitValidationIssue> issues = authority.ValidateAcquiredTraitCandidate(
            current, candidate, expectedRevision, settings, definitions);
        string[] evidence = (evidenceFactIds ?? Array.Empty<string>()).ToArray();
        GameplayOutcomeEvidenceBindingSnapshot[] exactBindings = (evidenceBindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .Select(value => value.Clone()).ToArray();
        HashSet<string> exactIds = exactBindings
            .Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        if (evidence.Length == 0
            || evidence.Any(value => !IsCanonicalProjectedTraitEvidenceId(value))
            || evidence.Distinct(StringComparer.Ordinal).Count() != evidence.Length)
        {
            issues.Add(new CharacterAcquiredTraitValidationIssue(
                CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                "Formula acquired-trait evidence IDs must be non-empty, distinct canonical public-fact:sha256 IDs."));
        }

        List<CharacterNarrativeFact> selectedFacts = new List<CharacterNarrativeFact>();
        if (issues.All(value => value.Code != CharacterAcquiredTraitValidationIssueCode.InvalidEvidence))
        {
            foreach (string evidenceId in evidence)
            {
                if (exactIds.Contains(evidenceId))
                    continue;
                if (!CharacterAcquiredTraitEvidenceProjection.TryResolve(
                        authority.NarrativeLedger,
                        evidenceId,
                        out CharacterNarrativeFact[] resolved,
                        out string resolutionError)
                    || resolved.Length != 1
                    || resolved[0].influenceUseCount == int.MaxValue)
                {
                    issues.Add(new CharacterAcquiredTraitValidationIssue(
                        CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                        resolved.Length == 1 && resolved[0].influenceUseCount == int.MaxValue
                            ? $"Formula acquired-trait evidence '{evidenceId}' exhausted its influence counter."
                            : resolutionError));
                    continue;
                }
                selectedFacts.Add(resolved[0]);
            }
        }

        if (candidate != null && evidence.Length > 0)
        {
            HashSet<string> previousIds = new HashSet<string>(
                (current.instances ?? new List<CharacterAcquiredTraitInstanceState>())
                .Where(value => value != null)
                .Select(value => value.instanceId),
                StringComparer.Ordinal);
            CharacterAcquiredTraitInstanceState[] published = (candidate.instances
                    ?? new List<CharacterAcquiredTraitInstanceState>())
                .Where(value => value != null
                    && value.formulaVersion > 0
                    && value.acceptedRevision == candidate.revision
                    && !previousIds.Contains(value.instanceId))
                .ToArray();
            string[] canonicalEvidence = evidence.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] publishedExact = published.Length == 1
                ? (published[0].evidenceBindings
                        ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
                    .Where(value => value != null)
                    .Select(value => value.publicFactId)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            if (published.Length != 1
                || published[0].evidenceFactIds == null
                || !published[0].evidenceFactIds.SequenceEqual(canonicalEvidence, StringComparer.Ordinal)
                || !publishedExact.SequenceEqual(
                    exactIds.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal)
                || exactIds.Any(value => !evidence.Contains(value, StringComparer.Ordinal)))
            {
                issues.Add(new CharacterAcquiredTraitValidationIssue(
                    CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                    "Formula acquired-trait commit must publish exactly one new v1 instance with the exact consumed evidence IDs."));
            }
        }

        if (issues.Count > 0)
        {
            validationIssues = issues;
            return false;
        }


        var selected = candidate.instances.Single(value => value != null
            && value.formulaVersion > 0 && value.acceptedRevision == candidate.revision);
        validationIssues = issues;
        return ApplyInferredTrait(candidate, current, selectedFacts, selected, exactBindings,
            fact, lease, publications, issues);
    }

    private bool ApplyInferredTrait(
        CharacterAcquiredTraitAggregateState candidate,
        CharacterAcquiredTraitAggregateState previous,
        List<CharacterNarrativeFact> selectedFacts,
        CharacterAcquiredTraitInstanceState selected,
        GameplayOutcomeEvidenceBindingSnapshot[] exactBindings,
        CharacterTraitInferredFact fact,
        DomainFactLease lease,
        CharacterTraitInferencePublicationRuntime publications,
        List<CharacterAcquiredTraitValidationIssue> issues)
    {
        string characterId = fact.Snapshot().characterId;
        var frozenCandidate = candidate.Clone();
        int[] beforeUses = selectedFacts.Select(value => value.influenceUseCount).ToArray();
        PreparedGameplayOutcomeEvidenceUse prepared = null;
        bool applied = false;
        bool retained = false;
        bool Rollback()
        {
            try { prepared?.Cancel(); }
            catch (Exception error) when (error is not OutOfMemoryException
                && error is not StackOverflowException && error is not AccessViolationException) { return false; }
            if (applied)
            {
                authority.ReplaceAcquiredTraitState(previous);
                for (int i = 0; i < selectedFacts.Count; i++) selectedFacts[i].influenceUseCount = beforeUses[i];
            }
            traitInferenceRollbackPending = false;
            return true;
        }
        try
        {
            if (exactBindings.Length > 0)
            {
                string failure = "evidence-transaction-unavailable";
                if (authority.EvidenceUseTransaction == null
                    || !authority.EvidenceUseTransaction.TryPrepareBindings("acquired-trait",
                        string.IsNullOrWhiteSpace(selected.presentationId) ? selected.instanceId : selected.presentationId,
                        exactBindings, out prepared, out failure)
                    || !prepared.TryCommitAnchors(out failure))
                {
                    issues.Add(new(CharacterAcquiredTraitValidationIssueCode.InvalidEvidence, failure));
                    return false;
                }
            }
            applied = true;
            authority.ReplaceAcquiredTraitState(frozenCandidate);
            foreach (var local in selectedFacts) local.influenceUseCount++;
            if (prepared != null && !prepared.TrySealOwnerCommitForJointOutcome(out string sealFailure))
            {
                issues.Add(new(CharacterAcquiredTraitValidationIssueCode.InvalidEvidence, sealFailure));
                return false;
            }
            DomainFactDelivery delivered = publications.PublishApplied(fact, lease);
            retained = delivered.MayBeCommitted;
            if (!retained)
            {
                issues.Add(new(CharacterAcquiredTraitValidationIssueCode.Conflict, delivered.Detail));
                return false;
            }
            prepared?.CompleteJointOutcomeCommit();
            authority.PublishInferredTraitChanged();
            return true;
        }
        finally
        {
            if (!retained && !Rollback())
            {
                traitInferenceRollbackPending = true;
                publications.RetainRollback(characterId, Rollback);
                issues.Add(new(CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                    "trait-inference-evidence-rollback-pending"));
            }
        }
    }

    private static bool IsCanonicalProjectedTraitEvidenceId(string value)
    {
        string prefix = CharacterAcquiredTraitEvidenceProjection.ProjectedFactIdPrefix;
        return value != null
            && value.Length == prefix.Length + 64
            && value.StartsWith(prefix, StringComparison.Ordinal)
            && value.Skip(prefix.Length).All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}
