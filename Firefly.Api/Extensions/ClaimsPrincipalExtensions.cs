using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Firefly.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Pobiera GUID użytkownika z claimsów tokenu JWT. 
    /// Rzuca UnauthorizedAccessException, jeśli token nie zawiera poprawnego ID.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal? user)
    {
        var val = user?.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user?.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? user?.FindFirstValue("sub")
               ?? user?.FindFirstValue("id")
               ?? user?.FindFirstValue("userId");

        if (string.IsNullOrEmpty(val) || !Guid.TryParse(val, out var guid))
        {
            throw new UnauthorizedAccessException("Brak lub niepoprawny identyfikator użytkownika w tokenie JWT.");
        }

        return guid;
    }

    /// <summary>
    /// Bezpiecznie próbuje pobrać GUID użytkownika (zwraca null zamiast rzucać błąd).
    /// </summary>
    public static Guid? TryGetUserId(this ClaimsPrincipal? user)
    {
        var val = user?.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user?.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? user?.FindFirstValue("sub")
               ?? user?.FindFirstValue("id")
               ?? user?.FindFirstValue("userId");

        return Guid.TryParse(val, out var guid) ? guid : null;
    }
}