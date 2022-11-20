using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EasyExtractUnitypackageRework.Methods;
using EasyExtractUnitypackageRework.Theme.MessageBox;

namespace EasyExtractUnitypackageRework.UserControls;

public partial class SearchEverything : UserControl
{
    private const string EndsWithString = ".unitypackage";
    private readonly Dictionary<string, string> _tempList = new();

    public SearchEverything()
    {
        InitializeComponent();
    }

    private void SearchEverything_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _tempList.Clear();
            // ReSharper disable once ComplexConditionExpression
            if (!CheckEverythingRunning() || !CheckOperatingSystem())
            {
                new EasyMessageBox("Your System Doesnt Support This feature or you dont have SearchEverything open.", MessageType.Info, MessageButtons.Ok)
                    .ShowDialog();
                CompletedEverything();
            }

            DoneBtn.Content = "Cancel";
            // set the search
            Everything.Everything_SetSearchW("endwith:.unitypackage");

            // request name and Path
            Everything.Everything_SetRequestFlags(Everything.EVERYTHING_REQUEST_FILE_NAME
                                                  | Everything.EVERYTHING_REQUEST_PATH
            );
        
            // execute the query
            Everything.Everything_QueryW(true);

            var myThread = new Thread(() => { LoopList(); });
            myThread.SetApartmentState(ApartmentState.STA);
            myThread.Start();
        }
        catch (Exception exception)
        {
            new EasyMessageBox(exception.Message, MessageType.Error, MessageButtons.Ok).ShowDialog();
            Clipboard.SetText(exception.ToString());
            throw;
        }
        
    }

    private bool CheckOperatingSystem()
    {
        return Environment.Is64BitOperatingSystem;
    }

    private void OnBtnOnClick(object sender, RoutedEventArgs args)
    {
        if (sender is not Button button) return;
        var path = button.Tag.ToString();
        if (path.EndsWith(EndsWithString))
        {
            ((ModernMainWindow)Window.GetWindow(this))!._TempQueue.Add(path);
            InformationTxt.Text = $"Added {path} to queue";
            DoneBtn.Content = "Start Extraction";
        }
        else
        {
            InformationTxt.Text = "Not a unitypackage, its a folder.";
        }
    }

    private static bool CheckEverythingRunning()
    {
        return Process.GetProcessesByName("Everything").Length > 0;
    }

    private void DoneBtn_OnClick(object sender, RoutedEventArgs e)
    {
        UnitypackagesList.Items.Clear();
        Everything.Everything_Reset();
        CompletedEverything();
    }

    private void CompletedEverything()
    {
        ((ModernMainWindow)Window.GetWindow(this))!.SearchComputerBtn.IsEnabled = true;
        ((ModernMainWindow)Window.GetWindow(this))!.disableTop.IsEnabled = true;
        ((ModernMainWindow)Window.GetWindow(this))!.disableCard.IsEnabled = true;
        ((ModernMainWindow)Window.GetWindow(this))!.Frame.Navigate(
            new Uri("UserControls/ExtractUserControlModern.xaml", UriKind.Relative));
        Config.Config.GoFrame = "UserControls/ExtractUserControlModern.xaml";
        Config.Config.UpdateConfig();
    }


    private void SearchBtn_OnClick(object sender, RoutedEventArgs e)
    {
        UnitypackagesList.Items.Clear();
        if (string.IsNullOrEmpty(SearchBox.Text) || string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            InformationTxt.Text = "Please enter a search term";
            return;
        }
        foreach (var content in _tempList)
        {
            if (content.Value.ToLower().Contains(SearchBox.Text.ToLower()))
            {
                Dispatcher.BeginInvoke(() =>
                {
                    var count = UnitypackagesList.Items.Count + 1; // +1 because we start at 0
                    InformationTxt.Text = $"Found {count.ToString()} / [ {Everything.Everything_GetNumResults().ToString()} ] unitypackages";
                    UnitypackagesList.Items.Refresh();
                    var txt = new TextBlock
                    {
                        Text = content.Value,
                        FontSize = 14,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5),
                        Foreground = Brushes.White
                    };
                    var btn = new Button
                    {
                        Content = "Add to Queue",
                        Tag = content.Key,
                        Margin = new Thickness(5),
                        Foreground = Brushes.White,
                        Style = (Style)FindResource("MaterialDesignFlatButton")
                    };
                    UnitypackagesList.Items.Add(new ListBoxItem
                    {
                        Content = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(5),
                            Children =
                            {
                                txt,
                                btn
                            }
                        },
                        Tag = content.Key
                    });
                    btn.Click += OnBtnOnClick;
                    
                });
            }
            else
            {
                InformationTxt.Text = "No Results Found";
            }
        }
    }

    private void LoopList()
    {
        uint i;
        
        for (i = 0; i < Everything.Everything_GetNumResults(); i++)
        {
            var path = Marshal.PtrToStringUni(Everything.Everything_GetResultFullPathName(i));
            var name = Marshal.PtrToStringUni(Everything.Everything_GetResultFileName(i));
            if (path != null) _tempList.Add(path, name);
        }
    }

}