using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IResourcesAssetLoader
{
    T LoadOptional<T>(string resourcePath) where T : UnityEngine.Object;
    T LoadRequired<T>(string resourcePath) where T : UnityEngine.Object;
    IReadOnlyCollection<T> LoadAllOptional<T>(string resourcePath) where T : UnityEngine.Object;
    IReadOnlyCollection<T> LoadAllRequired<T>(string resourcePath) where T : UnityEngine.Object;
}

public sealed class UnityResourcesAssetLoader : IResourcesAssetLoader
{
    public T LoadOptional<T>(string resourcePath) where T : UnityEngine.Object
    {
        ValidateResourcePath(resourcePath);
        return Resources.Load<T>(resourcePath);
    }

    public T LoadRequired<T>(string resourcePath) where T : UnityEngine.Object
    {
        T asset = LoadOptional<T>(resourcePath);
        if (asset == null)
        {
            throw new InvalidOperationException(
                $"Required resource asset is missing: Resources/{resourcePath}");
        }

        return asset;
    }

    public IReadOnlyCollection<T> LoadAllRequired<T>(string resourcePath) where T : UnityEngine.Object
    {
        IReadOnlyCollection<T> assets = LoadAllOptional<T>(resourcePath);

        if (assets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Required resource asset collection is empty: Resources/{resourcePath}");
        }

        return assets;
    }

    public IReadOnlyCollection<T> LoadAllOptional<T>(string resourcePath) where T : UnityEngine.Object
    {
        ValidateResourcePath(resourcePath);

        return Resources
            .LoadAll<T>(resourcePath)
            .Where((asset) => asset != null)
            .ToArray();
    }

    private static void ValidateResourcePath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new ArgumentException("Resource path is required.", nameof(resourcePath));
        }
    }
}
