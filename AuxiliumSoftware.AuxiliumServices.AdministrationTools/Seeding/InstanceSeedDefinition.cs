using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using System.Net;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Seeding;

internal static class InstanceSeedDefinition
{
    public const string ReasonForModification = "Set through the Admin Tools CLI during instance initialisation.";

    public static readonly IReadOnlyList<InstanceSettingSeed> PromptedSettings =
    [
        new(SystemSettingKeyEnum.Instance_Branding_Name),
        new(SystemSettingKeyEnum.Instance_Fqdn,                        SuggestFromDomain: domain => domain),
        new(SystemSettingKeyEnum.Instance_Navigation_PortalBaseUrl,    SuggestFromDomain: domain => $"https://{domain}"),
    ];

    public static readonly IReadOnlyList<IpWhitelistSeed> IpWhitelist =
    [
        new(IPAddress.Loopback,     "SYSTEM: Prevents the portal from blocking itself (IPv4 loopback)."),
        new(IPAddress.IPv6Loopback, "SYSTEM: Prevents the portal from blocking itself (IPv6 loopback)."),
    ];

    public static readonly IReadOnlyList<DataEnumeratorSeed> DataEnumerators =
    [
        new(DataEnumeratorScopeEnum.CalendarEventCategory, "/data-enumerator/builtin/calendar-event-category"),
    ];
}

internal sealed record InstanceSettingSeed(
    SystemSettingKeyEnum Key,
    Func<string, string>? SuggestFromDomain = null
);

internal sealed record IpWhitelistSeed(
    IPAddress IpAddress,
    string Justification
);

internal sealed record DataEnumeratorSeed(
    DataEnumeratorScopeEnum Scope,
    string CanonicalName,
    string? Description = null
);
