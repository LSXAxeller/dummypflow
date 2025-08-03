using Microsoft.EntityFrameworkCore;
using ProseFlow.Core.Interfaces.Repositories;
using ProseFlow.Core.Models;

namespace ProseFlow.Infrastructure.Data.Repositories;

public class HistoryRepository(AppDbContext context) : Repository<HistoryEntry>(context), IHistoryRepository
{
    /// <inheritdoc />
    public async Task<List<HistoryEntry>> GetAllOrderedByTimestampAsync()
    {
        return await Context.History.OrderByDescending(h => h.Timestamp).ToListAsync();
    }

    /// <inheritdoc />
    public async Task ClearAllAsync()
    {
        await Context.History.ExecuteDeleteAsync();
    }
}