namespace ExpenseTracker.Api.Common;

// Every new account starts with these categories
public static class DefaultCategories
{
    public static readonly (string Name, string Color)[] All =
    {
        ("Food",          "#f97316"),
        ("Transport",     "#3b82f6"),
        ("Rent",          "#8b5cf6"),
        ("Shopping",      "#ec4899"),
        ("Bills",         "#eab308"),
        ("Entertainment", "#14b8a6"),
        ("Health",        "#22c55e"),
        ("Other",         "#94a3b8")
    };
}