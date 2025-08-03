using Microsoft.Extensions.Logging;
using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Models;

namespace ProseFlow.Application.Services;

/// <summary>
/// Manages CRUD operations for cloud provider configurations by coordinating with the repository.
/// </summary>
public class CloudProviderManagementService(IUnitOfWork unitOfWork, ILogger<CloudProviderManagementService> logger)
{
    /// <summary>
    /// Gets all ordered cloud provider configurations. Decryption is handled by the repository.
    /// </summary>
    public async Task<List<CloudProviderConfiguration>> GetConfigurationsAsync()
    {
        return await unitOfWork.CloudProviderConfigurations.GetAllOrderedAsync();
    }

    /// <summary>
    /// Updates a configuration. Encryption is handled by the repository.
    /// </summary>
    public async Task UpdateConfigurationAsync(CloudProviderConfiguration config)
    {
        var trackedConfig = await unitOfWork.CloudProviderConfigurations.GetByIdAsync(config.Id);
        if (trackedConfig is null) return;
        
        if (trackedConfig is null)
        {
            logger.LogWarning("Provider configuration with ID {ConfigId} not found.", config.Id);
            throw new InvalidOperationException($"Provider configuration with ID {config.Id} not found.");
        }
        
        trackedConfig.Name = config.Name;
        trackedConfig.Model = config.Model;
        trackedConfig.ApiKey = config.ApiKey;
        trackedConfig.Temperature = config.Temperature;
        trackedConfig.IsEnabled = config.IsEnabled;
        trackedConfig.BaseUrl = config.BaseUrl;
        trackedConfig.ProviderType = config.ProviderType;
        
        // The repository will handle encrypting the API key before updating.
        unitOfWork.CloudProviderConfigurations.Update(trackedConfig);
        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a new configuration. Sort order calculation and encryption are handled by the repository.
    /// </summary>
    public async Task CreateConfigurationAsync(CloudProviderConfiguration config)
    {
        // The repository's custom AddAsync method will handle setting the sort order and encrypting the key.
        await unitOfWork.CloudProviderConfigurations.AddAsync(config);
        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a configuration by its ID.
    /// </summary>
    public async Task DeleteConfigurationAsync(int configId)
    {
        var config = await unitOfWork.CloudProviderConfigurations.GetByIdAsync(configId);
        if (config is not null)
        {
            unitOfWork.CloudProviderConfigurations.Delete(config);
            await unitOfWork.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Updates the sort order of all configurations.
    /// </summary>
    public async Task UpdateConfigurationOrderAsync(List<CloudProviderConfiguration> orderedConfigs)
    {
        await unitOfWork.CloudProviderConfigurations.UpdateOrderAsync(orderedConfigs);
        await unitOfWork.SaveChangesAsync();
    }
}