using EasyExtract.Config;
using EasyExtract.Services;

namespace EasyExtract.Controls;

public partial class BetterDetails : UserControl
{
    private readonly string _extractionPath = ConfigHandler.Instance.Config.LastExtractedPath;

    public BetterDetails()
    {
        InitializeComponent();
        DataContext = ConfigHandler.Instance.Config;
    }

    private async void BetterDetails_OnLoaded(object sender, RoutedEventArgs e)
    {
        await DiscordRpcManager.Instance.TryUpdatePresenceAsync("Details");
        var model = (ConfigModel)DataContext;

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
    }
}