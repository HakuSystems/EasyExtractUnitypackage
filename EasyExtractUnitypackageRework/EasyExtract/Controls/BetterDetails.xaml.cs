using System.Text;
using System.Xml.Linq;
using EasyExtract.Config;
using EasyExtract.Services;
using EasyExtract.Utilities.Logger;
using EasyExtract.Views;
using Newtonsoft.Json;

namespace EasyExtract.Controls;

public partial class BetterDetails
{
    private readonly string _extractionPath = ConfigHandler.Instance.Config.DefaultOutputPath;

    public BetterDetails()
    {
        InitializeComponent();
        DataContext = ConfigHandler.Instance.Config;
    }

    private async void BetterDetails_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await DiscordRpcManager.Instance.TryUpdatePresenceAsync("Details");
            Dashboard.Instance.NavigateBackBtn.Visibility = Visibility.Visible;
            await LoadDetailsAsync();
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error loading details", "UI");
        }
    }

    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await LoadDetailsAsync();
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error refreshing details", "UI");
        }
    }

    private async Task LoadDetailsAsync()
    {
        using var scope = new LogScope("LoadDetails", "Details");
        var model = (ConfigModel)DataContext;

        var context = new Dictionary<string, object>();

        model.TotalFolders = await ExtractionHelper.GetTotalFolderCount(_extractionPath);
        model.TotalFilesExtracted = await ExtractionHelper.GetTotalFileCount(_extractionPath);
        model.TotalScripts = await ExtractionHelper.GetTotalScriptCount(_extractionPath);
        model.TotalMaterials = await ExtractionHelper.GetTotalMaterialCount(_extractionPath);
        model.Total3DObjects = await ExtractionHelper.GetTotal3DObjectCount(_extractionPath);
        model.TotalImages = await ExtractionHelper.GetTotalImageCount(_extractionPath);
        model.TotalAudios = await ExtractionHelper.GetTotalAudioCount(_extractionPath);
        model.TotalControllers = await ExtractionHelper.GetTotalControllerCount(_extractionPath);
        model.TotalConfigurations = await ExtractionHelper.GetTotalConfigurationCount(_extractionPath);
        model.TotalAnimations = await ExtractionHelper.GetTotalAnimationCount(_extractionPath);

        model.TotalSizeBytes = await ExtractionHelper.GetTotalSizeInBytesAsync(_extractionPath);
        model.TotalExtracted = model.ExtractedUnitypackages.Count;
        model.LastExtractionTime = DateTime.Now;

        context.Add("TotalFiles", model.TotalFilesExtracted);
        context.Add("TotalSize", model.TotalSizeBytes);

        BetterLogger.LogWithContext("Details loaded successfully", context, LogLevel.Info, "Details");
    }

    private async void ExportJsonButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = new LogScope("ExportJson", "Export");
            var model = (ConfigModel)DataContext;

            var details = new
            {
                model.TotalFolders,
                model.TotalFilesExtracted,
                model.TotalScripts,
                model.TotalMaterials,
                model.Total3DObjects,
                model.TotalImages,
                model.TotalAudios,
                model.TotalControllers,
                model.TotalConfigurations,
                model.TotalAnimations,
                TotalSize = model.TotalSizeBytes,
                LastExtracted = model.LastExtractionTime
            };

            var json = JsonConvert.SerializeObject(details, Formatting.Indented);

            var saveFileDialog = new SaveFileDialog
            {
                FileName = "ExtractedDetails.json",
                Filter = "JSON files (*.json)|*.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(saveFileDialog.FileName, json);
                BetterLogger.LogWithContext("JSON export completed",
                    new Dictionary<string, object> { ["Path"] = saveFileDialog.FileName }, LogLevel.Info, "Export");
            }
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error exporting JSON", "Export");
        }
    }

    private async void ExportXmlButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = new LogScope("ExportXml", "Export");
            var model = (ConfigModel)DataContext;

            var xDoc = new XDocument(
                new XElement("ExtractedDetails",
                    new XElement("Folders", model.TotalFolders),
                    new XElement("Files", model.TotalFilesExtracted),
                    new XElement("Scripts", model.TotalScripts),
                    new XElement("Materials", model.TotalMaterials),
                    new XElement("Objects3D", model.Total3DObjects),
                    new XElement("Images", model.TotalImages),
                    new XElement("Audios", model.TotalAudios),
                    new XElement("Controllers", model.TotalControllers),
                    new XElement("Configurations", model.TotalConfigurations),
                    new XElement("Animations", model.TotalAnimations),
                    new XElement("TotalSizeBytes", model.TotalSizeBytes),
                    new XElement("LastExtracted", model.LastExtractionTime.ToString("o"))
                )
            );

            var saveFileDialog = new SaveFileDialog
            {
                FileName = "ExtractedDetails.xml",
                Filter = "XML files (*.xml)|*.xml"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(saveFileDialog.FileName, xDoc.ToString());
                BetterLogger.LogWithContext("XML export completed",
                    new Dictionary<string, object> { ["Path"] = saveFileDialog.FileName }, LogLevel.Info, "Export");
            }
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error exporting XML", "Export");
        }
    }


    private async void ExportCsvButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = new LogScope("ExportCsv", "Export");
            var model = (ConfigModel)DataContext;

            var csvContent = new StringBuilder();
            csvContent.AppendLine("Category,Count");
            csvContent.AppendLine($"Folders,{model.TotalFolders}");
            csvContent.AppendLine($"Files,{model.TotalFilesExtracted}");
            csvContent.AppendLine($"Scripts,{model.TotalScripts}");
            csvContent.AppendLine($"Materials,{model.TotalMaterials}");
            csvContent.AppendLine($"3D Objects,{model.Total3DObjects}");
            csvContent.AppendLine($"Images,{model.TotalImages}");
            csvContent.AppendLine($"Audios,{model.TotalAudios}");
            csvContent.AppendLine($"Controllers,{model.TotalControllers}");
            csvContent.AppendLine($"Configurations,{model.TotalConfigurations}");
            csvContent.AppendLine($"Animations,{model.TotalAnimations}");
            csvContent.AppendLine($"Total Size,{model.TotalSizeBytes}");
            csvContent.AppendLine($"Last Extracted,{model.LastExtractionTime:G}");

            var saveFileDialog = new SaveFileDialog
            {
                FileName = "ExtractedDetails.csv",
                Filter = "CSV files (*.csv)|*.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(saveFileDialog.FileName, csvContent.ToString());
                BetterLogger.LogWithContext("CSV export completed",
                    new Dictionary<string, object> { ["Path"] = saveFileDialog.FileName }, LogLevel.Info, "Export");
            }
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error exporting CSV", "Export");
        }
    }
}