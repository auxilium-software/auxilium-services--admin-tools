using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands.Superclasses;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Interaction;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Services;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands;

public sealed class ResetUserPasswordCommand(
    AuxiliumDbContext dbContext,
    AdministratorAccountService accountService
) : InitialisedInstanceAsyncCommand<EmptyCommandSettings>(dbContext)
{
    protected override void WriteHeader()
    {
        AnsiConsole.Write(new Rule("[bold blue]Reset User Password[/]").LeftJustified());
        AnsiConsole.WriteLine();
    }

    protected override async Task<int> ExecuteOnInstanceAsync(
        CommandContext context,
        EmptyCommandSettings settings,
        CancellationToken cancellationToken)
    {
        var email = Prompts.AskEmailAddress("User's email address");

        var user = await accountService.FindByEmailAddressAsync(email, cancellationToken);
        if (user is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error: No user with email '{email}'.[/]");
            return 1;
        }

        AnsiConsole.MarkupLineInterpolated($"[yellow]Found:[/] {user.FullName} <{user.EmailAddress}> [grey]({user.Id})[/]");
        if (user.MergedIntoUserId is not null)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]Warning: this account was merged into {user.MergedIntoUserId}.[/]");
        }
        if (!user.AllowLogin)
        {
            AnsiConsole.MarkupLine("[yellow]Warning: this account has login disabled — resetting the password won't change that.[/]");
        }
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("[yellow]Reset the password for this user?[/]", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[blue]Cancelled, nothing was written.[/]");
            return 0;
        }

        var password = Prompts.AskNewPassword();
        var mustChange = AnsiConsole.Confirm("[green]Require them to change it at next login?[/]", defaultValue: true);
        var revokeSessions = AnsiConsole.Confirm("[green]Sign them out of all existing sessions?[/]", defaultValue: true);

        accountService.SetPassword(user, password, mustChange);
        await dbContext.SaveChangesAsync(cancellationToken);

        var revoked = revokeSessions
            ? await accountService.RevokeRefreshTokensAsync(user.Id, cancellationToken)
            : 0;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ Password reset.[/]");

        var table = new Table().Border(TableBorder.Rounded).AddColumn("Field").AddColumn("Value");
        table.AddRow("User ID", user.Id.ToString());
        table.AddRow("Email", Markup.Escape(user.EmailAddress ?? ""));
        table.AddRow("Name", Markup.Escape(user.FullName));
        table.AddRow("Must change password", mustChange ? "Yes" : "No");
        table.AddRow("Sessions revoked", revokeSessions ? revoked.ToString() : "[grey]skipped[/]");
        AnsiConsole.Write(table);

        return 0;
    }
}
