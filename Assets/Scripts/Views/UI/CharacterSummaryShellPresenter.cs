using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns popup lifecycle, generated-view setup, and the detailed-stat overlay.
/// </summary>
public sealed class CharacterSummaryShellPresenter
{
    private readonly IUiPopupService popupService;
    private readonly ICharacterSummaryRuntimeLogFactory viewFactory;
    private readonly ICharacterDetailedStatsRuntime detailedStatsRuntime;
    private GameObject detailedStatsPanel;
    private TMP_Text detailedStatsTitle;
    private TMP_Text detailedStatsText;
    private Button[] detailedStatsTabButtons = Array.Empty<Button>();
    private CharacterDetailedStatsTab selectedDetailedStatsTab;

    public CharacterSummaryShellPresenter(
        IUiPopupService popupService,
        ICharacterSummaryRuntimeLogFactory viewFactory,
        ICharacterDetailedStatsRuntime detailedStatsRuntime)
    {
        this.popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        this.viewFactory = viewFactory ?? throw new ArgumentNullException(nameof(viewFactory));
        this.detailedStatsRuntime = detailedStatsRuntime
            ?? throw new ArgumentNullException(nameof(detailedStatsRuntime));
    }

    public void Initialize(
        ICharacterSummaryGeneratedView view,
        CharacterSummaryViewActions actions,
        GameObject uiRoot)
    {
        EnsureView(view, actions, uiRoot);
        uiRoot.SetActive(false);
    }

    public void Open(
        UIPopUp popup,
        ICharacterSummaryGeneratedView view,
        CharacterSummaryViewActions actions,
        GameObject uiRoot)
    {
        if (popup == null)
        {
            throw new ArgumentNullException(nameof(popup));
        }
        popupService.CloseAll();
        EnsureView(view, actions, uiRoot);
        uiRoot.SetActive(true);
        popupService.Open(popup);
        CloseDetailedStats();
    }

    public void RequestClose(UIPopUp popup)
    {
        if (popup != null)
        {
            popupService.ClosePeek(popup);
        }
    }

    public void BindDetailedStats(
        Button entryButton,
        GameObject panel,
        TMP_Text title,
        TMP_Text content,
        Button[] tabButtons)
    {
        if (entryButton != null)
        {
            entryButton.interactable = true;
        }
        detailedStatsPanel = panel;
        detailedStatsTitle = title;
        detailedStatsText = content;
        detailedStatsTabButtons = tabButtons ?? Array.Empty<Button>();
        CloseDetailedStats();
    }

    public void OpenDetailedStats(CharacterActor actor)
    {
        if (actor == null || detailedStatsPanel == null)
        {
            return;
        }

        selectedDetailedStatsTab = CharacterDetailedStatsTab.Summary;
        detailedStatsPanel.SetActive(true);
        RefreshDetailedStats(actor);
    }

    public void CloseDetailedStats()
    {
        detailedStatsPanel?.SetActive(false);
    }

    public void ShowDetailedStatsTab(CharacterActor actor, CharacterDetailedStatsTab tab)
    {
        selectedDetailedStatsTab = tab;
        RefreshDetailedStats(actor);
    }

    public void RefreshDetailedStats(CharacterActor actor)
    {
        if (actor == null
            || detailedStatsPanel == null
            || !detailedStatsPanel.activeInHierarchy)
        {
            return;
        }

        CharacterDetailedStatsSnapshot snapshot = detailedStatsRuntime.GetSnapshot(actor);
        if (detailedStatsTitle != null)
        {
            detailedStatsTitle.text =
                $"{snapshot.DisplayName} · {CharacterDetailedStatsRuntime.TabLabel(selectedDetailedStatsTab)}";
        }

        if (detailedStatsText != null)
        {
            StringBuilder builder = new StringBuilder(2048);
            foreach (CharacterDetailedStatRow row in snapshot.GetRows(selectedDetailedStatsTab))
            {
                builder.Append("<b>").Append(row.Label).Append("</b>  ")
                    .Append(row.Value).AppendLine();
                if (!string.IsNullOrWhiteSpace(row.Detail))
                {
                    builder.Append("<color=#B8B5AD>")
                        .Append(row.Detail)
                        .Append("</color>")
                        .AppendLine();
                }
                builder.AppendLine();
            }
            detailedStatsText.text = builder.Length > 0
                ? builder.ToString().TrimEnd()
                : CharacterSummaryUiTextQuery.Get(
                    "CharacterSummary.Detailed.Empty");
        }

        for (int i = 0; i < detailedStatsTabButtons.Length; i++)
        {
            DungeonUiTheme.StyleButton(
                detailedStatsTabButtons[i],
                selected: i == (int)selectedDetailedStatsTab);
        }
    }

    private void EnsureView(
        ICharacterSummaryGeneratedView view,
        CharacterSummaryViewActions actions,
        GameObject uiRoot)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }
        if (actions == null)
        {
            throw new ArgumentNullException(nameof(actions));
        }
        if (uiRoot == null)
        {
            throw new ArgumentNullException(nameof(uiRoot));
        }

        viewFactory.Ensure(view, actions, uiRoot);
        viewFactory.ApplyFonts(uiRoot.transform);
    }
}
