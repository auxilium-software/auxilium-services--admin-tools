using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Common;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.EntityModels;
using AuxiliumSoftware.AuxiliumServices.Common.Enumerators;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Services.Implementations;
using AuxiliumSoftware.AuxiliumServices.Common.Utilities;
using Konscious.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Security.Cryptography;
using System.Text.Json;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Tools;

public sealed class SetInitialSystemSettingsTool(
    IConfiguration configuration,
    IPasswordService passwordService
) : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold blue]===== Set Initial System Settings =====[/]");
        AnsiConsole.WriteLine();

        using var dbContext = MariaDBInteractions.GetDbContext(configuration);

        SystemSettingEntityModel instance_branding_name = new()
        {
            Id = UUIDUtilities.GenerateV5(objectType: DatabaseObjectTypeEnum.System_SettingEntry),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = null,
            ConfigKey = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingKeyEnum.Instance_Branding_Name,
            ConfigValue = JsonSerializer.Serialize(AnsiConsole.Ask<string>("[green]instance.branding.name:[/]")),
            ValueType = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingValueTypeEnum.Json,
            ReasonForModification = "Set through the Admin Tools CLI by a server administrator.",
        };
        SystemSettingEntityModel instance_navigation_portalBaseUrl = new()
        {
            Id = UUIDUtilities.GenerateV5(objectType: DatabaseObjectTypeEnum.System_SettingEntry),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = null,
            ConfigKey = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingKeyEnum.Instance_Navigation_PortalBaseUrl,
            ConfigValue = JsonSerializer.Serialize(AnsiConsole.Ask<string>("[green]instance.navigation.portalBaseUrl:[/]")),
            ValueType = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingValueTypeEnum.Json,
            ReasonForModification = "Set through the Admin Tools CLI by a server administrator.",
        };
        SystemSettingEntityModel instance_fqdn = new()
        {
            Id = UUIDUtilities.GenerateV5(objectType: DatabaseObjectTypeEnum.System_SettingEntry),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = null,
            ConfigKey = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingKeyEnum.Instance_Fqdn,
            ConfigValue = JsonSerializer.Serialize(AnsiConsole.Ask<string>("[green]instance.fqdn:[/]")),
            ValueType = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingValueTypeEnum.Json,
            ReasonForModification = "Set through the Admin Tools CLI by a server administrator.",
        };


        dbContext.System_Settings.Add(instance_branding_name);
        dbContext.System_Settings.Add(instance_navigation_portalBaseUrl);
        dbContext.System_Settings.Add(instance_fqdn);

        await dbContext.SaveChangesAsync(cancellationToken);

        return 0;
    }
}
