using Microsoft.AspNetCore.Http;

namespace Firefly.Api.Models.Dto.Profile;

public class UpdateProfileDto
{
    public string? Username { get; set; }
    public IFormFile? Avatar { get; set; }
}

public record UpdateProfileResponseDto(string? AvatarUrl, string? Username = null);