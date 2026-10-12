using System.Security.Cryptography;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Api.Services.Auth;
using ExpenseTracker.Api.Services.Advisor;
using ExpenseTracker.Api.Services.Banking;
using ExpenseTracker.Api.Services.QuickAdd;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ----- Accounts: ASP.NET Core Identity + JWT -----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<AppDbContext>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.Key))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Jwt:Key must be configured outside Development.");
    }

    // Development fallback: a random key per run (you'll need to log in again after a restart).
    // Set a permanent one with: dotnet user-secrets set "Jwt:Key" "<32+ random characters>"
    jwtOptions.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    Console.WriteLine("warn: Jwt:Key is not set, using a temporary development key.");
}

builder.Services.Configure<JwtOptions>(options =>
{
    jwtSection.Bind(options);
    options.Key = jwtOptions.Key;
});
builder.Services.AddSingleton<JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtTokenService.SigningKey(jwtOptions),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// Smart insights: regret score, subscriptions, Monte Carlo forecast, quick add, rule-based advisor
builder.Services.AddSingleton<AppClock>();
builder.Services.AddScoped<InsightsService>();
builder.Services.AddScoped<QuickAddService>();
builder.Services.AddScoped<AdvisorService>();

// Bank sync through Enable Banking (PSD2). Configure it with user secrets, see docs/bank-sync.md.
builder.Services.Configure<EnableBankingOptions>(builder.Configuration.GetSection(EnableBankingOptions.SectionName));
builder.Services.AddHttpClient<EnableBankingClient>(http => http.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<BankSyncService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "ExpenseTracker API v1"
        );
    });
}

app.UseHttpsRedirection();

app.UseCors("AngularClient");

app.UseAuthentication();
app.UseAuthorization();

// Every endpoint requires a logged-in user, except the ones marked [AllowAnonymous] (register / login).
app.MapControllers().RequireAuthorization();

app.Run();