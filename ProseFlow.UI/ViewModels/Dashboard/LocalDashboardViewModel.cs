using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ProseFlow.Application.DTOs.Dashboard;
using ProseFlow.Application.Services;
using SkiaSharp;


namespace ProseFlow.UI.ViewModels.Dashboard;

public partial class LocalDashboardViewModel(DashboardService dashboardService) : DashboardViewModelBase
{
    public override string Title => "Local";
    
    // KPIs
    [ObservableProperty] private long _totalLocalTokens;
    [ObservableProperty] private int _totalLocalActions;
    
    // TODO: Implement an actual hardware monitoring
    // Live Metrics (placeholders for now)
    [ObservableProperty] private string _vramUsage = "N/A";
    [ObservableProperty] private string _inferenceSpeed = "N/A";

    // Grid
    public ObservableCollection<ActionUsageDto> TopLocalActions { get; } = [];

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        var (startDate, endDate) = GetDateRange();

        var dailyUsageTask = dashboardService.GetDailyUsageAsync(startDate, endDate, "Local");
        var topActionsTask = dashboardService.GetTopActionsAsync(startDate, endDate, "Local");

        await Task.WhenAll(dailyUsageTask, topActionsTask);

        var dailyUsage = await dailyUsageTask;

        // Update KPIs
        TotalLocalTokens = dailyUsage.Sum(d => d.PromptTokens + d.CompletionTokens);
        TotalLocalActions = await dashboardService.GetTotalUsageCountAsync(startDate, endDate, "Local");
        InferenceSpeed = $"{dailyUsage.Average(d => d.TokensPerSecond):F2} T/s";

        // Update Grid
        TopLocalActions.Clear();
        foreach (var action in await topActionsTask)
        {
            TopLocalActions.Add(action);
        }

        // Update Chart
        UpdateUsageChart(dailyUsage);
        
        IsLoading = false;
    }

    private void UpdateUsageChart(System.Collections.Generic.List<DailyUsageDto> dailyUsage)
    {
        Series =
        [
            new ColumnSeries<long>
            {
                Name = "Prompt Tokens",
                Values = dailyUsage.Select(d => d.PromptTokens).ToList(),
                Stroke = null,
                Fill = new SolidColorPaint(SKColor.Parse("#16a34a")), // Green for local
                MaxBarWidth = 40,
                Rx = 4,
                Ry = 4
            },

            new ColumnSeries<long>
            {
                Name = "Completion Tokens",
                Values = dailyUsage.Select(d => d.CompletionTokens).ToList(),
                Stroke = null,
                Fill = new SolidColorPaint(SKColor.Parse("#a8a29e")),
                MaxBarWidth = 40,
                Rx = 4,
                Ry = 4
            }
        ];

        XAxes =
        [
            new Axis
            {
                Labels = dailyUsage.Select(d => d.Date.ToString("MMM d")).ToList(),
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
}