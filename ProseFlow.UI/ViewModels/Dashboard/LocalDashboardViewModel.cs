using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ProseFlow.Application.DTOs.Dashboard;
using ProseFlow.Application.Services;
using ProseFlow.Infrastructure.Services.AiProviders.Local;

namespace ProseFlow.UI.ViewModels.Dashboard;

public partial class LocalDashboardViewModel : DashboardTabViewModelBase, IDisposable
{
    private readonly DashboardService _dashboardService;
    private readonly LocalModelManagerService _modelManager;

    public override string Title => "Local";

    // KPIs
    [ObservableProperty] private long _totalLocalTokens;
    [ObservableProperty] private double _averageInferenceSpeed;
    [ObservableProperty] private string _ramUsage = "N/A";
    [ObservableProperty] private int _totalLocalActions;
    
    // Data Grid
    [ObservableProperty] private ObservableCollection<ActionUsageDto> _topActions = [];
    
    public LocalDashboardViewModel(DashboardService dashboardService, LocalModelManagerService modelManager)
    {
        _dashboardService = dashboardService;
        _modelManager = modelManager;
        
        _modelManager.StateChanged += OnModelStateChanged;
    }
    
    private void OnModelStateChanged()
    {
        // TODO: Implement real-time hardware monitoring
    }
    
    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        var (startDate, endDate) = GetDateRange();
        
        var localHistoryTask = _dashboardService.GetHistoryByDateRangeAsync(startDate, endDate, "Local");
        var topActionsTask = _dashboardService.GetTopActionsAsync(startDate, endDate, "Local", 10);
        
        await Task.WhenAll(localHistoryTask, topActionsTask);
        
        var localHistory = await localHistoryTask;

        // Update KPIs
        TotalLocalActions = localHistory.Count;
        if (TotalLocalActions > 0)
        {
            TotalLocalTokens = localHistory.Sum(e => e.PromptTokens + e.CompletionTokens);
            
            // Calculate inference speed in tokens per second
            var totalLatencySeconds = localHistory.Sum(e => e.LatencyMs) / 1000.0;
            AverageInferenceSpeed = totalLatencySeconds > 0 ? localHistory.Sum(e => e.CompletionTokens) / totalLatencySeconds : 0;
        }
        else
        {
            TotalLocalTokens = 0;
            AverageInferenceSpeed = 0;
        }
        
        // Update Data Grid
        TopActions.Clear();
        foreach (var action in await topActionsTask) TopActions.Add(action);

        IsLoading = false;
    }

    public void Dispose()
    {
        _modelManager.StateChanged -= OnModelStateChanged;
        GC.SuppressFinalize(this);
    }
}