namespace ExpenseTracker.Api.Common;

// Problems the user can understand and fix. The middleware turns each one into an HTTP status.
public abstract class AppException : Exception
{
    public int StatusCode { get; }

    protected AppException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class NotFoundException : AppException          // 404
{
    public NotFoundException(string message) : base(StatusCodes.Status404NotFound, message) { }
}

public class ConflictException : AppException          // 409
{
    public ConflictException(string message) : base(StatusCodes.Status409Conflict, message) { }
}

public class BadRequestException : AppException        // 400
{
    public BadRequestException(string message) : base(StatusCodes.Status400BadRequest, message) { }
}
public class UnauthorizedException : AppException     // 401
{
    public UnauthorizedException(string message) : base(StatusCodes.Status401Unauthorized, message) { }
}