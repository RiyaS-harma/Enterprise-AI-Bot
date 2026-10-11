using System.ComponentModel.DataAnnotations;

namespace EnterpriseAIBot.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32)]
    public string SecretKey { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 60 * 24)]
    public int ExpiryMinutes { get; init; } = 480; // 8 hours
}

