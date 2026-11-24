using Firefly.Api.Models.Dto.Profile;
using Firefly.Api.Services.Results;

namespace Firefly.Api.Services.Interfaces
{
    public interface IProfileService
    {
        Task<ServiceResult<UserProfileDto>> GetProfileAsync();       
        Task<ServiceResult<UpdateProfileResponseDto>> UpdateProfileAsync(UpdateProfileDto dto);        
    }
}