using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<FinancialTransaction> Transactions =>
        Set<FinancialTransaction>();

    public DbSet<UserSettings> Settings => Set<UserSettings>();

    /// <summary>
    /// Id of the logged-in user. Used by the global query filters below, so every query
    /// only ever sees the current user's data (EF re-evaluates it for each context instance).
    /// </summary>
    public string? CurrentUserId => currentUser.UserId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ----- Per-user data isolation -----
        modelBuilder.Entity<Category>()
            .HasQueryFilter(category => category.UserId == CurrentUserId);

        modelBuilder.Entity<FinancialTransaction>()
            .HasQueryFilter(transaction => transaction.UserId == CurrentUserId);

        modelBuilder.Entity<UserSettings>()
            .HasQueryFilter(settings => settings.UserId == CurrentUserId);

        modelBuilder.Entity<Category>()
            .HasOne(category => category.User)
            .WithMany()
            .HasForeignKey(category => category.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FinancialTransaction>()
            .HasOne(transaction => transaction.User)
            .WithMany()
            .HasForeignKey(transaction => transaction.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserSettings>()
            .HasOne(settings => settings.User)
            .WithMany()
            .HasForeignKey(settings => settings.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Category names are unique per user (two users can both have "Food").
        modelBuilder.Entity<Category>()
            .HasIndex(category => new { category.UserId, category.Name })
            .IsUnique();

        modelBuilder.Entity<UserSettings>()
            .HasIndex(settings => settings.UserId)
            .IsUnique();

        modelBuilder.Entity<FinancialTransaction>()
            .HasIndex(transaction => new { transaction.UserId, transaction.Date });

        // ----- Existing configuration -----
        modelBuilder.Entity<FinancialTransaction>()
            .HasOne(transaction => transaction.Category)
            .WithMany(category => category.Transactions)
            .HasForeignKey(transaction => transaction.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FinancialTransaction>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_Transactions_RegretScore",
                "\"RegretScore\" IS NULL OR (\"RegretScore\" BETWEEN 1 AND 5)"));

        // Settings row created before accounts existed; claimed by the first registered user.
        modelBuilder.Entity<UserSettings>()
            .HasData(new UserSettings
            {
                Id = UserSettings.SeedId,
                Language = "ro"
            });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AssignOwner();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AssignOwner();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>New categories / transactions / settings automatically belong to the current user.</summary>
    private void AssignOwner()
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<IUserOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.UserId is null)
            {
                entry.Entity.UserId = userId;
            }
        }
    }
}
