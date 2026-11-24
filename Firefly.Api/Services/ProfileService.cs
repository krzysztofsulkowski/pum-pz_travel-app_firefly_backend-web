using Firefly.Api.Models.DatabaseModels;
using Firefly.Api.Models.Dto.Profile;
using Firefly.Api.Services.Auth;
using Firefly.Api.Services.Errors;
using Firefly.Api.Services.Interfaces;
using Firefly.Api.Services.Results;
using Firefly.Api.Services.Storage;
using Microsoft.AspNetCore.Identity;

namespace Firefly.Api.Services;

public class ProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<ProfileService> _logger;
    private readonly ICurrentUserService _currentUserService;

    public ProfileService(
        UserManager<ApplicationUser> userManager,
        IFileStorageService fileStorageService,
        ILogger<ProfileService> logger,
        ICurrentUserService currentUserService)
    {
        _userManager = userManager;
        _fileStorageService = fileStorageService;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<UserProfileDto>> GetProfileAsync()
    {
        var userId = _currentUserService.UserId;
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ServiceResult<UserProfileDto>.NotFound(CommonErrors.NotFound("User", userId));
        }

        var dto = new UserProfileDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            AvatarUrl = user.AvatarUrl
        };

        return ServiceResult<UserProfileDto>.Success(dto);
    }

    public async Task<ServiceResult<UpdateProfileResponseDto>> UpdateProfileAsync(UpdateProfileDto dto)
    {
        var userId = _currentUserService.UserId;
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ServiceResult<UpdateProfileResponseDto>.NotFound(CommonErrors.NotFound("User", userId));
        }

        if (!string.IsNullOrWhiteSpace(dto.Username) && dto.Username.Trim() != user.UserName)
        {
            var setUserNameResult = await _userManager.SetUserNameAsync(user, dto.Username.Trim());
            if (!setUserNameResult.Succeeded)
            {
                var errors = string.Join(", ", setUserNameResult.Errors.Select(e => e.Description));
                return ServiceResult<UpdateProfileResponseDto>.Failure($"Błąd zmiany nazwy: {errors}");
            }
        }

        if (dto.Avatar != null && dto.Avatar.Length > 0)
        {
            var previousAvatarUrl = user.AvatarUrl;
            var newAvatarUrl = await _fileStorageService.SaveFileAsync(dto.Avatar, "avatars");
            if (string.IsNullOrWhiteSpace(newAvatarUrl))
            {
                return ServiceResult<UpdateProfileResponseDto>.Failure("Nie udało się zapisać przesłanego zdjęcia profilowego (sprawdź format lub rozmiar maks. 5 MB).");
            }

            user.AvatarUrl = newAvatarUrl;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                await _fileStorageService.DeleteFileAsync(newAvatarUrl);
                return ServiceResult<UpdateProfileResponseDto>.Failure("Nie udało się zaktualizować zdjęcia profilowego.");
            }

            if (!string.IsNullOrWhiteSpace(previousAvatarUrl))
            {
                await _fileStorageService.DeleteFileAsync(previousAvatarUrl);
            }
        }

        _logger.LogInformation("Zaktualizowano profil użytkownika {UserId}", userId);

        return ServiceResult<UpdateProfileResponseDto>.Success(new UpdateProfileResponseDto(user.AvatarUrl, user.UserName));
    }
}