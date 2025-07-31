using Microsoft.EntityFrameworkCore;
using ProseFlow.Application.Events;
using ProseFlow.Core.Models;
using ProseFlow.Infrastructure.Data;
using ProseFlow.Infrastructure.Security;

namespace ProseFlow.Application.Services;

public class SettingsService(IDbContextFactory<AppDbContext> dbFactory, ApiKeyProtector protector)
{
    public async Task<GeneralSettings> GetGeneralSettingsAsync()
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        return await dbContext.GeneralSettings.FindAsync(1)
               ?? throw new InvalidOperationException("General settings not found.");
    }

    public async Task SaveGeneralSettingsAsync(GeneralSettings settings)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        dbContext.GeneralSettings.Update(settings);
        await dbContext.SaveChangesAsync();
    }

    public async Task<ProviderSettings> GetProviderSettingsAsync()
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var settings = await dbContext.ProviderSettings.FindAsync(1)
                       ?? throw new InvalidOperationException("Provider settings not found.");

        // Decrypt the key for display in the UI
        if (!string.IsNullOrWhiteSpace(settings.CloudApiKey))
        {
            try
            {
                settings.CloudApiKey = protector.Unprotect(settings.CloudApiKey);
            }
            catch
            {
                // If unprotection fails (e.g., key corruption), return an empty string
                settings.CloudApiKey = string.Empty;
                AppEvents.RequestNotification("Could not decrypt Cloud API key. Please re-enter it.", NotificationType.Error);
            }
        }
        return settings;
    }

    public async Task SaveProviderSettingsAsync(ProviderSettings settings)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();

        // Encrypt the key before saving
        if (!string.IsNullOrWhiteSpace(settings.CloudApiKey)) 
            settings.CloudApiKey = protector.Protect(settings.CloudApiKey);

        dbContext.ProviderSettings.Update(settings);
        await dbContext.SaveChangesAsync();
    }
}