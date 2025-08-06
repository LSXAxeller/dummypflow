using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ProseFlow.Application.DTOs.Dashboard;
using ProseFlow.Application.Services;

namespace ProseFlow.UI.ViewModels.Dashboard;

public partial class CloudDashboardViewModel(DashboardService dashboardService) : DashboardTabViewModelBase
{
    public override string Title => "Cloud";

    // KPIs
    [ObservableProperty] private long _totalCloudTokens;
    [ObservableProperty] private double _averageTokensPerAction;
    [ObservableProperty] private int _totalCloudActions;

    // Data Grids
    [ObservableProperty] private ObservableCollection<ActionUsageDto> _topActions = [];
    [ObservableProperty] private ObservableCollection<ProviderPerformanceDto> _providerPerformance = [];

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        var (startDate, endDate) = GetDateRange();

        var cloudHistoryTask = dashboardService.GetHistoryByDateRangeAsync(startDate, endDate, "Cloud");
        var topActionsTask = dashboardService.GetTopActionsAsync(startDate, endDate, "Cloud", 10);
        var performanceTask = dashboardService.GetCloudProviderPerformanceAsync(startDate, endDate);

        await Task.WhenAll(cloudHistoryTask, topActionsTask, performanceTask);

        var cloudHistory = await cloudHistoryTask;

        // Update KPIs
        TotalCloudActions = cloudHistory.Count;
        if (TotalCloudActions > 0)
        {
            TotalCloudTokens = cloudHistory.Sum(e => e.PromptTokens + e.CompletionTokens);
            AverageTokensPerAction = (double)TotalCloudTokens / TotalCloudActions;
        }
        else
        {
            TotalCloudTokens = 0;
            AverageTokensPerAction = 0;
        }

        // Update Data Grids
        TopActions.Clear();
        foreach (var action in await topActionsTask) TopActions.Add(action);

        ProviderPerformance.Clear();
        foreach (var perf in await performanceTask) ProviderPerformance.Add(perf);

        IsLoading = false;
    }
}