using System;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Captivity projection and commands used by the character summary health tab.
/// </summary>
public sealed class CharacterSummaryCaptivityPresenter
{
    private readonly ICaptivityRuntime captivityRuntime;
    private readonly ICaptivityCommandService captivityCommands;
    private readonly ICharacterAiWorldRegistry characterWorld;
    private readonly IGameEventBus eventBus;
    private Button actionButton;

    public CharacterSummaryCaptivityPresenter(
        ICaptivityRuntime captivityRuntime,
        ICaptivityCommandService captivityCommands,
        ICharacterAiWorldRegistry characterWorld,
        IGameEventBus eventBus)
    {
        this.captivityRuntime = captivityRuntime
            ?? throw new ArgumentNullException(nameof(captivityRuntime));
        this.captivityCommands = captivityCommands
            ?? throw new ArgumentNullException(nameof(captivityCommands));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.eventBus = eventBus
            ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public void Bind(Button generatedActionButton)
    {
        actionButton = generatedActionButton;
    }

    public void Execute(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        string captiveId = actor.Identity?.PersistentId ?? string.Empty;
        if (!captivityRuntime.TryGetCaptive(captiveId, out CaptiveState captive)
            || !captive.IsActive)
        {
            ShowNotice("이 대상은 포획할 수 있는 쓰러진 침입자가 아닙니다.", false);
            return;
        }

        if (captive.status == CaptivityStatus.AwaitingCapture)
        {
            CharacterActor carrier = characterWorld.AllCharacters
                .Where(candidate => IsAvailableCaptureCarrier(candidate, captiveId))
                .OrderBy(candidate => Manhattan(candidate.GetNowXY(), actor.GetNowXY()))
                .FirstOrDefault();
            string captureFailure = string.Empty;
            bool started = carrier != null
                && captivityCommands.TryOrderCapture(actor, carrier, out captureFailure);
            string message = carrier == null
                ? "포로를 운반할 수 있는 직원이 없습니다."
                : started
                    ? $"{carrier.Identity?.DisplayName ?? carrier.name}에게 포획과 호송을 명령했습니다."
                    : captureFailure;
            ShowNotice(message, started);
        }
        else if (captive.status == CaptivityStatus.Confined)
        {
            bool started = captivityCommands.TrySetLaborPermissions(
                captiveId,
                CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul,
                out string reason);
            ShowNotice(started ? "청소·운반 노역을 허용했습니다." : reason, started);
        }
        else if (captive.status == CaptivityStatus.Labor)
        {
            bool stopped = captivityCommands.TrySetLaborPermissions(
                captiveId,
                CaptiveLaborPermission.None,
                out string reason);
            ShowNotice(stopped ? "노역을 중지하고 감방으로 돌려보냈습니다." : reason, stopped);
        }
        else
        {
            ShowNotice("세부 처우는 운영 탭의 포로·노역 항목에서 관리할 수 있습니다.", true);
        }
    }

    public void AppendDetails(StringBuilder builder, CharacterActor actor)
    {
        string captiveId = actor?.Identity?.PersistentId ?? string.Empty;
        if (!captivityRuntime.TryGetCaptive(captiveId, out CaptiveState captive)
            || !captive.IsActive)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("포로 상태");
        builder.AppendLine(
            $"{CharacterSummaryTextFormatter.FormatCaptivityStatus(captive.status)} · 건강 {captive.health:0}"
            + $" · 순응 {captive.compliance:0} · 탈출 위험 {captive.escapeRisk:0}");
        builder.AppendLine(
            $"의지 {captive.will:0} · 공포 {captive.fear:0}"
            + $" · 신뢰 {captive.trust:0} · 원한 {captive.grudge:0}"
            + $" · 타락 {captive.corruption:0}");
        if (captive.falseCompliance)
        {
            builder.AppendLine("복종 진위 불명: 원한이 높아 배신 가능성이 있습니다.");
        }

        if (!string.IsNullOrWhiteSpace(captive.lastResult))
        {
            builder.AppendLine($"최근 상태  {captive.lastResult}");
        }
    }

    public void RefreshActionButton(CharacterActor actor)
    {
        if (actionButton == null)
        {
            return;
        }

        string captiveId = actor?.Identity?.PersistentId ?? string.Empty;
        bool hasCaptive = captivityRuntime.TryGetCaptive(captiveId, out CaptiveState captive)
            && captive.IsActive;
        actionButton.gameObject.SetActive(hasCaptive);
        if (!hasCaptive)
        {
            return;
        }

        string label = captive.status switch
        {
            CaptivityStatus.AwaitingCapture => "포획·호송 명령",
            CaptivityStatus.Confined => "기본 노역 허용",
            CaptivityStatus.Labor => "노역 중지",
            CaptivityStatus.Stabilizing
                or CaptivityStatus.AwaitingEscort
                or CaptivityStatus.Escorting => "포획 진행 중",
            _ => "운영 탭에서 관리"
        };
        TMP_Text text = actionButton.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.text = label;
        }

        actionButton.interactable = captive.status is CaptivityStatus.AwaitingCapture
            or CaptivityStatus.Confined
            or CaptivityStatus.Labor;
    }

    private static bool IsAvailableCaptureCarrier(CharacterActor candidate, string captiveId)
    {
        return candidate != null
            && !candidate.IsDead
            && candidate.CurrentLifecycleState == CharacterLifecycleState.Active
            && candidate.characterType == CharacterType.NPC
            && !string.Equals(candidate.Identity?.PersistentId, captiveId, StringComparison.Ordinal)
            && candidate.TryGetAbility(out AbilityMove _);
    }

    private void ShowNotice(string message, bool success)
    {
        eventBus.ShowNotice(
            string.IsNullOrWhiteSpace(message) ? "포로 명령을 처리하지 못했습니다." : message,
            success ? NoticeFeedEvent.Grade.NONE : NoticeFeedEvent.Grade.WARNING);
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }
}
