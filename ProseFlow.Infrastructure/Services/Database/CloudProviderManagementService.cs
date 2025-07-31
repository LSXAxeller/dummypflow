using Microsoft.EntityFrameworkCore;
using ProseFlow.Core.Models;
using ProseFlow.Infrastructure.Data;
using ProseFlow.Infrastructure.Security;

namespace ProseFlow.Infrastructure.Services.Database;

/// <summary>
/// Manages CRUD operations for cloud provider configurations.
/// </summary>
public class CloudProviderManagementService(IDbContextFactory<AppDbContext> dbFactory, ApiKeyProtector protector)
{
    public async Task<List<CloudProviderConfiguration>> GetConfigurationsAsync()
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var configs = await dbContext.CloudProviderConfigurations.OrderBy(c => c.SortOrder).ToListAsync();
        
        // Decrypt keys for UI/API usage
        foreach (var config in configs)
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey)) continue;
            try
            {
                config.ApiKey = protector.Unprotect(config.ApiKey);
            }
            catch
            {
                // If decryption fails, treat the key as empty and let the user re-enter it.
                config.ApiKey = string.Empty;
            }
        }
        
        return configs;
    }

    public async Task UpdateConfigurationAsync(CloudProviderConfiguration config)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
            config.ApiKey = protector.Protect(config.ApiKey);

        dbContext.CloudProviderConfigurations.Update(config);
        await dbContext.SaveChangesAsync();
    }

    public async Task CreateConfigurationAsync(CloudProviderConfiguration config)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        
        var maxSortOrder = await dbContext.CloudProviderConfigurations.AnyAsync()
            ? await dbContext.CloudProviderConfigurations.MaxAsync(c => c.SortOrder)
            : 0;
        config.SortOrder = maxSortOrder + 1;

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
            config.ApiKey = protector.Protect(config.ApiKey);
            
        dbContext.CloudProviderConfigurations.Add(config);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteConfigurationAsync(int configId)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var config = await dbContext.CloudProviderConfigurations.FindAsync(configId);
        if (config is not null)
        {
            dbContext.CloudProviderConfigurations.Remove(config);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task UpdateConfigurationOrderAsync(List<CloudProviderConfiguration> orderedConfigs)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        for (var i = 0; i < orderedConfigs.Count; i++)
        {
            var configToUpdate = await dbContext.CloudProviderConfigurations.FindAsync(orderedConfigs[i].Id);
            if (configToUpdate != null) 
                configToUpdate.SortOrder = i;
        }
        await dbContext.SaveChangesAsync();
    }
}