using Avalonia.Controls;
using EasyExtractCrossPlatform.Utilities;
using EasyExtractCrossPlatform.ViewModels;

namespace EasyExtractCrossPlatform;

public partial class HistoryWindow : Window
{
    public HistoryWindow()
    {
        InitializeComponent();
        LinuxUiHelper.ApplyWindowTweaks(this);
    }

    public HistoryWindow(HistoryViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}