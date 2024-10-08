using EasyExtract.Config;
using EasyExtract.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;

namespace EasyExtract.Controls;

public partial class BetterSettings
{
    private readonly BackgroundManager _backgroundManager = BackgroundManager.Instance;
    private readonly ConfigHelper _configHelper = new();

    public BetterSettings()
    {
        InitializeComponent();
        DataContext = _configHelper.Config;
    }

    private async void BetterSettings_OnLoaded(object sender, RoutedEventArgs e)
    {
        await _configHelper.ReadConfigAsync();
        await ChangeUiToMatchConfigAsync();
        await _configHelper.UpdateConfigAsync();
    }

    private async Task ChangeUiToMatchConfigAsync()
    {
        try
        {
            BorderMenuSwitch.IsChecked = _configHelper.Config.BorderThicknessActive;
            ContextMenuSwitch.IsChecked = _configHelper.Config.ContextMenuToggle;
            SkipIntroLogoAnimationToggleSwitch.IsChecked = _configHelper.Config.IntroLogoAnimation;
            UwUToggleSwitch.IsChecked = _configHelper.Config.UwUModeActive;

            var themes = Enum.GetValues(typeof(ApplicationTheme))
                .Cast<ApplicationTheme>()
                .Where(theme => theme != ApplicationTheme.Unknown)
                .ToList();

            ThemeComboBox.ItemsSource = themes;

            foreach (var theme in themes)
                await BetterLogger.LogAsync($"Available Theme: {theme}", $"{nameof(BetterSettings)}.xaml.cs", Importance.Info);

            if (themes.Contains(_configHelper.Config.ApplicationTheme))
            {
                ThemeComboBox.SelectedItem = _configHelper.Config.ApplicationTheme;
                await BetterLogger.LogAsync($"Set ThemeComboBox.SelectedItem to: {_configHelper.Config.ApplicationTheme}",
                    $"{nameof(BetterSettings)}.xaml.cs", Importance.Info);
            }
            else
            {
                await BetterLogger.LogAsync(
                    $"ThemeComboBox.ItemsSource does not contain {_configHelper.Config.ApplicationTheme}",
                    $"{nameof(BetterSettings)}.xaml.cs", Importance.Warning);
                ThemeComboBox.SelectedItem = ApplicationTheme.Dark; // Default to Dark
                await BetterLogger.LogAsync("Set ThemeComboBox.SelectedItem to default: Dark", $"{nameof(BetterSettings)}.xaml.cs",
                    Importance.Info);
            }

            CheckForUpdatesOnStartUpToggleSwitch.IsChecked = _configHelper.Config.Update.AutoUpdate;
            DiscordRpcToggleSwitch.IsChecked = _configHelper.Config.DiscordRpc;
            DefaultTempPathTextBox.Text = _configHelper.Config.DefaultTempPath;
            BackgroundOpacitySlider.Value = _configHelper.Config.Backgrounds.BackgroundOpacity;
            await BetterLogger.LogAsync("UI updated to match config", $"{nameof(BetterSettings)}.xaml.cs", Importance.Info);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Exception in ChangeUiToMatchConfigAsync: {ex.Message}", $"{nameof(BetterSettings)}.xaml.cs",
                Importance.Error);
        }
    }

    private async void UwUToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        await UwUToggleSwitchCheckUnCheck();
    }

    private async Task UwUToggleSwitchCheckUnCheck()
    {
        _configHelper.Config.UwUModeActive = UwUToggleSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
    }

    private async void UwUToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await UwUToggleSwitchCheckUnCheck();
    }

    private async void BackgroundOpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var currentBackground = _backgroundManager.CurrentBackground;
        _configHelper.Config.Backgrounds.BackgroundOpacity = (float)BackgroundOpacitySlider.Value;
        _configHelper.Config.Backgrounds.BackgroundPath =
            currentBackground.ImageSource
                .ToString(); // Save the current background path
        // since it's not saved in the config when changing opacity.
        _backgroundManager.UpdateOpacity(_configHelper.Config.Backgrounds.BackgroundOpacity);
        await _configHelper.UpdateConfigAsync();
    }

    private async void CheckForUpdatesOnStartUpToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesUpdateToggle();
    }

    private async Task CheckForUpdatesUpdateToggle()
    {
        _configHelper.Config.Update.AutoUpdate = CheckForUpdatesOnStartUpToggleSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
    }

    private async void CheckForUpdatesOnStartUpToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesUpdateToggle();
    }

    private async void DiscordRpcToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.DiscordRpc = DiscordRpcToggleSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
        await DiscordRpcManager.Instance.UpdatePresenceAsync("Settings");
    }

    private async void DiscordRpcToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.DiscordRpc = DiscordRpcToggleSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
        DiscordRpcManager.Instance.Dispose();
    }

    private async void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _configHelper.Config.ApplicationTheme = (ApplicationTheme)ThemeComboBox.SelectedItem;
        ApplicationThemeManager.Apply(_configHelper.Config.ApplicationTheme);
        await _configHelper.UpdateConfigAsync();
    }

    private async void BackgroundChangeButton_OnClick(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.Backgrounds.BackgroundPath = string.Empty;
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Image Files (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png",
            Title = "Select a background image"
        };
        var result = openFileDialog.ShowDialog();
        if (result != true) return;
        _configHelper.Config.Backgrounds.BackgroundPath = openFileDialog.FileName;

        _backgroundManager.UpdateBackground(_configHelper.Config.Backgrounds.BackgroundPath);
        _backgroundManager.UpdateOpacity(_configHelper.Config.Backgrounds.BackgroundOpacity);
        await _configHelper.UpdateConfigAsync();
    }

    private async void BackgroundResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.Backgrounds.BackgroundPath = string.Empty;
        _backgroundManager.ResetBackground();
        await _configHelper.UpdateConfigAsync();
    }

    private async void DefaultTempPathChangeButton_OnClick(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog();
        var result = folderDialog.ShowDialog();
        if (result != true) return;
        DefaultTempPathTextBox.Text = folderDialog.FolderName;

        _configHelper.Config.DefaultTempPath = folderDialog.FolderName;
        await _configHelper.UpdateConfigAsync();
        await BetterLogger.LogAsync($"Default temp path set to: {folderDialog.FolderName}", "Settings.xaml.cs",
            Importance.Info);
    }

    private async void DefaultTempPathResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.DefaultTempPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Temp");
        DefaultTempPathTextBox.Text = _configHelper.Config.DefaultTempPath;
        await _configHelper.UpdateConfigAsync();
        await BetterLogger.LogAsync("Default temp path reset", "Settings.xaml.cs", Importance.Info);
    }

    private async void SkipIntroLogoAnimationToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        await SkipIntroLogoAnimationToggleSwitchCheckUnCheck();
    }

    private async Task SkipIntroLogoAnimationToggleSwitchCheckUnCheck()
    {
        _configHelper.Config.IntroLogoAnimation = SkipIntroLogoAnimationToggleSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
    }

    private async void SkipIntroLogoAnimationToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await SkipIntroLogoAnimationToggleSwitchCheckUnCheck();
    }

    private async void ContextMenuSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        await UpdateContextMenuToggleSettingAsync();
    }

    private async void ContextMenuSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await UpdateContextMenuToggleSettingAsync();
    }

    private async Task UpdateContextMenuToggleSettingAsync()
    {
        _configHelper.Config.ContextMenuToggle = ContextMenuSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
    }

    private async void BorderMenuSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        await UpdateBorderThicknessConfigAsync();
    }

    private async Task UpdateBorderThicknessConfigAsync()
    {
        _configHelper.Config.BorderThicknessActive = BorderMenuSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
    }

    private async void BorderMenuSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await UpdateBorderThicknessConfigAsync();
    }
}