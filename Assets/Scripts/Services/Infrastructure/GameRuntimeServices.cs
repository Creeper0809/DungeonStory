using System;
using DamageNumbersPro;
using UnityEngine;

public interface IGameDataProvider
{
    bool TryGetGameData(out GameData gameData);
}

public interface IFloatingNumberFeedbackService
{
    bool TryShow(NumberCondition condition, Vector3 worldPosition, float value);
}

public sealed class GameManagerGameDataProvider : IGameDataProvider
{
    private readonly DungeonSceneRuntimeReferences sceneReferences;

    public GameManagerGameDataProvider(DungeonSceneRuntimeReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
    }

    public bool TryGetGameData(out GameData gameData)
    {
        GameManager gameManager = sceneReferences.GameManager;
        gameData = gameManager != null ? gameManager.gameData : null;
        return gameData != null;
    }
}

public sealed class GameManagerFloatingNumberFeedbackService : IFloatingNumberFeedbackService
{
    private readonly DungeonSceneRuntimeReferences sceneReferences;

    public GameManagerFloatingNumberFeedbackService(
        DungeonSceneRuntimeReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
    }

    public bool TryShow(NumberCondition condition, Vector3 worldPosition, float value)
    {
        GameManager manager = ResolveGameManager();
        if (manager.numbers == null
            || !manager.numbers.TryGetValue(condition, out DamageNumber number)
            || number == null)
        {
            return false;
        }

        number.Spawn(worldPosition, value);
        return true;
    }

    private GameManager ResolveGameManager()
    {
        GameManager gameManager = sceneReferences.GameManager;
        return gameManager != null
            ? gameManager
            : throw new InvalidOperationException(
                $"{nameof(IFloatingNumberFeedbackService)} requires a registered {nameof(GameManager)}.");
    }
}
