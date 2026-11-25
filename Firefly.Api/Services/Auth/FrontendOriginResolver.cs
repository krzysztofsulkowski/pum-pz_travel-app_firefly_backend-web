using System.Net;

namespace Firefly.Api.Services.Auth;

public static class FrontendOriginResolver
{
    /// <summary>
    /// Zwraca listę wszystkich zaufanych domen frontendowych (User App + Admin App).
    /// </summary>
    public static string[] GetAllowedOrigins(IConfiguration configuration)
    {
        var userOrigin = ExtractOrigin(configuration["FRONTEND_BASE_URL"]);
        var adminOrigin = ExtractOrigin(configuration["ADMIN_BASE_URL"]);

        return new[] { userOrigin, adminOrigin }
            .Where(url => !string.IsNullOrEmpty(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    /// <summary>
    /// Sprawdza, czy podany kandydat (np. returnUrl lub origin z formularza) należy do zaufanych domen.
    /// Jeśli tak, zwraca dopasowaną domenę bazową. Jeśli nie podano kandydata, zwraca domyślny FRONTEND_BASE_URL.
    /// </summary>
    public static string? Resolve(IConfiguration configuration, string? candidate = null)
    {
        var allowedOrigins = GetAllowedOrigins(configuration);
        if (allowedOrigins.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var candidateOrigin = ExtractOrigin(candidate);
            if (candidateOrigin is not null)
            {
                var matched = allowedOrigins.FirstOrDefault(o =>
                    string.Equals(o, candidateOrigin, StringComparison.OrdinalIgnoreCase));

                if (matched is not null)
                {
                    return matched;
                }
            }

            return null;
        }

        return allowedOrigins[0];
    }

    /// <summary>
    /// Bezpiecznie wyciąga sam protokół + host (Origin), ignorując podstrony, query i fragmenty.
    /// </summary>
    public static string? ExtractOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            string.IsNullOrEmpty(uri.UserInfo))
        {
            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        return null;
    }

    public static string BuildResetPasswordUrl(IConfiguration configuration, string token, string email, string? frontendOrigin = null)
    {
        var baseUrl = Resolve(configuration, frontendOrigin)
            ?? throw new InvalidOperationException("Brak poprawnego FRONTEND_BASE_URL w konfiguracji.");

        return $"{baseUrl}/reset-password?token={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(email)}";
    }

    /// <summary>
    /// Buduje link do pierwszego ustawienia hasła przez użytkownika utworzonego przez admina.
    /// Jeśli tworzony jest Administrator -> link prowadzi do admin-app.
    /// Jeśli tworzony jest zwykły User -> link prowadzi do user-app.
    /// </summary>
    public static string BuildAccountSetupUrl(IConfiguration configuration, string token, string email, bool isAdmin = false)
    {
        var targetOrigin = isAdmin
            ? configuration["ADMIN_BASE_URL"] ?? configuration["FRONTEND_BASE_URL"]
            : configuration["FRONTEND_BASE_URL"];

        var baseUrl = ExtractOrigin(targetOrigin)
            ?? throw new InvalidOperationException("Brak poprawnego adresu bazowego (FRONTEND_BASE_URL / ADMIN_BASE_URL) w konfiguracji.");

        return $"{baseUrl}/reset-password?token={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(email)}";
    }
}