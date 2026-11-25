using Firefly.Api.Models.DatabaseModels;
using Firefly.Api.Models.Dto.Auth;
using Firefly.Api.Services.Auth;
using Firefly.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;

namespace Firefly.Api.Controllers;

[ApiController]
[Route("api/authentication")]
[Produces("application/json")]
public class AuthController : FireflyBaseController
{
    private readonly IAuthService _authService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAuthService authService,
        IConfiguration configuration,
        SignInManager<ApplicationUser> signInManager)
    {
        _authService = authService;
        _signInManager = signInManager;
        _configuration = configuration;
    }

    /// <summary>
    /// Rejestruje nowego użytkownika na podstawie e-maila, nazwy użytkownika i hasła oraz wysyła link potwierdzający e-mail.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        => HandleServiceResult(await _authService.RegisterAsync(registerDto));

    /// <summary>
    /// Loguje użytkownika przy użyciu nazwy użytkownika lub adresu e-mail oraz hasła.
    /// </summary>
    [EnableRateLimiting("LoginPolicy")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        => HandleServiceResult(await _authService.Login(loginDto));

    /// <summary>
    /// Loguje administratora do panelu zarządzania (wymaga roli Administrator).
    /// </summary>
    [EnableRateLimiting("LoginPolicy")]
    [HttpPost("admin-login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> AdminLogin([FromBody] LoginDto loginDto)
        => HandleServiceResult(await _authService.AdminLogin(loginDto));

    /// <summary>
    /// Odświeża token dostępu (JWT) przy użyciu ważnego refresh tokena (z rotacją tokena).
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
        => HandleServiceResult(await _authService.RefreshTokenAsync(dto));

    /// <summary>
    /// Wysyła wiadomość e-mail z linkiem do zresetowania zapomnianego hasła.
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        => HandleServiceResult(await _authService.ForgotPasswordAsync(forgotPasswordDto));

    /// <summary>
    /// Ustawia nowe hasło użytkownika (używane także przy pierwszym ustawieniu hasła po rejestracji).
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        => HandleServiceResult(await _authService.ResetPasswordAsync(resetPasswordDto));

    /// <summary>
    /// Rozpoczyna logowanie przez zewnętrznego dostawcę OAuth (np. Google).
    /// </summary>
    [HttpGet("external-login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ExternalLogin(string provider = "Google", string? returnUrl = null)
    {
        var frontendOrigin = FrontendOriginResolver.Resolve(_configuration, returnUrl);
        if (frontendOrigin is null)
        {
            return Problem(
                detail: "Brak poprawnego FRONTEND_BASE_URL lub origin nie jest dozwolony.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), null, new { returnUrl = frontendOrigin });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return new ChallengeResult(provider, properties);
    }

    /// <summary>
    /// Obsługuje powrót od zewnętrznego dostawcy OAuth i przekierowuje na frontend z tokenami we fragmencie URL.
    /// </summary>
    [HttpGet("external-login-callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        var frontendUrl = FrontendOriginResolver.Resolve(_configuration, returnUrl);
        if (frontendUrl is null)
        {
            return Problem(
                detail: "Brak poprawnego FRONTEND_BASE_URL lub origin nie jest dozwolony.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (remoteError != null)
        {
            return Redirect($"{frontendUrl}/login?error={WebUtility.UrlEncode(remoteError)}");
        }

        var result = await _authService.HandleExternalLoginAsync();
        if (!result.IsSuccess)
        {
            return Redirect($"{frontendUrl}/login?error={WebUtility.UrlEncode(result.Error.Description)}");
        }

        var encodedToken = Uri.EscapeDataString(result.Data.Token);
        var encodedRefreshToken = Uri.EscapeDataString(result.Data.RefreshToken);
        return Redirect($"{frontendUrl}/login-success#token={encodedToken}&refreshToken={encodedRefreshToken}");
    }

    /// <summary>
    /// Wylogowuje użytkownika: unieważnia podany refresh token (nawet bez ważnego JWT),
    /// a jeśli nie podano tokena i użytkownik jest zalogowany — unieważnia wszystkie jego aktywne sesje.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RevokeTokenRequestDto? dto = null)
        => HandleServiceResult(await _authService.LogoutAsync(dto?.RefreshToken));
}