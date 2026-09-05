using System;
using DungeonStory.Operation;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CodexRuntime : MonoBehaviour
{
    private static readonly string[] MemoryResidueClues =
    {
        "서로 다른 증언에서 같은 종소리가 반복된다. 장소는 달라지지만 시간은 같았다.",
        "침입자의 기억에는 지도에 없는 검문소가 있다. 통행증에는 오래된 문장이 찍혀 있다.",
        "지워진 이름 뒤에 같은 필체가 남아 있다. 예정 기록과 침공 명령서의 작성자가 같다.",
        "봉인 지대의 보급 목록에는 전투 물자가 없다. 대신 빛, 관, 기억 봉합재만 적혀 있다.",
        "변경의 증인들은 길을 걸은 적이 없다고 말한다. 그러나 모두 같은 꿈에서 길을 배웠다.",
        "경외 회전의 병사들은 승리를 기억하지만 전장은 기억하지 못한다.",
        "회수된 기억 파편은 두 번 울린다. 두 번째 파동에서는 그림자가 사람보다 먼저 움직인다.",
        "명령서의 마지막 문장은 잉크가 아니라 기억에서 지워진 흔적으로 남아 있다."
    };

    [SerializeField] private bool importReferenceDataOnAwake = true;

    private CodexState state = new CodexState();
    private ICodexRuntimeApplicationPort applicationPort;
    private ICodexReferenceImporter referenceImporter;

    public CodexState State => state;

    [Inject]
    public void ConstructCodexRuntime(
        ICodexRuntimeApplicationPort applicationPort,
        ICodexReferenceImporter referenceImporter,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        if (this.applicationPort != null && isActiveAndEnabled)
        {
            this.applicationPort.Unbind(this);
        }

        this.applicationPort = applicationPort
            ?? throw new ArgumentNullException(nameof(applicationPort));
        this.referenceImporter = referenceImporter
            ?? throw new ArgumentNullException(nameof(referenceImporter));
        state = new CodexState(aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore)));
        if (isActiveAndEnabled)
        {
            this.applicationPort.Bind(this);
        }
    }

    private void Start()
    {
        if (importReferenceDataOnAwake)
        {
            ImportReferenceData();
        }
    }

    public void ImportReferenceData()
    {
        CodexService.ImportReferenceData(
            state,
            referenceImporter ?? throw Missing(nameof(ICodexReferenceImporter)));
        PublishUpdated(CodexEntryCategory.Facility, "reference");
        PublishUpdated(CodexEntryCategory.Monster, "reference");
        PublishUpdated(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
    }

    public IReadOnlyList<CodexEntrySnapshot> GetEntries(CodexEntryCategory category)
    {
        return state.GetSnapshots(category);
    }

    public bool HasMemoryResidueClueAvailable
    {
        get
        {
            const string entryId = "memory-residue";
            CodexEntrySnapshot entry = state.GetSnapshot(CodexEntryCategory.Invasion, entryId);
            return MemoryResidueClues.Any(candidate => entry?.lines == null
                || !entry.lines.Any(line => string.Equals(
                    line.Text,
                    candidate,
                    StringComparison.Ordinal)));
        }
    }

    public void ReplaceStateFromRestore(CodexState restored)
    {
        state.ReplaceFrom(restored);
    }

#if UNITY_EDITOR
    public void ReplaceWithEmptyStateForDebug()
    {
        state.ReplaceFrom(new CodexState());
    }
#endif

    public bool TryRecordMemoryResidueClue(out string message)
    {
        if (!TryGetNextMemoryResidueClue(out string clue))
        {
            message = "분석 가능한 기억 잔재 단서를 모두 정리했습니다.";
            return false;
        }

        return TryRecordMemoryResidueClue(clue, out message);
    }

    public bool TryGetNextMemoryResidueClue(out string clue)
    {
        const string entryId = "memory-residue";
        CodexEntrySnapshot entry = state.GetSnapshot(
            CodexEntryCategory.Invasion,
            entryId);
        clue = MemoryResidueClues.FirstOrDefault(candidate =>
            entry?.lines == null
            || !entry.lines.Any(line => string.Equals(
                line.Text,
                candidate,
                StringComparison.Ordinal)));
        return !string.IsNullOrWhiteSpace(clue);
    }

    public bool TryRecordMemoryResidueClue(
        string expectedClue,
        out string message)
    {
        const string entryId = "memory-residue";
        if (string.IsNullOrWhiteSpace(expectedClue)
            || !string.Equals(
                expectedClue,
                expectedClue.Trim(),
                StringComparison.Ordinal)
            || !MemoryResidueClues.Contains(
                expectedClue,
                StringComparer.Ordinal))
        {
            message = "기억 잔재 단서 식별자가 올바르지 않습니다.";
            return false;
        }

        CodexEntryRecord entry = state.GetOrCreate(
            CodexEntryCategory.Invasion,
            entryId,
            "기억 잔재");
        if (entry.Lines.Any(line => string.Equals(
                line.Text,
                expectedClue,
                StringComparison.Ordinal)))
        {
            message = $"월간 단서 정보: {expectedClue}";
            return true;
        }

        string next = MemoryResidueClues.FirstOrDefault(candidate =>
            !entry.Lines.Any(line => string.Equals(
                line.Text,
                candidate,
                StringComparison.Ordinal)));
        if (!string.Equals(next, expectedClue, StringComparison.Ordinal))
        {
            message = "기억 잔재 단서 순서가 현재 도감 상태와 일치하지 않습니다.";
            return false;
        }

        entry.AddInfo(expectedClue, CodexInfoSource.Research);
        PublishUpdated(CodexEntryCategory.Invasion, entryId);
        message = $"월간 단서 정보: {expectedClue}";
        RequireApplicationPort().RaiseAlert(new CodexAlertRequest(
            "기억 잔재 분석",
            expectedClue,
            "월간"));
        return true;
    }

    public void RecordCharacterVisit(
        CodexCharacterObservationSnapshot character,
        CodexFacilityObservationSnapshot facility)
    {
        if (character != null)
        {
            CodexService.ObserveCharacter(state, character);
            PublishUpdated(CodexEntryCategory.Monster, character.Entry.EntryId);
        }

        if (facility != null)
        {
            CodexService.ObserveFacility(state, facility);
            PublishUpdated(CodexEntryCategory.Facility, facility.Entry.EntryId);
        }
    }

    public void RecordDefenseObservation(CodexInvasionObservationSnapshot snapshot)
    {
        CodexService.RecordInvasion(state, snapshot);
        PublishUpdated(CodexEntryCategory.Facility, "defense");
        PublishUpdated(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
    }

    public void RecordCombatReport(CodexInvasionObservationSnapshot snapshot)
    {
        CodexService.RecordInvasion(state, snapshot);
        PublishUpdated(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
    }

    public void RecordFacilityDamage(CodexInvasionObservationSnapshot snapshot)
    {
        CodexService.RecordInvasion(state, snapshot);
        PublishUpdated(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
    }

    public void RecordInvasionSpawned(CodexCharacterObservationSnapshot intruder)
    {
        CodexService.SeedBreakthroughIntruder(state);
        CodexService.ObserveCharacter(state, intruder);
        PublishUpdated(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
    }

    public void RecordResearch(CodexResearchObservationSnapshot snapshot)
    {
        CodexService.RecordResearch(state, snapshot);
        PublishUpdated(CodexEntryCategory.Facility, "research");
    }

    public void RecordSynthesis(CodexRecipeObservationSnapshot snapshot)
    {
        CodexService.RecordSynthesis(state, snapshot);
        PublishUpdated(CodexEntryCategory.Facility, "synthesis");
    }

    public void RecordEvolution(CodexEvolutionObservationSnapshot snapshot)
    {
        CodexService.RecordEvolution(state, snapshot);
        PublishUpdated(CodexEntryCategory.Facility, "evolution");
    }

    private void PublishUpdated(CodexEntryCategory category, string entryId)
    {
        RequireApplicationPort().PublishUpdated(new CodexUpdatedEvent(category, entryId));
    }

    private ICodexRuntimeApplicationPort RequireApplicationPort()
    {
        return applicationPort ?? throw Missing(nameof(ICodexRuntimeApplicationPort));
    }

    private static InvalidOperationException Missing(string dependency)
    {
        return new InvalidOperationException(
            $"{nameof(CodexRuntime)} requires {dependency} injection.");
    }

    private void OnEnable()
    {
        applicationPort?.Bind(this);
    }

    private void OnDisable()
    {
        applicationPort?.Unbind(this);
    }
}
