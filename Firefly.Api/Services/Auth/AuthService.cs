using Firefly.Api.Models;
using Firefly.Api.Models.DatabaseModels;
using Firefly.Api.Models.Dto.Auth;
using Firefly.Api.Models.Settings;
using Firefly.Api.Services.Errors;
using Firefly.Api.Services.Interfaces;
using Firefly.Api.Services.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Firefly.Api.Services.Auth;

public class AuthService : IAuthService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan UserRefreshTokenLifetime = TimeSpan.FromDays(14);
    private static readonly TimeSpan AdminRefreshTokenLifetime = TimeSpan.FromHours(4);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AuthService> _logger;
    private readonly IConfiguration _configuration;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailService _emailService;
    private readonly JwtSettings _jwtSettings;
    private readonly FireflyDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ILogger<AuthService> logger,
        IConfiguration configuration,
        SignInManager<ApplicationUser> signInManager,
        IEmailService emailService,
        IOptions<JwtSettings> jwtOptions,
        FireflyDbContext context,
        ICurrentUserService currentUserService)
    {
        _userManager = userManager;
        _logger = logger;
        _configuration = configuration;
        _signInManager = signInManager;
        _emailService = emailService;
        _jwtSettings = jwtOptions.Value;
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult> RegisterAsync(RegisterDto registerDto)
    {
        var cleanEmail = registerDto.Email.Trim().ToLowerInvariant();
        var cleanUsername = registerDto.Username.Trim();

        if (await _userManager.FindByEmailAsync(cleanEmail) != null)
        {
            return ServiceResult.Failure(AuthErrors.UserAlreadyExists());
        }

        if (await _userManager.FindByNameAsync(cleanUsername) != null)
        {
            return ServiceResult.Failure(AuthErrors.UsernameTaken());
        }

        var newUser = new ApplicationUser
        {
            Email = cleanEmail,
            UserName = cleanUsername,
            EmailConfirmed = true
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var createResult = await _userManager.CreateAsync(newUser, registerDto.Password);
        if (!createResult.Succeeded)
        {
            var details = string.Join(", ", createResult.Errors.Select(e => e.Description));
            return ServiceResult.Failure(AuthErrors.UserCreationFailed(details));
        }

        var roleResult = await _userManager.AddToRoleAsync(newUser, "User");
        if (!roleResult.Succeeded)
        {
            return ServiceResult.Failure(AuthErrors.UserCreationFailed("Nie udało się przypisać roli użytkownika."));
        }

        await transaction.CommitAsync();

        _logger.LogInformation("Zarejestrowano użytkownika {Email} ({Username}).", cleanEmail, cleanUsername);
        return ServiceResult.Success();
    }

    
    public async Task<ServiceResult<LoginResponse>> Login(LoginDto loginDto)
    {
        var user = await FindByIdentifierAsync(loginDto.Identifier);
        if (user == null)
        {
            return ServiceResult<LoginResponse>.Failure(AuthErrors.InvalidCredentials());
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return ServiceResult<LoginResponse>.Failure("Konto zostało tymczasowo zablokowane. Spróbuj ponownie później.");
        }

        if (!result.Succeeded)
        {
            return ServiceResult<LoginResponse>.Failure(AuthErrors.InvalidCredentials());
        }

        if (!user.EmailConfirmed)
        {
            return ServiceResult<LoginResponse>.Failure("Twoje konto nie zostało jeszcze aktywowane. Sprawdź swoją skrzynkę e-mail.");
        }

        var isAdmin = await _userManager.IsInRoleAsync(user, "Administrator");
        var refreshLifetime = isAdmin ? AdminRefreshTokenLifetime : UserRefreshTokenLifetime;

        var accessToken = await GenerateJwtTokenAsync(user, AccessTokenLifetime);
        var refreshToken = await CreateAndSaveRefreshTokenAsync(user.Id, refreshLifetime);

        return ServiceResult<LoginResponse>.Success(new LoginResponse(accessToken, refreshToken));
    }

    public async Task<ServiceResult<LoginResponse>> AdminLogin(LoginDto loginDto)
    {
        var user = await FindByIdentifierAsync(loginDto.Identifier);
        if (user == null)
        {
            return ServiceResult<LoginResponse>.Failure(AuthErrors.InvalidCredentials());
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return ServiceResult<LoginResponse>.Failure("Konto zostało tymczasowo zablokowane. Spróbuj ponownie później.");
        }

        if (!result.Succeeded)
        {
            return ServiceResult<LoginResponse>.Failure(AuthErrors.InvalidCredentials());
        }

        var isAdmin = await _userManager.IsInRoleAsync(user, "Administrator");
        if (!isAdmin)
        {
            _logger.LogWarning("Odmowa dostępu do panelu administratora dla {Identifier}.", loginDto.Identifier);
            return ServiceResult<LoginResponse>.Failure(AuthErrors.InvalidCredentials());
        }

        if (!user.EmailConfirmed)
        {
            return ServiceResult<LoginResponse>.Failure("Konto administratora nie zostało aktywowane.");
        }

        var accessToken = await GenerateJwtTokenAsync(user, AccessTokenLifetime);
        var refreshToken = await CreateAndSaveRefreshTokenAsync(user.Id, AdminRefreshTokenLifetime);

        _logger.LogInformation("Administrator {Email} zalogował się do panelu.", user.Email);
        return ServiceResult<LoginResponse>.Success(new LoginResponse(accessToken, refreshToken));
    }

    public async Task<ServiceResult<LoginResponse>> RefreshTokenAsync(RefreshTokenRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            return ServiceResult<LoginResponse>.Failure("Brak tokena odświeżającego.");
        }

        var hashedToken = HashToken(dto.RefreshToken);
        var existingToken = await _context.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == hashedToken);

        if (existingToken == null)
        {
            return ServiceResult<LoginResponse>.Failure("Nieprawidłowy token odświeżający.");
        }

        if (existingToken.IsRevoked)
        {
            _logger.LogWarning("Wykryto próbę ponownego użycia unieważnionego Refresh Tokena! User ID: {UserId}. Unieważniam wszystkie sesje użytkownika.", existingToken.UserId);
            await RevokeAllUserRefreshTokensAsync(existingToken.UserId);
            return ServiceResult<LoginResponse>.Failure("Wykryto naruszenie bezpieczeństwa sesji. Zaloguj się ponownie.");
        }

        if (existingToken.ExpiryDate < DateTime.UtcNow)
        {
            existingToken.IsRevoked = true;
            existingToken.RevokedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ServiceResult<LoginResponse>.Failure("Sesja wygasła. Zaloguj się ponownie.");
        }

        var user = existingToken.User;
        if (user == null || await _userManager.IsLockedOutAsync(user))
        {
            return ServiceResult<LoginResponse>.Failure("Konto użytkownika jest zablokowane.");
        }

        existingToken.IsRevoked = true;
        existingToken.RevokedDate = DateTime.UtcNow;

        var isAdmin = await _userManager.IsInRoleAsync(user, "Administrator");
        var refreshLifetime = isAdmin ? AdminRefreshTokenLifetime : UserRefreshTokenLifetime;

        var newAccessToken = await GenerateJwtTokenAsync(user, AccessTokenLifetime);
        var newRefreshToken = await CreateAndSaveRefreshTokenAsync(user.Id, refreshLifetime);

        return ServiceResult<LoginResponse>.Success(new LoginResponse(newAccessToken, newRefreshToken));
    }
       
    public async Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
    {
        if (FrontendOriginResolver.Resolve(_configuration, forgotPasswordDto.FrontendOrigin) is null)
        {
            return ServiceResult.Failure(CommonErrors.BadRequest("Nieprawidłowy adres frontendu."));
        }

        if (string.IsNullOrWhiteSpace(forgotPasswordDto.Email))
        {
            return ServiceResult.Success();
        }

        var user = await _userManager.FindByEmailAsync(forgotPasswordDto.Email.Trim().ToLowerInvariant());
        if (user == null)
        {
            return ServiceResult.Success();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var callbackUrl = FrontendOriginResolver.BuildResetPasswordUrl(_configuration, token, user.Email!, forgotPasswordDto.FrontendOrigin);

        await _emailService.SendPasswordResetEmailAsync(user.Email!, user.UserName ?? "użytkowniku", callbackUrl);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        if (string.IsNullOrWhiteSpace(resetPasswordDto.Email) ||
            string.IsNullOrWhiteSpace(resetPasswordDto.Token) ||
            string.IsNullOrWhiteSpace(resetPasswordDto.NewPassword))
        {
            return ServiceResult.Failure(CommonErrors.BadRequest("Brak wymaganych danych do zmiany hasła."));
        }

        var user = await _userManager.FindByEmailAsync(resetPasswordDto.Email.Trim().ToLowerInvariant());
        if (user == null)
        {
            return ServiceResult.Failure(CommonErrors.BadRequest("Nieudana próba resetu hasła."));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var resetResult = await _userManager.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.NewPassword);
        if (!resetResult.Succeeded)
        {
            var errorMessage = string.Join(", ", resetResult.Errors.Select(e => e.Description));
            return ServiceResult.Failure(errorMessage);
        }

        if (!string.IsNullOrWhiteSpace(resetPasswordDto.Username))
        {
            var cleanUsername = resetPasswordDto.Username.Trim();
            if (!string.Equals(user.UserName, cleanUsername, StringComparison.OrdinalIgnoreCase))
            {
                var setNameResult = await _userManager.SetUserNameAsync(user, cleanUsername);
                if (!setNameResult.Succeeded)
                {
                    var nameErrors = string.Join(", ", setNameResult.Errors.Select(e => e.Description));
                    return ServiceResult.Failure(nameErrors);
                }
            }
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
        }

        await RevokeAllUserRefreshTokensAsync(user.Id);
        await transaction.CommitAsync();

        _logger.LogInformation("Hasło i profil dla {Email} zostały pomyślnie zaktualizowane.", user.Email);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<LoginResponse>> HandleExternalLoginAsync()
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning("Brak informacji od zewnętrznego dostawcy OAuth.");
            return ServiceResult<LoginResponse>.Failure(AuthErrors.ExternalAuthFailed("Unknown"));
        }

        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signInResult.IsLockedOut)
        {
            return ServiceResult<LoginResponse>.Failure("Konto użytkownika jest zablokowane.");
        }

        ApplicationUser? user;
        if (signInResult.Succeeded)
        {
            user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user == null)
            {
                return ServiceResult<LoginResponse>.Failure(AuthErrors.ExternalAuthFailed(info.LoginProvider));
            }
        }
        else
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                return ServiceResult<LoginResponse>.Failure(AuthErrors.EmailRequired());
            }

            var cleanEmail = email.Trim().ToLowerInvariant();
            user = await _userManager.FindByEmailAsync(cleanEmail);
            if (user == null)
            {
                var uniqueUsername = await GenerateUniqueUsernameAsync(cleanEmail.Split('@')[0]);
                user = new ApplicationUser
                {
                    UserName = uniqueUsername,
                    Email = cleanEmail,
                    EmailConfirmed = true
                };

                var createUserResult = await _userManager.CreateAsync(user);
                if (!createUserResult.Succeeded)
                {
                    return ServiceResult<LoginResponse>.Failure(AuthErrors.UserCreationFailed());
                }

                await _userManager.AddToRoleAsync(user, "User");
            }
            else if (await _userManager.IsLockedOutAsync(user))
            {
                return ServiceResult<LoginResponse>.Failure("Konto użytkownika jest zablokowane.");
            }

            var addLoginResult = await _userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
            {
                return ServiceResult<LoginResponse>.Failure(AuthErrors.AccountLinkingFailed());
            }
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return ServiceResult<LoginResponse>.Failure("Konto użytkownika jest zablokowane.");
        }

        var isAdmin = await _userManager.IsInRoleAsync(user, "Administrator");
        var refreshLifetime = isAdmin ? AdminRefreshTokenLifetime : UserRefreshTokenLifetime;

        var token = await GenerateJwtTokenAsync(user, AccessTokenLifetime);
        var refreshToken = await CreateAndSaveRefreshTokenAsync(user.Id, refreshLifetime);
        return ServiceResult<LoginResponse>.Success(new LoginResponse(token, refreshToken));
    }

    public async Task<ServiceResult> LogoutAsync(string? refreshToken = null)
    {
        await _signInManager.SignOutAsync();

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hashedToken = HashToken(refreshToken);
            var tokenEntity = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == hashedToken);
            if (tokenEntity != null && !tokenEntity.IsRevoked)
            {
                tokenEntity.IsRevoked = true;
                tokenEntity.RevokedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        else if (_currentUserService.IsAuthenticated)
        {
            await RevokeAllUserRefreshTokensAsync(_currentUserService.UserId);
        }

        return ServiceResult.Success();
    }

    private async Task<string> CreateAndSaveRefreshTokenAsync(Guid userId, TimeSpan lifetime)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(randomBytes);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = HashToken(rawToken),
            UserId = userId,
            CreationDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.Add(lifetime),
            IsRevoked = false
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return rawToken;
    }

    private async Task RevokeAllUserRefreshTokensAsync(Guid userId)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ToListAsync();

        if (activeTokens.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var t in activeTokens)
        {
            t.IsRevoked = true;
            t.RevokedDate = now;
        }

        await _context.SaveChangesAsync();
    }

    private static string HashToken(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes);
    }

    private async Task<string> GenerateUniqueUsernameAsync(string baseUsername)
    {
        var candidate = baseUsername;
        var counter = 1;
        while (await _userManager.FindByNameAsync(candidate) != null)
        {
            candidate = $"{baseUsername}{counter++}";
        }
        return candidate;
    }

    private async Task<ApplicationUser?> FindByIdentifierAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;

        var clean = identifier.Trim();
        return clean.Contains('@')
            ? await _userManager.FindByEmailAsync(clean.ToLowerInvariant())
            : await _userManager.FindByNameAsync(clean);
    }

    private async Task<string> GenerateJwtTokenAsync(ApplicationUser user, TimeSpan lifetime)
    {
        var userIdString = user.Id.ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userIdString),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        var userRoles = await _userManager.GetRolesAsync(user);
        foreach (var role in userRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}