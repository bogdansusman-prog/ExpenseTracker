using System.Security.Claims;

namespace ExpenseTracker.Api.Services;

public interface ICurrentUser
{
    /// <summary>Id of the logged-in user, or null outside an authenticated request.</summary>
    string? UserId { get; }
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            return principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? principal?.FindFirstValue("sub");
        }
    }
}
