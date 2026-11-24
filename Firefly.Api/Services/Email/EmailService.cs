using System.Net;
using Firefly.Api.Models.Dto.Contact;
using Firefly.Api.Services.Errors;
using Firefly.Api.Services.Interfaces;
using Firefly.Api.Services.Results;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Firefly.Api.Services.Email;

public class EmailService : IEmailService
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(
        IEmailSender emailSender,
        ILogger<EmailService> logger,
        IConfiguration configuration)
    {
        _emailSender = emailSender;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ServiceResult> SendContactMessageToAdminAsync(ContactMessageDto message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message.Email) || string.IsNullOrWhiteSpace(message.Message))
            {
                return ServiceResult.Failure("Adres e-mail oraz treść wiadomości są wymagane.");
            }

            var adminEmail = new[]
            {
                _configuration["EmailConfiguration:AdminEmail"],
                _configuration["EmailConfiguration:SupportEmail"],
                _configuration["EmailConfiguration:From"]
            }.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

            if (string.IsNullOrEmpty(adminEmail))
            {
                _logger.LogError("Adres email administratora nie został skonfigurowany.");
                return ServiceResult.Failure(EmailError.ConfigurationError());
            }

            var rawSubject = (string.IsNullOrWhiteSpace(message.Subject)
                ? "Zapytanie z Centrum Pomocy"
                : message.Subject.Trim())
                .Replace("\r", " ")
                .Replace("\n", " ");

            var cleanUserEmail = message.Email.Trim();
            var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(message.Name) ? "Użytkownik" : message.Name.Trim());
            var safeEmail = WebUtility.HtmlEncode(cleanUserEmail);
            var safeSubject = WebUtility.HtmlEncode(rawSubject);
            var safeMessage = WebUtility.HtmlEncode(message.Message.Trim()).Replace("\n", "<br/>");

            var emailSubject = $"[Firefly Wsparcie] {rawSubject}";
            var emailBody = $@"
                <div style='font-family: Arial, sans-serif; color: #1B1C18; line-height: 1.6; max-width: 600px;'>
                    <h2 style='color: #536500; border-bottom: 2px solid #536500; padding-bottom: 8px;'>Nowa wiadomość z Centrum Pomocy</h2>
                    <p><strong>Użytkownik:</strong> {safeName}</p>
                    <p><strong>Adres zwrotny:</strong> <a href='mailto:{safeEmail}' style='color: #536500;'>{safeEmail}</a></p>
                    <p><strong>Wybrany temat:</strong> {safeSubject}</p>
                    <p><strong>Treść zgłoszenia:</strong></p>
                    <div style='background-color: #F6F3F2; padding: 16px; border-left: 4px solid #536500; border-radius: 6px; margin: 12px 0;'>
                        {safeMessage}
                    </div>
                    <hr style='border: none; border-top: 1px solid #E1E3D5; margin: 20px 0;'>
                    <p style='font-size: 11px; color: #757962;'>Wiadomość wysłana z poziomu profilu użytkownika w aplikacji Firefly.</p>
                </div>
            ";

            await _emailSender.SendEmailAsync(adminEmail, emailSubject, emailBody);
            await SendContactConfirmationToUserAsync(cleanUserEmail, message.Name, safeSubject, safeMessage);

            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wystąpił błąd podczas wysyłania wiadomości kontaktowej.");
            return ServiceResult.Failure(EmailError.SendFailed());
        }
    }

    public Task<ServiceResult> SendAccountSetupAsync(string recipientEmail, string userName, string setupUrl)
        => SendTemplateAsync(
            recipientEmail,
            "Firefly – ustaw hasło do swojego konta",
            "AccountSetupEmail.html",
            new Dictionary<string, string>
            {
                ["{UserName}"] = WebUtility.HtmlEncode(userName),
                ["{SetupUrl}"] = WebUtility.HtmlEncode(setupUrl)
            });

    public Task<ServiceResult> SendPasswordResetEmailAsync(string recipientEmail, string userName, string resetUrl)
        => SendTemplateAsync(
            recipientEmail,
            "Reset hasła – Firefly",
            "ForgotPasswordEmail.html",
            new Dictionary<string, string>
            {
                ["{UserName}"] = WebUtility.HtmlEncode(userName),
                ["{CallbackUrl}"] = WebUtility.HtmlEncode(resetUrl)
            });

    private async Task SendContactConfirmationToUserAsync(string userEmail, string? userName, string safeSubject, string safeMessageHtml)
    {
        try
        {
            var safeUserName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(userName) ? "Użytkowniku" : userName.Trim());
            const string confirmationSubject = "Potwierdzenie otrzymania wiadomości – Firefly";
            var confirmationBody = $@"
                <div style='font-family: Arial, sans-serif; color: #1B1C18; line-height: 1.6; max-width: 600px;'>
                    <h2 style='color: #536500;'>Dziękujemy za kontakt!</h2>
                    <p>Witaj <strong>{safeUserName}</strong>,</p>
                    <p>Otrzymaliśmy Twoją wiadomość w sprawie: <em>{safeSubject}</em>. Nasz zespół odpowie na nią bezpośrednio na ten adres e-mail.</p>
                    <div style='background-color: #F6F3F2; padding: 14px; border-left: 4px solid #536500; border-radius: 4px; margin: 16px 0; font-size: 13px;'>
                        {safeMessageHtml}
                    </div>
                    <hr style='border: none; border-top: 1px solid #E1E3D5; margin: 20px 0;'>
                    <p style='color: #536500; font-weight: bold;'>Zespół Firefly</p>
                </div>
            ";

            await _emailSender.SendEmailAsync(userEmail, confirmationSubject, confirmationBody);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać automatycznego potwierdzenia do użytkownika {UserEmail}.", userEmail);
        }
    }

    private async Task<ServiceResult> SendTemplateAsync(
        string recipientEmail,
        string subject,
        string templateFileName,
        Dictionary<string, string> placeholders)
    {
        try
        {
            var templatePath = ResolveTemplatePath(templateFileName);
            if (!File.Exists(templatePath))
            {
                _logger.LogError("Nie znaleziono szablonu email: {TemplatePath}", templatePath);
                return ServiceResult.Failure("Błąd szablonu wiadomości.");
            }

            var body = await File.ReadAllTextAsync(templatePath);
            foreach (var (key, value) in placeholders)
            {
                body = body.Replace(key, value);
            }

            await _emailSender.SendEmailAsync(recipientEmail, subject, body);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd wysyłania wiadomości ({Template}) do {RecipientEmail}", templateFileName, recipientEmail);
            return ServiceResult.Failure(EmailError.SendFailed());
        }
    }

    private static string ResolveTemplatePath(string templateFileName)
    {
        var primaryPath = Path.Combine(AppContext.BaseDirectory, "Templates", templateFileName);
        if (File.Exists(primaryPath)) return primaryPath;

        return Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateFileName);
    }
}