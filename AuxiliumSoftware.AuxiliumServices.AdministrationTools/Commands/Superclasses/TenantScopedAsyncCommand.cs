using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands.Settings;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Tenancy;
using Spectre.Console.Cli;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands.Superclasses;

public abstract class TenantScopedAsyncCommand<TSettings>(TenantSelector tenantSelector) : AsyncCommand<TSettings>
    where TSettings : TenantScopedCommandSettings
{
    public sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken)
    {
        WriteHeader();

        var tenant = await tenantSelector.SelectAsync(settings.Tenant, cancellationToken);
        if (tenant is null)
            return 1;

        return await ExecuteWithinTenantAsync(context, settings, tenant, cancellationToken);
    }

    protected abstract void WriteHeader();

    protected abstract Task<int> ExecuteWithinTenantAsync(
        CommandContext context,
        TSettings settings,
        ResolvedTenant tenant,
        CancellationToken cancellationToken
    );
}
