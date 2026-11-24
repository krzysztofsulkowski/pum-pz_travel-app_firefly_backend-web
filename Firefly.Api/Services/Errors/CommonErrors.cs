namespace Firefly.Api.Services.Errors;

public static class CommonErrors
{
    public static ServiceError NotFound(string objectName, object id) => new(
        $"{objectName}.NotFound", $"Obiekt '{objectName}' o identyfikatorze '{id}' nie został znaleziony.");

    public static ServiceError Conflict(string objectName, string details) => new(
        $"{objectName}.Conflict", details);

    public static ServiceError Unauthorized() => new(
        "Auth.Unauthorized", "Brak autoryzacji. Musisz być zalogowany, aby wykonać tę akcję.");

    public static ServiceError Forbidden(string details = "Brak uprawnień do wykonania tej operacji.") => new(
        "Auth.Forbidden", details);

    public static ServiceError BadRequest(string details = "Nieprawidłowe żądanie. Sprawdź poprawność wysłanych danych.") => new(
        "Request.BadRequest", details);

    public static ServiceError InternalServerError(string details = "Wystąpił nieoczekiwany błąd serwera.") => new(
        "Server.InternalError", details);

    public static ServiceError DataProcessingError(string details = "Wystąpił nieoczekiwany błąd podczas przetwarzania danych.") => new(
        "Server.DataProcessingError", details);

    public static ServiceError CreateFailed(string objectName, string? details = null)
    {
        var message = details ?? $"Nie udało się utworzyć obiektu '{objectName}'. Wystąpił nieoczekiwany błąd podczas zapisu.";
        return new ServiceError($"{objectName}.CreateFailed", message);
    }

    public static ServiceError UpdateFailed(string objectName, object id, string? details = null)
    {
        var message = details ?? $"Nie udało się zaktualizować obiektu '{objectName}' o identyfikatorze '{id}'.";
        return new ServiceError($"{objectName}.UpdateFailed", message);
    }

    public static ServiceError FetchFailed(string objectName, string? details = null)
    {
        var message = details ?? $"Wystąpił nieoczekiwany błąd podczas pobierania danych dla obiektu '{objectName}'.";
        return new ServiceError($"{objectName}.FetchFailed", message);
    }

    public static ServiceError DeleteFailed(string objectName, object? id = null, string? details = null)
    {
        var idPart = id != null ? $" (ID: {id})" : "";
        return new ServiceError($"{objectName}.DeleteFailed", details ?? $"Nie udało się usunąć obiektu '{objectName}'{idPart}.");
    }
}