using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands.Settings;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands.Superclasses;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Interaction;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Services;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Tenancy;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands;

public sealed class CreateAdministratorUserCommand(
    TenantSelector tenantSelector,
    AuxiliumDbContext dbContext,
    AdministratorAccountService accountService
) : TenantScopedAsyncCommand<TenantScopedCommandSettings>(tenantSelector)
{
    protected override void WriteHeader()
    {
        AnsiConsole.Write(new Rule("[bold blue]Create Administrator User[/]").LeftJustified());
        AnsiConsole.WriteLine();
    }

    protected override async Task<int> ExecuteWithinTenantAsync(
        CommandContext context,
        TenantScopedCommandSettings settings,
        ResolvedTenant tenant,
        CancellationToken cancellationToken
    )
    {
        var email = Prompts.AskEmailAddress();

        if (await accountService.EmailAddressExistsAsync(email, cancellationToken))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error: A user with email '{email}' already exists in {tenant.Domain}.[/]");
            AnsiConsole.MarkupLine("[blue]Use reset-user-password if you need to get back into that account.[/]");
            return 1;
        }

        var fullName = Prompts.AskRequiredText("Full name");
        var password = Prompts.AskNewPassword();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await accountService.CreateAdministratorAsync(email, fullName, password, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ Administrator created.[/]");

        var table = new Table().Border(TableBorder.Rounded).AddColumn("Field").AddColumn("Value");
        table.AddRow("Tenant", Markup.Escape(tenant.Domain));
        table.AddRow("User ID", user.Id.ToString());
        table.AddRow("Email", Markup.Escape(email));
        table.AddRow("Name", Markup.Escape(fullName));
        AnsiConsole.Write(table);

        return 0;
    }
}
