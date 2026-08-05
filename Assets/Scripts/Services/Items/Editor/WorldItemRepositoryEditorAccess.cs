#if UNITY_EDITOR
using System;

public static class WorldItemRepositoryEditorAccess
{
    public static string AddStack(
        WorldItemRepository repository,
        string itemId,
        int quantity,
        WorldItemStackState state,
        string destinationId = "",
        string sourceStorageDestinationId = "")
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
            sourceStorageDestinationId);
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
}
#endif
