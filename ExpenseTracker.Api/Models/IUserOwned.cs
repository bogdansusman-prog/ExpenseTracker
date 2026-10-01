namespace ExpenseTracker.Api.Models;

/// <summary>
/// Marks an entity that belongs to a user. The DbContext filters these entities by the current user
/// automatically (global query filter) and fills in <see cref="UserId"/> when they are created.
/// </summary>
public interface IUserOwned
{
    string? UserId { get; set; }
}
