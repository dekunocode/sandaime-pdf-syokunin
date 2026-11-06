using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace PdfSyokunin.Views;

public enum MessageDialogKind
{
    Error,
    Warning,
    Confirm
}

public partial class MessageDialogWindow : Window
{
    public MessageDialogWindow() => InitializeComponent();

    public MessageDialogWindow(string message, string title, MessageDialogKind kind)
    {
        InitializeComponent();
        Title = title;
        this.FindControl<TextBlock>("TitleText")!.Text = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;
        var isConfirm = kind == MessageDialogKind.Confirm;
        this.FindControl<StackPanel>("ConfirmButtons")!.IsVisible = isConfirm;
        this.FindControl<Button>("OkButton")!.IsVisible = !isConfirm;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Yes_OnClick(object? sender, RoutedEventArgs e) => Close(true);

    private void No_OnClick(object? sender, RoutedEventArgs e) => Close(false);

    private void Ok_OnClick(object? sender, RoutedEventArgs e) => Close(true);
}
