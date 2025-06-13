using Microsoft.Extensions.Configuration;
using Npgsql;
using System.IO;

namespace TaskManager;

public class DatabaseService
{
    private readonly IConfiguration _config;

    public DatabaseService()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false);

        _config = builder.Build();
    }

    public NpgsqlConnection GetConnection()
    {
        return new NpgsqlConnection(_config.GetConnectionString("PostgreSQL"));
    }
}
