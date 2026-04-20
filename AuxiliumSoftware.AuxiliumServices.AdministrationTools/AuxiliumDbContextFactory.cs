using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AuxiliumServices.AdminTools;

public class AuxiliumDbContextFactory : IDesignTimeDbContextFactory<AuxiliumDbContext>
{
    public AuxiliumDbContext CreateDbContext(string[] args)
    {
        var configPath = ExtractConfigPath(args)
            ?? Environment.GetEnvironmentVariable("AUXILIUM_CONFIG_PATH")
            ?? throw new InvalidOperationException("Config path must be supplied via --config-path <path> or the AUXILIUM_CONFIG_PATH environment variable.");

        var configuration = new ConfigurationBuilder()
            .AddYamlFile(configPath, optional: false, reloadOnChange: false)
            .Build();

        var host     = configuration["Databases:MariaDB:Host"]     ?? throw new InvalidOperationException("MariaDB Host not found in configuration.");
        var port     = configuration["Databases:MariaDB:Port"]     ?? throw new InvalidOperationException("MariaDB Port not found in configuration.");
        var username = configuration["Databases:MariaDB:Username"] ?? throw new InvalidOperationException("MariaDB Username not found in configuration.");
        var password = configuration["Databases:MariaDB:Password"] ?? throw new InvalidOperationException("MariaDB Password not found in configuration.");
        var database = configuration["Databases:MariaDB:Database"] ?? throw new InvalidOperationException("MariaDB Database not found in configuration.");

        var connectionString =
            $"Server={host};"
            + $"Port={port};"
            + $"Database={database};"
            + $"User={username};"
            + $"Password={password};"
            + $"CharSet=utf8mb4;"
            + $"Pooling=true;"
            + $"MinimumPoolSize=5;"
            + $"MaximumPoolSize=100;"
            + $"ConnectionLifeTime=300;"
            + $"ConnectionIdleTimeout=180;"
            + $"CancellationTimeout=5;"
            + $"ConnectionReset=false;"
            + $"DefaultCommandTimeout=30;";

        var optionsBuilder = new DbContextOptionsBuilder<AuxiliumDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            ServerVersion.AutoDetect(connectionString),
            b => b.MigrationsAssembly("AuxiliumServices.AdminTools")
        );

        return new AuxiliumDbContext(optionsBuilder.Options);
    }

    private static string? ExtractConfigPath(string[] args)
    {
        var argList = args.ToList();
        var index = argList.IndexOf("--config-path");

        if (index == -1 || index + 1 >= argList.Count)
            return null;

        return argList[index + 1];
    }
}
