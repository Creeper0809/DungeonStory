using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VContainer;

public class UIManager : SerializedMonoBehaviour
{
    public TMP_Text timeText;
    public TMP_Text holdingMoneyText;
    public TMP_Text gameSpeedText;
    public CanvasGroup touchGaurd;
    public GameData gameData;
    [ReadOnly]
    [ShowInInspector]
    private Stack<UIPopUp> popups = new Stack<UIPopUp>();
    private IPlayerInputReader inputReader;

    [Inject]
    public void Construct(IPlayerInputReader inputReader)
    {
        this.inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
    }

    public void Update()
    {
        if (inputReader == null)
        {
            return;
        }

        if (inputReader.GetKeyDown(KeyCode.Escape))
        {
            ClosePopupPeek();
        }
    }
    public void CloseAllPopup()
    {
        while (popups.Count > 0)
        {
            ClosePopupPeek();
        }
    }
    public void OpenPopup(UIPopUp popup)
    {
        if (popup == null) return;
        popup.OnOpen();
        popups.Push(popup);
    }
    public void ClosePopupPeek(UIPopUp popup)
    {
        if (popups.Count == 0 || popups.Peek() != popup) return;
        ClosePopupPeek();
    }
    public void ClosePopupPeek()
    {
        if (popups.Count == 0) return;
        UIPopUp popup = popups.Pop();
        popup.OnClose();
    }
    public void UpdateTime()
    {
        timeText.text = $"Day {gameData.day.Value} - {gameData.hour.Value}:00";
    }
    private void UpdateHoldingMoneyText(int holdingMoney)
    {
        holdingMoneyText.text = holdingMoney.ToString();
    }
    private void UpdateGameSpeedText(int gameSpeed)
    {
        gameSpeedText.text = $"X{gameSpeed}";
    }
    public void MakeTouchFalse()
    {
        touchGaurd.interactable = true ;
        touchGaurd.blocksRaycasts = true;
    }
    public void MakeTouchTrue()
    {
        touchGaurd.interactable = false;
        touchGaurd.blocksRaycasts = false;
    }
    private void OnEnable()
    {
        gameData.gameSpeed.OnValueChange += UpdateGameSpeedText;
        gameData.holdingMoney.OnValueChange += UpdateHoldingMoneyText;
        gameData.hour.OnValueChange += OnHourChanged;
        gameData.day.OnValueChange += OnDayChanged;
    }
    private void OnDisable()
    {
        gameData.gameSpeed.OnValueChange -= UpdateGameSpeedText;
        gameData.holdingMoney.OnValueChange -= UpdateHoldingMoneyText;
        gameData.hour.OnValueChange -= OnHourChanged;
        gameData.day.OnValueChange -= OnDayChanged;
    }

    private void OnHourChanged(int _)
    {
        UpdateTime();
    }

    private void OnDayChanged(int _)
    {
        UpdateTime();
    }

    private IPlayerInputReader RequireInputReader()
    {
        return inputReader
            ?? throw new InvalidOperationException($"{nameof(UIManager)} requires {nameof(IPlayerInputReader)} injection.");
    }
}
