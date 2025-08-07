using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Lucide.Avalonia;
using ProseFlow.Application.Services;
using ProseFlow.Core.Models;
using ProseFlow.UI.Services;

namespace ProseFlow.UI.ViewModels.History;

public partial class HistoryViewModel(
    HistoryService historyService,
    IDialogService dialogService) : ViewModelBase
{
    public override string Title => "History";
    public override LucideIconKind Icon => LucideIconKind.History;

    public ObservableCollection<HistoryEntry> HistoryEntries { get; } = [];

    public override async Task OnNavigatedToAsync()
    {
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        HistoryEntries.Clear();
        var entries = await historyService.GetHistoryAsync();
        foreach (var entry in entries)
        {
            HistoryEntries.Add(entry);
        }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        dialogService.ShowConfirmationDialogAsync("Clear History", "Are you sure you want to clear the history?",
            async () =>
            {
                await historyService.ClearHistoryAsync();
                HistoryEntries.Clear();
            });
    }
}