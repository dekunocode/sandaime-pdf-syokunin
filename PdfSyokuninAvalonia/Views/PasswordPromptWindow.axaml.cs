using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace PdfSyokunin.Views;

public partial class PasswordPromptWindow : Window
{
    public PasswordPromptWindow() => InitializeComponent();

    public PasswordPromptWindow(string fileName, bool isRetry)
    {
        InitializeComponent();
        this.FindControl<TextBlock>("FileNameText")!.Text = fileName;
        this.FindControl<TextBlock>("ValidationText")!.Text = isRetry ? "パスワードが正しくありません。もう一度入力してください。" : string.Empty;
        Opened += (_, _) => this.FindControl<TextBox>("PasswordInput")!.Focus();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void PasswordInput_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Submit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    private void Open_OnClick(object? sender, RoutedEventArgs e) => Submit();

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void Submit()
    {
        var password = this.FindControl<TextBox>("PasswordInput")!.Text;
        if (string.IsNullOrEmpty(password))
        {
            this.FindControl<TextBlock>("ValidationText")!.Text = "パスワードを入力してください。";
            return;
        }

        Close(password);
    }
}
