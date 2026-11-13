using ClockMetrics.Core.Entities.Security;

namespace ClockMetrics.Application.Interfaces.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(AppUser appUser, IEnumerable<string> roles);
    string GenerateRefreshToken();
}
