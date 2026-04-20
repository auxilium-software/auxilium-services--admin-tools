using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumServices.AdminTools.Common
{
    internal static class MariaDBInteractions
    {
        public static AuxiliumDbContext GetDbContext(IConfiguration configuration)
        {
            var mariaDbHost = configuration["Databases:MariaDB:Host"]           ?? throw new InvalidOperationException("MariaDB Host not found");
            var mariaDbPort = configuration["Databases:MariaDB:Port"]           ?? throw new InvalidOperationException("MariaDB Port not found");
            var mariaDbUsername = configuration["Databases:MariaDB:Username"]   ?? throw new InvalidOperationException("MariaDB Username not found");
            var mariaDbPassword = configuration["Databases:MariaDB:Password"]   ?? throw new InvalidOperationException("MariaDB Password not found");
            var mariaDbDatabase = configuration["Databases:MariaDB:Database"]   ?? throw new InvalidOperationException("MariaDB Database not found");

            var connectionString = $"Server={mariaDbHost};Port={mariaDbPort};Database={mariaDbDatabase};User={mariaDbUsername};Password={mariaDbPassword};CharSet=utf8mb4;";

            var optionsBuilder = new DbContextOptionsBuilder<AuxiliumDbContext>();
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            return new AuxiliumDbContext(optionsBuilder.Options);
        }
    }
}
