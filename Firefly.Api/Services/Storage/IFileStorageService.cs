using Microsoft.AspNetCore.Http;

namespace Firefly.Api.Services.Storage;

public interface IFileStorageService
{
    Task<string?> SaveFileAsync(IFormFile file, string subfolder = "avatars");
    Task DeleteFileAsync(string? storagePathOrUrl);
}