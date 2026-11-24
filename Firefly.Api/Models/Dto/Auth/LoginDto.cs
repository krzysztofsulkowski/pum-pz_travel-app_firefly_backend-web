using System.ComponentModel.DataAnnotations;

namespace Firefly.Api.Models.Dto.Auth
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Adres e-mail lub nazwa użytkownika jest wymagana.")]
        public string Identifier { get; set; } = string.Empty;

        [Required(ErrorMessage = "Hasło jest wymagane.")]
        public string Password { get; set; } = string.Empty;
    }
}
