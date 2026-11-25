using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Firefly.Api.Services.Storage;

public class FileStorageService : IFileStorageService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private readonly HttpClient _httpClient;
    private readonly FileStorageSettings _settings;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        HttpClient httpClient,
        IOptions<FileStorageSettings> options,
        ILogger<FileStorageService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<string?> SaveFileAsync(IFormFile file, string subfolder = "avatars")
    {
        if (file == null || file.Length == 0 || file.Length > MaxFileSizeBytes)
            return null;

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";

        if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(file.ContentType))
            return null;

        try
        {
            var storagePath = $"{subfolder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            await using var stream = file.OpenReadStream();
            return await UploadToStorageAsync(stream, storagePath, file.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas zapisu pliku {FileName} do Supabase Storage", file.FileName);
            return null;
        }
    }

    public async Task DeleteFileAsync(string? storagePathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(storagePathOrUrl)) return;

        try
        {
            var storagePath = ExtractStoragePath(storagePathOrUrl);
            if (string.IsNullOrWhiteSpace(storagePath)) return;

            var baseUrl = _settings.Url.TrimEnd('/');
            var deleteEndpoint = $"{baseUrl}/storage/v1/object/{_settings.Bucket}";

            using var request = new HttpRequestMessage(HttpMethod.Delete, deleteEndpoint);
            request.Headers.Add("apikey", _settings.Key);
            
            var payload = new { prefixes = new[] { storagePath } };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Błąd usuwania pliku {Path} z Supabase: {Status} - {Error}", storagePath, response.StatusCode, err);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wyjątek podczas usuwania pliku {Key} z Supabase Storage", storagePathOrUrl);
        }
    }

    private async Task<string?> UploadToStorageAsync(Stream stream, string storagePath, string contentType)
    {
        var baseUrl = _settings.Url.TrimEnd('/');
        var uploadUrl = $"{baseUrl}/storage/v1/object/{_settings.Bucket}/{storagePath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Add("apikey", _settings.Key);
        request.Headers.Add("x-upsert", "true");

        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = content;

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Błąd uploadu do Supabase ({StatusCode}): {Body}", response.StatusCode, errorBody);
            return null;
        }

        return $"{baseUrl}/storage/v1/object/public/{_settings.Bucket}/{storagePath}";
    }

    private string ExtractStoragePath(string fileUrlOrKey)
    {
        var clean = fileUrlOrKey.Trim();
        var marker = $"/object/public/{_settings.Bucket}/";
        var index = clean.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        return index >= 0 ? clean[(index + marker.Length)..] : clean.TrimStart('/');
    }
}