using AuxiliumServices.AdminTools.Infrastructure;
using AuxiliumServices.AdminTools.Tools;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Services.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace AuxiliumServices.AdminTools;

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
            config.AddCommand<CreateAdminUserTool>("create-admin")
                .WithDescription("Creates an admin user.");
            config.AddCommand<PasswordResetTool>("password-reset")
                .WithDescription("Resets a user's password.");
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
