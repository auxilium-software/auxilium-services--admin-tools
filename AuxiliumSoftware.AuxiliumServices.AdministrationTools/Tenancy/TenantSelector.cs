using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using AuxiliumSoftware.AuxiliumServices.Common.Tenancy;
using AuxiliumSoftware.AuxiliumServices.Common.Tenancy.TenantLookup;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Tenancy;

public sealed class TenantSelector(
    AuxiliumDbContext dbContext,
    ITenantLookup tenantLookup,
    ITenantContext tenantContext,
    ITenantContextSetter tenantContextSetter
)
{
    public async Task<ResolvedTenant?> SelectAsync(string? requestedDomain, CancellationToken cancellationToken)
    {
        var resolved = string.IsNullOrWhiteSpace(requestedDomain)
            ? await PickInteractivelyAsync(cancellationToken)
            : await LookUpAsync(TenantDomain.Normalise(requestedDomain), cancellationToken);

        if (resolved is null)
        {
            return null;
        }

        if (resolved.LifecycleStatus != TenantLifecycleStatusEnum.Active)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error: Tenant '{resolved.Domain}' is {resolved.LifecycleStatus}, not Active.[/]");
            return null;
        }

        Pin(resolved.TenantId);

        AnsiConsole.MarkupLineInterpolated($"[grey]Tenant:[/] [bold]{resolved.Domain}[/] [grey]({resolved.TenantId})[/]");
        AnsiConsole.WriteLine();

        return resolved;
    }

    public void Pin(Guid tenantId)
    {
        if (tenantContext.IsResolved && tenantContext.TenantId == tenantId)
            return;

        tenantContextSetter.SetTenant(tenantId);
    }

    private async Task<ResolvedTenant?> LookUpAsync(string domain, CancellationToken cancellationToken)
    {
        var result = await tenantLookup.FindByDomainAsync(domain, cancellationToken);

        if (result is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error: No tenant with domain '{domain}' exists. Run list-tenants to see them.[/]");
            return null;
        }

        return new ResolvedTenant(result.TenantId, domain, result.LifecycleStatus);
    }

    private async Task<ResolvedTenant?> PickInteractivelyAsync(CancellationToken cancellationToken)
    {
        var tenants = await dbContext.Global_Tenants
            .AsNoTracking()
            .OrderBy(t => t.Domain)
            .Select(t => new ResolvedTenant(t.Id, t.Domain, t.LifecycleStatus))
            .ToListAsync(cancellationToken);

        switch (tenants.Count)
        {
            case 0:
                AnsiConsole.MarkupLine("[red]Error: No tenants exist yet. Run [bold]create-tenant[/] first.[/]");
                return null;

            case 1:
                return tenants[0];

            default:
                return AnsiConsole.Prompt(
                    new SelectionPrompt<ResolvedTenant>()
                        .Title("[green]Which tenant?[/]")
                        .PageSize(15)
                        .UseConverter(t => t.LifecycleStatus == TenantLifecycleStatusEnum.Active
                            ? Markup.Escape(t.Domain)
                            : $"{Markup.Escape(t.Domain)} [grey]({t.LifecycleStatus})[/]")
                        .AddChoices(tenants));
        }
    }
}

public sealed record ResolvedTenant(
    Guid TenantId,
    string Domain,
    TenantLifecycleStatusEnum LifecycleStatus
);
