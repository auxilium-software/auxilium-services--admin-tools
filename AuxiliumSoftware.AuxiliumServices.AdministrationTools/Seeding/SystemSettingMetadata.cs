using AuxiliumSoftware.AuxiliumServices.Common.Attributes;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using System.Reflection;
using System.Text.Json.Serialization;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Seeding;

internal sealed record SystemSettingMetadata(
    SystemSettingKeyEnum Key,
    string Name,
    SystemSettingValueTypeEnum ValueType,
    string? Description,
    object? DefaultValue
)
{
    public static SystemSettingMetadata For(SystemSettingKeyEnum key)
    {
        var field = typeof(SystemSettingKeyEnum).GetField(key.ToString(), BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"'{key}' is not a member of {nameof(SystemSettingKeyEnum)}.");

        var valueType = field.GetCustomAttribute<SystemSettingExpectedValueTypeAttribute>()?.ValueType
            ?? throw new InvalidOperationException($"System setting '{key}' has no {nameof(SystemSettingExpectedValueTypeAttribute)}.");

        return new SystemSettingMetadata(
            Key:          key,
            Name:         field.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? key.ToString(),
            ValueType:    valueType,
            Description:  field.GetCustomAttribute<SystemSettingDescriptionAttribute>()?.Description,
            DefaultValue: field.GetCustomAttribute<SystemSettingDefaultValueAttribute>()?.DefaultValue
        );
    }
}
