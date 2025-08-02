using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProseFlow.Core.Models;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Action> Actions { get; set; }
    public DbSet<GeneralSettings> GeneralSettings { get; set; }
    public DbSet<ProviderSettings> ProviderSettings { get; set; }
    public DbSet<CloudProviderConfiguration> CloudProviderConfigurations { get; set; }
    public DbSet<HistoryEntry> History { get; set; }
    public DbSet<UsageStatistic> UsageStatistics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure singleton settings tables by seeding them with a default entity.
        modelBuilder.Entity<GeneralSettings>().HasData(new GeneralSettings { Id = 1 });
        // The seed data for ProviderSettings is now simpler.
        modelBuilder.Entity<ProviderSettings>().HasData(new ProviderSettings { Id = 1 });

        // Configure the List<string> to be stored as a JSON string in the database.
        modelBuilder.Entity<Action>()
            .Property(a => a.ApplicationContext)
            .HasConversion(
                // Convert List<string> to a JSON string for storage
                list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
                // Convert JSON string back to List<string> when reading
                jsonString => JsonSerializer.Deserialize<List<string>>(jsonString, (JsonSerializerOptions?)null) ??
                              new List<string>());
        
        // Ensure Year and Month are a unique combination for usage statistics.
        modelBuilder.Entity<UsageStatistic>()
            .HasIndex(u => new { u.Year, u.Month })
            .IsUnique();
    }
}