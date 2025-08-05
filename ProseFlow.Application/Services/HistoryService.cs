using Microsoft.Extensions.DependencyInjection;
using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Models;

namespace ProseFlow.Application.Services;

/// <summary>
/// Manages creating and retrieving history entries.
/// </summary>
public class HistoryService(IServiceScopeFactory scopeFactory)
{
    /// <summary>
    /// Adds a new entry to the history.
    /// </summary>
    public Task AddHistoryEntryAsync(string actionName, string providerUsed, string input, string output)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var entry = new HistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                ActionName = actionName,
                ProviderUsed = providerUsed,
                InputText = input,
                OutputText = output
            };

            await unitOfWork.History.AddAsync(entry);
        });
    }

    /// <summary>
    /// Retrieves all history entries, ordered by the most recent.
    /// </summary>
    public Task<List<HistoryEntry>> GetHistoryAsync()
    {
        return ExecuteQueryAsync(unitOfWork => unitOfWork.History.GetAllOrderedByTimestampAsync());
    }

    /// <summary>
    /// Deletes all entries from the history.
    /// </summary>
    public Task ClearHistoryAsync()
    {
        return ExecuteCommandAsync(unitOfWork => unitOfWork.History.ClearAllAsync());
    }

    #region Private Helpers

    /// <summary>
    /// Creates a UoW scope, executes a command, and saves changes.
    /// </summary>
    private async Task ExecuteCommandAsync(Func<IUnitOfWork, Task> command)
    {
        using var scope = scopeFactory.CreateScope();
        await using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await command(unitOfWork);
        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a UoW scope and executes a query.
    /// </summary>
    private async Task<T> ExecuteQueryAsync<T>(Func<IUnitOfWork, Task<T>> query)
    {
        using var scope = scopeFactory.CreateScope();
        await using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await query(unitOfWork);
    }

    #endregion
}