using ProseFlow.Core.Interfaces.Repositories;

namespace ProseFlow.Core.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    IActionRepository Actions { get; }
    ICloudProviderConfigurationRepository CloudProviderConfigurations { get; }
    IHistoryRepository History { get; }
    ISettingsRepository Settings { get; }
    IUsageStatisticRepository UsageStatistics { get; }

    /// <summary>
    /// Saves all changes made in this unit of work to the underlying database.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}