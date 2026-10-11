using Microsoft.AspNetCore.Identity;

namespace EnterpriseAIBot.Data.Entities;

public sealed class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

