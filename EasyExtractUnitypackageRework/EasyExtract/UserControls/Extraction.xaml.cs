using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using EasyExtract.Config;
using EasyExtract.Discord;

namespace EasyExtract.UserControls;

public partial class Extraction : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    public static List<SearchEverythingModel>? _queueList { get; set; }

    public List<SearchEverythingModel>? QueueList
    {
        get => _queueList;
        set
        {
            if (_queueList == value) return;
            _queueList = value;
            OnPropertyChanged();
        }
    }
    
    
    public Extraction()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void Extraction_OnLoaded(object sender, RoutedEventArgs e)
    {
        #region Discord

        var isDiscordEnabled = false;
        try
        {
            isDiscordEnabled = (await ConfigHelper.LoadConfig()).DiscordRpc;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }

        if (isDiscordEnabled)
            try
            {
                await DiscordRpcManager.Instance.UpdatePresenceAsync("Extraction");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }

        #endregion
        
        
    }
}