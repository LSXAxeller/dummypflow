using System.Linq;
using Avalonia.Input;
using ShadUI;

namespace ProseFlow.UI.Views.Providers;

public partial class CloudProviderEditorView : Window
{
    public CloudProviderEditorView()
    {
        InitializeComponent();
    }
    
    private void NumericTextBox_OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text?.Any(c => !char.IsDigit(c)) ?? false) e.Handled = true;
    }
}