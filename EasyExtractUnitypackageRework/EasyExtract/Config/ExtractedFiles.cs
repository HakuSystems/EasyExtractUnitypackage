using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace EasyExtract.Config;

public class ExtractedFiles : INotifyPropertyChanged
{
    private string _category = "No Category Available";
    private string _extension = "No Extension Available";
    private DateTime _extractedDate = DateTime.Now;
    private string _fileName = "No Name Available";
    private string _filePath = "No Path Available";
    private bool _isChecked;
    private ImageSource? _previewImage;
    private string _size = "No Size Available";
    private string _symbolIcon = "No Symbol Icon Available";


    public ImageSource? PreviewImage
    {
        get => _previewImage;
        set
        {
            _previewImage = value;
            OnPropertyChanged();
        }
    }

    public string SymbolIconImage
    {
        get => _symbolIcon;
        set
        {
            _symbolIcon = value;
            OnPropertyChanged();
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            OnPropertyChanged();
        }
    }

    public string FileName
    {
        get => _fileName;
        set
        {
            _fileName = value;
            OnPropertyChanged();
        }
    }

    public string FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged();
        }
    }

    public string Category
    {
        get => _category;
        set
        {
            _category = value;
            OnPropertyChanged();
        }
    }

    public string Extension
    {
        get => _extension;
        set
        {
            _extension = value;
            OnPropertyChanged();
        }
    }

    public string Size
    {
        get => _size;
        set
        {
            _size = value;
            OnPropertyChanged();
        }
    }

    public DateTime ExtractedDate
    {
        get => _extractedDate;
        set
        {
            _extractedDate = value;
            OnPropertyChanged();
        }
    }

    private string unityFileMessasge =>
        $"Category: {Category} / File Size: {Size}";


    public string UnityFileMessasge => unityFileMessasge;

    private string unityFileMessasgeTooltip =>
        $"Category: {Category}\nFile Size: {Size}";


    public string UnityFileMessasgeTooltip => unityFileMessasgeTooltip;

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}