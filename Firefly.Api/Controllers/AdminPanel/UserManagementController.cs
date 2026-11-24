using Firefly.Api.Extensions;
using Firefly.Api.Models.Dto;
using Firefly.Api.Models.Dto.Users;
using Firefly.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Firefly.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Administrator")]
[Route("api/adminPanel/users")]
[Produces("application/json")]
public class UserManagementController : FireflyBaseController
{
    private readonly IUserManagementService _userManagementService;

    public UserManagementController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    /// <summary>
    /// Pobiera listę wszystkich dostępnych ról w systemie (np. User, Administrator).
    /// </summary>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(List<IdentityRole<Guid>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllRoles()
        => HandleServiceResult(await _userManagementService.GetAllRolesAsync());

    /// <summary>
    /// Pobiera listę użytkowników w formacie tabelarycznym.
    /// </summary>
    /// <param name="query">Parametry tabeli (wyszukiwanie, sortowanie, stronicowanie).</param>
    [HttpGet("get-all-users")]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllUsers([FromQuery] PagedQueryDto query)
        => HandleServiceResult(await _userManagementService.GetAllUsersAsync(query));

    /// <summary>
    /// Tworzy nowego użytkownika administracyjnie.
    /// </summary>
    /// <remarks>
    /// Konto powstaje z losowym hasłem, a użytkownik dostaje e-mail z linkiem do ustawienia własnego hasła (ważnym 24 h).
    /// Jeśli wysyłka e-maila się nie powiedzie, konto nie zostaje utworzone. Przypisuje wskazaną rolę.
    /// </remarks>
    [HttpPost("create-user")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateUser([FromBody] UserDto userDto)
        => HandleServiceResult(await _userManagementService.CreateUser(userDto, User));

    /// <summary>
    /// Aktualizuje dane istniejącego użytkownika (Nazwa, Email, Rola).
    /// </summary>
    [HttpPost("update-user")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateUser([FromBody] UserDto user)
        => HandleServiceResult(await _userManagementService.UpdateUser(user));

    /// <summary>
    /// Blokuje użytkownika.
    /// </summary>
    /// <remarks>
    /// Nie można zablokować własnego konta.
    /// </remarks>
    [HttpPost("lock-user/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LockUser([FromRoute] Guid userId)
        => HandleServiceResult(await _userManagementService.LockUser(userId, User.GetUserId()));

    /// <summary>
    /// Odblokowuje użytkownika.
    /// </summary>
    [HttpPost("unlock-user/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UnlockUser([FromRoute] Guid userId)
        => HandleServiceResult(await _userManagementService.UnlockUser(userId));

    /// <summary>
    /// Pobiera szczegóły pojedynczego użytkownika.
    /// </summary>
    [HttpGet("get-user-by-id")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserById([FromQuery, BindRequired] Guid userId)
        => HandleServiceResult(await _userManagementService.GetUserById(userId));

    /// <summary>
    /// Wysyła ponownie link aktywacyjny (do ustawienia hasła) dla nieaktywnego konta.
    /// </summary>
    [HttpPost("resend-setup-link/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResendSetupLink([FromRoute] Guid userId)
        => HandleServiceResult(await _userManagementService.ResendSetupLinkAsync(userId));
}