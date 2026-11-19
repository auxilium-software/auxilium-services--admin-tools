using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands;

public sealed class ListTenantsCommand(AuxiliumDbContext dbContext) : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var tenants = await dbContext.Global_Tenants
            .AsNoTracking()
            .OrderBy(t => t.Domain)
            .ToListAsync(cancellationToken);

        if (tenants.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No tenants exist yet. Run [bold]create-tenant[/] to make one.[/]");
            return 0;
        }

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("Domain")
            .AddColumn("Status")
            .AddColumn("Created (UTC)")
            .AddColumn("ID");

        foreach (var tenant in tenants)
        {
            var status = tenant.LifecycleStatus == TenantLifecycleStatusEnum.Active
                ? "[green]Active[/]"
                : $"[yellow]{tenant.LifecycleStatus}[/]";

            table.AddRow(
                Markup.Escape(tenant.Domain),
                status,
                tenant.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                tenant.Id.ToString()
            );
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
