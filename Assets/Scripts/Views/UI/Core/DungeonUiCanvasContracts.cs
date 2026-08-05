using UnityEngine;

public interface IDungeonUiCanvasProvider
{
    Canvas GetOrCreateCanvas();
}

public interface IEventAlertCanvasProvider : IDungeonUiCanvasProvider
{
}
