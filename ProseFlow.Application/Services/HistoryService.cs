using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Models;

namespace ProseFlow.Application.Services;

public class HistoryService(IUnitOfWork unitOfWork)
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
        
        await unitOfWork.History.AddAsync(entry);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task<List<HistoryEntry>> GetHistoryAsync()
    {
        return await unitOfWork.History.GetAllOrderedByTimestampAsync();
    }

    public async Task ClearHistoryAsync()
    {
        await unitOfWork.History.ClearAllAsync();
    }
}