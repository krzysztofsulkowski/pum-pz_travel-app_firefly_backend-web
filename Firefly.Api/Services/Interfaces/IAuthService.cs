using Firefly.Api.Models.Dto.Auth;
using Firefly.Api.Services.Results;

namespace Firefly.Api.Services.Interfaces
{
    public interface IAuthService
    {
        Task<ServiceResult> RegisterAsync(RegisterDto registerDto);
        Task<ServiceResult<LoginResponse>> Login(LoginDto loginDto);
        Task<ServiceResult<LoginResponse>> AdminLogin(LoginDto loginDto);
        Task<ServiceResult<LoginResponse>> RefreshTokenAsync(RefreshTokenRequestDto dto);
        Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto);
        Task<ServiceResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
        Task<ServiceResult<LoginResponse>> HandleExternalLoginAsync();
        Task<ServiceResult> LogoutAsync(string? refreshToken = null);
    }
}