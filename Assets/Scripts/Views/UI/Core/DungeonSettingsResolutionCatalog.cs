using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class DungeonSettingsResolutionCatalog
{
    public static IReadOnlyList<Vector2Int> Build()
    {
        List<Vector2Int> resolutions = new List<Vector2Int>();
        foreach (Resolution resolution in Screen.resolutions)
        {
            Vector2Int value = new Vector2Int(
                resolution.width,
                resolution.height);
            if (!resolutions.Contains(value))
            {
                resolutions.Add(value);
            }
        }

        foreach (Vector2Int fallback in new[]
                 {
                     new Vector2Int(1280, 720),
                     new Vector2Int(1600, 900),
                     new Vector2Int(1920, 1080),
                     new Vector2Int(2560, 1440)
                 })
        {
            if (!resolutions.Contains(fallback))
            {
                resolutions.Add(fallback);
            }
        }

        resolutions.Sort((left, right) =>
        {
            int area = (left.x * left.y).CompareTo(right.x * right.y);
            return area != 0 ? area : left.x.CompareTo(right.x);
        });
        return resolutions;
    }
}
