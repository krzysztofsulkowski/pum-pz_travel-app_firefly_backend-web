namespace Firefly.Api.Services.Errors
{
    public static class UserManagementErrors
    {

        public static ServiceError RoleNotFound(string roleId) => new ServiceError(
            "UserMgmt.RoleNotFound", $"Nie znaleziono roli o identyfikatorze '{roleId}'.");

        public static ServiceError CannotLockSelf() => new ServiceError(
            "UserMgmt.SelfAction", "Nie można zablokować własnego konta.");

        public static ServiceError CannotCreateAdminWithoutPrivileges() => new ServiceError(
             "Auth.PrivilegesRequired", "Brak uprawnień. Tylko administratorzy mogą tworzyć konta administratorów.");

        public static ServiceError UpdateFailed(string details) => new ServiceError(
            "UserMgmt.UpdateFailed", details);

        public static ServiceError CreateFailed(string details) => new ServiceError(
            "UserMgmt.CreateFailed", details);
    }
}
