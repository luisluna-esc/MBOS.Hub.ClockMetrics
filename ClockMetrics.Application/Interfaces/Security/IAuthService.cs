using ClockMetrics.Application.Dtos.Security;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;

namespace ClockMetrics.Application.Interfaces.Security;

public interface IAuthService
{
    Task<ResponseGetObject> Login(LoginDto loginDto, string? ipAddress);
    Task<ResponseGetObject> RefreshToken(RefreshRequestDto refreshRequestDto, string? ipAddress);
    Task<ResponsePost> Logout(LogoutRequestDto logoutRequestDto);
}
