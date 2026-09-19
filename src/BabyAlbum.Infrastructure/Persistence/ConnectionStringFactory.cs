using Microsoft.Extensions.Configuration;
using Npgsql;

namespace BabyAlbum.Infrastructure.Persistence;

public static class ConnectionStringFactory
{
    private const string LocalFallback = "Host=localhost;Port=5432;Database=pablos_album;Username=postgres;Password=postgres";

    public static string GetDefaultConnection(IConfiguration? configuration = null)
    {
        var configured = configuration?.GetConnectionString("DefaultConnection");
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        var selected = NormalizeConnectionStringInput(string.IsNullOrWhiteSpace(configured) ? databaseUrl : configured);

        if (string.IsNullOrWhiteSpace(selected))
        {
            return LocalFallback;
        }

        return selected.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || selected.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? ConvertDatabaseUrl(selected)
            : selected;
    }

    private static string ConvertDatabaseUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }

    private static string? NormalizeConnectionStringInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var normalized = value.Trim().Trim('"', '\'');
        const string databaseUrlPrefix = "DATABASE_URL=";
        if (normalized.StartsWith(databaseUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[databaseUrlPrefix.Length..].Trim().Trim('"', '\'');
        }

        if (normalized.StartsWith("psql ", StringComparison.OrdinalIgnoreCase))
        {
            var firstQuote = normalized.IndexOf('\'');
            var lastQuote = normalized.LastIndexOf('\'');
            if (firstQuote >= 0 && lastQuote > firstQuote)
            {
                return normalized[(firstQuote + 1)..lastQuote].Trim();
            }

            return normalized["psql ".Length..].Trim().Trim('"', '\'');
        }

        return normalized;
    }
}
