using Microsoft.EntityFrameworkCore;
using ProseFlow.Core.Models;
using ProseFlow.Infrastructure.Data;

namespace ProseFlow.Application.Services;

public class HistoryService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task AddHistoryEntryAsync(string actionName, string providerUsed, string input, string output)
    {
        var entry = new HistoryEntry
        {
            Timestamp = DateTime.UtcNow,
            ActionName = actionName,
            ProviderUsed = providerUsed,
            InputText = input,
            OutputText = output
        };

        await using var dbContext = await dbFactory.CreateDbContextAsync();
        dbContext.History.Add(entry);
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<HistoryEntry>> GetHistoryAsync()
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        return await dbContext.History.OrderByDescending(h => h.Timestamp).ToListAsync();
    }

    public async Task ClearHistoryAsync()
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        await dbContext.History.ExecuteDeleteAsync();
    }
}