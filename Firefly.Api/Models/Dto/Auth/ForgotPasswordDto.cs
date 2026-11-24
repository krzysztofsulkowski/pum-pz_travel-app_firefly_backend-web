using System.ComponentModel.DataAnnotations;

namespace Firefly.Api.Models.Dto.Auth
{
    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? FrontendOrigin { get; set; }
    }

    public class ResetPasswordDto
    {
        [Required(ErrorMessage = "Adres e-mail jest wymagany.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Token jest wymagany.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nowe hasło jest wymagane.")]
        [MinLength(8, ErrorMessage = "Hasło musi mieć co najmniej 8 znaków.")]
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>
        /// Opcjonalna zmiana lub zatwierdzenie loginu podczas pierwszego setupu hasła.
        /// </summary>
        [RegularExpression(@"^[a-zA-Z0-9_\-\.]{3,30}$", ErrorMessage = "Nazwa użytkownika musi mieć od 3 do 30 znaków.")]
        public string? Username { get; set; }
    }
}
