using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EasyExtract.Config;
using EasyExtract.Discord;
using XamlAnimatedGif;

namespace EasyExtract.UserControls;

public partial class Extraction : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _isExtraction;

    private static readonly Uri IdleAnimationUri =
        new("pack://application:,,,/EasyExtract;component/Resources/ExtractionProcess/Closed.png");

    private static readonly Uri ExtractionAnimationUri =
        new("pack://application:,,,/EasyExtract;component/Resources/Gifs/IconAnim.gif");

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
        await ChangeDiscordState();


        UpdateQueueHeader();
        ChangeExtractionAnimation();
    }

    private static async Task ChangeDiscordState()
    {
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
    }

    private void UpdateQueueHeader()
    {
        switch (QueueListView.Items.Count)
        {
            case 0:
                QueueExpander.Header = "Queue (Nothing to extract)";
                ExtractionBtn.Visibility = Visibility.Collapsed;
                break;
            default:
                QueueExpander.Header = QueueListView.Items.Count == 1
                    ? $"Queue ({QueueListView.Items.Count} Unitypackage)"
                    : $"Queue ({QueueListView.Items.Count} Unitypackage(s))";
                ExtractionBtn.Visibility = Visibility.Visible;
                break;
        }
    }

    private void ChangeExtractionAnimation()
    {
        AnimationBehavior.SetSourceUri(ExtractingIcon, _isExtraction ? ExtractionAnimationUri : IdleAnimationUri);
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

    private void QueueListView_OnSourceUpdated(object? sender, DataTransferEventArgs e)
    {
        UpdateQueueHeader();
    }
}