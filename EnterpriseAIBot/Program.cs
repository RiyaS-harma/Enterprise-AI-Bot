using System.Text;
using Azure.Identity;
using EnterpriseAIBot.AI.SemanticKernel;
using EnterpriseAIBot.AI.Tools;
using EnterpriseAIBot.Configuration;
using EnterpriseAIBot.Data;
using EnterpriseAIBot.Data.Entities;
using EnterpriseAIBot.Services;
using EnterpriseAIBot.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// var keyVaultUri = builder.Configuration["KeyVaultUri"];
// if (!string.IsNullOrEmpty(keyVaultUri))
//     builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());

builder.Services.Configure<AzureOpenAIOptions>(
    builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));
builder.Services.Configure<AzureEmbeddingOptions>(
    builder.Configuration.GetSection(AzureEmbeddingOptions.SectionName));
builder.Services.Configure<AzureSearchOptions>(
    builder.Configuration.GetSection(AzureSearchOptions.SectionName));
builder.Services.Configure<BlobStorageOptions>(
    builder.Configuration.GetSection(BlobStorageOptions.SectionName));
builder.Services.Configure<AdminSeedOptions>(
    builder.Configuration.GetSection(AdminSeedOptions.SectionName));
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(opts => !opts.SecretKey.Contains("<your-secret-key", StringComparison.OrdinalIgnoreCase),
        "Jwt:SecretKey must be set to a real 32+ character key.")
    .ValidateOnStart();

builder.Services.AddDbContext<EnterpriseAIBotDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<AppUser, AppRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<EnterpriseAIBotDbContext>()
.AddDefaultTokenProviders();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");
var key = Encoding.UTF8.GetBytes(jwtOptions.SecretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Support browser auth by reading the JWT from a secure cookie.
            if (string.IsNullOrWhiteSpace(context.Token)
                && context.Request.Cookies.TryGetValue("capg_access_token", out var cookieToken))
            {
                context.Token = cookieToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EmployeeOrAdmin", policy =>
        policy.RequireRole(AppRoles.Employee, AppRoles.Admin));
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole(AppRoles.Admin));
});

builder.Services.AddSingleton<IAzureOpenAIService, AzureOpenAIService>();
builder.Services.AddSingleton<IAzureSearchService, AzureSearchService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();

builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<IHistoryService, HistoryService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddSingleton<SemanticKernelService>();

builder.Services.AddScoped<IMcpTool, SearchDocumentsTool>();
builder.Services.AddScoped<IMcpTool, GetDocumentMetadataTool>();
builder.Services.AddScoped<IMcpTool, GetChatHistoryTool>();
builder.Services.AddScoped<IMcpTool, GetDocumentSourcesTool>();
builder.Services.AddScoped<IRAGOrchestrationService, RAGOrchestrationService>();

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
    o.MultipartBodyLengthLimit = 52_428_800);

builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

var httpsPort = builder.Configuration["ASPNETCORE_HTTPS_PORT"]
    ?? builder.Configuration["HTTPS_PORT"];
if (!string.IsNullOrWhiteSpace(httpsPort))
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<EnterpriseAIBotDbContext>();
    await db.Database.MigrateAsync();

    // Seed roles
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
    foreach (var role in new[] { AppRoles.Employee, AppRoles.Admin })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new AppRole(role));
    }

    // Seed an initial admin user for first-time login.
    var adminSeed = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminSeedOptions>>().Value;
    if (adminSeed.Enabled)
    {
        if (string.IsNullOrWhiteSpace(adminSeed.Email) || string.IsNullOrWhiteSpace(adminSeed.Password))
        {
            startupLogger.LogWarning("AdminSeed is enabled but Email/Password is missing.");
        }
        else
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var adminUser = await userManager.FindByEmailAsync(adminSeed.Email);

            if (adminUser is null)
            {
                adminUser = new AppUser
                {
                    UserName = adminSeed.Email,
                    Email = adminSeed.Email,
                    DisplayName = string.IsNullOrWhiteSpace(adminSeed.DisplayName) ? "System Admin" : adminSeed.DisplayName
                };

                var createResult = await userManager.CreateAsync(adminUser, adminSeed.Password);
                if (!createResult.Succeeded)
                {
                    startupLogger.LogWarning(
                        "Failed to seed admin user {Email}: {Errors}",
                        adminSeed.Email,
                        string.Join("; ", createResult.Errors.Select(e => e.Description)));
                }
                else
                {
                    startupLogger.LogInformation("Seeded admin user {Email}.", adminSeed.Email);
                }
            }

            if (adminUser is not null && !await userManager.IsInRoleAsync(adminUser, AppRoles.Admin))
            {
                await userManager.AddToRoleAsync(adminUser, AppRoles.Admin);
                startupLogger.LogInformation("Assigned Admin role to {Email}.", adminSeed.Email);
            }
        }
    }
}

_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var searchService = scope.ServiceProvider.GetRequiredService<IAzureSearchService>();
        await searchService.EnsureIndexExistsAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Azure AI Search index check failed at startup. Verify AzureSearch configuration.");
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Chat}/{action=Index}/{id?}");

app.Run();

