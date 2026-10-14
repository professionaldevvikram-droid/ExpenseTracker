namespace ExpenseTracker.Api.Common;

public class JwtOptions
{
    public string Issuer { get; set; } = "ExpenseTracker";
    public string Audience { get; set; } = "ExpenseTrackerApp";
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 480;
}