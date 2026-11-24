using System.ComponentModel.DataAnnotations;

namespace Firefly.Api.Models.Dto.Auth
{
    public class RefreshTokenRequestDto
    {
        [Required(ErrorMessage = "Token odświeżający jest wymagany.")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}