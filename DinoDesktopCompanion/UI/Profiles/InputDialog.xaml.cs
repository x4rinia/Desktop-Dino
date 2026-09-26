using System.Windows;

namespace DinoDesktopCompanion.UI.Profiles;

public partial class InputDialog : Window
{
    public string InputText => InputTextBox.Text;

    public InputDialog(string prompt, string title, string defaultText = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputTextBox.Text = defaultText;
        InputTextBox.SelectAll();
        InputTextBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
