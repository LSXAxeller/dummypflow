using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ProseFlow.UI.ViewModels.Dashboard;

public partial class DashboardViewModel : ViewModelBase
{
    public override string Title => "Dashboard";
    public override string Icon => "\uE038";

    public ObservableCollection<ViewModelBase> TabViewModels { get; } = [];

    public DashboardViewModel(OverviewDashboardView overviewDashboardVm, CloudDashboardViewModel cloudVm, LocalDashboardViewModel localVm)
    {
        TabViewModels.Add(overviewDashboardVm);
        TabViewModels.Add(cloudVm);
        TabViewModels.Add(localVm);
    }
}