namespace DungeonStory.Foundation.Facts
{
    /// <summary>Portable bounds for frozen identities and text; no presentation or storage authority.</summary>
    public static class DomainFactValueSyntax
    {
        public const int MaximumNameCharacters = 256;

        public static bool IsIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 192) return false;
            foreach (char current in value)
                if (!(current >= 'a' && current <= 'z' || current >= 'A' && current <= 'Z'
                    || current >= '0' && current <= '9' || current is '.' or ':' or '_' or '-' or '/'))
                    return false;
            return true;
        }

        public static bool IsText(string value, int maximumLength, bool allowEmpty)
        {
            if (value == null || value.Length > maximumLength
                || !allowEmpty && string.IsNullOrWhiteSpace(value)) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (char.IsHighSurrogate(current))
                {
                    if (++index >= value.Length || !char.IsLowSurrogate(value[index])) return false;
                }
                else if (char.IsLowSurrogate(current)) return false;
            }
            return true;
        }
    }
}
