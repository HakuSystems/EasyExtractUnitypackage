using System.Windows;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace EasyExtract.Services.CustomMessageBox;

public partial class CustomMessageBox : FluentWindow
{
    public CustomMessageBox(string message, string title, MessageBoxButton button)
    {
        InitializeComponent();
        Message.Text = message;
        TitleBar.Title = title;
        switch (button)
        {
            case MessageBoxButton.OK:
                OkBtn.Visibility = Visibility.Visible;
                break;
            case MessageBoxButton.OKCancel:
                OkBtn.Visibility = Visibility.Visible;
                CancelBtn.Visibility = Visibility.Visible;
                break;
            case MessageBoxButton.YesNo:
                YesBtn.Visibility = Visibility.Visible;
                NoBtn.Visibility = Visibility.Visible;
                break;
            case MessageBoxButton.YesNoCancel:
                YesBtn.Visibility = Visibility.Visible;
                NoBtn.Visibility = Visibility.Visible;
                CancelBtn.Visibility = Visibility.Visible;
                break;
        }
    }

    private void OkBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CancelBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void YesBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void NoBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}