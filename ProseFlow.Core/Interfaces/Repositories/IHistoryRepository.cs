using ProseFlow.Core.Models;

namespace ProseFlow.Core.Interfaces.Repositories;

public interface IHistoryRepository : IRepository<HistoryEntry>
{
    Task<List<HistoryEntry>> GetAllOrderedByTimestampAsync();
    Task ClearAllAsync();
}