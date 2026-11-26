using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Configuration;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools;

public class AuxiliumDbContextFactory : IDesignTimeDbContextFactory<AuxiliumDbContext>
{
    public AuxiliumDbContext CreateDbContext(string[] args)
    {
        var (configPath, _) = AdminToolsConfiguration.ExtractConfigPath(args);

        if (configPath is null)
        {
            throw new InvalidOperationException(
                $"Config path must be supplied via {AdminToolsConfiguration.ConfigPathArgument} <path> or the {AdminToolsConfiguration.ConfigPathEnvironmentVariable} environment variable."
            );
        }

        var (_, configuration) = AdminToolsConfiguration.Load(configPath);
        var connectionString = AdminToolsConfiguration.BuildConnectionString(configuration.Databases.MariaDB);

        var optionsBuilder = new DbContextOptionsBuilder<AuxiliumDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            ServerVersion.AutoDetect(connectionString),
            b => b.MigrationsAssembly(typeof(AuxiliumDbContextFactory).Assembly.GetName().Name)
        );

        return new AuxiliumDbContext(optionsBuilder.Options);
    }
}
