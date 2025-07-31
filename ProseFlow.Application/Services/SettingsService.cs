using Microsoft.EntityFrameworkCore;
using ProseFlow.Core.Models;
using ProseFlow.Infrastructure.Data;

namespace ProseFlow.Application.Services;

/// <summary>
/// Manages loading and saving of global application settings.
/// This service handles the GeneralSettings and the top-level ProviderSettings entities.
/// </summary>
public class SettingsService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<GeneralSettings> GetGeneralSettingsAsync()
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        return await dbContext.GeneralSettings.FindAsync(1)
               ?? throw new InvalidOperationException("General settings not found in the database.");
    }

    public async Task SaveGeneralSettingsAsync(GeneralSettings settings)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        dbContext.GeneralSettings.Update(settings);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Gets the top-level provider settings, which include local model configuration
    /// and the primary/fallback service type choice.
    /// </summary>
    /// <returns>The ProviderSettings entity.</returns>
    public async Task<ProviderSettings> GetProviderSettingsAsync()
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        return await dbContext.ProviderSettings.FindAsync(1)
                       ?? throw new InvalidOperationException("Provider settings not found in the database.");
    }

    /// <summary>
    /// Saves the top-level provider settings.
    /// </summary>
    /// <param name="settings">The ProviderSettings entity to save.</param>
    public async Task SaveProviderSettingsAsync(ProviderSettings settings)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        dbContext.ProviderSettings.Update(settings);
        await dbContext.SaveChangesAsync();
    }
}