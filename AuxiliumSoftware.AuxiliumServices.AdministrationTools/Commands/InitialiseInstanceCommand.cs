using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Interaction;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Seeding;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Services;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.EntityModels;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands;

public sealed class InitialiseInstanceCommand(
    AuxiliumDbContext dbContext,
    AdministratorAccountService accountService,
    ISystemSettingsService systemSettings,
    IWebApplicationFirewallService webApplicationFirewall,
    IDataEnumeratorService dataEnumerators
) : AsyncCommand<InitialiseInstanceCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--domain <DOMAIN>")]
        [Description("Domain this instance is served on (e.g. portal.example.org). Prompted for if omitted.")]
        public string? Domain { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new Rule("[bold blue]Initialise Instance[/]").LeftJustified());
        AnsiConsole.MarkupLine("[blue]This will set up the configured Auxilium|3 instance with its first administrator, settings, WAF whitelist and built-in enumerators.[/]");
        AnsiConsole.WriteLine();

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            AnsiConsole.MarkupLine("[red]Error: This instance has already been initialised.[/]");
            AnsiConsole.MarkupLine("[blue]Use create-administrator-user to add another administrator, or reset-user-password to get back into an existing account.[/]");
            return 1;
        }

        var domain = AskDomain(settings.Domain);
        if (domain is null)
            return 1;
        AnsiConsole.WriteLine();

        var settingValues = new List<(SystemSettingMetadata Metadata, object Value)>();
        foreach (var seed in InstanceSeedDefinition.PromptedSettings)
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
        summary.AddRow("IP whitelist", Markup.Escape(string.Join(", ", InstanceSeedDefinition.IpWhitelist.Select(i => i.IpAddress))));
        summary.AddRow("Data enumerators", Markup.Escape(string.Join(", ", InstanceSeedDefinition.DataEnumerators.Select(e => e.CanonicalName))));
        summary.AddRow("Administrator", Markup.Escape($"{adminName} <{adminEmail}>"));
        AnsiConsole.Write(summary);

        if (!AnsiConsole.Confirm("[yellow]Initialise this instance?[/]", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[blue]Cancelled, nothing was written.[/]");
            return 0;
        }

        UserEntityModel admin;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            admin = await accountService.CreateAdministratorAsync(adminEmail, adminName, adminPassword, cancellationToken);

            foreach (var (metadata, value) in settingValues)
                await systemSettings.SetAsync(metadata.Key, value, admin.Id, InstanceSeedDefinition.ReasonForModification, cancellationToken);

            foreach (var entry in InstanceSeedDefinition.IpWhitelist)
                await webApplicationFirewall.WhitelistIpAddressAsync(entry.IpAddress, entry.Justification, admin, permanent: true, ct: cancellationToken);

            foreach (var enumerator in InstanceSeedDefinition.DataEnumerators)
                await dataEnumerators.CreateEnumeratorAsync(enumerator.Scope, enumerator.CanonicalName, enumerator.Description, admin, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ Instance initialised.[/]");

        var result = new Table().Border(TableBorder.Rounded).AddColumn("Field").AddColumn("Value");
        result.AddRow("Domain", Markup.Escape(domain));
        result.AddRow("Administrator ID", admin.Id.ToString());
        result.AddRow("Administrator email", Markup.Escape(adminEmail));
        AnsiConsole.Write(result);

        return 0;
    }

    private static string? AskDomain(string? fromArgs)
    {
        while (true)
        {
            var domain = InstanceDomain.Normalise(fromArgs ?? AnsiConsole.Ask<string>("[green]Instance domain:[/]"));

            if (InstanceDomain.IsValid(domain, out var validationError))
                return domain;

            AnsiConsole.MarkupLineInterpolated($"[red]Error: {validationError}[/]");

            if (fromArgs is not null)
                return null;
        }
    }
}
