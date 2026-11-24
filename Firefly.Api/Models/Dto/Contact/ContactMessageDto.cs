using System.ComponentModel.DataAnnotations;

namespace Firefly.Api.Models.Dto.Contact
{
    public class ContactMessageDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Adres e-mail jest wymagany.")]
        [EmailAddress(ErrorMessage = "Nieprawidłowy format adresu e-mail.")]
        public string Email { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Subject { get; set; }

        [Required(ErrorMessage = "Treść wiadomości nie może być pusta.")]
        [MaxLength(3000, ErrorMessage = "Wiadomość jest zbyt długa (maks. 3000 znaków).")]
        public string Message { get; set; } = string.Empty;
    }
}