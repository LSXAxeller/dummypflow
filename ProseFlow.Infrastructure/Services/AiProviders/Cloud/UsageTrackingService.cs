using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProseFlow.Core.Models;
using ProseFlow.Infrastructure.Data;

namespace ProseFlow.Infrastructure.Services.AiProviders.Cloud;

/// <summary>
/// Manages reading, writing, and updating token usage statistics from the database.
/// This service is designed to be a singleton.
/// </summary>
public class UsageTrackingService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<UsageTrackingService> logger)
{
    private UsageStatistic _currentMonthUsage = new();

    public async Task InitializeAsync()
    {
        _currentMonthUsage = await GetOrCreateCurrentUsageStatisticAsync();
    }

    public UsageStatistic GetCurrentUsage() => _currentMonthUsage;

    public async Task AddUsageAsync(long promptTokens, long completionTokens)
    {
        var now = DateTime.UtcNow;
        if (now.Year != _currentMonthUsage.Year || now.Month != _currentMonthUsage.Month)
        {
            // It's a new month, get or create the new record
            _currentMonthUsage = await GetOrCreateCurrentUsageStatisticAsync();
        }

        _currentMonthUsage.PromptTokens += promptTokens;
        _currentMonthUsage.CompletionTokens += completionTokens;

        try
        {
            await using var dbContext = await dbFactory.CreateDbContextAsync();
            dbContext.UsageStatistics.Update(_currentMonthUsage);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save usage data to database.");
        }
    }

    public async Task ResetUsageAsync()
    {
        _currentMonthUsage.PromptTokens = 0;
        _currentMonthUsage.CompletionTokens = 0;

        await using var dbContext = await dbFactory.CreateDbContextAsync();
        dbContext.UsageStatistics.Update(_currentMonthUsage);
        await dbContext.SaveChangesAsync();
    }

    private async Task<UsageStatistic> GetOrCreateCurrentUsageStatisticAsync()
    {
        var now = DateTime.UtcNow;
        await using var dbContext = await dbFactory.CreateDbContextAsync();

        var usage = await dbContext.UsageStatistics
            .FirstOrDefaultAsync(u => u.Year == now.Year && u.Month == now.Month);

        if (usage is null)
        {
            logger.LogInformation("No usage record for {Month}/{Year}. Creating a new one.", now.Month, now.Year);
            usage = new UsageStatistic { Year = now.Year, Month = now.Month };
            dbContext.UsageStatistics.Add(usage);
            await dbContext.SaveChangesAsync();
        }

        return usage;
    }
}