using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

[Serializable]
public sealed class GlobalFacilityReputationSnapshot
{
    public List<SocialRumorSnapshot> rumors = new List<SocialRumorSnapshot>();
    public List<SocialMemoryFloat> reputation = new List<SocialMemoryFloat>();

    public GlobalFacilityReputationSnapshot Clone()
    {
        return new GlobalFacilityReputationSnapshot
        {
            rumors = rumors?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<SocialRumorSnapshot>(),
            reputation = reputation?.Where(item => item != null)
                .Select(item => new SocialMemoryFloat(item.key, item.value)).ToList()
                ?? new List<SocialMemoryFloat>()
        };
    }
}

internal sealed class GlobalFacilityReputationLedgerState
{
    internal GlobalFacilityReputationLedgerState(
        List<SocialRumor> rumors,
        List<SocialMemoryFloat> debugProjection,
        Dictionary<string, float> reputationByKey)
    {
        Rumors = rumors ?? throw new ArgumentNullException(nameof(rumors));
        DebugProjection = debugProjection
            ?? throw new ArgumentNullException(nameof(debugProjection));
        ReputationByKey = reputationByKey
            ?? throw new ArgumentNullException(nameof(reputationByKey));
    }

    internal List<SocialRumor> Rumors { get; }
    internal List<SocialMemoryFloat> DebugProjection { get; }
    internal Dictionary<string, float> ReputationByKey { get; }
}

public sealed class GlobalFacilityReputationRestoreCandidate
{
    private readonly GlobalFacilityReputationLedger owner;
    private GlobalFacilityReputationLedgerState state;

    internal GlobalFacilityReputationRestoreCandidate(
        GlobalFacilityReputationLedger owner,
        GlobalFacilityReputationLedgerState state)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal GlobalFacilityReputationLedgerState Peek(
        GlobalFacilityReputationLedger expectedOwner)
    {
        if (!ReferenceEquals(owner, expectedOwner) || state == null)
        {
            throw new InvalidOperationException(
                "Global facility reputation restore candidate has the wrong owner or was already applied.");
        }

        return state;
    }

    internal void Consume(
        GlobalFacilityReputationLedger expectedOwner,
        GlobalFacilityReputationLedgerState expectedState)
    {
        if (!ReferenceEquals(owner, expectedOwner)
            || state == null
            || !ReferenceEquals(state, expectedState))
        {
            throw new InvalidOperationException(
                "Global facility reputation restore candidate has the wrong owner or was already applied.");
        }

        state = null;
    }
}

public sealed class GlobalFacilityReputationRestoreTransaction
{
    private readonly GlobalFacilityReputationLedger owner;
    private GlobalFacilityReputationLedgerState previous;
    private readonly GlobalFacilityReputationLedgerState applied;
    private bool active = true;

    internal GlobalFacilityReputationRestoreTransaction(
        GlobalFacilityReputationLedger owner,
        GlobalFacilityReputationLedgerState previous,
        GlobalFacilityReputationLedgerState applied)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.previous = previous ?? throw new ArgumentNullException(nameof(previous));
        this.applied = applied ?? throw new ArgumentNullException(nameof(applied));
    }

    internal GlobalFacilityReputationLedgerState Rollback(
        GlobalFacilityReputationLedger expectedOwner,
        GlobalFacilityReputationLedgerState current)
    {
        RequireActive(expectedOwner, current);
        GlobalFacilityReputationLedgerState result = previous;
        previous = null;
        active = false;
        return result;
    }

    internal void Complete(
        GlobalFacilityReputationLedger expectedOwner,
        GlobalFacilityReputationLedgerState current)
    {
        RequireActive(expectedOwner, current);
        previous = null;
        active = false;
    }

    private void RequireActive(
        GlobalFacilityReputationLedger expectedOwner,
        GlobalFacilityReputationLedgerState current)
    {
        if (!active
            || !ReferenceEquals(owner, expectedOwner)
            || !ReferenceEquals(applied, current))
        {
            throw new InvalidOperationException(
                "Global facility reputation restore transaction has the wrong owner, is no longer active, or was already finished.");
        }
    }
}

public sealed class GlobalFacilityReputationLedger
{
    private readonly IGameClock gameClock;
    private GlobalFacilityReputationLedgerState state;
    private List<SocialRumor> rumors => state.Rumors;
    private List<SocialMemoryFloat> debugProjection => state.DebugProjection;
    private Dictionary<string, float> reputationByKey => state.ReputationByKey;

    internal List<SocialRumor> Rumors => rumors;
    internal List<SocialMemoryFloat> DebugProjection => debugProjection;

    public GlobalFacilityReputationLedger(
        List<SocialRumor> rumors,
        List<SocialMemoryFloat> debugProjection,
        IGameClock gameClock)
    {
        state = new GlobalFacilityReputationLedgerState(
            rumors ?? throw new ArgumentNullException(nameof(rumors)),
            debugProjection ?? throw new ArgumentNullException(nameof(debugProjection)),
            new Dictionary<string, float>(StringComparer.Ordinal));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public GlobalFacilityReputationSnapshot CaptureSnapshot(float blend)
    {
        PruneExpired(blend);
        float now = gameClock.Time;
        return new GlobalFacilityReputationSnapshot
        {
            rumors = rumors
                .Where(rumor => rumor != null && !rumor.IsExpiredAt(now))
                .Select(rumor => SocialRumorSnapshot.Capture(rumor, now))
                .Where(snapshot => snapshot != null)
                .ToList(),
            reputation = reputationByKey
                .Select(entry => new SocialMemoryFloat(entry.Key, entry.Value))
                .ToList()
        };
    }

    public void RestoreSnapshot(GlobalFacilityReputationSnapshot snapshot)
    {
        GlobalFacilityReputationRestoreTransaction transaction =
            ApplyRestoreCandidate(BuildRestoreCandidate(snapshot));
        CompleteRestore(transaction);
    }

    public GlobalFacilityReputationRestoreCandidate BuildRestoreCandidate(
        GlobalFacilityReputationSnapshot snapshot)
    {
        float now = gameClock.Time;
        List<SocialRumor> restoredRumors = new List<SocialRumor>();
        Dictionary<string, float> restoredReputation =
            new Dictionary<string, float>(StringComparer.Ordinal);
        if (snapshot != null)
        {
            restoredRumors.AddRange(snapshot.rumors?
                .Where(item => item != null && item.remainingSeconds > 0f)
                .Select(item => item.Restore(now))
                .Where(rumor => rumor != null) ?? Enumerable.Empty<SocialRumor>());
            foreach (SocialMemoryFloat entry in snapshot.reputation ?? new List<SocialMemoryFloat>())
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.key))
                {
                    restoredReputation[entry.key] = Mathf.Clamp(
                        entry.value,
                        -1f,
                        1f);
                }
            }
        }

        List<SocialMemoryFloat> restoredDebug = restoredReputation
            .Select(entry => new SocialMemoryFloat(entry.Key, entry.Value))
            .ToList();
        return new GlobalFacilityReputationRestoreCandidate(
            this,
            new GlobalFacilityReputationLedgerState(
                restoredRumors,
                restoredDebug,
                restoredReputation));
    }

    public GlobalFacilityReputationRestoreTransaction ApplyRestoreCandidate(
        GlobalFacilityReputationRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        GlobalFacilityReputationLedgerState applied = candidate.Peek(this);
        GlobalFacilityReputationLedgerState previous = state;
        GlobalFacilityReputationRestoreTransaction transaction =
            new GlobalFacilityReputationRestoreTransaction(
                this,
                previous,
                applied);
        candidate.Consume(this, applied);
        state = applied;
        return transaction;
    }

    public void RollbackRestore(
        GlobalFacilityReputationRestoreTransaction transaction)
    {
        state = (transaction ?? throw new ArgumentNullException(nameof(transaction)))
            .Rollback(this, state);
    }

    public void CompleteRestore(
        GlobalFacilityReputationRestoreTransaction transaction)
    {
        (transaction ?? throw new ArgumentNullException(nameof(transaction)))
            .Complete(this, state);
    }

    public void Apply(SocialRumor rumor, float blend)
    {
        if (rumor == null)
        {
            throw new ArgumentNullException(nameof(rumor));
        }

        rumors.Add(rumor.Clone());
        PruneExpired(blend);
        Rebuild(blend);
    }

    public float GetSentiment(BuildableObject building, float blend)
    {
        if (building == null)
        {
            return 0f;
        }

        PruneExpired(blend);
        float sum = 0f;
        int count = 0;
        foreach (KeyValuePair<string, float> entry in reputationByKey)
        {
            if (!SocialRumorUtility.MatchesFacilityKey(building, entry.Key))
            {
                continue;
            }

            sum += entry.Value;
            count++;
        }

        return count > 0 ? Mathf.Clamp(sum / count, -1f, 1f) : 0f;
    }

    public void Clear()
    {
        rumors.Clear();
        reputationByKey.Clear();
        SyncDebugProjection();
    }

    public void SyncDebugProjection()
    {
        debugProjection.Clear();
        foreach (KeyValuePair<string, float> entry in reputationByKey)
        {
            debugProjection.Add(new SocialMemoryFloat(entry.Key, entry.Value));
        }
    }

    private void PruneExpired(float blend)
    {
        float now = gameClock.Time;
        bool removed = false;
        for (int i = rumors.Count - 1; i >= 0; i--)
        {
            if (rumors[i] == null || rumors[i].IsExpiredAt(now))
            {
                rumors.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
        {
            Rebuild(blend);
        }
    }

    private void Rebuild(float blend)
    {
        float now = gameClock.Time;
        reputationByKey.Clear();
        foreach (SocialRumor rumor in rumors)
        {
            if (rumor == null
                || rumor.IsExpiredAt(now)
                || rumor.targetType != SocialRumorTargetType.Facility)
            {
                continue;
            }

            ApplyEntry(rumor, blend);
        }

        SyncDebugProjection();
    }

    private void ApplyEntry(SocialRumor rumor, float blend)
    {
        foreach (string key in SocialRumorUtility.GetFacilityKeys(rumor))
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            float current = reputationByKey.TryGetValue(key, out float value) ? value : 0f;
            reputationByKey[key] = Mathf.Clamp(
                Mathf.Lerp(current, rumor.sentiment, blend),
                -1f,
                1f);
        }
    }
}
