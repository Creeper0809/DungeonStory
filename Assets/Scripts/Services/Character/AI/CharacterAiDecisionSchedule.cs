using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class CharacterAiDecisionSchedule
{
    private readonly HashSet<CharacterActor> registeredActors;
    private readonly Dictionary<CharacterActor, float> dueTimes;
    private readonly Dictionary<CharacterActor, int> versions =
        new Dictionary<CharacterActor, int>();
    private readonly List<CharacterAiScheduledDecision> heap =
        new List<CharacterAiScheduledDecision>();
    private long sequence;

    public CharacterAiDecisionSchedule(
        HashSet<CharacterActor> registeredActors,
        Dictionary<CharacterActor, float> dueTimes)
    {
        this.registeredActors = registeredActors
            ?? throw new ArgumentNullException(nameof(registeredActors));
        this.dueTimes = dueTimes
            ?? throw new ArgumentNullException(nameof(dueTimes));
    }

    // The heap can contain invalidated entries after an earlier reschedule.
    // dueTimes is the authority for actors that actually own a live request.
    public int Count => dueTimes.Count;

    public void Clear()
    {
        versions.Clear();
        heap.Clear();
        dueTimes.Clear();
        sequence = 0L;
    }

    public void Remove(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        dueTimes.Remove(actor);
        versions.Remove(actor);
    }

    public void Schedule(CharacterActor actor, float dueTime)
    {
        if (actor == null || !registeredActors.Contains(actor))
        {
            return;
        }

        if (dueTimes.TryGetValue(actor, out float existingDueTime)
            && existingDueTime <= dueTime + 0.0001f)
        {
            return;
        }

        int version = versions.TryGetValue(actor, out int currentVersion)
            ? currentVersion + 1
            : 1;
        versions[actor] = version;
        dueTimes[actor] = dueTime;
        Push(new CharacterAiScheduledDecision(
            actor,
            dueTime,
            version,
            sequence++));
        CompactIfNeeded();
    }

    public bool TryPeekDue(
        float now,
        out CharacterAiScheduledDecision scheduled)
    {
        scheduled = default;
        while (heap.Count > 0)
        {
            CharacterAiScheduledDecision next = heap[0];
            if (!IsCurrent(next))
            {
                Pop();
                continue;
            }

            if (next.DueTime > now)
            {
                return false;
            }

            scheduled = next;
            return true;
        }

        return false;
    }

    public bool TryTakeDue(float now, out CharacterActor actor)
    {
        actor = null;
        while (heap.Count > 0)
        {
            CharacterAiScheduledDecision next = heap[0];
            if (next.DueTime > now)
            {
                return false;
            }

            Pop();
            if (!IsCurrent(next))
            {
                continue;
            }

            dueTimes.Remove(next.Actor);
            actor = next.Actor;
            return true;
        }

        return false;
    }

    private bool IsCurrent(CharacterAiScheduledDecision entry)
    {
        return entry.Actor != null
            && registeredActors.Contains(entry.Actor)
            && versions.TryGetValue(entry.Actor, out int activeVersion)
            && activeVersion == entry.Version;
    }

    private void Push(CharacterAiScheduledDecision entry)
    {
        int index = heap.Count;
        heap.Add(entry);
        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            if (!entry.IsEarlierThan(heap[parent]))
            {
                break;
            }

            heap[index] = heap[parent];
            index = parent;
        }

        heap[index] = entry;
    }

    private void Pop()
    {
        int lastIndex = heap.Count - 1;
        CharacterAiScheduledDecision tail = heap[lastIndex];
        heap.RemoveAt(lastIndex);
        if (lastIndex == 0)
        {
            return;
        }

        int index = 0;
        while (true)
        {
            int left = (index << 1) + 1;
            if (left >= lastIndex)
            {
                break;
            }

            int right = left + 1;
            int child = right < lastIndex
                && heap[right].IsEarlierThan(heap[left])
                    ? right
                    : left;
            if (!heap[child].IsEarlierThan(tail))
            {
                break;
            }

            heap[index] = heap[child];
            index = child;
        }

        heap[index] = tail;
    }

    private void CompactIfNeeded()
    {
        int maximumUsefulEntries =
            Mathf.Max(128, registeredActors.Count * 4 + 128);
        if (heap.Count <= maximumUsefulEntries)
        {
            return;
        }

        heap.Clear();
        foreach (KeyValuePair<CharacterActor, float> pair in dueTimes)
        {
            CharacterActor actor = pair.Key;
            if (actor == null
                || !registeredActors.Contains(actor)
                || !versions.TryGetValue(actor, out int version))
            {
                continue;
            }

            Push(new CharacterAiScheduledDecision(
                actor,
                pair.Value,
                version,
                sequence++));
        }
    }
}

internal readonly struct CharacterAiScheduledDecision
{
    public CharacterAiScheduledDecision(
        CharacterActor actor,
        float dueTime,
        int version,
        long sequence)
    {
        Actor = actor;
        DueTime = dueTime;
        Version = version;
        Sequence = sequence;
    }

    public CharacterActor Actor { get; }
    public float DueTime { get; }
    public int Version { get; }
    public long Sequence { get; }

    public bool IsEarlierThan(CharacterAiScheduledDecision other)
    {
        return DueTime < other.DueTime
            || (DueTime == other.DueTime && Sequence < other.Sequence);
    }
}
