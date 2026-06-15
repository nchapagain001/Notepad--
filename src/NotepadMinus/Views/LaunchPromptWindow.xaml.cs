using System.Windows;

namespace NotepadMinus.Views;

public partial class LaunchPromptWindow : Window
{
    public LaunchChoice Choice { get; private set; } = LaunchChoice.StartFresh;

    public LaunchPromptWindow()
    {
        InitializeComponent();
    }

    private void StartFreshButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = LaunchChoice.StartFresh;
        DialogResult = true;
        Close();
    }

    private void OpenYesterdayButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = LaunchChoice.OpenYesterday;
        DialogResult = true;
        Close();
    }
}
