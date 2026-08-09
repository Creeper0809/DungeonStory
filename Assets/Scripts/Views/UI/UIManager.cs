using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using VContainer;

public class UIManager : SerializedMonoBehaviour
{
    public TMP_Text timeText;
    public TMP_Text holdingMoneyText;
    public TMP_Text gameSpeedText;
    public CanvasGroup touchGaurd;
    public GameSessionState gameData { get; private set; }
    [ReadOnly]
    [ShowInInspector]
    private Stack<UIPopUp> popups = new Stack<UIPopUp>();
    private IPlayerInputReader inputReader;
    private IGameSessionStateProvider sessionStateProvider;
    private IGameCalendar calendar;
    private IClimateQuery climate;
    private IPopulationHealthQuery populationHealth;
    private IDiseaseDefinitionCatalog diseaseDefinitions;

    [Inject]
    public void Construct(
        IPlayerInputReader inputReader,
        IGameSessionStateProvider sessionStateProvider,
        IGameCalendar calendar,
        IClimateQuery climate,
        IPopulationHealthQuery populationHealth,
        IDiseaseDefinitionCatalog diseaseDefinitions)
    {
        this.inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
        this.sessionStateProvider = sessionStateProvider
            ?? throw new ArgumentNullException(nameof(sessionStateProvider));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.climate = climate ?? throw new ArgumentNullException(nameof(climate));
        this.populationHealth = populationHealth
            ?? throw new ArgumentNullException(nameof(populationHealth));
        this.diseaseDefinitions = diseaseDefinitions
            ?? throw new ArgumentNullException(nameof(diseaseDefinitions));
    }

    private void Start()
    {
        if (!sessionStateProvider.TryGetSessionState(out GameSessionState state)
            || state == null)
        {
            throw new InvalidOperationException("UI requires an active game session state.");
        }

        gameData = state;
        Subscribe();
        UpdateTime();
        UpdateHoldingMoneyText(gameData.holdingMoney.Value);
        UpdateGameSpeedText(gameData.gameSpeed.Value);
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
        string epidemic = string.Join(
            ", ",
            populationHealth.GetEpidemics(declaredOnly: true)
                .Select(value => diseaseDefinitions
                    .Require(value.DiseaseId).DisplayName));
        timeText.text = $"{calendar.Year}년 {SeasonLabel(calendar.Season)} "
            + $"{calendar.DayOfSeason}일 {calendar.Hour:00}:00 · "
            + $"{climate.OutdoorTemperatureC:0.#}℃ "
            + $"{WeatherLabel(climate.WeatherFrontId)}"
            + (climate is IWeatherForecastQuery forecast
                && forecast.ObservationToolsOperational
                    ? $" · 예보 {forecast.ForecastHorizonDays}일"
                    : string.Empty)
            + (epidemic.Length > 0 ? $" · 유행: {epidemic}" : string.Empty);
    }

    private static string SeasonLabel(Season season) => season switch
    {
        Season.Spring => "봄",
        Season.Summer => "여름",
        Season.Autumn => "가을",
        Season.Winter => "겨울",
        _ => season.ToString()
    };

    private static string WeatherLabel(string weatherFrontId) =>
        weatherFrontId switch
        {
            "weather:clear" => "맑음",
            "weather:rain" => "비",
            "weather:fog" => "안개",
            "weather:heatwave" => "폭염",
            "weather:cold-snap" => "한파",
            "weather:storm" => "폭풍",
            _ => weatherFrontId ?? string.Empty
        };
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
    private void Subscribe()
    {
        gameData.gameSpeed.OnValueChange += UpdateGameSpeedText;
        gameData.holdingMoney.OnValueChange += UpdateHoldingMoneyText;
        gameData.hour.OnValueChange += OnHourChanged;
        gameData.day.OnValueChange += OnDayChanged;
    }
    private void OnDisable()
    {
        if (gameData == null)
        {
            return;
        }

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
