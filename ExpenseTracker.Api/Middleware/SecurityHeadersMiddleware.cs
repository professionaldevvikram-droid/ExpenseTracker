namespace ExpenseTracker.Api.Middleware;

// Adds browser-side protections to every response
public class SecurityHeadersMiddleware
{
    // Says exactly where the page may load scripts, styles and fonts from.
    // If you add a new CDN or font host later, add it here too.
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' https://cdn.jsdelivr.net; " +
        "style-src 'self' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // The API docs page (Development only) uses its own scripts and styles, so it gets no policy
        var isDocsPage = context.Request.Path.StartsWithSegments("/scalar")
                      || context.Request.Path.StartsWithSegments("/openapi");

        if (!isDocsPage)
            headers["Content-Security-Policy"] = ContentSecurityPolicy;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        return _next(context);
    }
}