using System.Windows.Media;

namespace EasyExtract.Config.Models;

public class ExtractedFiles : INotifyPropertyChanged
{
    private readonly string? _category = "No Category Available";
    private readonly string _fileName = "No Name Available";
    private readonly string _filePath = "No Path Available";
    private readonly string? _size = "No Size Available";
    private string _extension = "No Extension Available";
    private DateTime _extractedDate = DateTime.Now;

    private bool _isCodeFile;
    private ImageSource? _previewImage;
    private string _securityWarning = string.Empty;
    private string _symbolIcon = "No Symbol Icon Available";

    public string SecurityWarning
    {
        get => _securityWarning;
        set
        {
            _securityWarning = value;
            OnPropertyChanged();
        }
    }

    public bool IsCodeFile
    {
        get => _isCodeFile;
        set
        {
            _isCodeFile = value;
            OnPropertyChanged();
        }
    }


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


    public string FileName
    {
        get => _fileName;
        init
        {
            _fileName = value;
            OnPropertyChanged();
        }
    }

    public string FilePath
    {
        get => _filePath;
        init
        {
            _filePath = value;
            OnPropertyChanged();
        }
    }

    public string? Category
    {
        get => _category;
        init
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

    public string? Size
    {
        get => _size;
        init
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}