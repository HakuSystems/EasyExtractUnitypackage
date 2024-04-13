using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EasyExtract.Config;
using EasyExtract.Discord;
using XamlAnimatedGif;

namespace EasyExtract.UserControls;

public partial class Extraction : UserControl, INotifyPropertyChanged
{
    //gif time = 5 seconds
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _isExtraction = false;
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

        UpdateQueueHeader();
        ChangeExtractionAnimation();
    }

    private void UpdateQueueHeader()
    {
        if (QueueListView.Items.Count == 0)
        {
            QueueExpander.Header = "Queue (Nothing to extract)";
            ExtractionBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            QueueExpander.Header = QueueListView.Items.Count == 1
                ? $"Queue ({QueueListView.Items.Count} Unitypackage)"
                : $"Queue ({QueueListView.Items.Count} Unitypackage(s))";
            ExtractionBtn.Visibility = Visibility.Visible;
        }
    }

    private void ChangeExtractionAnimation()
    {
        AnimationBehavior.SetSourceUri(ExtractingIcon,
            !_isExtraction
                ? new Uri("pack://application:,,,/EasyExtract;component/Resources/ExtractionProcess/Closed.png") //Idle
                : new Uri("pack://application:,,,/EasyExtract;component/Resources/Gifs/IconAnim.gif")); //Extraction
    }

    private void ExtractingIcon_OnSourceUpdated(object? sender, DataTransferEventArgs e)
    {
        ChangeExtractionAnimation();
    }

    private void ExtractionBtn_OnClick(object sender, RoutedEventArgs e)
    {
        _isExtraction = true;
        ChangeExtractionAnimation();
    }
}