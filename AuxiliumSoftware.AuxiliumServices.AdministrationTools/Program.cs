using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Configuration;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Infrastructure;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Configuration;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Services.Implementations;
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

            config.AddCommand<InitialiseInstanceCommand>("initialise-instance")
                .WithDescription("Sets up a fresh instance with its first administrator, settings, WAF whitelist and built-in enumerators.")
                .WithExample("initialise-instance", "--domain", "portal.example.org");

            config.AddCommand<CreateAdministratorUserCommand>("create-administrator-user")
                .WithDescription("Creates an administrator user.")
                .WithExample("create-administrator-user");

            config.AddCommand<ResetUserPasswordCommand>("reset-user-password")
                .WithDescription("Resets a user's password.")
                .WithExample("reset-user-password");

            config.AddCommand<PopulateWithDemoDataCommand>("populate-with-demo-data")
                .WithDescription("Fills this instance with fictional users, cases, messages and calendar events from a JSON file.")
                .WithExample("populate-with-demo-data")
                .WithExample("populate-with-demo-data", "--file", "./my-demo-data.json");
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

        var connectionString = AdminToolsConfiguration.BuildConnectionString(configuration.Databases.MariaDB);
        services.AddDbContext<AuxiliumDbContext>(
            options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)),
            contextLifetime: ServiceLifetime.Singleton,
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ISystemSettingsService, SystemSettingsService>();
        services.AddSingleton<IWebApplicationFirewallService, WebApplicationFirewallService>();
        services.AddSingleton<IDataEnumeratorService, DataEnumeratorService>();
        services.AddSingleton<ICaseDocumentService, CaseDocumentService>();
        services.AddSingleton<IMessageDocumentService, MessageDocumentService>();
        services.AddSingleton<ICaseAdditionalPropertyGroupService, CaseAdditionalPropertyGroupService>();

        services.AddSingleton<AdministratorAccountService>();
        services.AddSingleton<DemoDataValidator>();
        services.AddSingleton<DemoDataImporter>();

        return services;
    }

    private static void WriteBanner()
    {
        AnsiConsole.Write(new FigletText("Auxilium|3").Color(Color.Blue));
        AnsiConsole.MarkupLine("[grey]Administration Tools[/]");
        AnsiConsole.WriteLine();
    }
}
