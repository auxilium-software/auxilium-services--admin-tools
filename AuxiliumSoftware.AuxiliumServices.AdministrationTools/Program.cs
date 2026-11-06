using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Infrastructure;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Tools;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Services.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools;

internal class Program
{
    public static int Main(string[] args)
    {
        var (configPath, remainingArgs) = ExtractConfigPath(args);

        if (configPath is null)
        {
            Console.Error.WriteLine("Error: --config-path <path> is required.");
            return 1;
        }

        IConfiguration configuration;
        try
        {
            configuration = new ConfigurationBuilder()
                .AddYamlFile(configPath, optional: false, reloadOnChange: false)
                .Build();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to load config from '{configPath}': {ex.Message}");
            return 1;
        }

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        RegisterTools(services);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.AddCommand<CreateAdminUserTool>("create-administrator-user")         .WithDescription("Creates an Administrator user.");
            config.AddCommand<PasswordResetTool>("reset-user-password")                 .WithDescription("Resets a user's password.");
            config.AddCommand<CreateTenant>("create-tenant")                            .WithDescription("Sets the initial system settings.");
            config.AddCommand<SetInitiallyRequiredData>("set-initially-required-data")  .WithDescription("Sets the initially required data.");
        });

        try
        {
            var provider = services.BuildServiceProvider();
            var tool = provider.GetRequiredService<CreateAdminUserTool>();
            Console.WriteLine($"Resolution OK: {tool.GetType().Name}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"DI Resolution failed: {ex}");
            return 1;
        }

        AnsiConsole.MarkupLine("""
                                _ _ _                    _    ____                    _           _             _______          _     
                /\             (_) (_)                  | |  |___ \          /\      | |         (_)           |__   __|        | |    
               /  \  _   ___  ___| |_ _   _ _ __ ___    | |    __) |        /  \   __| |_ __ ___  _ _ __          | | ___   ___ | |___ 
              / /\ \| | | \ \/ / | | | | | | '_ ` _ \   | |   |__ <        / /\ \ / _` | '_ ` _ \| | '_ \         | |/ _ \ / _ \| / __|
             / ____ \ |_| |>  <| | | | |_| | | | | | |  | |   ___) |      / ____ \ (_| | | | | | | | | | |        | | (_) | (_) | \__ \
            /_/    \_\__,_/_/\_\_|_|_|\__,_|_| |_| |_|  |_|  |____/      /_/    \_\__,_|_| |_| |_|_|_| |_|        |_|\___/ \___/|_|___/

                                                                                                                                       
            """);

        return app.Run(remainingArgs);
    }

    private static void RegisterTools(IServiceCollection services)
    {
        services.AddTransient<IPasswordService, PasswordService>();
        services.AddTransient<CreateAdminUserTool>();
        services.AddTransient<PasswordResetTool>();
    }

    private static (string? ConfigPath, string[] RemainingArgs) ExtractConfigPath(string[] args)
    {
        var argList = args.ToList();
        var index = argList.IndexOf("--config-path");

        if (index == -1 || index + 1 >= argList.Count)
            return (null, args);

        var configPath = argList[index + 1];
        argList.RemoveRange(index, 2);

        return (configPath, argList.ToArray());
    }
}
