using System.ComponentModel.DataAnnotations;

namespace Firefly.Api.Models.Dto.Auth;

public class RegisterDto
{
    [Required(ErrorMessage = "Adres e-mail jest wymagany.")]
    [EmailAddress(ErrorMessage = "Nieprawidłowy format adresu e-mail.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nazwa użytkownika jest wymagana.")]
    [MinLength(3, ErrorMessage = "Nazwa użytkownika musi mieć co najmniej 3 znaki.")]
    [MaxLength(50, ErrorMessage = "Nazwa użytkownika może mieć maksymalnie 50 znaków.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Hasło jest wymagane.")]
    public string Password { get; set; } = string.Empty;
}