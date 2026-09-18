using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Operation;

internal sealed class CaptivityInterrogationInformationRuntime
{
    private readonly ICharacterNarrativeQuery narratives;
    private readonly IEnemyArchetypeCatalog enemyArchetypes;
    private readonly ICaptivityInterrogationCodexPort codex;
    private readonly IGameEventBus gameEventBus;

    internal CaptivityInterrogationInformationRuntime(
        ICharacterNarrativeQuery narratives,
        IEnemyArchetypeCatalog enemyArchetypes,
        ICaptivityInterrogationCodexPort codex,
        IGameEventBus gameEventBus)
    {
        this.narratives = narratives
            ?? throw new ArgumentNullException(nameof(narratives));
        this.enemyArchetypes = enemyArchetypes
            ?? throw new ArgumentNullException(nameof(enemyArchetypes));
        this.codex = codex ?? throw new ArgumentNullException(nameof(codex));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    internal CaptivityInterrogationTerminalState FreezeOutcome(
        CaptiveState captive,
        IEnumerable<CaptiveState> captives)
    {
        if (captive == null)
        {
            throw new ArgumentNullException(nameof(captive));
        }
        if (captive.currentInterrogationAttemptId <= 0
            || captive.currentInterrogationAttemptId
                > captive.interrogationAttemptSequence)
        {
            throw new InvalidOperationException(
                $"Captive '{captive.captiveId}' has no active interrogation attempt identity.");
        }

        CaptivityInterrogationTerminalState terminal =
            new CaptivityInterrogationTerminalState
            {
                attemptId = captive.currentInterrogationAttemptId,
                subjectDisplayName = string.IsNullOrWhiteSpace(
                    captive.displayName)
                    ? captive.captiveId
                    : captive.displayName.Trim(),
                highFearCaution = captive.fear
                    >= CaptivityInterrogationAttemptIdentity
                        .HighFearCautionThreshold,
                codexPublicationCompleted = true
            };

        if (!narratives.TryGet(
                new CharacterId(captive.captiveId),
                out CharacterNarrativeSnapshot narrative)
            || narrative == null
            || string.IsNullOrWhiteSpace(
                narrative.OriginEnemyArchetypeId)
            || string.IsNullOrWhiteSpace(narrative.OriginFactionId)
            || !enemyArchetypes.TryGet(
                narrative.OriginEnemyArchetypeId,
                out EnemyArchetypeDefinitionSO definition)
            || definition == null
            || !string.Equals(
                definition.factionId?.Trim(),
                narrative.OriginFactionId.Trim(),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(definition.speciesTag)
            || definition.tacticalProfile == null
            || string.IsNullOrWhiteSpace(
                definition.tacticalProfile.formationTag))
        {
            return terminal;
        }

        string originEnemyArchetypeId =
            narrative.OriginEnemyArchetypeId.Trim();
        string originFactionId = narrative.OriginFactionId.Trim();
        string formationTag =
            definition.tacticalProfile.formationTag.Trim();
        string codexEntryId = $"monster:{definition.speciesTag.Trim()}";
        string informationText = CaptivityInterrogationAttemptIdentity
            .FormatInformationText(
                originEnemyArchetypeId,
                originFactionId,
                formationTag);
        if (codex.HasInformation(codexEntryId, informationText)
            || HasFrozenInformation(
                captives,
                captive.captiveId,
                codexEntryId,
                informationText))
        {
            return terminal;
        }

        terminal.hasInformation = true;
        terminal.codexEntryId = codexEntryId;
        terminal.codexEntryTitle = definition.speciesTag.Trim();
        terminal.originEnemyArchetypeId = originEnemyArchetypeId;
        terminal.originFactionId = originFactionId;
        terminal.enemyDisplayName = string.IsNullOrWhiteSpace(
            definition.displayName)
            ? originEnemyArchetypeId
            : definition.displayName.Trim();
        terminal.formationTag = formationTag;
        terminal.informationText = informationText;
        terminal.codexPublicationCompleted = false;
        return terminal;
    }

    private static bool HasFrozenInformation(
        IEnumerable<CaptiveState> captives,
        string currentCaptiveId,
        string entryId,
        string informationText)
    {
        foreach (CaptiveState candidate in
                 captives ?? Array.Empty<CaptiveState>())
        {
            CaptivityInterrogationTerminalState other =
                candidate?.interrogationTerminal;
            if (other?.hasInformation != true
                || (string.Equals(
                        candidate.captiveId,
                        currentCaptiveId,
                        StringComparison.Ordinal)
                    && other.attemptId
                        == candidate.currentInterrogationAttemptId)
                || !string.Equals(
                    other.codexEntryId,
                    entryId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    other.informationText,
                    informationText,
                    StringComparison.Ordinal))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    internal bool TryPublish(
        string captiveId,
        CaptivityInterrogationTerminalState terminal,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (terminal == null || !terminal.HasOutcome)
        {
            failureReason = "A frozen interrogation outcome is required.";
            return false;
        }

        try
        {
            if (!terminal.codexPublicationCompleted)
            {
                if (!terminal.hasInformation)
                {
                    throw new InvalidOperationException(
                        "A no-information interrogation cannot have pending codex publication.");
                }

                codex.PublishInformation(
                    terminal.codexEntryId,
                    terminal.codexEntryTitle,
                    terminal.informationText);
                terminal.codexPublicationCompleted = true;
            }

            if (!terminal.noticePublicationCompleted)
            {
                gameEventBus.Publish(new EventAlertRequestedEvent(
                    CreateNotice(captiveId, terminal)));
                terminal.noticePublicationCompleted = true;
            }
            return true;
        }
        catch (Exception exception)
        {
            failureReason = exception.Message;
            return false;
        }
    }

    internal static string GetResultTitle(
        CaptivityInterrogationTerminalState terminal) =>
        terminal?.hasInformation == true
            ? "심문 성공"
            : "심문 종료 — 추가 정보 없음";

    private static EventAlertRequest CreateNotice(
        string captiveId,
        CaptivityInterrogationTerminalState terminal)
    {
        string title = GetResultTitle(terminal);
        string detail;
        if (terminal.hasInformation)
        {
            detail =
                $"대상 포로: {terminal.subjectDisplayName}\n"
                + $"정보 대상: {terminal.enemyDisplayName} "
                + $"({terminal.originEnemyArchetypeId})\n"
                + $"소속 세력: {terminal.originFactionId}\n"
                + "주제: 출신 부대의 전열·전술\n"
                + $"획득 내용: 전열·전술 표식 "
                + $"'{terminal.formationTag}'\n"
                + "검증 상태: 미확인 진술";
            if (terminal.highFearCaution)
            {
                detail +=
                    "\n주의: 공포도가 높은 상태의 진술이며, "
                    + "진실 또는 거짓으로 판정되지 않았습니다.";
            }
        }
        else
        {
            detail =
                $"대상 포로: {terminal.subjectDisplayName}\n"
                + "추가로 기록할 수 있는 실제 출신 부대의 "
                + "전열·전술 진술이 없습니다.";
        }

        return new EventAlertRequest(
            title,
            detail,
            EventAlertImportance.Medium,
            "포로",
            sourceId: CaptivityInterrogationAttemptIdentity
                .FormatNoticeSourceId(captiveId, terminal.attemptId));
    }
}

public sealed class CaptivityInterrogationCodexAdapter :
    ICaptivityInterrogationCodexPort
{
    private readonly CodexRuntime codex;

    public CaptivityInterrogationCodexAdapter(
        FacilityFeatureSceneRuntimeReferences runtimeReferences)
    {
        codex = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .Codex
            ?? throw new InvalidOperationException(
                $"{nameof(CaptivityInterrogationCodexAdapter)} requires a loaded {nameof(CodexRuntime)}.");
    }

    public bool HasInformation(string entryId, string informationText) =>
        codex.HasInformation(
            CodexEntryCategory.Monster,
            entryId,
            informationText);

    public void PublishInformation(
        string entryId,
        string fallbackTitle,
        string informationText)
    {
        codex.RecordInformation(
            CodexEntryCategory.Monster,
            entryId,
            fallbackTitle,
            informationText,
            CodexInfoSource.Observation);
    }
}
