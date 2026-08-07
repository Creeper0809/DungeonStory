#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VContainer;

public static class V19SaveAtomicityDebugScenarios
{
    private static readonly string[] V19AggregateSectionIds =
    {
        FoundationSessionSaveSection.Id,
        CalendarClimateSaveSection.Id,
        CharacterLifeSaveSection.Id,
        KinshipHouseholdSaveSection.Id,
        ReproductionSaveSection.Id,
        CropEcologySaveSection.Id,
        PopulationHealthSaveSection.Id,
        CharacterCareerSaveSection.Id,
        CharacterPsychosocialSaveSection.Id
    };

    public static string RunLoaded()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException(
                "V19 save atomicity verification requires a loaded PlayMode world.");

        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate =>
                candidate != null && candidate.Container != null);
        if (scope?.Container == null)
            throw new InvalidOperationException("Dungeon runtime scope is unavailable.");

        IDungeonGameSaveService saves = scope.Container.Resolve<IDungeonGameSaveService>();
        IGameSpeedController speed = scope.Container.Resolve<IGameSpeedController>();
        DungeonRuntimeAggregateRootStore roots =
            scope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
        bool wasPaused = speed.IsPaused;
        speed.SetPaused(true);
        try
        {
            DungeonGameSaveData baseline = Clone(saves, saves.Capture());
            Dictionary<string, string> expected = CaptureV19Payloads(baseline);

            DungeonGameSaveData roundTrip = Clone(saves, baseline);
            if (!saves.TryRestore(roundTrip, out DungeonGameRestoreReport validReport)
                || !validReport.Success)
            {
                throw new InvalidOperationException(
                    "V19 full round trip failed: "
                    + string.Join(" | ", validReport.Errors));
            }
            RequireSame(expected, CaptureV19Payloads(saves.Capture()), "valid round trip");

            DungeonGameSaveData invalid = Clone(saves, baseline);
            DungeonSaveSectionEnvelope psychosocial = invalid.sections.FirstOrDefault(value =>
                value != null
                && string.Equals(
                    value.sectionId,
                    CharacterPsychosocialSaveSection.Id,
                    StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    "Captured V19 save has no psychosocial section.");
            psychosocial.payloadJson = "{}";
            invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);

            Dictionary<string, string> beforeFailure =
                CaptureV19Payloads(saves.Capture());
            int revisionBeforeFailure = roots.PublishedRestoreRevision;
            bool accepted = saves.TryRestore(invalid, out DungeonGameRestoreReport failedReport);
            if (accepted || failedReport.Success || failedReport.Errors.Count == 0)
                throw new InvalidOperationException(
                    "Invalid final V19 section was accepted.");
            if (roots.PublishedRestoreRevision != revisionBeforeFailure)
                throw new InvalidOperationException(
                    "Failed V19 restore published the aggregate root.");
            RequireSame(
                beforeFailure,
                CaptureV19Payloads(saves.Capture()),
                "failed final-section restore");

            return "V19_SAVE_ROUND_TRIP_AND_LATE_FAILURE=PASS; sections="
                + V19AggregateSectionIds.Length
                + "; revision="
                + roots.PublishedRestoreRevision;
        }
        finally
        {
            speed.SetPaused(wasPaused);
        }
    }

    private static DungeonGameSaveData Clone(
        IDungeonGameSaveService saves,
        DungeonGameSaveData source) =>
        saves.FromJson(saves.ToJson(source));

    private static Dictionary<string, string> CaptureV19Payloads(
        DungeonGameSaveData save)
    {
        Dictionary<string, DungeonSaveSectionEnvelope> byId =
            (save?.sections ?? new List<DungeonSaveSectionEnvelope>())
            .Where(value => value != null)
            .ToDictionary(value => value.sectionId, StringComparer.Ordinal);
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (string sectionId in V19AggregateSectionIds)
        {
            if (!byId.TryGetValue(sectionId, out DungeonSaveSectionEnvelope envelope))
                throw new InvalidOperationException(
                    $"Captured V19 save is missing required section '{sectionId}'.");
            result.Add(sectionId, envelope.payloadJson ?? string.Empty);
        }
        return result;
    }

    private static void RequireSame(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual,
        string phase)
    {
        foreach (string sectionId in V19AggregateSectionIds)
        {
            expected.TryGetValue(sectionId, out string left);
            actual.TryGetValue(sectionId, out string right);
            if (left == null
                || right == null
                || !string.Equals(left, right, StringComparison.Ordinal))
            {
                if (string.Equals(
                        sectionId,
                        CharacterLifeSaveSection.Id,
                        StringComparison.Ordinal))
                {
                    Directory.CreateDirectory("Artifacts/QA");
                    File.WriteAllText(
                        "Artifacts/QA/v19-life-before-roundtrip.json",
                        left ?? string.Empty);
                    File.WriteAllText(
                        "Artifacts/QA/v19-life-after-roundtrip.json",
                        right ?? string.Empty);
                }

                int firstDifference = FindFirstDifference(left, right);
                throw new InvalidOperationException(
                    $"V19 section '{sectionId}' changed during {phase}; "
                    + $"firstDifference={firstDifference}; "
                    + $"beforeLength={left?.Length ?? 0}; "
                    + $"afterLength={right?.Length ?? 0}.");
            }
        }
    }

    private static int FindFirstDifference(string left, string right)
    {
        left ??= string.Empty;
        right ??= string.Empty;
        int shared = Math.Min(left.Length, right.Length);
        for (int index = 0; index < shared; index++)
        {
            if (left[index] != right[index])
            {
                return index;
            }
        }

        return left.Length == right.Length ? -1 : shared;
    }
}
#endif
