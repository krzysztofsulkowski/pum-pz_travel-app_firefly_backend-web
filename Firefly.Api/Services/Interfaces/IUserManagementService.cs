using Firefly.Api.Models.DatabaseModels;
using Firefly.Api.Models.Dto;
using Firefly.Api.Models.Dto.Users;
using Firefly.Api.Services.Results;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Firefly.Api.Services.Interfaces
{
    public interface IUserManagementService
    {
        Task<ServiceResult<List<IdentityRole<Guid>>>> GetAllRolesAsync();
        Task<ServiceResult<PagedResult<UserDto>>> GetAllUsersAsync(PagedQueryDto query);
        Task<ServiceResult<UserDto>> GetUserById(Guid userId);
        Task<ServiceResult<UserDto>> CreateUser(UserDto user, ClaimsPrincipal currentUser);
        Task<ServiceResult> UpdateUser(UserDto model);
        Task<ServiceResult> LockUser(Guid userId, Guid currentUserId);
        Task<ServiceResult> UnlockUser(Guid userId);
        Task<ServiceResult> ResendSetupLinkAsync(Guid userId);
    }
}