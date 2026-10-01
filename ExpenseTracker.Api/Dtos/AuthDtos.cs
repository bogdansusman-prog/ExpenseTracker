using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos;

public class RegisterDto
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public record UserDto(string Id, string Email, string DisplayName, string? AvatarUrl, DateTime CreatedAt);

public class UpdateProfileDto
{
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}

public class UpdateAvatarDto
{
    /// <summary>data:image/png|jpeg|webp;base64,... (max ~400 KB)</summary>
    [Required]
    public string DataUrl { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}

public record AuthResponseDto(string Token, DateTime ExpiresAt, UserDto User);
