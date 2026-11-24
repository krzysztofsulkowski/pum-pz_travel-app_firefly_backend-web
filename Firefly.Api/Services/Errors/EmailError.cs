namespace Firefly.Api.Services.Errors
{
    public static class EmailError
    {
        private static readonly string objectName = "Email";
        public static ServiceError ConfigurationError() => new ServiceError(
            $"{objectName}.ConfigurationError", "Błąd konfiguracji serwera. Nie można wysłać wiadomości.");

        public static ServiceError SendFailed() => new ServiceError(
            $"{objectName}.SendFailed", "Wystąpił błąd podczas wysyłania wiadomości. Spróbuj ponownie później.");
    }
}
