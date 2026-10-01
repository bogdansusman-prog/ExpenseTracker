using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Api.Models;

/// <summary>Application user (ASP.NET Core Identity). Every category, transaction and setting belongs to one user.</summary>
public class AppUser : IdentityUser
{
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Profile picture as a small data URL (the client resizes it to 256x256 before upload),
    /// so it can be shown directly in an &lt;img&gt; without an extra authenticated request.
    /// </summary>
    public string? AvatarDataUrl { get; set; }
}
