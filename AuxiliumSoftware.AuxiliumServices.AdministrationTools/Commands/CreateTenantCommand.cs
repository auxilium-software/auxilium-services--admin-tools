using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Interaction;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Seeding;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Services;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Tenancy;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.EntityModels;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using AuxiliumSoftware.AuxiliumServices.Common.Enumerators;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Tenancy.TenantLookup;
using AuxiliumSoftware.AuxiliumServices.Common.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands;

public sealed class CreateTenantCommand(
    AuxiliumDbContext dbContext,
    ITenantLookup tenantLookup,
    TenantSelector tenantSelector,
    AdministratorAccountService accountService,
    ISystemSettingsService systemSettings,
    IWebApplicationFirewallService webApplicationFirewall,
    IDataEnumeratorService dataEnumerators
) : AsyncCommand<CreateTenantCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--domain <DOMAIN>")]
        [Description("Domain the new tenant is served on (e.g. portal.example.org). Prompted for if omitted.")]
        public string? Domain { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new Rule("[bold blue]Create Tenant[/]").LeftJustified());
        AnsiConsole.MarkupLine("[blue]This will create a new tenant and its first administrator on the configured Auxilium|3 instance.[/]");
        AnsiConsole.WriteLine();

        var domain = await AskDomainAsync(settings.Domain, cancellationToken);
        if (domain is null)
            return 1;
        AnsiConsole.WriteLine();

        var settingValues = new List<(SystemSettingMetadata Metadata, object Value)>();
        foreach (var seed in TenantSeedDefinition.PromptedSettings)
        {
            var metadata = SystemSettingMetadata.For(seed.Key);
            var value = Prompts.AskSystemSetting(metadata, seed.SuggestFromDomain?.Invoke(domain));
            settingValues.Add((metadata, value));
        }

        AnsiConsole.Write(new Rule("[bold]First administrator[/]").LeftJustified());
        var adminEmail = Prompts.AskEmailAddress();
        var adminName = Prompts.AskRequiredText("Full name");
        var adminPassword = Prompts.AskNewPassword();
        AnsiConsole.WriteLine();

        var summary = new Table().Border(TableBorder.Rounded).AddColumn("Field").AddColumn("Value");
        summary.AddRow("Domain", Markup.Escape(domain));
        foreach (var (metadata, value) in settingValues)
            summary.AddRow(Markup.Escape(metadata.Name), Markup.Escape(value.ToString() ?? ""));
        summary.AddRow("IP whitelist", Markup.Escape(string.Join(", ", TenantSeedDefinition.IpWhitelist.Select(i => i.IpAddress))));
        summary.AddRow("Data enumerators", Markup.Escape(string.Join(", ", TenantSeedDefinition.DataEnumerators.Select(e => e.CanonicalName))));
        summary.AddRow("Administrator", Markup.Escape($"{adminName} <{adminEmail}>"));
        AnsiConsole.Write(summary);

        if (!AnsiConsole.Confirm("[yellow]Create this tenant?[/]", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[blue]Cancelled, nothing was written.[/]");
            return 0;
        }

        var tenantId = UUIDUtilities.GenerateV5(DatabaseObjectTypeEnum.Global_Tenant_Tenant);
        UserEntityModel admin;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            dbContext.Global_Tenants.Add(new TenantEntityModel
            {
                Id = tenantId,
                CreatedAt = DateTime.UtcNow,
                Domain = domain,
                LifecycleStatus = TenantLifecycleStatusEnum.Active,
            });
            await dbContext.SaveChangesAsync(cancellationToken);

            tenantSelector.Pin(tenantId);

            admin = await accountService.CreateAdministratorAsync(adminEmail, adminName, adminPassword, cancellationToken);

            foreach (var (metadata, value) in settingValues)
                await systemSettings.SetAsync(metadata.Key, value, admin.Id, TenantSeedDefinition.ReasonForModification, cancellationToken);

            foreach (var entry in TenantSeedDefinition.IpWhitelist)
                await webApplicationFirewall.WhitelistIpAddressAsync(entry.IpAddress, entry.Justification, admin, permanent: true, ct: cancellationToken);

            foreach (var enumerator in TenantSeedDefinition.DataEnumerators)
                await dataEnumerators.CreateEnumeratorAsync(enumerator.Scope, enumerator.CanonicalName, enumerator.Description, admin, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ Tenant created.[/]");

        var result = new Table().Border(TableBorder.Rounded).AddColumn("Field").AddColumn("Value");
        result.AddRow("Tenant ID", tenantId.ToString());
        result.AddRow("Domain", Markup.Escape(domain));
        result.AddRow("Administrator ID", admin.Id.ToString());
        result.AddRow("Administrator email", Markup.Escape(adminEmail));
        AnsiConsole.Write(result);

        return 0;
    }

    private async Task<string?> AskDomainAsync(string? fromArgs, CancellationToken cancellationToken)
    {
        while (true)
        {
            var domain = TenantDomain.Normalise(fromArgs ?? AnsiConsole.Ask<string>("[green]Tenant domain:[/]"));

            var error = !TenantDomain.IsValid(domain, out var validationError)
                ? validationError
                : await tenantLookup.FindByDomainAsync(domain, cancellationToken) is not null
                    ? $"A tenant with domain '{domain}' already exists."
                    : null;

            if (error is null)
                return domain;

            AnsiConsole.MarkupLineInterpolated($"[red]Error: {error}[/]");

            if (fromArgs is not null)
                return null;
        }
    }
}
