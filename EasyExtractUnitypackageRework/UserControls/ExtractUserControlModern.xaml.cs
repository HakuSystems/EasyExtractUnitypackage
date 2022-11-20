using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EasyExtractUnitypackageRework.Theme.MessageBox;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;

namespace EasyExtractUnitypackageRework.UserControls;

public partial class ExtractUserControlModern : UserControl
{
    public ExtractUserControlModern()
    {
        InitializeComponent();
    }


    private void Search_OnClick(object sender, RoutedEventArgs e)
    {
        var openFile = new OpenFileDialog();
        openFile.Multiselect = true;

        openFile.Filter = "Unitypackages (*.Unitypackage)|*.Unitypackage";
        openFile.ShowDialog();

        var files = openFile.FileNames.Where(file => !string.IsNullOrEmpty(file)).ToArray();

        foreach (var filepath in files)
            if (files.Length > 1)
            {
                if (!filepath.EndsWith(".unitypackage"))
                {
                    new EasyMessageBox("The file you selected is not a unitypackage file.", MessageType.Error,
                        MessageButtons.Ok).ShowDialog();
                    return;
                }
                QueueGrid.Visibility = Visibility.Visible;
                QueueGrid.IsEnabled = true;
                QueueListBox.Items.Add(filepath);
            }
            else
            {
                Extract(filepath);
            }
    }

    private void Extract(string filepath)
    {
        if (!filepath.EndsWith(".unitypackage"))
        {
            new EasyMessageBox("The file you selected is not a unitypackage file.", MessageType.Error,
                MessageButtons.Ok).ShowDialog();
            return;
        }
        progressBar.Visibility = Visibility.Visible;
        progressBar.IsIndeterminate = true;
        var tempFolder = Path.Combine(Config.Config.UseDefaultTempPath
            ? Path.GetTempPath()
            : Directory.GetCurrentDirectory(), $"tmp_{Path.GetFileNameWithoutExtension(filepath)}");
        ExtractionStatus.Content = "Extracting...";
        DisableImportants();

        var targetFolder = $"{filepath}_extracted";
        Config.Config.lastTargetPath = targetFolder;
        Config.Config.UpdateConfig();

        if (Directory.Exists(tempFolder))
            Directory.Delete(tempFolder, true); //we dont want to handle with old temp
        if (Directory.Exists(targetFolder))
            Directory.Delete(targetFolder, true); //program will redo the extraction


        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(targetFolder);

        var myNewThread = new Thread(() => TarGzExtract(filepath, tempFolder));
        myNewThread.Start();

        PlayFinishedExtractedAnimation();
    }

    private void ProcessExtractedContent(string tempFolder, string targetFolder)
    {
        foreach (var d in Directory.EnumerateDirectories(tempFolder))
        {
            var hashPathName = "";
            var targetFullPath = "";
            var targetFullFile = "";
            try
            {
                if (File.Exists(Path.Combine(d, "pathname")))
                {
                    hashPathName = File.ReadAllText(Path.Combine(d, "pathname"));

                    targetFullPath = Path.GetDirectoryName(Path.Combine(targetFolder, hashPathName));
                    targetFullFile = Path.Combine(targetFolder, hashPathName);
                }

                if (File.Exists(Path.Combine(d, "asset")))
                {
                    Directory.CreateDirectory(targetFullPath);
                    File.Move(Path.Combine(d, "asset"), targetFullFile);
                }

                if (File.Exists(Path.Combine(d, "asset.meta")))
                {
                    Directory.CreateDirectory(targetFullPath);
                    File.Move(Path.Combine(d, "asset.meta"), targetFullFile + ".meta");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        Directory.Delete(tempFolder, true);
    }

    [Obsolete("Obsolete")]
    private void TarGzExtract(string filepath, string tempFolder)
    {
        Stream inStream = File.OpenRead(filepath);
        Stream gzipStream = new GZipInputStream(inStream);

        var tarArchive = TarArchive.CreateInputTarArchive(gzipStream);
        tarArchive.ExtractContents(tempFolder);
        tarArchive.Close();

        gzipStream.Close();
        inStream.Close();
        ProcessExtractedContent(tempFolder, $"{filepath}_extracted");
    }


    private void UIElement_OnDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = DragDropEffects.Copy;
    }

    private void UIElement_OnDragLeave(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        DragDropInformation.Text = "Drag and Drop .unitypackage File to extract it.";
        DragDropInformation.Foreground = Brushes.White;
    }

    private void UIElement_OnDrop(object sender, DragEventArgs e)
    {
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);

        foreach (var filepath in files)
            if (files.Length > 1)
            {
                if (!filepath.EndsWith(".unitypackage"))
                {
                    new EasyMessageBox("The file you selected is not a unitypackage file.", MessageType.Error,
                        MessageButtons.Ok).ShowDialog();
                    return;
                }
                QueueGrid.Visibility = Visibility.Visible;
                QueueGrid.IsEnabled = true;
                QueueListBox.Items.Add(filepath);
            }
            else
            {
                Extract(filepath);
            }

        e.Effects = DragDropEffects.None;
        Mouse.OverrideCursor = Cursors.Arrow;
    }

    private void PlayFinishedExtractedAnimation()
    {
        ExtractImage.IsEnabled = false;
        ExtractImage.Visibility = Visibility.Collapsed;
        ExtractMedia.Source = new Uri("LogoAnimation2.mp4", UriKind.Relative);
        ExtractMedia.Visibility = Visibility.Visible;
        ExtractMedia.Play();
    }

    private void MediaElement_OnMediaEnded(object sender, RoutedEventArgs e)
    {
        ((MediaElement)sender).Close();
        ((MediaElement)sender).Source = null;
        ((MediaElement)sender).Visibility = Visibility.Collapsed;
        ExtractImage.Visibility = Visibility.Visible;

        var targetFolder = $"{Config.Config.lastTargetPath}";
        var root = new TreeViewItem();
        root.Header = Path.GetFileName(targetFolder);
        root.IsExpanded = true;

        LoadAssets(targetFolder, root);
        AssetTreeView.Items.Add(root);
    }

    private void LoadAssets(string targetFolder, TreeViewItem root)
    {
        var files = Directory.GetFiles(targetFolder, "*.*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (file.EndsWith(".meta")) continue;
            var path = file.Replace(targetFolder, "Read Instructions on the right side when you're lost here.");
            var pathParts = path.Split('\\');
            var current = root;
            for (var i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];
                if (i == pathParts.Length - 1)
                {
                    var item = new TreeViewItem();
                    item.Header = part;
                    item.MouseDoubleClick += ItemOnMouseDoubleClick;
                    current.Items.Add(item);
                }
                else
                {
                    var item = current.Items.Cast<TreeViewItem>().FirstOrDefault(x => x.Header.ToString() == part);
                    if (item == null)
                    {
                        item = new TreeViewItem();
                        item.Header = part;
                        current.Items.Add(item);
                    }

                    current = item;
                }
            }
        }

        NextStep();
    }

    private void ItemOnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ToExtractContentListBox.Items.Add(((TreeViewItem)sender).Header);
        FilesToExtractTxt.Visibility = Visibility.Visible;
        ExtractSelectedBtn.IsEnabled = true;
        OpenFolderBtn.IsEnabled = false;
        var item = (TreeViewItem)sender;
        var parent = (TreeViewItem)item.Parent;
        parent.Items.Remove(item);
    }

    private void NextStep()
    {
        ToExtractContentListBox.Items.Clear();
        FilesToExtractTxt.Visibility = Visibility.Collapsed;
        NextStepGrid.Visibility = Visibility.Visible;
        ExtractAllBtn.IsEnabled = true;
        ExtractionStatus.Content = "Waiting for Actions...";
        progressBar.IsIndeterminate = true;
        
        TutorialText.Text =
            "Step 2: Select the files you want to extract by DOUBLE clicking on them. files will be displayed in a list below." +
            "when you are done, click on the Extract button to extract the files. after that, you can click on the Open Folder Button" +
            $" to continue. or Press the Continue button to Continue with the Queue. if there is one.";
    }

    private void ExtractAllBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var targetFolder = $"{Config.Config.lastTargetPath}";
        var files = Directory.GetFiles(targetFolder, "*.*", SearchOption.AllDirectories);
        foreach (var file in files)
            if (file.EndsWith(".meta"))
                File.Delete(file);
        ContinueBtn.IsEnabled = true;
        OpenFolderBtn.IsEnabled = true;
        ExtractAllBtn.IsEnabled = false;
        ExtractSelectedBtn.IsEnabled = false;
        ExtractionStatus.Content = "Done!";
        progressBar.Visibility = Visibility.Collapsed;
        AssetTreeView.Items.Clear();
        ToExtractContentListBox.Items.Clear();
        FilesToExtractTxt.Visibility = Visibility.Collapsed;
        ContinueBtn.IsEnabled = true;
        OpenFolderBtn.IsEnabled = true;

        //Config Update
        ConfigTotalExtractedFiles(files.Length);
    }

    private void ConfigTotalExtractedFiles(int files)
    {
        Config.Config.TotalFilesExtracted += files;
        Config.Config.TotalUnitypackgesExtracted++;
        Config.Config.UpdateConfig();
        ((ModernMainWindow)Window.GetWindow(this))!.TotalFilesExLabeltrac.Content =
            Config.Config.TotalFilesExtracted.ToString();
        ((ModernMainWindow)Window.GetWindow(this))!.TotalUnityExLabeltrac.Content =
            Config.Config.TotalUnitypackgesExtracted.ToString();
        WNotification(files);
        
    }

    private void ExtractSelectedBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var configFiles = 0;
        var targetFolder = $"{Config.Config.lastTargetPath}";
        var files = Directory.GetFiles(targetFolder, "*.*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (ToExtractContentListBox.Items.Contains(Path.GetFileName(file)))
            {
                configFiles++;
                continue;
            }

            File.Delete(file);
        }
        
        var folders = Directory.GetDirectories(targetFolder, "*", SearchOption.AllDirectories);
        foreach (var folder in folders)
            if (Directory.GetFiles(folder).Length == 0 && Directory.GetDirectories(folder).Length == 0)
                Directory.Delete(folder);
        
        ContinueBtn.IsEnabled = true;
        OpenFolderBtn.IsEnabled = true;
        ExtractSelectedBtn.IsEnabled = false;
        ExtractAllBtn.IsEnabled = false;
        ExtractionStatus.Content = "Done!";
        progressBar.Visibility = Visibility.Collapsed;
        AssetTreeView.Items.Clear();
        ToExtractContentListBox.Items.Clear();
        FilesToExtractTxt.Visibility = Visibility.Collapsed;

        //Config Update
        ConfigTotalExtractedFiles(configFiles);
    }

    private void OpenFolderBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(Config.Config.lastTargetPath);
    }

    private void ContinueBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ContinueBtn.IsEnabled = false;
        OpenFolderBtn.IsEnabled = false;
        if (QueueListBox.Items.Count == 0)
        {
            QueueGrid.Visibility = Visibility.Collapsed;
            ExtractionToolBox.IsEnabled = true;
            AllowDrop = true;
            NextStepGrid.Visibility = Visibility.Collapsed;
            ExtractionStatus.Content = "";
            progressBar.Visibility = Visibility.Collapsed;
            AssetTreeView.Items.Clear();
            ToExtractContentListBox.Items.Clear();
            FilesToExtractTxt.Visibility = Visibility.Collapsed;
            DragDropInformation.Text = "Drag and Drop .unitypackage File to extract it.";
            DragDropInformation.Foreground = Brushes.White;
            ((ModernMainWindow)Window.GetWindow(this))!.disableCard.IsEnabled = true;
            ((ModernMainWindow)Window.GetWindow(this))!.disableTop.IsEnabled = true;

            return;
        }

        StartQueue();
    }

    private void QueueStartBtn_OnClick(object sender, RoutedEventArgs e)
    {
        StartQueue();
    }

    private void StartQueue()
    {
        var item = QueueListBox.Items[0];
        QueueGrid.Visibility = Visibility.Visible;
        QueueListBox.Items.Remove(item);
        Extract(item.ToString());
        QueueGrid.IsEnabled = false;
    }

    private void DisableImportants()
    {
        AllowDrop = false;
        ExtractionToolBox.IsEnabled = false;
        QueueGrid.IsEnabled = false;
        DragDropInformation.Text = "Drag and Drop Disabled";
        DragDropInformation.Foreground = Brushes.Red;
        ContinueBtn.IsEnabled = false;
        ((ModernMainWindow)Window.GetWindow(this))!.disableCard.IsEnabled = false;
        ((ModernMainWindow)Window.GetWindow(this))!.disableTop.IsEnabled = false;
    }

    private void ExtractUserControlModern_OnLoaded(object sender, RoutedEventArgs e)
    {
        var tempQueue = ((ModernMainWindow)Window.GetWindow(this))!._TempQueue;
        if(tempQueue.Count == 0) return;
        foreach (var item in tempQueue)
            QueueListBox.Items.Add(item);
        StartQueue();
    }

    private void WNotification(int assetCounter) // Windows Notification and data refresh
    {
        if (Config.Config.WindowsNotification)
        {
            new ToastContentBuilder()
                .AddArgument("action", "viewConversation")
                .AddArgument("conversationId", 9813)
                .AddText("EasyExtract has finished extracting...")
                // ReSharper disable once HeapView.BoxingAllocation
                .AddText($"Extracted {assetCounter} files!")
                .Show();
        }
    }
}