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

public sealed class CreateTenant(
    IConfiguration configuration,
    IPasswordService passwordService
) : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold blue]===== Tenant Creation Tool =====[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]This tool will guide you through creating a new Tenant for the configured instance of Auxilium|3.[/]");
        AnsiConsole.WriteLine();

        using var dbContext = MariaDBInteractions.GetDbContext(configuration);

        Guid tenantId = UUIDUtilities.GenerateV5(objectType: DatabaseObjectTypeEnum.Global_Tenant_Tenant);

        TenantEntityModel tenant = new()
        {
            Id = tenantId,
            CreatedAt = DateTime.UtcNow,
            Domain = AnsiConsole.Ask<string>("[green]Tenant Domain:[/]"),
            LifecycleStatus = AuxiliumServices.Common.EntityFramework.Enumerators.TenantLifecycleStatusEnum.Active,
        };
        SystemSettingEntityModel instance_branding_name = new()
        {
            Id = UUIDUtilities.GenerateV5(objectType: DatabaseObjectTypeEnum.WithinTenancy_System_SettingEntry),
            TenantId = tenantId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = null,
            ConfigKey = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingKeyEnum.Instance_Branding_Name,
            ConfigValue = JsonSerializer.Serialize(AnsiConsole.Ask<string>("[green]instance.branding.name:[/]")),
            ValueType = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingValueTypeEnum.String,
            ReasonForModification = "Set through the Admin Tools CLI by a server administrator.",
        };
        SystemSettingEntityModel instance_navigation_portalBaseUrl = new()
        {
            Id = UUIDUtilities.GenerateV5(objectType: DatabaseObjectTypeEnum.WithinTenancy_System_SettingEntry),
            TenantId = tenantId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = null,
            ConfigKey = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingKeyEnum.Instance_Navigation_PortalBaseUrl,
            ConfigValue = JsonSerializer.Serialize(AnsiConsole.Ask<string>("[green]instance.navigation.portalBaseUrl:[/]")),
            ValueType = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingValueTypeEnum.String,
            ReasonForModification = "Set through the Admin Tools CLI by a server administrator.",
        };
        SystemSettingEntityModel instance_fqdn = new()
        {
            Id = UUIDUtilities.GenerateV5(objectType: DatabaseObjectTypeEnum.WithinTenancy_System_SettingEntry),
            TenantId = tenantId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = null,
            ConfigKey = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingKeyEnum.Instance_Fqdn,
            ConfigValue = JsonSerializer.Serialize(AnsiConsole.Ask<string>("[green]instance.fqdn:[/]")),
            ValueType = AuxiliumServices.Common.EntityFramework.Enumerators.SystemSettingValueTypeEnum.String,
            ReasonForModification = "Set through the Admin Tools CLI by a server administrator.",
        };


        dbContext.Global_Tenants.Add(tenant);
        dbContext.WithinTenancy_System_Settings.Add(instance_branding_name);
        dbContext.WithinTenancy_System_Settings.Add(instance_navigation_portalBaseUrl);
        dbContext.WithinTenancy_System_Settings.Add(instance_fqdn);

        await dbContext.SaveChangesAsync(cancellationToken);

        return 0;
    }
}
