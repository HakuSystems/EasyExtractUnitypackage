using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using EasyExtract.Discord;
using EasyExtract.Services.CustomMessageBox;
using Newtonsoft.Json;

namespace EasyExtract.UI.Feedback;

public partial class Feedback : UserControl
{
    public Feedback()
    {
        InitializeComponent();
    }

    private string SenderName
    {
        get => DiscordRpcManager.Instance.client.CurrentUser != null
            ? DiscordRpcManager.Instance.client.CurrentUser.Username
            : "Anonymous";
    }

    private void Feedback_OnLoaded(object sender, RoutedEventArgs e)
    {
        GetDiscordUsername();
        UpdateDiscordPresence();
    }

    private async void UpdateDiscordPresence()
    {
        await DiscordRpcManager.Instance.UpdatePresenceAsync("Feedback");
    }

    private void GetDiscordUsername()
    {
        DiscordNameRequest.Text = DiscordRpcManager.Instance.client.CurrentUser != null
            ? $"Sending Request as {DiscordRpcManager.Instance.client.CurrentUser.Username}"
            : "Sending Request as Anonymous";
    }

    private void SubmitFeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FeedbackTextBox.Text))
        {
            var customMessageBox = new CustomMessageBox("Please enter your feedback.", "Warning",
                MessageBoxButton.OK);
            customMessageBox.ShowDialog();
            return;
        }

        if (FeedbackSelection.SelectedIndex.Equals(-1))
        {
            var customMessageBox = new CustomMessageBox("Please select your satisfaction level.", "Warning",
                MessageBoxButton.OK);
            customMessageBox.ShowDialog();
            return;
        }

        SendFeedback();
        FeedbackTextBox.Text = string.Empty;
        FeedbackSelection.SelectedIndex = -1; // Reset the selection
    }

    private async void SendFeedback()
    {
        var feedbackData = new
        {
            user = SenderName,
            satisfaction = FeedbackSelection.Text,
            feedback = FeedbackTextBox.Text,
            appVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString()
        };
        var url = "https://hooker.zkwolf.com/easy_extractor/feedback";
        var jsonData = JsonConvert.SerializeObject(new
        {
            data = feedbackData
        });
        try
        {
            using HttpClient client = new();
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                var customMessageBox = new CustomMessageBox("Feedback submitted successfully!", "Success",
                    MessageBoxButton.OK);
                customMessageBox.ShowDialog();
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic errorResponse = JsonConvert.DeserializeObject(responseContent);
                string errorMessage = errorResponse.message;
                string errorStatus = errorResponse.status;
                var customMessageBox = new CustomMessageBox(errorMessage, errorStatus, MessageBoxButton.OK);
                customMessageBox.ShowDialog();
            }
        }
        catch (HttpRequestException)
        {
            var customMessageBox =
                new CustomMessageBox("Network error. Please check your internet connection and try again.", "Error",
                    MessageBoxButton.OK);
            customMessageBox.ShowDialog();
        }
        catch (Exception ex)
        {
            var customMessageBox =
                new CustomMessageBox("An unexpected error occurred. Please check the logs for more information.",
                    "Error", MessageBoxButton.OK);
            customMessageBox.ShowDialog();
        }
    }
}