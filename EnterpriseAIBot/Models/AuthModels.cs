using System.ComponentModel.DataAnnotations;

namespace EnterpriseAIBot.Models;

public sealed record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed record RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string DisplayName { get; init; } = string.Empty;

    // Only "Employee" or "Admin" accepted
    public string Role { get; init; } = "Employee";
}

public sealed record AuthResponse
{
    public string Token { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IList<string> Roles { get; init; } = [];
    public DateTime ExpiresAt { get; init; }
}

