#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Exercises whole-save sealing through the real save service while keeping the
/// section boundary explicit and deliberately free of gameplay restore logic.
/// This is test-only integrity coverage, not a gameplay owner-restore fixture.
/// </summary>
public static class DungeonSavePayloadIntegrityDebugScenarios
{
    private const string ResearchSectionId = "research.blueprints";
    private const string PhysicalSectionId = "items.physical";
    private const string OutcomeSectionId = "world.gameplay-outcome-ledger";
    private const string LegacyIntegrityWarning = "LegacySnapshotIntegrityAbsent";
    private const int GroupCount = 10;

    public static string RunAll()
    {
        VerifyCanonicalRoundTripInvokesRegistry();
        VerifyResearchAndPhysicalPayloadMutationIsRejectedBeforeRestore();
        VerifyMixedSnapshotMetadataAndRootMutationAreRejectedBeforeRestore();
        VerifySectionAndManifestRowSwapIsRejectedBeforeRestore();
        VerifyUnsealedLegacySnapshotWarnsWithoutResealing();
        VerifyPartialAndUnknownIntegrityMetadataAreRejectedBeforeRestore();
        VerifyRestorePreflightCannotMutateSealedSnapshot();
        VerifyCapturedValidatorCannotMutateSealedSnapshot();
        VerifyCapturedValidatorCannotResealOrDowngradeSealedSnapshot();
        VerifyEmptyManifestIntegrityBoundary();
        return "PASS DUNGEON_SAVE_PAYLOAD_INTEGRITY; groups=" + GroupCount;
    }

    private static void VerifyCanonicalRoundTripInvokesRegistry()
    {
        Fixture fixture = CreateFixture();
        DungeonGameSaveData captured = CaptureJsonClone(fixture);
        Require(captured.manifest != null
                && captured.manifest.payloadHashVersion
                    == DungeonSavePayloadIntegrity.CurrentVersion
                && IsHash(captured.manifest.snapshotSha256)
                && captured.manifest.sections.Count == 3
                && captured.manifest.sections.All(section =>
                    section != null && IsHash(section.payloadSha256)),
            "Canonical capture did not seal every test section and the root manifest.");

        bool restored = fixture.Service.TryRestore(
            captured,
            out DungeonGameRestoreReport report);
        Require(restored && report.Success && fixture.Registry.RestoreCalls == 1
                && fixture.Registry.LiveSentinel.Contains("outcomeSequence", StringComparison.Ordinal),
            "Canonical sealed save did not reach the recording registry exactly once.");
    }

    private static void VerifyResearchAndPhysicalPayloadMutationIsRejectedBeforeRestore()
    {
        Fixture researchFixture = CreateFixture();
        DungeonGameSaveData researchProgressMutation =
            CaptureJsonClone(researchFixture);
        DungeonSaveSectionEnvelope research = Find(
            researchProgressMutation,
            ResearchSectionId);
        Require(research.payloadJson.Contains("\"outcomeSequence\":7", StringComparison.Ordinal),
            "Research integrity fixture lost its fixed outcome sequence.");
        research.payloadJson = "{\"outcomeSequence\":7,\"projectProgress\":5.0,\"taskId\":\"project:alpha\"}";
        Require(research.payloadJson.Contains("\"outcomeSequence\":7", StringComparison.Ordinal),
            "Research-progress mutation accidentally changed its outcome sequence.");
        RequireRejectedWithoutRestore(
            researchFixture,
            researchProgressMutation,
            "same-sequence research progress mutation");

        Fixture physicalFixture = CreateFixture();
        DungeonGameSaveData physicalWearMutation =
            CaptureJsonClone(physicalFixture);
        DungeonSaveSectionEnvelope physical = Find(
            physicalWearMutation,
            PhysicalSectionId);
        Require(physical.payloadJson.Contains("\"durability\":80.0", StringComparison.Ordinal),
            "Physical integrity fixture lost its baseline wear value.");
        physical.payloadJson = "{\"stackId\":\"stack:research-tool\",\"durability\":79.0,\"contentRevision\":12}";
        RequireRejectedWithoutRestore(
            physicalFixture,
            physicalWearMutation,
            "physical equipment-wear mutation");
    }

    private static void VerifyMixedSnapshotMetadataAndRootMutationAreRejectedBeforeRestore()
    {
        Fixture mixedFixture = CreateFixture();
        DungeonGameSaveData first = CaptureJsonClone(mixedFixture);
        mixedFixture.Registry.AdvanceToSecondSnapshot();
        DungeonGameSaveData second = CaptureJsonClone(mixedFixture);
        Require(DungeonSaveManifest.TryValidate(first.manifest, first.sections, out _)
                && DungeonSaveManifest.TryValidate(second.manifest, second.sections, out _),
            "Fixture could not produce two independently valid sealed snapshots.");

        DungeonGameSaveData mixed = CloneThroughJson(mixedFixture, first);
        ReplaceEnvelope(
            mixed,
            Find(second, PhysicalSectionId));
        RequireRejectedWithoutRestore(
            mixedFixture,
            mixed,
            "physical section swapped from a second sealed snapshot");

        Fixture metadataFixture = CreateFixture();
        DungeonGameSaveData metadataMutation = CaptureJsonClone(metadataFixture);
        Find(metadataMutation, OutcomeSectionId).restorePhase =
            DungeonSaveRestorePhase.Items;
        RequireRejectedWithoutRestore(
            metadataFixture,
            metadataMutation,
            "outcome envelope restore-phase mutation");

        Fixture rootFixture = CreateFixture();
        DungeonGameSaveData rootMutation = CaptureJsonClone(rootFixture);
        rootMutation.manifest.snapshotSha256 = DifferentCanonicalHash(
            rootMutation.manifest.snapshotSha256);
        RequireRejectedWithoutRestore(
            rootFixture,
            rootMutation,
            "manifest root digest mutation");
    }

    private static void VerifySectionAndManifestRowSwapIsRejectedBeforeRestore()
    {
        Fixture fixture = CreateFixture();
        DungeonGameSaveData first = CaptureJsonClone(fixture);
        fixture.Registry.AdvanceToSecondSnapshot();
        DungeonGameSaveData second = CaptureJsonClone(fixture);
        Require(!string.Equals(
                    first.manifest.snapshotSha256,
                    second.manifest.snapshotSha256,
                    StringComparison.Ordinal),
            "Fixture snapshots unexpectedly share one root digest.");

        DungeonGameSaveData swapped = CloneThroughJson(fixture, first);
        ReplaceEnvelope(swapped, Find(second, OutcomeSectionId));
        DungeonSaveManifestSectionData secondRow = second.manifest.sections.Single(
            section => section != null
                && string.Equals(section.sectionId, OutcomeSectionId,
                    StringComparison.Ordinal));
        ReplaceManifestRow(swapped, secondRow);
        RequireRejectedWithoutRestore(
            fixture,
            swapped,
            "outcome section and matching manifest row swapped from a second snapshot");
    }

    private static void VerifyUnsealedLegacySnapshotWarnsWithoutResealing()
    {
        Fixture fixture = CreateFixture();
        DungeonGameSaveData legacy = CaptureJsonClone(fixture);
        legacy.manifest.payloadHashVersion = 0;
        legacy.manifest.snapshotSha256 = string.Empty;
        foreach (DungeonSaveManifestSectionData section in legacy.manifest.sections)
            section.payloadSha256 = string.Empty;
        string manifestBeforeRestore = JsonUtility.ToJson(legacy.manifest);

        bool restored = fixture.Service.TryRestore(legacy, out DungeonGameRestoreReport report);
        Require(restored && report.Success && fixture.Registry.RestoreCalls == 1
                && report.Warnings.Any(warning => warning != null
                    && warning.Contains(LegacyIntegrityWarning, StringComparison.Ordinal))
                && string.Equals(
                    manifestBeforeRestore,
                    JsonUtility.ToJson(legacy.manifest),
                    StringComparison.Ordinal)
                && legacy.manifest.payloadHashVersion == 0
                && string.IsNullOrEmpty(legacy.manifest.snapshotSha256)
                && legacy.manifest.sections.All(section =>
                    section != null && string.IsNullOrEmpty(section.payloadSha256)),
            "A fully unsealed legacy snapshot was not warned about or was resealed in place.");
    }

    private static void VerifyPartialAndUnknownIntegrityMetadataAreRejectedBeforeRestore()
    {
        Fixture partialFixture = CreateFixture();
        DungeonGameSaveData partial = CaptureJsonClone(partialFixture);
        partial.manifest.payloadHashVersion = 0;
        RequireRejectedWithoutRestore(
            partialFixture,
            partial,
            "partial legacy integrity metadata");

        Fixture unknownFixture = CreateFixture();
        DungeonGameSaveData unknown = CaptureJsonClone(unknownFixture);
        unknown.manifest.payloadHashVersion =
            DungeonSavePayloadIntegrity.CurrentVersion + 1;
        RequireRejectedWithoutRestore(
            unknownFixture,
            unknown,
            "unknown integrity metadata version");
    }

    private static void VerifyRestorePreflightCannotMutateSealedSnapshot()
    {
        foreach (RestorePreflightMutationKind mutation in new[]
                 {
                     RestorePreflightMutationKind.Payload,
                     RestorePreflightMutationKind.Reseal,
                     RestorePreflightMutationKind.LegacyDowngrade
                 })
        {
            Fixture fixture = CreateFixture(new SnapshotMutatingPreflightValidator(mutation));
            DungeonGameSaveData candidate = CaptureJsonClone(fixture);
            RequireRejectedWithoutRestore(
                fixture,
                candidate,
                "sealed restore preflight mutation: " + mutation);
        }
    }

    private static void VerifyCapturedValidatorCannotMutateSealedSnapshot()
    {
        RecordingRegistry registry = new();
        DungeonGameSaveService service = new(
            registry,
            Array.Empty<IDungeonSavePreflightValidator>(),
            Array.Empty<IDungeonSaveCaptureGuard>(),
            new IDungeonCapturedSavePreflightValidator[]
            {
                new MutatingCapturedSaveValidator()
            },
            Array.Empty<IDungeonSaveRestoreCompletedHook>());

        RequireThrows(
            () => service.Capture(),
            "A captured-save observer changed sealed payload bytes without rejection.");
        Require(registry.CaptureCalls == 1 && registry.RestoreCalls == 0
                && string.Equals(registry.LiveSentinel, "live:initial", StringComparison.Ordinal),
            "Capture-observer mutation unexpectedly restored or changed live boundary state.");
    }

    private static void VerifyCapturedValidatorCannotResealOrDowngradeSealedSnapshot()
    {
        foreach (CaptureMutationKind mutation in new[]
                 {
                     CaptureMutationKind.Reseal,
                     CaptureMutationKind.LegacyDowngrade
                 })
        {
            RecordingRegistry registry = new();
            DungeonGameSaveService service = new(
                registry,
                Array.Empty<IDungeonSavePreflightValidator>(),
                Array.Empty<IDungeonSaveCaptureGuard>(),
                new IDungeonCapturedSavePreflightValidator[]
                {
                    new MutatingCapturedSaveValidator(mutation)
                },
                Array.Empty<IDungeonSaveRestoreCompletedHook>());

            RequireThrows(
                () => service.Capture(),
                "A capture observer mutation (" + mutation
                + ") changed a sealed snapshot without rejection.");
            Require(registry.CaptureCalls == 1 && registry.RestoreCalls == 0
                    && string.Equals(
                        registry.LiveSentinel,
                        "live:initial",
                        StringComparison.Ordinal),
                "Capture observer " + mutation
                + " reached the live restore boundary.");
        }
    }

    private static void VerifyEmptyManifestIntegrityBoundary()
    {
        DungeonSaveManifestData sealedManifest = DungeonSaveManifest.Capture(
            Array.Empty<DungeonSaveSectionEnvelope>());
        sealedManifest.sections = null;
        Require(DungeonSaveManifest.TryValidate(
                sealedManifest,
                null,
                out string sealedReason),
            "Empty sealed snapshot boundary threw or rejected: " + sealedReason);

        DungeonSaveManifestData legacyManifest = DungeonSaveManifest.Capture(
            Array.Empty<DungeonSaveSectionEnvelope>());
        legacyManifest.payloadHashVersion = 0;
        legacyManifest.snapshotSha256 = string.Empty;
        legacyManifest.sections = null;
        Require(DungeonSaveManifest.TryValidate(
                legacyManifest,
                null,
                out string legacyReason),
            "Empty legacy snapshot boundary threw or rejected: " + legacyReason);
    }

    private static Fixture CreateFixture(
        params IDungeonSavePreflightValidator[] preflightValidators)
    {
        RecordingRegistry registry = new();
        return new Fixture(
            registry,
            new DungeonGameSaveService(
                registry,
                preflightValidators ?? Array.Empty<IDungeonSavePreflightValidator>(),
                Array.Empty<IDungeonSaveCaptureGuard>(),
                Array.Empty<IDungeonCapturedSavePreflightValidator>(),
                Array.Empty<IDungeonSaveRestoreCompletedHook>()));
    }

    private static DungeonGameSaveData CaptureJsonClone(Fixture fixture) =>
        CloneThroughJson(fixture, fixture.Service.Capture());

    private static DungeonGameSaveData CloneThroughJson(
        Fixture fixture,
        DungeonGameSaveData source) => fixture.Service.FromJson(
        fixture.Service.ToJson(source));

    private static DungeonSaveSectionEnvelope Find(
        DungeonGameSaveData save,
        string sectionId) => (save?.sections ?? new List<DungeonSaveSectionEnvelope>())
        .Single(envelope => envelope != null
            && string.Equals(envelope.sectionId, sectionId, StringComparison.Ordinal));

    private static void ReplaceEnvelope(
        DungeonGameSaveData target,
        DungeonSaveSectionEnvelope replacement)
    {
        int index = target.sections.FindIndex(envelope => envelope != null
            && string.Equals(envelope.sectionId, replacement.sectionId,
                StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException("Fixture target omitted a known save section.");
        target.sections[index] = CloneEnvelope(replacement);
    }

    private static void ReplaceManifestRow(
        DungeonGameSaveData target,
        DungeonSaveManifestSectionData replacement)
    {
        int index = target.manifest.sections.FindIndex(section => section != null
            && string.Equals(section.sectionId, replacement.sectionId,
                StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException("Fixture target omitted a known manifest section.");
        target.manifest.sections[index] = CloneManifestRow(replacement);
    }

    private static void RequireRejectedWithoutRestore(
        Fixture fixture,
        DungeonGameSaveData candidate,
        string label)
    {
        int restoreCallsBefore = fixture.Registry.RestoreCalls;
        string sentinelBefore = fixture.Registry.LiveSentinel;
        bool restored = fixture.Service.TryRestore(
            candidate,
            out DungeonGameRestoreReport report);
        Require(!restored && !report.Success && report.Errors.Count > 0
                && fixture.Registry.RestoreCalls == restoreCallsBefore
                && string.Equals(
                    fixture.Registry.LiveSentinel,
                    sentinelBefore,
                    StringComparison.Ordinal),
            "Integrity/service rejection for " + label
            + " reached the live restore boundary: "
            + string.Join(" | ", report.Errors));
    }

    private static bool IsHash(string value) => value != null && value.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string DifferentCanonicalHash(string value)
    {
        if (!IsHash(value))
            throw new InvalidOperationException("Fixture root digest is not canonical.");
        return (value[0] == '0' ? "1" : "0") + value.Substring(1);
    }

    private static DungeonSaveSectionEnvelope CloneEnvelope(
        DungeonSaveSectionEnvelope source) => new()
    {
        sectionId = source?.sectionId ?? string.Empty,
        sectionVersion = source?.sectionVersion ?? 0,
        restorePhase = source?.restorePhase ?? DungeonSaveRestorePhase.RuntimeState,
        optional = source != null && source.optional,
        payloadJson = source?.payloadJson
    };

    private static DungeonSaveManifestSectionData CloneManifestRow(
        DungeonSaveManifestSectionData source) => new()
    {
        sectionId = source?.sectionId ?? string.Empty,
        sectionVersion = source?.sectionVersion ?? 0,
        optional = source != null && source.optional,
        payloadSha256 = source?.payloadSha256 ?? string.Empty
    };

    private static void RequireThrows(Action action, string failureMessage)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception) when (
            exception.Message.StartsWith("Captured save integrity failed:", StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(failureMessage);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        public Fixture(RecordingRegistry registry, DungeonGameSaveService service)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public RecordingRegistry Registry { get; }
        public DungeonGameSaveService Service { get; }
    }

    private enum RestorePreflightMutationKind
    {
        Payload,
        Reseal,
        LegacyDowngrade
    }

    private enum CaptureMutationKind
    {
        Payload,
        Reseal,
        LegacyDowngrade
    }

    /// <summary>
    /// This is an explicit save-section boundary, not a replacement save
    /// service. It records only successful registry publication.
    /// </summary>
    private sealed class RecordingRegistry : IDungeonSaveSectionRegistry
    {
        private readonly Dictionary<string, DungeonSaveSectionEnvelope> source =
            new(StringComparer.Ordinal)
            {
                [ResearchSectionId] = CreateEnvelope(
                    ResearchSectionId,
                    "{\"outcomeSequence\":7,\"projectProgress\":4.0,\"taskId\":\"project:alpha\"}"),
                [PhysicalSectionId] = CreateEnvelope(
                    PhysicalSectionId,
                    "{\"stackId\":\"stack:research-tool\",\"durability\":80.0,\"contentRevision\":12}"),
                [OutcomeSectionId] = CreateEnvelope(
                    OutcomeSectionId,
                    "{\"operationId\":\"research-work:7\",\"commitRevision\":7,\"toolRevisionAfter\":12}")
            };

        public IReadOnlyList<IDungeonSaveSection> OrderedSections =>
            Array.Empty<IDungeonSaveSection>();
        public int CaptureCalls { get; private set; }
        public int RestoreCalls { get; private set; }
        public string LiveSentinel { get; private set; } = "live:initial";

        public List<DungeonSaveSectionEnvelope> CaptureAll()
        {
            CaptureCalls++;
            return source.Values
                .OrderBy(envelope => envelope.sectionId, StringComparer.Ordinal)
                .Select(CloneEnvelope)
                .ToList();
        }

        public bool RestoreAll(
            IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
            DungeonGameRestoreReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            RestoreCalls++;
            Dictionary<string, DungeonSaveSectionEnvelope> incoming =
                (envelopes ?? Array.Empty<DungeonSaveSectionEnvelope>())
                .ToDictionary(
                    envelope => envelope?.sectionId
                        ?? throw new InvalidOperationException(
                            "Recording registry received a null save section."),
                    StringComparer.Ordinal);
            if (incoming.Count != source.Count
                || source.Keys.Any(key => !incoming.ContainsKey(key))
                || incoming.Values.Any(envelope => envelope.sectionVersion != 1
                    || envelope.restorePhase != DungeonSaveRestorePhase.RuntimeState
                    || envelope.optional
                    || string.IsNullOrWhiteSpace(envelope.payloadJson)))
            {
                throw new InvalidOperationException(
                    "Recording registry received an unexpected restore boundary.");
            }

            LiveSentinel = incoming[ResearchSectionId].payloadJson;
            return true;
        }

        public bool TryGetEnvelope(
            IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
            string sectionId,
            out DungeonSaveSectionEnvelope envelope)
        {
            envelope = (envelopes ?? Array.Empty<DungeonSaveSectionEnvelope>())
                .FirstOrDefault(value => value != null
                    && string.Equals(value.sectionId, sectionId, StringComparison.Ordinal));
            return envelope != null;
        }

        public void AdvanceToSecondSnapshot()
        {
            source[ResearchSectionId].payloadJson =
                "{\"outcomeSequence\":8,\"projectProgress\":5.0,\"taskId\":\"project:alpha\"}";
            source[PhysicalSectionId].payloadJson =
                "{\"stackId\":\"stack:research-tool\",\"durability\":79.0,\"contentRevision\":13}";
            source[OutcomeSectionId].payloadJson =
                "{\"operationId\":\"research-work:8\",\"commitRevision\":8,\"toolRevisionAfter\":13}";
        }

        private static DungeonSaveSectionEnvelope CreateEnvelope(
            string sectionId,
            string payloadJson) => new()
        {
            sectionId = sectionId,
            sectionVersion = 1,
            restorePhase = DungeonSaveRestorePhase.RuntimeState,
            optional = false,
            payloadJson = payloadJson
        };
    }

    private sealed class SnapshotMutatingPreflightValidator :
        IDungeonSavePreflightValidator
    {
        private readonly RestorePreflightMutationKind mutation;

        public SnapshotMutatingPreflightValidator(
            RestorePreflightMutationKind mutation)
        {
            this.mutation = mutation;
        }

        public void Validate(
            DungeonGameSaveData saveData,
            DungeonGameRestoreReport report)
        {
            MutateSnapshot(saveData, report, mutation.ToString());
        }

        private void MutateSnapshot(
            DungeonGameSaveData saveData,
            DungeonGameRestoreReport report,
            string label)
        {
            if (saveData == null || report == null)
                throw new InvalidOperationException(
                    "Restore preflight " + label + " received no snapshot.");

            switch (mutation)
            {
                case RestorePreflightMutationKind.Payload:
                    MutatePayload(saveData);
                    return;
                case RestorePreflightMutationKind.Reseal:
                    MutatePayload(saveData);
                    saveData.manifest = DungeonSaveManifest.Capture(saveData.sections);
                    return;
                case RestorePreflightMutationKind.LegacyDowngrade:
                    MutatePayload(saveData);
                    DowngradeToLegacy(saveData);
                    return;
                default:
                    throw new InvalidOperationException(
                        "Unknown restore preflight mutation " + mutation + ".");
            }
        }
    }

    private sealed class MutatingCapturedSaveValidator :
        IDungeonCapturedSavePreflightValidator
    {
        private readonly CaptureMutationKind mutation;

        public MutatingCapturedSaveValidator(
            CaptureMutationKind mutation = CaptureMutationKind.Payload)
        {
            this.mutation = mutation;
        }

        public void Validate(
            DungeonGameSaveData captured,
            DungeonGameRestoreReport report)
        {
            if (captured == null || report == null)
                throw new InvalidOperationException(
                    "Mutating capture validator received no snapshot.");

            switch (mutation)
            {
                case CaptureMutationKind.Payload:
                    MutatePayload(captured);
                    return;
                case CaptureMutationKind.Reseal:
                    MutatePayload(captured);
                    captured.manifest = DungeonSaveManifest.Capture(captured.sections);
                    return;
                case CaptureMutationKind.LegacyDowngrade:
                    MutatePayload(captured);
                    DowngradeToLegacy(captured);
                    return;
                default:
                    throw new InvalidOperationException(
                        "Unknown capture observer mutation " + mutation + ".");
            }
        }
    }

    private static void MutatePayload(DungeonGameSaveData saveData)
    {
        if (saveData?.sections == null || saveData.sections.Count == 0
            || saveData.sections[0] == null)
        {
            throw new InvalidOperationException(
                "Snapshot mutation received no captured sections.");
        }

        saveData.sections[0].payloadJson += " ";
    }

    private static void DowngradeToLegacy(DungeonGameSaveData saveData)
    {
        if (saveData?.manifest == null || saveData.manifest.sections == null)
            throw new InvalidOperationException(
                "Snapshot legacy downgrade received no manifest.");

        saveData.manifest.payloadHashVersion = 0;
        saveData.manifest.snapshotSha256 = string.Empty;
        foreach (DungeonSaveManifestSectionData section in saveData.manifest.sections)
        {
            if (section == null)
                throw new InvalidOperationException(
                    "Snapshot legacy downgrade received a null manifest row.");
            section.payloadSha256 = string.Empty;
        }
    }
}
#endif
