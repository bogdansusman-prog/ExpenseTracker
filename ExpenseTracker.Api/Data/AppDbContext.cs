using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<FinancialTransaction> Transactions =>
        Set<FinancialTransaction>();

    public DbSet<UserSettings> Settings => Set<UserSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>()
            .HasIndex(category => category.Name)
            .IsUnique();

        modelBuilder.Entity<FinancialTransaction>()
            .HasOne(transaction => transaction.Category)
            .WithMany(category => category.Transactions)
            .HasForeignKey(transaction => transaction.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FinancialTransaction>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_Transactions_RegretScore",
                "\"RegretScore\" IS NULL OR (\"RegretScore\" BETWEEN 1 AND 5)"));

        // Single-row settings table (the app has one user).
        modelBuilder.Entity<UserSettings>()
            .HasData(new UserSettings
            {
                Id = UserSettings.SingletonId,
                Language = "ro"
            });
    }
}