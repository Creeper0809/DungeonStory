using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

/// <summary>
/// Character DTOs own public fields, not derived getters. Preserve absent
/// ultimate skills, drawbacks and requests as null; Unity inline serialization
/// materializes them into empty objects and changes gameplay null checks.
/// </summary>
internal static class DungeonCharacterWorldSaveCodec
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new PublicDtoFieldsResolver(),
        Culture = System.Globalization.CultureInfo.InvariantCulture,
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include,
        TypeNameHandling = TypeNameHandling.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Error
    };

    internal static string Serialize(DungeonCharacterWorldSaveData payload) =>
        SerializeFields(payload);

    internal static string SerializeFields(object payload) =>
        JsonConvert.SerializeObject(payload ?? throw new ArgumentNullException(nameof(payload)), Settings);

    private sealed class PublicDtoFieldsResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !field.IsInitOnly && !field.IsDefined(typeof(NonSerializedAttribute), true))
                .ToArray();
            // This codec deliberately supports public-field DTOs. A newly added
            // property-only value or private serialized authority must get an
            // explicit contract, not be silently saved as {}.
            bool privateAuthority = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.GetCustomAttributes(true).Any(attribute =>
                    attribute.GetType().FullName is "UnityEngine.SerializeField" or "UnityEngine.SerializeReference"));
            if (typeof(UnityEngine.Object).IsAssignableFrom(type) || privateAuthority
                || fields.Length == 0 && type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 0)
                throw new InvalidOperationException("Character save requires a public-field DTO contract: " + type.FullName);
            return fields.Select(field => CreateProperty(field, MemberSerialization.Fields)).ToList();
        }
    }
}
