using System;
using System.Collections.Generic;

/// <summary>
/// Coordinates exact-outcome retention anchors with the owning mechanic commit.
/// Compacted memories remain useful read-only context but cannot be selected as
/// mechanical evidence because they have no exact outcome anchor identity.
/// </summary>
public interface IGameplayOutcomeEvidenceUseTransaction
{
    bool TryPrepare(
        string anchorTypeId,
        string anchorId,
        IReadOnlyList<GameplayOutcomeNarrativeEvidenceSelection> selections,
        out PreparedGameplayOutcomeEvidenceUse prepared,
        out string failureCode);

    bool TryPrepareBindings(
        string anchorTypeId,
        string anchorId,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings,
        out PreparedGameplayOutcomeEvidenceUse prepared,
        out string failureCode);
}

public sealed class PreparedGameplayOutcomeEvidenceUse : IDisposable
{
    private readonly IGameplayOutcomeMemoryCommands commands;
    private readonly PreparedEvidenceAnchorToken[] preparedTokens;
    private readonly bool[] needsCommit;
    private readonly EvidenceAnchorRollbackToken[] rollbackTokens;
    private readonly PreparedInfluenceUseToken[] influenceTokens;
    private readonly InfluenceUseRollbackToken[] influenceRollbackTokens;
    private readonly bool[] pendingInfluenceCancellations;
    private int committedCount;
    private bool terminal;
    public bool IsTerminal => terminal;

    internal PreparedGameplayOutcomeEvidenceUse(
        IGameplayOutcomeMemoryCommands commands,
        PreparedEvidenceAnchorToken[] preparedTokens,
        bool[] needsCommit,
        PreparedInfluenceUseToken[] influenceTokens)
    {
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.preparedTokens = preparedTokens ?? Array.Empty<PreparedEvidenceAnchorToken>();
        this.needsCommit = needsCommit ?? Array.Empty<bool>();
        this.influenceTokens = influenceTokens
            ?? Array.Empty<PreparedInfluenceUseToken>();
        rollbackTokens = new EvidenceAnchorRollbackToken[this.preparedTokens.Length];
        influenceRollbackTokens = new InfluenceUseRollbackToken[
            this.influenceTokens.Length];
        pendingInfluenceCancellations = new bool[this.influenceTokens.Length];
    }

    public bool TryCommitAnchors(out string failureCode)
    {
        failureCode = string.Empty;
        if (terminal)
        {
            failureCode = "evidence-use-already-terminal";
            return false;
        }

        for (int index = 0; index < preparedTokens.Length; index++)
        {
            if (!needsCommit[index])
                continue;
            EvidenceAnchorCommitResult result = commands.CommitEvidenceAnchorMutation(
                preparedTokens[index], out EvidenceAnchorRollbackToken rollback);
            if (!result.Success)
            {
                failureCode = "evidence-anchor-commit-" + result.Code;
                // Commit failures may or may not release the reservation in the
                // ledger implementation. Cancel from the current token so the
                // transaction never relies on an implicit failure-side release.
                CancelFrom(index);
                bool rolledBack = RollbackCommitted();
                terminal = rolledBack;
                if (!rolledBack)
                    failureCode += ":rollback-pending";
                return false;
            }
            rollbackTokens[index] = rollback;
            committedCount++;

            InfluenceUseCommitResult influence = commands.CommitInfluenceUse(
                influenceTokens[index],
                out InfluenceUseRollbackToken influenceRollback);
            if (!influence.Success)
            {
                failureCode = "evidence-influence-commit-" + influence.Code;
                // Keep this reservation as the save guard until the anchor
                // that already committed has rolled back successfully.
                pendingInfluenceCancellations[index] = true;
                CancelFrom(index + 1);
                bool rolledBack = RollbackCommitted();
                terminal = rolledBack;
                if (!rolledBack)
                    failureCode += ":rollback-pending";
                return false;
            }
            influenceRollbackTokens[index] = influenceRollback;
        }
        return true;
    }

    /// <summary>
    /// Called only if the owner candidate failed to publish after all anchors
    /// committed. A failed rollback is loud because continuing would preserve a
    /// false mechanical-evidence retention claim.
    /// </summary>
    public bool TryRollbackAnchors(out string failureCode)
    {
        failureCode = string.Empty;
        if (terminal)
            return true;
        bool success = RollbackCommitted();
        // Failed tokens retain their save-blocking influence guard and remain
        // retryable. The owner must not publish its rollback until this method
        // reaches the terminal success state.
        terminal = success;
        if (!success)
            failureCode = "evidence-anchor-rollback-failed";
        return success;
    }

    public void CompleteOwnerCommit()
    {
        if (terminal)
            throw new InvalidOperationException("Evidence-use transaction is already terminal.");
        bool hasCommittedInfluence = false;
        for (int index = 0; index < influenceRollbackTokens.Length; index++)
            hasCommittedInfluence |= influenceRollbackTokens[index].IsValid;
        if (hasCommittedInfluence)
        {
            InfluenceUseCommitResult completed =
                commands.CompleteInfluenceUses(influenceRollbackTokens);
            if (!completed.Success)
                throw new InvalidOperationException(
                    "Evidence influence-use completion failed: "
                    + completed.Code);
            Array.Clear(
                influenceRollbackTokens,
                0,
                influenceRollbackTokens.Length);
        }
        terminal = true;
    }

    public void Cancel()
    {
        if (terminal)
            return;
        CancelFrom(0);
        if (HasCommittedRollback())
        {
            if (!RollbackCommitted())
            {
                throw new InvalidOperationException(
                    "Evidence-use cancellation left a guarded rollback pending.");
            }
        }
        terminal = true;
    }

    public void Dispose() => Cancel();

    private void CancelFrom(int start)
    {
        for (int index = Math.Max(0, start); index < preparedTokens.Length; index++)
        {
            if (needsCommit[index] && preparedTokens[index].IsValid)
            {
                if (!rollbackTokens[index].IsValid)
                    commands.CancelEvidenceAnchorMutation(preparedTokens[index]);
            }
            if (index < influenceTokens.Length
                && influenceTokens[index].IsValid
                && !influenceRollbackTokens[index].IsValid
                && !pendingInfluenceCancellations[index])
            {
                commands.CancelInfluenceUse(influenceTokens[index]);
            }
        }
    }

    private bool RollbackCommitted()
    {
        bool success = true;
        for (int index = rollbackTokens.Length - 1; index >= 0; index--)
        {
            // Roll back the anchor while the matching influence reservation
            // still blocks save/consolidation. If the anchor rollback cannot
            // complete, retain both tokens and the guard for reconciliation.
            if (rollbackTokens[index].IsValid)
            {
                EvidenceAnchorRollbackResult anchor =
                    commands.RollbackEvidenceAnchorMutation(
                        rollbackTokens[index]);
                if (!anchor.Success)
                {
                    success = false;
                    continue;
                }
                rollbackTokens[index] = default;
            }
            if (index < pendingInfluenceCancellations.Length
                && pendingInfluenceCancellations[index])
            {
                commands.CancelInfluenceUse(influenceTokens[index]);
                pendingInfluenceCancellations[index] = false;
            }
            if (index < influenceRollbackTokens.Length
                && influenceRollbackTokens[index].IsValid)
            {
                InfluenceUseRollbackResult influence =
                    commands.RollbackInfluenceUse(
                        influenceRollbackTokens[index]);
                if (!influence.Success)
                {
                    success = false;
                    continue;
                }
                influenceRollbackTokens[index] = default;
            }
        }
        committedCount = 0;
        for (int index = 0; index < rollbackTokens.Length; index++)
        {
            if (rollbackTokens[index].IsValid
                || index < influenceRollbackTokens.Length
                    && influenceRollbackTokens[index].IsValid
                || index < pendingInfluenceCancellations.Length
                    && pendingInfluenceCancellations[index])
                committedCount++;
        }
        return success;
    }

    private bool HasCommittedRollback()
    {
        for (int index = 0; index < rollbackTokens.Length; index++)
        {
            if (rollbackTokens[index].IsValid
                || index < influenceRollbackTokens.Length
                    && influenceRollbackTokens[index].IsValid
                || index < pendingInfluenceCancellations.Length
                    && pendingInfluenceCancellations[index])
                return true;
        }
        return false;
    }
}

public sealed class GameplayOutcomeEvidenceUseTransaction :
    IGameplayOutcomeEvidenceUseTransaction
{
    private readonly IGameplayOutcomeMemoryCommands commands;
    private readonly IGameplayOutcomeNarrativeEvidenceQuery evidenceQuery;

    public GameplayOutcomeEvidenceUseTransaction(
        IGameplayOutcomeMemoryCommands commands,
        IGameplayOutcomeNarrativeEvidenceQuery evidenceQuery)
    {
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.evidenceQuery = evidenceQuery
            ?? throw new ArgumentNullException(nameof(evidenceQuery));
    }

    public bool TryPrepare(
        string anchorTypeId,
        string anchorId,
        IReadOnlyList<GameplayOutcomeNarrativeEvidenceSelection> selections,
        out PreparedGameplayOutcomeEvidenceUse prepared,
        out string failureCode)
    {
        prepared = null;
        failureCode = string.Empty;
        NarrativeEvidenceReference anchor;
        try
        {
            anchor = new NarrativeEvidenceReference(
                anchorTypeId?.Trim() ?? string.Empty,
                anchorId?.Trim() ?? string.Empty);
        }
        catch (ArgumentException)
        {
            failureCode = "evidence-anchor-identity-invalid";
            return false;
        }

        int count = selections?.Count ?? 0;
        if (count == 0)
        {
            failureCode = "evidence-selection-empty";
            return false;
        }
        PreparedEvidenceAnchorToken[] tokens = new PreparedEvidenceAnchorToken[count];
        PreparedInfluenceUseToken[] influenceTokens =
            new PreparedInfluenceUseToken[count];
        bool[] needsCommit = new bool[count];
        HashSet<GameplayOutcomeId> outcomes = new();
        for (int index = 0; index < count; index++)
        {
            GameplayOutcomeNarrativeEvidenceSelection selection = selections[index];
            GameplayOutcomeNarrativeEvidenceSource source = selection.Source;
            if (!selection.IsValid || source.IsCompacted || !source.HasExactOutcome)
            {
                failureCode = source.IsCompacted
                    ? "compacted-evidence-is-context-only"
                    : "exact-evidence-selection-invalid";
                Cancel(tokens, needsCommit, influenceTokens, index);
                return false;
            }

            // Never trust a caller-assembled evidence DTO. Rebuild its typed
            // binding and resolve it against the authoritative exact ledger row;
            // every public fact, subject, descriptor and revision must match.
            List<GameplayOutcomeEvidenceBindingSnapshot> issued =
                GameplayOutcomeEvidenceFormulaProjection.CaptureExact(new[] { source });
            if (issued.Count != 1
                || !evidenceQuery.TryResolveExactBinding(issued[0], out source)
                || !string.Equals(selection.PublicFactId, source.SourceId,
                    StringComparison.Ordinal))
            {
                failureCode = "exact-evidence-binding-untrusted";
                Cancel(tokens, needsCommit, influenceTokens, index);
                return false;
            }
            if (!outcomes.Add(source.ExactOutcomeId))
            {
                failureCode = "duplicate-exact-evidence-outcome";
                Cancel(tokens, needsCommit, influenceTokens, index);
                return false;
            }

            InfluenceUsePrepareResult influence =
                commands.TryPrepareInfluenceUse(
                    source.ExactOutcomeId,
                    source.SubjectId,
                    source.InfluenceRevision,
                    source.InfluenceUseCount,
                    out influenceTokens[index]);
            if (!influence.Success)
            {
                failureCode = string.IsNullOrWhiteSpace(influence.DetailCode)
                    ? "evidence-influence-prepare-" + influence.Code
                    : influence.DetailCode;
                Cancel(tokens, needsCommit, influenceTokens, index + 1);
                return false;
            }

            EvidenceAnchorPrepareResult result =
                commands.TryPrepareEvidenceAnchorMutation(
                    source.ExactOutcomeId,
                    source.SubjectId,
                    anchor,
                    EvidenceAnchorMutationKind.Add,
                    source.AnchorRevision,
                    out tokens[index]);
            if (!result.Success)
            {
                failureCode = string.IsNullOrWhiteSpace(result.DetailCode)
                    ? "evidence-anchor-prepare-" + result.Code
                    : result.DetailCode;
                Cancel(tokens, needsCommit, influenceTokens, index + 1);
                return false;
            }
            needsCommit[index] = result.Code == EvidenceAnchorPrepareCode.Prepared;
            if (!needsCommit[index])
            {
                commands.CancelInfluenceUse(influenceTokens[index]);
                influenceTokens[index] = default;
            }
        }

        prepared = new PreparedGameplayOutcomeEvidenceUse(
            commands,
            tokens,
            needsCommit,
            influenceTokens);
        return true;
    }

    public bool TryPrepareBindings(
        string anchorTypeId,
        string anchorId,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings,
        out PreparedGameplayOutcomeEvidenceUse prepared,
        out string failureCode)
    {
        int count = bindings?.Count ?? 0;
        if (count == 0)
        {
            prepared = null;
            failureCode = "evidence-binding-empty";
            return false;
        }
        GameplayOutcomeNarrativeEvidenceSelection[] selections =
            new GameplayOutcomeNarrativeEvidenceSelection[count];
        try
        {
            for (int index = 0; index < count; index++)
            {
                GameplayOutcomeEvidenceBindingSnapshot binding = bindings[index]
                    ?? throw new ArgumentException("Evidence binding is null.");
                if (!evidenceQuery.TryResolveExactBinding(binding, out var source))
                    throw new ArgumentException("Evidence binding no longer resolves exactly.");
                selections[index] = new GameplayOutcomeNarrativeEvidenceSelection(
                    binding.publicFactId, source);
            }
        }
        catch (ArgumentException)
        {
            prepared = null;
            failureCode = "evidence-binding-invalid";
            return false;
        }
        return TryPrepare(
            anchorTypeId, anchorId, selections, out prepared, out failureCode);
    }

    private void Cancel(
        IReadOnlyList<PreparedEvidenceAnchorToken> tokens,
        IReadOnlyList<bool> needsCommit,
        IReadOnlyList<PreparedInfluenceUseToken> influenceTokens,
        int count)
    {
        for (int index = 0; index < count; index++)
        {
            if (needsCommit[index] && tokens[index].IsValid)
                commands.CancelEvidenceAnchorMutation(tokens[index]);
            if (influenceTokens[index].IsValid)
                commands.CancelInfluenceUse(influenceTokens[index]);
        }
    }
}
