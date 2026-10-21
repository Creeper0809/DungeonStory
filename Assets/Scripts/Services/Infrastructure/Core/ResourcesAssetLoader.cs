using System;
using UnityEngine;

public interface IGameContentRootLoader
{
    ScriptableObject LoadRequiredRoot();
}

public interface ITextAssetResourceLoader
{
    TextAsset LoadTextAsset(string resourcePath);
}

public sealed class UnityGameContentRootLoader : IGameContentRootLoader, ITextAssetResourceLoader
{
    private const string RootResourcePath = "SO/GameContentCatalog";

    public ScriptableObject LoadRequiredRoot()
    {
        ScriptableObject root = (ScriptableObject)Load(
            RootResourcePath, typeof(ScriptableObject));
        if (root == null)
        {
            throw new InvalidOperationException(
                "Required root content catalog is missing: Resources/"
                + RootResourcePath);
        }

        return root;
    }

    public TextAsset LoadTextAsset(string resourcePath) =>
        (TextAsset)Load(resourcePath, typeof(TextAsset));

    private static UnityEngine.Object Load(string resourcePath, Type assetType) =>
        Resources.Load(resourcePath, assetType);
}
