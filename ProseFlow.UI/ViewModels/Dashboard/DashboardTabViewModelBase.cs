using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ProseFlow.UI.ViewModels.Dashboard;

public abstract partial class DashboardTabViewModelBase : ViewModelBase
{
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _selectedDateRange = "Last 7 Days";
    public List<string> DateRanges { get; } = ["Today", "Last 7 Days", "Last 30 Days", "This Month", "All Time"];

    partial void OnSelectedDateRangeChanged(string value) => _ = LoadDataAsync();

    public override async Task OnNavigatedToAsync() => await LoadDataAsync();

    protected abstract Task LoadDataAsync();

    protected (DateTime Start, DateTime End) GetDateRange()
    {
        var now = DateTime.UtcNow;
        return SelectedDateRange switch
        {
            "Today" => (now.Date, now),
            "Last 30 Days" => (now.AddDays(-30), now),
            "This Month" => (new DateTime(now.Year, now.Month, 1), now),
            "All Time" => (DateTime.MinValue, DateTime.MaxValue),
            _ => (now.AddDays(-7), now) // Default to "Last 7 Days"
        };
    }
}