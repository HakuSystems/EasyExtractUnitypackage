using Avalonia.Controls;
using EasyExtractCrossPlatform.ViewModels;

namespace EasyExtractCrossPlatform;

public partial class CreditsWindow : Window
{
    public CreditsWindow()
    {
        InitializeComponent();
        ViewModel = new CreditsViewModel();
        DataContext = ViewModel;
    }

    public CreditsViewModel ViewModel { get; }
}