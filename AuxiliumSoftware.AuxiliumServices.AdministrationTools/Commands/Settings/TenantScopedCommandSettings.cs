using Spectre.Console.Cli;
using System.ComponentModel;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands.Settings;

public class TenantScopedCommandSettings : CommandSettings
{
    [CommandOption("-t|--tenant <DOMAIN>")]
    [Description("Domain of the tenant to operate on. If omitted you'll be asked to pick one (or the only tenant is used).")]
    public string? Tenant { get; set; }
}
