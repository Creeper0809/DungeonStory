using DamageNumbersPro;
using DG.Tweening;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
public enum TimeOfDay
{
    None,
    Morning,
    Noon,
    Evening,
    Night,
}
public enum NumberCondition
{
    ONBUYINGITEM,
    ONEARNMONEY
}
public class GameManager : SerializedMonoBehaviour
{
    public GameData gameData;
    public Dictionary<NumberCondition,DamageNumber> numbers;
    public bool isPause;

    private IOwnerRunManagerProvider ownerRunManagerProvider;
    private OwnerRunManager ownerRunManager;
    private Coroutine timerRoutine;
    private IGameEventBus gameEventBus;
    private IGameClock gameClock;
    private IGameTimeScaleController timeScaleController;

    [Inject]
    public void ConstructGameManager(
        IOwnerRunManagerProvider ownerRunManagerProvider,
        IGameEventBus gameEventBus,
        IGameClock gameClock,
        IGameTimeScaleController timeScaleController)
    {
        this.ownerRunManagerProvider = ownerRunManagerProvider;
        this.gameEventBus = gameEventBus
            ?? throw new System.ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock
            ?? throw new System.ArgumentNullException(nameof(gameClock));
        this.timeScaleController = timeScaleController
            ?? throw new System.ArgumentNullException(nameof(timeScaleController));
    }

    private void Awake()
    {
        DOTween.Init();
    }
    void Start()
    {
        gameData.gameSpeed.Initialize(1);
        gameData.holdingMoney.Initialize(5000);
        gameData.day.Initialize(1);

        if (ownerRunManagerProvider != null
            && ownerRunManagerProvider.TryGetManager(out ownerRunManager)
            && ownerRunManager != null)
        {
            ownerRunManager.OnOwnerSelected += HandleOwnerSelected;
            if (ownerRunManager.CurrentOwnerActor != null)
            {
                StartGameClock();
            }

            return;
        }

        StartGameClock();
    }

    private void HandleOwnerSelected(CharacterSO ownerData)
    {
        if (ownerData != null)
        {
            StartGameClock();
        }
    }

    private void StartGameClock()
    {
        if (timerRoutine != null)
        {
            return;
        }

        timerRoutine = StartCoroutine(Timer());
        gameEventBus.Publish(new OperatingDayStartedEvent(gameData.day.Value));
    }
    public void ConvertSecondsToGameTime()
    {
        float dayFraction = gameData.curTime.Value / 180;

        float gameHours = dayFraction * 24;
        gameData.hour.Value = (int)gameHours % 24;
    }
    public void ChangeGameSpeed()
    {
        gameData.gameSpeed.Value = gameData.gameSpeed.Value % 5 + 1;
        if (!isPause)
        {
            timeScaleController.Scale = gameData.gameSpeed.Value;
        }
    }
    public void TogglePause()
    {
        isPause = !isPause;
        if (isPause)
        {
            timeScaleController.Scale = 0f;
        }
        else
        {
            timeScaleController.Scale = gameData.gameSpeed.Value;
        }
    }
    // Update is called once per frame
    void Update()
    {
        ConvertSecondsToGameTime();
        if (gameData.curTime.Value < 40f)
        {
            gameData.timeOfDay.Value = TimeOfDay.Night;
        }
        else if (gameData.curTime.Value < 50f)
        {
            gameData.timeOfDay.Value = TimeOfDay.Morning;
        }
        else if(gameData.curTime.Value < 145f)
        {
            gameData.timeOfDay.Value = TimeOfDay.Noon;
        }
        else if(gameData.curTime.Value < 155f)
        {
            gameData.timeOfDay.Value = TimeOfDay.Evening;
        }
        else
        {
            gameData.timeOfDay.Value = TimeOfDay.Night;
        }
    }
    public IEnumerator Timer()
    {
        while (true)
        {
            gameData.curTime.Value = 0;
            while (gameData.curTime.Value <= 180)
            {
                gameData.curTime.Value += gameClock.DeltaTime;
                yield return null;
            }
            gameEventBus.Publish(new OperatingDayEndedEvent(gameData.day.Value));
            gameData.day.Value++;
            gameEventBus.Publish(new OperatingDayStartedEvent(gameData.day.Value));
        }
    }

    private void OnDestroy()
    {
        if (ownerRunManager != null)
        {
            ownerRunManager.OnOwnerSelected -= HandleOwnerSelected;
        }
    }

}
