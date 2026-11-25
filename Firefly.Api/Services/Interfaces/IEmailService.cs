using Firefly.Api.Models.Dto.Contact;
using Firefly.Api.Services.Results;

namespace Firefly.Api.Services.Interfaces
{
    public interface IEmailService
    {
        Task<ServiceResult> SendContactMessageToAdminAsync(ContactMessageDto message);     
        Task<ServiceResult> SendAccountSetupAsync(string recipientEmail, string userName, string setupUrl);
        Task<ServiceResult> SendPasswordResetEmailAsync(string recipientEmail, string userName, string resetUrl);
    }
}