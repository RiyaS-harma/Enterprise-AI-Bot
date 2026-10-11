using EnterpriseAIBot.Data.Entities;

namespace EnterpriseAIBot.Services.Interfaces;

public sealed record JwtTokenResult(string Token, DateTime ExpiresAtUtc);

public interface IJwtTokenService
{
    Task<JwtTokenResult> GenerateTokenAsync(AppUser user);
}

