using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(u =>
        {
            u.HasIndex(x => x.Email).IsUnique();               // one account per email
        });

        modelBuilder.Entity<Category>(c =>
        {
            c.Property(x => x.MonthlyBudget).HasPrecision(18, 2);

            // The same name is allowed for different users, but not twice for one user
            c.HasIndex(x => new { x.UserId, x.Name }).IsUnique();

            c.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Expense>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);

            // Speeds up "my expenses, newest first" and date filters
            e.HasIndex(x => new { x.UserId, x.Date });

            // A category in use can't be deleted
            e.HasOne(x => x.Category)
             .WithMany()
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}