using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using DungeonStory.Foundation;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public class RunResultPanel : MonoBehaviour
{
    private TMP_Text detailText;
    private Button nextRunButton;
    private IDungeonRunTransitionService transitionService;
    private IGameTimeScaleController timeScaleController;
    private IRunResultTextQuery textQuery;
    private IRunResultThemeQuery theme;
    private bool pauseCaptured;
    private float previousTimeScale = 1f;

    [Inject]
    public void Construct(
        IDungeonRunTransitionService transitionService,
        IGameTimeScaleController timeScaleController,
        IRunResultTextQuery textQuery,
        IRunResultThemeQuery theme)
    {
        this.transitionService = transitionService
            ?? throw new System.ArgumentNullException(nameof(transitionService));
        this.timeScaleController = timeScaleController
            ?? throw new System.ArgumentNullException(nameof(timeScaleController));
        this.textQuery = textQuery
            ?? throw new System.ArgumentNullException(nameof(textQuery));
        this.theme = theme
            ?? throw new System.ArgumentNullException(nameof(theme));
    }

    public void Render(RunResultSnapshot result)
    {
        EnsureView();
        if (!pauseCaptured)
        {
            pauseCaptured = true;
            previousTimeScale = timeScaleController.Scale;
        }

        timeScaleController.Scale = 0f;
        gameObject.SetActive(true);
        if (detailText != null)
        {
            detailText.text = result != null
                ? result.ToDetailText()
                : textQuery.Get(RunResultTextId.EmptyResult);
            detailText.color = theme.TextPrimary;
        }

        if (nextRunButton != null)
        {
            nextRunButton.interactable = transitionService != null && !transitionService.IsTransitioning;
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        if (pauseCaptured)
        {
            timeScaleController.Scale = previousTimeScale;
            pauseCaptured = false;
        }
    }

    private void EnsureView()
    {
        if (detailText != null && nextRunButton != null) return;

        detailText = transform.Find("RunResultText")?.GetComponent<TMP_Text>()
            ?? GetComponentInChildren<TMP_Text>(true);
        nextRunButton = transform.Find("NextRunButton")?.GetComponent<Button>();
        if (nextRunButton != null)
        {
            nextRunButton.onClick.RemoveListener(HandleNextRun);
            nextRunButton.onClick.AddListener(HandleNextRun);
        }
    }

    private void HandleNextRun()
    {
        if (transitionService == null || transitionService.IsTransitioning)
        {
            return;
        }

        if (nextRunButton != null)
        {
            nextRunButton.interactable = false;
        }

        transitionService.StartNextRun();
    }

    private void OnDestroy()
    {
        if (nextRunButton != null)
        {
            nextRunButton.onClick.RemoveListener(HandleNextRun);
        }
    }
}
