using System.ComponentModel.DataAnnotations;

namespace Firefly.Api.Models.Dto.Auth
{
    public class RevokeTokenRequestDto
    {
        [Required(ErrorMessage = "Token jest wymagany do unieważnienia.")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}