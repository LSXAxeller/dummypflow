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
    public DbSet<HistoryEntry> History { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure singleton settings tables by seeding them with a default entity.
        // This ensures we can always query for the single settings row using Id = 1.
        modelBuilder.Entity<GeneralSettings>().HasData(new GeneralSettings { Id = 1 });
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
    }
}