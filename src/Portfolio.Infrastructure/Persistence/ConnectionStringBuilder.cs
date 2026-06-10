using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Portfolio.Infrastructure.Persistence;

/// <summary>Resolves the DB connection string from config/env, accepting either a postgresql:// URL or keyword form.</summary>
public static class ConnectionStringBuilder
{
    public static string Build(IConfiguration config)
    {
        var raw = Environment.GetEnvironmentVariable("DATABASE_URL")
                  ?? config.GetConnectionString("DefaultConnection")
                  ?? config["DATABASE_URL"];

        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException(
                "No database connection string configured. Set 'ConnectionStrings:DefaultConnection' (user-secrets) " +
                "or the 'DATABASE_URL' environment variable.");

        if (!raw.StartsWith("postgres://") && !raw.StartsWith("postgresql://"))
            return raw; // already in keyword form

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);

        var b = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            SslMode = SslMode.Require,
        };
        return b.ConnectionString;
    }
}
