using System;
using System.Security.Cryptography;
using System.Text;

// Stable identities for domain commands and incidents, independent of observation.
public static class ReproductionCommandIdentity
{
    public static string Hash(string value)
    {
        using var sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return "sha256:" + BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
