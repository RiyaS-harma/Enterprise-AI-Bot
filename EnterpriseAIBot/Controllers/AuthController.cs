using EnterpriseAIBot.Data.Entities;
using EnterpriseAIBot.Models;
using EnterpriseAIBot.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EnterpriseAIBot.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const string AccessTokenCookieName = "capg_access_token";

    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IJwtTokenService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IJwtTokenService jwtService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { error = "Invalid email or password." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed login attempt for {Email}.", request.Email);
            return Unauthorized(new { error = "Invalid email or password." });
        }

        var tokenResult = await _jwtService.GenerateTokenAsync(user);
        var roles = await _userManager.GetRolesAsync(user);

        Response.Cookies.Append(AccessTokenCookieName, tokenResult.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = new DateTimeOffset(tokenResult.ExpiresAtUtc)
        });

        _logger.LogInformation("User {Email} logged in.", request.Email);

        return Ok(new AuthResponse
        {
            Token = tokenResult.Token,
            Email = user.Email!,
            DisplayName = user.DisplayName,
            Roles = roles,
            ExpiresAt = tokenResult.ExpiresAtUtc
        });
    }

    // Admin-only: register new users and assign roles
    [HttpPost("register")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var allowedRoles = new[] { AppRoles.Employee, AppRoles.Admin };
        if (!allowedRoles.Contains(request.Role))
            return BadRequest(new { error = $"Role must be '{AppRoles.Employee}' or '{AppRoles.Admin}'." });

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, request.Role);

        _logger.LogInformation("User {Email} registered with role {Role}.", request.Email, request.Role);
        return Ok(new { message = $"User registered successfully with role '{request.Role}'." });
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AccessTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax
        });

        return Ok(new { message = "Logged out successfully." });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        return Ok(new
        {
            email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            displayName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            roles
        });
    }
}

