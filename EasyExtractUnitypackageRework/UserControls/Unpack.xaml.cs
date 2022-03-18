using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EasyExtractUnitypackageRework.UserControls
{
    /// <summary>
    /// Interaction logic for Unpack.xaml
    /// </summary>
    public partial class Unpack : UserControl
    {
        private int assetCounter = 0;
        private int fileCounter = 0;

        private string tfiles = "Total Files Extracted: " + Properties.Settings.Default.files;
        private string ufiles = ".unitypackage Files Extracted: " + Properties.Settings.Default.packages;

        public string TFiles
        {
            get { return tfiles; }
            set { tfiles = value; }
        }
        public string UFiles
        {
            get { return ufiles; }
            set { ufiles = value; }
        }
        public Unpack()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UserControl_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DragDropIcon.Kind = (MahApps.Metro.IconPacks.PackIconMaterialKind)MahApps.Metro.IconPacks.PackIconMaterialKind.FileImport;
                Mouse.OverrideCursor = Cursors.AppStarting;
            }
        }

        private void ExtractUnitypackage(string filename)
        {

            Mouse.OverrideCursor = Cursors.Wait;
            var tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tmp_" + System.IO.Path.GetFileNameWithoutExtension(filename));
            var targetFolder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filename), System.IO.Path.GetFileNameWithoutExtension(filename) + "_extracted");

            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
            if (Directory.Exists(targetFolder))
            {
                MessageBox.Show("Folder already exists", "Error");
                Mouse.OverrideCursor = Cursors.Arrow;
                return;
            }
            Directory.CreateDirectory(tempFolder);
            Directory.CreateDirectory(targetFolder);

            ExtractTGZ(filename, tempFolder);
            ProcessExtracted(tempFolder, targetFolder);

            Directory.Delete(tempFolder, true);
            //InfoText.Content = "Completed";
            Mouse.OverrideCursor = Cursors.Arrow;

        }

        public void ExtractTGZ(string gzArchiveName, string destFolder)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Stream inStream = File.OpenRead(gzArchiveName);
            Stream gzipStream = new GZipInputStream(inStream);

            TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream);
            tarArchive.ExtractContents(destFolder);
            tarArchive.Close();

            gzipStream.Close();
            inStream.Close();
        }

        private void ProcessExtracted(string tempFolder, string targetFolder)
        {
            foreach (string d in Directory.EnumerateDirectories(tempFolder))
            {
                string relativePath = "";
                string targetFullPath = "";
                string targetFullFile = "";
                try
                {
                    if (File.Exists(System.IO.Path.Combine(d, "pathname")))
                    {
                        relativePath = File.ReadAllText(System.IO.Path.Combine(d, "pathname"));

                        targetFullPath = System.IO.Path.GetDirectoryName(System.IO.Path.Combine(targetFolder, relativePath));
                        targetFullFile = System.IO.Path.Combine(targetFolder, relativePath);
                    }
                    if (File.Exists(System.IO.Path.Combine(d, "asset")))
                    {
                        Directory.CreateDirectory(targetFullPath);
                        File.Move(System.IO.Path.Combine(d, "asset"), targetFullFile);
                        assetCounter++;
                    }

                    if (File.Exists(System.IO.Path.Combine(d, "asset.meta")))
                    {
                        Directory.CreateDirectory(targetFullPath);
                        File.Move(System.IO.Path.Combine(d, "asset.meta"), targetFullFile + ".meta");
                    }

                    /*
                    if (File.Exists(System.IO.Path.Combine(d, "preview.png")))
                    {

                    }
                    */
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("Error", ex.Message);
                }

            }
        }

        private void UserControl_DragLeave(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            DragDropIcon.Kind = (MahApps.Metro.IconPacks.PackIconMaterialKind)MahApps.Metro.IconPacks.PackIconMaterialKind.FileImportOutline;
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void SearchInsBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Multiselect = true;

            List<string> fList = new List<string>();

            openFile.Filter = "Unitypackages (*.Unitypackage)|*.Unitypackage";
            openFile.ShowDialog();

            foreach (string file in openFile.FileNames)
            {
                if (!string.IsNullOrEmpty(file))
                {
                    fList.Add(file);
                }
            }

            string[] files = fList.ToArray();

            foreach (string file in files)
            {
                if (System.IO.Path.GetExtension(file).Equals(".unitypackage"))
                {

                    fileCounter++;
                    ExtractUnitypackage(file);
                    Mouse.OverrideCursor = Cursors.Arrow;
                }
                else
                {
                    //InfoText.Content = "not an .unitypackage";
                    Mouse.OverrideCursor = Cursors.No;
                }
            }

            //MessageBox.Show(assetCounter + " Files EasyExtracted from " + fileCounter + " packages", "EasyExtractUnitypackage");


            Properties.Settings.Default.files += assetCounter;
            Properties.Settings.Default.packages += fileCounter;
            Properties.Settings.Default.Save();

            WNotification();

            assetCounter = 0;
            fileCounter = 0;

            Mouse.OverrideCursor = Cursors.Arrow;

            tfiles = "Total Files Extracted: " + Properties.Settings.Default.files;
            ufiles = ".unitypackage Files Extracted: " + Properties.Settings.Default.packages;

            tFilesV.Text = tfiles;
            uFilesV.Text = ufiles;
        }

        private void WNotification() // Windows Notification and data refresh
        {
            /*BindingExpression be = tFilesV.GetBindingExpression(TextBox.TextProperty);
            be.UpdateSource();
            be = BindingOperations.GetBindingExpression(uFilesV, TextBox.TextProperty);
            be.UpdateSource();*/

            new ToastContentBuilder()
                .AddArgument("action", "viewConversation")
                .AddArgument("conversationId", 9813)
                .AddText("EasyExtract has finished extracting...")
                .AddText($"Extracted {assetCounter} files, from {fileCounter} packages!")
                .Show();
        }

        private void UserControl_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (string file in files)
            {
                if (System.IO.Path.GetExtension(file).Equals(".unitypackage"))
                {
                    fileCounter++;
                    ExtractUnitypackage(file);
                    Mouse.OverrideCursor = Cursors.Arrow;
                }
                else
                {
                    //InfoText.Content = "not an .unitypackage";
                    Mouse.OverrideCursor = Cursors.No;
                }
            }

            //MessageBox.Show(assetCounter + " Files EasyExtracted from " + fileCounter + " packages" , "EasyExtractUnitypackage");


            Properties.Settings.Default.files += assetCounter;
            Properties.Settings.Default.packages += fileCounter;
            Properties.Settings.Default.Save();


            WNotification();

            tfiles = "Total Files Extracted: " + Properties.Settings.Default.files;
            ufiles = ".unitypackage Files Extracted: " + Properties.Settings.Default.packages;

            tFilesV.Text = tfiles;
            uFilesV.Text = ufiles;

            assetCounter = 0;
            fileCounter = 0;

            e.Effects = DragDropEffects.None;
            DragDropIcon.Kind = (MahApps.Metro.IconPacks.PackIconMaterialKind)MahApps.Metro.IconPacks.PackIconMaterialKind.FileImportOutline;
            Mouse.OverrideCursor = Cursors.Arrow;
        }
    }
}
