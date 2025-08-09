using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProseFlow.Application.Interfaces;
using ProseFlow.Core.Models;

namespace ProseFlow.UI.ViewModels.Downloads;

public partial class AvailableModelViewModel(
    ModelCatalogEntry model,
    IDownloadManager downloadManager) : ViewModelBase
{
    [ObservableProperty] private ModelCatalogEntry _model = model;

    [ObservableProperty] private ModelQuantization _selectedQuantization = model.Quantizations.First();

    [RelayCommand]
    private void StartDownload()
    {
        _ = downloadManager.StartDownloadAsync(Model, SelectedQuantization);
    }
}