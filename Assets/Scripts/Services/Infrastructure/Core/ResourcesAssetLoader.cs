using System;
using UnityEngine;

public interface IGameContentRootLoader
{
    ScriptableObject LoadRequiredRoot();
}

public sealed class UnityGameContentRootLoader : IGameContentRootLoader
{
    private const string RootResourcePath = "SO/GameContentCatalog";

    public ScriptableObject LoadRequiredRoot()
    {
        ScriptableObject root = Resources.Load<ScriptableObject>(
            RootResourcePath);
        if (root == null)
        {
            throw new InvalidOperationException(
                "Required root content catalog is missing: Resources/"
                + RootResourcePath);
        }

        return root;
    }
}
