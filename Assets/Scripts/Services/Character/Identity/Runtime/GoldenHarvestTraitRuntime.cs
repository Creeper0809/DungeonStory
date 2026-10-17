using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static ExtremeTraitRuleAccess;
using static ExtremeTraitRuleIds;

internal sealed class GoldenHarvestTraitRuntime
{
    private readonly CharacterIdentityStateStore states;

    internal GoldenHarvestTraitRuntime(CharacterIdentityStateStore states) =>
        this.states = states ?? throw new ArgumentNullException(nameof(states));

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryScheduleGoldenHarvest(
        CharacterActor actor,
        string fieldId,
        int attemptIndex,
        float elapsedSeconds)
    {
        GoldenHarvestRule rule = ResolveRule<GoldenHarvestRule>(
            actor, 304, GoldenHarvestRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        GoldenHarvestRuntimeState state = Read<GoldenHarvestRuntimeState>(states,
            actor, trait, GoldenHarvestRuleId);
        if (state.pending) return false;
        state.fieldId = Required(fieldId, nameof(fieldId));
        state.attemptIndex = Mathf.Max(0, attemptIndex);
        state.resolveAfterSeconds = Mathf.Max(0f, elapsedSeconds)
            + HoursToSeconds(rule.delayHours);
        state.pending = true;
        Write(states, actor, trait, GoldenHarvestRuleId, state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryResolveGoldenHarvest(
        CharacterActor actor,
        string fieldId,
        ulong runSeed,
        float elapsedSeconds,
        out ExtremeRiskResolution resolution)
    {
        resolution = default;
        GoldenHarvestRule rule = ResolveRule<GoldenHarvestRule>(
            actor, 304, GoldenHarvestRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        GoldenHarvestRuntimeState state = Read<GoldenHarvestRuntimeState>(states,
            actor, trait, GoldenHarvestRuleId);
        string field = Required(fieldId, nameof(fieldId));
        if (!state.pending
            || elapsedSeconds < state.resolveAfterSeconds
            || !string.Equals(state.fieldId, field, StringComparison.Ordinal))
            return false;
        ulong hash = GoldenHarvestDeterministicOutcomeAuthority.CaptureRollHash(
            runSeed,
            field,
            state.attemptIndex,
            ActorId(actor));
        float roll = GoldenHarvestDeterministicOutcomeAuthority
            .CaptureRoll01(hash);
        resolution = roll < rule.jackpotChance
            ? new ExtremeRiskResolution(
                ExtremeRiskOutcome.Jackpot,
                1f,
                1f,
                0f,
                hash)
            : roll < rule.jackpotChance + rule.lossChance
                ? new ExtremeRiskResolution(
                    ExtremeRiskOutcome.Loss,
                    rule.failureYieldMultiplier,
                    rule.failureYieldMultiplier,
                    0f,
                    hash)
                : new ExtremeRiskResolution(ExtremeRiskOutcome.Normal, 1f, 1f, 0f, hash);
        state.pending = false;
        state.fieldId = string.Empty;
        Write(states, actor, trait, GoldenHarvestRuleId, state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryPrepareGoldenHarvest(
        CharacterActor actor,
        string fieldId,
        string operationId,
        ulong runSeed,
        float elapsedSeconds,
        out GoldenHarvestPreparedResolution prepared)
    {
        prepared = default;
        GoldenHarvestRule rule = ResolveRule<GoldenHarvestRule>(
            actor,
            304,
            GoldenHarvestRuleId,
            out CharacterTraitSO trait);
        if (rule == null)
            return false;
        string operation = Required(operationId, nameof(operationId));
        string field = Required(fieldId, nameof(fieldId));
        string characterId = ActorId(actor);
        GoldenHarvestRuntimeState state = Read<GoldenHarvestRuntimeState>(states,
            actor,
            trait,
            GoldenHarvestRuleId);
        if (!string.IsNullOrEmpty(state.preparedOperationId))
        {
            if (!string.Equals(
                    state.preparedOperationId,
                    operation,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Golden harvest already has another prepared operation.");
            prepared = CapturePrepared(
                state,
                characterId,
                trait.DefinitionId.Value);
            return true;
        }
        if (!state.pending
            || elapsedSeconds < state.resolveAfterSeconds
            || !string.Equals(state.fieldId, field, StringComparison.Ordinal))
            return false;
        ulong hash = GoldenHarvestDeterministicOutcomeAuthority.CaptureRollHash(
            runSeed,
            field,
            state.attemptIndex,
            characterId);
        float roll = GoldenHarvestDeterministicOutcomeAuthority
            .CaptureRoll01(hash);
        ExtremeRiskResolution resolution = roll < rule.jackpotChance
            ? new ExtremeRiskResolution(
                ExtremeRiskOutcome.Jackpot,
                1f,
                1f,
                0f,
                hash)
            : roll < rule.jackpotChance + rule.lossChance
                ? new ExtremeRiskResolution(
                    ExtremeRiskOutcome.Loss,
                    rule.failureYieldMultiplier,
                    rule.failureYieldMultiplier,
                    0f,
                    hash)
                : new ExtremeRiskResolution(
                    ExtremeRiskOutcome.Normal,
                    1f,
                    1f,
                    0f,
                    hash);
        state.preparedOperationId = operation;
        state.preparedFieldId = field;
        state.preparedOutcome = resolution.Outcome;
        state.preparedPrimaryMultiplier = resolution.PrimaryMultiplier;
        state.preparedSecondaryMultiplier = resolution.SecondaryMultiplier;
        state.preparedProgressDelta = resolution.ProgressDelta;
        state.preparedRollHash = resolution.FixedRollHash;
        state.preparedFingerprint = CapturePreparedFingerprint(
            operation,
            characterId,
            trait.DefinitionId.Value,
            field,
            resolution);
        state.preparedCommitted = false;
        Write(states, actor, trait, GoldenHarvestRuleId, state);
        prepared = CapturePrepared(
            state,
            characterId,
            trait.DefinitionId.Value);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryCommitPreparedGoldenHarvest(
        string characterId,
        string traitDefinitionId,
        string operationId,
        out GoldenHarvestPreparedResolution prepared)
    {
        prepared = default;
        if (!TryReadPreparedByIdentity(
                characterId,
                traitDefinitionId,
                operationId,
                out GoldenHarvestRuntimeState state))
            return false;
        if (!state.preparedCommitted)
        {
            state.pending = false;
            state.fieldId = string.Empty;
            state.preparedCommitted = true;
            WriteByIdentity(
                characterId,
                traitDefinitionId,
                GoldenHarvestRuleId,
                state);
        }
        prepared = CapturePrepared(state, characterId, traitDefinitionId);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryAcknowledgePreparedGoldenHarvest(
        string characterId,
        string traitDefinitionId,
        string operationId)
    {
        if (!TryReadPreparedByIdentity(
                characterId,
                traitDefinitionId,
                operationId,
                out GoldenHarvestRuntimeState state)
            || !state.preparedCommitted)
            return false;
        bool retireDeathRetainedState = state.preparedOwnerDead;
        ClearPrepared(state);
        if (retireDeathRetainedState)
            states.RemoveRule(
                characterId,
                traitDefinitionId,
                GoldenHarvestRuleId);
        else
            WriteByIdentity(
                characterId,
                traitDefinitionId,
                GoldenHarvestRuleId,
                state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryAbortPreparedGoldenHarvest(
        string characterId,
        string traitDefinitionId,
        string operationId)
    {
        if (!TryReadPreparedByIdentity(
                characterId,
                traitDefinitionId,
                operationId,
                out GoldenHarvestRuntimeState state)
            || state.preparedCommitted)
            return false;
        bool retireDeathRetainedState = state.preparedOwnerDead;
        ClearPrepared(state);
        if (retireDeathRetainedState)
            states.RemoveRule(
                characterId,
                traitDefinitionId,
                GoldenHarvestRuleId);
        else
            WriteByIdentity(
                characterId,
                traitDefinitionId,
                GoldenHarvestRuleId,
                state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryRetainForPendingExternalOwner(
        string characterId,
        CharacterIdentityRuleStateSaveData saved)
    {
        if (saved == null
            || !string.Equals(
                saved.ruleId,
                GoldenHarvestRuleId,
                StringComparison.Ordinal))
            return false;
        CharacterIdentityRuntimeStateSaveData owner = new()
        {
            characterId = Required(characterId, nameof(characterId)),
            rules = new List<CharacterIdentityRuleStateSaveData>
            {
                saved.Clone()
            }
        };
        GoldenHarvestPreparedResolution[] prepared =
            CapturePreparedGoldenHarvests(new[] { owner }).ToArray();
        if (prepared.Length == 0)
            return false;
        GoldenHarvestRuntimeState state = JsonUtility.FromJson<
            GoldenHarvestRuntimeState>(saved.statePayload)
            ?? throw new InvalidOperationException(
                "Golden Harvest death retention payload is invalid.");
        state.preparedOwnerDead = true;
        WriteByIdentity(
            characterId,
            saved.traitDefinitionId,
            GoldenHarvestRuleId,
            state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal IReadOnlyList<GoldenHarvestPreparedResolution>
        CapturePreparedGoldenHarvests() => CapturePreparedGoldenHarvests(
            states.Capture());

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal static IReadOnlyList<GoldenHarvestPreparedResolution>
        CapturePreparedGoldenHarvests(
            IEnumerable<CharacterIdentityRuntimeStateSaveData> source)
    {
        List<GoldenHarvestPreparedResolution> result = new();
        HashSet<string> operations = new(StringComparer.Ordinal);
        foreach (CharacterIdentityRuntimeStateSaveData character in
                 source ?? Array.Empty<CharacterIdentityRuntimeStateSaveData>())
        {
            if (character == null)
                throw new InvalidOperationException(
                    "Golden Harvest state census contains a null character owner.");
            foreach (CharacterIdentityRuleStateSaveData saved in
                     character.rules ?? new List<CharacterIdentityRuleStateSaveData>())
            {
                if (!string.Equals(
                        saved.ruleId,
                        GoldenHarvestRuleId,
                        StringComparison.Ordinal))
                    continue;
                GoldenHarvestRuntimeState state = JsonUtility.FromJson<
                    GoldenHarvestRuntimeState>(saved.statePayload)
                    ?? new GoldenHarvestRuntimeState();
                bool hasPreparedProvenance =
                    !string.IsNullOrEmpty(state.preparedOperationId)
                    || !string.IsNullOrEmpty(state.preparedFieldId)
                    || !string.IsNullOrEmpty(state.preparedFingerprint)
                    || state.preparedOutcome != default
                    || state.preparedPrimaryMultiplier != 0f
                    || state.preparedSecondaryMultiplier != 0f
                    || state.preparedProgressDelta != 0f
                    || state.preparedRollHash != 0UL
                    || state.preparedCommitted;
                if (!hasPreparedProvenance)
                    continue;
                if (!Canonical(state.preparedOperationId)
                    || !Canonical(character.characterId)
                    || !Canonical(saved.traitDefinitionId)
                    || !Canonical(state.preparedFieldId)
                    || !Enum.IsDefined(
                        typeof(ExtremeRiskOutcome),
                        state.preparedOutcome)
                    || !FinitePositive(state.preparedPrimaryMultiplier)
                    || !FinitePositive(state.preparedSecondaryMultiplier)
                    || !state.preparedCommitted
                        && (!state.pending
                            || !string.Equals(
                                state.fieldId,
                                state.preparedFieldId,
                                StringComparison.Ordinal))
                    || state.preparedCommitted
                        && (state.pending
                            || !string.IsNullOrEmpty(state.fieldId))
                    || !string.Equals(
                        state.preparedFingerprint,
                        CapturePreparedFingerprint(
                            state.preparedOperationId,
                            character.characterId,
                            saved.traitDefinitionId,
                            state.preparedFieldId,
                            PreparedResolution(state)),
                        StringComparison.Ordinal)
                    || !operations.Add(state.preparedOperationId))
                    throw new InvalidOperationException(
                        "Golden Harvest prepared resolution census is invalid or duplicated.");
                result.Add(CapturePrepared(
                    state,
                    character.characterId,
                    saved.traitDefinitionId));
            }
        }
        return result
            .OrderBy(value => value.OperationId, StringComparer.Ordinal)
            .ToArray();
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryGetGoldenHarvestDelay(
        CharacterActor actor,
        string fieldId,
        float elapsedSeconds,
        out float remainingSeconds)
    {
        remainingSeconds = 0f;
        GoldenHarvestRule rule = ResolveRule<GoldenHarvestRule>(
            actor, 304, GoldenHarvestRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        GoldenHarvestRuntimeState state = Read<GoldenHarvestRuntimeState>(states,
            actor, trait, GoldenHarvestRuleId);
        if (!state.pending
            || !string.Equals(state.fieldId, fieldId?.Trim(), StringComparison.Ordinal))
            return false;
        remainingSeconds = Mathf.Max(0f, state.resolveAfterSeconds - elapsedSeconds);
        return remainingSeconds > 0f;
    }

    private bool TryReadPreparedByIdentity(
        string characterId,
        string traitDefinitionId,
        string operationId,
        out GoldenHarvestRuntimeState state)
    {
        state = null;
        string character = Required(characterId, nameof(characterId));
        string trait = Required(traitDefinitionId, nameof(traitDefinitionId));
        string operation = Required(operationId, nameof(operationId));
        if (!states.TryGet(
                character,
                trait,
                GoldenHarvestRuleId,
                out CharacterIdentityRuleStateSaveData saved)
            || string.IsNullOrWhiteSpace(saved.statePayload))
            return false;
        state = JsonUtility.FromJson<GoldenHarvestRuntimeState>(saved.statePayload)
            ?? new GoldenHarvestRuntimeState();
        return string.Equals(
                state.preparedOperationId,
                operation,
                StringComparison.Ordinal)
            && string.Equals(
                state.preparedFingerprint,
                CapturePreparedFingerprint(
                    operation,
                    character,
                    trait,
                    state.preparedFieldId,
                    PreparedResolution(state)),
                StringComparison.Ordinal);
    }

    private void WriteByIdentity<TState>(
        string characterId,
        string traitDefinitionId,
        string ruleId,
        TState state)
        where TState : class => states.Set(
        Required(characterId, nameof(characterId)),
        Required(traitDefinitionId, nameof(traitDefinitionId)),
        Required(ruleId, nameof(ruleId)),
        1,
        JsonUtility.ToJson(state));

    private static GoldenHarvestPreparedResolution CapturePrepared(
        GoldenHarvestRuntimeState state,
        string characterId,
        string traitDefinitionId) => new(
        state.preparedOperationId,
        characterId,
        traitDefinitionId,
        state.preparedFieldId,
        state.preparedFingerprint,
        state.preparedCommitted,
        PreparedResolution(state));

    private static ExtremeRiskResolution PreparedResolution(
        GoldenHarvestRuntimeState state) => new(
        state.preparedOutcome,
        state.preparedPrimaryMultiplier,
        state.preparedSecondaryMultiplier,
        state.preparedProgressDelta,
        state.preparedRollHash);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static string CapturePreparedFingerprint(
        string operationId,
        string characterId,
        string traitDefinitionId,
        string fieldId,
        ExtremeRiskResolution resolution)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("golden-harvest-prepared@1");
        digest.Append(operationId);
        digest.Append(characterId);
        digest.Append(traitDefinitionId);
        digest.Append(fieldId);
        digest.Append((int)resolution.Outcome);
        digest.Append(resolution.PrimaryMultiplier.ToString(
            "R",
            CultureInfo.InvariantCulture));
        digest.Append(resolution.SecondaryMultiplier.ToString(
            "R",
            CultureInfo.InvariantCulture));
        digest.Append(resolution.ProgressDelta.ToString(
            "R",
            CultureInfo.InvariantCulture));
        digest.Append(resolution.FixedRollHash.ToString(
            CultureInfo.InvariantCulture));
        return digest.ComputeSha256();
    }

    private static void ClearPrepared(GoldenHarvestRuntimeState state)
    {
        state.preparedOperationId = string.Empty;
        state.preparedFieldId = string.Empty;
        state.preparedOutcome = default;
        state.preparedPrimaryMultiplier = 0f;
        state.preparedSecondaryMultiplier = 0f;
        state.preparedProgressDelta = 0f;
        state.preparedRollHash = 0UL;
        state.preparedFingerprint = string.Empty;
        state.preparedCommitted = false;
        state.preparedOwnerDead = false;
    }
}
