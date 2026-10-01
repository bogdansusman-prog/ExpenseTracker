using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Api.Models;

/// <summary>Application user (ASP.NET Core Identity). Every category, transaction and setting belongs to one user.</summary>
public class AppUser : IdentityUser
{
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
