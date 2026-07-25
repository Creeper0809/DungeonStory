using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct EventAlertViewPresenterContext
{
    public EventAlertViewPresenterContext(
        Transform buttonRoot,
        GameObject detailPanel,
        TMP_Text detailText,
        Action<EventAlertRecord> openRecord,
        Func<int, bool> executeChoice,
        Action closeDetail)
    {
        ButtonRoot = buttonRoot;
        DetailPanel = detailPanel;
        DetailText = detailText;
        OpenRecord = openRecord;
        ExecuteChoice = executeChoice;
        CloseDetail = closeDetail;
    }

    public Transform ButtonRoot { get; }
    public GameObject DetailPanel { get; }
    public TMP_Text DetailText { get; }
    public Action<EventAlertRecord> OpenRecord { get; }
    public Func<int, bool> ExecuteChoice { get; }
    public Action CloseDetail { get; }
}

public interface IEventAlertViewPresenter
{
    bool IsDetailVisible { get; }
    void EnsureRuntimeUI();
    void DestroyRuntimeUI();
    void CreateButton(EventAlertRecord record);
    void UpdateButton(EventAlertRecord record);
    void OpenDetail(EventAlertRecord record);
    void CloseDetail();
}

public interface IEventAlertViewPresenterFactory
{
    IEventAlertViewPresenter Create(EventAlertViewPresenterContext context);
}

public sealed class EventAlertViewPresenterFactory : IEventAlertViewPresenterFactory
{
    private readonly IEventAlertCanvasProvider canvasProvider;
    private readonly IEventAlertViewUiFactory viewUiFactory;
    private readonly IEventAlertButtonFactory buttonFactory;

    public EventAlertViewPresenterFactory(
        IEventAlertCanvasProvider canvasProvider,
        IEventAlertViewUiFactory viewUiFactory,
        IEventAlertButtonFactory buttonFactory)
    {
        this.canvasProvider = canvasProvider
            ?? throw new ArgumentNullException(nameof(canvasProvider));
        this.viewUiFactory = viewUiFactory
            ?? throw new ArgumentNullException(nameof(viewUiFactory));
        this.buttonFactory = buttonFactory
            ?? throw new ArgumentNullException(nameof(buttonFactory));
    }

    public IEventAlertViewPresenter Create(EventAlertViewPresenterContext context)
    {
        return new EventAlertViewPresenter(context, canvasProvider, viewUiFactory, buttonFactory);
    }
}

public sealed class EventAlertViewPresenter : IEventAlertViewPresenter
{
    private readonly Dictionary<int, Button> buttonsById = new Dictionary<int, Button>();
    private readonly EventAlertChoicePresenter choicePresenter;
    private readonly Action<EventAlertRecord> openRecord;
    private readonly Func<int, bool> executeChoice;
    private readonly Action closeDetail;
    private readonly IEventAlertCanvasProvider canvasProvider;
    private readonly IEventAlertViewUiFactory viewUiFactory;
    private readonly IEventAlertButtonFactory buttonFactory;

    private Transform buttonRoot;
    private GameObject detailPanel;
    private TMP_Text detailText;
    private GameObject runtimeRoot;
    private RectTransform buttonViewportRect;
    private RectTransform buttonContentRect;
    private ScrollRect buttonScrollRect;

    public EventAlertViewPresenter(
        EventAlertViewPresenterContext context,
        IEventAlertCanvasProvider canvasProvider,
        IEventAlertViewUiFactory viewUiFactory,
        IEventAlertButtonFactory buttonFactory)
    {
        buttonRoot = context.ButtonRoot;
        detailPanel = context.DetailPanel;
        detailText = context.DetailText;
        openRecord = context.OpenRecord
            ?? throw new ArgumentNullException(nameof(context.OpenRecord));
        executeChoice = context.ExecuteChoice
            ?? throw new ArgumentNullException(nameof(context.ExecuteChoice));
        closeDetail = context.CloseDetail
            ?? throw new ArgumentNullException(nameof(context.CloseDetail));
        this.canvasProvider = canvasProvider
            ?? throw new ArgumentNullException(nameof(canvasProvider));
        this.viewUiFactory = viewUiFactory
            ?? throw new ArgumentNullException(nameof(viewUiFactory));
        this.buttonFactory = buttonFactory
            ?? throw new ArgumentNullException(nameof(buttonFactory));
        choicePresenter = new EventAlertChoicePresenter(buttonFactory);
    }

    public bool IsDetailVisible => detailPanel != null && detailPanel.activeSelf;

    public void EnsureRuntimeUI()
    {
        Canvas canvas = canvasProvider.GetOrCreateCanvas();

        if (runtimeRoot == null)
        {
            runtimeRoot = viewUiFactory.CreateRuntimeRoot(canvas);
            RefreshRuntimeRaycasters();
        }

        BringRuntimeUiToFront();

        viewUiFactory.BindExistingButtonRootReferences(
            buttonRoot,
            out buttonViewportRect,
            out buttonContentRect,
            out buttonScrollRect);
        if (!viewUiFactory.IsButtonRootReady(buttonRoot, buttonContentRect, buttonViewportRect, buttonScrollRect))
        {
            viewUiFactory.CreateButtonRoot(
                runtimeRoot.transform,
                canvas,
                buttonRoot,
                out buttonRoot,
                out buttonViewportRect,
                out buttonContentRect,
                out buttonScrollRect);
        }
        else
        {
            EventAlertLayout.ConfigureButtonViewport(canvas, buttonViewportRect, buttonContentRect);
        }

        if (detailPanel == null)
        {
            detailPanel = viewUiFactory.CreateDetailPanel(
                runtimeRoot.transform,
                closeDetail.Invoke,
                out detailText);
            detailPanel.SetActive(false);
        }
        else if (runtimeRoot != null && detailPanel.transform.parent != runtimeRoot.transform)
        {
            detailPanel.transform.SetParent(runtimeRoot.transform, false);
        }

        if (detailPanel != null)
        {
            detailPanel.transform.SetAsLastSibling();
        }

        if (buttonViewportRect != null)
        {
            buttonViewportRect.SetAsLastSibling();
        }

        if (buttonContentRect != null)
        {
            buttonContentRect.SetAsLastSibling();
        }

        if (buttonRoot != null)
        {
            buttonRoot.SetAsLastSibling();
        }

        if (detailPanel != null)
        {
            detailPanel.transform.SetAsLastSibling();
        }

        if (detailText == null && detailPanel != null)
        {
            detailText = detailPanel.GetComponentInChildren<TMP_Text>(true);
        }

        RefreshRuntimeRaycasters();
    }

    public void DestroyRuntimeUI()
    {
        choicePresenter.Clear();
        ClearButtons();
        if (runtimeRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(runtimeRoot);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(runtimeRoot);
        }
    }

    public void CreateButton(EventAlertRecord record)
    {
        EnsureRuntimeUI();
        if (buttonRoot == null)
        {
            return;
        }

        Button button = buttonFactory.CreateAlertButton(
            buttonRoot,
            record,
            () => HandleAlertButtonClick(record));
        if (button == null)
        {
            return;
        }

        buttonsById[record.Id] = button;
        LayoutButtons();
    }

    public void UpdateButton(EventAlertRecord record)
    {
        if (!buttonsById.TryGetValue(record.Id, out Button button) || button == null)
        {
            return;
        }

        buttonFactory.UpdateAlertButton(button, record);
    }

    public void OpenDetail(EventAlertRecord record)
    {
        EnsureRuntimeUI();
        if (record == null || detailPanel == null || detailText == null)
        {
            return;
        }

        detailPanel.SetActive(true);
        BringRuntimeUiToFront();
        detailPanel.transform.SetAsLastSibling();
        detailText.text = record.ToDetailText();
        choicePresenter.Rebuild(detailPanel.transform, record, executeChoice);
    }

    public void CloseDetail()
    {
        if (detailPanel != null)
        {
            detailPanel.SetActive(false);
        }
    }

    private void HandleAlertButtonClick(EventAlertRecord record)
    {
        if (record == null)
        {
            return;
        }

        openRecord(record);
        if (record.Choices.Count == 1 && record.Choices[0]?.Callback != null)
        {
            executeChoice(0);
        }
    }

    private void LayoutButtons()
    {
        EventAlertLayout.LayoutButtons(buttonRoot, buttonContentRect, buttonViewportRect, buttonScrollRect);
    }

    private void BringRuntimeUiToFront()
    {
        if (runtimeRoot != null)
        {
            runtimeRoot.transform.SetAsLastSibling();
        }
        else if (detailPanel != null)
        {
            detailPanel.transform.SetAsLastSibling();
        }
    }

    private void RefreshRuntimeRaycasters()
    {
        if (runtimeRoot == null)
        {
            return;
        }

        GraphicRaycaster[] raycasters =
            runtimeRoot.GetComponentsInChildren<GraphicRaycaster>(true);
        foreach (GraphicRaycaster raycaster in raycasters)
        {
            if (raycaster == null || !raycaster.enabled)
            {
                continue;
            }

            raycaster.enabled = false;
            raycaster.enabled = true;
        }
    }

    private void ClearButtons()
    {
        foreach (Button button in buttonsById.Values)
        {
            buttonFactory.Release(button);
        }

        buttonsById.Clear();
    }

}
