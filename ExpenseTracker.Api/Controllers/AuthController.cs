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
            return BadRequest(string.Join(" ", result.Errors.Select(error => error.Description)));
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);

        return user is null ? Unauthorized() : Ok(ToDto(user));
    }

    private AuthResponseDto CreateResponse(AppUser user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponseDto(token, expiresAt, ToDto(user));
    }

    private static UserDto ToDto(AppUser user) => new(user.Id, user.Email ?? string.Empty, user.DisplayName);

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
