using ExpenseTracker.Api.Common;

namespace ExpenseTracker.Api.Services;

public interface ICurrentUser
{
    int Id { get; }
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    // The id comes from the "sub" claim inside the JWT
    public int Id
    {
        get
        {
            var value = _accessor.HttpContext?.User.FindFirst("sub")?.Value;
            return int.TryParse(value, out var id)
                ? id
                : throw new UnauthorizedException("Please log in.");
        }
    }
}