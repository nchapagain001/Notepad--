using System.Windows;

namespace NotepadMinus;

public partial class RenameDialog : Window
{
    public string NewName => NameBox.Text;

    public RenameDialog(string current)
    {
        InitializeComponent();
        NameBox.Text = current;
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
