using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Configuration;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Infrastructure;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Services;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Tenancy;
using AuxiliumSoftware.AuxiliumServices.Common.Configuration;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Services.Implementations;
using AuxiliumSoftware.AuxiliumServices.Common.Tenancy;
using AuxiliumSoftware.AuxiliumServices.Common.Tenancy.TenantLookup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var (configPath, remainingArgs) = AdminToolsConfiguration.ExtractConfigPath(args);

        if (configPath is null)
        {
            AnsiConsole.MarkupLine(
                $"[red]Error: {AdminToolsConfiguration.ConfigPathArgument} <path> is required (or set {AdminToolsConfiguration.ConfigPathEnvironmentVariable}).[/]");
            return 1;
        }

        IConfiguration rawConfiguration;
        ConfigurationStructure configuration;
        try
        {
            (rawConfiguration, configuration) = AdminToolsConfiguration.Load(configPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error: Failed to load config from '{configPath}': {ex.Message}[/]");
            return 1;
        }

        var app = new CommandApp(new TypeRegistrar(ConfigureServices(rawConfiguration, configuration)));

        app.Configure(config =>
        {
            config.SetApplicationName("auxilium-admin-tools");
            config.PropagateExceptions();

            config.AddCommand<ListTenantsCommand>("list-tenants")
                .WithDescription("Lists every tenant on this instance.");

            config.AddCommand<CreateTenantCommand>("create-tenant")
                .WithDescription("Creates a tenant with its first administrator, settings, WAF whitelist and built-in enumerators.")
                .WithExample("create-tenant", "--domain", "portal.example.org");

            config.AddCommand<CreateAdministratorUserCommand>("create-administrator-user")
                .WithDescription("Creates an administrator user within a tenant.")
                .WithExample("create-administrator-user", "--tenant", "portal.example.org");

            config.AddCommand<ResetUserPasswordCommand>("reset-user-password")
                .WithDescription("Resets a user's password within a tenant.")
                .WithExample("reset-user-password", "--tenant", "portal.example.org");
        });

        WriteBanner();

        try
        {
            return await app.RunAsync(remainingArgs);
        }
        catch (CommandAppException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error: {ex.Message}[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes);
            return 1;
        }
    }

    private static ServiceCollection ConfigureServices(IConfiguration rawConfiguration, ConfigurationStructure configuration)
    {
        var services = new ServiceCollection();

        services.AddSingleton(rawConfiguration);
        services.AddSingleton(configuration);

        // Common's services log through ILogger<T>; no providers are added, so nothing interrupts the Spectre UI
        services.AddLogging();

        services.AddSingleton<TenantContextAccessor>();
        services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<TenantContextAccessor>());
        services.AddSingleton<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContextAccessor>());
        services.AddSingleton<ITenantLookup, TenantLookup>();

        var connectionString = AdminToolsConfiguration.BuildConnectionString(configuration.Databases.MariaDB);
        services.AddDbContext<AuxiliumDbContext>(
            options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)),
            contextLifetime: ServiceLifetime.Singleton,
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ISystemSettingsService, SystemSettingsService>();
        services.AddSingleton<IWebApplicationFirewallService, WebApplicationFirewallService>();
        services.AddSingleton<IDataEnumeratorService, DataEnumeratorService>();

        services.AddSingleton<TenantSelector>();
        services.AddSingleton<AdministratorAccountService>();

        return services;
    }

    private static void WriteBanner()
    {
        AnsiConsole.Write(new FigletText("Auxilium|3").Color(Color.Blue));
        AnsiConsole.MarkupLine("[grey]Administration Tools[/]");
        AnsiConsole.WriteLine();
    }
}
