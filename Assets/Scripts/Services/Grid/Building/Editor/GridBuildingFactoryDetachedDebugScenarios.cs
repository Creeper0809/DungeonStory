#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GridBuildingFactoryDetachedDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Grid Foundation/Verify Detached Building Factory")]
    public static void RunFromMenu()
    {
        Verify();
        Debug.Log("GRID_BUILDING_FACTORY_DETACHED=PASS");
    }

    public static void Verify()
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        Require(definition != null, "Detached factory fixture definition is missing.");

        RecordingObjectFactory objects = new();
        RecordingVisual visual = new();
        int callbacks = 0;
        GridBuildingFactory factory = new(
            visual,
            _ => callbacks++,
            objects);
        Grid grid = new(4, 1);

        BuildableObject published = factory.CreateDetached(
            grid,
            definition,
            new Vector2Int(1, 0));
        Require(published != null
                && !published.gameObject.activeSelf
                && objects.DetachedCreates == 1
                && objects.LiveCreates == 0
                && callbacks == 1
                && visual.Draws == 0,
            "Detached creation exposed a live object or visual before publication.");

        factory.PublishDetached(
            published,
            definition,
            new Vector2Int(1, 0));
        Require(published.gameObject.activeSelf && visual.Draws == 1,
            "Detached publication did not activate and draw exactly once.");

        BuildableObject discarded = factory.CreateDetached(
            grid,
            definition,
            new Vector2Int(2, 0));
        factory.DiscardDetached(discarded);
        Require(discarded == null
                && callbacks == 2
                && visual.Draws == 1,
            "Detached discard leaked an object or published a visual.");

        Object.DestroyImmediate(published.gameObject);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RecordingObjectFactory : IGridBuildingObjectFactory
    {
        public int LiveCreates { get; private set; }
        public int DetachedCreates { get; private set; }

        public BuildableObject Create(
            Grid grid,
            BuildingSO buildingData,
            Vector2Int selectPos)
        {
            LiveCreates++;
            return CreateObject(active: true);
        }

        public BuildableObject CreateDetached(
            Grid grid,
            BuildingSO buildingData,
            Vector2Int selectPos)
        {
            DetachedCreates++;
            return CreateObject(active: false);
        }

        private static BuildableObject CreateObject(bool active)
        {
            GameObject gameObject = new("GridBuildingFactoryDetachedFixture");
            gameObject.SetActive(active);
            return gameObject.AddComponent<BuildableObject>();
        }
    }

    private sealed class RecordingVisual : IGridBuildingVisual
    {
        public int Draws { get; private set; }

        public void DrawBuilding(BuildingSO buildingData, Vector2Int position) =>
            Draws++;

        public void DeleteBuilding(BuildingSO buildingData, Vector2Int position)
        {
        }
    }
}
#endif
