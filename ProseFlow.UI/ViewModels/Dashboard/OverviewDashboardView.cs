using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using ProseFlow.Application.Services;
using ProseFlow.Core.Models;
using ProseFlow.Infrastructure.Services.AiProviders.Local;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.Defaults;

namespace ProseFlow.UI.ViewModels.Dashboard;

public partial class OverviewDashboardView : DashboardTabViewModelBase, IDisposable
{
    private readonly DashboardService _dashboardService;
    private readonly HistoryService _historyService;
    private readonly LocalModelManagerService _modelManager;
    private readonly SettingsService _settingsService;

    public override string Title => "Overview";

    // KPI Properties
    [ObservableProperty] private int _totalActionsExecuted;
    [ObservableProperty] private long _totalCloudTokens;
    [ObservableProperty] private long _totalLocalTokens;
    
    // Local Model Status
    [ObservableProperty] private ModelStatus _localModelStatus;
    [ObservableProperty] private string _localModelName = "N/A";

    // Chart Properties
    [ObservableProperty] private ObservableCollection<ISeries> _usageSeries = [];
    [ObservableProperty] private Axis[] _xAxes = [ new Axis() ];
    [ObservableProperty] private Axis[] _yAxes = [ new Axis() ];

    // List Properties
    [ObservableProperty] private ObservableCollection<HistoryEntry> _recentActivity = [];
    
    public OverviewDashboardView(
        DashboardService dashboardService,
        HistoryService historyService,
        LocalModelManagerService modelManager,
        SettingsService settingsService)
    {
        _dashboardService = dashboardService;
        _historyService = historyService;
        _modelManager = modelManager;
        _settingsService = settingsService;

        _modelManager.StateChanged += OnModelStateChanged;
        OnModelStateChanged();
    }
    
    protected override async Task LoadDataAsync()
    {
        IsLoading = true;

        var (startDate, endDate) = GetDateRange();
        
        // Fetch all data in parallel
        var allHistoryTask = _dashboardService.GetHistoryByDateRangeAsync(startDate, endDate);
        var recentHistoryTask = _historyService.GetRecentHistoryAsync(5);
        var settingsTask = _settingsService.GetProviderSettingsAsync();
        
        await Task.WhenAll(allHistoryTask, recentHistoryTask, settingsTask);
        
        var allHistory = await allHistoryTask;
        var recentHistory = await recentHistoryTask;
        var settings = await settingsTask;
        
        // Update KPIs
        TotalActionsExecuted = allHistory.Count;
        TotalCloudTokens = allHistory.Where(e => e.ProviderUsed == "Cloud").Sum(e => e.PromptTokens + e.CompletionTokens);
        TotalLocalTokens = allHistory.Where(e => e.ProviderUsed == "Local").Sum(e => e.PromptTokens + e.CompletionTokens);
        LocalModelName = string.IsNullOrWhiteSpace(settings.LocalModelPath) ? "N/A" : System.IO.Path.GetFileName(settings.LocalModelPath);
        
        // Update Chart
        UpdateUsageChart(allHistory);
        
        // Update Recent Activity
        RecentActivity.Clear();
        foreach (var entry in recentHistory) RecentActivity.Add(entry);

        IsLoading = false;
    }
    
    private void UpdateUsageChart(System.Collections.Generic.List<HistoryEntry> historyEntries)
    {
        if (!historyEntries.Any())
        {
            UsageSeries.Clear();
            return;
        }

        var entriesByDay = historyEntries
            .GroupBy(e => DateOnly.FromDateTime(e.Timestamp.ToLocalTime()))
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dayLabels = entriesByDay.Keys.Select(d => d.ToString("MMM d")).ToArray();
        
        var observablePoints = historyEntries.Select(entry => new ObservablePoint(
            x: Array.IndexOf(dayLabels, DateOnly.FromDateTime(entry.Timestamp.ToLocalTime()).ToString("MMM d")),
            y: entry.PromptTokens + entry.CompletionTokens
        )).ToList();

        UsageSeries =
        [
            new ColumnSeries<ObservablePoint>
            {
                Values = observablePoints,
                Name = "Total Tokens",
                Fill = new SolidColorPaint(SKColor.Parse("#3b82f6"))
            }
        ];
        
        XAxes =
        [
            new Axis
            {
                Labels = dayLabels,
                LabelsRotation = 0,
                TextSize = 12,
                SeparatorsPaint = new SolidColorPaint(SKColors.Transparent)
            }
        ];
        
        YAxes = 
        [
            new Axis
            {
                TextSize = 12,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray) { StrokeThickness = 0.5f }
            }
        ];
    }
    
    private void OnModelStateChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            LocalModelStatus = _modelManager.Status;
        });
    }

    [RelayCommand(CanExecute = nameof(CanToggleModel))]
    private async Task ToggleLocalModel()
    {
        if (_modelManager.IsLoaded)
        {
            _modelManager.UnloadModel();
        }
        else
        {
            var settings = await _settingsService.GetProviderSettingsAsync();
            await _modelManager.LoadModelAsync(settings);
        }
    }
    private bool CanToggleModel() => LocalModelStatus is not ModelStatus.Loading;

    public void Dispose()
    {
        _modelManager.StateChanged -= OnModelStateChanged;
        GC.SuppressFinalize(this);
    }
}