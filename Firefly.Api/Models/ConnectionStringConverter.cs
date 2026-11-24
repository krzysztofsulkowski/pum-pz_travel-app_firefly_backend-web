namespace Firefly.Api.Models;

public static class ConnectionStringConverter
{
    public static string Convert(string databaseUrl, bool trustServerCertificate = false)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            return string.Empty;
        }

        if (!databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
            !databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return databaseUrl;
        }

        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

        var trustPart = trustServerCertificate ? "Trust Server Certificate=true;" : string.Empty;
        return $"Host={host};Port={port};Database={database};Username={user};Password={password};SSL Mode=Require;{trustPart}";
    }
}