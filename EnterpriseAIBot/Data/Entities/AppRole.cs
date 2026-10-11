using Microsoft.AspNetCore.Identity;

namespace EnterpriseAIBot.Data.Entities;

public sealed class AppRole : IdentityRole
{
    public AppRole() { }
    public AppRole(string roleName) : base(roleName) { }
}

/// <summary>
/// Role name constants used in [Authorize(Roles = ...)] attributes.
/// </summary>
public static class AppRoles
{
    public const string Employee = "Employee";
    public const string Admin = "Admin";
}

