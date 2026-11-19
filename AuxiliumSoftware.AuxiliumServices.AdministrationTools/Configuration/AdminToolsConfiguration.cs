using AuxiliumSoftware.AuxiliumServices.Common.Configuration;
using AuxiliumSoftware.AuxiliumServices.Common.Configuration.Sections.Databases;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Configuration;

internal static class AdminToolsConfiguration
{
    public const string ConfigPathArgument = "--config-path";
    public const string ConfigPathEnvironmentVariable = "AUXILIUM_CONFIG_PATH";

    public static (string? ConfigPath, string[] RemainingArgs) ExtractConfigPath(string[] args)
    {
        var argList = args.ToList();
        var index = argList.IndexOf(ConfigPathArgument);

        if (index != -1 && index + 1 < argList.Count)
        {
            var configPath = argList[index + 1];
            argList.RemoveRange(index, 2);
            return (configPath, argList.ToArray());
        }

        return (Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable), args);
    }

    public static (IConfiguration Raw, ConfigurationStructure Typed) Load(string configPath)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"Config file '{configPath}' does not exist.", configPath);

        var raw = new ConfigurationBuilder()
            .AddYamlFile(configPath, optional: false, reloadOnChange: false)
            .Build();

        var typed = raw.Get<ConfigurationStructure>()
            ?? throw new InvalidOperationException($"Config file '{configPath}' is empty.");

        typed.Validate();

        return (raw, typed);
    }

    public static string BuildConnectionString(MariaDBConfigurationSection mariaDb) =>
        new MySqlConnectionStringBuilder
        {
            Server = mariaDb.Host,
            Port = (uint)mariaDb.Port,
            Database = mariaDb.Database,
            UserID = mariaDb.Username,
            Password = mariaDb.Password,
            CharacterSet = "utf8mb4",
            Pooling = false,
        }.ConnectionString;
}
