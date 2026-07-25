using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class DungeonSaveSectionEnvelope
{
    public string sectionId = string.Empty;
    public int sectionVersion = 1;
    public DungeonSaveRestorePhase restorePhase = DungeonSaveRestorePhase.RuntimeState;
    public string payloadJson = string.Empty;
}

public enum DungeonSaveRestorePhase
{
    Foundation = 100,
    World = 200,
    Characters = 300,
    Items = 400,
    RuntimeState = 500,
    LateRuntimeState = 600,
    Presentation = 700
}

public interface IDungeonSaveSection
{
    string SectionId { get; }
    int SectionVersion { get; }
    DungeonSaveRestorePhase RestorePhase { get; }
    IReadOnlyList<string> DependsOn { get; }
    string Capture();
    void Restore(string payloadJson, int sectionVersion, DungeonGameRestoreReport report);
}

public interface IDungeonSaveSectionRegistry
{
    IReadOnlyList<IDungeonSaveSection> OrderedSections { get; }
    List<DungeonSaveSectionEnvelope> CaptureAll();
    bool RestoreAll(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report);
    bool TryGetEnvelope(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        out DungeonSaveSectionEnvelope envelope);
}

public sealed class DungeonSaveSectionRegistry : IDungeonSaveSectionRegistry
{
    private readonly Dictionary<string, IDungeonSaveSection> byId;
    private readonly IReadOnlyList<IDungeonSaveSection> orderedSections;

    public DungeonSaveSectionRegistry(IEnumerable<IDungeonSaveSection> sections)
    {
        IDungeonSaveSection[] source = sections?
            .Where(section => section != null)
            .ToArray() ?? Array.Empty<IDungeonSaveSection>();

        byId = new Dictionary<string, IDungeonSaveSection>(StringComparer.Ordinal);
        foreach (IDungeonSaveSection section in source)
        {
            string sectionId = NormalizeId(section.SectionId);
            if (sectionId.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Save section {section.GetType().Name} has an empty id.");
            }

            if (section.SectionVersion <= 0)
            {
                throw new InvalidOperationException(
                    $"Save section '{sectionId}' has invalid version {section.SectionVersion}.");
            }

            if (!byId.TryAdd(sectionId, section))
            {
                throw new InvalidOperationException($"Duplicate save section id '{sectionId}'.");
            }
        }

        orderedSections = TopologicalSort(source);
    }

    public IReadOnlyList<IDungeonSaveSection> OrderedSections => orderedSections;

    public List<DungeonSaveSectionEnvelope> CaptureAll()
    {
        return orderedSections.Select(section => new DungeonSaveSectionEnvelope
        {
            sectionId = section.SectionId,
            sectionVersion = section.SectionVersion,
            restorePhase = section.RestorePhase,
            payloadJson = section.Capture() ?? string.Empty
        }).ToList();
    }

    public bool RestoreAll(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        Dictionary<string, DungeonSaveSectionEnvelope> savedById =
            new Dictionary<string, DungeonSaveSectionEnvelope>(StringComparer.Ordinal);
        foreach (DungeonSaveSectionEnvelope envelope in envelopes
                     ?? Array.Empty<DungeonSaveSectionEnvelope>())
        {
            if (envelope == null)
            {
                continue;
            }

            string sectionId = NormalizeId(envelope.sectionId);
            if (sectionId.Length == 0)
            {
                report.AddError("V15 save contains a section with an empty id.");
                continue;
            }

            if (!savedById.TryAdd(sectionId, envelope))
            {
                report.AddError($"V15 save contains duplicate section '{sectionId}'.");
            }
        }

        if (!report.Success)
        {
            return false;
        }

        foreach (IDungeonSaveSection section in orderedSections)
        {
            if (!savedById.TryGetValue(section.SectionId, out DungeonSaveSectionEnvelope envelope))
            {
                report.AddError($"V15 save is missing required section '{section.SectionId}'.");
                continue;
            }

            try
            {
                section.Restore(envelope.payloadJson, envelope.sectionVersion, report);
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"Failed to restore section '{section.SectionId}': {exception.Message}");
            }
        }

        foreach (string unknownId in savedById.Keys.Where(id => !byId.ContainsKey(id)))
        {
            report.AddWarning($"Unknown V15 save section '{unknownId}' was ignored.");
        }

        return report.Success;
    }

    public bool TryGetEnvelope(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        out DungeonSaveSectionEnvelope envelope)
    {
        string normalizedId = NormalizeId(sectionId);
        envelope = envelopes?.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                NormalizeId(candidate.sectionId),
                normalizedId,
                StringComparison.Ordinal));
        return envelope != null;
    }

    private IReadOnlyList<IDungeonSaveSection> TopologicalSort(
        IReadOnlyList<IDungeonSaveSection> sections)
    {
        List<IDungeonSaveSection> result = new List<IDungeonSaveSection>(sections.Count);
        Dictionary<string, VisitState> states =
            new Dictionary<string, VisitState>(StringComparer.Ordinal);

        foreach (IDungeonSaveSection section in sections
                     .OrderBy(item => item.RestorePhase)
                     .ThenBy(item => item.SectionId, StringComparer.Ordinal))
        {
            Visit(section, states, result);
        }

        return result;
    }

    private void Visit(
        IDungeonSaveSection section,
        IDictionary<string, VisitState> states,
        ICollection<IDungeonSaveSection> result)
    {
        string sectionId = NormalizeId(section.SectionId);
        if (states.TryGetValue(sectionId, out VisitState state))
        {
            if (state == VisitState.Visiting)
            {
                throw new InvalidOperationException(
                    $"Save section dependency cycle includes '{sectionId}'.");
            }

            return;
        }

        states[sectionId] = VisitState.Visiting;
        foreach (string dependencyId in section.DependsOn ?? Array.Empty<string>())
        {
            string normalizedDependency = NormalizeId(dependencyId);
            if (!byId.TryGetValue(normalizedDependency, out IDungeonSaveSection dependency))
            {
                throw new InvalidOperationException(
                    $"Save section '{sectionId}' depends on missing section '{normalizedDependency}'.");
            }

            if (dependency.RestorePhase > section.RestorePhase)
            {
                throw new InvalidOperationException(
                    $"Save section '{sectionId}' depends on later phase section '{normalizedDependency}'.");
            }

            Visit(dependency, states, result);
        }

        states[sectionId] = VisitState.Visited;
        result.Add(section);
    }

    private static string NormalizeId(string sectionId)
    {
        return sectionId?.Trim() ?? string.Empty;
    }

    private enum VisitState
    {
        Visiting,
        Visited
    }
}
