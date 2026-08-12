using System;
using System.Collections.Generic;
using UnityEngine;

public static class RecruitmentSaveDebugScenarios
{
    public static void Validate()
    {
        InMemoryRecruitmentState state = new(
            new DungeonRegularCustomerSaveData
            {
                records = new List<DungeonRegularCustomerRecordSaveData>
                {
                    new()
                    {
                        customerId = "character:customer:save-fixture",
                        displayName = "Save Fixture Customer",
                        speciesTag = "Slime",
                        sourceDataId = 112,
                        visitCount = 2,
                        averageSatisfaction = 75f,
                        isRegular = true,
                        isRecruitCandidate = true,
                        isRecruited = false,
                        recruitCapabilities = RecruitCapability.All
                    }
                }
            });
        FixedCharacterCatalog catalog = new(112);
        RegularCustomerSaveSection section = new(state, catalog);
        string canonicalJson = section.Capture();

        DungeonGameRestoreReport validReport = new();
        section.Restore(
            canonicalJson,
            section.SectionVersion,
            validReport);
        object sectionContract = section;
        if (!validReport.Success
            || state.RestoreCount != 1
            || sectionContract is not IDungeonSaveSectionPreflight
            || sectionContract is not IDungeonRollbackFreeSaveSection
            || sectionContract is IOptionalDungeonSaveSection
            || sectionContract is IDungeonStagedOptionalSaveSection)
        {
            throw new InvalidOperationException(
                "Recruitment strict save boundary rejected canonical state.");
        }

        DungeonRegularCustomerSaveData invalid =
            JsonUtility.FromJson<DungeonRegularCustomerSaveData>(canonicalJson);
        invalid.records[0].isRegular = false;
        DungeonGameRestoreReport invalidReport = new();
        bool invalidRejected = false;
        try
        {
            section.Restore(
                JsonUtility.ToJson(invalid),
                section.SectionVersion,
                invalidReport);
        }
        catch (InvalidOperationException)
        {
            invalidRejected = true;
        }
        if (!invalidRejected || state.RestoreCount != 1)
        {
            throw new InvalidOperationException(
                "Recruitment strict save boundary mutated state after failed validation.");
        }

        RequireRawPayloadRejected(
            section,
            state,
            "{\"version\":1,\"records\":null}",
            "explicitly null records");
        RequireRawPayloadRejected(
            section,
            state,
            "{\"version\":1}",
            "missing records");
        RequireRawPayloadRejected(
            section,
            state,
            "{\"version\":1,\"description\":\"\\\"records\\\":[]\"}",
            "string-spoofed records");
        RequireRawPayloadRejected(
            section,
            state,
            "{\"version\":1,\"nested\":{\"records\":[]}}",
            "nested records");
    }

    private static void RequireRawPayloadRejected(
        RegularCustomerSaveSection section,
        InMemoryRecruitmentState state,
        string payloadJson,
        string scenario)
    {
        int restoreCountBefore = state.RestoreCount;
        bool rejected = false;
        try
        {
            section.Restore(
                payloadJson,
                section.SectionVersion,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        if (!rejected || state.RestoreCount != restoreCountBefore)
        {
            throw new InvalidOperationException(
                $"Recruitment strict save boundary accepted {scenario} "
                + "or mutated live state after rejection.");
        }
    }

    private sealed class InMemoryRecruitmentState : IRegularCustomerPersistence
    {
        private DungeonRegularCustomerSaveData state;

        public InMemoryRecruitmentState(DungeonRegularCustomerSaveData initial)
        {
            state = initial ?? throw new ArgumentNullException(nameof(initial));
        }

        public int RestoreCount { get; private set; }

        public DungeonRegularCustomerSaveData CaptureState() => state;

        public RegularCustomerRestoreCandidate PrepareRestore(
            DungeonRegularCustomerSaveData snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new InMemoryCandidate(
                JsonUtility.FromJson<DungeonRegularCustomerSaveData>(
                    JsonUtility.ToJson(snapshot)));
        }

        public void PublishRestore(RegularCustomerRestoreCandidate candidate)
        {
            if (candidate is not InMemoryCandidate prepared)
            {
                throw new InvalidOperationException(
                    "Recruitment fixture received a foreign candidate.");
            }

            state = prepared.State;
            RestoreCount++;
        }

        private sealed class InMemoryCandidate :
            RegularCustomerRestoreCandidate
        {
            internal InMemoryCandidate(DungeonRegularCustomerSaveData state)
            {
                State = state ?? throw new ArgumentNullException(nameof(state));
            }

            internal DungeonRegularCustomerSaveData State { get; }
        }
    }

    private sealed class FixedCharacterCatalog :
        IRecruitmentCharacterDefinitionCatalog
    {
        private readonly int[] ids;

        public FixedCharacterCatalog(params int[] ids)
        {
            this.ids = ids ?? Array.Empty<int>();
        }

        public IReadOnlyCollection<int> CharacterDefinitionIds => ids;
    }
}
