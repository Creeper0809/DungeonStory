using System;
using System.Linq;

public interface IInvasionIntruderDataProvider :
    IInvasionIntruderDataProvider<CharacterSO>
{
}

public sealed class ResourceInvasionIntruderDataProvider : IInvasionIntruderDataProvider
{
    private const int DefaultIntruderId = 2001;

    private readonly CharacterSO defaultIntruder;

    public ResourceInvasionIntruderDataProvider(IGameContentCatalog content)
    {
        CharacterSO[] matches = (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<CharacterSO>()
            .Where(definition => definition != null && definition.id == DefaultIntruderId)
            .ToArray();
        defaultIntruder = matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                $"Expected one authored default intruder with id {DefaultIntruderId}, found {matches.Length}.");
    }

    public CharacterSO GetRequiredIntruderData(CharacterSO configuredData)
    {
        if (configuredData != null)
        {
            return configuredData;
        }

        return defaultIntruder;
    }
}
