using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands.Superclasses;

public abstract class InitialisedInstanceAsyncCommand<TSettings>(AuxiliumDbContext dbContext) : AsyncCommand<TSettings>
    where TSettings : CommandSettings
{
    public sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken)
    {
        WriteHeader();

        if (!await dbContext.Users.AnyAsync(cancellationToken))
        {
            AnsiConsole.MarkupLine("[red]Error: This instance hasn't been initialised yet. Run [bold]initialise-instance[/] first.[/]");
            return 1;
        }

        return await ExecuteOnInstanceAsync(context, settings, cancellationToken);
    }

    protected abstract void WriteHeader();

    protected abstract Task<int> ExecuteOnInstanceAsync(
        CommandContext context,
        TSettings settings,
        CancellationToken cancellationToken
    );
}
