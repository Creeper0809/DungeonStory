#if UNITY_EDITOR
using System;
using UnityEngine;

public static class WorldItemRepositoryEditorAccess
{
    public static string AddStack(
        WorldItemRepository repository,
        string itemId,
        int quantity,
        WorldItemStackState state,
        string destinationId = "",
        string sourceStorageDestinationId = "",
        Vector2Int position = default)
    {
        if (repository == null)
        {
            throw new ArgumentNullException(nameof(repository));
        }

        return repository.AddEditorTestStack(
            itemId,
            quantity,
            state,
            destinationId,
            sourceStorageDestinationId,
            components: null,
            position: position);
    }

    public static void RemoveStack(
        WorldItemRepository repository,
        string stackId)
    {
        if (repository == null)
        {
            throw new ArgumentNullException(nameof(repository));
        }
        repository.RemoveEditorTestStack(stackId);
    }

    public static bool TryRemoveStack(
        WorldItemRepository repository,
        string stackId)
    {
        if (repository == null)
        {
            throw new ArgumentNullException(nameof(repository));
        }

        return repository.TryRemoveEditorTestStack(stackId);
    }

    public static void SetQuantity(
        WorldItemRepository repository,
        string stackId,
        int quantity)
    {
        if (repository == null)
        {
            throw new ArgumentNullException(nameof(repository));
        }

        repository.SetEditorTestQuantity(stackId, quantity);
    }
}
#endif
