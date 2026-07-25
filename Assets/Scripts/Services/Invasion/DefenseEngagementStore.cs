using System;
using System.Collections.Generic;

public interface IDefenseEngagementStore
{
    IReadOnlyList<DefenseEngagement> Engagements { get; }
    string AllocateId();
    void Add(DefenseEngagement engagement);
    bool Remove(DefenseEngagement engagement);
    void ClearEngagements();
    bool HasRetreated(string characterId);
    void MarkRetreated(string characterId);
    void ClearRetreated(string characterId);
    void ClearAll();
}

public sealed class DefenseEngagementStore : IDefenseEngagementStore
{
    private const string IdPrefix = "defense-engagement:";

    private readonly List<DefenseEngagement> engagements =
        new List<DefenseEngagement>();
    private readonly HashSet<string> retreatedCharacterIds =
        new HashSet<string>(StringComparer.Ordinal);
    private IReadOnlyList<DefenseEngagement> engagementsView;
    private int sequence;

    public IReadOnlyList<DefenseEngagement> Engagements =>
        engagementsView ??= ReadOnlyView.List(engagements);

    public string AllocateId()
    {
        string id;
        do
        {
            id = $"{IdPrefix}{++sequence}";
        }
        while (engagements.Exists(engagement =>
            engagement != null
            && string.Equals(engagement.Id, id, StringComparison.Ordinal)));

        return id;
    }

    public void Add(DefenseEngagement engagement)
    {
        if (engagement == null)
        {
            throw new ArgumentNullException(nameof(engagement));
        }

        if (string.IsNullOrWhiteSpace(engagement.Id))
        {
            engagement.Id = AllocateId();
        }

        if (engagements.Exists(existing =>
            existing != null
            && string.Equals(existing.Id, engagement.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Duplicate defense engagement id '{engagement.Id}'.");
        }

        ObserveSequence(engagement.Id);
        engagements.Add(engagement);
    }

    public bool Remove(DefenseEngagement engagement)
    {
        return engagement != null && engagements.Remove(engagement);
    }

    public void ClearEngagements()
    {
        engagements.Clear();
    }

    public bool HasRetreated(string characterId)
    {
        return !string.IsNullOrWhiteSpace(characterId)
            && retreatedCharacterIds.Contains(characterId);
    }

    public void MarkRetreated(string characterId)
    {
        if (!string.IsNullOrWhiteSpace(characterId))
        {
            retreatedCharacterIds.Add(characterId);
        }
    }

    public void ClearRetreated(string characterId)
    {
        if (!string.IsNullOrWhiteSpace(characterId))
        {
            retreatedCharacterIds.Remove(characterId);
        }
    }

    public void ClearAll()
    {
        engagements.Clear();
        retreatedCharacterIds.Clear();
        sequence = 0;
    }

    private void ObserveSequence(string id)
    {
        if (!id.StartsWith(IdPrefix, StringComparison.Ordinal)
            || !int.TryParse(id.Substring(IdPrefix.Length), out int parsed))
        {
            return;
        }

        sequence = Math.Max(sequence, parsed);
    }
}
