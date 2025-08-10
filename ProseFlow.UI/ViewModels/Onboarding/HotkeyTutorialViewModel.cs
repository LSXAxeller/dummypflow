using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ProseFlow.UI.ViewModels.Onboarding;

public partial class HotkeyTutorialViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _instructionText = "Let's try it! Select the text below and press Ctrl + J to see your actions.";

    [ObservableProperty]
    private string _sampleText = "ProseFlow is a grate tool it hlps me writng better";

    [ObservableProperty]
    private bool _showSimulatedMenu;

    [ObservableProperty]
    private bool _isCompleted;

    [RelayCommand]
    private void ShowMenu()
    {
        ShowSimulatedMenu = true;
    }
    
    [RelayCommand]
    private void SimulateFix()
    {
        ShowSimulatedMenu = false;
        SampleText = "✨ ProseFlow is a great tool. It helps me write better. ✨";
        InstructionText = "NICE! That's the magic. You can use this hotkey in any application.";
        IsCompleted = true;
    }
}