using DamageNumbersPro;
using DG.Tweening;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
public enum NumberCondition
{
    ONBUYINGITEM,
    ONEARNMONEY
}
public class GameManager : SerializedMonoBehaviour
{
    [FormerlySerializedAs("gameData")]
    [SerializeField] private GameData gameDataSettings;
    public Dictionary<NumberCondition,DamageNumber> numbers;

    public GameData Settings => gameDataSettings;
    public GameSessionState gameData =>
        sessionStateProvider != null
        && sessionStateProvider.TryGetSessionState(out GameSessionState state)
            ? state
            : null;
    public bool isPause
    {
        get => gameData?.IsPaused ?? false;
        set => gameSpeedController?.SetPaused(value);
    }

    private IOwnerRunManagerProvider ownerRunManagerProvider;
    private OwnerRunManager ownerRunManager;
    private IGameCalendar gameCalendar;
    private IGameSpeedController gameSpeedController;
    private IGameSessionStateProvider sessionStateProvider;

    [Inject]
    public void ConstructGameManager(
        IOwnerRunManagerProvider ownerRunManagerProvider,
        IGameCalendar gameCalendar,
        IGameSpeedController gameSpeedController,
        IGameSessionStateProvider sessionStateProvider)
    {
        this.ownerRunManagerProvider = ownerRunManagerProvider
            ?? throw new System.ArgumentNullException(nameof(ownerRunManagerProvider));
        this.gameCalendar = gameCalendar
            ?? throw new System.ArgumentNullException(nameof(gameCalendar));
        this.gameSpeedController = gameSpeedController
            ?? throw new System.ArgumentNullException(nameof(gameSpeedController));
        this.sessionStateProvider = sessionStateProvider
            ?? throw new System.ArgumentNullException(nameof(sessionStateProvider));
    }

    private void Awake()
    {
        if (gameDataSettings == null)
        {
            throw new System.InvalidOperationException(
                $"{nameof(GameManager)} requires a {nameof(GameData)} settings asset.");
        }

        DOTween.Init();
    }
    void Start()
    {
        if (ownerRunManagerProvider != null
            && ownerRunManagerProvider.TryGetManager(out ownerRunManager)
            && ownerRunManager != null)
        {
            ownerRunManager.OnOwnerSelected += HandleOwnerSelected;
            if (ownerRunManager.CurrentOwnerActor != null)
            {
                gameCalendar.Start();
            }

            return;
        }

        gameCalendar.Start();
    }

    private void HandleOwnerSelected(CharacterSO ownerData)
    {
        if (ownerData != null)
        {
            gameCalendar.Start();
        }
    }

    public void ChangeGameSpeed()
    {
        gameSpeedController.CycleSpeed();
    }
    public void TogglePause()
    {
        gameSpeedController.TogglePause();
    }

    private void OnDestroy()
    {
        if (ownerRunManager != null)
        {
            ownerRunManager.OnOwnerSelected -= HandleOwnerSelected;
        }
    }

}
