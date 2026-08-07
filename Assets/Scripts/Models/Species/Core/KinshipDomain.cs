using System;
using System.Collections.Generic;
using System.Linq;

public enum KinshipLinkKind
{
    GeneticParent = 0,
    AdoptiveParent = 1,
    Partner = 2,
    Guardian = 3
}

public enum KinshipRestriction
{
    None = 0,
    Self = 1,
    Ancestor = 2,
    Sibling = 3,
    FirstCousinOrCloser = 4,
    ExistingPartner = 5,
    ParentLimit = 6,
    AdoptionCycle = 7
}

[Serializable]
public sealed class CharacterKinshipLinkSaveData
{
    public string sourceCharacterId = string.Empty;
    public string targetCharacterId = string.Empty;
    public KinshipLinkKind kind;
}

[Serializable]
public sealed class CharacterTombstoneSaveData
{
    public string characterId = string.Empty;
    public string phenotypeSpeciesId = string.Empty;
    public int birthAbsoluteDay;
    public int deathAbsoluteDay;
    public bool famous;
    public string householdId = string.Empty;
    public int generation;
}

[Serializable]
public sealed class LineageSummarySaveData
{
    public string householdId = string.Empty;
    public int generation;
    public int archivedCharacterCount;
    public int earliestBirthDay;
    public int latestDeathDay;
}

[Serializable]
public sealed class KinshipWorldSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<CharacterKinshipLinkSaveData> links = new();
    public List<CharacterTombstoneSaveData> tombstones = new();
    public List<LineageSummarySaveData> lineageSummaries = new();
}

public readonly struct KinshipLink : IEquatable<KinshipLink>
{
    public KinshipLink(CharacterId source, CharacterId target, KinshipLinkKind kind)
    {
        if (!source.IsValid || !target.IsValid || source.Equals(target))
            throw new ArgumentException("Kinship links require two different valid characters.");
        if (kind == KinshipLinkKind.Partner
            && string.CompareOrdinal(source.Value, target.Value) > 0)
        {
            (source, target) = (target, source);
        }
        Source = source;
        Target = target;
        Kind = kind;
    }

    public CharacterId Source { get; }
    public CharacterId Target { get; }
    public KinshipLinkKind Kind { get; }
    public bool Equals(KinshipLink other) =>
        Source.Equals(other.Source) && Target.Equals(other.Target) && Kind == other.Kind;
    public override bool Equals(object obj) => obj is KinshipLink other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Source, Target, Kind);
}

public interface IKinshipQuery
{
    IReadOnlyList<CharacterId> GetParents(CharacterId child, bool includeAdoptive);
    IReadOnlyList<CharacterId> GetChildren(CharacterId parent, bool includeAdoptive);
    int GetGeneration(CharacterId characterId);
    bool IsAncestor(CharacterId possibleAncestor, CharacterId descendant, int maximumDepth);
    bool IsSibling(CharacterId left, CharacterId right);
    KinshipRestriction GetPartnershipOrReproductionRestriction(
        CharacterId left,
        CharacterId right);
    CharacterId GetPartner(CharacterId characterId);
    CharacterId GetGuardian(CharacterId child);
    bool TryGetTombstone(
        CharacterId characterId,
        out CharacterTombstoneSaveData tombstone);
}

public interface IKinshipCommand
{
    void AddParent(CharacterId child, CharacterId parent, bool adoptive);
    void SetPartner(CharacterId left, CharacterId right);
    void ClearPartner(CharacterId characterId);
    void SetGuardian(CharacterId child, CharacterId guardian);
    void ArchiveDeath(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        int birthAbsoluteDay,
        int deathAbsoluteDay,
        bool famous,
        HouseholdId householdId,
        int generation);
    void ArchiveColdData(int currentAbsoluteDay, IReadOnlyCollection<CharacterId> livingCharacters);
}

public sealed class CharacterKinshipAggregate : IKinshipQuery, IKinshipCommand
{
    public const int ParentSearchDepth = 3;
    public const int ActiveParentDepth = 2;
    public const int RecentDeathDays = 120;
    public const int MaximumUnrelatedFamousTombstones = 512;

    private readonly HashSet<KinshipLink> links = new();
    private readonly Dictionary<CharacterId, CharacterTombstoneSaveData> tombstones = new();
    private readonly Dictionary<string, LineageSummarySaveData> summaries =
        new(StringComparer.Ordinal);

    public bool TryGetTombstone(
        CharacterId characterId,
        out CharacterTombstoneSaveData tombstone) =>
        tombstones.TryGetValue(characterId, out tombstone);

    public IReadOnlyList<CharacterId> GetParents(
        CharacterId child,
        bool includeAdoptive)
    {
        return links
            .Where(link => link.Source.Equals(child)
                && (link.Kind == KinshipLinkKind.GeneticParent
                    || includeAdoptive && link.Kind == KinshipLinkKind.AdoptiveParent))
            .Select(link => link.Target)
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<CharacterId> GetChildren(
        CharacterId parent,
        bool includeAdoptive)
    {
        return links
            .Where(link => link.Target.Equals(parent)
                && (link.Kind == KinshipLinkKind.GeneticParent
                    || includeAdoptive && link.Kind == KinshipLinkKind.AdoptiveParent))
            .Select(link => link.Source)
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public int GetGeneration(CharacterId characterId)
    {
        if (!characterId.IsValid) return 0;
        return ResolveGeneration(characterId, new HashSet<CharacterId>());
    }

    public bool IsAncestor(
        CharacterId possibleAncestor,
        CharacterId descendant,
        int maximumDepth)
    {
        if (!possibleAncestor.IsValid || !descendant.IsValid || maximumDepth <= 0)
            return false;
        HashSet<CharacterId> visited = new() { descendant };
        Queue<(CharacterId Id, int Depth)> queue = new();
        queue.Enqueue((descendant, 0));
        while (queue.Count > 0)
        {
            (CharacterId current, int depth) = queue.Dequeue();
            if (depth >= maximumDepth) continue;
            foreach (CharacterId parent in GetParents(current, includeAdoptive: true))
            {
                if (parent.Equals(possibleAncestor)) return true;
                if (visited.Add(parent)) queue.Enqueue((parent, depth + 1));
            }
        }
        return false;
    }

    public bool IsSibling(CharacterId left, CharacterId right)
    {
        if (!left.IsValid || !right.IsValid || left.Equals(right)) return false;
        HashSet<CharacterId> leftParents = GetParents(left, true).ToHashSet();
        return GetParents(right, true).Any(leftParents.Contains);
    }

    public KinshipRestriction GetPartnershipOrReproductionRestriction(
        CharacterId left,
        CharacterId right)
    {
        if (!left.IsValid || !right.IsValid || left.Equals(right))
            return KinshipRestriction.Self;
        if (IsAncestor(left, right, ParentSearchDepth)
            || IsAncestor(right, left, ParentSearchDepth))
            return KinshipRestriction.Ancestor;
        if (IsSibling(left, right)) return KinshipRestriction.Sibling;
        if (HaveCommonAncestor(left, right, ParentSearchDepth))
            return KinshipRestriction.FirstCousinOrCloser;
        CharacterId leftPartner = GetPartner(left);
        CharacterId rightPartner = GetPartner(right);
        if (leftPartner.IsValid && !leftPartner.Equals(right)
            || rightPartner.IsValid && !rightPartner.Equals(left))
            return KinshipRestriction.ExistingPartner;
        return KinshipRestriction.None;
    }

    public CharacterId GetPartner(CharacterId characterId)
    {
        KinshipLink match = links.FirstOrDefault(link =>
            link.Kind == KinshipLinkKind.Partner
            && (link.Source.Equals(characterId) || link.Target.Equals(characterId)));
        if (match.Equals(default(KinshipLink))) return default;
        return match.Source.Equals(characterId) ? match.Target : match.Source;
    }

    public CharacterId GetGuardian(CharacterId child) =>
        links.FirstOrDefault(link => link.Kind == KinshipLinkKind.Guardian
                && link.Source.Equals(child))
            .Target;

    public void AddParent(CharacterId child, CharacterId parent, bool adoptive)
    {
        KinshipLinkKind kind = adoptive
            ? KinshipLinkKind.AdoptiveParent
            : KinshipLinkKind.GeneticParent;
        if (child.Equals(parent)) throw new InvalidOperationException("A character cannot parent itself.");
        int existing = links.Count(link => link.Source.Equals(child) && link.Kind == kind);
        if (existing >= 2) throw new InvalidOperationException($"{kind} limit is two.");
        if (IsAncestor(child, parent, int.MaxValue))
            throw new InvalidOperationException("Parent link would create an ancestry cycle.");
        KinshipLink link = new(child, parent, kind);
        if (!links.Add(link)) throw new InvalidOperationException("Duplicate kinship link.");
    }

    public void SetPartner(CharacterId left, CharacterId right)
    {
        KinshipRestriction restriction = GetPartnershipOrReproductionRestriction(left, right);
        if (restriction != KinshipRestriction.None)
            throw new InvalidOperationException($"Partnership is forbidden: {restriction}.");
        links.Add(new KinshipLink(left, right, KinshipLinkKind.Partner));
    }

    public void ClearPartner(CharacterId characterId)
    {
        links.RemoveWhere(link => link.Kind == KinshipLinkKind.Partner
            && (link.Source.Equals(characterId) || link.Target.Equals(characterId)));
    }

    public void SetGuardian(CharacterId child, CharacterId guardian)
    {
        if (!child.IsValid || !guardian.IsValid || child.Equals(guardian))
            throw new InvalidOperationException("Guardian assignment requires different valid characters.");
        links.RemoveWhere(link => link.Kind == KinshipLinkKind.Guardian
            && link.Source.Equals(child));
        links.Add(new KinshipLink(child, guardian, KinshipLinkKind.Guardian));
    }

    public void ArchiveDeath(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        int birthAbsoluteDay,
        int deathAbsoluteDay,
        bool famous,
        HouseholdId householdId,
        int generation)
    {
        if (!characterId.IsValid || !phenotypeSpeciesId.IsValid
            || deathAbsoluteDay < 1 || deathAbsoluteDay < birthAbsoluteDay)
            throw new InvalidOperationException("Tombstone data is invalid.");
        tombstones[characterId] = new CharacterTombstoneSaveData
        {
            characterId = characterId.Value,
            phenotypeSpeciesId = phenotypeSpeciesId.Value,
            birthAbsoluteDay = birthAbsoluteDay,
            deathAbsoluteDay = deathAbsoluteDay,
            famous = famous,
            householdId = householdId.IsValid ? householdId.Value : string.Empty,
            generation = Math.Max(0, generation)
        };
        TrimUnrelatedFamousTombstones();
    }

    public void ArchiveColdData(
        int currentAbsoluteDay,
        IReadOnlyCollection<CharacterId> livingCharacters)
    {
        if (currentAbsoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(currentAbsoluteDay));
        if (livingCharacters == null)
            throw new ArgumentNullException(nameof(livingCharacters));

        HashSet<CharacterId> living = livingCharacters
            .Where(id => id.IsValid)
            .ToHashSet();
        HashSet<KinshipLink> requiredParentLinks = new();
        HashSet<CharacterId> requiredAncestors = new();
        foreach (CharacterId character in living)
        {
            Queue<(CharacterId Id, int Depth)> queue = new();
            HashSet<CharacterId> visited = new() { character };
            queue.Enqueue((character, 0));
            while (queue.Count > 0)
            {
                (CharacterId current, int depth) = queue.Dequeue();
                if (depth >= ParentSearchDepth) continue;
                foreach (KinshipLink link in links.Where(link =>
                             link.Source.Equals(current)
                             && link.Kind is KinshipLinkKind.GeneticParent
                                 or KinshipLinkKind.AdoptiveParent))
                {
                    requiredParentLinks.Add(link);
                    requiredAncestors.Add(link.Target);
                    if (visited.Add(link.Target))
                        queue.Enqueue((link.Target, depth + 1));
                }
            }
        }

        HashSet<CharacterId> recentDeaths = tombstones
            .Where(pair => currentAbsoluteDay - pair.Value.deathAbsoluteDay
                <= RecentDeathDays)
            .Select(pair => pair.Key)
            .ToHashSet();
        HashSet<CharacterId> oldDeaths = tombstones.Keys
            .Where(id => !recentDeaths.Contains(id))
            .ToHashSet();
        links.RemoveWhere(link => link.Kind switch
        {
            KinshipLinkKind.GeneticParent or KinshipLinkKind.AdoptiveParent =>
                !requiredParentLinks.Contains(link)
                && !recentDeaths.Contains(link.Source),
            KinshipLinkKind.Partner =>
                oldDeaths.Contains(link.Source) || oldDeaths.Contains(link.Target),
            KinshipLinkKind.Guardian =>
                !living.Contains(link.Source) || oldDeaths.Contains(link.Target),
            _ => true
        });

        CharacterId[] removable = tombstones
            .Where(pair => currentAbsoluteDay - pair.Value.deathAbsoluteDay > RecentDeathDays
                && !requiredAncestors.Contains(pair.Key)
                && !pair.Value.famous)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (CharacterId id in removable)
        {
            AddToLineageSummary(tombstones[id]);
            tombstones.Remove(id);
        }
    }

    public KinshipWorldSaveData Capture() => new()
    {
        links = links
            .OrderBy(link => link.Kind)
            .ThenBy(link => link.Source.Value, StringComparer.Ordinal)
            .ThenBy(link => link.Target.Value, StringComparer.Ordinal)
            .Select(link => new CharacterKinshipLinkSaveData
            {
                sourceCharacterId = link.Source.Value,
                targetCharacterId = link.Target.Value,
                kind = link.Kind
            }).ToList(),
        tombstones = tombstones.Values
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .ToList(),
        lineageSummaries = summaries.Values
            .OrderBy(value => value.householdId, StringComparer.Ordinal)
            .ThenBy(value => value.generation)
            .ToList()
    };

    public static CharacterKinshipAggregate Restore(KinshipWorldSaveData data)
    {
        if (data == null || data.version != KinshipWorldSaveData.CurrentVersion
            || data.links == null || data.tombstones == null || data.lineageSummaries == null)
            throw new InvalidOperationException("Kinship payload is incomplete or unsupported.");
        CharacterKinshipAggregate result = new();
        foreach (CharacterKinshipLinkSaveData link in data.links)
        {
            if (link == null) throw new InvalidOperationException("Null kinship link.");
            CharacterId source = new(link.sourceCharacterId);
            CharacterId target = new(link.targetCharacterId);
            switch (link.kind)
            {
                case KinshipLinkKind.GeneticParent: result.AddParent(source, target, false); break;
                case KinshipLinkKind.AdoptiveParent: result.AddParent(source, target, true); break;
                case KinshipLinkKind.Partner: result.SetPartner(source, target); break;
                case KinshipLinkKind.Guardian: result.SetGuardian(source, target); break;
                default: throw new InvalidOperationException("Unknown kinship kind.");
            }
        }
        foreach (CharacterTombstoneSaveData tombstone in data.tombstones)
        {
            if (tombstone == null) throw new InvalidOperationException("Null tombstone.");
            result.ArchiveDeath(
                new CharacterId(tombstone.characterId),
                new CharacterSpeciesId(tombstone.phenotypeSpeciesId),
                tombstone.birthAbsoluteDay,
                tombstone.deathAbsoluteDay,
                tombstone.famous,
                string.IsNullOrWhiteSpace(tombstone.householdId)
                    ? default
                    : new HouseholdId(tombstone.householdId),
                tombstone.generation);
        }
        foreach (LineageSummarySaveData summary in data.lineageSummaries)
        {
            if (summary == null || string.IsNullOrWhiteSpace(summary.householdId))
                throw new InvalidOperationException("Invalid lineage summary.");
            string key = summary.householdId + ":" + summary.generation;
            if (!result.summaries.TryAdd(key, summary))
                throw new InvalidOperationException("Duplicate lineage summary.");
        }
        return result;
    }

    private bool HaveCommonAncestor(CharacterId left, CharacterId right, int depth)
    {
        HashSet<CharacterId> leftAncestors = GetAncestors(left, depth);
        return GetAncestors(right, depth).Any(leftAncestors.Contains);
    }

    private HashSet<CharacterId> GetAncestors(CharacterId character, int maximumDepth)
    {
        HashSet<CharacterId> result = new();
        Queue<(CharacterId Id, int Depth)> queue = new();
        queue.Enqueue((character, 0));
        while (queue.Count > 0)
        {
            (CharacterId current, int depth) = queue.Dequeue();
            if (depth >= maximumDepth) continue;
            foreach (CharacterId parent in GetParents(current, true))
            {
                if (result.Add(parent)) queue.Enqueue((parent, depth + 1));
            }
        }
        return result;
    }

    private int ResolveGeneration(CharacterId characterId, HashSet<CharacterId> path)
    {
        if (!path.Add(characterId))
            throw new InvalidOperationException("Kinship ancestry contains a cycle.");
        CharacterId[] parents = GetParents(characterId, includeAdoptive: true).ToArray();
        int generation = parents.Length == 0
            ? 0
            : parents.Max(parent => ResolveGeneration(parent, path)) + 1;
        path.Remove(characterId);
        return generation;
    }

    private void TrimUnrelatedFamousTombstones()
    {
        HashSet<CharacterId> referenced = links
            .SelectMany(link => new[] { link.Source, link.Target })
            .ToHashSet();
        CharacterId[] famous = tombstones
            .Where(pair => pair.Value.famous && !referenced.Contains(pair.Key))
            .OrderByDescending(pair => pair.Value.deathAbsoluteDay)
            .ThenBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Skip(MaximumUnrelatedFamousTombstones)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (CharacterId id in famous) tombstones.Remove(id);
    }

    private void AddToLineageSummary(CharacterTombstoneSaveData tombstone)
    {
        string householdId = string.IsNullOrWhiteSpace(tombstone.householdId)
            ? "household:unassigned"
            : tombstone.householdId;
        string key = householdId + ":" + tombstone.generation;
        if (!summaries.TryGetValue(key, out LineageSummarySaveData summary))
        {
            summary = new LineageSummarySaveData
            {
                householdId = householdId,
                generation = tombstone.generation,
                earliestBirthDay = tombstone.birthAbsoluteDay,
                latestDeathDay = tombstone.deathAbsoluteDay
            };
            summaries.Add(key, summary);
        }
        summary.archivedCharacterCount++;
        summary.earliestBirthDay = Math.Min(summary.earliestBirthDay, tombstone.birthAbsoluteDay);
        summary.latestDeathDay = Math.Max(summary.latestDeathDay, tombstone.deathAbsoluteDay);
    }
}
