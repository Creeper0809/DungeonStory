using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public partial class OffenseWorldMapPanel : MonoBehaviour
{
    private IOffenseCampaignQuery campaign;
    private IOffenseCampaignCommands commands;
    private TMP_Text headerText;
    private TMP_Text detailText;
    private RectTransform targetButtonRoot;
    private readonly List<GameObject> spawnedButtons = new();
    private IOffensePanelButtonFactory buttonFactory;

    public void Bind(
        IOffenseCampaignQuery source,
        IOffenseCampaignCommands commandSource,
        IOffensePanelButtonFactory factory)
    {
        campaign = source ?? throw new ArgumentNullException(nameof(source));
        commands = commandSource
            ?? throw new ArgumentNullException(nameof(commandSource));
        buttonFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        EnsureView();
        gameObject.SetActive(true);
        Render();
    }

    public void Render()
    {
        if (campaign == null)
        {
            return;
        }

        EnsureView();
        if (CanRenderStrategic())
        {
            RenderStrategic();
            return;
        }

        headerText.text =
            $"월드맵 / 정찰 Lv.{campaign.State.ReconLevel} / 범위 {campaign.CurrentScanRange:0.#}";
        ClearButtons();
        foreach (OffenseTargetSnapshot target in campaign.VisibleTargets)
        {
            GameObject buttonObject = RequireButtonFactory().CreateButton(
                targetButtonRoot,
                target.title,
                17f,
                () =>
                {
                    if (commands.TrySelectTarget(
                        target.id,
                        out OffenseTargetSnapshot selected,
                        out _))
                    {
                        detailText.text = selected.ToDetailText();
                    }
                    Render();
                });
            spawnedButtons.Add(buttonObject);
        }

        spawnedButtons.Add(RequireButtonFactory().CreateButton(
            targetButtonRoot,
            "정찰 강화",
            17f,
            () =>
            {
                commands.TryUpgradeRecon(out string message);
                detailText.text = message;
                Render();
            }));
        spawnedButtons.Add(RequireButtonFactory().CreateButton(
            targetButtonRoot,
            "닫기",
            17f,
            Hide));

        if (campaign.VisibleTargets.Count == 0)
        {
            detailText.text = "발견된 원정 대상이 없습니다.";
        }
        else if (!string.IsNullOrWhiteSpace(campaign.State.SelectedTargetId)
            && campaign.TryGetKnownTargetSnapshot(
                campaign.State.SelectedTargetId,
                out OffenseTargetSnapshot selected))
        {
            detailText.text = selected.ToDetailText();
        }
        else
        {
            detailText.text = campaign.VisibleTargets[0].ToDetailText();
        }
    }

    public void Hide() => gameObject.SetActive(false);

    private void EnsureView()
    {
        if (headerText != null && detailText != null && targetButtonRoot != null)
        {
            return;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        headerText = texts.FirstOrDefault(text => text.name == "OffenseWorldMapHeader");
        detailText = texts.FirstOrDefault(text => text.name == "OffenseWorldMapDetail");
        targetButtonRoot = GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(rect => rect.name == "OffenseWorldMapTargets");
    }

    private void ClearButtons()
    {
        foreach (GameObject button in spawnedButtons)
        {
            RequireButtonFactory().Release(button);
        }
        spawnedButtons.Clear();
    }

    internal void BindGeneratedView(
        TMP_Text generatedHeaderText,
        TMP_Text generatedDetailText,
        RectTransform generatedTargetButtonRoot)
    {
        headerText = generatedHeaderText
            ?? throw new ArgumentNullException(nameof(generatedHeaderText));
        detailText = generatedDetailText
            ?? throw new ArgumentNullException(nameof(generatedDetailText));
        targetButtonRoot = generatedTargetButtonRoot
            ?? throw new ArgumentNullException(nameof(generatedTargetButtonRoot));
    }

    private IOffensePanelButtonFactory RequireButtonFactory()
    {
        return buttonFactory
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseWorldMapPanel)} requires "
                + $"{nameof(IOffensePanelButtonFactory)} binding.");
    }
}
