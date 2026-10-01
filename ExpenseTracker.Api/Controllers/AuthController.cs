using System.Security.Claims;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<AppUser> userManager,
    JwtTokenService tokens,
    AppDbContext context,
    ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto, CancellationToken cancellationToken)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Conflict("An account with this email already exists.");
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = dto.DisplayName.Trim()
        };

        var result = await userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            return BadRequest(Errors(result));
        }

        await ClaimDataCreatedBeforeAccountsAsync(user, cancellationToken);

        return Ok(CreateResponse(user));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email.Trim().ToLowerInvariant());

        // Same message for "no such user" and "wrong password", so emails can't be probed.
        if (user is null || !await userManager.CheckPasswordAsync(user, dto.Password))
        {
            return Unauthorized("Email or password is incorrect.");
        }

        return Ok(CreateResponse(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await CurrentUserAsync();
        return user is null ? Unauthorized() : Ok(ToDto(user));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileDto dto)
    {
        var user = await CurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var name = dto.DisplayName.Trim();
        if (name.Length == 0)
        {
            return BadRequest("Name is required.");
        }

        user.DisplayName = name;
        var result = await userManager.UpdateAsync(user);

        return result.Succeeded ? Ok(ToDto(user)) : BadRequest(Errors(result));
    }

    private const int MaxAvatarLength = 400_000;

    private static readonly string[] AllowedAvatarPrefixes =
    [
        "data:image/png;base64,",
        "data:image/jpeg;base64,",
        "data:image/webp;base64,"
    ];

    [Authorize]
    [HttpPut("avatar")]
    public async Task<ActionResult<UserDto>> UpdateAvatar(UpdateAvatarDto dto)
    {
        var user = await CurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var prefix = AllowedAvatarPrefixes.FirstOrDefault(allowed => dto.DataUrl.StartsWith(allowed, StringComparison.Ordinal));
        if (prefix is null)
        {
            return BadRequest("Only PNG, JPEG or WebP images are allowed.");
        }

        if (dto.DataUrl.Length > MaxAvatarLength)
        {
            return BadRequest("The image is too large (max ~300 KB).");
        }

        try
        {
            // Make sure the payload really is base64 and not arbitrary text.
            Convert.FromBase64String(dto.DataUrl[prefix.Length..]);
        }
        catch (FormatException)
        {
            return BadRequest("The image data is invalid.");
        }

        user.AvatarDataUrl = dto.DataUrl;
        var result = await userManager.UpdateAsync(user);

        return result.Succeeded ? Ok(ToDto(user)) : BadRequest(Errors(result));
    }

    [Authorize]
    [HttpDelete("avatar")]
    public async Task<ActionResult<UserDto>> DeleteAvatar()
    {
        var user = await CurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        user.AvatarDataUrl = null;
        var result = await userManager.UpdateAsync(user);

        return result.Succeeded ? Ok(ToDto(user)) : BadRequest(Errors(result));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var user = await CurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        if (!await userManager.CheckPasswordAsync(user, dto.CurrentPassword))
        {
            return BadRequest("The current password is incorrect.");
        }

        var result = await userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);

        return result.Succeeded ? NoContent() : BadRequest(Errors(result));
    }

    private async Task<AppUser?> CurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await userManager.FindByIdAsync(userId);
    }

    private static string Errors(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Description));

    private AuthResponseDto CreateResponse(AppUser user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponseDto(token, expiresAt, ToDto(user));
    }

    private static UserDto ToDto(AppUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.DisplayName, user.AvatarDataUrl, user.CreatedAt);

    /// <summary>
    /// The app was single-user before accounts existed. The very first account that registers
    /// takes over all existing categories, transactions and settings, so no data is lost.
    /// </summary>
    private async Task ClaimDataCreatedBeforeAccountsAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (await userManager.Users.CountAsync(cancellationToken) != 1)
        {
            return;
        }

        var categories = await context.Categories.IgnoreQueryFilters()
            .Where(category => category.UserId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(category => category.UserId, user.Id), cancellationToken);

        var transactions = await context.Transactions.IgnoreQueryFilters()
            .Where(transaction => transaction.UserId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(transaction => transaction.UserId, user.Id), cancellationToken);

        var settings = await context.Settings.IgnoreQueryFilters()
            .Where(item => item.UserId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UserId, user.Id), cancellationToken);

        logger.LogInformation(
            "First account {Email} claimed {Categories} categories, {Transactions} transactions and {Settings} settings rows",
            user.Email, categories, transactions, settings);
    }
}
