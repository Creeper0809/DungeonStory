using System;
using System.Globalization;

/// <summary>
/// Canonical authored identity for every building definition. Legacy numeric
/// definitions remain first-class authorities and project to building:&lt;id&gt;;
/// newer content may author an explicit stable content definition ID.
/// </summary>
public static class BuildingDefinitionIdentity
{
    public static string Resolve(BuildingSO definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        string authored = definition.AuthoredContentDefinitionId;
        if (authored.Length > 0)
        {
            if (string.IsNullOrWhiteSpace(authored)
                || !string.Equals(authored, authored.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Building definition ID is noncanonical.");
            }
            return authored;
        }
        if (definition.id < 0)
        {
            throw new InvalidOperationException(
                "Building has neither a definition ID nor numeric authority.");
        }
        return "building:" + definition.id.ToString(CultureInfo.InvariantCulture);
    }
}
