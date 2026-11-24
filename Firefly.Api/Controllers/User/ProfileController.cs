using Firefly.Api.Models.Dto.Contact;
using Firefly.Api.Models.Dto.Profile;
using Firefly.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Firefly.Api.Controllers.Mobile;

/// <summary>
/// Zarządzanie profilem użytkownika i formularzem kontaktowym.
/// </summary>
[ApiController]
[Authorize]
[Route("api/profile")]
[Produces("application/json")]
public class ProfileController : FireflyBaseController
{
    private readonly IProfileService _profileService;
    private readonly IEmailService _emailService;

    public ProfileController(IProfileService profileService, IEmailService emailService)
    {
        _profileService = profileService;
        _emailService = emailService;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile()
    {
        var result = await _profileService.GetProfileAsync();
        return HandleServiceResult(result);
    }

    [HttpPut("update-profile")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UpdateProfileResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDto dto)
    {
        var result = await _profileService.UpdateProfileAsync(dto);
        return HandleServiceResult(result);
    }

    [HttpPost("contact")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendContactMessage([FromBody] ContactMessageDto dto)
    {
        var result = await _emailService.SendContactMessageToAdminAsync(dto);
        return HandleServiceResult(result);
    }
}